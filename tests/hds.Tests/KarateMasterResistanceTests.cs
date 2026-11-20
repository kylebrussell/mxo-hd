using System.Reflection;
using System.Runtime.CompilerServices;
using hds;

namespace hds.Tests;

[Collection("AbilityParityWorld")]
public class KarateMasterResistanceTests
{
    private const ushort Karate = CombatStyles.Karate;                                     // 132
    private const ushort KiChargedPunch = KarateAikidoMasterSpecials.KiChargedPunch;       // 265
    private const ushort SidekickCombo = KarateAikidoMasterSpecials.SidekickCombo;         // 266
    private const ushort SkyHighSidekick = KarateAikidoMasterSpecials.SkyHighSidekick;     // 267
    private const ushort SwirlingKiSummon = MartialArtsRemainingSpecials.SwirlingKiSummon; // 268
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
    public void KarateMasterPlanMemoryCostsCooldownsAndNeutralVsResistedValuesAreExact()
    {
        var plan = NativeQa.NativeQaModes.Find("ability-parity-karate-master-resistance").Plan;
        Assert.Equal(new[] { Karate, KiChargedPunch, SidekickCombo, SkyHighSidekick, SwirlingKiSummon }, plan.HotbarAbilities);
        Assert.Equal("1", plan.EnvironmentOverrides[CombatFixtureWeapons.ResetMode]);
        Assert.Equal("1", plan.EnvironmentOverrides["MXO_CONDITIONAL_POWERLESS_QA"]);
        Assert.Equal("awakened-interlock-resistance.csv", plan.TestMobFixture);

        int mem = plan.Abilities.Sum(AbilityMemory.Cost);
        Assert.True(mem <= AbilityMemory.ForLevel(FixtureLevel), $"Memory {mem} exceeds level-{FixtureLevel} cap");

        var client = Client(plan.Abilities.ToArray());

        (ushort Id, ushort Base, ushort Neutral, ushort Resisted, ushort Cost, float Reuse, ushort Group)[] expected =
        {
            (KiChargedPunch, 68, 93, 76, 30, 10f, 688),
            (SidekickCombo, 28, 38, 31, 25, 10f, 688),
            (SkyHighSidekick, 21, 28, 23, 30, 10f, 687),
            (SwirlingKiSummon, 2, 2, 1, 60, 12f, 687),
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
        Assert.Equal(47, mem);
    }
}
