namespace NativeQa.Modes;

public sealed class KarateFocusEnrageMode : EnrageDurationArchiveMode
{
    public KarateFocusEnrageMode() : base(531,
        new (ushort, int)[] { (995, 10), (1030, 12), (1068, 5), (1142, 6), (1144, 5) },
        withdraw: false, previousEvidence: "Check all five source 531 records at entry, unload and reload. Existing focused tests cover linked Stun, Viral special defense, melee damage and melee resistance.") { }

    public override string Name => "ability-parity-karate-focus-enrage";
}
