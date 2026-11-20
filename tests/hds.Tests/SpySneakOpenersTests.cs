using System.Runtime.CompilerServices;
using hds;
using hds.shared;

namespace hds.Tests;

[Collection("DelayedActions")]
public class SpySneakOpenersTests
{
    private const ushort SpyStyle = CombatStyles.SpyStyle;
    private const ushort DropKick = 153, SpinThrow = 286, TopsyTurvy = 288, Punt = 289,
        LegThrow = 1131, WeakeningBlow = 1133;
    private const byte FixtureLevel = 40, FixtureRank = 40;

    private static WorldClient Client(byte level = FixtureLevel)
    {
        var client = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        client.playerData = new ClientData();
        client.playerData.setDistrictId(1);
        client.playerData.setOnWorld(true);
        client.playerInstance = new PlayerCharacter();
        client.playerInstance.Level.setValue(level);
        client.playerInstance.Health.setValue((ushort)3000);
        client.playerInstance.MaxHealth.setValue((ushort)3000);
        client.playerInstance.InnerStrengthMax.setValue((ushort)2000);
        client.playerInstance.InnerStrengthAvailable.setValue((ushort)2000);
        client.messageQueue = new MessageQueue();
        typeof(WorldClient).GetField("lastFlushTick", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(client, long.MaxValue);
        return client;
    }

    [Theory]
    [InlineData(DropKick, 52, 150, 20, 30f, 691, 15f, false)]
    [InlineData(SpinThrow, 80, 300, 50, 38f, 691, 15f, false)]
    [InlineData(TopsyTurvy, 37, 775, 60, 38f, 691, 15f, false)]
    [InlineData(Punt, 140, 600, 40, 38f, 691, 15f, false)]
    [InlineData(LegThrow, 153, 0, 25, 45f, 691, 15f, true)]
    [InlineData(WeakeningBlow, 113, 0, 35, 45f, 691, 15f, true)]
    public void SpySneakOpenersResolveShippedThrownBaseAndEnforceAdmission(
        ushort ability, ushort expectedBase, int expectedStealthBonus, ushort expectedIs,
        float expectedOwn, ushort expectedGroup, float expectedGroupSec, bool retainsStealth)
    {
        var entry = AbilityCatalog.Find(ability)!;
        Assert.True(SpyAbilityRegistry.TryResolveDamage(entry, FixtureLevel, new Random(0), out ushort baseDamage));
        Assert.Equal(expectedBase, baseDamage);
        Assert.Equal(expectedStealthBonus, SneakAttacks.Bonus(ability, true));
        Assert.Equal(retainsStealth, SpyAbilityRegistry.RetainsStealthDuringCast(ability));

        Assert.Equal(expectedIs, AbilityCosts.Cost(ability));
        Assert.True(AbilityCooldowns.TryGet(ability, out var cd));
        Assert.Equal(expectedOwn, cd.Seconds);
        Assert.Equal(expectedGroup, cd.Group);
        Assert.Equal(expectedGroupSec, cd.GroupSeconds);
        Assert.Equal(SpyStyle, CombatStyles.RequiredStyle(ability));

        var client = Client();
        Assert.True(SpyAbilityRegistry.TryRefuse(client, ability, 0, null, out uint reason));
        Assert.Equal(AbilityRefusals.InvalidTarget, reason);
    }

    [Fact]
    public void FullLevel40ClosureMemoryScaledDamagesAndEffectsAreExact()
    {
        var plan = NativeQa.NativeQaModes.Find("ability-parity-spy-sneak-openers").Plan;
        Assert.Equal("ability-sweep-melee.csv", plan.TestMobFixture);
        int mem = plan.Abilities.Sum(AbilityMemory.Cost);
        Assert.True(mem <= AbilityMemory.ForLevel(FixtureLevel),
            $"Level-40 Spy sneak batch memory {mem} must fit capacity {AbilityMemory.ForLevel(FixtureLevel)}");

        Assert.Equal("1", plan.EnvironmentOverrides["MXO_COMBAT_FIXTURE_DISENGAGE"]);
        Assert.Equal(55, mem);

        var client = Client(FixtureLevel);
        AbilityPassives.SetLoaded(client, plan.Abilities.Select(id => (id, (ushort)FixtureRank)).ToArray());
        CombatStyles.Select(client, SpyStyle);

        Assert.Equal(32, AbilityStats.Influence(client, 1034));
        Assert.Equal((ushort)266, AbilityStats.ScaleAbilityDamage(client, DropKick, SneakAttacks.Add(52, DropKick, true)));
        Assert.Equal((ushort)501, AbilityStats.ScaleAbilityDamage(client, SpinThrow, SneakAttacks.Add(80, SpinThrow, true)));
        Assert.Equal((ushort)1071, AbilityStats.ScaleAbilityDamage(client, TopsyTurvy, SneakAttacks.Add(37, TopsyTurvy, true)));
        Assert.Equal((ushort)976, AbilityStats.ScaleAbilityDamage(client, Punt, SneakAttacks.Add(140, Punt, true)));
        Assert.Equal((ushort)201, AbilityStats.ScaleAbilityDamage(client, LegThrow, 153));
        Assert.Equal((ushort)149, AbilityStats.ScaleAbilityDamage(client, WeakeningBlow, 113));

        Assert.Equal(19598u, CombatAnimations.Special(DropKick, true, CombatTactic.Speed)!.Id);
        Assert.Equal(19593u, CombatAnimations.Special(SpinThrow, true, CombatTactic.Speed)!.Id);
        Assert.Equal(19594u, CombatAnimations.Special(TopsyTurvy, true, CombatTactic.Speed)!.Id);
        Assert.Equal(19590u, CombatAnimations.Special(Punt, true, CombatTactic.Speed)!.Id);

        Assert.True(TempMods.TryGet(TopsyTurvy, out var e288));
        Assert.Contains(e288.Mods, m => m.Target == TempMods.ConfuseAbility && m.Seconds == 10f);
        Assert.True(TempMods.TryGet(LegThrow, out var e1131));
        Assert.Contains(e1131.Mods, m => m.Target == TempMods.OffBalanceAbility && m.Seconds == 12f);
        Assert.True(TempMods.TryGet(WeakeningBlow, out var e1133));
        Assert.Contains(e1133.Mods, m => m.Target == 180 && m.MinValue == -45 && m.Seconds == 8f);
        Assert.Contains(e1133.Mods, m => m.Target == 181 && m.MinValue == -45 && m.Seconds == 8f);
    }
}
