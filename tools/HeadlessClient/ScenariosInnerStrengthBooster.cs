using hds;

namespace HeadlessClient;

public static partial class Scenarios
{
    private static async Task InnerStrengthBoosterUse(ScenarioContext context)
    {
        const ushort ability = 1333;
        var players = await context.AddPlayersInWorldAsync(
            new CharacterSeed("BoosterOwner") { InnerStrength = 100, MaxInnerStrength = 1500, Items = new[] { (5, 47363u, 2) } },
            new CharacterSeed("BoosterObserver") { InnerStrength = 100, MaxInnerStrength = 1500, X = 17120 });
        var actor = players[0];
        var observer = players[1];
        var seen = await observer.WaitForViewNamedAsync(actor.Handle);

        var baseline = await Sample(actor, actor.World.ViewSnapshot().Count, 3);
        Check.That(Steps(baseline).All(step => step == 8), "Ordinary regeneration is eight IS per tick");
        Cast(actor, ability);
        var refusal = await actor.WaitForRpcAsync("80 ba 35 05");
        Check.That(AbilityRefusals.TryDecode(refusal.Bytes, out var denied) &&
            denied.ErrorID == AbilityRefusals.RequiredItemCategory, "The granted ability requires its own item");

        actor.SendRpc("80 c6 05 00");
        await CheckCast();
        await actor.WaitForRpcAsync("69 01 05 00 00 00 00 00 00 00");
        await actor.WaitForRpcAsync(Modifier(off: false));
        Check.Equal(1L, await Remaining(), "Using the booster spends exactly one item");

        int boostedAt = actor.World.ViewSnapshot().Count;
        int controlAt = observer.World.ViewSnapshot().Count;
        const string pulse = "86 00 00 04";
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 30 " + pulse);
        await observer.WaitForViewUpdateAsync(seen.ViewId, "02 80 80 80 0c " + pulse);
        var boosted = await Sample(actor, boostedAt, 6);
        Check.That(Steps(boosted).All(step => step is 9 or 10), "Boosted ticks use fractional carry");
        Check.Equal(48, boosted[^1] - boosted[0], "Five boosted intervals restore 48 IS");
        var control = await Sample(observer, controlAt, 3);
        Check.That(Steps(control).All(step => step == 8), "The observer sees the FX without the regen bonus");

        actor.SendRpc("80 bb 35 05");
        await actor.WaitForRpcAsync(Modifier(off: true));
        var restored = await Sample(actor, actor.World.ViewSnapshot().Count, 3);
        Check.That(Steps(restored).All(step => step == 8), "Dismissal restores the ordinary regen rate");

        CastSweepItem(actor, ability, 5);
        await CheckCast();
        await actor.WaitForRpcAsync("5f 05 00 01");
        await actor.WaitForRpcAsync(Modifier(off: false));
        Check.Equal(0L, await Remaining(), "The alternate owned-item route spends the remaining booster");
        Check.Equal(0, actor.Received("80 bd 01").Count, "The booster has no separate reuse timer");
        Check.Equal(0, actor.World.Problems.Count + observer.World.Problems.Count, "Booster frames stay readable");

        async Task CheckCast()
        {
            var start = await actor.WaitForRpcAsync("80 ac 35 05");
            Check.Equal(1f, BitConverter.ToSingle(start.Bytes, 14), "The booster casts for one second");
        }

        Task<long> Remaining() => context.Database.LongAsync(
            $"SELECT COALESCE(SUM(count),0) FROM inventory WHERE charId={actor.CharId} AND goid=47363");

        static string Modifier(bool off) =>
            $"80 bc 15 00 ?? ?? 00 35 05 00 00 72 00 14 00 00 00 {(off ? "01" : "00")} 00 00 96 44";

        static IEnumerable<int> Steps(ushort[] values) => values.Skip(1).Zip(values, (next, previous) => next - previous);

        static async Task<ushort[]> Sample(HeadlessPlayer player, int from, int count)
        {
            List<ushort> values = new();
            await player.WaitUntilAsync(() =>
            {
                values = ViewUpdateValues(player, 2, "02 80 80 80 10", from);
                return values.Count >= count;
            }, $"{count} Inner Strength regen samples", TimeSpan.FromSeconds(19));
            return values.Take(count).ToArray();
        }
    }
}
