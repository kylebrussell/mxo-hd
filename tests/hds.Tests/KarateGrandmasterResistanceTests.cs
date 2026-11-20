using System.Reflection;
using System.Runtime.CompilerServices;
using hds;

namespace hds.Tests;

[Collection("AbilityParityWorld")]
public class KarateGrandmasterResistanceTests
{
    private const ushort Karate = CombatStyles.Karate;                                      // 132
    private const ushort MachinegunKick = KarateAikidoGrandmasterSpecials.MachinegunKick;   // 532
    private const ushort WristThrow = KarateAikidoGrandmasterSpecials.WristThrow;           // 534
    private const ushort KiChargedFootSweep = KarateAikidoGrandmasterSpecials.KiChargedFootSweep; // 536
    private const byte FixtureLevel = 50, FixtureRank = 50;

    private static WorldClient Client(ushort[] loadout)
    {
        var client = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        client.playerData = new ClientData();
        client.playerData.setCharID(110);
        client.playerData.setDistrictId(1);
        client.playerData.setOnWorld(true);
        client.playerData.isBot = true;
        client.playerInstance = new PlayerCharacter();
        client.playerInstance.Level.setValue(FixtureLevel);
        client.playerInstance.Health.setValue((ushort)5000);
        client.playerInstance.MaxHealth.setValue((ushort)5000);
        client.playerInstance.InnerStrengthMax.setValue((ushort)2000);
        client.playerInstance.InnerStrengthAvailable.setValue((ushort)2000);
        client.messageQueue = new MessageQueue();
        typeof(WorldClient).GetField("lastFlushTick", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(client, long.MaxValue);
        AbilityPassives.SetLoaded(client, loadout.Select(id => (id, (ushort)FixtureRank)).ToArray());
        CombatStyles.UseStyle(client, Karate);
        return client;
    }

    private static Mob Dummy(bool resisted)
    {
        var mob = new Mob();
        mob.setName("Melee Dummy");
        mob.setLevel(FixtureLevel);
        mob.setHealthM(10000);
        mob.setHealthC(10000);
        if (resisted)
        {
            TimedEffects.Apply(TimedEffects.ForMob(mob), new TempMods.Effect
            {
                Ability = 720,
                Mods = new[]
                {
                    new TempMods.Mod
                    {
                        Kind = TempMods.KindStat,
                        Proc = TempMods.Proc.Stat,
                        Target = 180,
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
    public void KarateGrandmasterPlanMemoryCostsAndNeutralVsResistedValuesAreExact()
    {
        var plan = NativeQa.NativeQaModes.Find("ability-parity-karate-grandmaster-resistance").Plan;
        Assert.Equal(new[] { Karate, WristThrow, MachinegunKick, KiChargedFootSweep }, plan.HotbarAbilities);
        Assert.Equal("1", plan.EnvironmentOverrides[CombatFixtureWeapons.ResetMode]);
        Assert.Equal("1", plan.EnvironmentOverrides["MXO_CONDITIONAL_POWERLESS_QA"]);
        Assert.Equal("awakened-interlock-resistance.csv", plan.TestMobFixture);

        int mem = plan.Abilities.Sum(AbilityMemory.Cost);
        Assert.Equal(60, mem);
        Assert.True(mem <= AbilityMemory.ForLevel(FixtureLevel), $"Memory {mem} exceeds level-{FixtureLevel} cap");

        var client = Client(plan.Abilities.ToArray());
        Assert.Equal(43, AbilityStats.Influence(client, 159));

        (ushort Id, ushort Base, ushort Neutral, ushort Resisted, ushort Cost, ushort Group)[] expected =
        {
            (WristThrow, 31, 44, 36, 65, 688),
            (MachinegunKick, 73, 104, 85, 32, 688),
            (KiChargedFootSweep, 134, 191, 157, 45, 687),
        };

        foreach (var e in expected)
        {
            var item = new AbilityItem();
            item.setAbilityID(e.Id);
            ushort b = AbilityHandler.AbilityDamage(item, new Random(0), characterLevel: FixtureLevel);
            ushort s = AbilityStats.ScaleAbilityDamage(client, e.Id, b);
            ushort n = TypedAbilityResistance.Npc(client, Dummy(false), e.Id, s);
            ushort r = TypedAbilityResistance.Npc(client, Dummy(true), e.Id, s);
            Assert.True(AbilityCooldowns.TryGet(e.Id, out var cd));
            Assert.Equal(0, TypedAbilityResistance.StoredType(e.Id));
            Assert.Equal(e.Base, b);
            Assert.Equal(e.Neutral, n);
            Assert.Equal(e.Resisted, r);
            Assert.Equal(e.Cost, AbilityCosts.Cost(e.Id));
            Assert.Equal(10, cd.Seconds);
            Assert.Equal(5, cd.GroupSeconds);
            Assert.Equal(e.Group, cd.Group);
        }
    }
}
