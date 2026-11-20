using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class StarterHackerViralSightMode : NativeQaMode
{
    private const ushort HackerStyle = CombatStyles.HackerStyle;
    // Stock PR487 (566) style and PR396 (4354, 4746, 5189, 5581) ability activations.
    private const string StockStyle = "80b9ee030000000000000000000000000000000000000000000000000000000090c97941086b0000000000";
    private static readonly (ushort Ability, float Reuse, string Capture)[] Samples =
    {
        (57, 4, "80b93900040002000000000000000000000000000000000000000000585cba0001000000086b0000000000"),
        (58, 4, "80b93a000400020000000000000000000000000000000000000000000000000090767241086b0000000000"),
        (59, 4, "80b93b00040002000000000000000000000000000000000000000000585cba0001000000086b0000000000"),
        (60, 2, "80b93c000400020000000000000000000000000000000000000000000000000090767241086b0000000000")
    };
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, HackerStyle, 57, 58, 59, 60).Order().ToArray();

    public override string Name => "ability-parity-starter-hacker-viral-sight";
    public override NativeQaModePlan Plan { get; } = HackerSightStandBatchMode.SightPlan(Loadout, Samples,
        closeableRows: 0, new[]
        {
            "Select Hacker Style in page 2 slot 1, run ?combatdepth foundations-sight-pick and select QA Sight Target two meters ahead.",
            "Keep it selected. Run ?combatdepth foundations-sight-far and try page 2 slots 2–5. Expect an out-of-range refusal if stock sends a request, with no IS debit or timers.",
            "Run ?combatdepth foundations-sight-blocked without pressing Escape after either teleport chat. The same target is six meters away. Press slots 2–5, waiting for shared reuse as needed. Expect blocked sight with no damage or Logic Bomb splash."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(50), "The viral sight loadout fits native memory");
        var target = await SightStandCastProof.PickAsync(actor, HackerStyle, StockStyle);
        uint handle = DuelPackets.Handle(target.ViewId, target.SpawnId);
        actor.SendChat("?combatdepth foundations-sight-far");
        await actor.WaitForTextAsync("QA Sight Target is beyond 20 m");
        foreach (var sample in Samples)
            await StarterHackerControlTargetMode.RefusedAsync(actor, sample.Ability,
                StarterHackerControlTargetMode.WithHandle(sample.Capture, handle), AbilityRefusals.TargetOutOfRange);
        await SightStandCastProof.CheckBlockedAsync(context, target, Samples);
        return Array.Empty<byte>();
    }
}
