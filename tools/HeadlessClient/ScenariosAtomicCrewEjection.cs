namespace HeadlessClient;

public static partial class Scenarios
{
    public const uint CrewEjectionCrew = 9101, CrewEjectionFaction = 9103;
    public const string CrewEjectionFault = "qa_crew_member_departure";
    private const string CrewEjectionDenied = "Only the crew captain can remove a current crew member.";

    public static string CrewEjectionSeedSql(string captain) => $"""
        INSERT INTO factions(id,name,master_player_handle,money)
            VALUES ({CrewEjectionFaction},'QAFaction','{captain}',500);
        INSERT INTO crews(id,crew_name,master_player_handle,money,org,faction_id,faction_rank)
            VALUES ({CrewEjectionCrew},'QACrew','{captain}',200,1,{CrewEjectionFaction},1);
        """;

    public static string CrewEjectionFaultSql => $"""
        SET SESSION sql_mode='';
        ALTER TABLE characters ADD CONSTRAINT {CrewEjectionFault}
            CHECK (handle<>'QAFriend' OR cash<>1000 OR crewId<>0);
        """;

    private static async Task AtomicCrewEjection(ScenarioContext context)
    {
        const string captain = "EjectionCaptain";
        var db = context.Database;
        await db.ExecuteAsync(CrewEjectionSeedSql(captain));
        var leader = await context.AddPlayerAsync(new CharacterSeed(captain)
        {
            X = 17000, Y = 495, Z = 6400, CrewId = (int)CrewEjectionCrew, FactionId = (int)CrewEjectionFaction
        });
        var member = await context.AddPlayerAsync(new CharacterSeed("QAFriend")
        {
            X = 17120, Y = 495, Z = 6400, CrewId = (int)CrewEjectionCrew, FactionId = (int)CrewEjectionFaction, Info = 1000
        });
        await db.ExecuteAsync($"""
            INSERT INTO crew_members(crew_id,char_id,is_captain,is_first_mate,created_at)
                VALUES ({CrewEjectionCrew},{leader.CharId},1,0,NOW()),({CrewEjectionCrew},{member.CharId},0,0,NOW());
            """);
        await db.ExecuteAsync(CrewEjectionFaultSql);
        await leader.EnterWorldAsync();
        await member.EnterWorldAsync();
        member.SendRpc($"72 02 {Le(leader.CharId)}");
        await member.WaitForTextAsync(CrewEjectionDenied);
        Check.Equal(1L, await db.LongAsync($"SELECT COUNT(*) FROM crew_members WHERE crew_id={CrewEjectionCrew} " +
            $"AND char_id={leader.CharId} AND is_captain=1"), "A member cannot remove the captain");
        await ProveAtomicCrewEjectionAsync(leader, member, db);
    }

    public static async Task ProveAtomicCrewEjectionAsync(HeadlessPlayer leader, HeadlessPlayer member, ThrowawayDatabase db)
    {
        string remove = $"72 02 {Le(member.CharId)}";
        leader.SkipReceived();
        member.SkipReceived();
        leader.SendRpc(remove);
        await leader.WaitForTextAsync("The crew member could not be removed. The roster was kept.");
        Check.Equal(0, leader.Received(remove).Count + member.Received(remove).Count,
            "A rollback publishes no removal to either player");
        Check.Equal(1L, await Membership(), "A failed character write restores the deleted crew roster row");
        Check.Equal(1L, await Affiliation(CrewEjectionCrew, CrewEjectionFaction),
            "The member retains their crew and faction after rollback");

        await DepositOneCrewInfoAsync(member);
        leader.SendRpc(remove);
        await leader.WaitForRpcAsync(remove);
        await member.WaitForRpcAsync(remove);
        Check.Equal(0L, await Membership(), "The retry removes the crew roster row");
        Check.Equal(1L, await Affiliation(0, 0), "The retry clears both affiliations");
        Check.Equal(201L, await db.LongAsync($"SELECT money FROM crews WHERE id={CrewEjectionCrew} " +
            $"AND faction_id={CrewEjectionFaction} AND faction_rank=1 AND deleted_at IS NULL"), "The remaining crew keeps the deposit and faction rank");
        Check.Equal(1L, await db.LongAsync($"SELECT COUNT(*) FROM characters WHERE charId={leader.CharId} " +
            $"AND crewId={CrewEjectionCrew} AND factionId={CrewEjectionFaction}"), "The captain's affiliations are unaffected");
        leader.SendRpc(remove);
        await leader.WaitForTextAsync(CrewEjectionDenied);
        Check.Equal(1, leader.Received(remove).Count, "A stale retry does not publish another removal");

        Task<long> Membership() => db.LongAsync($"SELECT COUNT(*) FROM crew_members WHERE crew_id={CrewEjectionCrew} AND char_id={member.CharId}");
        Task<long> Affiliation(uint crew, uint faction) => db.LongAsync($"SELECT COUNT(*) FROM characters WHERE charId={member.CharId} " +
            $"AND crewId={crew} AND factionId={faction}");
    }
}
