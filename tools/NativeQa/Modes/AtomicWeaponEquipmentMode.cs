using HeadlessClient;
using hds;

namespace NativeQa.Modes;

public sealed class AtomicWeaponEquipmentMode : NativeQaMode
{
    private const int X = 17000, Y = 495, Z = 6400;

    public override string Name => "weapon-equipment-atomic";
    public override NativeQaModePlan Plan { get; } = new(new ushort[] { 2 }, X, Z, Array.Empty<string>(), new[] { """
            UPDATE characters SET y=495,rotation=0 WHERE charId=110;
            DELETE FROM inventory WHERE charId=110 AND (slot<=96 OR slot=105);
            INSERT INTO inventory(charId,goid,slot,count,purity) VALUES (110,692,3,1,0),(110,698,4,1,0),(110,2565,105,1,0);
            """, Scenarios.WeaponSwapFaultSql(110) }, Array.Empty<string>(),
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        readyMessages: new[] {
            "Mara street verified NAV (17000,495,6400). No teleport, target, helper, or dev command. Open Inventory (I). The revolver is held, bags 1/2 are empty, and bags 3/4 hold two other guns.",
            "Double-click bag 3. A disclosed CHECK in the disposable DB rejects placing GOID 692 in the hand after moving the revolver out. Expect [INVENTORY] Weapon move rolled back, visible 'Your items were kept.', same-slot 65 03 00 03 00 00, and the revolver still held. Bag 3 stays and bag 1 is empty.",
            "Double-click bag 4. Expect 65 69 00 01 00 00 then 65 04 00 69 00 00. The revolver lands in bag 1, bag 4 clears, and GOID 698 appears in the hand.",
            "Double-click the held weapon on the paper doll. Expect 64 69 00 and 65 69 00 02 00 00. The weapon goes to bag 2 and the hand clears. Bag 3 still holds the original rifle."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var db = context.OwnedDatabase ?? throw new InvalidOperationException("Atomic weapon equipment QA requires a disposable database.");
        string data = Path.Combine(context.RepositoryRoot ?? throw new InvalidOperationException("Missing world data path."), "data");
        NavMesh.UseDirectory(Path.Combine(data, "navmesh"));
        Check.That(NavMesh.Locate(1, X, Y, Z, out var stand) && !stand.indoors && stand.floorY + 95 == Y,
            "CSR starts on open Mara NAV ground");
        Check.Equal(3L, await db.LongAsync("""
            SELECT COUNT(*) FROM inventory WHERE charId=110 AND count=1 AND
              ((slot=3 AND goid=692) OR (slot=4 AND goid=698) OR (slot=105 AND goid=2565))
            """), "The expected guns are in bags 3/4 and the hand");
        Check.Equal(0L, await db.LongAsync("SELECT COUNT(*) FROM inventory WHERE charId=110 AND slot IN (1,2)"),
            "Bags 1 and 2 are empty");
        Check.Equal(1L, await db.LongAsync("SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS " +
            "WHERE CONSTRAINT_SCHEMA=DATABASE() AND TABLE_NAME='inventory' AND CONSTRAINT_NAME='qa_equip_second_move'"),
            "The fixture has the disclosed equip constraint");
        if (context.StockProbe is { } player)
        {
            await player.EnterWorldAsync();
            NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, player);
            await Scenarios.ProveAtomicWeaponEquipmentAsync(player, db);
        }
        return Array.Empty<byte>();
    }
}
