using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class StarterHackerMovementDebuffRangeMode : NativeQaMode
{
    private const int Level = 19, X = 21550, Y = 1095, Z = 16150;
    private const ushort Hacker = 1006;
    // Full stock activations from the Code Freeze and Slow target modes. Only the live handle changes.
    private static readonly (ushort Ability, float Reuse, string Capture)[] Samples =
    {
        (41, 43, "80b92900000000000000000000000000000000000000000000000000d557377c20000000086b0000000000"),
        (86, 8, "80b95600040002000000000000000000000000000000000000000000585cbb0001000000086b0000000000"),
        (40, 58, "80b92800040002000000000000000000000000000000000000000000ffff7f7f00000000086b0000000000")
    };
    private static readonly ushort[] Abilities = StockAbilityProfile.Profile(Samples.Select(sample => sample.Ability)
        .Prepend(Hacker).Concat(new ushort[] { 2, 17 }).ToArray()).Order().ToArray();
    private static readonly int Memory = Abilities.Sum(AbilityMemory.Cost);

    public override string Name => "ability-parity-starter-hacker-movement-debuff-range";
    public override NativeQaModePlan Plan { get; } = new(Abilities, X, Z, Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110" },
        new[] { $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Z},rotation=64," +
            "innerStrC=700,innerStrM=1000 WHERE charId=110" }, DevCommands: true,
        hotbarAbilities: Samples.Select(sample => sample.Ability).Prepend(Hacker).ToArray(), environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_COMBAT_FOUNDATIONS_QA"] = "1",
            ["MXO_FREECAST_VIRAL_QA"] = "1", ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766",
            ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "40,41,86", ["NativeCloseableRows"] = 0, ["MemoryUsed"] = Memory },
        readyMessages: new[]
        {
            $"Level {Level}, full loadout {Memory}/{AbilityMemory.ForLevel(Level)} memory. Choose Hacker Style on page 2 slot 1. Enter ?combatdepth foundations-sight-pick, then select QA Sight Target 2 m ahead on open ground. Enter submits chat, keep selection without Escape.",
            "Enter ?combatdepth foundations-sight-far. The same view is beyond all three 20 m ranges. Press slots 2, 3 and 4 once. If a stock request is sent, expect '[ABILITY] Denied <41/86/40>: out-of-range selected target' and reason 34. Each leaves cast, IS and reuse unchanged.",
            "Enter ?combatdepth foundations-sight-blocked with selection held. Press slot 2 Code Freeze 2.0 (41) then slot 3 Slow (86), after each cast has settled. Expect 'Sight blocked ability 41' and 'Sight blocked ability 86'. Wait 20 s for the shared Code Freeze reuse, then press slot 4 Code Freeze 1.0 (40) and expect 'Sight blocked ability 40'.",
            "A blocked request can start a 2 s cast and own reuse (41:43 s, 86:8 s, 40:58 s). For each source expect no [FREECAST-VIRAL], no [EFFECTS] on QA Sight Target and no [MODIFIER] add for those sources. These are negative probes. If stock greys a slot without sending, record that outcome and continue."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor), row => row.Level);
        Check.That(Memory <= AbilityMemory.ForLevel(Level) && archive.Keys.ToHashSet().SetEquals(Abilities) &&
            Abilities.All(id => AbilityPrerequisites.Current.Closure(id).All(need => archive.TryGetValue(need.Key, out ushort rank) && rank >= need.Value)),
            "The full ranked movement-debuff profile fits the level-19 client");
        var target = await SightStandCastProof.PickAsync(actor, Hacker, StarterHackerViralSightMode.StockStyle);
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, actor);
        uint handle = DuelPackets.Handle(target.ViewId, target.SpawnId);
        actor.SendChat("?combatdepth foundations-sight-far");
        await actor.WaitForTextAsync("QA Sight Target is beyond 20 m");
        foreach (var (id, _, packet) in Samples)
            await StarterHackerControlTargetMode.RefusedAsync(actor, id,
                StarterHackerControlTargetMode.WithHandle(packet, handle), AbilityRefusals.TargetOutOfRange);
        await SightStandCastProof.CheckBlockedAsync(context, target, Samples);
        return Array.Empty<byte>();
    }
}
