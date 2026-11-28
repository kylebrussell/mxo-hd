namespace HeadlessClient;

public static partial class Scenarios
{
    public const string FactionRankFault = "qa_faction_rank_update";

    public static string FactionRankFaultSql => $"""
        SET SESSION sql_mode='';
        ALTER TABLE crews ADD CONSTRAINT {FactionRankFault}
            CHECK (crew_name<>'QAPeerCrew' OR money<>0 OR faction_rank<>4);
        """;

    private static async Task AtomicFactionRank(ScenarioContext context)
    {
        const uint leaderCrew = 9101, targetCrew = 9102, faction = 9103;
        var db = context.Database;
        await db.ExecuteAsync($"""
            INSERT INTO factions(id,name,master_player_handle,money) VALUES ({faction},'QAFaction','RankLeader',500);
            INSERT INTO crews(id,crew_name,master_player_handle,money,org,faction_id,faction_rank)
            VALUES ({leaderCrew},'QACrew','RankLeader',0,1,{faction},0);
            """);
        var leader = await context.AddPlayerAsync(new CharacterSeed("RankLeader")
        {
            X = 17000, Y = 495, Z = 6400, CrewId = (int)leaderCrew, FactionId = (int)faction
        });
        // Login can read the faction roster before explicit world entry. Add the peer crew just before its captain logs in.
        await db.ExecuteAsync($"INSERT INTO crews(id,crew_name,master_player_handle,money,org,faction_id,faction_rank) " +
            $"VALUES ({targetCrew},'QAPeerCrew','QAOther',0,1,{faction},5)");
        var peer = await context.AddPlayerAsync(new CharacterSeed("QAOther")
        {
            X = 17220, Y = 495, Z = 6400, Info = 1000, CrewId = (int)targetCrew, FactionId = (int)faction
        });
        var member = await context.AddPlayerAsync(new CharacterSeed("QAFriend")
        {
            X = 17120, Y = 495, Z = 6400, CrewId = (int)leaderCrew, FactionId = (int)faction
        });
        await db.ExecuteAsync($"""
            INSERT INTO crew_members(crew_id,char_id,is_captain,is_first_mate,created_at)
                VALUES ({leaderCrew},{leader.CharId},1,0,NOW()),({leaderCrew},{member.CharId},0,0,NOW()),
                    ({targetCrew},{peer.CharId},1,0,NOW());
            """);
        await db.ExecuteAsync(FactionRankFaultSql);
        await leader.EnterWorldAsync();
        await peer.EnterWorldAsync();
        await member.EnterWorldAsync();

        string target = Le(targetCrew);
        member.SendRpc($"80 80 {target} 01");
        await member.WaitForRpcAsync($"80 81 {target} 01 10 00 00 0d");

        // Model a crew departure while the old roster is still open on the requesting client.
        await db.ExecuteAsync($"UPDATE crews SET faction_id=0,faction_rank=0 WHERE id={targetCrew}");
        leader.SendRpc($"80 80 {target} 01");
        await leader.WaitForRpcAsync($"80 81 {target} 01 0b 00 00 0d");
        Check.Equal(0L, await db.LongAsync($"SELECT faction_rank FROM crews WHERE id={targetCrew}"),
            "A stale roster cannot change the rank of an unaffiliated crew");
        await db.ExecuteAsync($"UPDATE crews SET faction_id={faction},faction_rank=5 WHERE id={targetCrew}");

        await ProveAtomicFactionRankAsync(leader, peer, db, targetCrew, faction);
        await member.WaitForRpcAsync($"7f {target} 04");
        await member.WaitForRpcAsync($"7f {target} 05");
    }

    public static async Task ProveAtomicFactionRankAsync(HeadlessPlayer leader, HeadlessPlayer peer,
        ThrowawayDatabase db, uint targetCrew, uint faction)
    {
        string target = Le(targetCrew);
        string promote = $"80 80 {target} 01";
        string rankDelta = $"7f {target}";
        leader.SkipReceived();
        peer.SkipReceived();
        leader.SendRpc(promote);
        await leader.WaitForRpcAsync($"80 81 {target} 01 00 00 00 00");
        await leader.WaitForTextAsync("The faction rank change could not be saved. The roster was kept.");
        Check.Equal(0, leader.Received(rankDelta).Count + peer.Received(rankDelta).Count,
            "A refused write publishes no rank delta");
        await CheckStoredRankAsync(5, 0);

        await DepositOneCrewInfoAsync(peer);
        leader.SendRpc(promote);
        await leader.WaitForRpcAsync($"{rankDelta} 04");
        await peer.WaitForRpcAsync($"{rankDelta} 04");
        await CheckStoredRankAsync(4, 1);

        leader.SendRpc($"80 80 {target} 00");
        await leader.WaitForRpcAsync($"{rankDelta} 05");
        await peer.WaitForRpcAsync($"{rankDelta} 05");
        await CheckStoredRankAsync(5, 1);

        async Task CheckStoredRankAsync(int rank, int bank)
        {
            Check.Equal(1L, await db.LongAsync($"SELECT COUNT(*) FROM crews WHERE id={targetCrew} " +
                $"AND faction_id={faction} AND faction_rank={rank} AND money={bank}"),
                $"The faction, rank {rank} and crew bank {bank} are persisted");
        }
    }
}
