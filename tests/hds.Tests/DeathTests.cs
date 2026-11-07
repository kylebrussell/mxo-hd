using hds;
using hds.shared;

namespace hds.Tests;

// Expected bytes from three retail player deaths (afterwhoruneo pvp, morpheuzzz pvp, afterwhoruneo killed by an Agent).
public class DeathTests
{
    private static string Hex(byte[] bytes) => StringUtils.bytesToString_NS(bytes).ToLowerInvariant();

    [Fact]
    public void DeathSetsIsDeadHealthAndConditionFlags()
    {
        Assert.Equal("020002808080c1010000808001" + "00000001", Hex(ServerPackets.BuildSelfDeathUpdate()));
    }

    [Fact]
    public void DeathMessageIsTheRetail36Bytes()
    {
        byte[] message = ServerPackets.BuildDeathMessage();
        Assert.Equal(36, message.Length);
        Assert.StartsWith("2e67" + "0000000000000000000000" + "92000060", Hex(message));
    }

    [Fact]
    public void RespawnHealsAndClearsConditionFlags()
    {
        // Retail: 02 00 02 80 80 80 c0 28 0a 80 80 01 00 00 00 00 (MaxHealth 0x0a28)
        Assert.Equal("020002808080c0" + "280a" + "8080" + "01" + "00000000", Hex(ServerPackets.BuildSelfRespawnHeal(0x0a28)));
    }

    [Fact]
    public void RespawnFinishPicksTheLoadingArea()
    {
        Assert.Equal("80fd" + "000000000101", Hex(ServerPackets.BuildRespawnToLoadingArea()));
    }
}
