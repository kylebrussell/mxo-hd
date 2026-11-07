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
}
