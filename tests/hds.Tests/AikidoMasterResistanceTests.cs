using System.Reflection;
using System.Runtime.CompilerServices;
using hds;

namespace hds.Tests;

[Collection("AbilityParityWorld")]
public class AikidoMasterResistanceTests
{
    private const ushort Aikido = CombatStyles.Aikido;                                          // 101
    private const ushort CounterThrow = KarateAikidoMasterSpecials.CounterThrow;                // 273
    private const ushort MakiOtoshi = KarateAikidoMasterSpecials.MakiOtoshi;                    // 274
    private const ushort SpinningClayPigeon = MartialArtsRemainingSpecials.AikidoSpinningClayPigeon; // 296
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
        CombatStyles.UseStyle(client, Aikido);
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
    public void AikidoMasterPlanMemoryCostsCooldownsAndNeutralVsResistedValuesAreExact()
    {
        var plan = NativeQa.NativeQaModes.Find("ability-parity-aikido-master-resistance").Plan;
        Assert.Equal(new[] { Aikido, CounterThrow, MakiOtoshi, SpinningClayPigeon }, plan.HotbarAbilities);
        Assert.Equal("1", plan.EnvironmentOverrides[CombatFixtureWeapons.ResetMode]);
        Assert.Equal("1", plan.EnvironmentOverrides["MXO_CONDITIONAL_THROW_QA"]);
        Assert.Equal("awakened-interlock-resistance.csv", plan.TestMobFixture);

        int mem = plan.Abilities.Sum(AbilityMemory.Cost);
        Assert.True(mem <= AbilityMemory.ForLevel(FixtureLevel), $"Memory {mem} exceeds level-{FixtureLevel} cap");

        var client = Client(plan.Abilities.ToArray());

        (ushort Id, ushort Base, ushort Neutral, ushort Resisted, ushort Cost, float Reuse, ushort Group, float GroupSec)[] expected =
        {
            (CounterThrow, 56, 75, 61, 15, 10f, 687, 5f),
            (MakiOtoshi, 30, 40, 32, 30, 10f, 688, 5f),
            (SpinningClayPigeon, 375, 502, 413, 40, 40f, 0, 0f),
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

        string expectedStr = $"mem={mem} | " + string.Join(" | ", expected.Select(e => $"{e.Id}:{e.Base}/{e.Neutral}/{e.Resisted}/is={e.Cost}/cd={e.Reuse}/{e.Group}/{e.GroupSec}"));
        Assert.Equal(expectedStr, $"mem={mem} | " + string.Join(" | ", actuals));
        Assert.Equal(48, mem);
    }
}
