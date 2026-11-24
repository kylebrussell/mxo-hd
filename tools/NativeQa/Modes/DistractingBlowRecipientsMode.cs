using hds;
using HeadlessClient;
using MxoHd.PacketResearch;
using static NativeQa.StockAbilityProfile;
using static NativeQa.Modes.StarterHackerControlTargetMode;

namespace NativeQa.Modes;

public sealed class DistractingBlowRecipientsMode : NativeQaMode
{
    private const int Level = 20, X = 21550, Y = 1095, Z = 16150, EnemyX = 21750, FarX = 18650;
    private const ushort Spy = CombatStyles.SpyStyle, Sneak = Stealth.SneakGrantAbility, Blow = DistractingBlow.Id,
        Effect = DistractingBlow.EffectId;
    private static readonly ushort[] Loadout = DisablingShotMode.Closure(2, 17, Spy, 151, Blow);

    public override string Name => "ability-parity-distracting-blow-recipients";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), Array.Empty<string>(), new[]
    {
        PvpOpponentsFixture.NativeSql,
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y={Y},alignment=1,pvpflag=0 WHERE charId IN (110,6111,6112,6114)",
        $"UPDATE characters SET x={X},z={Z},rotation=64,innerStrC=1000,innerStrM=1000 WHERE charId=110",
        $"UPDATE characters SET x={EnemyX},z={Z},rotation=64,alignment=2,healthC=5000,healthM=5000 WHERE charId=6114",
        $"UPDATE characters SET x=21650,z=16250,rotation=192 WHERE charId=6111",
        $"UPDATE characters SET x={FarX},z={Z},rotation=64 WHERE charId=6112",
        $"UPDATE char_abilities SET level={Level} WHERE char_id=110",
        $"DELETE FROM inventory WHERE charId=110 AND slot={CombatFixtureWeapons.HandSlot}"
    }, DevCommands: true, hotbarAbilities: new[] { Spy, Sneak, Blow }, testMobFixture: "martial-player-controls.csv",
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15" },
        extraQaMetadata: new() { ["ScopedStockRows"] = "1128", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
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
        Check.Equal(0, audience.Sum(client => client.World.Problems.Count), "Clean stock protocol framing");
        Console.WriteLine("DISTRACTING BLOW RECIPIENT PASS: targeted motion and exact recipient adds/removals.");
        return Array.Empty<byte>();

        async Task<HeadlessPlayer> LoginAsync(string name)
        {
            var player = await HeadlessPlayer.LoginAsync(context.Address, name, name, stockCharacterLoad: true);
            context.Helpers.Add(name, player);
            await PvpOpponentsFixture.EnterAsync(player);
            NativeQaReadiness.RequireNoAmbientAggro(server, player);
            return player;
        }
    }

    private static async Task<uint> HandleAsync(HeadlessPlayer observer, HeadlessPlayer target)
    {
        var view = await observer.WaitForViewNamedAsync(target.Handle);
        return DuelPackets.Handle(view.ViewId, view.SpawnId);
    }

    // The selected 80 b9 shape uses the stock combat tail. The native plan captures this row’s original frame.
    private static byte[] Request(uint target) =>
        new AbilityUseRecord(Blow, target, 0, 0, 0, Blow | (0x6bUL << 40), 8, 0x6b, 0, 0).Encode();

    private static string MotionPattern(uint target) => $"01 20 ?? 01 0c 40 68 04 {WireFormat.ToHex(BitConverter.GetBytes(target))}";
}
