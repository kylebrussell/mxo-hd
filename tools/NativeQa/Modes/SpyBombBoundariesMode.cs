using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class SpyBombBoundariesMode : NativeQaMode
{
    private const ushort Flash = SpyGroundBombs.FlashBomb, Smoke = SpyGroundBombs.SmokeBomb;
    private const int Level = 50, X = 17000, Y = 495, Z = 6400, CentreZ = 6800;
    private const string Home = "?tp 17000 495 6400 4e2 12 4 - 00";
    private static readonly ushort[] Loadout = DisablingShotMode.Closure(2, 17, CombatStyles.SpyStyle, Flash, Smoke)
        .Order().ToArray();
    private static readonly (string Handle, int Id, int X, int Z, int Org)[] Witnesses =
    {
        ("QAFriend", 6111, X, CentreZ, 1),
        ("QAEnemy", 6114, X + 100, CentreZ + 100, 2),
        ("QAOther", 6113, X - 400, CentreZ, 2),
        ("QAFar", 6112, X, CentreZ + 600, 2)
    };

    public override string Name => "ability-parity-spy-bomb-boundaries";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), new[]
        {
            $"DELETE FROM inventory WHERE charId=110 AND goid={SpyGroundBombs.InvisibilityBeltGoid}"
        }, new[] { PvpOpponentsFixture.NativeSql,
            $"UPDATE characters SET x={X},y={Y},z={Z},rotation=0 WHERE charId=110" }
            .Concat(Witnesses.Select(w => $"UPDATE characters SET x={w.X},y={Y},z={w.Z},alignment={w.Org},rotation=128 WHERE charId={w.Id}"))
            .ToArray(), DevCommands: true,
        hotbarAbilities: new ushort[] { CombatStyles.SpyStyle, Flash, Smoke },
        testMobFixture: "martial-player-controls.csv",
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1",
            ["MXO_PVP_MAX_SAFE_LEVEL"] = "15", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1",
            [AreaTargetDiagnostics.Variable] = "1", [SpyGroundBombs.FlashProcQaMode] = "1"
        },
        extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "107,364", ["NativeCloseableRows"] = 2,
            ["LoadedMemory"] = Loadout.Sum(AbilityMemory.Cost), ["MemoryCapacity"] = AbilityMemory.ForLevel(Level)
        },
        readyMessages: new[]
        {
            $"CSR starts at ({X},{Y},{Z}) facing +Z. HOME fallback {Home}. Page 2 Spy, Flash Bomb, Smoke Bomb. Target the ground by QAFriend's feet, not the avatar. Aim centre ({X},{Y},{CentreZ}); PVP witnesses are on visible NAV. QAEnemy 1.41 m, QAOther 4 m, QAFar 6 m from the nominal centre. Nobody needs to be selected; strafe to refresh views if needed.",
            "Select Spy. Press Flash slot 2 and click the ground. Preserve the full stock 80 b9 6b 00. Fit the click within 0.5 m of QAFriend (use logged horizontal radius distances to validate); re-aim after reuse if the distances straddle the checkpoints. Expect 25 IS, 2 s cast, 58/3 s reuse, [BOMB] 107 radius=5 players=2 and [HOSTILE][AREA-QA] included QAEnemy and QAOther, pvp-NotOpponent QAFriend, radius QAFar.",
            "QA-only success aid is limited to CSR Flash modifiers on QAEnemy. Verify [EFFECTS] 107 on QAEnemy with states 90/199/200/201 for 12 s. Save the four recipient adds and removals, the caster's Blind line and visible flash. QAOther is always Blinded, with ordinary 15 percent state rolls. No damage, no damage-break or Stun/Pacify immunity. Wait for removals before Smoke.",
            "Press Smoke slot 3 and click the same centre. Preserve the full stock 80 b9 6c 01. Expect 120 IS, 1.5 s cast, 60 s reuse, radius=3 and QAEnemy only. State 90 lasts 20 s and the caster sees the Blind line. There is no Belt in the initial archive/bag: expect the missing-Belt message at the callback, no 1116, and normal visibility.",
            "After Smoke reuse, use ?additem 460, verify an Invisibility Belt arrived, then repeat Smoke from the same HOME and nominal click. Expect the same recipient/20 s Blind and cost. The callback starts 1116 after about 0.5 s, with 115 -300 and 881 for 130 s, pose 1a and the same enemy still in sight when cast. Ally sees CSR. Record removal and the return to standing. Prior stock NPC pass PR546 carries over; neither bomb should start interlock."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        var stands = Witnesses.Select(w => (w.X, w.Z)).Append((X, Z));
        foreach (var (x, z) in stands)
            Check.That(NavMesh.Locate(1, x, Y, z, out var stand) && stand.floorY + 95 == Y &&
                !stand.indoors && NavMesh.HasSightPath(1, X, Y, CentreZ, x, Y, z) &&
                NavMesh.HasSightPath(1, X, Y, Z, x, Y, z), $"Clear bomb witness NAV ({x},{Y},{z})");
        Check.That(NavMesh.Locate(1, X, Y, Z, out var home) &&
            (home.sector, home.region, home.polygon) == (0x4e2, 0x12, 4), "HOME matches NAV");

        foreach (var witness in Witnesses)
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, witness.Handle, witness.Handle, stockCharacterLoad: true);
            context.Helpers.Add(helper.Handle, helper);
            await PvpOpponentsFixture.EnterAsync(helper);
            NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, helper);
        }
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        DisablingShotMode.CheckProfile(actor, Loadout, Level);
        await PvpOpponentsFixture.EnterAsync(actor);
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, actor);
        foreach (var witness in Witnesses)
        {
            var view = await actor.WaitForViewNamedAsync(witness.Handle);
            PvpOpponentsFixture.Observe(view, (byte)witness.Org, (uint)witness.Id);
        }
        actor.SendRpc(AwakenedStockStealthSpeedMode.SpyUse);
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 58 04 00 00");
        Check.Equal(0, actor.World.Problems.Count + context.Helpers.Values.Sum(h => h.World.Problems.Count), "Stock framing");
        return Array.Empty<byte>();
    }
}
