using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using hds;

namespace hds.Tests;

public class MarginSessionLifecycleTests
{
    private static (TcpClient ServerSide, TcpClient ClientSide) OpenLoopbackPair()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var client = new TcpClient();
            var acceptTask = listener.AcceptTcpClientAsync();
            client.Connect(IPAddress.Loopback, port);
            TcpClient serverSide = acceptTask.GetAwaiter().GetResult();
            return (serverSide, client);
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public void DisconnectedMarginClientUnregistersAndDoesNotLockOutCharacter()
    {
        var margin = new MarginServer();
        margin.SetActiveWorldCharacterCheckForTesting(_ => false);
        var (serverSide, clientSide) = OpenLoopbackPair();
        using (clientSide)
        {
            var first = new MarginClient(101);
            var thread = new Thread(first.HandleClientComm) { IsBackground = true };
            first.marginThreadId = thread.ManagedThreadId;
            margin.RegisterClient(first, thread);
            Assert.True(margin.TryClaimCharacter(first, 4242));
            Assert.True(margin.IsAnotherClientActive(4242));
            thread.Start(serverSide);

            // Simulate TCP drop before World entry (CleanDeadPlayers never runs).
            clientSide.Close();
            Assert.True(thread.Join(TimeSpan.FromSeconds(5)));

            Assert.False(first.isWorking());
            Assert.Equal(0, margin.ActiveClientCount);
            Assert.False(margin.IsAnotherClientActive(4242));

            // Relogging immediately must succeed without "character already in use".
            var second = new MarginClient(102);
            margin.RegisterClient(second);
            Assert.True(margin.TryClaimCharacter(second, 4242));
            Assert.True(margin.IsAnotherClientActive(4242));
        }
    }

    [Fact]
    public void InWorldSessionStillReservesCharacterAfterMarginTcpCloseUntilWorldReap()
    {
        bool worldHoldsCharacter = true;
        var margin = new MarginServer();
        margin.SetActiveWorldCharacterCheckForTesting(id => id == 6060 && worldHoldsCharacter);

        var (serverSide, clientSide) = OpenLoopbackPair();
        using (clientSide)
        {
            var first = new MarginClient(111);
            var thread = new Thread(first.HandleClientComm) { IsBackground = true };
            first.marginThreadId = thread.ManagedThreadId;
            margin.RegisterClient(first, thread);
            // Initially allow the first login before World entry, then mark World active.
            worldHoldsCharacter = false;
            Assert.True(margin.TryClaimCharacter(first, 6060));
            worldHoldsCharacter = true;
            thread.Start(serverSide);

            // Plain TCP close without 80 fe 00: Margin unregisters the dead socket/thread,
            // but the character remains reserved until WorldServer reaps the idle WorldClient.
            clientSide.Close();
            Assert.True(thread.Join(TimeSpan.FromSeconds(5)));
            Assert.Equal(0, margin.ActiveClientCount);
            Assert.True(margin.IsAnotherClientActive(6060));

            var prematureRelog = new MarginClient(112);
            margin.RegisterClient(prematureRelog);
            Assert.False(margin.TryClaimCharacter(prematureRelog, 6060));

            // Once World idle reap clears WorldServer.Clients, relog succeeds immediately.
            worldHoldsCharacter = false;
            Assert.False(margin.IsAnotherClientActive(6060));
            Assert.True(margin.TryClaimCharacter(prematureRelog, 6060));
        }
    }

    [Fact]
    public async Task ConcurrentTryClaimCharacterAllowsExactlyOneWinner()
    {
        var margin = new MarginServer();
        margin.SetActiveWorldCharacterCheckForTesting(_ => false);
        var clients = Enumerable.Range(1, 16).Select(i => new MarginClient(i)).ToArray();
        foreach (var c in clients) margin.RegisterClient(c);

        using var gate = new ManualResetEventSlim(false);
        int winners = 0;
        Task[] racers = clients.Select(c => Task.Run(() =>
        {
            gate.Wait();
            if (margin.TryClaimCharacter(c, 7777))
            {
                Interlocked.Increment(ref winners);
            }
        })).ToArray();

        gate.Set();
        await Task.WhenAll(racers);

        Assert.Equal(1, winners);
        Assert.Equal(1, clients.Count(c => c.getCharID() == 7777));
    }

    [Fact]
    public void RemoveClientsByCharIdEvictsAllMatchesAndUnblocksBlockedTcpRead()
    {
        var margin = new MarginServer();
        var (serverSide, clientSide) = OpenLoopbackPair();
        using (clientSide)
        {
            var live = new MarginClient(201);
            var thread = new Thread(live.HandleClientComm) { IsBackground = true };
            live.marginThreadId = thread.ManagedThreadId;
            margin.RegisterClient(live, thread);
            Assert.True(margin.TryClaimCharacter(live, 5151));
            thread.Start(serverSide);

            // Also register a second entry with the same charId to ensure all matches are evicted.
            var duplicate = new MarginClient(202);
            duplicate.SetClaimedCharId(5151);
            margin.RegisterClient(duplicate);
            Assert.Equal(2, margin.ActiveClientCount);

            // Wait briefly so the worker enters blocking clientStream.Read().
            Thread.Sleep(50);

            // RemoveClientsByCharId must forceClose the socket so the blocked Read unblocks immediately.
            margin.RemoveClientsByCharId(5151);
            Assert.True(thread.Join(TimeSpan.FromSeconds(5)));
            Assert.Equal(0, margin.ActiveClientCount);
            Assert.False(live.isWorking());
            Assert.False(duplicate.isWorking());
        }
    }

    [Fact]
    public void UnauthenticatedSilentClientTimesOutAndUnregisters()
    {
        var margin = new MarginServer();
        var (serverSide, clientSide) = OpenLoopbackPair();
        using (clientSide)
        {
            serverSide.ReceiveTimeout = 200;
            var silent = new MarginClient(301);
            var thread = new Thread(silent.HandleClientComm) { IsBackground = true };
            silent.marginThreadId = thread.ManagedThreadId;
            margin.RegisterClient(silent, thread);
            thread.Start(serverSide);

            Assert.True(thread.Join(TimeSpan.FromSeconds(5)));
            Assert.False(silent.isWorking());
            Assert.Equal(0, margin.ActiveClientCount);
        }
    }

    [Fact]
    public async Task MarginEncryptionGuardsMalformedFramesAndSurvivesConcurrentEncryptDecrypt()
    {
        var enc = MarginEncryption.ForNewSession();
        byte[] key = enc.getTFKey();
        var peer = new MarginEncryption();
        peer.SetSessionKey(key);

        Assert.Null(enc.decryptMargin(null!));
        Assert.Null(enc.decryptMargin(Array.Empty<byte>()));
        Assert.Null(enc.decryptMargin(new byte[16]));
        Assert.Null(enc.decryptMargin(new byte[17]));
        Assert.Null(enc.decryptMargin(new byte[32]));
        Assert.Null(enc.decryptMargin(new byte[34]));

        // Short frames to packetHandler must close cleanly without IndexOutOfRangeException.
        var mc = new MarginClient(401);
        mc.packetHandler(new byte[] { 0x01, 0x02 }, null!);
        Assert.False(mc.isWorking());

        // Concurrent encrypt (World UDP thread) and decryptMargin (Margin TCP thread) on shared MarginEncryption.
        byte[] samplePayload = { 0x11, 0x00, 0x00, 0x00, 0x00, 0x01 };
        byte[] sampleWire = peer.encrypt(samplePayload);
        byte[] inboundFrame = new byte[1 + sampleWire.Length];
        inboundFrame[0] = (byte)sampleWire.Length;
        Buffer.BlockCopy(sampleWire, 0, inboundFrame, 1, sampleWire.Length);

        using var stop = new CancellationTokenSource();
        int encryptErrors = 0;
        Task writer = Task.Run(() =>
        {
            while (!stop.IsCancellationRequested)
            {
                try
                {
                    byte[] encrypted = enc.encrypt(samplePayload);
                    if (encrypted == null || encrypted.Length < 32 || ((encrypted.Length - 16) & 15) != 0)
                    {
                        Interlocked.Increment(ref encryptErrors);
                    }
                }
                catch
                {
                    Interlocked.Increment(ref encryptErrors);
                }
            }
        });

        for (int i = 0; i < 400; i++)
        {
            byte[]? dec = enc.decryptMargin(inboundFrame);
            Assert.NotNull(dec);
            Assert.Equal(samplePayload, dec!.Take(samplePayload.Length).ToArray());
        }

        stop.Cancel();
        await writer;
        Assert.Equal(0, encryptErrors);
    }
}
