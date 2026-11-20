using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class StarterCoderSightMode : NativeQaMode
{
    private const ushort CoderStyle = CombatStyles.CoderStyle;
    // Stock PR409 (3277, 5348) and PR452's existing samples from PR312, PR389 and PR399.
    private const string StockStyle = "80b9f003000000000000000000000000000000000000000000000000585cc70001000000086b0000000000";
    private static readonly (ushort Ability, float Reuse, string Capture)[] Samples =
    {
        (21, 13.5f, "80b915000d000b000000000000000000000000000000000000000000585cc70001000000086b0000000000"),
        (33, 6, "80b92100050003000000000000000000000000000000000000000000ffff7f7f00000000086b0000000000"),
        (36, 30, "80b924000400020000000000000000000000000000000000000000007802c50001000000086b0000000000"),
        (514, 30, "80b902020400020000000000000000000000000000000000000000000000000090767241086b0000000000")
    };
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, CoderStyle, 21, 33, 36, 514).Order().ToArray();

    public override string Name => "ability-parity-starter-coder-sight";
    public override NativeQaModePlan Plan { get; } = HackerSightStandBatchMode.SightPlan(Loadout, Samples,
        closeableRows: 1, new[]
        {
            "Select Coder Style in page 2 slot 1. Run ?combatdepth foundations-sight-pick and select QA Sight Target two meters ahead.",
            "Run ?combatdepth foundations-sight-blocked. Enter submits the command. Keep the same target selected and do not press Escape after the teleport chat.",
            "Press slots 2–5 for Lockdown, Static Blast, Tinkering and Advanced Tinkering. Wait for any shared reuse before the next press. Expect a sight block with no root, hit or splash. PR490 already verifies clear targeting and the splash comparison."
        }, style: CoderStyle);

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(50), "The Coder sight loadout fits native memory");
        var target = await SightStandCastProof.PickAsync(actor, CoderStyle, StockStyle);
        await SightStandCastProof.CheckBlockedAsync(context, target, Samples);
        return Array.Empty<byte>();
    }
}
