using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class StarterExecuteProgramConcentrationMode : StarterConcentrationBatchMode
{
    private const int X = 24800, Y = 495, Z = -1498, TargetZ = -1798;
    private const ushort Execute = 45, Insidious = 360, Tinkering = 36, Hacker = 1006, Coder = 1008;
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, Execute, Insidious, Tinkering, Hacker, Coder)
        .Order().ToArray();

    protected override (ushort Ability, ushort Stat, int Value) Passive => (Execute, 1090, 5);
    protected override string BaselineRoll => "concentration=0 through=75 roll=76";
    protected override string LoadedRoll => "concentration=5 through=77 roll=76";

    public override string Name => "ability-parity-starter-execute-program-concentration";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), Array.Empty<string>(),
        new[]
        {
            PvpOpponentsFixture.NativeSql,
            $"UPDATE characters SET x={X},y={Y},z={Z},rotation={MobAggro.YawTowards(X,Z,X,TargetZ)} WHERE charId=110",
            $"UPDATE characters SET x={X},y={Y},z={TargetZ} WHERE charId=6114",
            "UPDATE char_abilities SET level=50 WHERE char_id=110",
            "DELETE FROM char_abilities WHERE char_id=6114"
        }, DevCommands: true, hotbarAbilities: new[] { Hacker, Insidious, Coder, Tinkering },
        testMobFixture: "martial-player-controls.csv", environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1",
            ["MXO_PVP_MAX_SAFE_LEVEL"] = "15", ["MXO_CAST_INTERRUPT_QA_ROLL"] = "76",
            ["MXO_CONCENTRATION_QA_DAMAGE"] = "1", ["MXO_FREECAST_VIRAL_QA"] = "1",
            ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766", ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "45,360,36", ["NativeCloseableRows"] = 0, ["MemoryUsed"] = Loadout.Sum(AbilityMemory.Cost) },
        readyMessages: new[]
        {
            "Level 50 to load the interruptible control Insidious Code. QAEnemy stands 3 m straight ahead, clear of ambient NPCs. Select Hacker Style (page 2 slot 1), then QAEnemy. Stay on this stand outside interlock. Enter submits commands without Escape.",
            "Enter ?qaunload 45. Expect [MODIFIER] char=110 source=45 stat=1090 remove value=+5 duration=0s. Enter ?combatdepth hurtme 100 next and press slot 2. When stock sends the cast, expect loss 100, Concentration 0, through 75, roll 76 and a cast interruption with no completed effect or reuse. If stock greys the slot because an ancestor is unloaded, record that and continue.",
            "Enter ?qaload 45 and re-arm ?combatdepth hurtme 100 next. The +5 Concentration Bonus returns. Press slot 2. Expect loss 100, Concentration 5, through 77, roll 76. The cast completes, applies the 24 s modifiers and starts 58 s reuse.",
            "Select Coder Style (slot 3). Arm once more and use Tinkering (slot 4) on QAEnemy. Expect a six-second cast, 30 s reuse and no self-damage arm consumed."
        });
}
