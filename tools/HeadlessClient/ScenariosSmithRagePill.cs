using hds;

namespace HeadlessClient;

public static partial class Scenarios
{
    private static async Task SmithRagePillUse(ScenarioContext context)
    {
        const ushort ability = 1257;
        const uint item = 46994;
        var players = await context.AddPlayersInWorldAsync(
            NearNpc("SmithPillOwner") with { Level = 50, Health = 5000, MaxHealth = 5000, Items = new[] { (5, item, 2) } },
            NearNpc("SmithPillLower", 120) with { Level = 25, Items = new[] { (5, item, 1) } });
        var owner = players[0];
        var lower = players[1];
        var seen = await lower.WaitForViewNamedAsync(owner.Handle);

        // Bare ability use, even on oneself, must go through the owned-item gate.
        Cast(lower, ability);
        var denied = await lower.WaitForRpcAsync("80 ba e9 04");
        Check.That(AbilityRefusals.TryDecode(denied.Bytes, out var refusal) &&
            refusal.ErrorID == AbilityRefusals.RequiredItemCategory, "The pill cannot cast without an owned item");
        Check.Equal(1L, await Count(lower), "The bare ability request does not spend the pill");

        CastSweepItem(owner, ability, 5);
        await CastTime(owner);
        await owner.WaitForRpcAsync("69 01 05 00 00 00 00 00 00 00");
        var first = await Records(owner, 11, false, 0);
        await owner.WaitForViewUpdateAsync(2, "02 80 80 80 80 30 28 00 00 04");
        await lower.WaitForViewUpdateAsync(seen.ViewId, "02 80 80 80 0c 28 00 00 04");
        Check.Equal(1L, await Count(owner), "One pill remains in the owner's stack");
        Check.Equal(0, owner.Received("80 bd 01").Count, "The first pill starts no reuse timer");

        lower.SendRpc("80 c6 05 00");
        await CastTime(lower);
        await lower.WaitForRpcAsync("5f 05 00 01");
        var lowerAdds = await Records(lower, 6, false, 0);

        var npc = await owner.WaitForViewNamedAsync("WinterFrost");
        owner.SendRpc($"40 {HandleOf(npc)} 00 6b");
        await WaitForCombatView(owner);
        owner.SendRpc("44 01");
        await owner.WaitUntilAsync(() => context.Server.DebugLog().Contains($"[ENRAGE] withdraw suppressed char={owner.CharId}"),
            "Enrage suppresses Withdraw in a live interlock", HeadlessPlayer.DefaultTimeout);

        int repeatStart = owner.World.RpcSnapshot().Count;
        owner.SendRpc("80 c6 05 00");
        await CastTime(owner);
        await owner.WaitForRpcAsync("5f 05 00 01");
        var replaced = await Records(owner, 11, true, repeatStart);
        var refreshed = await Records(owner, 11, false, repeatStart);
        Check.That(RecordIds(first).SequenceEqual(RecordIds(replaced)), "Reapplication removes each original record");
        Check.That(!RecordIds(refreshed).Intersect(RecordIds(first)).Any(), "Reapplication assigns each replacement a new record");
        Check.That(refreshed.Min(r => r.ListCounter + r.Index) > replaced.Max(r => r.ListCounter + r.Index),
            "Each removal precedes the refreshed effect");
        Check.That(replaced.Concat(refreshed).GroupBy(record => record.DatagramSequence)
            .All(group => group.Key.HasValue && group.Count() <= 6), "Replacement respects the modifier datagram cap");
        Check.Equal(0L, await Count(owner), "The permitted repeat spends the last pill");

        int deathStart = owner.World.RpcSnapshot().Count;
        owner.SendChat("?die");
        Check.That(RecordIds(refreshed).SequenceEqual(RecordIds(await Records(owner, 11, true, deathStart))),
            "Death removes the refreshed effect");
        owner.SendRpc("81 48");
        await owner.WaitForRpcAsync("81 49");

        Check.That(RecordIds(lowerAdds).SequenceEqual(RecordIds(await Records(lower, 6, true, 0,
            TimeSpan.FromSeconds(307)))), "All level-25 modifiers are removed at natural expiry");
        Check.Equal(0, players.Sum(player => player.World.Problems.Count), "Pill frames stay readable");

        Task<long> Count(HeadlessPlayer player) => context.Database.LongAsync(
            $"SELECT COALESCE(SUM(count),0) FROM inventory WHERE charId={player.CharId} AND goid={item}");

        static async Task CastTime(HeadlessPlayer actor)
        {
            var cast = await actor.WaitForRpcAsync("80 ac e9 04");
            Check.Equal(1.5f, BitConverter.ToSingle(cast.Bytes, 14), "Smith's Rage casts in 1.5 seconds");
        }

        static async Task<RpcRecord[]> Records(HeadlessPlayer actor, uint damage, bool off, int after,
            TimeSpan? timeout = null)
        {
            (ushort Kind, ushort Stat, uint Value)[] modifiers = {
                (0x15, 159, damage), (0x15, 171, damage), (0x15, 172, damage), (0x15, 1034, damage),
                (0x15, 144, 20), (0x11, 93, 0)
            };
            var patterns = modifiers.Select(mod => BytePattern.Parse(
                $"80 bc {Le(mod.Kind)} ?? ?? 00 e9 04 00 00 {Le(mod.Stat)} {Le(mod.Value)} " +
                $"{(off ? "01" : "00")} 00 00 96 43")).ToArray();
            RpcRecord[] received = Array.Empty<RpcRecord>();
            await actor.WaitUntilAsync(() =>
            {
                received = actor.World.RpcSnapshot().Skip(after).ToArray();
                return patterns.All(pattern => received.Any(row => pattern.IsMatch(row.Bytes)));
            }, $"six Smith's Rage records with off={off}", timeout ?? HeadlessPlayer.DefaultTimeout);
            return patterns.Select(pattern => received.Single(row => pattern.IsMatch(row.Bytes))).ToArray();
        }

        static IEnumerable<ushort> RecordIds(IEnumerable<RpcRecord> records) =>
            records.Select(record => BitConverter.ToUInt16(record.Bytes, 4));
    }
}
