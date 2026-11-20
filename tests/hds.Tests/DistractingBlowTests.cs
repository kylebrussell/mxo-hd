using System.Reflection;
using System.Runtime.CompilerServices;
using hds;

namespace hds.Tests;

[Collection("DelayedActions")]
public class DistractingBlowTests
{
    private static WorldClient Client(byte level = 20)
    {
        var client = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        client.playerData = new ClientData();
        client.playerData.setDistrictId(1);
        client.playerInstance = new PlayerCharacter();
        client.playerInstance.Level.setValue(level);
        client.playerInstance.Health.setValue((ushort)3000);
        client.playerInstance.MaxHealth.setValue((ushort)3000);
        client.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(24499, 495, -1750));
        client.messageQueue = new MessageQueue();
        typeof(WorldClient).GetField("lastFlushTick", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(client, long.MaxValue);
        return client;
    }

    [Theory]
    [InlineData(1, 19)]
    [InlineData(20, 68)]
    [InlineData(30, 94)]
    [InlineData(38, 114)]
    [InlineData(50, 114)]
    public void DistractingBlowUsesShippedValueFunction3ScaledBaseWhenLegacyRangeIsIncomplete(int level, ushort expected)
    {
        string root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "hds.sln")))
            root = Path.GetDirectoryName(root)!;
        string[] row = File.ReadLines(Path.Combine(root, "data", "abilityIDs.csv"))
            .Single(line => line.StartsWith("1128;", StringComparison.Ordinal)).Split(';');

        var ability = new AbilityItem();
        ability.setAbilityID(1128);
        ability.setValueFrom(short.Parse(row[9]));
        ability.setValueTo(ushort.Parse(row[10]));

        Assert.Equal(1129, ability.getValueFrom());
        Assert.Equal(0, ability.getValueTo());
        Assert.Equal(7, TypedAbilityResistance.StoredType(1128));

        foreach (int seed in new[] { 1, 20, 987 })
            Assert.Equal(expected, AbilityHandler.AbilityDamage(ability, new Random(seed), characterLevel: level));

        ability.setValueFrom(40);
        ability.setValueTo(40);
        Assert.Equal((ushort)40, AbilityHandler.AbilityDamage(ability, new Random(1), characterLevel: level));
    }

    [Fact]
    public void DistractingBlowLandingAppliesEffect1129ContestedWithdrawBuffAndExpires()
    {
        var caster = Client(20);
        Assert.True(AbilityBuffs.TryGet(1129, out var buff));
        Assert.Equal(12f, buff.Seconds);
        var mod = Assert.Single(buff.Modifiers);
        Assert.Equal((ushort)1005, mod.Stat);
        Assert.Equal(50, mod.Value);

        Assert.Equal(0, AbilityStats.Player(caster, 1005));
        SpyAbilityRegistry.Landed(caster, 1128);
        Assert.Equal(50, AbilityStats.Player(caster, 1005));
        Assert.True(AbilityBuffs.Remove(caster, 1129));
        Assert.Equal(0, AbilityStats.Player(caster, 1005));

        // Dead caster does not trigger ApplyAuraEffect (tempmods.py:1310 Health > 0 check).
        caster.playerData.isDead = true;
        SpyAbilityRegistry.Landed(caster, 1128);
        Assert.Equal(0, AbilityStats.Player(caster, 1005));
    }

    [Fact]
    public void DistractingBlowLevel20InfluenceTimersStealthAndAdmissionMatchShippedProfile()
    {
        var caster = Client(20);
        AbilityPassives.SetLoaded(caster, new (ushort, ushort)[] { (2, 20), (1112, 20) });
        CombatStyles.Select(caster, 1112);

        // Rank-20 Spy Style (+14% thrown damage): trunc(68 * 1.14) = 77.
        Assert.Equal(14, AbilityStats.Influence(caster, 1034));
        Assert.Equal((ushort)77, AbilityStats.ScaleAbilityDamage(caster, 1128, 68));

        Assert.Equal((ushort)35, AbilityCosts.Cost(1128));
        Assert.Equal(2f, AbilityCatalog.Find(1128).CastSeconds);
        Assert.True(AbilityCooldowns.TryGet(1128, out var cd));
        Assert.Equal(45f, cd.Seconds);
        Assert.Equal((ushort)691, cd.Group);
        Assert.Equal(15f, cd.GroupSeconds);
        Assert.True(DistractingBlow.RetainsStealthDuringCast(1128));

        Assert.True(DistractingBlow.TryRefuse(caster, 1128, 0, null, out uint reason));
        Assert.Equal(AbilityRefusals.InvalidTarget, reason);
    }
}
