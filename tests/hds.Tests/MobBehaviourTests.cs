using hds;

namespace hds.Tests;

public class MobBehaviourTests
{
    [Theory]
    [InlineData("Zombie Rot", "", MobAggroKind.WithinLevelLimits)]
    [InlineData("Corrupted", "", MobAggroKind.WithinLevelLimits)]
    [InlineData("Special Agent", "6", MobAggroKind.HighLevelPlayersOnly)]
    [InlineData("Agent Lee", "", MobAggroKind.HighLevelPlayersOnly)]
    [InlineData("Club Guard", "5", MobAggroKind.Passive)]
    [InlineData("Blood Noble", "5", MobAggroKind.Passive)]
    [InlineData("Randy", "", MobAggroKind.Passive)]
    [InlineData("Chopper Runner", "", MobAggroKind.Passive)]
    [InlineData("Blackwood Goof", "3", MobAggroKind.WithinLevelLimits)]
    [InlineData("Bell Madonna", "1", MobAggroKind.WithinLevelLimits)]
    [InlineData("Blackwood Goof", "", MobAggroKind.Passive)]
    [InlineData("Gold Blood Enthusiast", "3", MobAggroKind.Passive)]
    public void Classifies(string name, string stealth, MobAggroKind expected)
    {
        Assert.Equal(expected, MobBehaviour.Classify(name, stealth));
    }

    [Fact]
    public void AnNpcFarAboveThePlayerDoesNotStartTheFight()
    {
        Assert.True(MobBehaviour.WillingAtLevels(MobAggroKind.WithinLevelLimits, 15, 10));
        Assert.False(MobBehaviour.WillingAtLevels(MobAggroKind.WithinLevelLimits, 16, 10));
    }

    [Fact]
    public void AgentsLeaveNewcomersAlone()
    {
        Assert.False(MobBehaviour.WillingAtLevels(MobAggroKind.HighLevelPlayersOnly, 255, 49));
        Assert.True(MobBehaviour.WillingAtLevels(MobAggroKind.HighLevelPlayersOnly, 255, 50));
    }

    [Fact]
    public void PassiveNeverStarts()
    {
        Assert.False(MobBehaviour.WillingAtLevels(MobAggroKind.Passive, 5, 5));
    }

    [Fact]
    public void WouldAttackUsesTheKind()
    {
        Mob mob = new Mob();
        mob.setLevel(5);
        mob.setIsSpawned(true);
        mob.aggroKind = MobAggroKind.Passive;
        Assert.False(MobAggro.WouldAttack(mob, 100, 5));
        mob.aggroKind = MobAggroKind.WithinLevelLimits;
        Assert.True(MobAggro.WouldAttack(mob, 100, 5));
    }
}
