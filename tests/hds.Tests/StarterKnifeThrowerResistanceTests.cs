using System.Reflection;
using System.Runtime.CompilerServices;
using hds;

namespace hds.Tests;

[Collection("AbilityParityWorld")]
public class StarterKnifeThrowerResistanceTests
{
    private const ushort SpyStyle = CombatStyles.SpyStyle;                  // 1112
    private const ushort SneakGrant = Stealth.SneakGrantAbility;            // 1111
    private const ushort DeadlyThrow = DeadlyThrowDamage.Id;                // 279
    private const ushort CripplingThrow = SpyAbilityDamage.CripplingThrow;  // 280
    private const ushort HarryingAttack = HarryingAttackDamage.Id;          // 1127
    private const ushort DistractingBlow = hds.DistractingBlow.Id;          // 1128
    private const ushort DazzlingThrow = DazzlingThrowDamage.Id;            // 1132
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
        CombatStyles.UseStyle(client, SpyStyle);
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
                        Target = 1033,
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
    public void StarterKnifeThrowerPlanMemoryCostsAndNeutralVsResistedThrownValuesAreExact()
    {
        var plan = NativeQa.NativeQaModes.Find("ability-parity-starter-knife-thrower-resistance").Plan;
        Assert.Equal(new[] { SpyStyle, SneakGrant, DeadlyThrow, CripplingThrow, HarryingAttack, DistractingBlow, DazzlingThrow }, plan.HotbarAbilities);
        Assert.Equal("1", plan.EnvironmentOverrides[CombatFixtureWeapons.ResetMode]);
        Assert.Equal("1", plan.EnvironmentOverrides["MXO_CONDITIONAL_THROW_QA"]);
        Assert.Equal("awakened-interlock-resistance.csv", plan.TestMobFixture);

        int mem = plan.Abilities.Sum(AbilityMemory.Cost);
        Assert.Equal(57, mem);
        Assert.True(mem <= AbilityMemory.ForLevel(FixtureLevel), $"Memory {mem} exceeds level-{FixtureLevel} cap");

        var client = Client(plan.Abilities.ToArray());
        Assert.Equal(54, AbilityStats.Influence(client, 1034));

        (ushort Id, bool Stealth, ushort Base, ushort Neutral, ushort Resisted, ushort Cost)[] expected =
        {
            (DeadlyThrow, false, 44, 67, 55, 18),
            (CripplingThrow, false, 1, 1, 0, 20),
            (HarryingAttack, true, 76, 117, 96, 25),
            (DistractingBlow, true, 114, 175, 143, 35),
            (DazzlingThrow, true, 43, 66, 54, 35),
        };

        foreach (var e in expected)
        {
            var item = new AbilityItem();
            item.setAbilityID(e.Id);
            ushort rawBase = AbilityHandler.AbilityDamage(item, new Random(0), characterLevel: FixtureLevel);
            ushort withStealth = SneakAttacks.Add(rawBase, e.Id, e.Stealth);
            ushort scaled = AbilityStats.ScaleAbilityDamage(client, e.Id, withStealth);
            ushort neutral = TypedAbilityResistance.Npc(client, Dummy(false), e.Id, scaled);
            ushort resisted = TypedAbilityResistance.Npc(client, Dummy(true), e.Id, scaled);
            Assert.Equal(7, TypedAbilityResistance.StoredType(e.Id));
            Assert.Equal(e.Base, rawBase);
            Assert.Equal(e.Neutral, neutral);
            Assert.Equal(e.Resisted, resisted);
            Assert.Equal(e.Cost, AbilityCosts.Cost(e.Id));
        }
    }
}
