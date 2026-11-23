using hds;
using HeadlessClient;
using static NativeQa.Modes.StarterHackerControlTargetMode;
using static NativeQa.StockAbilityProfile;

namespace NativeQa.Modes;

public sealed class FullAutoSpecialsMode : NativeQaMode
{
    private const ushort Style = CombatStyles.SubmachineGuns, FullAuto = 494, Redux = 496;
    private const int Level = 46, CasterX = 21550, EnemyX = 21750, Y = 1095, Z = 16150;
    private const uint Weapon = CombatFixtureWeapons.DualSmgs;
    private static readonly ushort[] Loadout = DisablingShotMode.Closure(2, 17, Style, FullAuto, Redux);
    private static readonly ushort[] EnemyLoadout = DisablingShotMode.Closure(2, 17, 154);
    // PR330 retained stock Actions frames. The native run captures both selected specials.
    private const string StockSelfDefense = "80b911000400020000000000000000000000000000000000000000000000000000000000086b0000000000";
    private const string StockSmg = "80b99e00040002000000000000000000000000000000000000000000d557377c20000000086b0000000000";

    public override string Name => "ability-parity-full-auto-specials";
    public override NativeQaModePlan Plan { get; } = new(Loadout, CasterX, Z, Array.Empty<string>(),
        new[] { CombatFixtureLoadout.SeedWeaponsFor(Weapon) }, new[]
        {
            PvpOpponentsFixture.NativeSql,
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y={Y},z={Z}," +
                "healthC=15000,healthM=15000,innerStrC=900,innerStrM=900 WHERE charId IN (110,6114)",
            $"UPDATE characters SET x={CasterX},rotation=64 WHERE charId=110",
            $"UPDATE characters SET x={EnemyX},rotation=192 WHERE charId=6114",
            $"UPDATE char_abilities SET level={Level} WHERE char_id=110",
            "DELETE FROM char_abilities WHERE char_id=6114",
            "INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES " +
                string.Join(",", EnemyLoadout.Select((id, i) => $"(6114,{400 + i},{AbilityMessages.AbilityGameObject(id)},{Level},1)"))
        }, DevCommands: true, hotbarAbilities: new[] { Style, FullAuto, Redux }, environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1", ["MXO_CONDITIONAL_POWERLESS_QA"] = "1",
            [CombatFixtureWeapons.DisengageMode] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "494,496", ["NativeCloseableRows"] = 2 }, readyMessages: new[]
        {
            $"Level {Level}, {Loadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} memory, full prerequisites, dual SMGs already in hand. Page 2 Submachine Guns / Full Auto / Full Auto Redux. Outdoor NAV CSR ({CasterX},{Y},{Z}) faces QAEnemy ({EnemyX},{Y},{Z}) at 2 m. Stay on these stands. Use /target QAEnemy, radial Close Combat and reselect slot 1 after the first round. Double-click starts Free Fire.",
            "Before setting target states, try slots 2 and 3. Stock suppression or reason 22 with mask 8/2 for Staggered/Dazed leaves IS and reuse unchanged. Keep QAEnemy selected. Use ?cd stagger 30 for 494 and ?cd daze 30 for 496. During a real interlock, record each full stock request, 4 s cast and queue on the same opponent only after the cast. Both get +24 tactic on the next eligible round, 10 s own and 5 s group 689. 494 costs 38 IS, lands 21084 for 153 and has ordinary 40% Enrage for 8 s. 496 costs 45 IS, lands 21085 for 187 and adds source496 stats 522 and 524 both -15 for 8 s. The paired removals return accuracy to its previous values. A non-landing uses the ordinary exchange without the ability hit or effect; repeat after reuse for one positive each.",
            """After the first two positives, use ?cd stop. Send {"actor":"QAEnemy","action":"full-auto-armor"} for stock source1061 stat181 +20. At rest on the same target, apply the appropriate Stagger/Daze and try each slot after reuse. If stock sends it, expect a 4 s direct hit without an interlock queue, [TYPED-RESISTANCE OURS] 494 type1 total20 raw153 final145 or 496 type1 total20 raw187 final177. If the client locally withholds a rest request, record its response and confirm that ability's resisted value in ordinary Close Combat instead. Save the 494 Enrage and 496 accuracy record pairs where available with the optional full-auto-records helper action."""
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var server = context.ActualServer!;
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        var stand = StarterConcealedShotTargetingMode.Stand(CasterX, Z);
        _ = StarterConcealedShotTargetingMode.Stand(EnemyX, Z);
        Check.That(NavMesh.HasSightPath(1, CasterX, Y, Z, EnemyX, Y, Z), "Verified clear outdoor Full Auto sight");
        Console.WriteLine($"CSR {stand.Command}");
        var enemy = await HeadlessPlayer.LoginAsync(context.Address, "QAEnemy", "QAEnemy", stockCharacterLoad: true);
        context.Helpers.Add(enemy.Handle, enemy);
        await PvpOpponentsFixture.EnterAsync(enemy);
        DisablingShotMode.CheckStand(enemy, EnemyX);
        DisablingShotMode.CheckProfile(enemy, EnemyLoadout, Level);
        NativeQaReadiness.RequireNoAmbientAggro(server, enemy);
        if (context.StockProbe is not { } caster) return Array.Empty<byte>();
        DisablingShotMode.CheckProfile(caster, Loadout, Level);
        await PvpOpponentsFixture.EnterAsync(caster);
        DisablingShotMode.CheckStand(caster, CasterX);
        await CombatFixtureLoadout.VerifyWeaponAtLoginAsync(caster, Weapon);
        NativeQaReadiness.RequireNoAmbientAggro(server, caster);

        var view = await caster.WaitForViewNamedAsync(enemy.Handle);
        uint target = DuelPackets.Handle(view.ViewId, view.SpawnId);
        string handle = WireFormat.ToHex(BitConverter.GetBytes(target));
        int roundAt = caster.World.RpcSnapshot().Count;
        caster.World.SendRpcs(WithHandle(StockSelfDefense, target), WireFormat.FromHex($"40 {handle} 01 6b"));
        await ObserveAsync(caster, roundAt, rpc => rpc.Bytes is [0x46, ..] && rpc.Bytes.Length >= 6 && rpc.Bytes[5] == 1,
            "Actual stock Close Combat opens the original opponent's fight");
        caster.SendRpc(WithHandle(StockSmg, target));
        foreach (var (source, stat, value) in new (ushort, ushort, int)[] { (Style, 171, 12), (125, 1139, 4), (246, 1139, 5), (491, 1139, 4) })
            await ObserveAsync(caster, 0, frame => Bonus(frame, source, stat, value, 0, false), $"Authored Ballistic influence {source}: +{value}");
        caster.SendRpc($"81 51 {handle}");
        caster.SendChat("?cd stagger 30");
        await ObserveAsync(enemy, 0, frame => Bonus(frame, 883, 201, 0, 30, false), "Selected player receives authored Stagger state 201");
        caster.SendChat("?cd daze 30");
        await ObserveAsync(enemy, 0, frame => Bonus(frame, 884, 199, 0, 30, false), "Selected player receives authored Daze state 199");
        caster.SendChat("?cd stop");
        await caster.WaitForTextAsync("Fixture combat ended");
        await PistolSlideMode.ApplyArmorAsync(enemy);
        Check.Equal((ushort)145, TypedAbilityResistance.Amount(153, Level, Level, 20), "Full Auto typed armor expectation");
        Check.Equal((ushort)177, TypedAbilityResistance.Amount(187, Level, Level, 20), "Full Auto Redux typed armor expectation");
        Check.Equal(0, caster.World.Problems.Count + enemy.World.Problems.Count, "Full Auto stock supporting framing");
        Console.WriteLine("FULL AUTO BOOTSTRAP PASS: legal dual SMG profile, exact Close Combat/style, both selected player states and stock armor.");
        return Array.Empty<byte>();
    }

    internal static object[] Records(HeadlessPlayer actor) => actor.World.RpcSnapshot()
        .Where(frame => MxoHd.PacketResearch.ManageBonusRecord.TryDecode(frame.Bytes, out var record) && record is { SourceId: FullAuto or Redux })
        .Select(frame => (object)frame.Hex).ToArray();
}
