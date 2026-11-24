namespace NativeQa.Modes;

public sealed class KungFuGrandmasterFixedDamageMode : MartialFixedDamageMode
{
    public KungFuGrandmasterFixedDamageMode() : base("kungfu-grandmaster", 133, MartialArtsStarterAdmissionMode.StockKungFu,
        new Move[] { new(427, 91, 124, 102, 10, 8086), new(428, 25, 34, 27, 10, 8084), new(429, 46, 63, 51, 10, 8083), new(430, 107, 146, 120, 10, 8080) },
        new[] { "427 is armed with Off Balance and 430 with Daze. Keep natural control procs separate from the damage result." })
    {
    }
}
