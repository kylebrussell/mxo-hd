namespace NativeQa.Modes;

public sealed class HandgunArtistryDeterminationMode : StarterControlDeflectMode
{
    public HandgunArtistryDeterminationMode() : base(498,
        new (ushort, int)[] { (1047, 5), (1042, 10), (1143, 10), (1140, 5) }) { }

    public override string Name => "ability-parity-handgun-artistry-determination";
}
