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
}
