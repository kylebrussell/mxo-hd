using hds;
using hds.shared;

namespace hds.Tests;

// Expected bytes come from retail captures under research/ and were checked against the 7.6005 client
// (health bars drop on both sides, the dead NPC stops offering combat and its corpse can be looted).
public class CombatPacketTests
{
    private static string Hex(byte[] bytes) => StringUtils.bytesToString_NS(bytes).ToLowerInvariant();

    [Fact]
    public void PlainHitUpdatesTheNpcHealthAlone()
    {
        // Retail sent this form for most hits: groups select view attribute 27 (Health) only.
        Assert.Equal("0480808040d101", Hex(Mob.BuildHitUpdate(0x01d1, 0, 7)));
    }

    [Fact]
    public void AbilityHitAddsEffectAndCounter()
    {
        // Retail: 04 80 80 80 c0 75 03 c0 b7 01 00 28 01 01 (health, effect 0x280001b7, counter).
        Assert.Equal("04808080c07503c0b701002801" + "01", Hex(Mob.BuildHitUpdate(0x0375, 0x280001b7, 1)));
    }

    [Fact]
    public void PlayerHealthUsesTheOwnViewLayout()
    {
        // View 2, own-view update (02), groups 80 80 80 40 = own-view attribute 27, Health.
        // Retail: 02 00 02 80 80 80 40 ec 13.
        Assert.Equal("0200028080804088" + "13", Hex(ServerPackets.BuildSelfHealthUpdate(0x1388)));
    }

    [Fact]
    public void AppearanceUpdateSelectsOnlyRsiDescription()
    {
        // Retail: 02 00 02 80 01 <15 RSI bytes>. Male, hat 44, coat 23, everything else 0.
        int[] rsi = new int[22];
        rsi[2] = 44;
        rsi[5] = 23;
        byte[] rsiBytes = PacketsUtils.getRSIBytes(rsi);

        Assert.Equal(13, rsiBytes.Length); // packed parts; RSIDescription itself is 15 bytes
        Assert.Equal("0200028001" + "160005c0" + "000000000000000000" + "0000",
            Hex(ServerPackets.BuildAppearanceUpdate(rsiBytes)));
    }

    [Fact]
    public void HyperSpeedUsesRetailMovementScaleAndRealInnerStrength()
    {
        // Retail: 02 00 02 88 05 8c 1e 66 66 e6 3e 80 90 <IS> b0 56 06 00 28 <ctr> 02 ...
        string hex = Hex(AbilityPackets_BuildHyperSpeed(207, 2));
        Assert.StartsWith("0200" + "0288058c1e" + "6666e63e" + "8090" + "cf00" + "b056060028" + "02", hex);
    }

    private static byte[] AbilityPackets_BuildHyperSpeed(ushort innerStrength, byte counter) =>
        ServerPackets.BuildHyperSpeedUpdate(innerStrength, counter);

    [Fact]
    public void NpcDeathMatchesRetail()
    {
        // Parts 01 and 04: UseCount=2, StopFollowActiveTracker=0, IsDead=1, Health=0,
        // CurrentState=0x7fd, ConditionStateFlags=0.
        Assert.Equal("0501c202008080e0010000808003fd07000000000000", Hex(ServerPackets.BuildNpcDeathUpdate()));
    }
}
