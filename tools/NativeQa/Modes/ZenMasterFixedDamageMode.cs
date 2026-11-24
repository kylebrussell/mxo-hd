namespace NativeQa.Modes;

public sealed class ZenMasterFixedDamageMode : MartialFixedDamageMode
{
    // PR477 stock level-99 diagnostic captures, after the native Close Combat move.
    public ZenMasterFixedDamageMode() : base("zen-master", 17, MartialArtsStarterAdmissionMode.StockOpen,
        new Move[]
        {
            new(870, 325, 419, 380, 10, 0, "80b9660304000200000000000000000000000000000000000000000066030000006b0000086b0000000000"),
            new(869, 312, 402, 365, 10, 0, "80b9650304000200000000000000000000000000000000000000000065030000006b0000086b0000000000")
        },
        new[] { "The fixture seeds server level 99 and rank 50 for the shipped qualification. Keep Self Defense. 869 receives Off Balance. Record the actual animation; accept the 0x100 exchange and fixed damage independently." },
        level: 99, mobFixture: "zen-fixed-damage.csv")
    {
    }
}
