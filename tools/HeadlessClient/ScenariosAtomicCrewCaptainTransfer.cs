using System.Text;

namespace HeadlessClient;

public static partial class Scenarios
{
    public const uint CaptainTransferCrew = 9101, CaptainTransferPeerCrew = 9102, CaptainTransferFaction = 9103;
    public const string CaptainTransferFault = "qa_faction_captain_update";
    private const string CaptainTransferRefusal = "Only the crew captain can transfer leadership to another current crew member.";
    private const string ReleaseCrewInput = "72 02 00 00 00 00";

    public static string CaptainTransferSeedSql(string captain) => $"""
        INSERT INTO factions(id,name,master_player_handle,money)
            VALUES ({CaptainTransferFaction},'QAFaction','{captain}',500);
        INSERT INTO crews(id,crew_name,master_player_handle,money,org,faction_id,faction_rank)
            VALUES ({CaptainTransferCrew},'QACrew','{captain}',200,1,{CaptainTransferFaction},1);
        """;

    public static string CaptainTransferPeerSql => $"""
        INSERT INTO crews(id,crew_name,master_player_handle,money,org,faction_id,faction_rank)
            VALUES ({CaptainTransferPeerCrew},'QAPeerCrew','QAOther',100,1,{CaptainTransferFaction},5);
        """;

    public static string CaptainTransferFaultSql => $"""
        SET SESSION sql_mode='';
        ALTER TABLE factions ADD CONSTRAINT {CaptainTransferFault}
            CHECK (name<>'QAFaction' OR money<>500 OR master_player_handle<>'QAFriend');
        """;

    private static async Task AtomicCrewCaptainTransfer(ScenarioContext context)
    {
        const string captain = "TransferCaptain";
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

        next.SendRpc($"75 02 {Le(leader.CharId)}");
        await next.WaitForRpcAsync(ReleaseCrewInput);
        await next.WaitForTextAsync(CaptainTransferRefusal);
        peer.SendRpc($"75 02 {Le(next.CharId)}");
        await peer.WaitForRpcAsync(ReleaseCrewInput);
        await peer.WaitForTextAsync(CaptainTransferRefusal);
        await ProveAtomicCrewCaptainTransferAsync(leader, next, peer, db);
    }

    public static async Task ProveAtomicCrewCaptainTransferAsync(HeadlessPlayer captain, HeadlessPlayer next,
        HeadlessPlayer peer, ThrowawayDatabase db)
    {
        string appoint = $"75 02 {Le(next.CharId)}";
        string announce = $"7e {Le(CaptainTransferCrew)} {Le(next.CharId)}";
        captain.SkipReceived();
        next.SkipReceived();
        peer.SkipReceived();
        int nextRefusals = next.Received(ReleaseCrewInput).Count, peerRefusals = peer.Received(ReleaseCrewInput).Count;
        captain.SendRpc(appoint);
        var refusal = await captain.WaitForRpcAsync(ReleaseCrewInput);
        Check.Equal(6, refusal.Bytes.Length, "The failure sends the stock-compatible crew input release");
        await captain.WaitForTextAsync("The captain could not be changed. Crew and faction leadership were kept.");
        Check.Equal(0, captain.Received(appoint).Count + next.Received(appoint).Count + peer.Received(announce).Count,
            "Rollback does not publish a captain or faction roster change");
        Check.Equal(nextRefusals, next.Received(ReleaseCrewInput).Count, "The first mate gets no refusal for CSR's request");
        Check.Equal(peerRefusals, peer.Received(ReleaseCrewInput).Count, "The other faction crew gets no refusal for CSR's request");
        await AssertLeadershipAsync(captain, next.CharId, 500);

        captain.SendRpc("78 01 01 00 00 00 01");
        await captain.WaitForRpcAsync($"79 01 01 00 00 00 {Le(captain.CharId)} 01");
        await captain.WaitForRpcAsync("80 e4 e7 03 00 00");
        captain.SendRpc(appoint);
        await captain.WaitForRpcAsync(appoint);
        await next.WaitForRpcAsync(appoint);
        var factionUpdate = await peer.WaitForRpcAsync(announce);
        Check.Equal(42, factionUpdate.Bytes.Length, "The other crew receives the faction captain packet");
        Check.Equal(next.Handle, Encoding.ASCII.GetString(factionUpdate.Bytes, 9, 32).TrimEnd('\0'),
            "The faction roster update names the new captain");
        await AssertLeadershipAsync(next, null, 501);

        captain.SendRpc(appoint);
        await captain.WaitForRpcAsync(ReleaseCrewInput);
        await captain.WaitForTextAsync(CaptainTransferRefusal);
        Check.Equal(1, captain.Received(appoint).Count, "The previous captain cannot repeat the transfer");

        async Task AssertLeadershipAsync(HeadlessPlayer leader, uint? mate, int bank)
        {
            Check.Equal(leader.Handle, await db.ScalarAsync($"SELECT master_player_handle FROM crews WHERE id={CaptainTransferCrew}"),
                "The crew keeps the expected master handle");
            Check.Equal((long)leader.CharId, await db.LongAsync($"SELECT char_id FROM crew_members WHERE crew_id={CaptainTransferCrew} AND is_captain=1"),
                "The roster captain matches the crew master");
            Check.Equal((long)(mate ?? 0), await db.LongAsync($"SELECT COALESCE(MAX(char_id),0) FROM crew_members " +
                $"WHERE crew_id={CaptainTransferCrew} AND is_first_mate=1"), "The roster keeps or clears the first mate with the transaction");
            Check.Equal(1L, await db.LongAsync($"SELECT COUNT(*) FROM factions WHERE id={CaptainTransferFaction} " +
                $"AND master_player_handle='{leader.Handle}' AND money={bank}"), "The faction master and bank match the committed state");
        }
    }
}
