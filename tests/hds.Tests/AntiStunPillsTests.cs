using hds;
namespace hds.Tests;

[Collection("DelayedActions")]
public class AntiStunPillsTests
{
    private sealed class Subject : TimedEffects.Subject
    {
        internal override WorldClient Context => null!;
        internal override bool Ticked(TimedEffects.Active mod, int damage) => true;
        internal override void Applied(TempMods.Effect effect, List<TimedEffects.Active> added, TimedEffects.Active immunity,
            List<TimedEffects.Active> replaced) { EndedMods.AddRange(replaced); }
        internal override void Ended(List<TimedEffects.Active> ended) => EndedMods.AddRange(ended);
        public readonly List<TimedEffects.Active> EndedMods = new();
    }
    private static TimedEffects.Active State(ushort target, float seconds) => new()
    { Target = target, Seconds = seconds, Kind = TempMods.KindState, Proc = TempMods.Proc.State };

    [Fact]
    public void ShippedItemsExplicitlyGrantTheStunExceptionToTheirExactTriggeredAbility()
    {
        using var json = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "anti_stun_pills.json")));
        Assert.Equal("3e47f11012f6f219a26c9f5decfa10cbc00726c397937d03c3068254e5424103", json.RootElement.GetProperty("gob_sha256").GetString());
        foreach (var item in json.RootElement.GetProperty("items").EnumerateArray())
        {
            var fields = item.GetProperty("fields");
            ushort ability = AbilityCatalog.IdOf(fields.GetProperty("TriggeredAbility/ReqAbilityID").GetUInt32());
            Assert.Equal(item.GetProperty("id").GetUInt32(), AntiStunPills.Item(ability));
            Assert.Equal(1, fields.GetProperty("InvItem/CanUseWhileStunned").GetInt32());
            Assert.Equal(1, fields.GetProperty("InvItem/IsAConsumable").GetInt32());
            Assert.Equal("AntiStunPillEffect", AbilityCatalog.Find(ability).Text("StatModifier/StatModInitFunc"));
        }
    }

    [Fact]
    public void RemovesStunAndMezTuplesPreservingSiblingRootAndOtherEffects()
    {
        var s = new Subject();
        s.Effects.Apply(1135, new[] { State(102, 20), State(99, 25) }, 100);
        s.Effects.Apply(48, new[] { State(85, 20), State(90, 25) }, 100);
        s.Effects.Apply(399, new[] { State(96, 20) }, 100);
        AntiStunPills.Apply(s, 102);
        Assert.Equal(new ushort[] { 102, 85 }, s.EndedMods.Select(m => m.Target));
        Assert.Equal(new ushort[] { 99, 90, 96 }, s.Effects.Snapshot().Select(m => m.Target));
        TimedEffects.Clear(s);
    }

    [Theory]
    [InlineData(8, 4)]
    [InlineData(2, 1)]
    [InlineData(1.9, 1.9)]
    [InlineData(0.5, 0.5)]
    public void HalvesActualRemainingImmunityOnlyWhenHalfAtLeastOne(double remaining, double expected)
    {
        var s = new Subject();
        var timer = State(681, (float)(10 + remaining));
        timer.Kind = TempMods.KindPlain;
        s.Effects.Apply(734, new[] { timer }, 100);
        AntiStunPills.Apply(s, 110);
        var current = Assert.Single(s.Effects.Snapshot());
        Assert.Equal(110 + expected, current.Expires, 5);
        Assert.Equal(734, current.Parent);
        Assert.Equal(681, current.Target);
        Assert.Equal(remaining >= 2 ? 1 : 0, s.EndedMods.Count);
        TimedEffects.Clear(s);
    }

    [Theory]
    [InlineData(971, 42763, 900)]
    [InlineData(972, 42764, 600)]
    public void ExactItemsAndStoredOwnAndFamilyTimers(int ability, int item, int seconds)
    {
        Assert.Equal((uint)item, AntiStunPills.Item((ushort)ability));
        Assert.Equal(ability, ConsumableItems.Items[(uint)item].AbilityId);
        Assert.True(AbilityCooldowns.TryGet((ushort)ability, out var cooldown));
        Assert.Equal(seconds, cooldown.Seconds);
        Assert.Equal(973, cooldown.Group);
        Assert.Equal(600, cooldown.GroupSeconds);
        Assert.Equal(1, AbilityCatalog.Find((ushort)ability).Number("Ability/CastingTime"));
    }
}
