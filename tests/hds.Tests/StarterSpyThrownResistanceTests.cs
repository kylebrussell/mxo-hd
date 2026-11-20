using System.Reflection;
using System.Runtime.CompilerServices;
using hds;

namespace hds.Tests;

[Collection("DelayedActions")]
public class StarterSpyThrownResistanceTests
{
    private const ushort SpyStyle = CombatStyles.SpyStyle;                  // 1112
    private const ushort LegSweep = 134;
    private const ushort PoisonKnife = CombatSpecials.PoisonKnife;          // 146
    private const ushort SpinningBacksweep = 152;
    private const ushort WoundingThrow = CombatSpecials.WoundingThrow;      // 281
    private const byte FixtureLevel = 50, FixtureRank = 50;

    private static WorldClient Client(byte level = FixtureLevel)
    {
        var client = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        client.playerData = new ClientData();
        client.playerData.setCharID(110);
        client.playerData.setDistrictId(1);
        client.playerData.setOnWorld(true);
        client.playerInstance = new PlayerCharacter();
        client.playerInstance.Level.setValue(level);
        client.playerInstance.Health.setValue((ushort)5000);
        client.playerInstance.MaxHealth.setValue((ushort)5000);
        client.playerInstance.InnerStrengthMax.setValue((ushort)2000);
        client.playerInstance.InnerStrengthAvailable.setValue((ushort)2000);
        client.messageQueue = new MessageQueue();
        typeof(WorldClient).GetField("lastFlushTick", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(client, long.MaxValue);
        return client;
    }

    private static Mob Dummy(bool thrownResisted)
    {
        var mob = new Mob();
        mob.setEntityId(990002);
        mob.setName("Melee Dummy");
        mob.setLevel(FixtureLevel);
        mob.setHealthM(30000);
        mob.setHealthC(30000);
        if (thrownResisted)
        {
            TimedEffects.Apply(TimedEffects.ForMob(mob), new TempMods.Effect
            {
                Mods = new[]
                {
                    new TempMods.Mod
                    {
                        Kind = TempMods.KindStat, Proc = TempMods.Proc.Stat, Target = 1033,
                        MinValue = CombatFixtureWeapons.DummyPhysicalResistance,
                        MaxValue = CombatFixtureWeapons.DummyPhysicalResistance,
                        Seconds = 300
                    }
                }
            }, TimeUtils.GetSimTimeSeconds());
        }
        return mob;
    }

    [Fact]
    public void StarterSpyThrownDirectAndDotNeutralAndResistedValuesAreExact()
    {
        var plan = NativeQa.NativeQaModes.Find("ability-parity-starter-spy-thrown-resistance").Plan;
        Assert.Equal("awakened-interlock-resistance.csv", plan.TestMobFixture);
        Assert.Equal("1", plan.EnvironmentOverrides[CombatFixtureWeapons.ResetMode]);
        Assert.Equal(new ushort[] { SpyStyle, 1111, LegSweep, SpinningBacksweep, PoisonKnife, WoundingThrow },
            plan.HotbarAbilities);

        int mem = plan.Abilities.Sum(AbilityMemory.Cost);
        Assert.Equal(44, mem);
        Assert.True(mem <= AbilityMemory.ForLevel(FixtureLevel));

        var client = Client(FixtureLevel);
        AbilityPassives.SetLoaded(client, plan.Abilities.Select(id => (id, (ushort)FixtureRank)).ToArray());
        CombatStyles.Select(client, SpyStyle);

        int thrownInf = AbilityStats.Influence(client, 1034);
        Assert.Equal(29, thrownInf);

        // 134 Leg Sweep: base 13 -> scaled -> +75 Thrown resisted.
        var a134 = new AbilityItem(); a134.setAbilityID(LegSweep);
        ushort base134 = AbilityHandler.AbilityDamage(a134, new Random(0), characterLevel: FixtureLevel);
        Assert.Equal((ushort)13, base134);
        ushort scaled134 = AbilityStats.ScaleAbilityDamage(client, LegSweep, base134);
        ushort neutral134 = TypedAbilityResistance.Npc(client, Dummy(false), LegSweep, scaled134);
        ushort resisted134 = TypedAbilityResistance.Npc(client, Dummy(true), LegSweep, scaled134);
        Assert.Equal((ushort)16, neutral134);
        Assert.Equal((ushort)13, resisted134);

        // 152 Spinning Backsweep: base 28 + 75 stealth = 103 -> scaled -> +75 Thrown resisted.
        var a152 = new AbilityItem(); a152.setAbilityID(SpinningBacksweep);
        ushort base152 = AbilityHandler.AbilityDamage(a152, new Random(0), characterLevel: FixtureLevel);
        Assert.Equal((ushort)28, base152);
        ushort scaled152 = AbilityStats.ScaleAbilityDamage(client, SpinningBacksweep, SneakAttacks.Add(base152, SpinningBacksweep, true));
        ushort neutral152 = TypedAbilityResistance.Npc(client, Dummy(false), SpinningBacksweep, scaled152);
        ushort resisted152 = TypedAbilityResistance.Npc(client, Dummy(true), SpinningBacksweep, scaled152);
        Assert.Equal((ushort)132, neutral152);
        Assert.Equal((ushort)108, resisted152);

        // 146 Poison Knife & 281 Wounding Throw ThrownDOT ticks (DamageType 7): neutral vs +75 Thrown resisted.
        Assert.Equal(7, TypedAbilityResistance.StoredType(PoisonKnife, dot: true));
        Assert.Equal(7, TypedAbilityResistance.StoredType(WoundingThrow, dot: true));

        Assert.True(TempMods.TryGet(PoisonKnife, out var e146));
        var dot146 = Assert.Single(e146.Mods, m => m.Proc == TempMods.Proc.Dot);
        int tick146 = TempMods.DotTick(AbilityStats.ScaleTypedAbilityDamage(client, 7, dot146.MinValue), dot146.Seconds, dot146.Interval);
        Assert.Equal((ushort)2, TypedAbilityResistance.Npc(client, Dummy(false), PoisonKnife, tick146, 7));
        Assert.Equal((ushort)1, TypedAbilityResistance.Npc(client, Dummy(true), PoisonKnife, tick146, 7));

        Assert.True(TempMods.TryGet(WoundingThrow, out var e281));
        var dot281 = Assert.Single(e281.Mods, m => m.Proc == TempMods.Proc.Dot);
        int tick281 = TempMods.DotTick(AbilityStats.ScaleTypedAbilityDamage(client, 7, dot281.MinValue), dot281.Seconds, dot281.Interval);
        Assert.Equal((ushort)8, TypedAbilityResistance.Npc(client, Dummy(false), WoundingThrow, tick281, 7));
        Assert.Equal((ushort)6, TypedAbilityResistance.Npc(client, Dummy(true), WoundingThrow, tick281, 7));
    }
}
