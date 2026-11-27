using HeadlessClient;
using hds;

namespace NativeQa.Modes;

public sealed class ModifierDatagramCapMode : NativeQaMode
{
    private const int X = 17000, Y = 495, Z = 2300;

    public override string Name => "modifier-datagram-cap";
    public override NativeQaModePlan Plan { get; } = new(
        new ushort[] { 2, 17 }, X, Z, Array.Empty<string>(), new[] { """
            UPDATE characters SET level=50,y=495,rotation=128 WHERE charId=110;
            UPDATE char_abilities SET level=50 WHERE char_id=110;
            DELETE FROM inventory WHERE charId=110 AND slot IN (5,6,7,8);
            INSERT INTO inventory(charId,goid,slot,count,purity)
                VALUES (110,47107,5,1,0),(110,47111,6,1,0),(110,47112,7,1,0),(110,47113,8,1,0);
            """ }, Array.Empty<string>(), DevCommands: true,
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        readyMessages: new[] {
            "Cold log in at Mara Central (17000,495,2300). No target or teleport. Confirm normal world entry, Status bonuses and Phone > Email. The entry snapshot has at least twelve 80 bc records across datagrams, with six or fewer per SSEQ.",
            "Close Email. For the world re-entry check, use ?stuck, select Mara NW in the Loading Area and Jack In. Check the Status bonuses and Email again. Count 80 bc records per SSEQ within the new world epoch.",
            "At rest, use bags 5 through 8 in order via each item's inventory menu. Expect [MODIFIER] adds for 300 s from 1274 (five), 1275 (one), 1276 (one) and 1277 (five, including Enrage 93). Expect one spend per item.",
            "While all four buffs are active, type ?die. Expect a visible death pose and the same twelve [MODIFIER] removes. Each SSEQ carries at most six 80 bc records. The icons clear and a subsequent chat response arrives. Look for ACKs for the datagrams and no sustained resends."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var db = context.OwnedDatabase ?? throw new InvalidOperationException("Modifier cap QA requires a disposable database.");
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Missing world data path.");
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        Check.That(NavMesh.Locate(1, X, Y, Z, out _), "Mara Central stand is on NAV");
        Check.Equal(4L, await db.LongAsync("""
            SELECT COUNT(*) FROM inventory WHERE charId=110 AND count=1
                AND ((slot=5 AND goid=47107) OR (slot BETWEEN 6 AND 8 AND goid=slot+47105))
            """), "All four cleanup items are in the expected bags");
        if (context.StockProbe is { } probe)
            await ModifierBatchProof.RunAsync(probe);
        return Array.Empty<byte>();
    }
}
