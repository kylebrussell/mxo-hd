using HeadlessClient;
using hds;

namespace NativeQa.Modes;

public sealed class AtomicCentralStorageMode : NativeQaMode
{
    private const int X = 17000, Y = 495, Z = 2300;

    public override string Name => "central-storage-atomic";
    public override NativeQaModePlan Plan { get; } = new(new ushort[] { 2 }, X, Z, Array.Empty<string>(), new[] { """
            UPDATE characters SET y=495,rotation=128 WHERE charId=110;
            DELETE FROM inventory WHERE charId=110 AND slot<=96;
            INSERT INTO inventory(charId,goid,slot,count,purity) VALUES (110,7332,3,10,0),(110,7333,4,3,0);
            DELETE FROM char_abilities WHERE char_id=110 AND (ability_id & 0xfc000000)=0x84000000;
            """, Scenarios.CentralStorageUploadFaultSql(110) }, Array.Empty<string>(),
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        readyMessages: new[] {
            "Mara Central verified NAV (17000,495,2300), facing booth 152. No teleport, helper or dev command. Bag 3 is Code Bit 1 x10 and bag 4 is Code Bit 2 x3. Right-click the booth > Loading Area > confirm. Open Central Storage > Equipment.",
            "Control first: double-click Code Bit 2 on the bag side. Expect 62 ... 01, 5f from bag, archive 5e, and successful 81 18. Dismiss Upload Status. Double-click Code Bit 2 on the archive side or use Move to bag. Expect 62 ... 00, archive 5f, bag 5e and all three Bits returned.",
            "Double-click Code Bit 1 on the bag side. A disclosed SQL CHECK confined to this disposable archive rejects the second write for Code Bit 1 x10. Expect 'Item(s) could not be uploaded!', 81 18 00 00 9c 02 00 0c, and [CENTRAL STORAGE] Move rolled back. The archive stays unchanged and bag 3 keeps all ten. No 5e/5f/69 from this request."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var db = context.OwnedDatabase ?? throw new InvalidOperationException("Central Storage QA requires a disposable database.");
        string data = Path.Combine(context.RepositoryRoot ?? throw new InvalidOperationException("Missing world data path."), "data");
        NavMesh.UseDirectory(Path.Combine(data, "navmesh"));
        HardlineNetwork.Load(Path.Combine(data, "hardline_network.csv"));
        var booth = HardlineNetwork.Get(1, 152) ?? throw new InvalidOperationException("Mara Central hardline is missing.");
        Check.That(booth.spotX == X && booth.spotY == Y && booth.spotZ == Z && NavMesh.Locate(1, X, Y, Z, out _),
            "CSR starts on the verified Mara Central hardline stand");
        Check.Equal(2L, await db.LongAsync("""
            SELECT COUNT(*) FROM inventory WHERE charId=110 AND
                ((slot=3 AND goid=7332 AND count=10) OR (slot=4 AND goid=7333 AND count=3))
            """), "Both outgoing stacks are seeded");
        Check.Equal(1L, await db.LongAsync("SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS " +
            "WHERE CONSTRAINT_SCHEMA=DATABASE() AND TABLE_NAME='char_abilities' AND CONSTRAINT_NAME='qa_archive_upload'"),
            "The disposable archive has the disclosed upload fault");
        if (context.StockProbe is { } player)
        {
            await player.EnterWorldAsync();
            NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, player);
            await Scenarios.ProveAtomicCentralStorageAsync(player, db);
        }
        return Array.Empty<byte>();
    }
}
