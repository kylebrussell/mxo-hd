using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class AtomicFactionDisbandMode : SocialBreadthMode
{
    private const int X = 17000, Y = 495, Z = 6400, PeerX = 17220, FriendX = 17120;
    private const string PeerCaptain = "QAOther";

    public override string Name => "faction-disband-atomic";
    public override NativeQaModePlan Plan { get; }

    public AtomicFactionDisbandMode() : base(seedFaction: true)
    {
        var social = base.Plan;
        Plan = new(social.Abilities.ToArray(), X, Z, social.BeforeLoadout.ToArray(), social.AfterLoadout.ToArray(),
            social.AfterHelpers.Concat(new[] { $"""
                UPDATE factions SET money={Scenarios.FactionDisbandBankInfo} WHERE id={FactionId};
                UPDATE characters SET z={Z},cash=1000 WHERE handle='{PeerCaptain}';
                UPDATE characters SET z={Z} WHERE handle='QAFriend';
                """, Scenarios.FactionDepartureFaultSql(PeerCrew) }).ToArray(),
            environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
            readyMessages: new[] {
                "CSR leads QAFaction with QACrew rank 1, QAPeerCrew rank 5 and 500 Info in the Faction bank. Open Mara NAV CSR (17000,495,6400), QAOther (17220,495,6400), QAFriend (17120,495,6400). No teleport, target selection or dev command.",
                "Open the people icon above chat, choose Faction, then Disband and confirm. The client may print 'Disbanding Faction.' as it sends 77 01. The disclosed disposable SQL CHECK refuses the last write. Expect [SOCIAL] Faction disband rolled back, 'The roster and bank were kept.' in chat, both crews and 500 Info still in Faction.",
                """Send {"actor":"QAOther","action":"faction-disband-retry"} and wait for "QAOther deposited 1 Info (999 remains)". It goes into the crew bank and releases the fixture predicate.""",
                "Choose Faction Disband and confirm again in the same session. Expect server 77 01 and both crews to lose QAFaction. QACrew stays in the Crew panel. Relog and verify the crew still exists and the Faction panel shows unaffiliated."
            });
    }

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        await base.AfterStartAsync(context);
        var db = context.OwnedDatabase!;
        NavMesh.UseDirectory(Path.Combine(context.RepositoryRoot!, "data", "navmesh"));
        foreach (int x in new[] { X, PeerX, FriendX })
            Check.That(NavMesh.Locate(1, x, Y, Z, out var stand) && !stand.indoors && stand.floorY + 95 == Y,
                $"Outdoor NAV at {x},{Y},{Z}");

        var peer = context.Helpers[PeerCaptain];
        Check.Equal((long)Scenarios.FactionDisbandBankInfo, await db.LongAsync($"SELECT money FROM factions WHERE id={FactionId} AND deleted_at IS NULL"),
            "The faction starts with the expected treasury");
        Check.Equal(1000L, await db.LongAsync($"SELECT cash FROM characters WHERE charId={peer.CharId}"),
            "The peer captain's wallet matches the fault predicate");
        Check.Equal(1L, await db.LongAsync("SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS " +
            $"WHERE CONSTRAINT_SCHEMA=DATABASE() AND TABLE_NAME='characters' AND CONSTRAINT_NAME='{Scenarios.FactionDepartureFault}'"),
            "The disclosed affiliation fault is installed");
        if (context.StockProbe is not { } leader) return Array.Empty<byte>();
        await leader.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, leader);
        await Scenarios.ProveAtomicFactionDisbandAsync(leader, peer, db, FactionId, CaptainCrew, PeerCrew);
        return Array.Empty<byte>();
    }

    internal static async Task<string> ReleaseFaultAsync(HeadlessPlayer actor)
    {
        if (actor.Handle != PeerCaptain) throw new ArgumentException("Use QAOther for the faction disband retry.");
        await Scenarios.DepositOneCrewInfoAsync(actor);
        return "QAOther deposited 1 Info (999 remains)";
    }
}
