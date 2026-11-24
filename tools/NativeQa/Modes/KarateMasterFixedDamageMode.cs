namespace NativeQa.Modes;

public sealed class KarateMasterFixedDamageMode : MartialFixedDamageMode
{
    public KarateMasterFixedDamageMode() : base("karate", 132, "80b984000400020000000000000000000000000000000000000000000000000000000000086b0000000000",
        new Move[] { new(265, 68, 93, 76, 10, 5940), new(266, 28, 38, 31, 10, 5936), new(267, 21, 28, 23, 10, 5937) },
        new[] { "265 is armed with Staggered. Use 267 last in each pass and wait 12 s for its target resistance debuff to end before the next neutral pass." })
    {
    }
}
