using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using hds;
using hds.accounts;
using hds.auth;
using HeadlessClient;

namespace hds.Tests;

public class MarginFramingAndDataLoaderTests
{
    [Fact]
    public void AuthFrameBuffer_custom_limit_handles_large_margin_creation_frames_and_rejects_oversize()
    {
        var buffer = new AuthFrameBuffer(MarginClient.MaxFrameLength);
        Assert.Equal(MarginClient.MaxFrameLength, buffer.MaxAllowedFrameLength);

        // 4136-byte encrypted body (e.g. max 4092-byte 0x0c creation body + IV + header + padding)
        byte[] largeBody = new byte[4136];
        for (int i = 0; i < largeBody.Length; i++) largeBody[i] = (byte)((i * 31) & 0xFF);
        byte[] largeFrame = FramedReader.Frame(largeBody);
        Assert.True(largeFrame.Length > AuthFrameBuffer.MaxFrameLength);
        Assert.True(largeFrame.Length <= MarginClient.MaxFrameLength);

        // Feed in uneven chunks (1, 13, 511, remainder) to test reassembly across TCP boundaries.
        var extracted = new List<byte[]>();
        int[] chunks = { 1, 13, 511, 1024, largeFrame.Length };
        int pos = 0;
        foreach (int chunk in chunks)
        {
            if (pos >= largeFrame.Length) break;
            int take = Math.Min(chunk, largeFrame.Length - pos);
            buffer.Append(largeFrame, pos, take, extracted);
            pos += take;
        }

        Assert.Equal(0, buffer.BufferedBytes);
        Assert.Single(extracted);
        Assert.Equal(largeFrame, extracted[0]);

        // Frame declaring length > MarginClient.MaxFrameLength must be rejected immediately.
        int overBody = MarginClient.MaxFrameLength;
        byte[] oversizeHeader = { (byte)(0x80 | (overBody >> 8)), (byte)(overBody & 0xFF), 0x01 };
        Assert.Throws<InvalidDataException>(() => buffer.Append(oversizeHeader, 0, oversizeHeader.Length, extracted));
    }

    [Fact]
    public async Task MarginClient_reassembles_fragmented_and_coalesced_TCP_handshake_frames()
    {
        var server = new MarginServer(IPAddress.Loopback, 0, maxActiveClients: 8);
        server.SetActiveWorldCharacterCheckForTesting(_ => false);
        server.SetAccountStatusCheckForTesting(uid => uid == 9001 ? AccountLogin.StatusActive : (int?)null);
        server.StartServer();
        try
        {
            int port = server.BoundPort;
            AccountKey account = AccountKeys.Generate();
            uint now = TimeUtils.getUnixTimeUint32();
            byte[] signedData = AuthTicketCodec.BuildSignedData(9001, "framingqa", now + 600, account.Modulus, now);
            byte[] signature = AuthTicketCodec.Sign(signedData);

            var certBody = new List<byte> { 0x01, 0x03, 0x00 };
            certBody.AddRange(BitConverter.GetBytes((ushort)(signature.Length + signedData.Length)));
            certBody.AddRange(signature);
            certBody.AddRange(signedData);
            byte[] certFrame = FramedReader.Frame(certBody.ToArray());

            using var tcp = new TcpClient { NoDelay = true };
            await tcp.ConnectAsync(IPAddress.Loopback, port);
            NetworkStream stream = tcp.GetStream();
            var reader = new FramedReader(stream);

            // 1. Send CertConnectRequest fragmented into 3 separate TCP writes (including a 1-byte header fragment).
            await stream.WriteAsync(certFrame.AsMemory(0, 1));
            await stream.FlushAsync();
            await Task.Delay(15);
            await stream.WriteAsync(certFrame.AsMemory(1, 77));
            await stream.FlushAsync();
            await Task.Delay(15);
            await stream.WriteAsync(certFrame.AsMemory(78));
            await stream.FlushAsync();

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            byte[] certReply = await reader.ReadFrameAsync(cts.Token);
            Assert.Equal(0x02, certReply[0]);
            int blobSize = BitConverter.ToUInt16(certReply, 3);
            byte[] plainKeyBlob = AuthLogin.DecryptWithAccountKey(certReply[5..(5 + blobSize)], account.Modulus, account.PrivateExponent);
            byte[] sessionKey = plainKeyBlob[1..17];

            var clientEnc = new MarginEncryption();
            clientEnc.SetSessionKey(sessionKey);

            // 2. Send CertConnectAck (0x03) and ConnectChallenge (0x06) COALESCED in a single TCP write.
            byte[] ackFrame = FramedReader.Frame(clientEnc.encrypt(new byte[] { 0x03 }));
            byte[] challengeFrame = FramedReader.Frame(clientEnc.encrypt(new byte[] { 0x06 }));
            byte[] coalesced = ackFrame.Concat(challengeFrame).ToArray();
            await stream.WriteAsync(coalesced, cts.Token);
            await stream.FlushAsync();

            byte[] reply1Raw = await reader.ReadFrameAsync(cts.Token);
            byte[] reply2Raw = await reader.ReadFrameAsync(cts.Token);
            byte[] reply1 = clientEnc.decryptMargin(new byte[] { 0x00 }.Concat(reply1Raw).ToArray());
            byte[] reply2 = clientEnc.decryptMargin(new byte[] { 0x00 }.Concat(reply2Raw).ToArray());

            Assert.NotNull(reply1);
            Assert.NotNull(reply2);
            Assert.Equal(0x04, reply1[0]);
            Assert.Equal(0x07, reply2[0]);
        }
        finally
        {
            server.StopServer();
        }
    }

    [Fact]
    public async Task MarginServer_enforces_active_client_cap_and_recovers_after_disconnect()
    {
        var server = new MarginServer(IPAddress.Loopback, 0, maxActiveClients: 2);
        server.SetActiveWorldCharacterCheckForTesting(_ => false);
        server.StartServer();
        var held = new List<TcpClient>();
        try
        {
            int port = server.BoundPort;
            for (int i = 0; i < 2; i++)
            {
                var c = new TcpClient { NoDelay = true };
                await c.ConnectAsync(IPAddress.Loopback, port);
                held.Add(c);
            }

            SpinWait.SpinUntil(() => server.ActiveClientCount == 2, TimeSpan.FromSeconds(5));
            Assert.Equal(2, server.ActiveClientCount);

            // A 3rd connection over the cap must be closed immediately without increasing ActiveClientCount.
            using (var overflow = new TcpClient { NoDelay = true })
            {
                await overflow.ConnectAsync(IPAddress.Loopback, port);
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                byte[] probe = new byte[16];
                int n = 0;
                try
                {
                    n = await overflow.GetStream().ReadAsync(probe, cts.Token);
                }
                catch (IOException)
                {
                    n = 0;
                }
                Assert.Equal(0, n);
            }
            Assert.Equal(2, server.ActiveClientCount);

            // Release one slot and confirm a new client is accepted cleanly.
            held[0].Close();
            held.RemoveAt(0);
            Assert.True(SpinWait.SpinUntil(() => server.ActiveClientCount == 1, TimeSpan.FromSeconds(5)));

            var replacement = new TcpClient { NoDelay = true };
            await replacement.ConnectAsync(IPAddress.Loopback, port);
            held.Add(replacement);
            Assert.True(SpinWait.SpinUntil(() => server.ActiveClientCount == 2, TimeSpan.FromSeconds(5)));
        }
        finally
        {
            foreach (var c in held)
            {
                try { c.Close(); } catch { }
            }
            server.StopServer();
        }
    }

    [Fact]
    public void DataLoader_O1_catalog_indexes_match_first_match_and_refresh_on_mutation()
    {
        DataLoader loader = DataLoader.CreateEmptyForTesting();

        var ab1 = new AbilityItem();
        ab1.setAbilityID(42);
        ab1.setAbilityName("First42");
        var ab2 = new AbilityItem();
        ab2.setAbilityID(42);
        ab2.setAbilityName("Duplicate42");
        loader.AbilityDB.Add(ab1);
        loader.AbilityDB.Add(ab2);

        Assert.Same(ab1, loader.getAbilityByID(42));
        Assert.Null(loader.getAbilityByID(999));

        var ab3 = new AbilityItem();
        ab3.setAbilityID(999);
        ab3.setAbilityName("Added999");
        loader.AbilityDB.Add(ab3);
        Assert.Same(ab3, loader.getAbilityByID(999));

        var go1 = new GameObjectItem();
        go1.setGOID(500);
        go1.setName("FirstGO");
        var go2 = new GameObjectItem();
        go2.setGOID(500);
        go2.setName("DupGO");
        loader.GODB.Add(go1);
        loader.GODB.Add(go2);
        Assert.Same(go1, loader.getGameObjectItemById(500));
        Assert.Null(loader.getGameObjectItemById(501));

        var cl1 = new ClothingItem();
        cl1.setGoidDecimal(1234);
        cl1.setItemName("FirstCoat");
        var cl2 = new ClothingItem();
        cl2.setGoidDecimal(1234);
        cl2.setItemName("DupCoat");
        loader.ClothingRSIDB.Add(cl1);
        loader.ClothingRSIDB.Add(cl2);
        Assert.Same(cl1, loader.getItemValues(1234));
        ClothingItem missingClothing = loader.getItemValues(9999);
        Assert.NotNull(missingClothing);
        Assert.Equal(0u, missingClothing.getGoidDecimal());

        loader.Emotes.Add(new EmoteItem(0x11223344u, 0x05));
        loader.Emotes.Add(new EmoteItem(0x11223344u, 0x09));
        Assert.Equal((byte)0x05, loader.findEmoteByLongId(0x11223344u));
        Assert.Equal((byte)0x00, loader.findEmoteByLongId(0xDEADBEEFu));

        // Mutating/clearing Emotes refreshes index automatically.
        loader.Emotes.Clear();
        Assert.Equal((byte)0x00, loader.findEmoteByLongId(0x11223344u));
    }

    [Fact]
    public void ObjectMessageReceipt_AckWindow_matches_ISet_and_Advance_expires_cleanly()
    {
        var r1 = new ObjectMessageReceipt();
        r1.Dispatched(0xFFF, 100);
        r1.Advance(100 + 0x7FF);
        Assert.False(r1.Finished);
        Assert.False(r1.Received);

        // ACK sequence 0x002 with bit for 3 packets back (0xFFF) set: bit (3-1)=2 -> 1 << 2 = 0x04.
        var missWindow = new SequencedMessage.AckWindow(0x002, 0x00);
        r1.Observe(in missWindow);
        Assert.False(r1.Received);

        var hitWindow = new SequencedMessage.AckWindow(0x002, 0x04);
        r1.Observe(in hitWindow);
        Assert.True(r1.Received);
        Assert.True(r1.Finished);

        // Expiry at delta >= 0x800 cancels receipt.
        var r2 = new ObjectMessageReceipt();
        r2.Dispatched(10, 50);
        r2.Advance(50 + 0x800);
        Assert.True(r2.Finished);
        Assert.False(r2.Received);
    }

    [Fact]
    public void WorldServer_BuildCombinedViewUpdate_matches_concatenated_BuildViewUpdate()
    {
        byte[] a = { 0x01, 0x0c, 0x10, 0x20 };
        byte[] b = { 0x02, 0xaa, 0xbb };
        byte[] expected = WorldServer.BuildViewUpdate(0x1234, a)
            .Concat(WorldServer.BuildViewUpdate(0x1234, b))
            .ToArray();
        byte[] actual = WorldServer.BuildCombinedViewUpdate(0x1234, a, b);
        Assert.Equal(expected, actual);
    }
}
