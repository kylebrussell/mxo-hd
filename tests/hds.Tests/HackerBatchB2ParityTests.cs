using System.Reflection;
using System.Runtime.CompilerServices;
using hds;
using hds.world.Structures;

namespace hds.Tests;

[Collection("ConcentrationEnvironment")]
public class HackerBatchB2ParityTests
{
    private static WorldClient Client(byte level = 50, uint character = 6100)
    {
        var player = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        player.playerData = new ClientData();
        player.playerData.setDistrictId(1);
        player.playerData.setCharID(character);
        player.playerData.setEntityId(character);
        player.playerData.setOnWorld(true);
        player.playerInstance = new PlayerCharacter();
        player.playerInstance.Level.setValue(level);
        player.playerInstance.MaxHealth.setValue((ushort)1000);
        player.playerInstance.Health.setValue((ushort)400);
        player.playerInstance.InnerStrengthMax.setValue((ushort)200);
        player.playerInstance.InnerStrengthAvailable.setValue((ushort)100);
        player.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(0, 495, 0));
        player.messageQueue = new MessageQueue();
        player.viewMan = new ViewManager();
        player.LivenessClock = () => 0;
        var flags = BindingFlags.NonPublic | BindingFlags.Instance;
        typeof(WorldClient).GetField("lastFlushTick", flags)!.SetValue(player, long.MaxValue);
        typeof(WorldClient).GetField("alive", flags)!.SetValue(player, true);
        typeof(WorldClient).GetField("<TransportFence>k__BackingField", flags)!.SetValue(player, new WorldSendFence());
        return player;
    }

    [Fact]
    public void LoadedNetworkAttackerPublishesTheStoredGrantOnceAndCanReload()
    {
        var caster = Client();
        var loaded = new[] { ((ushort)483, (ushort)50) };
        byte[] add = Convert.FromHexString("80bc4503380400e30100003804000000000000000000");
        var remove = add.ToArray();
        remove[17] = 1;
        int Count(byte[] expected) => caster.messageQueue.RPCMessagesQueue.Cast<SequencedMessage>()
            .Count(packet => packet.content.AsSpan(1).SequenceEqual(expected));

        AbilityPassives.SetLoaded(caster, loaded, newWorld: true);
        Assert.True(AbilityPassives.IsLoaded(caster, 1080));
        Assert.Equal(0, Count(add));
        AbilityGrantRegistration.LoadingComplete(caster, new byte[] { 1 });
        AbilityGrantRegistration.LoadingComplete(caster, new byte[] { 1 });
        Assert.Equal(1, Count(add));

        AbilityPassives.SetLoaded(caster, Array.Empty<(ushort, ushort)>());
        Assert.False(AbilityPassives.IsLoaded(caster, 1080));
        Assert.Equal(1, Count(remove));
        AbilityPassives.SetLoaded(caster, loaded);
        Assert.Equal(2, Count(add));
    }

    [Fact]
    public void HowitzerUpgradeUsesTheCasterAndClearsAllFourConsumersOnClassReplacement()
    {
        var caster = Client();
        Assert.Same(caster, Assert.Single(AbilitySupport.Targets(caster, 1087, 999)));
        AbilitySupport.Land(caster, 1087, 999);
        Assert.Equal(13, AbilityStats.ViralAccuracy(caster));
        Assert.Equal(8, AbilityStats.Influence(caster, 172));
        Assert.Equal((int)(1000 * (double)(float)1.08), AbilityStats.ScaleTypedAbilityDamage(caster, 2, 1000));
        Assert.Equal(78, AbilityCastInterrupts.CastThroughPercent(10, 500,
            (short)AbilityStats.Influence(caster, AbilityCastInterrupts.Concentration)));
        Assert.Equal(4 * (1 + (double)0.17f), AbilityRadius.TargetMetres(caster, 333));

        AbilitySupport.Land(caster, 1078, 999);
        Assert.False(caster.playerData.activeBuffs.ContainsKey(1087));
        Assert.Equal(4, AbilityStats.ViralAccuracy(caster));
        Assert.Equal(0, AbilityStats.Influence(caster, 172));
        Assert.Equal(4, AbilityRadius.TargetMetres(caster, 333));
        Assert.True(AbilityBuffs.Remove(caster, 1078));
        Assert.Equal(75, AbilityCastInterrupts.CastThroughPercent(10, 500,
            (short)AbilityStats.Influence(caster, AbilityCastInterrupts.Concentration)));
    }

    [Theory]
    [InlineData(55, 21)]
    [InlineData(80, 13)]
    [InlineData(413, 70)]
    [InlineData(414, 76)]
    public void DirectHealsChangeHealthAndStopAtRecipientMaximum(ushort id, int delta)
    {
        var caster = Client();
        AbilitySupport.Land(caster, id, 2);
        Assert.Equal(400 + delta, PvpCombat.HealthOf(caster));
        caster.playerInstance.Health.setValue((ushort)998);
        AbilitySupport.Land(caster, id, 2);
        Assert.Equal(1000, PvpCombat.HealthOf(caster));
        if (id == 55)
        {
            Assert.Equal(-25, AbilityStats.Attack(caster, false));
            Assert.Equal(-18, AbilityStats.Attack(caster, true));
            Assert.Equal(-18, AbilityStats.ThrownAccuracy(caster));
            Assert.Equal(-25, AbilityStats.ViralAccuracy(caster));
            TimedEffects.RunDue(caster.playerData.activeBuffs[id].Started + 10.01);
            Assert.Equal(0, AbilityStats.Attack(caster, false));
            Assert.Equal(0, AbilityStats.Attack(caster, true));
            Assert.Equal(0, AbilityStats.ThrownAccuracy(caster));
            Assert.Equal(0, AbilityStats.ViralAccuracy(caster));
        }
    }

    [Fact]
    public void RestoreGroup56HealsNearbyTeamMembersAtTheRadiusBoundary()
    {
        var caster = Client();
        var member = Client(character: 6101);
        var far = Client(character: 6102);
        var outsider = Client(character: 6103);
        member.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(600, 495, 0));
        far.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(601, 495, 0));
        var previousClients = WorldServer.Clients;
        WorldServer.Clients = new() { ["caster"] = caster, ["member"] = member, ["far"] = far, ["outsider"] = outsider };
        var team = SocialGroups.Instance.CreateTeam(6100, "caster");
        try
        {
            SocialGroups.Instance.AddTeamMember(team.Id, 6101, "member");
            SocialGroups.Instance.AddTeamMember(team.Id, 6102, "far");
            AbilitySupport.Land(caster, 56, 2);
            Assert.Equal(408, PvpCombat.HealthOf(caster));
            Assert.Equal(408, PvpCombat.HealthOf(member));
            Assert.Equal(400, PvpCombat.HealthOf(far));
            Assert.Equal(400, PvpCombat.HealthOf(outsider));
        }
        finally
        {
            SocialGroups.Instance.DisbandTeam(team.Id);
            WorldServer.Clients = previousClients;
        }
    }

    [Fact]
    public void MovementAccelerator62PublishesRecipientScaleUntilExpiry()
    {
        var caster = Client();
        var target = Client(character: 6101);
        target.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(400, 495, 0));
        var view = caster.viewMan.addView(6101, 1);
        view.viewCreated = true;
        var previousClients = WorldServer.Clients;
        WorldServer.Clients = new() { ["caster"] = caster, ["recipient"] = target };
        var speedOn = Convert.FromHexString("0200028088CDCCCC3D808080020000");
        var speedOff = Convert.FromHexString("020002808800000000808080020000");
        try
        {
            AbilitySupport.Land(caster, 62, view.ViewID);
            var buff = target.playerData.activeBuffs[62];
            Assert.Equal(10, AbilityStats.Player(target, 115));
            Assert.Equal(0, AbilityStats.Player(caster, 115));
            Assert.Equal(.10f, BitConverter.ToSingle(target.playerInstance.MovementScale.getValue()));
            Assert.Equal(0f, BitConverter.ToSingle(caster.playerInstance.MovementScale.getValue()));
            Assert.Contains(target.messageQueue.ObjectMessagesQueue.Cast<SequencedMessage>(),
                message => message.content.AsSpan().IndexOf(speedOn) >= 0);
            Assert.DoesNotContain(caster.messageQueue.ObjectMessagesQueue.Cast<SequencedMessage>(),
                message => message.content.AsSpan().IndexOf(speedOn) >= 0);
            TimedEffects.RunDue(buff.Started + 599.99);
            Assert.True(AbilityBuffs.IsActive(target, 62));
            TimedEffects.RunDue(buff.Started + 600.01);
            Assert.Equal(0, AbilityStats.Player(target, 115));
            Assert.Equal(0f, BitConverter.ToSingle(target.playerInstance.MovementScale.getValue()));
            Assert.Contains(target.messageQueue.ObjectMessagesQueue.Cast<SequencedMessage>(),
                message => message.content.AsSpan().IndexOf(speedOff) >= 0);
        }
        finally
        {
            AbilityBuffs.RemoveAll(target);
            WorldServer.Clients = previousClients;
        }
    }

    [Theory]
    [InlineData(1078, 4, 10, 0)]
    [InlineData(1080, 9, 12, 0)]
    [InlineData(1085, 4, 0, 10)]
    public void ClassUpgradesModifyViralAccuracyConcentrationAndTheirDistinctConsumer(ushort id, int accuracy, int regen, int radius)
    {
        var player = Client();
        AbilitySupport.Land(player, id, 2);
        Assert.Equal(accuracy, AbilityStats.ViralAccuracy(player));
        Assert.Equal(78, AbilityCastInterrupts.CastThroughPercent(0, 1000, (short)AbilityStats.Influence(player, 564)));
        Assert.Equal(regen, AbilityStats.Influence(player, 114));
        Assert.Equal((ushort)108, AbilityHandler.RegenerateInnerStrength(player));
        Assert.Equal((ushort)(116 + (regen == 12 ? 1 : regen == 10 ? 1 : 0)), AbilityHandler.RegenerateInnerStrength(player));
        Assert.Equal(4 * (1 + radius / 100.0), AbilityRadius.TargetMetres(player, 60), 3);
        Assert.True(AbilityCancellation.Dismiss(player, id));
        Assert.Equal(0, AbilityStats.ViralAccuracy(player));
        Assert.Equal(0, AbilityStats.Influence(player, 564));
    }

    [Theory]
    [InlineData(64, 1078)]
    [InlineData(67, 1085)]
    public void LoadedDisciplineGrantsTrackMembershipWhenTheParentUnloads(ushort discipline, ushort grant)
    {
        var player = Client();
        AbilityPassives.SetLoaded(player, new[] { (discipline, (ushort)50) });
        Assert.True(AbilityPassives.IsLoaded(player, grant));
        Assert.Equal(discipline == 64 ? 1 : 0, AbilityPassives.Total(player, 1147));
        AbilityPassives.SetLoaded(player, Array.Empty<(ushort, ushort)>());
        Assert.False(AbilityPassives.IsLoaded(player, grant));
        Assert.Equal(0, AbilityPassives.Total(player, 1147));
    }

    [Fact]
    public void ResistContagion73ChangesViralAccuracyAndOutgoingStunDuration()
    {
        var player = Client();
        AbilityPassives.SetLoaded(player, new[] { ((ushort)73, (ushort)50) });
        Assert.Equal(7, AbilityStats.ViralAccuracy(player));
        Assert.Equal(10.5f, AbilityControlDurations.Scale(player, TempMods.StunAbility, 10), 3);
        AbilityPassives.SetLoaded(player, Array.Empty<(ushort, ushort)>());
        Assert.Equal(10f, AbilityControlDurations.Scale(player, TempMods.StunAbility, 10));
    }

    [Theory]
    [InlineData(71, 5)]
    [InlineData(409, 0)]
    public void LoadedHealingBonusesReachTheHealthMutation(ushort id, int sweep)
    {
        var player = Client();
        AbilityPassives.SetLoaded(player, new[] { (id, (ushort)50) });
        Assert.Equal(sweep, AbilityPassives.Total(player, 1052));
        Assert.Equal(id == 409 ? 5 : 0, AbilityStats.Defense(player, false));
        Assert.Equal(id == 409 ? 5 : 0, AbilityStats.Defense(player, true));
        AbilitySupport.Land(player, 413, 2);
        Assert.Equal(473, PvpCombat.HealthOf(player));
        AbilityPassives.SetLoaded(player, Array.Empty<(ushort, ushort)>());
        AbilitySupport.Land(player, 413, 2);
        Assert.Equal(543, PvpCombat.HealthOf(player));
    }

    [Fact]
    public void PassiveCode170SnapshotsCasterLevelAndMagnifiesARealHeal()
    {
        var caster = Client(20);
        var receiver = Client();
        Assert.True(AbilityBuffs.Apply(receiver, 170, caster));
        AbilitySupport.Land(receiver, 413, 2);
        Assert.Equal(477, PvpCombat.HealthOf(receiver));
        var buff = receiver.playerData.activeBuffs[170];
        TimedEffects.RunDue(buff.Started + 20.01);
        AbilitySupport.Land(receiver, 413, 2);
        Assert.Equal(547, PvpCombat.HealthOf(receiver));
    }

    [Theory]
    [InlineData(39, 50, 600)]
    [InlineData(417, 500, 300)]
    public void HealthBuffsChangeTheMaximumWithoutHealingAndExpire(ushort id, int delta, double duration)
    {
        var player = Client();
        AbilitySupport.Land(player, id, 2);
        Assert.Equal(1000 + delta, NumericalUtils.ByteArrayToUint16(player.playerInstance.MaxHealth.getValue(), 1));
        Assert.Equal(400, PvpCombat.HealthOf(player));
        TimedEffects.RunDue(player.playerData.activeBuffs[id].Started + duration + .01);
        Assert.Equal(1000, NumericalUtils.ByteArrayToUint16(player.playerInstance.MaxHealth.getValue(), 1));
    }

    [Theory]
    [InlineData(47, -30)]
    [InlineData(48, -20)]
    public void FreezePacifyEndsOnDamageWhileKeepingTwelveSecondImmunity(ushort id, int resistance)
    {
        var player = Client();
        Assert.True(TimedEffects.ApplyToPlayer(player, id, "Enemy"));
        var subject = TimedEffects.ForPlayer(player);
        var pacify = Assert.Single(subject.Effects.Snapshot(), m => m.Target == TempMods.MezAbility);
        Assert.Equal(6, pacify.Seconds);
        Assert.False(ControlActions.CanUseAbilities(player));
        Assert.Equal(resistance, AbilityStats.Player(player, 157));
        TimedEffects.HealthChanged(subject, -1, true);
        Assert.False(ControlActions.CanUseAbilities(player));
        TimedEffects.HealthChanged(subject, 1, true, damageEvent: true);
        Assert.True(ControlActions.CanUseAbilities(player));
        Assert.True(subject.Effects.HasControlImmunity(pacify.Start + 11.99));
        Assert.False(subject.Effects.HasControlImmunity(pacify.Start + 12.01));
    }

    [Fact]
    public void AreaDisruption38CreatesTheActionGateAndSeparateImmunity()
    {
        var player = Client();
        Assert.True(TimedEffects.ApplyToPlayer(player, 38, "Enemy"));
        var subject = TimedEffects.ForPlayer(player);
        var stun = Assert.Single(subject.Effects.Snapshot(), m => m.Target == TempMods.StunAbility);
        Assert.Equal(3, stun.Seconds);
        Assert.False(ControlActions.CanMove(player));
        TimedEffects.RunDue(stun.Start + 3.01);
        Assert.True(ControlActions.CanMove(player));
        Assert.True(subject.Effects.HasControlImmunity(stun.Start + 7.99));
        Assert.False(subject.Effects.HasControlImmunity(stun.Start + 8.01));
    }

    [Fact]
    public void CodeFreeze41ChangesMovementAndItsForceCombatInput()
    {
        var player = Client();
        Assert.True(TempMods.TryGet(41, out var effect));
        var subject = TimedEffects.ForPlayer(player);
        var mods = TimedEffects.Apply(subject, effect, TimeUtils.GetSimTimeSeconds());
        var root = Assert.Single(mods, m => m.Target == TempMods.RootedAbility);
        Assert.Equal(19, root.Seconds);
        Assert.False(ControlActions.CanMove(player));
        Assert.Equal(80, CombatInsightEngagement.Calculate(0, AbilityStats.Player(player, 519), 0, 0, 0).Force);
        TimedEffects.RunDue(root.Start + 19.01);
        Assert.True(ControlActions.CanMove(player));
        Assert.Equal(0, AbilityStats.Player(player, 519));
    }

    [Theory]
    [InlineData(65, -24, 33)]
    [InlineData(701, -14, 5)]
    public void HostileNumericEffectsFeedTheirOwnCombatConsumers(ushort id, int delta, int duration)
    {
        var player = Client();
        Assert.True(TimedEffects.ApplyToPlayer(player, id, "Enemy"));
        int Check(bool ranged) => id == 65 ? AbilityStats.Toughness(player, ranged) : AbilityStats.Defense(player, ranged);
        Assert.Equal(delta, Check(false));
        Assert.Equal(delta, Check(true));
        double start = TimedEffects.ForPlayer(player).Effects.Snapshot().First(m => m.Parent == id).Start;
        TimedEffects.RunDue(start + duration + .01);
        Assert.Equal(0, Check(false));
        Assert.Equal(0, Check(true));
    }

    [Theory]
    [InlineData(61, 3)]
    [InlineData(89, 12)]
    public void AuraDrainConsumesExactStrengthAndDeactivatesWhenNextPaymentCannotBeMade(ushort id, ushort rate)
    {
        var player = Client();
        player.playerInstance.InnerStrengthAvailable.setValue((ushort)(rate + 1));
        var previousMobs = WorldServer.mobs;
        var previousClients = WorldServer.Clients;
        WorldServer.mobs = new();
        WorldServer.Clients = new();
        try
        {
            AbilityAuras.Start(player, id);
            Assert.True(AbilityAuras.IsOn(player, id));
            Assert.True(AbilityAuras.PayDrain(player, id));
            Assert.Equal(1, AbilityHandler.CurrentInnerStrength(player));
            Assert.False(AbilityAuras.PayDrain(player, id));
            Assert.False(AbilityAuras.IsOn(player, id));
            Assert.Equal(1, AbilityHandler.CurrentInnerStrength(player));
        }
        finally
        {
            AbilityAuras.Stop(player, id);
            WorldServer.mobs = previousMobs;
            WorldServer.Clients = previousClients;
        }
    }

    [Theory]
    [InlineData(53)]
    [InlineData(60)]
    [InlineData(415)]
    [InlineData(694)]
    public void ViralEffectsReadActualResistanceWhenCombatDamageIsResolved(ushort id)
    {
        var caster = Client();
        var target = Client();
        Assert.True(AbilityBuffs.Apply(target, 8));
        Assert.Equal(82, TypedAbilityResistance.Player(caster, target, id, 100));
        Assert.True(AbilityBuffs.Remove(target, 8));
        Assert.Equal(100, TypedAbilityResistance.Player(caster, target, id, 100));
    }
}
