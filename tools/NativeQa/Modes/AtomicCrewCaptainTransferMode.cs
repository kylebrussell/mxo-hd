using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class AtomicCrewCaptainTransferMode : NativeQaMode
{
    private const int X = 17000, Y = 495, Z = 6400, FriendX = 17120, PeerX = 17220;
    private const uint Crew = Scenarios.CaptainTransferCrew, PeerCrew = Scenarios.CaptainTransferPeerCrew,
        Faction = Scenarios.CaptainTransferFaction;

    public override string Name => "crew-captain-transfer-atomic";

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
            "Open Mara NAV: CSR (17000,495,6400), QAFriend (17120,495,6400), QAOther (17220,495,6400). No teleport, world target or dev command. CSR leads QACrew and QAFaction. QAFriend is first mate, QAOther is peer captain. CSR has 1000 Info and the faction bank has 500.",
            "Open Crew from the people icon above chat. Right-click QAFriend > Make Captain and confirm if asked. The disclosed disposable CHECK refuses the last faction-master write. Expect [SOCIAL] Crew captain transfer rolled back, requester-only 72 02 00 00 00 00, the chat refusal, QAFriend still first mate and CSR still captain. Verify the roster menu reopens.",
            "From the people icon open Faction. Deposit 1 into the faction bank (500 to 501, CSR Info 1000 to 999). This releases the fixture predicate.",
            "Back in Crew, right-click QAFriend > Make Captain again in the same session. Expect 75 02 df 17 00 00, QAFriend captain and no first mate. QAOther receives 7e 8d 23 00 00 df 17 00 00. After relog verify both crews persist, QAFriend still captain and faction bank 501."
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
        string nextId = WireFormat.ToHex(BitConverter.GetBytes(next.CharId));
        string peerId = WireFormat.ToHex(BitConverter.GetBytes(peer.CharId));
        await next.WaitForRpcAsync($"80 86 {nextId} 8d 23 00 00");
        await peer.WaitForRpcAsync($"7c {peerId} 8f 23 00 00 02");
        Check.Equal(3L, await db.LongAsync($"""
            SELECT COUNT(*) FROM characters p
            JOIN crew_members m ON m.char_id=p.charId AND m.crew_id=p.crewId
            JOIN crews c ON c.id=m.crew_id AND c.faction_id=p.factionId
            JOIN factions f ON f.id=c.faction_id
            WHERE f.id={Faction} AND f.master_player_handle='CSR_Tester' AND f.money=500
              AND ((p.charId=110 AND c.id={Crew} AND m.is_captain=1 AND p.cash=1000)
                OR (p.charId={next.CharId} AND c.id={Crew} AND m.is_first_mate=1)
                OR (p.charId={peer.CharId} AND c.id={PeerCrew} AND m.is_captain=1));
            """), "The starting crew roles, faction and bank agree");
        Check.Equal(1L, await db.LongAsync("SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS " +
            $"WHERE CONSTRAINT_SCHEMA=DATABASE() AND TABLE_NAME='factions' AND CONSTRAINT_NAME='{Scenarios.CaptainTransferFault}'"),
            "The disclosed faction master write fault is installed");
        if (context.StockProbe is not { } captain) return Array.Empty<byte>();
        await captain.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, captain);
        await Scenarios.ProveAtomicCrewCaptainTransferAsync(captain, next, peer, db);
        return Array.Empty<byte>();
    }
}
