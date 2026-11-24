namespace NativeQa.Modes;

public sealed class DisruptConnectionEnrageMode : EnrageDurationArchiveMode
{
    public DisruptConnectionEnrageMode() : base(384, new (ushort, int)[] { (995, 15), (1045, 10), (1068, 5) }, withdraw: true) { }

    public override string Name => "ability-parity-disrupt-connection-enrage-duration";
}
