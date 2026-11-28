namespace HeadlessClient;

public static partial class Scenarios
{
    public const uint FirstMateCrew = 9101;
    public const string FirstMateFault = "qa_first_mate_write";
    private const string FirstMateRefusal = "Only the crew captain can appoint another current crew member as first mate or clear the role.";

    public static string FirstMateCrewSql(string captain) => $"""
        INSERT INTO crews(id,crew_name,master_player_handle,money,org,faction_id,faction_rank)
            VALUES ({FirstMateCrew},'QACrew','{captain}',200,1,0,0);
        """;

    public static string FirstMateFaultSql(uint target) => $"""
        SET SESSION sql_mode='';
        ALTER TABLE crew_members ADD CONSTRAINT {FirstMateFault}
            CHECK (crew_id<>{FirstMateCrew} OR char_id<>{target} OR is_first_mate=0);
        """;

    public static Task<string> ReleaseFirstMateFaultAsync(ThrowawayDatabase db) =>
        db.ExecuteAsync($"ALTER TABLE crew_members DROP CHECK {FirstMateFault}");

    private static async Task AtomicCrewFirstMate(ScenarioContext context)
    {
        const string captain = "FirstMateCaptain";
        var db = context.Database;
        await db.ExecuteAsync(FirstMateCrewSql(captain));
        var leader = await context.AddPlayerAsync(new CharacterSeed(captain)
        {
            X = 17000, Y = 495, Z = 6400, CrewId = (int)FirstMateCrew
        });
        var mate = await context.AddPlayerAsync(new CharacterSeed("QAFriend")
        {
            X = 17120, Y = 495, Z = 6400, CrewId = (int)FirstMateCrew
        });
        var target = await context.AddPlayerAsync(new CharacterSeed("QAOther")
        {
            X = 17220, Y = 495, Z = 6400, CrewId = (int)FirstMateCrew
        });
        await db.ExecuteAsync($"""
            INSERT INTO crew_members(crew_id,char_id,is_captain,is_first_mate,created_at)
                VALUES ({FirstMateCrew},{leader.CharId},1,0,NOW()),({FirstMateCrew},{mate.CharId},0,1,NOW()),
                    ({FirstMateCrew},{target.CharId},0,0,NOW());
            """);
        await db.ExecuteAsync(FirstMateFaultSql(target.CharId));
        await leader.EnterWorldAsync();
        await mate.EnterWorldAsync();
        await target.EnterWorldAsync();

        mate.SendRpc("80 87 00 00 00 00");
        await mate.WaitForTextAsync(FirstMateRefusal);

        // The roster row alone grants no role when the character's crew affiliation has changed.
        await db.ExecuteAsync($"UPDATE characters SET crewId=0 WHERE charId={target.CharId}");
        leader.SendRpc($"80 87 {Le(target.CharId)}");
        await leader.WaitForTextAsync(FirstMateRefusal);
        await db.ExecuteAsync($"UPDATE characters SET crewId={FirstMateCrew} WHERE charId={target.CharId}");

        await ProveAtomicCrewFirstMateAsync(leader, mate, target, db);
    }

    public static async Task ProveAtomicCrewFirstMateAsync(HeadlessPlayer captain, HeadlessPlayer mate,
        HeadlessPlayer target, ThrowawayDatabase db)
    {
        string appoint = $"80 87 {Le(target.CharId)}";
        const string clear = "80 87 00 00 00 00";
        var members = new[] { captain, mate, target };
        foreach (var member in members) member.SkipReceived();
        await CheckStoredMateAsync(mate.CharId);

        captain.SendRpc(appoint);
        await captain.WaitForTextAsync("The first mate could not be changed. The roster was kept.");
        foreach (var member in members)
            Check.Equal(0, member.Received("80 87").Count, "Rollback publishes no crew role update");
        await CheckStoredMateAsync(mate.CharId);

        await ReleaseFirstMateFaultAsync(db);
        captain.SendRpc(appoint);
        foreach (var member in members) await member.WaitForRpcAsync(appoint);
        await CheckStoredMateAsync(target.CharId);

        captain.SendRpc(clear);
        foreach (var member in members) await member.WaitForRpcAsync(clear);
        await CheckStoredMateAsync(0);

        async Task CheckStoredMateAsync(uint id)
        {
            string roles = await db.ScalarAsync($"SELECT COALESCE(GROUP_CONCAT(char_id), '') FROM crew_members " +
                $"WHERE crew_id={FirstMateCrew} AND is_first_mate=1");
            Check.Equal(id == 0 ? "" : id.ToString(), roles, "Exactly the expected first mate is stored");
            Check.Equal((long)captain.CharId, await db.LongAsync($"SELECT char_id FROM crew_members " +
                $"WHERE crew_id={FirstMateCrew} AND is_captain=1"), "The same captain remains in place");
        }
    }
}
