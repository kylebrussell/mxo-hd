namespace HeadlessClient;

public static partial class Scenarios
{
    public const uint CrewDisbandTarget = 9101, CrewDisbandPeer = 9102, CrewDisbandFaction = 9103;
    private const string CrewDisbandUnauthorized = "Only the captain of an active crew can disband it.";

    public static string CrewDisbandFactionSql => $"""
        INSERT INTO factions(id,name,master_player_handle,money)
            VALUES ({CrewDisbandFaction},'QAFaction','QAOther',500);
        INSERT INTO crews(id,crew_name,master_player_handle,money,org,faction_id,faction_rank)
            VALUES ({CrewDisbandPeer},'QAPeerCrew','QAOther',100,1,{CrewDisbandFaction},1);
        """;

    public static string CrewDisbandSeedSql(string captain) => $"""
        INSERT INTO crews(id,crew_name,master_player_handle,money,org,faction_id,faction_rank)
            VALUES ({CrewDisbandTarget},'QACrew','{captain}',200,1,{CrewDisbandFaction},5);
        """;

    private static async Task AtomicCrewDisband(ScenarioContext context)
    {
        const string captain = "DisbandCaptain";
        var db = context.Database;
        await db.ExecuteAsync(CrewDisbandFactionSql);
        var peer = await context.AddPlayerAsync(new CharacterSeed("QAOther")
        {
            X = 17220, Y = 495, Z = 6400, CrewId = (int)CrewDisbandPeer, FactionId = (int)CrewDisbandFaction
        });
        await db.ExecuteAsync(CrewDisbandSeedSql(captain));
        var leader = await context.AddPlayerAsync(new CharacterSeed(captain)
        {
            X = 17000, Y = 495, Z = 6400, CrewId = (int)CrewDisbandTarget, FactionId = (int)CrewDisbandFaction
        });
        var member = await context.AddPlayerAsync(new CharacterSeed("QAFriend")
        {
            X = 17120, Y = 495, Z = 6400, CrewId = (int)CrewDisbandTarget, FactionId = (int)CrewDisbandFaction, Info = 1000
        });
        await db.ExecuteAsync($"""
            INSERT INTO crew_members(crew_id,char_id,is_captain,is_first_mate,created_at)
                VALUES ({CrewDisbandTarget},{leader.CharId},1,0,NOW()),({CrewDisbandTarget},{member.CharId},0,0,NOW()),
                    ({CrewDisbandPeer},{peer.CharId},1,0,NOW());
            """);
        await db.ExecuteAsync(CrewEjectionFaultSql);
        await peer.EnterWorldAsync();
        await leader.EnterWorldAsync();
        await member.EnterWorldAsync();

        member.SendRpc("77 02");
        await member.WaitForTextAsync(CrewDisbandUnauthorized);
        peer.SendRpc("77 02");
        await peer.WaitForTextAsync("Disband the faction before disbanding its leading crew.");
        Check.Equal(1L, await db.LongAsync($"SELECT COUNT(*) FROM crews WHERE id={CrewDisbandPeer} " +
            $"AND faction_id={CrewDisbandFaction} AND faction_rank=1 AND deleted_at IS NULL"),
            "Disbanding the leading crew cannot strand the faction");
        await ProveAtomicCrewDisbandAsync(leader, member, peer, db);

        // An ordinary leave still archives an empty crew when the last member departs.
        leader.SendRpc(CrewCreate("QAEmptyCrew", member.Handle, 1));
        await member.WaitForRpcAsync("80 88 07 00");
        member.SendRpc(InviteAnswer(Crew, 0, leader.Handle));
        var created = await leader.WaitForRpcAsync("80 85 ?? ?? ?? ?? 0d 00 01 00 00 00 00");
        uint emptyCrew = BitConverter.ToUInt32(created.Bytes, 2);
        member.SendRpc("71 02");
        await leader.WaitForRpcAsync($"72 02 {Le(member.CharId)}");
        leader.SendRpc("71 02");
        await WaitForDatabaseAsync(context, $"SELECT COUNT(*) FROM crews WHERE id={emptyCrew} AND deleted_at IS NOT NULL",
            "1", "the last member's departure archives the empty crew");
    }

    public static async Task ProveAtomicCrewDisbandAsync(HeadlessPlayer leader, HeadlessPlayer member,
        HeadlessPlayer peer, ThrowawayDatabase db)
    {
        string factionRemoval = $"73 {Le(CrewDisbandTarget)}";
        leader.SkipReceived();
        member.SkipReceived();
        peer.SkipReceived();
        leader.SendRpc("77 02");
        await leader.WaitForTextAsync("The crew could not be disbanded. The roster and bank were kept.");
        Check.Equal(0, leader.Received("77 02").Count + member.Received("77 02").Count + peer.Received(factionRemoval).Count,
            "Rollback sends no disband or faction removal");
        Check.Equal(200L, await db.LongAsync($"SELECT money FROM crews WHERE id={CrewDisbandTarget} " +
            $"AND faction_id={CrewDisbandFaction} AND faction_rank=5 AND deleted_at IS NULL"),
            "The active crew, rank and treasury survive rollback");
        Check.Equal(2L, await RosterRows(), "Both roster rows survive rollback");
        Check.Equal(2L, await Affiliations(CrewDisbandTarget, CrewDisbandFaction), "Both players keep their affiliations");

        await DepositOneCrewInfoAsync(member);
        leader.SendRpc("77 02");
        await leader.WaitForRpcAsync("77 02");
        await member.WaitForRpcAsync("77 02");
        await peer.WaitForRpcAsync(factionRemoval);
        Check.Equal(201L, await db.LongAsync($"SELECT money FROM crews WHERE id={CrewDisbandTarget} " +
            "AND faction_id=0 AND faction_rank=0 AND deleted_at IS NOT NULL"), "The disband archives the crew and its historical bank");
        Check.Equal(0L, await RosterRows(), "Both crew roster rows are deleted");
        Check.Equal(2L, await Affiliations(0, 0), "Both players leave the crew and faction");
        Check.Equal(500L, await db.LongAsync($"""
            SELECT f.money FROM factions f
            JOIN crews c ON c.faction_id=f.id AND c.id={CrewDisbandPeer} AND c.faction_rank=1 AND c.deleted_at IS NULL
            JOIN characters p ON p.factionId=f.id AND p.crewId=c.id AND p.charId={peer.CharId}
            WHERE f.id={CrewDisbandFaction} AND f.master_player_handle=p.handle AND f.deleted_at IS NULL;
            """), "The peer crew retains its faction, leader and treasury");
        Check.Equal(0, peer.Received("77 02").Count, "The other crew receives only its faction roster update");
        leader.SendRpc("77 02");
        await leader.WaitForTextAsync(CrewDisbandUnauthorized);
        Check.Equal(1, leader.Received("77 02").Count, "A repeated disband publishes no second removal");

        Task<long> RosterRows() => db.LongAsync($"SELECT COUNT(*) FROM crew_members WHERE crew_id={CrewDisbandTarget}");
        Task<long> Affiliations(uint crew, uint faction) => db.LongAsync($"SELECT COUNT(*) FROM characters " +
            $"WHERE charId IN ({leader.CharId},{member.CharId}) AND crewId={crew} AND factionId={faction}");
    }
}
