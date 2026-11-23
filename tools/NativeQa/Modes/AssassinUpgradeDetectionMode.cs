using hds;
using HeadlessClient;
using static NativeQa.StockAbilityProfile;

namespace NativeQa.Modes;

public sealed class AssassinUpgradeDetectionMode : NativeQaMode
{
    private const ushort SneakParent = 151, Sneak = Stealth.SneakGrantAbility;
    private const ushort Assassin = 1122, MasterAssassin = 1123, Countermeasures = 293;
    private const int X = 17000, Y = 495, CasterZ = 6400, AwayZ = 8300, MidZ = 7200, NearZ = 6650;
    private const int Level = 50, SneakRank = 15, Awareness = 13, Roll = 35;
    private static readonly ushort[] Loadout = WithPrerequisites(2, 17, SneakParent, 155, 285, 389)
        .Where(id => id is not (Sneak or Assassin or MasterAssassin)).Order().ToArray();
    private static readonly ushort[] ObserverLoadout = WithPrerequisites(2, 17, Countermeasures)
        .Where(id => id != Sneak).Order().ToArray();
    private static readonly int Memory = Loadout.Sum(AbilityMemory.Cost);

    public override string Name => "ability-parity-assassin-upgrade-detection";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, CasterZ,
        Array.Empty<string>(), Array.Empty<string>(), new[]
        {
            $"UPDATE characters SET x={X},y={Y},z={CasterZ},rotation=0,alignment=1,pvpflag=0,level={Level}," +
                $"exp={PlayerProgression.ExperienceForLevel(Level)},innerStrC=400,innerStrM=400 WHERE charId=110",
            "UPDATE char_abilities SET level=50 WHERE char_id=110",
            $"UPDATE char_abilities SET level={SneakRank} WHERE char_id=110 AND ability_id={AbilityMessages.AbilityGameObject(SneakParent)}",
            $"UPDATE characters SET x={X},y={Y},z={AwayZ},rotation=128,alignment=2,pvpflag=0,level={Level}," +
                $"exp={PlayerProgression.ExperienceForLevel(Level)},innerStrC=400,innerStrM=400 WHERE charId=6114",
            "DELETE FROM char_abilities WHERE char_id=6114",
            "INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES " +
                string.Join(",", ObserverLoadout.Select((id, i) => $"(6114,{400 + i},{AbilityMessages.AbilityGameObject(id)},50,1)"))
        }, DevCommands: true, hotbarAbilities: new[] { CombatStyles.SpyStyle, Sneak, Assassin, MasterAssassin },
        environmentOverrides: new()
        {
            ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1",
            ["MXO_ASSASSIN_DETECTION_QA"] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "1122,1123", ["NativeCloseableRows"] = 2, ["NativeCloseableIds"] = "1122,1123" },
        readyMessages: new[]
        {
            $"Level {Level}, {Memory}/100 memory with the full Assassin, Master Assassin and Sneak prerequisites. CSR stays on outdoor ({X},{Y},{CasterZ}) facing north. " +
                $"QAEnemy faces south from ({X},{Y},{AwayZ}), moves to MID ({X},{Y},{MidZ}) or NEAR ({X},{Y},{NearZ}). " +
                "No teleport or NPC selection is required. If CSR moves off the stand, return with ?tp 17000 495 6400 4e2 12 4 - 00. All buttons are self-casts. To see QAEnemy before beginning, strafe once and turn north or use /target QAEnemy. Stay still during the contests.",
            $"The disposable aid fixes only this pair's ordinary Sneak observer roll at {Roll}. Every low check must log SA {Awareness}, noise 0, roll {Roll}; compare the real detection and view updates.",
            """Press page 2 slots 1 (Spy) and 2 (Sneak). Once Hidden, run {"actor":"QAEnemy","action":"assassin-watch","phase":"mid"}. Expect SL 15, QAEnemy sees CSR, Sneak off (revealed), normal standing pose and CSR visible again.""",
            """Run {"actor":"QAEnemy","action":"assassin-watch","phase":"away"}. Press slot 3 (1122) then slot 2 (Sneak). At phase mid expect SL 20 and a miss, with Sneak still active. At phase near expect SL 20 and reveal. Keep the +5 upgrade on. Save stat209 +5 and its locator.""",
            """Run phase away again. Press slot 4 (1123), replacing 1122. Require the matching 1122 stat209 +5 removal and 1123 stat209 +8 add. Press Sneak. At phase near expect SL 23 and a miss. Then run phase reveal. QAEnemy's stock Stealth Countermeasures adds +80 awareness to reach SA 93, and the same point and same roll must now reveal CSR. Sneak off and standing return, with the +8 upgrade still active.""",
            "Press slot 4 again once visible. Verify the matching 1123 stat209 +8 removal. If either shortcut does nothing, use its exact entry under Actions > Abilities. Save both full stock upgrade frames and the debug and helper-view evidence."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (int z in new[] { CasterZ, AwayZ, MidZ, NearZ })
            Check.That(NavMesh.Locate(1, X, Y, z, out var stand) && !stand.indoors && Math.Abs(stand.floorY + 95 - Y) < 1 &&
                NavMesh.HasSightPath(1, X, Y, CasterZ, X, Y, z), $"Verified outdoor stand and sight ({X},{Y},{z})");
        Check.That(AwayZ - CasterZ > Stealth.DetectionRangeUnits(SneakRank) && MidZ - CasterZ < Stealth.DetectionRangeUnits(SneakRank),
            "Activation starts outside the live Sneak range");
        Check.That(Memory <= AbilityMemory.ForLevel(Level) && ObserverLoadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level),
            "Both complete ranked profiles fit memory");
        Check.That(AbilityProgression.GrantsOf(285).Contains(Assassin) && AbilityProgression.GrantsOf(389).Contains(MasterAssassin),
            "Both upgrade hotbar entries are ordinary loaded-parent grants");
        Check.That(Stealth.SneakTest(Awareness, 15, 0, MidZ - CasterZ, Roll) &&
            !Stealth.SneakTest(Awareness, 20, 0, MidZ - CasterZ, Roll) &&
            Stealth.SneakTest(Awareness, 20, 0, NearZ - CasterZ, Roll) &&
            !Stealth.SneakTest(Awareness, 23, 0, NearZ - CasterZ, Roll) &&
            Stealth.SneakTest(Awareness + 80, 23, 0, NearZ - CasterZ, Roll), "One roll crosses the two independent shipped thresholds");

        var server = context.ActualServer!;
        var observer = await HeadlessPlayer.LoginAsync(context.Address, "QAEnemy", "QAEnemy", stockCharacterLoad: true);
        context.Helpers.Add(observer.Handle, observer);
        await PvpOpponentsFixture.EnterAsync(observer);
        NativeQaReadiness.RequireNoAmbientAggro(server, observer);
        Check.That(EngageFoesFixture.Position(observer.World.CreationSnapshot().Last(v => v.ViewId == 2))
            .SequenceEqual(new double[] { X, Y, AwayZ }), "Observer starts on the verified away stand");
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        var loaded = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor), row => row.Level);
        Check.That(Loadout.All(id => loaded.TryGetValue(id, out ushort actual) && (id != SneakParent || actual == SneakRank) &&
            AbilityPrerequisites.Current.Closure(id).All(need => loaded.TryGetValue(need.Key, out ushort rank) && rank >= need.Value)),
            "Margin sends a complete legal stock ranked prerequisite profile");
        await PvpOpponentsFixture.EnterAsync(actor);
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        await ObserveAsync(actor, 0, GrantRecord(Sneak, false, SneakRank), "Initial ranked Sneak grant");
        await ObserveAsync(actor, 0, GrantRecord(Assassin, false), "Initial Assassin grant");
        await ObserveAsync(actor, 0, GrantRecord(MasterAssassin, false), "Initial Master Assassin grant");

        var view = await observer.WaitForViewNamedAsync(actor.Handle);
        int viewCursor = observer.World.ViewSnapshot().Count, creationCursor = observer.World.CreationSnapshot().Count;
        actor.SendRpc(AwakenedStockStealthSpeedMode.SpyUse);
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 58 04 00 00");
        int cast = actor.World.RpcSnapshot().Count, poseCursor = actor.World.ViewSnapshot().Count;
        actor.SendRpc(AwakenedStockStealthSpeedMode.SneakUse);
        await ObserveAsync(actor, cast, frame => Bonus(frame, Sneak, 209, SneakRank, 0, false), "Stock Sneak rank 15 active");
        byte[] deleted = { 1, 1, 0, (byte)view.ViewId, (byte)(view.ViewId >> 8) };
        await observer.WaitUntilAsync(() => observer.World.ViewSnapshot().Skip(viewCursor)
            .Any(list => BytePattern.IndexOf(list, deleted, 0) >= 0), "Observer drops the hidden actor view", HeadlessPlayer.DefaultTimeout);
        await EvidenceRetestWire.ViewSinceAsync(actor, poseCursor, 2, "01 28 ?? 40 09 00 00 10");
        poseCursor = actor.World.ViewSnapshot().Count;
        string baseline = await WatchAsync(observer, server, "mid");
        Check.That(baseline.Contains("SA 13 SL 15 noise 0 roll 35", StringComparison.Ordinal) &&
            baseline.Contains("QAEnemy sees CSR_Tester", StringComparison.Ordinal), "Measured baseline reveals at identical fixture inputs");
        await ObserveAsync(actor, cast, frame => frame.Bytes.AsSpan().SequenceEqual(AbilityBuffs.BuildExclusive(Sneak, false)), "Sneak ends on genuine reveal");
        await ObserveAsync(actor, cast, frame => Bonus(frame, Sneak, 209, SneakRank, 0, true), "Sneak rank 15 removes on reveal");
        await EvidenceRetestWire.ViewSinceAsync(actor, poseCursor, 2, "01 28 ?? 40 00 00 00 10");
        await observer.WaitUntilAsync(() => observer.World.CreationSnapshot().Skip(creationCursor).Any(creation => creation.Name == actor.Handle),
            "The same hostile helper regains the revealed player view", HeadlessPlayer.DefaultTimeout);
        await WatchAsync(observer, server, "away");
        cast = actor.World.RpcSnapshot().Count;
        actor.SendRpc(AwakenedStockStealthSpeedMode.SneakUse);
        await ObserveAsync(actor, cast, frame => Bonus(frame, Sneak, 209, SneakRank, 0, false), "Second ordinary stock Sneak");
        string high = await WatchAsync(observer, server, "reveal");
        Check.That(high.Contains("SA 93 SL 15 noise 0 roll 35", StringComparison.Ordinal) &&
            high.Contains("QAEnemy sees CSR_Tester", StringComparison.Ordinal), "Stock Countermeasures increases actual observation awareness");
        await ObserveAsync(actor, cast, frame => frame.Bytes.AsSpan().SequenceEqual(AbilityBuffs.BuildExclusive(Sneak, false)), "High-awareness reveal ends Sneak");
        Check.Equal(0, observer.World.Problems.Count + actor.World.Problems.Count, "Assassin visibility packet framing");
        Console.WriteLine("ASSASSIN DETECTION BOOTSTRAP PASS: legal grants, outdoor geometry, measured stock baseline and Countermeasures reveal with real observer view recreation. Upgrade requests remain native.");
        return Array.Empty<byte>();
    }

    private static IEnumerable<ushort> WithPrerequisites(params ushort[] roots) =>
        roots.SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys.Append(id)).Distinct();

    internal static async Task<string> WatchAsync(HeadlessPlayer observer, ServerInstance server, string phase)
    {
        if (observer.Handle != "QAEnemy")
            throw new InvalidOperationException("Use the seeded QAEnemy observer.");
        int z = phase switch
        {
            "away" => AwayZ,
            "mid" => MidZ,
            "near" or "reveal" => NearZ,
            _ => throw new InvalidOperationException("Use phase away, mid, near or reveal.")
        };
        int restoredFrom = observer.World.CreationSnapshot().Count;
        int logCursor = server.DebugLog().Length;
        if (phase == "reveal")
        {
            int cast = observer.World.RpcSnapshot().Count;
            observer.SendRpc(StarterStealthObserverMode.StockCountermeasures);
            await ObserveAsync(observer, cast, frame => Bonus(frame, Countermeasures, 204, 80, 30, false),
                "Countermeasures adds +80 observation awareness");
        }

        // Retained stock self-position layout. Only the fixture coordinate changes.
        var position = new[] { (float)X, (float)Y, (float)z }.SelectMany(BitConverter.GetBytes);
        observer.World.SendViewList(Convert.FromHexString("02000108").Concat(position).Concat(new byte[] { 0, 0 }).ToArray());
        observer.SkipReceived();
        observer.SendRpc("81 48");
        await observer.WaitForRpcAsync("81 49");
        if (phase == "away") return $"QAEnemy on away stand ({X},{Y},{z}).";

        string checkpoint = $"QAEnemy vs CSR_Tester: {(z - CasterZ) / 100.0:0.0} m";
        string trace = "";
        string outcome = "";
        await observer.WaitUntilAsync(() =>
        {
            var lines = server.DebugLog()[logCursor..].Split('\n', StringSplitOptions.TrimEntries);
            trace = lines.LastOrDefault(line => line.Contains("[ASSASSIN DETECTION QA] " + checkpoint, StringComparison.Ordinal) &&
                (phase != "reveal" || line.Contains("SA 93", StringComparison.Ordinal))) ?? "";
            if (trace.Length == 0) return false;
            outcome = lines.LastOrDefault(line => line.Contains("[STEALTH] QAEnemy sees CSR_Tester", StringComparison.Ordinal) ||
                line.Contains("[STEALTH] detect " + checkpoint, StringComparison.Ordinal) && line.Contains("missed", StringComparison.Ordinal)) ?? "";
            return outcome.Length != 0;
        }, $"QAEnemy completes the {phase} Sneak contest", TimeSpan.FromSeconds(12));
        if (outcome.Contains("QAEnemy sees CSR_Tester", StringComparison.Ordinal))
        {
            ushort restoredView = 0;
            await observer.WaitUntilAsync(() =>
            {
                var creation = observer.World.CreationSnapshot().Skip(restoredFrom).FirstOrDefault(view => view.Name == "CSR_Tester");
                restoredView = creation?.ViewId ?? 0;
                return restoredView != 0;
            }, "QAEnemy regains a rendered CSR view", HeadlessPlayer.DefaultTimeout);
            return trace + " | " + outcome + $" | QAEnemy regains CSR view {restoredView}.";
        }
        return trace + " | " + outcome;
    }
}
