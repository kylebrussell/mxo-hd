using hds;
using HeadlessClient;
using MxoHd.PacketResearch;
using static NativeQa.Modes.StarterConcealedShotTargetingMode;
using static NativeQa.Modes.StarterHackerControlTargetMode;
using static NativeQa.StockAbilityProfile;

namespace NativeQa.Modes;

public sealed class PoisonKnifeTargetingMode : NativeQaMode
{
    private const ushort Knife = 146, Spy = 1112;
    private const int Level = 19, Rank = 14, PickX = 21550, EnemyX = 21750, DeflectX = 21650, AllyX = 21350;
    private const int BlockX = 22350, FarX = 19600, Y = 1095, Z = 16150;
    private static readonly ushort[] Loadout = DisablingShotMode.Closure(2, 17, Spy, Knife);
    private static readonly (string Name, uint Id, int X, int Alignment)[] Helpers =
        { ("QAEnemy", 6114, EnemyX, 2), ("QAFar", 6112, DeflectX, 2), ("QAFriend", 6111, AllyX, 1) };

    public override string Name => "ability-parity-poison-knife-targeting";
    public override NativeQaModePlan Plan { get; } = new(Loadout, PickX, Z,
        Array.Empty<string>(), Array.Empty<string>(), new[]
        {
            PvpOpponentsFixture.NativeSql,
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y={Y},z={Z}," +
                "healthC=5000,healthM=5000,innerStrC=500,innerStrM=500 WHERE charId IN (110,6111,6112,6114)",
            $"UPDATE characters SET x={PickX},rotation=64 WHERE charId=110",
            $"UPDATE char_abilities SET level={Rank} WHERE char_id=110",
            "DELETE FROM char_abilities WHERE char_id IN (6111,6112,6114)",
            $"DELETE FROM inventory WHERE charId=110 AND slot={CombatFixtureWeapons.HandSlot}"
        }.Concat(Helpers.Select(helper => $"UPDATE characters SET x={helper.X},rotation=192,alignment={helper.Alignment} WHERE charId={helper.Id}")).ToArray(),
        DevCommands: true, hotbarAbilities: new[] { Spy, Knife }, environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1", ["MXO_POISON_KNIFE_QA"] = "players"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "146", ["NativeCloseableRows"] = 1, ["NativeCloseableIds"] = "146" },
        readyMessages: new[]
        {
            $"L{Level} rank {Rank}, {Loadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} legal memory, unarmed, page 2 Spy Style and Poison Knife. " +
                $"CSR faces QAEnemy from verified outdoor PICK ({PickX},{Y},{Z}), with no ?tp needed for positives. QAFar ({DeflectX},{Y},{Z}) is also hostile. " +
                $"QAFriend ({AllyX},{Y},{Z}) is a player behind CSR to the west. Use /target with the exact names, or strafe once and turn the camera. No NPC selection.",
            "At PICK select Spy. Try with no target and with /target QAFriend. Expect local refusal or reason 24 with no debit, bar or reuse. " +
                "For geometry use the printed BLOCK and FAR ?tp commands and /target QAEnemy before each try. Expect local suppression or reason 9/34. Return using PICK.",
            "At PICK select /target QAEnemy and use slot 2 from rest. The disclosed pair-scoped draws produce the normal ranged hit path. Expect 15 IS (500 to 485), " +
                "2 s cast, 20 s own and 3 s family 1216. Look for [POISON KNIFE CONTEST] inputs(524/1010) hit=True, recipient 81 67 0x7a0002d6, one private caster hit notice and no direct damage. " +
                "Only QAEnemy gets 146/1157 total 18 for 25 s, with two damage every 2.5 s, matching removal and no further ticks after expiry.",
            "After expiry select /target QAFar and use slot 2. The same scoped fixture yields hit=False, recipient 81 67 0x7a000218, one private caster deflection notice and the same cost/bar/reuse. " +
                "QAFar receives no DOT, tick or direct damage. Close Combat on QAEnemy after this, reselect Spy, then try Poison Knife during ordinary interlock. " +
                "Expect the client to block it or reason 21 before any charge, cast or reuse."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        var pick = Stand(PickX, Z);
        var blocked = Stand(BlockX, Z);
        var far = Stand(FarX, Z);
        foreach (var helper in Helpers)
        {
            _ = Stand(helper.X, Z);
            Check.That(NavMesh.HasSightPath(1, PickX, Y, Z, helper.X, Y, Z), $"Clear PICK sight to {helper.Name}");
        }
        Check.That(!NavMesh.HasSightPath(1, BlockX, Y, Z, EnemyX, Y, Z) &&
            NavMesh.HasSightPath(1, FarX, Y, Z, EnemyX, Y, Z) && EnemyX - FarX > 2000, "In-range blocked and out-of-range clear checks");
        Console.WriteLine($"PICK {pick.Command}");
        Console.WriteLine($"BLOCK {blocked.Command} (6 m, blocked)");
        Console.WriteLine($"FAR {far.Command} (21.5 m, clear)");

        var server = context.ActualServer!;
        foreach (var helper in Helpers)
        {
            var player = await HeadlessPlayer.LoginAsync(context.Address, helper.Name, helper.Name, stockCharacterLoad: true);
            context.Helpers.Add(helper.Name, player);
            await PvpOpponentsFixture.EnterAsync(player);
            DisablingShotMode.CheckStand(player, helper.X);
            NativeQaReadiness.RequireNoAmbientAggro(server, player);
        }
        if (context.StockProbe is not { } caster) return Array.Empty<byte>();
        DisablingShotMode.CheckProfile(caster, Loadout, Level);
        await PvpOpponentsFixture.EnterAsync(caster);
        DisablingShotMode.CheckStand(caster, PickX);
        NativeQaReadiness.RequireNoAmbientAggro(server, caster);

        var enemy = context.Helpers["QAEnemy"];
        var resistant = context.Helpers["QAFar"];
        var ally = context.Helpers["QAFriend"];
        uint enemyHandle = await HandleAsync(enemy);
        uint allyHandle = await HandleAsync(ally);
        await RefusedAsync(caster, Knife, Request(enemyHandle), AbilityRefusals.WrongStyle);
        caster.SendRpc(AwakenedStockStealthSpeedMode.SpyUse);
        await ObserveAsync(caster, 0, rpc => Bonus(rpc, Spy, 1034, 11, 0, false), "Spy Style rank 14");
        await RefusedAsync(caster, Knife, Request(0));
        await RefusedAsync(caster, Knife, Request(allyHandle));
        await RefusedAsync(caster, Knife, Request(enemyHandle ^ 0x10000u));
        await MoveAsync(blocked);
        await RefusedAsync(caster, Knife, Request(enemyHandle), AbilityRefusals.TargetNotInSight);
        await MoveAsync(far);
        await RefusedAsync(caster, Knife, Request(enemyHandle), AbilityRefusals.TargetOutOfRange);
        await MoveAsync(pick);

        int logAt = server.DebugLog().Length;
        int targetAt = enemy.World.RpcSnapshot().Count;
        int sourceAt = caster.World.RpcSnapshot().Count;
        caster.SendRpc(Request(enemyHandle));
        await EvidenceRetestWire.CastBarAsync(caster, Knife, 2);
        await ObserveAsync(enemy, targetAt, rpc => Contest(rpc, enemy.Handle, true), "Selected recipient receives stock ranged success");
        await caster.WaitForTextAsync($"Poison Knife hit {enemy.Handle}.");
        await VerifyReuseAsync(sourceAt);
        var dot = await ObserveAsync(enemy, targetAt, rpc => Bonus(rpc, Knife, 1157, 18, 25, false), "The selected target receives its 25 s, total 18 ThrownDOT");
        ManageBonusRecord.TryDecode(dot.Bytes, out var application);
        var removal = application! with { AddRemoveType = 1 };
        await enemy.WaitUntilAsync(() => enemy.World.RpcSnapshot().Skip(targetAt)
            .Any(rpc => ManageBonusRecord.TryDecode(rpc.Bytes, out var record) && record == removal),
            "The original ThrownDOT record expires", TimeSpan.FromSeconds(30));
        string paid = server.DebugLog()[logAt..];
        Check.Equal(9, paid.Split("[EFFECTS] 146 ticks on QAEnemy for 2", StringSplitOptions.None).Length - 1,
            "25 s DOT delivers nine typed ticks before expiry");
        Check.That(!paid.Contains("146 ticks on QAFriend", StringComparison.Ordinal) &&
            !paid.Contains("146 ticks on QAFar", StringComparison.Ordinal), "The effect never hits bystanders");

        int deflectAt = resistant.World.RpcSnapshot().Count;
        sourceAt = caster.World.RpcSnapshot().Count;
        caster.SkipReceived();
        caster.SendRpc(Request(await HandleAsync(resistant)));
        await EvidenceRetestWire.CastBarAsync(caster, Knife, 2);
        await ObserveAsync(resistant, deflectAt, rpc => Contest(rpc, resistant.Handle, false), "Selected recipient receives the stock ranged deflection");
        await caster.WaitForTextAsync($"{resistant.Handle} deflected Poison Knife.");
        await VerifyReuseAsync(sourceAt);
        Check.That(!resistant.World.RpcSnapshot().Skip(deflectAt).Any(rpc => Bonus(rpc, Knife, 1157, 18, 25, false)),
            "Deflection installs no ThrownDOT");
        var strength = caster.World.ViewSnapshot().SelectMany(AbilityParityCoderSweepMode.InnerStrengthUpdates);
        Check.That(strength.Contains((ushort)485) && strength.Contains((ushort)470), "Only the two valid casts each spend 15 IS");

        byte[] ownExpired = AbilityCooldowns.BuildTimer(Knife, AbilityCooldowns.OwnTimer, 20, false);
        await caster.WaitUntilAsync(() => caster.World.RpcSnapshot().Skip(sourceAt)
            .Any(rpc => rpc.Bytes.AsSpan().SequenceEqual(ownExpired)), "Deflected cast's 20 s reuse expires", TimeSpan.FromSeconds(25));
        int movedAt = server.DebugLog().Length;
        caster.SkipReceived();
        caster.SendRpc(Request(enemyHandle));
        await EvidenceRetestWire.CastBarAsync(caster, Knife, 2);
        await MoveAsync(blocked);
        await caster.WaitUntilAsync(() => server.DebugLog()[movedAt..].Contains("146 fizzles", StringComparison.Ordinal),
            "Admitted cast rechecks the original opponent's line of sight at landing", HeadlessPlayer.DefaultTimeout);
        Check.That(!server.DebugLog()[movedAt..].Contains("[POISON KNIFE CONTEST]", StringComparison.Ordinal),
            "A moved or blocked admitted cast does not reach the contest");
        await MoveAsync(pick);

        caster.SendRpc($"40 {WireFormat.ToHex(BitConverter.GetBytes(enemyHandle))} 01 6b");
        await caster.WaitForRpcAsync("47");
        await RefusedAsync(caster, Knife, Request(enemyHandle), AbilityRefusals.MustBeOutsideCombat);
        Check.Equal(0, context.Helpers.Values.Sum(helper => helper.World.Problems.Count) + caster.World.Problems.Count,
            "Poison Knife targeting protocol framing");
        Console.WriteLine("POISON KNIFE BOOTSTRAP PASS: stock admission, selected hit, nine ticks, matched removal, deflection and active-fight denial.");
        return Array.Empty<byte>();

        async Task<uint> HandleAsync(HeadlessPlayer selected)
        {
            var v = await caster.WaitForViewNamedAsync(selected.Handle);
            return DuelPackets.Handle(v.ViewId, v.SpawnId);
        }

        async Task MoveAsync(Teleport stand)
        {
            caster.SkipReceived();
            caster.SendChat(stand.Command);
            await caster.WaitForTextAsync(stand.Confirmation);
        }

        async Task VerifyReuseAsync(int cursor)
        {
            await EvidenceRetestWire.TimerAsync(caster, cursor, Knife, AbilityCooldowns.OwnTimer, 20);
            await EvidenceRetestWire.TimerAsync(caster, cursor, 1216, AbilityCooldowns.GroupTimer, 3);
        }

        bool Contest(RpcRecord rpc, string recipient, bool landed)
        {
            byte[] frame = rpc.Bytes;
            if (frame.Length < 40 || frame[0] != 0x81 || frame[1] != 0x67 ||
                BitConverter.ToUInt32(frame, 6) != (landed ? 0x7a0002d6u : 0x7a000218u)) return false;
            int values = BitConverter.ToUInt16(frame, 19);
            if (values + 18 != frame.Length) return false;
            int attack = BitConverter.ToInt32(frame, values + 2), defense = BitConverter.ToInt32(frame, values + 10);
            return (attack > defense) == landed && frame.AsSpan().SequenceEqual(
                AbilityMessages.AboutAbility(landed ? 0x7a0002d6u : 0x7a000218u, true, Knife, caster.Handle, recipient, attack, 0, defense, 0));
        }
    }

    private static byte[] Request(uint handle) => WithHandle(StarterSpyCombatAdmissionMode.PoisonCast, handle);
}
