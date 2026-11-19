namespace NativeQa.Modes;

public sealed class AwakenedHyperDefensesMode : NativeQaMode
{
    public override string Name => "awakened-hyper-defenses";
    public override NativeQaModePlan Plan { get; } = new(
        new ushort[] { 2, 17, 10, 1099, 1100, 1101 },
        CombatFixtureLoadout.CasterX, CombatFixtureLoadout.CasterZ, Array.Empty<string>(), Array.Empty<string>(),
        new[]
        {
            $"UPDATE characters SET y={CombatFixtureLoadout.GroundY},innerStrC=2000,innerStrM=2000 WHERE charId=110"
        },
        readyMessages: new[]
        {
            "Record Status Combat and the complete outgoing ability use before each change.",
            "Cast Hyper-Sense 1101, Hyper-Deflect 1100, Hyper-Block 1099, then Hyper-Dodge 10 from Actions.",
            "Check the previous icon and bonus disappear before the next. The expected deltas are thrown defense +47, viral defense +77, melee defense +52 and ballistic defense +51.",
            "Check the two outstanding native representatives independently: Hyper-Sense and Hyper-Deflect."
        }, loadAbilityAncestors: true);
}
