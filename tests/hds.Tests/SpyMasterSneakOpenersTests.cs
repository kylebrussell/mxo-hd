using System.Runtime.CompilerServices;
using hds;
using hds.shared;

namespace hds.Tests;

[Collection("DelayedActions")]
public class SpyMasterSneakOpenersTests
{
    private const ushort SpyStyle = CombatStyles.SpyStyle;
    private const ushort StaggeringThrow = 348, DivingFrankensteiner = 385, FreedomToJoints = 386,
        InfuriatingAssault = 1130, WardingBlow = 1134;
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
    [InlineData(StaggeringThrow, 190, 0, 45, 45f, 691, 15f, true)]
    [InlineData(DivingFrankensteiner, 164, 725, 80, 60f, 691, 15f, false)]
    [InlineData(FreedomToJoints, 92, 900, 60, 60f, 691, 15f, false)]
    [InlineData(InfuriatingAssault, 131, 0, 40, 45f, 691, 15f, true)]
    [InlineData(WardingBlow, 141, 0, 45, 45f, 691, 15f, true)]
    public void SpyMasterSneakOpenersResolveShippedThrownBaseAndEnforceAdmission(
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
        if (ability == FreedomToJoints || ability == StaggeringThrow || ability == InfuriatingAssault)
        {
            Assert.True(SpyAbilityRegistry.TryRefuse(client, ability, 0, null, out uint wrongStyle));
            Assert.Equal(AbilityRefusals.WrongStyle, wrongStyle);

            AbilityPassives.SetLoaded(client, new[] { (SpyStyle, (ushort)FixtureRank) });
            CombatStyles.Select(client, SpyStyle);
            Assert.True(SpyAbilityRegistry.TryRefuse(client, ability, 0, null, out uint noSneak));
            Assert.Equal(AbilityRefusals.CasterStateRequired, noSneak);

            client.playerData.conditionStateFlags |= Stealth.StealthedFlag;
        }
        Assert.True(SpyAbilityRegistry.TryRefuse(client, ability, 0, null, out uint reason));
        Assert.Equal(AbilityRefusals.InvalidTarget, reason);
    }

    [Fact]
    public void FullLevel50ClosureMemoryScaledDamagesAndEffectsAreExact()
    {
        var plan = NativeQa.NativeQaModes.Find("ability-parity-spy-master-sneak-openers").Plan;
        Assert.Equal("ability-sweep-melee.csv", plan.TestMobFixture);
        int mem = plan.Abilities.Sum(AbilityMemory.Cost);

        Assert.Equal("1", plan.EnvironmentOverrides["MXO_COMBAT_FIXTURE_RESET"]);
        Assert.True(mem <= AbilityMemory.ForLevel(FixtureLevel),
            $"Level-50 Master Spy sneak batch memory {mem} must fit capacity {AbilityMemory.ForLevel(FixtureLevel)}");
        Assert.Equal(99, mem);

        var client = Client(FixtureLevel);
        AbilityPassives.SetLoaded(client, plan.Abilities.Select(id => (id, (ushort)FixtureRank)).ToArray());
        CombatStyles.Select(client, SpyStyle);

        Assert.Equal(54, AbilityStats.Influence(client, 1034));
        Assert.Equal((ushort)292, AbilityStats.ScaleAbilityDamage(client, StaggeringThrow, 190));
        Assert.Equal((ushort)1369, AbilityStats.ScaleAbilityDamage(client, DivingFrankensteiner, SneakAttacks.Add(164, DivingFrankensteiner, true)));
        Assert.Equal((ushort)1527, AbilityStats.ScaleAbilityDamage(client, FreedomToJoints, SneakAttacks.Add(92, FreedomToJoints, true)));
        Assert.Equal((ushort)201, AbilityStats.ScaleAbilityDamage(client, InfuriatingAssault, 131));
        Assert.Equal((ushort)217, AbilityStats.ScaleAbilityDamage(client, WardingBlow, 141));

        Assert.Equal(19592u, CombatAnimations.Special(DivingFrankensteiner, true, CombatTactic.Speed)!.Id);
        Assert.Equal(19600u, CombatAnimations.Special(FreedomToJoints, true, CombatTactic.Speed)!.Id);

        Assert.True(TempMods.TryGet(StaggeringThrow, out var e348));
        Assert.Contains(e348.Mods, m => m.Target == TempMods.StaggeredAbility && m.Seconds == 12f);
        Assert.True(TempMods.TryGet(FreedomToJoints, out var e386));
        Assert.Contains(e386.Mods, m => m.Target == TempMods.PowerlessAbility && m.Seconds == 8f);
        Assert.True(TempMods.TryGet(InfuriatingAssault, out var e1130));
        Assert.Contains(e1130.Mods, m => m.Target == TempMods.EnrageAbility && m.Seconds == 4f);
        Assert.True(TempMods.TryGet(WardingBlow, out var e1134));
        Assert.Contains(e1134.Mods, m => m.Target == 522 && m.MinValue == -30 && m.Seconds == 8f);
        Assert.Contains(e1134.Mods, m => m.Target == 524 && m.MinValue == -30 && m.Seconds == 8f);
    }
}
