using hds;

namespace NativeQa.Modes;

public sealed class AikidoGrandmasterEnrageMode : EnrageDurationArchiveMode
{
    public AikidoGrandmasterEnrageMode() : base(410,
        new (ushort, int)[] { (1046, 1), (1068, 2), (1143, 5), (1144, 5) },
        withdraw: false, grant: 1069, initiative: true) { }

    public override string Name => "ability-parity-aikido-grandmaster-enrage";
}
