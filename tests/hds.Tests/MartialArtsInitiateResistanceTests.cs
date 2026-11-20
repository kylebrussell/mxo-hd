using System.Reflection;
using System.Runtime.CompilerServices;
using hds;

namespace hds.Tests;

[Collection("DelayedActions")]
public class MartialArtsInitiateResistanceTests
{
    private const ushort Aikido = CombatStyles.Aikido;                            // 101
    private const ushort KungFu = CombatStyles.KungFu;                            // 133
    private const ushort GuardBreaker = MartialArtsStarterAdmission.GuardBreaker; // 138
    private const ushort IronGuard = MartialArtsStarterAdmission.IronGuard;       // 139
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
        client.playerInstance.InnerStrengthMax.setValue((ushort)1000);
        client.playerInstance.InnerStrengthAvailable.setValue((ushort)1000);
        client.messageQueue = new MessageQueue();
        typeof(WorldClient).GetField("lastFlushTick", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(client, long.MaxValue);
        return client;
    }

    private static Mob Dummy(bool resisted)
    {
        var mob = new Mob();
        mob.setEntityId(990001);
        mob.setName("Melee Dummy");
        mob.setLevel(FixtureLevel);
        mob.setHealthM(30000);
        mob.setHealthC(30000);
        if (resisted)
        {
            TimedEffects.Apply(TimedEffects.ForMob(mob), new TempMods.Effect
            {
                Mods = new[]
                {
                    new TempMods.Mod
                    {
                        Kind = TempMods.KindStat, Proc = TempMods.Proc.Stat, Target = 180,
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
    public void GuardBreaker138AndIronGuard139NeutralAndResistedDamagesBuffsAndChoreographiesAreExact()
    {
        var plan = NativeQa.NativeQaModes.Find("ability-parity-ma-initiate-resistance").Plan;
        Assert.Equal("awakened-interlock-resistance.csv", plan.TestMobFixture);
        Assert.Equal("1", plan.EnvironmentOverrides[CombatFixtureWeapons.ResetMode]);
        Assert.Equal("1", plan.EnvironmentOverrides["MXO_CONDITIONAL_THROW_QA"]);
        Assert.Equal(new[] { Aikido, KungFu, GuardBreaker, IronGuard }, plan.HotbarAbilities);

        int mem = plan.Abilities.Sum(AbilityMemory.Cost);
        Assert.True(mem <= AbilityMemory.ForLevel(FixtureLevel));

        var client = Client(FixtureLevel);
        AbilityPassives.SetLoaded(client, plan.Abilities.Select(id => (id, (ushort)FixtureRank)).ToArray());

        // Iron Guard (139) in Aikido (101): base 12 -> scaled -> resisted at +75 Melee Damage Resistance.
        CombatStyles.Select(client, Aikido);
        var a139 = new AbilityItem(); a139.setAbilityID(IronGuard);
        ushort base139 = AbilityHandler.AbilityDamage(a139, new Random(0), characterLevel: FixtureLevel);
        Assert.Equal((ushort)12, base139);
        ushort scaled139 = AbilityStats.ScaleAbilityDamage(client, IronGuard, base139);
        ushort neutral139 = TypedAbilityResistance.Npc(client, Dummy(resisted: false), IronGuard, scaled139);
        ushort resisted139 = TypedAbilityResistance.Npc(client, Dummy(resisted: true), IronGuard, scaled139);
        Assert.Equal(scaled139, neutral139);
        Assert.Equal((ushort)15, neutral139);
        Assert.Equal((ushort)12, resisted139);
        Assert.Equal(738u, CombatAnimations.Special(IronGuard, true, CombatTactic.Speed)!.Id);
        Assert.True(CombatSpecials.TryCasterBuff(IronGuard, out var buff139));
        Assert.Equal(12f, buff139.Seconds);
        Assert.Contains(buff139.Modifiers, m => m.Stat == 1009 && m.Value == 10);
        Assert.Contains(buff139.Modifiers, m => m.Stat == 1010 && m.Value == 10);

        // Guard Breaker (138) in Kung-Fu (133): base 36 -> scaled -> resisted at +75 Melee Damage Resistance.
        CombatStyles.Select(client, KungFu);
        var a138 = new AbilityItem(); a138.setAbilityID(GuardBreaker);
        ushort base138 = AbilityHandler.AbilityDamage(a138, new Random(0), characterLevel: FixtureLevel);
        Assert.Equal((ushort)36, base138);
        ushort scaled138 = AbilityStats.ScaleAbilityDamage(client, GuardBreaker, base138);
        ushort neutral138 = TypedAbilityResistance.Npc(client, Dummy(resisted: false), GuardBreaker, scaled138);
        ushort resisted138 = TypedAbilityResistance.Npc(client, Dummy(resisted: true), GuardBreaker, scaled138);
        Assert.Equal(scaled138, neutral138);
        Assert.Equal((ushort)47, neutral138);
        Assert.Equal((ushort)38, resisted138);
        Assert.Equal(5951u, CombatAnimations.Special(GuardBreaker, true, CombatTactic.Speed)!.Id);
        Assert.True(TempMods.TryGet(GuardBreaker, out var debuff138));
        Assert.Contains(debuff138.Mods, m => m.Target == 1009 && m.MinValue == -10 && m.Seconds == 15f);
    }
}
