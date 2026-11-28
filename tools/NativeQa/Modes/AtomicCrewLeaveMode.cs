using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class AtomicCrewLeaveMode : NativeQaMode
{
    private const int X = 17000, Y = 495, Z = 6400, FriendX = 17120, PeerX = 17220;
    private const uint Crew = Scenarios.CaptainTransferCrew, PeerCrew = Scenarios.CaptainTransferPeerCrew,
        Faction = Scenarios.CaptainTransferFaction;

    public override string Name => "crew-leave-atomic";

    public override NativeQaModePlan Plan { get; } = new(new ushort[] { 2 }, X, Z, Array.Empty<string>(),
        new[] { $"UPDATE characters SET y={Y} WHERE charId=110" },
        new[] { Scenarios.CaptainTransferSeedSql("CSR_Tester"), Scenarios.CaptainTransferPeerSql, $"""
            UPDATE characters SET crewId={Crew},factionId={Faction},cash=1000 WHERE charId=110;
            UPDATE characters SET x={FriendX},y={Y},z={Z},crewId={Crew},factionId={Faction} WHERE handle='QAFriend';
            UPDATE characters SET x={PeerX},y={Y},z={Z},crewId={PeerCrew},factionId={Faction} WHERE handle='QAOther';
            INSERT INTO crew_members(crew_id,char_id,is_captain,is_first_mate,created_at)
                SELECT crewId,charId,(handle<>'QAFriend'),(handle='QAFriend'),NOW()
                FROM characters WHERE charId=110 OR handle IN ('QAFriend','QAOther');
            """, Scenarios.CaptainTransferFaultSql },
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        readyMessages: new[] {
            "Open Mara NAV CSR (17000,495,6400), QAFriend (17120,495,6400) and QAOther (17220,495,6400). No teleport, world target, helper action or dev command. CSR captains QACrew and leads QAFaction. QAFriend is first mate, QAOther leads QAPeerCrew. CSR has 1000 Info and the faction bank has 500.",
            "People icon > Crew > Leave and confirm. The stock client clears the panel immediately. The disclosed disposable CHECK refuses the final faction-master write. Expect [SOCIAL] Crew leave rolled back, 'Membership and leadership were kept.' in chat, 80 86 restoring QACrew with CSR captain and QAFriend first mate, and current Faction 7c. Reopen Crew if the client switched panels.",
            "Open Faction from the people icon and deposit 1 Info. Expect faction bank 500 to 501 and CSR Info 1000 to 999. This releases the fixture predicate.",
            "Open Crew and repeat Leave in the same session. CSR becomes unaffiliated. Expect 72 02 6e 00 00 00 and 75 02 df 17 00 00 to QAFriend, and 7c to the remaining faction with QAFriend as leader and bank 501. Relog and verify CSR remains unaffiliated."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var db = context.OwnedDatabase!;
        var server = context.ActualServer!;
        NavMesh.UseDirectory(Path.Combine(context.RepositoryRoot!, "data", "navmesh"));
        foreach (int x in new[] { X, FriendX, PeerX })
            Check.That(NavMesh.Locate(1, x, Y, Z, out var stand) && !stand.indoors && stand.floorY + 95 == Y,
                $"Outdoor NAV at {x},{Y},{Z}");
        foreach (string handle in new[] { "QAFriend", "QAOther" })
        {
            var player = await HeadlessPlayer.LoginAsync(context.Address, handle, handle);
            context.Helpers.Add(handle, player);
            await player.EnterWorldAsync();
            NativeQaReadiness.RequireNoAmbientAggro(server, player);
        }
        var next = context.Helpers["QAFriend"];
        var peer = context.Helpers["QAOther"];
        Check.Equal(3L, await db.LongAsync($"""
            SELECT COUNT(*) FROM characters p JOIN crew_members m ON m.char_id=p.charId AND m.crew_id=p.crewId
            JOIN crews c ON c.id=m.crew_id AND c.faction_id=p.factionId JOIN factions f ON f.id=c.faction_id
            WHERE f.id={Faction} AND f.master_player_handle='CSR_Tester' AND f.money=500
              AND ((p.charId=110 AND c.id={Crew} AND m.is_captain=1 AND p.cash=1000)
                OR (p.charId={next.CharId} AND c.id={Crew} AND m.is_first_mate=1)
                OR (p.charId={peer.CharId} AND c.id={PeerCrew} AND m.is_captain=1));
            """), "The starting crew roles, faction and bank agree");
        Check.Equal(1L, await db.LongAsync("SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS " +
            $"WHERE CONSTRAINT_SCHEMA=DATABASE() AND TABLE_NAME='factions' AND CONSTRAINT_NAME='{Scenarios.CaptainTransferFault}'"),
            "The disclosed faction-master write fault is installed");
        if (context.StockProbe is not { } captain) return Array.Empty<byte>();
        await captain.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, captain);
        await Scenarios.ProveAtomicCrewLeaveAsync(captain, next, peer, db);
        return Array.Empty<byte>();
    }
}
