using hds;

namespace hds.Tests;

/// <summary>The own-view teleport record, laid out as retail's (docs/WORLD-DATA.md, "The own-view teleport record").</summary>
public class TeleportRecordTests
{
    [Fact]
    public void LaysOutTheRecordAsRetail()
    {
        // Retail (luizoe Recursion_Char 38892): 03 02 00 01 1c 2a d1 00 52 00 03 00 7c <x y z f32> <sim time ms u32>:
        // teleport id 0x2a, sector 209, region 0x52, polygon 3, yaw 0x7c
        byte[] record = PacketsUtils.createTeleportPacket(23828.3f, 1295f, 13984.9f, 0xd1, 0x52, 3, 0x2a, 0x7c);

        Assert.Equal(new byte[] { 0x02, 0x00, 0x01, 0x1c, 0x2a, 0xd1, 0x00, 0x52, 0x00, 0x03, 0x00, 0x7c }, record.Take(12).ToArray());
        Assert.Equal(23828.3f, BitConverter.ToSingle(record, 12));
        Assert.Equal(1295f, BitConverter.ToSingle(record, 16));
        Assert.Equal(13984.9f, BitConverter.ToSingle(record, 20));
        Assert.Equal(28, record.Length); // then the server's sim time in ms; the queue adds the 00 00
    }

    [Fact]
    public void WithoutAClientItNamesNoPlace()
    {
        byte[] record = PacketsUtils.createTeleportPacket(null, 100f, 200f, 300f);

        Assert.Equal(new byte[] { 0x02, 0x00, 0x01, 0x1c, 0x01, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff }, record.Take(11).ToArray());
    }

    [Fact]
    public void TheDingCarriesTheDestination()
    {
        Assert.Equal(new byte[] { 0x81, 0x42, 0x2a, 0x06, 0x00, 0x22, 0, 0, 0, 0, 0x19, 0x00, 0x95, 0x01 },
            PacketsUtils.BuildElevatorDing(0x19, 405));
    }
}
