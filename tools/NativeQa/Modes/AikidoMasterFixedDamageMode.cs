namespace NativeQa.Modes;

public sealed class AikidoMasterFixedDamageMode : MartialFixedDamageMode
{
    public AikidoMasterFixedDamageMode() : base("aikido", 101, MartialArtsStarterAdmissionMode.StockAikido,
        new Move[] { new(273, 56, 75, 61, 10, 779), new(274, 30, 40, 32, 10, 803), new(296, 375, 502, 413, 40, 741) },
        new[] { "273 and 296 are armed with Off Balance. 296 has 40 s own reuse, so wait for its slot after each pass." })
    {
    }
}
