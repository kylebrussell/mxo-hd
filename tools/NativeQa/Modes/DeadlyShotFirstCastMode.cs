using System.Diagnostics;
using hds;
using HeadlessClient;
using MxoHd.PacketResearch;
using static NativeQa.StockAbilityProfile;

namespace NativeQa.Modes;

/// <summary>
/// mxo-web SERVER-PROPOSALS #9: the first Deadly Shot after a fresh rifle equip and a Rifles selection. Measures the
/// request-to-cast-bar (80 ac) and request-to-landing times of the first and a second at-rest 454 on QAEnemy.
/// </summary>
public sealed class DeadlyShotFirstCastMode : NativeQaMode
{
    private const ushort Shot = CombatSpecials.DeadlyShot, Rifles = CombatStyles.Rifles;
    private const int Level = 46, PickX = 21550, EnemyX = 21750, Y = 1095, Z = 16150;
    private static readonly ushort[] Loadout = DisablingShotMode.Closure(2, 17, Rifles, Shot);

    public override string Name => "ability-parity-deadly-shot-first-cast";

    public override NativeQaModePlan Plan { get; } = new(Loadout, PickX, Z, Array.Empty<string>(),
        new[] { CombatFixtureLoadout.SeedWeaponsFor(CombatFixtureWeapons.Handgun) }, new[]
        {
            PvpOpponentsFixture.NativeSql,
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y={Y}," +
                "healthC=5000,healthM=5000,innerStrC=700,innerStrM=700 WHERE charId IN (110,6114)",
            $"UPDATE characters SET x={PickX},z={Z},rotation={MobAggro.YawTowards(PickX, Z, EnemyX, Z)} WHERE charId=110",
            $"UPDATE characters SET x={EnemyX},z={Z},rotation={MobAggro.YawTowards(EnemyX, Z, PickX, Z)} WHERE charId=6114",
            $"UPDATE char_abilities SET level={Level} WHERE char_id=110",
            "DELETE FROM char_abilities WHERE char_id=6114"
        }, DevCommands: true, hotbarAbilities: new[] { Rifles, Shot }, testMobFixture: "wounding-throw.csv", environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            [CombatFixtureWeapons.WeaponMode] = "1", ["MXO_RANGED_CONTEST_QA"] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "454", ["NativeCloseableRows"] = 0 }, readyMessages: new[]
        {
            "Starts with the handgun in hand. Type ?cd w rifle (fresh rifle equip), select Rifles (slot 1), /target QAEnemy, ?rangedcontest hit, then press slot 2 (454) at once. Time the cast bar: it should appear immediately, not seconds later. Repeat after the 10 s reuse."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var server = context.ActualServer!;
        var enemy = await HeadlessPlayer.LoginAsync(context.Address, "QAEnemy", "QAEnemy", stockCharacterLoad: true);
        context.Helpers.Add(enemy.Handle, enemy);
        await PvpOpponentsFixture.EnterAsync(enemy);
        if (context.StockProbe is not { } caster) return Array.Empty<byte>();
        await PvpOpponentsFixture.EnterAsync(caster);
        NativeQaReadiness.RequireNoAmbientAggro(server, caster);

        // Fresh rifle equip, then the Rifles selection, then the selected shot straight away.
        await CombatFixtureLoadout.VerifyWeaponSwapAsync(caster, context.OwnedDatabase!, "rifle", CombatFixtureWeapons.Rifle);
        var view = await caster.WaitForViewNamedAsync(enemy.Handle);
        uint handle = DuelPackets.Handle(view.ViewId, view.SpawnId);
        caster.SendRpc(StarterHackerControlTargetMode.WithHandle(DisablingShotMode.StockRifles, handle));
        caster.SendChat("?rangedcontest hit");
        await caster.WaitForTextAsync("Ranged contest hit.");
        // mxo-web's sequence: walk to an NPC, ?weaken 1, target it, cast 454 first; then QAEnemy after the reuse.
        SectorProperties.Load(Path.Combine(context.RepositoryRoot!, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(context.RepositoryRoot!, "data", "navmesh"));
        Check.That(NavMesh.Locate(1, 17000, 495, 6200, out var stand) && !stand.indoors, "Stand 5 m south of the dummy");
        caster.SkipReceived();
        caster.SendChat($"?tp 17000 495 6200 {stand.sector:x} {stand.region:x} {stand.polygon:x} - 00");
        await caster.WaitForTextAsync($"Teleported to {stand.sector:x}/{stand.region:x}");
        await caster.WaitUntilAsync(() => caster.World.CreationSnapshot().Any(v => v.Name == CombatFixtureLoadout.DummyName), "Dummy view", HeadlessPlayer.DefaultTimeout);
        var dummy = caster.World.CreationSnapshot().Last(v => v.Name == CombatFixtureLoadout.DummyName);
        uint dummyHandle = DuelPackets.Handle(dummy.ViewId, dummy.SpawnId);
        caster.SendChat("?weaken 1");
        await Task.Delay(TimeSpan.FromSeconds(1));
        foreach (string label in new[] { "first", "second" })
        {
            if (label == "second")
            {
                await Task.Delay(TimeSpan.FromSeconds(10.5));
                caster.SkipReceived();
                var pick = StarterConcealedShotTargetingMode.Stand(PickX, Z);
                caster.SendChat(pick.Command);
                await caster.WaitForTextAsync(pick.Confirmation);
                await Task.Delay(TimeSpan.FromSeconds(2));
                view = await caster.WaitForViewNamedAsync(enemy.Handle);
                handle = DuelPackets.Handle(view.ViewId, view.SpawnId);
            }
            uint aim = label == "first" ? dummyHandle : handle;
            caster.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(aim))}");
            int rpc = caster.World.RpcSnapshot().Count, log = server.DebugLog().Length;
            var clock = Stopwatch.StartNew();
            caster.SendRpc(new AbilityUseRecord(Shot, aim, 0, 0, 0, Shot | (0x6bUL << 40), 8, 0x6b, 0, 0).Encode());
            await caster.WaitUntilAsync(() => caster.World.RpcSnapshot().Skip(rpc).Any(r => r.Bytes is [0x80, 0xac, 0xc6, 0x01, ..]),
                $"{label} 454 cast bar", TimeSpan.FromSeconds(12));
            double bar = clock.Elapsed.TotalSeconds;
            await caster.WaitUntilAsync(() => server.DebugLog()[log..].Contains("lands on QAEnemy", StringComparison.Ordinal) ||
                server.DebugLog()[log..].Contains("[ABILITY] 454 lands on view", StringComparison.Ordinal),
                $"{label} 454 lands", TimeSpan.FromSeconds(12));
            double landed = clock.Elapsed.TotalSeconds;
            Console.WriteLine(FormattableString.Invariant($"READY BOOTSTRAP {label} 454: cast bar after {bar:0.000} s, landing after {landed:0.000} s"));
            Check.That(bar < 0.5, $"{label} 454 cast bar is sent at once ({bar:0.000} s)");
            Check.That(landed < 3.0, $"{label} 454 lands after its 2 s cast ({landed:0.000} s)");
        }
        Console.WriteLine("DEADLY SHOT FIRST CAST PASS: fresh rifle equip and Rifles selection do not delay the first cast.");
        return Array.Empty<byte>();
    }
}
