namespace MxoHd.PacketResearch;

public enum RpcMatchKind
{
    Missing,
    ExactCommandBytes,
    DecodedValue,
    HeaderPrefix
}

public sealed record RpcHeaderEntry(
    string Source,
    string Protocol,
    string Direction,
    string EnumName,
    string Name,
    int Value,
    string EncodedHeader,
    string File,
    int Line);

public sealed record RajkoRpcEntry(
    string Width,
    int RawValue,
    int DecodedValue,
    string RawHeader,
    string Handler,
    string File,
    int Line);

public sealed record HardcodedCommandExample(
    string Source,
    string RawHeader,
    int DecodedValue,
    int PayloadLength,
    string Field0,
    int Field0Value,
    string Field1,
    int Field1Value,
    string Field2,
    int Field2Value,
    string PayloadHex,
    string File,
    int Line);

public sealed record Protocol03HardcodedExample(
    string Source,
    string File,
    int Line,
    int PayloadLength,
    string PrefixHex,
    IReadOnlyList<Protocol03ObjectViewSample> ObjectViews);

public sealed record RpcComparison(
    RajkoRpcEntry External,
    RpcHeaderEntry? Local,
    RpcMatchKind MatchKind);

public sealed record PacketDumpHeaderHit(
    string RawHeader,
    int DecodedValue,
    string? Direction,
    string? LocalName);

public sealed record Protocol04PacketSequenceSample(
    int Line,
    string? Direction,
    IReadOnlyList<string> Headers);

public sealed record ManageBonusPayloadSample(
    int Line,
    int PayloadLength,
    string Field0,
    int Field0Value,
    string Field1,
    int Field1Value,
    string Field2,
    int Field2Value,
    string PayloadHex);

public sealed record PlayerAttributePayloadSample(
    int Line,
    int PayloadLength,
    string Field0,
    int Field0Value,
    string Field1,
    int Field1Value,
    string Field2,
    int Field2Value,
    string PayloadHex);

public sealed record Protocol03ObjectViewSample(
    int Line,
    string? Direction,
    string TransportHeader,
    int ViewId,
    int UpdateCount,
    string FirstSelector,
    string Classification,
    string PrefixHex,
    int ParsedUpdateCount,
    int UnparsedBytes,
    bool Complete,
    IReadOnlyList<Protocol03ObjectUpdateSegment> Segments);

public sealed record Protocol03ObjectUpdateSegment(
    string Selector,
    string Classification,
    int PayloadBytes,
    int Offset,
    string PayloadPrefixHex,
    bool IsKnownLength);

public sealed record Protocol04InteractionPayloadSample(
    int Line,
    string? Direction,
    string RawHeader,
    string InteractionKind,
    int PayloadLength,
    uint ObjectId,
    int SectorId,
    int ObjectType,
    string ObjectTypeName,
    string PayloadHex);

public sealed record Protocol04InteractionCommandExample(
    string Source,
    string File,
    int Line,
    string RawHeader,
    string InteractionKind,
    int PayloadLength,
    uint ObjectId,
    int SectorId,
    int ObjectType,
    string ObjectTypeName,
    string PayloadHex);

public sealed record VendorInventoryEntry(
    ushort MetrId,
    uint VendorStaticId,
    IReadOnlyList<uint> Items,
    string File,
    int Line);

public sealed record AttributeDefinition(
    string Source,
    string ClassName,
    int? ClassId,
    string AttributeName,
    int Size,
    int CreationIndex,
    int UpdateIndex,
    string File,
    int Line);

public sealed record FxDefinition(
    string LittleEndianHex,
    string BigEndianHex,
    string Name,
    string File,
    int Line);

public sealed record PacketDumpFileSummary(
    string File,
    int PacketLikeLines,
    IReadOnlyDictionary<string, int> ProtocolCounts,
    IReadOnlyDictionary<string, int> RpcHeaderCounts,
    IReadOnlyDictionary<string, int> KnownHeaderCounts,
    IReadOnlyDictionary<string, IReadOnlyList<int>> KnownHeaderSampleLines,
    IReadOnlyList<Protocol04PacketSequenceSample> Protocol04PacketSequences,
    IReadOnlyList<Protocol03ObjectViewSample> Protocol03ObjectViews,
    IReadOnlyList<Protocol04InteractionPayloadSample> Protocol04InteractionPayloads,
    IReadOnlyList<PlayerAttributePayloadSample> PlayerAttributePayloads,
    IReadOnlyList<ManageBonusPayloadSample> ManageBonusPayloads);

public sealed record PacketResearchReport(
    string RepositoryRoot,
    IReadOnlyList<string> PacketDumpRoots,
    string? RajkoSourceRoot,
    IReadOnlyList<string> ExternalSourceRoots,
    IReadOnlyList<RpcHeaderEntry> LocalHeaders,
    IReadOnlyList<AttributeDefinition> AttributeDefinitions,
    IReadOnlyList<FxDefinition> FxDefinitions,
    IReadOnlyList<RajkoRpcEntry> RajkoRpcHeaders,
    IReadOnlyList<HardcodedCommandExample> HardcodedCommands,
    IReadOnlyList<Protocol03HardcodedExample> HardcodedProtocol03Examples,
    IReadOnlyList<Protocol04InteractionCommandExample> Protocol04InteractionCommandExamples,
    IReadOnlyList<VendorInventoryEntry> VendorInventoryEntries,
    IReadOnlyList<RpcComparison> RajkoComparisons,
    IReadOnlyList<PacketDumpFileSummary> PacketDumpFiles);
