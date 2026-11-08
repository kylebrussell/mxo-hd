using System.Collections;
using System.Net;
using System.Net.Sockets;
using hds;

namespace HeadlessClient;

/// <summary>
/// The world UDP session: sequence numbers, acks, encryption, and the two protocols (03 views, 04 RPCs). Holds what the
/// server sent, for HeadlessPlayer to wait on, and counts anything a real client would have tripped over.
/// </summary>
public sealed class WorldSession : IDisposable
{
    private readonly UdpClient udp;
    private readonly IPEndPoint server;
    private readonly CancellationTokenSource stop = new();
    private readonly Task receiveLoop;
    private readonly Task keepAliveLoop;

    // The server's WorldEncryption keeps an IV as state, shared by encrypt and decrypt. Here each direction has its own.
    private readonly WorldEncryption sendCypher = new();
    private readonly WorldEncryption receiveCypher = new();

    private readonly object sendLock = new();
    private ushort ownSeq;
    private ushort lastServerSeq;
    private byte pss;
    private ushort rpcCounter;
    private byte[]? lastRpcPlain;
    private int? expectedServerRpc;
    private bool resetAnswered;
    private bool disposed;

    private readonly object logLock = new();
    private readonly List<RpcRecord> rpcLog = new();
    private readonly List<byte[]> viewLog = new();
    private readonly List<ViewCreation> creations = new();
    private readonly List<string> problems = new();
    private int rawPackets;
    private long version;

    public WorldSession(string host, int port)
    {
        server = new IPEndPoint(IPAddress.Parse(host), port);
        udp = new UdpClient(new IPEndPoint(IPAddress.Parse(host), 0));
        receiveLoop = Task.Run(ReceiveLoopAsync);
        keepAliveLoop = Task.Run(KeepAliveLoopAsync);
    }

    /// <summary>Things a real client would have trouble with: corrupt packets, RPC counter gaps, unreadable records.</summary>
    public IReadOnlyList<string> Problems
    {
        get { lock (logLock) { return problems.ToList(); } }
    }

    public int RawPackets
    {
        get { lock (logLock) { return rawPackets; } }
    }

    /// <summary>Goes up when anything arrives. Waiters poll against this.</summary>
    public long Version => Interlocked.Read(ref version);

    public List<RpcRecord> RpcSnapshot()
    {
        lock (logLock) { return rpcLog.ToList(); }
    }

    public List<byte[]> ViewSnapshot()
    {
        lock (logLock) { return viewLog.ToList(); }
    }

    public List<ViewCreation> CreationSnapshot()
    {
        lock (logLock) { return creations.ToList(); }
    }

    /// <summary>
    /// The first, unencrypted packet: 00, 10 bytes the server doesn't read, the character id. The server answers with
    /// raw packets and starts the session. The 10 bytes are zeros here; what retail put there is not known.
    /// </summary>
    public void SendSessionRequest(uint charId)
    {
        byte[] packet = new byte[16];
        BitConverter.GetBytes(charId).CopyTo(packet, 11);
        udp.Send(packet, packet.Length, server);
    }

    /// <summary>Sends RPC records as one list under the next counter value.</summary>
    public void SendRpcs(params byte[][] records)
    {
        lock (sendLock)
        {
            byte[] plain = WireFormat.BuildRpcPacket(rpcCounter, records);
            rpcCounter += (ushort)records.Length;
            lastRpcPlain = plain;
            SendEncrypted(plain);
        }
    }

    /// <summary>Sends the last RPC list again with the same counter and bytes under a new packet sequence: what the real client does when an ack is late.</summary>
    public void ResendLastRpcs()
    {
        lock (sendLock)
        {
            if (lastRpcPlain == null)
            {
                throw new InvalidOperationException("No RPC list has been sent yet");
            }
            SendEncrypted(lastRpcPlain);
        }
    }

    /// <summary>Sends a client 03 section (the player's own view).</summary>
    public void SendViewList(byte[] viewList)
    {
        lock (sendLock)
        {
            SendEncrypted(WireFormat.BuildViewPacket(viewList));
        }
    }

    public void SendAck()
    {
        lock (sendLock)
        {
            SendEncrypted(new byte[] { 0x02 });
        }
    }

    private void SendEncrypted(byte[] plain)
    {
        // Sequence word: own sequence in the low 12 bits, the last server sequence seen in the next 12. The server's
        // encrypt() parameter names are from its side: there, cseq is the peer's number and sseq its own.
        ownSeq = (ushort)((ownSeq + 1) & 0xfff);
        byte[] packet = sendCypher.encrypt(plain, plain.Length, pss, lastServerSeq, ownSeq);
        udp.Send(packet, packet.Length, server);
    }

    private async Task ReceiveLoopAsync()
    {
        while (!stop.IsCancellationRequested)
        {
            UdpReceiveResult received;
            try
            {
                received = await udp.ReceiveAsync(stop.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (SocketException)
            {
                // Windows reports ICMP port-unreachable here if the server has gone: keep trying until disposed
                continue;
            }

            try
            {
                Handle(received.Buffer);
            }
            catch (Exception ex)
            {
                AddProblem("Unreadable packet: " + ex.Message);
            }
            Interlocked.Increment(ref version);
        }
    }

    private void Handle(byte[] packet)
    {
        if (packet.Length == 0)
        {
            return;
        }
        if (packet[0] == 0x00)
        {
            lock (logLock)
            {
                rawPackets++;
            }
            return;
        }

        ArrayList? decrypted = receiveCypher.decrypt(packet, packet.Length);
        if (decrypted == null)
        {
            AddProblem("Corrupt packet from the server (CRC or length), " + packet.Length + " bytes");
            return;
        }
        byte[] plain = (byte[])decrypted[0]!;
        byte packetPss = (byte)(ushort)decrypted[1]!;
        ushort serverSeq = (ushort)decrypted[2]!;

        WorldPayload payload = WireFormat.ParsePayload(plain);
        lock (sendLock)
        {
            lastServerSeq = serverSeq;
            pss = packetPss;
        }

        lock (logLock)
        {
            if (payload.ViewSection != null)
            {
                viewLog.Add(payload.ViewSection);
                creations.AddRange(WireFormat.FindCreations(payload.ViewSection));
            }
            foreach ((ushort counter, int count) in payload.RpcLists)
            {
                if (expectedServerRpc.HasValue && expectedServerRpc.Value != counter)
                {
                    problems.Add($"Server RPC counter jumped: expected {expectedServerRpc.Value}, got {counter}");
                }
                expectedServerRpc = (ushort)(counter + count);
            }
            rpcLog.AddRange(payload.Rpcs);
        }

        if (payload.ResetRequested)
        {
            // It stays on the server's packets until our answer arrives: answer once
            if (!resetAnswered)
            {
                resetAnswered = true;
                HandleReset();
            }
            return;
        }
        resetAnswered = false;
        if (!payload.IsAckOnly)
        {
            // Bare acks aren't acked: the server answers an ack-only packet with another, so that would never end.
            SendAck();
        }
    }

    /// <summary>
    /// 0x40 from the server: reset. The client says it is done with 0x40 on its own packet, and both sides start their
    /// numbers again (MultiProtocolManager CLIENTFLAGS_RESETDONE).
    /// </summary>
    private void HandleReset()
    {
        lock (sendLock)
        {
            lastServerSeq = 0;
            pss = 0;
            SendEncrypted(new byte[] { 0x42 });
            ownSeq = 0;
            rpcCounter = 0;
            lastRpcPlain = null;
        }
        lock (logLock)
        {
            expectedServerRpc = null;
        }
    }

    private async Task KeepAliveLoopAsync()
    {
        try
        {
            while (!stop.IsCancellationRequested)
            {
                // WorldClient.Alive drops a client after 30 seconds of silence
                await Task.Delay(TimeSpan.FromSeconds(5), stop.Token);
                SendAck();
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private void AddProblem(string problem)
    {
        lock (logLock)
        {
            problems.Add(problem);
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }
        disposed = true;
        stop.Cancel();
        udp.Dispose();
        try
        {
            Task.WaitAll(new[] { receiveLoop, keepAliveLoop }, TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
        }
        stop.Dispose();
    }
}
