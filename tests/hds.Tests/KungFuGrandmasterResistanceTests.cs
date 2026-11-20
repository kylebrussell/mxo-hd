using System.Reflection;
using System.Runtime.CompilerServices;
using hds;

namespace hds.Tests;

[Collection("AbilityParityWorld")]
public class KungFuGrandmasterResistanceTests
{
    private const ushort KungFu = CombatStyles.KungFu;                                          // 133
    private const ushort TripleFrontKick = KungFuGrandmasterSpecials.TripleFrontKick;           // 427
    private const ushort SuicidalButterfly = KungFuGrandmasterSpecials.SuicidalButterfly;       // 428
    private const ushort ExtremeFallingKick = KungFuGrandmasterSpecials.ExtremeFallingKick;     // 429
    private const ushort WoodenDummyDrill = KungFuGrandmasterSpecials.WoodenDummyDrill;         // 430
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
    public void KungFuGrandmasterPlanMemoryCostsCooldownsAndNeutralVsResistedValuesAreExact()
    {
        var plan = NativeQa.NativeQaModes.Find("ability-parity-kung-fu-grandmaster-resistance").Plan;
        Assert.Equal(new[] { KungFu, TripleFrontKick, SuicidalButterfly, ExtremeFallingKick, WoodenDummyDrill }, plan.HotbarAbilities);
        Assert.Equal("1", plan.EnvironmentOverrides[CombatFixtureWeapons.ResetMode]);
        Assert.Equal("1", plan.EnvironmentOverrides["MXO_CONDITIONAL_THROW_QA"]);
        Assert.Equal("awakened-interlock-resistance.csv", plan.TestMobFixture);

        int mem = plan.Abilities.Sum(AbilityMemory.Cost);
        Assert.True(mem <= AbilityMemory.ForLevel(FixtureLevel), $"Memory {mem} exceeds level-{FixtureLevel} cap");

        var client = Client(plan.Abilities.ToArray());

        (ushort Id, ushort Base, ushort Neutral, ushort Resisted, ushort Cost, float Reuse, ushort Group)[] expected =
        {
            (TripleFrontKick, 91, 124, 102, 25, 10f, 687),
            (SuicidalButterfly, 25, 34, 27, 35, 10f, 688),
            (ExtremeFallingKick, 46, 63, 51, 40, 10f, 688),
            (WoodenDummyDrill, 107, 146, 120, 30, 10f, 687),
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

        string expectedStr = $"mem={mem} | " + string.Join(" | ", expected.Select(e => $"{e.Id}:{e.Base}/{e.Neutral}/{e.Resisted}/is={e.Cost}/cd={e.Reuse}/{e.Group}/5"));
        Assert.Equal(expectedStr, $"mem={mem} | " + string.Join(" | ", actuals));
        Assert.Equal(61, mem);
    }
}
