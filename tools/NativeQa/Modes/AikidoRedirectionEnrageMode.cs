namespace NativeQa.Modes;

public sealed class AikidoRedirectionEnrageMode : EnrageDurationArchiveMode
{
    public AikidoRedirectionEnrageMode() : base(405,
        new (ushort, int)[] { (1068, 5), (1142, 1), (1143, 5), (1042, 10), (1030, 6), (1148, 12) },
        withdraw: false, previousEvidence:
            "PR473/1117 already observed rank-50 Redirection's linked Powerless at 4.4 s on Punch Reversal. This run isolates stat 1068 with the independent Karate Overhand Smash control and verifies all six source records together. It does not measure the historical nonzero Viral Resistance coefficient.", nativeCloseableRows: 0) { }

    public override string Name => "ability-parity-aikido-redirection-enrage";
}
