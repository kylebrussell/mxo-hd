using hds;
using HeadlessClient;
using MxoHd.PacketResearch;
using static NativeQa.StockAbilityProfile;

namespace NativeQa.Modes;

/// <summary>
/// Presentation probe for the instant sneak openers' named moves (ILDB 19590–19595). <c>?openermove &lt;id&gt; &lt;a|b|c&gt;</c>
/// sends the stock client one candidate presentation without casting, cost, damage or state change: a = one Close Combat
/// round naming the landed move, b = an at-rest targeted named action, c = the Failed Sneak control round.
/// </summary>
public sealed class OpenerMoveProbeMode : NativeQaMode
{
    private const int Level = 50, X = 21550, Y = 1095, Z = 16150, EnemyX = 21750;
    private const int DummyX = 17000, DummyY = 495, RearZ = 6448;
    private const ushort Spy = CombatStyles.SpyStyle;
    private static readonly ushort[] Loadout = DisablingShotMode.Closure(2, 17, Spy, 151, 286, 288, 289, 385, 387, 388);

    public override string Name => "ability-parity-opener-move-probe";

    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), Array.Empty<string>(), new[]
    {
        PvpOpponentsFixture.NativeSql,
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y={Y},alignment=1,pvpflag=0 WHERE charId IN (110,6111,6114)",
        $"UPDATE characters SET x={X},z={Z},rotation=64,healthC=5000,healthM=5000,innerStrC=1000,innerStrM=1000 WHERE charId=110",
        $"UPDATE characters SET x={EnemyX},z={Z},rotation=64,alignment=2,healthC=5000,healthM=5000 WHERE charId=6114",
        $"UPDATE char_abilities SET level={Level} WHERE char_id=110",
        "DELETE FROM char_abilities WHERE char_id IN (6111,6114)",
        $"DELETE FROM inventory WHERE charId IN (110,6111,6114) AND slot={CombatFixtureWeapons.HandSlot}"
    }, DevCommands: true, hotbarAbilities: new ushort[] { Spy },
        testMobFixture: "wounding-throw.csv",
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0",
            ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            [CombatFixtureWeapons.DisengageMode] = "1",
            [OpenerMoveProbe.Mode] = "1"
        },
        extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "286,288,289,385,387,388",
            ["NativeCloseableRows"] = 0,
            ["MemoryUsed"] = Loadout.Sum(AbilityMemory.Cost)
        },
        readyMessages: new[]
        {
            $"Level {Level}. HOME ({X},{Y},{Z}) is 2 m behind QAEnemy ({EnemyX},{Y},{Z}, facing east); no initial tp. Page 2: 1=Spy Style. Nothing is cast: ?openermove <id> <a|b|c> only sends a candidate presentation to the stock client (no IS, damage, timer or state). Ids and the ILDB move named: 286 Twist Throw 19593, 288 Topsy Turvy 19594, 289 Punt 19590, 385 Diving Frankensteiner 19592, 387 Running Bulldog 19591, 388 Reverse Pulldown 19595; control c = Failed Sneak v Self-Defense 19603.",
            "Variant b (at rest, for each of the six ids): /target QAEnemy, type ?openermove <id> b. Record whether CSR plays a full-body named move toward QAEnemy (vs nothing, a generic stance, or the knife throw).",
            "Variants a and c (for each id): select Spy Style (slot 1), /target QAEnemy and open Close Combat. Type ?openermove <id> a: one extra round names the move, and the probe disengages after its duration. Record whether both fighters play the named full-body move. Reopen Close Combat (and reselect slot 1: ?cd stop and disengage drop Spy Style) and type ?openermove <id> c to see the Failed Sneak control. Note anything odd (pose snap, frozen fighters, client error).",
            "Report per id and variant: named move played (yes/no/partial), what was seen instead. If a variant clearly plays the named move, a follow-up PR can wire the real openers to that presentation."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        SectorProperties.Load(Path.Combine(context.RepositoryRoot!, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(context.RepositoryRoot!, "data", "navmesh"));
        Check.That(NavMesh.Locate(1, DummyX, DummyY, RearZ, out var stand) && !stand.indoors, "Verified outdoor dummy stand");
        string dummyStand = $"?tp {DummyX} {DummyY} {RearZ} {stand.sector:x} {stand.region:x} {stand.polygon:x} - 00";

        var server = context.ActualServer!;
        var enemy = await HeadlessPlayer.LoginAsync(context.Address, "QAEnemy", "QAEnemy", stockCharacterLoad: true);
        context.Helpers.Add("QAEnemy", enemy);
        await PvpOpponentsFixture.EnterAsync(enemy);
        if (context.StockProbe is not { } caster) return Array.Empty<byte>();
        DisablingShotMode.CheckProfile(caster, Loadout, Level);
        await PvpOpponentsFixture.EnterAsync(caster);
        NativeQaReadiness.RequireNoAmbientAggro(server, caster);

        // b: the at-rest named action reaches the caster.
        var seen = await caster.WaitForViewNamedAsync(enemy.Handle);
        uint handle = DuelPackets.Handle(seen.ViewId, seen.SpawnId);
        caster.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(handle))}");
        await Task.Delay(TimeSpan.FromSeconds(1));
        int log = server.DebugLog().Length;
        caster.SendChat("?openermove 286 b");
        await caster.WaitForTextAsync("Probe b sent: named action 19593 (Twist Throw).");
        Check.That(server.DebugLog()[log..].Contains("[OPENER-PROBE] 286 variant b move 19593 (Twist Throw) at rest", StringComparison.Ordinal), "Probe b logs its move");
        // The record after its counter: stance Aggro, 25 <19593>, ability 286, the receiver's target handle.
        byte[] tail = OpenerMoveProbe.BuildNamedMove(false, 0, 19593, 286, handle)[3..];
        await caster.WaitUntilAsync(() => caster.World.ViewSnapshot().Any(v => v.AsSpan().IndexOf(tail) >= 0),
            "Own named-action record", HeadlessPlayer.DefaultTimeout);
        Console.WriteLine("READY BOOTSTRAP probe b: own named-action record for 286 / 19593 (observers get the same record through their view of CSR)");

        // a and c: one presentation round in a live Close Combat view, then the probe's disengage.
        caster.SkipReceived();
        caster.SendChat(dummyStand);
        await caster.WaitForTextAsync($"Teleported to {stand.sector:x}/{stand.region:x}");
        await Task.Delay(TimeSpan.FromSeconds(3));
        var dummy = caster.World.CreationSnapshot().Last(v => v.Name == CombatFixtureLoadout.DummyName);
        foreach (var (variant, move, name) in new[] { ('a', 19593, "Twist Throw"), ('c', 19603, "Failed Sneak v Self-Defense") })
        {
            await StarterInterlockDamageMode.Open(caster, dummy);
            log = server.DebugLog().Length;
            caster.SendChat($"?openermove 286 {variant}");
            await caster.WaitUntilAsync(() => server.DebugLog()[log..].Contains($"animation {move} ({name})", StringComparison.Ordinal) &&
                server.DebugLog()[log..].Contains($"[OPENER-PROBE] 286 variant {variant} move {move}", StringComparison.Ordinal),
                $"Probe {variant} round names {move}", TimeSpan.FromSeconds(8));
            await Task.Delay(TimeSpan.FromSeconds(4));
            Console.WriteLine($"READY BOOTSTRAP probe {variant}: Close Combat round animation {move} ({name}), then disengage");
        }

        Check.Equal(0, caster.World.Problems.Count + enemy.World.Problems.Count, "Clean stock framing");
        Console.WriteLine("OPENER MOVE PROBE PASS: variants a, b and c reach the client without cost, damage or state.");
        return Array.Empty<byte>();
    }
}
