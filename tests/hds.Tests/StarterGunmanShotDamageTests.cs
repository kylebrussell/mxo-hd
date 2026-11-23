using System.Runtime.CompilerServices;
using hds;

namespace hds.Tests;

public class StarterGunmanShotDamageTests
{
    [Theory]
    [InlineData(242, 20, 0, 5)]
    [InlineData(244, 0, 0, 18)]
    [InlineData(105, -5, 0, 8)]
    [InlineData(253, 6, 0, 6)]
    [InlineData(176, 50, 0, 17)]
    [InlineData(243, 0, 0, 16)]
    [InlineData(CombatSpecials.ControlledBurst, 0, 0, 26)]
    [InlineData(247, 25, 0, 25)]
    [InlineData(248, 0, 0, 15)]
    [InlineData(CombatSpecials.SlidingVolley, 0, 0, 77)]
    public void IncompleteLegacyEndpointsUseFixedBallisticBase(ushort id, short oldMin, ushort oldMax, ushort expected)
    {
        string root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "hds.sln")))
            root = Path.GetDirectoryName(root)!;
        string[] row = File.ReadLines(Path.Combine(root, "data", "abilityIDs.csv"))
            .Single(line => line.StartsWith($"{id};", StringComparison.Ordinal)).Split(';');
        var item = new AbilityItem();
        item.setAbilityID(id);
        item.setValueFrom(short.Parse(row[9]));
        item.setValueTo(ushort.Parse(row[10]));

        Assert.Equal(oldMin, item.getValueFrom());
        Assert.Equal(oldMax, item.getValueTo());
        Assert.Equal(1, TypedAbilityResistance.StoredType(id));
        Assert.False(AbilityTargets.Retail.ContainsKey(id));
        Assert.Null(CombatSpecials.Damage(id, null));
        foreach (int seed in new[] { 1, 2, 987 })
            Assert.Equal(expected, AbilityHandler.AbilityDamage(item, new Random(seed)));

        item.setValueFrom(29);
        item.setValueTo(29);
        Assert.Equal(29, AbilityHandler.AbilityDamage(item, new Random(1)));
    }

    [Fact]
    public void BodyShotRetainsBothTenSecondResistanceDebuffs()
    {
        Assert.True(TempMods.TryGet(105, out var effect));
        var applied = TimedEffects.Instantiate(effect, new Random(1)).OrderBy(modifier => modifier.Target).ToArray();
        Assert.Equal(new ushort[] { 180, 181 }, applied.Select(modifier => modifier.Target));
        Assert.All(applied, modifier =>
        {
            Assert.Equal(-5, modifier.Value);
            Assert.Equal(10, modifier.Seconds);
        });
    }

    [Theory]
    [InlineData(247, 25, 28, 26)]
    [InlineData(248, 15, 17, 15)]
    public void DualSmgConesUseTheMatchingBallisticInfluenceAndResistance(ushort ability, int basis, ushort raw, ushort resisted)
    {
        var caster = PlayerWithStats((158, 171, 9), (125, 1139, 3), (246, 1139, 4));
        var target = PlayerWithStats((1061, 181, 20));
        Assert.Equal(16, AbilityStats.Influence(caster, 171));
        Assert.Equal(raw, AbilityStats.ScaleAbilityDamage(caster, ability, basis));
        Assert.Equal(resisted, TypedAbilityResistance.Player(caster, target, ability, raw));

        static WorldClient PlayerWithStats(params (ushort Source, ushort Stat, int Value)[] modifiers)
        {
            var player = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
            player.playerData = new ClientData();
            player.playerInstance = new PlayerCharacter();
            player.playerInstance.Level.setValue((byte)33);
            foreach (var (source, stat, value) in modifiers)
                TimedEffects.ForPlayer(player).Effects.Apply(source,
                    new[] { new TimedEffects.Active { Proc = TempMods.Proc.Stat, Target = stat, Value = value, Seconds = 10 } },
                    TimeUtils.GetSimTimeSeconds());
            return player;
        }
    }

    [Fact]
    public void CoveringFireRetainsItsExistingRootRollAndBreakPolicy()
    {
        Assert.True(TempMods.TryGet(176, out var effect));
        Assert.Empty(TimedEffects.Instantiate(effect, new FractionRoll(.151)));
        var root = Assert.Single(TimedEffects.Instantiate(effect, new FractionRoll(.149)));
        Assert.Equal(TempMods.RootedAbility, root.Target);
        Assert.Equal(8, root.Seconds);
        Assert.Equal(TempMods.BreakRule.Chance, root.Break);
        Assert.Equal(50, root.BreakChance);
        Assert.True(root.ProtectFirstHealthChange);
    }

    private sealed class FractionRoll(double roll) : Random
    {
        public override double NextDouble() => roll;
    }
}
