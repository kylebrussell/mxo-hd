using hds;

namespace HeadlessClient;

public static partial class Scenarios
{
    private static async Task ArchiveLuggableDrop(ScenarioContext context)
    {
        var traveller = new CharacterSeed("ArchiveLugger")
        {
            X = SatiFallbackLayout.ArrivalX, Y = SatiFallbackLayout.ArrivalY, Z = SatiFallbackLayout.ArrivalZ,
            District = "archive_sati", DistrictId = SatiFallbackLayout.Map, Organization = 1, Level = 50,
            Items = new[] { (5, 561u, 1), (6, 562u, 1), (7, 565u, 1), (8, 2399u, 1) }
        };
        var actor = await context.AddPlayerAsync(traveller);
        var witness = await context.AddPlayerAsync(traveller with { Handle = "ArchiveWitness", Items = Array.Empty<(int, uint, int)>() });
        var db = context.Database;
        await db.ExecuteAsync($"UPDATE inventory SET count=0 WHERE charId={actor.CharId} AND slot=5");
        await actor.EnterWorldAsync();
        await witness.EnterWorldAsync();
        await ProveArchiveLuggableReturnAsync(actor, db, forceRollback: true);

        foreach (ushort goid in new ushort[] { 561, 562 })
        {
            await witness.WaitUntilAsync(() => witness.ViewsOfGoid(goid).Count > 0,
                $"The departing player's {goid} is shared at the original stand", TimeSpan.FromSeconds(15));
            witness.SendRpc($"80 c7 {MissionNpcId(witness.ViewsOfGoid(goid).Last())} 02 00");
            await witness.WaitForRpcAsync($"5e ?? ?? {Le((uint)goid)}");
            Check.Equal(1L, await db.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={witness.CharId} AND goid={goid}"),
                "The witness picks up each release exactly once");
        }
        Check.Equal(0L, await db.LongAsync("SELECT COUNT(*) FROM world_luggables"),
            "Picking up both items removes both persistent ground rows");
        Check.Equal(0, witness.World.Problems.Count, "The witness sees a healthy object and inventory stream");
    }

    public static async Task ProveArchiveLuggableReturnAsync(HeadlessPlayer actor, ThrowawayDatabase db, bool forceRollback = false)
    {
        await actor.WaitUntilAsync(() => actor.ViewsOfGoid(38271).Count > 0,
            "The Zion Archive returner is ready", TimeSpan.FromSeconds(20));
        var returner = actor.ViewsOfGoid(38271).Last();
        string leave = $"80 c7 {MissionNpcId(returner)} 02 00";
        string inventory = $"FROM inventory WHERE charId={actor.CharId}";
        if (forceRollback)
        {
            await db.ExecuteAsync("ALTER TABLE world_luggables ADD CONSTRAINT refuse_second_release CHECK (goid<>562)");
            actor.SendRpc(leave);
            await actor.WaitForTextAsync("Your luggables could not be dropped. Archive travel was cancelled.");
            Check.Equal(2L, await db.LongAsync($"SELECT COUNT(*) {inventory} AND goid IN (561,562)"),
                "Failure on the second insert restores both original bag rows");
            Check.Equal(0L, await db.LongAsync("SELECT COUNT(*) FROM world_luggables"),
                "Failure on the second insert rolls back the first ground row");
            Check.Equal((long)SatiFallbackLayout.Map,
                await db.LongAsync($"SELECT districtId FROM characters WHERE charId={actor.CharId}"),
                "The character stays in the departure map on failure");
            Check.Equal(0, actor.Received("81 07").Count, "A refused release emits no world reset");
            await db.ExecuteAsync("ALTER TABLE world_luggables DROP CONSTRAINT refuse_second_release");
        }

        actor.SendRpc(leave);
        await actor.WaitForRpcAsync("5f 05 00 01");
        await actor.WaitForRpcAsync("5f 06 00 01");
        await actor.WaitForRpcAsync("81 07");
        Check.Equal(1L, await db.LongAsync($"SELECT districtId FROM characters WHERE charId={actor.CharId}"),
            "The accepted return saves Richland");
        Check.Equal(0L, await db.LongAsync($"SELECT COUNT(*) {inventory} AND goid IN (561,562)"), "The carried copies are gone");
        Check.Equal(1L, await db.LongAsync($"SELECT COUNT(*) {inventory} AND slot=7 AND goid=565"), "The unflagged luggable stays in its slot");
        Check.Equal(1L, await db.LongAsync($"SELECT COUNT(*) {inventory} AND slot=8 AND goid=2399"), "The ordinary bag item stays in its slot");
        Check.Equal(2L, await db.LongAsync("SELECT COUNT(DISTINCT goid) FROM world_luggables " +
            "WHERE district=9 AND x=14225 AND y=95 AND z=-5225 AND goid IN (561,562)"),
            "Each flagged item has one persistent row on the exact departure floor");
        Check.Equal(2L, await db.LongAsync("SELECT COUNT(*) FROM world_luggables"), "Exactly two rows were published");

        actor.SendRpc("81 08");
        await actor.WaitForRpcAsync("81 08");
        var load = await actor.WaitForRpcAsync("06");
        Check.Equal(1u, BitConverter.ToUInt32(load.Bytes, 3), "The return client loads Richland");
        int selfSpawn = actor.World.CreationSnapshot().Count(view => view.ViewId == 2);
        actor.SendRpc("05");
        await actor.WaitUntilAsync(() => actor.World.CreationSnapshot().Count(view => view.ViewId == 2) > selfSpawn,
            "The client spawns in Richland", TimeSpan.FromSeconds(15));
        Check.Equal(0, actor.World.Problems.Count, "The inventory deletions and return records decode");
    }
}
