using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class FactionDemoteStockMode : SocialBreadthMode
{
    private const int X = 17000, Y = 495, Z = 6400, FriendX = 17120, PeerX = 17220;
    // The stock menu's 620b2160 predicate requires more than 2^(rank-1) crews at the target's rank.
    private const int Rank4Capacity = 8;
    private const int CrewCount = Rank4Capacity + 2;

    public override string Name => "faction-demote-stock";
    public override NativeQaModePlan Plan { get; }

    public FactionDemoteStockMode() : base(seedFaction: true, extraFactionCrews: Rank4Capacity)
    {
        var social = base.Plan;
        string offlineCrews = string.Join("\n", Enumerable.Range(1, Rank4Capacity).Select(i => $"""
            INSERT INTO crews(id,crew_name,master_player_handle,money,org,faction_id,faction_rank)
                VALUES ({9200 + i},'QARank{i}','QARankCaptain{i}',0,1,{FactionId},4);
            INSERT INTO characters(charId,userId,worldId,status,handle,alignment,crewId,factionId,is_online,created)
                VALUES ({6200 + i},0,1,0,'QARankCaptain{i}',1,{9200 + i},{FactionId},0,NOW());
            INSERT INTO crew_members(crew_id,char_id,is_captain,is_first_mate,created_at)
                VALUES ({9200 + i},{6200 + i},1,0,NOW());
            """));
        Plan = new(social.Abilities.ToArray(), X, Z, social.BeforeLoadout.ToArray(), social.AfterLoadout.ToArray(),
            social.AfterHelpers.Concat(new[] {
                $"UPDATE characters SET z={Z} WHERE handle IN ('QAFriend','QAOther');", offlineCrews
            }).ToArray(),
            environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
            readyMessages: new[] {
                "Open Mara NAV CSR (17000,495,6400), QAFriend (17120,495,6400) and QAOther (17220,495,6400). CSR leads QACrew at rank 1. QARank1 to QARank8 fill rank 4. QAPeerCrew starts at rank 5. No teleport, selection, helper command or forced failure.",
                "People icon > Faction > More. Right-click QARank1 under Rank 4. With exactly eight crews in that rank, Demote is absent. Dismiss the menu without changing the roster.",
                "Scroll to QAPeerCrew under Rank 5. Right-click > Promote > Yes. Expect 80 80 8e 23 00 00 01 and 7f 8e 23 00 00 04. Rank 4 now has nine crews.",
                "Right-click QAPeerCrew under Rank 4 > Demote > Yes. Expect 80 80 8e 23 00 00 00 and 7f 8e 23 00 00 05. QAPeerCrew returns to Rank 5. Confirm the result after relog. Rank 4 is back to eight and QARank1 no longer offers Demote."
            });
    }

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        await base.AfterStartAsync(context);
        var db = context.OwnedDatabase!;
        var server = context.ActualServer!;
        NavMesh.UseDirectory(Path.Combine(context.RepositoryRoot!, "data", "navmesh"));
        foreach (int x in new[] { X, FriendX, PeerX })
            Check.That(NavMesh.Locate(1, x, Y, Z, out var stand) && !stand.indoors && stand.floorY + 95 == Y,
                $"Outdoor NAV at {x},{Y},{Z}");
        Check.Equal((long)Rank4Capacity, await db.LongAsync($"""
            SELECT COUNT(*) FROM crews c JOIN characters p ON p.crewId=c.id AND p.handle=c.master_player_handle
                JOIN crew_members m ON m.crew_id=c.id AND m.char_id=p.charId
            WHERE c.faction_id={FactionId} AND p.factionId={FactionId} AND c.faction_rank=4
                AND m.is_captain=1 AND p.is_online=0
            """), "Eight rank-4 crews have valid offline captains");
        if (context.StockProbe is not { } leader) return Array.Empty<byte>();

        await leader.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, leader);
        var peer = context.Helpers["QAOther"];
        var ranks = await ReadFactionRanksAsync(leader, CrewCount);
        Check.Equal((byte)1, ranks[CaptainCrew], "CSR's serialized rank permits managing lower crews");
        Check.Equal((byte)5, ranks[PeerCrew], "QAPeerCrew starts as the only crew at the deepest rank");
        Check.Equal(Rank4Capacity, ranks.Values.Count(rank => rank == 4), "Demote is hidden at rank-4 capacity");
        Check.Equal(1, ranks.Values.Count(rank => rank == 5), "QAPeerCrew is promotable from the bottom rank");

        foreach (byte rank in new byte[] { 4, 5 })
        {
            string requestFlag = rank == 4 ? "01" : "00";
            leader.SendRpc($"80 80 {Le(PeerCrew)} {requestFlag}");
            var delta = await leader.WaitForRpcAsync($"7f {Le(PeerCrew)} {rank:x2}");
            await peer.WaitForRpcAsync($"7f {Le(PeerCrew)} {rank:x2}");
            ranks[PeerCrew] = delta.Bytes[5]; // The stock 7f consumer updates the roster member's rank.
            int expected = rank == 4 ? Rank4Capacity + 1 : Rank4Capacity;
            Check.Equal(expected, ranks.Values.Count(value => value == 4), "The wire rank delta crosses the Demote threshold");
            Check.Equal((long)expected, await db.LongAsync(
                $"SELECT COUNT(*) FROM crews WHERE faction_id={FactionId} AND faction_rank=4"),
                "The committed roster matches the stock wire ranks");
        }

        return Array.Empty<byte>();
    }
}
