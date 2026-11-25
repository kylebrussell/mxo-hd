namespace NativeQa.Modes;

public sealed class SmgSpecializationDeterminationMode : StarterControlDeflectMode
{
    public SmgSpecializationDeterminationMode() : base(491,
        new (ushort, int)[] { (1047, 10), (1068, 5), (1139, 5), (1143, 5) }, loadedStun: 6.615f) { }

    public override string Name => "ability-parity-smg-specialization-determination";
}
