namespace NativeQa.Modes;

public sealed class EnhancedPotencyViralMode : TransmitCodeMode
{
    public EnhancedPotencyViralMode() : base(422,
        new (ushort, int)[] { (1142, 10), (1139, 10), (1146, 5), (1148, 5) }, reducedDamage: 623) { }

    public override string Name => "ability-parity-enhanced-potency-viral";
}
