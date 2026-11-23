using System.Globalization;
using hds;

namespace HeadlessClient;

public static partial class Scenarios
{
    private static (ushort Ability, ushort Level)[] ReactiveCodeProfile(params ushort[] roots) =>
        roots.Append((ushort)2).Append((ushort)17)
            .SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys.Append(id))
            .Distinct().Order().Select(id => (id, (ushort)50)).ToArray();

    private static IReadOnlyDictionary<string, string> ReactiveCodeAreaEnvironment(int damageDraw, bool hits) =>
        new Dictionary<string, string>(HealthCallbackNpcs)
        {
            ["MXO_CALLBACK_QA_ROLL15"] = damageDraw.ToString(CultureInfo.InvariantCulture),
            ["MXO_FREECAST_VIRAL_QA"] = "1", ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = hits ? "32766" : "0",
            ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = hits ? "15636" : "32766",
            ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            ["MXO_DESPERATION_QA_HOLD_HEALTH"] = "1", [AreaTargetDiagnostics.Variable] = "1"
        };

    private static async Task ReactiveCodeNeutralArea(ScenarioContext context, int damage, bool hits)
    {
        const int x = 21925, y = 1095, z = 16100;
        const ushort parent = 941, child = 942, emptyMind = 8, maximum = 1000;
        // Complete stock 941 input from PR545. Empty Mind uses the PR382 layout with its self target handle cleared.
        const string stockReactiveCode = "80b9ad03000000000000000000000000000000000000000000000000585cbc0001000000086b0000000000";
        const string stockEmptyMind = "80b90800000000000000000000000000000000000000000000000000a8fb7fbf00000000086b0000000000";

        // Match the stock test profile, including the containing discipline. 941's stored level-99 gate is outside this child check.
        var ownerProfile = ReactiveCodeProfile(parent, 943, 945);
        Check.That(ownerProfile.Sum(row => AbilityMemory.Cost(row.Ability)) <= AbilityMemory.ForLevel(50),
            "The PR545 profile fits memory");
        var owner = await AddPlayer("ReactiveOwner", x, 1, ownerProfile);
        var edge = await AddPlayer("ReactiveEdge", x + 300, 2, ReactiveCodeProfile(emptyMind));
        var outside = await AddPlayer("ReactiveOutside", x + 301, 2);
        var blocked = await AddPlayer("ReactiveBlocked", x - 250, 2);
        var ally = await AddPlayer("ReactiveAlly", x + 100, 1);
        var targets = new[] { edge, outside, blocked, ally };
        var players = targets.Prepend(owner).ToArray();

        SectorProperties.Load(Path.Combine(context.RepositoryRoot, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(context.RepositoryRoot, "data", "navmesh"));
        foreach (int standX in new[] { x, x + 300, x + 301, x - 250, x + 100 })
            Check.That(NavMesh.Locate(1, standX, y, z, out var stand) && !stand.indoors && stand.floorY + 95 == y,
                $"Reactive Code stand ({standX},{y},{z}) is on outdoor NAV");
        Check.That(NavMesh.HasSightPath(1, x, y, z, x + 300, y, z, rejectUnmappedHeightDifference: false),
            "The exact radius boundary is visible");
        Check.That(!NavMesh.HasSightPath(1, x, y, z, x - 250, y, z, rejectUnmappedHeightDifference: false),
            "The 2.50 m hostile is behind the mapped obstruction");

        await context.Database.ExecuteAsync($"UPDATE characters SET pvpflag=1 WHERE charId IN ({string.Join(",", players.Select(p => p.CharId))})");
        foreach (var player in players) await PvpOpponentsFixture.EnterAsync(player);
        foreach (var target in targets) await owner.WaitForViewNamedAsync(target.Handle);

        edge.SendRpc(stockEmptyMind);
        var resist = await edge.WaitForRpcAsync(ModPattern(emptyMind, 157));
        Check.Equal(75, BitConverter.ToInt32(resist.Bytes, 13), "The boundary target has live +75 Viral Resistance");
        Check.Equal(20f, BitConverter.ToSingle(resist.Bytes, 18), "The resistance still covers the callback");

        Cast(owner, child);
        await owner.WaitForTextAsync("Use the parent ability, not its internal effect.");
        owner.SendRpc(stockReactiveCode);
        await owner.WaitForRpcAsync(WireFormat.ToHex(AbilityBuffs.BuildExclusive(parent, true)));

        int debug = context.Server.DebugLog().Length;
        var healthCursors = targets.ToDictionary(player => player, player => player.World.ViewSnapshot().Count);
        int hitsBefore = edge.World.RpcSnapshot().Count;
        await SetProcHealth(owner, 500, 500);
        await owner.WaitUntilAsync(() => context.Server.DebugLog()[debug..].Contains("[HEALTH-CALLBACK] 941 on ReactiveOwner"),
            "The live parent processes the health change", TimeSpan.FromSeconds(8));

        foreach (var target in targets)
        {
            target.SkipReceived();
            target.SendRpc("81 48");
            await target.WaitForRpcAsync("81 49");
        }

        string trace = context.Server.DebugLog()[debug..];
        CheckArea(edge, "3.0000", "included");
        CheckArea(outside, "3.0100", "radius");
        CheckArea(blocked, "2.5000", "sight");
        CheckArea(ally, "1.0000", "pvp-NotOpponent");
        string[] contests = trace.Split('\n').Where(line => line.Contains("[FREECAST-VIRAL] ability=942 ")).ToArray();
        Check.Equal(1, contests.Length, "Only the eligible hostile enters the child's contest");
        Check.That(contests[0].Contains($"target={edge.Handle} ") && contests[0].Contains($"hit={hits} qa=True"),
            "The expected forced viral draw applies to the eligible recipient");

        var damageRpc = AbilityMessages.Damage(false, child, owner.Handle, edge.Handle, damage, 0);
        Check.Equal(hits ? 1 : 0, edge.World.RpcSnapshot().Skip(hitsBefore).Count(frame => frame.Bytes.AsSpan().SequenceEqual(damageRpc)),
            "A landed neutral hit retains the sampled amount and zero resistance despite live Empty Mind");
        foreach (var target in targets)
        {
            ushort[] changes = PvpOpponentsFixture.HealthHistory(target.World.ViewSnapshot(), healthCursors[target], 2, self: true);
            if (hits && target == edge)
                Check.That(changes.SequenceEqual(new[] { (ushort)(maximum - damage) }), "The in-range hostile takes the exact neutral hit once");
            else
                Check.Equal(0, changes.Length, $"{target.Handle} receives no health change");
        }
        Check.That(players.All(player => player.World.Problems.Count == 0), "Reactive Code transport framing");

        void CheckArea(HeadlessPlayer target, string distance, string reason) => Check.That(trace.Split('\n').Any(line =>
            line.Contains("[AREA-QA]") && line.Contains($"candidate={target.Handle} ") &&
            line.Contains($"centre={x},{y},{z} radius=3.0000 distance={distance} ") && line.TrimEnd().EndsWith("reason=" + reason)),
            $"{target.Handle} takes the expected owner-centred decision at {distance} m");

        Task<HeadlessPlayer> AddPlayer(string name, int standX, int organization, (ushort Ability, ushort Level)[]? profile = null) =>
            context.AddPlayerAsync(new CharacterSeed(name)
            {
                Level = 50, Experience = (int)PlayerProgression.ExperienceForLevel(50), X = standX, Y = y, Z = z,
                Health = maximum, MaxHealth = maximum, InnerStrength = 1000, MaxInnerStrength = 1000,
                Organization = organization, OwnedLoadedAbilities = profile ?? ReactiveCodeProfile()
            });
    }
}
