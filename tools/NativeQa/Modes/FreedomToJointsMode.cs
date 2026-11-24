using MxoHd.PacketResearch;
using HeadlessClient;
using hds;
using static NativeQa.StockAbilityProfile;
using static NativeQa.Modes.StarterHackerControlTargetMode;
using static NativeQa.Modes.StarterConcealedShotTargetingMode;

namespace NativeQa.Modes;

public sealed class FreedomToJointsMode : NativeQaMode
{
    private const int X = 21550, Y = 1095, Z = 16150, TargetX = 21750, Level = 50;
    private const ushort Spy = 1112, Sneak = 1111, Shot = SpyMasterSneakOpeners.FreedomToJoints;
    // Retained PR1099 stock request. The live target's handle replaces the captured handle.
    private const string StockUse = "80b982010c00100000000000000000000000000000000000000000000000000090767241086b0000000000";
    private static readonly ushort[] Loadout = DisablingShotMode.Closure(2, 17, Spy, 151, 340, 347, Shot);

    public override string Name => "ability-parity-freedom-to-joints-opener";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), Array.Empty<string>(), new[]
    {
        PvpOpponentsFixture.NativeSql,
        $"UPDATE characters SET x={X},y={Y},z={Z},rotation=64,alignment=1," +
            "healthC=5000,healthM=5000,innerStrC=1000,innerStrM=1000 WHERE charId=110",
        $"UPDATE characters SET x={TargetX},y={Y},z={Z},rotation=64,alignment=2," +
            "healthC=5000,healthM=5000 WHERE charId=6114",
        $"UPDATE characters SET x=21650,y={Y},z=16250,rotation=192,alignment=1 WHERE charId=6111",
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)} WHERE charId IN (110,6111,6114)",
        $"UPDATE char_abilities SET level={Level} WHERE char_id=110",
        $"DELETE FROM inventory WHERE charId IN (110,6114) AND slot={CombatFixtureWeapons.HandSlot}"
    }, DevCommands: true, testMobFixture: "martial-player-controls.csv", hotbarAbilities: new[] { Spy, Sneak, Shot },
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1",
            ["MXO_PVP_MAX_SAFE_LEVEL"] = "15"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "386", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            "At HOME behind QAEnemy. Page 2 slots 1 Spy, 2 Sneak, 3 Freedom to Joints. Select Spy then /target QAEnemy and press 3 before Sneak. Expect local gate or 80 ba reason 23. No IS or reuse.",
            "At HOME press 2 for Sneak, /target QAFriend and press 3. Expect local gate or reason 24. For BLOCK and FAR use the printed tp chunks and press Enter once. Retarget QAEnemy; tap Sneak if dropped, then press 3. Expect sight 9 and range 34 or local gates.",
            "Return HOME facing east, retarget QAEnemy by name. Sneak if needed, then slot 3. Keep close combat off. Expect Sneak off, 60 IS, instant 1527 Thrown damage, source386 stat96 add/remove at 7.6 s (8 s authored), and 60 s own reuse. Watch whether the client plays a named move. Save the first successful 386 frame and surrounding replies. No RNG aid. ID 19600 is in the shipped move table; this check observes what the stock client plays."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var server = context.ActualServer!;
        SectorProperties.Load(Path.Combine(context.RepositoryRoot!, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(context.RepositoryRoot!, "data", "navmesh"));
        var home = Stand(X, Z);
        var blocked = Stand(22350, Z);
        var far = FindFarStand();
        PrintStand("HOME", home);
        PrintStand("BLOCK", blocked);
        PrintStand("FAR", far);
        Console.WriteLine($"READY QAEnemy {InvisibilityBeltWatcherMode.Warp(TargetX, Y, Z, 64, inside: false)}");
        _ = InvisibilityBeltWatcherMode.Warp(21650, Y, 16250, 192, inside: false);
        Check.That(NavMesh.HasSightPath(1, X, Y, Z, TargetX, Y, Z), "Clear rear approach");
        Check.That(!NavMesh.HasSightPath(1, 22350, Y, Z, TargetX, Y, Z), "Blocked stand uses a solid wall");
        var friend = await PartyAsync("QAFriend");
        var enemy = await PartyAsync("QAEnemy");
        if (context.StockProbe is not { } caster) return Array.Empty<byte>();
        DisablingShotMode.CheckProfile(caster, Loadout, Level);
        await PvpOpponentsFixture.EnterAsync(caster);
        NativeQaReadiness.RequireNoAmbientAggro(server, caster);
        uint rearHandle = await HandleAsync(enemy);
        await RefusedAsync(caster, Shot, WithHandle(StockUse, rearHandle), AbilityRefusals.WrongStyle);
        caster.SendRpc(AwakenedStockStealthSpeedMode.SpyUse);
        await caster.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 58 04 00 00");
        int start = caster.World.RpcSnapshot().Count;
        await RefusedAsync(caster, Shot, WithHandle(StockUse, rearHandle), AbilityRefusals.CasterStateRequired);
        await ObserveAsync(caster, start, rpc => AbilityRefusals.TryDecode(rpc.Bytes, out var r) &&
            r.AbilityID == Shot && r.StatesNotMet == Stealth.StealthedFlag, "Refusal carries the required Stealthed flag");
        await SneakAsync();
        await RefusedAsync(caster, Shot, WithHandle(StockUse, 0), AbilityRefusals.InvalidTarget);
        await RefusedAsync(caster, Shot, WithHandle(StockUse, await HandleAsync(friend)), AbilityRefusals.InvalidTarget);
        await MoveAsync(blocked);
        await RefusedAsync(caster, Shot, WithHandle(StockUse, await HandleAsync(enemy)), AbilityRefusals.TargetNotInSight);
        await MoveAsync(far);
        await RefusedAsync(caster, Shot, WithHandle(StockUse, await HandleAsync(enemy)), AbilityRefusals.TargetOutOfRange);
        await MoveAsync(home);

        int before = caster.World.RpcSnapshot().Count, enemyBefore = enemy.World.RpcSnapshot().Count;
        int logBefore = server.DebugLog().Length;
        uint handle = await HandleAsync(enemy);
        caster.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(handle))}");
        caster.SendRpc(WithHandle(StockUse, handle));
        await EvidenceRetestWire.TimerAsync(caster, before, Shot, AbilityCooldowns.OwnTimer, 60);
        var hit = AbilityMessages.Damage(false, Shot, caster.Handle, enemy.Handle, 1527, 0);
        await ObserveAsync(enemy, enemyBefore, r => r.Bytes.AsSpan().SequenceEqual(hit), "Freedom to Joints hits the rear live target for 1527");
        await caster.WaitUntilAsync(() => server.DebugLog()[logBefore..].Contains("[STEALTH] CSR_Tester 1111 off", StringComparison.Ordinal),
            "The successful opener consumes Sneak", HeadlessPlayer.DefaultTimeout);
        var added = await ObserveAsync(enemy, enemyBefore, r => Bonus(r, Shot, TempMods.PowerlessAbility, 0, 7.6f, false),
            "Live Powerless uses 7.6 s from the authored 8 s");
        Check.That(ManageBonusRecord.TryDecode(added.Bytes, out var modifier) && modifier != null, "Decode Powerless record");
        await ObserveAsync(enemy, enemyBefore, r => ManageBonusRecord.TryDecode(r.Bytes, out var removal) &&
            removal == modifier! with { AddRemoveType = 1 }, "Powerless removes the same modifier");
        await ObserveAsync(enemy, enemyBefore, r => r.Bytes.AsSpan().SequenceEqual(
            AbilityMessages.StateEnd(TempMods.PowerlessAbility, enemy.Handle)), "Powerless release notice");
        string landed = server.DebugLog()[logBefore..];
        Check.That(landed.Contains("[ABILITY-COST] char=110 ability=386 paid=60", StringComparison.Ordinal),
            "Only the admitted stock opener spends 60 IS");
        Check.That(!caster.World.RpcSnapshot().Skip(before).Any(r => r.Bytes is [0x46, ..] && r.Bytes.Length == 6 && r.Bytes[5] == 1),
            "The live opener did not enter the interlock path");
        Check.Equal(0, caster.World.Problems.Count + enemy.World.Problems.Count + friend.World.Problems.Count, "Retained stock framing");
        Console.WriteLine("FREEDOM TO JOINTS BOOTSTRAP PASS: style, Sneak, target, wall, and range refusals; legal opener consumes Sneak.");
        return Array.Empty<byte>();

        async Task<HeadlessPlayer> PartyAsync(string name)
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, name, name, stockCharacterLoad: true);
            context.Helpers.Add(helper.Handle, helper);
            await PvpOpponentsFixture.EnterAsync(helper);
            NativeQaReadiness.RequireNoAmbientAggro(server, helper);
            return helper;
        }

        async Task<uint> HandleAsync(HeadlessPlayer other)
        {
            var view = await caster.WaitForViewNamedAsync(other.Handle);
            return DuelPackets.Handle(view.ViewId, view.SpawnId);
        }

        async Task MoveAsync(Teleport stand)
        {
            caster.SkipReceived();
            caster.SendChat(stand.Command);
            await caster.WaitForTextAsync(stand.Confirmation);
        }

        async Task SneakAsync()
        {
            int from = server.DebugLog().Length;
            caster.SendRpc(AwakenedStockStealthSpeedMode.SneakUse);
            await caster.WaitUntilAsync(() => server.DebugLog()[from..].Contains("[STEALTH] CSR_Tester 1111 on", StringComparison.Ordinal),
                "Stock Sneak activates", HeadlessPlayer.DefaultTimeout);
        }
    }

    private static void PrintStand(string label, Teleport stand)
    {
        string[] p = stand.Command.Split(' ');
        Console.WriteLine($"READY {label} type '{string.Join(' ', p[..3])}', then ' {string.Join(' ', p[3..7])}', then ' {p[7]} {p[8]}'; press Enter once.");
    }
}
