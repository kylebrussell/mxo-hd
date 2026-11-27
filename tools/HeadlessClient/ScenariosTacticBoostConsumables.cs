using hds;

namespace HeadlessClient;

public static partial class Scenarios
{
    private static async Task TacticBoostConsumables(ScenarioContext context)
    {
        var players = await context.AddPlayersInWorldAsync(
            new CharacterSeed("TacticBooster") { Level = 45, Items = new[] { (5, 8322u, 2), (6, 8323u, 1), (7, 8324u, 1), (8, 8325u, 1) } },
            new CharacterSeed("TacticObserver") { Level = 14, X = 17120, Items = new[] { (6, 8323u, 1) } });
        var actor = players[0];
        var observer = players[1];
        var observed = await observer.WaitForViewNamedAsync(actor.Handle);
        var db = context.Database;

        observer.SendRpc("80 c6 06 00");
        await observer.WaitForTextAsync("character level is too low");
        Check.Equal(1L, await BagCount(observer, 6), "A locked tier keeps its item");
        Cast(actor, 549);
        var denied = await actor.WaitForRpcAsync("80 ba 25 02");
        Check.That(AbilityRefusals.TryDecode(denied.Bytes, out var refusal) &&
            refusal.ErrorID == AbilityRefusals.RequiredItemCategory, "The granted ability needs its owned item");
        Check.Equal(0, actor.Received("80 ac 25 02").Count, "A bare ability request starts no cast");

        actor.SendRpc("80 c6 05 00");
        await CastBar(548);
        await actor.WaitForRpcAsync("69 01 05 00 00 00 00 00 00 00");
        await Stat(548, 5);
        Check.Equal(1L, await BagCount(actor, 5), "Tier 1 spends one item");
        string pulse = WireFormat.ToHex(PlayerViewAttributes.U32(67108932));
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 30 " + pulse);
        await observer.WaitForViewUpdateAsync(observed.ViewId, "02 80 80 80 0c " + pulse);

        CastSweepItem(actor, 549, 6);
        await CastBar(549);
        await actor.WaitForRpcAsync("5f 06 00 01");
        await Stat(548, 5, off: true);
        await Stat(549, 10);

        actor.SendRpc("80 c6 05 00");
        await actor.WaitForTextAsync("A stronger Tactic Boost is already active.");
        Check.Equal(1L, await BagCount(actor, 5), "A weaker boost is refused without consuming it");
        Check.Equal(1, actor.Received("80 ac 24 02").Count, "Priority refusal starts no second tier 1 cast");

        actor.SendRpc("80 c6 07 00");
        await CastBar(550);
        await actor.WaitForRpcAsync("5f 07 00 01");
        await Stat(549, 10, off: true);
        await Stat(550, 15);

        actor.SendRpc("80 c6 08 00");
        await CastBar(551);
        await actor.WaitForRpcAsync("5f 08 00 01");
        await Stat(550, 15, off: true);
        await Stat(551, 20);
        Check.Equal(0L, await BagCount(actor, 8), "The final boost leaves its bag slot");
        Check.Equal(0, actor.Received("80 bd 01").Count, "The boosts add no cooldown");
        actor.SendRpc("80 bb 27 02");
        await Stat(551, 20, off: true);
        Check.Equal(0, actor.World.Problems.Count + observer.World.Problems.Count, "Tactic Boost frames stay readable");

        async Task CastBar(ushort ability)
        {
            var row = await actor.WaitForRpcAsync("80 ac " + Le(ability));
            Check.Equal(1f, BitConverter.ToSingle(row.Bytes, 14), $"Boost {ability} casts for one second");
        }

        Task<RpcRecord> Stat(ushort ability, uint value, bool off = false) =>
            actor.WaitForRpcAsync($"80 bc 15 00 ?? ?? 00 {Le((uint)ability)} a3 00 {Le(value)} {(off ? "01" : "00")} 00 00 16 44");

        Task<long> BagCount(HeadlessPlayer player, int slot) => db.LongAsync(
            $"SELECT COALESCE(SUM(count),0) FROM inventory WHERE charId={player.CharId} AND slot={slot}");
    }
}
