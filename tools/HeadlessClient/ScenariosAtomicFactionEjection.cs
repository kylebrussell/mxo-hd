namespace HeadlessClient;

public static partial class Scenarios
{
    public const string FactionDepartureFault = "qa_faction_departure_update";

    public static string FactionDepartureFaultSql(uint crew) => $"""
        SET SESSION sql_mode='';
        ALTER TABLE characters ADD CONSTRAINT {FactionDepartureFault}
            CHECK (crewId<>{crew} OR cash<>1000 OR factionId<>0);
        """;

    private static async Task AtomicFactionEjection(ScenarioContext context)
    {
        const uint leaderCrew = 9101, targetCrew = 9102, faction = 9103;
        var db = context.Database;
        await db.ExecuteAsync($"""
            INSERT INTO factions(id,name,master_player_handle,money) VALUES ({faction},'QAFaction','EjectionLeader',0);
            INSERT INTO crews(id,crew_name,master_player_handle,money,org,faction_id,faction_rank)
            VALUES ({leaderCrew},'QACrew','EjectionLeader',0,1,{faction},0);
            """);
        var leader = await context.AddPlayerAsync(new CharacterSeed("EjectionLeader")
        {
            X = 17000, Y = 495, Z = 6400, CrewId = (int)leaderCrew, FactionId = (int)faction
        });
        await leader.EnterWorldAsync();
        await leader.WaitForRpcAsync($"7c {Le(leader.CharId)} {Le(faction)} 02");
        // The first login reads the faction roster. Add the next crew just before its captain logs in.
        await db.ExecuteAsync($"""
            INSERT INTO crews(id,crew_name,master_player_handle,money,org,faction_id,faction_rank)
            VALUES ({targetCrew},'QAPeerCrew','EjectionTarget',0,1,{faction},5);
            """);
        var captain = await context.AddPlayerAsync(new CharacterSeed("EjectionTarget")
        {
            X = 17220, Y = 495, Z = 6400, CrewId = (int)targetCrew, FactionId = (int)faction
        });
        var member = await context.AddPlayerAsync(new CharacterSeed("EjectionMember")
        {
            X = 17120, Y = 495, Z = 6400, CrewId = (int)targetCrew, FactionId = (int)faction, Info = 500
        });
        await db.ExecuteAsync($"""
            INSERT INTO crew_members(crew_id,char_id,is_captain,is_first_mate,created_at)
            VALUES ({leaderCrew},{leader.CharId},1,0,NOW()),({targetCrew},{captain.CharId},1,0,NOW()),
                ({targetCrew},{member.CharId},0,0,NOW());
            """);
        await db.ExecuteAsync(FactionDepartureFaultSql(targetCrew));
        await captain.EnterWorldAsync();
        await member.EnterWorldAsync();

        captain.SendRpc($"73 {Le(leaderCrew)}");
        await captain.WaitForRpcAsync($"74 {Le(leaderCrew)} 14 00 00 0d");
        Check.Equal((long)faction, await db.LongAsync($"SELECT faction_id FROM crews WHERE id={leaderCrew}"),
            "A rank-5 captain cannot eject a crew");
        // The stored rank zero uses the legacy rank-1 fallback for the faction's master captain.
        await ProveAtomicFactionEjectionAsync(leader, captain, db, targetCrew, faction, member);
    }

    public static async Task DepositOneCrewInfoAsync(HeadlessPlayer captain)
    {
        captain.SkipReceived();
        captain.SendRpc("78 02 01 00 00 00 01");
        await captain.WaitForRpcAsync($"79 02 01 00 00 00 {Le(captain.CharId)} 01");
        await captain.WaitForRpcAsync("80 e4 e7 03 00 00");
    }

    public static async Task ProveAtomicFactionEjectionAsync(HeadlessPlayer leader, HeadlessPlayer captain,
        ThrowawayDatabase db, uint targetCrew, uint faction, HeadlessPlayer? member = null)
    {
        string eject = $"73 {Le(targetCrew)}";
        int removedMembers = member == null ? 1 : 2;
        leader.SkipReceived();
        captain.SkipReceived();
        member?.SkipReceived();
        leader.SendRpc(eject);
        await leader.WaitForRpcAsync($"74 {Le(targetCrew)} 00 00 00 00");
        await leader.WaitForTextAsync("The faction removal could not be saved. The roster was kept.");
        Check.Equal(0, leader.Received(eject).Count + captain.Received(eject).Count + (member?.Received(eject).Count ?? 0),
            "Nobody receives a removal after a rollback");
        Check.Equal(1L, await db.LongAsync($"SELECT COUNT(*) FROM crews WHERE id={targetCrew} AND faction_id={faction} AND faction_rank=5"),
            "A member write failure restores the target crew's faction and rank");
        Check.Equal((long)removedMembers, await MemberCount(faction), "Every target member still belongs to the faction");

        await DepositOneCrewInfoAsync(captain);
        leader.SendRpc(eject);
        await leader.WaitForRpcAsync(eject);
        await captain.WaitForRpcAsync(eject);
        if (member != null) await member.WaitForRpcAsync(eject);
        Check.Equal(1L, await db.LongAsync($"SELECT COUNT(*) FROM crews WHERE id={targetCrew} AND faction_id=0 AND faction_rank=0"),
            "The identical retry clears the faction and rank");
        Check.Equal((long)removedMembers, await MemberCount(0), "Every target member still belongs to the crew with no faction");
        Check.Equal((long)faction, await db.LongAsync($"SELECT factionId FROM characters WHERE charId={leader.CharId}"),
            "The requesting captain keeps their faction");

        leader.SendRpc(eject);
        await leader.WaitForRpcAsync($"74 {Le(targetCrew)} 14 00 00 0d");
        Check.Equal(1, leader.Received(eject).Count, "The stale repeat cannot publish another removal");

        Task<long> MemberCount(uint id) =>
            db.LongAsync($"SELECT COUNT(*) FROM characters WHERE crewId={targetCrew} AND factionId={id}");
    }
}
