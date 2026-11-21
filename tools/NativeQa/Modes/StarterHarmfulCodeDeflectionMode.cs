using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class StarterHarmfulCodeDeflectionMode : NativeQaMode
{
    private const int Level = 19;
    private const ushort Style = CombatStyles.HackerStyle;
    private static readonly (ushort Ability, float Reuse, string Capture)[] Samples =
    {
        (53, 8f, HarmfulCodeMode.StockViralUse),
        StarterHackerViralSightMode.Samples[0],
        StarterHackerViralSightMode.Samples[1]
    };
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, Style, 53, 57, 58).Order().ToArray();
    private static readonly int Memory = Loadout.Sum(AbilityMemory.Cost);

    public override string Name => "ability-parity-starter-harmful-code-deflection";

    public override NativeQaModePlan Plan { get; } = new(Loadout, 21550, 16150, Array.Empty<string>(), Array.Empty<string>(),
        new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x=21550,y=1095,z=16150," +
                "rotation=64,healthC=5000,healthM=5000,innerStrC=700,innerStrM=1000 WHERE charId=110",
            $"UPDATE char_abilities SET level={Level} WHERE char_id=110"
        }, DevCommands: true, hotbarAbilities: Samples.Select(sample => sample.Ability).Prepend(Style).ToArray(),
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_COMBAT_FOUNDATIONS_QA"] = "1",
            ["MXO_FREECAST_VIRAL_QA"] = "1", ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "15000",
            ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "32766"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "53,57,58", ["NativeCloseableRows"] = 0, ["MemoryUsed"] = Memory },
        readyMessages: new[]
        {
            $"Level {Level}, full {Memory}/{AbilityMemory.ForLevel(Level)} prerequisite memory. Select Hacker Style on page 2 slot 1. Expect [MODIFIER] char=110 source=95 stat=1031 +5 at login and source=1006 stat=172 +12 on style selection.",
            "Use ?combatdepth foundations-sight-pick and select QA Sight Target 2 m straight ahead. Stay on the clear stand. Do not use the blocked stand or press Escape. This disposable profile fixes only the eligible viral contest draw.",
            "Press slot 2 Harmful Code. Expect a 4 s cast, 18 IS debit, 8 s own and 4 s shared reuse. Expect '[FREECAST-VIRAL] ability=53 ... hit=False qa=True', client deflection feedback and FX, with no [ABILITY] landed damage or [EFFECTS] on the target.",
            "After each shared reuse clears, use slot 3 Logic Barrage and slot 4 Logic Blast as controls. Expect the same deflect branch. For 57/58 expect 2 s casts, 5/6 IS debits, and 4 s own and shared reuse. 53 is the new native check."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        var loaded = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor), row => row.Level);
        Check.That(Memory <= AbilityMemory.ForLevel(Level) && loaded.Keys.ToHashSet().SetEquals(Loadout) &&
            Loadout.All(id => AbilityPrerequisites.Current.Closure(id).All(need =>
                loaded.TryGetValue(need.Key, out ushort rank) && rank >= need.Value)), "Level-19 profile contains every ranked prerequisite");
        return await StarterHackerViralDeflectionMode.VerifyAsync(context, Samples);
    }
}
