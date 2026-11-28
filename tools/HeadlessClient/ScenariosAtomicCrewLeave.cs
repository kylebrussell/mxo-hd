namespace HeadlessClient;

public static partial class Scenarios
{
    private static async Task AtomicCrewLeave(ScenarioContext context)
    {
        const string captain = "LeavingCaptain";
        var db = context.Database;
        await db.ExecuteAsync(CaptainTransferSeedSql(captain));
        var leader = await context.AddPlayerAsync(new CharacterSeed(captain)
        {
            X = 17000, Y = 495, Z = 6400, Info = 1000, CrewId = (int)CaptainTransferCrew, FactionId = (int)CaptainTransferFaction
        });
        var next = await context.AddPlayerAsync(new CharacterSeed("QAFriend")
        {
            X = 17120, Y = 495, Z = 6400, CrewId = (int)CaptainTransferCrew, FactionId = (int)CaptainTransferFaction
        });
        await db.ExecuteAsync(CaptainTransferPeerSql);
        var peer = await context.AddPlayerAsync(new CharacterSeed("QAOther")
        {
            X = 17220, Y = 495, Z = 6400, CrewId = (int)CaptainTransferPeerCrew, FactionId = (int)CaptainTransferFaction
        });
        await db.ExecuteAsync($"""
            INSERT INTO crew_members(crew_id,char_id,is_captain,is_first_mate,created_at)
                VALUES ({CaptainTransferCrew},{leader.CharId},1,0,NOW()),({CaptainTransferCrew},{next.CharId},0,1,NOW()),
                    ({CaptainTransferPeerCrew},{peer.CharId},1,0,NOW());
            """);
        await db.ExecuteAsync(CaptainTransferFaultSql);
        await leader.EnterWorldAsync();
        await next.EnterWorldAsync();
        await peer.EnterWorldAsync();
        await ProveAtomicCrewLeaveAsync(leader, next, peer, db);

        next.SendRpc("71 02");
        await next.WaitForTextAsync("Disband the faction before leaving its last crew membership.");
        await next.WaitForRpcAsync($"80 86 {Le(next.CharId)} {Le(CaptainTransferCrew)}");
        Check.Equal(1L, await db.LongAsync($"SELECT COUNT(*) FROM characters WHERE charId={next.CharId} " +
            $"AND crewId={CaptainTransferCrew} AND factionId={CaptainTransferFaction}"), "The last member cannot strand their faction");
    }

    public static async Task ProveAtomicCrewLeaveAsync(HeadlessPlayer leader, HeadlessPlayer next,
        HeadlessPlayer peer, ThrowawayDatabase db)
    {
        string left = $"72 02 {Le(leader.CharId)}";
        string promoted = $"75 02 {Le(next.CharId)}";
        leader.SkipReceived();
        next.SkipReceived();
        peer.SkipReceived();
        leader.SendRpc("71 02");
        await leader.WaitForTextAsync("The crew could not be left. Membership and leadership were kept.");
        var restoredCrew = await leader.WaitForRpcAsync($"80 86 {Le(leader.CharId)} {Le(CaptainTransferCrew)}");
        Check.Equal(leader.CharId, BitConverter.ToUInt32(restoredCrew.Bytes, 13), "Refusal restores the captain in the crew window");
        Check.Equal(next.CharId, BitConverter.ToUInt32(restoredCrew.Bytes, 17), "Refusal restores the first mate");
        await WaitForFactionLeaderAsync(leader, leader.CharId, 500);
        Check.Equal(0, next.Received(left).Count + next.Received(promoted).Count, "Nothing is published to the remaining crew on rollback");
        Check.Equal(1L, await db.LongAsync($"SELECT COUNT(*) FROM characters WHERE charId={leader.CharId} " +
            $"AND crewId={CaptainTransferCrew} AND factionId={CaptainTransferFaction}"), "The leaver keeps both affiliations on rollback");
        Check.Equal(2L, await db.LongAsync($"SELECT COUNT(*) FROM crew_members WHERE crew_id={CaptainTransferCrew} " +
            $"AND ((char_id={leader.CharId} AND is_captain=1) OR (char_id={next.CharId} AND is_first_mate=1))"),
            "Rollback restores both roles after the final faction write fails");
        await AssertMasterAsync(leader, 500);

        leader.SendRpc("78 01 01 00 00 00 01");
        await leader.WaitForRpcAsync($"79 01 01 00 00 00 {Le(leader.CharId)} 01");
        await leader.WaitForRpcAsync("80 e4 e7 03 00 00");
        leader.SendRpc("71 02");
        await next.WaitForRpcAsync(left);
        await next.WaitForRpcAsync(promoted);
        await WaitForFactionLeaderAsync(next, next.CharId, 501);
        await WaitForFactionLeaderAsync(peer, next.CharId, 501);
        await AssertMasterAsync(next, 501);
        Check.Equal(1L, await db.LongAsync($"SELECT COUNT(*) FROM characters WHERE charId={leader.CharId} AND crewId=0 AND factionId=0"),
            "The leaver becomes unaffiliated on commit");
        Check.Equal(1L, await db.LongAsync($"SELECT COUNT(*) FROM crew_members WHERE crew_id={CaptainTransferCrew}"),
            "Exactly the successor remains in the crew");
        Check.Equal(1L, await db.LongAsync($"SELECT COUNT(*) FROM crew_members WHERE crew_id={CaptainTransferCrew} " +
            $"AND char_id={next.CharId} AND is_captain=1 AND is_first_mate=0"), "The first mate becomes captain and loses the old role");

        async Task AssertMasterAsync(HeadlessPlayer owner, int bank)
        {
            Check.Equal(owner.Handle, await db.ScalarAsync($"SELECT master_player_handle FROM crews WHERE id={CaptainTransferCrew}"),
                "The persisted crew captain is correct");
            Check.Equal(1L, await db.LongAsync($"SELECT COUNT(*) FROM factions WHERE id={CaptainTransferFaction} " +
                $"AND master_player_handle='{owner.Handle}' AND money={bank} AND deleted_at IS NULL"), "The active faction master and bank are correct");
        }
    }

    private static async Task WaitForFactionLeaderAsync(HeadlessPlayer viewer, uint captain, uint bank)
    {
        var packet = await viewer.WaitForRpcAsync($"7c {Le(viewer.CharId)} {Le(CaptainTransferFaction)} 01");
        Check.Equal(captain, BitConverter.ToUInt32(packet.Bytes, packet.Bytes.Length - 8), "The stock faction status names the current leader");
        Check.Equal(bank, BitConverter.ToUInt32(packet.Bytes, packet.Bytes.Length - 4), "The stock faction status has the committed bank");
        await viewer.WaitForRpcAsync($"7c {Le(viewer.CharId)} {Le(CaptainTransferFaction)} 02");
    }
}
