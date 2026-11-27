using HeadlessClient;
using hds;

namespace NativeQa.Modes;

public sealed class ArchiveLuggableDropMode : NativeQaMode
{
    public override string Name => "archive-luggable-drop";

    public override NativeQaModePlan Plan { get; } = new(new ushort[] { 2, 17 },
        (int)SatiFallbackLayout.ArrivalX, (int)SatiFallbackLayout.ArrivalZ,
        Array.Empty<string>(), new[] { $"""
            UPDATE characters SET level=50,exp={PlayerProgression.ExperienceForLevel(50)},alignment=1,
                district='archive_sati',districtId=9,y=95,rotation=0 WHERE charId=110;
            DELETE FROM inventory WHERE charId=110 AND slot BETWEEN 1 AND 96;
            INSERT INTO inventory(charId,slot,goid,count,purity) VALUES
                (110,5,561,1,0),(110,6,562,1,0),(110,7,565,1,0),(110,8,2399,1,0);
            """ }, Array.Empty<string>(),
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        readyMessages: new[] {
            "CSR is Zion level 50 in Sati's Playground at (14225,95,-5225), facing the Zion returner 3 m ahead at (14225,95,-4925). No teleport or archive book needed. Bag 5/6 are flagged luggables (561/562), bag 7 is unflagged (565), bag 8 is a Health Pill.",
            "Open Inventory to check slots 5-8 before travel. Right-click the Zion returner and use its normal return action. Expect Mara fallback after the world load. Reopen Inventory: 5/6 are empty, 7/8 still occupied.",
            "Expect 5f 05 00 01 and 5f 06 00 01 before 81 07. DebugLog shows exactly two [DATA-MINING] archive drop records in district 9, one each for 561/562. The headless witness checks pickup on the departure floor."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var db = context.OwnedDatabase ?? throw new InvalidOperationException("Archive QA requires a disposable database.");
        string data = Path.Combine(context.RepositoryRoot ?? throw new InvalidOperationException("Missing world data path."), "data");
        NavMesh.UseDirectory(Path.Combine(data, "navmesh"));
        Check.That(SatiFallbackLayout.TryGet(out _, out _),
            "The starting stand and Zion returner share the verified Sati NAV floor");
        foreach (var (slot, goid) in new[] { (5, 561), (6, 562), (7, 565), (8, 2399) })
            Check.Equal(1L, await db.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId=110 AND slot={slot} AND goid={goid}"),
                $"Bag {slot} has fixture item {goid}");
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, actor);
        await Scenarios.ProveArchiveLuggableReturnAsync(actor, db);
        return Array.Empty<byte>();
    }
}
