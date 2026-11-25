using hds;
using HeadlessClient;
using MxoHd.PacketResearch;
using static NativeQa.Modes.StarterConcealedShotTargetingMode;
using static NativeQa.Modes.StarterHackerControlTargetMode;
using static NativeQa.StockAbilityProfile;

namespace NativeQa.Modes;

public sealed class DualPistolExecutionTargetMode : NativeQaMode
{
    private const ushort Execution = 502, Handguns = CombatStyles.Handguns;
    private const int Level = 46, X = 21550, EnemyX = 21750, BlockX = 22350, FarX = 19600, Y = 1095, Z = 16150;
    private const int FriendX = 21650, FriendZ = 16250;
    private const string StockExecution = "80b9f601040002000000000000000000000000000000000000000000f601000000006b0000086b0000000000";
    private static readonly ushort[] Loadout = DisablingShotMode.Closure(2, 17, Handguns, Execution);

    public override string Name => "ability-parity-dual-pistol-execution-targets";
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
            $"UPDATE characters SET x={FriendX},y={Y},z={FriendZ},level={Level},alignment=1 WHERE charId=6111",
            $"UPDATE char_abilities SET level={Level} WHERE char_id=110"
        },
        DevCommands: true, hotbarAbilities: new[] { Handguns, Execution },
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1",
            ["MXO_PVP_MAX_SAFE_LEVEL"] = "15", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "502", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            $"Level {Level}, {Loadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} memory, dual handguns 2496 equipped. CSR ({X},{Y},{Z}) faces QAEnemy ({EnemyX},{Y},{Z}) on clear NAV. Page 2 Handguns / Dual Pistol Execution. Stay at rest, use /target on the two named players, and strafe once if a view needs refresh. No NPC or Free Fire selection.",
            "Select slot 1, /target QAFriend, press slot 2. Expect reason 24 or stock suppression. From printed BLOCK and FAR select /target QAEnemy and try slot 2 at each. Expect reason 9 and 34 or stock suppression. All three deny before cast, timer or debit. Each printed teleport is one chat line typed in chunks, then Enter.",
            "Return to HOME and /target QAEnemy. Press slot 2 without Close Combat. Expect a 4 s cast, 35 IS (900 to 865), 10 s own / 5 s family 689, and a direct 53-damage hit. Save the stock request and any naturally landed modifier add/removal. No RNG aid. The proc probabilities and original shipped magnitudes remain open."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        var home = Stand(X, Z);
        var block = Stand(BlockX, Z);
        var far = Stand(FarX, Z);
        _ = Stand(EnemyX, Z);
        _ = Stand(FriendX, FriendZ);
        Check.That(NavMesh.HasSightPath(1, X, Y, Z, EnemyX, Y, Z) &&
            NavMesh.HasSightPath(1, FarX, Y, Z, EnemyX, Y, Z) &&
            !NavMesh.HasSightPath(1, BlockX, Y, Z, EnemyX, Y, Z), "Verified clear, distant and solid-wall stands");
        foreach (var (label, stand) in new[] { ("HOME", home), ("BLOCK", block), ("FAR", far) })
        {
            string[] words = stand.Command.Split(' ');
            Console.WriteLine($"READY {label}: type '{string.Join(' ', words[..3])}', then ' {string.Join(' ', words[3..7])}', then ' {words[7]} {words[8]}'; press Enter once.");
        }
        var server = context.ActualServer!;
        var enemy = await LoginAsync("QAEnemy");
        var friend = await LoginAsync("QAFriend");
        if (context.StockProbe is not { } caster) return Array.Empty<byte>();
        DisablingShotMode.CheckProfile(caster, Loadout, Level);
        await PvpOpponentsFixture.EnterAsync(caster);
        await CombatFixtureLoadout.VerifyWeaponAtLoginAsync(caster, CombatFixtureWeapons.DualHandguns);
        NativeQaReadiness.RequireNoAmbientAggro(server, caster);
        uint enemyHandle = await HandleAsync(enemy);
        caster.SendRpc(WithHandle(PistolSlideMode.StockHandguns, enemyHandle));
        await ObserveAsync(caster, 0, rpc => ManageBonusRecord.TryDecode(rpc.Bytes, out var record) &&
            record is { SourceId: Handguns, TargetId: 171, AddRemoveType: 0 }, "Retained Handguns selects the dual-pistol style");
        await RefusedAsync(caster, Execution, WithHandle(StockExecution, await HandleAsync(friend)));
        await RefusedAsync(caster, Execution, WithHandle(StockExecution, enemyHandle ^ 0x10000));
        await MoveAsync(block);
        await RefusedAsync(caster, Execution, WithHandle(StockExecution, await HandleAsync(enemy)), AbilityRefusals.TargetNotInSight);
        await MoveAsync(far);
        await RefusedAsync(caster, Execution, WithHandle(StockExecution, await HandleAsync(enemy)), AbilityRefusals.TargetOutOfRange);
        await MoveAsync(home);
        int cursor = caster.World.RpcSnapshot().Count;
        int log = server.DebugLog().Length;
        caster.SendRpc(WithHandle(StockExecution, await HandleAsync(enemy)));
        await EvidenceRetestWire.CastBarAsync(caster, Execution, 4);
        await EvidenceRetestWire.TimerAsync(caster, cursor, Execution, AbilityCooldowns.OwnTimer, 10);
        await EvidenceRetestWire.TimerAsync(caster, cursor, 689, AbilityCooldowns.GroupTimer, 5);
        await caster.WaitUntilAsync(() => server.DebugLog()[log..].Contains(
            "[ABILITY-COST] char=110 ability=502 paid=35 IS=900->865", StringComparison.Ordinal) &&
            server.DebugLog()[log..].Contains("PistolExecutionAbility lands on QAEnemy for 53", StringComparison.Ordinal),
            "At-rest 502 pays and lands only on the admitted target", HeadlessPlayer.DefaultTimeout);
        Check.Equal(0, caster.World.Problems.Count + enemy.World.Problems.Count + friend.World.Problems.Count, "Clean stock framing");
        Console.WriteLine("DUAL PISTOL EXECUTION TARGETS PASS: ally, stale, sight and range denied; retained stock 502 casts at rest.");
        return Array.Empty<byte>();

        async Task<HeadlessPlayer> LoginAsync(string handle)
        {
            var actor = await HeadlessPlayer.LoginAsync(context.Address, handle, handle, stockCharacterLoad: true);
            context.Helpers.Add(handle, actor);
            await PvpOpponentsFixture.EnterAsync(actor);
            NativeQaReadiness.RequireNoAmbientAggro(server, actor);
            return actor;
        }

        async Task<uint> HandleAsync(HeadlessPlayer actor)
        {
            var view = await caster.WaitForViewNamedAsync(actor.Handle);
            return DuelPackets.Handle(view.ViewId, view.SpawnId);
        }

        async Task MoveAsync(Teleport stand)
        {
            caster.SendChat(stand.Command);
            await caster.WaitForTextAsync(stand.Confirmation);
        }
    }
}
