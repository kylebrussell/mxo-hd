using System.Runtime.CompilerServices;
using hds;
using hds.shared;

namespace hds.Tests;

[Collection("DelayedActions")]
public class KarateAikidoMasterSpecialsTests
{
    private const ushort Karate = CombatStyles.Karate, Aikido = CombatStyles.Aikido;
    private const ushort KiChargedPunch = 265, SidekickCombo = 266, SkyHighSidekick = 267,
        CounterThrow = 273, MakiOtoshi = 274;
    private const byte FixtureLevel = 40, FixtureRank = 40;

    private static WorldClient Client(byte level = FixtureLevel)
    {
        var client = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        client.playerData = new ClientData();
        client.playerData.setDistrictId(1);
        client.playerData.setOnWorld(true);
        client.playerInstance = new PlayerCharacter();
        client.playerInstance.Level.setValue(level);
        client.playerInstance.Health.setValue((ushort)2500);
        client.playerInstance.MaxHealth.setValue((ushort)2500);
        client.playerInstance.InnerStrengthMax.setValue((ushort)2500);
        client.playerInstance.InnerStrengthAvailable.setValue((ushort)2500);
        client.messageQueue = new MessageQueue();
        typeof(WorldClient).GetField("lastFlushTick", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(client, long.MaxValue);
        return client;
    }

    [Theory]
    [InlineData(KiChargedPunch, 68, 30, 10f, 688, 5f, Karate, 8)]
    [InlineData(SidekickCombo, 28, 25, 10f, 688, 5f, Karate, 0)]
    [InlineData(SkyHighSidekick, 21, 30, 10f, 687, 5f, Karate, 0)]
    [InlineData(CounterThrow, 56, 15, 10f, 687, 5f, Aikido, 4)]
    [InlineData(MakiOtoshi, 30, 30, 10f, 688, 5f, Aikido, 0)]
    public void KarateAikidoMasterSpecialsResolveShippedPhysicalBaseAndRefuseOutsideInterlock(
        ushort ability, ushort expectedBase, ushort expectedIs, float expectedOwn, ushort expectedGroup, float expectedGroupSec,
        ushort expectedStyle, int expectedState)
    {
        var entry = AbilityCatalog.Find(ability)!;
        Assert.True(SpyAbilityRegistry.TryResolveDamage(entry, FixtureLevel, new Random(0), out ushort baseDamage));
        Assert.Equal(expectedBase, baseDamage);

        Assert.Equal(expectedIs, AbilityCosts.Cost(ability));
        Assert.True(AbilityCooldowns.TryGet(ability, out var cd));
        Assert.Equal(expectedOwn, cd.Seconds);
        Assert.Equal(expectedGroup, cd.Group);
        Assert.Equal(expectedGroupSec, cd.GroupSeconds);
        Assert.Equal(expectedStyle, CombatStyles.RequiredStyle(ability));
        Assert.Equal(expectedState, (int)entry.Number("Ability/CastRequiredStates"));

        var client = Client();
        Assert.True(SpyAbilityRegistry.TryRefuse(client, ability, 0, null, out uint reason));
        Assert.Equal(AbilityRefusals.InvalidTarget, reason);
    }

    [Fact]
    public void FullLevel40ClosureMemoryScaledDamagesAndEffectsAreExact()
    {
        var plan = NativeQa.NativeQaModes.Find("ability-parity-karate-aikido-master-specials").Plan;
        int mem = plan.Abilities.Sum(AbilityMemory.Cost);
        Assert.True(mem <= AbilityMemory.ForLevel(FixtureLevel),
            $"Level-40 Karate+Aikido batch memory {mem} must fit capacity {AbilityMemory.ForLevel(FixtureLevel)}");

        Assert.Equal("1", plan.EnvironmentOverrides["MXO_CONDITIONAL_POWERLESS_QA"]);
        Assert.Equal("1", plan.EnvironmentOverrides["MXO_CONDITIONAL_THROW_QA"]);
        Assert.Equal(57, mem);

        var client = Client(FixtureLevel);
        AbilityPassives.SetLoaded(client, plan.Abilities.Select(id => (id, (ushort)FixtureRank)).ToArray());

        CombatStyles.Select(client, Karate);
        Assert.Equal(34, AbilityStats.Influence(client, 159));
        Assert.Equal((ushort)91, AbilityStats.ScaleAbilityDamage(client, KiChargedPunch, 68));
        Assert.Equal((ushort)37, AbilityStats.ScaleAbilityDamage(client, SidekickCombo, 28));
        Assert.Equal((ushort)28, AbilityStats.ScaleAbilityDamage(client, SkyHighSidekick, 21));

        CombatStyles.Select(client, Aikido);
        Assert.Equal(32, AbilityStats.Influence(client, 159));
        Assert.Equal((ushort)73, AbilityStats.ScaleAbilityDamage(client, CounterThrow, 56));
        Assert.Equal((ushort)39, AbilityStats.ScaleAbilityDamage(client, MakiOtoshi, 30));

        Assert.Equal(5940u, CombatAnimations.Special(KiChargedPunch, true, CombatTactic.Speed)!.Id);
        Assert.Equal(5936u, CombatAnimations.Special(SidekickCombo, true, CombatTactic.Speed)!.Id);
        Assert.Equal(5937u, CombatAnimations.Special(SkyHighSidekick, true, CombatTactic.Speed)!.Id);
        Assert.Equal(779u, CombatAnimations.Special(CounterThrow, true, CombatTactic.Speed)!.Id);
        Assert.Equal(803u, CombatAnimations.Special(MakiOtoshi, true, CombatTactic.Speed)!.Id);

        Assert.True(TempMods.TryGet(SkyHighSidekick, out var e267));
        Assert.Contains(e267.Mods, m => m.Target == 180 && m.MinValue == -64 && m.Seconds == 12f);
        Assert.Contains(e267.Mods, m => m.Target == 181 && m.MinValue == -62 && m.Seconds == 12f);
        Assert.True(TempMods.TryGet(SidekickCombo, out var e266));
        Assert.Contains(e266.Mods, m => m.Target == TempMods.EnrageAbility && m.Seconds == 8f);
        Assert.True(TempMods.TryGet(CounterThrow, out var e273));
        Assert.Contains(e273.Mods, m => m.Target == TempMods.ConfuseAbility && m.Seconds == 4f);
        Assert.True(TempMods.TryGet(MakiOtoshi, out var e274));
        Assert.Contains(e274.Mods, m => m.Target == 522 && m.MinValue == -60 && m.Seconds == 16f);
    }
}
