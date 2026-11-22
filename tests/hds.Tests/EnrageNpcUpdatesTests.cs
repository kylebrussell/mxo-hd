using System.Runtime.CompilerServices;
using hds;
using hds.shared;

namespace hds.Tests;

[Collection("DelayedActions")]
public class EnrageNpcUpdatesTests
{
    [Fact]
    public void OnlyTheSelectedEnrageOnAnNpcUsesTheShippedThreeSecondUpdates()
    {
        var mob = Npc();
        var subject = TimedEffects.ForMob(mob);
        Assert.True(TempMods.TryGet(588, out var enrage));
        Assert.True(TempMods.TryGet(412, out var tomoNage));
        double now = TimeUtils.GetSimTimeSeconds();
        try
        {
            var state = Assert.Single(TimedEffects.Apply(subject, enrage, now));
            Assert.Equal(TempMods.EnrageAbility, state.Target);
            Assert.Equal(now + 3, state.NextTick);
            var ticks = new List<TimedEffects.Active>();
            var expired = new List<TimedEffects.Active>();
            subject.Effects.Advance(now + 11, ticks, expired);
            Assert.Equal(new[] { state, state, state }, ticks);
            Assert.Empty(expired);
            subject.Effects.Advance(now + 12, ticks, expired);
            Assert.Same(state, Assert.Single(expired));

            var other = Assert.Single(TimedEffects.Apply(subject, tomoNage, now), m => m.Target == TempMods.EnrageAbility);
            Assert.True(double.IsPositiveInfinity(other.NextTick));
            var player = TimedEffects.ForPlayer(Player());
            try
            {
                var playerState = Assert.Single(TimedEffects.Apply(player, enrage, now));
                Assert.False(ControlActions.CanWithdraw(player.Effects));
                Assert.True(double.IsPositiveInfinity(playerState.NextTick));
            }
            finally { player.Effects.RemoveAll(); }
        }
        finally { subject.Effects.RemoveAll(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExpiryOrDeathClearsAnEnrageBoundChaseBeforeItCanStartCombat(bool death)
    {
        string root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "data", "sector_properties.csv"))) root = Path.GetDirectoryName(root)!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        var mob = Npc();
        var player = Player();
        var view = player.viewMan.addView(mob.getEntityId(), 599);
        view.viewCreated = true;
        double now = TimeUtils.GetSimTimeSeconds();
        var subject = TimedEffects.ForMob(mob);
        Assert.True(TempMods.TryGet(588, out var enrage));
        try
        {
            var state = Assert.Single(TimedEffects.Apply(subject, enrage, now));
            Assert.True(MobMotion.BeginChase(player, mob, state));
            Assert.True(MobMotion.Chasing(mob));
            if (death) mob.setIsDead(true);
            var mobs = new Dictionary<ulong, Mob> { [mob.getEntityId()] = mob };
            MobMotion.Tick(new List<WorldClient> { player }, mobs, now + (death ? 4 : 12.01));
            Assert.False(MobMotion.Chasing(mob));
            Assert.Null(mob.getCombatTarget());
        }
        finally
        {
            MobMotion.ResetFixtureMotion(mob);
            subject.Effects.RemoveAll();
        }
    }

    private static Mob Npc()
    {
        var mob = new Mob();
        mob.setDistrict(1);
        mob.setEntityId(588001);
        mob.setXPos(17000);
        mob.setYPos(495);
        mob.setZPos(6400);
        mob.setHealthC(500);
        mob.setHealthM(500);
        return mob;
    }

    private static WorldClient Player()
    {
        var player = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        player.LivenessClock = () => 0;
        player.Alive = true;
        player.playerData = new ClientData();
        player.playerData.setDistrictId(1);
        player.playerData.setOnWorld(true);
        player.playerInstance = new PlayerCharacter();
        player.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(17400, 495, 6400));
        player.playerInstance.Level.setValue((byte)25);
        player.viewMan = new ViewManager();
        player.messageQueue = new MessageQueue();
        typeof(WorldClient).GetField("lastFlushTick", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(player, long.MaxValue);
        return player;
    }
}
