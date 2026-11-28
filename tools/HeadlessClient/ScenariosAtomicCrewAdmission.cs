namespace HeadlessClient;

public static partial class Scenarios
{
    public const uint CrewAdmissionRejected = 9101, CrewAdmissionAccepted = 9102, CrewAdmissionFaction = 9103;
    public const string CrewAdmissionFault = "qa_crew_invitee_affiliation";
    private const string CrewAdmissionRefused = "The crew invitation could not be accepted. Your affiliations were kept.";

    public static string CrewAdmissionFactionSql => $"""
        INSERT INTO factions(id,name,master_player_handle,money)
            VALUES ({CrewAdmissionFaction},'QAFaction','QAOther',500);
        INSERT INTO crews(id,crew_name,master_player_handle,money,org,faction_id,faction_rank)
            VALUES ({CrewAdmissionAccepted},'QAPeerCrew','QAOther',200,1,{CrewAdmissionFaction},1);
        """;

    public static string CrewAdmissionPeerSql => $"""
        INSERT INTO crews(id,crew_name,master_player_handle,money,org,faction_id,faction_rank)
            VALUES ({CrewAdmissionRejected},'QACrew','QAFriend',100,1,{CrewAdmissionFaction},5);
        """;

    public static string CrewAdmissionFaultSql(string invitee) => $"""
        SET SESSION sql_mode='';
        ALTER TABLE characters ADD CONSTRAINT {CrewAdmissionFault}
            CHECK (handle<>'{invitee}' OR crewId<>{CrewAdmissionRejected} OR factionId<>{CrewAdmissionFaction});
        """;

    private static async Task AtomicCrewAdmission(ScenarioContext context)
    {
        var db = context.Database;
        await db.ExecuteAsync(CrewAdmissionFactionSql);
        var accepted = await context.AddPlayerAsync(new CharacterSeed("QAOther")
        {
            X = 17220, Y = 495, Z = 6400, CrewId = (int)CrewAdmissionAccepted, FactionId = (int)CrewAdmissionFaction
        });
        await db.ExecuteAsync(CrewAdmissionPeerSql);
        var rejected = await context.AddPlayerAsync(new CharacterSeed("QAFriend")
        {
            X = 17120, Y = 495, Z = 6400, CrewId = (int)CrewAdmissionRejected, FactionId = (int)CrewAdmissionFaction
        });
        var invitee = await context.AddPlayerAsync(new CharacterSeed("AdmissionGuest") { X = 17000, Y = 495, Z = 6400 });
        var stale = await context.AddPlayerAsync(new CharacterSeed("CrewStaleGuest") { X = 16900, Y = 495, Z = 6400 });
        await db.ExecuteAsync($"""
            INSERT INTO crew_members(crew_id,char_id,is_captain,is_first_mate,created_at)
                VALUES ({CrewAdmissionRejected},{rejected.CharId},1,0,NOW()),({CrewAdmissionAccepted},{accepted.CharId},1,0,NOW());
            """);
        await db.ExecuteAsync(CrewAdmissionFaultSql(invitee.Handle));
        await accepted.EnterWorldAsync();
        await rejected.EnterWorldAsync();
        await invitee.EnterWorldAsync();
        await stale.EnterWorldAsync();
        await ProveAtomicCrewAdmissionAsync(invitee, rejected, accepted, db);

        SendCrewAdmissionInvite(rejected, stale.Handle);
        await stale.WaitForRpcAsync("80 88 07 00");
        // Model the inviter changing crews after sending the invitation, before the invitee replies.
        await db.ExecuteAsync($"UPDATE characters SET crewId={CrewAdmissionAccepted} WHERE charId={rejected.CharId}");
        stale.SendRpc(InviteAnswer(Crew, 0, rejected.Handle));
        await rejected.WaitForRpcAsync("6f 02 08 00 01 00 00 00");
        await stale.WaitForTextAsync(CrewAdmissionRefused);
        Check.Equal(0L, await db.LongAsync($"SELECT COUNT(*) FROM crew_members WHERE char_id={stale.CharId}"),
            "A stale invite cannot admit the player to either crew");
        Check.Equal(0L, await db.LongAsync($"SELECT crewId FROM characters WHERE charId={stale.CharId}"),
            "Stale acceptance keeps the invitee unaffiliated");
    }

    public static void SendCrewAdmissionInvite(HeadlessPlayer captain, string invitee) =>
        captain.SendRpc(GroupInvite(Crew, invitee, ""));

    public static async Task ProveAtomicCrewAdmissionAsync(HeadlessPlayer invitee, HeadlessPlayer rejected,
        HeadlessPlayer accepted, ThrowawayDatabase db)
    {
        invitee.SkipReceived();
        rejected.SkipReceived();
        accepted.SkipReceived();
        SendCrewAdmissionInvite(rejected, invitee.Handle);
        await invitee.WaitForRpcAsync("80 88 07 00");
        invitee.SendRpc(InviteAnswer(Crew, 0, rejected.Handle));
        await rejected.WaitForRpcAsync("6f 02 08 00 01 00 00 00");
        await invitee.WaitForTextAsync(CrewAdmissionRefused);
        await rejected.WaitForTextAsync(CrewAdmissionRefused);
        Check.Equal(0L, await db.LongAsync($"SELECT COUNT(*) FROM crew_members WHERE crew_id={CrewAdmissionRejected} AND char_id={invitee.CharId}"),
            "A failed character affiliation rolls back the roster insert");
        Check.Equal(1L, await db.LongAsync($"SELECT COUNT(*) FROM characters WHERE charId={invitee.CharId} AND crewId=0 AND factionId=0"),
            "The invitee stays unaffiliated after rollback");
        Check.Equal(0, rejected.Received($"70 02 {Le(invitee.CharId)}").Count, "The failed crew receives no join broadcast");
        Check.Equal(0, invitee.Received($"80 86 {Le(invitee.CharId)} {Le(CrewAdmissionRejected)}").Count +
            invitee.Received($"7c {Le(invitee.CharId)} {Le(CrewAdmissionFaction)} 01").Count,
            "The invitee receives no rejected crew or faction membership");

        SendCrewAdmissionInvite(accepted, invitee.Handle);
        await invitee.WaitForRpcAsync("80 88 07 00");
        invitee.SendRpc(InviteAnswer(Crew, 0, accepted.Handle));
        await invitee.WaitForRpcAsync($"80 86 {Le(invitee.CharId)} {Le(CrewAdmissionAccepted)}");
        await invitee.WaitForRpcAsync($"7c {Le(invitee.CharId)} {Le(CrewAdmissionFaction)} 01");
        await invitee.WaitForRpcAsync($"7c {Le(invitee.CharId)} {Le(CrewAdmissionFaction)} 02");
        await accepted.WaitForRpcAsync($"70 02 {Le(invitee.CharId)}");
        Check.Equal(1L, await db.LongAsync($"""
            SELECT COUNT(*) FROM characters p JOIN crew_members m ON m.char_id=p.charId AND m.crew_id=p.crewId
            WHERE p.charId={invitee.CharId} AND p.crewId={CrewAdmissionAccepted} AND p.factionId={CrewAdmissionFaction}
              AND m.is_captain=0 AND m.is_first_mate=0;
            """), "The new roster row and both affiliations commit together");
        Check.Equal(0L, await db.LongAsync($"SELECT COUNT(*) FROM crew_members WHERE crew_id={CrewAdmissionRejected} AND char_id={invitee.CharId}"),
            "The first crew still has no row for the invitee");
    }
}
