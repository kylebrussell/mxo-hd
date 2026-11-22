using System.Globalization;
using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class StarterHackerPathogenistRadiusMode : NativeQaMode
{
    private const int Level = StarterHackerPassiveFixture.Level;
    private const int X = StarterHackerPassiveFixture.X, Y = StarterHackerPassiveFixture.Y, Z = StarterHackerPassiveFixture.Z;
    private const ushort Pathogenist = 67, Upgrade = 1085;
    private const string Far = "QAFar";
    private static readonly (string Handle, int Id, int Offset)[] Stands =
    {
        ("QAEnemy", 6114, 300), ("QAFriend", 6111, 320), ("QAOther", 6113, 840), (Far, 6112, 881)
    };

    // PR512 and PR556b captured the three area requests. Substitute only the live view and spawn.
    private static readonly Sample[] Samples =
    {
        new(97, 3, 12, 58, 342, -14, 27, "80b96100060004000000000000000000000000000000000000000000585cb70001000000086b0000000000"),
        new(38, 8, 14, 43, 102, 0, 3 * 1.01f, "80b92600060004000000000000000000000000000000000000000000585cc60001000000086b0000000000"),
        new(47, 3, 32, 58, 85, 0, 6, "80b92f00060004000000000000000000000000000000000000000000585cc60001000000086b0000000000")
    };

    // Setup only: the PR368 stock Actions result identifies 1085, but has no raw request.
    // Use the captured 1078 self-upgrade layout, replacing only the ability ID.
    private const string UpgradeSetup = "80b93d04000000000000000000000000000000000000000000000000d557377c20000000086b0000000000";
    private static readonly ushort[] Loadout = StarterHackerPassiveFixture.Loadout(
        Pathogenist, 64, 98, Samples[0].Id, Samples[1].Id, Samples[2].Id);
    private static readonly int Memory = Loadout.Sum(AbilityMemory.Cost);

    private sealed record Sample(ushort Id, int Radius, int Cost, float Reuse, ushort Stat, int Value, float Seconds, string Request);

    public override string Name => "ability-parity-starter-hacker-pathogenist-radius";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(),
        new[] { StarterHackerPassiveFixture.CapRanks },
        new[] { StarterHackerPassiveFixture.PutActor(X + Stands[0].Offset),
            "DELETE FROM char_abilities WHERE char_id IN (6111,6112,6113,6114)" }
            .Concat(Stands.Select(stand => StarterHackerPassiveFixture.PutHelper(stand.Id, X + stand.Offset, 2, 3000, 3000))).ToArray(),
        DevCommands: true, hotbarAbilities: new[] { StarterHackerPassiveFixture.Style, Samples[0].Id, Samples[1].Id, Samples[2].Id, Upgrade },
        testMobFixture: "martial-player-controls.csv", environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1",
            ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            ["MXO_FREECAST_VIRAL_QA"] = "1", ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766",
            ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0", [AreaTargetDiagnostics.Variable] = "1"
        },
        extraQaMetadata: new() { ["ScopedStockRows"] = "38,47,97,1085", ["MemoryUsed"] = Memory, ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            $"Level {Level}; legal {Memory}/{AbilityMemory.ForLevel(Level)} memory. Stay at outdoor CSR ({X},{Y},{Z}) on floor 400. The four opposing helpers are QAEnemy at 3.00 m, QAFriend at 3.20 m, QAOther at 8.40 m and QAFar at 8.81 m.",
            "Choose page 2 slot 1 Hacker Style and /target QAFar. Keep QAFar selected throughout. Press slot 2 UI Lag and slot 3 Area Disruption. Wait for the eight-second immunity to clear before slot 4 Freeze Area. At base, UI Lag and Freeze reach only QAEnemy, and Area Disruption reaches QAEnemy and QAFriend.",
            "Press slot 5 Pathogenist Upgrade. If the pre-seeded slot is unavailable, double-click Pathogenist Upgrade in stock Actions. Preserve the first 1085 client request. Expect [MODIFIER] 1085/177 +10, 1085/342 +4 and 1085/564 +7 on CSR.",
            "After all three reuse timers clear, repeat slots 2 to 4 in the same order, waiting for stun immunity before Freeze again. Expect [HOSTILE][AREA-QA] radius=3.3000 for UI Lag and Freeze, which now also reach QAFriend, and radius=8.8000 for Area Disruption, which now also reaches QAOther. QAFar stays excluded by radius, with no contest. All centres stay on CSR.",
            "Expect [MODIFIER] UI Lag 342 -14 for 27 s, Area Disruption state 102 for 3.03 s and immunity for 8 s, Freeze Area state 85 for 6 s and immunity for 12 s. Each round costs 12/14/32 IS, and 1085 costs 20 IS between rounds. Expect 700 to 642 to 622 to 564 with no interlock or maximum-vital change."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        StarterHackerPassiveFixture.CheckNav(context, X + Stands[^1].Offset);
        foreach (var stand in Stands[..^1])
        {
            int x = X + stand.Offset;
            Check.That(NavMesh.Locate(1, x, Y, Z, out var nav) && !nav.indoors &&
                Math.Abs(nav.floorY + 95 - Y) < 1 && NavMesh.HasSightPath(1, X, Y, Z, x, Y, Z),
                $"Outdoor NAV and clear sight for {stand.Handle} at ({x},{Y},{Z})");
        }

        var server = context.ActualServer!;
        foreach (var stand in Stands)
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, stand.Handle, stand.Handle, stockCharacterLoad: true);
            context.Helpers.Add(helper.Handle, helper);
            await PvpOpponentsFixture.EnterAsync(helper);
            NativeQaReadiness.RequireNoAmbientAggro(server, helper);
        }

        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        var archive = StarterHackerPassiveFixture.VerifyProfile(actor, Loadout);
        Check.That(AbilityPrerequisites.Current.Closure(Upgrade).All(need =>
            archive.TryGetValue(need.Key, out var row) && row.Level >= need.Value), "The granted upgrade has every required rank");
        uint handle = await StarterHackerPassiveFixture.StartAsync(actor, context.Helpers[Far], server, pvp: true);
        foreach (var helper in context.Helpers.Values)
            await actor.WaitForViewNamedAsync(helper.Handle);
        await StockAbilityProfile.ObserveAsync(actor, 0, StockAbilityProfile.GrantRecord(Upgrade, false), "Pathogenist grants 1085 in world");

        int strength = 700;
        var baseline = new Dictionary<ushort, int>();
        foreach (var sample in Samples)
            baseline.Add(sample.Id, await CastAsync(sample, expanded: false));

        int upgradeAt = actor.World.RpcSnapshot().Count;
        actor.SendRpc(UpgradeSetup);
        await StarterHackerPassiveFixture.RecordsAsync(actor, upgradeAt, Upgrade, new (ushort, int)[] { (177, 10), (342, 4), (564, 7) });
        await EvidenceRetestWire.TimerAsync(actor, upgradeAt, Upgrade, AbilityCooldowns.OwnTimer, 30);
        strength -= 20;

        foreach (var sample in Samples)
        {
            byte[] end = AbilityCooldowns.BuildTimer(sample.Id, AbilityCooldowns.OwnTimer, sample.Reuse, start: false);
            await actor.WaitUntilAsync(() => actor.World.RpcSnapshot().Skip(baseline[sample.Id])
                .Any(frame => frame.Bytes.AsSpan().SequenceEqual(end)), $"The original {sample.Id} reuse ends", TimeSpan.FromSeconds(62));
            await CastAsync(sample, expanded: true);
        }
        Check.Equal(0, actor.World.Problems.Count + context.Helpers.Values.Sum(helper => helper.World.Problems.Count), "Radius witness framing");
        return Array.Empty<byte>();

        async Task<int> CastAsync(Sample sample, bool expanded)
        {
            int from = actor.World.RpcSnapshot().Count;
            int viewFrom = actor.World.ViewSnapshot().Count;
            int logFrom = server.DebugLog().Length;
            var witnesses = context.Helpers.Values.ToDictionary(helper => helper.Handle,
                helper => (Player: helper, Cursor: helper.World.RpcSnapshot().Count));
            actor.SendRpc(StarterHackerControlTargetMode.WithHandle(sample.Request, handle));
            await EvidenceRetestWire.CastBarAsync(actor, sample.Id, 2);
            await EvidenceRetestWire.TimerAsync(actor, from, sample.Id, AbilityCooldowns.OwnTimer, sample.Reuse);
            strength -= sample.Cost;
            await actor.WaitUntilAsync(() => actor.World.ViewSnapshot().Skip(viewFrom)
                .SelectMany(AbilityParityCoderSweepMode.InnerStrengthUpdates).Contains((ushort)strength),
                $"{sample.Id} spends {sample.Cost} IS and leaves {strength}", HeadlessPlayer.DefaultTimeout);

            double radius = expanded ? sample.Radius * 1.1 : sample.Radius;
            foreach (var stand in Stands)
            {
                var witness = witnesses[stand.Handle];
                bool included = stand.Offset <= radius * 100;
                string reason = included ? "included" : "radius";
                string geometry = string.Format(CultureInfo.InvariantCulture,
                    "centre={0},{1},{2} radius={3:F4} distance={4:F4}", X, Y, Z, radius, stand.Offset / 100.0);
                string log = server.DebugLog()[logFrom..];
                Check.That(log.Split('\n').Any(line => line.Contains($"candidate={stand.Handle} ", StringComparison.Ordinal) &&
                        line.Contains(geometry, StringComparison.Ordinal) && line.TrimEnd().EndsWith("reason=" + reason, StringComparison.Ordinal)),
                    $"{sample.Id} {stand.Handle} uses the current caster-centred radius {radius:F1} m");
                if (included)
                    await StockAbilityProfile.ObserveAsync(witness.Player, witness.Cursor,
                        frame => StockAbilityProfile.Bonus(frame, sample.Id, sample.Stat, sample.Value, sample.Seconds, false),
                        $"Area cast {sample.Id} reaches {stand.Handle}");
                else
                    Check.That(!log.Split('\n').Any(line => line.Contains($"[FREECAST-VIRAL] ability={sample.Id} ", StringComparison.Ordinal) &&
                        line.Contains($"target={stand.Handle} ", StringComparison.Ordinal)), $"{stand.Handle} is excluded before rolling");
            }

            if (sample.Id == 38)
            {
                var witness = witnesses["QAEnemy"];
                await StockAbilityProfile.ObserveAsync(witness.Player, witness.Cursor,
                    frame => StockAbilityProfile.Bonus(frame, TempMods.ControlImmunityTimerAbility, 681, 0, 8, true),
                    "The independent eight-second immunity clears before Freeze Area");
            }
            return from;
        }
    }
}
