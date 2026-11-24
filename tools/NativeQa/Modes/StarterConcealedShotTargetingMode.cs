using hds;
using HeadlessClient;
using MxoHd.PacketResearch;
using static NativeQa.StockAbilityProfile;
using static NativeQa.Modes.StarterHackerControlTargetMode;

namespace NativeQa.Modes;

public sealed class StarterConcealedShotTargetingMode : NativeQaMode
{
    private const int Level = 19, PickX = 21550, TargetX = 21750, BlockedX = 22350, Y = 1095, Z = 16150, StartingIs = 400;
    private const ushort ConcealedIs = StartingIs - AbilityCosts.Default;
    private const ushort Rifles = 147, Parent = 503, Conceal = Stealth.ConcealmentGrantAbility, Shot = CombatSpecials.ConcealedShot;
    // Full stock PR429 Concealment (7483f644ff39:1600–1601) and PR410 Concealed Shot (3ef3dc4c8f4c:2400–2401).
    internal const string ConcealFrame = "80b97104000000000000000000000000000000000000000000000000d557377c20000000086b0000000000";
    internal const string ShotFrame = "80b9f801040002000000000000000000000000000000000000000000585cc10001000000086b0000000000";
    private static readonly ushort[] Loadout = new ushort[] { 2, 17, Rifles, Parent, Shot }
        .SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys.Append(id)).Distinct().Order().ToArray();

    public override string Name => "ability-parity-starter-concealed-shot-targeting";
    public override NativeQaModePlan Plan { get; } = new(Loadout, PickX, Z, Array.Empty<string>(), new[]
    {
        CombatFixtureLoadout.SeedWeaponsFor(CombatFixtureWeapons.Rifle),
        StarterHackerPassiveFixture.CapRanks
    }, new[]
    {
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y={Y},rotation=64," +
            $"healthC=1000,healthM=1000,innerStrC={StartingIs},innerStrM={StartingIs},alignment=1,pvpflag=0 WHERE charId=110",
        $"UPDATE characters SET x={TargetX},y={Y},z={Z},rotation=64,alignment=2,level=10," +
            $"exp={PlayerProgression.ExperienceForLevel(10)},pvpflag=0,healthC=1000,healthM=1000 WHERE charId=6114",
        "DELETE FROM char_abilities WHERE char_id=6114 AND ability_id NOT IN (2147485696,2147501056)"
    }, DevCommands: true, hotbarAbilities: new[] { Rifles, Conceal, Shot }, testMobFixture: "martial-player-controls.csv",
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "6",
            ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1", [StarterInterlockControlQa.ConcealedShotMode] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "504,1137", ["NativeCloseableRows"] = 1 },
        readyMessages: new[]
        {
            $"Level {Level}, 42/49 legal memory, rifle equipped. CSR faces QAEnemy from PICK ({PickX},{Y},{Z}). Page 2 Rifles / Concealment / Concealed Shot. Use slot 1 and /target QAEnemy. No teleport for this first check.",
            "With QAEnemy selected, press slot 3 before Concealment. Expect a local Concealed restriction or the server caster-state refusal. No 45 IS debit or damage.",
            "Use slot 2, wait for 1137 on, then use the printed BLOCK teleport. /target QAEnemy and press slot 3. Expect sight refusal 9 if the request reaches the server. Repeat at FAR for range refusal 34. Local refusals are also useful: record the exact client text and whether a 504 request appeared. Neither attempt should drop Concealment, debit 45 IS or start the 45 s timer.",
            $"Return to printed PICK, /target QAEnemy and use slot 3 with 1137 active. If it expired during setup, wait for Concealment's own reuse and activate again. Expect 1137 off at cast start, 45 IS ({ConcealedIs} to {ConcealedIs - 45}), 6 s bar, 23 damage and 45 s own reuse. No interlock round.",
            "QA fixes only the landed 504 marker roll. Expect QAEnemy source 504 marker value 4 for 0.1 s, then Stun 102 and linked Combat Tactics 163 -20, plus independent immunity 734/681. Capture their matched removals at 4 s. Enter the teleport commands in the printed chunks with one final Enter. No helper action."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        var pick = Stand(PickX, Z);
        var blocked = Stand(BlockedX, Z);
        _ = Stand(TargetX, Z);
        Check.That(NavMesh.HasSightPath(1, PickX, Y, Z, TargetX, Y, Z) &&
            !NavMesh.HasSightPath(1, BlockedX, Y, Z, TargetX, Y, Z), "The near and blocked stands distinguish sight inside 30 m");
        var far = FindFarStand();
        PrintStand("PICK", pick);
        PrintStand("BLOCK", blocked);
        PrintStand("FAR (>30 m, clear sight)", far);

        var enemy = await HeadlessPlayer.LoginAsync(context.Address, "QAEnemy", "QAEnemy", stockCharacterLoad: true);
        context.Helpers.Add(enemy.Handle, enemy);
        await enemy.EnterWorldAsync();
        await enemy.WaitForViewUpdateAsync(2, PvpOpponentsFixture.SelfFlag);
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, enemy);
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        StarterHackerPassiveFixture.VerifyProfile(actor, Loadout);
        Check.Equal(42, Loadout.Sum(AbilityMemory.Cost), "Full starter Concealed Shot prerequisites fit");
        await CombatFixtureLoadout.VerifyWeaponAtLoginAsync(actor, CombatFixtureWeapons.Rifle);
        await actor.WaitForViewUpdateAsync(2, PvpOpponentsFixture.SelfFlag);
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, actor);

        var self = actor.World.CreationSnapshot().Last(view => view.ViewId == 2);
        var target = await actor.WaitForViewNamedAsync(enemy.Handle);
        uint handle = DuelPackets.Handle(target.ViewId, target.SpawnId);
        byte[] shot = WithHandle(ShotFrame, handle);
        await StarterFixedDamageMode.SelectStyle(actor, self, Rifles, 171);
        actor.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(handle))}");
        await RefusedAsync(actor, Shot, shot, AbilityRefusals.CasterStateRequired);
        int activated = actor.World.RpcSnapshot().Count;
        actor.SendRpc(ConcealFrame);
        await EvidenceRetestWire.CastBarAsync(actor, Conceal, 2);
        byte[] on = AbilityBuffs.BuildExclusive(Conceal, true), off = AbilityBuffs.BuildExclusive(Conceal, false);
        await ObserveAsync(actor, activated, rpc => rpc.Bytes.AsSpan().SequenceEqual(on), "Concealment is active");
        await ObserveAsync(actor, activated, rpc => Bonus(rpc, Conceal, 209, Level, 0, false), "Concealment adds Stealth +19");

        await MoveAsync(blocked);
        await RefusedAsync(actor, Shot, shot, AbilityRefusals.TargetNotInSight);
        await MoveAsync(far);
        await RefusedAsync(actor, Shot, shot, AbilityRefusals.TargetOutOfRange);
        Check.That(!actor.World.RpcSnapshot().Skip(activated).Any(rpc => rpc.Bytes.AsSpan().SequenceEqual(off)),
            "The two geometry refusals preserve the same 1137 activation");
        await MoveAsync(pick);
        int castAt = actor.World.RpcSnapshot().Count;
        int hitAt = enemy.World.RpcSnapshot().Count;
        var hit = AbilityMessages.Damage(false, Shot, actor.Handle, enemy.Handle, 23, 0);
        actor.SendRpc(shot);
        await ObserveAsync(actor, castAt, rpc => rpc.Bytes.AsSpan().SequenceEqual(off), "The legal 504 consumes Concealment");
        Check.That(!enemy.World.RpcSnapshot().Skip(hitAt).Any(rpc => rpc.Bytes.AsSpan().SequenceEqual(hit)),
            "Concealment is consumed before the damage lands");
        await EvidenceRetestWire.CastBarAsync(actor, Shot, 6);
        await ObserveAsync(enemy, hitAt, rpc => rpc.Bytes.AsSpan().SequenceEqual(hit), "Neutral 504 hits the original enemy for 23");
        await EvidenceRetestWire.TimerAsync(actor, castAt, Shot, AbilityCooldowns.OwnTimer, 45);
        var marker = await EffectAsync(Shot, Shot, 4, .1f);
        var stun = await EffectAsync(Shot, TempMods.StunAbility, 0, 4);
        var tactics = await EffectAsync(TempMods.StunAbility, TempMods.CombatTacticsAbility, -20, 0);
        var immunity = await EffectAsync(TempMods.ControlImmunityTimerAbility, TempMods.ISDrainTargetAbility, 0, 4);
        Check.That(marker.RecordId < stun.RecordId && stun.RecordId < tactics.RecordId && tactics.RecordId < immunity.RecordId,
            "Concealed Shot registers the terminal marker, Stun, linked tactics and independent immunity in order");
        foreach (var record in new[] { marker, stun, tactics, immunity })
        {
            var removal = record with { AddRemoveType = 1 };
            await ObserveAsync(enemy, hitAt, rpc => ManageBonusRecord.TryDecode(rpc.Bytes, out var decoded) && decoded == removal,
                $"The original {record.SourceId}/{record.TargetId} record is removed");
        }
        await ObserveAsync(actor, castAt, rpc => rpc.Bytes.AsSpan().SequenceEqual(AbilityMessages.AboutAbility(
            AbilityMessages.StunCaster, true, Shot, actor.Handle, enemy.Handle, 0)), "Caster sees the Concealed Shot Stun notice");
        await ObserveAsync(actor, castAt, rpc => rpc.Bytes.AsSpan().SequenceEqual(AbilityMessages.AboutAbility(
            AbilityMessages.ImmunityCaster, true, Shot, actor.Handle, enemy.Handle, 4)), "Caster sees the four-second immunity notice");
        var innerStrength = actor.World.ViewSnapshot().SelectMany(AbilityParityCoderSweepMode.InnerStrengthUpdates).ToArray();
        Check.That(innerStrength.Contains(ConcealedIs) && innerStrength.Contains((ushort)(ConcealedIs - 45)),
            "The original stealth cast spends the current 20 IS and only the final admitted shot debits another 45 IS");
        Check.Equal(0, actor.World.Problems.Count + enemy.World.Problems.Count, "Concealed Shot targeting protocol framing");
        Console.WriteLine("CONCEALED SHOT PASS: retained cast delivered the 0.1 s marker, 4 s Stun and immunity, linked -20 tactics, and matching removals.");
        return Array.Empty<byte>();

        async Task<ManageBonusRecord> EffectAsync(ushort source, ushort stat, int value, float seconds)
        {
            var frame = await ObserveAsync(enemy, hitAt, rpc => Bonus(rpc, source, stat, value, seconds, false),
                $"Concealed Shot publishes {source}/{stat} {value} for {seconds} s");
            ManageBonusRecord.TryDecode(frame.Bytes, out var record);
            return record!;
        }

        async Task MoveAsync(Teleport location)
        {
            actor.SkipReceived();
            actor.SendChat(location.Command);
            await actor.WaitForTextAsync(location.Confirmation);
        }
    }

    private static void PrintStand(string name, Teleport stand)
    {
        string[] words = stand.Command.Split(' ');
        Console.WriteLine($"READY {name} type '{string.Join(' ', words[..3])}', then ' {words[3]} {words[4]} {words[5]} {words[6]}', then ' {words[7]} {words[8]}'; press Enter once.");
    }

    internal static Teleport Stand(int x, int z)
    {
        Check.That(NavMesh.Locate(1, x, Y, z, out var floor) && !floor.indoors && Math.Abs(floor.floorY + 95 - Y) <= 100,
            $"Verified outdoor NAV ({x},{Y},{z})");
        byte yaw = MobAggro.YawTowards(x, z, TargetX, Z);
        return new($"?tp {x} {Y} {z} {floor.sector:x} {floor.region:x} {floor.polygon:x} - {yaw:x2}",
            $"Teleported to {floor.sector:x}/{floor.region:x}");
    }

    internal static Teleport FindFarStand()
    {
        for (int distance = 3100; distance <= 3900; distance += 200)
        foreach (var (dx, dz) in new[] { (-1, 0), (0, -1), (0, 1), (1, 0) })
        {
            int x = TargetX + dx * distance, z = Z + dz * distance;
            if (NavMesh.Locate(1, x, Y, z, out var floor) && !floor.indoors && Math.Abs(floor.floorY + 95 - Y) <= 100 &&
                NavMesh.HasSightPath(1, x, Y, z, TargetX, Y, Z))
                return Stand(x, z);
        }
        throw new InvalidOperationException("Concealed Shot needs an outdoor clear-sight stand beyond its shipped 30 m limit.");
    }

    internal sealed record Teleport(string Command, string Confirmation);
}
