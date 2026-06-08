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

public sealed record KnownHeaderContextSummary(
    string Label,
    string? PacketDirection,
    string PacketProtocol,
    string LineProtocol,
    int Count,
    IReadOnlyList<int> SampleLines)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<KnownHeaderWindowSummary>? Windows { get; init; }
}

public sealed record KnownHeaderWindowSummary(
    int Offset,
    string PrefixHex,
    string SuffixHex,
    int Count,
    IReadOnlyList<int> SampleLines,
    IReadOnlyList<int> SamplePacketStartLines)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<KnownHeaderWindowLocation>? SampleLocations { get; init; }
}

public sealed record KnownHeaderWindowLocation(
    int Line,
    int PacketStartLine,
    int PacketOffset);

public sealed record Protocol04PacketSequenceSample(
    int Line,
    string? Direction,
    IReadOnlyList<string> Headers)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<Protocol04RpcFlowBlockSample>? Blocks { get; init; }
}

public sealed record Protocol04RpcFlowBlockSample(
    int Index,
    string Header,
    string? LocalName,
    int PayloadLength,
    string PayloadPrefixHex,
    string PayloadSuffixHex,
    IReadOnlyList<string> Texts);

public sealed record VendorTransactionFlowSample(
    int Line,
    string? Direction,
    int VendorBlockIndex,
    string VendorHeader,
    string? VendorLocalName,
    int VendorPayloadLength,
    string VendorPayloadPrefixHex,
    string VendorPayloadSuffixHex,
    IReadOnlyList<Protocol04RpcFlowBlockSample> Blocks)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? VendorPayloadHex { get; init; }
}

public sealed record VendorOpenItemListSample(
    int Line,
    string? Direction,
    string VendorHeader,
    string? VendorLocalName,
    int PayloadLength,
    string PayloadPrefixHex,
    string CountFieldHex,
    ushort DeclaredCount,
    int ParsedIdCount,
    IReadOnlyList<uint> ItemIds,
    string PayloadSuffixHex)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? HeaderHex { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ListOffsetFieldHex { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ushort? ListOffset { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public float? HeaderFloat0 { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public float? HeaderFloat8 { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public float? HeaderFloat16 { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public float? HeaderFloat24 { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? HeaderWord4Hex { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? HeaderWord12Hex { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? HeaderWord20Hex { get; init; }
}

public sealed record VendorBuyAckSample(
    string PayloadPrefixHex,
    string SequenceFieldHex,
    ushort Sequence,
    string EchoFieldHex,
    bool EchoMatchesRequest,
    string TailMarkerHex)
{
    public string Header { get; init; } = "5e";

    public string Layout { get; init; } = "5e buy payload echo";

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SecondaryFieldHex { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public uint? SecondaryFieldValue { get; init; }
}

public sealed record VendorSellAckSample(
    string PayloadPrefixHex,
    string SellItemFieldHex,
    ushort SellItemValue,
    string FlagHex,
    byte Flag);

public sealed record VendorBuyTransactionSample(
    int Line,
    string? Direction,
    string BuyPayloadPrefixHex,
    string ItemIdFieldHex,
    uint ItemId,
    int NextLine,
    string? NextDirection,
    IReadOnlyList<string> NextHeaders,
    IReadOnlyList<VendorBuyAckSample> BuyAcks,
    IReadOnlyList<long> CashDeltas,
    IReadOnlyList<uint> BuyValues,
    IReadOnlyList<uint> ValueKinds);

public sealed record VendorSellTransactionSample(
    int Line,
    string? Direction,
    string SellPayloadPrefixHex,
    int NextLine,
    string? NextDirection,
    IReadOnlyList<string> NextHeaders,
    IReadOnlyList<VendorSellAckSample> SellAcks,
    string SellResponsePayloadPrefixHex,
    string NormalItemFieldHex,
    uint NormalItemId,
    string AbilityFieldHex,
    uint AbilityId,
    uint AbilityBaseId,
    IReadOnlyList<long> CashDeltas,
    IReadOnlyList<uint> SellValues,
    IReadOnlyList<uint> ValueKinds);

public sealed record VendorTransactionSampleLocation(
    string File,
    int Line,
    int NextLine);

public sealed record VendorBuyPriceLeadSummary(
    string BuyPayloadPrefixHex,
    string ItemIdFieldHex,
    uint ItemId,
    string? ItemSymbol,
    string? ItemDisplayName,
    uint? VendorPrice,
    string? VendorPriceCodeName,
    string PriceMatch,
    int TransactionCount,
    IReadOnlyList<ushort> BuyAckSequences,
    IReadOnlyList<string> BuyAckTailMarkers,
    IReadOnlyList<long> CashDeltas,
    IReadOnlyList<uint> BuyValues,
    IReadOnlyList<uint> ValueKinds,
    int VendorInventoryRowCount,
    IReadOnlyList<VendorTransactionSampleLocation> Samples);

public sealed record VendorSellPriceLeadSummary(
    string SellPayloadPrefixHex,
    string ResponseField,
    uint ResponseId,
    uint PriceId,
    bool IsAbility,
    string? ReferenceCodeName,
    string? ReferenceDisplayName,
    uint VendorPrice,
    string VendorPriceCodeName,
    uint ExpectedSell,
    string PriceMatch,
    int TransactionCount,
    IReadOnlyList<ushort> SellAckItemValues,
    IReadOnlyList<byte> SellAckFlags,
    IReadOnlyList<long> CashDeltas,
    IReadOnlyList<uint> SellValues,
    IReadOnlyList<uint> ValueKinds,
    int VendorInventoryRowCount,
    IReadOnlyList<VendorTransactionSampleLocation> Samples);

public sealed record Protocol04ServerPayloadShapeSample(
    int Line,
    string Header,
    string? LocalName,
    int PayloadLength,
    string Field0,
    int Field0Value,
    string Field1,
    int Field1Value,
    string Field2,
    int Field2Value,
    string PayloadPrefixHex,
    string PayloadSuffixHex,
    IReadOnlyList<string> Texts);

public sealed record Unknown8167PayloadSample(
    int Line,
    int PayloadLength,
    string Field0,
    int Field0Value,
    string Field1,
    int Field1Value,
    string Field2,
    int Field2Value,
    string PrefixHex,
    int? Text0Bytes,
    string? Text0,
    int? Text1Bytes,
    string? Text1,
    string TailField0,
    int TailField0Value,
    string TailField1,
    int TailField1Value,
    string PayloadHex);

public sealed record Unknown47PayloadSample(
    int Line,
    int PayloadLength,
    string Field0,
    int Field0Value,
    string Field1,
    int Field1Value,
    string Field2,
    int Field2Value,
    string MidField0,
    int MidField0Value,
    string MidField1,
    int MidField1Value,
    string TailHex,
    string PayloadHex);

public sealed record Unknown6dPayloadSample(
    int Line,
    int PayloadLength,
    string ModeHex,
    int ModeValue,
    string Field1,
    int Field1Value,
    int ListOffset,
    int? ListFlag,
    int? EntryCount,
    int? ParsedEntryCount,
    IReadOnlyList<int> EntryIds,
    string PayloadHex);

public sealed record Unknown80c1PayloadSample(
    int Line,
    int PayloadLength,
    string Field0,
    int Field0Value,
    string Field1,
    int Field1Value,
    string PayloadHex);

public sealed record LoadWorldPayloadSample(
    int Line,
    int PayloadLength,
    string MarkerHex,
    int MarkerValue,
    uint DistrictId,
    string SimTimeHex,
    uint SimTimeValue,
    string FlagHex,
    int EnvironmentOffset,
    int? WorldPathBytes,
    string? WorldPath,
    int? EnvironmentBytes,
    string? Environment,
    string PayloadHex);

public sealed record PlayerValuePayloadSample(
    int Line,
    string Header,
    string LocalName,
    int PayloadLength,
    uint Value,
    string ValueHex,
    string TailHex,
    string PayloadHex);

public sealed record FlashTrafficPayloadSample(
    int Line,
    int PayloadLength,
    string Field0,
    int Field0Value,
    string Field1,
    int Field1Value,
    string MarkerHex,
    int? UrlBytes,
    string? Url,
    string PayloadHex);

public sealed record FeatureEventPayloadSample(
    int Line,
    int PayloadLength,
    string PrefixHex,
    int DeclaredPayloadBytes,
    int? KeyBytes,
    string? Key,
    int? ValueBytes,
    string? ValueText,
    uint? ValueUInt32,
    string ValueHex,
    string PayloadHex);

public sealed record FactionPlayerInfoPayloadSample(
    int Line,
    int PayloadLength,
    uint CharacterId,
    uint FactionId,
    string PrefixHex,
    int Mode,
    int DataBytes,
    int? Alignment,
    string? FactionName,
    uint? MasterCharacterId,
    uint? Money,
    int? CrewCount,
    IReadOnlyList<string> CrewNames,
    IReadOnlyList<string> CrewLeaderNames,
    string PayloadHex);

public sealed record CrewMembersListPayloadSample(
    int Line,
    int PayloadLength,
    uint CharacterId,
    uint CrewId,
    int Organization,
    int CrewNameOffset,
    uint CaptainCharacterId,
    uint FirstMateCharacterId,
    uint Money,
    int MemberListOffset,
    string ConstantHex,
    int FullSize,
    string? CrewName,
    int? MemberCount,
    IReadOnlyList<string> MemberHandles,
    string PayloadHex);

public sealed record FriendOnlinePayloadSample(
    int Line,
    int PayloadLength,
    int StatusCode,
    string FlagHex,
    int? HandleBytes,
    string? Handle,
    string PayloadHex);

public sealed record ChatMessageResponsePayloadSample(
    int Line,
    int PayloadLength,
    string TypeHex,
    int TypeValue,
    string TypeName,
    string Field12Hex,
    int Field12Value,
    string Field15Hex,
    int Field15Value,
    int? TextBytes,
    string? Text,
    string PayloadHex);

public sealed record WhereamiResponsePayloadSample(
    int Line,
    int PayloadLength,
    float? X,
    float? Y,
    float? Z,
    string TailHex,
    string PayloadHex);

public sealed record FactionNameResponsePayloadSample(
    int Line,
    int PayloadLength,
    uint FactionId,
    string? FactionName,
    string PayloadHex);

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
    IReadOnlyList<Protocol03ObjectUpdateSegment> Segments)
{
    public int PacketOffset { get; init; } = -1;

    [property: JsonIgnore]
    public IReadOnlyList<Protocol03NestedMovementConsumedBodyBoundary> NestedMovementConsumedBodyBoundaries { get; init; } = Array.Empty<Protocol03NestedMovementConsumedBodyBoundary>();
}

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
    int CreationAttributeBytes,
    string PostCreationAttributePrefixHex,
    int? PostCreationObjectViewHeaderOffset,
    string PostCreationObjectViewPreHeaderHex,
    string PostCreationObjectViewHeaderHex,
    string PostCreationObjectViewHeaderClassification,
    string PostCreationObjectViewBoundaryFamily,
    int PostCreationObjectViewParsedUpdateCount,
    int PostCreationObjectViewUnparsedBytes,
    int PostCreationObjectViewConsumedBytes,
    string PostCreationObjectViewParseStatus,
    string PostCreationObjectViewDynamicTailPrefixHex,
    int PostCreationObjectViewDynamicTailAvailableBytes,
    int? PostCreationObjectViewDynamicTailNextHeaderOffset,
    string PostCreationObjectViewDynamicTailPreHeaderHex,
    string PostCreationObjectViewDynamicTailNextHeaderHex,
    string PostCreationObjectViewDynamicTailNextHeaderClassification,
    bool PostCreationObjectViewComplete,
    int SuffixZeroBytes,
    string TitleAbilityHex,
    string CombatantModeHex,
    string PostNameSlotHex,
    string PostCombatantModeHex,
    IReadOnlyList<Protocol03CreationAttributeSample> CreationAttributes);

public sealed record Protocol03NpcBaseDynamicCreationTailSummary(
    string TailFamily,
    bool FollowUpComplete,
    string ParseStatus,
    int CandidateCount,
    IReadOnlyDictionary<string, int> TailAvailableBytes,
    IReadOnlyDictionary<string, int> CapturedBytes,
    IReadOnlyDictionary<string, int> ParentPreValues,
    IReadOnlyDictionary<string, int> TailPrefixes,
    IReadOnlyDictionary<string, int> HeaderLikeLeads,
    IReadOnlyDictionary<string, int> TextSamples,
    string SampleFile,
    int SampleLine);

public sealed record Protocol03NpcBaseDynamicCreationHeaderLikeLeadSummary(
    int LeadOffset,
    int ViewId,
    string UpdateCountHex,
    string FirstSelector,
    int CandidateCount,
    IReadOnlyDictionary<string, int> CompleteParents,
    IReadOnlyDictionary<string, int> TailAvailableBytes,
    IReadOnlyDictionary<string, int> ParseStatuses,
    IReadOnlyDictionary<string, int> TailFamilies,
    IReadOnlyDictionary<string, int> ParentPreValues,
    IReadOnlyDictionary<string, int> LeadPrefixes,
    IReadOnlyDictionary<string, int> BodyHints,
    IReadOnlyDictionary<string, int> TextSamples,
    string SampleFile,
    int SampleLine);

public sealed record Protocol03NpcBaseDynamicCreationBodyAnchorSummary(
    int LeadOffset,
    int ViewId,
    string UpdateCountHex,
    string FirstSelector,
    int BodySeparatorOffset,
    int CandidateCount,
    IReadOnlyDictionary<string, int> CompleteParents,
    IReadOnlyDictionary<string, int> TailAvailableBytes,
    IReadOnlyDictionary<string, int> ParseStatuses,
    IReadOnlyDictionary<string, int> BodyPrefixes,
    IReadOnlyDictionary<string, int> PostSeparatorPrefixes,
    IReadOnlyDictionary<string, int> TextSamples,
    string SampleFile,
    int SampleLine);

public sealed record Protocol03NpcBaseDynamicCreationExtendedBoundarySummary(
    int TailHeaderOffset,
    string PreHeaderHex,
    string HeaderPrefixHex,
    string HeaderClassification,
    string BoundaryInterpretation,
    string ParserAction,
    int CandidateCount,
    IReadOnlyDictionary<string, int> TailAvailableBytes,
    IReadOnlyDictionary<string, int> TailFamilies,
    IReadOnlyDictionary<string, int> ParentPreValues,
    IReadOnlyDictionary<string, int> TextSamples,
    string SampleFile,
    int SampleLine);

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
    int CreationAttributeBytes,
    string PostCreationAttributePrefixHex,
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

public sealed record Protocol03SelectorFfRepeatListCandidate(
    string File,
    int Line,
    string? Direction,
    int PacketOffset,
    int ViewId,
    int UpdateCount,
    string FirstSelector,
    string Classification,
    string SegmentClassification,
    int SelectorOffset,
    int PayloadBytes,
    int PositionOffset,
    string Lead0Hex,
    string Field9Hex,
    string PrePositionHex,
    string PositionHex,
    double X,
    double Y,
    double Z,
    int PostPositionBytes,
    int RepeatStride,
    int RepeatRecordCount,
    int ContinuationOffset,
    int ContinuationBytes,
    string Entry1LeadHex,
    int Entry1ZeroRun,
    int? Entry1FieldOffset,
    string Entry1FieldHex,
    int? Entry1FieldValue,
    string Entry1FieldLayoutKind,
    int? Entry1SecondaryFieldOffset,
    string Entry1SecondaryFieldHex,
    int? Entry1SecondaryFieldValue,
    int? Entry1EffectiveFieldOffset,
    string Entry1EffectiveFieldHex,
    int? Entry1EffectiveFieldValue,
    string Entry1EffectiveFieldSource,
    string Entry2LeadHex,
    int Entry2ZeroRun,
    int? Entry2FieldOffset,
    string Entry2FieldHex,
    int? Entry2FieldValue,
    string Entry2FieldLayoutKind,
    int? Entry2SecondaryFieldOffset,
    string Entry2SecondaryFieldHex,
    int? Entry2SecondaryFieldValue,
    int? Entry2EffectiveFieldOffset,
    string Entry2EffectiveFieldHex,
    int? Entry2EffectiveFieldValue,
    string Entry2EffectiveFieldSource,
    string ContinuationPrefixHex,
    string ContinuationPrefixBeforeMarkerHex,
    string ContinuationMarkerHex,
    string PostMarkerLeadHex,
    int PostMarkerZeroRun,
    int? PostMarkerFirstFieldOffset,
    string PostMarkerFirstFieldHex,
    int? PostMarkerFirstFieldValue,
    string PostMarkerFieldLayoutKind);

public sealed record Protocol03SelectorFfRepeatListFieldSummary(
    string FieldRole,
    int RoleOrder,
    int ZeroRun,
    int? FieldOffset,
    string FieldHex,
    int? FieldValue,
    string FieldLayoutKind,
    IReadOnlyList<string> LeadHexes,
    int CandidateCount,
    IReadOnlyDictionary<string, int> UpdateCounts,
    IReadOnlyDictionary<string, int> FirstSelectors,
    IReadOnlyDictionary<string, int> ContinuationPrefixes,
    IReadOnlyDictionary<string, int> SegmentClassifications,
    IReadOnlyList<string> ResourceReferences,
    string SampleFile,
    int SampleLine,
    int SamplePositionOffset,
    string SamplePositionHex,
    double SampleX,
    double SampleY,
    double SampleZ);

public sealed record Protocol03SelectorFfRepeatListShapePositionSample(
    string File,
    int Line,
    int PositionOffset,
    string PositionHex,
    double X,
    double Y,
    double Z);

public sealed record Protocol03SelectorFfRepeatListShapeSummary(
    int RepeatStride,
    int RepeatRecordCount,
    int ContinuationOffset,
    string ContinuationPrefixHex,
    string ContinuationPrefixBeforeMarkerHex,
    string ContinuationMarkerHex,
    int Entry1ZeroRun,
    int? Entry1FieldOffset,
    string Entry1FieldHex,
    int? Entry1FieldValue,
    string Entry1FieldLayoutKind,
    IReadOnlyDictionary<string, int> Entry1SecondaryFields,
    IReadOnlyDictionary<string, int> Entry1EffectiveFields,
    IReadOnlyList<string> Entry1ResourceReferences,
    int Entry2ZeroRun,
    int? Entry2FieldOffset,
    string Entry2FieldHex,
    int? Entry2FieldValue,
    string Entry2FieldLayoutKind,
    IReadOnlyDictionary<string, int> Entry2SecondaryFields,
    IReadOnlyDictionary<string, int> Entry2EffectiveFields,
    IReadOnlyList<string> Entry2ResourceReferences,
    int PostMarkerZeroRun,
    int? PostMarkerFirstFieldOffset,
    string PostMarkerFirstFieldHex,
    int? PostMarkerFirstFieldValue,
    string PostMarkerFieldLayoutKind,
    IReadOnlyList<string> PostMarkerResourceReferences,
    int CandidateCount,
    IReadOnlyDictionary<string, int> UpdateCounts,
    IReadOnlyDictionary<string, int> FirstSelectors,
    IReadOnlyDictionary<string, int> SegmentClassifications,
    IReadOnlyList<Protocol03SelectorFfRepeatListShapePositionSample> PositionSamples,
    string SampleFile,
    int SampleLine);

public sealed record Protocol03SelectorFfRepeatListEffectiveShapeSummary(
    int RepeatStride,
    int RepeatRecordCount,
    int ContinuationOffset,
    string ContinuationPrefixHex,
    string ContinuationPrefixBeforeMarkerHex,
    string ContinuationMarkerHex,
    int Entry1ZeroRun,
    int? Entry1FieldOffset,
    string Entry1FieldHex,
    int? Entry1FieldValue,
    string Entry1FieldLayoutKind,
    IReadOnlyList<string> Entry1ResourceReferences,
    string Entry2EffectiveFieldHex,
    int? Entry2EffectiveFieldValue,
    IReadOnlyList<string> Entry2EffectiveResourceReferences,
    int PostMarkerZeroRun,
    int? PostMarkerFirstFieldOffset,
    string PostMarkerFirstFieldHex,
    int? PostMarkerFirstFieldValue,
    string PostMarkerFieldLayoutKind,
    IReadOnlyList<string> PostMarkerResourceReferences,
    int CandidateCount,
    IReadOnlyDictionary<string, int> Entry2EffectiveSources,
    IReadOnlyDictionary<string, int> Entry2PrimaryFields,
    IReadOnlyDictionary<string, int> Entry2SecondaryFields,
    IReadOnlyDictionary<string, int> Entry2FieldLayouts,
    IReadOnlyDictionary<string, int> UpdateCounts,
    IReadOnlyDictionary<string, int> FirstSelectors,
    IReadOnlyDictionary<string, int> SegmentClassifications,
    IReadOnlyList<Protocol03SelectorFfRepeatListShapePositionSample> PositionSamples,
    string SampleFile,
    int SampleLine);

public sealed record Protocol03SelectorFfRepeatListContinuationMarkerSummary(
    string ContinuationPrefixBeforeMarkerHex,
    string ContinuationMarkerHex,
    int PostMarkerZeroRun,
    int? PostMarkerFirstFieldOffset,
    string PostMarkerFirstFieldHex,
    int? PostMarkerFirstFieldValue,
    string PostMarkerFieldLayoutKind,
    int CandidateCount,
    int ShapeCount,
    int CandidatesWithHeaderWindows,
    int CandidatesWithVendorHeaderWindows,
    int HeaderWindowCount,
    int VendorHeaderWindowCount,
    IReadOnlyDictionary<string, int> HeaderRegions,
    IReadOnlyList<Protocol03SelectorFfRepeatListHeaderWindowSummary> HeaderWindows,
    IReadOnlyList<Protocol03SelectorFfRepeatListHeaderWindowSummary> VendorHeaderWindows,
    IReadOnlyDictionary<string, int> ContinuationPrefixes,
    IReadOnlyDictionary<string, int> Entry1Fields,
    IReadOnlyDictionary<string, int> Entry2PrimaryFields,
    IReadOnlyDictionary<string, int> Entry2SecondaryFields,
    IReadOnlyDictionary<string, int> Entry2EffectiveFields,
    IReadOnlyDictionary<string, int> Entry2EffectiveSources,
    IReadOnlyDictionary<string, int> Entry1ResourceReferences,
    IReadOnlyDictionary<string, int> Entry2EffectiveResourceReferences,
    IReadOnlyDictionary<string, int> PostMarkerResourceReferences,
    IReadOnlyDictionary<string, int> VendorEntry1ResourceReferences,
    IReadOnlyDictionary<string, int> VendorEntry2EffectiveResourceReferences,
    IReadOnlyDictionary<string, int> VendorPostMarkerResourceReferences,
    IReadOnlyDictionary<string, int> PostMarkerLeadHexes,
    IReadOnlyDictionary<string, int> FirstSelectors,
    IReadOnlyDictionary<string, int> SegmentClassifications,
    IReadOnlyList<Protocol03SelectorFfRepeatListShapePositionSample> PositionSamples,
    string SampleFile,
    int SampleLine);

public sealed record Protocol03SelectorFfRepeatListVendorContinuationBodyOffsetSummary(
    int ContinuationOffset,
    int PostMarkerOffset,
    int CandidateCount,
    int HeaderWindowCount,
    int ShapeCount,
    IReadOnlyDictionary<string, int> Headers,
    IReadOnlyDictionary<string, int> PayloadOffsets,
    IReadOnlyDictionary<string, int> ObjectOffsets,
    IReadOnlyDictionary<string, int> WindowHexes,
    IReadOnlyDictionary<string, int> ContextHexes,
    IReadOnlyDictionary<string, int> TargetBodyHexes,
    IReadOnlyDictionary<string, int> ContinuationBytes,
    IReadOnlyDictionary<string, int> BytesAfterHeaderInContinuation,
    IReadOnlyDictionary<string, int> HeaderTailClasses,
    IReadOnlyDictionary<string, int> ContinuationPrefixes,
    IReadOnlyDictionary<string, int> Entry1Fields,
    IReadOnlyDictionary<string, int> Entry2EffectiveFields,
    IReadOnlyDictionary<string, int> PostMarkerFields,
    IReadOnlyDictionary<string, int> PositionOffsets,
    IReadOnlyDictionary<string, int> FirstSelectors,
    string SampleFile,
    int SampleLine);

public sealed record Protocol03SelectorFfRepeatListVendorPrePositionBodyOffsetSummary(
    int PrePositionOffset,
    int BytesBeforePosition,
    IReadOnlyDictionary<string, int> BytesBeforePositions,
    IReadOnlyDictionary<string, int> BytesAfterHeaderBeforePosition,
    IReadOnlyDictionary<string, int> PositionGapClasses,
    int CandidateCount,
    int HeaderWindowCount,
    int ShapeCount,
    IReadOnlyDictionary<string, int> Headers,
    IReadOnlyDictionary<string, int> PayloadOffsets,
    IReadOnlyDictionary<string, int> ObjectOffsets,
    IReadOnlyDictionary<string, int> WindowHexes,
    IReadOnlyDictionary<string, int> ContextHexes,
    IReadOnlyDictionary<string, int> TargetBodyHexes,
    IReadOnlyDictionary<string, int> PrePositionHexes,
    IReadOnlyDictionary<string, int> PositionOffsets,
    IReadOnlyDictionary<string, int> ContinuationPrefixes,
    IReadOnlyDictionary<string, int> Entry1Fields,
    IReadOnlyDictionary<string, int> Entry2EffectiveFields,
    IReadOnlyDictionary<string, int> PostMarkerFields,
    IReadOnlyDictionary<string, int> FirstSelectors,
    string SampleFile,
    int SampleLine);

public sealed record Protocol03SelectorFfRepeatListVendorBodyWindowProbeSummary(
    string BodyRegion,
    string ParserPriority,
    string TargetInterpretation,
    string Name,
    string Direction,
    string EncodedHeader,
    IReadOnlyList<string> Protocols,
    string TargetLocalOffset,
    int BodyOffset,
    int BodyOffsetModulo2,
    int BodyOffsetModulo4,
    int CandidateCount,
    int HeaderWindowCount,
    int ShapeCount,
    IReadOnlyDictionary<string, int> BytesBeforeHeaderInBody,
    IReadOnlyDictionary<string, int> BytesAfterHeaderInBody,
    IReadOnlyDictionary<string, int> BodyByteCounts,
    IReadOnlyDictionary<string, int> TargetBodySpanBytes,
    IReadOnlyDictionary<string, int> TargetBodyHexes,
    IReadOnlyDictionary<string, int> PayloadOffsets,
    IReadOnlyDictionary<string, int> ObjectOffsets,
    IReadOnlyDictionary<string, int> WindowHexes,
    IReadOnlyDictionary<string, int> WindowU32Values,
    IReadOnlyDictionary<string, int> HeaderU16Values,
    IReadOnlyDictionary<string, int> WindowHalfU16ResourceReferences,
    IReadOnlyDictionary<string, int> NearbyU16Values,
    IReadOnlyDictionary<string, int> NearbyU16LengthMatches,
    IReadOnlyDictionary<string, int> ContinuationPrefixes,
    IReadOnlyDictionary<string, int> Entry1Fields,
    IReadOnlyDictionary<string, int> Entry2EffectiveFields,
    IReadOnlyDictionary<string, int> PostMarkerFields,
    IReadOnlyDictionary<string, int> FirstSelectors,
    string SampleFile,
    int SampleLine);

public sealed record Protocol03SelectorFfRepeatListVendorBodyWindowResourceCollisionSummary(
    string WindowHalf,
    string WindowU16,
    string ResourceKind,
    string ResourceReference,
    int ProbeRowCount,
    int ReferenceWindowCount,
    int CandidateCount,
    int HeaderWindowCount,
    int ShapeCount,
    IReadOnlyDictionary<string, int> BodyRegions,
    IReadOnlyDictionary<string, int> ParserPriorities,
    IReadOnlyDictionary<string, int> TargetInterpretations,
    IReadOnlyDictionary<string, int> Headers,
    IReadOnlyDictionary<string, int> TargetLocalOffsets,
    IReadOnlyDictionary<string, int> BodyOffsets,
    IReadOnlyDictionary<string, int> TargetBodySpanBytes,
    IReadOnlyDictionary<string, int> WindowHexes,
    IReadOnlyDictionary<string, int> HeaderU16Values,
    IReadOnlyDictionary<string, int> NearbyU16LengthMatches,
    string SampleFile,
    int SampleLine);

public sealed record Protocol03SelectorFfRepeatListVendorBodyWindowEvidenceSummary(
    string EvidenceDisposition,
    int ProbeRowCount,
    int CandidateCount,
    int HeaderWindowCount,
    int ShapeCount,
    IReadOnlyDictionary<string, int> BodyRegions,
    IReadOnlyDictionary<string, int> ParserPriorities,
    IReadOnlyDictionary<string, int> TargetInterpretations,
    IReadOnlyDictionary<string, int> Headers,
    IReadOnlyDictionary<string, int> TargetLocalOffsets,
    IReadOnlyDictionary<string, int> BodyOffsets,
    IReadOnlyDictionary<string, int> TargetBodySpanBytes,
    IReadOnlyDictionary<string, int> WindowHexes,
    IReadOnlyDictionary<string, int> LowHalfResourceReferences,
    IReadOnlyDictionary<string, int> HighHalfResourceReferences,
    IReadOnlyDictionary<string, int> NearbyU16LengthMatches,
    string SampleFile,
    int SampleLine);

public sealed record Protocol03SelectorFfRepeatListVendorBodyWindowParserActionSummary(
    string ParserAction,
    string ActionReason,
    int ProbeRowCount,
    int CandidateCount,
    int HeaderWindowCount,
    int ShapeCount,
    IReadOnlyDictionary<string, int> EvidenceDispositions,
    IReadOnlyDictionary<string, int> BodyRegions,
    IReadOnlyDictionary<string, int> ParserPriorities,
    IReadOnlyDictionary<string, int> TargetInterpretations,
    IReadOnlyDictionary<string, int> Headers,
    IReadOnlyDictionary<string, int> TargetLocalOffsets,
    IReadOnlyDictionary<string, int> TargetBodySpanBytes,
    IReadOnlyDictionary<string, int> WindowHexes,
    IReadOnlyDictionary<string, int> LowHalfResourceReferences,
    IReadOnlyDictionary<string, int> HighHalfResourceReferences,
    IReadOnlyDictionary<string, int> NearbyU16LengthMatches,
    string SampleFile,
    int SampleLine);

public sealed record Protocol03SelectorFfRepeatListHeaderWindowSummary(
    int PayloadOffset,
    string Name,
    string Direction,
    string EncodedHeader,
    IReadOnlyList<string> Protocols,
    int Count,
    IReadOnlyDictionary<string, int> ObjectOffsets,
    IReadOnlyDictionary<string, int> WindowHexes);

public sealed record Protocol03SelectorFfRepeatListEffectiveShapeHeaderSummary(
    int RepeatStride,
    int RepeatRecordCount,
    int ContinuationOffset,
    string ContinuationPrefixHex,
    string ContinuationPrefixBeforeMarkerHex,
    string ContinuationMarkerHex,
    int Entry1ZeroRun,
    int? Entry1FieldOffset,
    string Entry1FieldHex,
    int? Entry1FieldValue,
    string Entry1FieldLayoutKind,
    string Entry2EffectiveFieldHex,
    int? Entry2EffectiveFieldValue,
    int PostMarkerZeroRun,
    int? PostMarkerFirstFieldOffset,
    string PostMarkerFirstFieldHex,
    int? PostMarkerFirstFieldValue,
    string PostMarkerFieldLayoutKind,
    int CandidateCount,
    int CandidatesWithHeaderWindows,
    int CandidatesWithVendorHeaderWindows,
    IReadOnlyDictionary<string, int> Entry2EffectiveSources,
    IReadOnlyDictionary<string, int> Entry2PrimaryFields,
    IReadOnlyDictionary<string, int> Entry2SecondaryFields,
    IReadOnlyDictionary<string, int> FirstSelectors,
    IReadOnlyDictionary<string, int> SegmentClassifications,
    IReadOnlyList<Protocol03SelectorFfRepeatListHeaderWindowSummary> HeaderWindows,
    IReadOnlyList<Protocol03SelectorFfRepeatListHeaderWindowSummary> VendorHeaderWindows,
    IReadOnlyList<Protocol03SelectorFfRepeatListShapePositionSample> PositionSamples,
    string SampleFile,
    int SampleLine);

public sealed record Protocol03SelectorFfRepeatListHeaderNameShapeSummary(
    int RepeatStride,
    int RepeatRecordCount,
    int ContinuationOffset,
    string ContinuationPrefixHex,
    string ContinuationPrefixBeforeMarkerHex,
    string ContinuationMarkerHex,
    int Entry1ZeroRun,
    int? Entry1FieldOffset,
    string Entry1FieldHex,
    int? Entry1FieldValue,
    string Entry1FieldLayoutKind,
    string Entry2EffectiveFieldHex,
    int? Entry2EffectiveFieldValue,
    int PostMarkerZeroRun,
    int? PostMarkerFirstFieldOffset,
    string PostMarkerFirstFieldHex,
    int? PostMarkerFirstFieldValue,
    string PostMarkerFieldLayoutKind,
    int CandidateCount,
    int HeaderWindowCount,
    IReadOnlyDictionary<string, int> PayloadOffsets,
    IReadOnlyDictionary<string, int> ObjectOffsets,
    IReadOnlyDictionary<string, int> WindowHexes,
    IReadOnlyDictionary<string, int> Entry2EffectiveSources,
    IReadOnlyDictionary<string, int> Entry2PrimaryFields,
    IReadOnlyDictionary<string, int> Entry2SecondaryFields,
    IReadOnlyDictionary<string, int> FirstSelectors,
    IReadOnlyDictionary<string, int> SegmentClassifications,
    IReadOnlyList<Protocol03SelectorFfRepeatListShapePositionSample> PositionSamples,
    string SampleFile,
    int SampleLine);

public sealed record Protocol03SelectorFfRepeatListHeaderNameSummary(
    string Name,
    string Direction,
    string EncodedHeader,
    IReadOnlyList<string> Protocols,
    int CandidateCount,
    int HeaderWindowCount,
    int ShapeCount,
    bool IsVendorHeader,
    IReadOnlyDictionary<string, int> PayloadOffsets,
    IReadOnlyDictionary<string, int> ObjectOffsets,
    IReadOnlyDictionary<string, int> WindowHexes,
    IReadOnlyDictionary<string, int> ContinuationPrefixes,
    IReadOnlyDictionary<string, int> Entry1Fields,
    IReadOnlyDictionary<string, int> Entry2EffectiveFields,
    IReadOnlyDictionary<string, int> PostMarkerFields,
    IReadOnlyDictionary<string, int> FirstSelectors,
    IReadOnlyDictionary<string, int> SegmentClassifications,
    IReadOnlyDictionary<string, int> HeaderRegions,
    IReadOnlyList<Protocol03SelectorFfRepeatListHeaderNameShapeSummary> TopShapes,
    string SampleFile,
    int SampleLine);

public sealed record Protocol03SelectorFfRepeatListHeaderRegionSummary(
    string Name,
    string Direction,
    string EncodedHeader,
    IReadOnlyList<string> Protocols,
    string HeaderRegion,
    int CandidateCount,
    int HeaderWindowCount,
    int ShapeCount,
    bool IsVendorHeader,
    IReadOnlyDictionary<string, int> PayloadOffsets,
    IReadOnlyDictionary<string, int> ObjectOffsets,
    IReadOnlyDictionary<string, int> WindowHexes,
    IReadOnlyDictionary<string, int> Entry1Fields,
    IReadOnlyDictionary<string, int> Entry2EffectiveFields,
    IReadOnlyDictionary<string, int> PostMarkerFields,
    IReadOnlyDictionary<string, int> Entry1ResourceReferences,
    IReadOnlyDictionary<string, int> Entry2EffectiveResourceReferences,
    IReadOnlyDictionary<string, int> PostMarkerResourceReferences,
    IReadOnlyDictionary<string, int> FirstSelectors,
    IReadOnlyDictionary<string, int> SegmentClassifications,
    IReadOnlyList<Protocol03SelectorFfRepeatListHeaderNameShapeSummary> TopShapes,
    string SampleFile,
    int SampleLine);

public sealed record Protocol03SelectorFfRepeatListVendorBoundaryTargetSummary(
    string Name,
    string Direction,
    string EncodedHeader,
    IReadOnlyList<string> Protocols,
    string HeaderRegion,
    int HeaderPayloadOffset,
    int HeaderObjectOffset,
    int CandidateCount,
    int HeaderWindowCount,
    int ShapeCount,
    IReadOnlyDictionary<string, int> TargetInterpretations,
    IReadOnlyDictionary<string, int> TargetLocalOffsets,
    IReadOnlyDictionary<string, int> TargetBodySpanBytes,
    IReadOnlyDictionary<string, int> TargetBodyHexes,
    IReadOnlyDictionary<string, int> PositionOffsets,
    IReadOnlyDictionary<string, int> WindowHexes,
    IReadOnlyDictionary<string, int> ContinuationPrefixes,
    IReadOnlyDictionary<string, int> Entry1Fields,
    IReadOnlyDictionary<string, int> Entry1ResourceReferences,
    IReadOnlyDictionary<string, int> Entry2EffectiveFields,
    IReadOnlyDictionary<string, int> Entry2EffectiveResourceReferences,
    IReadOnlyDictionary<string, int> PostMarkerFields,
    IReadOnlyDictionary<string, int> PostMarkerResourceReferences,
    IReadOnlyDictionary<string, int> FirstSelectors,
    IReadOnlyDictionary<string, int> SegmentClassifications,
    IReadOnlyList<Protocol03SelectorFfRepeatListHeaderNameShapeSummary> TopShapes,
    string SampleFile,
    int SampleLine);

public sealed record Protocol03SelectorFfRepeatListVendorBoundaryInterpretationTargetSummary(
    string Name,
    string Direction,
    string EncodedHeader,
    IReadOnlyList<string> Protocols,
    string HeaderRegion,
    int HeaderPayloadOffset,
    int HeaderObjectOffset,
    int CandidateCount,
    int HeaderWindowCount,
    int ShapeCount,
    IReadOnlyDictionary<string, int> TargetLocalOffsets,
    IReadOnlyDictionary<string, int> TargetBodySpanBytes,
    IReadOnlyDictionary<string, int> TargetBodyHexes,
    IReadOnlyDictionary<string, int> PositionOffsets,
    IReadOnlyDictionary<string, int> Entry2EffectiveFields,
    IReadOnlyDictionary<string, int> PostMarkerFields,
    string SampleFile,
    int SampleLine);

public sealed record Protocol03SelectorFfRepeatListVendorBoundaryInterpretationSummary(
    string TargetInterpretation,
    string ParserPriority,
    bool IsModeledFieldOverlap,
    int TargetCount,
    int CandidateCount,
    int HeaderWindowCount,
    int ShapeCount,
    IReadOnlyDictionary<string, int> Headers,
    IReadOnlyDictionary<string, int> HeaderRegions,
    IReadOnlyDictionary<string, int> PayloadOffsets,
    IReadOnlyDictionary<string, int> ObjectOffsets,
    IReadOnlyDictionary<string, int> TargetLocalOffsets,
    IReadOnlyDictionary<string, int> TargetBodySpanBytes,
    IReadOnlyDictionary<string, int> TargetBodyHexes,
    IReadOnlyDictionary<string, int> PositionOffsets,
    IReadOnlyDictionary<string, int> Entry1Fields,
    IReadOnlyDictionary<string, int> Entry2EffectiveFields,
    IReadOnlyDictionary<string, int> PostMarkerFields,
    IReadOnlyList<Protocol03SelectorFfRepeatListVendorBoundaryInterpretationTargetSummary> TopTargets);

public sealed record Protocol03SelectorFfRepeatListVendorBoundaryPrioritySummary(
    string ParserPriority,
    bool IsModeledFieldOverlap,
    int TargetCount,
    int CandidateCount,
    int HeaderWindowCount,
    int ShapeCount,
    IReadOnlyDictionary<string, int> TargetInterpretations,
    IReadOnlyDictionary<string, int> Headers,
    IReadOnlyDictionary<string, int> HeaderRegions,
    IReadOnlyDictionary<string, int> PayloadOffsets,
    IReadOnlyDictionary<string, int> ObjectOffsets,
    IReadOnlyDictionary<string, int> TargetLocalOffsets,
    IReadOnlyDictionary<string, int> TargetBodySpanBytes,
    IReadOnlyDictionary<string, int> TargetBodyHexes,
    IReadOnlyDictionary<string, int> PositionOffsets,
    IReadOnlyDictionary<string, int> Entry1Fields,
    IReadOnlyDictionary<string, int> Entry2EffectiveFields,
    IReadOnlyDictionary<string, int> PostMarkerFields,
    IReadOnlyList<Protocol03SelectorFfRepeatListVendorBoundaryInterpretationTargetSummary> TopTargets);

public sealed record Protocol03SelectorFfRepeatListHeaderSample(
    string Name,
    string Direction,
    string EncodedHeader,
    IReadOnlyList<string> Protocols,
    bool IsVendorHeader,
    string File,
    int Line,
    string? PacketDirection,
    int PacketOffset,
    int ViewId,
    int UpdateCount,
    string FirstSelector,
    string Classification,
    string SegmentClassification,
    int SelectorOffset,
    int PayloadBytes,
    int HeaderPayloadOffset,
    int HeaderObjectOffset,
    string HeaderWindowHex,
    string HeaderRegion,
    string HeaderInterpretation,
    string TargetLocalOffset,
    string PayloadPrefixHex,
    string HeaderContextHex,
    string RepeatListContextHex,
    string Entry1FieldHex,
    int? Entry1FieldValue,
    string Entry2EffectiveFieldHex,
    int? Entry2EffectiveFieldValue,
    string Entry2EffectiveFieldSource,
    string ContinuationPrefixHex,
    string PostMarkerFirstFieldHex,
    int? PostMarkerFirstFieldValue,
    int PositionOffset,
    string PositionHex,
    double X,
    double Y,
    double Z);

public sealed record Protocol03SelectorFfRepeatListLayoutSummary(
    string Entry1FieldLayoutKind,
    string Entry2FieldLayoutKind,
    string ContinuationPrefixHex,
    string ContinuationPrefixBeforeMarkerHex,
    string ContinuationMarkerHex,
    string PostMarkerFieldLayoutKind,
    int CandidateCount,
    int ShapeCount,
    IReadOnlyDictionary<string, int> Entry1Fields,
    IReadOnlyDictionary<string, int> Entry1SecondaryFields,
    IReadOnlyDictionary<string, int> Entry1EffectiveFields,
    IReadOnlyDictionary<string, int> Entry2Fields,
    IReadOnlyDictionary<string, int> Entry2SecondaryFields,
    IReadOnlyDictionary<string, int> Entry2EffectiveFields,
    IReadOnlyDictionary<string, int> PostMarkerFields,
    IReadOnlyDictionary<string, int> FirstSelectors,
    IReadOnlyDictionary<string, int> SegmentClassifications,
    string SampleFile,
    int SampleLine);

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

public sealed record Protocol03NestedMovementConsumedBodyBoundary(
    string ParserAction,
    string Selector,
    int Offset,
    int PayloadBytes,
    int EvidencePayloadBytes,
    int TailStartOffset,
    int TailBoundaryOffset,
    int PostTupleBodyBytes,
    int BoundaryOffset,
    string TupleLayoutKind,
    string BodyPrefixHex,
    int? FirstNonZeroOffset,
    string FirstNonZeroFieldHex,
    string BoundaryHeaderHex,
    string BoundaryHeaderClassification);

public sealed record Protocol03NestedMovementBoundaryEvidenceSummary(
    string EvidenceDisposition,
    string ParserAction,
    string ActionReason,
    int WindowCount,
    int BoundedPrimaryWrapperCount,
    int UnresolvedPayloadLeadCount,
    IReadOnlyDictionary<string, int> MarkerModes,
    IReadOnlyDictionary<string, int> InnerSelectors,
    IReadOnlyDictionary<string, int> OuterSelectors,
    IReadOnlyDictionary<string, int> ObjectClassifications,
    IReadOnlyDictionary<string, int> SuffixPrefixes,
    IReadOnlyDictionary<string, int> PostSuffixPrefixes,
    IReadOnlyDictionary<string, int> PayloadBytes,
    IReadOnlyDictionary<string, int> PositionSamples,
    string SampleFile,
    int SampleLine);

public sealed record Protocol03NestedMovementMode06BoundaryCandidateSummary(
    string TailPrefixKind,
    string SuffixPrefixHex,
    string PostSuffixPrefixHex,
    int WindowCount,
    IReadOnlyDictionary<string, int> TailStartOffsets,
    IReadOnlyDictionary<string, int> CandidateCutOffsets,
    IReadOnlyDictionary<string, int> TailBytes,
    IReadOnlyDictionary<string, int> HeaderLikeTailLeads,
    IReadOnlyDictionary<string, int> InnerSelectors,
    IReadOnlyDictionary<string, int> OuterSelectors,
    IReadOnlyDictionary<string, int> ObjectClassifications,
    IReadOnlyDictionary<string, int> PayloadBytes,
    IReadOnlyDictionary<string, int> LeadPrefixes,
    IReadOnlyDictionary<string, int> PositionSamples,
    string SampleFile,
    int SampleLine);

public sealed record Protocol03NestedMovementMode06FfContinuationSummary(
    string TailPrefixKind,
    string MarkerPrefixHex,
    string TailU16Offset4Hex,
    string TailU16Offset6Hex,
    string ContinuationPrefixHex,
    string ContinuationMarkerHex,
    string PostMarkerPrefixHex,
    int WindowCount,
    IReadOnlyDictionary<string, int> TailStartOffsets,
    IReadOnlyDictionary<string, int> SuffixCutOffsets,
    IReadOnlyDictionary<string, int> ContinuationCutOffsets,
    IReadOnlyDictionary<string, int> TailBytes,
    IReadOnlyDictionary<string, int> InnerSelectors,
    IReadOnlyDictionary<string, int> OuterSelectors,
    IReadOnlyDictionary<string, int> ObjectClassifications,
    IReadOnlyDictionary<string, int> PayloadBytes,
    IReadOnlyDictionary<string, int> PositionSamples,
    string SampleFile,
    int SampleLine);

public sealed record Protocol03NestedMovementMode06PostContinuationBoundarySummary(
    string TailPrefixKind,
    string BoundaryDisposition,
    int? TailBoundaryOffset,
    int? PostContinuationOffset,
    string PreBoundaryPrefixHex,
    string HeaderHex,
    string HeaderClassification,
    int WindowCount,
    IReadOnlyDictionary<string, int> TailStartOffsets,
    IReadOnlyDictionary<string, int> ContinuationCutOffsets,
    IReadOnlyDictionary<string, int> BoundaryCutOffsets,
    IReadOnlyDictionary<string, int> TailBytes,
    IReadOnlyDictionary<string, int> InnerSelectors,
    IReadOnlyDictionary<string, int> OuterSelectors,
    IReadOnlyDictionary<string, int> ObjectClassifications,
    IReadOnlyDictionary<string, int> PayloadBytes,
    IReadOnlyDictionary<string, int> PositionSamples,
    string SampleFile,
    int SampleLine);

public sealed record Protocol03NestedMovementMode06PostContinuationBodySummary(
    string TailPrefixKind,
    string BodyDisposition,
    int PostContinuationBodyBytes,
    string BodyPrefixHex,
    string BodySuffixHex,
    int? FirstNonZeroOffset,
    string FirstNonZeroFieldHex,
    int WindowCount,
    IReadOnlyDictionary<string, int> TailStartOffsets,
    IReadOnlyDictionary<string, int> ContinuationCutOffsets,
    IReadOnlyDictionary<string, int> BoundaryCutOffsets,
    IReadOnlyDictionary<string, int> HeaderClassifications,
    IReadOnlyDictionary<string, int> HeaderPrefixes,
    IReadOnlyDictionary<string, int> InnerSelectors,
    IReadOnlyDictionary<string, int> OuterSelectors,
    IReadOnlyDictionary<string, int> ObjectClassifications,
    IReadOnlyDictionary<string, int> PayloadBytes,
    IReadOnlyDictionary<string, int> PositionSamples,
    string SampleFile,
    int SampleLine);

public sealed record Protocol03NestedMovementMode06PostContinuationPrefixSummary(
    string TailPrefixKind,
    string PrefixDisposition,
    int PrefixBytes,
    string PrefixLeadHex,
    string PrefixSuffixHex,
    int? FirstNonZeroOffset,
    string FirstNonZeroFieldHex,
    int WindowCount,
    IReadOnlyDictionary<string, int> BoundaryDispositions,
    IReadOnlyDictionary<string, int> PostContinuationBoundaryOffsets,
    IReadOnlyDictionary<string, int> BytesAfterPrefix,
    IReadOnlyDictionary<string, int> TailStartOffsets,
    IReadOnlyDictionary<string, int> ContinuationCutOffsets,
    IReadOnlyDictionary<string, int> BoundaryCutOffsets,
    IReadOnlyDictionary<string, int> InnerSelectors,
    IReadOnlyDictionary<string, int> OuterSelectors,
    IReadOnlyDictionary<string, int> ObjectClassifications,
    IReadOnlyDictionary<string, int> PayloadBytes,
    IReadOnlyDictionary<string, int> PositionSamples,
    string SampleFile,
    int SampleLine);

public sealed record Protocol03NestedMovementMode06PostContinuationRemainderSummary(
    string TailPrefixKind,
    string PrefixFirstNonZeroFieldHex,
    string PostPrefixDisposition,
    string PostPrefixLeadHex,
    string PostPrefixU16Offset0Hex,
    string PostPrefixU16Offset4Hex,
    string PostPrefixU16Offset6Hex,
    int WindowCount,
    IReadOnlyDictionary<string, int> BoundaryDispositions,
    IReadOnlyDictionary<string, int> PostContinuationBoundaryOffsets,
    IReadOnlyDictionary<string, int> BytesAfterPrefix,
    IReadOnlyDictionary<string, int> TailStartOffsets,
    IReadOnlyDictionary<string, int> ContinuationCutOffsets,
    IReadOnlyDictionary<string, int> BoundaryCutOffsets,
    IReadOnlyDictionary<string, int> InnerSelectors,
    IReadOnlyDictionary<string, int> OuterSelectors,
    IReadOnlyDictionary<string, int> ObjectClassifications,
    IReadOnlyDictionary<string, int> PayloadBytes,
    IReadOnlyDictionary<string, int> PositionSamples,
    string SampleFile,
    int SampleLine);

public sealed record Protocol03NestedMovementMode06PostContinuationTupleSummary(
    string TailPrefixKind,
    string PrefixFirstNonZeroFieldHex,
    string TupleLayoutKind,
    string PostPrefixDisposition,
    string Byte0Hex,
    string UpdateCountHex,
    string FirstSelectorHex,
    string MiddleBytesHex,
    string MarkerByteHex,
    int WindowCount,
    IReadOnlyDictionary<string, int> BoundaryDispositions,
    IReadOnlyDictionary<string, int> PostContinuationBoundaryOffsets,
    IReadOnlyDictionary<string, int> BytesAfterPrefix,
    IReadOnlyDictionary<string, int> Byte1Values,
    IReadOnlyDictionary<string, int> PostPrefixLeads,
    IReadOnlyDictionary<string, int> TailStartOffsets,
    IReadOnlyDictionary<string, int> ContinuationCutOffsets,
    IReadOnlyDictionary<string, int> BoundaryCutOffsets,
    IReadOnlyDictionary<string, int> InnerSelectors,
    IReadOnlyDictionary<string, int> OuterSelectors,
    IReadOnlyDictionary<string, int> ObjectClassifications,
    IReadOnlyDictionary<string, int> PayloadBytes,
    IReadOnlyDictionary<string, int> PositionSamples,
    string SampleFile,
    int SampleLine);

public sealed record Protocol03NestedMovementMode06PostContinuationTupleBoundarySummary(
    string TailPrefixKind,
    string PrefixFirstNonZeroFieldHex,
    string TupleLayoutKind,
    string PostPrefixDisposition,
    string BoundaryDisposition,
    int? TailBoundaryOffset,
    int? PostContinuationBoundaryOffset,
    int? PostTupleBodyBytes,
    string HeaderHex,
    string HeaderClassification,
    int WindowCount,
    IReadOnlyDictionary<string, int> BytesAfterPrefix,
    IReadOnlyDictionary<string, int> PostPrefixLeads,
    IReadOnlyDictionary<string, int> Byte1Values,
    IReadOnlyDictionary<string, int> MarkerBytes,
    IReadOnlyDictionary<string, int> TailStartOffsets,
    IReadOnlyDictionary<string, int> ContinuationCutOffsets,
    IReadOnlyDictionary<string, int> BoundaryCutOffsets,
    IReadOnlyDictionary<string, int> InnerSelectors,
    IReadOnlyDictionary<string, int> OuterSelectors,
    IReadOnlyDictionary<string, int> ObjectClassifications,
    IReadOnlyDictionary<string, int> PayloadBytes,
    IReadOnlyDictionary<string, int> PositionSamples,
    string SampleFile,
    int SampleLine);

public sealed record Protocol03NestedMovementMode06PostContinuationTupleBodySummary(
    string TailPrefixKind,
    string PrefixFirstNonZeroFieldHex,
    string TupleLayoutKind,
    string PostPrefixDisposition,
    string BodyDisposition,
    int PostTupleBodyBytes,
    string BodyPrefixHex,
    string BodySuffixHex,
    string PreAnchorBodyPrefixHex,
    string PreAnchorBodySuffixHex,
    int? PreAnchorFirstNonZeroOffset,
    string PreAnchorFirstNonZeroFieldHex,
    string PreAnchorTrailingLeadHex,
    string PreAnchorPreLeadHex,
    int? FirstNonZeroOffset,
    string FirstNonZeroFieldHex,
    int WindowCount,
    IReadOnlyDictionary<string, int> HeaderClassifications,
    IReadOnlyDictionary<string, int> HeaderPrefixes,
    IReadOnlyDictionary<string, int> PostContinuationBoundaryOffsets,
    IReadOnlyDictionary<string, int> BytesAfterPrefix,
    IReadOnlyDictionary<string, int> PostPrefixLeads,
    IReadOnlyDictionary<string, int> Byte1Values,
    IReadOnlyDictionary<string, int> MarkerBytes,
    IReadOnlyDictionary<string, int> TailStartOffsets,
    IReadOnlyDictionary<string, int> ContinuationCutOffsets,
    IReadOnlyDictionary<string, int> BoundaryCutOffsets,
    IReadOnlyDictionary<string, int> InnerSelectors,
    IReadOnlyDictionary<string, int> OuterSelectors,
    IReadOnlyDictionary<string, int> ObjectClassifications,
    IReadOnlyDictionary<string, int> PayloadBytes,
    IReadOnlyDictionary<string, int> PositionSamples,
    string SampleFile,
    int SampleLine);

public sealed record Protocol03NestedMovementMode06PostContinuationTerminalTupleBodySummary(
    string TailPrefixKind,
    string PrefixFirstNonZeroFieldHex,
    string TupleLayoutKind,
    string PostPrefixDisposition,
    string BodyDisposition,
    int TerminalPostTupleBodyBytes,
    int WindowCount,
    IReadOnlyDictionary<string, int> BodyPrefixes,
    IReadOnlyDictionary<string, int> BodySuffixes,
    IReadOnlyDictionary<string, int> PreAnchorBodyPrefixes,
    IReadOnlyDictionary<string, int> PreAnchorBodySuffixes,
    IReadOnlyDictionary<string, int> PreAnchorFirstNonZeroOffsets,
    IReadOnlyDictionary<string, int> PreAnchorFirstNonZeroFields,
    IReadOnlyDictionary<string, int> PreAnchorTrailingLeads,
    IReadOnlyDictionary<string, int> PreAnchorPreLeadBytes,
    IReadOnlyDictionary<string, int> PreTailFieldOffsets,
    IReadOnlyDictionary<string, int> PreTailFieldBytes,
    IReadOnlyDictionary<string, int> FirstNonZeroOffsets,
    IReadOnlyDictionary<string, int> FirstNonZeroFields,
    IReadOnlyDictionary<string, int> BytesAfterPrefix,
    IReadOnlyDictionary<string, int> PostPrefixLeads,
    IReadOnlyDictionary<string, int> Byte1Values,
    IReadOnlyDictionary<string, int> MarkerBytes,
    IReadOnlyDictionary<string, int> TailStartOffsets,
    IReadOnlyDictionary<string, int> ContinuationCutOffsets,
    IReadOnlyDictionary<string, int> TerminalTailEndOffsets,
    IReadOnlyDictionary<string, int> InnerSelectors,
    IReadOnlyDictionary<string, int> OuterSelectors,
    IReadOnlyDictionary<string, int> ObjectClassifications,
    IReadOnlyDictionary<string, int> PayloadBytes,
    IReadOnlyDictionary<string, int> PositionSamples,
    string SampleFile,
    int SampleLine);

public sealed record Protocol03NestedMovementMode06LongTupleBodyTailAnchorSummary(
    string SourceDisposition,
    string TailPrefixKind,
    string PrefixFirstNonZeroFieldHex,
    string TupleLayoutKind,
    string PostPrefixDisposition,
    int BodyBytes,
    int WindowCount,
    IReadOnlyDictionary<string, int> TailAnchorKinds,
    IReadOnlyDictionary<string, int> InferredTrailingBodyBytes,
    IReadOnlyDictionary<string, int> PreAnchorBodyBytes,
    IReadOnlyDictionary<string, int> PreAnchorBodyPrefixes,
    IReadOnlyDictionary<string, int> PreAnchorBodySuffixes,
    IReadOnlyDictionary<string, int> PreAnchorFirstNonZeroOffsets,
    IReadOnlyDictionary<string, int> PreAnchorFirstNonZeroFields,
    IReadOnlyDictionary<string, int> PreAnchorTrailingLeads,
    IReadOnlyDictionary<string, int> PreAnchorPreLeadBytes,
    IReadOnlyDictionary<string, int> BodyPrefixes,
    IReadOnlyDictionary<string, int> BodySuffixes,
    IReadOnlyDictionary<string, int> HeaderClassifications,
    IReadOnlyDictionary<string, int> HeaderPrefixes,
    IReadOnlyDictionary<string, int> BytesAfterPrefix,
    IReadOnlyDictionary<string, int> PostPrefixLeads,
    IReadOnlyDictionary<string, int> MarkerBytes,
    IReadOnlyDictionary<string, int> InnerSelectors,
    IReadOnlyDictionary<string, int> OuterSelectors,
    IReadOnlyDictionary<string, int> ObjectClassifications,
    IReadOnlyDictionary<string, int> PayloadBytes,
    IReadOnlyDictionary<string, int> PositionSamples,
    string SampleFile,
    int SampleLine);

public sealed record Protocol03NestedMovementMode06LongTupleBodyPreAnchorLeadSummary(
    string ParserTarget,
    string ActionReason,
    string SourceDisposition,
    string PreAnchorTrailingLeadHex,
    string PreAnchorPreLeadHex,
    string TailAnchorKind,
    int EvidenceRowCount,
    int WindowCount,
    IReadOnlyDictionary<string, int> PrefixFields,
    IReadOnlyDictionary<string, int> TupleLayouts,
    IReadOnlyDictionary<string, int> PostPrefixDispositions,
    IReadOnlyDictionary<string, int> BodyByteLengths,
    IReadOnlyDictionary<string, int> PreAnchorBodyBytes,
    IReadOnlyDictionary<string, int> InferredTrailingBodyBytes,
    IReadOnlyDictionary<string, int> PreAnchorFirstNonZeroOffsets,
    IReadOnlyDictionary<string, int> PreAnchorFirstNonZeroFields,
    IReadOnlyDictionary<string, int> PostPrefixLeads,
    IReadOnlyDictionary<string, int> MarkerBytes,
    IReadOnlyDictionary<string, int> HeaderClassifications,
    IReadOnlyDictionary<string, int> HeaderPrefixes,
    IReadOnlyDictionary<string, int> BodySuffixes,
    IReadOnlyDictionary<string, int> ObjectClassifications,
    IReadOnlyDictionary<string, int> PayloadBytes,
    IReadOnlyDictionary<string, int> PositionSamples,
    string SampleFile,
    int SampleLine);

public sealed record Protocol03NestedMovementMode06LongTupleBodyPreAnchorBodySummary(
    string ParserTarget,
    string ActionReason,
    string SourceDisposition,
    string TailAnchorKind,
    string PreAnchorTrailingLeadHex,
    string PreAnchorPreLeadHex,
    int BodyBytes,
    int PreAnchorBodyBytes,
    int InferredTrailingBodyBytes,
    int WindowCount,
    IReadOnlyDictionary<string, int> PrefixFields,
    IReadOnlyDictionary<string, int> TupleLayouts,
    IReadOnlyDictionary<string, int> PostPrefixDispositions,
    IReadOnlyDictionary<string, int> PreAnchorFirstNonZeroOffsets,
    IReadOnlyDictionary<string, int> PreAnchorFirstNonZeroFields,
    IReadOnlyDictionary<string, int> PreAnchorNonZeroByteOffsets,
    IReadOnlyDictionary<string, int> PreAnchorHeaderLikeOffsets,
    IReadOnlyDictionary<string, int> PreAnchorPrefixes,
    IReadOnlyDictionary<string, int> PreAnchorFirstNonZeroWindows,
    IReadOnlyDictionary<string, int> PreAnchorSuffixes,
    IReadOnlyDictionary<string, int> BodySuffixes,
    IReadOnlyDictionary<string, int> HeaderClassifications,
    IReadOnlyDictionary<string, int> HeaderPrefixes,
    IReadOnlyDictionary<string, int> ObjectClassifications,
    IReadOnlyDictionary<string, int> PayloadBytes,
    IReadOnlyDictionary<string, int> PositionSamples,
    string SampleFile,
    int SampleLine);

public sealed record Protocol03NestedMovementMode06LongTupleBodyPreAnchorOffsetSummary(
    int Offset,
    int WindowCount,
    int NonZeroCount,
    int HeaderLikeCount,
    IReadOnlyDictionary<string, int> ByteValues,
    IReadOnlyDictionary<string, int> TwoByteValues,
    IReadOnlyDictionary<string, int> FourByteWindows,
    IReadOnlyDictionary<string, int> HeaderClassifications,
    IReadOnlyDictionary<string, int> ParserTargets,
    IReadOnlyDictionary<string, int> PreAnchorTrailingLeads,
    IReadOnlyDictionary<string, int> TailAnchorKinds,
    IReadOnlyDictionary<string, int> PreAnchorBodyBytes,
    IReadOnlyDictionary<string, int> BodyBytes,
    string SampleFile,
    int SampleLine);

public sealed record Protocol03NestedMovementMode06LongTupleBodyPreAnchorHeaderCandidateSummary(
    int Offset,
    string CandidateDisposition,
    int WindowCount,
    IReadOnlyDictionary<string, int> ViewIds,
    IReadOnlyDictionary<string, int> UpdateCounts,
    IReadOnlyDictionary<string, int> FirstSelectors,
    IReadOnlyDictionary<string, int> HeaderClassifications,
    IReadOnlyDictionary<string, int> FourByteWindows,
    IReadOnlyDictionary<string, int> NextHeaderLikeOffsets,
    IReadOnlyDictionary<string, int> BytesToNextHeaderLike,
    IReadOnlyDictionary<string, int> BytesToPreAnchorEnd,
    IReadOnlyDictionary<string, int> ParserTargets,
    IReadOnlyDictionary<string, int> PreAnchorTrailingLeads,
    IReadOnlyDictionary<string, int> TailAnchorKinds,
    IReadOnlyDictionary<string, int> PreAnchorBodyBytes,
    IReadOnlyDictionary<string, int> BodyBytes,
    string SampleFile,
    int SampleLine);

public sealed record Protocol03NestedMovementMode06LongTupleBodyPreAnchorEmbeddedMovementSummary(
    int MarkerOffset,
    string MovementDisposition,
    int WindowCount,
    IReadOnlyDictionary<string, int> InnerSelectors,
    IReadOnlyDictionary<string, int> InnerPayloadBytes,
    IReadOnlyDictionary<string, int> InnerPositionOffsets,
    IReadOnlyDictionary<string, int> PreAnchorPositionOffsets,
    IReadOnlyDictionary<string, int> MarkerWindows,
    IReadOnlyDictionary<string, int> PositionSamples,
    IReadOnlyDictionary<string, int> OuterPositionSamples,
    IReadOnlyDictionary<string, int> PostMovementLeads,
    IReadOnlyDictionary<string, int> ParserTargets,
    IReadOnlyDictionary<string, int> PreAnchorTrailingLeads,
    IReadOnlyDictionary<string, int> TailAnchorKinds,
    IReadOnlyDictionary<string, int> PreAnchorBodyBytes,
    IReadOnlyDictionary<string, int> BodyBytes,
    string SampleFile,
    int SampleLine);

public sealed record Protocol03NestedMovementMode06LongTupleBodyPreAnchorEmbeddedMovementTrailerSummary(
    int MarkerOffset,
    string MovementDisposition,
    string TrailerDisposition,
    string PostMovementLeadHex,
    int WindowCount,
    IReadOnlyDictionary<string, int> InnerSelectors,
    IReadOnlyDictionary<string, int> PreAnchorPostMovementOffsets,
    IReadOnlyDictionary<string, int> BytesRemainingAfterMovement,
    IReadOnlyDictionary<string, int> PostMovementNextLeads,
    IReadOnlyDictionary<string, int> PostMovementTailLeads,
    IReadOnlyDictionary<string, int> PreTailFieldOffsets,
    IReadOnlyDictionary<string, int> PreTailFieldBytes,
    IReadOnlyDictionary<string, int> PreAnchorPreLeads,
    IReadOnlyDictionary<string, int> PostMovementNonZeroByteOffsets,
    IReadOnlyDictionary<string, int> ParserTargets,
    IReadOnlyDictionary<string, int> PreAnchorTrailingLeads,
    IReadOnlyDictionary<string, int> TailAnchorKinds,
    IReadOnlyDictionary<string, int> PreAnchorBodyBytes,
    IReadOnlyDictionary<string, int> BodyBytes,
    string SampleFile,
    int SampleLine);

public sealed record Protocol03NestedMovementMode06LongTupleBodyPreAnchorEmbeddedMovementTrailerShapeSummary(
    int MarkerOffset,
    string ShapeDisposition,
    string MovementDisposition,
    string TrailerDisposition,
    string PostMovementLeadHex,
    string PostMovementNextLeadHex,
    int BytesRemainingAfterMovement,
    int? PreTailFieldOffset,
    string PreTailFieldHex,
    string PreAnchorPreLeadHex,
    string PreAnchorTrailingLeadHex,
    string TailAnchorKind,
    int WindowCount,
    IReadOnlyDictionary<string, int> InnerSelectors,
    IReadOnlyDictionary<string, int> PreAnchorPostMovementOffsets,
    IReadOnlyDictionary<string, int> ParserTargets,
    IReadOnlyDictionary<string, int> PreAnchorBodyBytes,
    IReadOnlyDictionary<string, int> BodyBytes,
    IReadOnlyDictionary<string, int> PostMovementNonZeroByteOffsets,
    string SampleFile,
    int SampleLine);

public sealed record Protocol03NestedMovementMode06TupleBodyParserActionSummary(
    string ParserAction,
    string ActionReason,
    int EvidenceRowCount,
    int WindowCount,
    IReadOnlyDictionary<string, int> TailPrefixKinds,
    IReadOnlyDictionary<string, int> TupleLayouts,
    IReadOnlyDictionary<string, int> PostPrefixDispositions,
    IReadOnlyDictionary<string, int> BodyDispositions,
    IReadOnlyDictionary<string, int> BodyByteLengths,
    IReadOnlyDictionary<string, int> FirstNonZeroOffsets,
    IReadOnlyDictionary<string, int> FirstNonZeroFields,
    IReadOnlyDictionary<string, int> HeaderClassifications,
    IReadOnlyDictionary<string, int> HeaderPrefixes,
    IReadOnlyDictionary<string, int> PostPrefixLeads,
    IReadOnlyDictionary<string, int> MarkerBytes,
    IReadOnlyDictionary<string, int> InnerSelectors,
    IReadOnlyDictionary<string, int> OuterSelectors,
    IReadOnlyDictionary<string, int> ObjectClassifications,
    IReadOnlyDictionary<string, int> PayloadBytes,
    string SampleFile,
    int SampleLine);

public sealed record Protocol03NestedMovementParserActionSummary(
    string ParserAction,
    string ActionReason,
    int EvidenceRowCount,
    int WindowCount,
    int BoundedPrimaryWrapperCount,
    int UnresolvedPayloadLeadCount,
    IReadOnlyDictionary<string, int> EvidenceDispositions,
    IReadOnlyDictionary<string, int> MarkerModes,
    IReadOnlyDictionary<string, int> InnerSelectors,
    IReadOnlyDictionary<string, int> OuterSelectors,
    IReadOnlyDictionary<string, int> ObjectClassifications,
    IReadOnlyDictionary<string, int> SuffixPrefixes,
    IReadOnlyDictionary<string, int> PostSuffixPrefixes,
    IReadOnlyDictionary<string, int> PayloadBytes,
    IReadOnlyDictionary<string, int> PositionSamples,
    string SampleFile,
    int SampleLine);

public sealed record Protocol03MovementStateTailLead(
    string FirstSelector,
    string TailSelector,
    int Offset,
    int PayloadBytes,
    string TagHex,
    uint TagValue,
    string TagPayloadHex,
    string MarkerHex,
    string PostMarkerPrefixHex,
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
    int PostQuaternionBytes,
    string PostQuaternionMarkerHex,
    double? PostQuaternionX,
    double? PostQuaternionY,
    double? PostQuaternionZ,
    string PostQuaternionTailHex,
    string PostQuaternionTailField0Hex,
    uint? PostQuaternionTailField0,
    string PostQuaternionTailField1Hex,
    uint? PostQuaternionTailField1,
    string PostQuaternionTailSuffixHex,
    string PostQuaternionHex,
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

public sealed record VendorPriceEntry(
    uint GoId,
    string CodeName,
    uint VendorPrice,
    bool NonSellable,
    string File,
    int Line);

public sealed record AbilityDefinition(
    uint AbilityId,
    int SignedGoId,
    string CodeName,
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

public sealed record AnimationDefinition(
    string LittleEndianHex,
    string BigEndianHex,
    int Value,
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
    [property: JsonIgnore]
    IReadOnlyList<Protocol04PacketSequenceSample> Protocol04PacketSequences,
    [property: JsonIgnore]
    IReadOnlyList<Protocol03ObjectViewSample> Protocol03ObjectViews,
    [property: JsonIgnore]
    IReadOnlyList<Protocol04InteractionPayloadSample> Protocol04InteractionPayloads,
    [property: JsonIgnore]
    IReadOnlyList<PlayerAttributePayloadSample> PlayerAttributePayloads,
    [property: JsonIgnore]
    IReadOnlyList<ManageBonusPayloadSample> ManageBonusPayloads)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int Protocol04PacketSequenceCount => Protocol04PacketSequences.Count;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int Protocol03ObjectViewCount => Protocol03ObjectViews.Count;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int Protocol04InteractionPayloadCount => Protocol04InteractionPayloads.Count;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int PlayerAttributePayloadCount => PlayerAttributePayloads.Count;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int ManageBonusPayloadCount => ManageBonusPayloads.Count;

    [JsonIgnore]
    public IReadOnlyList<Protocol04ServerPayloadShapeSample>? Protocol04ServerPayloadShapes { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int Protocol04ServerPayloadShapeCount => Protocol04ServerPayloadShapes?.Count ?? 0;

    [JsonIgnore]
    public IReadOnlyList<VendorTransactionFlowSample>? VendorTransactionFlows { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int VendorTransactionFlowCount => VendorTransactionFlows?.Count ?? 0;

    [JsonIgnore]
    public IReadOnlyList<VendorOpenItemListSample>? VendorOpenItemLists { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int VendorOpenItemListCount => VendorOpenItemLists?.Count ?? 0;

    [JsonIgnore]
    public IReadOnlyList<VendorBuyTransactionSample>? VendorBuyTransactions { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int VendorBuyTransactionCount => VendorBuyTransactions?.Count ?? 0;

    [JsonIgnore]
    public IReadOnlyList<VendorSellTransactionSample>? VendorSellTransactions { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int VendorSellTransactionCount => VendorSellTransactions?.Count ?? 0;

    [JsonIgnore]
    public IReadOnlyList<Unknown8167PayloadSample>? Unknown8167Payloads { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int Unknown8167PayloadCount => Unknown8167Payloads?.Count ?? 0;

    [JsonIgnore]
    public IReadOnlyList<Unknown47PayloadSample>? Unknown47Payloads { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int Unknown47PayloadCount => Unknown47Payloads?.Count ?? 0;

    [JsonIgnore]
    public IReadOnlyList<Unknown6dPayloadSample>? Unknown6dPayloads { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int Unknown6dPayloadCount => Unknown6dPayloads?.Count ?? 0;

    [JsonIgnore]
    public IReadOnlyList<Unknown80c1PayloadSample>? Unknown80c1Payloads { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int Unknown80c1PayloadCount => Unknown80c1Payloads?.Count ?? 0;

    [JsonIgnore]
    public IReadOnlyList<LoadWorldPayloadSample>? LoadWorldPayloads { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int LoadWorldPayloadCount => LoadWorldPayloads?.Count ?? 0;

    [JsonIgnore]
    public IReadOnlyList<PlayerValuePayloadSample>? PlayerValuePayloads { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int PlayerValuePayloadCount => PlayerValuePayloads?.Count ?? 0;

    [JsonIgnore]
    public IReadOnlyList<FlashTrafficPayloadSample>? FlashTrafficPayloads { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int FlashTrafficPayloadCount => FlashTrafficPayloads?.Count ?? 0;

    [JsonIgnore]
    public IReadOnlyList<FeatureEventPayloadSample>? FeatureEventPayloads { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int FeatureEventPayloadCount => FeatureEventPayloads?.Count ?? 0;

    [JsonIgnore]
    public IReadOnlyList<FactionPlayerInfoPayloadSample>? FactionPlayerInfoPayloads { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int FactionPlayerInfoPayloadCount => FactionPlayerInfoPayloads?.Count ?? 0;

    [JsonIgnore]
    public IReadOnlyList<CrewMembersListPayloadSample>? CrewMembersListPayloads { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int CrewMembersListPayloadCount => CrewMembersListPayloads?.Count ?? 0;

    [JsonIgnore]
    public IReadOnlyList<FriendOnlinePayloadSample>? FriendOnlinePayloads { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int FriendOnlinePayloadCount => FriendOnlinePayloads?.Count ?? 0;

    [JsonIgnore]
    public IReadOnlyList<ChatMessageResponsePayloadSample>? ChatMessageResponsePayloads { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int ChatMessageResponsePayloadCount => ChatMessageResponsePayloads?.Count ?? 0;

    [JsonIgnore]
    public IReadOnlyList<WhereamiResponsePayloadSample>? WhereamiResponsePayloads { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int WhereamiResponsePayloadCount => WhereamiResponsePayloads?.Count ?? 0;

    [JsonIgnore]
    public IReadOnlyList<FactionNameResponsePayloadSample>? FactionNameResponsePayloads { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int FactionNameResponsePayloadCount => FactionNameResponsePayloads?.Count ?? 0;

    [JsonIgnore]
    public IReadOnlyList<AbilityUnloadPayloadSample>? AbilityUnloadPayloads { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int AbilityUnloadPayloadCount => AbilityUnloadPayloads?.Count ?? 0;

    [JsonIgnore]
    public IReadOnlyList<FriendListStatusPayloadSample>? FriendListStatusPayloads { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int FriendListStatusPayloadCount => FriendListStatusPayloads?.Count ?? 0;

    [JsonIgnore]
    public IReadOnlyList<CoderAttributePayloadSample>? CoderAttributePayloads { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int CoderAttributePayloadCount => CoderAttributePayloads?.Count ?? 0;

    [JsonIgnore]
    public IReadOnlyList<KnownHeaderContextSummary>? KnownHeaderContexts { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int KnownHeaderContextCount => KnownHeaderContexts?.Count ?? 0;
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
    public IReadOnlyList<AbilityDefinition> AbilityDefinitions { get; init; } = Array.Empty<AbilityDefinition>();

    [JsonIgnore]
    public IReadOnlyList<AnimationDefinition> AnimationDefinitions { get; init; } = Array.Empty<AnimationDefinition>();

    [JsonIgnore]
    public IReadOnlyList<ItemCommandEntry> ItemCommandEntries { get; init; } = Array.Empty<ItemCommandEntry>();

    [JsonIgnore]
    public IReadOnlyList<VendorPriceEntry> VendorPriceEntries { get; init; } = Array.Empty<VendorPriceEntry>();

    [JsonIgnore]
    public IReadOnlyList<StaticObjectEntry> StaticObjectEntries { get; init; } = Array.Empty<StaticObjectEntry>();

    public IReadOnlyList<VendorBuyPriceLeadSummary> VendorBuyPriceLeadSummaries { get; init; } = Array.Empty<VendorBuyPriceLeadSummary>();

    public IReadOnlyList<VendorSellPriceLeadSummary> VendorSellPriceLeadSummaries { get; init; } = Array.Empty<VendorSellPriceLeadSummary>();

    public IReadOnlyList<Protocol03SelectorFfRepeatListCandidate> Protocol03SelectorFfRepeatListCandidates { get; init; } = Array.Empty<Protocol03SelectorFfRepeatListCandidate>();

    public IReadOnlyList<Protocol03SelectorFfRepeatListFieldSummary> Protocol03SelectorFfRepeatListFieldSummaries { get; init; } = Array.Empty<Protocol03SelectorFfRepeatListFieldSummary>();

    public IReadOnlyList<Protocol03SelectorFfRepeatListShapeSummary> Protocol03SelectorFfRepeatListShapeSummaries { get; init; } = Array.Empty<Protocol03SelectorFfRepeatListShapeSummary>();

    public IReadOnlyList<Protocol03SelectorFfRepeatListEffectiveShapeSummary> Protocol03SelectorFfRepeatListEffectiveShapeSummaries { get; init; } = Array.Empty<Protocol03SelectorFfRepeatListEffectiveShapeSummary>();

    public IReadOnlyList<Protocol03SelectorFfRepeatListContinuationMarkerSummary> Protocol03SelectorFfRepeatListContinuationMarkerSummaries { get; init; } = Array.Empty<Protocol03SelectorFfRepeatListContinuationMarkerSummary>();

    public IReadOnlyList<Protocol03SelectorFfRepeatListVendorContinuationBodyOffsetSummary> Protocol03SelectorFfRepeatListVendorContinuationBodyOffsetSummaries { get; init; } = Array.Empty<Protocol03SelectorFfRepeatListVendorContinuationBodyOffsetSummary>();

    public IReadOnlyList<Protocol03SelectorFfRepeatListVendorPrePositionBodyOffsetSummary> Protocol03SelectorFfRepeatListVendorPrePositionBodyOffsetSummaries { get; init; } = Array.Empty<Protocol03SelectorFfRepeatListVendorPrePositionBodyOffsetSummary>();

    public IReadOnlyList<Protocol03SelectorFfRepeatListVendorBodyWindowProbeSummary> Protocol03SelectorFfRepeatListVendorBodyWindowProbeSummaries { get; init; } = Array.Empty<Protocol03SelectorFfRepeatListVendorBodyWindowProbeSummary>();

    public IReadOnlyList<Protocol03SelectorFfRepeatListVendorBodyWindowResourceCollisionSummary> Protocol03SelectorFfRepeatListVendorBodyWindowResourceCollisionSummaries { get; init; } = Array.Empty<Protocol03SelectorFfRepeatListVendorBodyWindowResourceCollisionSummary>();

    public IReadOnlyList<Protocol03SelectorFfRepeatListVendorBodyWindowEvidenceSummary> Protocol03SelectorFfRepeatListVendorBodyWindowEvidenceSummaries { get; init; } = Array.Empty<Protocol03SelectorFfRepeatListVendorBodyWindowEvidenceSummary>();

    public IReadOnlyList<Protocol03SelectorFfRepeatListVendorBodyWindowParserActionSummary> Protocol03SelectorFfRepeatListVendorBodyWindowParserActionSummaries { get; init; } = Array.Empty<Protocol03SelectorFfRepeatListVendorBodyWindowParserActionSummary>();

    public IReadOnlyList<Protocol03SelectorFfRepeatListEffectiveShapeHeaderSummary> Protocol03SelectorFfRepeatListEffectiveShapeHeaderSummaries { get; init; } = Array.Empty<Protocol03SelectorFfRepeatListEffectiveShapeHeaderSummary>();

    public IReadOnlyList<Protocol03SelectorFfRepeatListHeaderNameSummary> Protocol03SelectorFfRepeatListHeaderNameSummaries { get; init; } = Array.Empty<Protocol03SelectorFfRepeatListHeaderNameSummary>();

    public IReadOnlyList<Protocol03SelectorFfRepeatListHeaderRegionSummary> Protocol03SelectorFfRepeatListHeaderRegionSummaries { get; init; } = Array.Empty<Protocol03SelectorFfRepeatListHeaderRegionSummary>();

    public IReadOnlyList<Protocol03SelectorFfRepeatListVendorBoundaryTargetSummary> Protocol03SelectorFfRepeatListVendorBoundaryTargetSummaries { get; init; } = Array.Empty<Protocol03SelectorFfRepeatListVendorBoundaryTargetSummary>();

    public IReadOnlyList<Protocol03SelectorFfRepeatListVendorBoundaryInterpretationSummary> Protocol03SelectorFfRepeatListVendorBoundaryInterpretationSummaries { get; init; } = Array.Empty<Protocol03SelectorFfRepeatListVendorBoundaryInterpretationSummary>();

    public IReadOnlyList<Protocol03SelectorFfRepeatListVendorBoundaryPrioritySummary> Protocol03SelectorFfRepeatListVendorBoundaryPrioritySummaries { get; init; } = Array.Empty<Protocol03SelectorFfRepeatListVendorBoundaryPrioritySummary>();

    public IReadOnlyList<Protocol03SelectorFfRepeatListHeaderSample> Protocol03SelectorFfRepeatListHeaderSamples { get; init; } = Array.Empty<Protocol03SelectorFfRepeatListHeaderSample>();

    public IReadOnlyList<Protocol03SelectorFfRepeatListLayoutSummary> Protocol03SelectorFfRepeatListLayoutSummaries { get; init; } = Array.Empty<Protocol03SelectorFfRepeatListLayoutSummary>();

    public IReadOnlyList<Protocol03NestedMovementBoundaryEvidenceSummary> Protocol03NestedMovementBoundaryEvidenceSummaries { get; init; } = Array.Empty<Protocol03NestedMovementBoundaryEvidenceSummary>();

    public IReadOnlyList<Protocol03NestedMovementMode06BoundaryCandidateSummary> Protocol03NestedMovementMode06BoundaryCandidateSummaries { get; init; } = Array.Empty<Protocol03NestedMovementMode06BoundaryCandidateSummary>();

    public IReadOnlyList<Protocol03NestedMovementMode06FfContinuationSummary> Protocol03NestedMovementMode06FfContinuationSummaries { get; init; } = Array.Empty<Protocol03NestedMovementMode06FfContinuationSummary>();

    public IReadOnlyList<Protocol03NestedMovementMode06PostContinuationBoundarySummary> Protocol03NestedMovementMode06PostContinuationBoundarySummaries { get; init; } = Array.Empty<Protocol03NestedMovementMode06PostContinuationBoundarySummary>();

    public IReadOnlyList<Protocol03NestedMovementMode06PostContinuationBodySummary> Protocol03NestedMovementMode06PostContinuationBodySummaries { get; init; } = Array.Empty<Protocol03NestedMovementMode06PostContinuationBodySummary>();

    public IReadOnlyList<Protocol03NestedMovementMode06PostContinuationPrefixSummary> Protocol03NestedMovementMode06PostContinuationPrefixSummaries { get; init; } = Array.Empty<Protocol03NestedMovementMode06PostContinuationPrefixSummary>();

    public IReadOnlyList<Protocol03NestedMovementMode06PostContinuationRemainderSummary> Protocol03NestedMovementMode06PostContinuationRemainderSummaries { get; init; } = Array.Empty<Protocol03NestedMovementMode06PostContinuationRemainderSummary>();

    public IReadOnlyList<Protocol03NestedMovementMode06PostContinuationTupleSummary> Protocol03NestedMovementMode06PostContinuationTupleSummaries { get; init; } = Array.Empty<Protocol03NestedMovementMode06PostContinuationTupleSummary>();

    public IReadOnlyList<Protocol03NestedMovementMode06PostContinuationTupleBoundarySummary> Protocol03NestedMovementMode06PostContinuationTupleBoundarySummaries { get; init; } = Array.Empty<Protocol03NestedMovementMode06PostContinuationTupleBoundarySummary>();

    public IReadOnlyList<Protocol03NestedMovementMode06PostContinuationTupleBodySummary> Protocol03NestedMovementMode06PostContinuationTupleBodySummaries { get; init; } = Array.Empty<Protocol03NestedMovementMode06PostContinuationTupleBodySummary>();

    public IReadOnlyList<Protocol03NestedMovementMode06PostContinuationTerminalTupleBodySummary> Protocol03NestedMovementMode06PostContinuationTerminalTupleBodySummaries { get; init; } = Array.Empty<Protocol03NestedMovementMode06PostContinuationTerminalTupleBodySummary>();

    public IReadOnlyList<Protocol03NestedMovementMode06LongTupleBodyTailAnchorSummary> Protocol03NestedMovementMode06LongTupleBodyTailAnchorSummaries { get; init; } = Array.Empty<Protocol03NestedMovementMode06LongTupleBodyTailAnchorSummary>();

    public IReadOnlyList<Protocol03NestedMovementMode06LongTupleBodyPreAnchorLeadSummary> Protocol03NestedMovementMode06LongTupleBodyPreAnchorLeadSummaries { get; init; } = Array.Empty<Protocol03NestedMovementMode06LongTupleBodyPreAnchorLeadSummary>();

    public IReadOnlyList<Protocol03NestedMovementMode06LongTupleBodyPreAnchorBodySummary> Protocol03NestedMovementMode06LongTupleBodyPreAnchorBodySummaries { get; init; } = Array.Empty<Protocol03NestedMovementMode06LongTupleBodyPreAnchorBodySummary>();

    public IReadOnlyList<Protocol03NestedMovementMode06LongTupleBodyPreAnchorOffsetSummary> Protocol03NestedMovementMode06LongTupleBodyPreAnchorOffsetSummaries { get; init; } = Array.Empty<Protocol03NestedMovementMode06LongTupleBodyPreAnchorOffsetSummary>();

    public IReadOnlyList<Protocol03NestedMovementMode06LongTupleBodyPreAnchorHeaderCandidateSummary> Protocol03NestedMovementMode06LongTupleBodyPreAnchorHeaderCandidateSummaries { get; init; } = Array.Empty<Protocol03NestedMovementMode06LongTupleBodyPreAnchorHeaderCandidateSummary>();

    public IReadOnlyList<Protocol03NestedMovementMode06LongTupleBodyPreAnchorEmbeddedMovementSummary> Protocol03NestedMovementMode06LongTupleBodyPreAnchorEmbeddedMovementSummaries { get; init; } = Array.Empty<Protocol03NestedMovementMode06LongTupleBodyPreAnchorEmbeddedMovementSummary>();

    public IReadOnlyList<Protocol03NestedMovementMode06LongTupleBodyPreAnchorEmbeddedMovementTrailerSummary> Protocol03NestedMovementMode06LongTupleBodyPreAnchorEmbeddedMovementTrailerSummaries { get; init; } = Array.Empty<Protocol03NestedMovementMode06LongTupleBodyPreAnchorEmbeddedMovementTrailerSummary>();

    public IReadOnlyList<Protocol03NestedMovementMode06LongTupleBodyPreAnchorEmbeddedMovementTrailerShapeSummary> Protocol03NestedMovementMode06LongTupleBodyPreAnchorEmbeddedMovementTrailerShapeSummaries { get; init; } = Array.Empty<Protocol03NestedMovementMode06LongTupleBodyPreAnchorEmbeddedMovementTrailerShapeSummary>();

    public IReadOnlyList<Protocol03NestedMovementMode06TupleBodyParserActionSummary> Protocol03NestedMovementMode06TupleBodyParserActionSummaries { get; init; } = Array.Empty<Protocol03NestedMovementMode06TupleBodyParserActionSummary>();

    public IReadOnlyList<Protocol03NestedMovementParserActionSummary> Protocol03NestedMovementParserActionSummaries { get; init; } = Array.Empty<Protocol03NestedMovementParserActionSummary>();

    public IReadOnlyList<Protocol03NpcBaseDynamicCreationTailSummary> Protocol03NpcBaseDynamicCreationTailSummaries { get; init; } = Array.Empty<Protocol03NpcBaseDynamicCreationTailSummary>();

    public IReadOnlyList<Protocol03NpcBaseDynamicCreationHeaderLikeLeadSummary> Protocol03NpcBaseDynamicCreationHeaderLikeLeadSummaries { get; init; } = Array.Empty<Protocol03NpcBaseDynamicCreationHeaderLikeLeadSummary>();

    public IReadOnlyList<Protocol03NpcBaseDynamicCreationBodyAnchorSummary> Protocol03NpcBaseDynamicCreationBodyAnchorSummaries { get; init; } = Array.Empty<Protocol03NpcBaseDynamicCreationBodyAnchorSummary>();

    public IReadOnlyList<Protocol03NpcBaseDynamicCreationExtendedBoundarySummary> Protocol03NpcBaseDynamicCreationExtendedBoundarySummaries { get; init; } = Array.Empty<Protocol03NpcBaseDynamicCreationExtendedBoundarySummary>();
}
