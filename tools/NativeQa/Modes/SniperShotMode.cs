using hds;
using HeadlessClient;
using MxoHd.PacketResearch;
using static NativeQa.StockAbilityProfile;
using static NativeQa.Modes.StarterHackerControlTargetMode;
using static NativeQa.Modes.StarterConcealedShotTargetingMode;

namespace NativeQa.Modes;

public sealed class SniperShotMode : NativeQaMode
{
    private const int Level = 26, X = 21550, TargetX = 21750, BlockedX = 22350, Y = 1095, Z = 16150;
    private const ushort Rifles = CombatStyles.Rifles, Conceal = Stealth.ConcealmentGrantAbility,
        Baseline = CombatSpecials.ConcealedShot, Shot = CombatSpecials.SniperShot;
    // PR330 stock Rifle style capture as reused in GunStyleOpeningBatchMode.
    private const string RiflesFrame = "80b99300040002000000000000000000000000000000000000000000d557377c20000000086b0000000000";
    private static readonly ushort[] Loadout = new ushort[] { 2, 17, Rifles, 503, Baseline, Shot }
        .SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys.Append(id)).Distinct().Order().ToArray();
    private static readonly ushort[] EnemyLoadout = DisablingShotMode.Closure(2, 17, 154);
    private static readonly int Memory = Loadout.Sum(AbilityMemory.Cost);

    public override string Name => "ability-parity-sniper-shot";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(),
        new[] { CombatFixtureLoadout.SeedWeaponsFor(CombatFixtureWeapons.Rifle) },
        new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y={Y}," +
                "rotation=64,alignment=1,pvpflag=0,innerStrC=500,innerStrM=500 WHERE charId=110",
            $"UPDATE char_abilities SET level={Level} WHERE char_id=110",
            $"UPDATE characters SET x={TargetX},y={Y},z={Z},rotation=64,alignment=2,level={Level}," +
                $"exp={PlayerProgression.ExperienceForLevel(Level)},pvpflag=0,healthC=1000,healthM=1000 WHERE charId=6114",
            "DELETE FROM char_abilities WHERE char_id=6114",
            "INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES " +
                string.Join(",", EnemyLoadout.Select((id, index) => $"(6114,{400 + index},{AbilityMessages.AbilityGameObject(id)},{Level},1)"))
        }, DevCommands: true, hotbarAbilities: new[] { Rifles, Conceal, Shot, Baseline },
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1",
            ["MXO_PVP_MAX_SAFE_LEVEL"] = "15", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "505", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            $"Level {Level}, legal {Memory}/{AbilityMemory.ForLevel(Level)} memory and a rifle equipped. " +
                $"CSR PICK ({X},{Y},{Z}) faces QAEnemy ({TargetX},{Y},{Z}), outside ambient effects. Page 2 Rifles/Concealment/Sniper Shot/Concealed Shot.",
            "Select Rifles and QAEnemy at PICK. Try slot 3 while exposed. Expect a local Concealment refusal or reason 23 if it sends. Then activate slot 2. " +
                "Keep the selection without Escape. Try 505 from the printed BLOCK then the printed FAR. Expect sight/range refusal or reason 9/34, with no shot debit, cast or timer.",
            "Return to PICK. Keep 1137 active and press slot 3. Capture the complete direct stock 505 request. Expect 1137 off at cast start, 60 IS (480 to 420), " +
                "6 s bar, 51 neutral Ballistic damage (base 47 with Rifles +8 and Firearms Expert +2 influence), and 45 s own reuse. A completed roll may apply Stun for 4 s. The helper PacketLog should show the 505 marker retiring before Stun 102, linked Combat Tactics 163 -20, and the separate 4 s immunity 734/681.",
            """After reuse, send {"actor":"QAEnemy","action":"sniper-armor"} and confirm source1061 stat181 +20. While still outside combat, activate Concealment and repeat at PICK. Expect [TYPED-RESISTANCE OURS] 505 type1 total20 raw51 final46. Retry after reuse if neither cast rolled Stun; its ordinary chance is 50%. Record the normal cast/release presentation and verify no interlock round is opened.""",
            "Slot 4 is the retained Concealed Shot control. Capture stock frames and visible presentation on 505 itself."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        var pick = Stand(X, Z);
        var blocked = Stand(BlockedX, Z);
        var far = FindFarStand();
        _ = Stand(TargetX, Z);
        Check.That(NavMesh.HasSightPath(1, X, Y, Z, TargetX, Y, Z) &&
            !NavMesh.HasSightPath(1, BlockedX, Y, Z, TargetX, Y, Z), "Verified near sight and blocked geometry");
        Console.WriteLine($"PICK {pick.Command}");
        Console.WriteLine($"BLOCK {blocked.Command}");
        Console.WriteLine($"FAR {far.Command} (over 30 m, clear sight)");

        var enemy = await HeadlessPlayer.LoginAsync(context.Address, "QAEnemy", "QAEnemy", stockCharacterLoad: true);
        context.Helpers.Add(enemy.Handle, enemy);
        await PvpOpponentsFixture.EnterAsync(enemy);
        DisablingShotMode.CheckProfile(enemy, EnemyLoadout, Level);
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, enemy);
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        var loaded = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor), row => row.Level);
        Check.That(Memory == 46 && Memory <= AbilityMemory.ForLevel(Level) && loaded.Keys.ToHashSet().SetEquals(Loadout) &&
            Loadout.All(id => AbilityPrerequisites.Current.Closure(id).All(required =>
                loaded.TryGetValue(required.Key, out ushort rank) && rank >= required.Value)), "Exact, legal L26 stock profile");
        await PvpOpponentsFixture.EnterAsync(actor);
        await CombatFixtureLoadout.VerifyWeaponAtLoginAsync(actor, CombatFixtureWeapons.Rifle);
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, actor);

        var target = await actor.WaitForViewNamedAsync(enemy.Handle);
        uint handle = DuelPackets.Handle(target.ViewId, target.SpawnId);
        int styleAt = actor.World.RpcSnapshot().Count;
        actor.SendRpc(RiflesFrame);
        await ObserveAsync(actor, styleAt, rpc => Bonus(rpc, Rifles, 171, 8, 0, false), "Captured Rifles gives +8 Ballistic Damage");
        await ObserveAsync(actor, 0, rpc => Bonus(rpc, 125, 1139, 2, 0, false), "Loaded Firearms Expert gives +2 linked Ballistic Damage");
        actor.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(handle))}");
        int concealAt = actor.World.RpcSnapshot().Count;
        actor.SendRpc(ConcealFrame);
        await EvidenceRetestWire.CastBarAsync(actor, Conceal, 2);
        byte[] on = AbilityBuffs.BuildExclusive(Conceal, true), off = AbilityBuffs.BuildExclusive(Conceal, false);
        await ObserveAsync(actor, concealAt, rpc => rpc.Bytes.AsSpan().SequenceEqual(on), "Captured Concealment becomes active");

        // 505 has no complete archive frame. Use the previously captured 504 path as the real protocol control.
        int castAt = actor.World.RpcSnapshot().Count, targetAt = enemy.World.RpcSnapshot().Count;
        actor.SendRpc(WithHandle(ShotFrame, handle));
        await ObserveAsync(actor, castAt, rpc => rpc.Bytes.AsSpan().SequenceEqual(off), "Captured 504 consumes Concealment");
        await EvidenceRetestWire.CastBarAsync(actor, Baseline, 6);
        byte[] damage = AbilityMessages.Damage(false, Baseline, actor.Handle, enemy.Handle, 24, 0);
        await ObserveAsync(enemy, targetAt, rpc => rpc.Bytes.AsSpan().SequenceEqual(damage), "Captured 504 baseline lands for 24");
        await EvidenceRetestWire.TimerAsync(actor, castAt, Baseline, AbilityCooldowns.OwnTimer, 45);
        await PistolSlideMode.ApplyArmorAsync(enemy);
        Check.Equal(0, actor.World.Problems.Count + enemy.World.Problems.Count, "Sniper stock control framing");
        return Array.Empty<byte>();
    }
}
