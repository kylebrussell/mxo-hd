using System.Reflection;
using System.Runtime.CompilerServices;
using hds;

namespace hds.Tests;

public class DazzlingThrowTests
{
    private static WorldClient Client(byte level = 23)
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

    [Fact]
    public void DazzlingThrowUsesShippedFixedThrownBase43WhenLegacyRangeIsIncomplete()
    {
        string root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "hds.sln")))
            root = Path.GetDirectoryName(root)!;
        string[] row = File.ReadLines(Path.Combine(root, "data", "abilityIDs.csv"))
            .Single(line => line.StartsWith("1132;", StringComparison.Ordinal)).Split(';');

        var ability = new AbilityItem();
        ability.setAbilityID(1132);
        ability.setValueFrom(short.Parse(row[9]));
        ability.setValueTo(ushort.Parse(row[10]));

        Assert.Equal(43, ability.getValueFrom());
        Assert.Equal(0, ability.getValueTo());
        Assert.Equal(7, TypedAbilityResistance.StoredType(1132));

        foreach (int seed in new[] { 1, 23, 987 })
            Assert.Equal((ushort)43, AbilityHandler.AbilityDamage(ability, new Random(seed)));

        ability.setValueFrom(60);
        ability.setValueTo(60);
        Assert.Equal((ushort)60, AbilityHandler.AbilityDamage(ability, new Random(1)));
    }

    [Fact]
    public void DazzlingThrowFullLevel23ClosureComputesExactInfluenceDamageControlsAndTimers()
    {
        var caster = Client(23);
        var roots = new ushort[] { 2, 17, 151, 1112, 1132 };
        var closure = roots.Concat(roots.SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys)).Distinct().ToArray();
        AbilityPassives.SetLoaded(caster, closure.Select(id => (id, (ushort)23)).ToArray());
        CombatStyles.Select(caster, 1112);

        // Full rank-23 closure: Spy Style (+15) + Weak Points 164 (+4) + Wicked Aim 278 (+6) = +25% thrown damage -> trunc(43 * 1.25) = 53.
        Assert.Equal(25, AbilityStats.Influence(caster, 1034));
        Assert.Equal((ushort)53, AbilityStats.ScaleAbilityDamage(caster, 1132, 43));

        Assert.Equal((ushort)35, AbilityCosts.Cost(1132));
        Assert.Equal(2f, AbilityCatalog.Find(1132).CastSeconds);
        Assert.True(AbilityCooldowns.TryGet(1132, out var cd));
        Assert.Equal(45f, cd.Seconds);
        Assert.Equal((ushort)691, cd.Group);
        Assert.Equal(15f, cd.GroupSeconds);
        Assert.True(DazzlingThrowDamage.RetainsStealthDuringCast(1132));

        Assert.True(DazzlingThrowDamage.TryRefuse(caster, 1132, 0, null, out uint reason));
        Assert.Equal(AbilityRefusals.InvalidTarget, reason);

        Assert.Equal((ushort)199, TempMods.DazeAbility);
        Assert.True(TempMods.TryGet(1132, out var effect));
        var daze = Assert.Single(effect.Mods, m => m.Target == TempMods.DazeAbility);
        Assert.Equal(12f, daze.Seconds);
        Assert.Equal(12f, AbilityControlDurations.Scale(caster, TempMods.DazeAbility, daze.Seconds), 4);
        Assert.InRange(daze.Chance, 0.499f, 0.501f);
    }
}
