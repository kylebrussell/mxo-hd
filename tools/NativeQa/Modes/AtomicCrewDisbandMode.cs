using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class AtomicCrewDisbandMode : NativeQaMode
{
    private const int X = 17000, Y = 495, Z = 6400, FriendX = 17120, PeerX = 17220;
    private const uint Crew = Scenarios.CrewDisbandTarget, PeerCrew = Scenarios.CrewDisbandPeer,
        Faction = Scenarios.CrewDisbandFaction;

    public override string Name => "crew-disband-atomic";

    public override NativeQaModePlan Plan { get; } = new(new ushort[] { 2 }, X, Z, Array.Empty<string>(),
        new[] { $"UPDATE characters SET y={Y} WHERE charId=110" },
        new[] { Scenarios.CrewDisbandFactionSql, Scenarios.CrewDisbandSeedSql("CSR_Tester"), $"""
            UPDATE characters SET crewId={Crew},factionId={Faction} WHERE charId=110;
            UPDATE characters SET x={FriendX},y={Y},z={Z},crewId={Crew},factionId={Faction},cash=1000 WHERE handle='QAFriend';
            UPDATE characters SET x={PeerX},y={Y},z={Z},crewId={PeerCrew},factionId={Faction} WHERE handle='QAOther';
            INSERT INTO crew_members(crew_id,char_id,is_captain,is_first_mate,created_at)
                SELECT crewId,charId,(handle<>'QAFriend'),0,NOW()
                FROM characters WHERE charId=110 OR handle IN ('QAFriend','QAOther');
            """, Scenarios.CrewEjectionFaultSql },
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        readyMessages: new[] {
            "Open Mara NAV CSR (17000,495,6400), QAFriend (17120,495,6400), QAOther (17220,495,6400). No teleport, world target or dev command. CSR captains QACrew rank 5 with QAFriend and a bank of 200. QAOther's QAPeerCrew leads QAFaction.",
            "People icon > Crew > Disband and confirm. The disclosed disposable CHECK blocks the final affiliation write at QAFriend Info 1000. Expect [SOCIAL] Crew disband rolled back, 'The roster and bank were kept.' in chat, both members still listed and crew bank 200.",
            """Send {"actor":"QAFriend","action":"crew-disband-retry"} and wait for "QAFriend deposited 1 Info (999 remains)". The crew bank becomes 201 and the fixture allows disband.""",
            "Repeat Crew Disband in the same session. Expect server 77 02, both members unaffiliated and QAOther receiving 73 8d 23 00 00. Relog and verify CSR is still unaffiliated. QAOther and QAFaction stay in place."
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
        var member = context.Helpers["QAFriend"];
        var peer = context.Helpers["QAOther"];
        Check.Equal(3L, await db.LongAsync($"""
            SELECT COUNT(*) FROM characters p
            JOIN crew_members m ON m.char_id=p.charId AND m.crew_id=p.crewId
            JOIN crews c ON c.id=m.crew_id AND c.faction_id=p.factionId
            JOIN factions f ON f.id=c.faction_id
            WHERE f.id={Faction} AND f.master_player_handle='QAOther' AND f.money=500
              AND ((c.id={Crew} AND c.money=200 AND c.faction_rank=5
                    AND ((p.charId=110 AND m.is_captain=1) OR (p.charId={member.CharId} AND m.is_captain=0 AND p.cash=1000)))
                OR (p.charId={peer.CharId} AND c.id={PeerCrew} AND c.faction_rank=1 AND m.is_captain=1));
            """), "Both crews have the expected leaders, affiliations and bank balances");
        Check.Equal(1L, await db.LongAsync("SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS " +
            $"WHERE CONSTRAINT_SCHEMA=DATABASE() AND TABLE_NAME='characters' AND CONSTRAINT_NAME='{Scenarios.CrewEjectionFault}'"),
            "The disclosed affiliation write fault is installed");
        if (context.StockProbe is not { } captain) return Array.Empty<byte>();
        await captain.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, captain);
        await Scenarios.ProveAtomicCrewDisbandAsync(captain, member, peer, db);
        return Array.Empty<byte>();
    }

    internal static async Task<string> ReleaseFaultAsync(HeadlessPlayer actor)
    {
        if (actor.Handle != "QAFriend") throw new ArgumentException("Use QAFriend for the crew disband retry.");
        await Scenarios.DepositOneCrewInfoAsync(actor);
        return "QAFriend deposited 1 Info (999 remains)";
    }
}
