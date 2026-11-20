using System.Runtime.CompilerServices;
using hds;
using hds.shared;

namespace hds.Tests;

[Collection("DelayedActions")]
public class ZenMasterSpecialsTests
{
    private const ushort Technique = 860, SweepTheFloor = 869, SpinningFlower = 870;
    private const byte FixtureLevel = 99, FixtureRank = 50;

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
    [InlineData(SweepTheFloor, 312, 40, 10f, 687, 5f, 4)]
    [InlineData(SpinningFlower, 325, 40, 10f, 688, 5f, 0)]
    public void ZenMasterSpecialsResolveShippedPhysicalBaseAndRefuseOutsideInterlock(
        ushort ability, ushort expectedBase, ushort expectedIs, float expectedOwn, ushort expectedGroup, float expectedGroupSec,
        int expectedState)
    {
        var entry = AbilityCatalog.Find(ability)!;
        Assert.True(SpyAbilityRegistry.TryResolveDamage(entry, FixtureLevel, new Random(0), out ushort baseDamage));
        Assert.Equal(expectedBase, baseDamage);

        Assert.Equal(expectedIs, AbilityCosts.Cost(ability));
        Assert.True(AbilityCooldowns.TryGet(ability, out var cd));
        Assert.Equal(expectedOwn, cd.Seconds);
        Assert.Equal(expectedGroup, cd.Group);
        Assert.Equal(expectedGroupSec, cd.GroupSeconds);
        Assert.Equal(0, CombatStyles.RequiredStyle(ability));
        Assert.Equal(expectedState, (int)entry.Number("Ability/CastRequiredStates"));

        var client = Client();
        Assert.True(SpyAbilityRegistry.TryRefuse(client, ability, 0, null, out uint reason));
        Assert.Equal(AbilityRefusals.InvalidTarget, reason);
    }

    [Fact]
    public void FullLevel99ClosureMemoryScaledDamagesAndTechniquePassiveAreExact()
    {
        var plan = NativeQa.NativeQaModes.Find("ability-parity-zen-master-specials").Plan;
        Assert.Equal("ability-sweep-melee.csv", plan.TestMobFixture);
        int mem = plan.Abilities.Sum(AbilityMemory.Cost);

        Assert.Equal("1", plan.EnvironmentOverrides["MXO_CONDITIONAL_THROW_QA"]);
        Assert.Equal(46, mem);

        var c860 = Client(FixtureLevel);
        AbilityPassives.SetLoaded(c860, new (ushort, ushort)[] { (Technique, FixtureRank) });
        Assert.Equal(198, AbilityPassives.Total(c860, 183));
        Assert.Equal(99, AbilityPassives.Total(c860, 572));

        var client = Client(FixtureLevel);
        AbilityPassives.SetLoaded(client, plan.Abilities.Select(id => (id, (ushort)FixtureRank)).ToArray());

        Assert.Equal(29, AbilityStats.Influence(client, 159));
        Assert.Equal((ushort)402, AbilityStats.ScaleAbilityDamage(client, SweepTheFloor, 312));
        Assert.Equal((ushort)419, AbilityStats.ScaleAbilityDamage(client, SpinningFlower, 325));

        Assert.True(TempMods.TryGet(SweepTheFloor, out var e869));
        Assert.Contains(e869.Mods, m => m.Target == TempMods.DisarmedAbility && m.Seconds == 20f);
        Assert.True(TempMods.TryGet(SpinningFlower, out var e870));
        Assert.Contains(e870.Mods, m => m.Target == TempMods.PowerlessAbility && m.Seconds == 24.6f);
        Assert.Contains(e870.Mods, m => m.Target == TempMods.CombatTacticsAbility && m.MinValue == -10 && m.Seconds == 15f);
    }
}
