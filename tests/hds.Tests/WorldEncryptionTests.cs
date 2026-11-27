using System.Collections;
using hds;

namespace hds.Tests;

public class WorldEncryptionTests
{
    [Fact]
    public void RoundTripsAPacket()
    {
        byte[] plain = { 0x02, 0x04, 0x01, 0x00, 0x05, 0x01, 0x02, 0x81, 0x4c };
        byte[] wire = new WorldEncryption().encrypt(plain, plain.Length, 0x7f, 3, 4);

        ArrayList result = new WorldEncryption().decrypt(wire, wire.Length);

        Assert.NotNull(result);
        Assert.Equal(plain, (byte[])result[0]);
        Assert.Equal((ushort)0x7f, (ushort)result[1]);
    }

    [Fact]
    public void DropsACorruptPacketInsteadOfThrowing()
    {
        // A datagram that decrypts to garbage once crashed the server: its declared length ran past the buffer
        // and PacketReader threw inside the socket callback.
        byte[] wire = new byte[49];
        wire[0] = 0x01;
        for (int i = 1; i < wire.Length; i++)
        {
            wire[i] = (byte)(i * 37);
        }

        Assert.Null(new WorldEncryption().decrypt(wire, wire.Length));
    }

    [Fact]
    public void DropsAPacketWithABadChecksum()
    {
        byte[] plain = { 0x02, 0x04, 0x01, 0x00, 0x05, 0x01, 0x02, 0x81, 0x4c };
        byte[] wire = new WorldEncryption().encrypt(plain, plain.Length, 0x7f, 3, 4);
        wire[wire.Length - 1] ^= 0xff; // flip the last encrypted block

        Assert.Null(new WorldEncryption().decrypt(wire, wire.Length));
    }

    /// <summary>
    /// A client's packets are decrypted on the socket callback while its queue is encrypted on the flush thread. Sharing
    /// a working buffer sent all-zero 2065-byte packets now and then (seen by tools/HeadlessClient's login scenario).
    /// </summary>
    [Fact]
    public async Task EncryptingWhileDecryptingOnAnotherThreadAlwaysGivesAValidPacket()
    {
        WorldEncryption shared = new WorldEncryption();
        byte[] incoming = new WorldEncryption().encrypt(new byte[] { 0x02 }, 1, 0, 0, 1);
        byte[] plain = new byte[700];
        plain[0] = 0x02;

        using CancellationTokenSource stop = new CancellationTokenSource();
        Task decrypting = Task.Run(() =>
        {
            while (!stop.IsCancellationRequested)
            {
                Assert.NotNull(shared.decrypt(incoming, incoming.Length));
            }
        });

        WorldEncryption reader = new WorldEncryption();
        for (int i = 0; i < 3000; i++)
        {
            byte[] packet = shared.encrypt(plain, plain.Length, 0, 0, (ushort)(i & 0xfff));
            ArrayList decrypted = reader.decrypt(packet, packet.Length);
            Assert.NotNull(decrypted);
            Assert.Equal(plain, (byte[])decrypted[0]);
        }

        stop.Cancel();
        await decrypting;
    }

    [Fact]
    public void EncryptGeneratesFreshIvAndNeverEmitsZeroWireByteFive()
    {
        WorldEncryption sender = new WorldEncryption();
        WorldEncryption reader = new WorldEncryption();
        byte[] plain = { 0x02 };
        byte[]? previousIv = null;

        for (int i = 0; i < 2000; i++)
        {
            byte[] wire = sender.encrypt(plain, plain.Length, 0x7f, 1, (ushort)((i + 1) & 0xfff));
            Assert.Equal(0x01, wire[0]);
            Assert.NotEqual(0x00, wire[5]); // IV[4]: stock client CThreadedUDPDriver::GetPacket drops wire[5] == 0

            byte[] iv = wire[1..17];
            if (previousIv != null)
            {
                Assert.NotEqual(previousIv, iv);
            }
            previousIv = iv;

            ArrayList decrypted = reader.decrypt(wire, wire.Length);
            Assert.NotNull(decrypted);
            Assert.Equal(plain, (byte[])decrypted[0]);
        }
    }

    [Fact]
    public void DecryptingClientPacketWithZeroIvByteFourDoesNotPoisonSubsequentEncrypts()
    {
        byte[] sessionKey = new byte[16];
        for (int i = 0; i < sessionKey.Length; i++) sessionKey[i] = (byte)(0x21 + i);

        // Build a valid encrypted client->server datagram whose IV[4] == 0x00 (wire[5] == 0x00),
        // matching the 1-in-256 packets stock client.dll sends that used to poison WorldEncryption.IV
        // and cause a 12-second server->client blackout.
        byte[] badClientIv = { 0x11, 0x22, 0x33, 0x44, 0x00, 0x55, 0x66, 0x77, 0x88, 0x99, 0xaa, 0xbb, 0xcc, 0xdd, 0xee, 0xff };
        byte[] clientPlain = { 0x02 };
        byte[] clientWire = EncryptWithExplicitIv(sessionKey, badClientIv, clientPlain, 0x7f, 136, 256);
        Assert.Equal(0x00, clientWire[5]);

        WorldEncryption server = new WorldEncryption(sessionKey);
        ArrayList inbound = server.decrypt(clientWire, clientWire.Length);
        Assert.NotNull(inbound);
        Assert.Equal(clientPlain, (byte[])inbound[0]);

        WorldEncryption clientPeer = new WorldEncryption(sessionKey);
        for (int i = 0; i < 500; i++)
        {
            byte[] reply = server.encrypt(clientPlain, clientPlain.Length, 0x7f, 136, (ushort)(257 + i));
            Assert.NotEqual(0x00, reply[5]);
            Assert.NotEqual(badClientIv, reply[1..17]);

            ArrayList decrypted = clientPeer.decrypt(reply, reply.Length);
            Assert.NotNull(decrypted);
            Assert.Equal(clientPlain, (byte[])decrypted[0]);
        }
    }

    private static byte[] EncryptWithExplicitIv(byte[] key, byte[] iv, byte[] plainData, ushort pss, ushort cseq, ushort sseq)
    {
        uint seqValue = ((uint)pss << 24) | ((uint)cseq << 12) | sseq;
        PacketContent body = new PacketContent();
        body.AddUint16((ushort)(plainData.Length + 4), 1);
        body.AddByteArray(TimeUtils.getCurrentTime());
        body.AddUint32(seqValue, 0);
        body.AddByteArray(plainData);

        byte[] bodyBytes = body.ReturnFinalPacket();
        int padding = 16 - ((bodyBytes.Length + 4) % 16);
        byte[] padBytes = new byte[padding];
        Array.Fill(padBytes, (byte)padding);

        PacketContent withCrc = new PacketContent();
        withCrc.AddByteArray(new Crc32().checksumB(bodyBytes, 1));
        withCrc.AddByteArray(bodyBytes);
        withCrc.AddByteArray(padBytes);

        byte[] plaintext = withCrc.ReturnFinalPacket();
        byte[] ciphertext = new byte[plaintext.Length];
        MxoTwofish tf = new MxoTwofish();
        tf.SetKey(key);
        tf.SetIV(iv);
        tf.Encrypt(plaintext, ciphertext);

        PacketContent wire = new PacketContent();
        wire.AddByte(0x01);
        wire.AddByteArray(iv);
        wire.AddByteArray(ciphertext);
        return wire.ReturnFinalPacket();
    }
}
