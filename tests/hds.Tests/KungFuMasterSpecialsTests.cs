using System.Runtime.CompilerServices;
using hds;
using hds.shared;

namespace hds.Tests;

[Collection("DelayedActions")]
public class KungFuMasterSpecialsTests
{
    private const ushort KungFu = CombatStyles.KungFu;
    private const ushort DimMakStrike = 259, MisdirectPunch = 260, MachinegunFistCombo = 261, PistonKicks = 262;
    private const byte FixtureLevel = 30, FixtureRank = 30;

    private static WorldClient Client(byte level = FixtureLevel)
    {
        var client = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        client.playerData = new ClientData();
        client.playerData.setDistrictId(1);
        client.playerData.setOnWorld(true);
        client.playerInstance = new PlayerCharacter();
        client.playerInstance.Level.setValue(level);
        client.playerInstance.Health.setValue((ushort)2000);
        client.playerInstance.MaxHealth.setValue((ushort)2000);
        client.playerInstance.InnerStrengthMax.setValue((ushort)2000);
        client.playerInstance.InnerStrengthAvailable.setValue((ushort)2000);
        client.messageQueue = new MessageQueue();
        typeof(WorldClient).GetField("lastFlushTick", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(client, long.MaxValue);
        return client;
    }

    [Theory]
    [InlineData(DimMakStrike, 71, 20, 10f, 688, 5f)]
    [InlineData(MisdirectPunch, 24, 30, 10f, 688, 5f)]
    [InlineData(MachinegunFistCombo, 23, 30, 10f, 687, 5f)]
    [InlineData(PistonKicks, 108, 25, 10f, 687, 5f)]
    public void KungFuMasterSpecialsResolveShippedPhysicalBaseAndRefuseOutsideInterlock(
        ushort ability, ushort expectedBase, ushort expectedIs, float expectedOwn, ushort expectedGroup, float expectedGroupSec)
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

        var client = Client();
        Assert.True(SpyAbilityRegistry.TryRefuse(client, ability, 0, null, out uint reason));
        Assert.Equal(AbilityRefusals.InvalidTarget, reason);
    }

    [Fact]
    public void FullLevel30ClosureMemoryAndScaledDamagesAreExact()
    {
        var plan = NativeQa.NativeQaModes.Find("ability-parity-kungfu-master-specials").Plan;
        int mem = plan.Abilities.Sum(AbilityMemory.Cost);
        Assert.True(mem <= AbilityMemory.ForLevel(FixtureLevel),
            $"Level-30 Kung Fu batch memory {mem} must fit capacity {AbilityMemory.ForLevel(FixtureLevel)}");

        var client = Client(FixtureLevel);
        AbilityPassives.SetLoaded(client, plan.Abilities.Select(id => (id, (ushort)FixtureRank)).ToArray());
        CombatStyles.Select(client, KungFu);

        // Compute exact full-closure melee damage influence (Kung-Fu + MA Expertise + Kung Fu Proficiency + Kung Fu Master).
        int meleeInfluence = AbilityStats.Influence(client, 159);
        Assert.Equal(23, meleeInfluence);
        Assert.Equal((ushort)87, AbilityStats.ScaleAbilityDamage(client, DimMakStrike, 71));
        Assert.Equal((ushort)29, AbilityStats.ScaleAbilityDamage(client, MisdirectPunch, 24));
        Assert.Equal((ushort)28, AbilityStats.ScaleAbilityDamage(client, MachinegunFistCombo, 23));
        Assert.Equal((ushort)132, AbilityStats.ScaleAbilityDamage(client, PistonKicks, 108));
    }
}
