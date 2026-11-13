namespace HeadlessClient;

/// <summary>Diagnostic starts beside existing source objects, never a claim about the walking route.</summary>
public sealed record NativeWorldDoorFixture(string Mode, string District, ushort World, ushort Goid,
    int Sector, int LocalId, int X, int Y, int Z, int DoorX, int DoorY, int DoorZ)
{
    public uint StaticId => ((uint)Sector << 20) | (uint)LocalId;
    public byte Heading => 0;
}

public static class NativeWorldDoorFixtures
{
    public static readonly NativeWorldDoorFixture Large01 =
        new("large01-door", "large01", 12, 365, 136, 1, 12900, 95, 15500, 12900, 145, 15600);
    public static readonly NativeWorldDoorFixture Large02 =
        new("large02-door", "large02", 13, 414, 27, 669, 14900, 95, 6300, 14900, 145, 6400);
}
