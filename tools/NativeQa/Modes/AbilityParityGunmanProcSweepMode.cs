namespace NativeQa.Modes;

public sealed class AbilityParityGunmanProcSweepMode : AbilitySweepMode
{
    public override string Name => "ability-parity-gunman-proc-sweep";

    public AbilityParityGunmanProcSweepMode() : base(new Sample[]
    {
        new(147, "Rifles", "self with rifle", "0", "Rifles style", "while selected", 0),
        new(253, "Wounding Shot", "Melee Dummy in Rifles interlock", "record hit and each DOT tick",
            "30% stun roll on landing; record stun and immunity independently", "4 s stun / 4 s immunity on proc", 20),
        new(256, "Three Round Burst", "Melee Dummy in Rifles interlock", "record landed hit",
            "40% stun roll on landing; observe move 11367 and immunity", "4 s stun / 4 s immunity on proc", 20),
        new(158, "Submachine Guns", "self with SMG", "0", "Submachine Guns style", "while selected", 0),
        new(247, "Bullet Spray", "Melee Dummy in SMG interlock", "record landed primary hit",
            "record Enraged if the 15% roll procs", "8 s on proc", 30),
        new(248, "Suppression Fire", "Melee Dummy in SMG interlock", "record landed primary hit",
            "record Rooted and any break; leave cone admission unclaimed in this isolated fixture", "12 s GOB input", 35),
        new(250, "Controlled Burst", "Melee Dummy in SMG interlock", "record landed hit",
            "observe move 21082 and Enraged if the 40% roll procs", "8 s on proc", 30),
        new(496, "Full Auto Redux", "Melee Dummy in SMG interlock", "record landed hit",
            "check both accuracy penalties on the actual recipient", "8 s", 45)
    }, new ushort[] { 2, 17 }, firearms: true, isolatedMelee: true) { }
}
