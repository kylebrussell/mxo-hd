using System.Text.Json;
using hds;
using HeadlessClient;
using static NativeQa.StockAbilityProfile;

namespace NativeQa.Modes;

public sealed class SteadyAimMode : NativeQaMode
{
    private const ushort SteadyAim = 442, Rifles = CombatStyles.Rifles;
    private const int Level = 36, X = 17000, Y = 495, Z = 2300, EnemyX = 17500, StartingInnerStrength = 120;
    private static readonly ushort[] Loadout = DisablingShotMode.Closure(2, 17, Rifles, SteadyAim);
    private static readonly ushort[] EnemyLoadout = { 2, 17 };

    public override string Name => "ability-parity-steady-aim";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), Array.Empty<string>(), new[]
    {
        PvpOpponentsFixture.NativeSql,
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Z}," +
            $"rotation=64,healthC=15000,healthM=15000,innerStrC={StartingInnerStrength},innerStrM={StartingInnerStrength} WHERE charId=110",
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={EnemyX},y={Y},z={Z}," +
            "rotation=192,healthC=15000,healthM=15000 WHERE charId=6114",
        $"DELETE FROM inventory WHERE charId IN (110,6114) AND slot IN (107,{CombatFixtureWeapons.HandSlot})",
        "INSERT INTO inventory(charId,goid,slot,count,purity) VALUES " +
            $"(110,{CombatFixtureWeapons.Rifle},{CombatFixtureWeapons.HandSlot},1,0)," +
            $"(6114,{CombatFixtureWeapons.Rifle},{CombatFixtureWeapons.HandSlot},1,0)",
        $"UPDATE char_abilities SET level={Level} WHERE char_id=110",
        "DELETE FROM char_abilities WHERE char_id=6114",
        "INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES " +
            string.Join(",", EnemyLoadout.Select((id, index) => $"(6114,{400 + index},{AbilityMessages.AbilityGameObject(id)},{Level},1)"))
    }, DevCommands: true, hotbarAbilities: new[] { Rifles, SteadyAim },
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1",
            ["MXO_PVP_MAX_SAFE_LEVEL"] = "15", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1",
            [CombatFixtureWeapons.DisengageMode] = "1"
        },
        extraQaMetadata: new() { ["ScopedStockRows"] = "442", ["NativeCloseableRows"] = 1, ["NativeCloseableIds"] = "442" },
        readyMessages: new[]
        {
            $"Level {Level}, {Loadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} memory with full ability prerequisites. Outdoor NAV CSR ({X},{Y},{Z}) faces armed QAEnemy ({EnemyX},{Y},{Z}) at 5 m. Both have rifle 692. CSR starts at 120 IS with regen held. Stay on the same stands. Page 2 slot 1 Rifles, slot 2 Steady Aim.",
            "Select Rifles, then /target QAEnemy and use the target radial Fire!. If necessary select rifle 692 in the Free Attack weapon button first and retain that selection throughout. Record [SHARPSHOOTER OURS] CSR class55, defenderGunTrue, 5.000 m, attackBonus 48->48 as baseline. While firing and 442 off, try slot 2 once. Expect local suppression or reason 21, with no debit or reuse. End the shots with ?cd stop.",
            "Use slot 2 at rest. Steady Aim is self-only. Save the first complete stock 80 b9 ba 01. Expect 120 to 90 IS, one [MODIFIER] char110 source=442 stat=524 +20, exclusive ON, and no reuse yet. After the first [BUFF-DRAIN] at 5 s takes 90 to 60, press slot 2 again before the next tick. It switches off for zero IS, removes the matching +20 and starts one own 15 s reuse. Try once early at 60 IS, then wait for the timer to clear.",
            "Use slot 2 at rest again (60 to 30 IS), reselect QAEnemy and Fire! immediately. In the live rifle input expect attackBonus 68->68. Keep firing without changing weapons or stance. The first upkeep at 5 s spends 30 to 0, leaving 442 active. Five seconds later the shortfall deactivates 442 at unchanged 0 IS, with a 30 IS shortfall message, the matching +20 removal, exclusive OFF, and one new 15 s reuse. The first shot after release uses attackBonus 48->48 again. Stop with ?cd stop."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (int x in new[] { X, EnemyX })
            Check.That(NavMesh.Locate(1, x, Y, Z, out var stand) && !stand.indoors && stand.floorY + 95 == Y,
                $"Grounded outdoor rifle stand ({x},{Y},{Z})");
        Check.That(NavMesh.HasSightPath(1, X, Y, Z, EnemyX, Y, Z), "Clear sight for unchanged 5 m rifle inputs");

        var server = context.ActualServer!;
        var enemy = await HeadlessPlayer.LoginAsync(context.Address, "QAEnemy", "QAEnemy", stockCharacterLoad: true);
        context.Helpers.Add(enemy.Handle, enemy);
        await PvpOpponentsFixture.EnterAsync(enemy);
        await CombatFixtureLoadout.VerifyWeaponAtLoginAsync(enemy, CombatFixtureWeapons.Rifle);
        NativeQaReadiness.RequireNoAmbientAggro(server, enemy);
        Check.Equal(1L, await context.OwnedDatabase!.LongAsync($"SELECT COUNT(*) FROM characters WHERE charId=110 AND innerStrC={StartingInnerStrength}"),
            "The manual and shortage phases start with the same seeded IS");
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        DisablingShotMode.CheckProfile(actor, Loadout, Level);
        await PvpOpponentsFixture.EnterAsync(actor);
        await CombatFixtureLoadout.VerifyWeaponAtLoginAsync(actor, CombatFixtureWeapons.Rifle);
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        int cursor = actor.World.RpcSnapshot().Count;
        actor.SendRpc(DisablingShotMode.StockRifles);
        await ObserveAsync(actor, cursor, packet => Bonus(packet, Rifles, 524, 46, 0, false),
            "Retained stock Rifles publishes its level-36 Ballistic Accuracy bonus");

        actor.SendRpc("3f 69 00 10 00 00 00");
        await actor.WaitUntilAsync(() => server.DebugLog().Contains("slot 105, kind 16, goid 692", StringComparison.Ordinal),
            "The rifle is selected through the retained Free Attack frame", HeadlessPlayer.DefaultTimeout);

        var view = await actor.WaitForViewNamedAsync(enemy.Handle);
        string handle = WireFormat.ToHex(BitConverter.GetBytes(DuelPackets.Handle(view.ViewId, view.SpawnId)));
        int logAt = server.DebugLog().Length;
        actor.World.SendRpcs(WireFormat.FromHex("3d 01"), WireFormat.FromHex($"41 {handle}"), WireFormat.FromHex($"40 {handle} 00 69"));
        await actor.WaitForRpcAsync("4e 00");
        actor.SendChat("?cd stop");
        await actor.WaitForTextAsync("Fixture combat ended");
        var baseline = server.DebugLog()[logAt..].Split('\n')
            .Select(line => SharpshooterFixture.ReadInput(line.TrimEnd('\r'), actor.Handle))
            .OfType<SharpshooterFixture.Input>().ToArray();
        Check.That(baseline.Length > 0 && baseline.All(shot => shot is { Class: 55, DefenderGun: true, Modifier: 0, Before: 48, After: 48 }),
            "The baseline was an actual rifle attack against the unchanged armed target");
        Check.Equal(0, actor.World.Problems.Count + enemy.World.Problems.Count, "Stock rifle support framing");
        await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "steady-aim-baseline.json"),
            JsonSerializer.Serialize(baseline, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"STEADY AIM BOOTSTRAP PASS: retained stock Rifles and Fire!, live unadjusted attackBonus {baseline[0].Before}. The first 442 request stays with the native run.");
        return Array.Empty<byte>();
    }
}
