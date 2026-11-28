using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class AtomicCrewAdmissionMode : NativeQaMode
{
    private const int X = 17000, Y = 495, Z = 6400, FriendX = 17120, OtherX = 17220;
    private const uint RejectedCrew = Scenarios.CrewAdmissionRejected, AcceptedCrew = Scenarios.CrewAdmissionAccepted,
        Faction = Scenarios.CrewAdmissionFaction;

    public override string Name => "crew-admission-atomic";

    public override NativeQaModePlan Plan { get; } = new(new ushort[] { 2 }, X, Z, Array.Empty<string>(),
        new[] { $"UPDATE characters SET y={Y},repZion=100 WHERE charId=110" },
        new[] { Scenarios.CrewAdmissionFactionSql, Scenarios.CrewAdmissionPeerSql, $"""
            UPDATE characters SET x={FriendX},y={Y},z={Z},crewId={RejectedCrew},factionId={Faction} WHERE handle='QAFriend';
            UPDATE characters SET x={OtherX},y={Y},z={Z},crewId={AcceptedCrew},factionId={Faction} WHERE handle='QAOther';
            INSERT INTO crew_members(crew_id,char_id,is_captain,is_first_mate,created_at)
                SELECT crewId,charId,1,0,NOW() FROM characters WHERE handle IN ('QAFriend','QAOther');
            """, Scenarios.CrewAdmissionFaultSql("CSR_Tester") },
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        readyMessages: new[] {
            "Open Mara NAV CSR (17000,495,6400), QAFriend (17120,495,6400), QAOther (17220,495,6400). CSR starts unaffiliated. QAFriend leads QACrew and QAOther leads QAPeerCrew, both in QAFaction. No teleport, target selection or dev command.",
            """Send {"actor":"QAFriend","action":"crew-admission-invite"} and accept the stock crew invitation. The disclosed disposable CHECK refuses assigning CSR to QACrew. Expect [SOCIAL] Crew admission rolled back, 'Your affiliations were kept.' in chat, CSR still unaffiliated and no joined crew or faction status.""",
            """Send {"actor":"QAOther","action":"crew-admission-invite"} and accept the fresh crew invitation in the same session. Expect Crew 80 86 and Faction 7c, showing QAPeerCrew with QAOther captain and CSR member. Relog and verify QAPeerCrew and QAFaction persist."""
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
        var first = context.Helpers["QAFriend"];
        var second = context.Helpers["QAOther"];
        Check.Equal(2L, await db.LongAsync($"""
            SELECT COUNT(*) FROM characters p JOIN crews c ON p.crewId=c.id AND p.factionId=c.faction_id
            JOIN crew_members m ON m.char_id=p.charId AND m.crew_id=c.id
            WHERE c.faction_id={Faction} AND c.master_player_handle=p.handle AND m.is_captain=1
              AND ((p.charId={first.CharId} AND c.id={RejectedCrew}) OR (p.charId={second.CharId} AND c.id={AcceptedCrew}));
            """), "Both helpers are captains of the intended faction crews");
        Check.Equal(1L, await db.LongAsync("SELECT COUNT(*) FROM characters WHERE charId=110 AND crewId=0 AND factionId=0"),
            "CSR starts without a crew or faction");
        Check.Equal(1L, await db.LongAsync("SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS " +
            $"WHERE CONSTRAINT_SCHEMA=DATABASE() AND TABLE_NAME='characters' AND CONSTRAINT_NAME='{Scenarios.CrewAdmissionFault}'"),
            "The disclosed affiliation write fault is installed");
        if (context.StockProbe is not { } invitee) return Array.Empty<byte>();
        await invitee.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, invitee);
        await Scenarios.ProveAtomicCrewAdmissionAsync(invitee, first, second, db);
        return Array.Empty<byte>();
    }

    internal static string OfferInvitation(HeadlessPlayer actor)
    {
        if (actor.Handle is not ("QAFriend" or "QAOther")) throw new ArgumentException("Use QAFriend or QAOther.");
        Scenarios.SendCrewAdmissionInvite(actor, "CSR_Tester");
        return $"Sent {actor.Handle}'s crew invitation to CSR_Tester";
    }
}
