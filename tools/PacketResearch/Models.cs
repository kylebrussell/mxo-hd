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
    IReadOnlyList<Unknown6dEntryRecord> EntryRecords,
    string PayloadHex);

public sealed record Unknown6dEntryRecord(
    string PrefixHex,
    int PrefixValue,
    string EntryIdHex,
    int EntryId,
    string TailHex);

public sealed record Unknown6dListRecordShapeSummary(
    int PayloadLength,
    string ModeHex,
    int ModeValue,
    string Field1,
    int Field1Value,
    int ListOffset,
    int? ListFlag,
    int? EntryCount,
    int? ParsedEntryCount,
    int PayloadCount,
    int TotalRecordCount,
    int DistinctEntryIdCount,
    IReadOnlyDictionary<string, int> RecordPrefixes,
    IReadOnlyDictionary<string, int> RecordTails,
    IReadOnlyDictionary<string, int> RecordTailU16Offset0Values,
    IReadOnlyDictionary<string, int> RecordTailU16Offset1Values,
    IReadOnlyDictionary<string, int> EntryIds,
    IReadOnlyDictionary<string, int> EntryIdResourceReferences,
    IReadOnlyDictionary<string, int> EntryIdAttributeReferences,
    IReadOnlyDictionary<string, int> CaptureScopes,
    IReadOnlyDictionary<string, int> TopPacketSequences,
    string SampleFile,
    int SampleLine,
    string SamplePayloadHex);

public sealed record Unknown6dEntryIdSummary(
    int EntryId,
    IReadOnlyDictionary<string, int> EntryIdHexes,
    int PayloadCount,
    int TotalRecordCount,
    IReadOnlyDictionary<string, int> Modes,
    IReadOnlyDictionary<string, int> PayloadShapes,
    IReadOnlyDictionary<string, int> RecordPrefixes,
    IReadOnlyDictionary<string, int> RecordTails,
    IReadOnlyDictionary<string, int> RecordTailU16Offset1Values,
    IReadOnlyDictionary<string, int> ResourceReferences,
    IReadOnlyDictionary<string, int> AttributeReferences,
    IReadOnlyDictionary<string, int> CaptureScopes,
    IReadOnlyDictionary<string, int> TopPacketSequences,
    string SampleFile,
    int SampleLine,
    string SamplePayloadHex);

public sealed record Unknown6dEntryTailFlagSummary(
    int EntryId,
    IReadOnlyDictionary<string, int> EntryIdHexes,
    string TailHex,
    IReadOnlyDictionary<string, int> TailU16Offset0Values,
    IReadOnlyDictionary<string, int> TailU16Offset1Values,
    int FlagRecordCount,
    int TotalEntryRecordCount,
    int FlagPayloadCount,
    int TotalEntryPayloadCount,
    IReadOnlyDictionary<string, int> Modes,
    IReadOnlyDictionary<string, int> PayloadShapes,
    IReadOnlyDictionary<string, int> ResourceReferences,
    IReadOnlyDictionary<string, int> AttributeReferences,
    IReadOnlyDictionary<string, int> CaptureScopes,
    IReadOnlyDictionary<string, int> TopPacketSequences,
    string SampleFile,
    int SampleLine,
    string SamplePayloadHex);

public sealed record Unknown6dPacketContextSummary(
    string ModeHex,
    int ModeValue,
    string Field1,
    int Field1Value,
    int PayloadCount,
    int PacketCount,
    int TotalRecordCount,
    int DistinctEntryIdCount,
    int SamePacket80c1PayloadCount,
    int NonzeroTailRecordCount,
    IReadOnlyDictionary<string, int> EntryCounts,
    IReadOnlyDictionary<string, int> PayloadShapes,
    IReadOnlyDictionary<string, int> PreviousHeaders,
    IReadOnlyDictionary<string, int> NextHeaders,
    IReadOnlyDictionary<string, int> SamePacket80c1Fields,
    IReadOnlyDictionary<string, int> RecordTails,
    IReadOnlyDictionary<string, int> EntryIdResourceReferences,
    IReadOnlyDictionary<string, int> EntryIdAttributeReferences,
    IReadOnlyDictionary<string, int> CaptureScopes,
    IReadOnlyDictionary<string, int> TopPacketSequences,
    string SampleFile,
    int SampleLine,
    string SamplePayloadHex);

public sealed record Unknown80c1PayloadSample(
    int Line,
    int PayloadLength,
    string Field0,
    int Field0Value,
    string Field1,
    int Field1Value,
    string PayloadHex);

public sealed record Unknown80c1PacketContextSummary(
    string Field0,
    int Field0Value,
    string Field1,
    int Field1Value,
    int PayloadCount,
    int PacketCount,
    int SamePacket6dPayloadCount,
    int SamePacket80b2PayloadCount,
    int SamePacket80b3PayloadCount,
    int SamePacket80bcPayloadCount,
    IReadOnlyDictionary<string, int> PayloadShapes,
    IReadOnlyDictionary<string, int> PreviousHeaders,
    IReadOnlyDictionary<string, int> NextHeaders,
    IReadOnlyDictionary<string, int> SamePacket6dShapes,
    IReadOnlyDictionary<string, int> SamePacket80b2Fields,
    IReadOnlyDictionary<string, int> SamePacket80b3Fields,
    IReadOnlyDictionary<string, int> SamePacket80bcShapes,
    IReadOnlyDictionary<string, int> Field0LocalReferences,
    IReadOnlyDictionary<string, int> Field1LocalReferences,
    IReadOnlyDictionary<string, int> CaptureScopes,
    IReadOnlyDictionary<string, int> TopPacketSequences,
    string SampleFile,
    int SampleLine,
    string SamplePayloadHex);

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

public sealed record ManageBonusTradeStatePayloadSample(
    int Line,
    int PayloadLength,
    string TradeField0,
    string TradeField1,
    int TradeField1Value,
    string TradeField2,
    int TradeField2Value,
    string RepeatedTradeField1,
    int RepeatedTradeField1Value,
    string VariantValue,
    int VariantValueInt,
    string VariantRole,
    string Byte15Value,
    int Byte15ValueInt,
    string StateName,
    string PayloadHex);

public sealed record ManageBonusTradeStatePayloadDecodeSummary(
    string VariantValue,
    int VariantValueInt,
    string VariantRole,
    string Byte15Value,
    int Byte15ValueInt,
    string StateName,
    int PayloadCount,
    IReadOnlyDictionary<string, int> CaptureScopes,
    string SampleFile,
    int SampleLine,
    string SamplePayloadHex);

public sealed record ManageBonusStateIdLongFormFieldLeadSummary(
    string Field0,
    string Field1,
    int Field1Value,
    IReadOnlyList<string> FieldReferences,
    int PayloadCount,
    int SamePacketB2Matches,
    int SamePacketB2ValueMirrors,
    int SamePacketB3Matches,
    IReadOnlyDictionary<string, int> Field2Values,
    IReadOnlyDictionary<string, int> Byte11Values,
    IReadOnlyDictionary<string, int> Byte15Values,
    int SamePacketVendorOrMarketHeaders,
    IReadOnlyDictionary<string, int> TopPacketSequences,
    string SampleFile,
    int SampleLine,
    string SamplePayloadHex);

public sealed record ManageBonusStateIdLongFormSemanticFamilySummary(
    string SemanticFamily,
    string Interpretation,
    int DistinctFields,
    int PayloadCount,
    int SamePacketB2Matches,
    int SamePacketB2ValueMirrors,
    int SamePacketB3Matches,
    int SamePacketVendorOrMarketHeaders,
    IReadOnlyDictionary<string, int> Field0Values,
    IReadOnlyDictionary<string, int> Field2Values,
    IReadOnlyDictionary<string, int> Byte11Values,
    IReadOnlyDictionary<string, int> Byte15Values,
    IReadOnlyDictionary<string, int> TopFieldReferences,
    IReadOnlyDictionary<string, int> TopFields,
    IReadOnlyDictionary<string, int> TopPacketSequences,
    string SampleField0,
    string SampleField1,
    int SampleField1Value,
    IReadOnlyList<string> SampleFieldReferences,
    string SampleFile,
    int SampleLine,
    string SamplePayloadHex);

public sealed record ManageBonusTradeStateValueSummary(
    string Field0,
    string Field1,
    int Field1Value,
    string Field2,
    int Field2Value,
    IReadOnlyDictionary<string, int> Field2References,
    string Byte11Value,
    int Byte11ValueInt,
    IReadOnlyDictionary<string, int> Byte11References,
    string Byte15Value,
    int Byte15ValueInt,
    int PayloadCount,
    int SamePacketB2Matches,
    int SamePacketB2ValueMirrors,
    IReadOnlyDictionary<string, int> SamePacketB2Field1Values,
    IReadOnlyDictionary<string, int> SamePacketB2Field2Values,
    int SamePacketOppositeByte15Rows,
    int SamePacketB3Matches,
    int SamePacketVendorOrMarketHeaders,
    int SamePacketObjectInteractionHeaders,
    IReadOnlyDictionary<string, int> CaptureScopes,
    IReadOnlyDictionary<string, int> TopPacketSequences,
    IReadOnlyDictionary<string, int> SampleFiles,
    string SampleFile,
    int SampleLine,
    string SamplePayloadHex);

public sealed record ManageBonusTradeStateByte15PairSummary(
    string Field0,
    string Field1,
    int Field1Value,
    string Field2,
    int Field2Value,
    string Byte11Value,
    int Byte11ValueInt,
    int OffPayloadCount,
    int OnPayloadCount,
    int SamePacketPairedRows,
    int OffSamePacketB2ValueMirrors,
    int OnSamePacketB2ValueMirrors,
    int OffSamePacketB3Matches,
    int OnSamePacketB3Matches,
    int SamePacketVendorOrMarketHeaders,
    IReadOnlyDictionary<string, int> LinkedB2Field1Values,
    IReadOnlyDictionary<string, int> LinkedB2Field2Values,
    IReadOnlyDictionary<string, int> CaptureScopes,
    IReadOnlyDictionary<string, int> TopPacketSequences,
    string OffSampleFile,
    int OffSampleLine,
    string OffSamplePayloadHex,
    string OnSampleFile,
    int OnSampleLine,
    string OnSamplePayloadHex);

public sealed record ManageBonusTradeStateByte15TransitionSummary(
    string Field0,
    string Field1,
    int Field1Value,
    string Field2,
    int Field2Value,
    string Byte11Value,
    int Byte11ValueInt,
    int PairedPacketCount,
    int PairCombinationCount,
    int OffBeforeOnPairs,
    int OnBeforeOffPairs,
    int AdjacentPairCombinations,
    int MinManageBonusRowDistance,
    int MaxManageBonusRowDistance,
    int AdjacentProtocolBlockPairs,
    int MinProtocolBlockDistance,
    int MaxProtocolBlockDistance,
    int SamePacketB2Packets,
    int SamePacketB3Packets,
    int SamePacketVendorOrMarketPackets,
    int SamePacketObjectInteractionPackets,
    int BetweenPairB2MirrorPairs,
    int PrecedingB3MatchPairs,
    int CompleteStateRefreshPairs,
    int B2OnlyStateRefreshPairs,
    int UnmatchedStateRefreshPairs,
    int OnCompanionRows,
    int OffCompanionRows,
    int BothCompanionRows,
    int OnCompanionByte15Matches,
    int OffCompanionByte15Matches,
    int SameCompanionField1Pairs,
    int SameCompanionByte11Pairs,
    IReadOnlyDictionary<string, int> CaptureScopes,
    IReadOnlyDictionary<string, int> TopPacketSequences,
    IReadOnlyDictionary<string, int> TopPairHeaderWindows,
    IReadOnlyDictionary<string, int> BetweenPairB2Values,
    IReadOnlyDictionary<string, int> PrecedingB3Values,
    IReadOnlyDictionary<string, int> OnCompanionField0Values,
    IReadOnlyDictionary<string, int> OffCompanionField0Values,
    IReadOnlyDictionary<string, int> CompanionField0Pairs,
    IReadOnlyDictionary<string, int> OnCompanionField1Values,
    IReadOnlyDictionary<string, int> OffCompanionField1Values,
    IReadOnlyDictionary<string, int> CompanionField1Pairs,
    IReadOnlyDictionary<string, int> OnCompanionByte11Values,
    IReadOnlyDictionary<string, int> OffCompanionByte11Values,
    IReadOnlyDictionary<string, int> CompanionByte11Pairs,
    IReadOnlyList<ManageBonusTradeStateCompanionMapFieldSummary> CompanionMapFieldSummaries,
    IReadOnlyDictionary<string, int> OnCompanionShapes,
    IReadOnlyDictionary<string, int> OffCompanionShapes,
    IReadOnlyDictionary<string, int> CompanionShapePairs,
    IReadOnlyDictionary<string, int> SampleFiles,
    string SampleFile,
    int SampleLine,
    int OffManageBonusIndex,
    int OnManageBonusIndex,
    string OffSamplePayloadHex,
    string OnSamplePayloadHex);

public sealed record ManageBonusTradeStateCompanionMapFieldSummary(
    string OnCompanionByte11Value,
    int OnCompanionByte11ValueInt,
    string OffCompanionByte11Value,
    int OffCompanionByte11ValueInt,
    int CompanionPairCombinationCount,
    int SameCompanionField1Pairs,
    int CompanionField0PairDistinctCount,
    int CompanionField1PairDistinctCount,
    int ListedCompanionField1PairCount,
    int OnCompanionField1DistinctCount,
    int OffCompanionField1DistinctCount,
    IReadOnlyDictionary<string, int> CompanionField0Pairs,
    IReadOnlyDictionary<string, int> CompanionField1Pairs,
    IReadOnlyDictionary<string, int> CompanionField1PairRepeatBuckets,
    IReadOnlyDictionary<string, int> OnCompanionField1Values,
    IReadOnlyDictionary<string, int> OffCompanionField1Values);

public sealed record ManageBonusTradeStateCompanionMapSummary(
    string TradeByte11Value,
    int TradeByte11ValueInt,
    string OnCompanionByte11Value,
    int OnCompanionByte11ValueInt,
    string OffCompanionByte11Value,
    int OffCompanionByte11ValueInt,
    IReadOnlyDictionary<string, int> CompanionByte11PairReferences,
    IReadOnlyDictionary<string, int> OnCompanionByte11References,
    IReadOnlyDictionary<string, int> OffCompanionByte11References,
    int CompanionPairCombinationCount,
    int TransitionPairCombinationCount,
    int SameCompanionByte11Pairs,
    int TransitionCompleteStateRefreshPairs,
    int TransitionB2OnlyStateRefreshPairs,
    int TransitionUnmatchedStateRefreshPairs,
    int SameCompanionField1Pairs,
    int CompanionField0PairDistinctCount,
    int CompanionField1PairDistinctCount,
    int ListedCompanionField1PairCount,
    int OnCompanionField1DistinctCount,
    int OffCompanionField1DistinctCount,
    IReadOnlyDictionary<string, int> CompanionField0Pairs,
    IReadOnlyDictionary<string, int> CompanionField1Pairs,
    IReadOnlyDictionary<string, int> CompanionField1PairRepeatBuckets,
    IReadOnlyDictionary<string, int> CompanionField1PairReferences,
    IReadOnlyDictionary<string, int> OnCompanionField1Values,
    IReadOnlyDictionary<string, int> OffCompanionField1Values,
    IReadOnlyDictionary<string, int> OnCompanionField1References,
    IReadOnlyDictionary<string, int> OffCompanionField1References,
    IReadOnlyDictionary<string, int> CaptureScopes,
    IReadOnlyDictionary<string, int> TopPairHeaderWindows,
    string SampleFile,
    int SampleLine);

public sealed record ManageBonusTradeStateParserActionSummary(
    string ParserAction,
    string ActionReason,
    int CompanionMapRows,
    int TransitionPairCombinationCount,
    int CompanionPairCombinationCount,
    int StableCompanionByte11Pairs,
    int CompleteStateRefreshPairs,
    int B2OnlyStateRefreshPairs,
    int UnmatchedStateRefreshPairs,
    int SameCompanionField1Pairs,
    int CompanionField1PairDistinctCount,
    IReadOnlyDictionary<string, int> TradeByte11Values,
    IReadOnlyDictionary<string, int> CompanionByte11Pairs,
    IReadOnlyDictionary<string, int> CompanionField0Pairs,
    IReadOnlyDictionary<string, int> CompanionField1PairRepeatBuckets,
    IReadOnlyDictionary<string, int> CaptureScopes,
    IReadOnlyDictionary<string, int> TopPairHeaderWindows,
    string SampleFile,
    int SampleLine);

public sealed record ManageBonusTradeStateSerializedTransitionSummary(
    string ParserAction,
    string ActionReason,
    string TradeField0,
    string TradeField1,
    int TradeField1Value,
    string TradeField2,
    int TradeField2Value,
    string TradeByte11Value,
    int TradeByte11ValueInt,
    string TradeOnShape,
    string TradeOffShape,
    string OnCompanionByte11Value,
    int OnCompanionByte11ValueInt,
    string OffCompanionByte11Value,
    int OffCompanionByte11ValueInt,
    int TransitionPairCombinationCount,
    int CompanionPairCombinationCount,
    int StableCompanionByte11Pairs,
    int CompleteStateRefreshPairs,
    int B2OnlyStateRefreshPairs,
    int UnmatchedStateRefreshPairs,
    string RefreshClassification,
    IReadOnlyDictionary<string, int> BetweenPairB2Values,
    IReadOnlyDictionary<string, int> PrecedingB3Values,
    IReadOnlyDictionary<string, int> CompanionField0Pairs,
    IReadOnlyDictionary<string, int> CompanionField1Pairs,
    IReadOnlyDictionary<string, int> CompanionField1PairRepeatBuckets,
    IReadOnlyDictionary<string, int> CaptureScopes,
    IReadOnlyDictionary<string, int> TopPairHeaderWindows,
    string SampleFile,
    int SampleLine);

public sealed record ManageBonusTradeStateSerializedTransitionSample(
    string ParserAction,
    string ActionReason,
    string RefreshClassification,
    int ObservedPairCount,
    string TradeField0,
    string TradeField1,
    int TradeField1Value,
    string TradeField2,
    int TradeField2Value,
    string TradeByte11Value,
    int TradeByte11ValueInt,
    string TradeOnShape,
    string TradeOffShape,
    string OnCompanionByte11Value,
    int OnCompanionByte11ValueInt,
    string OffCompanionByte11Value,
    int OffCompanionByte11ValueInt,
    string? OnCompanionShape,
    string? OffCompanionShape,
    int RowDistance,
    int ProtocolBlockDistance,
    string PairHeaderWindow,
    IReadOnlyList<string> BetweenPairB2Values,
    string? PrecedingB3Value,
    string CaptureScope,
    string PacketSequence,
    bool HasSamePacketB2,
    bool HasSamePacketB3,
    string SampleFile,
    int SampleLine,
    int OffManageBonusIndex,
    int OnManageBonusIndex,
    string OffSamplePayloadHex,
    string OnSamplePayloadHex);

public sealed record ManageBonusTradeStateSerializedTransitionParserCoverageSummary(
    string ParserAction,
    string RefreshClassification,
    string ParserCase,
    int SampleRows,
    int ObservedPairCount,
    IReadOnlyDictionary<string, int> TradeByte11Values,
    IReadOnlyDictionary<string, int> CompanionByte11Pairs,
    IReadOnlyDictionary<string, int> TradeShapePairs,
    IReadOnlyDictionary<string, int> BetweenPairB2Values,
    IReadOnlyDictionary<string, int> PrecedingB3Values,
    IReadOnlyDictionary<string, int> PairHeaderWindows,
    IReadOnlyDictionary<string, int> CaptureScopes,
    string SampleFile,
    int SampleLine);

public sealed record ManageBonusTradeStateSerializedTransitionFieldRoleSummary(
    string FieldRole,
    string Interpretation,
    int ObservedPairCount,
    IReadOnlyDictionary<string, int> Values,
    IReadOnlyDictionary<string, int> ParserCases,
    string SampleFile,
    int SampleLine);

public sealed record ManageBonusTradeStateSerializedTransitionDecoderRow(
    string ParserAction,
    string RefreshClassification,
    string ParserCase,
    int ObservedPairCount,
    string TradeField0,
    string TradeField1,
    int TradeField1Value,
    string TradeField2,
    int TradeField2Value,
    string TradeByte11Value,
    int TradeByte11ValueInt,
    string OnTradeByte15Value,
    int OnTradeByte15ValueInt,
    string OffTradeByte15Value,
    int OffTradeByte15ValueInt,
    string BetweenPairB2Field0,
    int BetweenPairB2Field0Value,
    string BetweenPairB2Field1,
    int BetweenPairB2Field1Value,
    string BetweenPairB2Field2,
    int BetweenPairB2Field2Value,
    string? PrecedingB3Value,
    int? PrecedingB3ValueInt,
    string? OnCompanionField0,
    string? OffCompanionField0,
    string? OnCompanionByte11Value,
    int? OnCompanionByte11ValueInt,
    string? OffCompanionByte11Value,
    int? OffCompanionByte11ValueInt,
    string? OnCompanionByte15Value,
    int? OnCompanionByte15ValueInt,
    string? OffCompanionByte15Value,
    int? OffCompanionByte15ValueInt,
    int ProtocolBlockDistance,
    string PairHeaderWindow,
    string SampleFile,
    int SampleLine);

public sealed record ManageBonusTradeStateSerializedTransitionDetection(
    string ParserAction,
    string RefreshClassification,
    string ParserCase,
    string VariantValue,
    int VariantValueInt,
    string VariantRole,
    string TradeOnShape,
    string TradeOffShape,
    int RowDistance,
    int ProtocolBlockDistance,
    string PairHeaderWindow,
    IReadOnlyList<string> BetweenPairB2Values,
    string? PrecedingB3Value,
    string? OnCompanionShape,
    string? OffCompanionShape,
    string CaptureScope,
    string PacketSequence,
    bool HasSamePacketB2,
    bool HasSamePacketB3,
    string SampleFile,
    int SampleLine,
    int OnManageBonusIndex,
    int OffManageBonusIndex,
    string OnSamplePayloadHex,
    string OffSamplePayloadHex);

public sealed record ManageBonusTradeStateSerializedTransitionDetectionActionSummary(
    string ParserAction,
    string RefreshClassification,
    string ParserCase,
    int DetectionCount,
    int DetectionsWithCompanionShapes,
    int SamePacketB2Count,
    int SamePacketB3Count,
    IReadOnlyDictionary<string, int> VariantValues,
    IReadOnlyDictionary<string, int> VariantRoles,
    IReadOnlyDictionary<string, int> TradeShapePairs,
    IReadOnlyDictionary<string, int> CompanionShapePairs,
    IReadOnlyDictionary<string, int> BetweenPairB2Values,
    IReadOnlyDictionary<string, int> PrecedingB3Values,
    IReadOnlyDictionary<string, int> PairHeaderWindows,
    IReadOnlyDictionary<string, int> CaptureScopes,
    string SampleFile,
    int SampleLine);

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

public sealed record Protocol03NpcBaseDynamicCreationExtendedBoundaryParserActionSummary(
    string ParserAction,
    int BoundaryRowCount,
    int CandidateCount,
    IReadOnlyDictionary<string, int> BoundaryInterpretations,
    IReadOnlyDictionary<string, int> HeaderClassifications,
    IReadOnlyDictionary<string, int> TailHeaderOffsets,
    IReadOnlyDictionary<string, int> HeaderPrefixes,
    IReadOnlyDictionary<string, int> PreHeaderPrefixes,
    IReadOnlyDictionary<string, int> TailAvailableBytes,
    IReadOnlyDictionary<string, int> TailFamilies,
    IReadOnlyDictionary<string, int> ParentPreValues,
    IReadOnlyDictionary<string, int> TextSamples,
    string SampleFile,
    int SampleLine);

public sealed record Protocol03NpcBaseDynamicCreationExtendedBoundaryBodyShapeSummary(
    string BoundaryInterpretation,
    string ParserAction,
    int BodyByteCount,
    string SeparatorOffset,
    int PreSeparatorByteCount,
    int PostSeparatorByteCount,
    int BoundaryRowCount,
    int CandidateCount,
    IReadOnlyDictionary<string, int> HeaderClassifications,
    IReadOnlyDictionary<string, int> TailHeaderOffsets,
    IReadOnlyDictionary<string, int> HeaderPrefixes,
    IReadOnlyDictionary<string, int> PreSeparatorPrefixes,
    IReadOnlyDictionary<string, int> PostSeparatorPrefixes,
    IReadOnlyDictionary<string, int> TailAvailableBytes,
    IReadOnlyDictionary<string, int> TailFamilies,
    IReadOnlyDictionary<string, int> ParentPreValues,
    IReadOnlyDictionary<string, int> TextSamples,
    string SampleFile,
    int SampleLine);

public sealed record Protocol03NpcBaseDynamicCreationExtendedBoundaryPostSeparatorFieldSummary(
    string BoundaryInterpretation,
    string ParserAction,
    int BodyByteCount,
    int SeparatorOffset,
    int PostSeparatorByteCount,
    string PostSeparatorLeadByteHex,
    string PostSeparatorSecondByteHex,
    int ZeroRunAfterSecondByte,
    int? FirstNonZeroAfterSecondByteOffset,
    int BoundaryRowCount,
    int CandidateCount,
    IReadOnlyDictionary<string, int> HeaderClassifications,
    IReadOnlyDictionary<string, int> TailHeaderOffsets,
    IReadOnlyDictionary<string, int> HeaderPrefixes,
    IReadOnlyDictionary<string, int> PostSeparatorPrefixes,
    IReadOnlyDictionary<string, int> FirstNonZeroWindows,
    IReadOnlyDictionary<string, int> TailAvailableBytes,
    IReadOnlyDictionary<string, int> TailFamilies,
    IReadOnlyDictionary<string, int> ParentPreValues,
    IReadOnlyDictionary<string, int> TextSamples,
    string SampleFile,
    int SampleLine);

public sealed record Protocol03NpcBaseDynamicCreationExtendedBoundaryPostSeparatorVectorProbeSummary(
    string BoundaryInterpretation,
    string ParserAction,
    int BodyByteCount,
    int SeparatorOffset,
    int PostSeparatorByteCount,
    string PostSeparatorLeadByteHex,
    string PostSeparatorSecondByteHex,
    int ZeroRunAfterSecondByte,
    int VectorOffset,
    int PreVectorBridgeOffset,
    int PreVectorBridgeByteCount,
    string VectorEncoding,
    string VectorDisposition,
    int BoundaryRowCount,
    int CandidateCount,
    IReadOnlyDictionary<string, int> PreVectorBridges,
    IReadOnlyDictionary<string, int> VectorSamples,
    IReadOnlyDictionary<string, int> VectorWindows,
    IReadOnlyDictionary<string, int> FirstNonZeroWindows,
    IReadOnlyDictionary<string, int> HeaderClassifications,
    IReadOnlyDictionary<string, int> TailHeaderOffsets,
    IReadOnlyDictionary<string, int> HeaderPrefixes,
    IReadOnlyDictionary<string, int> TailAvailableBytes,
    IReadOnlyDictionary<string, int> TailFamilies,
    IReadOnlyDictionary<string, int> ParentPreValues,
    IReadOnlyDictionary<string, int> TextSamples,
    string SampleFile,
    int SampleLine);

public sealed record Protocol03NpcBaseDynamicCreationExtendedBoundaryPostSeparatorVectorTailSummary(
    string BoundaryInterpretation,
    string ParserAction,
    int BodyByteCount,
    int SeparatorOffset,
    int PostSeparatorByteCount,
    string PostSeparatorLeadByteHex,
    string PostSeparatorSecondByteHex,
    int ZeroRunAfterSecondByte,
    int VectorOffset,
    int PreVectorBridgeOffset,
    int PreVectorBridgeByteCount,
    string VectorEncoding,
    string VectorDisposition,
    int TailOffset,
    int TailByteCount,
    string TailFloatEncoding,
    string TailDisposition,
    int BoundaryRowCount,
    int CandidateCount,
    IReadOnlyDictionary<string, int> PreVectorBridges,
    IReadOnlyDictionary<string, int> VectorSamples,
    IReadOnlyDictionary<string, int> TailHexes,
    IReadOnlyDictionary<string, int> TailFloatSamples,
    IReadOnlyDictionary<string, int> HeaderClassifications,
    IReadOnlyDictionary<string, int> TailHeaderOffsets,
    IReadOnlyDictionary<string, int> HeaderPrefixes,
    IReadOnlyDictionary<string, int> TailAvailableBytes,
    IReadOnlyDictionary<string, int> TailFamilies,
    IReadOnlyDictionary<string, int> ParentPreValues,
    IReadOnlyDictionary<string, int> TextSamples,
    string SampleFile,
    int SampleLine);

public sealed record Protocol03NpcBaseDynamicCreationExtendedBoundaryPostSeparatorLayoutActionSummary(
    string ParserAction,
    string ActionReason,
    int LayoutRowCount,
    int CandidateCount,
    IReadOnlyDictionary<string, int> BoundaryInterpretations,
    IReadOnlyDictionary<string, int> SourceParserActions,
    IReadOnlyDictionary<string, int> BodyByteCounts,
    IReadOnlyDictionary<string, int> PostSeparatorLeadBytes,
    IReadOnlyDictionary<string, int> PostSeparatorSecondBytes,
    IReadOnlyDictionary<string, int> ZeroRunsAfterSecondByte,
    IReadOnlyDictionary<string, int> PreVectorBridgeByteCounts,
    IReadOnlyDictionary<string, int> PreVectorBridges,
    IReadOnlyDictionary<string, int> VectorDispositions,
    IReadOnlyDictionary<string, int> VectorSamples,
    IReadOnlyDictionary<string, int> TailByteCounts,
    IReadOnlyDictionary<string, int> TailDispositions,
    IReadOnlyDictionary<string, int> TailFloatSamples,
    IReadOnlyDictionary<string, int> HeaderClassifications,
    IReadOnlyDictionary<string, int> HeaderPrefixes,
    IReadOnlyDictionary<string, int> TailAvailableBytes,
    IReadOnlyDictionary<string, int> TailFamilies,
    IReadOnlyDictionary<string, int> ParentPreValues,
    IReadOnlyDictionary<string, int> TextSamples,
    string SampleFile,
    int SampleLine);

public sealed record Protocol03NpcBaseDynamicCreationExtendedBoundaryPostSeparatorReviewedBodyLayoutSummary(
    string LayoutAction,
    string LayoutInterpretation,
    string FieldLayout,
    int BodyByteCount,
    int SeparatorOffset,
    int PostSeparatorByteCount,
    string PostSeparatorLeadByteHex,
    string PostSeparatorSecondByteHex,
    int ZeroRunAfterSecondByte,
    int PreVectorBridgeOffset,
    int PreVectorBridgeByteCount,
    int VectorOffset,
    string VectorEncoding,
    int TailOffset,
    int TailByteCount,
    string TailFloatEncoding,
    string TailDisposition,
    int LayoutRowCount,
    int CandidateCount,
    IReadOnlyDictionary<string, int> BoundaryInterpretations,
    IReadOnlyDictionary<string, int> SourceParserActions,
    IReadOnlyDictionary<string, int> PreVectorBridges,
    IReadOnlyDictionary<string, int> VectorSamples,
    IReadOnlyDictionary<string, int> TailHexes,
    IReadOnlyDictionary<string, int> TailFloatSamples,
    IReadOnlyDictionary<string, int> HeaderClassifications,
    IReadOnlyDictionary<string, int> HeaderPrefixes,
    IReadOnlyDictionary<string, int> TailAvailableBytes,
    IReadOnlyDictionary<string, int> TailFamilies,
    IReadOnlyDictionary<string, int> ParentPreValues,
    IReadOnlyDictionary<string, int> TextSamples,
    string SampleFile,
    int SampleLine);

public sealed record Protocol03NpcBaseDynamicCreationExtendedBoundaryPostSeparatorVectorSemanticSummary(
    string SemanticAction,
    string SemanticReason,
    string ParentPositionRelation,
    int CandidateCount,
    IReadOnlyDictionary<string, int> FieldLayouts,
    IReadOnlyDictionary<string, int> BodyByteCounts,
    IReadOnlyDictionary<string, int> PostSeparatorLeadBytes,
    IReadOnlyDictionary<string, int> PostSeparatorSecondBytes,
    IReadOnlyDictionary<string, int> ZeroRunsAfterSecondByte,
    IReadOnlyDictionary<string, int> PreVectorBridgeByteCounts,
    IReadOnlyDictionary<string, int> PreVectorBridges,
    IReadOnlyDictionary<string, int> VectorSamples,
    IReadOnlyDictionary<string, int> ParentPositionSamples,
    IReadOnlyDictionary<string, int> VectorToParentDistanceBuckets,
    IReadOnlyDictionary<string, int> VectorToParentDistanceSamples,
    IReadOnlyDictionary<string, int> TailDispositions,
    IReadOnlyDictionary<string, int> TailFloatSamples,
    IReadOnlyDictionary<string, int> HeaderClassifications,
    IReadOnlyDictionary<string, int> HeaderPrefixes,
    IReadOnlyDictionary<string, int> TextSamples,
    string SampleFile,
    int SampleLine);

public sealed record Protocol03NpcBaseDynamicCreationExtendedBoundaryPostSeparatorTailSemanticSummary(
    string SemanticAction,
    string SemanticReason,
    string TailFloatKind,
    int CandidateCount,
    IReadOnlyDictionary<string, int> FieldLayouts,
    IReadOnlyDictionary<string, int> TailByteCounts,
    IReadOnlyDictionary<string, int> TailFloatEncodings,
    IReadOnlyDictionary<string, int> TailDispositions,
    IReadOnlyDictionary<string, int> TailAxisPatterns,
    IReadOnlyDictionary<string, int> TailPlanarAxisPatterns,
    IReadOnlyDictionary<string, int> TailMagnitudeBuckets,
    IReadOnlyDictionary<string, int> TailMagnitudeSamples,
    IReadOnlyDictionary<string, int> TailFloatSamples,
    IReadOnlyDictionary<string, int> VectorSamples,
    IReadOnlyDictionary<string, int> PreVectorBridges,
    IReadOnlyDictionary<string, int> HeaderClassifications,
    IReadOnlyDictionary<string, int> HeaderPrefixes,
    IReadOnlyDictionary<string, int> TextSamples,
    string SampleFile,
    int SampleLine);

public sealed record Protocol03NpcBaseDynamicCreationExtendedBoundaryPostSeparatorControlSemanticSummary(
    string ControlAction,
    string ControlReason,
    string ParentPositionRelation,
    int CandidateCount,
    IReadOnlyDictionary<string, int> FieldLayouts,
    IReadOnlyDictionary<string, int> BodyByteCounts,
    IReadOnlyDictionary<string, int> PostSeparatorLeadBytes,
    IReadOnlyDictionary<string, int> PostSeparatorSecondBytes,
    IReadOnlyDictionary<string, int> ZeroRunsAfterSecondByte,
    IReadOnlyDictionary<string, int> PreVectorBridgeByteCounts,
    IReadOnlyDictionary<string, int> PreVectorBridges,
    IReadOnlyDictionary<string, int> VectorSamples,
    IReadOnlyDictionary<string, int> ParentPositionSamples,
    IReadOnlyDictionary<string, int> VectorToParentDistanceBuckets,
    IReadOnlyDictionary<string, int> VectorToParentDistanceSamples,
    IReadOnlyDictionary<string, int> TailByteCounts,
    IReadOnlyDictionary<string, int> TailFloatEncodings,
    IReadOnlyDictionary<string, int> TailDispositions,
    IReadOnlyDictionary<string, int> TailFloatSamples,
    IReadOnlyDictionary<string, int> HeaderClassifications,
    IReadOnlyDictionary<string, int> HeaderPrefixes,
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

public sealed record Protocol03SelectorFfRepeatListDecodedRow(
    string ParserAction,
    string ContinuationMarkerRole,
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
    string PositionHex,
    double X,
    double Y,
    double Z,
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
    int ContinuationMarkerOffset,
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

public sealed record Protocol03SelectorFfRepeatListEntryPairSummary(
    int RepeatStride,
    int RepeatRecordCount,
    int ContinuationOffset,
    string ContinuationPrefixHex,
    string Entry1FieldHex,
    int? Entry1FieldValue,
    string Entry2EffectiveFieldHex,
    int? Entry2EffectiveFieldValue,
    int CandidateCount,
    int DistinctPostMarkerFieldCount,
    IReadOnlyDictionary<string, int> Entry2EffectiveSources,
    IReadOnlyDictionary<string, int> Entry1Layouts,
    IReadOnlyDictionary<string, int> Entry2Layouts,
    IReadOnlyDictionary<string, int> PostMarkerFields,
    IReadOnlyDictionary<string, int> PostMarkerLayouts,
    IReadOnlyDictionary<string, int> UpdateCounts,
    IReadOnlyDictionary<string, int> FirstSelectors,
    IReadOnlyDictionary<string, int> SegmentClassifications,
    IReadOnlyDictionary<string, int> Entry1ResourceReferences,
    IReadOnlyDictionary<string, int> Entry2EffectiveResourceReferences,
    IReadOnlyDictionary<string, int> PostMarkerResourceReferences,
    IReadOnlyDictionary<string, int> CaptureScopes,
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

public sealed record Protocol03SelectorFfRepeatListContinuationParserActionSummary(
    string ParserAction,
    string ActionReason,
    int MarkerRowCount,
    int CandidateCount,
    int ShapeCount,
    int CandidatesWithHeaderWindows,
    int CandidatesWithVendorHeaderWindows,
    int HeaderWindowCount,
    int VendorHeaderWindowCount,
    IReadOnlyDictionary<string, int> ContinuationPrefixBeforeMarkers,
    IReadOnlyDictionary<string, int> ContinuationPrefixes,
    IReadOnlyDictionary<string, int> PostMarkerLayouts,
    IReadOnlyDictionary<string, int> PostMarkerFields,
    IReadOnlyDictionary<string, int> Entry1Fields,
    IReadOnlyDictionary<string, int> Entry2PrimaryFields,
    IReadOnlyDictionary<string, int> Entry2SecondaryFields,
    IReadOnlyDictionary<string, int> Entry2EffectiveFields,
    IReadOnlyDictionary<string, int> Entry2EffectiveSources,
    IReadOnlyDictionary<string, int> HeaderRegions,
    IReadOnlyDictionary<string, int> VendorHeaderWindows,
    IReadOnlyDictionary<string, int> FirstSelectors,
    IReadOnlyDictionary<string, int> SegmentClassifications,
    string SampleFile,
    int SampleLine);

public sealed record Protocol03SelectorFfRepeatListParserCoverageSummary(
    string ParserCase,
    string CoverageDisposition,
    int ActionRowCount,
    int MarkerRowCount,
    int CoveredCandidateCount,
    int ShapeCount,
    int CandidatesWithHeaderWindows,
    int CandidatesWithVendorHeaderWindows,
    int HeaderWindowCount,
    int VendorHeaderWindowCount,
    IReadOnlyDictionary<string, int> ParserActions,
    IReadOnlyDictionary<string, int> ActionReasons,
    IReadOnlyDictionary<string, int> ContinuationPrefixBeforeMarkers,
    IReadOnlyDictionary<string, int> ContinuationPrefixes,
    IReadOnlyDictionary<string, int> PostMarkerLayouts,
    IReadOnlyDictionary<string, int> PostMarkerFields,
    IReadOnlyDictionary<string, int> Entry1Fields,
    IReadOnlyDictionary<string, int> Entry2EffectiveFields,
    IReadOnlyDictionary<string, int> Entry2EffectiveSources,
    IReadOnlyDictionary<string, int> HeaderRegions,
    IReadOnlyDictionary<string, int> VendorHeaderWindows,
    IReadOnlyDictionary<string, int> FirstSelectors,
    IReadOnlyDictionary<string, int> SegmentClassifications,
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

public sealed record Protocol03SelectorFfRepeatListVendorBoundaryDecisionRow(
    string ParserDecision,
    string DecisionReason,
    string EvidenceDisposition,
    string ParserPriority,
    bool IsModeledFieldOverlap,
    string TargetInterpretation,
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
    IReadOnlyDictionary<string, int> WindowHexes,
    IReadOnlyDictionary<string, int> ContinuationPrefixes,
    IReadOnlyDictionary<string, int> LowHalfResourceReferences,
    IReadOnlyDictionary<string, int> HighHalfResourceReferences,
    IReadOnlyDictionary<string, int> NearbyU16LengthMatches,
    IReadOnlyDictionary<string, int> Entry1Fields,
    IReadOnlyDictionary<string, int> Entry1ResourceReferences,
    IReadOnlyDictionary<string, int> Entry2EffectiveFields,
    IReadOnlyDictionary<string, int> Entry2EffectiveResourceReferences,
    IReadOnlyDictionary<string, int> PostMarkerFields,
    IReadOnlyDictionary<string, int> PostMarkerResourceReferences,
    IReadOnlyDictionary<string, int> FirstSelectors,
    string SampleFile,
    int SampleLine);

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

public sealed record Protocol03NestedMovementMode06UnsupportedEmbeddedControlSelectorSummary(
    string MovementDisposition,
    string InnerSelectorHex,
    int WindowCount,
    IReadOnlyDictionary<string, int> ShapeDispositions,
    IReadOnlyDictionary<string, int> TrailerDispositions,
    IReadOnlyDictionary<string, int> PostMovementLeads,
    IReadOnlyDictionary<string, int> PostMovementNextLeads,
    IReadOnlyDictionary<string, int> BytesRemainingAfterMovement,
    IReadOnlyDictionary<string, int> PreTailFieldOffsets,
    IReadOnlyDictionary<string, int> PreTailFields,
    IReadOnlyDictionary<string, int> PreAnchorPreLeads,
    IReadOnlyDictionary<string, int> PreAnchorTrailingLeads,
    IReadOnlyDictionary<string, int> TailAnchorKinds,
    IReadOnlyDictionary<string, int> ParserTargets,
    IReadOnlyDictionary<string, int> PreAnchorBodyBytes,
    IReadOnlyDictionary<string, int> BodyBytes,
    IReadOnlyDictionary<string, int> PostMovementNonZeroByteOffsets,
    string SampleFile,
    int SampleLine);

public sealed record Protocol03NestedMovementMode06HeldTupleBodySummary(
    string ParserAction,
    string ActionReason,
    string SourceDisposition,
    string TailPrefixKind,
    string PrefixFirstNonZeroFieldHex,
    string TupleLayoutKind,
    string PostPrefixDisposition,
    string BodyDisposition,
    int BodyBytes,
    int WindowCount,
    IReadOnlyDictionary<string, int> BodyPrefixes,
    IReadOnlyDictionary<string, int> BodySuffixes,
    IReadOnlyDictionary<string, int> FirstNonZeroOffsets,
    IReadOnlyDictionary<string, int> FirstNonZeroFields,
    IReadOnlyDictionary<string, int> PreAnchorFirstNonZeroOffsets,
    IReadOnlyDictionary<string, int> PreAnchorFirstNonZeroFields,
    IReadOnlyDictionary<string, int> PreAnchorTrailingLeads,
    IReadOnlyDictionary<string, int> PreAnchorPreLeadBytes,
    IReadOnlyDictionary<string, int> PreTailFieldOffsets,
    IReadOnlyDictionary<string, int> PreTailFieldBytes,
    IReadOnlyDictionary<string, int> HeaderClassifications,
    IReadOnlyDictionary<string, int> HeaderPrefixes,
    IReadOnlyDictionary<string, int> PostPrefixLeads,
    IReadOnlyDictionary<string, int> MarkerBytes,
    IReadOnlyDictionary<string, int> InnerSelectors,
    IReadOnlyDictionary<string, int> OuterSelectors,
    IReadOnlyDictionary<string, int> ObjectClassifications,
    IReadOnlyDictionary<string, int> PayloadBytes,
    IReadOnlyDictionary<string, int> PositionSamples,
    string SampleFile,
    int SampleLine);

public sealed record Protocol03NestedMovementMode06Marker34LongBodyFixtureSummary(
    string ParserAction,
    string ActionReason,
    int BodyBytes,
    string BodyHex,
    string BodyPrefixHex,
    string BodySuffixHex,
    int? FirstNonZeroOffset,
    string FirstNonZeroFieldHex,
    string BodyNonZeroOffsets,
    int TailStartOffset,
    int ContinuationCutOffset,
    int BodyOffset,
    int BoundaryCutOffset,
    int PostContinuationBoundaryOffset,
    string TailPrefixKind,
    string PrefixFirstNonZeroFieldHex,
    string TupleLayoutKind,
    string PostPrefixDisposition,
    string PostPrefixLeadHex,
    string MarkerByteHex,
    string BoundaryHeaderClassification,
    string BoundaryHeaderHex,
    string InnerSelectorHex,
    string OuterSelectorHex,
    string ObjectClassification,
    int PayloadBytes,
    string Position,
    string SampleFile,
    int SampleLine);

public sealed record Protocol03NestedMovementMode06Marker34LongBodyOffsetSummary(
    int BodyOffset,
    string OffsetDisposition,
    int PresentFixtureCount,
    int NonZeroFixtureCount,
    IReadOnlyDictionary<string, int> ByteValues,
    IReadOnlyDictionary<string, int> U16LeValues,
    IReadOnlyDictionary<string, int> U16BeValues,
    IReadOnlyDictionary<string, int> U32LeValues,
    IReadOnlyDictionary<string, int> BodyByteLengths,
    IReadOnlyDictionary<string, int> BoundaryHeaderClassifications,
    IReadOnlyDictionary<string, int> BoundaryHeaderPrefixes,
    IReadOnlyDictionary<string, int> InnerSelectors,
    IReadOnlyDictionary<string, int> OuterSelectors,
    string SampleFile,
    int SampleLine);

public sealed record Protocol03NestedMovementMode06Marker34LongBodyInteriorWindowSummary(
    string WindowDisposition,
    int BodyOffset,
    string WindowHex,
    int FixtureCount,
    IReadOnlyDictionary<string, int> BodyByteLengths,
    IReadOnlyDictionary<string, int> BoundaryHeaderClassifications,
    IReadOnlyDictionary<string, int> BoundaryHeaderPrefixes,
    IReadOnlyDictionary<string, int> InnerSelectors,
    IReadOnlyDictionary<string, int> OuterSelectors,
    IReadOnlyDictionary<string, int> Positions,
    string SampleFile,
    int SampleLine);

public sealed record Protocol03NestedMovementMode06Marker34LongBodyKnownFxWindowSummary(
    int BodyOffset,
    string FxHex,
    string FxName,
    int FixtureCount,
    IReadOnlyDictionary<string, int> FollowupBytes,
    IReadOnlyDictionary<string, int> BodyByteLengths,
    IReadOnlyDictionary<string, int> BoundaryHeaderClassifications,
    IReadOnlyDictionary<string, int> BoundaryHeaderPrefixes,
    IReadOnlyDictionary<string, int> InnerSelectors,
    IReadOnlyDictionary<string, int> OuterSelectors,
    IReadOnlyDictionary<string, int> Positions,
    string SampleFile,
    int SampleLine);

public sealed record Protocol03NestedMovementMode06Marker34LongBodyFxFieldRoleSummary(
    string FieldDisposition,
    string Marker34BodyScope,
    int Marker34BodyOffset,
    int BodyOffset,
    int Marker34RelativeOffset,
    string FxHex,
    string FxName,
    int FixtureCount,
    IReadOnlyDictionary<string, int> FollowupBytes,
    IReadOnlyDictionary<string, int> BodyByteLengths,
    IReadOnlyDictionary<string, int> BoundaryHeaderClassifications,
    IReadOnlyDictionary<string, int> BoundaryHeaderPrefixes,
    IReadOnlyDictionary<string, int> InnerSelectors,
    IReadOnlyDictionary<string, int> OuterSelectors,
    IReadOnlyDictionary<string, int> Positions,
    string SampleFile,
    int SampleLine);

public sealed record Protocol03NestedMovementMode06Marker34LongBodyStructuralRoleSummary(
    string FieldDisposition,
    string Marker34BodyScope,
    int Marker34BodyOffset,
    int BodyOffset,
    int Marker34RelativeOffset,
    int WindowBytes,
    int FixtureCount,
    IReadOnlyDictionary<string, int> WindowHexes,
    IReadOnlyDictionary<string, int> Interpretations,
    IReadOnlyDictionary<string, int> BodyByteLengths,
    IReadOnlyDictionary<string, int> BoundaryHeaderClassifications,
    IReadOnlyDictionary<string, int> BoundaryHeaderPrefixes,
    IReadOnlyDictionary<string, int> InnerSelectors,
    IReadOnlyDictionary<string, int> OuterSelectors,
    IReadOnlyDictionary<string, int> Positions,
    string SampleFile,
    int SampleLine);

public sealed record Protocol03NestedMovementMode06Marker34LongBodyLayoutSummary(
    string LayoutDisposition,
    string ParserAction,
    string ActionReason,
    int BodyBytes,
    string BodyPrefixHex,
    string BodySuffixHex,
    string PostPrefixLeadHex,
    string MarkerByteHex,
    string MarkerTagValue,
    string MarkerTagWindowHex,
    string MarkerFxHex,
    string MarkerFxName,
    string MarkerFxFollowupBytes,
    string MarkerScaffoldHex,
    string EmbeddedMovementSelectorHex,
    string EmbeddedMovementPosition,
    string EmbeddedMovementRelation,
    string EmbeddedMovementDistance,
    int NestedReplayPrefixOffset,
    int NestedMarker34BodyOffset,
    string NestedTagValue,
    string NestedTagWindowHex,
    string NestedFxHex,
    string NestedFxName,
    string NestedFxFollowupBytes,
    string NestedScaffoldHex,
    string BoundaryHeaderClassification,
    string BoundaryHeaderHex,
    string InnerSelectorHex,
    string OuterSelectorHex,
    string Position,
    string SampleFile,
    int SampleLine);

public sealed record Protocol03NestedMovementMode06Marker34LongBodyParserReadinessSummary(
    string ReadinessDisposition,
    string PromotionBlocker,
    int FixtureCount,
    IReadOnlyDictionary<string, int> ParserActions,
    IReadOnlyDictionary<string, int> ActionReasons,
    IReadOnlyDictionary<string, int> Layouts,
    IReadOnlyDictionary<string, int> BodyByteLengths,
    IReadOnlyDictionary<string, int> BodyPrefixes,
    IReadOnlyDictionary<string, int> BodySuffixes,
    IReadOnlyDictionary<string, int> PostPrefixLeads,
    IReadOnlyDictionary<string, int> MarkerBytes,
    IReadOnlyDictionary<string, int> MarkerTags,
    IReadOnlyDictionary<string, int> MarkerFxs,
    IReadOnlyDictionary<string, int> EmbeddedMovementSelectors,
    IReadOnlyDictionary<string, int> EmbeddedMovementRelations,
    IReadOnlyDictionary<string, int> NestedReplayOffsets,
    IReadOnlyDictionary<string, int> BoundaryHeaderClassifications,
    IReadOnlyDictionary<string, int> BoundaryHeaderPrefixes,
    IReadOnlyDictionary<string, int> InnerSelectors,
    IReadOnlyDictionary<string, int> OuterSelectors,
    IReadOnlyDictionary<string, int> Positions,
    string SampleFile,
    int SampleLine);

public sealed record Protocol03NestedMovementMode06Marker34LongBodyNestedReplaySummary(
    string ReplayDisposition,
    int PrefixBodyOffset,
    int NestedBodyOffset,
    int AvailableNestedBodyBytes,
    string PrefixHex,
    string NestedTagWindowHex,
    string NestedKnownFxHex,
    string NestedKnownFxName,
    string NestedKnownFxFollowupBytes,
    int StableOffsetMatchCount,
    int StableOffsetProbeCount,
    string MatchedStableOffsets,
    int FixtureCount,
    IReadOnlyDictionary<string, int> BodyByteLengths,
    IReadOnlyDictionary<string, int> BoundaryHeaderClassifications,
    IReadOnlyDictionary<string, int> BoundaryHeaderPrefixes,
    IReadOnlyDictionary<string, int> InnerSelectors,
    IReadOnlyDictionary<string, int> OuterSelectors,
    IReadOnlyDictionary<string, int> Positions,
    string SampleFile,
    int SampleLine);

public sealed record Protocol03NestedMovementMode06Marker34LongBodyFieldWindowSummary(
    string WindowDisposition,
    int BodyOffset,
    int WindowBytes,
    int FixtureCount,
    IReadOnlyDictionary<string, int> WindowHexes,
    IReadOnlyDictionary<string, int> Interpretations,
    IReadOnlyDictionary<string, int> BodyByteLengths,
    IReadOnlyDictionary<string, int> BoundaryHeaderClassifications,
    IReadOnlyDictionary<string, int> BoundaryHeaderPrefixes,
    IReadOnlyDictionary<string, int> InnerSelectors,
    IReadOnlyDictionary<string, int> OuterSelectors,
    IReadOnlyDictionary<string, int> Positions,
    string SampleFile,
    int SampleLine);

public sealed record Protocol03NestedMovementMode06Marker34LongBodyEmbeddedMovementVectorSummary(
    string MovementDisposition,
    int LeadBodyOffset,
    int MarkerBodyOffset,
    string LeadByteHex,
    string SelectorHex,
    int InnerPayloadBytes,
    int InnerPositionOffset,
    int BodyPositionOffset,
    int FixtureCount,
    IReadOnlyDictionary<string, int> PrePositionBytes,
    IReadOnlyDictionary<string, int> PositionHexes,
    IReadOnlyDictionary<string, int> DecodedPositions,
    IReadOnlyDictionary<string, int> OuterPositions,
    IReadOnlyDictionary<string, int> OuterPositionRelations,
    IReadOnlyDictionary<string, int> DistancesFromOuterPosition,
    IReadOnlyDictionary<string, int> PostMovementLeads,
    IReadOnlyDictionary<string, int> BodyByteLengths,
    IReadOnlyDictionary<string, int> BoundaryHeaderClassifications,
    IReadOnlyDictionary<string, int> BoundaryHeaderPrefixes,
    IReadOnlyDictionary<string, int> InnerSelectors,
    IReadOnlyDictionary<string, int> OuterSelectors,
    string SampleFile,
    int SampleLine);

public sealed record Protocol03NestedMovementMode06HeldTupleBodyPrioritySummary(
    string PriorityAction,
    string PriorityReason,
    int HeldRowCount,
    int WindowCount,
    IReadOnlyDictionary<string, int> ParserActions,
    IReadOnlyDictionary<string, int> SourceDispositions,
    IReadOnlyDictionary<string, int> TailPrefixKinds,
    IReadOnlyDictionary<string, int> PrefixFirstNonZeroFields,
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
    IReadOnlyDictionary<string, int> BodySuffixes,
    IReadOnlyDictionary<string, int> PositionSamples,
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
    public IReadOnlyList<ManageBonusTradeStatePayloadSample>? ManageBonusTradeStatePayloads { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int ManageBonusTradeStatePayloadCount => ManageBonusTradeStatePayloads?.Count ?? 0;

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

    public IReadOnlyList<ManageBonusStateIdLongFormFieldLeadSummary> ManageBonusStateIdLongFormFieldLeadSummaries { get; init; } = Array.Empty<ManageBonusStateIdLongFormFieldLeadSummary>();

    public IReadOnlyList<ManageBonusStateIdLongFormSemanticFamilySummary> ManageBonusStateIdLongFormSemanticFamilySummaries { get; init; } = Array.Empty<ManageBonusStateIdLongFormSemanticFamilySummary>();

    public IReadOnlyList<ManageBonusTradeStateValueSummary> ManageBonusTradeStateValueSummaries { get; init; } = Array.Empty<ManageBonusTradeStateValueSummary>();

    public IReadOnlyList<ManageBonusTradeStatePayloadDecodeSummary> ManageBonusTradeStatePayloadDecodeSummaries { get; init; } = Array.Empty<ManageBonusTradeStatePayloadDecodeSummary>();

    public IReadOnlyList<ManageBonusTradeStateByte15PairSummary> ManageBonusTradeStateByte15PairSummaries { get; init; } = Array.Empty<ManageBonusTradeStateByte15PairSummary>();

    public IReadOnlyList<ManageBonusTradeStateByte15TransitionSummary> ManageBonusTradeStateByte15TransitionSummaries { get; init; } = Array.Empty<ManageBonusTradeStateByte15TransitionSummary>();

    public IReadOnlyList<ManageBonusTradeStateCompanionMapSummary> ManageBonusTradeStateCompanionMapSummaries { get; init; } = Array.Empty<ManageBonusTradeStateCompanionMapSummary>();

    public IReadOnlyList<ManageBonusTradeStateParserActionSummary> ManageBonusTradeStateParserActionSummaries { get; init; } = Array.Empty<ManageBonusTradeStateParserActionSummary>();

    public IReadOnlyList<ManageBonusTradeStateSerializedTransitionSummary> ManageBonusTradeStateSerializedTransitionSummaries { get; init; } = Array.Empty<ManageBonusTradeStateSerializedTransitionSummary>();

    public IReadOnlyList<ManageBonusTradeStateSerializedTransitionSample> ManageBonusTradeStateSerializedTransitionSamples { get; init; } = Array.Empty<ManageBonusTradeStateSerializedTransitionSample>();

    public IReadOnlyList<ManageBonusTradeStateSerializedTransitionParserCoverageSummary> ManageBonusTradeStateSerializedTransitionParserCoverageSummaries { get; init; } = Array.Empty<ManageBonusTradeStateSerializedTransitionParserCoverageSummary>();

    public IReadOnlyList<ManageBonusTradeStateSerializedTransitionFieldRoleSummary> ManageBonusTradeStateSerializedTransitionFieldRoleSummaries { get; init; } = Array.Empty<ManageBonusTradeStateSerializedTransitionFieldRoleSummary>();

    public IReadOnlyList<ManageBonusTradeStateSerializedTransitionDecoderRow> ManageBonusTradeStateSerializedTransitionDecoderRows { get; init; } = Array.Empty<ManageBonusTradeStateSerializedTransitionDecoderRow>();

    public IReadOnlyList<ManageBonusTradeStateSerializedTransitionDetection> ManageBonusTradeStateSerializedTransitionDetections { get; init; } = Array.Empty<ManageBonusTradeStateSerializedTransitionDetection>();

    public IReadOnlyList<ManageBonusTradeStateSerializedTransitionDetectionActionSummary> ManageBonusTradeStateSerializedTransitionDetectionActionSummaries { get; init; } = Array.Empty<ManageBonusTradeStateSerializedTransitionDetectionActionSummary>();

    public IReadOnlyList<Unknown6dListRecordShapeSummary> Unknown6dListRecordShapeSummaries { get; init; } = Array.Empty<Unknown6dListRecordShapeSummary>();

    public IReadOnlyList<Unknown6dEntryIdSummary> Unknown6dEntryIdSummaries { get; init; } = Array.Empty<Unknown6dEntryIdSummary>();

    public IReadOnlyList<Unknown6dEntryTailFlagSummary> Unknown6dEntryTailFlagSummaries { get; init; } = Array.Empty<Unknown6dEntryTailFlagSummary>();

    public IReadOnlyList<Unknown6dPacketContextSummary> Unknown6dPacketContextSummaries { get; init; } = Array.Empty<Unknown6dPacketContextSummary>();

    public IReadOnlyList<Unknown80c1PacketContextSummary> Unknown80c1PacketContextSummaries { get; init; } = Array.Empty<Unknown80c1PacketContextSummary>();

    public IReadOnlyList<Protocol03SelectorFfRepeatListCandidate> Protocol03SelectorFfRepeatListCandidates { get; init; } = Array.Empty<Protocol03SelectorFfRepeatListCandidate>();

    public IReadOnlyList<Protocol03SelectorFfRepeatListDecodedRow> Protocol03SelectorFfRepeatListDecodedRows { get; init; } = Array.Empty<Protocol03SelectorFfRepeatListDecodedRow>();

    public IReadOnlyList<Protocol03SelectorFfRepeatListFieldSummary> Protocol03SelectorFfRepeatListFieldSummaries { get; init; } = Array.Empty<Protocol03SelectorFfRepeatListFieldSummary>();

    public IReadOnlyList<Protocol03SelectorFfRepeatListShapeSummary> Protocol03SelectorFfRepeatListShapeSummaries { get; init; } = Array.Empty<Protocol03SelectorFfRepeatListShapeSummary>();

    public IReadOnlyList<Protocol03SelectorFfRepeatListEffectiveShapeSummary> Protocol03SelectorFfRepeatListEffectiveShapeSummaries { get; init; } = Array.Empty<Protocol03SelectorFfRepeatListEffectiveShapeSummary>();

    public IReadOnlyList<Protocol03SelectorFfRepeatListEntryPairSummary> Protocol03SelectorFfRepeatListEntryPairSummaries { get; init; } = Array.Empty<Protocol03SelectorFfRepeatListEntryPairSummary>();

    public IReadOnlyList<Protocol03SelectorFfRepeatListContinuationMarkerSummary> Protocol03SelectorFfRepeatListContinuationMarkerSummaries { get; init; } = Array.Empty<Protocol03SelectorFfRepeatListContinuationMarkerSummary>();

    public IReadOnlyList<Protocol03SelectorFfRepeatListContinuationParserActionSummary> Protocol03SelectorFfRepeatListContinuationParserActionSummaries { get; init; } = Array.Empty<Protocol03SelectorFfRepeatListContinuationParserActionSummary>();

    public IReadOnlyList<Protocol03SelectorFfRepeatListParserCoverageSummary> Protocol03SelectorFfRepeatListParserCoverageSummaries { get; init; } = Array.Empty<Protocol03SelectorFfRepeatListParserCoverageSummary>();

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

    public IReadOnlyList<Protocol03SelectorFfRepeatListVendorBoundaryDecisionRow> Protocol03SelectorFfRepeatListVendorBoundaryDecisionRows { get; init; } = Array.Empty<Protocol03SelectorFfRepeatListVendorBoundaryDecisionRow>();

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

    public IReadOnlyList<Protocol03NestedMovementMode06UnsupportedEmbeddedControlSelectorSummary> Protocol03NestedMovementMode06UnsupportedEmbeddedControlSelectorSummaries { get; init; } = Array.Empty<Protocol03NestedMovementMode06UnsupportedEmbeddedControlSelectorSummary>();

    public IReadOnlyList<Protocol03NestedMovementMode06HeldTupleBodySummary> Protocol03NestedMovementMode06HeldTupleBodySummaries { get; init; } = Array.Empty<Protocol03NestedMovementMode06HeldTupleBodySummary>();

    public IReadOnlyList<Protocol03NestedMovementMode06Marker34LongBodyFixtureSummary> Protocol03NestedMovementMode06Marker34LongBodyFixtureSummaries { get; init; } = Array.Empty<Protocol03NestedMovementMode06Marker34LongBodyFixtureSummary>();

    public IReadOnlyList<Protocol03NestedMovementMode06Marker34LongBodyOffsetSummary> Protocol03NestedMovementMode06Marker34LongBodyOffsetSummaries { get; init; } = Array.Empty<Protocol03NestedMovementMode06Marker34LongBodyOffsetSummary>();

    public IReadOnlyList<Protocol03NestedMovementMode06Marker34LongBodyInteriorWindowSummary> Protocol03NestedMovementMode06Marker34LongBodyInteriorWindowSummaries { get; init; } = Array.Empty<Protocol03NestedMovementMode06Marker34LongBodyInteriorWindowSummary>();

    public IReadOnlyList<Protocol03NestedMovementMode06Marker34LongBodyKnownFxWindowSummary> Protocol03NestedMovementMode06Marker34LongBodyKnownFxWindowSummaries { get; init; } = Array.Empty<Protocol03NestedMovementMode06Marker34LongBodyKnownFxWindowSummary>();

    public IReadOnlyList<Protocol03NestedMovementMode06Marker34LongBodyFxFieldRoleSummary> Protocol03NestedMovementMode06Marker34LongBodyFxFieldRoleSummaries { get; init; } = Array.Empty<Protocol03NestedMovementMode06Marker34LongBodyFxFieldRoleSummary>();

    public IReadOnlyList<Protocol03NestedMovementMode06Marker34LongBodyStructuralRoleSummary> Protocol03NestedMovementMode06Marker34LongBodyStructuralRoleSummaries { get; init; } = Array.Empty<Protocol03NestedMovementMode06Marker34LongBodyStructuralRoleSummary>();

    public IReadOnlyList<Protocol03NestedMovementMode06Marker34LongBodyLayoutSummary> Protocol03NestedMovementMode06Marker34LongBodyLayoutSummaries { get; init; } = Array.Empty<Protocol03NestedMovementMode06Marker34LongBodyLayoutSummary>();

    public IReadOnlyList<Protocol03NestedMovementMode06Marker34LongBodyParserReadinessSummary> Protocol03NestedMovementMode06Marker34LongBodyParserReadinessSummaries { get; init; } = Array.Empty<Protocol03NestedMovementMode06Marker34LongBodyParserReadinessSummary>();

    public IReadOnlyList<Protocol03NestedMovementMode06Marker34LongBodyNestedReplaySummary> Protocol03NestedMovementMode06Marker34LongBodyNestedReplaySummaries { get; init; } = Array.Empty<Protocol03NestedMovementMode06Marker34LongBodyNestedReplaySummary>();

    public IReadOnlyList<Protocol03NestedMovementMode06Marker34LongBodyFieldWindowSummary> Protocol03NestedMovementMode06Marker34LongBodyFieldWindowSummaries { get; init; } = Array.Empty<Protocol03NestedMovementMode06Marker34LongBodyFieldWindowSummary>();

    public IReadOnlyList<Protocol03NestedMovementMode06Marker34LongBodyEmbeddedMovementVectorSummary> Protocol03NestedMovementMode06Marker34LongBodyEmbeddedMovementVectorSummaries { get; init; } = Array.Empty<Protocol03NestedMovementMode06Marker34LongBodyEmbeddedMovementVectorSummary>();

    public IReadOnlyList<Protocol03NestedMovementMode06HeldTupleBodyPrioritySummary> Protocol03NestedMovementMode06HeldTupleBodyPrioritySummaries { get; init; } = Array.Empty<Protocol03NestedMovementMode06HeldTupleBodyPrioritySummary>();

    public IReadOnlyList<Protocol03NestedMovementMode06TupleBodyParserActionSummary> Protocol03NestedMovementMode06TupleBodyParserActionSummaries { get; init; } = Array.Empty<Protocol03NestedMovementMode06TupleBodyParserActionSummary>();

    public IReadOnlyList<Protocol03NestedMovementParserActionSummary> Protocol03NestedMovementParserActionSummaries { get; init; } = Array.Empty<Protocol03NestedMovementParserActionSummary>();

    public IReadOnlyList<Protocol03NpcBaseDynamicCreationTailSummary> Protocol03NpcBaseDynamicCreationTailSummaries { get; init; } = Array.Empty<Protocol03NpcBaseDynamicCreationTailSummary>();

    public IReadOnlyList<Protocol03NpcBaseDynamicCreationHeaderLikeLeadSummary> Protocol03NpcBaseDynamicCreationHeaderLikeLeadSummaries { get; init; } = Array.Empty<Protocol03NpcBaseDynamicCreationHeaderLikeLeadSummary>();

    public IReadOnlyList<Protocol03NpcBaseDynamicCreationBodyAnchorSummary> Protocol03NpcBaseDynamicCreationBodyAnchorSummaries { get; init; } = Array.Empty<Protocol03NpcBaseDynamicCreationBodyAnchorSummary>();

    public IReadOnlyList<Protocol03NpcBaseDynamicCreationExtendedBoundarySummary> Protocol03NpcBaseDynamicCreationExtendedBoundarySummaries { get; init; } = Array.Empty<Protocol03NpcBaseDynamicCreationExtendedBoundarySummary>();

    public IReadOnlyList<Protocol03NpcBaseDynamicCreationExtendedBoundaryParserActionSummary> Protocol03NpcBaseDynamicCreationExtendedBoundaryParserActionSummaries { get; init; } = Array.Empty<Protocol03NpcBaseDynamicCreationExtendedBoundaryParserActionSummary>();

    public IReadOnlyList<Protocol03NpcBaseDynamicCreationExtendedBoundaryBodyShapeSummary> Protocol03NpcBaseDynamicCreationExtendedBoundaryBodyShapeSummaries { get; init; } = Array.Empty<Protocol03NpcBaseDynamicCreationExtendedBoundaryBodyShapeSummary>();

    public IReadOnlyList<Protocol03NpcBaseDynamicCreationExtendedBoundaryPostSeparatorFieldSummary> Protocol03NpcBaseDynamicCreationExtendedBoundaryPostSeparatorFieldSummaries { get; init; } = Array.Empty<Protocol03NpcBaseDynamicCreationExtendedBoundaryPostSeparatorFieldSummary>();

    public IReadOnlyList<Protocol03NpcBaseDynamicCreationExtendedBoundaryPostSeparatorVectorProbeSummary> Protocol03NpcBaseDynamicCreationExtendedBoundaryPostSeparatorVectorProbeSummaries { get; init; } = Array.Empty<Protocol03NpcBaseDynamicCreationExtendedBoundaryPostSeparatorVectorProbeSummary>();

    public IReadOnlyList<Protocol03NpcBaseDynamicCreationExtendedBoundaryPostSeparatorVectorTailSummary> Protocol03NpcBaseDynamicCreationExtendedBoundaryPostSeparatorVectorTailSummaries { get; init; } = Array.Empty<Protocol03NpcBaseDynamicCreationExtendedBoundaryPostSeparatorVectorTailSummary>();

    public IReadOnlyList<Protocol03NpcBaseDynamicCreationExtendedBoundaryPostSeparatorLayoutActionSummary> Protocol03NpcBaseDynamicCreationExtendedBoundaryPostSeparatorLayoutActionSummaries { get; init; } = Array.Empty<Protocol03NpcBaseDynamicCreationExtendedBoundaryPostSeparatorLayoutActionSummary>();

    public IReadOnlyList<Protocol03NpcBaseDynamicCreationExtendedBoundaryPostSeparatorReviewedBodyLayoutSummary> Protocol03NpcBaseDynamicCreationExtendedBoundaryPostSeparatorReviewedBodyLayoutSummaries { get; init; } = Array.Empty<Protocol03NpcBaseDynamicCreationExtendedBoundaryPostSeparatorReviewedBodyLayoutSummary>();

    public IReadOnlyList<Protocol03NpcBaseDynamicCreationExtendedBoundaryPostSeparatorVectorSemanticSummary> Protocol03NpcBaseDynamicCreationExtendedBoundaryPostSeparatorVectorSemanticSummaries { get; init; } = Array.Empty<Protocol03NpcBaseDynamicCreationExtendedBoundaryPostSeparatorVectorSemanticSummary>();

    public IReadOnlyList<Protocol03NpcBaseDynamicCreationExtendedBoundaryPostSeparatorTailSemanticSummary> Protocol03NpcBaseDynamicCreationExtendedBoundaryPostSeparatorTailSemanticSummaries { get; init; } = Array.Empty<Protocol03NpcBaseDynamicCreationExtendedBoundaryPostSeparatorTailSemanticSummary>();

    public IReadOnlyList<Protocol03NpcBaseDynamicCreationExtendedBoundaryPostSeparatorControlSemanticSummary> Protocol03NpcBaseDynamicCreationExtendedBoundaryPostSeparatorControlSemanticSummaries { get; init; } = Array.Empty<Protocol03NpcBaseDynamicCreationExtendedBoundaryPostSeparatorControlSemanticSummary>();
}
