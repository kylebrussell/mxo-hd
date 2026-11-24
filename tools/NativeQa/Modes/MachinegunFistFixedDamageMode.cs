namespace NativeQa.Modes;

public sealed class MachinegunFistFixedDamageMode : MartialFixedDamageMode
{
    public MachinegunFistFixedDamageMode() : base("machinegun-fist", 133, MartialArtsStarterAdmissionMode.StockKungFu,
        new Move[] { new(261, 23, 31, 25, 10, 8081) },
        new[] { "The dummy has no removable player upgrades. Any random dispel callback and no-upgrade feedback are separate from the physical damage comparison." })
    {
    }
}
