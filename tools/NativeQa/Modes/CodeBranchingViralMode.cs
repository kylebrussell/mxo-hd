namespace NativeQa.Modes;

public sealed class CodeBranchingViralMode : TransmitCodeMode
{
    public CodeBranchingViralMode() : base(328,
        new (ushort, int)[] { (1143, 5), (1027, 5), (1142, 10), (996, 10), (1148, 10) },
        reducedDamage: 556, baselinePercent: 15, baselineDamage: 590) { }

    public override string Name => "ability-parity-code-branching-viral";
}
