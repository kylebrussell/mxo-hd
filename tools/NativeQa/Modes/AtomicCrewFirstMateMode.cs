using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class AtomicCrewFirstMateMode : NativeQaMode
{
    private const int X = 17000, Y = 495, Z = 6400, FriendX = 17120, OtherX = 17220;
    private const uint Crew = Scenarios.FirstMateCrew, Target = 6113;

    public override string Name => "crew-first-mate-atomic";
    public override NativeQaModePlan Plan { get; } = new(new ushort[] { 2 }, X, Z, Array.Empty<string>(),
        new[] { $"UPDATE characters SET y={Y} WHERE charId=110" },
        new[] { Scenarios.FirstMateCrewSql("CSR_Tester"), $"""
            UPDATE characters SET crewId={Crew},factionId=0 WHERE charId=110;
            UPDATE characters SET x={FriendX},y={Y},z={Z},crewId={Crew},factionId=0 WHERE handle='QAFriend';
            UPDATE characters SET x={OtherX},y={Y},z={Z},crewId={Crew},factionId=0 WHERE handle='QAOther';
            INSERT INTO crew_members(crew_id,char_id,is_captain,is_first_mate,created_at)
                SELECT crewId,charId,(charId=110),(handle='QAFriend'),NOW()
                FROM characters WHERE charId=110 OR handle IN ('QAFriend','QAOther');
            """, Scenarios.FirstMateFaultSql(Target) },
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        readyMessages: new[] {
            "Open Mara NAV: CSR (17000,495,6400), QAFriend (17120,495,6400), QAOther (17220,495,6400). No teleport, world target or dev command. CSR captains QACrew, QAFriend is first mate and QAOther is a regular member.",
            "People icon > Crew. Right-click QAOther > Make First Mate > Yes. The disclosed disposable CHECK rejects the new first-mate flag after the old one is cleared in the transaction. Expect [SOCIAL] Crew first mate change rolled back, 'The roster was kept.' in chat, QAFriend still marked FM and no server 80 87.",
            """Send {"actor":"QAOther","action":"first-mate-retry"} and wait for "First-mate fixture CHECK removed". This removes only the named CHECK from the disposable crew_members table.""",
            "Repeat Make First Mate on QAOther in the same session. Expect 80 87 e1 17 00 00, and only QAOther tagged FM. After relog, check the persisted tag. Right-click QAOther > Demote > Yes. Expect 80 87 00 00 00 00 and no FM tag. Reopen Crew and verify CSR stays captain and both members remain."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var db = context.OwnedDatabase!;
        var server = context.ActualServer!;
        NavMesh.UseDirectory(Path.Combine(context.RepositoryRoot!, "data", "navmesh"));
        foreach (int x in new[] { X, FriendX, OtherX })
            Check.That(NavMesh.Locate(1, x, Y, Z, out var stand) && !stand.indoors && stand.floorY + 95 == Y,
                $"Outdoor NAV at {x},{Y},{Z}");
        foreach (string handle in new[] { "QAFriend", "QAOther" })
        {
            var player = await HeadlessPlayer.LoginAsync(context.Address, handle, handle);
            context.Helpers.Add(handle, player);
            await player.EnterWorldAsync();
            NativeQaReadiness.RequireNoAmbientAggro(server, player);
        }
        var mate = context.Helpers["QAFriend"];
        var target = context.Helpers["QAOther"];
        Check.Equal(Target, target.CharId, "The fixture fault applies to QAOther");
        var roster = await target.WaitForRpcAsync("80 86 e1 17 00 00 8d 23 00 00");
        Check.Equal(110u, BitConverter.ToUInt32(roster.Bytes, 13), "CSR appears as captain in the stock roster");
        Check.Equal(mate.CharId, BitConverter.ToUInt32(roster.Bytes, 17), "QAFriend starts as the first mate in the stock roster");
        Check.Equal(3L, await db.LongAsync($"""
            SELECT COUNT(*) FROM characters p JOIN crew_members m ON m.crew_id=p.crewId AND m.char_id=p.charId
            WHERE m.crew_id={Crew} AND p.factionId=0
              AND ((p.charId=110 AND m.is_captain=1 AND m.is_first_mate=0)
                OR (p.charId={mate.CharId} AND m.is_captain=0 AND m.is_first_mate=1)
                OR (p.charId={target.CharId} AND m.is_captain=0 AND m.is_first_mate=0))
            """), "The three seeded roster roles match their affiliations");
        Check.Equal(1L, await db.LongAsync("SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS " +
            $"WHERE CONSTRAINT_SCHEMA=DATABASE() AND TABLE_NAME='crew_members' AND CONSTRAINT_NAME='{Scenarios.FirstMateFault}'"),
            "The disclosed first-mate write fault is installed");
        if (context.StockProbe is not { } captain) return Array.Empty<byte>();
        await captain.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, captain);
        await Scenarios.ProveAtomicCrewFirstMateAsync(captain, mate, target, db);
        return Array.Empty<byte>();
    }
}
