using System.Runtime.CompilerServices;
using hds;
using hds.shared;

namespace hds.Tests;

[Collection("DelayedActions")]
public class MartialArtsRemainingSpecialsTests
{
    private const ushort Karate = CombatStyles.Karate, Aikido = CombatStyles.Aikido;
    private const ushort SwirlingKiSummon = 268, AikidoSpinningClayPigeon = 296, KiBurst = 406;
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
    [InlineData(SwirlingKiSummon, 2, 60, 12f, 687, 5f, Karate, 0, false)]
    [InlineData(AikidoSpinningClayPigeon, 375, 40, 40f, 0, 0f, Aikido, 4, true)]
    [InlineData(KiBurst, 23, 70, 15f, 688, 5f, Aikido, 0, false)]
    public void RemainingMartialArtsSpecialsResolveShippedPhysicalBaseAndAdmission(
        ushort ability, ushort expectedBase, ushort expectedIs, float expectedOwn, ushort expectedGroup, float expectedGroupSec,
        ushort expectedStyle, int expectedState, bool requiresInterlock)
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
        bool refused = SpyAbilityRegistry.TryRefuse(client, ability, 0, null, out uint reason);
        Assert.Equal(requiresInterlock, refused);
        if (requiresInterlock) Assert.Equal(AbilityRefusals.InvalidTarget, reason);
    }

    [Fact]
    public void FullLevel50ClosureMemoryScaledDamagesAndEffectsAreExact()
    {
        var plan = NativeQa.NativeQaModes.Find("ability-parity-ma-remaining-specials").Plan;
        Assert.Equal("ability-sweep-melee.csv", plan.TestMobFixture);
        int mem = plan.Abilities.Sum(AbilityMemory.Cost);

        Assert.Equal("1", plan.EnvironmentOverrides["MXO_CONDITIONAL_THROW_QA"]);
        Assert.True(mem <= AbilityMemory.ForLevel(FixtureLevel),
            $"Level-50 remaining MA specials memory {mem} must fit capacity {AbilityMemory.ForLevel(FixtureLevel)}");
        Assert.Equal(64, mem);

        var client = Client(FixtureLevel);
        AbilityPassives.SetLoaded(client, plan.Abilities.Select(id => (id, (ushort)FixtureRank)).ToArray());

        CombatStyles.Select(client, Karate);
        Assert.Equal(41, AbilityStats.Influence(client, 159));
        Assert.Equal((ushort)2, AbilityStats.ScaleAbilityDamage(client, SwirlingKiSummon, 2));

        CombatStyles.Select(client, Aikido);
        Assert.Equal(38, AbilityStats.Influence(client, 159));
        Assert.Equal((ushort)517, AbilityStats.ScaleAbilityDamage(client, AikidoSpinningClayPigeon, 375));
        Assert.Equal((ushort)31, AbilityStats.ScaleAbilityDamage(client, KiBurst, 23));

        Assert.Equal(741u, CombatAnimations.Special(AikidoSpinningClayPigeon, true, CombatTactic.Speed)!.Id);

        Assert.True(TempMods.TryGet(SwirlingKiSummon, out var e268));
        Assert.Contains(e268.Mods, m => m.Target == 522 && m.MinValue == -60 && m.Seconds == 12f);
        Assert.Contains(e268.Mods, m => m.Target == 524 && m.MinValue == -60 && m.Seconds == 12f);
        Assert.True(TempMods.TryGet(AikidoSpinningClayPigeon, out var e296));
        Assert.Contains(e296.Mods, m => m.Target == TempMods.ConfuseAbility && m.Seconds == 8f);
        Assert.True(TempMods.TryGet(KiBurst, out var e406));
        Assert.Contains(e406.Mods, m => m.Target == TempMods.EnrageAbility && m.Seconds == 15f && m.Chance == .2f);
    }
}
