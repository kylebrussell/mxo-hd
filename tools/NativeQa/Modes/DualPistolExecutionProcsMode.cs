using System.Diagnostics;
using hds;
using HeadlessClient;
using MxoHd.PacketResearch;
using static NativeQa.Modes.StarterConcealedShotTargetingMode;
using static NativeQa.Modes.StarterHackerControlTargetMode;
using static NativeQa.StockAbilityProfile;

namespace NativeQa.Modes;

public sealed class DualPistolExecutionProcsMode : NativeQaMode
{
    private const ushort Execution = DualPistolExecutionQa.AbilityId, Handguns = CombatStyles.Handguns;
    private const int Level = 46, X = 21550, EnemyX = 21750, Y = 1095, Z = 16150;
    private const string StockExecution = "80b9f601040002000000000000000000000000000000000000000000f601000000006b0000086b0000000000";
    private static readonly ushort[] Loadout = DisablingShotMode.Closure(2, 17, Handguns, Execution);

    public override string Name => "ability-parity-dual-pistol-execution-procs";

    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(),
        new[]
        {
            CombatFixtureLoadout.SeedWeaponsFor(CombatFixtureWeapons.DualHandguns),
            $"INSERT INTO inventory(charId,goid,slot,count,purity) VALUES (110,{CombatFixtureWeapons.DualHandguns},{CombatFixtureWeapons.HandSlot},1,0)"
        },
        new[]
        {
            PvpOpponentsFixture.NativeSql,
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y={Y},z={Z}," +
                "healthC=12000,healthM=12000,innerStrC=900,innerStrM=900 WHERE charId IN (110,6114)",
            $"UPDATE characters SET x={X},rotation={MobAggro.YawTowards(X, Z, EnemyX, Z)} WHERE charId=110",
            $"UPDATE characters SET x={EnemyX},rotation={MobAggro.YawTowards(EnemyX, Z, X, Z)} WHERE charId=6114",
            $"UPDATE char_abilities SET level={Level} WHERE char_id=110"
        },
        DevCommands: true, hotbarAbilities: new[] { Handguns, Execution },
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0",
            ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1",
            [DualPistolExecutionQa.Mode] = "1"
        },
        extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "502",
            ["NativeCloseableRows"] = 1,
            ["ProcDrawAid"] = "?pistolproc <both|1010|522|none|off>"
        },
        readyMessages: new[]
        {
            $"Level {Level}, {Loadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} memory, dual handguns 2496 equipped. CSR starts at verified outdoor HOME ({X},{Y},{Z}) facing QAEnemy ({EnemyX},{Y},{Z}). Page 2: 1=Handguns (129), 2=Dual Pistol Execution (502). Stay at rest; no tp needed. Select slot 1 (Handguns) and /target QAEnemy.",
            "Step 1 (Both independent 40% procs): Type ?pistolproc both, press slot 2. Expect 4 s cast, 35 IS, 53 dmg, and BOTH source502 8 s records on QAEnemy: stat1010 -31 (Ballistic Defense) AND stat522 -27 (Melee Accuracy), both removing cleanly at ~8 s.",
            "Step 2 (1010-only branch): Wait 10 s reuse, type ?pistolproc 1010, press slot 2. Expect 53 dmg and ONLY source502 stat1010 -31 (8 s) added and removed; no stat522 record.",
            "Step 3 (522-only branch): Wait 10 s reuse, type ?pistolproc 522, press slot 2. Expect 53 dmg and ONLY source502 stat522 -27 (8 s) added and removed; no stat1010 record.",
            "Step 4 (Miss/none branch): Wait 10 s reuse, type ?pistolproc none, press slot 2. Expect 53 dmg with ZERO source502 stat modifiers on QAEnemy. QA aid controls only the two independent 40% proc draws; cast, IS, damage and 8 s expiry are ordinary."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        var home = Stand(X, Z);
        _ = Stand(EnemyX, Z);
        Check.That(NavMesh.HasSightPath(1, X, Y, Z, EnemyX, Y, Z), "Verified clear outdoor stand for 502 procs");
        string[] words = home.Command.Split(' ');
        Console.WriteLine($"READY HOME: type '{string.Join(' ', words[..3])}', then ' {string.Join(' ', words[3..7])}', then ' {words[7]} {words[8]}'; press Enter once.");

        var server = context.ActualServer!;
        var enemy = await HeadlessPlayer.LoginAsync(context.Address, "QAEnemy", "QAEnemy", stockCharacterLoad: true);
        context.Helpers.Add("QAEnemy", enemy);
        await PvpOpponentsFixture.EnterAsync(enemy);
        NativeQaReadiness.RequireNoAmbientAggro(server, enemy);

        if (context.StockProbe is not { } caster) return Array.Empty<byte>();
        DisablingShotMode.CheckProfile(caster, Loadout, Level);
        await PvpOpponentsFixture.EnterAsync(caster);
        await CombatFixtureLoadout.VerifyWeaponAtLoginAsync(caster, CombatFixtureWeapons.DualHandguns);
        NativeQaReadiness.RequireNoAmbientAggro(server, caster);

        var enemyView = await caster.WaitForViewNamedAsync("QAEnemy");
        uint enemyHandle = DuelPackets.Handle(enemyView.ViewId, enemyView.SpawnId);

        caster.SendRpc(WithHandle(PistolSlideMode.StockHandguns, enemyHandle));
        await ObserveAsync(caster, 0, rpc => ManageBonusRecord.TryDecode(rpc.Bytes, out var record) &&
            record is { SourceId: Handguns, TargetId: 171, AddRemoveType: 0 }, "Retained Handguns selects dual-pistol style");

        // Exercise all 4 independent 40%/40% proc combinations on live QAEnemy.
        await RunBranchAsync("both", expect1010: true, expect522: true, waitReuse: true);
        await RunBranchAsync("1010", expect1010: true, expect522: false, waitReuse: true);
        await RunBranchAsync("522", expect1010: false, expect522: true, waitReuse: true);
        await RunBranchAsync("none", expect1010: false, expect522: false, waitReuse: false);

        Check.Equal(0, caster.World.Problems.Count + enemy.World.Problems.Count, "Clean stock framing for 502 procs");
        Console.WriteLine("DUAL PISTOL EXECUTION PROCS PASS: both, 1010-only, 522-only, and none verified with paired 8 s removals.");
        return Array.Empty<byte>();

        async Task RunBranchAsync(string preset, bool expect1010, bool expect522, bool waitReuse)
        {
            caster.SendChat($"?pistolproc {preset}");
            await caster.WaitForTextAsync($"Dual Pistol Execution 502 proc preset: {preset}");

            int casterRpc = caster.World.RpcSnapshot().Count;
            int enemyRpc = enemy.World.RpcSnapshot().Count;
            int logFrom = server.DebugLog().Length;
            caster.SendRpc(WithHandle(StockExecution, enemyHandle));
            await EvidenceRetestWire.CastBarAsync(caster, Execution, 4);
            await EvidenceRetestWire.TimerAsync(caster, casterRpc, Execution, AbilityCooldowns.OwnTimer, 10);
            await caster.WaitUntilAsync(() => server.DebugLog()[logFrom..].Contains(
                "PistolExecutionAbility lands on QAEnemy for 53", StringComparison.Ordinal),
                $"502 lands for 53 on branch {preset}", HeadlessPlayer.DefaultTimeout);

            RpcRecord? add1010 = null, add522 = null;
            if (expect1010)
            {
                add1010 = await ObserveAsync(enemy, enemyRpc,
                    f => StatRecord(f, Execution, 1010, -31, 8f, remove: false),
                    $"502 branch {preset} adds 1010 -31 for 8 s");
            }
            if (expect522)
            {
                add522 = await ObserveAsync(enemy, enemyRpc,
                    f => StatRecord(f, Execution, 522, -27, 8f, remove: false),
                    $"502 branch {preset} adds 522 -27 for 8 s");
            }
            long hitAt = Stopwatch.GetTimestamp();

            // Sync QAEnemy so any unexpected modifier would already be delivered.
            enemy.SendRpc("81 48");
            await enemy.WaitForRpcAsync("81 49", HeadlessPlayer.DefaultTimeout);
            bool saw1010 = enemy.World.RpcSnapshot().Skip(enemyRpc).Any(f => StatRecord(f, Execution, 1010, -31, 8f, remove: false));
            bool saw522 = enemy.World.RpcSnapshot().Skip(enemyRpc).Any(f => StatRecord(f, Execution, 522, -27, 8f, remove: false));
            Check.Equal(expect1010, saw1010, $"502 branch {preset} 1010 presence");
            Check.Equal(expect522, saw522, $"502 branch {preset} 522 presence");

            if (add1010 != null)
            {
                await ObserveAsync(enemy, enemyRpc,
                    f => StatRecord(f, Execution, 1010, -31, 8f, remove: true) && SameLocator(add1010, f),
                    $"502 branch {preset} removes 1010 -31 at 8 s");
            }
            if (add522 != null)
            {
                await ObserveAsync(enemy, enemyRpc,
                    f => StatRecord(f, Execution, 522, -27, 8f, remove: true) && SameLocator(add522, f),
                    $"502 branch {preset} removes 522 -27 at 8 s");
            }
            if (add1010 != null || add522 != null)
            {
                double elapsed = Stopwatch.GetElapsedTime(hitAt).TotalSeconds;
                Check.That(Math.Abs(elapsed - 8.0) < 2.0, $"502 branch {preset} expiry at {elapsed:F2}s");
            }
            if (waitReuse)
            {
                await ObserveAsync(caster, casterRpc,
                    f => BytePattern.Parse($"80 bd 01 f6 01 {AbilityCooldowns.OwnTimer:x2} 00 00 20 41 00").IsMatch(f.Bytes),
                    $"502 10 s own reuse ends after branch {preset}");
            }
        }
    }

    private static bool StatRecord(RpcRecord frame, ushort source, ushort stat, int value, float seconds, bool remove) =>
        ManageBonusRecord.TryDecode(frame.Bytes, out var r) && r!.Type == TempMods.KindStat &&
        r.SourceId == source && r.TargetId == stat && r.SignedValue == value &&
        Math.Abs(r.Duration - seconds) < 0.001 && r.AddRemoveType == (remove ? 1 : 0);

    private static bool SameLocator(RpcRecord add, RpcRecord remove) =>
        ManageBonusRecord.TryDecode(add.Bytes, out var a) && ManageBonusRecord.TryDecode(remove.Bytes, out var b) &&
        a!.Locator == b!.Locator;
}
