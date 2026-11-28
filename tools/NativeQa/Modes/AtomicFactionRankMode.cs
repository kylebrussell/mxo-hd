using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class AtomicFactionRankMode : SocialBreadthMode
{
    private const int X = 17000, Y = 495, Z = 6400, FriendX = 17120, PeerX = 17220;

    public override string Name => "faction-rank-atomic";
    public override NativeQaModePlan Plan { get; }

    public AtomicFactionRankMode() : base(seedFaction: true)
    {
        var social = base.Plan;
        Plan = new(social.Abilities.ToArray(), X, Z, social.BeforeLoadout.ToArray(), social.AfterLoadout.ToArray(),
            social.AfterHelpers.Concat(new[] { $"""
                UPDATE characters SET z={Z},cash=1000 WHERE handle='QAOther';
                UPDATE characters SET z={Z} WHERE handle='QAFriend';
                """, Scenarios.FactionRankFaultSql }).ToArray(),
            environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
            readyMessages: new[] {
                "Open Mara NAV CSR (17000,495,6400), QAFriend (17120,495,6400) and QAOther (17220,495,6400). No teleport, world target or dev command. CSR leads QACrew at rank 1. QAOther captains QAPeerCrew at rank 5.",
                "People icon > Faction > More. Right-click QAPeerCrew and choose Promote. Confirm if asked. The disclosed disposable SQL CHECK rejects rank 4 with the peer crew bank at 0. Expect 80 81, a generic rank error, 'The roster was kept.' in chat and QAPeerCrew still at rank 5. Dismiss the error if modal.",
                """Send {"actor":"QAOther","action":"faction-rank-retry"} and wait for "QAOther deposited 1 Info (999 remains)". This raises the peer crew bank to 1 and releases the CHECK.""",
                "Promote QAPeerCrew again in the same session. Expect 7f 8e 23 00 00 04 and rank 4. Relog to check both ranks. Use faction-demote-stock for the stock Demote menu."
            });
    }

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        await base.AfterStartAsync(context);
        var db = context.OwnedDatabase!;
        NavMesh.UseDirectory(Path.Combine(context.RepositoryRoot!, "data", "navmesh"));
        foreach (int x in new[] { X, FriendX, PeerX })
            Check.That(NavMesh.Locate(1, x, Y, Z, out var stand) && !stand.indoors && stand.floorY + 95 == Y,
                $"Outdoor NAV at {x},{Y},{Z}");
        var peer = context.Helpers["QAOther"];
        Check.Equal(1000L, await db.LongAsync($"SELECT cash FROM characters WHERE charId={peer.CharId}"),
            "The peer can make the one-Info deposit");
        Check.Equal(0L, await db.LongAsync($"SELECT money FROM crews WHERE id={PeerCrew}"), "The fault starts armed");
        Check.Equal(1L, await db.LongAsync("SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS " +
            $"WHERE CONSTRAINT_SCHEMA=DATABASE() AND TABLE_NAME='crews' AND CONSTRAINT_NAME='{Scenarios.FactionRankFault}'"),
            "The disclosed rank write fault is installed");
        if (context.StockProbe is not { } leader) return Array.Empty<byte>();
        await leader.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, leader);
        await Scenarios.ProveAtomicFactionRankAsync(leader, peer, db, PeerCrew, FactionId);
        return Array.Empty<byte>();
    }
}
