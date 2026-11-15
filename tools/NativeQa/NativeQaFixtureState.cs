namespace NativeQa;

/// <summary>The standard state.json fields shared by the executor and mode serialization checks.</summary>
public sealed record NativeQaFixtureState
{
    public string Database { get; init; } = "";
    public string Directory { get; init; } = "";
    public string Account { get; init; } = "";
    public string Character { get; init; } = "";
    public uint CharId { get; init; }
    public string BuildCommit { get; init; } = "";
    public string Configuration { get; init; } = "";
    public string SourceAssembly { get; init; } = "";
    public string SourceAssemblySha256 { get; init; } = "";
    public string SnapshotAssemblySha256 { get; init; } = "";
    public string Status { get; init; } = "";
    public IReadOnlyList<ushort> Abilities { get; init; } = Array.Empty<ushort>();
    public bool StealthShieldQa { get; init; }
    public bool ShieldReady { get; init; }
    public int? CasterOrganization { get; init; }
    public bool CasterPvpFlag { get; init; }
    public IReadOnlyList<ushort> OwnedUnloadedDisciplineAncestors { get; init; } = Array.Empty<ushort>();
    public bool HealthCallbackQa { get; init; }
    public bool CompileItemsQa { get; init; }
    public int? CallbackRoll15 { get; init; }
    public int? CallbackRoll100 { get; init; }
}
