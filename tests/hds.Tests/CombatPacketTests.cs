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
    public void NpcDeathMatchesRetail()
    {
        // Parts 01 and 04: UseCount=2, StopFollowActiveTracker=0, IsDead=1, Health=0,
        // CurrentState=0x7fd, ConditionStateFlags=0.
        Assert.Equal("0501c202008080e0010000808003fd07000000000000", Hex(ServerPackets.BuildNpcDeathUpdate()));
    }
}
