using System.Reflection;
using System.Runtime.CompilerServices;
using hds;

namespace hds.Tests;

public class LegSweepCannyStrikeParityTests
{
    private const ushort Spy = CombatStyles.SpyStyle;
    private const ushort LegSweep = LegSweepAdmission.LegSweep;       // 134
    private const ushort CannyStrike = CannyStrikeDamage.CannyStrike; // 295

    private static WorldClient Client(byte level = 50)
    {
        var client = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        client.playerData = new ClientData();
        client.playerData.setDistrictId(1);
        client.playerData.setOnWorld(true);
        client.playerInstance = new PlayerCharacter();
        client.playerInstance.Level.setValue(level);
        client.playerInstance.Health.setValue((ushort)5000);
        client.playerInstance.MaxHealth.setValue((ushort)5000);
        client.viewMan = new ViewManager();
        client.messageQueue = new MessageQueue();
        typeof(WorldClient).GetField("lastFlushTick", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(client, long.MaxValue);
        return client;
    }

    [Theory]
    [InlineData(LegSweep)]
    [InlineData(CannyStrike)]
    public void LegSweepAndCannyStrikeRefuseOutsideCloseCombatWithNativeInvalidTarget(ushort id)
    {
        var client = Client();
        Assert.True(SpyAbilityRegistry.TryRefuse(client, id, 0, null, out uint emptyReason));
        Assert.Equal(AbilityRefusals.InvalidTarget, emptyReason);

        Assert.True(SpyAbilityRegistry.TryRefuse(client, id, 4, 1, out uint restReason));
        Assert.Equal(AbilityRefusals.InvalidTarget, restReason);
    }

    [Fact]
    public void LegSweepAndCannyStrikeLevel50ClosureDamageResistanceMoveAndDebuffsAreExact()
    {
        var plan = NativeQa.NativeQaModes.Find("ability-parity-leg-sweep-canny-strike").Plan;
        Assert.True(plan.Abilities.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(50));

        var caster = Client(50);
        AbilityPassives.SetLoaded(caster, plan.Abilities.Select(id => (id, (ushort)50)));
        CombatStyles.Select(caster, Spy);

        Assert.Equal(54, AbilityStats.Influence(caster, 1034));

        // 134 Leg Sweep: fixed thrown base 13 -> +54% = 20, +75 Thrown resisted = 16, ILDB move 283, 12 IS, 12 s / grp 687 5 s, 1009 -15 (10 s).
        var legItem = new AbilityItem();
        legItem.setAbilityID(LegSweep);
        ushort legBase = AbilityHandler.AbilityDamage(legItem, new Random(1), characterLevel: 50);
        Assert.Equal((ushort)13, legBase);
        ushort legNeutral = AbilityStats.ScaleAbilityDamage(caster, LegSweep, legBase);
        Assert.Equal((ushort)20, legNeutral);
        Assert.Equal((ushort)16, TypedAbilityResistance.Amount(legNeutral, 50, 50, 75));
        Assert.Equal(283u, CombatAnimations.Special(LegSweep, true, CombatTactic.Speed)!.Id);
        Assert.Equal((ushort)12, AbilityCosts.Cost(LegSweep));
        Assert.True(AbilityCooldowns.TryGet(LegSweep, out var legCd));
        Assert.Equal(12f, legCd.Seconds);
        Assert.Equal((ushort)687, legCd.Group);
        Assert.Equal(5f, legCd.GroupSeconds);
        Assert.True(TempMods.TryGet(LegSweep, out var legEffect));
        Assert.Contains(legEffect.Mods, m => m.Target == 1009 && m.MinValue == -15 && m.Seconds == 10f);

        // 295 Canny Strike: VF4 8..10 -> +54% = 12..15, +75 Thrown resisted = 9..12, 70 IS, 30 s own.
        var cannyItem = new AbilityItem();
        cannyItem.setAbilityID(CannyStrike);
        for (int seed = 0; seed < 30; seed++)
        {
            ushort cannyBase = AbilityHandler.AbilityDamage(cannyItem, new Random(seed), characterLevel: 50);
            Assert.InRange(cannyBase, (ushort)8, (ushort)10);
            ushort cannyNeutral = AbilityStats.ScaleAbilityDamage(caster, CannyStrike, cannyBase);
            Assert.InRange(cannyNeutral, (ushort)12, (ushort)15);
            ushort cannyResisted = TypedAbilityResistance.Amount(cannyNeutral, 50, 50, 75);
            Assert.InRange(cannyResisted, (ushort)9, (ushort)12);
        }
        Assert.Equal((ushort)70, AbilityCosts.Cost(CannyStrike));
        Assert.True(AbilityCooldowns.TryGet(CannyStrike, out var cannyCd));
        Assert.Equal(30f, cannyCd.Seconds);
    }
}
