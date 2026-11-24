using hds;
using HeadlessClient;
using MxoHd.PacketResearch;
using static NativeQa.StockAbilityProfile;
using static NativeQa.Modes.StarterHackerControlTargetMode;

namespace NativeQa.Modes;

public class DistractingBlowRecipientsMode : NativeQaMode
{
    private const int Level = 20, X = 21550, Y = 1095, Z = 16150, EnemyX = 21750, FarX = 18650;
    private const ushort Spy = CombatStyles.SpyStyle, Sneak = Stealth.SneakGrantAbility, Blow = DistractingBlow.Id,
        Effect = DistractingBlow.EffectId;
    private static readonly ushort[] Loadout = DisablingShotMode.Closure(2, 17, Spy, 151, Blow);

    private readonly bool verifyWithdrawal;

    public DistractingBlowRecipientsMode() : this(false) { }

    protected DistractingBlowRecipientsMode(bool verifyWithdrawal)
    {
        this.verifyWithdrawal = verifyWithdrawal;
        Plan = BuildPlan();
    }

    public override string Name => "ability-parity-distracting-blow-recipients";
    public override NativeQaModePlan Plan { get; }

    private NativeQaModePlan BuildPlan() => new(Loadout, X, Z, Array.Empty<string>(), Array.Empty<string>(), new[]
    {
        PvpOpponentsFixture.NativeSql,
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y={Y},alignment=1,pvpflag=0 WHERE charId IN (110,6111,6112,6114)",
        $"UPDATE characters SET x={X},z={Z},rotation=64,innerStrC=1000,innerStrM=1000 WHERE charId=110",
        $"UPDATE characters SET x={EnemyX},z={Z},rotation=64,alignment=2,healthC=5000,healthM=5000 WHERE charId=6114",
        $"UPDATE characters SET x=21650,z=16250,rotation=192 WHERE charId=6111",
        $"UPDATE characters SET x={FarX},z={Z},rotation=64 WHERE charId=6112",
        $"UPDATE char_abilities SET level={Level} WHERE char_id=110",
        $"DELETE FROM inventory WHERE charId=110 AND slot={CombatFixtureWeapons.HandSlot}"
    }, DevCommands: true, hotbarAbilities: new[] { Spy, Sneak, Blow }, hotbarWithdraw: verifyWithdrawal, testMobFixture: "martial-player-controls.csv",
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            [CombatFixtureWeapons.DisengageMode] = verifyWithdrawal ? "1" : "0"
        },
        extraQaMetadata: new() { ["ScopedStockRows"] = "1128", ["NativeCloseableRows"] = verifyWithdrawal ? 1 : 0 },
        readyMessages: verifyWithdrawal ? new[]
        {
            "Verified NAV HOME faces QAEnemy's back 2 m east. Page 2 slots 1 Spy, 2 Sneak, 3 Distracting Blow, 4 Withdraw. Start with the cast on a fresh helper. An opening baseline fight turns QAEnemy toward CSR and prevents Distracting Blow. Measure baseline Withdraw after the buff expires.",
            "Select Spy and Sneak at HOME. For the frontal refusal, use the printed FRONT teleport chunks and /target QAEnemy. Tap slot 3. Expect a local facing gate or invalid target and no debit. At BLOCK and FAR, retarget and try 3 for sight or range refusal without debit. Return to HOME with printed chunks, reselect QAEnemy and Sneak if dropped. Select Spy again if needed.",
            "At HOME press 3. Expect a 2 s cast, 35 IS, 77 damage and source1129 stat1005 +50 for 12 s. On add, immediately use radial Close Combat on QAEnemy and press slot 4. Expect direct=76 input=83 bonus1045=10. A win is unnecessary. Wait for the 1129 removal.",
            "After removal, use ?cd stop if still in combat, open another Close Combat and use slot 4 again. Expect direct=26 input=28 bonus1045=10. No RNG aid. Printed stand teleports and ?cd stop are disposable geometry and cleanup aids."
        } : new[]
        {
            "Verified outdoor HOME faces QAEnemy's back 2 m east. No teleport for the main sequence. Page 2 slots 1 Spy Style, 2 Sneak, 3 Distracting Blow. Select Spy, /target QAEnemy, try slot 3 before Sneak. Expect stock gate or reason 23 and no debit.",
            "Use slot 2, /target QAFriend and press slot 3. Expect invalid target and no debit. /target QAEnemy, press slot 3. Expect retained Sneak during the 2 s cast, 35 IS, then Sneak off, a targeted knife throw and 77 damage. Save the first successful stock request.",
            "Expect source1129 stat1005 +50 for 12 s on CSR and nearby QAFriend, with matching removals. QAEnemy and QAFar get no 1129. Own/family timers 45/15 s. No QA roll aid. Observer replay checks the actual target-local motion handles."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        SectorProperties.Load(Path.Combine(context.RepositoryRoot!, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(context.RepositoryRoot!, "data", "navmesh"));
        foreach (var (name, x, z, yaw) in new[]
        {
            ("HOME", X, Z, (byte)64), ("QAEnemy", EnemyX, Z, (byte)64),
            ("QAFriend", 21650, 16250, (byte)192), ("QAFar", FarX, Z, (byte)64)
        })
            Console.WriteLine($"READY {name} {InvisibilityBeltWatcherMode.Warp(x, Y, z, yaw, inside: false)}");
        Check.That(NavMesh.HasSightPath(1, X, Y, Z, EnemyX, Y, Z), "Clear two metre rear approach");
        var home = StarterConcealedShotTargetingMode.Stand(X, Z);
        StarterConcealedShotTargetingMode.Teleport? front = null, block = null, distant = null;
        if (verifyWithdrawal)
        {
            front = StarterConcealedShotTargetingMode.Stand(21850, 16200);
            Check.That(NavMesh.HasSightPath(1, 21850, Y, 16200, EnemyX, Y, Z) &&
                Stealth.InFrustum(EnemyX, Z, 64, 21850, 16200, 50), "Clear frontal stand inside the authored cone");
            block = StarterConcealedShotTargetingMode.Stand(21950, Z);
            distant = StarterConcealedShotTargetingMode.FindFarStand();
            Check.That(!NavMesh.HasSightPath(1, 21950, Y, Z, EnemyX, Y, Z), "Solid wall at the near blocked stand");
            PrintChunks("HOME", home);
            PrintChunks("FRONT", front);
            PrintChunks("BLOCK", block);
            PrintChunks("FAR", distant);
        }
        var server = context.ActualServer!;
        var enemy = await LoginAsync("QAEnemy");
        var near = await LoginAsync("QAFriend");
        var far = await LoginAsync("QAFar");
        if (context.StockProbe is not { } caster) return Array.Empty<byte>();
        DisablingShotMode.CheckProfile(caster, Loadout, Level);
        await PvpOpponentsFixture.EnterAsync(caster);
        NativeQaReadiness.RequireNoAmbientAggro(server, caster);
        uint target = await HandleAsync(caster, enemy);
        caster.SendRpc(AwakenedStockStealthSpeedMode.SpyUse);
        await caster.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 58 04 00 00");
        await RefusedAsync(caster, Blow, Request(target), AbilityRefusals.CasterStateRequired);
        caster.SendRpc(AwakenedStockStealthSpeedMode.SneakUse);
        await caster.WaitUntilAsync(() => server.DebugLog().Contains("[STEALTH] CSR_Tester 1111 on", StringComparison.Ordinal),
            "Stock Sneak starts behind QAEnemy", HeadlessPlayer.DefaultTimeout);
        await RefusedAsync(caster, Blow, Request(await HandleAsync(caster, near)), AbilityRefusals.InvalidTarget);
        await RefusedAsync(caster, Blow, Request(target ^ 0x10000), AbilityRefusals.InvalidTarget);
        if (front != null)
        {
            await MoveAsync(front);
            await RefusedAsync(caster, Blow, Request(await HandleAsync(caster, enemy)), AbilityRefusals.InvalidTarget);
            await MoveAsync(block!);
            await RefusedAsync(caster, Blow, Request(await HandleAsync(caster, enemy)), AbilityRefusals.TargetNotInSight);
            await MoveAsync(distant!);
            await RefusedAsync(caster, Blow, Request(await HandleAsync(caster, enemy)), AbilityRefusals.TargetOutOfRange);
            await MoveAsync(home);
            target = await HandleAsync(caster, enemy);
        }
        caster.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(target))}");
        var seenCaster = await near.WaitForViewNamedAsync("CSR_Tester");
        uint seenEnemy = await HandleAsync(near, enemy);
        var audience = new[] { caster, near, enemy, far };
        int[] cursors = audience.Select(client => client.World.RpcSnapshot().Count).ToArray();
        caster.SendRpc(Request(target));
        await EvidenceRetestWire.CastBarAsync(caster, Blow, 2);
        byte[] hit = AbilityMessages.Damage(false, Blow, caster.Handle, enemy.Handle, 77, 0);
        await ObserveAsync(enemy, cursors[2], p => p.Bytes.AsSpan().SequenceEqual(hit), "The live opponent takes 77 Thrown damage");
        await caster.WaitForViewUpdateAsync(2, MotionPattern(target));
        await near.WaitForViewUpdateAsync(seenCaster.ViewId, MotionPattern(seenEnemy));
        await EvidenceRetestWire.TimerAsync(caster, cursors[0], Blow, AbilityCooldowns.OwnTimer, 45);
        await EvidenceRetestWire.TimerAsync(caster, cursors[0], 691, AbilityCooldowns.GroupTimer, 15);
        if (verifyWithdrawal) await MeasureWithdrawAsync(76, 83);
        foreach (int i in new[] { 0, 1 })
        {
            var added = await ObserveAsync(audience[i], cursors[i], p => Bonus(p, Effect, 1005, 50, 12, false),
                "Caster and nearby ally receive the same authored aura");
            Check.That(ManageBonusRecord.TryDecode(added.Bytes, out var record) && record != null, "Decode the 1129 add");
            await audience[i].WaitUntilAsync(() => audience[i].World.RpcSnapshot().Skip(cursors[i])
                .Any(p => ManageBonusRecord.TryDecode(p.Bytes, out var removed) && removed == record! with { AddRemoveType = 1 }),
                "A matching removal follows the 12 s aura", TimeSpan.FromSeconds(14));
        }
        foreach (int i in new[] { 2, 3 })
            Check.That(!audience[i].World.RpcSnapshot().Skip(cursors[i]).Any(p => Bonus(p, Effect, 1005, 50, 12, false)),
                "The hostile target and far ally receive no aura");
        if (verifyWithdrawal) await MeasureWithdrawAsync(26, 28);
        Check.Equal(0, audience.Sum(client => client.World.Problems.Count), "Clean stock protocol framing");
        Console.WriteLine("DISTRACTING BLOW RECIPIENT PASS: targeted motion and exact recipient adds/removals.");
        return Array.Empty<byte>();

        async Task MoveAsync(StarterConcealedShotTargetingMode.Teleport stand)
        {
            caster.SkipReceived();
            caster.SendChat(stand.Command);
            await caster.WaitForTextAsync(stand.Confirmation);
        }

        async Task MeasureWithdrawAsync(int direct, int input)
        {
            int rpcAt = caster.World.RpcSnapshot().Count, logAt = server.DebugLog().Length;
            string selected = WireFormat.ToHex(BitConverter.GetBytes(target));
            caster.World.SendRpcs(WithHandle(PistolSlideMode.StockSelfDefense, target), WireFormat.FromHex($"40 {selected} 01 6b"));
            await ObserveAsync(caster, rpcAt, p => p.Bytes is [0x46, ..] && p.Bytes.Length >= 6 && p.Bytes[5] == 1,
                "Stock Close Combat with the same visible enemy");
            await ObserveAsync(caster, rpcAt, p => p.Bytes is [0x45, ..],
                "The opening round precedes the ordinary Withdraw");
            caster.SendRpc("44 01");
            await caster.WaitUntilAsync(() => server.DebugLog()[logAt..].Contains(
                $"[WITHDRAW INPUT OURS] CSR_Tester direct={direct} input={input} bonus1045=10", StringComparison.Ordinal),
                $"Ordinary Withdraw consumes {direct} as effective input {input}", HeadlessPlayer.DefaultTimeout);
            caster.SkipReceived();
            caster.SendChat("?cd stop");
            await caster.WaitForTextAsync("Fixture combat ended");
        }

        async Task<HeadlessPlayer> LoginAsync(string name)
        {
            var player = await HeadlessPlayer.LoginAsync(context.Address, name, name, stockCharacterLoad: true);
            context.Helpers.Add(name, player);
            await PvpOpponentsFixture.EnterAsync(player);
            NativeQaReadiness.RequireNoAmbientAggro(server, player);
            return player;
        }
    }

    private static void PrintChunks(string name, StarterConcealedShotTargetingMode.Teleport stand)
    {
        string[] parts = stand.Command.Split(' ');
        Console.WriteLine($"READY {name} type '{string.Join(' ', parts[..3])}', then ' {string.Join(' ', parts[3..7])}', then ' {parts[7]} {parts[8]}'. Press Enter once.");
    }

    private static async Task<uint> HandleAsync(HeadlessPlayer observer, HeadlessPlayer target)
    {
        var view = await observer.WaitForViewNamedAsync(target.Handle);
        return DuelPackets.Handle(view.ViewId, view.SpawnId);
    }

    // The selected 80 b9 shape uses the stock combat tail. The native plan captures this rowÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢s original frame.
    private static byte[] Request(uint target) =>
        new AbilityUseRecord(Blow, target, 0, 0, 0, Blow | (0x6bUL << 40), 8, 0x6b, 0, 0).Encode();

    private static string MotionPattern(uint target) => $"01 20 ?? 01 0c 40 68 04 {WireFormat.ToHex(BitConverter.GetBytes(target))}";
}

public sealed class DistractingBlowWithdrawalMode : DistractingBlowRecipientsMode
{
    public DistractingBlowWithdrawalMode() : base(true) { }

    public override string Name => "ability-parity-distracting-blow-contested-withdraw";
}
