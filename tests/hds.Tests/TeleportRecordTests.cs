using hds;

namespace hds.Tests;

/// <summary>The own-view teleport record names the nav mesh place it lands in, as retail's did.</summary>
public class TeleportRecordTests
{
    [Fact]
    public void BuildsRetailsRecordForAPlace()
    {
        // Retail, Slums sector 209 (0xd1), region 0x52, polygon 3: 03 02 00 01 1c <yaw> d1 00 52 00 03 00 7c <x y z f32>
        byte[] record = PacketsUtils.createTeleportPacket(23828.3f, 1295f, 13984.9f, 0xd1, 0x52, 3, 0x7c, 0x11);

        Assert.Equal(new byte[] { 0x02, 0x00, 0x01, 0x1c, 0x11, 0xd1, 0x00, 0x52, 0x00, 0x03, 0x00, 0x7c }, record.Take(12).ToArray());
        Assert.Equal(23828.3f, BitConverter.ToSingle(record, 12));
        Assert.Equal(1295f, BitConverter.ToSingle(record, 16));
        Assert.Equal(13984.9f, BitConverter.ToSingle(record, 20));
        Assert.Equal(26, record.Length);
    }

    [Fact]
    public void WithoutAClientItFallsBackToTheFixedRecord()
    {
        byte[] record = PacketsUtils.createTeleportPacket(null, 100f, 200f, 300f);

        Assert.Equal(new byte[] { 0x02, 0x00, 0x01, 0x1c, 0x11, 0xf7, 0x02, 0x5c, 0x00, 0x03, 0x00, 0x00 }, record.Take(12).ToArray());
    }
}
