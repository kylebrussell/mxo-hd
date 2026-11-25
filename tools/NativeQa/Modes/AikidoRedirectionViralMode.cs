namespace NativeQa.Modes;

public sealed class AikidoRedirectionViralMode : TransmitCodeMode
{
    public AikidoRedirectionViralMode() : base(405,
        new (ushort, int)[] { (1068, 5), (1142, 1), (1143, 5), (1042, 10), (1030, 6), (1148, 12) }, reducedDamage: 600) { }

    public override string Name => "ability-parity-aikido-redirection-viral";
}
