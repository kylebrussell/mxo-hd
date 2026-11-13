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
    public void CapturedReason31RemainsLosslessWithoutATimerClassification()
    {
        // Captured mission_and_crew_things literal31 is retained. Temporal adjacency to80bd did not establish its meaning.
        byte[] expected = { 0x80, 0xba, 0x49, 0x01, 0x00, 0x00, 0x6b, 0x1f, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
        Assert.Equal(expected, AbilityCooldowns.BuildDenied(0x149, 0x1f));
    }
}
