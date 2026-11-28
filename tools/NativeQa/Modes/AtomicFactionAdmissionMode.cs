using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class AtomicFactionAdmissionMode : NativeQaMode
{
    private const int X = 17000, Y = 495, Z = 6400, LeaderX = 17220, MemberX = 16880;
    private const string Leader = "QAOther", Member = "QAFriend";
    private const uint Crew = Scenarios.AdmissionCrew, PeerCrew = Scenarios.InvitingCrew, Faction = Scenarios.InvitingFaction;

    public override string Name => "faction-admission-atomic";
    public override NativeQaModePlan Plan { get; } = new(new ushort[] {2}, X, Z, Array.Empty<string>(),
        new[] { $"UPDATE characters SET y={Y},rotation=64,cash=1000 WHERE charId=110;" },
        new[] { $"""
            INSERT INTO factions(id,name,master_player_handle,money) VALUES ({Faction},'QAFaction','{Leader}',0);
            INSERT INTO crews(id,crew_name,master_player_handle,money,org,faction_id,faction_rank)
            VALUES ({Crew},'QACrew','CSR_Tester',0,1,0,0),({PeerCrew},'QAPeerCrew','{Leader}',0,1,{Faction},1);
            UPDATE characters SET crewId={Crew},factionId=0 WHERE charId=110;
            UPDATE characters SET x={LeaderX},y={Y},z={Z},rotation=192,crewId={PeerCrew},factionId={Faction} WHERE handle='{Leader}';
            UPDATE characters SET x={MemberX},y={Y},z={Z},crewId={Crew},factionId=0,cash=500 WHERE handle='{Member}';
            INSERT INTO crew_members(crew_id,char_id,is_captain,is_first_mate,created_at)
                SELECT crewId,charId,handle<>'{Member}',0,NOW() FROM characters WHERE charId=110 OR handle IN ('{Leader}','{Member}');
            """, Scenarios.FactionAffiliationFaultSql(Crew) },
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        readyMessages: new[] {
            "CSR captains unaffiliated QACrew with QAFriend. QAOther leads QAFaction. Open Mara NAV at CSR (17000,495,6400), QAOther (17220,495,6400), QAFriend (16880,495,6400). No teleport, selection or dev command.",
            """After logging in, send {"actor":"QAOther","action":"faction-admission"} to offer the invite. Accept the stock invitation. The disclosed disposable SQL CHECK rejects the membership write at CSR Info 1000. Expect 'Your crew was kept. Ask for a new invitation.' in chat, no faction status for the new faction, and QACrew still unaffiliated.""",
            "Open Crew via the people icon above chat. Deposit 1 Info. Crew bank becomes 1 and personal Info 999, which releases the fixture predicate.",
            """Send {"actor":"QAOther","action":"faction-admission"} again and accept the fresh invitation. Expect two 7c records and QAFaction. In Faction > More, QAPeerCrew is rank 1 and QACrew rank 5. The same roster should appear after relog."""
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var db = context.OwnedDatabase ?? throw new InvalidOperationException("Faction admission QA requires a disposable database.");
        var server = context.ActualServer ?? throw new InvalidOperationException("Faction admission QA requires a server.");
        NavMesh.UseDirectory(Path.Combine(context.RepositoryRoot!, "data", "navmesh"));
        foreach (int x in new[] { X, LeaderX, MemberX })
            Check.That(NavMesh.Locate(1, x, Y, Z, out var stand) && !stand.indoors && stand.floorY + 95 == Y, $"Outdoor NAV at {x},{Y},{Z}");

        foreach (string name in new[] { Leader, Member })
        {
            var player = await HeadlessPlayer.LoginAsync(context.Address, name, name);
            context.Helpers.Add(name, player);
            await player.EnterWorldAsync();
            NativeQaReadiness.RequireNoAmbientAggro(server, player);
        }
        var leader = context.Helpers[Leader];
        var member = context.Helpers[Member];
        Check.Equal(1L, await db.LongAsync($"""
            SELECT COUNT(*) FROM factions f JOIN crews c ON f.id=c.faction_id JOIN characters p ON p.factionId=f.id AND p.crewId=c.id
            WHERE f.id={Faction} AND c.id={PeerCrew} AND c.faction_rank=1 AND p.charId={leader.CharId}
               AND f.master_player_handle=p.handle AND c.master_player_handle=p.handle
            """), "QAOther is the inviting faction's leader");
        Check.Equal(2L, await db.LongAsync($"""
            SELECT COUNT(*) FROM characters p JOIN crews c ON p.crewId=c.id JOIN crew_members m ON m.crew_id=c.id AND m.char_id=p.charId
            WHERE c.id={Crew} AND c.faction_id=0 AND c.faction_rank=0 AND p.factionId=0
                AND ((p.charId=110 AND m.is_captain=1 AND c.master_player_handle=p.handle AND p.cash=1000)
                    OR (p.charId={member.CharId} AND m.is_captain=0 AND p.cash=500))
            """), "CSR and QAFriend are ready in the unaffiliated crew");
        Check.Equal(1L, await db.LongAsync("SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS " +
            "WHERE CONSTRAINT_SCHEMA=DATABASE() AND TABLE_NAME='characters' AND CONSTRAINT_NAME='qa_faction_founder_update'"),
            "The disclosed affiliation fault is installed");
        if (context.StockProbe is not { } captain) return Array.Empty<byte>();
        await captain.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, captain);
        await Scenarios.ProveAtomicFactionAdmissionAsync(captain, leader, member, db);
        return Array.Empty<byte>();
    }

    internal static string OfferInvitation(HeadlessPlayer actor)
    {
        if (actor.Handle != Leader) throw new ArgumentException("Use QAOther for the faction invitation.");
        Scenarios.SendFactionAdmissionInvite(actor, "CSR_Tester");
        return "Sent faction invitation request for CSR_Tester";
    }
}
