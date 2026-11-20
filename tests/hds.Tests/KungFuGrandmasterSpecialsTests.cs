using System.Runtime.CompilerServices;
using hds;
using hds.shared;

namespace hds.Tests;

[Collection("DelayedActions")]
public class KungFuGrandmasterSpecialsTests
{
    private const ushort KungFu = CombatStyles.KungFu;
    private const ushort TripleFrontKick = 427, SuicidalButterfly = 428,
        ExtremeFallingKick = 429, WoodenDummyDrill = 430;
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
    [InlineData(TripleFrontKick, 91, 25, 10f, 687, 5f, 4)]
    [InlineData(SuicidalButterfly, 25, 35, 10f, 688, 5f, 0)]
    [InlineData(ExtremeFallingKick, 46, 40, 10f, 688, 5f, 0)]
    [InlineData(WoodenDummyDrill, 107, 30, 10f, 687, 5f, 2)]
    public void KungFuGrandmasterSpecialsResolveShippedPhysicalBaseAndRefuseOutsideInterlock(
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
        Assert.Equal(KungFu, CombatStyles.RequiredStyle(ability));
        Assert.Equal(expectedState, (int)entry.Number("Ability/CastRequiredStates"));

        var client = Client();
        Assert.True(SpyAbilityRegistry.TryRefuse(client, ability, 0, null, out uint reason));
        Assert.Equal(AbilityRefusals.InvalidTarget, reason);
    }

    [Fact]
    public void FullLevel50ClosureMemoryScaledDamagesAndEffectsAreExact()
    {
        var plan = NativeQa.NativeQaModes.Find("ability-parity-kungfu-gm-specials").Plan;
        Assert.Equal("ability-sweep-melee.csv", plan.TestMobFixture);
        int mem = plan.Abilities.Sum(AbilityMemory.Cost);
        Assert.True(mem <= AbilityMemory.ForLevel(FixtureLevel),
            $"Level-50 Kung Fu GM batch memory {mem} must fit capacity {AbilityMemory.ForLevel(FixtureLevel)}");

        Assert.Equal("1", plan.EnvironmentOverrides["MXO_CONDITIONAL_THROW_QA"]);
        Assert.Equal(61, mem);

        var client = Client(FixtureLevel);
        AbilityPassives.SetLoaded(client, plan.Abilities.Select(id => (id, (ushort)FixtureRank)).ToArray());
        CombatStyles.Select(client, KungFu);

        Assert.Equal(37, AbilityStats.Influence(client, 159));
        Assert.Equal((ushort)124, AbilityStats.ScaleAbilityDamage(client, TripleFrontKick, 91));
        Assert.Equal((ushort)34, AbilityStats.ScaleAbilityDamage(client, SuicidalButterfly, 25));
        Assert.Equal((ushort)63, AbilityStats.ScaleAbilityDamage(client, ExtremeFallingKick, 46));
        Assert.Equal((ushort)146, AbilityStats.ScaleAbilityDamage(client, WoodenDummyDrill, 107));

        Assert.Equal(8086u, CombatAnimations.Special(TripleFrontKick, true, CombatTactic.Speed)!.Id);
        Assert.Equal(8084u, CombatAnimations.Special(SuicidalButterfly, true, CombatTactic.Speed)!.Id);
        Assert.Equal(8083u, CombatAnimations.Special(ExtremeFallingKick, true, CombatTactic.Speed)!.Id);
        Assert.Equal(8080u, CombatAnimations.Special(WoodenDummyDrill, true, CombatTactic.Speed)!.Id);

        Assert.True(TempMods.TryGet(TripleFrontKick, out var e427));
        Assert.Contains(e427.Mods, m => m.Target == TempMods.PowerlessAbility && m.Seconds == 6f);
        Assert.True(TempMods.TryGet(SuicidalButterfly, out var e428));
        Assert.Contains(e428.Mods, m => m.Target == TempMods.ConfuseAbility && m.Seconds == 4f);
        Assert.True(TempMods.TryGet(WoodenDummyDrill, out var e430));
        Assert.Contains(e430.Mods, m => m.Target == TempMods.PowerlessAbility && m.Seconds == 6f);
    }
}
