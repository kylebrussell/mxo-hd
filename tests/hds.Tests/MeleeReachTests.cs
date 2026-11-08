using hds;

namespace hds.Tests;

public class MeleeReachTests
{
    [Theory]
    [InlineData(0f, true)]
    [InlineData(1.5f, true)]   // where an aggroing NPC stops (150 units)
    [InlineData(3f, true)]
    [InlineData(3.1f, false)]
    [InlineData(16f, false)]   // pistol range: a melee NPC has to run up first
    public void MeleeReach(float metres, bool reach)
    {
        Assert.Equal(reach, CombatHandler.InMeleeReach(metres));
    }

    [Fact]
    public void AggroStopPointIsWithinReach()
    {
        Assert.True(CombatHandler.InMeleeReach((float)(MobAggro.MeleeDistanceUnits / 100)));
    }
}

public class AbilityDeniedTests
{
    [Fact]
    public void DeniedMatchesRetail()
    {
        // mission_and_crew_things: 80 ba 49 01 00 00 6b 1f + 11 zero bytes while 80 bd locked ability 0x149
        byte[] expected = { 0x80, 0xba, 0x49, 0x01, 0x00, 0x00, 0x6b, 0x1f, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
        Assert.Equal(expected, AbilityCooldowns.BuildDenied(0x149, AbilityCooldowns.DeniedTimerRunning));
    }
}
