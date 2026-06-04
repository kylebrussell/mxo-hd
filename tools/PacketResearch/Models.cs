using System.Text.Json.Serialization;

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

public sealed record AbilityUnloadPayloadSample(
    int Line,
    int PayloadLength,
    string Field0,
    int Field0Value,
    string PayloadHex);

public sealed record FriendListStatusPayloadSample(
    int Line,
    int PayloadLength,
    string Field0,
    int Field0Value,
    string Field1,
    int Field1Value,
    string Field2,
    int Field2Value,
    int? TextBytes,
    string? Text,
    string PayloadHex);

public sealed record CoderAttributePayloadSample(
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
    [property: JsonIgnore]
    IReadOnlyList<Protocol03StringSample> Strings,
    [property: JsonIgnore]
    IReadOnlyList<Protocol03NamedProfileRecord> NamedProfileRecords,
    [property: JsonIgnore]
    IReadOnlyList<Protocol03PlayerCharacterCreationRecord> PlayerCharacterCreationRecords,
    [property: JsonIgnore]
    IReadOnlyList<Protocol03EffectEmoteLead> EffectEmoteLeads,
    [property: JsonIgnore]
    IReadOnlyList<Protocol03PositionLikeLead> PositionLikeLeads,
    [property: JsonIgnore]
    IReadOnlyList<Protocol03NestedMovementLead> NestedMovementLeads,
    [property: JsonIgnore]
    IReadOnlyList<Protocol03MovementStateTailLead> MovementStateTailLeads,
    [property: JsonIgnore]
    IReadOnlyList<Protocol03VariableSelectorLead> VariableSelectorLeads,
    [property: JsonIgnore]
    IReadOnlyList<Protocol03SelfViewAttributeLead> SelfViewAttributeLeads,
    [property: JsonIgnore]
    IReadOnlyList<Protocol03StaticObjectLead> StaticObjectLeads,
    int ParsedUpdateCount,
    int UnparsedBytes,
    bool Complete,
    IReadOnlyList<Protocol03ObjectUpdateSegment> Segments);

public sealed record Protocol03StringSample(
    int Offset,
    string Text,
    string PrefixHex,
    string SuffixHex);

public sealed record Protocol03NamedProfileRecord(
    int Offset,
    string Text,
    int NameBytes,
    int NameSlotBytes,
    string RecordPrefixHex,
    string CreationFlagHex,
    string GameObjectIdHex,
    int GameObjectId,
    string SpawnCounterHex,
    string SeparatorHex,
    int CreationAttributeCount,
    string FirstAttributeMaskHex,
    int SuffixZeroBytes,
    string TitleAbilityHex,
    string CombatantModeHex,
    string PostNameSlotHex,
    string PostCombatantModeHex,
    IReadOnlyList<Protocol03CreationAttributeSample> CreationAttributes);

public sealed record Protocol03PlayerCharacterCreationRecord(
    int Offset,
    string RecordPrefixHex,
    string CreationFlagHex,
    string GameObjectIdHex,
    int GameObjectId,
    string SpawnCounterHex,
    string SeparatorHex,
    int CreationAttributeCount,
    string FirstAttributeMaskHex,
    IReadOnlyList<Protocol03CreationAttributeSample> CreationAttributes);

public sealed record Protocol03CreationAttributeSample(
    int Index,
    string Name,
    int Size,
    int Offset,
    string ValueHex);

public sealed record Protocol03EffectEmoteLead(
    int Offset,
    int PayloadBytes,
    string LeadByte0Hex,
    string LeadByte1Hex,
    string Field2Hex,
    string PositionHex,
    float? X,
    float? Y,
    float? Z,
    string PayloadHex);

public sealed record Protocol03PositionLikeLead(
    string Selector,
    int Offset,
    int PayloadBytes,
    string LeadPrefixHex,
    int PositionOffset,
    string PositionHex,
    float X,
    float Y,
    float Z,
    string PayloadHex);

public sealed record Protocol03VariableSelectorLead(
    string Selector,
    int Offset,
    int PayloadBytes,
    string PrefixHex,
    string PayloadHex);

public sealed record Protocol03SelfViewAttributeLead(
    string Selector,
    int Offset,
    int PayloadBytes,
    string MaskPrefixHex,
    IReadOnlyList<Protocol03CreationAttributeSample> Attributes,
    string SuffixPrefixHex,
    string PayloadHex);

public sealed record Protocol03NestedMovementLead(
    string OuterSelector,
    int Offset,
    int PayloadBytes,
    int MarkerOffset,
    string LeadPrefixHex,
    string MarkerModeHex,
    string InnerSelector,
    int InnerPayloadBytes,
    int InnerPositionOffset,
    string PositionHex,
    float X,
    float Y,
    float Z,
    string SuffixHex,
    string PostSuffixHex,
    string PayloadHex);

public sealed record Protocol03MovementStateTailLead(
    string FirstSelector,
    string TailSelector,
    int Offset,
    int PayloadBytes,
    string TagHex,
    uint TagValue,
    string MarkerHex,
    string PrefixHex,
    string PayloadHex);

public sealed record Protocol03StaticObjectLead(
    string Selector,
    int Offset,
    int PayloadBytes,
    string LeadByteHex,
    string ObjectIdHex,
    uint ObjectId,
    string InstanceByteHex,
    string SeparatorHex,
    int ObjectType,
    string UnknownByteHex,
    string TransformHex,
    float? Q0,
    float? Q1,
    float? Q2,
    float? Q3,
    string PayloadHex);

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

public sealed record ItemCommandEntry(
    uint ItemId,
    string Symbol,
    string? DisplayName,
    string File,
    int Line);

public sealed record StaticObjectEntry(
    ushort MetrId,
    ushort SectorId,
    uint MxoId,
    string MxoIdHex,
    uint StaticId,
    string StaticIdHex,
    ushort TypeId,
    string TypeHex,
    bool Exterior,
    double X,
    double Y,
    double Z,
    double Rotation,
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

public sealed record GameObjectEntry(
    string CodeName,
    int GoId,
    string File,
    int Line);

public sealed record WorldEntityEntry(
    string Source,
    string Name,
    string? Zone,
    int? Level,
    string? Kind,
    string? Rsi,
    string File,
    int Line,
    int? Health = null,
    int? MaxHealth = null,
    double? X = null,
    double? Y = null,
    double? Z = null,
    int? NpcRank = null,
    int? DebuffState = null,
    string? CurrentStateHex = null);

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
    IReadOnlyList<ManageBonusPayloadSample> ManageBonusPayloads)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<AbilityUnloadPayloadSample>? AbilityUnloadPayloads { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<FriendListStatusPayloadSample>? FriendListStatusPayloads { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<CoderAttributePayloadSample>? CoderAttributePayloads { get; init; }
}

public sealed record PacketResearchReport(
    string RepositoryRoot,
    IReadOnlyList<string> PacketDumpRoots,
    string? RajkoSourceRoot,
    IReadOnlyList<string> ExternalSourceRoots,
    IReadOnlyList<RpcHeaderEntry> LocalHeaders,
    IReadOnlyList<AttributeDefinition> AttributeDefinitions,
    IReadOnlyList<FxDefinition> FxDefinitions,
    [property: JsonIgnore]
    IReadOnlyList<GameObjectEntry> GameObjectEntries,
    [property: JsonIgnore]
    IReadOnlyList<WorldEntityEntry> WorldEntityEntries,
    IReadOnlyList<RajkoRpcEntry> RajkoRpcHeaders,
    IReadOnlyList<HardcodedCommandExample> HardcodedCommands,
    IReadOnlyList<Protocol03HardcodedExample> HardcodedProtocol03Examples,
    IReadOnlyList<Protocol04InteractionCommandExample> Protocol04InteractionCommandExamples,
    IReadOnlyList<VendorInventoryEntry> VendorInventoryEntries,
    IReadOnlyList<RpcComparison> RajkoComparisons,
    IReadOnlyList<PacketDumpFileSummary> PacketDumpFiles)
{
    [JsonIgnore]
    public IReadOnlyList<ItemCommandEntry> ItemCommandEntries { get; init; } = Array.Empty<ItemCommandEntry>();

    [JsonIgnore]
    public IReadOnlyList<StaticObjectEntry> StaticObjectEntries { get; init; } = Array.Empty<StaticObjectEntry>();
}
