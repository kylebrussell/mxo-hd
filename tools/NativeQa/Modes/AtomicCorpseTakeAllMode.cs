using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class AtomicCorpseTakeAllMode : NativeQaMode
{
    private const int X = 17000, Y = 495, Z = 2300, DummyX = 17200;
    private const string Loot = "81 19 00 00 00 00 10 00 19 00 00 00 00 00 00 00 02 00 00 00 00 00 a4 1c 00 00 00 00 00 00 5f 09 00 00";

    public override string Name => "corpse-take-all-atomic";
    public override NativeQaModePlan Plan { get; } = new(new ushort[] { 2, 17 }, X, Z, Array.Empty<string>(), new[] { """
            UPDATE characters SET y=495,rotation=64,cash=1000 WHERE charId=110;
            DELETE FROM inventory WHERE charId=110 AND slot<=105;
            INSERT INTO inventory(charId,goid,slot,count,purity) VALUES (110,512,1,1,0),(110,531,2,1,0),(110,7332,3,2,0);
            SET SESSION sql_mode='';
            ALTER TABLE inventory ADD CONSTRAINT qa_corpse_second_item CHECK (charId<>110 OR slot<>4 OR goid<>2399);
            """ }, Array.Empty<string>(), testMobFixture: "atomic-corpse-take-all.csv",
        environmentOverrides: new() {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_CORPSE_TAKE_ALL_QA"] = "1",
            ["MXO_MOB_CORPSE_SECONDS"] = "120"
        },
        readyMessages: new[] {
            "Outdoor NAV CSR (17000,495,2300) faces Loot Test Dummy (17200,495,2300). Right-click the dummy > Close Combat, then wait for its loot window. No teleport, helper or dev command.",
            "The 1 HP disposable dummy always drops 25 Info, Code Bit 1 and one Antibiotic. A disclosed SQL CHECK rejects the Antibiotic going into bag 4. Corpse lifetime is raised to 120 s for the retry.",
            "Click Take All. Expect [LOOT] Take All rolled back and 'The items and information were kept.' The loot window reopens with 25 Info and both items. CSR still has 1000 Info, bag 3 still has two Bits, and bag 4 is empty.",
            "With the loot window open, open Inventory (I) and drag the Code Analyser from bag 2 to empty bag 5. If needed, close the loot window and use the same corpse to reopen it. Click Take All. Expect [LOOT] Take All committed items=2 info=25, 1025 Info, bag 3 x3, one Antibiotic in bag 2 and both rows cleared."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var db = context.OwnedDatabase ?? throw new InvalidOperationException("Corpse loot QA requires a disposable database.");
        string data = Path.Combine(context.RepositoryRoot!, "data");
        SectorProperties.Load(Path.Combine(data, "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(data, "navmesh"));
        foreach (int x in new[] { X, DummyX })
            Check.That(NavMesh.Locate(1, x, Y, Z, out var stand) && !stand.indoors && stand.floorY + 95 == Y,
                $"Verified outdoor NAV at ({x},{Y},{Z})");
        Check.That(NavMesh.HasSightPath(1, X, Y, Z, DummyX, Y, Z), "CSR has sight to the dummy");
        Check.Equal(1L, await db.LongAsync("SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS " +
            "WHERE CONSTRAINT_SCHEMA=DATABASE() AND TABLE_NAME='inventory' AND CONSTRAINT_NAME='qa_corpse_second_item'"),
            "The disposable SQL fault is installed");
        Check.Equal("1:512:1\n2:531:1\n3:7332:2", (await Bag()).Replace("\r\n", "\n"), "Exact bag seed");
        Check.Equal(1000L, await Cash(), "Exact wallet seed");
        if (context.StockProbe is not { } player) return Array.Empty<byte>();
        await player.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, player);
        var dummy = await player.WaitForViewNamedAsync("Loot Test Dummy");
        Check.Equal((ushort)1, BitConverter.ToUInt16(dummy.Attributes["MaxHealth"]), "The disclosed 1 HP target is present");
        string target = WireFormat.ToHex(BitConverter.GetBytes(DuelPackets.Handle(dummy.ViewId, dummy.SpawnId)));
        player.SendRpc($"40 {target} 01 6b");
        await player.WaitForRpcAsync(Loot, TimeSpan.FromSeconds(30));
        string before = await Bag();

        player.SkipReceived();
        int cursor = player.World.RpcSnapshot().Count;
        player.SendRpc("81 1f");
        await player.WaitForTextAsync("The items and information were kept.");
        await player.WaitForRpcAsync("81 1a");
        await player.WaitForRpcAsync(Loot);
        Check.Equal(before, await Bag(), "Late insertion failure rolls back the earlier stack write");
        Check.Equal(1000L, await Cash(), "The same failure also rolls back Info");
        Check.That(!player.World.RpcSnapshot().Skip(cursor).Any(packet => packet.Bytes[0] is 0x5e or 0x69 ||
            packet.Bytes[0] == 0x81 && packet.Bytes[1] == 0x20), "The refused attempt publishes no grants");

        player.SendRpc("65 02 00 05 00 00");
        await player.WaitForRpcAsync("65 02 00 05 00 00");
        player.SendRpc("81 1f");
        await player.WaitForRpcAsync("80 e4 01 04 00 00");
        await player.WaitForRpcAsync("69 01 03 00 00 00 08 00 00 00");
        await player.WaitForRpcAsync("5e 02 00 5f 09 00 00");
        await player.WaitForRpcAsync("81 20 0c 00 00 00 00 00 00 00 00 00 02 00 00 00 00 00 a4 1c 00 00 00 00 00 00 5f 09 00 00");
        Check.Equal(1025L, await Cash(), "One Info credit after retry");
        Check.Equal(3L, await db.LongAsync("SELECT count FROM inventory WHERE charId=110 AND slot=3"), "One Bit stacked");
        Check.Equal(1L, await db.LongAsync("SELECT COUNT(*) FROM inventory WHERE charId=110 AND slot=2 AND goid=2399"),
            "The retry delivers one pill");
        string committed = await Bag();
        player.SendRpc("81 1f");
        await player.WaitForRpcAsync("81 20 0c 00 00 00 00 00 00 00 00 00 00 00");
        Check.Equal(committed, await Bag(), "A repeat cannot duplicate the items");
        Check.Equal(1025L, await Cash(), "A repeat cannot duplicate Info");
        Check.Equal(0, player.World.Problems.Count, "Loot and retry frames decode");
        return Array.Empty<byte>();

        Task<string> Bag() => db.ScalarAsync("SELECT CONCAT(slot,':',goid,':',count) FROM inventory WHERE charId=110 AND slot BETWEEN 1 AND 96 ORDER BY slot");
        Task<long> Cash() => db.LongAsync("SELECT cash FROM characters WHERE charId=110");
    }
}
