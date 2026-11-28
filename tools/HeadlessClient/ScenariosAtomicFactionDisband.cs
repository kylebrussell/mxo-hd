namespace HeadlessClient;

public static partial class Scenarios
{
    public const int FactionDisbandBankInfo = 500;
    private const string DisbandRefusal = "Only the leader of an active faction can disband it.";

    private static async Task AtomicFactionDisband(ScenarioContext context)
    {
        const uint faction = 9103, leadCrew = 9101, peerCrew = 9102;
        var db = context.Database;
        await db.ExecuteAsync($"""
            INSERT INTO factions(id,name,master_player_handle,money)
                VALUES ({faction},'QAFaction','DisbandLeader',{FactionDisbandBankInfo});
            INSERT INTO crews(id,crew_name,master_player_handle,money,org,faction_id,faction_rank)
                VALUES ({leadCrew},'QACrew','DisbandLeader',0,1,{faction},1);
            """);
        var leader = await context.AddPlayerAsync(new CharacterSeed("DisbandLeader")
        {
            X = 17000, Y = 495, Z = 6400, CrewId = (int)leadCrew, FactionId = (int)faction
        });
        await leader.EnterWorldAsync();
        await leader.WaitForRpcAsync($"7c {Le(leader.CharId)} {Le(faction)} 02");
        await db.ExecuteAsync($"""
            INSERT INTO crews(id,crew_name,master_player_handle,money,org,faction_id,faction_rank)
                VALUES ({peerCrew},'QAPeerCrew','DisbandPeer',0,1,{faction},5);
            """);
        var peer = await context.AddPlayerAsync(new CharacterSeed("DisbandPeer")
        {
            X = 17220, Y = 495, Z = 6400, CrewId = (int)peerCrew, FactionId = (int)faction
        });
        var member = await context.AddPlayerAsync(new CharacterSeed("DisbandMember")
        {
            X = 17120, Y = 495, Z = 6400, CrewId = (int)peerCrew, FactionId = (int)faction, Info = 500
        });
        await db.ExecuteAsync($"""
            INSERT INTO crew_members(crew_id,char_id,is_captain,is_first_mate,created_at)
                VALUES ({leadCrew},{leader.CharId},1,0,NOW()),({peerCrew},{peer.CharId},1,0,NOW()),
                    ({peerCrew},{member.CharId},0,0,NOW());
            """);
        await db.ExecuteAsync(FactionDepartureFaultSql(peerCrew));
        await peer.EnterWorldAsync();
        await member.EnterWorldAsync();

        peer.SendRpc("77 01");
        await peer.WaitForTextAsync(DisbandRefusal);
        Check.Equal(1L, await db.LongAsync($"SELECT COUNT(*) FROM factions WHERE id={faction} AND deleted_at IS NULL"),
            "A crew captain cannot disband a faction they do not lead");
        await ProveAtomicFactionDisbandAsync(leader, peer, db, faction, leadCrew, peerCrew, member);
    }

    public static async Task ProveAtomicFactionDisbandAsync(HeadlessPlayer leader, HeadlessPlayer peer,
        ThrowawayDatabase db, uint faction, uint leadCrew, uint peerCrew, HeadlessPlayer? member = null)
    {
        int count = member == null ? 2 : 3;
        leader.SkipReceived();
        peer.SkipReceived();
        member?.SkipReceived();
        leader.SendRpc("77 01");
        await leader.WaitForTextAsync("The faction could not be disbanded. The roster and bank were kept.");
        Check.Equal(0, leader.Received("77 01").Count + peer.Received("77 01").Count + (member?.Received("77 01").Count ?? 0),
            "A refusal does not publish a disband");
        Check.Equal(FactionDisbandBankInfo, await db.LongAsync($"SELECT money FROM factions WHERE id={faction} AND deleted_at IS NULL"),
            "The failed affiliation write restores the live faction and treasury");
        Check.Equal(2L, await db.LongAsync($"SELECT COUNT(*) FROM crews WHERE faction_id={faction} " +
            $"AND ((id={leadCrew} AND faction_rank=1) OR (id={peerCrew} AND faction_rank=5))"),
            "Both crews retain their ranks and affiliation");
        Check.Equal((long)count, await Members(faction), "Every member's faction survives rollback");

        await DepositOneCrewInfoAsync(peer);
        leader.SendRpc("77 01");
        await leader.WaitForRpcAsync("77 01");
        await peer.WaitForRpcAsync("77 01");
        if (member != null) await member.WaitForRpcAsync("77 01");
        Check.Equal(FactionDisbandBankInfo, await db.LongAsync($"SELECT money FROM factions WHERE id={faction} AND deleted_at IS NOT NULL"),
            "The committed disband archives the faction with its historical balance");
        Check.Equal(2L, await db.LongAsync($"SELECT COUNT(*) FROM crews WHERE id IN ({leadCrew},{peerCrew}) AND faction_id=0 AND faction_rank=0"),
            "Both crews survive without a faction or faction rank");
        Check.Equal((long)count, await Members(0), "Every member keeps their crew and loses the faction");
        leader.SendRpc("77 01");
        await leader.WaitForTextAsync(DisbandRefusal);
        Check.Equal(1, leader.Received("77 01").Count, "A repeated disband does not publish again");

        Task<long> Members(uint id) =>
            db.LongAsync($"SELECT COUNT(*) FROM characters WHERE crewId IN ({leadCrew},{peerCrew}) AND factionId={id}");
    }
}
