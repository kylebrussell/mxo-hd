using System.Reflection;
using System.Runtime.CompilerServices;
using hds;
using hds.world.Structures;

namespace hds.Tests;

[Collection("ConcentrationEnvironment")]
public class HackerBatchB3ParityTests
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
    public void Physician418GrantAndHeal1098ChangeOnlyEligibleRecipients()
    {
        var caster = Client();
        var member = Client(character: 6101);
        var outOfRange = Client(character: 6102);
        var outsider = Client(character: 6103);
        member.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(400, 495, 0));
        outOfRange.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(401, 495, 0));
        var previous = WorldServer.Clients;
        WorldServer.Clients = new() { ["caster"] = caster, ["member"] = member, ["far"] = outOfRange, ["outsider"] = outsider };
        var team = SocialGroups.Instance.CreateTeam(6100, "caster");
        try
        {
            SocialGroups.Instance.AddTeamMember(team.Id, 6101, "member");
            SocialGroups.Instance.AddTeamMember(team.Id, 6102, "far");
            AbilityPassives.SetLoaded(caster, new[] { ((ushort)418, (ushort)50) });
            Assert.True(AbilityPassives.IsLoaded(caster, 1098));
            AbilitySupport.Land(caster, 1098, 2);
            Assert.Equal(588, PvpCombat.HealthOf(caster));
            Assert.Equal(588, PvpCombat.HealthOf(member));
            Assert.Equal(400, PvpCombat.HealthOf(outOfRange));
            Assert.Equal(400, PvpCombat.HealthOf(outsider));

            AbilityPassives.SetLoaded(caster, Array.Empty<(ushort, ushort)>());
            Assert.False(AbilityPassives.IsLoaded(caster, 1098));
            AbilitySupport.Land(caster, 413, 2);
            Assert.Equal(658, PvpCombat.HealthOf(caster));
        }
        finally
        {
            SocialGroups.Instance.DisbandTeam(team.Id);
            WorldServer.Clients = previous;
        }
    }

    [Fact]
    public void Ravager470GrantsTheDowngradeAndChangesStrengthMaximumAndRegeneration()
    {
        var caster = Client();
        AbilityPassives.SetLoaded(caster, new[] { ((ushort)470, (ushort)50) });
        Assert.True(AbilityPassives.IsLoaded(caster, 1081));
        Assert.Equal(210, AbilityHandler.MaxInnerStrength(caster));
        Assert.Equal((ushort)108, AbilityHandler.RegenerateInnerStrength(caster));
        Assert.Equal((ushort)116, AbilityHandler.RegenerateInnerStrength(caster));
        Assert.Equal((ushort)125, AbilityHandler.RegenerateInnerStrength(caster));

        AbilityPassives.SetLoaded(caster, Array.Empty<(ushort, ushort)>());
        Assert.False(AbilityPassives.IsLoaded(caster, 1081));
        Assert.Equal(200, AbilityHandler.MaxInnerStrength(caster));
        Assert.Equal((ushort)133, AbilityHandler.RegenerateInnerStrength(caster));
    }

    [Fact]
    public void AttackProgramming471StrengthRegenConsumesItsLoadedBonus()
    {
        var caster = Client();
        AbilityPassives.SetLoaded(caster, new[] { ((ushort)471, (ushort)50) });
        Assert.Equal(5, AbilityPassives.Total(caster, 1053));
        Assert.Equal((ushort)109, AbilityHandler.RegenerateInnerStrength(caster));
        AbilityPassives.SetLoaded(caster, Array.Empty<(ushort, ushort)>());
        Assert.Equal(0, AbilityPassives.Total(caster, 1053));
        Assert.Equal((ushort)117, AbilityHandler.RegenerateInnerStrength(caster));
    }

    [Theory]
    [InlineData(true, 30, 38, 52)]
    [InlineData(false, 15, 40, 46)]
    public void AttackProgrammingChangesRestingRegenAndEvadeCostInItsRankedProfile(
        bool programmingLoaded, int regen, ushort evadeCost, int fiveTickIncrease)
    {
        var caster = Client();
        var profile = new NativeQa.Modes.HackerAttackProgrammingEfficientCodeMode().Plan.Abilities;
        AbilityPassives.SetLoaded(caster, profile.Where(id => programmingLoaded || id != 471).Select(id => (id, (ushort)50)));

        Assert.Equal(regen, AbilityStats.Influence(caster, 114));
        Assert.Equal(evadeCost, AbilityCosts.Effective(caster, 217));
        Assert.Equal(14, AbilityCosts.Effective(caster, 38));
        for (int tick = 0; tick < 5; tick++) AbilityHandler.RegenerateInnerStrength(caster);
        Assert.Equal(100 + fiveTickIncrease, AbilityHandler.CurrentInnerStrength(caster));
    }

    [Fact]
    public void EfficientCodeRoundTripsItsIndependentAccuracyDamageAndStunConsumers()
    {
        var caster = Client();
        var profile = new NativeQa.Modes.HackerAttackProgrammingEfficientCodeMode().Plan.Abilities
            .Select(id => (id, (ushort)50)).ToArray();
        var archived = profile.Where(row => row.id != 477).ToArray();
        AbilityPassives.SetLoaded(caster, archived);
        int meleeAccuracy = AbilityStats.Attack(caster, false), rangedAccuracy = AbilityStats.Attack(caster, true);
        int meleeDamage = AbilityStats.ScaleTypedAbilityDamage(caster, 0, 100);
        int rangedDamage = AbilityStats.ScaleTypedAbilityDamage(caster, 1, 100);
        Assert.Equal(3.15f, AbilityControlDurations.Scale(caster, TempMods.StunAbility, 3), 3);

        AbilityPassives.SetLoaded(caster, profile);
        Assert.Equal(meleeAccuracy + 5, AbilityStats.Attack(caster, false));
        Assert.Equal(rangedAccuracy + 5, AbilityStats.Attack(caster, true));
        Assert.Equal(meleeDamage + 10, AbilityStats.ScaleTypedAbilityDamage(caster, 0, 100));
        Assert.Equal(rangedDamage + 10, AbilityStats.ScaleTypedAbilityDamage(caster, 1, 100));
        Assert.Equal(3.30f, AbilityControlDurations.Scale(caster, TempMods.StunAbility, 3), 3);

        AbilityPassives.SetLoaded(caster, archived);
        Assert.Equal(meleeAccuracy, AbilityStats.Attack(caster, false));
        Assert.Equal(rangedAccuracy, AbilityStats.Attack(caster, true));
        Assert.Equal(meleeDamage, AbilityStats.ScaleTypedAbilityDamage(caster, 0, 100));
        Assert.Equal(rangedDamage, AbilityStats.ScaleTypedAbilityDamage(caster, 1, 100));
        Assert.Equal(3.15f, AbilityControlDurations.Scale(caster, TempMods.StunAbility, 3), 3);
    }

    [Theory]
    [InlineData(472, false, 60, 49)]
    [InlineData(474, false, 72, 59)]
    [InlineData(474, true, 73, 60)]
    [InlineData(475, false, 52, 42)]
    public void CapturedViralDotsTickElevenTimesAndResistanceIsReadAtTheTick(ushort id, bool highRoll, int plain, int resisted)
    {
        var caster = Client();
        var target = Client(character: 6101);
        target.playerInstance.Health.setValue((ushort)1000);
        var subject = TimedEffects.ForPlayer(target);
        Assert.True(TempMods.TryGet(id, out var effect));
        var dot = Assert.Single(TimedEffects.Apply(subject, effect, TimeUtils.GetSimTimeSeconds(),
            caster, "Caster", rng: new CaptureRoll(highRoll)));
        Assert.Equal(2, dot.DamageType);
        Assert.Equal(30, dot.Seconds);

        TimedEffects.RunDue(dot.Start + 2.499);
        Assert.Equal(1000, PvpCombat.HealthOf(target));
        TimedEffects.RunDue(dot.Start + 2.501);
        Assert.Equal(1000 - plain, PvpCombat.HealthOf(target));
        byte[] tickFx = BitConverter.GetBytes(TempMods.VirusCrashFx);
        Assert.Contains(target.messageQueue.ObjectMessagesQueue.Cast<SequencedMessage>(),
            message => message.content.AsSpan().IndexOf(tickFx) >= 0);

        Assert.True(AbilityBuffs.Apply(target, 8));
        TimedEffects.RunDue(dot.Start + 5.001);
        Assert.Equal(1000 - plain - resisted, PvpCombat.HealthOf(target));
        Assert.True(AbilityBuffs.Remove(target, 8));
        for (int tick = 3; tick <= 11; ++tick)
        {
            TimedEffects.RunDue(dot.Start + 2.5 * tick + .001);
            Assert.Equal(1000 - plain * (tick - 1) - resisted, PvpCombat.HealthOf(target));
        }

        TimedEffects.RunDue(dot.Start + 30.001);
        Assert.False(subject.Effects.HasParent(id));
        Assert.Equal(1000 - 10 * plain - resisted, PvpCombat.HealthOf(target));
    }

    [Fact]
    public void FreezeSystem473PacifiesAndAppliesItsViralVulnerability()
    {
        var caster = Client();
        var player = Client(character: 6101);
        Assert.True(TimedEffects.ApplyToPlayer(player, 473, "Caster", caster));
        var subject = TimedEffects.ForPlayer(player);
        var pacify = Assert.Single(subject.Effects.Snapshot(), m => m.Target == TempMods.MezAbility);
        Assert.Equal(4, pacify.Seconds);
        Assert.Equal(-80, AbilityStats.Player(player, 157));
        AssertNotice(caster, AbilityMessages.ImmunityCaster);
        AssertNotice(player, AbilityMessages.ImmunityTarget);
        Assert.False(ControlActions.CanUseAbilities(player));
        TimedEffects.HealthChanged(subject, -1, true);
        Assert.False(ControlActions.CanUseAbilities(player));
        TimedEffects.HealthChanged(subject, 1, true, damageEvent: true);
        Assert.True(ControlActions.CanUseAbilities(player));
        Assert.True(subject.Effects.HasControlImmunity(pacify.Start + 7.99));
        TimedEffects.RunDue(pacify.Start + 8.01);
        Assert.False(subject.Effects.HasControlImmunity(pacify.Start + 8.01));
        Assert.Equal(0, AbilityStats.Player(player, 157));
    }

    [Theory]
    [InlineData(473, 400)]
    [InlineData(475, 500)]
    public void HostileAreaExpandsFromTheSelectedOpponentAndChecksItsOwnRadius(ushort id, int radius)
    {
        var caster = Client();
        caster.playerInstance.OrganizationID.setValue((byte)1);
        var previousClients = WorldServer.Clients;
        var previousMobs = WorldServer.mobs;
        WorldServer.Clients = new() { ["caster"] = caster };
        WorldServer.mobs = new();

        WorldClient Opponent(uint character, int x, int y = 495)
        {
            var player = Client(character: character);
            player.playerInstance.OrganizationID.setValue((byte)2);
            player.playerData.pvpFlag = true;
            player.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(x, y, 0));
            caster.viewMan.addView(character, 1).viewCreated = true;
            WorldServer.Clients.Add(character.ToString(), player);
            return player;
        }

        try
        {
            var primary = Opponent(6101, 800);
            var edge = Opponent(6102, 800 + radius);
            var outside = Opponent(6103, 801 + radius);
            var otherFloor = Opponent(6104, 800, 645);
            var ability = new AbilityItem();
            ability.setAbilityID(id);
            Assert.True(PvpCombat.LandAbility(caster, ability, caster.viewMan.FindViewForEntity(6101).ViewID));
            Assert.True(TimedEffects.ForPlayer(primary).Effects.HasParent(id));
            Assert.True(TimedEffects.ForPlayer(edge).Effects.HasParent(id));
            Assert.False(TimedEffects.ForPlayer(outside).Effects.HasParent(id));
            Assert.False(TimedEffects.ForPlayer(otherFloor).Effects.HasParent(id));
            Assert.Equal(400, PvpCombat.HealthOf(primary));
            Assert.Equal(400, PvpCombat.HealthOf(edge));
        }
        finally
        {
            foreach (var player in WorldServer.Clients.Values)
                TimedEffects.Clear(TimedEffects.ForPlayer(player));
            WorldServer.Clients = previousClients;
            WorldServer.mobs = previousMobs;
        }
    }

    [Fact]
    public void DestroyHealth476ClampsTheLiveMaximumAndChangesActualRegenUntilExpiry()
    {
        var player = Client();
        player.playerInstance.Health.setValue((ushort)900);
        Assert.True(TimedEffects.ApplyToPlayer(player, 476, "Caster"));
        var effect = TimedEffects.ForPlayer(player).Effects.Snapshot().First(m => m.Parent == 476);
        Assert.Equal(480, NumericalUtils.ByteArrayToUint16(player.playerInstance.MaxHealth.getValue(), 1));
        Assert.Equal(480, PvpCombat.HealthOf(player));
        player.playerInstance.Health.setValue((ushort)400);
        HealthRegen.Tick(player, 1);
        Assert.Equal(404, PvpCombat.HealthOf(player));
        TimedEffects.RunDue(effect.Start + 40.01);
        Assert.Equal(1000, NumericalUtils.ByteArrayToUint16(player.playerInstance.MaxHealth.getValue(), 1));
        HealthRegen.Tick(player, 1);
        Assert.Equal(414, PvpCombat.HealthOf(player));
    }

    [Fact]
    public void RavagerDowngrade1081DispelsAnUpgradeAndSeparatelyAppliesConfuseAndDefense()
    {
        var caster = Client();
        var target = Client(character: 6101);
        Assert.True(AbilityBuffs.Apply(target, 46));
        var ability = new AbilityItem();
        ability.setAbilityID(1081);
        PvpCombat.LandAbilityOn(caster, ability, target);
        Assert.False(AbilityBuffs.IsActive(target, 46));
        var subject = TimedEffects.ForPlayer(target);
        var confuse = Assert.Single(subject.Effects.Snapshot(), m => m.Target == TempMods.ConfuseAbility);
        Assert.False(ControlActions.CanChangeTactic(subject.Effects));
        Assert.Equal(-12, AbilityStats.Player(target, 183));
        TimedEffects.RunDue(confuse.Start + 8.01);
        Assert.True(ControlActions.CanChangeTactic(subject.Effects));
        Assert.Equal(-12, AbilityStats.Player(target, 183));
        TimedEffects.RunDue(confuse.Start + 30.01);
        Assert.Equal(0, AbilityStats.Player(target, 183));
        caster.messageQueue.RPCMessagesQueue.Clear();
        PvpCombat.LandAbilityOn(caster, ability, target);
        Assert.Empty(AbilityHostile.RemovableBuffs(target));
        byte[] failure = BitConverter.GetBytes(AbilityHostile.CancelUpgradeFailCaster);
        Assert.Contains(caster.messageQueue.RPCMessagesQueue.Cast<SequencedMessage>(),
            message => message.content.AsSpan().IndexOf(failure) >= 0);
    }

    [Fact]
    public void DisruptInputs43ReducesAttackUntilTheSixteenSecondBlindEnds()
    {
        var player = Client();
        Assert.True(TimedEffects.ApplyToPlayer(player, 43, "Caster"));
        var subject = TimedEffects.ForPlayer(player);
        var blind = Assert.Single(subject.Effects.Snapshot(), m => m.Target == TempMods.BlindAbility);
        Assert.Equal(16, blind.Seconds);
        Assert.Equal(-30, AbilityStats.Attack(player, false));
        Assert.Equal(-30, AbilityStats.Attack(player, true));
        TimedEffects.RunDue(blind.Start + 16.01);
        Assert.Equal(0, AbilityStats.Attack(player, false));
        Assert.Equal(0, AbilityStats.Attack(player, true));
    }
    private static void AssertNotice(WorldClient player, uint stringId)
    {
        byte[] wireId = BitConverter.GetBytes(stringId);
        Assert.Contains(player.messageQueue.RPCMessagesQueue.Cast<SequencedMessage>(),
            message => message.content.AsSpan().IndexOf(wireId) >= 0);
    }

    private sealed class CaptureRoll(bool high) : Random
    {
        public override int Next(int minValue, int maxValue) => high ? maxValue - 1 : minValue;
    }
}
