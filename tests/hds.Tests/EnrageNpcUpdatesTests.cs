using System.Runtime.CompilerServices;
using hds;
using hds.shared;

namespace hds.Tests;

[Collection("DelayedActions")]
public class EnrageNpcUpdatesTests
{
    [Theory]
    [InlineData(462, 6, 1)]
    [InlineData(588, 12, 3)]
    [InlineData(597, 10, 3)]
    public void NpcEnrageCastsUseTheShippedThreeSecondUpdates(ushort ability, int duration, int pulses)
    {
        var mob = Npc();
        var subject = TimedEffects.ForMob(mob);
        Assert.True(TempMods.TryGet(ability, out var enrage));
        Assert.True(TempMods.TryGet(412, out var tomoNage));
        double now = TimeUtils.GetSimTimeSeconds();
        try
        {
            var state = Assert.Single(TimedEffects.Apply(subject, enrage, now));
            Assert.Equal(TempMods.EnrageAbility, state.Target);
            Assert.Equal(now + 3, state.NextTick);
            var ticks = new List<TimedEffects.Active>();
            var expired = new List<TimedEffects.Active>();
            subject.Effects.Advance(now + duration - 0.01, ticks, expired);
            Assert.Equal(Enumerable.Repeat(state, pulses), ticks);
            Assert.Empty(expired);
            subject.Effects.Advance(now + duration, ticks, expired);
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

    [Theory]
    [InlineData(462, true, false)]
    [InlineData(462, false, false)]
    [InlineData(588, true, false)]
    [InlineData(588, false, false)]
    [InlineData(597, true, false)]
    [InlineData(597, false, false)]
    [InlineData(40, true, false)]
    [InlineData(40, false, true)]
    public void ExpiryDoesNotRedirectAnOccupiedNpcAndOnlyOrdinaryEffectsCanStartRetaliation(
        ushort ability, bool anotherPlayerIsFighting, bool shouldRetaliate)
    {
        string root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "data", "sector_properties.csv"))) root = Path.GetDirectoryName(root)!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        var mob = Npc();
        var caster = Player();
        var opponent = Player();
        caster.viewMan.addView(mob.getEntityId(), 599).viewCreated = true;
        if (anotherPlayerIsFighting) mob.setCombatTarget(opponent);
        var subject = TimedEffects.ForMob(mob);
        Assert.True(TempMods.TryGet(ability, out var effect));
        var previousClients = WorldServer.Clients;
        WorldServer.Clients = new Dictionary<string, WorldClient>();

        try
        {
            var now = TimeUtils.GetSimTimeSeconds();
            var applied = TimedEffects.Apply(subject, effect, now, caster: caster);
            Assert.NotEmpty(applied);
            mob.setHealthC(450);

            // Expire only this subject, without advancing unrelated test combat.
            var expired = new List<TimedEffects.Active>();
            subject.Effects.Advance(applied.Max(mod => mod.Expires) + 0.01, new List<TimedEffects.Active>(), expired);
            Assert.NotEmpty(expired);
            subject.Ended(expired);

            Assert.Equal(shouldRetaliate, MobMotion.Chasing(mob));
            Assert.Same(anotherPlayerIsFighting ? opponent : null, mob.getCombatTarget());
            Assert.Equal(anotherPlayerIsFighting ? 1 : 0, mob.CombatantCount);
            Assert.False(CombatManager.GetHandler(caster).isCombatRunning);
        }
        finally
        {
            MobMotion.ResetFixtureMotion(mob);
            subject.Effects.RemoveAll();
            try { CombatManager.RemoveClient(caster); }
            finally { WorldServer.Clients = previousClients; }
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
