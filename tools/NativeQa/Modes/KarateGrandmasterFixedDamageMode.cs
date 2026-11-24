namespace NativeQa.Modes;

public sealed class KarateGrandmasterFixedDamageMode : MartialFixedDamageMode
{
    public KarateGrandmasterFixedDamageMode() : base("karate-grandmaster", 132, "80b984000400020000000000000000000000000000000000000000000000000000000000086b0000000000",
        new Move[] { new(534, 31, 44, 36, 10, 755), new(532, 73, 104, 85, 10, 5934), new(536, 134, 191, 157, 10, 5935) },
        new[] { "532 gets Daze and 536 gets Staggered. Natural effects may proc, but the damage and move witnesses do not depend on them." })
    {
    }
}
