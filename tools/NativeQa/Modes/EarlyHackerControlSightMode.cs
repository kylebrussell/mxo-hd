namespace NativeQa.Modes;

public sealed class EarlyHackerControlSightMode : NativeQaMode
{
    private static readonly (ushort Ability, float Reuse, string Capture)[] Samples =
    {
        (43, 43, EarlyHackerControlRecoveryMode.BlindRequest),
        (48, 43, EarlyHackerControlRecoveryMode.PacifyRequest),
        (315, 8, EarlyHackerControlRecoveryMode.StunRequest)
    };

    private static readonly ushort[] Loadout =
        StockAbilityProfile.Profile(2, 17, 1006, 43, 48, 315).Order().ToArray();

    public override string Name => "ability-parity-early-hacker-control-sight";
    public override NativeQaModePlan Plan { get; } = HackerSightStandBatchMode.SightPlan(Loadout, Samples, closeableRows: 2, new[]
    {
        "Use Hacker Style on page 2 slot 1. Clear selection and press slot 4 once. Expect an invalid-target refusal with no cast bar or reuse. Then run ?combatdepth foundations-sight-pick and select QA Sight Target 2 m ahead.",
        "Keep that selection and run ?combatdepth foundations-sight-blocked. The same target is 6 m away behind the obstruction. Use Tab if the teleport cleared the selection.",
        "Press page 2 slots 2/3/4 for Disrupt Inputs, Freeze System and Stun. Each must be refused by the client or log a blocked sight cast without reaching the viral contest or applying a control. Record the selection and each outcome. Clear-sight casts already passed in PR482."
    });

    public override Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context) =>
        HackerSightStandBatchMode.CheckSightAsync(context, Loadout, Samples);
}
