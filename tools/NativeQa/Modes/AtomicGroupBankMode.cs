using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class AtomicGroupBankMode : NativeQaMode
{
    private const int X = 17000, Y = 495, Z = 6400;

    public override string Name => "group-bank-atomic";

    public override NativeQaModePlan Plan { get; } = new(new ushort[] { 2 }, X, Z, Array.Empty<string>(),
        new[] { $"UPDATE characters SET y={Y},rotation=0 WHERE charId=110",
            Scenarios.GroupBankSeedSql(110), Scenarios.GroupBankFaultSql }, Array.Empty<string>(),
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        readyMessages: new[] {
            "CSR is crew captain and faction leader on open Mara NAV (17000,495,6400). No teleport, target, helper, bag action or dev command. Open the Crew/Faction Status windows. Starting Info is 1,000, Crew bank 200, Faction bank 500.",
            "In the Crew status bank controls, Deposit 30. The disposable database rejects the wallet write after the bank update. Expect 'The transfer could not be saved. Your information was kept.', both balances unchanged and no 79 delta.",
            "Deposit 100 to the Crew bank. Expect 79 type 02, deposit=true, Crew 300 and Info 900. Withdraw 50. Expect 79 type 02, deposit=false, Crew 250 and Info 950.",
            "In the Faction status bank controls, Deposit 25. Expect 79 type 01, deposit=true, Faction 525 and Info 925. Crew stays at 250. Check the balances again after relog."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var db = context.OwnedDatabase ?? throw new InvalidOperationException("Group bank QA requires a disposable database.");
        NavMesh.UseDirectory(Path.Combine(context.RepositoryRoot!, "data", "navmesh"));
        Check.That(NavMesh.Locate(1, X, Y, Z, out var stand) && !stand.indoors && stand.floorY + 95 == Y,
            "CSR starts on open NAV ground");
        Check.Equal(1L, await db.LongAsync($"""
            SELECT COUNT(*) FROM characters p
            JOIN crews c ON c.id=p.crewId AND c.id={Scenarios.QaCrewBankId} AND c.money=200
            JOIN factions f ON f.id=p.factionId AND f.id={Scenarios.QaFactionBankId} AND f.money=500
            JOIN crew_members m ON m.crew_id=c.id AND m.char_id=p.charId AND m.is_captain=1
            WHERE p.charId=110 AND p.cash=1000 AND p.handle=c.master_player_handle AND p.handle=f.master_player_handle
            """), "The wallet, crew and faction are seeded with CSR as leader");
        Check.Equal(1L, await db.LongAsync("SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS " +
            "WHERE CONSTRAINT_SCHEMA=DATABASE() AND TABLE_NAME='characters' AND CONSTRAINT_NAME='qa_group_bank_wallet'"),
            "The disclosed wallet fault is installed");
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, actor);
        await actor.WaitUntilAsync(() => actor.Received("80 86 6e 00 00 00 47 23 00 00").Count > 0 &&
            actor.Received("7c 6e 00 00 00 48 23 00 00 01").Count > 0,
            "The stock bootstrap receives the seeded Crew and Faction status records", HeadlessPlayer.DefaultTimeout);
        await Scenarios.ProveAtomicGroupBankAsync(actor, db);
        return Array.Empty<byte>();
    }
}
