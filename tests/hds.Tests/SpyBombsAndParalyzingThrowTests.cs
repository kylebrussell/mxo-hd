using System.Reflection;
using System.Runtime.CompilerServices;
using hds;
using hds.shared;

namespace hds.Tests;

[Collection("DelayedActions")]
public class SpyBombsAndParalyzingThrowTests
{
    private const ushort SpyStyle = CombatStyles.SpyStyle;
    private const ushort FlashBomb = SpyGroundBombs.FlashBomb;        // 107
    private const ushort SmokeBomb = SpyGroundBombs.SmokeBomb;        // 364
    private const ushort ParalyzingThrow = ParalyzingThrowDamage.Id;  // 1135
    private const byte FixtureLevel = 50, FixtureRank = 50;

    private static WorldClient Client(byte level = FixtureLevel)
    {
        var client = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        client.playerData = new ClientData();
        client.playerData.setCharID(110);
        client.playerData.setDistrictId(1);
        client.playerData.setOnWorld(true);
        client.playerInstance = new PlayerCharacter();
        client.playerInstance.Level.setValue(level);
        client.playerInstance.Health.setValue((ushort)3000);
        client.playerInstance.MaxHealth.setValue((ushort)3000);
        client.playerInstance.InnerStrengthMax.setValue((ushort)2000);
        client.playerInstance.InnerStrengthAvailable.setValue((ushort)2000);
        client.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(24499, 495, -1750));
        client.messageQueue = new MessageQueue();
        typeof(WorldClient).GetField("lastFlushTick", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(client, long.MaxValue);
        typeof(WorldClient).GetProperty("IsBot")!.SetValue(client, true);
        typeof(WorldClient).GetField("alive", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(client, true);
        typeof(WorldClient).GetField("<TransportFence>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(client, new WorldSendFence());
        client.playerData.isBot = true;
        return client;
    }

    private static byte[] BodyWithGround(ushort id, double x, double y, double z)
    {
        byte[] body = new byte[41];
        BitConverter.GetBytes(id).CopyTo(body, 0);
        BitConverter.GetBytes(x).CopyTo(body, 10);
        BitConverter.GetBytes(y).CopyTo(body, 18);
        BitConverter.GetBytes(z).CopyTo(body, 26);
        return body;
    }

    [Fact]
    public void GroundBombsParseGroundCoordinatesEnforceRangeAndRunSmokeBombVanish()
    {
        Assert.True(SpyGroundBombs.Handles(FlashBomb));
        Assert.True(SpyGroundBombs.Handles(SmokeBomb));
        Assert.False(SpyGroundBombs.Handles(ParalyzingThrow));

        byte[] near = BodyWithGround(FlashBomb, 24499, 495, -1498);
        Assert.True(SpyGroundBombs.TryReadGroundTarget(near, out double gx, out double gy, out double gz));
        Assert.Equal(24499, gx);
        Assert.Equal(495, gy);
        Assert.Equal(-1498, gz);

        // Zero / subnormal stack garbage falls back cleanly without throwing.
        byte[] zeros = new byte[41];
        Assert.False(SpyGroundBombs.TryReadGroundTarget(zeros, out _, out _, out _));

        var actor = Client();
        Assert.False(SpyGroundBombs.TryRefuse(actor, FlashBomb, near, out _));
        Assert.False(SpyGroundBombs.TryRefuse(actor, SmokeBomb, near, out _));

        byte[] tooFar = BodyWithGround(FlashBomb, 24499, 495, -4000); // 22.5 m > 16 m
        Assert.True(SpyGroundBombs.TryRefuse(actor, FlashBomb, tooFar, out uint reason));
        Assert.Equal(AbilityRefusals.TargetOutOfRange, reason);

        // SmokeBombInit: with GOID 460 -> vanishes into 1116 Invisibility after 0.5 s; without GOID 460 -> does not vanish.
        AbilityPassives.SetLoaded(actor, new (ushort, ushort)[] { (175, FixtureRank) });
        double now = TimeUtils.GetSimTimeSeconds();
        SpyGroundBombs.ScheduleSmokeBombVanish(actor, _ => false);
        DelayedActions.RunDue(now + 0.6);
        Assert.Null(Stealth.Current(actor));

        now = TimeUtils.GetSimTimeSeconds();
        SpyGroundBombs.ScheduleSmokeBombVanish(actor, _ => true);
        Assert.Null(Stealth.Current(actor));
        DelayedActions.RunDue(now + 0.6);
        var stealth = Stealth.Current(actor);
        Assert.NotNull(stealth);
        Assert.Equal(Stealth.InvisibilityGrantAbility, stealth!.Def.Ability);
        Assert.Equal(130f, stealth.Seconds);
        Stealth.Cleared(actor);
    }

    [Fact]
    public void ParalyzingThrowResolvesShipped30BaseWhilePreservingTypedResistanceQaOverride()
    {
        var ability = new AbilityItem();
        ability.setAbilityID(ParalyzingThrow);
        Assert.True(SpyAbilityRegistry.TryResolveDamage(AbilityCatalog.Find(ParalyzingThrow)!, FixtureLevel, new Random(0), out ushort baseDmg));
        Assert.Equal((ushort)30, baseDmg);
        Assert.Equal((ushort)30, AbilityHandler.AbilityDamage(ability, new Random(0), allowTypedResistanceQa: false, characterLevel: FixtureLevel));

        var actor = Client();
        Assert.True(SpyAbilityRegistry.TryRefuse(actor, ParalyzingThrow, 0, null, out uint reason));
        Assert.Equal(AbilityRefusals.InvalidTarget, reason);
    }

    [Fact]
    public void BatchFixtureMemoryScaledDamageEffectsAndCooldownsAreExact()
    {
        var plan = NativeQa.NativeQaModes.Find("ability-parity-spy-bombs-and-paralyzing-throw").Plan;
        Assert.Equal("ability-sweep-melee.csv", plan.TestMobFixture);
        Assert.Equal("1", plan.EnvironmentOverrides[CombatFixtureWeapons.ResetMode]);
        Assert.Equal(new ushort[] { SpyStyle, FlashBomb, SmokeBomb, 1132, ParalyzingThrow }, plan.HotbarAbilities);

        int mem = plan.Abilities.Sum(AbilityMemory.Cost);
        Assert.Equal(80, mem);
        Assert.True(mem <= AbilityMemory.ForLevel(FixtureLevel));

        var client = Client(FixtureLevel);
        AbilityPassives.SetLoaded(client, plan.Abilities.Select(id => (id, (ushort)FixtureRank)).ToArray());
        CombatStyles.Select(client, SpyStyle);

        int thrownInfluence = AbilityStats.Influence(client, 1034);
        Assert.Equal(54, thrownInfluence);
        Assert.Equal((ushort)46, AbilityStats.ScaleAbilityDamage(client, ParalyzingThrow, 30));

        Assert.Equal((ushort)25, AbilityCosts.Cost(FlashBomb));
        Assert.True(AbilityCooldowns.TryGet(FlashBomb, out var cd107));
        Assert.Equal(58f, cd107.Seconds);
        Assert.Equal((ushort)1216, cd107.Group);
        Assert.Equal(3f, cd107.GroupSeconds);
        Assert.True(TempMods.TryGet(FlashBomb, out var e107));
        Assert.Contains(e107.Mods, m => m.Target == TempMods.BlindAbility && m.Seconds == 12f);

        Assert.Equal((ushort)120, AbilityCosts.Cost(SmokeBomb));
        Assert.True(AbilityCooldowns.TryGet(SmokeBomb, out var cd364));
        Assert.Equal(60f, cd364.Seconds);
        Assert.True(TempMods.TryGet(SmokeBomb, out var e364));
        Assert.Contains(e364.Mods, m => m.Target == TempMods.BlindAbility && m.Seconds == 20f);

        Assert.Equal((ushort)30, AbilityCosts.Cost(ParalyzingThrow));
        Assert.True(AbilityCooldowns.TryGet(ParalyzingThrow, out var cd1135));
        Assert.Equal(6f, cd1135.Seconds);
        Assert.Equal((ushort)1216, cd1135.Group);
        Assert.Equal(3f, cd1135.GroupSeconds);
    }
}
