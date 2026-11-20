using System.Runtime.CompilerServices;
using hds;
using hds.shared;

namespace hds.Tests;

[Collection("DelayedActions")]
public class MasterKnifeThrowerBatchTests
{
    private const ushort SpyStyle = CombatStyles.SpyStyle;
    private const ushort NeuroDart = 141, BlindingThrow = 282, DeadlyAim = 339,
        ThroatCuttingThrow = 340, SubduingThrow = 347;
    private const byte FixtureLevel = 50, FixtureRank = 50;

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
        client.playerInstance.InnerStrengthMax.setValue((ushort)2500);
        client.playerInstance.InnerStrengthAvailable.setValue((ushort)2500);
        client.messageQueue = new MessageQueue();
        typeof(WorldClient).GetField("lastFlushTick", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(client, long.MaxValue);
        return client;
    }

    [Theory]
    [InlineData(ThroatCuttingThrow, 80, 15, 8f)]
    [InlineData(SubduingThrow, 57, 15, 8f)]
    public void DirectDamageThrowsResolveShippedThrownBaseAndEnforceAdmission(
        ushort ability, ushort expectedBase, ushort expectedIs, float expectedOwn)
    {
        var entry = AbilityCatalog.Find(ability)!;
        Assert.True(SpyAbilityRegistry.TryResolveDamage(entry, FixtureLevel, new Random(0), out ushort baseDamage));
        Assert.Equal(expectedBase, baseDamage);

        Assert.Equal(expectedIs, AbilityCosts.Cost(ability));
        Assert.True(AbilityCooldowns.TryGet(ability, out var cd));
        Assert.Equal(expectedOwn, cd.Seconds);
        Assert.Equal(SpyStyle, CombatStyles.RequiredStyle(ability));

        var client = Client();
        Assert.True(SpyAbilityRegistry.TryRefuse(client, ability, 0, null, out uint reason));
        Assert.Equal(AbilityRefusals.InvalidTarget, reason);
    }

    [Theory]
    [InlineData(NeuroDart, 20, 18f)]
    [InlineData(BlindingThrow, 25, 8f)]
    public void DotThrowsHaveNoDirectDamageAndEnforceAdmission(ushort ability, ushort expectedIs, float expectedOwn)
    {
        var entry = AbilityCatalog.Find(ability)!;
        Assert.False(entry.DirectDamage);
        Assert.Equal(expectedIs, AbilityCosts.Cost(ability));
        Assert.True(AbilityCooldowns.TryGet(ability, out var cd));
        Assert.Equal(expectedOwn, cd.Seconds);
        Assert.Equal(SpyStyle, CombatStyles.RequiredStyle(ability));

        var client = Client();
        Assert.True(SpyAbilityRegistry.TryRefuse(client, ability, 0, null, out uint reason));
        Assert.Equal(AbilityRefusals.InvalidTarget, reason);
    }

    [Fact]
    public void FullLevel50ClosureMemoryScaledDamagesAndDeadlyAimAreExact()
    {
        var plan = NativeQa.NativeQaModes.Find("ability-parity-master-knife-thrower-throws").Plan;
        Assert.Equal("ability-sweep-melee.csv", plan.TestMobFixture);
        int mem = plan.Abilities.Sum(AbilityMemory.Cost);

        Assert.Equal("1", plan.EnvironmentOverrides["MXO_CONDITIONAL_POWERLESS_QA"]);
        Assert.Equal("1", plan.EnvironmentOverrides["MXO_CONDITIONAL_THROW_QA"]);
        Assert.Equal("1", plan.EnvironmentOverrides[CombatFixtureWeapons.ResetMode]);
        Assert.True(mem <= AbilityMemory.ForLevel(FixtureLevel),
            $"Level-50 Master Knife Thrower batch memory {mem} must fit capacity {AbilityMemory.ForLevel(FixtureLevel)}");
        Assert.Equal(64, mem);

        var c339 = Client(FixtureLevel);
        AbilityPassives.SetLoaded(c339, new (ushort, ushort)[] { (DeadlyAim, FixtureRank) });
        Assert.Equal(5, AbilityPassives.Total(c339, 1068));
        Assert.Equal(25, AbilityPassives.Total(c339, 1041));

        ushort[] reloadOrder = { DeadlyAim, ThroatCuttingThrow, SubduingThrow };
        var active = plan.Abilities.Except(reloadOrder).ToDictionary(id => id, _ => (ushort)FixtureRank);
        foreach (ushort id in reloadOrder)
        {
            foreach (var prereq in AbilityPrerequisites.Current.Closure(id))
            {
                Assert.True(active.TryGetValue(prereq.Key, out ushort loadedRank) && loadedRank >= prereq.Value,
                    $"Reloading {id} requires {prereq.Key} at rank {prereq.Value} to already be loaded");
            }
            active[id] = FixtureRank;
        }

        var client = Client(FixtureLevel);
        AbilityPassives.SetLoaded(client, plan.Abilities.Select(id => (id, (ushort)FixtureRank)).ToArray());
        CombatStyles.Select(client, SpyStyle);

        Assert.Equal(54, AbilityStats.Influence(client, 1034));
        Assert.Equal((ushort)123, AbilityStats.ScaleAbilityDamage(client, ThroatCuttingThrow, 80));
        Assert.Equal((ushort)87, AbilityStats.ScaleAbilityDamage(client, SubduingThrow, 57));
        ushort blindingTotal = AbilityStats.ScaleAbilityDamage(client, BlindingThrow, 24);
        Assert.Equal((ushort)36, blindingTotal);
        Assert.Equal(6, TempMods.DotTick(blindingTotal, 15, 2.5f));
        Assert.Equal((ushort)1041, AbilityControlDurations.BonusStat(TempMods.BlindAbility));
        Assert.Equal(12.5f, AbilityControlDurations.Scale(client, TempMods.BlindAbility, 10f), 3);
        Assert.Equal(7.5f, AbilityControlDurations.Scale(client, TempMods.PowerlessAbility, 6f), 3);

        Assert.True(AbilityCooldowns.TryGet(NeuroDart, out var cd141));
        Assert.Equal((ushort)1215, cd141.Group);
        Assert.Equal(3f, cd141.GroupSeconds);
        Assert.True(AbilityCooldowns.TryGet(BlindingThrow, out var cd282));
        Assert.Equal((ushort)1215, cd282.Group);
        Assert.Equal(3f, cd282.GroupSeconds);
        Assert.True(AbilityCooldowns.TryGet(ThroatCuttingThrow, out var cd340));
        Assert.Equal((ushort)1215, cd340.Group);
        Assert.Equal(3f, cd340.GroupSeconds);
        Assert.True(AbilityCooldowns.TryGet(SubduingThrow, out var cd347));
        Assert.Equal((ushort)1216, cd347.Group);
        Assert.Equal(3f, cd347.GroupSeconds);

        Assert.True(TempMods.TryGet(NeuroDart, out var e141));
        Assert.Contains(e141.Mods, m => m.Proc == TempMods.Proc.Dot && m.MinValue == 264 && m.Seconds == 15f);
        Assert.True(TempMods.TryGet(BlindingThrow, out var e282));
        Assert.Contains(e282.Mods, m => m.Target == TempMods.BlindAbility && m.Seconds == 10f);
        Assert.True(TempMods.TryGet(ThroatCuttingThrow, out var e340));
        Assert.Contains(e340.Mods, m => m.Target == TempMods.OffBalanceAbility && m.Seconds == 8f);
        Assert.Contains(e340.Mods, m => m.Target == TempMods.DazeAbility && m.Seconds == 8f);
        Assert.True(TempMods.TryGet(SubduingThrow, out var e347));
        Assert.Contains(e347.Mods, m => m.Target == TempMods.OffBalanceAbility && m.Seconds == 8f);
        Assert.Contains(e347.Mods, m => m.Target == TempMods.StaggeredAbility && m.Seconds == 8f);
    }
}
