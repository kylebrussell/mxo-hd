using System.Reflection;
using System.Runtime.CompilerServices;
using hds;

namespace hds.Tests;

public class BullDogDamageTests
{
    private static WorldClient Client(byte level = 19)
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
    public void BullDogUsesShippedFixedThrownBase75WhenLegacyRangeIsIncomplete()
    {
        string root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "hds.sln")))
            root = Path.GetDirectoryName(root)!;
        string[] row = File.ReadLines(Path.Combine(root, "data", "abilityIDs.csv"))
            .Single(line => line.StartsWith("387;", StringComparison.Ordinal)).Split(';');

        var ability = new AbilityItem();
        ability.setAbilityID(387);
        ability.setValueFrom(short.Parse(row[9]));
        ability.setValueTo(ushort.Parse(row[10]));

        Assert.Equal(75, ability.getValueFrom());
        Assert.Equal(0, ability.getValueTo());
        Assert.Equal(7, TypedAbilityResistance.StoredType(387));

        foreach (int seed in new[] { 1, 19, 987 })
            Assert.Equal((ushort)75, AbilityHandler.AbilityDamage(ability, new Random(seed)));

        ability.setValueFrom(90);
        ability.setValueTo(90);
        Assert.Equal((ushort)90, AbilityHandler.AbilityDamage(ability, new Random(1)));
    }

    [Fact]
    public void BullDogLevel19InfluenceStealthOpenerResistanceAndTimersMatchShippedProfile()
    {
        var caster = Client(19);
        var target = Client(19);
        AbilityPassives.SetLoaded(caster, new (ushort, ushort)[] { (2, 19), (1112, 19) });
        CombatStyles.Select(caster, 1112);

        // Rank-19 Spy Style: 4 + trunc(19 * 0.5) = +13% thrown damage.
        Assert.Equal(13, AbilityStats.Influence(caster, 1034));
        Assert.Equal((ushort)84, AbilityStats.ScaleAbilityDamage(caster, 387, SneakAttacks.Add(75, 387, fromStealth: false)));
        Assert.Equal((ushort)1158, AbilityStats.ScaleAbilityDamage(caster, 387, SneakAttacks.Add(75, 387, fromStealth: true)));

        Assert.True(AbilityBuffs.Apply(target, 5));
        ushort resisted = TypedAbilityResistance.Player(caster, target, 387, 1158);
        Assert.True(resisted < 1158);
        Assert.True(AbilityBuffs.Remove(target, 5));

        Assert.Equal((ushort)37, AbilityCosts.Cost(387));
        Assert.Equal(0f, AbilityCatalog.Find(387).CastSeconds);
        Assert.True(AbilityCooldowns.TryGet(387, out var cd));
        Assert.Equal(60f, cd.Seconds);
        Assert.Equal((ushort)691, cd.Group);
        Assert.Equal(15f, cd.GroupSeconds);
    }

    [Fact]
    public void BullDogAdmissionRejectsEmptySelectionWithNativeInvalidTarget()
    {
        var caster = Client(19);
        Assert.True(BullDogDamage.TryRefuse(caster, 387, 0, null, out uint reason));
        Assert.Equal(AbilityRefusals.InvalidTarget, reason);
    }
}
