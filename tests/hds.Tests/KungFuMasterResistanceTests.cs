using System.Reflection;
using System.Runtime.CompilerServices;
using hds;

namespace hds.Tests;

[Collection("AbilityParityWorld")]
public class KungFuMasterResistanceTests
{
    private const ushort KungFu = CombatStyles.KungFu;                                   // 133
    private const ushort DimMakStrike = KungFuMasterSpecials.DimMakStrike;               // 259
    private const ushort MisdirectPunch = KungFuMasterSpecials.MisdirectPunch;           // 260
    private const ushort MachinegunFistCombo = KungFuMasterSpecials.MachinegunFistCombo; // 261
    private const ushort PistonKicks = KungFuMasterSpecials.PistonKicks;                 // 262
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
        CombatStyles.UseStyle(client, KungFu);
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
    public void KungFuMasterPlanMemoryCostsCooldownsAndNeutralVsResistedValuesAreExact()
    {
        var plan = NativeQa.NativeQaModes.Find("ability-parity-kung-fu-master-resistance").Plan;
        Assert.Equal(new[] { KungFu, DimMakStrike, MisdirectPunch, MachinegunFistCombo, PistonKicks }, plan.HotbarAbilities);
        Assert.Equal("1", plan.EnvironmentOverrides[CombatFixtureWeapons.ResetMode]);
        Assert.Equal("1", plan.EnvironmentOverrides["MXO_CONDITIONAL_POWERLESS_QA"]);
        Assert.Equal("awakened-interlock-resistance.csv", plan.TestMobFixture);

        int mem = plan.Abilities.Sum(AbilityMemory.Cost);
        Assert.Equal(47, mem);
        Assert.True(mem <= AbilityMemory.ForLevel(FixtureLevel), $"Memory {mem} exceeds level-{FixtureLevel} cap");

        var client = Client(plan.Abilities.ToArray());

        (ushort Id, ushort Base, ushort Neutral, ushort Resisted, ushort Cost, float Reuse, ushort Family)[] expected =
        {
            (DimMakStrike, 71, 95, 78, 20, 10f, 688),
            (MisdirectPunch, 24, 32, 26, 30, 10f, 688),
            (MachinegunFistCombo, 23, 31, 25, 30, 10f, 687),
            (PistonKicks, 108, 145, 119, 25, 10f, 687),
        };

        var actuals = new List<string>();
        foreach (var e in expected)
        {
            var item = new AbilityItem();
            item.setAbilityID(e.Id);
            ushort rawBase = AbilityHandler.AbilityDamage(item, new Random(0), characterLevel: FixtureLevel);
            ushort scaled = AbilityStats.ScaleAbilityDamage(client, e.Id, rawBase);
            ushort neutral = TypedAbilityResistance.Npc(client, Dummy(false), e.Id, scaled);
            ushort resisted = TypedAbilityResistance.Npc(client, Dummy(true), e.Id, scaled);
            Assert.Equal(0, TypedAbilityResistance.StoredType(e.Id));
            Assert.True(AbilityCooldowns.TryGet(e.Id, out var cd), $"Cooldown missing for {e.Id}");
            actuals.Add($"{e.Id}:{rawBase}/{neutral}/{resisted}/is={AbilityCosts.Cost(e.Id)}/cd={cd.Seconds}/{cd.Group}/{cd.GroupSeconds}");
        }

        string expectedStr = string.Join(" | ", expected.Select(e => $"{e.Id}:{e.Base}/{e.Neutral}/{e.Resisted}/is={e.Cost}/cd={e.Reuse}/{e.Family}/5"));
        Assert.Equal(expectedStr, string.Join(" | ", actuals));
    }
}
