namespace HeadlessClient;

public static partial class Scenarios
{
    private const string FactionLeaveUnauthorized = "Only the captain of a crew in the faction can leave it.";
    public const string FactionLeaveFault = "qa_faction_leave_update";

    public static string FactionLeaveFaultSql => $"""
        SET SESSION sql_mode='';
        ALTER TABLE characters ADD CONSTRAINT {FactionLeaveFault}
            CHECK (handle<>'QAFriend' OR crewId<>{CrewDisbandTarget} OR cash<>1000 OR factionId<>0);
        """;

    private static async Task AtomicFactionLeave(ScenarioContext context)
    {
        const string captain = "FactionLeaveCaptain";
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
            X = 17120, Y = 495, Z = 6400, Info = 1000, CrewId = (int)CrewDisbandTarget, FactionId = (int)CrewDisbandFaction
        });
        await db.ExecuteAsync($"""
            INSERT INTO crew_members(crew_id,char_id,is_captain,is_first_mate,created_at)
                VALUES ({CrewDisbandTarget},{leader.CharId},1,0,NOW()),({CrewDisbandTarget},{member.CharId},0,0,NOW()),
                    ({CrewDisbandPeer},{peer.CharId},1,0,NOW());
            """);
        await db.ExecuteAsync(FactionLeaveFaultSql);
        await peer.EnterWorldAsync();
        await leader.EnterWorldAsync();
        await member.EnterWorldAsync();

        member.SendRpc("71 01");
        await member.WaitForTextAsync(FactionLeaveUnauthorized);
        await member.WaitForRpcAsync($"7c {Le(member.CharId)} {Le(CrewDisbandFaction)} 02");
        peer.SendRpc("71 01");
        await peer.WaitForTextAsync("Disband the faction before its leading crew leaves.");
        await peer.WaitForRpcAsync($"7c {Le(peer.CharId)} {Le(CrewDisbandFaction)} 02");
        Check.Equal(1L, await db.LongAsync($"SELECT COUNT(*) FROM crews WHERE id={CrewDisbandPeer} " +
            $"AND faction_id={CrewDisbandFaction} AND faction_rank=1"), "The faction leader cannot withdraw the leading crew");
        await ProveAtomicFactionLeaveAsync(leader, member, peer, db);
    }

    public static async Task ProveAtomicFactionLeaveAsync(HeadlessPlayer leader, HeadlessPlayer member,
        HeadlessPlayer peer, ThrowawayDatabase db)
    {
        string removed = $"73 {Le(CrewDisbandTarget)}";
        leader.SkipReceived();
        member.SkipReceived();
        peer.SkipReceived();
        leader.SendRpc("71 01");
        await leader.WaitForTextAsync("The faction could not be left. Your crew and faction were kept.");
        var restored = await leader.WaitForRpcAsync($"7c {Le(leader.CharId)} {Le(CrewDisbandFaction)} 01");
        Check.Equal(peer.CharId, BitConverter.ToUInt32(restored.Bytes, restored.Bytes.Length - 8),
            "Refusal restores the faction leader");
        Check.Equal(500u, BitConverter.ToUInt32(restored.Bytes, restored.Bytes.Length - 4), "Refusal restores the faction bank");
        var roster = await leader.WaitForRpcAsync($"7c {Le(leader.CharId)} {Le(CrewDisbandFaction)} 02");
        const int headerSize = 17, rowSize = 81;
        Check.Equal(headerSize + 2 * rowSize, roster.Bytes.Length, "Refusal restores both faction roster entries");
        var ranks = Enumerable.Range(0, 2).Select(i => headerSize + rowSize * i)
            .ToDictionary(i => BitConverter.ToUInt32(roster.Bytes, i), i => roster.Bytes[i + rowSize - 1]);
        Check.Equal((byte)5, ranks[CrewDisbandTarget], "The restored crew still has rank 5");
        Check.Equal((byte)1, ranks[CrewDisbandPeer], "The restored leading crew still has rank 1");
        Check.Equal(0, leader.Received("77 01").Count + member.Received("77 01").Count + peer.Received(removed).Count,
            "No departure is published on rollback");
        Check.Equal(1L, await db.LongAsync($"SELECT COUNT(*) FROM crews WHERE id={CrewDisbandTarget} " +
            $"AND faction_id={CrewDisbandFaction} AND faction_rank=5 AND money=200"), "Rollback keeps the crew's rank and bank");
        Check.Equal(2L, await MemberCount(CrewDisbandFaction), "Both players retain faction membership on rollback");

        await DepositOneCrewInfoAsync(member);
        Check.Equal(999L, await db.LongAsync($"SELECT cash FROM characters WHERE charId={member.CharId}"),
            "Only QAFriend's wallet releases the disclosed failure");
        leader.SendRpc("71 01");
        await leader.WaitForRpcAsync("77 01");
        await member.WaitForRpcAsync("77 01");
        await peer.WaitForRpcAsync(removed);
        Check.Equal(1L, await db.LongAsync($"SELECT COUNT(*) FROM crews WHERE id={CrewDisbandTarget} " +
            "AND faction_id=0 AND faction_rank=0 AND money=201 AND deleted_at IS NULL"), "The crew stays active and retains its bank");
        Check.Equal(2L, await MemberCount(0), "Both players keep their crew and leave the faction");
        Check.Equal(2L, await db.LongAsync($"SELECT COUNT(*) FROM crew_members WHERE crew_id={CrewDisbandTarget}"),
            "The crew roster survives the faction departure");
        Check.Equal(500L, await db.LongAsync($"SELECT money FROM factions WHERE id={CrewDisbandFaction} " +
            $"AND master_player_handle='{peer.Handle}' AND deleted_at IS NULL"), "The faction keeps its leader and bank");
        Check.Equal(1L, await db.LongAsync($"SELECT COUNT(*) FROM characters WHERE charId={peer.CharId} " +
            $"AND factionId={CrewDisbandFaction} AND crewId={CrewDisbandPeer}"), "The peer remains in the faction");
        leader.SendRpc("71 01");
        await leader.WaitForTextAsync(FactionLeaveUnauthorized);
        Check.Equal(1, leader.Received("77 01").Count, "The repeated request cannot publish a second departure");

        Task<long> MemberCount(uint faction) => db.LongAsync($"SELECT COUNT(*) FROM characters WHERE charId IN ({leader.CharId},{member.CharId}) " +
            $"AND crewId={CrewDisbandTarget} AND factionId={faction}");
    }
}
