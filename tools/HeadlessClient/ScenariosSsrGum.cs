using hds;

namespace HeadlessClient;

public static partial class Scenarios
{
    private static async Task SsrGumUse(ScenarioContext context)
    {
        const ushort ability = 1325;
        var players = await context.AddPlayersInWorldAsync(
            new CharacterSeed("GumOwner") { Level = 45, Items = new[] { (5, 47360u, 2), (6, 47107u, 1) } },
            new CharacterSeed("GumObserver") { X = 17120 });
        var actor = players[0];
        var observer = players[1];
        var seen = await observer.WaitForViewNamedAsync(actor.Handle);

        CastSweepItem(actor, ability, 5);
        var cast = await actor.WaitForRpcAsync("80 ac 2d 05");
        Check.Equal(1f, BitConverter.ToSingle(cast.Bytes, 14), "SSR Gum casts in one second");
        await actor.WaitForRpcAsync("69 01 05 00 00 00 00 00 00 00");
        await CheckModifiers(off: false);
        Check.Equal(0, actor.Received("80 bd 01").Count, "SSR Gum starts no separate reuse timer");
        Check.Equal(1L, await context.Database.LongAsync(
            $"SELECT count FROM inventory WHERE charId={actor.CharId} AND slot=5 AND goid=47360"),
            "One piece of gum remains after use");

        await CheckMovement("cd cc 4c 3d");
        const string fx = "84 00 00 04";
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 30 " + fx);
        await observer.WaitForViewUpdateAsync(seen.ViewId, "02 80 80 80 0c " + fx);

        actor.SendRpc("80 c6 06 00");
        await actor.WaitForRpcAsync("5f 06 00 01");
        await CheckMovement("9a 99 19 3e");

        await CheckModifiers(off: true);
        await CheckMovement("cd cc cc 3d");
        actor.SendRpc("81 48");
        await actor.WaitForRpcAsync("81 49");

        Cast(actor, ability);
        var refusal = await actor.WaitForRpcAsync("80 ba 2d 05");
        Check.That(AbilityRefusals.TryDecode(refusal.Bytes, out var denied) &&
            denied.ErrorID == AbilityRefusals.RequiredItemCategory, "SSR Gum requires an owned item");
        Check.Equal(1, actor.Received("80 ac 2d 05").Count, "A bare ability request does not cast");
        Check.Equal(0, actor.World.Problems.Count + observer.World.Problems.Count, "Gum frames stay readable");

        async Task CheckMovement(string scale)
        {
            await actor.WaitForViewUpdateAsync(2, "02 80 08 " + scale);
            await observer.WaitForViewUpdateAsync(seen.ViewId, "02 20 " + scale);
        }

        async Task CheckModifiers(bool off)
        {
            var expected = new (ushort Stat, int Value)[] {
                (524, 1), (564, -1), (638, 10), (575, -1), (519, 2), (1044, 1),
                (522, 1), (209, -5), (115, 5), (1032, 1), (342, 1), (157, -4)
            };
            string[] patterns = expected.Select(mod =>
                $"80 bc 15 00 ?? ?? 00 2d 05 00 00 {Le(mod.Stat)} {Le(unchecked((uint)mod.Value))} " +
                $"{(off ? "01" : "00")} 00 00 70 42").ToArray();
            await actor.WaitUntilAsync(() => patterns.All(pattern => actor.Received(pattern).Count > 0),
                off ? "all twelve gum removals at expiry" : "all twelve gum adds for 60 seconds",
                TimeSpan.FromSeconds(off ? 65 : 5));
            var records = patterns.Select(pattern => actor.Received(pattern).Single()).ToArray();
            foreach (var datagram in records.GroupBy(record => record.DatagramSequence))
            {
                Check.That(datagram.Key.HasValue && datagram.Count() <= 6,
                    $"A datagram carries at most six gum records, got {datagram.Count()}");
            }
            var counts = records.GroupBy(record => record.ListCounter).Select(list => list.Count()).ToArray();
            Check.That(counts.Length > 1 && counts.All(count => count <= 6),
                $"Gum {(off ? "expiry" : "apply")} spans separate reliable blocks");
        }
    }
}
