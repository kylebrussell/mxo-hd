using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class HackerRenewRsiMode : NativeQaMode
{
    internal const ushort Renew = 438;
    private const int Level = 41;
    // The level-50 Trauma Surgeon discipline is an owned Actions folder, not a loaded prerequisite.
    private static readonly ushort[] Loadout = new ushort[] { 2, 17, CombatStyles.HackerStyle, Renew }
        .SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys.Append(id)).Distinct()
        .Order().ToArray();

    public override string Name => "ability-parity-hacker-renew-rsi";
    public override NativeQaModePlan Plan { get; } = new(Loadout, HackerRevitalizeReviveMode.X, HackerRevitalizeReviveMode.Z,
        Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110" },
        HackerRevitalizeReviveMode.CorpseStandSql(Level),
        DevCommands: true, hotbarAbilities: new[] { CombatStyles.HackerStyle, Renew },
        testMobFixture: "martial-player-controls.csv", environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0",
            ["MXO_MOB_WANDER"] = "0",
            ["MXO_PVP_SERVER"] = "0",
            ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1",
            ["MXO_HEALTH_PARITY_QA"] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "438", ["NativeCloseableRows"] = 1 },
        readyMessages: new[]
        {
            "Level 41, 50/82 Archive. Verified outdoor Mara NAV: CSR (17000,495,6400) faces the downed QAFriend (17200,495,6400). Page 2 slots 1/2 are Hacker Style and Renew RSI.",
            "Use Hacker Style, /target QAFriend, then Renew. Save the complete first 438 stock request. Expect a 2 s cast, IS 500→350, its own 15 s reuse, and an 81 88 offer naming 438. QAFriend remains down with the offer pending.",
            """Send {"actor":"QAFriend","action":"approve","accept":true}. Expect restored HP=777 IS=333 and an in-place stand with action 63 in Revive RSI 375 motion context. The helper logs ability 438 and motion 375. The caster and recipient result messages name Renew RSI (438). No relocation or PvP flag in this unflagged case."""
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var helper = await HackerRevitalizeReviveMode.PrepareCorpseAsync(context, Loadout, Level);
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        Check.Equal(50, Loadout.Sum(AbilityMemory.Cost), "Fully qualified Renew RSI memory");
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, actor);
        actor.SendRpc(StarterHackerViralSightMode.StockStyle);
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 ee 03 00 00");
        Check.Equal(0, actor.World.Problems.Count + helper.World.Problems.Count, "Renew fixture stock bootstrap framing");
        Console.WriteLine("RENEW RSI BOOTSTRAP PASS: legal Archive, both NAV stands, helper down, and retained Hacker Style activation.");
        return Array.Empty<byte>();
    }
}
