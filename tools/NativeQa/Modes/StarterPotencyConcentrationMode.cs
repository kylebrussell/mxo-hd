using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class StarterPotencyConcentrationMode : StarterConcentrationBatchMode
{
    private const int X = 17000, Y = 495, Z = 2300, EnemyX = 17300, Level = 50;
    private const ushort Potency = 317, Hacker = 1006, Insidious = 360, Execute = 45;
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, Potency, Hacker, Insidious, Execute)
        .Order().ToArray();
    private static readonly (ushort Stat, int Value)[] Effects = { (1144, 11), (1140, 11), (1090, 10) };

    protected override (ushort Ability, ushort Stat, int Value) Passive => (Potency, 1090, 10);
    protected override string BaselineRoll => "concentration=5 through=77 roll=79";
    protected override string LoadedRoll => "concentration=15 through=82 roll=79";
    protected override bool CheckTinkering => false;

    public override string Name => "ability-parity-starter-potency-concentration";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), Array.Empty<string>(), new[]
    {
        PvpOpponentsFixture.NativeSql,
        $"UPDATE characters SET x={X},y={Y},z={Z},rotation=64 WHERE charId=110",
        $"UPDATE characters SET x={EnemyX},y={Y},z={Z} WHERE charId=6114",
        "DELETE FROM char_abilities WHERE char_id=6114"
    }, DevCommands: true, loadAbilityAncestors: true, hotbarAbilities: new[] { Hacker, Insidious },
        testMobFixture: "martial-player-controls.csv", environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            ["MXO_CAST_INTERRUPT_QA_ROLL"] = "79", ["MXO_CONCENTRATION_QA_DAMAGE"] = "1",
            ["MXO_FREECAST_VIRAL_QA"] = "1", ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766", ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "317,360,45", ["NativeCloseableRows"] = 1, ["MemoryUsed"] = Loadout.Sum(AbilityMemory.Cost) },
        readyMessages: new[]
        {
            $"Level {Level} to carry the interruptible reference cast. Potency is a starter passive. Full loadout {Loadout.Sum(AbilityMemory.Cost)}/100 memory. CSR ({X},{Y},{Z}) faces QAEnemy ({EnemyX},{Y},{Z}) on the clear street. Type /target QAEnemy if needed. Page 2: Hacker, Insidious Code. Stay outside interlock.",
            "Login [MODIFIER] char=110 source=317 duration=0s adds stat 1144 +11, 1140 +11 and 1090 +10. Select Hacker Style and QAEnemy. Type ?qaunload 317. All three records remove. Type ?cd hurtme 100 next and press slot 2. Expect [CAST-INTERRUPT] loss=100, fixed roll 79 interrupts the 4 s cast, no target modifiers and no reuse.",
            "Type ?qaload 317. All three records return. Re-arm ?cd hurtme 100 next and press slot 2 on the same target. The ten extra points of concentration make the same roll land. Expect a 58 s timer and 24 s target modifiers. The physical resistance inputs are checked in the focused executable test.",
            "Execute Program 45 stays loaded at stat 1090 +5 in both cases. No interlock [PVP] rounds or maximum [VITALS] changes during the comparison."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        byte[] result = await base.AfterStartAsync(context);
        if (context.StockProbe is { } caster)
        {
            await StockAbilityProfile.ObserveAsync(caster, 0,
                frame => StockAbilityProfile.Bonus(frame, Execute, 1090, 5, 0, false), "The unchanged five-point concentration control");
            var frames = caster.World.RpcSnapshot();
            foreach (var (stat, value) in Effects)
            {
                int[] expected = { 0, 1, 0 };
                int found = 0;
                foreach (var frame in frames)
                {
                    if (found < expected.Length && StockAbilityProfile.Bonus(frame, Potency, stat, value, 0, expected[found] == 1))
                        found++;
                }
                Check.Equal(expected.Length, found, $"Potency stat {stat} adds, removes and restores in order");
            }
            Console.WriteLine("POTENCY PASS: all three records add, remove and restore; only the loaded interruptible cast completes.");
        }
        return result;
    }
}
