using System.Runtime.CompilerServices;
using hds;
using hds.shared;

namespace hds.Tests;

[Collection("DelayedActions")]
public class KarateAikidoGrandmasterSpecialsTests
{
    private const ushort Karate = CombatStyles.Karate, Aikido = CombatStyles.Aikido;
    private const ushort PunchReversal = 407, AerialTakedown = 411, TomoNage = 412,
        MachinegunKick = 532, WristThrow = 534, KiChargedFootSweep = 536;
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
    [InlineData(PunchReversal, 21, 40, 10f, 688, 5f, Aikido, 0)]
    [InlineData(AerialTakedown, 96, 25, 10f, 687, 5f, Aikido, 4)]
    [InlineData(TomoNage, 113, 45, 10f, 687, 5f, Aikido, 4)]
    [InlineData(MachinegunKick, 73, 32, 10f, 688, 5f, Karate, 2)]
    [InlineData(WristThrow, 31, 65, 10f, 688, 5f, Karate, 0)]
    [InlineData(KiChargedFootSweep, 134, 45, 10f, 687, 5f, Karate, 8)]
    public void KarateAikidoGrandmasterSpecialsResolveShippedPhysicalBaseAndRefuseOutsideInterlock(
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
    public void FullLevel50ClosureMemoryScaledDamagesAndEffectsAreExact()
    {
        var plan = NativeQa.NativeQaModes.Find("ability-parity-karate-aikido-gm-specials").Plan;
        Assert.Equal("ability-sweep-melee.csv", plan.TestMobFixture);
        int mem = plan.Abilities.Sum(AbilityMemory.Cost);

        Assert.Equal("1", plan.EnvironmentOverrides["MXO_CONDITIONAL_POWERLESS_QA"]);
        Assert.Equal("1", plan.EnvironmentOverrides["MXO_CONDITIONAL_THROW_QA"]);
        Assert.True(mem <= AbilityMemory.ForLevel(FixtureLevel),
            $"Level-50 Karate+Aikido GM batch memory {mem} must fit capacity {AbilityMemory.ForLevel(FixtureLevel)}");
        Assert.Equal(85, mem);

        var client = Client(FixtureLevel);
        AbilityPassives.SetLoaded(client, plan.Abilities.Select(id => (id, (ushort)FixtureRank)).ToArray());

        CombatStyles.Select(client, Karate);
        Assert.Equal(47, AbilityStats.Influence(client, 159));
        Assert.Equal((ushort)107, AbilityStats.ScaleAbilityDamage(client, MachinegunKick, 73));
        Assert.Equal((ushort)45, AbilityStats.ScaleAbilityDamage(client, WristThrow, 31));
        Assert.Equal((ushort)196, AbilityStats.ScaleAbilityDamage(client, KiChargedFootSweep, 134));

        CombatStyles.Select(client, Aikido);
        Assert.Equal(44, AbilityStats.Influence(client, 159));
        Assert.Equal((ushort)30, AbilityStats.ScaleAbilityDamage(client, PunchReversal, 21));
        Assert.Equal((ushort)138, AbilityStats.ScaleAbilityDamage(client, AerialTakedown, 96));
        Assert.Equal((ushort)162, AbilityStats.ScaleAbilityDamage(client, TomoNage, 113));

        Assert.Equal(827u, CombatAnimations.Special(PunchReversal, true, CombatTactic.Speed)!.Id);
        Assert.Equal(735u, CombatAnimations.Special(AerialTakedown, true, CombatTactic.Speed)!.Id);
        Assert.Equal(751u, CombatAnimations.Special(TomoNage, true, CombatTactic.Speed)!.Id);
        Assert.Equal(5934u, CombatAnimations.Special(MachinegunKick, true, CombatTactic.Speed)!.Id);
        Assert.Equal(755u, CombatAnimations.Special(WristThrow, true, CombatTactic.Speed)!.Id);
        Assert.Equal(5935u, CombatAnimations.Special(KiChargedFootSweep, true, CombatTactic.Speed)!.Id);

        Assert.True(TempMods.TryGet(PunchReversal, out var e407));
        Assert.Contains(e407.Mods, m => m.Target == TempMods.PowerlessAbility && m.Seconds == 4f);
        Assert.True(TempMods.TryGet(AerialTakedown, out var e411));
        Assert.Contains(e411.Mods, m => m.Target == TempMods.EnrageAbility && m.Seconds == 15f);
        Assert.True(TempMods.TryGet(TomoNage, out var e412));
        Assert.Contains(e412.Mods, m => m.Target == 180 && m.MinValue == -140 && m.Seconds == 12f);
        Assert.Contains(e412.Mods, m => m.Target == 181 && m.MinValue == -100 && m.Seconds == 12f);
        Assert.Contains(e412.Mods, m => m.Target == TempMods.EnrageAbility && m.Seconds == 6f);
        Assert.True(TempMods.TryGet(KiChargedFootSweep, out var e536));
        Assert.Contains(e536.Mods, m => m.Target == TempMods.PowerlessAbility && m.Seconds == 6f);
    }
}
