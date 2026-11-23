using hds;
using HeadlessClient;
using MxoHd.PacketResearch;
using static NativeQa.Modes.StarterHackerControlTargetMode;
using static NativeQa.StockAbilityProfile;

namespace NativeQa.Modes;

public sealed class PistolAerialMode : NativeQaMode
{
    private const ushort Aerial = 243, Slide = 241, Handguns = CombatStyles.Handguns, Soldier = 154, Upgrade = 1061;
    private const int Level = 25, X = 17000, Y = 495, CasterZ = 6400, EnemyZ = 6600, OtherX = 17100;
    private static readonly ushort[] Loadout = DisablingShotMode.Closure(2, 17, Handguns, Slide, Aerial);
    private static readonly ushort[] EnemyLoadout = DisablingShotMode.Closure(2, 17, Soldier);
    // Retained PR330 handgun-choreography requests. The live run retains the first complete stock 243 request.
    private const string StockSlide = "80b9f100040002000000000000000000000000000000000000000000f1000000006b0000086b0000000000";
    private const string StockHandguns = "80b98100040002000000000000000000000000000000000000000000985bac0001000000086b0000000000";
    private const string StockSelfDefense = "80b911000400020000000000000000000000000000000000000000000000000000000000086b0000000000";

    public override string Name => "ability-parity-pistol-aerial";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, CasterZ, Array.Empty<string>(),
        new[] { CombatFixtureLoadout.SeedWeaponsFor(CombatFixtureWeapons.Handgun) }, new[]
        {
            PvpOpponentsFixture.NativeSql,
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y={Y},healthC=15000,healthM=15000,innerStrC=700,innerStrM=700 WHERE charId IN (110,6113,6114)",
            $"UPDATE characters SET x={X},z={CasterZ},rotation=0 WHERE charId=110",
            $"UPDATE characters SET x={X},z={EnemyZ},rotation=128 WHERE charId=6114",
            $"UPDATE characters SET x={OtherX},z={EnemyZ},rotation=128,alignment=2 WHERE charId=6113",
            $"UPDATE char_abilities SET level={Level} WHERE char_id=110",
            "DELETE FROM char_abilities WHERE char_id=6114",
            "INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES " +
                string.Join(",", EnemyLoadout.Select((id, index) => $"(6114,{400 + index},{AbilityMessages.AbilityGameObject(id)},{Level},1)"))
        }, DevCommands: true, hotbarAbilities: new[] { Handguns, Aerial },
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1",
            ["MXO_PVP_MAX_SAFE_LEVEL"] = "15", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1",
            ["MXO_CONDITIONAL_THROW_QA"] = "1", [CombatFixtureWeapons.DisengageMode] = "1",
            [StarterInterlockControlQa.PistolAerialMode] = "1"
        },
        extraQaMetadata: new() { ["ScopedStockRows"] = "243", ["NativeCloseableRows"] = 1 },
        readyMessages: new[]
        {
            $"Level {Level}; CSR {Loadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} memory, QAEnemy {EnemyLoadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)}. Outdoor NAV: CSR ({X},{Y},{CasterZ}) faces QAEnemy ({X},{Y},{EnemyZ}), with QAOther ({OtherX},{Y},{EnemyZ}) off to the side. Strafe once after login if needed. Use the equipped revolver. The fixture forces only the stored 40% Stun modifier on landed Pistol Aerial hits. Hit rolls, damage and timers stay ordinary.",
            "Select Handguns on page 2 slot 1. Select QAEnemy and try slot 2 at rest. Expect a local gate or invalid-interlock refusal with no IS or reuse. Use the target radial Close Combat, then reselect Handguns. If testing a different target mid-fight, select QAOther, try slot 2 and retain the requested handle. If a QAOther request is sent it must refuse. Reselect QAEnemy before the next step.",
            "Neutral: press slot 2 in a live QAEnemy interlock. Retain the first complete client-to-server 80 b9 f3 00. Expect 20 IS and 10 s own/5 s group 690 reuse. Expect [SPECIAL-TACTIC OURS] 243 +19, [PVP] PistolAerialAbility hits QAEnemy for 17, and 9518. A combat contest miss can retry after 10 s. A landed hit sends the 0.01 s marker on/off, then state102, its linked record, Combat Tactics163 -20 and immunity734/681. Stun and immunity last 4 s and remove using their original IDs. One Stun notice and one immunity notice appear to the caster.",
            """After the neutral Stun ends, use ?cd stop, then send {"actor":"QAEnemy","action":"aerial-armor"}. Check the +20 stock Soldier Upgrade Ballistic Resistance181. Reopen Close Combat, select Handguns and land slot 2 again. Expect [TYPED-RESISTANCE OURS] 243 type1 total20 raw17 final15, the same 9518 and the same four-second Stun stack.""",
            """After both the four-second Stun and 10 s own reuse end, stay in QAEnemy's interlock and send {"actor":"QAEnemy","action":"aerial-immune"}. Wait for its response containing the held 734/681 record ID. Use slot 2 before the 40 s hold ends. A landed hit still does 15 but only the short marker turns on/off. Expect one private caster line 'QAEnemy is immune to Pistol Aerial's Stun.' and [EFFECTS] 243 Stun suppressed. This is the fallback for the shipped caster-only lower-buff refusal. The held immunity does not refresh or disappear early. Retry a missed hit after 10 s or re-arm the hold after expiry.""",
            """Send {"actor":"QAEnemy","action":"aerial-records"} right after the immune hit and again after the 40 s hold ends. Send {"actor":"QAOther","action":"aerial-records"} as the bystander control. Compare original record IDs and exact bytes: both natural Stun stacks, the hold active after the hit and then removed once, and zero matching records on QAOther. Retain the CSR PacketLog for the stock request and caster-only notice."""
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var server = context.ActualServer!;
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (var (x, z) in new[] { (X, CasterZ), (X, EnemyZ), (OtherX, EnemyZ) })
            Check.That(NavMesh.Locate(1, x, Y, z, out var stand) && !stand.indoors && Math.Abs(stand.floorY + 95 - Y) < 1 &&
                NavMesh.HasSightPath(1, X, Y, CasterZ, x, Y, z), $"Verified outdoor NAV and sight ({x},{Y},{z})");

        var enemy = await EnterAsync("QAEnemy", X, EnemyZ);
        DisablingShotMode.CheckProfile(enemy, EnemyLoadout, Level);
        var other = await EnterAsync("QAOther", OtherX, EnemyZ);
        enemy.SendRpc("42 01 00 00 00 00 00");
        if (context.StockProbe is not { } caster) return Array.Empty<byte>();

        DisablingShotMode.CheckProfile(caster, Loadout, Level);
        await PvpOpponentsFixture.EnterAsync(caster);
        await CombatFixtureLoadout.VerifyWeaponAtLoginAsync(caster, CombatFixtureWeapons.Handgun);
        NativeQaReadiness.RequireNoAmbientAggro(server, caster);
        AssertStand(caster, X, CasterZ);
        var view = await caster.WaitForViewNamedAsync(enemy.Handle);
        _ = await caster.WaitForViewNamedAsync(other.Handle);
        uint opponent = DuelPackets.Handle(view.ViewId, view.SpawnId);
        byte[] slide = WithHandle(StockSlide, opponent);
        await RefusedAsync(caster, Slide, slide);

        string handle = WireFormat.ToHex(BitConverter.GetBytes(opponent));
        int opened = caster.World.RpcSnapshot().Count;
        caster.World.SendRpcs(WithHandle(StockSelfDefense, opponent), WireFormat.FromHex($"40 {handle} 01 6b"));
        await ObserveAsync(caster, opened, rpc => rpc.Bytes is [0x46, ..] && rpc.Bytes.Length >= 6 && rpc.Bytes[5] == 1,
            "Ordinary PvP Close Combat opens");
        int styleAt = caster.World.RpcSnapshot().Count;
        caster.SendRpc(WithHandle(StockHandguns, opponent));
        await ObserveAsync(caster, styleAt, rpc => ManageBonusRecord.TryDecode(rpc.Bytes, out var record) &&
            record is { SourceId: Handguns, TargetId: 171, ValueBits: 7, AddRemoveType: 0 },
            "The actual level-25 Handguns bonus adds +7 Ballistic Damage");
        caster.SendRpc($"81 51 {handle}");
        caster.SkipReceived();
        caster.SendChat("?cd offbalance 30");
        await caster.WaitForTextAsync("OffBalance fixture applied 885/200 to QAEnemy");
        int logAt = server.DebugLog().Length, timerAt = caster.World.RpcSnapshot().Count;
        caster.SendRpc(slide);
        await EvidenceRetestWire.TimerAsync(caster, timerAt, Slide, AbilityCooldowns.OwnTimer, 10);
        await EvidenceRetestWire.TimerAsync(caster, timerAt, 690, AbilityCooldowns.GroupTimer, 5);
        await caster.WaitUntilAsync(() => server.DebugLog()[logAt..].Contains("[SPECIAL-TACTIC OURS] ability=241 bonus=29 ", StringComparison.Ordinal),
            "The retained 241 reaches the existing adjacent Handguns interlock contest", HeadlessPlayer.DefaultTimeout);

        caster.SendChat("?cd stop");
        await caster.WaitForTextAsync("Fixture combat ended.");
        await PistolSlideMode.ApplyArmorAsync(enemy);
        int immuneAt = enemy.World.RpcSnapshot().Count;
        ManageBonusRecord immune = await HoldImmunityAsync(enemy);
        var removal = immune with { AddRemoveType = 1 };
        await enemy.WaitUntilAsync(() => enemy.World.RpcSnapshot().Skip(immuneAt)
            .Any(frame => ManageBonusRecord.TryDecode(frame.Bytes, out var record) && record == removal),
            "The original immunity hold record expires",
            TimeSpan.FromSeconds(StarterInterlockControlQa.AerialImmunitySeconds + 6));
        Check.That(Records(other).Length == 0, "The off-target helper sees no own Pistol Aerial or immunity record");
        Check.Equal(0, caster.World.Problems.Count + enemy.World.Problems.Count + other.World.Problems.Count,
            "Retained 241 support and Pistol Aerial fixture transport");
        Console.WriteLine("PISTOL AERIAL BOOTSTRAP PASS: real stands, legal profile, retained Handguns/Close Combat/241, actual armor and isolated immunity hold.");
        return Array.Empty<byte>();

        async Task<HeadlessPlayer> EnterAsync(string name, int x, int z)
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, name, name, stockCharacterLoad: true);
            context.Helpers.Add(name, helper);
            await PvpOpponentsFixture.EnterAsync(helper);
            NativeQaReadiness.RequireNoAmbientAggro(server, helper);
            AssertStand(helper, x, z);
            return helper;
        }
    }

    private static void AssertStand(HeadlessPlayer actor, int x, int z) =>
        Check.That(EngageFoesFixture.Position(actor.World.CreationSnapshot().Last(view => view.ViewId == 2))
            .SequenceEqual(new double[] { x, Y, z }), $"{actor.Handle} enters on the verified stand");

    internal static async Task<ManageBonusRecord> HoldImmunityAsync(HeadlessPlayer enemy)
    {
        Check.Equal(6114u, enemy.CharId, "Only QAEnemy receives the isolated immunity hold");
        int cursor = enemy.World.RpcSnapshot().Count;
        enemy.SendChat("?cd aerial-immune");
        var frame = await ObserveAsync(enemy, cursor, rpc => Bonus(rpc, TempMods.ControlImmunityTimerAbility, TempMods.ISDrainTargetAbility,
            0, StarterInterlockControlQa.AerialImmunitySeconds, false), "The actual isolated immunity record is added");
        ManageBonusRecord.TryDecode(frame.Bytes, out var hold);
        return hold!;
    }

    internal static object[] Records(HeadlessPlayer actor) =>
        actor.World.RpcSnapshot().Select(frame => (frame,
            record: ManageBonusRecord.TryDecode(frame.Bytes, out var record) ? record : null))
            .Where(item => item.record is { SourceId: Aerial or TempMods.StunAbility or TempMods.ControlImmunityTimerAbility })
            .Select(item => new { item.record!.SourceId, item.record.TargetId, item.record.RecordId, item.record.SignedValue,
                item.record.Duration, item.record.AddRemoveType, Wire = item.frame.Hex }).Cast<object>().ToArray();
}
