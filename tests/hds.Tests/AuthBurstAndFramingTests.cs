using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using hds.auth;
using HeadlessClient;
using HDS.Network;

namespace hds.Tests;

public class AuthBurstAndFramingTests
{
    // ---------- AuthFrameBuffer ----------

    [Fact]
    public void Frame_buffer_reassembles_fragmented_short_and_long_frames_with_nonzero_offset()
    {
        byte[] shortFrame = AuthHandler.FrameAuthMessage(new byte[] { (byte)AuthOpcode.AS_GetPublicKeyRequest, 0x00, 0x00 });
        byte[] longBody = Enumerable.Range(0, 170).Select(i => (byte)(i & 0xFF)).ToArray();
        longBody[0] = (byte)AuthOpcode.AS_AuthRequest;
        byte[] longFrame = AuthHandler.FrameAuthMessage(longBody);

        // Concatenate with empty 0x00 separator frames and feed 1 byte at a time from a non-zero offset.
        byte[] wire = new byte[] { 0x00 }
            .Concat(shortFrame)
            .Concat(new byte[] { 0x00, 0x00 })
            .Concat(longFrame)
            .ToArray();

        var buffer = new AuthFrameBuffer();
        var extracted = new List<byte[]>();
        byte[] scratch = new byte[16];

        for (int i = 0; i < wire.Length; i++)
        {
            scratch[7] = wire[i];
            buffer.Append(scratch, offset: 7, size: 1, extracted);
        }

        Assert.Equal(0, buffer.BufferedBytes);
        Assert.Equal(2, extracted.Count);
        Assert.Equal(shortFrame, extracted[0]);
        Assert.Equal(longFrame, extracted[1]);
    }

    [Fact]
    public void Frame_buffer_splits_coalesced_frames_and_rejects_oversized_frames()
    {
        byte[] f1 = AuthHandler.FrameAuthMessage(new byte[] { 0x06, 0x00, 0x00 });
        byte[] f2 = AuthHandler.FrameAuthMessage(new byte[] { 0x06, 0x01, 0x02 });
        byte[] combined = f1.Concat(f2).ToArray();

        var buffer = new AuthFrameBuffer();
        var extracted = new List<byte[]>();
        buffer.Append(combined, 0, combined.Length, extracted);

        Assert.Equal(2, extracted.Count);
        Assert.Equal(f1, extracted[0]);
        Assert.Equal(f2, extracted[1]);

        // Frame declaring length 5000 (> MaxFrameLength = 4096) must be rejected immediately.
        byte[] oversizedHeader = { (byte)(0x80 | (5000 >> 8)), (byte)(5000 & 0xFF), 0x08 };
        Assert.Throws<InvalidDataException>(() => buffer.Append(oversizedHeader, 0, oversizedHeader.Length, extracted));
    }

    // ---------- MxoRSA cached parameters ----------

    [Fact]
    public void Cached_rsa_decrypt_and_sign_are_thread_safe_and_defensive()
    {
        byte[] mod1 = MxoRSA.ServerPublicModulus();
        mod1[0] ^= 0xFF;
        byte[] mod2 = MxoRSA.ServerPublicModulus();
        Assert.NotEqual(mod1[0], mod2[0]);

        using RSA pub = RSA.Create();
        pub.ImportParameters(new RSAParameters { Modulus = mod2, Exponent = new[] { MxoRSA.ServerPublicExponent() } });
        byte[] plain = Encoding.ASCII.GetBytes("0123456789abcdef0123456789abcdef");
        byte[] cipher = pub.Encrypt(plain, RSAEncryptionPadding.OaepSHA1);

        Parallel.For(0, 64, i =>
        {
            var rsa = new MxoRSA();
            Assert.Equal(plain, rsa.decryptWithPrivkey(cipher));
            byte[] digest = MD5.HashData(Encoding.ASCII.GetBytes("ticket-" + i));
            byte[] sig = rsa.signWithMD5(digest);
            Assert.True(MxoRSA.VerifyWithServerKey(digest, sig));
        });
    }

    // ---------- AuthWorkQueue ----------

    [Fact]
    public void Work_queue_bounds_capacity_runs_off_caller_thread_and_recovers_from_exceptions()
    {
        using var queue = new AuthWorkQueue(workerCount: 1, maxQueueDepth: 1, namePrefix: "TestAuthQueue");
        using var started = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        int callerThreadId = Environment.CurrentManagedThreadId;
        int workerThreadId = -1;

        Assert.True(queue.TryEnqueue(() =>
        {
            workerThreadId = Environment.CurrentManagedThreadId;
            started.Set();
            release.Wait(TimeSpan.FromSeconds(5));
        }));
        Assert.True(started.Wait(TimeSpan.FromSeconds(5)));

        // One item fits in maxQueueDepth=1; the next must be rejected without blocking.
        using var secondDone = new ManualResetEventSlim();
        Assert.True(queue.TryEnqueue(() =>
        {
            throw new InvalidOperationException("intentional worker exception");
        }));
        Assert.False(queue.TryEnqueue(() => { }));

        release.Set();

        // Wait briefly for the throwing item to clear, then ensure the worker still processes subsequent items.
        bool enqueuedAfter = SpinWait.SpinUntil(() => queue.TryEnqueue(() => secondDone.Set()), TimeSpan.FromSeconds(5));
        Assert.True(enqueuedAfter);
        Assert.True(secondDone.Wait(TimeSpan.FromSeconds(5)));
        Assert.NotEqual(callerThreadId, workerThreadId);
    }

    // ---------- Live AuthServer TCP framing + overload busy reply ----------

    private static int GetFreeTcpPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    [Fact]
    public async Task Auth_server_handles_fragmented_and_coalesced_handshake_over_tcp()
    {
        int port = GetFreeTcpPort();
        using var server = new AuthServer(IPAddress.Loopback, port, workerCount: 2, maxQueueDepth: 16);
        Assert.True(server.Start());
        try
        {
            using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var tcp = new TcpClient();
            await tcp.ConnectAsync(IPAddress.Loopback, port, cancel.Token);
            NetworkStream stream = tcp.GetStream();
            var reader = new FramedReader(stream);

            // Fragmented 1-byte-at-a-time AS_GetPublicKeyRequest.
            byte[] getKey = FramedReader.Frame(new byte[] { (byte)AuthOpcode.AS_GetPublicKeyRequest, 0, 0 });
            for (int i = 0; i < getKey.Length; i++)
            {
                await stream.WriteAsync(getKey.AsMemory(i, 1), cancel.Token);
                await stream.FlushAsync(cancel.Token);
            }

            byte[] keyReply = await reader.ReadFrameAsync(cancel.Token);
            Assert.Equal((byte)AuthOpcode.AS_GetPublicKeyReply, keyReply[0]);

            // Build a valid AS_AuthRequest and coalesce a second AS_GetPublicKeyRequest right before it in one WriteAsync.
            byte[] blob = new byte[29 + "tester".Length + 1];
            RandomNumberGenerator.Fill(blob.AsSpan(7, 16));
            Encoding.ASCII.GetBytes("tester").CopyTo(blob, 29);
            using RSA serverPub = RSA.Create(new RSAParameters
            {
                Modulus = MxoRSA.ServerPublicModulus(),
                Exponent = new[] { MxoRSA.ServerPublicExponent() },
            });
            byte[] encryptedBlob = serverPub.Encrypt(blob, RSAEncryptionPadding.OaepSHA1);
            byte[] authReqBody = new byte[42 + encryptedBlob.Length];
            authReqBody[0] = (byte)AuthOpcode.AS_AuthRequest;
            BitConverter.GetBytes(4u).CopyTo(authReqBody, 1);
            encryptedBlob.CopyTo(authReqBody, 42);
            byte[] authReqFramed = FramedReader.Frame(authReqBody);

            byte[] coalesced = getKey.Concat(authReqFramed).ToArray();
            await stream.WriteAsync(coalesced, cancel.Token);

            byte[] secondKeyReply = await reader.ReadFrameAsync(cancel.Token);
            Assert.Equal((byte)AuthOpcode.AS_GetPublicKeyReply, secondKeyReply[0]);

            byte[] challengeReply = await reader.ReadFrameAsync(cancel.Token);
            Assert.Equal((byte)AuthOpcode.AS_AuthChallenge, challengeReply[0]);
            Assert.Equal(17, challengeReply.Length);
        }
        finally
        {
            server.stopServer();
        }
    }

    [Fact]
    public async Task Auth_server_overload_replies_server_busy_instead_of_dropping_socket()
    {
        int port = GetFreeTcpPort();
        using var server = new AuthServer(IPAddress.Loopback, port, workerCount: 1, maxQueueDepth: 1);
        Assert.True(server.Start());

        using var workerBlocked = new ManualResetEventSlim();
        using var releaseWorker = new ManualResetEventSlim();
        try
        {
            // Fill the single login worker and the single queue slot deterministically.
            Assert.True(server.LoginQueue.TryEnqueue(() =>
            {
                workerBlocked.Set();
                releaseWorker.Wait(TimeSpan.FromSeconds(10));
            }));
            Assert.True(workerBlocked.Wait(TimeSpan.FromSeconds(5)));
            Assert.True(server.LoginQueue.TryEnqueue(() => { }));

            using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var tcp = new TcpClient();
            await tcp.ConnectAsync(IPAddress.Loopback, port, cancel.Token);
            NetworkStream stream = tcp.GetStream();
            var reader = new FramedReader(stream);

            // Send an AS_AuthChallengeResponse while LoginQueue is full; server must reply LTAS_DATABASECONNDOWN.
            byte[] challengeResponseBody = new byte[5 + 32];
            challengeResponseBody[0] = (byte)AuthOpcode.AS_AuthChallengeResponse;
            challengeResponseBody[3] = 32;
            await stream.WriteAsync(FramedReader.Frame(challengeResponseBody), cancel.Token);

            byte[] reply = await reader.ReadFrameAsync(cancel.Token);
            Assert.Equal((byte)AuthOpcode.AS_AuthReply, reply[0]);
            Assert.Equal(AuthErrorReply.BodySize, reply.Length);
            Assert.Equal((uint)AuthErrorCode.LTAS_DATABASECONNDOWN, BitConverter.ToUInt32(reply, 1));
        }
        finally
        {
            releaseWorker.Set();
            server.stopServer();
        }
    }
}
