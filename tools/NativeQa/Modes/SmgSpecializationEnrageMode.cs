namespace NativeQa.Modes;

public sealed class SmgSpecializationEnrageMode : EnrageDurationArchiveMode
{
    public SmgSpecializationEnrageMode() : base(491,
        new (ushort, int)[] { (1047, 10), (1068, 5), (1139, 5), (1143, 5) },
        withdraw: false, previousEvidence:
            "Check source=491 stat=1047 +10, stat=1139 +5 and stat=1143 +5 on entry, unload and reload. This fixture verifies these records and the independent 1068 Enrage-duration control. The historical Determination Bonus coefficient remains open.", nativeCloseableRows: 0) { }

    public override string Name => "ability-parity-smg-specialization-enrage";
}
