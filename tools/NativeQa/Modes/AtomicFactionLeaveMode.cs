using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class AtomicFactionLeaveMode : NativeQaMode
{
    private const int X = 17000, Y = 495, Z = 6400, FriendX = 17120, PeerX = 17220;
    private const uint Crew = Scenarios.CrewDisbandTarget, PeerCrew = Scenarios.CrewDisbandPeer,
        Faction = Scenarios.CrewDisbandFaction;

    public override string Name => "faction-leave-atomic";

    public override NativeQaModePlan Plan { get; } = new(new ushort[] { 2 }, X, Z, Array.Empty<string>(),
        new[] { $"UPDATE characters SET y={Y} WHERE charId=110" },
        new[] { Scenarios.CrewDisbandFactionSql, Scenarios.CrewDisbandSeedSql("CSR_Tester"), $"""
            UPDATE characters SET crewId={Crew},factionId={Faction} WHERE charId=110;
            UPDATE characters SET x={FriendX},y={Y},z={Z},crewId={Crew},factionId={Faction},cash=1000 WHERE handle='QAFriend';
            UPDATE characters SET x={PeerX},y={Y},z={Z},crewId={PeerCrew},factionId={Faction} WHERE handle='QAOther';
            INSERT INTO crew_members(crew_id,char_id,is_captain,is_first_mate,created_at)
                SELECT crewId,charId,(handle<>'QAFriend'),0,NOW()
                FROM characters WHERE charId=110 OR handle IN ('QAFriend','QAOther');
            """, Scenarios.FactionLeaveFaultSql },
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        readyMessages: new[] {
            "Open Mara NAV CSR (17000,495,6400), QAFriend (17120,495,6400) and QAOther (17220,495,6400). No teleport, world target or dev command. CSR captains QACrew rank 5 with QAFriend and crew bank 200. QAOther's QAPeerCrew leads QAFaction, bank 500.",
            "People icon > Faction > Leave and confirm if asked. The stock client prints 'Leaving Faction.' then clears the local faction panel. The disclosed disposable CHECK rejects the final affiliation write at QAFriend Info 1000. Expect [SOCIAL] Faction leave rolled back, 'Your crew and faction were kept.' and 7c restoring QAFaction. Reopen Faction > More and verify both crews and faction bank 500.",
            """Send {"actor":"QAFriend","action":"faction-leave-retry"} and wait for "QAFriend deposited 1 Info (999 remains)". This goes to the crew bank (200 to 201) and releases the check.""",
            "Repeat Faction Leave in the same session. Expect 77 01 to CSR and QAFriend, and 73 8d 23 00 00 to QAOther. Crew still shows CSR captain, QAFriend, and 201 Info while Faction shows unaffiliated. Verify the same after relog."
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
            SELECT COUNT(*) FROM characters p JOIN crew_members m ON m.char_id=p.charId AND m.crew_id=p.crewId
            JOIN crews c ON c.id=m.crew_id AND c.faction_id=p.factionId JOIN factions f ON f.id=c.faction_id
            WHERE f.id={Faction} AND f.master_player_handle='QAOther' AND f.money=500
              AND ((c.id={Crew} AND c.money=200 AND c.faction_rank=5
                    AND ((p.charId=110 AND m.is_captain=1)
                      OR (p.charId={member.CharId} AND m.is_captain=0 AND p.cash=1000)))
                OR (p.charId={peer.CharId} AND c.id={PeerCrew} AND c.faction_rank=1 AND m.is_captain=1));
            """), "Both active faction crews have the expected ranks, roster and balances");
        Check.Equal(1L, await db.LongAsync("SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS " +
            $"WHERE CONSTRAINT_SCHEMA=DATABASE() AND TABLE_NAME='characters' AND CONSTRAINT_NAME='{Scenarios.FactionLeaveFault}'"),
            "The disclosed faction affiliation write fault is installed");
        if (context.StockProbe is not { } captain) return Array.Empty<byte>();
        await captain.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, captain);
        await Scenarios.ProveAtomicFactionLeaveAsync(captain, member, peer, db);
        return Array.Empty<byte>();
    }
}
