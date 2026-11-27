using HeadlessClient;
using hds;

namespace NativeQa.Modes;

public sealed class BlueMarbleConsumablesMode : NativeQaMode
{
    private const int X = 17000, Y = 495, Z = 2300;

    public override string Name => "blue-marble-consumables";
    public override NativeQaModePlan Plan { get; } = new(new ushort[] { 2 }, X, Z,
        Array.Empty<string>(), new[] { """
            UPDATE characters SET level=50,y=495,rotation=128 WHERE charId=110;
            UPDATE char_abilities SET level=50 WHERE char_id=110 AND (ability_id & 0xffffffff)=2147485696;
            DELETE FROM inventory WHERE charId=110 AND slot IN (5,6,7);
            INSERT INTO inventory(charId,goid,slot,count,purity) VALUES (110,47907,5,2,0),(110,47908,6,1,0),(110,47909,7,1,0);
            """ }, Array.Empty<string>(),
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        readyMessages: new[] {
            "Mara Central (17000,495,2300), level 50 and Awakened 50. Stay out of combat. No target or teleport. Use Stage 1 Blue Marble in Inventory bag 5 via its item menu or double-click.",
            "Expect instant use, one Stage 1 Marble left, Blue Shift FX, and [MODIFIER] char=110 source=1374 add value=+30 duration=6s on stats 520/183/1009/1010/1011. All five should expire naturally after 6 s.",
            "Before 30 s, try bag 6. Expect a local timer block or 80 ba reason 5 and the marble retained. After Stage 1 reuse ends, use bag 6. Expect source 1375, +40 on those five stats for 6 s, and bag 6 empty.",
            "After another 30 s, use bag 7. Expect source 1376, +50 on those five stats for 6 s and bag 7 empty. In PacketLog, each use starts a 30 s item timer and the shared timer 80 bd 01 5e 05 00 00 00 f0 41 01."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var db = context.OwnedDatabase ?? throw new InvalidOperationException("Blue Marble QA requires a disposable database.");
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Missing world data path.");
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        Check.That(NavMesh.Locate(1, X, Y, Z, out _), "Mara Central stand is on NAV");
        Check.Equal(50L, await db.LongAsync("SELECT level FROM characters WHERE charId=110"), "CSR meets the level gates");
        Check.Equal(50L, await db.LongAsync("SELECT level FROM char_abilities WHERE char_id=110 AND (ability_id & 0xffffffff)=2147485696 AND is_loaded=1"),
            "CSR has Awakened rank 50");
        Check.Equal(4L, await db.LongAsync("SELECT SUM(count) FROM inventory WHERE charId=110 AND goid=slot+47902 AND slot BETWEEN 5 AND 7"),
            "All three Blue Marble stages are in bags 5 through 7");
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, actor);
        actor.SendRpc("80 c6 05 00");
        await actor.WaitForRpcAsync("69 01 05 00 00 00 00 00 00 00");
        await actor.WaitForRpcAsync(WireFormat.ToHex(AbilityCooldowns.BuildTimer(1374, AbilityCooldowns.GroupTimer, 30, true)));
        actor.SendRpc("80 c6 06 00");
        var denial = await actor.WaitForRpcAsync("80 ba 5f 05");
        Check.That(AbilityRefusals.TryDecode(denial.Bytes, out var reason) && reason.ErrorID == AbilityRefusals.ReuseWait,
            "The Stage 1 timer blocks Stage 2");
        Check.Equal(3L, await db.LongAsync("SELECT SUM(count) FROM inventory WHERE charId=110 AND goid BETWEEN 47907 AND 47909"),
            "Only the Stage 1 marble is spent");
        return Array.Empty<byte>();
    }
}
