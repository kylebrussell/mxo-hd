namespace HeadlessClient;

public static partial class Scenarios
{
    public const uint AdmissionCrew = 9101, InvitingCrew = 9102, InvitingFaction = 9103;
    private const string AdmissionFailure = "The faction join could not be saved. Your crew was kept.";

    private static async Task AtomicFactionAdmission(ScenarioContext context)
    {
        var db = context.Database;
        await db.ExecuteAsync($"""
            INSERT INTO factions(id,name,master_player_handle,money) VALUES ({InvitingFaction},'QAFaction','InviteLeader',0);
            INSERT INTO crews(id,crew_name,master_player_handle,money,org,faction_id,faction_rank)
            VALUES ({AdmissionCrew},'QACrew','InviteCaptain',0,1,0,0),
                ({InvitingCrew},'QAPeerCrew','InviteLeader',0,1,{InvitingFaction},1);
            """);
        var captain = await context.AddPlayerAsync(new CharacterSeed("InviteCaptain")
        {
            X = 17000, Y = 495, Z = 6400, CrewId = (int)AdmissionCrew
        });
        var leader = await context.AddPlayerAsync(new CharacterSeed("InviteLeader")
        {
            X = 17220, Y = 495, Z = 6400, CrewId = (int)InvitingCrew, FactionId = (int)InvitingFaction
        });
        var member = await context.AddPlayerAsync(new CharacterSeed("InviteMember")
        {
            X = 16880, Y = 495, Z = 6400, CrewId = (int)AdmissionCrew, Info = 500
        });
        await db.ExecuteAsync($"""
            INSERT INTO crew_members(crew_id,char_id,is_captain,is_first_mate,created_at)
            VALUES ({AdmissionCrew},{captain.CharId},1,0,NOW()),({AdmissionCrew},{member.CharId},0,0,NOW()),
                ({InvitingCrew},{leader.CharId},1,0,NOW());
            """);
        await db.ExecuteAsync(FactionAffiliationFaultSql(AdmissionCrew));
        await captain.EnterWorldAsync();
        await leader.EnterWorldAsync();
        await member.EnterWorldAsync();

        await OfferFactionInvite(captain, leader);
        await db.ExecuteAsync($"UPDATE factions SET master_player_handle='ReplacementLeader' WHERE id={InvitingFaction}");
        captain.SendRpc(InviteAnswer(1, 0, leader.Handle));
        await captain.WaitForTextAsync("That faction invitation is no longer valid.");
        await leader.WaitForTextAsync("That faction invitation is no longer valid.");
        Check.Equal(0L, await db.LongAsync($"SELECT faction_id FROM crews WHERE id={AdmissionCrew}"),
            "An invitation cannot be accepted after its sender loses authority");
        await db.ExecuteAsync($"UPDATE factions SET master_player_handle='InviteLeader' WHERE id={InvitingFaction}");
        await ProveAtomicFactionAdmissionAsync(captain, leader, member, db);
    }

    public static void SendFactionAdmissionInvite(HeadlessPlayer leader, string target) =>
        leader.SendRpc(new byte[] { 0x6e }.Concat(hds.SocialRequests.BuildGroupInvite(1, target, "")).ToArray());

    private static async Task OfferFactionInvite(HeadlessPlayer captain, HeadlessPlayer leader)
    {
        captain.SkipReceived();
        leader.SkipReceived();
        SendFactionAdmissionInvite(leader, captain.Handle);
        await captain.WaitForRpcAsync("6e 01 06 00");
    }

    public static async Task ProveAtomicFactionAdmissionAsync(
        HeadlessPlayer captain, HeadlessPlayer leader, HeadlessPlayer member, ThrowawayDatabase db)
    {
        string roster = $"7c {Le(captain.CharId)} {Le(InvitingFaction)} 02";
        byte[] accept = InviteAnswer(1, 0, leader.Handle);
        await OfferFactionInvite(captain, leader);
        captain.SendRpc(accept);
        await captain.WaitForTextAsync(AdmissionFailure);
        await leader.WaitForTextAsync(AdmissionFailure);
        Check.Equal(1L, await db.LongAsync($"SELECT COUNT(*) FROM crews WHERE id={AdmissionCrew} AND faction_id=0 AND faction_rank=0"),
            "A failed member write rolls back the crew's faction and rank");
        Check.Equal(2L, await db.LongAsync($"SELECT COUNT(*) FROM characters WHERE crewId={AdmissionCrew} AND factionId=0"),
            "Both crew members remain unaffiliated");

        captain.SendRpc("78 02 01 00 00 00 01");
        await captain.WaitForRpcAsync("80 e4 e7 03 00 00");
        captain.SendRpc(accept); // With writes available again, the consumed invitation must still fail.
        captain.SendRpc("81 48");
        await captain.WaitForRpcAsync("81 49");
        Check.Equal(0, captain.Received(roster).Count + member.Received($"7c {Le(member.CharId)} {Le(InvitingFaction)} 02").Count,
            "Neither the failure nor replay publishes an accepted roster");

        member.SkipReceived();
        await OfferFactionInvite(captain, leader);
        captain.SendRpc(accept);
        await captain.WaitForRpcAsync(roster);
        await member.WaitForRpcAsync($"7c {Le(member.CharId)} {Le(InvitingFaction)} 02");
        await leader.WaitForRpcAsync($"7c {Le(leader.CharId)} {Le(InvitingFaction)} 02");
        Check.Equal(1L, await db.LongAsync($"SELECT COUNT(*) FROM crews WHERE id={AdmissionCrew} AND faction_id={InvitingFaction} AND faction_rank=5"),
            "The fresh invitation adds the crew to the intended faction at rank 5");
        Check.Equal(2L, await db.LongAsync($"SELECT COUNT(*) FROM characters WHERE crewId={AdmissionCrew} AND factionId={InvitingFaction}"),
            "Both members receive the same committed affiliation");
    }
}
