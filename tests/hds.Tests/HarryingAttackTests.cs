using System.Reflection;
using System.Runtime.CompilerServices;
using hds;

namespace hds.Tests;

public class HarryingAttackTests
{
    private static WorldClient Client(byte level = 11)
    {
        var client = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        client.playerData = new ClientData();
        client.playerInstance = new PlayerCharacter();
        client.playerInstance.Level.setValue(level);
        client.playerInstance.Health.setValue((ushort)3000);
        client.playerInstance.MaxHealth.setValue((ushort)3000);
        client.messageQueue = new MessageQueue();
        typeof(WorldClient).GetField("lastFlushTick", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(client, long.MaxValue);
        return client;
    }

    [Theory]
    [InlineData(1, 19)]
    [InlineData(11, 49)]
    [InlineData(15, 61)]
    [InlineData(19, 73)]
    [InlineData(20, 76)]
    [InlineData(50, 76)]
    public void HarryingAttackUsesShippedValueFunction3ScaledBaseWhenLegacyRangeIsIncomplete(int level, ushort expected)
    {
        string root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "hds.sln")))
            root = Path.GetDirectoryName(root)!;
        string[] row = File.ReadLines(Path.Combine(root, "data", "abilityIDs.csv"))
            .Single(line => line.StartsWith("1127;", StringComparison.Ordinal)).Split(';');

        var ability = new AbilityItem();
        ability.setAbilityID(1127);
        ability.setValueFrom(short.Parse(row[9]));
        ability.setValueTo(ushort.Parse(row[10]));

        Assert.Equal(0, ability.getValueFrom());
        Assert.Equal(0, ability.getValueTo());
        Assert.Equal(7, TypedAbilityResistance.StoredType(1127));

        foreach (int seed in new[] { 1, 11, 987 })
            Assert.Equal(expected, AbilityHandler.AbilityDamage(ability, new Random(seed), characterLevel: level));

        ability.setValueFrom(31);
        ability.setValueTo(31);
        Assert.Equal((ushort)31, AbilityHandler.AbilityDamage(ability, new Random(1), characterLevel: level));
    }

    [Fact]
    public void HarryingAttackLevel11InfluenceControlsTimersAndAdmissionMatchShippedProfile()
    {
        var caster = Client(11);
        var target = Client(11);
        AbilityPassives.SetLoaded(caster, new (ushort, ushort)[] { (2, 11), (1112, 11) });
        CombatStyles.Select(caster, 1112);

        // Rank-11 Spy Style: 4 + trunc(11 * 0.5) = +9% thrown damage -> trunc(49 * 1.09) = 53.
        Assert.Equal(9, AbilityStats.Influence(caster, 1034));
        Assert.Equal((ushort)53, AbilityStats.ScaleAbilityDamage(caster, 1127, 49));
        Assert.Equal((ushort)53, TypedAbilityResistance.Player(caster, target, 1127, 53));

        Assert.Equal((ushort)25, AbilityCosts.Cost(1127));
        Assert.Equal(2f, AbilityCatalog.Find(1127).CastSeconds);
        Assert.True(AbilityCooldowns.TryGet(1127, out var cd));
        Assert.Equal(45f, cd.Seconds);
        Assert.Equal((ushort)691, cd.Group);
        Assert.Equal(15f, cd.GroupSeconds);
        Assert.True(HarryingAttackDamage.RetainsStealthDuringCast(1127));

        Assert.True(HarryingAttackDamage.TryRefuse(caster, 1127, 0, null, out uint unstealthed));
        Assert.Equal(AbilityRefusals.CasterStateRequired, unstealthed);
        caster.playerData.conditionStateFlags = Stealth.StealthedFlag;
        Assert.True(HarryingAttackDamage.TryRefuse(caster, 1127, 0, null, out uint reason));
        Assert.Equal(AbilityRefusals.InvalidTarget, reason);

        Assert.True(TempMods.TryGet(1127, out var effect));
        Assert.Contains(effect.Mods, m => m.Target == TempMods.DazeAbility && m.Seconds == 12f && Math.Abs(m.Chance - 0.3f) < 0.001f);
        Assert.Contains(effect.Mods, m => m.Target == TempMods.OffBalanceAbility && m.Seconds == 12f && Math.Abs(m.Chance - 0.3f) < 0.001f);
        Assert.Contains(effect.Mods, m => m.Target == TempMods.StaggeredAbility && m.Seconds == 12f && Math.Abs(m.Chance - 0.3f) < 0.001f);
    }
}
