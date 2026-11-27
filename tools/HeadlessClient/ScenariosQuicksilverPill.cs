using hds;

namespace HeadlessClient;

public static partial class Scenarios
{
    private static async Task QuicksilverPillUse(ScenarioContext context)
    {
        const ushort ability = 1274;
        const ushort item = 47107;
        var players = await context.AddPlayersInWorldAsync(
            new CharacterSeed("QuicksilverOwner") { Level = 45, Items = new[] { (5, (uint)item, 2) } },
            new CharacterSeed("QuicksilverObserver") { Level = 44, X = 17120, Items = new[] { (5, (uint)item, 1) } });
        var actor = players[0];
        var observer = players[1];
        var observed = await observer.WaitForViewNamedAsync(actor.Handle);
        var db = context.Database;

        observer.SendRpc("80 c6 05 00");
        await observer.WaitForTextAsync("character level is too low");
        Check.Equal(1L, await Count(observer), "The below-level pill is retained");
        Cast(actor, ability);
        await actor.WaitForRpcAsync("80 ba fa 04");
        Check.Equal(0, actor.Received("80 ac fa 04").Count, "The internal ability is not a free cast");

        const string fixtureDdl = "SET SESSION sql_mode='STRICT_TRANS_TABLES,NO_ENGINE_SUBSTITUTION'; ";
        await db.ExecuteAsync(fixtureDdl + "ALTER TABLE inventory ADD CONSTRAINT reject_quicksilver_spend " +
            $"CHECK (charId <> {actor.CharId} OR goid <> {item} OR count <> 1)");
        actor.SendRpc("80 c6 05 00");
        await actor.WaitForRpcAsync("80 ac fa 04");
        await actor.WaitForTextAsync("Your item was kept.");
        Check.Equal(2L, await Count(actor), "A failed spend retains both pills");
        Check.Equal(0, actor.Received(ModPattern(ability, 115)).Count, "A failed spend adds no buff");
        Check.Equal(0, actor.Received("80 bd 01 03 b8").Count, "A failed spend starts no reuse");
        await db.ExecuteAsync(fixtureDdl + "ALTER TABLE inventory DROP CONSTRAINT reject_quicksilver_spend");

        CastSweepItem(actor, ability, 5);
        var cast = await actor.WaitForRpcAsync("80 ac fa 04");
        Check.Equal(1f, BitConverter.ToSingle(cast.Bytes, 14), "Quicksilver casts in one second");
        await actor.WaitForRpcAsync("69 01 05 00 00 00 00 00 00 00");
        await actor.WaitForRpcAsync(WireFormat.ToHex(AbilityCooldowns.BuildTimer(item, AbilityCooldowns.GroupTimer, 30, true)));
        AssertBuff(actor, off: false);
        await actor.WaitForViewUpdateAsync(2, "02 80 08 cd cc cc 3d");
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 02 00 01");
        await observer.WaitForViewUpdateAsync(observed.ViewId, "02 80 80 80 40 00 01");
        await observer.WaitForViewUpdateAsync(observed.ViewId, "02 20 cd cc cc 3d");
        Check.Equal(1L, await Count(actor), "One item in the stack was spent");

        actor.SendRpc("80 c6 05 00");
        await actor.WaitForRpcAsync("80 ba fa 04");
        Check.Equal(2, actor.Received("80 ac fa 04").Count, "Reuse refusal starts no second cast");
        Check.Equal(1L, await Count(actor), "Reuse refusal preserves the second pill");
        actor.SendRpc("80 bb fa 04");
        await actor.WaitForRpcAsync(ModPattern(ability, 115, true));
        AssertBuff(actor, off: true);
        await actor.WaitForViewUpdateAsync(2, "02 80 08 00 00 00 00");
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 02 00 00");
        await observer.WaitForViewUpdateAsync(observed.ViewId, "02 80 80 80 40 00 00");
        await observer.WaitForViewUpdateAsync(observed.ViewId, "02 20 00 00 00 00");
        Check.Equal(0, actor.World.Problems.Count + observer.World.Problems.Count, "Pill and removal frames stay readable");

        Task<long> Count(HeadlessPlayer player) => db.LongAsync(
            $"SELECT COALESCE(SUM(count),0) FROM inventory WHERE charId={player.CharId} AND slot=5");
    }

    private static void AssertBuff(HeadlessPlayer player, bool off)
    {
        foreach (var (stat, value) in new[] { (115, 10), (183, 12), (1009, 12), (1010, 12), (1011, 12) })
        {
            var records = player.Received("80 bc 15 00 ?? ?? 00 fa 04 00 00 " +
                $"{Le((ushort)stat)} {Le((uint)value)} {(off ? "01" : "00")} 00 00 96 43");
            Check.Equal(1, records.Count, $"Quicksilver stat {stat} {(off ? "removes" : "adds")} once for 300 seconds");
        }
    }
}
