using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class AtomicFactionEjectionMode : SocialBreadthMode
{
    private const int X = 17000, Y = 495, Z = 6400, PeerX = 17220, FriendX = 17120;
    private const string Target = "QAOther";

    public override string Name => "faction-ejection-atomic";
    public override NativeQaModePlan Plan { get; }

    public AtomicFactionEjectionMode() : base(seedFaction: true)
    {
        var social = base.Plan;
        Plan = new(social.Abilities.ToArray(), X, Z, social.BeforeLoadout.ToArray(), social.AfterLoadout.ToArray(),
            social.AfterHelpers.Concat(new[] { $"""
                UPDATE characters SET z={Z},cash=1000 WHERE handle='{Target}';
                UPDATE characters SET z={Z} WHERE handle='QAFriend';
                """, Scenarios.FactionDepartureFaultSql(PeerCrew) }).ToArray(),
            environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
            readyMessages: new[] {
                "CSR leads QACrew at rank 1. QAOther captains QAPeerCrew at rank 5 in QAFaction. The open NAV stands are CSR (17000,495,6400), QAOther (17220,495,6400), and QAFriend (17120,495,6400). No teleport, selection or dev command.",
                "Open the people icon above chat, then Faction > More. Right-click the QAPeerCrew row and choose Remove. Accept the confirmation if shown. A disclosed disposable SQL CHECK rejects the target member write at QAOther Info 1000. Expect [SOCIAL] Faction ejection rolled back, a 74 refusal, 'The roster was kept.' in chat and both crews still present.",
                """Send {"actor":"QAOther","action":"faction-ejection-retry"} and wait for the response 'QAOther deposited 1 Info (999 remains)'. This releases the fixture predicate.""",
                "In Faction > More, remove the same QAPeerCrew again. Expect 73, precisely one crew removed, and QACrew still rank 1. Reopen the roster after relog."
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
        var captain = context.Helpers[Target];
        Check.Equal(1000L, await db.LongAsync($"SELECT cash FROM characters WHERE charId={captain.CharId}"),
            "The target captain's wallet matches the fault predicate");
        Check.Equal(1L, await db.LongAsync("SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS " +
            $"WHERE CONSTRAINT_SCHEMA=DATABASE() AND TABLE_NAME='characters' AND CONSTRAINT_NAME='{Scenarios.FactionDepartureFault}'"),
            "The disclosed affiliation fault is installed");
        if (context.StockProbe is not { } leader) return Array.Empty<byte>();
        await leader.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, leader);
        await Scenarios.ProveAtomicFactionEjectionAsync(leader, captain, db, PeerCrew, FactionId);
        return Array.Empty<byte>();
    }

    internal static async Task<string> ReleaseFaultAsync(HeadlessPlayer actor)
    {
        if (actor.Handle != Target) throw new ArgumentException("Use QAOther for the retry deposit.");
        await Scenarios.DepositOneCrewInfoAsync(actor);
        return "QAOther deposited 1 Info (999 remains)";
    }
}
