using System.Runtime.CompilerServices;
using hds;
using hds.shared;

namespace hds.Tests;

[Collection("DelayedActions")]
public class SpyShadowAssassinBatchTests
{
    private const ushort SpyStyle = CombatStyles.SpyStyle;
    private const ushort UncannySenses = 161, SeverArtery = 341, GhostStrike = 365,
        DisruptConnection = 384, ReverseBullDog = 388;
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
    [InlineData(GhostStrike, 29, 0, 60, 10f)]
    [InlineData(ReverseBullDog, 56, 1200, 60, 60f)]
    public void GhostStrikeAndReverseBullDogResolveShippedThrownBaseAndEnforceAdmission(
        ushort ability, ushort expectedBase, int expectedStealthBonus, ushort expectedIs, float expectedOwn)
    {
        var entry = AbilityCatalog.Find(ability)!;
        Assert.True(SpyAbilityRegistry.TryResolveDamage(entry, FixtureLevel, new Random(0), out ushort baseDamage));
        Assert.Equal(expectedBase, baseDamage);
        Assert.Equal(expectedStealthBonus, SneakAttacks.Bonus(ability, true));

        Assert.Equal(expectedIs, AbilityCosts.Cost(ability));
        Assert.True(AbilityCooldowns.TryGet(ability, out var cd));
        Assert.Equal(expectedOwn, cd.Seconds);
        Assert.Equal(SpyStyle, CombatStyles.RequiredStyle(ability));

        var client = Client();
        Assert.True(SpyAbilityRegistry.TryRefuse(client, ability, 0, null, out uint reason));
        Assert.Equal(AbilityRefusals.InvalidTarget, reason);
    }

    [Fact]
    public void SeverArteryHasNoDirectDamageAndEnforcesOutsideCombatAdmission()
    {
        var entry = AbilityCatalog.Find(SeverArtery)!;
        Assert.False(entry.DirectDamage);
        Assert.Equal((ushort)35, AbilityCosts.Cost(SeverArtery));
        Assert.True(AbilityCooldowns.TryGet(SeverArtery, out var cd));
        Assert.Equal(15f, cd.Seconds);
        Assert.Equal(SpyStyle, CombatStyles.RequiredStyle(SeverArtery));

        var client = Client();
        Assert.True(SpyAbilityRegistry.TryRefuse(client, SeverArtery, 0, null, out uint reason));
        Assert.Equal(AbilityRefusals.InvalidTarget, reason);
    }

    [Fact]
    public void FullLevel50ClosureMemoryScaledDamagesAndPassivesAreExact()
    {
        var plan = NativeQa.NativeQaModes.Find("ability-parity-spy-shadow-assassin").Plan;
        Assert.Equal("ability-sweep-melee.csv", plan.TestMobFixture);
        int mem = plan.Abilities.Sum(AbilityMemory.Cost);

        Assert.Equal("1", plan.EnvironmentOverrides["MXO_COMBAT_FIXTURE_DISENGAGE"]);
        Assert.True(mem <= AbilityMemory.ForLevel(FixtureLevel),
            $"Level-50 Spy Shadow/Assassin batch memory {mem} must fit capacity {AbilityMemory.ForLevel(FixtureLevel)}");
        Assert.Equal(97, mem);

        var c161 = Client(FixtureLevel);
        AbilityPassives.SetLoaded(c161, new (ushort, ushort)[] { (UncannySenses, FixtureRank) });
        Assert.Equal(40, AbilityPassives.Total(c161, 577));

        var c384 = Client(FixtureLevel);
        AbilityPassives.SetLoaded(c384, new (ushort, ushort)[] { (DisruptConnection, FixtureRank) });
        Assert.Equal(10, AbilityPassives.Total(c384, 1045));
        Assert.Equal(5, AbilityPassives.Total(c384, 1068));
        Assert.Equal(15, AbilityPassives.Total(c384, 995));

        ushort[] reloadOrder = { UncannySenses, DisruptConnection, ReverseBullDog };
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
        Assert.Equal((ushort)44, AbilityStats.ScaleAbilityDamage(client, GhostStrike, 29));
        Assert.Equal((ushort)1934, AbilityStats.ScaleAbilityDamage(client, ReverseBullDog, SneakAttacks.Add(56, ReverseBullDog, true)));
        Assert.Equal(19595u, CombatAnimations.Special(ReverseBullDog, true, CombatTactic.Speed)!.Id);

        Assert.True(AbilityCooldowns.TryGet(SeverArtery, out var cd341));
        Assert.Equal((ushort)1216, cd341.Group);
        Assert.Equal(3f, cd341.GroupSeconds);
        Assert.True(AbilityCooldowns.TryGet(GhostStrike, out var cd365));
        Assert.Equal((ushort)0, cd365.Group);
        Assert.True(AbilityCooldowns.TryGet(ReverseBullDog, out var cd388));
        Assert.Equal((ushort)691, cd388.Group);
        Assert.Equal(15f, cd388.GroupSeconds);

        Assert.True(TempMods.TryGet(SeverArtery, out var e341));
        Assert.Contains(e341.Mods, m => m.Proc == TempMods.Proc.Dot && m.MinValue == 68 && m.Seconds == 30f);
        Assert.True(TempMods.TryGet(ReverseBullDog, out var e388));
        Assert.Contains(e388.Mods, m => m.Target == ReverseBullDog && m.MinValue == 8 && m.Proc == TempMods.Proc.Terminal);
    }
}
