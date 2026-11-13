namespace HeadlessClient;

/// <summary>Diagnostic starts beside existing source objects, never a claim about the walking route.</summary>
public sealed record NativeWorldDoorFixture(string Mode, string District, ushort World, ushort Goid,
    int Sector, int LocalId, int X, int Y, int Z, int DoorX, int DoorY, int DoorZ, string? MetrResourceSuffix = null)
{
    public uint StaticId => ((uint)Sector << 20) | (uint)LocalId;
    public byte Heading => 0;
    public string MetrResource => MetrResourceSuffix ?? $"{District}/{District}.metr";
    public string? ExpectedQuaternion { get; init; }
    public string EvidenceDocument { get; init; } = "WORLD-MEDIUM03-PREFAB-ROTATIONS.md";
}

public static class NativeWorldDoorFixtures
{
    public static readonly NativeWorldDoorFixture Medium02Prefab =
        new("medium02-prefab-door", "medium02", 15, 414, 76, 56, -6100, 95, 17100, -6000, 145, 17100)
        { ExpectedQuaternion = "00000000f304353f00000000f204353f", EvidenceDocument = "WORLD-MEDIUM02-PREFAB-ROTATIONS.md" };
    public static readonly NativeWorldDoorFixture Medium03Prefab =
        new("medium03-prefab-door", "medium03", 16, 580, 33, 1380, 25300, 2495, 43100, 25300, 2545, 43200)
        { ExpectedQuaternion = "00000000f30435b300000000ffff7f3f" };
    public static readonly NativeWorldDoorFixture DataMinePrefab =
        new("datamine-prefab-door", "datamine", 7, 355, 18, 21, -11000, 3695, -3000, -10900, 3745, -3000, "archive_datamine/datamine.metr")
        { ExpectedQuaternion = "00000000f304353f00000000f304353f", EvidenceDocument = "WORLD-DATAMINE-PREFAB-ROTATIONS.md" };
    public static readonly NativeWorldDoorFixture Large01 =
        new("large01-door", "large01", 12, 365, 136, 1, 12900, 95, 15500, 12900, 145, 15600);
    public static readonly NativeWorldDoorFixture Large02 =
        new("large02-door", "large02", 13, 414, 27, 669, 14900, 95, 6300, 14900, 145, 6400);
    public static readonly NativeWorldDoorFixture Sati =
        new("sati-door", "archive_sati", 9, 365, 0, 8, 34800, 95, -13800, 34800, 145, -13700, "archive_sati/sati.metr");
    public static readonly NativeWorldDoorFixture Large02Prefab =
        new("large02-prefab-door", "large02", 13, 414, 27, 572, 16900, 95, 7100, 17000, 145, 7100)
        { ExpectedQuaternion = "00000000f304353f00000000f204353f", EvidenceDocument = "WORLD-LARGE02-PREFAB-ROTATIONS.md" };
}
