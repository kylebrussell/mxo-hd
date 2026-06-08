using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace MxoHd.PacketResearch;

public static partial class PacketResearcher
{
    private const int Protocol03DynamicTailPrefixBytes = 32;
    private const int Protocol03DynamicTailExtendedBoundarySearchBytes = 64;
    private const uint AbilityCodeFlag = 0x80000000;
    private const uint AbilityCodeMask = 0xfffffc00;
    private const uint AbilityLevelMask = 0x000003ff;

    private static readonly HashSet<uint> FullPriceAbilityCodes = new()
    {
        2148365312, // BalanceAbility
        2147490816, // DetectVulnerabilityAbility
        2147605504, // DisarmTrapsAbility
        2147492864, // EnergizedAttacksAbility
        2147496960, // HyperStrengthAbility
        2147684352, // IgnorePainAbility
        2147858432, // ImpartInvisibilityAbility
        2147497984, // PowerShotAbility
        2147499008, // PreciseBlowAbility
        2147500032, // PunishingBlowsAbility
        2148372480, // PurityAbility
        2148374528, // SpinningFlowerAbility
        2148373504, // SweepTheFloorAbility
        2148364288, // TechniqueAbility
        2148375552 // ZenMasterAbility
    };

    private static readonly HashSet<string> SpecializedProtocol04PayloadHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "3a",
        "06",
        "7c",
        "2e",
        "47",
        "69",
        "6d",
        "80 b2",
        "80 b3",
        "80 bc",
        "80 bd",
        "80 c1",
        "80 c3",
        "80 c8",
        "80 d7",
        "80 de",
        "80 86",
        "80 e4",
        "80 e5",
        "80 f5",
        "81 a9",
        "81 54",
        "81 67"
    };

    private static readonly HashSet<string> VendorAdjacentProtocol04Headers = new(StringComparer.OrdinalIgnoreCase)
    {
        "5e",
        "5f",
        "69",
        "80 c7",
        "80 c8",
        "80 e4",
        "80 e7",
        "81 0d",
        "81 0e",
        "81 0f",
        "81 11",
        "81 12",
        "81 9c"
    };

    private static readonly Protocol03AttributeDescriptor[] Object599CreationAttributes =
    {
        new(0, "EvadeShieldHealth", 1),
        new(1, "CharacterName", 32),
        new(2, "TitleAbility", 4),
        new(3, "CombatantMode", 1),
        new(4, "JumpFlags", 2),
        new(5, "ConditionStateFlags", 4),
        new(6, "Jumping", 1),
        new(7, "EffectCounter", 1),
        new(8, "UseRSIDescription", 1),
        new(9, "YawInterval", 1),
        new(10, "MasterCharacterID", 4),
        new(11, "MoreInfoID", 4),
        new(12, "StealthLevel", 1),
        new(13, "StopFollowActiveTracker", 1),
        new(14, "IsDead", 1),
        new(15, "DissemblingType", 2),
        new(16, "JumpEndTime", 4),
        new(17, "HalfExtents", 12),
        new(18, "WanderRange", 4),
        new(19, "ScriptCounter", 1),
        new(20, "EvadeShieldDamageScale", 4),
        new(21, "Description", 4),
        new(22, "EffectID", 4),
        new(23, "FactionID", 4),
        new(24, "DontAttack", 1),
        new(25, "NoiseLevel", 1),
        new(26, "ExclusiveLocator", 18),
        new(27, "Position", 24),
        new(28, "ProneState", 4),
        new(29, "CancelAbility", 4),
        new(30, "UseCount", 1),
        new(31, "MissionKey", 4),
        new(32, "Stance", 1),
        new(33, "OrganizationID", 1),
        new(34, "IsEnemy", 1),
        new(35, "Action", 1),
        new(36, "NPCRank", 1),
        new(37, "IsFriendly", 1),
        new(38, "Demeanor", 1),
        new(39, "MovementScale", 4),
        new(40, "CancelAbilityCounter", 1),
        new(41, "JumpPeakHeight", 4),
        new(42, "FollowActiveTracker", 1),
        new(43, "GuardType", 1),
        new(44, "ExclusiveType", 1),
        new(45, "Level", 1),
        new(46, "GiverActiveTracker", 1),
        new(47, "Health", 2),
        new(48, "EffectCommand", 4),
        new(49, "JumpDestination", 24),
        new(50, "DebuffState", 1),
        new(51, "TakerActiveTracker", 1),
        new(52, "RSIDescription", 15),
        new(53, "MaxHealth", 2),
        new(54, "CurrentState", 4),
        new(55, "IsEscortable", 1),
        new(56, "EquippedItemID", 4),
        new(57, "SpawnFX", 4),
        new(58, "DuelID", 4),
        new(59, "TalkDefaultable", 1),
        new(60, "OwnerCharacterID", 4),
        new(61, "PutActiveTracker", 1),
        new(62, "RelevancyFlags", 1),
        new(63, "TalkActiveTracker", 1),
        new(64, "StealthAwareness", 1),
        new(65, "CurrentStateContainer", 4)
    };

    private static readonly Protocol03AttributeDescriptor[] Object12CreationAttributes =
    {
        new(0, "ReputationMerovingian", 2),
        new(1, "ConquestPoints", 4),
        new(2, "CancelAbilityCounter", 1),
        new(3, "RealLastName", 32),
        new(4, "ScriptCounter", 1),
        new(5, "MissionTypeFlags", 1),
        new(6, "IsHeadless", 1),
        new(7, "IsDead", 1),
        new(8, "AbandonedState", 1),
        new(9, "CancelAbility", 4),
        new(10, "JumpEndTime", 4),
        new(11, "RealFirstName", 32),
        new(12, "TeleportCounter", 1),
        new(13, "MovementScale", 4),
        new(14, "EvadeShieldDamageScale", 4),
        new(15, "EffectCommand", 4),
        new(16, "EvadeShieldHealth", 1),
        new(17, "SecureTradeFlag", 4),
        new(18, "InteractionFlags", 4),
        new(19, "StealthLevel", 1),
        new(20, "RippleMagnitude", 1),
        new(21, "HeavyLuggableID", 4),
        new(22, "AFK", 1),
        new(23, "MissionTeamID", 4),
        new(24, "MaxHealth", 2),
        new(25, "StealthAwareness", 1),
        new(26, "JumpPeakHeight", 4),
        new(27, "ConditionStateFlags", 4),
        new(28, "InnerStrengthCommitted", 2),
        new(29, "ReputationMachines", 2),
        new(30, "InnerStrengthAvailable", 2),
        new(31, "FactionID", 4),
        new(32, "DeathPenalty", 4),
        new(33, "FollowingPath", 1),
        new(34, "EffectCounter", 1),
        new(35, "Description", 4),
        new(36, "ReputationNiobe", 2),
        new(37, "CharacterName", 32),
        new(38, "JumpDestination", 24),
        new(39, "SelectionRangeDebuff", 4),
        new(40, "RepelDistance", 4),
        new(41, "Health", 2),
        new(42, "TitleAbility", 4),
        new(43, "Demeanor", 1),
        new(44, "ReputationPluribusNeo", 2),
        new(45, "CharacterID", 4),
        new(46, "CrewID", 4),
        new(47, "RSIDescription", 15),
        new(48, "InnerStrengthMax", 2),
        new(49, "Position", 24),
        new(50, "IsDuelDeath", 1),
        new(51, "ReputationCypherites", 2),
        new(52, "Level", 1),
        new(53, "CombatantMode", 1),
        new(54, "ReputationGMOrganization", 2),
        new(55, "EffectID", 4),
        new(56, "DuelID", 4),
        new(57, "HasLuggable", 1),
        new(58, "Jumping", 1),
        new(59, "MissionKey", 4),
        new(60, "DissemblingType", 2),
        new(61, "JumpFlags", 2),
        new(62, "CurExclusiveAbility", 4),
        new(63, "ZoneInTime", 4),
        new(64, "NoiseLevel", 1),
        new(65, "HalfExtents", 12),
        new(66, "YawInterval", 1),
        new(67, "RelevancyFlags", 1),
        new(68, "Stance", 1),
        new(69, "UseRSIDescription", 1),
        new(70, "EquippedItemID", 4),
        new(71, "Action", 1),
        new(72, "OrganizationID", 1),
        new(73, "CurCombatExclusiveAbility", 4),
        new(74, "ReputationZionMilitary", 2)
    };

    private static readonly Protocol03AttributeDescriptor[] Object12SelfViewAttributes =
    {
        new(0, "UseRSIDescription", 1),
        new(1, "TitleAbility", 4),
        new(2, "SelectionRangeDebuff", 4),
        new(3, "RippleMagnitude", 1),
        new(4, "RepelDistance", 4),
        new(5, "RealLastName", 32),
        new(6, "RealFirstName", 32),
        new(7, "RSIDescription", 15),
        new(8, "OrganizationID", 1),
        new(9, "NoiseLevel", 1),
        new(10, "MovementScale", 4),
        new(11, "MissionTypeFlags", 1),
        new(12, "MissionTeamID", 4),
        new(13, "MissionKey", 4),
        new(14, "MaxHealth", 2),
        new(15, "Level", 1),
        new(16, "JumpPeakHeight", 4),
        new(17, "JumpFlags", 2),
        new(18, "JumpEndTime", 4),
        new(19, "JumpDestination", 24),
        new(20, "IsDuelDeath", 1),
        new(21, "IsDead", 1),
        new(22, "InteractionFlags", 4),
        new(23, "InnerStrengthMax", 2),
        new(24, "InnerStrengthCommitted", 2),
        new(25, "InnerStrengthAvailable", 2),
        new(26, "HeavyLuggableID", 4),
        new(27, "Health", 2),
        new(28, "FollowingPath", 1),
        new(29, "FactionID", 4),
        new(30, "EvadeShieldHealth", 1),
        new(31, "EquippedItemID", 4),
        new(32, "EffectID", 4),
        new(33, "EffectCounter", 1),
        new(34, "EffectCommand", 4),
        new(35, "DuelID", 4),
        new(36, "DissemblingType", 2),
        new(37, "Description", 4),
        new(38, "DeathPenalty", 4),
        new(39, "CurCombatExclusiveAbility", 4),
        new(40, "CrewID", 4),
        new(41, "ConquestPoints", 4),
        new(42, "ConditionStateFlags", 4),
        new(43, "CombatantMode", 1),
        new(44, "CancelAbilityCounter", 1),
        new(45, "CancelAbility", 4),
        new(46, "AbandonedState", 1),
        new(47, "AFK", 1)
    };

    public static PacketResearchReport BuildReport(
        string repositoryRoot,
        IEnumerable<string> packetDumpRoots,
        string? rajkoSourceRoot,
        IEnumerable<string>? externalSourceRoots = null)
    {
        repositoryRoot = Path.GetFullPath(repositoryRoot);
        string[] dumpRoots = packetDumpRoots
            .Where(root => !string.IsNullOrWhiteSpace(root))
            .Select(Path.GetFullPath)
            .Where(Directory.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        string? normalizedRajkoRoot = string.IsNullOrWhiteSpace(rajkoSourceRoot)
            ? null
            : Path.GetFullPath(rajkoSourceRoot);
        string[] normalizedExternalRoots = (externalSourceRoots ?? Enumerable.Empty<string>())
            .Where(root => !string.IsNullOrWhiteSpace(root))
            .Select(Path.GetFullPath)
            .Where(Directory.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        List<RpcHeaderEntry> localHeaders = LoadLocalHeaders(repositoryRoot).ToList();
        List<AttributeDefinition> attributeDefinitions = LoadAttributeDefinitions(repositoryRoot).ToList();
        List<FxDefinition> fxDefinitions = LoadFxDefinitions(repositoryRoot).ToList();
        List<AbilityDefinition> abilityDefinitions = LoadAbilityDefinitions(repositoryRoot).ToList();
        List<AnimationDefinition> animationDefinitions = LoadAnimationDefinitions(repositoryRoot).ToList();
        List<GameObjectEntry> gameObjectEntries = LoadGameObjectEntries(repositoryRoot).ToList();
        List<ItemCommandEntry> itemCommandEntries = LoadItemCommandEntries(repositoryRoot).ToList();
        List<VendorPriceEntry> vendorPriceEntries = LoadVendorPriceEntries(repositoryRoot).ToList();
        List<WorldEntityEntry> worldEntityEntries = LoadWorldEntityEntries(repositoryRoot).ToList();
        List<RajkoRpcEntry> rajkoRpcHeaders = Directory.Exists(normalizedRajkoRoot)
            ? LoadRajkoRpcHeaders(normalizedRajkoRoot).ToList()
            : new List<RajkoRpcEntry>();
        List<HardcodedCommandExample> hardcodedCommands = LoadLocalHardcodedCommands(repositoryRoot)
            .Concat(Directory.Exists(normalizedRajkoRoot)
                ? LoadRajkoHardcodedCommands(normalizedRajkoRoot)
                : Enumerable.Empty<HardcodedCommandExample>())
            .ToList();
        List<Protocol03HardcodedExample> hardcodedProtocol03Examples = LoadLocalProtocol03HardcodedExamples(repositoryRoot)
            .Concat(Directory.Exists(normalizedRajkoRoot)
                ? LoadExternalProtocol03HardcodedExamples(normalizedRajkoRoot, "rajko")
                : Enumerable.Empty<Protocol03HardcodedExample>())
            .Concat(normalizedExternalRoots.SelectMany(root => LoadExternalProtocol03HardcodedExamples(root, InferExternalSourceName(root))))
            .ToList();
        List<Protocol04InteractionCommandExample> protocol04InteractionCommandExamples = (Directory.Exists(normalizedRajkoRoot)
                ? LoadExternalProtocol04InteractionCommandExamples(normalizedRajkoRoot, "rajko")
                : Enumerable.Empty<Protocol04InteractionCommandExample>())
            .Concat(normalizedExternalRoots.SelectMany(root => LoadExternalProtocol04InteractionCommandExamples(root, InferExternalSourceName(root))))
            .ToList();
        List<VendorInventoryEntry> vendorInventoryEntries = LoadVendorInventoryEntries(repositoryRoot).ToList();

        List<RpcComparison> comparisons = CompareRajkoToLocal(rajkoRpcHeaders, localHeaders).ToList();
        List<PacketDumpFileSummary> dumpSummaries = dumpRoots
            .SelectMany(root => LoadPacketDumpSummaries(root, localHeaders, repositoryRoot))
            .OrderBy(summary => summary.File, StringComparer.OrdinalIgnoreCase)
            .ToList();
        uint[] interactionObjectIds = dumpSummaries
            .SelectMany(summary => summary.Protocol04InteractionPayloads.Select(payload => payload.ObjectId))
            .Concat(protocol04InteractionCommandExamples.Select(example => example.ObjectId))
            .Concat(dumpSummaries.SelectMany(summary => summary.Protocol03ObjectViews.SelectMany(view => view.StaticObjectLeads.Select(lead => lead.ObjectId))))
            .Concat(FindProtocol03SelectorFfField9Candidates(dumpSummaries))
            .Distinct()
            .ToArray();
        List<StaticObjectEntry> staticObjectEntries = LoadStaticObjectEntries(repositoryRoot, interactionObjectIds).ToList();
        List<Protocol03SelectorFfRepeatListCandidate> selectorFfRepeatListCandidates = BuildProtocol03SelectorFfRepeatListCandidates(dumpSummaries).ToList();
        List<Protocol03SelectorFfRepeatListFieldSummary> selectorFfRepeatListFieldSummaries = BuildProtocol03SelectorFfRepeatListFieldSummaries(
            selectorFfRepeatListCandidates,
            abilityDefinitions,
            itemCommandEntries,
            gameObjectEntries,
            animationDefinitions).ToList();
        List<Protocol03SelectorFfRepeatListShapeSummary> selectorFfRepeatListShapeSummaries = BuildProtocol03SelectorFfRepeatListShapeSummaries(
            selectorFfRepeatListCandidates,
            abilityDefinitions,
            itemCommandEntries,
            gameObjectEntries,
            animationDefinitions).ToList();
        List<Protocol03SelectorFfRepeatListEffectiveShapeSummary> selectorFfRepeatListEffectiveShapeSummaries = BuildProtocol03SelectorFfRepeatListEffectiveShapeSummaries(
            selectorFfRepeatListCandidates,
            abilityDefinitions,
            itemCommandEntries,
            gameObjectEntries,
            animationDefinitions).ToList();
        List<Protocol03SelectorFfRepeatListContinuationMarkerSummary> selectorFfRepeatListContinuationMarkerSummaries = BuildProtocol03SelectorFfRepeatListContinuationMarkerSummaries(
            selectorFfRepeatListCandidates,
            dumpSummaries,
            localHeaders,
            abilityDefinitions,
            itemCommandEntries,
            gameObjectEntries,
            animationDefinitions).ToList();
        List<Protocol03SelectorFfRepeatListVendorContinuationBodyOffsetSummary> selectorFfRepeatListVendorContinuationBodyOffsetSummaries = BuildProtocol03SelectorFfRepeatListVendorContinuationBodyOffsetSummaries(
            selectorFfRepeatListCandidates,
            dumpSummaries,
            localHeaders).ToList();
        List<Protocol03SelectorFfRepeatListVendorPrePositionBodyOffsetSummary> selectorFfRepeatListVendorPrePositionBodyOffsetSummaries = BuildProtocol03SelectorFfRepeatListVendorPrePositionBodyOffsetSummaries(
            selectorFfRepeatListCandidates,
            dumpSummaries,
            localHeaders).ToList();
        List<Protocol03SelectorFfRepeatListVendorBodyWindowProbeSummary> selectorFfRepeatListVendorBodyWindowProbeSummaries = BuildProtocol03SelectorFfRepeatListVendorBodyWindowProbeSummaries(
            selectorFfRepeatListCandidates,
            dumpSummaries,
            localHeaders,
            abilityDefinitions,
            itemCommandEntries,
            gameObjectEntries,
            animationDefinitions).ToList();
        List<Protocol03SelectorFfRepeatListVendorBodyWindowResourceCollisionSummary> selectorFfRepeatListVendorBodyWindowResourceCollisionSummaries =
            BuildProtocol03SelectorFfRepeatListVendorBodyWindowResourceCollisionSummaries(selectorFfRepeatListVendorBodyWindowProbeSummaries).ToList();
        List<Protocol03SelectorFfRepeatListVendorBodyWindowEvidenceSummary> selectorFfRepeatListVendorBodyWindowEvidenceSummaries =
            BuildProtocol03SelectorFfRepeatListVendorBodyWindowEvidenceSummaries(selectorFfRepeatListVendorBodyWindowProbeSummaries).ToList();
        List<Protocol03SelectorFfRepeatListVendorBodyWindowParserActionSummary> selectorFfRepeatListVendorBodyWindowParserActionSummaries =
            BuildProtocol03SelectorFfRepeatListVendorBodyWindowParserActionSummaries(selectorFfRepeatListVendorBodyWindowEvidenceSummaries).ToList();
        List<Protocol03SelectorFfRepeatListEffectiveShapeHeaderSummary> selectorFfRepeatListEffectiveShapeHeaderSummaries = BuildProtocol03SelectorFfRepeatListEffectiveShapeHeaderSummaries(
            selectorFfRepeatListCandidates,
            dumpSummaries,
            localHeaders).ToList();
        List<Protocol03SelectorFfRepeatListHeaderNameSummary> selectorFfRepeatListHeaderNameSummaries = BuildProtocol03SelectorFfRepeatListHeaderNameSummaries(
            selectorFfRepeatListCandidates,
            dumpSummaries,
            localHeaders).ToList();
        List<Protocol03SelectorFfRepeatListHeaderRegionSummary> selectorFfRepeatListHeaderRegionSummaries = BuildProtocol03SelectorFfRepeatListHeaderRegionSummaries(
            selectorFfRepeatListCandidates,
            dumpSummaries,
            localHeaders,
            abilityDefinitions,
            itemCommandEntries,
            gameObjectEntries,
            animationDefinitions).ToList();
        List<Protocol03SelectorFfRepeatListVendorBoundaryTargetSummary> selectorFfRepeatListVendorBoundaryTargetSummaries = BuildProtocol03SelectorFfRepeatListVendorBoundaryTargetSummaries(
            selectorFfRepeatListCandidates,
            dumpSummaries,
            localHeaders,
            abilityDefinitions,
            itemCommandEntries,
            gameObjectEntries,
            animationDefinitions).ToList();
        List<Protocol03SelectorFfRepeatListVendorBoundaryInterpretationSummary> selectorFfRepeatListVendorBoundaryInterpretationSummaries = BuildProtocol03SelectorFfRepeatListVendorBoundaryInterpretationSummaries(
            selectorFfRepeatListVendorBoundaryTargetSummaries).ToList();
        List<Protocol03SelectorFfRepeatListVendorBoundaryPrioritySummary> selectorFfRepeatListVendorBoundaryPrioritySummaries = BuildProtocol03SelectorFfRepeatListVendorBoundaryPrioritySummaries(
            selectorFfRepeatListVendorBoundaryTargetSummaries).ToList();
        List<Protocol03SelectorFfRepeatListHeaderSample> selectorFfRepeatListHeaderSamples = BuildProtocol03SelectorFfRepeatListHeaderSamples(
            selectorFfRepeatListCandidates,
            dumpSummaries,
            localHeaders).ToList();
        List<Protocol03SelectorFfRepeatListLayoutSummary> selectorFfRepeatListLayoutSummaries = BuildProtocol03SelectorFfRepeatListLayoutSummaries(
            selectorFfRepeatListShapeSummaries).ToList();
        List<Protocol03NestedMovementBoundaryEvidenceSummary> nestedMovementBoundaryEvidenceSummaries =
            BuildProtocol03NestedMovementBoundaryEvidenceSummaries(dumpSummaries).ToList();
        List<Protocol03NestedMovementMode06BoundaryCandidateSummary> nestedMovementMode06BoundaryCandidateSummaries =
            BuildProtocol03NestedMovementMode06BoundaryCandidateSummaries(dumpSummaries).ToList();
        List<Protocol03NestedMovementMode06FfContinuationSummary> nestedMovementMode06FfContinuationSummaries =
            BuildProtocol03NestedMovementMode06FfContinuationSummaries(dumpSummaries).ToList();
        List<Protocol03NestedMovementMode06PostContinuationBoundarySummary> nestedMovementMode06PostContinuationBoundarySummaries =
            BuildProtocol03NestedMovementMode06PostContinuationBoundarySummaries(dumpSummaries).ToList();
        List<Protocol03NestedMovementMode06PostContinuationBodySummary> nestedMovementMode06PostContinuationBodySummaries =
            BuildProtocol03NestedMovementMode06PostContinuationBodySummaries(dumpSummaries).ToList();
        List<Protocol03NestedMovementMode06PostContinuationPrefixSummary> nestedMovementMode06PostContinuationPrefixSummaries =
            BuildProtocol03NestedMovementMode06PostContinuationPrefixSummaries(dumpSummaries).ToList();
        List<Protocol03NestedMovementMode06PostContinuationRemainderSummary> nestedMovementMode06PostContinuationRemainderSummaries =
            BuildProtocol03NestedMovementMode06PostContinuationRemainderSummaries(dumpSummaries).ToList();
        List<Protocol03NestedMovementMode06PostContinuationTupleSummary> nestedMovementMode06PostContinuationTupleSummaries =
            BuildProtocol03NestedMovementMode06PostContinuationTupleSummaries(dumpSummaries).ToList();
        List<Protocol03NestedMovementMode06PostContinuationTupleBoundarySummary> nestedMovementMode06PostContinuationTupleBoundarySummaries =
            BuildProtocol03NestedMovementMode06PostContinuationTupleBoundarySummaries(dumpSummaries).ToList();
        List<Protocol03NestedMovementMode06PostContinuationTupleBodySummary> nestedMovementMode06PostContinuationTupleBodySummaries =
            BuildProtocol03NestedMovementMode06PostContinuationTupleBodySummaries(dumpSummaries).ToList();
        List<Protocol03NestedMovementMode06PostContinuationTerminalTupleBodySummary> nestedMovementMode06PostContinuationTerminalTupleBodySummaries =
            BuildProtocol03NestedMovementMode06PostContinuationTerminalTupleBodySummaries(dumpSummaries).ToList();
        List<Protocol03NestedMovementMode06LongTupleBodyTailAnchorSummary> nestedMovementMode06LongTupleBodyTailAnchorSummaries =
            BuildProtocol03NestedMovementMode06LongTupleBodyTailAnchorSummaries(
                nestedMovementMode06PostContinuationTupleBodySummaries,
                nestedMovementMode06PostContinuationTerminalTupleBodySummaries).ToList();
        List<Protocol03NestedMovementMode06LongTupleBodyPreAnchorLeadSummary> nestedMovementMode06LongTupleBodyPreAnchorLeadSummaries =
            BuildProtocol03NestedMovementMode06LongTupleBodyPreAnchorLeadSummaries(nestedMovementMode06LongTupleBodyTailAnchorSummaries).ToList();
        List<Protocol03NestedMovementMode06LongTupleBodyPreAnchorBodySummary> nestedMovementMode06LongTupleBodyPreAnchorBodySummaries =
            BuildProtocol03NestedMovementMode06LongTupleBodyPreAnchorBodySummaries(dumpSummaries).ToList();
        List<Protocol03NestedMovementMode06LongTupleBodyPreAnchorOffsetSummary> nestedMovementMode06LongTupleBodyPreAnchorOffsetSummaries =
            BuildProtocol03NestedMovementMode06LongTupleBodyPreAnchorOffsetSummaries(dumpSummaries).ToList();
        List<Protocol03NestedMovementMode06LongTupleBodyPreAnchorHeaderCandidateSummary> nestedMovementMode06LongTupleBodyPreAnchorHeaderCandidateSummaries =
            BuildProtocol03NestedMovementMode06LongTupleBodyPreAnchorHeaderCandidateSummaries(dumpSummaries).ToList();
        List<Protocol03NestedMovementMode06LongTupleBodyPreAnchorEmbeddedMovementSummary> nestedMovementMode06LongTupleBodyPreAnchorEmbeddedMovementSummaries =
            BuildProtocol03NestedMovementMode06LongTupleBodyPreAnchorEmbeddedMovementSummaries(dumpSummaries).ToList();
        List<Protocol03NestedMovementMode06LongTupleBodyPreAnchorEmbeddedMovementTrailerSummary> nestedMovementMode06LongTupleBodyPreAnchorEmbeddedMovementTrailerSummaries =
            BuildProtocol03NestedMovementMode06LongTupleBodyPreAnchorEmbeddedMovementTrailerSummaries(dumpSummaries).ToList();
        List<Protocol03NestedMovementMode06LongTupleBodyPreAnchorEmbeddedMovementTrailerShapeSummary> nestedMovementMode06LongTupleBodyPreAnchorEmbeddedMovementTrailerShapeSummaries =
            BuildProtocol03NestedMovementMode06LongTupleBodyPreAnchorEmbeddedMovementTrailerShapeSummaries(dumpSummaries).ToList();
        List<Protocol03NestedMovementMode06TupleBodyParserActionSummary> nestedMovementMode06TupleBodyParserActionSummaries =
            BuildProtocol03NestedMovementMode06TupleBodyParserActionSummaries(
                nestedMovementMode06PostContinuationTupleBodySummaries,
                nestedMovementMode06PostContinuationTerminalTupleBodySummaries).ToList();
        List<Protocol03NestedMovementParserActionSummary> nestedMovementParserActionSummaries =
            BuildProtocol03NestedMovementParserActionSummaries(nestedMovementBoundaryEvidenceSummaries).ToList();
        List<Protocol03NpcBaseDynamicCreationTailSummary> npcBaseDynamicCreationTailSummaries = BuildProtocol03NpcBaseDynamicCreationTailSummaries(dumpSummaries).ToList();
        List<Protocol03NpcBaseDynamicCreationHeaderLikeLeadSummary> npcBaseDynamicCreationHeaderLikeLeadSummaries = BuildProtocol03NpcBaseDynamicCreationHeaderLikeLeadSummaries(dumpSummaries).ToList();
        List<Protocol03NpcBaseDynamicCreationBodyAnchorSummary> npcBaseDynamicCreationBodyAnchorSummaries = BuildProtocol03NpcBaseDynamicCreationBodyAnchorSummaries(dumpSummaries).ToList();
        List<Protocol03NpcBaseDynamicCreationExtendedBoundarySummary> npcBaseDynamicCreationExtendedBoundarySummaries = BuildProtocol03NpcBaseDynamicCreationExtendedBoundarySummaries(dumpSummaries).ToList();
        List<VendorBuyPriceLeadSummary> vendorBuyPriceLeadSummaries = BuildVendorBuyPriceLeadSummaries(
            dumpSummaries,
            itemCommandEntries,
            vendorPriceEntries,
            vendorInventoryEntries).ToList();
        List<VendorSellPriceLeadSummary> vendorSellPriceLeadSummaries = BuildVendorSellPriceLeadSummaries(
            dumpSummaries,
            abilityDefinitions,
            itemCommandEntries,
            vendorPriceEntries,
            vendorInventoryEntries).ToList();

        return new PacketResearchReport(
            ".",
            dumpRoots.Select(root => DisplayPath(root, repositoryRoot)).ToArray(),
            normalizedRajkoRoot is null ? null : DisplayPath(normalizedRajkoRoot, repositoryRoot),
            normalizedExternalRoots.Select(root => DisplayPath(root, repositoryRoot)).ToArray(),
            localHeaders,
            attributeDefinitions,
            fxDefinitions,
            gameObjectEntries,
            worldEntityEntries,
            rajkoRpcHeaders,
            hardcodedCommands,
            hardcodedProtocol03Examples,
            protocol04InteractionCommandExamples,
            vendorInventoryEntries,
            comparisons,
            dumpSummaries)
        {
            AbilityDefinitions = abilityDefinitions,
            AnimationDefinitions = animationDefinitions,
            ItemCommandEntries = itemCommandEntries,
            VendorPriceEntries = vendorPriceEntries,
            StaticObjectEntries = staticObjectEntries,
            VendorBuyPriceLeadSummaries = vendorBuyPriceLeadSummaries,
            VendorSellPriceLeadSummaries = vendorSellPriceLeadSummaries,
            Protocol03SelectorFfRepeatListCandidates = selectorFfRepeatListCandidates,
            Protocol03SelectorFfRepeatListFieldSummaries = selectorFfRepeatListFieldSummaries,
            Protocol03SelectorFfRepeatListShapeSummaries = selectorFfRepeatListShapeSummaries,
            Protocol03SelectorFfRepeatListEffectiveShapeSummaries = selectorFfRepeatListEffectiveShapeSummaries,
            Protocol03SelectorFfRepeatListContinuationMarkerSummaries = selectorFfRepeatListContinuationMarkerSummaries,
            Protocol03SelectorFfRepeatListVendorContinuationBodyOffsetSummaries = selectorFfRepeatListVendorContinuationBodyOffsetSummaries,
            Protocol03SelectorFfRepeatListVendorPrePositionBodyOffsetSummaries = selectorFfRepeatListVendorPrePositionBodyOffsetSummaries,
            Protocol03SelectorFfRepeatListVendorBodyWindowProbeSummaries = selectorFfRepeatListVendorBodyWindowProbeSummaries,
            Protocol03SelectorFfRepeatListVendorBodyWindowResourceCollisionSummaries = selectorFfRepeatListVendorBodyWindowResourceCollisionSummaries,
            Protocol03SelectorFfRepeatListVendorBodyWindowEvidenceSummaries = selectorFfRepeatListVendorBodyWindowEvidenceSummaries,
            Protocol03SelectorFfRepeatListVendorBodyWindowParserActionSummaries = selectorFfRepeatListVendorBodyWindowParserActionSummaries,
            Protocol03SelectorFfRepeatListEffectiveShapeHeaderSummaries = selectorFfRepeatListEffectiveShapeHeaderSummaries,
            Protocol03SelectorFfRepeatListHeaderNameSummaries = selectorFfRepeatListHeaderNameSummaries,
            Protocol03SelectorFfRepeatListHeaderRegionSummaries = selectorFfRepeatListHeaderRegionSummaries,
            Protocol03SelectorFfRepeatListVendorBoundaryTargetSummaries = selectorFfRepeatListVendorBoundaryTargetSummaries,
            Protocol03SelectorFfRepeatListVendorBoundaryInterpretationSummaries = selectorFfRepeatListVendorBoundaryInterpretationSummaries,
            Protocol03SelectorFfRepeatListVendorBoundaryPrioritySummaries = selectorFfRepeatListVendorBoundaryPrioritySummaries,
            Protocol03SelectorFfRepeatListHeaderSamples = selectorFfRepeatListHeaderSamples,
            Protocol03SelectorFfRepeatListLayoutSummaries = selectorFfRepeatListLayoutSummaries,
            Protocol03NestedMovementBoundaryEvidenceSummaries = nestedMovementBoundaryEvidenceSummaries,
            Protocol03NestedMovementMode06BoundaryCandidateSummaries = nestedMovementMode06BoundaryCandidateSummaries,
            Protocol03NestedMovementMode06FfContinuationSummaries = nestedMovementMode06FfContinuationSummaries,
            Protocol03NestedMovementMode06PostContinuationBoundarySummaries = nestedMovementMode06PostContinuationBoundarySummaries,
            Protocol03NestedMovementMode06PostContinuationBodySummaries = nestedMovementMode06PostContinuationBodySummaries,
            Protocol03NestedMovementMode06PostContinuationPrefixSummaries = nestedMovementMode06PostContinuationPrefixSummaries,
            Protocol03NestedMovementMode06PostContinuationRemainderSummaries = nestedMovementMode06PostContinuationRemainderSummaries,
            Protocol03NestedMovementMode06PostContinuationTupleSummaries = nestedMovementMode06PostContinuationTupleSummaries,
            Protocol03NestedMovementMode06PostContinuationTupleBoundarySummaries = nestedMovementMode06PostContinuationTupleBoundarySummaries,
            Protocol03NestedMovementMode06PostContinuationTupleBodySummaries = nestedMovementMode06PostContinuationTupleBodySummaries,
            Protocol03NestedMovementMode06PostContinuationTerminalTupleBodySummaries = nestedMovementMode06PostContinuationTerminalTupleBodySummaries,
            Protocol03NestedMovementMode06LongTupleBodyTailAnchorSummaries = nestedMovementMode06LongTupleBodyTailAnchorSummaries,
            Protocol03NestedMovementMode06LongTupleBodyPreAnchorLeadSummaries = nestedMovementMode06LongTupleBodyPreAnchorLeadSummaries,
            Protocol03NestedMovementMode06LongTupleBodyPreAnchorBodySummaries = nestedMovementMode06LongTupleBodyPreAnchorBodySummaries,
            Protocol03NestedMovementMode06LongTupleBodyPreAnchorOffsetSummaries = nestedMovementMode06LongTupleBodyPreAnchorOffsetSummaries,
            Protocol03NestedMovementMode06LongTupleBodyPreAnchorHeaderCandidateSummaries = nestedMovementMode06LongTupleBodyPreAnchorHeaderCandidateSummaries,
            Protocol03NestedMovementMode06LongTupleBodyPreAnchorEmbeddedMovementSummaries = nestedMovementMode06LongTupleBodyPreAnchorEmbeddedMovementSummaries,
            Protocol03NestedMovementMode06LongTupleBodyPreAnchorEmbeddedMovementTrailerSummaries = nestedMovementMode06LongTupleBodyPreAnchorEmbeddedMovementTrailerSummaries,
            Protocol03NestedMovementMode06LongTupleBodyPreAnchorEmbeddedMovementTrailerShapeSummaries = nestedMovementMode06LongTupleBodyPreAnchorEmbeddedMovementTrailerShapeSummaries,
            Protocol03NestedMovementMode06TupleBodyParserActionSummaries = nestedMovementMode06TupleBodyParserActionSummaries,
            Protocol03NestedMovementParserActionSummaries = nestedMovementParserActionSummaries,
            Protocol03NpcBaseDynamicCreationTailSummaries = npcBaseDynamicCreationTailSummaries,
            Protocol03NpcBaseDynamicCreationHeaderLikeLeadSummaries = npcBaseDynamicCreationHeaderLikeLeadSummaries,
            Protocol03NpcBaseDynamicCreationBodyAnchorSummaries = npcBaseDynamicCreationBodyAnchorSummaries,
            Protocol03NpcBaseDynamicCreationExtendedBoundarySummaries = npcBaseDynamicCreationExtendedBoundarySummaries
        };
    }

    public static IEnumerable<Protocol03NestedMovementBoundaryEvidenceSummary> BuildProtocol03NestedMovementBoundaryEvidenceSummaries(
        IEnumerable<PacketDumpFileSummary> dumpSummaries)
    {
        return EnumerateProtocol03NestedMovementObservations(dumpSummaries)
            .GroupBy(observation =>
            {
                string disposition = ClassifyProtocol03NestedMovementEvidence(observation);
                (string action, string reason) = GetProtocol03NestedMovementParserAction(disposition);
                return new
                {
                    EvidenceDisposition = disposition,
                    ParserAction = action,
                    ActionReason = reason
                };
            })
            .Select(group =>
            {
                Protocol03NestedMovementObservation[] observations = group.ToArray();
                Protocol03NestedMovementObservation sample = observations[0];
                return new Protocol03NestedMovementBoundaryEvidenceSummary(
                    group.Key.EvidenceDisposition,
                    group.Key.ParserAction,
                    group.Key.ActionReason,
                    observations.Length,
                    observations.Count(observation => observation.IsBoundedPrimaryWrapper),
                    observations.Count(observation => !observation.IsBoundedPrimaryWrapper),
                    CountProtocol03NestedMovementValues(observations.Select(observation => observation.Lead.MarkerModeHex), 8),
                    CountProtocol03NestedMovementValues(observations.Select(observation => observation.Lead.InnerSelector), 8),
                    CountProtocol03NestedMovementValues(observations.Select(observation => observation.Lead.OuterSelector), 12),
                    CountProtocol03NestedMovementValues(observations.Select(observation => observation.Sample.Classification), 12),
                    CountProtocol03NestedMovementValues(observations.Select(observation => FormatProtocol03NestedMovementEmptyHex(observation.Lead.SuffixHex)), 12),
                    CountProtocol03NestedMovementValues(observations.Select(observation => FormatProtocol03NestedMovementEmptyHex(observation.Lead.PostSuffixHex)), 12),
                    CountProtocol03NestedMovementValues(observations.Select(observation => observation.Lead.PayloadBytes.ToString(CultureInfo.InvariantCulture)), 12),
                    CountProtocol03NestedMovementValues(observations.Select(observation => FormatProtocol03NestedMovementPosition(observation.Lead)), 8),
                    sample.File,
                    sample.Line);
            })
            .OrderBy(summary => GetProtocol03NestedMovementParserActionOrder(summary.ParserAction))
            .ThenByDescending(summary => summary.WindowCount)
            .ThenBy(summary => summary.EvidenceDisposition, StringComparer.OrdinalIgnoreCase);
    }

    public static IEnumerable<Protocol03NestedMovementMode06BoundaryCandidateSummary> BuildProtocol03NestedMovementMode06BoundaryCandidateSummaries(
        IEnumerable<PacketDumpFileSummary> dumpSummaries)
    {
        return EnumerateProtocol03NestedMovementObservations(dumpSummaries)
            .Where(observation => !observation.IsBoundedPrimaryWrapper)
            .Where(observation => observation.Lead.MarkerModeHex.Equals("06", StringComparison.OrdinalIgnoreCase))
            .GroupBy(observation => new
            {
                TailPrefixKind = ClassifyProtocol03NestedMovementMode06TailPrefix(observation.Lead),
                SuffixPrefixHex = FormatProtocol03NestedMovementEmptyHex(observation.Lead.SuffixHex),
                PostSuffixPrefixHex = FormatProtocol03NestedMovementEmptyHex(observation.Lead.PostSuffixHex)
            })
            .Select(group =>
            {
                Protocol03NestedMovementObservation[] observations = group.ToArray();
                Protocol03NestedMovementObservation sample = observations[0];
                return new Protocol03NestedMovementMode06BoundaryCandidateSummary(
                    group.Key.TailPrefixKind,
                    group.Key.SuffixPrefixHex,
                    group.Key.PostSuffixPrefixHex,
                    observations.Length,
                    CountProtocol03NestedMovementValues(observations.Select(observation => GetProtocol03NestedMovementTailStartOffset(observation.Lead).ToString(CultureInfo.InvariantCulture)), 8),
                    CountProtocol03NestedMovementValues(observations.Select(observation => GetProtocol03NestedMovementCandidateCutOffset(observation.Lead).ToString(CultureInfo.InvariantCulture)), 8),
                    CountProtocol03NestedMovementValues(observations.Select(observation => GetProtocol03NestedMovementTailByteCount(observation.Lead).ToString(CultureInfo.InvariantCulture)), 8),
                    CountProtocol03NestedMovementValues(observations.SelectMany(observation => EnumerateProtocol03NestedMovementHeaderLikeTailLeads(observation.Lead).DefaultIfEmpty("-")), 8),
                    CountProtocol03NestedMovementValues(observations.Select(observation => observation.Lead.InnerSelector), 8),
                    CountProtocol03NestedMovementValues(observations.Select(observation => observation.Lead.OuterSelector), 12),
                    CountProtocol03NestedMovementValues(observations.Select(observation => observation.Sample.Classification), 12),
                    CountProtocol03NestedMovementValues(observations.Select(observation => observation.Lead.PayloadBytes.ToString(CultureInfo.InvariantCulture)), 12),
                    CountProtocol03NestedMovementValues(observations.Select(observation => FormatProtocol03NestedMovementEmptyHex(observation.Lead.LeadPrefixHex)), 12),
                    CountProtocol03NestedMovementValues(observations.Select(observation => FormatProtocol03NestedMovementPosition(observation.Lead)), 8),
                    sample.File,
                    sample.Line);
            })
            .OrderByDescending(summary => summary.WindowCount)
            .ThenBy(summary => summary.TailPrefixKind, StringComparer.OrdinalIgnoreCase)
            .ThenBy(summary => summary.SuffixPrefixHex, StringComparer.OrdinalIgnoreCase)
            .ThenBy(summary => summary.PostSuffixPrefixHex, StringComparer.OrdinalIgnoreCase);
    }

    public static IEnumerable<Protocol03NestedMovementMode06FfContinuationSummary> BuildProtocol03NestedMovementMode06FfContinuationSummaries(
        IEnumerable<PacketDumpFileSummary> dumpSummaries)
    {
        var entries = EnumerateProtocol03NestedMovementObservations(dumpSummaries)
            .Where(observation => !observation.IsBoundedPrimaryWrapper)
            .Where(observation => observation.Lead.MarkerModeHex.Equals("06", StringComparison.OrdinalIgnoreCase))
            .Select(observation => new
            {
                Observation = observation,
                Tail = GetProtocol03NestedMovementTailByteValues(observation.Lead).ToArray()
            })
            .Where(entry => IsProtocol03NestedMovementMode06FfContinuationLead(entry.Tail))
            .ToArray();

        return entries
            .GroupBy(entry => new
            {
                TailPrefixKind = ClassifyProtocol03NestedMovementMode06TailPrefix(entry.Observation.Lead),
                MarkerPrefixHex = FormatHeader(entry.Tail.Take(4)),
                TailU16Offset4Hex = FormatHeader(entry.Tail.Skip(4).Take(2)),
                TailU16Offset6Hex = FormatHeader(entry.Tail.Skip(6).Take(2)),
                ContinuationPrefixHex = FormatHeader(entry.Tail.Skip(8).Take(3)),
                ContinuationMarkerHex = entry.Tail[11].ToString("x2", CultureInfo.InvariantCulture),
                PostMarkerPrefixHex = FormatHeader(entry.Tail.Skip(12).Take(4))
            })
            .Select(group =>
            {
                var groupEntries = group.ToArray();
                Protocol03NestedMovementObservation sample = groupEntries[0].Observation;
                return new Protocol03NestedMovementMode06FfContinuationSummary(
                    group.Key.TailPrefixKind,
                    group.Key.MarkerPrefixHex,
                    group.Key.TailU16Offset4Hex,
                    group.Key.TailU16Offset6Hex,
                    group.Key.ContinuationPrefixHex,
                    group.Key.ContinuationMarkerHex,
                    group.Key.PostMarkerPrefixHex,
                    groupEntries.Length,
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => GetProtocol03NestedMovementTailStartOffset(entry.Observation.Lead).ToString(CultureInfo.InvariantCulture)), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => GetProtocol03NestedMovementCandidateCutOffset(entry.Observation.Lead).ToString(CultureInfo.InvariantCulture)), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => (GetProtocol03NestedMovementTailStartOffset(entry.Observation.Lead) + entry.Tail.Length).ToString(CultureInfo.InvariantCulture)), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => GetProtocol03NestedMovementTailByteCount(entry.Observation.Lead).ToString(CultureInfo.InvariantCulture)), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.Observation.Lead.InnerSelector), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.Observation.Lead.OuterSelector), 12),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.Observation.Sample.Classification), 12),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.Observation.Lead.PayloadBytes.ToString(CultureInfo.InvariantCulture)), 12),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => FormatProtocol03NestedMovementPosition(entry.Observation.Lead)), 8),
                    sample.File,
                    sample.Line);
            })
            .OrderByDescending(summary => summary.WindowCount)
            .ThenBy(summary => summary.TailPrefixKind, StringComparer.OrdinalIgnoreCase)
            .ThenBy(summary => summary.ContinuationPrefixHex, StringComparer.OrdinalIgnoreCase)
            .ThenBy(summary => summary.PostMarkerPrefixHex, StringComparer.OrdinalIgnoreCase);
    }

    public static IEnumerable<Protocol03NestedMovementMode06PostContinuationBoundarySummary> BuildProtocol03NestedMovementMode06PostContinuationBoundarySummaries(
        IEnumerable<PacketDumpFileSummary> dumpSummaries)
    {
        var entries = EnumerateProtocol03NestedMovementObservations(dumpSummaries)
            .Where(observation => !observation.IsBoundedPrimaryWrapper)
            .Where(observation => observation.Lead.MarkerModeHex.Equals("06", StringComparison.OrdinalIgnoreCase))
            .Select(observation => new
            {
                Observation = observation,
                Tail = GetProtocol03NestedMovementFullTailByteValues(observation.Lead).ToArray()
            })
            .Where(entry => IsProtocol03NestedMovementMode06FfContinuationLead(entry.Tail))
            .Select(entry =>
            {
                bool hasBoundary = TryFindProtocol03ObjectViewHeader(
                    entry.Tail,
                    16,
                    entry.Tail.Length,
                    Protocol03DynamicTailExtendedBoundarySearchBytes,
                    includeNpcBasePostCreationTailSelectors: false,
                    out int boundaryOffset);
                int? tailBoundaryOffset = hasBoundary ? boundaryOffset : null;
                int? postContinuationOffset = hasBoundary ? boundaryOffset - 16 : null;
                string preBoundaryPrefixHex = hasBoundary
                    ? FormatHeader(entry.Tail.Skip(16).Take(Math.Min(8, Math.Max(0, boundaryOffset - 16))))
                    : FormatHeader(entry.Tail.Skip(16).Take(Math.Min(8, Math.Max(0, entry.Tail.Length - 16))));
                string headerHex = hasBoundary
                    ? FormatHeader(entry.Tail.Skip(boundaryOffset).Take(Math.Min(8, Math.Max(0, entry.Tail.Length - boundaryOffset))))
                    : string.Empty;
                string headerClassification = hasBoundary
                    ? ClassifyProtocol03ObjectView(entry.Tail[boundaryOffset + 2], entry.Tail[boundaryOffset + 3])
                    : string.Empty;
                string disposition = hasBoundary
                    ? "later object-view candidate after continuation"
                    : "no object-view candidate within 64 bytes after continuation";

                return new
                {
                    entry.Observation,
                    entry.Tail,
                    TailPrefixKind = ClassifyProtocol03NestedMovementMode06TailPrefix(entry.Observation.Lead),
                    BoundaryDisposition = disposition,
                    TailBoundaryOffset = tailBoundaryOffset,
                    PostContinuationOffset = postContinuationOffset,
                    PreBoundaryPrefixHex = preBoundaryPrefixHex,
                    HeaderHex = headerHex,
                    HeaderClassification = headerClassification
                };
            })
            .ToArray();

        return entries
            .GroupBy(entry => new
            {
                entry.TailPrefixKind,
                entry.BoundaryDisposition,
                entry.TailBoundaryOffset,
                entry.PostContinuationOffset,
                entry.PreBoundaryPrefixHex,
                entry.HeaderHex,
                entry.HeaderClassification
            })
            .Select(group =>
            {
                var groupEntries = group.ToArray();
                var sample = groupEntries[0];
                return new Protocol03NestedMovementMode06PostContinuationBoundarySummary(
                    group.Key.TailPrefixKind,
                    group.Key.BoundaryDisposition,
                    group.Key.TailBoundaryOffset,
                    group.Key.PostContinuationOffset,
                    group.Key.PreBoundaryPrefixHex,
                    group.Key.HeaderHex,
                    group.Key.HeaderClassification,
                    groupEntries.Length,
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => GetProtocol03NestedMovementTailStartOffset(entry.Observation.Lead).ToString(CultureInfo.InvariantCulture)), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => (GetProtocol03NestedMovementTailStartOffset(entry.Observation.Lead) + 16).ToString(CultureInfo.InvariantCulture)), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.TailBoundaryOffset is null ? "-" : (GetProtocol03NestedMovementTailStartOffset(entry.Observation.Lead) + entry.TailBoundaryOffset.Value).ToString(CultureInfo.InvariantCulture)), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => GetProtocol03NestedMovementTailByteCount(entry.Observation.Lead).ToString(CultureInfo.InvariantCulture)), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.Observation.Lead.InnerSelector), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.Observation.Lead.OuterSelector), 12),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.Observation.Sample.Classification), 12),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.Observation.Lead.PayloadBytes.ToString(CultureInfo.InvariantCulture)), 12),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => FormatProtocol03NestedMovementPosition(entry.Observation.Lead)), 8),
                    sample.Observation.File,
                    sample.Observation.Line);
            })
            .OrderBy(summary => summary.BoundaryDisposition.Equals("later object-view candidate after continuation", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenByDescending(summary => summary.WindowCount)
            .ThenBy(summary => summary.TailBoundaryOffset ?? int.MaxValue)
            .ThenBy(summary => summary.HeaderClassification, StringComparer.OrdinalIgnoreCase)
            .ThenBy(summary => summary.HeaderHex, StringComparer.OrdinalIgnoreCase);
    }

    public static IEnumerable<Protocol03NestedMovementMode06PostContinuationBodySummary> BuildProtocol03NestedMovementMode06PostContinuationBodySummaries(
        IEnumerable<PacketDumpFileSummary> dumpSummaries)
    {
        var entries = EnumerateProtocol03NestedMovementObservations(dumpSummaries)
            .Where(observation => !observation.IsBoundedPrimaryWrapper)
            .Where(observation => observation.Lead.MarkerModeHex.Equals("06", StringComparison.OrdinalIgnoreCase))
            .Select(observation => new
            {
                Observation = observation,
                Tail = GetProtocol03NestedMovementFullTailByteValues(observation.Lead).ToArray()
            })
            .Where(entry => IsProtocol03NestedMovementMode06FfContinuationLead(entry.Tail))
            .Select(entry =>
            {
                bool hasBoundary = TryFindProtocol03ObjectViewHeader(
                    entry.Tail,
                    16,
                    entry.Tail.Length,
                    Protocol03DynamicTailExtendedBoundarySearchBytes,
                    includeNpcBasePostCreationTailSelectors: false,
                    out int boundaryOffset);

                return new
                {
                    entry.Observation,
                    entry.Tail,
                    HasBoundary = hasBoundary,
                    BoundaryOffset = boundaryOffset
                };
            })
            .Where(entry => entry.HasBoundary)
            .Select(entry =>
            {
                byte[] body = entry.Tail.Skip(16).Take(entry.BoundaryOffset - 16).ToArray();
                int? firstNonZeroOffset = FindFirstNonZeroOffset(body);
                string firstNonZeroFieldHex = firstNonZeroOffset is null
                    ? string.Empty
                    : FormatHeader(body.Skip(firstNonZeroOffset.Value).Take(Math.Min(2, body.Length - firstNonZeroOffset.Value)));
                string bodyDisposition = body.Length == 0
                    ? "empty body before next object-view"
                    : firstNonZeroOffset is null
                        ? "zero-filled body before next object-view"
                        : "nonzero body before next object-view";
                string headerHex = FormatHeader(entry.Tail.Skip(entry.BoundaryOffset).Take(Math.Min(8, Math.Max(0, entry.Tail.Length - entry.BoundaryOffset))));
                string headerClassification = ClassifyProtocol03ObjectView(entry.Tail[entry.BoundaryOffset + 2], entry.Tail[entry.BoundaryOffset + 3]);

                return new
                {
                    entry.Observation,
                    TailPrefixKind = ClassifyProtocol03NestedMovementMode06TailPrefix(entry.Observation.Lead),
                    BodyDisposition = bodyDisposition,
                    PostContinuationBodyBytes = body.Length,
                    BodyPrefixHex = FormatHeader(body.Take(Math.Min(8, body.Length))),
                    BodySuffixHex = FormatHeader(body.Skip(Math.Max(0, body.Length - 8)).Take(Math.Min(8, body.Length))),
                    FirstNonZeroOffset = firstNonZeroOffset,
                    FirstNonZeroFieldHex = firstNonZeroFieldHex,
                    HeaderHex = headerHex,
                    HeaderClassification = headerClassification
                };
            })
            .ToArray();

        return entries
            .GroupBy(entry => new
            {
                entry.TailPrefixKind,
                entry.BodyDisposition,
                entry.PostContinuationBodyBytes,
                entry.BodyPrefixHex,
                entry.BodySuffixHex,
                entry.FirstNonZeroOffset,
                entry.FirstNonZeroFieldHex
            })
            .Select(group =>
            {
                var groupEntries = group.ToArray();
                var sample = groupEntries[0];
                return new Protocol03NestedMovementMode06PostContinuationBodySummary(
                    group.Key.TailPrefixKind,
                    group.Key.BodyDisposition,
                    group.Key.PostContinuationBodyBytes,
                    group.Key.BodyPrefixHex,
                    group.Key.BodySuffixHex,
                    group.Key.FirstNonZeroOffset,
                    group.Key.FirstNonZeroFieldHex,
                    groupEntries.Length,
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => GetProtocol03NestedMovementTailStartOffset(entry.Observation.Lead).ToString(CultureInfo.InvariantCulture)), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => (GetProtocol03NestedMovementTailStartOffset(entry.Observation.Lead) + 16).ToString(CultureInfo.InvariantCulture)), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => (GetProtocol03NestedMovementTailStartOffset(entry.Observation.Lead) + 16 + entry.PostContinuationBodyBytes).ToString(CultureInfo.InvariantCulture)), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.HeaderClassification), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.HeaderHex), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.Observation.Lead.InnerSelector), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.Observation.Lead.OuterSelector), 12),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.Observation.Sample.Classification), 12),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.Observation.Lead.PayloadBytes.ToString(CultureInfo.InvariantCulture)), 12),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => FormatProtocol03NestedMovementPosition(entry.Observation.Lead)), 8),
                    sample.Observation.File,
                    sample.Observation.Line);
            })
            .OrderByDescending(summary => summary.WindowCount)
            .ThenBy(summary => summary.PostContinuationBodyBytes)
            .ThenBy(summary => summary.BodyDisposition, StringComparer.OrdinalIgnoreCase)
            .ThenBy(summary => summary.BodyPrefixHex, StringComparer.OrdinalIgnoreCase)
            .ThenBy(summary => summary.BodySuffixHex, StringComparer.OrdinalIgnoreCase);
    }

    public static IEnumerable<Protocol03NestedMovementMode06PostContinuationPrefixSummary> BuildProtocol03NestedMovementMode06PostContinuationPrefixSummaries(
        IEnumerable<PacketDumpFileSummary> dumpSummaries)
    {
        const int targetPrefixBytes = 31;

        var entries = EnumerateProtocol03NestedMovementObservations(dumpSummaries)
            .Where(observation => !observation.IsBoundedPrimaryWrapper)
            .Where(observation => observation.Lead.MarkerModeHex.Equals("06", StringComparison.OrdinalIgnoreCase))
            .Select(observation => new
            {
                Observation = observation,
                Tail = GetProtocol03NestedMovementFullTailByteValues(observation.Lead).ToArray()
            })
            .Where(entry => IsProtocol03NestedMovementMode06FfContinuationLead(entry.Tail))
            .Select(entry =>
            {
                bool hasBoundary = TryFindProtocol03ObjectViewHeader(
                    entry.Tail,
                    16,
                    entry.Tail.Length,
                    Protocol03DynamicTailExtendedBoundarySearchBytes,
                    includeNpcBasePostCreationTailSelectors: false,
                    out int boundaryOffset);
                int postContinuationBytes = Math.Max(0, entry.Tail.Length - 16);
                int prefixBytes = Math.Min(targetPrefixBytes, postContinuationBytes);
                byte[] prefix = entry.Tail.Skip(16).Take(prefixBytes).ToArray();
                int? firstNonZeroOffset = FindFirstNonZeroOffset(prefix);
                int? postContinuationBoundaryOffset = hasBoundary ? boundaryOffset - 16 : null;
                string boundaryDisposition = hasBoundary
                    ? $"later object-view candidate at +{postContinuationBoundaryOffset!.Value.ToString(CultureInfo.InvariantCulture)}"
                    : "no object-view candidate within 64 bytes after continuation";
                string prefixDisposition = prefixBytes == targetPrefixBytes
                    ? "31-byte post-continuation prefix available"
                    : "short post-continuation prefix";

                return new
                {
                    entry.Observation,
                    TailPrefixKind = ClassifyProtocol03NestedMovementMode06TailPrefix(entry.Observation.Lead),
                    PrefixDisposition = prefixDisposition,
                    PrefixBytes = prefixBytes,
                    PrefixLeadHex = FormatHeader(prefix.Take(Math.Min(8, prefix.Length))),
                    PrefixSuffixHex = FormatHeader(prefix.Skip(Math.Max(0, prefix.Length - 8)).Take(Math.Min(8, prefix.Length))),
                    FirstNonZeroOffset = firstNonZeroOffset,
                    FirstNonZeroFieldHex = firstNonZeroOffset is null
                        ? string.Empty
                        : FormatHeader(prefix.Skip(firstNonZeroOffset.Value).Take(Math.Min(2, prefix.Length - firstNonZeroOffset.Value))),
                    BoundaryDisposition = boundaryDisposition,
                    PostContinuationBoundaryOffset = postContinuationBoundaryOffset,
                    BytesAfterPrefix = postContinuationBytes - prefixBytes
                };
            })
            .ToArray();

        return entries
            .GroupBy(entry => new
            {
                entry.TailPrefixKind,
                entry.PrefixDisposition,
                entry.PrefixBytes,
                entry.PrefixLeadHex,
                entry.PrefixSuffixHex,
                entry.FirstNonZeroOffset,
                entry.FirstNonZeroFieldHex
            })
            .Select(group =>
            {
                var groupEntries = group.ToArray();
                var sample = groupEntries[0];
                return new Protocol03NestedMovementMode06PostContinuationPrefixSummary(
                    group.Key.TailPrefixKind,
                    group.Key.PrefixDisposition,
                    group.Key.PrefixBytes,
                    group.Key.PrefixLeadHex,
                    group.Key.PrefixSuffixHex,
                    group.Key.FirstNonZeroOffset,
                    group.Key.FirstNonZeroFieldHex,
                    groupEntries.Length,
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.BoundaryDisposition), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.PostContinuationBoundaryOffset is null ? "-" : entry.PostContinuationBoundaryOffset.Value.ToString(CultureInfo.InvariantCulture)), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.BytesAfterPrefix.ToString(CultureInfo.InvariantCulture)), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => GetProtocol03NestedMovementTailStartOffset(entry.Observation.Lead).ToString(CultureInfo.InvariantCulture)), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => (GetProtocol03NestedMovementTailStartOffset(entry.Observation.Lead) + 16).ToString(CultureInfo.InvariantCulture)), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.PostContinuationBoundaryOffset is null ? "-" : (GetProtocol03NestedMovementTailStartOffset(entry.Observation.Lead) + 16 + entry.PostContinuationBoundaryOffset.Value).ToString(CultureInfo.InvariantCulture)), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.Observation.Lead.InnerSelector), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.Observation.Lead.OuterSelector), 12),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.Observation.Sample.Classification), 12),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.Observation.Lead.PayloadBytes.ToString(CultureInfo.InvariantCulture)), 12),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => FormatProtocol03NestedMovementPosition(entry.Observation.Lead)), 8),
                    sample.Observation.File,
                    sample.Observation.Line);
            })
            .OrderByDescending(summary => summary.WindowCount)
            .ThenBy(summary => summary.TailPrefixKind, StringComparer.OrdinalIgnoreCase)
            .ThenBy(summary => summary.PrefixDisposition, StringComparer.OrdinalIgnoreCase)
            .ThenBy(summary => summary.FirstNonZeroOffset ?? int.MaxValue)
            .ThenBy(summary => summary.FirstNonZeroFieldHex, StringComparer.OrdinalIgnoreCase)
            .ThenBy(summary => summary.PrefixSuffixHex, StringComparer.OrdinalIgnoreCase);
    }

    public static IEnumerable<Protocol03NestedMovementMode06PostContinuationRemainderSummary> BuildProtocol03NestedMovementMode06PostContinuationRemainderSummaries(
        IEnumerable<PacketDumpFileSummary> dumpSummaries)
    {
        const int targetPrefixBytes = 31;
        const int postPrefixTailOffset = 16 + targetPrefixBytes;

        var entries = EnumerateProtocol03NestedMovementObservations(dumpSummaries)
            .Where(observation => !observation.IsBoundedPrimaryWrapper)
            .Where(observation => observation.Lead.MarkerModeHex.Equals("06", StringComparison.OrdinalIgnoreCase))
            .Select(observation => new
            {
                Observation = observation,
                Tail = GetProtocol03NestedMovementFullTailByteValues(observation.Lead).ToArray()
            })
            .Where(entry => IsProtocol03NestedMovementMode06FfContinuationLead(entry.Tail))
            .Where(entry => entry.Tail.Length >= postPrefixTailOffset)
            .Select(entry =>
            {
                bool hasBoundary = TryFindProtocol03ObjectViewHeader(
                    entry.Tail,
                    16,
                    entry.Tail.Length,
                    Protocol03DynamicTailExtendedBoundarySearchBytes,
                    includeNpcBasePostCreationTailSelectors: false,
                    out int boundaryOffset);
                int postContinuationBytes = Math.Max(0, entry.Tail.Length - 16);
                byte[] prefix = entry.Tail.Skip(16).Take(targetPrefixBytes).ToArray();
                int? prefixFirstNonZeroOffset = FindFirstNonZeroOffset(prefix);
                string prefixFirstNonZeroFieldHex = prefixFirstNonZeroOffset is null
                    ? string.Empty
                    : FormatHeader(prefix.Skip(prefixFirstNonZeroOffset.Value).Take(Math.Min(2, prefix.Length - prefixFirstNonZeroOffset.Value)));
                bool postPrefixLooksLikeObjectView = LooksLikeProtocol03ObjectViewHeader(entry.Tail, postPrefixTailOffset);
                string postPrefixDisposition = postPrefixLooksLikeObjectView
                    ? ClassifyProtocol03ObjectView(entry.Tail[postPrefixTailOffset + 2], entry.Tail[postPrefixTailOffset + 3])
                    : "non-object-view post-prefix lead";
                int? postContinuationBoundaryOffset = hasBoundary ? boundaryOffset - 16 : null;
                string boundaryDisposition = hasBoundary
                    ? $"later object-view candidate at +{postContinuationBoundaryOffset!.Value.ToString(CultureInfo.InvariantCulture)}"
                    : "no object-view candidate within 64 bytes after continuation";

                return new
                {
                    entry.Observation,
                    TailPrefixKind = ClassifyProtocol03NestedMovementMode06TailPrefix(entry.Observation.Lead),
                    PrefixFirstNonZeroFieldHex = prefixFirstNonZeroFieldHex,
                    PostPrefixDisposition = postPrefixDisposition,
                    PostPrefixLeadHex = FormatHeader(entry.Tail.Skip(postPrefixTailOffset).Take(Math.Min(8, entry.Tail.Length - postPrefixTailOffset))),
                    PostPrefixU16Offset0Hex = FormatHeader(entry.Tail.Skip(postPrefixTailOffset).Take(Math.Min(2, entry.Tail.Length - postPrefixTailOffset))),
                    PostPrefixU16Offset4Hex = entry.Tail.Length >= postPrefixTailOffset + 6
                        ? FormatHeader(entry.Tail.Skip(postPrefixTailOffset + 4).Take(2))
                        : string.Empty,
                    PostPrefixU16Offset6Hex = entry.Tail.Length >= postPrefixTailOffset + 8
                        ? FormatHeader(entry.Tail.Skip(postPrefixTailOffset + 6).Take(2))
                        : string.Empty,
                    BoundaryDisposition = boundaryDisposition,
                    PostContinuationBoundaryOffset = postContinuationBoundaryOffset,
                    BytesAfterPrefix = postContinuationBytes - targetPrefixBytes
                };
            })
            .ToArray();

        return entries
            .GroupBy(entry => new
            {
                entry.TailPrefixKind,
                entry.PrefixFirstNonZeroFieldHex,
                entry.PostPrefixDisposition,
                entry.PostPrefixLeadHex,
                entry.PostPrefixU16Offset0Hex,
                entry.PostPrefixU16Offset4Hex,
                entry.PostPrefixU16Offset6Hex
            })
            .Select(group =>
            {
                var groupEntries = group.ToArray();
                var sample = groupEntries[0];
                return new Protocol03NestedMovementMode06PostContinuationRemainderSummary(
                    group.Key.TailPrefixKind,
                    group.Key.PrefixFirstNonZeroFieldHex,
                    group.Key.PostPrefixDisposition,
                    group.Key.PostPrefixLeadHex,
                    group.Key.PostPrefixU16Offset0Hex,
                    group.Key.PostPrefixU16Offset4Hex,
                    group.Key.PostPrefixU16Offset6Hex,
                    groupEntries.Length,
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.BoundaryDisposition), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.PostContinuationBoundaryOffset is null ? "-" : entry.PostContinuationBoundaryOffset.Value.ToString(CultureInfo.InvariantCulture)), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.BytesAfterPrefix.ToString(CultureInfo.InvariantCulture)), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => GetProtocol03NestedMovementTailStartOffset(entry.Observation.Lead).ToString(CultureInfo.InvariantCulture)), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => (GetProtocol03NestedMovementTailStartOffset(entry.Observation.Lead) + 16).ToString(CultureInfo.InvariantCulture)), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.PostContinuationBoundaryOffset is null ? "-" : (GetProtocol03NestedMovementTailStartOffset(entry.Observation.Lead) + 16 + entry.PostContinuationBoundaryOffset.Value).ToString(CultureInfo.InvariantCulture)), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.Observation.Lead.InnerSelector), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.Observation.Lead.OuterSelector), 12),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.Observation.Sample.Classification), 12),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.Observation.Lead.PayloadBytes.ToString(CultureInfo.InvariantCulture)), 12),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => FormatProtocol03NestedMovementPosition(entry.Observation.Lead)), 8),
                    sample.Observation.File,
                    sample.Observation.Line);
            })
            .OrderByDescending(summary => summary.WindowCount)
            .ThenBy(summary => summary.TailPrefixKind, StringComparer.OrdinalIgnoreCase)
            .ThenBy(summary => summary.PrefixFirstNonZeroFieldHex, StringComparer.OrdinalIgnoreCase)
            .ThenBy(summary => summary.PostPrefixDisposition, StringComparer.OrdinalIgnoreCase)
            .ThenBy(summary => summary.PostPrefixLeadHex, StringComparer.OrdinalIgnoreCase);
    }

    public static IEnumerable<Protocol03NestedMovementMode06PostContinuationTupleSummary> BuildProtocol03NestedMovementMode06PostContinuationTupleSummaries(
        IEnumerable<PacketDumpFileSummary> dumpSummaries)
    {
        const int targetPrefixBytes = 31;
        const int postPrefixTailOffset = 16 + targetPrefixBytes;

        var entries = EnumerateProtocol03NestedMovementObservations(dumpSummaries)
            .Where(observation => !observation.IsBoundedPrimaryWrapper)
            .Where(observation => observation.Lead.MarkerModeHex.Equals("06", StringComparison.OrdinalIgnoreCase))
            .Select(observation => new
            {
                Observation = observation,
                Tail = GetProtocol03NestedMovementFullTailByteValues(observation.Lead).ToArray()
            })
            .Where(entry => IsProtocol03NestedMovementMode06FfContinuationLead(entry.Tail))
            .Where(entry => entry.Tail.Length >= postPrefixTailOffset)
            .Select(entry =>
            {
                bool hasBoundary = TryFindProtocol03ObjectViewHeader(
                    entry.Tail,
                    16,
                    entry.Tail.Length,
                    Protocol03DynamicTailExtendedBoundarySearchBytes,
                    includeNpcBasePostCreationTailSelectors: false,
                    out int boundaryOffset);
                int postContinuationBytes = Math.Max(0, entry.Tail.Length - 16);
                byte[] prefix = entry.Tail.Skip(16).Take(targetPrefixBytes).ToArray();
                int? prefixFirstNonZeroOffset = FindFirstNonZeroOffset(prefix);
                string prefixFirstNonZeroFieldHex = prefixFirstNonZeroOffset is null
                    ? string.Empty
                    : FormatHeader(prefix.Skip(prefixFirstNonZeroOffset.Value).Take(Math.Min(2, prefix.Length - prefixFirstNonZeroOffset.Value)));
                bool postPrefixLooksLikeObjectView = LooksLikeProtocol03ObjectViewHeader(entry.Tail, postPrefixTailOffset);
                string postPrefixDisposition = postPrefixLooksLikeObjectView
                    ? ClassifyProtocol03ObjectView(entry.Tail[postPrefixTailOffset + 2], entry.Tail[postPrefixTailOffset + 3])
                    : "non-object-view post-prefix lead";
                int? postContinuationBoundaryOffset = hasBoundary ? boundaryOffset - 16 : null;
                string boundaryDisposition = hasBoundary
                    ? $"later object-view candidate at +{postContinuationBoundaryOffset!.Value.ToString(CultureInfo.InvariantCulture)}"
                    : "no object-view candidate within 64 bytes after continuation";

                return new
                {
                    entry.Observation,
                    TailPrefixKind = ClassifyProtocol03NestedMovementMode06TailPrefix(entry.Observation.Lead),
                    PrefixFirstNonZeroFieldHex = prefixFirstNonZeroFieldHex,
                    TupleLayoutKind = ClassifyProtocol03NestedMovementMode06PostPrefixTuple(entry.Tail, postPrefixTailOffset, postPrefixLooksLikeObjectView, postPrefixDisposition),
                    PostPrefixDisposition = postPrefixDisposition,
                    PostPrefixLeadHex = FormatHeader(entry.Tail.Skip(postPrefixTailOffset).Take(Math.Min(8, entry.Tail.Length - postPrefixTailOffset))),
                    Byte0Hex = FormatProtocol03Byte(entry.Tail, postPrefixTailOffset),
                    Byte1Hex = FormatProtocol03Byte(entry.Tail, postPrefixTailOffset + 1),
                    UpdateCountHex = FormatProtocol03Byte(entry.Tail, postPrefixTailOffset + 2),
                    FirstSelectorHex = FormatProtocol03Byte(entry.Tail, postPrefixTailOffset + 3),
                    MiddleBytesHex = FormatHeader(entry.Tail.Skip(postPrefixTailOffset + 4).Take(Math.Min(3, Math.Max(0, entry.Tail.Length - postPrefixTailOffset - 4)))),
                    MarkerByteHex = FormatProtocol03Byte(entry.Tail, postPrefixTailOffset + 7),
                    BoundaryDisposition = boundaryDisposition,
                    PostContinuationBoundaryOffset = postContinuationBoundaryOffset,
                    BytesAfterPrefix = postContinuationBytes - targetPrefixBytes
                };
            })
            .ToArray();

        return entries
            .GroupBy(entry => new
            {
                entry.TailPrefixKind,
                entry.PrefixFirstNonZeroFieldHex,
                entry.TupleLayoutKind,
                entry.PostPrefixDisposition,
                entry.Byte0Hex,
                entry.UpdateCountHex,
                entry.FirstSelectorHex,
                entry.MiddleBytesHex,
                entry.MarkerByteHex
            })
            .Select(group =>
            {
                var groupEntries = group.ToArray();
                var sample = groupEntries[0];
                return new Protocol03NestedMovementMode06PostContinuationTupleSummary(
                    group.Key.TailPrefixKind,
                    group.Key.PrefixFirstNonZeroFieldHex,
                    group.Key.TupleLayoutKind,
                    group.Key.PostPrefixDisposition,
                    group.Key.Byte0Hex,
                    group.Key.UpdateCountHex,
                    group.Key.FirstSelectorHex,
                    group.Key.MiddleBytesHex,
                    group.Key.MarkerByteHex,
                    groupEntries.Length,
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.BoundaryDisposition), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.PostContinuationBoundaryOffset is null ? "-" : entry.PostContinuationBoundaryOffset.Value.ToString(CultureInfo.InvariantCulture)), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.BytesAfterPrefix.ToString(CultureInfo.InvariantCulture)), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => FormatProtocol03NestedMovementEmptyHex(entry.Byte1Hex)), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.PostPrefixLeadHex), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => GetProtocol03NestedMovementTailStartOffset(entry.Observation.Lead).ToString(CultureInfo.InvariantCulture)), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => (GetProtocol03NestedMovementTailStartOffset(entry.Observation.Lead) + 16).ToString(CultureInfo.InvariantCulture)), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.PostContinuationBoundaryOffset is null ? "-" : (GetProtocol03NestedMovementTailStartOffset(entry.Observation.Lead) + 16 + entry.PostContinuationBoundaryOffset.Value).ToString(CultureInfo.InvariantCulture)), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.Observation.Lead.InnerSelector), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.Observation.Lead.OuterSelector), 12),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.Observation.Sample.Classification), 12),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.Observation.Lead.PayloadBytes.ToString(CultureInfo.InvariantCulture)), 12),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => FormatProtocol03NestedMovementPosition(entry.Observation.Lead)), 8),
                    sample.Observation.File,
                    sample.Observation.Line);
            })
            .OrderByDescending(summary => summary.WindowCount)
            .ThenBy(summary => summary.TailPrefixKind, StringComparer.OrdinalIgnoreCase)
            .ThenBy(summary => summary.PrefixFirstNonZeroFieldHex, StringComparer.OrdinalIgnoreCase)
            .ThenBy(summary => summary.TupleLayoutKind, StringComparer.OrdinalIgnoreCase)
            .ThenBy(summary => summary.UpdateCountHex, StringComparer.OrdinalIgnoreCase)
            .ThenBy(summary => summary.FirstSelectorHex, StringComparer.OrdinalIgnoreCase)
            .ThenBy(summary => summary.MarkerByteHex, StringComparer.OrdinalIgnoreCase);
    }

    public static IEnumerable<Protocol03NestedMovementMode06PostContinuationTupleBoundarySummary> BuildProtocol03NestedMovementMode06PostContinuationTupleBoundarySummaries(
        IEnumerable<PacketDumpFileSummary> dumpSummaries)
    {
        const int targetPrefixBytes = 31;
        const int postPrefixTailOffset = 16 + targetPrefixBytes;
        const int postTupleTailOffset = postPrefixTailOffset + 8;

        var entries = EnumerateProtocol03NestedMovementObservations(dumpSummaries)
            .Where(observation => !observation.IsBoundedPrimaryWrapper)
            .Where(observation => observation.Lead.MarkerModeHex.Equals("06", StringComparison.OrdinalIgnoreCase))
            .Select(observation => new
            {
                Observation = observation,
                Tail = GetProtocol03NestedMovementFullTailByteValues(observation.Lead).ToArray()
            })
            .Where(entry => IsProtocol03NestedMovementMode06FfContinuationLead(entry.Tail))
            .Where(entry => entry.Tail.Length >= postTupleTailOffset)
            .Select(entry =>
            {
                int postContinuationBytes = Math.Max(0, entry.Tail.Length - 16);
                byte[] prefix = entry.Tail.Skip(16).Take(targetPrefixBytes).ToArray();
                int? prefixFirstNonZeroOffset = FindFirstNonZeroOffset(prefix);
                string prefixFirstNonZeroFieldHex = prefixFirstNonZeroOffset is null
                    ? string.Empty
                    : FormatHeader(prefix.Skip(prefixFirstNonZeroOffset.Value).Take(Math.Min(2, prefix.Length - prefixFirstNonZeroOffset.Value)));
                bool postPrefixLooksLikeObjectView = LooksLikeProtocol03ObjectViewHeader(entry.Tail, postPrefixTailOffset);
                string postPrefixDisposition = postPrefixLooksLikeObjectView
                    ? ClassifyProtocol03ObjectView(entry.Tail[postPrefixTailOffset + 2], entry.Tail[postPrefixTailOffset + 3])
                    : "non-object-view post-prefix lead";
                bool tupleIsAcceptedBoundary = postPrefixLooksLikeObjectView &&
                    !postPrefixDisposition.Equals("unclassified object-view update", StringComparison.Ordinal);
                int boundaryAfterTupleOffset = -1;
                bool hasBoundaryAfterTuple = !tupleIsAcceptedBoundary &&
                    TryFindProtocol03ObjectViewHeader(
                        entry.Tail,
                        postTupleTailOffset,
                        entry.Tail.Length,
                        Math.Max(0, entry.Tail.Length - postTupleTailOffset),
                        includeNpcBasePostCreationTailSelectors: false,
                        out boundaryAfterTupleOffset);
                int terminalPostTupleBodyBytes = entry.Tail.Length - postTupleTailOffset;
                bool hasTerminalTupleBody = !tupleIsAcceptedBoundary &&
                    !hasBoundaryAfterTuple &&
                    terminalPostTupleBodyBytes > 0;
                int? tailBoundaryOffset = tupleIsAcceptedBoundary
                    ? postPrefixTailOffset
                    : hasBoundaryAfterTuple
                        ? boundaryAfterTupleOffset
                        : null;
                int? postContinuationBoundaryOffset = tailBoundaryOffset is null ? null : tailBoundaryOffset.Value - 16;
                int? postTupleBodyBytes = tupleIsAcceptedBoundary
                    ? 0
                    : hasBoundaryAfterTuple
                        ? boundaryAfterTupleOffset - postTupleTailOffset
                        : null;
                string boundaryDisposition = tupleIsAcceptedBoundary
                    ? "tuple itself is accepted object-view boundary"
                    : hasBoundaryAfterTuple
                        ? "later accepted object-view boundary after tuple"
                        : hasTerminalTupleBody &&
                            terminalPostTupleBodyBytes == 96 &&
                            IsProtocol03Mode06Terminal96ByteTupleBody(entry.Tail.Skip(postTupleTailOffset).ToArray())
                            ? "terminal 96-byte tuple body at tail end"
                        : "no accepted object-view boundary after tuple";
                string headerHex = tailBoundaryOffset is null
                    ? string.Empty
                    : FormatHeader(entry.Tail.Skip(tailBoundaryOffset.Value).Take(Math.Min(8, Math.Max(0, entry.Tail.Length - tailBoundaryOffset.Value))));
                string headerClassification = tailBoundaryOffset is null
                    ? string.Empty
                    : ClassifyProtocol03ObjectView(entry.Tail[tailBoundaryOffset.Value + 2], entry.Tail[tailBoundaryOffset.Value + 3]);

                return new
                {
                    entry.Observation,
                    TailPrefixKind = ClassifyProtocol03NestedMovementMode06TailPrefix(entry.Observation.Lead),
                    PrefixFirstNonZeroFieldHex = prefixFirstNonZeroFieldHex,
                    TupleLayoutKind = ClassifyProtocol03NestedMovementMode06PostPrefixTuple(entry.Tail, postPrefixTailOffset, postPrefixLooksLikeObjectView, postPrefixDisposition),
                    PostPrefixDisposition = postPrefixDisposition,
                    BoundaryDisposition = boundaryDisposition,
                    TailBoundaryOffset = tailBoundaryOffset,
                    PostContinuationBoundaryOffset = postContinuationBoundaryOffset,
                    PostTupleBodyBytes = postTupleBodyBytes,
                    HeaderHex = headerHex,
                    HeaderClassification = headerClassification,
                    BytesAfterPrefix = postContinuationBytes - targetPrefixBytes,
                    PostPrefixLeadHex = FormatHeader(entry.Tail.Skip(postPrefixTailOffset).Take(Math.Min(8, entry.Tail.Length - postPrefixTailOffset))),
                    Byte1Hex = FormatProtocol03Byte(entry.Tail, postPrefixTailOffset + 1),
                    MarkerByteHex = FormatProtocol03Byte(entry.Tail, postPrefixTailOffset + 7)
                };
            })
            .ToArray();

        return entries
            .GroupBy(entry => new
            {
                entry.TailPrefixKind,
                entry.PrefixFirstNonZeroFieldHex,
                entry.TupleLayoutKind,
                entry.PostPrefixDisposition,
                entry.BoundaryDisposition,
                entry.TailBoundaryOffset,
                entry.PostContinuationBoundaryOffset,
                entry.PostTupleBodyBytes,
                entry.HeaderHex,
                entry.HeaderClassification
            })
            .Select(group =>
            {
                var groupEntries = group.ToArray();
                var sample = groupEntries[0];
                return new Protocol03NestedMovementMode06PostContinuationTupleBoundarySummary(
                    group.Key.TailPrefixKind,
                    group.Key.PrefixFirstNonZeroFieldHex,
                    group.Key.TupleLayoutKind,
                    group.Key.PostPrefixDisposition,
                    group.Key.BoundaryDisposition,
                    group.Key.TailBoundaryOffset,
                    group.Key.PostContinuationBoundaryOffset,
                    group.Key.PostTupleBodyBytes,
                    group.Key.HeaderHex,
                    group.Key.HeaderClassification,
                    groupEntries.Length,
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.BytesAfterPrefix.ToString(CultureInfo.InvariantCulture)), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.PostPrefixLeadHex), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => FormatProtocol03NestedMovementEmptyHex(entry.Byte1Hex)), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => FormatProtocol03NestedMovementEmptyHex(entry.MarkerByteHex)), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => GetProtocol03NestedMovementTailStartOffset(entry.Observation.Lead).ToString(CultureInfo.InvariantCulture)), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => (GetProtocol03NestedMovementTailStartOffset(entry.Observation.Lead) + 16).ToString(CultureInfo.InvariantCulture)), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.TailBoundaryOffset is null ? "-" : (GetProtocol03NestedMovementTailStartOffset(entry.Observation.Lead) + entry.TailBoundaryOffset.Value).ToString(CultureInfo.InvariantCulture)), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.Observation.Lead.InnerSelector), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.Observation.Lead.OuterSelector), 12),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.Observation.Sample.Classification), 12),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.Observation.Lead.PayloadBytes.ToString(CultureInfo.InvariantCulture)), 12),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => FormatProtocol03NestedMovementPosition(entry.Observation.Lead)), 8),
                    sample.Observation.File,
                    sample.Observation.Line);
            })
            .OrderByDescending(summary => summary.WindowCount)
            .ThenBy(summary => summary.TailPrefixKind, StringComparer.OrdinalIgnoreCase)
            .ThenBy(summary => summary.PrefixFirstNonZeroFieldHex, StringComparer.OrdinalIgnoreCase)
            .ThenBy(summary => summary.TupleLayoutKind, StringComparer.OrdinalIgnoreCase)
            .ThenBy(summary => summary.BoundaryDisposition, StringComparer.OrdinalIgnoreCase)
            .ThenBy(summary => summary.PostTupleBodyBytes ?? int.MaxValue)
            .ThenBy(summary => summary.HeaderClassification, StringComparer.OrdinalIgnoreCase);
    }

    public static IEnumerable<Protocol03NestedMovementMode06PostContinuationTupleBodySummary> BuildProtocol03NestedMovementMode06PostContinuationTupleBodySummaries(
        IEnumerable<PacketDumpFileSummary> dumpSummaries)
    {
        const int targetPrefixBytes = 31;
        const int postPrefixTailOffset = 16 + targetPrefixBytes;
        const int postTupleTailOffset = postPrefixTailOffset + 8;

        var entries = EnumerateProtocol03NestedMovementObservations(dumpSummaries)
            .Where(observation => !observation.IsBoundedPrimaryWrapper)
            .Where(observation => observation.Lead.MarkerModeHex.Equals("06", StringComparison.OrdinalIgnoreCase))
            .Select(observation => new
            {
                Observation = observation,
                Tail = GetProtocol03NestedMovementFullTailByteValues(observation.Lead).ToArray()
            })
            .Where(entry => IsProtocol03NestedMovementMode06FfContinuationLead(entry.Tail))
            .Where(entry => entry.Tail.Length >= postTupleTailOffset)
            .Select(entry =>
            {
                int postContinuationBytes = Math.Max(0, entry.Tail.Length - 16);
                byte[] prefix = entry.Tail.Skip(16).Take(targetPrefixBytes).ToArray();
                int? prefixFirstNonZeroOffset = FindFirstNonZeroOffset(prefix);
                string prefixFirstNonZeroFieldHex = prefixFirstNonZeroOffset is null
                    ? string.Empty
                    : FormatHeader(prefix.Skip(prefixFirstNonZeroOffset.Value).Take(Math.Min(2, prefix.Length - prefixFirstNonZeroOffset.Value)));
                bool postPrefixLooksLikeObjectView = LooksLikeProtocol03ObjectViewHeader(entry.Tail, postPrefixTailOffset);
                string postPrefixDisposition = postPrefixLooksLikeObjectView
                    ? ClassifyProtocol03ObjectView(entry.Tail[postPrefixTailOffset + 2], entry.Tail[postPrefixTailOffset + 3])
                    : "non-object-view post-prefix lead";
                bool tupleIsAcceptedBoundary = postPrefixLooksLikeObjectView &&
                    !postPrefixDisposition.Equals("unclassified object-view update", StringComparison.Ordinal);
                int boundaryAfterTupleOffset = -1;
                bool hasBoundaryAfterTuple = !tupleIsAcceptedBoundary &&
                    TryFindProtocol03ObjectViewHeader(
                        entry.Tail,
                        postTupleTailOffset,
                        entry.Tail.Length,
                        Math.Max(0, entry.Tail.Length - postTupleTailOffset),
                        includeNpcBasePostCreationTailSelectors: false,
                        out boundaryAfterTupleOffset);

                if (!hasBoundaryAfterTuple)
                {
                    return null;
                }

                byte[] body = entry.Tail.Skip(postTupleTailOffset).Take(boundaryAfterTupleOffset - postTupleTailOffset).ToArray();
                (
                    string preAnchorBodyPrefixHex,
                    string preAnchorBodySuffixHex,
                    int? preAnchorFirstNonZeroOffset,
                    string preAnchorFirstNonZeroFieldHex,
                    string preAnchorTrailingLeadHex,
                    string preAnchorPreLeadHex,
                    _,
                    _) = GetProtocol03Mode06PreAnchorBodyEvidence(body);
                int? firstNonZeroOffset = FindFirstNonZeroOffset(body);
                string firstNonZeroFieldHex = firstNonZeroOffset is null
                    ? string.Empty
                    : FormatHeader(body.Skip(firstNonZeroOffset.Value).Take(Math.Min(2, body.Length - firstNonZeroOffset.Value)));
                string bodyDisposition = body.Length == 0
                    ? "empty post-tuple body"
                    : firstNonZeroOffset is null
                        ? "zero-filled post-tuple body"
                        : "nonzero post-tuple body";
                string headerHex = FormatHeader(entry.Tail.Skip(boundaryAfterTupleOffset).Take(Math.Min(8, Math.Max(0, entry.Tail.Length - boundaryAfterTupleOffset))));
                string headerClassification = ClassifyProtocol03ObjectView(entry.Tail[boundaryAfterTupleOffset + 2], entry.Tail[boundaryAfterTupleOffset + 3]);

                return new
                {
                    entry.Observation,
                    TailPrefixKind = ClassifyProtocol03NestedMovementMode06TailPrefix(entry.Observation.Lead),
                    PrefixFirstNonZeroFieldHex = prefixFirstNonZeroFieldHex,
                    TupleLayoutKind = ClassifyProtocol03NestedMovementMode06PostPrefixTuple(entry.Tail, postPrefixTailOffset, postPrefixLooksLikeObjectView, postPrefixDisposition),
                    PostPrefixDisposition = postPrefixDisposition,
                    BodyDisposition = bodyDisposition,
                    PostTupleBodyBytes = body.Length,
                    BodyPrefixHex = FormatHeader(body.Take(Math.Min(16, body.Length))),
                    BodySuffixHex = FormatHeader(body.Skip(Math.Max(0, body.Length - 16)).Take(Math.Min(16, body.Length))),
                    PreAnchorBodyPrefixHex = preAnchorBodyPrefixHex,
                    PreAnchorBodySuffixHex = preAnchorBodySuffixHex,
                    PreAnchorFirstNonZeroOffset = preAnchorFirstNonZeroOffset,
                    PreAnchorFirstNonZeroFieldHex = preAnchorFirstNonZeroFieldHex,
                    PreAnchorTrailingLeadHex = preAnchorTrailingLeadHex,
                    PreAnchorPreLeadHex = preAnchorPreLeadHex,
                    FirstNonZeroOffset = firstNonZeroOffset,
                    FirstNonZeroFieldHex = firstNonZeroFieldHex,
                    HeaderHex = headerHex,
                    HeaderClassification = headerClassification,
                    PostContinuationBoundaryOffset = boundaryAfterTupleOffset - 16,
                    BytesAfterPrefix = postContinuationBytes - targetPrefixBytes,
                    PostPrefixLeadHex = FormatHeader(entry.Tail.Skip(postPrefixTailOffset).Take(Math.Min(8, entry.Tail.Length - postPrefixTailOffset))),
                    Byte1Hex = FormatProtocol03Byte(entry.Tail, postPrefixTailOffset + 1),
                    MarkerByteHex = FormatProtocol03Byte(entry.Tail, postPrefixTailOffset + 7)
                };
            })
            .Where(entry => entry is not null)
            .Select(entry => entry!)
            .ToArray();

        return entries
            .GroupBy(entry => new
            {
                entry.TailPrefixKind,
                entry.PrefixFirstNonZeroFieldHex,
                entry.TupleLayoutKind,
                entry.PostPrefixDisposition,
                entry.BodyDisposition,
                entry.PostTupleBodyBytes,
                entry.BodyPrefixHex,
                entry.BodySuffixHex,
                entry.PreAnchorBodyPrefixHex,
                entry.PreAnchorBodySuffixHex,
                entry.PreAnchorFirstNonZeroOffset,
                entry.PreAnchorFirstNonZeroFieldHex,
                entry.PreAnchorTrailingLeadHex,
                entry.PreAnchorPreLeadHex,
                entry.FirstNonZeroOffset,
                entry.FirstNonZeroFieldHex
            })
            .Select(group =>
            {
                var groupEntries = group.ToArray();
                var sample = groupEntries[0];
                return new Protocol03NestedMovementMode06PostContinuationTupleBodySummary(
                    group.Key.TailPrefixKind,
                    group.Key.PrefixFirstNonZeroFieldHex,
                    group.Key.TupleLayoutKind,
                    group.Key.PostPrefixDisposition,
                    group.Key.BodyDisposition,
                    group.Key.PostTupleBodyBytes,
                    group.Key.BodyPrefixHex,
                    group.Key.BodySuffixHex,
                    group.Key.PreAnchorBodyPrefixHex,
                    group.Key.PreAnchorBodySuffixHex,
                    group.Key.PreAnchorFirstNonZeroOffset,
                    group.Key.PreAnchorFirstNonZeroFieldHex,
                    group.Key.PreAnchorTrailingLeadHex,
                    group.Key.PreAnchorPreLeadHex,
                    group.Key.FirstNonZeroOffset,
                    group.Key.FirstNonZeroFieldHex,
                    groupEntries.Length,
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.HeaderClassification), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.HeaderHex), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.PostContinuationBoundaryOffset.ToString(CultureInfo.InvariantCulture)), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.BytesAfterPrefix.ToString(CultureInfo.InvariantCulture)), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.PostPrefixLeadHex), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => FormatProtocol03NestedMovementEmptyHex(entry.Byte1Hex)), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => FormatProtocol03NestedMovementEmptyHex(entry.MarkerByteHex)), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => GetProtocol03NestedMovementTailStartOffset(entry.Observation.Lead).ToString(CultureInfo.InvariantCulture)), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => (GetProtocol03NestedMovementTailStartOffset(entry.Observation.Lead) + 16).ToString(CultureInfo.InvariantCulture)), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => (GetProtocol03NestedMovementTailStartOffset(entry.Observation.Lead) + 16 + entry.PostContinuationBoundaryOffset).ToString(CultureInfo.InvariantCulture)), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.Observation.Lead.InnerSelector), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.Observation.Lead.OuterSelector), 12),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.Observation.Sample.Classification), 12),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.Observation.Lead.PayloadBytes.ToString(CultureInfo.InvariantCulture)), 12),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => FormatProtocol03NestedMovementPosition(entry.Observation.Lead)), 8),
                    sample.Observation.File,
                    sample.Observation.Line);
            })
            .OrderByDescending(summary => summary.WindowCount)
            .ThenBy(summary => summary.TailPrefixKind, StringComparer.OrdinalIgnoreCase)
            .ThenBy(summary => summary.PrefixFirstNonZeroFieldHex, StringComparer.OrdinalIgnoreCase)
            .ThenBy(summary => summary.TupleLayoutKind, StringComparer.OrdinalIgnoreCase)
            .ThenBy(summary => summary.PostTupleBodyBytes)
            .ThenBy(summary => summary.BodyPrefixHex, StringComparer.OrdinalIgnoreCase)
            .ThenBy(summary => summary.BodySuffixHex, StringComparer.OrdinalIgnoreCase);
    }

    public static IEnumerable<Protocol03NestedMovementMode06PostContinuationTerminalTupleBodySummary> BuildProtocol03NestedMovementMode06PostContinuationTerminalTupleBodySummaries(
        IEnumerable<PacketDumpFileSummary> dumpSummaries)
    {
        const int targetPrefixBytes = 31;
        const int postPrefixTailOffset = 16 + targetPrefixBytes;
        const int postTupleTailOffset = postPrefixTailOffset + 8;

        var entries = EnumerateProtocol03NestedMovementObservations(dumpSummaries)
            .Where(observation => !observation.IsBoundedPrimaryWrapper)
            .Where(observation => observation.Lead.MarkerModeHex.Equals("06", StringComparison.OrdinalIgnoreCase))
            .Select(observation => new
            {
                Observation = observation,
                Tail = GetProtocol03NestedMovementFullTailByteValues(observation.Lead).ToArray()
            })
            .Where(entry => IsProtocol03NestedMovementMode06FfContinuationLead(entry.Tail))
            .Where(entry => entry.Tail.Length >= postTupleTailOffset)
            .Select(entry =>
            {
                int postContinuationBytes = Math.Max(0, entry.Tail.Length - 16);
                byte[] prefix = entry.Tail.Skip(16).Take(targetPrefixBytes).ToArray();
                int? prefixFirstNonZeroOffset = FindFirstNonZeroOffset(prefix);
                string prefixFirstNonZeroFieldHex = prefixFirstNonZeroOffset is null
                    ? string.Empty
                    : FormatHeader(prefix.Skip(prefixFirstNonZeroOffset.Value).Take(Math.Min(2, prefix.Length - prefixFirstNonZeroOffset.Value)));
                bool postPrefixLooksLikeObjectView = LooksLikeProtocol03ObjectViewHeader(entry.Tail, postPrefixTailOffset);
                string postPrefixDisposition = postPrefixLooksLikeObjectView
                    ? ClassifyProtocol03ObjectView(entry.Tail[postPrefixTailOffset + 2], entry.Tail[postPrefixTailOffset + 3])
                    : "non-object-view post-prefix lead";
                bool tupleIsAcceptedBoundary = postPrefixLooksLikeObjectView &&
                    !postPrefixDisposition.Equals("unclassified object-view update", StringComparison.Ordinal);
                int boundaryAfterTupleOffset = -1;
                bool hasBoundaryAfterTuple = !tupleIsAcceptedBoundary &&
                    TryFindProtocol03ObjectViewHeader(
                        entry.Tail,
                        postTupleTailOffset,
                        entry.Tail.Length,
                        Math.Max(0, entry.Tail.Length - postTupleTailOffset),
                        includeNpcBasePostCreationTailSelectors: false,
                        out boundaryAfterTupleOffset);

                if (tupleIsAcceptedBoundary || hasBoundaryAfterTuple)
                {
                    return null;
                }

                byte[] body = entry.Tail.Skip(postTupleTailOffset).ToArray();
                (
                    string preAnchorBodyPrefixHex,
                    string preAnchorBodySuffixHex,
                    int? preAnchorFirstNonZeroOffset,
                    string preAnchorFirstNonZeroFieldHex,
                    string preAnchorTrailingLeadHex,
                    string preAnchorPreLeadHex,
                    int? preTailFieldOffset,
                    string preTailFieldHex) = GetProtocol03Mode06PreAnchorBodyEvidence(body);
                int? firstNonZeroOffset = FindFirstNonZeroOffset(body);
                string firstNonZeroFieldHex = firstNonZeroOffset is null
                    ? string.Empty
                    : FormatHeader(body.Skip(firstNonZeroOffset.Value).Take(Math.Min(2, body.Length - firstNonZeroOffset.Value)));
                string bodyDisposition = body.Length == 0
                    ? "empty terminal post-tuple body"
                    : firstNonZeroOffset is null
                        ? "zero-filled terminal post-tuple body"
                        : "nonzero terminal post-tuple body";

                return new
                {
                    entry.Observation,
                    Tail = entry.Tail,
                    TailPrefixKind = ClassifyProtocol03NestedMovementMode06TailPrefix(entry.Observation.Lead),
                    PrefixFirstNonZeroFieldHex = prefixFirstNonZeroFieldHex,
                    TupleLayoutKind = ClassifyProtocol03NestedMovementMode06PostPrefixTuple(entry.Tail, postPrefixTailOffset, postPrefixLooksLikeObjectView, postPrefixDisposition),
                    PostPrefixDisposition = postPrefixDisposition,
                    BodyDisposition = bodyDisposition,
                    TerminalPostTupleBodyBytes = body.Length,
                    BodyPrefixHex = FormatHeader(body.Take(Math.Min(16, body.Length))),
                    BodySuffixHex = FormatHeader(body.Skip(Math.Max(0, body.Length - 16)).Take(Math.Min(16, body.Length))),
                    PreAnchorBodyPrefixHex = preAnchorBodyPrefixHex,
                    PreAnchorBodySuffixHex = preAnchorBodySuffixHex,
                    PreAnchorFirstNonZeroOffset = preAnchorFirstNonZeroOffset,
                    PreAnchorFirstNonZeroFieldHex = preAnchorFirstNonZeroFieldHex,
                    PreAnchorTrailingLeadHex = preAnchorTrailingLeadHex,
                    PreAnchorPreLeadHex = preAnchorPreLeadHex,
                    PreTailFieldOffset = preTailFieldOffset,
                    PreTailFieldHex = preTailFieldHex,
                    FirstNonZeroOffset = firstNonZeroOffset,
                    FirstNonZeroFieldHex = firstNonZeroFieldHex,
                    BytesAfterPrefix = postContinuationBytes - targetPrefixBytes,
                    PostPrefixLeadHex = FormatHeader(entry.Tail.Skip(postPrefixTailOffset).Take(Math.Min(8, entry.Tail.Length - postPrefixTailOffset))),
                    Byte1Hex = FormatProtocol03Byte(entry.Tail, postPrefixTailOffset + 1),
                    MarkerByteHex = FormatProtocol03Byte(entry.Tail, postPrefixTailOffset + 7)
                };
            })
            .Where(entry => entry is not null)
            .Select(entry => entry!)
            .ToArray();

        return entries
            .GroupBy(entry => new
            {
                entry.TailPrefixKind,
                entry.PrefixFirstNonZeroFieldHex,
                entry.TupleLayoutKind,
                entry.PostPrefixDisposition,
                entry.BodyDisposition,
                entry.TerminalPostTupleBodyBytes,
                entry.PreAnchorFirstNonZeroOffset,
                entry.PreAnchorFirstNonZeroFieldHex,
                entry.PreAnchorTrailingLeadHex,
                entry.PreAnchorPreLeadHex,
                entry.PreTailFieldOffset,
                entry.PreTailFieldHex
            })
            .Select(group =>
            {
                var groupEntries = group.ToArray();
                var sample = groupEntries[0];
                return new Protocol03NestedMovementMode06PostContinuationTerminalTupleBodySummary(
                    group.Key.TailPrefixKind,
                    group.Key.PrefixFirstNonZeroFieldHex,
                    group.Key.TupleLayoutKind,
                    group.Key.PostPrefixDisposition,
                    group.Key.BodyDisposition,
                    group.Key.TerminalPostTupleBodyBytes,
                    groupEntries.Length,
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => FormatProtocol03NestedMovementEmptyHex(entry.BodyPrefixHex)), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => FormatProtocol03NestedMovementEmptyHex(entry.BodySuffixHex)), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => FormatProtocol03NestedMovementEmptyHex(entry.PreAnchorBodyPrefixHex)), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => FormatProtocol03NestedMovementEmptyHex(entry.PreAnchorBodySuffixHex)), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.PreAnchorFirstNonZeroOffset?.ToString(CultureInfo.InvariantCulture) ?? "-"), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => FormatProtocol03NestedMovementEmptyHex(entry.PreAnchorFirstNonZeroFieldHex)), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => FormatProtocol03NestedMovementEmptyHex(entry.PreAnchorTrailingLeadHex)), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => FormatProtocol03NestedMovementEmptyHex(entry.PreAnchorPreLeadHex)), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.PreTailFieldOffset?.ToString(CultureInfo.InvariantCulture) ?? "-"), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => FormatProtocol03NestedMovementEmptyHex(entry.PreTailFieldHex)), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.FirstNonZeroOffset?.ToString(CultureInfo.InvariantCulture) ?? "-"), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => FormatProtocol03NestedMovementEmptyHex(entry.FirstNonZeroFieldHex)), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.BytesAfterPrefix.ToString(CultureInfo.InvariantCulture)), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.PostPrefixLeadHex), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => FormatProtocol03NestedMovementEmptyHex(entry.Byte1Hex)), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => FormatProtocol03NestedMovementEmptyHex(entry.MarkerByteHex)), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => GetProtocol03NestedMovementTailStartOffset(entry.Observation.Lead).ToString(CultureInfo.InvariantCulture)), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => (GetProtocol03NestedMovementTailStartOffset(entry.Observation.Lead) + 16).ToString(CultureInfo.InvariantCulture)), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => (GetProtocol03NestedMovementTailStartOffset(entry.Observation.Lead) + entry.Tail.Length).ToString(CultureInfo.InvariantCulture)), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.Observation.Lead.InnerSelector), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.Observation.Lead.OuterSelector), 12),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.Observation.Sample.Classification), 12),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.Observation.Lead.PayloadBytes.ToString(CultureInfo.InvariantCulture)), 12),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => FormatProtocol03NestedMovementPosition(entry.Observation.Lead)), 8),
                    sample.Observation.File,
                    sample.Observation.Line);
            })
            .OrderByDescending(summary => summary.WindowCount)
            .ThenBy(summary => summary.TailPrefixKind, StringComparer.OrdinalIgnoreCase)
            .ThenBy(summary => summary.PrefixFirstNonZeroFieldHex, StringComparer.OrdinalIgnoreCase)
            .ThenBy(summary => summary.TupleLayoutKind, StringComparer.OrdinalIgnoreCase)
            .ThenBy(summary => summary.TerminalPostTupleBodyBytes)
            .ThenBy(summary => summary.PostPrefixDisposition, StringComparer.OrdinalIgnoreCase);
    }

    public static IEnumerable<Protocol03NestedMovementMode06TupleBodyParserActionSummary> BuildProtocol03NestedMovementMode06TupleBodyParserActionSummaries(
        IEnumerable<Protocol03NestedMovementMode06PostContinuationTupleBodySummary> summaries,
        IEnumerable<Protocol03NestedMovementMode06PostContinuationTerminalTupleBodySummary>? terminalSummaries = null)
    {
        static IReadOnlyDictionary<string, int> SingleValue(string value, int count)
        {
            return CountProtocol03NestedMovementWeightedValues(
                new[] { new WeightedValue(value, count) },
                8);
        }

        var boundedEntries = summaries
            .Select(summary =>
            {
                (string action, string reason) = GetProtocol03NestedMovementMode06TupleBodyParserAction(summary);
                return new
                {
                    ParserAction = action,
                    ActionReason = reason,
                    summary.WindowCount,
                    summary.TailPrefixKind,
                    summary.TupleLayoutKind,
                    summary.PostPrefixDisposition,
                    summary.BodyDisposition,
                    BodyByteLengths = SingleValue(summary.PostTupleBodyBytes.ToString(CultureInfo.InvariantCulture), summary.WindowCount),
                    FirstNonZeroOffsets = SingleValue(summary.FirstNonZeroOffset?.ToString(CultureInfo.InvariantCulture) ?? "-", summary.WindowCount),
                    FirstNonZeroFields = SingleValue(FormatProtocol03NestedMovementEmptyHex(summary.FirstNonZeroFieldHex), summary.WindowCount),
                    summary.HeaderClassifications,
                    summary.HeaderPrefixes,
                    summary.PostPrefixLeads,
                    summary.MarkerBytes,
                    summary.InnerSelectors,
                    summary.OuterSelectors,
                    summary.ObjectClassifications,
                    summary.PayloadBytes,
                    summary.SampleFile,
                    summary.SampleLine
                };
            });
        var terminalEntries = (terminalSummaries ?? Array.Empty<Protocol03NestedMovementMode06PostContinuationTerminalTupleBodySummary>())
            .Select(summary =>
            {
                (string action, string reason) = GetProtocol03NestedMovementMode06TerminalTupleBodyParserAction(summary);
                return new
                {
                    ParserAction = action,
                    ActionReason = reason,
                    summary.WindowCount,
                    summary.TailPrefixKind,
                    summary.TupleLayoutKind,
                    summary.PostPrefixDisposition,
                    summary.BodyDisposition,
                    BodyByteLengths = SingleValue(summary.TerminalPostTupleBodyBytes.ToString(CultureInfo.InvariantCulture), summary.WindowCount),
                    FirstNonZeroOffsets = summary.FirstNonZeroOffsets,
                    FirstNonZeroFields = summary.FirstNonZeroFields,
                    HeaderClassifications = SingleValue("terminal tail end", summary.WindowCount),
                    HeaderPrefixes = SingleValue("-", summary.WindowCount),
                    summary.PostPrefixLeads,
                    summary.MarkerBytes,
                    summary.InnerSelectors,
                    summary.OuterSelectors,
                    summary.ObjectClassifications,
                    summary.PayloadBytes,
                    summary.SampleFile,
                    summary.SampleLine
                };
            });

        return boundedEntries
            .Concat(terminalEntries)
            .GroupBy(entry => new
            {
                entry.ParserAction,
                entry.ActionReason
            })
            .Select(group =>
            {
                var groupEntries = group.ToArray();
                var sample = groupEntries[0];
                return new Protocol03NestedMovementMode06TupleBodyParserActionSummary(
                    group.Key.ParserAction,
                    group.Key.ActionReason,
                    groupEntries.Length,
                    groupEntries.Sum(entry => entry.WindowCount),
                    CountProtocol03NestedMovementWeightedValues(groupEntries.Select(entry => new WeightedValue(entry.TailPrefixKind, entry.WindowCount)), 8),
                    CountProtocol03NestedMovementWeightedValues(groupEntries.Select(entry => new WeightedValue(entry.TupleLayoutKind, entry.WindowCount)), 8),
                    CountProtocol03NestedMovementWeightedValues(groupEntries.Select(entry => new WeightedValue(entry.PostPrefixDisposition, entry.WindowCount)), 8),
                    CountProtocol03NestedMovementWeightedValues(groupEntries.Select(entry => new WeightedValue(entry.BodyDisposition, entry.WindowCount)), 8),
                    CountProtocol03NestedMovementWeightedValues(groupEntries.SelectMany(entry => entry.BodyByteLengths.Select(value => new WeightedValue(value.Key, value.Value))), 8),
                    CountProtocol03NestedMovementWeightedValues(groupEntries.SelectMany(entry => entry.FirstNonZeroOffsets.Select(value => new WeightedValue(value.Key, value.Value))), 8),
                    CountProtocol03NestedMovementWeightedValues(groupEntries.SelectMany(entry => entry.FirstNonZeroFields.Select(value => new WeightedValue(value.Key, value.Value))), 8),
                    CountProtocol03NestedMovementWeightedValues(groupEntries.SelectMany(entry => entry.HeaderClassifications.Select(value => new WeightedValue(value.Key, value.Value))), 8),
                    CountProtocol03NestedMovementWeightedValues(groupEntries.SelectMany(entry => entry.HeaderPrefixes.Select(value => new WeightedValue(value.Key, value.Value))), 8),
                    CountProtocol03NestedMovementWeightedValues(groupEntries.SelectMany(entry => entry.PostPrefixLeads.Select(value => new WeightedValue(value.Key, value.Value))), 8),
                    CountProtocol03NestedMovementWeightedValues(groupEntries.SelectMany(entry => entry.MarkerBytes.Select(value => new WeightedValue(value.Key, value.Value))), 8),
                    CountProtocol03NestedMovementWeightedValues(groupEntries.SelectMany(entry => entry.InnerSelectors.Select(value => new WeightedValue(value.Key, value.Value))), 8),
                    CountProtocol03NestedMovementWeightedValues(groupEntries.SelectMany(entry => entry.OuterSelectors.Select(value => new WeightedValue(value.Key, value.Value))), 12),
                    CountProtocol03NestedMovementWeightedValues(groupEntries.SelectMany(entry => entry.ObjectClassifications.Select(value => new WeightedValue(value.Key, value.Value))), 12),
                    CountProtocol03NestedMovementWeightedValues(groupEntries.SelectMany(entry => entry.PayloadBytes.Select(value => new WeightedValue(value.Key, value.Value))), 12),
                    sample.SampleFile,
                    sample.SampleLine);
            })
            .OrderBy(summary => GetProtocol03NestedMovementMode06TupleBodyParserActionOrder(summary.ParserAction))
            .ThenByDescending(summary => summary.WindowCount)
            .ThenBy(summary => summary.ActionReason, StringComparer.OrdinalIgnoreCase);
    }

    public static IEnumerable<Protocol03NestedMovementMode06LongTupleBodyTailAnchorSummary> BuildProtocol03NestedMovementMode06LongTupleBodyTailAnchorSummaries(
        IEnumerable<Protocol03NestedMovementMode06PostContinuationTupleBodySummary> boundedSummaries,
        IEnumerable<Protocol03NestedMovementMode06PostContinuationTerminalTupleBodySummary> terminalSummaries)
    {
        var boundedRows = boundedSummaries
            .Where(summary => summary.PostTupleBodyBytes > 96)
            .Select(summary => new
            {
                SourceDisposition = "bounded before accepted object-view header",
                summary.TailPrefixKind,
                summary.PrefixFirstNonZeroFieldHex,
                summary.TupleLayoutKind,
                summary.PostPrefixDisposition,
                BodyBytes = summary.PostTupleBodyBytes,
                summary.WindowCount,
                BodyPrefixes = CountProtocol03NestedMovementWeightedValues(
                    new[] { new WeightedValue(FormatProtocol03NestedMovementEmptyHex(summary.BodyPrefixHex), summary.WindowCount) },
                    8),
                BodySuffixes = CountProtocol03NestedMovementWeightedValues(
                    new[] { new WeightedValue(FormatProtocol03NestedMovementEmptyHex(summary.BodySuffixHex), summary.WindowCount) },
                    8),
                PreAnchorBodyPrefixes = CountProtocol03NestedMovementWeightedValues(
                    new[] { new WeightedValue(FormatProtocol03NestedMovementEmptyHex(summary.PreAnchorBodyPrefixHex), summary.WindowCount) },
                    8),
                PreAnchorBodySuffixes = CountProtocol03NestedMovementWeightedValues(
                    new[] { new WeightedValue(FormatProtocol03NestedMovementEmptyHex(summary.PreAnchorBodySuffixHex), summary.WindowCount) },
                    8),
                PreAnchorFirstNonZeroOffsets = CountProtocol03NestedMovementWeightedValues(
                    new[] { new WeightedValue(summary.PreAnchorFirstNonZeroOffset?.ToString(CultureInfo.InvariantCulture) ?? "-", summary.WindowCount) },
                    8),
                PreAnchorFirstNonZeroFields = CountProtocol03NestedMovementWeightedValues(
                    new[] { new WeightedValue(FormatProtocol03NestedMovementEmptyHex(summary.PreAnchorFirstNonZeroFieldHex), summary.WindowCount) },
                    8),
                PreAnchorTrailingLeads = CountProtocol03NestedMovementWeightedValues(
                    new[] { new WeightedValue(FormatProtocol03NestedMovementEmptyHex(summary.PreAnchorTrailingLeadHex), summary.WindowCount) },
                    8),
                PreAnchorPreLeadBytes = CountProtocol03NestedMovementWeightedValues(
                    new[] { new WeightedValue(FormatProtocol03NestedMovementEmptyHex(summary.PreAnchorPreLeadHex), summary.WindowCount) },
                    8),
                summary.HeaderClassifications,
                summary.HeaderPrefixes,
                summary.BytesAfterPrefix,
                summary.PostPrefixLeads,
                summary.MarkerBytes,
                summary.InnerSelectors,
                summary.OuterSelectors,
                summary.ObjectClassifications,
                summary.PayloadBytes,
                summary.PositionSamples,
                summary.SampleFile,
                summary.SampleLine
            });
        var terminalRows = terminalSummaries
            .Where(summary => summary.TerminalPostTupleBodyBytes > 96)
            .Select(summary => new
            {
                SourceDisposition = "terminal tail end",
                summary.TailPrefixKind,
                summary.PrefixFirstNonZeroFieldHex,
                summary.TupleLayoutKind,
                summary.PostPrefixDisposition,
                BodyBytes = summary.TerminalPostTupleBodyBytes,
                summary.WindowCount,
                BodyPrefixes = summary.BodyPrefixes,
                BodySuffixes = summary.BodySuffixes,
                PreAnchorBodyPrefixes = summary.PreAnchorBodyPrefixes,
                PreAnchorBodySuffixes = summary.PreAnchorBodySuffixes,
                PreAnchorFirstNonZeroOffsets = summary.PreAnchorFirstNonZeroOffsets,
                PreAnchorFirstNonZeroFields = summary.PreAnchorFirstNonZeroFields,
                PreAnchorTrailingLeads = summary.PreAnchorTrailingLeads,
                PreAnchorPreLeadBytes = summary.PreAnchorPreLeadBytes,
                HeaderClassifications = CountProtocol03NestedMovementWeightedValues(
                    new[] { new WeightedValue("terminal tail end", summary.WindowCount) },
                    8),
                HeaderPrefixes = CountProtocol03NestedMovementWeightedValues(
                    new[] { new WeightedValue("-", summary.WindowCount) },
                    8),
                summary.BytesAfterPrefix,
                summary.PostPrefixLeads,
                summary.MarkerBytes,
                summary.InnerSelectors,
                summary.OuterSelectors,
                summary.ObjectClassifications,
                summary.PayloadBytes,
                summary.PositionSamples,
                summary.SampleFile,
                summary.SampleLine
            });

        return boundedRows
            .Concat(terminalRows)
            .Select(row =>
            {
                WeightedValue[] tailAnchors = row.BodySuffixes
                    .Select(entry => new WeightedValue(ClassifyProtocol03Mode06LongTupleBodyTailAnchor(entry.Key), entry.Value))
                    .ToArray();
                WeightedValue[] inferredTrailingBodyBytes = row.BodySuffixes
                    .Select(entry => new WeightedValue(GetProtocol03Mode06LongTupleBodyTailAnchorBytes(entry.Key)?.ToString(CultureInfo.InvariantCulture) ?? "-", entry.Value))
                    .ToArray();
                WeightedValue[] preAnchorBodyBytes = row.BodySuffixes
                    .Select(entry =>
                    {
                        int? anchorBytes = GetProtocol03Mode06LongTupleBodyTailAnchorBytes(entry.Key);
                        string value = anchorBytes is null
                            ? "-"
                            : Math.Max(0, row.BodyBytes - anchorBytes.Value).ToString(CultureInfo.InvariantCulture);
                        return new WeightedValue(value, entry.Value);
                    })
                    .ToArray();

                return new Protocol03NestedMovementMode06LongTupleBodyTailAnchorSummary(
                    row.SourceDisposition,
                    row.TailPrefixKind,
                    row.PrefixFirstNonZeroFieldHex,
                    row.TupleLayoutKind,
                    row.PostPrefixDisposition,
                    row.BodyBytes,
                    row.WindowCount,
                    CountProtocol03NestedMovementWeightedValues(tailAnchors, 8),
                    CountProtocol03NestedMovementWeightedValues(inferredTrailingBodyBytes, 8),
                    CountProtocol03NestedMovementWeightedValues(preAnchorBodyBytes, 8),
                    row.PreAnchorBodyPrefixes,
                    row.PreAnchorBodySuffixes,
                    row.PreAnchorFirstNonZeroOffsets,
                    row.PreAnchorFirstNonZeroFields,
                    row.PreAnchorTrailingLeads,
                    row.PreAnchorPreLeadBytes,
                    row.BodyPrefixes,
                    row.BodySuffixes,
                    row.HeaderClassifications,
                    row.HeaderPrefixes,
                    row.BytesAfterPrefix,
                    row.PostPrefixLeads,
                    row.MarkerBytes,
                    row.InnerSelectors,
                    row.OuterSelectors,
                    row.ObjectClassifications,
                    row.PayloadBytes,
                    row.PositionSamples,
                    row.SampleFile,
                    row.SampleLine);
            })
            .OrderByDescending(summary => summary.WindowCount)
            .ThenBy(summary => summary.SourceDisposition, StringComparer.OrdinalIgnoreCase)
            .ThenBy(summary => summary.BodyBytes)
            .ThenBy(summary => summary.PrefixFirstNonZeroFieldHex, StringComparer.OrdinalIgnoreCase);
    }

    public static IEnumerable<Protocol03NestedMovementMode06LongTupleBodyPreAnchorBodySummary> BuildProtocol03NestedMovementMode06LongTupleBodyPreAnchorBodySummaries(
        IEnumerable<PacketDumpFileSummary> dumpSummaries)
    {
        const int targetPrefixBytes = 31;
        const int postPrefixTailOffset = 16 + targetPrefixBytes;
        const int postTupleTailOffset = postPrefixTailOffset + 8;

        var entries = EnumerateProtocol03NestedMovementObservations(dumpSummaries)
            .Where(observation => !observation.IsBoundedPrimaryWrapper)
            .Where(observation => observation.Lead.MarkerModeHex.Equals("06", StringComparison.OrdinalIgnoreCase))
            .Select(observation => new
            {
                Observation = observation,
                Tail = GetProtocol03NestedMovementFullTailByteValues(observation.Lead).ToArray()
            })
            .Where(entry => IsProtocol03NestedMovementMode06FfContinuationLead(entry.Tail))
            .Where(entry => entry.Tail.Length >= postTupleTailOffset)
            .Select(entry =>
            {
                byte[] prefix = entry.Tail.Skip(16).Take(targetPrefixBytes).ToArray();
                int? prefixFirstNonZeroOffset = FindFirstNonZeroOffset(prefix);
                string prefixFirstNonZeroFieldHex = prefixFirstNonZeroOffset is null
                    ? string.Empty
                    : FormatHeader(prefix.Skip(prefixFirstNonZeroOffset.Value).Take(Math.Min(2, prefix.Length - prefixFirstNonZeroOffset.Value)));
                bool postPrefixLooksLikeObjectView = LooksLikeProtocol03ObjectViewHeader(entry.Tail, postPrefixTailOffset);
                string postPrefixDisposition = postPrefixLooksLikeObjectView
                    ? ClassifyProtocol03ObjectView(entry.Tail[postPrefixTailOffset + 2], entry.Tail[postPrefixTailOffset + 3])
                    : "non-object-view post-prefix lead";
                bool tupleIsAcceptedBoundary = postPrefixLooksLikeObjectView &&
                    !postPrefixDisposition.Equals("unclassified object-view update", StringComparison.Ordinal);
                int boundaryAfterTupleOffset = -1;
                bool hasBoundaryAfterTuple = !tupleIsAcceptedBoundary &&
                    TryFindProtocol03ObjectViewHeader(
                        entry.Tail,
                        postTupleTailOffset,
                        entry.Tail.Length,
                        Math.Max(0, entry.Tail.Length - postTupleTailOffset),
                        includeNpcBasePostCreationTailSelectors: false,
                        out boundaryAfterTupleOffset);

                if (tupleIsAcceptedBoundary)
                {
                    return null;
                }

                string sourceDisposition = hasBoundaryAfterTuple
                    ? "bounded before accepted object-view header"
                    : "terminal tail end";
                int bodyEndOffset = hasBoundaryAfterTuple
                    ? boundaryAfterTupleOffset
                    : entry.Tail.Length;
                byte[] body = entry.Tail
                    .Skip(postTupleTailOffset)
                    .Take(Math.Max(0, bodyEndOffset - postTupleTailOffset))
                    .ToArray();
                if (body.Length <= 96)
                {
                    return null;
                }

                string bodySuffixHex = FormatHeader(body.Skip(Math.Max(0, body.Length - 16)).Take(Math.Min(16, body.Length)));
                int? anchorBytes = GetProtocol03Mode06LongTupleBodyTailAnchorBytes(bodySuffixHex);
                if (anchorBytes is null || anchorBytes.Value >= body.Length)
                {
                    return null;
                }

                byte[] preAnchorBody = body.Take(body.Length - anchorBytes.Value).ToArray();
                int? preAnchorFirstNonZeroOffset = FindFirstNonZeroOffset(preAnchorBody);
                string preAnchorFirstNonZeroFieldHex = preAnchorFirstNonZeroOffset is null
                    ? string.Empty
                    : FormatHeader(preAnchorBody.Skip(preAnchorFirstNonZeroOffset.Value).Take(Math.Min(2, preAnchorBody.Length - preAnchorFirstNonZeroOffset.Value)));
                string preAnchorTrailingLeadHex = preAnchorBody.Length >= 8
                    ? FormatHeader(preAnchorBody.Skip(preAnchorBody.Length - 8).Take(8))
                    : string.Empty;
                string preAnchorPreLeadHex = preAnchorBody.Length >= 16
                    ? FormatHeader(preAnchorBody.Skip(preAnchorBody.Length - 16).Take(8))
                    : string.Empty;
                string headerHex = hasBoundaryAfterTuple
                    ? FormatHeader(entry.Tail.Skip(boundaryAfterTupleOffset).Take(Math.Min(8, Math.Max(0, entry.Tail.Length - boundaryAfterTupleOffset))))
                    : "-";
                string headerClassification = hasBoundaryAfterTuple
                    ? ClassifyProtocol03ObjectView(entry.Tail[boundaryAfterTupleOffset + 2], entry.Tail[boundaryAfterTupleOffset + 3])
                    : "terminal tail end";

                return new
                {
                    entry.Observation,
                    SourceDisposition = sourceDisposition,
                    TailAnchorKind = ClassifyProtocol03Mode06LongTupleBodyTailAnchor(bodySuffixHex),
                    PreAnchorTrailingLeadHex = preAnchorTrailingLeadHex,
                    PreAnchorPreLeadHex = preAnchorPreLeadHex,
                    BodyBytes = body.Length,
                    PreAnchorBodyBytes = preAnchorBody.Length,
                    InferredTrailingBodyBytes = anchorBytes.Value,
                    PrefixFirstNonZeroFieldHex = prefixFirstNonZeroFieldHex,
                    TupleLayoutKind = ClassifyProtocol03NestedMovementMode06PostPrefixTuple(entry.Tail, postPrefixTailOffset, postPrefixLooksLikeObjectView, postPrefixDisposition),
                    PostPrefixDisposition = postPrefixDisposition,
                    PreAnchorFirstNonZeroOffset = preAnchorFirstNonZeroOffset,
                    PreAnchorFirstNonZeroFieldHex = preAnchorFirstNonZeroFieldHex,
                    PreAnchorNonZeroByteOffsets = FormatProtocol03Mode06PreAnchorNonZeroByteOffsets(preAnchorBody, 32),
                    PreAnchorHeaderLikeOffsets = FormatProtocol03Mode06PreAnchorHeaderLikeOffsets(preAnchorBody, 16),
                    PreAnchorPrefixHex = FormatHeader(preAnchorBody.Take(Math.Min(32, preAnchorBody.Length))),
                    PreAnchorFirstNonZeroWindowHex = FormatProtocol03Mode06PreAnchorFirstNonZeroWindow(preAnchorBody, preAnchorFirstNonZeroOffset),
                    PreAnchorSuffixHex = FormatHeader(preAnchorBody.Skip(Math.Max(0, preAnchorBody.Length - 32)).Take(Math.Min(32, preAnchorBody.Length))),
                    BodySuffixHex = bodySuffixHex,
                    HeaderClassification = headerClassification,
                    HeaderHex = headerHex
                };
            })
            .Where(entry => entry is not null)
            .Select(entry => entry!)
            .ToArray();

        var leadWindowCounts = entries
            .GroupBy(entry => new
            {
                entry.SourceDisposition,
                entry.PreAnchorTrailingLeadHex,
                entry.PreAnchorPreLeadHex,
                entry.TailAnchorKind
            })
            .ToDictionary(
                group => group.Key,
                group => group.Count());

        return entries
            .GroupBy(entry => new
            {
                entry.SourceDisposition,
                entry.TailAnchorKind,
                entry.PreAnchorTrailingLeadHex,
                entry.PreAnchorPreLeadHex,
                entry.BodyBytes,
                entry.PreAnchorBodyBytes,
                entry.InferredTrailingBodyBytes,
                entry.PrefixFirstNonZeroFieldHex,
                entry.TupleLayoutKind,
                entry.PostPrefixDisposition,
                entry.PreAnchorFirstNonZeroOffset,
                entry.PreAnchorFirstNonZeroFieldHex,
                entry.PreAnchorNonZeroByteOffsets,
                entry.PreAnchorHeaderLikeOffsets,
                entry.PreAnchorPrefixHex,
                entry.PreAnchorFirstNonZeroWindowHex,
                entry.PreAnchorSuffixHex,
                entry.BodySuffixHex
            })
            .Select(group =>
            {
                var groupEntries = group.ToArray();
                var sample = groupEntries[0];
                var leadKey = new
                {
                    group.Key.SourceDisposition,
                    group.Key.PreAnchorTrailingLeadHex,
                    group.Key.PreAnchorPreLeadHex,
                    group.Key.TailAnchorKind
                };
                int leadWindowCount = leadWindowCounts[leadKey];
                (string parserTarget, string actionReason) = GetProtocol03NestedMovementMode06LongTupleBodyPreAnchorLeadTarget(
                    group.Key.TailAnchorKind,
                    leadWindowCount);

                return new Protocol03NestedMovementMode06LongTupleBodyPreAnchorBodySummary(
                    parserTarget,
                    actionReason,
                    group.Key.SourceDisposition,
                    group.Key.TailAnchorKind,
                    group.Key.PreAnchorTrailingLeadHex,
                    group.Key.PreAnchorPreLeadHex,
                    group.Key.BodyBytes,
                    group.Key.PreAnchorBodyBytes,
                    group.Key.InferredTrailingBodyBytes,
                    groupEntries.Length,
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => FormatProtocol03NestedMovementEmptyHex(entry.PrefixFirstNonZeroFieldHex)), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.TupleLayoutKind), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.PostPrefixDisposition), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.PreAnchorFirstNonZeroOffset?.ToString(CultureInfo.InvariantCulture) ?? "-"), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => FormatProtocol03NestedMovementEmptyHex(entry.PreAnchorFirstNonZeroFieldHex)), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.PreAnchorNonZeroByteOffsets), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.PreAnchorHeaderLikeOffsets), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => FormatProtocol03NestedMovementEmptyHex(entry.PreAnchorPrefixHex)), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => FormatProtocol03NestedMovementEmptyHex(entry.PreAnchorFirstNonZeroWindowHex)), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => FormatProtocol03NestedMovementEmptyHex(entry.PreAnchorSuffixHex)), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => FormatProtocol03NestedMovementEmptyHex(entry.BodySuffixHex)), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.HeaderClassification), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.HeaderHex), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.Observation.Sample.Classification), 12),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.Observation.Lead.PayloadBytes.ToString(CultureInfo.InvariantCulture)), 12),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => FormatProtocol03NestedMovementPosition(entry.Observation.Lead)), 8),
                    sample.Observation.File,
                    sample.Observation.Line);
            })
            .OrderBy(summary => GetProtocol03NestedMovementMode06LongTupleBodyPreAnchorLeadTargetOrder(summary.ParserTarget))
            .ThenByDescending(summary => summary.WindowCount)
            .ThenBy(summary => summary.PreAnchorTrailingLeadHex, StringComparer.OrdinalIgnoreCase)
            .ThenBy(summary => summary.PreAnchorBodyBytes)
            .ThenBy(summary => summary.BodyBytes);
    }

    public static IEnumerable<Protocol03NestedMovementMode06LongTupleBodyPreAnchorOffsetSummary> BuildProtocol03NestedMovementMode06LongTupleBodyPreAnchorOffsetSummaries(
        IEnumerable<PacketDumpFileSummary> dumpSummaries)
    {
        const int targetPrefixBytes = 31;
        const int postPrefixTailOffset = 16 + targetPrefixBytes;
        const int postTupleTailOffset = postPrefixTailOffset + 8;

        var entries = EnumerateProtocol03NestedMovementObservations(dumpSummaries)
            .Where(observation => !observation.IsBoundedPrimaryWrapper)
            .Where(observation => observation.Lead.MarkerModeHex.Equals("06", StringComparison.OrdinalIgnoreCase))
            .Select(observation => new
            {
                Observation = observation,
                Tail = GetProtocol03NestedMovementFullTailByteValues(observation.Lead).ToArray()
            })
            .Where(entry => IsProtocol03NestedMovementMode06FfContinuationLead(entry.Tail))
            .Where(entry => entry.Tail.Length >= postTupleTailOffset)
            .Select(entry =>
            {
                bool postPrefixLooksLikeObjectView = LooksLikeProtocol03ObjectViewHeader(entry.Tail, postPrefixTailOffset);
                string postPrefixDisposition = postPrefixLooksLikeObjectView
                    ? ClassifyProtocol03ObjectView(entry.Tail[postPrefixTailOffset + 2], entry.Tail[postPrefixTailOffset + 3])
                    : "non-object-view post-prefix lead";
                bool tupleIsAcceptedBoundary = postPrefixLooksLikeObjectView &&
                    !postPrefixDisposition.Equals("unclassified object-view update", StringComparison.Ordinal);
                int boundaryAfterTupleOffset = -1;
                bool hasBoundaryAfterTuple = !tupleIsAcceptedBoundary &&
                    TryFindProtocol03ObjectViewHeader(
                        entry.Tail,
                        postTupleTailOffset,
                        entry.Tail.Length,
                        Math.Max(0, entry.Tail.Length - postTupleTailOffset),
                        includeNpcBasePostCreationTailSelectors: false,
                        out boundaryAfterTupleOffset);

                if (tupleIsAcceptedBoundary)
                {
                    return null;
                }

                string sourceDisposition = hasBoundaryAfterTuple
                    ? "bounded before accepted object-view header"
                    : "terminal tail end";
                int bodyEndOffset = hasBoundaryAfterTuple
                    ? boundaryAfterTupleOffset
                    : entry.Tail.Length;
                byte[] body = entry.Tail
                    .Skip(postTupleTailOffset)
                    .Take(Math.Max(0, bodyEndOffset - postTupleTailOffset))
                    .ToArray();
                if (body.Length <= 96)
                {
                    return null;
                }

                string bodySuffixHex = FormatHeader(body.Skip(Math.Max(0, body.Length - 16)).Take(Math.Min(16, body.Length)));
                int? anchorBytes = GetProtocol03Mode06LongTupleBodyTailAnchorBytes(bodySuffixHex);
                if (anchorBytes is null || anchorBytes.Value >= body.Length)
                {
                    return null;
                }

                byte[] preAnchorBody = body.Take(body.Length - anchorBytes.Value).ToArray();
                string preAnchorTrailingLeadHex = preAnchorBody.Length >= 8
                    ? FormatHeader(preAnchorBody.Skip(preAnchorBody.Length - 8).Take(8))
                    : string.Empty;
                string preAnchorPreLeadHex = preAnchorBody.Length >= 16
                    ? FormatHeader(preAnchorBody.Skip(preAnchorBody.Length - 16).Take(8))
                    : string.Empty;

                return new
                {
                    entry.Observation,
                    SourceDisposition = sourceDisposition,
                    TailAnchorKind = ClassifyProtocol03Mode06LongTupleBodyTailAnchor(bodySuffixHex),
                    PreAnchorTrailingLeadHex = preAnchorTrailingLeadHex,
                    PreAnchorPreLeadHex = preAnchorPreLeadHex,
                    BodyBytes = body.Length,
                    PreAnchorBody = preAnchorBody
                };
            })
            .Where(entry => entry is not null)
            .Select(entry => entry!)
            .ToArray();

        var leadWindowCounts = entries
            .GroupBy(entry => (
                entry.SourceDisposition,
                entry.PreAnchorTrailingLeadHex,
                entry.PreAnchorPreLeadHex,
                entry.TailAnchorKind))
            .ToDictionary(
                group => group.Key,
                group => group.Count());

        return entries
            .SelectMany(entry =>
            {
                var leadKey = (
                    entry.SourceDisposition,
                    entry.PreAnchorTrailingLeadHex,
                    entry.PreAnchorPreLeadHex,
                    entry.TailAnchorKind);
                var (parserTarget, _) = GetProtocol03NestedMovementMode06LongTupleBodyPreAnchorLeadTarget(
                    entry.TailAnchorKind,
                    leadWindowCounts[leadKey]);

                return Enumerable.Range(0, entry.PreAnchorBody.Length)
                    .Select(offset => new
                    {
                        entry.Observation,
                        entry.SourceDisposition,
                        entry.TailAnchorKind,
                        entry.PreAnchorTrailingLeadHex,
                        entry.BodyBytes,
                        entry.PreAnchorBody,
                        Offset = offset,
                        ParserTarget = parserTarget,
                        ByteHex = entry.PreAnchorBody[offset].ToString("x2", CultureInfo.InvariantCulture),
                        TwoByteHex = FormatHeader(entry.PreAnchorBody.Skip(offset).Take(Math.Min(2, entry.PreAnchorBody.Length - offset))),
                        FourByteHex = FormatHeader(entry.PreAnchorBody.Skip(offset).Take(Math.Min(4, entry.PreAnchorBody.Length - offset))),
                        HeaderClassification = LooksLikeProtocol03ObjectViewHeader(entry.PreAnchorBody, offset)
                            ? ClassifyProtocol03ObjectView(entry.PreAnchorBody[offset + 2], entry.PreAnchorBody[offset + 3])
                            : "-"
                    });
            })
            .GroupBy(entry => entry.Offset)
            .Select(group =>
            {
                var groupEntries = group.ToArray();
                var sample = groupEntries[0];
                int nonZeroCount = groupEntries.Count(entry => !entry.ByteHex.Equals("00", StringComparison.Ordinal));
                int headerLikeCount = groupEntries.Count(entry => !entry.HeaderClassification.Equals("-", StringComparison.Ordinal));

                return new Protocol03NestedMovementMode06LongTupleBodyPreAnchorOffsetSummary(
                    group.Key,
                    groupEntries.Length,
                    nonZeroCount,
                    headerLikeCount,
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.ByteHex), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => FormatProtocol03NestedMovementEmptyHex(entry.TwoByteHex)), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => FormatProtocol03NestedMovementEmptyHex(entry.FourByteHex)), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.HeaderClassification), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.ParserTarget), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.PreAnchorTrailingLeadHex), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.TailAnchorKind), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.PreAnchorBody.Length.ToString(CultureInfo.InvariantCulture)), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.BodyBytes.ToString(CultureInfo.InvariantCulture)), 8),
                    sample.Observation.File,
                    sample.Observation.Line);
            })
            .Where(summary => summary.NonZeroCount > 0 || summary.HeaderLikeCount > 0)
            .OrderBy(summary => summary.Offset);
    }

    public static IEnumerable<Protocol03NestedMovementMode06LongTupleBodyPreAnchorHeaderCandidateSummary> BuildProtocol03NestedMovementMode06LongTupleBodyPreAnchorHeaderCandidateSummaries(
        IEnumerable<PacketDumpFileSummary> dumpSummaries)
    {
        const int targetPrefixBytes = 31;
        const int postPrefixTailOffset = 16 + targetPrefixBytes;
        const int postTupleTailOffset = postPrefixTailOffset + 8;

        var preAnchorEntries = EnumerateProtocol03NestedMovementObservations(dumpSummaries)
            .Where(observation => !observation.IsBoundedPrimaryWrapper)
            .Where(observation => observation.Lead.MarkerModeHex.Equals("06", StringComparison.OrdinalIgnoreCase))
            .Select(observation => new
            {
                Observation = observation,
                Tail = GetProtocol03NestedMovementFullTailByteValues(observation.Lead).ToArray()
            })
            .Where(entry => IsProtocol03NestedMovementMode06FfContinuationLead(entry.Tail))
            .Where(entry => entry.Tail.Length >= postTupleTailOffset)
            .Select(entry =>
            {
                bool postPrefixLooksLikeObjectView = LooksLikeProtocol03ObjectViewHeader(entry.Tail, postPrefixTailOffset);
                string postPrefixDisposition = postPrefixLooksLikeObjectView
                    ? ClassifyProtocol03ObjectView(entry.Tail[postPrefixTailOffset + 2], entry.Tail[postPrefixTailOffset + 3])
                    : "non-object-view post-prefix lead";
                bool tupleIsAcceptedBoundary = postPrefixLooksLikeObjectView &&
                    !postPrefixDisposition.Equals("unclassified object-view update", StringComparison.Ordinal);
                int boundaryAfterTupleOffset = -1;
                bool hasBoundaryAfterTuple = !tupleIsAcceptedBoundary &&
                    TryFindProtocol03ObjectViewHeader(
                        entry.Tail,
                        postTupleTailOffset,
                        entry.Tail.Length,
                        Math.Max(0, entry.Tail.Length - postTupleTailOffset),
                        includeNpcBasePostCreationTailSelectors: false,
                        out boundaryAfterTupleOffset);

                if (tupleIsAcceptedBoundary)
                {
                    return null;
                }

                string sourceDisposition = hasBoundaryAfterTuple
                    ? "bounded before accepted object-view header"
                    : "terminal tail end";
                int bodyEndOffset = hasBoundaryAfterTuple
                    ? boundaryAfterTupleOffset
                    : entry.Tail.Length;
                byte[] body = entry.Tail
                    .Skip(postTupleTailOffset)
                    .Take(Math.Max(0, bodyEndOffset - postTupleTailOffset))
                    .ToArray();
                if (body.Length <= 96)
                {
                    return null;
                }

                string bodySuffixHex = FormatHeader(body.Skip(Math.Max(0, body.Length - 16)).Take(Math.Min(16, body.Length)));
                int? anchorBytes = GetProtocol03Mode06LongTupleBodyTailAnchorBytes(bodySuffixHex);
                if (anchorBytes is null || anchorBytes.Value >= body.Length)
                {
                    return null;
                }

                byte[] preAnchorBody = body.Take(body.Length - anchorBytes.Value).ToArray();
                string preAnchorTrailingLeadHex = preAnchorBody.Length >= 8
                    ? FormatHeader(preAnchorBody.Skip(preAnchorBody.Length - 8).Take(8))
                    : string.Empty;
                string preAnchorPreLeadHex = preAnchorBody.Length >= 16
                    ? FormatHeader(preAnchorBody.Skip(preAnchorBody.Length - 16).Take(8))
                    : string.Empty;

                return new
                {
                    entry.Observation,
                    SourceDisposition = sourceDisposition,
                    TailAnchorKind = ClassifyProtocol03Mode06LongTupleBodyTailAnchor(bodySuffixHex),
                    PreAnchorTrailingLeadHex = preAnchorTrailingLeadHex,
                    PreAnchorPreLeadHex = preAnchorPreLeadHex,
                    BodyBytes = body.Length,
                    PreAnchorBody = preAnchorBody
                };
            })
            .Where(entry => entry is not null)
            .Select(entry => entry!)
            .ToArray();

        var leadWindowCounts = preAnchorEntries
            .GroupBy(entry => (
                entry.SourceDisposition,
                entry.PreAnchorTrailingLeadHex,
                entry.PreAnchorPreLeadHex,
                entry.TailAnchorKind))
            .ToDictionary(
                group => group.Key,
                group => group.Count());

        var headerEntries = preAnchorEntries
            .SelectMany(entry =>
            {
                int[] headerOffsets = Enumerable.Range(0, Math.Max(0, entry.PreAnchorBody.Length - 3))
                    .Where(offset => LooksLikeProtocol03ObjectViewHeader(entry.PreAnchorBody, offset))
                    .ToArray();
                var leadKey = (
                    entry.SourceDisposition,
                    entry.PreAnchorTrailingLeadHex,
                    entry.PreAnchorPreLeadHex,
                    entry.TailAnchorKind);
                var (parserTarget, _) = GetProtocol03NestedMovementMode06LongTupleBodyPreAnchorLeadTarget(
                    entry.TailAnchorKind,
                    leadWindowCounts[leadKey]);

                return headerOffsets.Select((offset, index) =>
                {
                    int? nextHeaderOffset = index + 1 < headerOffsets.Length
                        ? headerOffsets[index + 1]
                        : null;
                    int viewId = ReadUInt16LittleEndian(entry.PreAnchorBody, offset);
                    byte updateCount = entry.PreAnchorBody[offset + 2];
                    byte firstSelector = entry.PreAnchorBody[offset + 3];

                    return new
                    {
                        entry.Observation,
                        entry.SourceDisposition,
                        entry.TailAnchorKind,
                        entry.PreAnchorTrailingLeadHex,
                        entry.BodyBytes,
                        PreAnchorBodyBytes = entry.PreAnchorBody.Length,
                        Offset = offset,
                        ParserTarget = parserTarget,
                        ViewId = viewId,
                        UpdateCountHex = updateCount.ToString("x2", CultureInfo.InvariantCulture),
                        FirstSelectorHex = firstSelector.ToString("x2", CultureInfo.InvariantCulture),
                        HeaderClassification = ClassifyProtocol03ObjectView(updateCount, firstSelector),
                        FourByteWindowHex = FormatHeader(entry.PreAnchorBody.Skip(offset).Take(4)),
                        NextHeaderOffset = nextHeaderOffset,
                        BytesToNextHeader = nextHeaderOffset is null
                            ? (int?)null
                            : nextHeaderOffset.Value - offset,
                        BytesToPreAnchorEnd = entry.PreAnchorBody.Length - offset
                    };
                });
            })
            .ToArray();

        return headerEntries
            .GroupBy(entry => entry.Offset)
            .Select(group =>
            {
                var groupEntries = group.ToArray();
                var sample = groupEntries[0];
                string candidateDisposition = ClassifyProtocol03Mode06PreAnchorHeaderCandidateOffset(
                    group.Key,
                    groupEntries.Length);

                return new Protocol03NestedMovementMode06LongTupleBodyPreAnchorHeaderCandidateSummary(
                    group.Key,
                    candidateDisposition,
                    groupEntries.Length,
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.ViewId.ToString(CultureInfo.InvariantCulture)), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.UpdateCountHex), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.FirstSelectorHex), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.HeaderClassification), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.FourByteWindowHex), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.NextHeaderOffset?.ToString(CultureInfo.InvariantCulture) ?? "-"), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.BytesToNextHeader?.ToString(CultureInfo.InvariantCulture) ?? "-"), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.BytesToPreAnchorEnd.ToString(CultureInfo.InvariantCulture)), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.ParserTarget), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.PreAnchorTrailingLeadHex), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.TailAnchorKind), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.PreAnchorBodyBytes.ToString(CultureInfo.InvariantCulture)), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.BodyBytes.ToString(CultureInfo.InvariantCulture)), 8),
                    sample.Observation.File,
                    sample.Observation.Line);
            })
            .OrderByDescending(summary => summary.WindowCount)
            .ThenBy(summary => summary.Offset);
    }

    public static IEnumerable<Protocol03NestedMovementMode06LongTupleBodyPreAnchorEmbeddedMovementSummary> BuildProtocol03NestedMovementMode06LongTupleBodyPreAnchorEmbeddedMovementSummaries(
        IEnumerable<PacketDumpFileSummary> dumpSummaries)
    {
        const int targetPrefixBytes = 31;
        const int postPrefixTailOffset = 16 + targetPrefixBytes;
        const int postTupleTailOffset = postPrefixTailOffset + 8;

        var preAnchorEntries = EnumerateProtocol03NestedMovementObservations(dumpSummaries)
            .Where(observation => !observation.IsBoundedPrimaryWrapper)
            .Where(observation => observation.Lead.MarkerModeHex.Equals("06", StringComparison.OrdinalIgnoreCase))
            .Select(observation => new
            {
                Observation = observation,
                Tail = GetProtocol03NestedMovementFullTailByteValues(observation.Lead).ToArray()
            })
            .Where(entry => IsProtocol03NestedMovementMode06FfContinuationLead(entry.Tail))
            .Where(entry => entry.Tail.Length >= postTupleTailOffset)
            .Select(entry =>
            {
                bool postPrefixLooksLikeObjectView = LooksLikeProtocol03ObjectViewHeader(entry.Tail, postPrefixTailOffset);
                string postPrefixDisposition = postPrefixLooksLikeObjectView
                    ? ClassifyProtocol03ObjectView(entry.Tail[postPrefixTailOffset + 2], entry.Tail[postPrefixTailOffset + 3])
                    : "non-object-view post-prefix lead";
                bool tupleIsAcceptedBoundary = postPrefixLooksLikeObjectView &&
                    !postPrefixDisposition.Equals("unclassified object-view update", StringComparison.Ordinal);
                int boundaryAfterTupleOffset = -1;
                bool hasBoundaryAfterTuple = !tupleIsAcceptedBoundary &&
                    TryFindProtocol03ObjectViewHeader(
                        entry.Tail,
                        postTupleTailOffset,
                        entry.Tail.Length,
                        Math.Max(0, entry.Tail.Length - postTupleTailOffset),
                        includeNpcBasePostCreationTailSelectors: false,
                        out boundaryAfterTupleOffset);

                if (tupleIsAcceptedBoundary)
                {
                    return null;
                }

                string sourceDisposition = hasBoundaryAfterTuple
                    ? "bounded before accepted object-view header"
                    : "terminal tail end";
                int bodyEndOffset = hasBoundaryAfterTuple
                    ? boundaryAfterTupleOffset
                    : entry.Tail.Length;
                byte[] body = entry.Tail
                    .Skip(postTupleTailOffset)
                    .Take(Math.Max(0, bodyEndOffset - postTupleTailOffset))
                    .ToArray();
                if (body.Length <= 96)
                {
                    return null;
                }

                string bodySuffixHex = FormatHeader(body.Skip(Math.Max(0, body.Length - 16)).Take(Math.Min(16, body.Length)));
                int? anchorBytes = GetProtocol03Mode06LongTupleBodyTailAnchorBytes(bodySuffixHex);
                if (anchorBytes is null || anchorBytes.Value >= body.Length)
                {
                    return null;
                }

                byte[] preAnchorBody = body.Take(body.Length - anchorBytes.Value).ToArray();
                string preAnchorTrailingLeadHex = preAnchorBody.Length >= 8
                    ? FormatHeader(preAnchorBody.Skip(preAnchorBody.Length - 8).Take(8))
                    : string.Empty;
                string preAnchorPreLeadHex = preAnchorBody.Length >= 16
                    ? FormatHeader(preAnchorBody.Skip(preAnchorBody.Length - 16).Take(8))
                    : string.Empty;

                return new
                {
                    entry.Observation,
                    SourceDisposition = sourceDisposition,
                    TailAnchorKind = ClassifyProtocol03Mode06LongTupleBodyTailAnchor(bodySuffixHex),
                    PreAnchorTrailingLeadHex = preAnchorTrailingLeadHex,
                    PreAnchorPreLeadHex = preAnchorPreLeadHex,
                    BodyBytes = body.Length,
                    PreAnchorBody = preAnchorBody
                };
            })
            .Where(entry => entry is not null)
            .Select(entry => entry!)
            .ToArray();

        var leadWindowCounts = preAnchorEntries
            .GroupBy(entry => (
                entry.SourceDisposition,
                entry.PreAnchorTrailingLeadHex,
                entry.PreAnchorPreLeadHex,
                entry.TailAnchorKind))
            .ToDictionary(
                group => group.Key,
                group => group.Count());

        var embeddedEntries = preAnchorEntries
            .SelectMany(entry =>
            {
                var leadKey = (
                    entry.SourceDisposition,
                    entry.PreAnchorTrailingLeadHex,
                    entry.PreAnchorPreLeadHex,
                    entry.TailAnchorKind);
                var (parserTarget, _) = GetProtocol03NestedMovementMode06LongTupleBodyPreAnchorLeadTarget(
                    entry.TailAnchorKind,
                    leadWindowCounts[leadKey]);

                return Enumerable.Range(0, Math.Max(0, entry.PreAnchorBody.Length - 2))
                    .Where(offset => entry.PreAnchorBody[offset] == 0x00 && entry.PreAnchorBody[offset + 1] == 0x06)
                    .Select(offset =>
                    {
                        byte selector = entry.PreAnchorBody[offset + 2];
                        bool supportedSelector = TryGetProtocol03MovementPayloadInfo(selector, out int innerPayloadBytes, out int innerPositionOffset);
                        int positionOffset = supportedSelector
                            ? offset + 3 + innerPositionOffset
                            : -1;
                        bool hasCompletePayload = supportedSelector && offset + 3 + innerPayloadBytes <= entry.PreAnchorBody.Length;
                        float x = 0;
                        float y = 0;
                        float z = 0;
                        bool hasPosition = hasCompletePayload &&
                            TryReadSingleLittleEndian(entry.PreAnchorBody, positionOffset, out x) &&
                            TryReadSingleLittleEndian(entry.PreAnchorBody, positionOffset + 4, out y) &&
                            TryReadSingleLittleEndian(entry.PreAnchorBody, positionOffset + 8, out z) &&
                            IsPlausibleProtocol03Position(x, y, z);
                        string movementDisposition = supportedSelector switch
                        {
                            false => "unsupported embedded mode 06 movement selector",
                            true when !hasCompletePayload => "incomplete embedded mode 06 movement payload",
                            true when !hasPosition => "invalid embedded mode 06 movement position",
                            _ => "decoded embedded mode 06 movement window"
                        };
                        string positionSample = hasPosition
                            ? string.Format(CultureInfo.InvariantCulture, "{0:0.###},{1:0.###},{2:0.###}", x, y, z)
                            : "-";
                        int postMovementOffset = hasCompletePayload
                            ? offset + 3 + innerPayloadBytes
                            : Math.Min(entry.PreAnchorBody.Length, offset + 3);

                        return new
                        {
                            entry.Observation,
                            entry.TailAnchorKind,
                            entry.PreAnchorTrailingLeadHex,
                            entry.BodyBytes,
                            PreAnchorBodyBytes = entry.PreAnchorBody.Length,
                            MarkerOffset = offset,
                            MovementDisposition = movementDisposition,
                            ParserTarget = parserTarget,
                            InnerSelectorHex = selector.ToString("x2", CultureInfo.InvariantCulture),
                            InnerPayloadBytes = supportedSelector
                                ? innerPayloadBytes.ToString(CultureInfo.InvariantCulture)
                                : "-",
                            InnerPositionOffset = supportedSelector
                                ? innerPositionOffset.ToString(CultureInfo.InvariantCulture)
                                : "-",
                            PreAnchorPositionOffset = hasPosition
                                ? positionOffset.ToString(CultureInfo.InvariantCulture)
                                : "-",
                            MarkerWindowHex = FormatHeader(entry.PreAnchorBody.Skip(offset).Take(Math.Min(16, entry.PreAnchorBody.Length - offset))),
                            PositionSample = positionSample,
                            OuterPositionSample = FormatProtocol03NestedMovementPosition(entry.Observation.Lead),
                            PostMovementLeadHex = FormatHeader(entry.PreAnchorBody.Skip(postMovementOffset).Take(Math.Min(8, entry.PreAnchorBody.Length - postMovementOffset)))
                        };
                    });
            })
            .ToArray();

        return embeddedEntries
            .GroupBy(entry => new
            {
                entry.MarkerOffset,
                entry.MovementDisposition
            })
            .Select(group =>
            {
                var groupEntries = group.ToArray();
                var sample = groupEntries[0];

                return new Protocol03NestedMovementMode06LongTupleBodyPreAnchorEmbeddedMovementSummary(
                    group.Key.MarkerOffset,
                    group.Key.MovementDisposition,
                    groupEntries.Length,
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.InnerSelectorHex), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.InnerPayloadBytes), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.InnerPositionOffset), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.PreAnchorPositionOffset), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.MarkerWindowHex), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.PositionSample), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.OuterPositionSample), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => FormatProtocol03NestedMovementEmptyHex(entry.PostMovementLeadHex)), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.ParserTarget), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.PreAnchorTrailingLeadHex), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.TailAnchorKind), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.PreAnchorBodyBytes.ToString(CultureInfo.InvariantCulture)), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.BodyBytes.ToString(CultureInfo.InvariantCulture)), 8),
                    sample.Observation.File,
                    sample.Observation.Line);
            })
            .OrderByDescending(summary => summary.WindowCount)
            .ThenBy(summary => summary.MarkerOffset)
            .ThenBy(summary => summary.MovementDisposition, StringComparer.OrdinalIgnoreCase);
    }

    public static IEnumerable<Protocol03NestedMovementMode06LongTupleBodyPreAnchorEmbeddedMovementTrailerSummary> BuildProtocol03NestedMovementMode06LongTupleBodyPreAnchorEmbeddedMovementTrailerSummaries(
        IEnumerable<PacketDumpFileSummary> dumpSummaries)
    {
        const int targetPrefixBytes = 31;
        const int postPrefixTailOffset = 16 + targetPrefixBytes;
        const int postTupleTailOffset = postPrefixTailOffset + 8;

        var preAnchorEntries = EnumerateProtocol03NestedMovementObservations(dumpSummaries)
            .Where(observation => !observation.IsBoundedPrimaryWrapper)
            .Where(observation => observation.Lead.MarkerModeHex.Equals("06", StringComparison.OrdinalIgnoreCase))
            .Select(observation => new
            {
                Observation = observation,
                Tail = GetProtocol03NestedMovementFullTailByteValues(observation.Lead).ToArray()
            })
            .Where(entry => IsProtocol03NestedMovementMode06FfContinuationLead(entry.Tail))
            .Where(entry => entry.Tail.Length >= postTupleTailOffset)
            .Select(entry =>
            {
                bool postPrefixLooksLikeObjectView = LooksLikeProtocol03ObjectViewHeader(entry.Tail, postPrefixTailOffset);
                string postPrefixDisposition = postPrefixLooksLikeObjectView
                    ? ClassifyProtocol03ObjectView(entry.Tail[postPrefixTailOffset + 2], entry.Tail[postPrefixTailOffset + 3])
                    : "non-object-view post-prefix lead";
                bool tupleIsAcceptedBoundary = postPrefixLooksLikeObjectView &&
                    !postPrefixDisposition.Equals("unclassified object-view update", StringComparison.Ordinal);
                int boundaryAfterTupleOffset = -1;
                bool hasBoundaryAfterTuple = !tupleIsAcceptedBoundary &&
                    TryFindProtocol03ObjectViewHeader(
                        entry.Tail,
                        postTupleTailOffset,
                        entry.Tail.Length,
                        Math.Max(0, entry.Tail.Length - postTupleTailOffset),
                        includeNpcBasePostCreationTailSelectors: false,
                        out boundaryAfterTupleOffset);

                if (tupleIsAcceptedBoundary)
                {
                    return null;
                }

                string sourceDisposition = hasBoundaryAfterTuple
                    ? "bounded before accepted object-view header"
                    : "terminal tail end";
                int bodyEndOffset = hasBoundaryAfterTuple
                    ? boundaryAfterTupleOffset
                    : entry.Tail.Length;
                byte[] body = entry.Tail
                    .Skip(postTupleTailOffset)
                    .Take(Math.Max(0, bodyEndOffset - postTupleTailOffset))
                    .ToArray();
                if (body.Length <= 96)
                {
                    return null;
                }

                string bodySuffixHex = FormatHeader(body.Skip(Math.Max(0, body.Length - 16)).Take(Math.Min(16, body.Length)));
                int? anchorBytes = GetProtocol03Mode06LongTupleBodyTailAnchorBytes(bodySuffixHex);
                if (anchorBytes is null || anchorBytes.Value >= body.Length)
                {
                    return null;
                }

                byte[] preAnchorBody = body.Take(body.Length - anchorBytes.Value).ToArray();
                string preAnchorTrailingLeadHex = preAnchorBody.Length >= 8
                    ? FormatHeader(preAnchorBody.Skip(preAnchorBody.Length - 8).Take(8))
                    : string.Empty;
                string preAnchorPreLeadHex = preAnchorBody.Length >= 16
                    ? FormatHeader(preAnchorBody.Skip(preAnchorBody.Length - 16).Take(8))
                    : string.Empty;

                return new
                {
                    entry.Observation,
                    SourceDisposition = sourceDisposition,
                    TailAnchorKind = ClassifyProtocol03Mode06LongTupleBodyTailAnchor(bodySuffixHex),
                    PreAnchorTrailingLeadHex = preAnchorTrailingLeadHex,
                    PreAnchorPreLeadHex = preAnchorPreLeadHex,
                    BodyBytes = body.Length,
                    PreAnchorBody = preAnchorBody
                };
            })
            .Where(entry => entry is not null)
            .Select(entry => entry!)
            .ToArray();

        var leadWindowCounts = preAnchorEntries
            .GroupBy(entry => (
                entry.SourceDisposition,
                entry.PreAnchorTrailingLeadHex,
                entry.PreAnchorPreLeadHex,
                entry.TailAnchorKind))
            .ToDictionary(
                group => group.Key,
                group => group.Count());

        var trailerEntries = preAnchorEntries
            .SelectMany(entry =>
            {
                var leadKey = (
                    entry.SourceDisposition,
                    entry.PreAnchorTrailingLeadHex,
                    entry.PreAnchorPreLeadHex,
                    entry.TailAnchorKind);
                var (parserTarget, _) = GetProtocol03NestedMovementMode06LongTupleBodyPreAnchorLeadTarget(
                    entry.TailAnchorKind,
                    leadWindowCounts[leadKey]);

                return Enumerable.Range(0, Math.Max(0, entry.PreAnchorBody.Length - 2))
                    .Where(offset => entry.PreAnchorBody[offset] == 0x00 && entry.PreAnchorBody[offset + 1] == 0x06)
                    .Select(offset =>
                    {
                        byte selector = entry.PreAnchorBody[offset + 2];
                        bool supportedSelector = TryGetProtocol03MovementPayloadInfo(selector, out int innerPayloadBytes, out int innerPositionOffset);
                        int positionOffset = supportedSelector
                            ? offset + 3 + innerPositionOffset
                            : -1;
                        bool hasCompletePayload = supportedSelector && offset + 3 + innerPayloadBytes <= entry.PreAnchorBody.Length;
                        float x = 0;
                        float y = 0;
                        float z = 0;
                        bool hasPosition = hasCompletePayload &&
                            TryReadSingleLittleEndian(entry.PreAnchorBody, positionOffset, out x) &&
                            TryReadSingleLittleEndian(entry.PreAnchorBody, positionOffset + 4, out y) &&
                            TryReadSingleLittleEndian(entry.PreAnchorBody, positionOffset + 8, out z) &&
                            IsPlausibleProtocol03Position(x, y, z);
                        string movementDisposition = supportedSelector switch
                        {
                            false => "unsupported embedded mode 06 movement selector",
                            true when !hasCompletePayload => "incomplete embedded mode 06 movement payload",
                            true when !hasPosition => "invalid embedded mode 06 movement position",
                            _ => "decoded embedded mode 06 movement window"
                        };
                        int postMovementOffset = hasCompletePayload
                            ? offset + 3 + innerPayloadBytes
                            : Math.Min(entry.PreAnchorBody.Length, offset + 3);
                        byte[] postMovementBody = entry.PreAnchorBody
                            .Skip(postMovementOffset)
                            .ToArray();
                        string postMovementLeadHex = FormatHeader(postMovementBody.Take(Math.Min(8, postMovementBody.Length)));
                        string postMovementNextLeadHex = FormatHeader(postMovementBody.Skip(8).Take(Math.Min(8, Math.Max(0, postMovementBody.Length - 8))));
                        string postMovementTailLeadHex = FormatHeader(postMovementBody.Skip(Math.Max(0, postMovementBody.Length - 8)).Take(Math.Min(8, postMovementBody.Length)));
                        int preTailFieldOffset = postMovementBody.Length >= 17
                            ? postMovementBody.Length - 17
                            : -1;
                        string preTailFieldHex = preTailFieldOffset >= 0
                            ? FormatHeader(postMovementBody.Skip(preTailFieldOffset).Take(1))
                            : string.Empty;
                        string preAnchorPreLeadHex = postMovementBody.Length >= 16
                            ? FormatHeader(postMovementBody.Skip(postMovementBody.Length - 16).Take(8))
                            : string.Empty;

                        return new
                        {
                            entry.Observation,
                            entry.TailAnchorKind,
                            entry.PreAnchorTrailingLeadHex,
                            entry.BodyBytes,
                            PreAnchorBodyBytes = entry.PreAnchorBody.Length,
                            MarkerOffset = offset,
                            MovementDisposition = movementDisposition,
                            TrailerDisposition = ClassifyProtocol03Mode06EmbeddedMovementTrailer(postMovementLeadHex),
                            ParserTarget = parserTarget,
                            InnerSelectorHex = selector.ToString("x2", CultureInfo.InvariantCulture),
                            PreAnchorPostMovementOffset = postMovementOffset.ToString(CultureInfo.InvariantCulture),
                            BytesRemainingAfterMovement = Math.Max(0, entry.PreAnchorBody.Length - postMovementOffset).ToString(CultureInfo.InvariantCulture),
                            PostMovementLeadHex = postMovementLeadHex,
                            PostMovementNextLeadHex = postMovementNextLeadHex,
                            PostMovementTailLeadHex = postMovementTailLeadHex,
                            PreTailFieldOffset = preTailFieldOffset >= 0
                                ? preTailFieldOffset.ToString(CultureInfo.InvariantCulture)
                                : "-",
                            PreTailFieldHex = preTailFieldHex,
                            PreAnchorPreLeadHex = preAnchorPreLeadHex,
                            PostMovementNonZeroByteOffsets = FormatProtocol03Mode06PreAnchorNonZeroByteOffsets(postMovementBody, 24)
                        };
                    });
            })
            .ToArray();

        return trailerEntries
            .GroupBy(entry => new
            {
                entry.MarkerOffset,
                entry.MovementDisposition,
                entry.TrailerDisposition,
                entry.PostMovementLeadHex
            })
            .Select(group =>
            {
                var groupEntries = group.ToArray();
                var sample = groupEntries[0];

                return new Protocol03NestedMovementMode06LongTupleBodyPreAnchorEmbeddedMovementTrailerSummary(
                    group.Key.MarkerOffset,
                    group.Key.MovementDisposition,
                    group.Key.TrailerDisposition,
                    FormatProtocol03NestedMovementEmptyHex(group.Key.PostMovementLeadHex),
                    groupEntries.Length,
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.InnerSelectorHex), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.PreAnchorPostMovementOffset), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.BytesRemainingAfterMovement), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => FormatProtocol03NestedMovementEmptyHex(entry.PostMovementNextLeadHex)), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => FormatProtocol03NestedMovementEmptyHex(entry.PostMovementTailLeadHex)), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.PreTailFieldOffset), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => FormatProtocol03NestedMovementEmptyHex(entry.PreTailFieldHex)), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => FormatProtocol03NestedMovementEmptyHex(entry.PreAnchorPreLeadHex)), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.PostMovementNonZeroByteOffsets), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.ParserTarget), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.PreAnchorTrailingLeadHex), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.TailAnchorKind), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.PreAnchorBodyBytes.ToString(CultureInfo.InvariantCulture)), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.BodyBytes.ToString(CultureInfo.InvariantCulture)), 8),
                    sample.Observation.File,
                    sample.Observation.Line);
            })
            .OrderByDescending(summary => summary.WindowCount)
            .ThenBy(summary => summary.MarkerOffset)
            .ThenBy(summary => summary.TrailerDisposition, StringComparer.OrdinalIgnoreCase)
            .ThenBy(summary => summary.PostMovementLeadHex, StringComparer.OrdinalIgnoreCase);
    }

    public static IEnumerable<Protocol03NestedMovementMode06LongTupleBodyPreAnchorEmbeddedMovementTrailerShapeSummary> BuildProtocol03NestedMovementMode06LongTupleBodyPreAnchorEmbeddedMovementTrailerShapeSummaries(
        IEnumerable<PacketDumpFileSummary> dumpSummaries)
    {
        const int targetPrefixBytes = 31;
        const int postPrefixTailOffset = 16 + targetPrefixBytes;
        const int postTupleTailOffset = postPrefixTailOffset + 8;

        var preAnchorEntries = EnumerateProtocol03NestedMovementObservations(dumpSummaries)
            .Where(observation => !observation.IsBoundedPrimaryWrapper)
            .Where(observation => observation.Lead.MarkerModeHex.Equals("06", StringComparison.OrdinalIgnoreCase))
            .Select(observation => new
            {
                Observation = observation,
                Tail = GetProtocol03NestedMovementFullTailByteValues(observation.Lead).ToArray()
            })
            .Where(entry => IsProtocol03NestedMovementMode06FfContinuationLead(entry.Tail))
            .Where(entry => entry.Tail.Length >= postTupleTailOffset)
            .Select(entry =>
            {
                bool postPrefixLooksLikeObjectView = LooksLikeProtocol03ObjectViewHeader(entry.Tail, postPrefixTailOffset);
                string postPrefixDisposition = postPrefixLooksLikeObjectView
                    ? ClassifyProtocol03ObjectView(entry.Tail[postPrefixTailOffset + 2], entry.Tail[postPrefixTailOffset + 3])
                    : "non-object-view post-prefix lead";
                bool tupleIsAcceptedBoundary = postPrefixLooksLikeObjectView &&
                    !postPrefixDisposition.Equals("unclassified object-view update", StringComparison.Ordinal);
                int boundaryAfterTupleOffset = -1;
                bool hasBoundaryAfterTuple = !tupleIsAcceptedBoundary &&
                    TryFindProtocol03ObjectViewHeader(
                        entry.Tail,
                        postTupleTailOffset,
                        entry.Tail.Length,
                        Math.Max(0, entry.Tail.Length - postTupleTailOffset),
                        includeNpcBasePostCreationTailSelectors: false,
                        out boundaryAfterTupleOffset);

                if (tupleIsAcceptedBoundary)
                {
                    return null;
                }

                string sourceDisposition = hasBoundaryAfterTuple
                    ? "bounded before accepted object-view header"
                    : "terminal tail end";
                int bodyEndOffset = hasBoundaryAfterTuple
                    ? boundaryAfterTupleOffset
                    : entry.Tail.Length;
                byte[] body = entry.Tail
                    .Skip(postTupleTailOffset)
                    .Take(Math.Max(0, bodyEndOffset - postTupleTailOffset))
                    .ToArray();
                if (body.Length <= 96)
                {
                    return null;
                }

                string bodySuffixHex = FormatHeader(body.Skip(Math.Max(0, body.Length - 16)).Take(Math.Min(16, body.Length)));
                int? anchorBytes = GetProtocol03Mode06LongTupleBodyTailAnchorBytes(bodySuffixHex);
                if (anchorBytes is null || anchorBytes.Value >= body.Length)
                {
                    return null;
                }

                byte[] preAnchorBody = body.Take(body.Length - anchorBytes.Value).ToArray();
                string preAnchorTrailingLeadHex = preAnchorBody.Length >= 8
                    ? FormatHeader(preAnchorBody.Skip(preAnchorBody.Length - 8).Take(8))
                    : string.Empty;
                string preAnchorPreLeadHex = preAnchorBody.Length >= 16
                    ? FormatHeader(preAnchorBody.Skip(preAnchorBody.Length - 16).Take(8))
                    : string.Empty;

                return new
                {
                    entry.Observation,
                    SourceDisposition = sourceDisposition,
                    TailAnchorKind = ClassifyProtocol03Mode06LongTupleBodyTailAnchor(bodySuffixHex),
                    PreAnchorTrailingLeadHex = preAnchorTrailingLeadHex,
                    PreAnchorPreLeadHex = preAnchorPreLeadHex,
                    BodyBytes = body.Length,
                    PreAnchorBody = preAnchorBody
                };
            })
            .Where(entry => entry is not null)
            .Select(entry => entry!)
            .ToArray();

        var leadWindowCounts = preAnchorEntries
            .GroupBy(entry => (
                entry.SourceDisposition,
                entry.PreAnchorTrailingLeadHex,
                entry.PreAnchorPreLeadHex,
                entry.TailAnchorKind))
            .ToDictionary(
                group => group.Key,
                group => group.Count());

        var trailerEntries = preAnchorEntries
            .SelectMany(entry =>
            {
                var leadKey = (
                    entry.SourceDisposition,
                    entry.PreAnchorTrailingLeadHex,
                    entry.PreAnchorPreLeadHex,
                    entry.TailAnchorKind);
                var (parserTarget, _) = GetProtocol03NestedMovementMode06LongTupleBodyPreAnchorLeadTarget(
                    entry.TailAnchorKind,
                    leadWindowCounts[leadKey]);

                return Enumerable.Range(0, Math.Max(0, entry.PreAnchorBody.Length - 2))
                    .Where(offset => entry.PreAnchorBody[offset] == 0x00 && entry.PreAnchorBody[offset + 1] == 0x06)
                    .Select(offset =>
                    {
                        byte selector = entry.PreAnchorBody[offset + 2];
                        bool supportedSelector = TryGetProtocol03MovementPayloadInfo(selector, out int innerPayloadBytes, out int innerPositionOffset);
                        int positionOffset = supportedSelector
                            ? offset + 3 + innerPositionOffset
                            : -1;
                        bool hasCompletePayload = supportedSelector && offset + 3 + innerPayloadBytes <= entry.PreAnchorBody.Length;
                        float x = 0;
                        float y = 0;
                        float z = 0;
                        bool hasPosition = hasCompletePayload &&
                            TryReadSingleLittleEndian(entry.PreAnchorBody, positionOffset, out x) &&
                            TryReadSingleLittleEndian(entry.PreAnchorBody, positionOffset + 4, out y) &&
                            TryReadSingleLittleEndian(entry.PreAnchorBody, positionOffset + 8, out z) &&
                            IsPlausibleProtocol03Position(x, y, z);
                        string movementDisposition = supportedSelector switch
                        {
                            false => "unsupported embedded mode 06 movement selector",
                            true when !hasCompletePayload => "incomplete embedded mode 06 movement payload",
                            true when !hasPosition => "invalid embedded mode 06 movement position",
                            _ => "decoded embedded mode 06 movement window"
                        };
                        int postMovementOffset = hasCompletePayload
                            ? offset + 3 + innerPayloadBytes
                            : Math.Min(entry.PreAnchorBody.Length, offset + 3);
                        byte[] postMovementBody = entry.PreAnchorBody
                            .Skip(postMovementOffset)
                            .ToArray();
                        int bytesRemainingAfterMovement = Math.Max(0, entry.PreAnchorBody.Length - postMovementOffset);
                        string postMovementLeadHex = FormatHeader(postMovementBody.Take(Math.Min(8, postMovementBody.Length)));
                        string postMovementNextLeadHex = FormatHeader(postMovementBody.Skip(8).Take(Math.Min(8, Math.Max(0, postMovementBody.Length - 8))));
                        int preTailFieldOffset = postMovementBody.Length >= 17
                            ? postMovementBody.Length - 17
                            : -1;
                        string preTailFieldHex = preTailFieldOffset >= 0
                            ? FormatHeader(postMovementBody.Skip(preTailFieldOffset).Take(1))
                            : string.Empty;
                        string preAnchorPreLeadHex = postMovementBody.Length >= 16
                            ? FormatHeader(postMovementBody.Skip(postMovementBody.Length - 16).Take(8))
                            : string.Empty;

                        return new
                        {
                            entry.Observation,
                            entry.TailAnchorKind,
                            entry.PreAnchorTrailingLeadHex,
                            entry.BodyBytes,
                            PreAnchorBodyBytes = entry.PreAnchorBody.Length,
                            MarkerOffset = offset,
                            MovementDisposition = movementDisposition,
                            TrailerDisposition = ClassifyProtocol03Mode06EmbeddedMovementTrailer(postMovementLeadHex),
                            ParserTarget = parserTarget,
                            InnerSelectorHex = selector.ToString("x2", CultureInfo.InvariantCulture),
                            PreAnchorPostMovementOffset = postMovementOffset.ToString(CultureInfo.InvariantCulture),
                            BytesRemainingAfterMovement = bytesRemainingAfterMovement,
                            PostMovementLeadHex = postMovementLeadHex,
                            PostMovementNextLeadHex = postMovementNextLeadHex,
                            PreTailFieldOffset = preTailFieldOffset >= 0 ? preTailFieldOffset : (int?)null,
                            PreTailFieldHex = preTailFieldHex,
                            PreAnchorPreLeadHex = preAnchorPreLeadHex,
                            PostMovementNonZeroByteOffsets = FormatProtocol03Mode06PreAnchorNonZeroByteOffsets(postMovementBody, 24)
                        };
                    });
            })
            .ToArray();

        return trailerEntries
            .GroupBy(entry => new
            {
                entry.MarkerOffset,
                entry.MovementDisposition,
                entry.TrailerDisposition,
                entry.PostMovementLeadHex,
                entry.PostMovementNextLeadHex,
                entry.BytesRemainingAfterMovement,
                entry.PreTailFieldOffset,
                entry.PreTailFieldHex,
                entry.PreAnchorPreLeadHex,
                entry.PreAnchorTrailingLeadHex,
                entry.TailAnchorKind
            })
            .Select(group =>
            {
                var groupEntries = group.ToArray();
                var sample = groupEntries[0];
                string shapeDisposition = ClassifyProtocol03Mode06EmbeddedMovementTrailerShape(
                    group.Key.MovementDisposition,
                    group.Key.TrailerDisposition,
                    groupEntries.Length);

                return new Protocol03NestedMovementMode06LongTupleBodyPreAnchorEmbeddedMovementTrailerShapeSummary(
                    group.Key.MarkerOffset,
                    shapeDisposition,
                    group.Key.MovementDisposition,
                    group.Key.TrailerDisposition,
                    FormatProtocol03NestedMovementEmptyHex(group.Key.PostMovementLeadHex),
                    FormatProtocol03NestedMovementEmptyHex(group.Key.PostMovementNextLeadHex),
                    group.Key.BytesRemainingAfterMovement,
                    group.Key.PreTailFieldOffset,
                    FormatProtocol03NestedMovementEmptyHex(group.Key.PreTailFieldHex),
                    FormatProtocol03NestedMovementEmptyHex(group.Key.PreAnchorPreLeadHex),
                    FormatProtocol03NestedMovementEmptyHex(group.Key.PreAnchorTrailingLeadHex),
                    group.Key.TailAnchorKind,
                    groupEntries.Length,
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.InnerSelectorHex), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.PreAnchorPostMovementOffset), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.ParserTarget), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.PreAnchorBodyBytes.ToString(CultureInfo.InvariantCulture)), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.BodyBytes.ToString(CultureInfo.InvariantCulture)), 8),
                    CountProtocol03NestedMovementValues(groupEntries.Select(entry => entry.PostMovementNonZeroByteOffsets), 8),
                    sample.Observation.File,
                    sample.Observation.Line);
            })
            .OrderBy(summary => GetProtocol03Mode06EmbeddedMovementTrailerShapeOrder(summary.ShapeDisposition))
            .ThenByDescending(summary => summary.WindowCount)
            .ThenBy(summary => summary.MarkerOffset)
            .ThenBy(summary => summary.PreTailFieldOffset ?? int.MaxValue)
            .ThenBy(summary => summary.PreTailFieldHex, StringComparer.OrdinalIgnoreCase)
            .ThenBy(summary => summary.PreAnchorTrailingLeadHex, StringComparer.OrdinalIgnoreCase);
    }

    private static string ClassifyProtocol03Mode06EmbeddedMovementTrailerShape(
        string movementDisposition,
        string trailerDisposition,
        int windowCount)
    {
        if (!movementDisposition.Equals("decoded embedded mode 06 movement window", StringComparison.Ordinal))
        {
            return "unsupported selector trailer control";
        }

        if (trailerDisposition.Equals("shared ff/10 post-movement continuation lead", StringComparison.Ordinal))
        {
            return windowCount > 1
                ? "repeated decoded shared trailer shape"
                : "singleton decoded shared trailer shape";
        }

        return windowCount > 1
            ? "repeated decoded non-shared trailer shape"
            : "singleton decoded non-shared trailer shape";
    }

    private static int GetProtocol03Mode06EmbeddedMovementTrailerShapeOrder(string shapeDisposition)
    {
        return shapeDisposition switch
        {
            "repeated decoded shared trailer shape" => 0,
            "singleton decoded shared trailer shape" => 1,
            "repeated decoded non-shared trailer shape" => 2,
            "singleton decoded non-shared trailer shape" => 3,
            "unsupported selector trailer control" => 4,
            _ => 5
        };
    }

    private static string ClassifyProtocol03Mode06EmbeddedMovementTrailer(string postMovementLeadHex)
    {
        if (string.IsNullOrWhiteSpace(postMovementLeadHex))
        {
            return "no post-movement bytes";
        }

        if (postMovementLeadHex.Equals("ff 00 00 00 10 00 00 00", StringComparison.OrdinalIgnoreCase))
        {
            return "shared ff/10 post-movement continuation lead";
        }

        if (postMovementLeadHex.StartsWith("ff 00 00 00", StringComparison.OrdinalIgnoreCase))
        {
            return "ff-prefixed post-movement continuation lead";
        }

        return "non-shared post-movement lead";
    }

    public static IEnumerable<Protocol03NestedMovementMode06LongTupleBodyPreAnchorLeadSummary> BuildProtocol03NestedMovementMode06LongTupleBodyPreAnchorLeadSummaries(
        IEnumerable<Protocol03NestedMovementMode06LongTupleBodyTailAnchorSummary> summaries)
    {
        var entries = summaries
            .SelectMany(summary => summary.PreAnchorTrailingLeads
                .Where(entry => !entry.Key.Equals("-", StringComparison.Ordinal))
                .Select(entry => new
                {
                    Summary = summary,
                    PreAnchorTrailingLeadHex = entry.Key,
                    PreAnchorPreLeadHex = GetProtocol03NestedMovementDominantNonEmptyKey(summary.PreAnchorPreLeadBytes),
                    TailAnchorKind = GetProtocol03NestedMovementDominantNonEmptyKey(summary.TailAnchorKinds),
                    WindowCount = entry.Value
                }))
            .ToArray();

        return entries
            .GroupBy(entry => new
            {
                entry.Summary.SourceDisposition,
                entry.PreAnchorTrailingLeadHex,
                entry.PreAnchorPreLeadHex,
                entry.TailAnchorKind
            })
            .Select(group =>
            {
                var groupEntries = group.ToArray();
                Protocol03NestedMovementMode06LongTupleBodyTailAnchorSummary sample = groupEntries[0].Summary;
                int windowCount = groupEntries.Sum(entry => entry.WindowCount);
                (string parserTarget, string actionReason) = GetProtocol03NestedMovementMode06LongTupleBodyPreAnchorLeadTarget(
                    group.Key.TailAnchorKind,
                    windowCount);

                return new Protocol03NestedMovementMode06LongTupleBodyPreAnchorLeadSummary(
                    parserTarget,
                    actionReason,
                    group.Key.SourceDisposition,
                    group.Key.PreAnchorTrailingLeadHex,
                    group.Key.PreAnchorPreLeadHex,
                    group.Key.TailAnchorKind,
                    groupEntries.Length,
                    windowCount,
                    CountProtocol03NestedMovementWeightedValues(groupEntries.Select(entry => new WeightedValue(FormatProtocol03NestedMovementEmptyHex(entry.Summary.PrefixFirstNonZeroFieldHex), entry.WindowCount)), 8),
                    CountProtocol03NestedMovementWeightedValues(groupEntries.Select(entry => new WeightedValue(entry.Summary.TupleLayoutKind, entry.WindowCount)), 8),
                    CountProtocol03NestedMovementWeightedValues(groupEntries.Select(entry => new WeightedValue(entry.Summary.PostPrefixDisposition, entry.WindowCount)), 8),
                    CountProtocol03NestedMovementWeightedValues(groupEntries.Select(entry => new WeightedValue(entry.Summary.BodyBytes.ToString(CultureInfo.InvariantCulture), entry.WindowCount)), 8),
                    CountProtocol03NestedMovementWeightedValues(groupEntries.SelectMany(entry => entry.Summary.PreAnchorBodyBytes.Select(value => new WeightedValue(value.Key, value.Value))), 8),
                    CountProtocol03NestedMovementWeightedValues(groupEntries.SelectMany(entry => entry.Summary.InferredTrailingBodyBytes.Select(value => new WeightedValue(value.Key, value.Value))), 8),
                    CountProtocol03NestedMovementWeightedValues(groupEntries.SelectMany(entry => entry.Summary.PreAnchorFirstNonZeroOffsets.Select(value => new WeightedValue(value.Key, value.Value))), 8),
                    CountProtocol03NestedMovementWeightedValues(groupEntries.SelectMany(entry => entry.Summary.PreAnchorFirstNonZeroFields.Select(value => new WeightedValue(value.Key, value.Value))), 8),
                    CountProtocol03NestedMovementWeightedValues(groupEntries.SelectMany(entry => entry.Summary.PostPrefixLeads.Select(value => new WeightedValue(value.Key, value.Value))), 8),
                    CountProtocol03NestedMovementWeightedValues(groupEntries.SelectMany(entry => entry.Summary.MarkerBytes.Select(value => new WeightedValue(value.Key, value.Value))), 8),
                    CountProtocol03NestedMovementWeightedValues(groupEntries.SelectMany(entry => entry.Summary.HeaderClassifications.Select(value => new WeightedValue(value.Key, value.Value))), 8),
                    CountProtocol03NestedMovementWeightedValues(groupEntries.SelectMany(entry => entry.Summary.HeaderPrefixes.Select(value => new WeightedValue(value.Key, value.Value))), 8),
                    CountProtocol03NestedMovementWeightedValues(groupEntries.SelectMany(entry => entry.Summary.BodySuffixes.Select(value => new WeightedValue(value.Key, value.Value))), 8),
                    CountProtocol03NestedMovementWeightedValues(groupEntries.SelectMany(entry => entry.Summary.ObjectClassifications.Select(value => new WeightedValue(value.Key, value.Value))), 8),
                    CountProtocol03NestedMovementWeightedValues(groupEntries.SelectMany(entry => entry.Summary.PayloadBytes.Select(value => new WeightedValue(value.Key, value.Value))), 8),
                    CountProtocol03NestedMovementWeightedValues(groupEntries.SelectMany(entry => entry.Summary.PositionSamples.Select(value => new WeightedValue(value.Key, value.Value))), 8),
                    sample.SampleFile,
                    sample.SampleLine);
            })
            .OrderBy(summary => GetProtocol03NestedMovementMode06LongTupleBodyPreAnchorLeadTargetOrder(summary.ParserTarget))
            .ThenByDescending(summary => summary.WindowCount)
            .ThenBy(summary => summary.PreAnchorTrailingLeadHex, StringComparer.OrdinalIgnoreCase)
            .ThenBy(summary => summary.SourceDisposition, StringComparer.OrdinalIgnoreCase);
    }

    private static (string ParserTarget, string ActionReason) GetProtocol03NestedMovementMode06LongTupleBodyPreAnchorLeadTarget(
        string tailAnchorKind,
        int windowCount)
    {
        bool repeated = windowCount >= 2;
        if (repeated &&
            tailAnchorKind.Equals("trailing terminal-96 state suffix", StringComparison.Ordinal))
        {
            return (
                "candidate mode 06 stacked pre-anchor lead before terminal body",
                "repeated pre-anchor lead before inferred terminal 96-byte tuple body");
        }

        if (repeated &&
            tailAnchorKind.Equals("trailing guarded 57-byte object-view suffix", StringComparison.Ordinal))
        {
            return (
                "candidate mode 06 stacked pre-anchor lead before guarded object-view body",
                "repeated pre-anchor lead before inferred guarded 57-byte object-view tuple body");
        }

        if (repeated &&
            tailAnchorKind.Equals("trailing guarded 94-byte state suffix", StringComparison.Ordinal))
        {
            return (
                "candidate mode 06 stacked pre-anchor lead before guarded state body",
                "repeated pre-anchor lead before inferred guarded 94-byte state tuple body");
        }

        return (
            "hold mode 06 pre-anchor lead as evidence only",
            "singleton pre-anchor lead or no repeated known trailing-body family");
    }

    private static int GetProtocol03NestedMovementMode06LongTupleBodyPreAnchorLeadTargetOrder(string parserTarget)
    {
        return parserTarget switch
        {
            "candidate mode 06 stacked pre-anchor lead before terminal body" => 0,
            "candidate mode 06 stacked pre-anchor lead before guarded object-view body" => 1,
            "candidate mode 06 stacked pre-anchor lead before guarded state body" => 2,
            "hold mode 06 pre-anchor lead as evidence only" => 3,
            _ => 10
        };
    }

    private static string ClassifyProtocol03Mode06PreAnchorHeaderCandidateOffset(int offset, int windowCount)
    {
        if (windowCount == 15 &&
            offset is 70 or 74 or 78 or 79 or 94)
        {
            return "recurring overlapping pre-anchor header-like collision";
        }

        if (offset >= 150)
        {
            return "trailing-lead overlap collision";
        }

        if (windowCount >= 5)
        {
            return "repeated pre-anchor header-like collision";
        }

        return "singleton pre-anchor header-like collision";
    }

    private static string GetProtocol03NestedMovementDominantNonEmptyKey(IReadOnlyDictionary<string, int> values)
    {
        return values
            .Where(entry => !entry.Key.Equals("-", StringComparison.Ordinal))
            .OrderByDescending(entry => entry.Value)
            .ThenBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
            .Select(entry => entry.Key)
            .FirstOrDefault() ?? "-";
    }

    private static (string Action, string Reason) GetProtocol03NestedMovementMode06TupleBodyParserAction(
        Protocol03NestedMovementMode06PostContinuationTupleBodySummary summary)
    {
        bool isDominantTail = summary.TailPrefixKind.Equals("ff marker plus zero-padded ff continuation lead", StringComparison.Ordinal);
        bool hasSharedBodyAnchor = summary.FirstNonZeroOffset == 6 &&
            summary.FirstNonZeroFieldHex.Equals("ff 00", StringComparison.OrdinalIgnoreCase);

        if (isDominantTail &&
            hasSharedBodyAnchor &&
            summary.TupleLayoutKind.Equals("zero-update marker tuple", StringComparison.Ordinal) &&
            summary.PostPrefixDisposition.Equals("non-object-view post-prefix lead", StringComparison.Ordinal) &&
            summary.PostTupleBodyBytes == 70)
        {
            return ("test consuming mode 06 zero-update tuple body", "dominant 70-byte body before accepted state-marker boundary");
        }

        if (isDominantTail &&
            hasSharedBodyAnchor &&
            summary.TupleLayoutKind.Equals("zero-update marker tuple", StringComparison.Ordinal) &&
            summary.PostPrefixDisposition.Equals("non-object-view post-prefix lead", StringComparison.Ordinal) &&
            summary.PostTupleBodyBytes == 94 &&
            IsProtocol03Mode06GuardedZeroUpdate94ByteBodySuffix(summary.BodySuffixHex))
        {
            return ("test guarded mode 06 94-byte zero-update tuple body", "low-count 94-byte zero-update body with shared terminal suffix before accepted object boundary");
        }

        if (isDominantTail &&
            hasSharedBodyAnchor &&
            IsProtocol03Mode06Guarded57ByteMarker24And25ObjectViewPrefixAndLead(summary.PrefixFirstNonZeroFieldHex, summary.PostPrefixLeads, summary.WindowCount) &&
            summary.TupleLayoutKind.Equals("unclassified object-view tuple", StringComparison.Ordinal) &&
            summary.PostPrefixDisposition.Equals("unclassified object-view update", StringComparison.Ordinal) &&
            summary.PostTupleBodyBytes == 57 &&
            IsProtocol03Mode06Guarded57ByteMarker24And25ObjectViewBodySuffix(summary.BodySuffixHex) &&
            HasOnlyProtocol03NestedMovementSummaryKeys(
                summary.HeaderPrefixes,
                summary.WindowCount,
                "28 ff 01 00 00 00 00 00",
                "28 ff 02 00 00 00 00 00"))
        {
            return ("test guarded mode 06 57-byte marker-24-25 object-view tuple body", "paired 57-byte marker-24/25 object-view body before 28 ff state-marker boundary");
        }

        if (isDominantTail &&
            hasSharedBodyAnchor &&
            IsProtocol03Mode06Guarded57ByteMarker24And26ObjectViewPrefixAndLead(summary.PrefixFirstNonZeroFieldHex, summary.PostPrefixLeads, summary.WindowCount) &&
            summary.TupleLayoutKind.Equals("unclassified object-view tuple", StringComparison.Ordinal) &&
            summary.PostPrefixDisposition.Equals("unclassified object-view update", StringComparison.Ordinal) &&
            summary.PostTupleBodyBytes == 57 &&
            IsProtocol03Mode06Guarded57ByteMarker24And26ObjectViewBodySuffix(summary.BodySuffixHex) &&
            HasOnlyProtocol03NestedMovementSummaryKeys(
                summary.HeaderPrefixes,
                summary.WindowCount,
                "28 ff 01 00 00 00 00 00",
                "28 ff 02 00 00 00 00 00"))
        {
            return ("test guarded mode 06 57-byte marker-24-26 object-view tuple body", "paired 57-byte marker-24/26 object-view body before 28 ff state-marker boundary");
        }

        if (isDominantTail &&
            hasSharedBodyAnchor &&
            IsProtocol03Mode06Guarded57ByteMarker29And2aObjectViewPrefixAndLead(summary.PrefixFirstNonZeroFieldHex, summary.PostPrefixLeads, summary.WindowCount) &&
            summary.TupleLayoutKind.Equals("unclassified object-view tuple", StringComparison.Ordinal) &&
            summary.PostPrefixDisposition.Equals("unclassified object-view update", StringComparison.Ordinal) &&
            summary.PostTupleBodyBytes == 57 &&
            IsProtocol03Mode06Guarded57ByteMarker29And2aObjectViewBodySuffix(summary.BodySuffixHex) &&
            HasOnlyProtocol03NestedMovementSummaryKeys(
                summary.HeaderPrefixes,
                summary.WindowCount,
                "28 ff 01 00 00 00 00 00",
                "28 ff 02 00 00 00 00 00"))
        {
            return ("test guarded mode 06 57-byte marker-29-2a object-view tuple body", "paired 57-byte marker-29/2a object-view body before 28 ff state-marker boundary");
        }

        if (isDominantTail &&
            hasSharedBodyAnchor &&
            IsProtocol03Mode06Guarded57ByteMarker24AltObjectViewPrefixAndLead(summary.PrefixFirstNonZeroFieldHex, summary.PostPrefixLeads, summary.WindowCount) &&
            summary.TupleLayoutKind.Equals("unclassified object-view tuple", StringComparison.Ordinal) &&
            summary.PostPrefixDisposition.Equals("unclassified object-view update", StringComparison.Ordinal) &&
            summary.PostTupleBodyBytes == 57 &&
            IsProtocol03Mode06Guarded57ByteMarker24AltObjectViewBodySuffix(summary.BodySuffixHex) &&
            HasOnlyProtocol03NestedMovementSummaryKeys(
                summary.HeaderPrefixes,
                summary.WindowCount,
                "28 ff 01 00 00 00 00 00",
                "28 ff 02 00 00 00 00 00"))
        {
            return ("test guarded mode 06 57-byte marker-24-alt object-view tuple body", "alternate 57-byte marker-24 object-view body before 28 ff state-marker boundary");
        }

        if (isDominantTail &&
            hasSharedBodyAnchor &&
            IsProtocol03Mode06Guarded57ByteMarker1eAnd1fObjectViewPrefixAndLead(summary.PrefixFirstNonZeroFieldHex, summary.PostPrefixLeads, summary.WindowCount) &&
            summary.TupleLayoutKind.Equals("unclassified object-view tuple", StringComparison.Ordinal) &&
            summary.PostPrefixDisposition.Equals("unclassified object-view update", StringComparison.Ordinal) &&
            summary.PostTupleBodyBytes == 57 &&
            IsProtocol03Mode06Guarded57ByteMarker1eAnd1fObjectViewBodySuffix(summary.BodySuffixHex) &&
            HasOnlyProtocol03NestedMovementSummaryKeys(
                summary.HeaderPrefixes,
                summary.WindowCount,
                "28 ff 01 00 00 00 00 00",
                "28 ff 02 00 00 00 00 00"))
        {
            return ("test guarded mode 06 57-byte marker-1e-1f object-view tuple body", "paired 57-byte marker-1e/1f object-view body before 28 ff state-marker boundary");
        }

        if (isDominantTail &&
            hasSharedBodyAnchor &&
            summary.TupleLayoutKind.Equals("unclassified object-view tuple", StringComparison.Ordinal) &&
            summary.PostPrefixDisposition.Equals("unclassified object-view update", StringComparison.Ordinal) &&
            summary.PostTupleBodyBytes == 57 &&
            IsProtocol03Mode06Guarded57ByteObjectViewBodySuffix(summary.BodySuffixHex))
        {
            return ("test guarded mode 06 57-byte object-view tuple body", "bounded 57-byte object-view tuple body with shared terminal state-marker suffix");
        }

        if (isDominantTail &&
            hasSharedBodyAnchor &&
            summary.PrefixFirstNonZeroFieldHex.Equals("03 ff", StringComparison.OrdinalIgnoreCase) &&
            summary.TupleLayoutKind.Equals("unclassified object-view tuple", StringComparison.Ordinal) &&
            summary.PostPrefixDisposition.Equals("unclassified object-view update", StringComparison.Ordinal) &&
            summary.PostTupleBodyBytes == 94 &&
            HasProtocol03NestedMovementSummaryFullCount(summary.PostPrefixLeads, "00 46 05 00 00 00 00 34", summary.WindowCount) &&
            IsProtocol03Mode06Guarded94ByteMarker34ObjectViewBodySuffix(summary.BodySuffixHex))
        {
            return ("test guarded mode 06 94-byte marker-34 object-view tuple body", "bounded 94-byte marker-34 object-view body with shared terminal state suffix");
        }

        if (isDominantTail &&
            hasSharedBodyAnchor &&
            summary.TupleLayoutKind.Equals("unclassified object-view tuple", StringComparison.Ordinal) &&
            summary.PostPrefixDisposition.Equals("unclassified object-view update", StringComparison.Ordinal) &&
            summary.PostTupleBodyBytes == 94 &&
            IsProtocol03Mode06Guarded94ByteObjectViewBodySuffix(summary.BodySuffixHex))
        {
            return ("test guarded mode 06 94-byte object-view tuple body", "bounded 94-byte object-view tuple body with shared terminal state-marker suffix");
        }

        if (isDominantTail &&
            hasSharedBodyAnchor &&
            summary.PrefixFirstNonZeroFieldHex.Equals("01 ff", StringComparison.OrdinalIgnoreCase) &&
            summary.TupleLayoutKind.Equals("unclassified object-view tuple", StringComparison.Ordinal) &&
            summary.PostPrefixDisposition.Equals("unclassified object-view update", StringComparison.Ordinal) &&
            summary.PostTupleBodyBytes == 70 &&
            IsProtocol03Mode06Guarded70ByteObjectViewBodySuffix(summary.BodySuffixHex) &&
            HasProtocol03NestedMovementSummaryFullCount(summary.PostPrefixLeads, "00 98 08 00 00 00 00 2a", summary.WindowCount) &&
            HasProtocol03NestedMovementSummaryFullCount(summary.HeaderPrefixes, "00 68 01 00 4e 37 08 00", summary.WindowCount))
        {
            return ("test guarded mode 06 70-byte object-view tuple body", "bounded 70-byte object-view tuple body with shared post-prefix and boundary header");
        }

        if (isDominantTail &&
            hasSharedBodyAnchor &&
            IsProtocol03Mode06Guarded57ByteMarker16And17ObjectViewPrefixAndLead(summary.PrefixFirstNonZeroFieldHex, summary.PostPrefixLeads, summary.WindowCount) &&
            summary.TupleLayoutKind.Equals("unclassified object-view tuple", StringComparison.Ordinal) &&
            summary.PostPrefixDisposition.Equals("unclassified object-view update", StringComparison.Ordinal) &&
            summary.PostTupleBodyBytes == 57 &&
            IsProtocol03Mode06Guarded57ByteMarker16And17ObjectViewBodySuffix(summary.BodySuffixHex) &&
            HasOnlyProtocol03NestedMovementSummaryKeys(
                summary.HeaderPrefixes,
                summary.WindowCount,
                "28 ff 01 00 00 00 00 00",
                "28 ff 02 00 00 00 00 00"))
        {
            return ("test guarded mode 06 57-byte marker-16-17 object-view tuple body", "paired 57-byte marker-16/17 object-view body before 28 ff state-marker boundary");
        }

        if (isDominantTail &&
            hasSharedBodyAnchor &&
            IsProtocol03Mode06Guarded57ByteMarker16ObjectViewPrefixAndLead(summary.PrefixFirstNonZeroFieldHex, summary.PostPrefixLeads, summary.WindowCount) &&
            summary.TupleLayoutKind.Equals("unclassified object-view tuple", StringComparison.Ordinal) &&
            summary.PostPrefixDisposition.Equals("unclassified object-view update", StringComparison.Ordinal) &&
            summary.PostTupleBodyBytes == 57 &&
            IsProtocol03Mode06Guarded57ByteMarker16ObjectViewBodySuffix(summary.BodySuffixHex) &&
            HasOnlyProtocol03NestedMovementSummaryKeys(
                summary.HeaderPrefixes,
                summary.WindowCount,
                "28 ff 01 00 00 00 00 00",
                "28 ff 02 00 00 00 00 00"))
        {
            return ("test guarded mode 06 57-byte marker-16 object-view tuple body", "paired 57-byte marker-16 object-view body before 28 ff state-marker boundary");
        }

        if (isDominantTail &&
            hasSharedBodyAnchor &&
            IsProtocol03Mode06Guarded57ByteMarker15ObjectViewPrefixAndLead(summary.PrefixFirstNonZeroFieldHex, summary.PostPrefixLeads, summary.WindowCount) &&
            summary.TupleLayoutKind.Equals("unclassified object-view tuple", StringComparison.Ordinal) &&
            summary.PostPrefixDisposition.Equals("unclassified object-view update", StringComparison.Ordinal) &&
            summary.PostTupleBodyBytes == 57 &&
            IsProtocol03Mode06Guarded57ByteMarker15ObjectViewBodySuffix(summary.BodySuffixHex) &&
            HasProtocol03NestedMovementSummaryFullCount(summary.HeaderPrefixes, "28 ff 01 00 00 00 00 00", summary.WindowCount))
        {
            return ("test guarded mode 06 57-byte marker-15 object-view tuple body", "57-byte marker-15 object-view body before 28 ff 01 state-marker boundary");
        }

        if (isDominantTail &&
            hasSharedBodyAnchor &&
            IsProtocol03Mode06Guarded57ByteMarker19ObjectViewPrefixAndLead(summary.PrefixFirstNonZeroFieldHex, summary.PostPrefixLeads, summary.WindowCount) &&
            summary.TupleLayoutKind.Equals("unclassified object-view tuple", StringComparison.Ordinal) &&
            summary.PostPrefixDisposition.Equals("unclassified object-view update", StringComparison.Ordinal) &&
            summary.PostTupleBodyBytes == 57 &&
            IsProtocol03Mode06Guarded57ByteMarker19ObjectViewBodySuffix(summary.BodySuffixHex) &&
            HasOnlyProtocol03NestedMovementSummaryKeys(
                summary.HeaderPrefixes,
                summary.WindowCount,
                "28 ff 01 00 00 00 00 00",
                "28 ff 02 00 00 00 00 00"))
        {
            return ("test guarded mode 06 57-byte marker-19 object-view tuple body", "57-byte marker-19 object-view body before 28 ff state-marker boundary");
        }

        if (isDominantTail &&
            hasSharedBodyAnchor &&
            IsProtocol03Mode06Guarded57ByteMarker19AltObjectViewPrefixAndLead(summary.PrefixFirstNonZeroFieldHex, summary.PostPrefixLeads, summary.WindowCount) &&
            summary.TupleLayoutKind.Equals("unclassified object-view tuple", StringComparison.Ordinal) &&
            summary.PostPrefixDisposition.Equals("unclassified object-view update", StringComparison.Ordinal) &&
            summary.PostTupleBodyBytes == 57 &&
            IsProtocol03Mode06Guarded57ByteMarker19AltObjectViewBodySuffix(summary.BodySuffixHex) &&
            HasProtocol03NestedMovementSummaryFullCount(summary.HeaderPrefixes, "28 ff 01 00 00 00 00 00", summary.WindowCount))
        {
            return ("test guarded mode 06 57-byte marker-19-alt object-view tuple body", "alternate 57-byte marker-19 object-view body before 28 ff 01 state-marker boundary");
        }

        if (isDominantTail &&
            hasSharedBodyAnchor &&
            IsProtocol03Mode06Guarded57ByteMarker1fObjectViewPrefixAndLead(summary.PrefixFirstNonZeroFieldHex, summary.PostPrefixLeads, summary.WindowCount) &&
            summary.TupleLayoutKind.Equals("unclassified object-view tuple", StringComparison.Ordinal) &&
            summary.PostPrefixDisposition.Equals("unclassified object-view update", StringComparison.Ordinal) &&
            summary.PostTupleBodyBytes == 57 &&
            IsProtocol03Mode06Guarded57ByteMarker1fObjectViewBodySuffix(summary.BodySuffixHex) &&
            HasOnlyProtocol03NestedMovementSummaryKeys(
                summary.HeaderPrefixes,
                summary.WindowCount,
                "28 ff 01 00 00 00 00 00",
                "28 ff 02 00 00 00 00 00"))
        {
            return ("test guarded mode 06 57-byte marker-1f object-view tuple body", "57-byte marker-1f object-view body before 28 ff state-marker boundary");
        }

        if (isDominantTail &&
            hasSharedBodyAnchor &&
            IsProtocol03Mode06Guarded57ByteMarker24ObjectViewPrefixAndLead(summary.PrefixFirstNonZeroFieldHex, summary.PostPrefixLeads, summary.WindowCount) &&
            summary.TupleLayoutKind.Equals("unclassified object-view tuple", StringComparison.Ordinal) &&
            summary.PostPrefixDisposition.Equals("unclassified object-view update", StringComparison.Ordinal) &&
            summary.PostTupleBodyBytes == 57 &&
            IsProtocol03Mode06Guarded57ByteMarker24ObjectViewBodySuffix(summary.BodySuffixHex) &&
            HasOnlyProtocol03NestedMovementSummaryKeys(
                summary.HeaderPrefixes,
                summary.WindowCount,
                "28 ff 01 00 00 00 00 00",
                "28 ff 02 00 00 00 00 00"))
        {
            return ("test guarded mode 06 57-byte marker-24 object-view tuple body", "57-byte marker-24 object-view body before 28 ff state-marker boundary");
        }

        if (isDominantTail &&
            hasSharedBodyAnchor &&
            IsProtocol03Mode06Guarded57ByteMarker33ObjectViewPrefixAndLead(summary.PrefixFirstNonZeroFieldHex, summary.PostPrefixLeads, summary.WindowCount) &&
            summary.TupleLayoutKind.Equals("unclassified object-view tuple", StringComparison.Ordinal) &&
            summary.PostPrefixDisposition.Equals("unclassified object-view update", StringComparison.Ordinal) &&
            summary.PostTupleBodyBytes == 57 &&
            IsProtocol03Mode06Guarded57ByteMarker33ObjectViewBodySuffix(summary.BodySuffixHex) &&
            HasOnlyProtocol03NestedMovementSummaryKeys(
                summary.HeaderPrefixes,
                summary.WindowCount,
                "28 ff 01 00 00 00 00 00",
                "28 ff 02 00 00 00 00 00"))
        {
            return ("test guarded mode 06 57-byte marker-33 object-view tuple body", "57-byte marker-33 object-view body before 28 ff state-marker boundary");
        }

        if (isDominantTail &&
            hasSharedBodyAnchor &&
            IsProtocol03Mode06Guarded57ByteMarker13ObjectViewPrefixAndLead(summary.PrefixFirstNonZeroFieldHex, summary.PostPrefixLeads, summary.WindowCount) &&
            summary.TupleLayoutKind.Equals("unclassified object-view tuple", StringComparison.Ordinal) &&
            summary.PostPrefixDisposition.Equals("unclassified object-view update", StringComparison.Ordinal) &&
            summary.PostTupleBodyBytes == 57 &&
            IsProtocol03Mode06Guarded57ByteMarker13ObjectViewBodySuffix(summary.BodySuffixHex) &&
            HasProtocol03NestedMovementSummaryFullCount(summary.HeaderPrefixes, "28 ff 02 00 00 00 00 00", summary.WindowCount))
        {
            return ("test guarded mode 06 57-byte marker-13 object-view tuple body", "57-byte marker-13 object-view body before 28 ff 02 state-marker boundary");
        }

        if (isDominantTail &&
            hasSharedBodyAnchor &&
            IsProtocol03Mode06Guarded57ByteMarker1dAnd1eObjectViewPrefixAndLead(summary.PrefixFirstNonZeroFieldHex, summary.PostPrefixLeads, summary.WindowCount) &&
            summary.TupleLayoutKind.Equals("unclassified object-view tuple", StringComparison.Ordinal) &&
            summary.PostPrefixDisposition.Equals("unclassified object-view update", StringComparison.Ordinal) &&
            summary.PostTupleBodyBytes == 57 &&
            IsProtocol03Mode06Guarded57ByteMarker1dAnd1eObjectViewBodySuffix(summary.BodySuffixHex) &&
            HasProtocol03NestedMovementSummaryFullCount(summary.HeaderPrefixes, "28 ff 01 00 00 00 00 00", summary.WindowCount))
        {
            return ("test guarded mode 06 57-byte marker-1d-1e object-view tuple body", "paired 57-byte marker-1d/1e object-view body before 28 ff 01 state-marker boundary");
        }

        if (isDominantTail &&
            hasSharedBodyAnchor &&
            IsProtocol03Mode06Guarded57ByteMarker1eObjectViewPrefixAndLead(summary.PrefixFirstNonZeroFieldHex, summary.PostPrefixLeads, summary.WindowCount) &&
            summary.TupleLayoutKind.Equals("unclassified object-view tuple", StringComparison.Ordinal) &&
            summary.PostPrefixDisposition.Equals("unclassified object-view update", StringComparison.Ordinal) &&
            summary.PostTupleBodyBytes == 57 &&
            IsProtocol03Mode06Guarded57ByteMarker1eObjectViewBodySuffix(summary.BodySuffixHex) &&
            HasProtocol03NestedMovementSummaryFullCount(summary.HeaderPrefixes, "28 ff 01 00 00 00 00 00", summary.WindowCount))
        {
            return ("test guarded mode 06 57-byte marker-1e object-view tuple body", "57-byte marker-1e object-view body before 28 ff 01 state-marker boundary");
        }

        if (isDominantTail &&
            hasSharedBodyAnchor &&
            IsProtocol03Mode06Guarded57ByteMarker1dObjectViewPrefixAndLead(summary.PrefixFirstNonZeroFieldHex, summary.PostPrefixLeads, summary.WindowCount) &&
            summary.TupleLayoutKind.Equals("unclassified object-view tuple", StringComparison.Ordinal) &&
            summary.PostPrefixDisposition.Equals("unclassified object-view update", StringComparison.Ordinal) &&
            summary.PostTupleBodyBytes == 57 &&
            IsProtocol03Mode06Guarded57ByteMarker1dObjectViewBodySuffix(summary.BodySuffixHex) &&
            HasProtocol03NestedMovementSummaryFullCount(summary.HeaderPrefixes, "28 ff 01 00 00 00 00 00", summary.WindowCount))
        {
            return ("test guarded mode 06 57-byte marker-1d object-view tuple body", "57-byte marker-1d object-view body before 28 ff 01 state-marker boundary");
        }

        if (isDominantTail &&
            hasSharedBodyAnchor &&
            IsProtocol03Mode06Guarded57ByteMarker25ObjectViewPrefixAndLead(summary.PrefixFirstNonZeroFieldHex, summary.PostPrefixLeads, summary.WindowCount) &&
            summary.TupleLayoutKind.Equals("unclassified object-view tuple", StringComparison.Ordinal) &&
            summary.PostPrefixDisposition.Equals("unclassified object-view update", StringComparison.Ordinal) &&
            summary.PostTupleBodyBytes == 57 &&
            IsProtocol03Mode06Guarded57ByteMarker25ObjectViewBodySuffix(summary.BodySuffixHex) &&
            HasOnlyProtocol03NestedMovementSummaryKeys(
                summary.HeaderPrefixes,
                summary.WindowCount,
                "28 ff 01 00 00 00 00 00",
                "28 ff 02 00 00 00 00 00"))
        {
            return ("test guarded mode 06 57-byte marker-25 object-view tuple body", "57-byte marker-25 object-view body before 28 ff state-marker boundary");
        }

        if (isDominantTail &&
            hasSharedBodyAnchor &&
            IsProtocol03Mode06Guarded57ByteMarker1dB9ObjectViewPrefixAndLead(summary.PrefixFirstNonZeroFieldHex, summary.PostPrefixLeads, summary.WindowCount) &&
            summary.TupleLayoutKind.Equals("unclassified object-view tuple", StringComparison.Ordinal) &&
            summary.PostPrefixDisposition.Equals("unclassified object-view update", StringComparison.Ordinal) &&
            summary.PostTupleBodyBytes == 57 &&
            IsProtocol03Mode06Guarded57ByteMarker1dB9ObjectViewBodySuffix(summary.BodySuffixHex) &&
            HasProtocol03NestedMovementSummaryFullCount(summary.HeaderPrefixes, "28 ff 02 00 00 00 00 00", summary.WindowCount))
        {
            return ("test guarded mode 06 57-byte marker-1d-b9 object-view tuple body", "57-byte marker-1d-b9 object-view body before 28 ff 02 state-marker boundary");
        }

        if (isDominantTail &&
            hasSharedBodyAnchor &&
            IsProtocol03Mode06Guarded57ByteMarker15EcObjectViewPrefixAndLead(summary.PrefixFirstNonZeroFieldHex, summary.PostPrefixLeads, summary.WindowCount) &&
            summary.TupleLayoutKind.Equals("unclassified object-view tuple", StringComparison.Ordinal) &&
            summary.PostPrefixDisposition.Equals("unclassified object-view update", StringComparison.Ordinal) &&
            summary.PostTupleBodyBytes == 57 &&
            IsProtocol03Mode06Guarded57ByteMarker15EcObjectViewBodySuffix(summary.BodySuffixHex) &&
            HasProtocol03NestedMovementSummaryFullCount(summary.HeaderPrefixes, "28 ff 01 00 00 00 00 00", summary.WindowCount))
        {
            return ("test guarded mode 06 57-byte marker-15-ec object-view tuple body", "57-byte marker-15-ec object-view body before 28 ff 01 state-marker boundary");
        }

        if (isDominantTail &&
            hasSharedBodyAnchor &&
            summary.PrefixFirstNonZeroFieldHex is "02 ff" or "03 ff" &&
            summary.TupleLayoutKind.Equals("unclassified object-view tuple", StringComparison.Ordinal) &&
            summary.PostPrefixDisposition.Equals("unclassified object-view update", StringComparison.Ordinal) &&
            summary.PostTupleBodyBytes == 224 &&
            HasOnlyProtocol03NestedMovementSummaryKeys(
                summary.PostPrefixLeads,
                summary.WindowCount,
                "00 cf 03 00 00 00 00 25",
                "00 b6 05 00 00 00 00 25") &&
            summary.BodySuffixHex.Equals("ff 00 00 00 00 00 00 00 00 d4 1e 00 00 4c 0a 00", StringComparison.OrdinalIgnoreCase) &&
            HasProtocol03NestedMovementSummaryFullCount(summary.InnerSelectors, "0e", summary.WindowCount) &&
            HasProtocol03NestedMovementSummaryFullCount(summary.HeaderPrefixes, "28 ff 01 00 00 00 00 00", summary.WindowCount))
        {
            return ("test guarded mode 06 long embedded marker-25 trailer body", "repeated long body with decoded embedded movement trailer before 57-byte object-view suffix");
        }

        if (isDominantTail &&
            hasSharedBodyAnchor &&
            summary.TupleLayoutKind.Equals("unclassified object-view tuple", StringComparison.Ordinal) &&
            summary.PostPrefixDisposition.Equals("unclassified object-view update", StringComparison.Ordinal) &&
            summary.PostTupleBodyBytes is 57 or 70 or 94)
        {
            return ("test guarded mode 06 unclassified tuple body", "bounded 57/70/94-byte unclassified body needs semantic guard");
        }

        if (isDominantTail &&
            hasSharedBodyAnchor &&
            summary.PrefixFirstNonZeroFieldHex.Equals("02 ff", StringComparison.OrdinalIgnoreCase) &&
            summary.TupleLayoutKind.Equals("unclassified object-view tuple", StringComparison.Ordinal) &&
            summary.PostPrefixDisposition.Equals("unclassified object-view update", StringComparison.Ordinal) &&
            summary.PostTupleBodyBytes == 53 &&
            HasProtocol03NestedMovementSummaryFullCount(summary.PostPrefixLeads, "00 e9 07 00 00 00 00 34", summary.WindowCount) &&
            IsProtocol03Mode06Guarded53ByteMarker34ObjectViewBodySuffix(summary.BodySuffixHex) &&
            HasProtocol03NestedMovementSummaryFullCount(summary.HeaderPrefixes, "00 b8 01 00 28 ff 15 00", summary.WindowCount))
        {
            return ("test guarded mode 06 53-byte marker-34-b8-15 object-view tuple body", "53-byte marker-34 object-view body before 00 b8 28 ff 15 state-marker boundary");
        }

        if (isDominantTail &&
            hasSharedBodyAnchor &&
            summary.PrefixFirstNonZeroFieldHex.Equals("02 ff", StringComparison.OrdinalIgnoreCase) &&
            summary.TupleLayoutKind.Equals("unclassified object-view tuple", StringComparison.Ordinal) &&
            summary.PostPrefixDisposition.Equals("unclassified object-view update", StringComparison.Ordinal) &&
            summary.PostTupleBodyBytes == 53 &&
            HasProtocol03NestedMovementSummaryFullCount(summary.PostPrefixLeads, "00 e9 07 00 00 00 00 34", summary.WindowCount) &&
            IsProtocol03Mode06Guarded53ByteMarker34ObjectViewBodySuffix(summary.BodySuffixHex) &&
            HasProtocol03NestedMovementSummaryFullCount(summary.HeaderPrefixes, "00 62 01 00 28 ff 0f 00", summary.WindowCount))
        {
            return ("test guarded mode 06 53-byte marker-34-62-0f object-view tuple body", "53-byte marker-34 object-view body before 00 62 28 ff 0f state-marker boundary");
        }

        if (isDominantTail &&
            hasSharedBodyAnchor &&
            summary.PrefixFirstNonZeroFieldHex.Equals("02 ff", StringComparison.OrdinalIgnoreCase) &&
            summary.TupleLayoutKind.Equals("unclassified object-view tuple", StringComparison.Ordinal) &&
            summary.PostPrefixDisposition.Equals("unclassified object-view update", StringComparison.Ordinal) &&
            summary.PostTupleBodyBytes == 53 &&
            HasProtocol03NestedMovementSummaryFullCount(summary.PostPrefixLeads, "00 e9 07 00 00 00 00 34", summary.WindowCount) &&
            IsProtocol03Mode06Guarded53ByteMarker34ObjectViewBodySuffix(summary.BodySuffixHex) &&
            HasProtocol03NestedMovementSummaryFullCount(summary.HeaderPrefixes, "00 b8 01 00 28 ff 1c 00", summary.WindowCount))
        {
            return ("test guarded mode 06 53-byte marker-34-b8-1c object-view tuple body", "53-byte marker-34 object-view body before 00 b8 28 ff 1c state-marker boundary");
        }

        if (isDominantTail &&
            hasSharedBodyAnchor &&
            summary.PrefixFirstNonZeroFieldHex.Equals("02 ff", StringComparison.OrdinalIgnoreCase) &&
            summary.TupleLayoutKind.Equals("unclassified object-view tuple", StringComparison.Ordinal) &&
            summary.PostPrefixDisposition.Equals("unclassified object-view update", StringComparison.Ordinal) &&
            summary.PostTupleBodyBytes == 53 &&
            HasProtocol03NestedMovementSummaryFullCount(summary.PostPrefixLeads, "00 e9 07 00 00 00 00 34", summary.WindowCount) &&
            HasProtocol03NestedMovementSummaryFullCount(summary.HeaderClassifications, "single state marker update", summary.WindowCount))
        {
            return ("test guarded mode 06 unclassified 53-byte tuple body", "low-count 53-byte unclassified body before accepted state-marker boundary");
        }

        if (isDominantTail &&
            hasSharedBodyAnchor &&
            summary.PrefixFirstNonZeroFieldHex.Equals("02 ff", StringComparison.OrdinalIgnoreCase) &&
            summary.TupleLayoutKind.Equals("unclassified object-view tuple", StringComparison.Ordinal) &&
            summary.PostPrefixDisposition.Equals("unclassified object-view update", StringComparison.Ordinal) &&
            summary.PostTupleBodyBytes > 94 &&
            HasProtocol03NestedMovementSummaryFullCount(summary.PostPrefixLeads, "00 e9 07 00 00 00 00 34", summary.WindowCount))
        {
            return ("hold mode 06 marker-34 long object-view tuple body as evidence only", "variable-length marker-34 object-view body needs body-length rule");
        }

        if (hasSharedBodyAnchor &&
            summary.TailPrefixKind.Equals("ff marker-prefixed tail", StringComparison.Ordinal) &&
            summary.PrefixFirstNonZeroFieldHex.Equals("01 ff", StringComparison.OrdinalIgnoreCase) &&
            summary.TupleLayoutKind.Equals("zero-update marker tuple", StringComparison.Ordinal) &&
            summary.PostPrefixDisposition.Equals("non-object-view post-prefix lead", StringComparison.Ordinal) &&
            summary.PostTupleBodyBytes == 57 &&
            HasProtocol03NestedMovementSummaryFullCount(summary.PostPrefixLeads, "03 fa 00 00 00 00 00 03", summary.WindowCount) &&
            HasProtocol03NestedMovementSummaryFullCount(summary.HeaderClassifications, "single state marker update", summary.WindowCount) &&
            HasProtocol03NestedMovementSummaryFullCount(summary.HeaderPrefixes, "28 ff 01 00 00 00 00 00", summary.WindowCount))
        {
            return ("test guarded mode 06 non-dominant zero-update tuple body", "non-dominant 57-byte zero-update body before shared 28 ff state-marker boundary");
        }

        if (hasSharedBodyAnchor &&
            summary.TailPrefixKind.Equals("ff marker-prefixed tail", StringComparison.Ordinal) &&
            summary.PrefixFirstNonZeroFieldHex.Equals("03 ff", StringComparison.OrdinalIgnoreCase) &&
            summary.TupleLayoutKind.Equals("zero-update marker tuple", StringComparison.Ordinal) &&
            summary.PostPrefixDisposition.Equals("non-object-view post-prefix lead", StringComparison.Ordinal) &&
            summary.PostTupleBodyBytes == 49 &&
            HasProtocol03NestedMovementSummaryFullCount(summary.PostPrefixLeads, "03 7d 00 00 00 00 00 03", summary.WindowCount) &&
            HasProtocol03NestedMovementSummaryFullCount(summary.HeaderClassifications, "state marker/object update candidate", summary.WindowCount) &&
            HasProtocol03NestedMovementSummaryFullCount(summary.HeaderPrefixes, "00 bc 02 00 00 00 00 00", summary.WindowCount))
        {
            return ("test guarded mode 06 non-dominant 49-byte zero-update tuple body", "non-dominant 49-byte zero-update body before shared 00 bc state-marker boundary");
        }

        return ("hold mode 06 tuple body as evidence only", "low-count or non-dominant tuple body shape");
    }

    private static (string Action, string Reason) GetProtocol03NestedMovementMode06TerminalTupleBodyParserAction(
        Protocol03NestedMovementMode06PostContinuationTerminalTupleBodySummary summary)
    {
        bool isDominantTail = summary.TailPrefixKind.Equals("ff marker plus zero-padded ff continuation lead", StringComparison.Ordinal);
        bool hasSharedBodyAnchor =
            HasProtocol03NestedMovementSummaryFullCount(summary.FirstNonZeroOffsets, "6", summary.WindowCount) &&
            HasProtocol03NestedMovementSummaryFullCount(summary.FirstNonZeroFields, "ff 00", summary.WindowCount);
        bool hasAcceptedTerminalTupleLayout =
            (summary.TupleLayoutKind.Equals("unclassified object-view tuple", StringComparison.Ordinal) &&
                summary.PostPrefixDisposition.Equals("unclassified object-view update", StringComparison.Ordinal)) ||
            (summary.TupleLayoutKind.Equals("zero-update marker tuple", StringComparison.Ordinal) &&
                summary.PostPrefixDisposition.Equals("non-object-view post-prefix lead", StringComparison.Ordinal));

        if (isDominantTail &&
            hasSharedBodyAnchor &&
            hasAcceptedTerminalTupleLayout &&
            summary.TerminalPostTupleBodyBytes == 96 &&
            HasProtocol03NestedMovementSummaryFullCount(
                summary.BodyPrefixes,
                "00 00 00 00 00 00 ff 00 00 00 00 00 00 00 00 00",
                summary.WindowCount) &&
            HasOnlyProtocol03Mode06Terminal96ByteBodySuffixes(summary.BodySuffixes, summary.WindowCount))
        {
            return ("test terminal mode 06 96-byte tuple body", "terminal 96-byte body with shared state-marker suffix");
        }

        if (isDominantTail &&
            hasSharedBodyAnchor &&
            summary.TerminalPostTupleBodyBytes > 96 &&
            HasOnlyProtocol03Mode06Terminal96ByteBodySuffixes(summary.BodySuffixes, summary.WindowCount) &&
            HasProtocol03NestedMovementSummaryFullCount(summary.PreAnchorFirstNonZeroOffsets, "6", summary.WindowCount) &&
            HasProtocol03NestedMovementSummaryFullCount(summary.PreAnchorFirstNonZeroFields, "ff 00", summary.WindowCount) &&
            HasProtocol03NestedMovementSummaryFullCount(summary.PreTailFieldBytes, "02", summary.WindowCount) &&
            HasProtocol03NestedMovementSummaryFullCount(summary.PreAnchorPreLeadBytes, "ff cd cc 4c be 00 00 00", summary.WindowCount) &&
            HasProtocol03NestedMovementSummaryFullCount(summary.PreAnchorTrailingLeads, "00 e9 07 00 00 00 00 34", summary.WindowCount) &&
            HasProtocol03NestedMovementSummaryFullCount(summary.PostPrefixLeads, "00 e9 07 00 00 00 00 34", summary.WindowCount) &&
            HasOnlyProtocol03NestedMovementSummaryKeys(summary.InnerSelectors, summary.WindowCount, "0e", "0c"))
        {
            return ("test terminal mode 06 long embedded marker-34 trailer body", "terminal long body with decoded embedded movement trailer before 96-byte state suffix");
        }

        if (isDominantTail &&
            hasSharedBodyAnchor &&
            summary.TerminalPostTupleBodyBytes > 96 &&
            HasOnlyProtocol03Mode06Terminal96ByteBodySuffixes(summary.BodySuffixes, summary.WindowCount) &&
            HasProtocol03NestedMovementSummaryFullCount(summary.PreAnchorFirstNonZeroOffsets, "6", summary.WindowCount) &&
            HasProtocol03NestedMovementSummaryFullCount(summary.PreAnchorFirstNonZeroFields, "ff 00", summary.WindowCount) &&
            HasProtocol03NestedMovementSummaryFullCount(summary.PreTailFieldBytes, "03", summary.WindowCount) &&
            HasProtocol03NestedMovementSummaryFullCount(summary.PreAnchorPreLeadBytes, "ff 00 00 00 00 00 00 00", summary.WindowCount) &&
            HasProtocol03NestedMovementSummaryFullCount(summary.PreAnchorTrailingLeads, "00 96 00 00 00 00 00 04", summary.WindowCount) &&
            HasOnlyProtocol03NestedMovementSummaryKeys(summary.InnerSelectors, summary.WindowCount, "0e", "08"))
        {
            return ("test terminal mode 06 long embedded marker-04 trailer body", "terminal long body with decoded embedded movement trailer before 96-byte state suffix");
        }

        if (isDominantTail &&
            hasSharedBodyAnchor &&
            summary.TerminalPostTupleBodyBytes > 96 &&
            HasOnlyProtocol03Mode06Terminal96ByteBodySuffixes(summary.BodySuffixes, summary.WindowCount) &&
            HasProtocol03NestedMovementSummaryFullCount(summary.PreAnchorFirstNonZeroOffsets, "6", summary.WindowCount) &&
            HasProtocol03NestedMovementSummaryFullCount(summary.PreAnchorFirstNonZeroFields, "ff 00", summary.WindowCount) &&
            HasProtocol03NestedMovementSummaryFullCount(summary.PreTailFieldBytes, "03", summary.WindowCount) &&
            HasProtocol03NestedMovementSummaryFullCount(summary.PreAnchorPreLeadBytes, "ff 00 00 00 00 00 00 00", summary.WindowCount) &&
            HasProtocol03NestedMovementSummaryFullCount(summary.PreAnchorTrailingLeads, "00 b6 03 00 00 00 00 24", summary.WindowCount) &&
            HasProtocol03NestedMovementSummaryFullCount(summary.PostPrefixLeads, "00 b6 03 00 00 00 00 24", summary.WindowCount) &&
            HasOnlyProtocol03NestedMovementSummaryKeys(summary.InnerSelectors, summary.WindowCount, "0e", "0c"))
        {
            return ("test terminal mode 06 long embedded marker-24 trailer body", "terminal long body with decoded embedded movement trailer before 96-byte state suffix");
        }

        if (isDominantTail &&
            hasSharedBodyAnchor &&
            summary.TerminalPostTupleBodyBytes > 96 &&
            HasOnlyProtocol03Mode06Terminal96ByteBodySuffixes(summary.BodySuffixes, summary.WindowCount) &&
            HasProtocol03NestedMovementSummaryFullCount(summary.PreAnchorFirstNonZeroOffsets, "6", summary.WindowCount) &&
            HasProtocol03NestedMovementSummaryFullCount(summary.PreAnchorFirstNonZeroFields, "ff 00", summary.WindowCount) &&
            HasProtocol03NestedMovementSummaryFullCount(summary.PreTailFieldBytes, "03", summary.WindowCount) &&
            HasProtocol03NestedMovementSummaryFullCount(summary.PreAnchorPreLeadBytes, "ff 00 00 00 00 00 00 00", summary.WindowCount) &&
            HasProtocol03NestedMovementSummaryFullCount(summary.PreAnchorTrailingLeads, "00 4c 04 00 00 00 00 2a", summary.WindowCount) &&
            HasProtocol03NestedMovementSummaryFullCount(summary.PostPrefixLeads, "00 4c 04 00 00 00 00 2a", summary.WindowCount) &&
            HasOnlyProtocol03NestedMovementSummaryKeys(summary.InnerSelectors, summary.WindowCount, "0e", "0c"))
        {
            return ("test terminal mode 06 long embedded marker-2a trailer body", "terminal long body with decoded embedded movement trailer before 96-byte state suffix");
        }

        return ("hold mode 06 tuple body as evidence only", "low-count or non-dominant tuple body shape");
    }

    private static bool HasProtocol03NestedMovementSummaryFullCount(
        IReadOnlyDictionary<string, int> values,
        string key,
        int expectedCount)
    {
        return values.TryGetValue(key, out int count) && count == expectedCount;
    }

    private static bool HasOnlyProtocol03NestedMovementSummaryKeys(
        IReadOnlyDictionary<string, int> values,
        int expectedCount,
        params string[] allowedKeys)
    {
        HashSet<string> allowed = allowedKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return values.Count > 0 &&
            values.Keys.All(allowed.Contains) &&
            values.Values.Sum() == expectedCount;
    }

    private static bool HasOnlyProtocol03Mode06Terminal96ByteBodySuffixes(
        IReadOnlyDictionary<string, int> values,
        int expectedCount)
    {
        return values.Count > 0 &&
            values.Values.Sum() == expectedCount &&
            values.Keys.All(value => !value.Equals("-", StringComparison.Ordinal) &&
                IsProtocol03Mode06Terminal96ByteTupleBodySuffix(value));
    }

    private static bool IsProtocol03Mode06GuardedZeroUpdate94ByteBodySuffix(string bodySuffixHex)
    {
        return bodySuffixHex.Equals("00 1f 07 08 00 00 00 00 00 00 22 00 00 00 00 00", StringComparison.OrdinalIgnoreCase) ||
            bodySuffixHex.Equals("00 1f 12 08 00 00 00 00 00 00 22 00 00 00 00 00", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsProtocol03Mode06Guarded57ByteObjectViewBodySuffix(string bodySuffixHex)
    {
        return bodySuffixHex.Equals("ff 00 00 00 00 00 00 00 00 d4 1e 00 00 4c 0a 00", StringComparison.OrdinalIgnoreCase) ||
            bodySuffixHex.Equals("ff 00 00 00 00 00 00 00 00 c1 1e 00 00 4c 0a 00", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsProtocol03Mode06Guarded57ByteMarker24And25ObjectViewPrefixAndLead(
        string prefixFirstNonZeroFieldHex,
        IReadOnlyDictionary<string, int> postPrefixLeads,
        int expectedCount)
    {
        return prefixFirstNonZeroFieldHex.Equals("03 ff", StringComparison.OrdinalIgnoreCase) &&
            HasOnlyProtocol03NestedMovementSummaryKeys(
                postPrefixLeads,
                expectedCount,
                "00 b6 03 00 00 00 00 24",
                "00 cf 03 00 00 00 00 25");
    }

    private static bool IsProtocol03Mode06Guarded57ByteMarker24And25ObjectViewPrefixAndLead(
        string prefixFirstNonZeroFieldHex,
        string postPrefixLeadHex)
    {
        return prefixFirstNonZeroFieldHex.Equals("03 ff", StringComparison.OrdinalIgnoreCase) &&
            (postPrefixLeadHex.Equals("00 b6 03 00 00 00 00 24", StringComparison.OrdinalIgnoreCase) ||
                postPrefixLeadHex.Equals("00 cf 03 00 00 00 00 25", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsProtocol03Mode06Guarded57ByteMarker24And25ObjectViewBodySuffix(string bodySuffixHex)
    {
        return bodySuffixHex.Equals("ff 00 00 00 00 00 00 00 00 d4 1e 00 00 4c 0a 00", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsProtocol03Mode06Guarded57ByteMarker24And26ObjectViewPrefixAndLead(
        string prefixFirstNonZeroFieldHex,
        IReadOnlyDictionary<string, int> postPrefixLeads,
        int expectedCount)
    {
        return prefixFirstNonZeroFieldHex.Equals("03 ff", StringComparison.OrdinalIgnoreCase) &&
            HasOnlyProtocol03NestedMovementSummaryKeys(
                postPrefixLeads,
                expectedCount,
                "00 b6 03 00 00 00 00 24",
                "00 e8 03 00 00 00 00 26");
    }

    private static bool IsProtocol03Mode06Guarded57ByteMarker24And26ObjectViewPrefixAndLead(
        string prefixFirstNonZeroFieldHex,
        string postPrefixLeadHex)
    {
        return prefixFirstNonZeroFieldHex.Equals("03 ff", StringComparison.OrdinalIgnoreCase) &&
            (postPrefixLeadHex.Equals("00 b6 03 00 00 00 00 24", StringComparison.OrdinalIgnoreCase) ||
                postPrefixLeadHex.Equals("00 e8 03 00 00 00 00 26", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsProtocol03Mode06Guarded57ByteMarker24And26ObjectViewBodySuffix(string bodySuffixHex)
    {
        return bodySuffixHex.Equals("ff 00 00 00 00 00 00 00 00 c2 1e 00 00 4c 0a 00", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsProtocol03Mode06Guarded57ByteMarker29And2aObjectViewPrefixAndLead(
        string prefixFirstNonZeroFieldHex,
        IReadOnlyDictionary<string, int> postPrefixLeads,
        int expectedCount)
    {
        return prefixFirstNonZeroFieldHex.Equals("03 ff", StringComparison.OrdinalIgnoreCase) &&
            HasOnlyProtocol03NestedMovementSummaryKeys(
                postPrefixLeads,
                expectedCount,
                "00 33 04 00 00 00 00 29",
                "00 4c 04 00 00 00 00 2a");
    }

    private static bool IsProtocol03Mode06Guarded57ByteMarker29And2aObjectViewPrefixAndLead(
        string prefixFirstNonZeroFieldHex,
        string postPrefixLeadHex)
    {
        return prefixFirstNonZeroFieldHex.Equals("03 ff", StringComparison.OrdinalIgnoreCase) &&
            (postPrefixLeadHex.Equals("00 33 04 00 00 00 00 29", StringComparison.OrdinalIgnoreCase) ||
                postPrefixLeadHex.Equals("00 4c 04 00 00 00 00 2a", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsProtocol03Mode06Guarded57ByteMarker29And2aObjectViewBodySuffix(string bodySuffixHex)
    {
        return bodySuffixHex.Equals("ff 00 00 00 00 00 00 00 00 d5 1e 00 00 4c 0a 00", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsProtocol03Mode06Guarded57ByteMarker24AltObjectViewPrefixAndLead(
        string prefixFirstNonZeroFieldHex,
        IReadOnlyDictionary<string, int> postPrefixLeads,
        int expectedCount)
    {
        return prefixFirstNonZeroFieldHex.Equals("02 ff", StringComparison.OrdinalIgnoreCase) &&
            HasProtocol03NestedMovementSummaryFullCount(postPrefixLeads, "00 91 05 00 00 00 00 24", expectedCount);
    }

    private static bool IsProtocol03Mode06Guarded57ByteMarker24AltObjectViewPrefixAndLead(
        string prefixFirstNonZeroFieldHex,
        string postPrefixLeadHex)
    {
        return prefixFirstNonZeroFieldHex.Equals("02 ff", StringComparison.OrdinalIgnoreCase) &&
            postPrefixLeadHex.Equals("00 91 05 00 00 00 00 24", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsProtocol03Mode06Guarded57ByteMarker24AltObjectViewBodySuffix(string bodySuffixHex)
    {
        return bodySuffixHex.Equals("ff 00 00 00 00 00 00 00 00 d9 1e 00 00 4c 0a 00", StringComparison.OrdinalIgnoreCase) ||
            bodySuffixHex.Equals("ff 00 00 00 00 00 00 00 00 c7 1e 00 00 4c 0a 00", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsProtocol03Mode06Guarded57ByteMarker1eAnd1fObjectViewPrefixAndLead(
        string prefixFirstNonZeroFieldHex,
        IReadOnlyDictionary<string, int> postPrefixLeads,
        int expectedCount)
    {
        return prefixFirstNonZeroFieldHex.Equals("02 ff", StringComparison.OrdinalIgnoreCase) &&
            HasOnlyProtocol03NestedMovementSummaryKeys(
                postPrefixLeads,
                expectedCount,
                "00 b0 04 00 00 00 00 1e",
                "00 d5 04 00 00 00 00 1f");
    }

    private static bool IsProtocol03Mode06Guarded57ByteMarker1eAnd1fObjectViewPrefixAndLead(
        string prefixFirstNonZeroFieldHex,
        string postPrefixLeadHex)
    {
        return prefixFirstNonZeroFieldHex.Equals("02 ff", StringComparison.OrdinalIgnoreCase) &&
            (postPrefixLeadHex.Equals("00 b0 04 00 00 00 00 1e", StringComparison.OrdinalIgnoreCase) ||
                postPrefixLeadHex.Equals("00 d5 04 00 00 00 00 1f", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsProtocol03Mode06Guarded57ByteMarker1eAnd1fObjectViewBodySuffix(string bodySuffixHex)
    {
        return bodySuffixHex.Equals("ff 00 00 00 00 00 00 00 00 c1 09 00 00 4c 0a 00", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsProtocol03Mode06Guarded94ByteObjectViewBodySuffix(string bodySuffixHex)
    {
        return bodySuffixHex.Equals("00 1f 06 08 00 00 00 00 00 00 22 00 00 00 00 00", StringComparison.OrdinalIgnoreCase) ||
            bodySuffixHex.Equals("00 1f 06 08 00 00 00 00 10 00 22 00 00 00 00 00", StringComparison.OrdinalIgnoreCase) ||
            bodySuffixHex.Equals("00 1f 07 08 00 00 00 00 00 00 22 00 00 00 00 00", StringComparison.OrdinalIgnoreCase) ||
            bodySuffixHex.Equals("00 1f 12 08 00 00 00 00 00 00 22 00 00 00 00 00", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsProtocol03Mode06Guarded94ByteMarker34ObjectViewBodySuffix(string bodySuffixHex)
    {
        return bodySuffixHex.Equals("00 1f 06 08 00 00 00 00 00 00 22 00 00 00 00 00", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsProtocol03Mode06Guarded70ByteObjectViewBodySuffix(string bodySuffixHex)
    {
        string[] parts = bodySuffixHex.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 16 &&
            parts[0].Equals("66", StringComparison.OrdinalIgnoreCase) &&
            parts[1].Equals("00", StringComparison.OrdinalIgnoreCase) &&
            parts[2].Equals("00", StringComparison.OrdinalIgnoreCase) &&
            parts[3].Equals("04", StringComparison.OrdinalIgnoreCase) &&
            parts[4].Equals("ff", StringComparison.OrdinalIgnoreCase) &&
            parts.Skip(6).All(part => part.Equals("00", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsProtocol03Mode06Guarded57ByteMarker16ObjectViewPrefixAndLead(
        string prefixFirstNonZeroFieldHex,
        IReadOnlyDictionary<string, int> postPrefixLeads,
        int expectedCount)
    {
        if (prefixFirstNonZeroFieldHex.Equals("01 ff", StringComparison.OrdinalIgnoreCase))
        {
            return HasProtocol03NestedMovementSummaryFullCount(postPrefixLeads, "00 b0 04 00 00 00 00 16", expectedCount);
        }

        if (prefixFirstNonZeroFieldHex.Equals("02 ff", StringComparison.OrdinalIgnoreCase))
        {
            return HasProtocol03NestedMovementSummaryFullCount(postPrefixLeads, "00 84 03 00 00 00 00 16", expectedCount);
        }

        return false;
    }

    private static bool IsProtocol03Mode06Guarded57ByteMarker16ObjectViewPrefixAndLead(
        string prefixFirstNonZeroFieldHex,
        string postPrefixLeadHex)
    {
        return prefixFirstNonZeroFieldHex.Equals("01 ff", StringComparison.OrdinalIgnoreCase) &&
                postPrefixLeadHex.Equals("00 b0 04 00 00 00 00 16", StringComparison.OrdinalIgnoreCase) ||
            prefixFirstNonZeroFieldHex.Equals("02 ff", StringComparison.OrdinalIgnoreCase) &&
                postPrefixLeadHex.Equals("00 84 03 00 00 00 00 16", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsProtocol03Mode06Guarded57ByteMarker16ObjectViewBodySuffix(string bodySuffixHex)
    {
        string[] parts = bodySuffixHex.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 16 &&
            parts[0].Equals("ff", StringComparison.OrdinalIgnoreCase) &&
            parts.Skip(1).Take(8).All(part => part.Equals("00", StringComparison.OrdinalIgnoreCase)) &&
            parts[9].Equals("c7", StringComparison.OrdinalIgnoreCase) &&
            parts[10].Equals("09", StringComparison.OrdinalIgnoreCase) &&
            parts[11].Equals("00", StringComparison.OrdinalIgnoreCase) &&
            parts[12].Equals("00", StringComparison.OrdinalIgnoreCase) &&
            (parts[13].Equals("b5", StringComparison.OrdinalIgnoreCase) &&
                parts[14].Equals("07", StringComparison.OrdinalIgnoreCase) ||
                parts[13].Equals("4c", StringComparison.OrdinalIgnoreCase) &&
                parts[14].Equals("0a", StringComparison.OrdinalIgnoreCase)) &&
            parts[15].Equals("00", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsProtocol03Mode06Guarded57ByteMarker16ObjectViewBoundaryHeader(string boundaryHeaderHex)
    {
        return boundaryHeaderHex.Equals("28 ff 01 00 00 00 00 00", StringComparison.OrdinalIgnoreCase) ||
            boundaryHeaderHex.Equals("28 ff 02 00 00 00 00 00", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsProtocol03Mode06Guarded57ByteMarker16And17ObjectViewPrefixAndLead(
        string prefixFirstNonZeroFieldHex,
        IReadOnlyDictionary<string, int> postPrefixLeads,
        int expectedCount)
    {
        return prefixFirstNonZeroFieldHex.Equals("01 ff", StringComparison.OrdinalIgnoreCase) &&
            HasOnlyProtocol03NestedMovementSummaryKeys(
                postPrefixLeads,
                expectedCount,
                "00 b0 04 00 00 00 00 16",
                "00 e2 04 00 00 00 00 17");
    }

    private static bool IsProtocol03Mode06Guarded57ByteMarker16And17ObjectViewPrefixAndLead(
        string prefixFirstNonZeroFieldHex,
        string postPrefixLeadHex)
    {
        return prefixFirstNonZeroFieldHex.Equals("01 ff", StringComparison.OrdinalIgnoreCase) &&
            (postPrefixLeadHex.Equals("00 b0 04 00 00 00 00 16", StringComparison.OrdinalIgnoreCase) ||
                postPrefixLeadHex.Equals("00 e2 04 00 00 00 00 17", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsProtocol03Mode06Guarded57ByteMarker16And17ObjectViewBodySuffix(string bodySuffixHex)
    {
        return IsProtocol03Mode06Guarded57ByteMarker16ObjectViewBodySuffix(bodySuffixHex);
    }

    private static bool IsProtocol03Mode06Guarded57ByteMarker15ObjectViewPrefixAndLead(
        string prefixFirstNonZeroFieldHex,
        IReadOnlyDictionary<string, int> postPrefixLeads,
        int expectedCount)
    {
        return prefixFirstNonZeroFieldHex.Equals("02 ff", StringComparison.OrdinalIgnoreCase) &&
            HasOnlyProtocol03NestedMovementSummaryKeys(
                postPrefixLeads,
                expectedCount,
                "00 5e 03 00 00 00 00 15",
                "00 1a 04 00 00 00 00 1a",
                "00 a9 03 00 00 00 00 17");
    }

    private static bool IsProtocol03Mode06Guarded57ByteMarker15ObjectViewPrefixAndLead(
        string prefixFirstNonZeroFieldHex,
        string postPrefixLeadHex)
    {
        return prefixFirstNonZeroFieldHex.Equals("02 ff", StringComparison.OrdinalIgnoreCase) &&
            (postPrefixLeadHex.Equals("00 5e 03 00 00 00 00 15", StringComparison.OrdinalIgnoreCase) ||
                postPrefixLeadHex.Equals("00 1a 04 00 00 00 00 1a", StringComparison.OrdinalIgnoreCase) ||
                postPrefixLeadHex.Equals("00 a9 03 00 00 00 00 17", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsProtocol03Mode06Guarded57ByteMarker15ObjectViewBodySuffix(string bodySuffixHex)
    {
        return bodySuffixHex.Equals("ff 00 00 00 00 00 00 00 00 00 00 00 00 4c 0a 00", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsProtocol03Mode06Guarded57ByteMarker19ObjectViewPrefixAndLead(
        string prefixFirstNonZeroFieldHex,
        IReadOnlyDictionary<string, int> postPrefixLeads,
        int expectedCount)
    {
        return prefixFirstNonZeroFieldHex.Equals("01 ff", StringComparison.OrdinalIgnoreCase) &&
            HasProtocol03NestedMovementSummaryFullCount(postPrefixLeads, "00 46 05 00 00 00 00 19", expectedCount);
    }

    private static bool IsProtocol03Mode06Guarded57ByteMarker19ObjectViewPrefixAndLead(
        string prefixFirstNonZeroFieldHex,
        string postPrefixLeadHex)
    {
        return prefixFirstNonZeroFieldHex.Equals("01 ff", StringComparison.OrdinalIgnoreCase) &&
            postPrefixLeadHex.Equals("00 46 05 00 00 00 00 19", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsProtocol03Mode06Guarded57ByteMarker19ObjectViewBodySuffix(string bodySuffixHex)
    {
        return bodySuffixHex.Equals("ff 00 00 00 00 00 00 00 00 d7 1e 00 00 4c 0a 00", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsProtocol03Mode06Guarded57ByteMarker19AltObjectViewPrefixAndLead(
        string prefixFirstNonZeroFieldHex,
        IReadOnlyDictionary<string, int> postPrefixLeads,
        int expectedCount)
    {
        return prefixFirstNonZeroFieldHex.Equals("02 ff", StringComparison.OrdinalIgnoreCase) &&
            HasProtocol03NestedMovementSummaryFullCount(postPrefixLeads, "00 f4 03 00 00 00 00 19", expectedCount);
    }

    private static bool IsProtocol03Mode06Guarded57ByteMarker19AltObjectViewPrefixAndLead(
        string prefixFirstNonZeroFieldHex,
        string postPrefixLeadHex)
    {
        return prefixFirstNonZeroFieldHex.Equals("02 ff", StringComparison.OrdinalIgnoreCase) &&
            postPrefixLeadHex.Equals("00 f4 03 00 00 00 00 19", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsProtocol03Mode06Guarded57ByteMarker19AltObjectViewBodySuffix(string bodySuffixHex)
    {
        return bodySuffixHex.Equals("ff 00 00 00 00 00 00 00 00 d2 1e 00 00 1d 0a 00", StringComparison.OrdinalIgnoreCase) ||
            bodySuffixHex.Equals("ff 00 00 00 00 00 00 00 ff 00 00 00 00 1d 0a 00", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsProtocol03Mode06Guarded57ByteMarker1fObjectViewPrefixAndLead(
        string prefixFirstNonZeroFieldHex,
        IReadOnlyDictionary<string, int> postPrefixLeads,
        int expectedCount)
    {
        return prefixFirstNonZeroFieldHex.Equals("03 ff", StringComparison.OrdinalIgnoreCase) &&
            HasProtocol03NestedMovementSummaryFullCount(postPrefixLeads, "00 39 03 00 00 00 00 1f", expectedCount);
    }

    private static bool IsProtocol03Mode06Guarded57ByteMarker1fObjectViewPrefixAndLead(
        string prefixFirstNonZeroFieldHex,
        string postPrefixLeadHex)
    {
        return prefixFirstNonZeroFieldHex.Equals("03 ff", StringComparison.OrdinalIgnoreCase) &&
            postPrefixLeadHex.Equals("00 39 03 00 00 00 00 1f", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsProtocol03Mode06Guarded57ByteMarker1fObjectViewBodySuffix(string bodySuffixHex)
    {
        return bodySuffixHex.Equals("ff 00 00 00 00 00 00 00 00 cb 1e 00 00 4c 0a 00", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsProtocol03Mode06Guarded57ByteMarker24ObjectViewPrefixAndLead(
        string prefixFirstNonZeroFieldHex,
        IReadOnlyDictionary<string, int> postPrefixLeads,
        int expectedCount)
    {
        if (prefixFirstNonZeroFieldHex.Equals("01 ff", StringComparison.OrdinalIgnoreCase))
        {
            return HasProtocol03NestedMovementSummaryFullCount(postPrefixLeads, "00 6c 07 00 00 00 00 24", expectedCount);
        }

        if (prefixFirstNonZeroFieldHex.Equals("03 ff", StringComparison.OrdinalIgnoreCase))
        {
            return HasProtocol03NestedMovementSummaryFullCount(postPrefixLeads, "00 b6 03 00 00 00 00 24", expectedCount);
        }

        return false;
    }

    private static bool IsProtocol03Mode06Guarded57ByteMarker24ObjectViewPrefixAndLead(
        string prefixFirstNonZeroFieldHex,
        string postPrefixLeadHex)
    {
        return prefixFirstNonZeroFieldHex.Equals("01 ff", StringComparison.OrdinalIgnoreCase) &&
                postPrefixLeadHex.Equals("00 6c 07 00 00 00 00 24", StringComparison.OrdinalIgnoreCase) ||
            prefixFirstNonZeroFieldHex.Equals("03 ff", StringComparison.OrdinalIgnoreCase) &&
                postPrefixLeadHex.Equals("00 b6 03 00 00 00 00 24", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsProtocol03Mode06Guarded57ByteMarker24ObjectViewBodySuffix(string bodySuffixHex)
    {
        return bodySuffixHex.Equals("ff 00 00 00 00 00 00 00 00 c7 1e 00 00 4c 0a 00", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsProtocol03Mode06Guarded57ByteMarker33ObjectViewPrefixAndLead(
        string prefixFirstNonZeroFieldHex,
        IReadOnlyDictionary<string, int> postPrefixLeads,
        int expectedCount)
    {
        return prefixFirstNonZeroFieldHex.Equals("01 ff", StringComparison.OrdinalIgnoreCase) &&
            HasOnlyProtocol03NestedMovementSummaryKeys(
                postPrefixLeads,
                expectedCount,
                "00 5a 0a 00 00 00 00 33",
                "00 8c 0a 00 00 00 00 34",
                "00 be 0a 00 00 00 00 35");
    }

    private static bool IsProtocol03Mode06Guarded57ByteMarker33ObjectViewPrefixAndLead(
        string prefixFirstNonZeroFieldHex,
        string postPrefixLeadHex)
    {
        return prefixFirstNonZeroFieldHex.Equals("01 ff", StringComparison.OrdinalIgnoreCase) &&
            (postPrefixLeadHex.Equals("00 5a 0a 00 00 00 00 33", StringComparison.OrdinalIgnoreCase) ||
                postPrefixLeadHex.Equals("00 8c 0a 00 00 00 00 34", StringComparison.OrdinalIgnoreCase) ||
                postPrefixLeadHex.Equals("00 be 0a 00 00 00 00 35", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsProtocol03Mode06Guarded57ByteMarker33ObjectViewBodySuffix(string bodySuffixHex)
    {
        return bodySuffixHex.Equals("ff 00 00 00 00 00 00 00 00 22 b6 00 00 cd 08 00", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsProtocol03Mode06Guarded57ByteMarker13ObjectViewPrefixAndLead(
        string prefixFirstNonZeroFieldHex,
        IReadOnlyDictionary<string, int> postPrefixLeads,
        int expectedCount)
    {
        return prefixFirstNonZeroFieldHex.Equals("02 ff", StringComparison.OrdinalIgnoreCase) &&
            HasProtocol03NestedMovementSummaryFullCount(postPrefixLeads, "00 13 03 00 00 00 00 13", expectedCount);
    }

    private static bool IsProtocol03Mode06Guarded57ByteMarker13ObjectViewPrefixAndLead(
        string prefixFirstNonZeroFieldHex,
        string postPrefixLeadHex)
    {
        return prefixFirstNonZeroFieldHex.Equals("02 ff", StringComparison.OrdinalIgnoreCase) &&
            postPrefixLeadHex.Equals("00 13 03 00 00 00 00 13", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsProtocol03Mode06Guarded57ByteMarker13ObjectViewBodySuffix(string bodySuffixHex)
    {
        return bodySuffixHex.Equals("ff 00 00 00 00 00 00 00 00 e5 1a 00 00 4c 0a 00", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsProtocol03Mode06Guarded57ByteMarker1dAnd1eObjectViewPrefixAndLead(
        string prefixFirstNonZeroFieldHex,
        IReadOnlyDictionary<string, int> postPrefixLeads,
        int expectedCount)
    {
        return prefixFirstNonZeroFieldHex.Equals("03 ff", StringComparison.OrdinalIgnoreCase) &&
            HasOnlyProtocol03NestedMovementSummaryKeys(
                postPrefixLeads,
                expectedCount,
                "00 07 03 00 00 00 00 1d",
                "00 20 03 00 00 00 00 1e");
    }

    private static bool IsProtocol03Mode06Guarded57ByteMarker1dAnd1eObjectViewPrefixAndLead(
        string prefixFirstNonZeroFieldHex,
        string postPrefixLeadHex)
    {
        return prefixFirstNonZeroFieldHex.Equals("03 ff", StringComparison.OrdinalIgnoreCase) &&
            (postPrefixLeadHex.Equals("00 07 03 00 00 00 00 1d", StringComparison.OrdinalIgnoreCase) ||
                postPrefixLeadHex.Equals("00 20 03 00 00 00 00 1e", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsProtocol03Mode06Guarded57ByteMarker1dAnd1eObjectViewBodySuffix(string bodySuffixHex)
    {
        return bodySuffixHex.Equals("ff 00 00 00 00 00 00 00 ff 00 00 00 00 1d 0a 00", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsProtocol03Mode06Guarded57ByteMarker1eObjectViewPrefixAndLead(
        string prefixFirstNonZeroFieldHex,
        IReadOnlyDictionary<string, int> postPrefixLeads,
        int expectedCount)
    {
        return prefixFirstNonZeroFieldHex.Equals("03 ff", StringComparison.OrdinalIgnoreCase) &&
            HasProtocol03NestedMovementSummaryFullCount(postPrefixLeads, "00 20 03 00 00 00 00 1e", expectedCount);
    }

    private static bool IsProtocol03Mode06Guarded57ByteMarker1eObjectViewPrefixAndLead(
        string prefixFirstNonZeroFieldHex,
        string postPrefixLeadHex)
    {
        return prefixFirstNonZeroFieldHex.Equals("03 ff", StringComparison.OrdinalIgnoreCase) &&
            postPrefixLeadHex.Equals("00 20 03 00 00 00 00 1e", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsProtocol03Mode06Guarded57ByteMarker1eObjectViewBodySuffix(string bodySuffixHex)
    {
        return bodySuffixHex.Equals("ff 00 00 00 00 00 00 00 00 d3 1e 00 00 1d 0a 00", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsProtocol03Mode06Guarded57ByteMarker1dObjectViewPrefixAndLead(
        string prefixFirstNonZeroFieldHex,
        IReadOnlyDictionary<string, int> postPrefixLeads,
        int expectedCount)
    {
        return prefixFirstNonZeroFieldHex.Equals("02 ff", StringComparison.OrdinalIgnoreCase) &&
            HasOnlyProtocol03NestedMovementSummaryKeys(
                postPrefixLeads,
                expectedCount,
                "00 8a 04 00 00 00 00 1d",
                "00 f4 03 00 00 00 00 19",
                "00 1a 04 00 00 00 00 1a");
    }

    private static bool IsProtocol03Mode06Guarded57ByteMarker1dObjectViewPrefixAndLead(
        string prefixFirstNonZeroFieldHex,
        string postPrefixLeadHex)
    {
        return prefixFirstNonZeroFieldHex.Equals("02 ff", StringComparison.OrdinalIgnoreCase) &&
            (postPrefixLeadHex.Equals("00 8a 04 00 00 00 00 1d", StringComparison.OrdinalIgnoreCase) ||
                postPrefixLeadHex.Equals("00 f4 03 00 00 00 00 19", StringComparison.OrdinalIgnoreCase) ||
                postPrefixLeadHex.Equals("00 1a 04 00 00 00 00 1a", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsProtocol03Mode06Guarded57ByteMarker1dObjectViewBodySuffix(string bodySuffixHex)
    {
        return bodySuffixHex.Equals("ff 00 00 00 00 00 00 00 00 c0 09 00 00 4c 0a 00", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsProtocol03Mode06Guarded57ByteMarker25ObjectViewPrefixAndLead(
        string prefixFirstNonZeroFieldHex,
        IReadOnlyDictionary<string, int> postPrefixLeads,
        int expectedCount)
    {
        return prefixFirstNonZeroFieldHex.Equals("01 ff", StringComparison.OrdinalIgnoreCase) &&
            HasOnlyProtocol03NestedMovementSummaryKeys(
                postPrefixLeads,
                expectedCount,
                "00 9e 07 00 00 00 00 25",
                "00 6c 07 00 00 00 00 24");
    }

    private static bool IsProtocol03Mode06Guarded57ByteMarker25ObjectViewPrefixAndLead(
        string prefixFirstNonZeroFieldHex,
        string postPrefixLeadHex)
    {
        return prefixFirstNonZeroFieldHex.Equals("01 ff", StringComparison.OrdinalIgnoreCase) &&
            (postPrefixLeadHex.Equals("00 9e 07 00 00 00 00 25", StringComparison.OrdinalIgnoreCase) ||
                postPrefixLeadHex.Equals("00 6c 07 00 00 00 00 24", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsProtocol03Mode06Guarded57ByteMarker25ObjectViewBodySuffix(string bodySuffixHex)
    {
        return bodySuffixHex.Equals("ff 00 00 00 00 00 00 00 00 d9 1e 00 00 4c 0a 00", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsProtocol03Mode06Guarded57ByteMarker1dB9ObjectViewPrefixAndLead(
        string prefixFirstNonZeroFieldHex,
        IReadOnlyDictionary<string, int> postPrefixLeads,
        int expectedCount)
    {
        return prefixFirstNonZeroFieldHex.Equals("03 ff", StringComparison.OrdinalIgnoreCase) &&
            HasProtocol03NestedMovementSummaryFullCount(postPrefixLeads, "00 07 03 00 00 00 00 1d", expectedCount);
    }

    private static bool IsProtocol03Mode06Guarded57ByteMarker1dB9ObjectViewPrefixAndLead(
        string prefixFirstNonZeroFieldHex,
        string postPrefixLeadHex)
    {
        return prefixFirstNonZeroFieldHex.Equals("03 ff", StringComparison.OrdinalIgnoreCase) &&
            postPrefixLeadHex.Equals("00 07 03 00 00 00 00 1d", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsProtocol03Mode06Guarded57ByteMarker1dB9ObjectViewBodySuffix(string bodySuffixHex)
    {
        return bodySuffixHex.Equals("ff 00 00 00 00 00 00 00 00 ca 1e 00 00 b9 07 00", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsProtocol03Mode06Guarded57ByteMarker15EcObjectViewPrefixAndLead(
        string prefixFirstNonZeroFieldHex,
        IReadOnlyDictionary<string, int> postPrefixLeads,
        int expectedCount)
    {
        return prefixFirstNonZeroFieldHex.Equals("02 ff", StringComparison.OrdinalIgnoreCase) &&
            HasProtocol03NestedMovementSummaryFullCount(postPrefixLeads, "00 5e 03 00 00 00 00 15", expectedCount);
    }

    private static bool IsProtocol03Mode06Guarded57ByteMarker15EcObjectViewPrefixAndLead(
        string prefixFirstNonZeroFieldHex,
        string postPrefixLeadHex)
    {
        return prefixFirstNonZeroFieldHex.Equals("02 ff", StringComparison.OrdinalIgnoreCase) &&
            postPrefixLeadHex.Equals("00 5e 03 00 00 00 00 15", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsProtocol03Mode06Guarded57ByteMarker15EcObjectViewBodySuffix(string bodySuffixHex)
    {
        return bodySuffixHex.Equals("ff 00 00 00 00 00 00 00 00 ec 1a 00 00 4c 0a 00", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsProtocol03Mode06Guarded53ByteMarker34ObjectViewBodySuffix(string bodySuffixHex)
    {
        string[] parts = bodySuffixHex.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 16 &&
            parts[0].Equals("01", StringComparison.OrdinalIgnoreCase) &&
            parts[1].Equals("00", StringComparison.OrdinalIgnoreCase) &&
            parts[4].Equals("ff", StringComparison.OrdinalIgnoreCase) &&
            parts.Skip(5).All(part => part.Equals("00", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsProtocol03Mode06Terminal96ByteTupleBody(IReadOnlyList<byte> body)
    {
        if (body.Count != 96 ||
            body[0] != 0x00 ||
            body[1] != 0x00 ||
            body[2] != 0x00 ||
            body[3] != 0x00 ||
            body[4] != 0x00 ||
            body[5] != 0x00 ||
            body[6] != 0xff ||
            body[7] != 0x00)
        {
            return false;
        }

        return IsProtocol03Mode06Terminal96ByteTupleBodySuffix(
            FormatHeader(body.Skip(body.Count - 16).Take(16)));
    }

    private static bool TryClassifyProtocol03Mode06LongEmbeddedMovementTrailerBody(
        IReadOnlyList<byte> body,
        bool terminalTupleBody,
        string bodySuffixHex,
        string boundaryHeaderHex,
        out string parserAction,
        out string classification)
    {
        parserAction = string.Empty;
        classification = string.Empty;
        int? anchorBytes = GetProtocol03Mode06LongTupleBodyTailAnchorBytes(bodySuffixHex);
        if (anchorBytes is null || anchorBytes.Value >= body.Count)
        {
            return false;
        }

        int preAnchorBodyBytes = body.Count - anchorBytes.Value;
        if (!TryReadProtocol03Mode06LongEmbeddedMovementTrailerShape(
                body,
                anchorBytes.Value,
                out byte innerSelector,
                out string preTailFieldHex,
                out string preAnchorPreLeadHex,
                out string preAnchorTrailingLeadHex))
        {
            return false;
        }

        byte[] anchorBody = body.Skip(preAnchorBodyBytes).Take(anchorBytes.Value).ToArray();
        if (terminalTupleBody &&
            anchorBytes.Value == 96 &&
            IsProtocol03Mode06Terminal96ByteTupleBody(anchorBody) &&
            innerSelector is 0x0e or 0x0c &&
            preTailFieldHex.Equals("02", StringComparison.OrdinalIgnoreCase) &&
            preAnchorPreLeadHex.Equals("ff cd cc 4c be 00 00 00", StringComparison.OrdinalIgnoreCase) &&
            preAnchorTrailingLeadHex.Equals("00 e9 07 00 00 00 00 34", StringComparison.OrdinalIgnoreCase))
        {
            parserAction = "test terminal mode 06 long embedded marker-34 trailer body";
            classification = "mode 06 terminal long embedded movement marker-34 trailer body";
            return true;
        }

        if (terminalTupleBody &&
            anchorBytes.Value == 96 &&
            IsProtocol03Mode06Terminal96ByteTupleBody(anchorBody) &&
            innerSelector is 0x0e or 0x08 &&
            preTailFieldHex.Equals("03", StringComparison.OrdinalIgnoreCase) &&
            preAnchorPreLeadHex.Equals("ff 00 00 00 00 00 00 00", StringComparison.OrdinalIgnoreCase) &&
            preAnchorTrailingLeadHex.Equals("00 96 00 00 00 00 00 04", StringComparison.OrdinalIgnoreCase))
        {
            parserAction = "test terminal mode 06 long embedded marker-04 trailer body";
            classification = "mode 06 terminal long embedded movement marker-04 trailer body";
            return true;
        }

        if (terminalTupleBody &&
            anchorBytes.Value == 96 &&
            IsProtocol03Mode06Terminal96ByteTupleBody(anchorBody) &&
            innerSelector is 0x0e or 0x0c &&
            preTailFieldHex.Equals("03", StringComparison.OrdinalIgnoreCase) &&
            preAnchorPreLeadHex.Equals("ff 00 00 00 00 00 00 00", StringComparison.OrdinalIgnoreCase) &&
            preAnchorTrailingLeadHex.Equals("00 b6 03 00 00 00 00 24", StringComparison.OrdinalIgnoreCase))
        {
            parserAction = "test terminal mode 06 long embedded marker-24 trailer body";
            classification = "mode 06 terminal long embedded movement marker-24 trailer body";
            return true;
        }

        if (terminalTupleBody &&
            anchorBytes.Value == 96 &&
            IsProtocol03Mode06Terminal96ByteTupleBody(anchorBody) &&
            innerSelector is 0x0e or 0x0c &&
            preTailFieldHex.Equals("03", StringComparison.OrdinalIgnoreCase) &&
            preAnchorPreLeadHex.Equals("ff 00 00 00 00 00 00 00", StringComparison.OrdinalIgnoreCase) &&
            preAnchorTrailingLeadHex.Equals("00 4c 04 00 00 00 00 2a", StringComparison.OrdinalIgnoreCase))
        {
            parserAction = "test terminal mode 06 long embedded marker-2a trailer body";
            classification = "mode 06 terminal long embedded movement marker-2a trailer body";
            return true;
        }

        if (!terminalTupleBody &&
            anchorBytes.Value == 57 &&
            innerSelector == 0x0e &&
            preTailFieldHex.Equals("03", StringComparison.OrdinalIgnoreCase) &&
            preAnchorPreLeadHex.Equals("ff 00 00 00 00 00 00 00", StringComparison.OrdinalIgnoreCase) &&
            preAnchorTrailingLeadHex.Equals("00 cf 03 00 00 00 00 25", StringComparison.OrdinalIgnoreCase) &&
            bodySuffixHex.Equals("ff 00 00 00 00 00 00 00 00 d4 1e 00 00 4c 0a 00", StringComparison.OrdinalIgnoreCase) &&
            boundaryHeaderHex.Equals("28 ff 01 00 00 00 00 00", StringComparison.OrdinalIgnoreCase))
        {
            parserAction = "test guarded mode 06 long embedded marker-25 trailer body";
            classification = "mode 06 guarded 57-byte long embedded movement marker-25 trailer body";
            return true;
        }

        return false;
    }

    private static bool TryReadProtocol03Mode06LongEmbeddedMovementTrailerShape(
        IReadOnlyList<byte> body,
        int anchorBytes,
        out byte innerSelector,
        out string preTailFieldHex,
        out string preAnchorPreLeadHex,
        out string preAnchorTrailingLeadHex)
    {
        const int markerOffset = 95;
        const int trailerBytes = 55;

        innerSelector = 0;
        preTailFieldHex = string.Empty;
        preAnchorPreLeadHex = string.Empty;
        preAnchorTrailingLeadHex = string.Empty;
        int preAnchorBodyBytes = body.Count - anchorBytes;
        if (preAnchorBodyBytes <= markerOffset + 2 ||
            body[markerOffset] != 0x00 ||
            body[markerOffset + 1] != 0x06)
        {
            return false;
        }

        innerSelector = body[markerOffset + 2];
        if (!TryGetProtocol03MovementPayloadInfo(innerSelector, out int innerPayloadBytes, out int innerPositionOffset))
        {
            return false;
        }

        int positionOffset = markerOffset + 3 + innerPositionOffset;
        int postMovementOffset = markerOffset + 3 + innerPayloadBytes;
        if (postMovementOffset > preAnchorBodyBytes ||
            preAnchorBodyBytes - postMovementOffset != trailerBytes ||
            !TryReadSingleLittleEndian(body, positionOffset, out float x) ||
            !TryReadSingleLittleEndian(body, positionOffset + 4, out float y) ||
            !TryReadSingleLittleEndian(body, positionOffset + 8, out float z) ||
            !IsPlausibleProtocol03Position(x, y, z))
        {
            return false;
        }

        byte[] postMovementBody = body.Skip(postMovementOffset).Take(trailerBytes).ToArray();
        if (!FormatHeader(postMovementBody.Take(8)).Equals("ff 00 00 00 10 00 00 00", StringComparison.OrdinalIgnoreCase) ||
            !FormatHeader(postMovementBody.Skip(8).Take(8)).Equals("00 00 01 ff 00 00 00 00", StringComparison.OrdinalIgnoreCase) ||
            !postMovementBody.Skip(16).Take(22).All(value => value == 0x00))
        {
            return false;
        }

        preTailFieldHex = FormatHeader(postMovementBody.Skip(38).Take(1));
        preAnchorPreLeadHex = FormatHeader(postMovementBody.Skip(39).Take(8));
        preAnchorTrailingLeadHex = FormatHeader(postMovementBody.Skip(47).Take(8));
        return true;
    }

    private static string ClassifyProtocol03Mode06LongTupleBodyTailAnchor(string bodySuffixHex)
    {
        int? anchorBytes = GetProtocol03Mode06LongTupleBodyTailAnchorBytes(bodySuffixHex);
        return anchorBytes switch
        {
            96 => "trailing terminal-96 state suffix",
            94 => "trailing guarded 94-byte state suffix",
            57 => "trailing guarded 57-byte object-view suffix",
            53 => "trailing marker-34 53-byte suffix",
            _ when ContainsProtocol03Mode06ContinuationBytes(bodySuffixHex) => "variable continuation suffix",
            _ => "unknown long tuple body suffix"
        };
    }

    private static (
        string PrefixHex,
        string SuffixHex,
        int? FirstNonZeroOffset,
        string FirstNonZeroFieldHex,
        string TrailingLeadHex,
        string PreLeadHex,
        int? PreTailFieldOffset,
        string PreTailFieldHex) GetProtocol03Mode06PreAnchorBodyEvidence(byte[] body)
    {
        if (body.Length == 0)
        {
            return (string.Empty, string.Empty, null, string.Empty, string.Empty, string.Empty, null, string.Empty);
        }

        string bodySuffixHex = FormatHeader(body.Skip(Math.Max(0, body.Length - 16)).Take(Math.Min(16, body.Length)));
        int? anchorBytes = GetProtocol03Mode06LongTupleBodyTailAnchorBytes(bodySuffixHex);
        if (anchorBytes is null || anchorBytes.Value >= body.Length)
        {
            return (string.Empty, string.Empty, null, string.Empty, string.Empty, string.Empty, null, string.Empty);
        }

        int preAnchorBodyBytes = body.Length - anchorBytes.Value;
        byte[] preAnchorBody = body.Take(preAnchorBodyBytes).ToArray();
        int? firstNonZeroOffset = FindFirstNonZeroOffset(preAnchorBody);
        string firstNonZeroFieldHex = firstNonZeroOffset is null
            ? string.Empty
            : FormatHeader(preAnchorBody.Skip(firstNonZeroOffset.Value).Take(Math.Min(2, preAnchorBody.Length - firstNonZeroOffset.Value)));
        string trailingLeadHex = preAnchorBody.Length >= 8
            ? FormatHeader(preAnchorBody.Skip(preAnchorBody.Length - 8).Take(8))
            : string.Empty;
        string preLeadHex = preAnchorBody.Length >= 16
            ? FormatHeader(preAnchorBody.Skip(preAnchorBody.Length - 16).Take(8))
            : string.Empty;
        int? preTailFieldOffset = preAnchorBody.Length >= 17
            ? preAnchorBody.Length - 17
            : null;
        string preTailFieldHex = preTailFieldOffset is null
            ? string.Empty
            : FormatHeader(preAnchorBody.Skip(preTailFieldOffset.Value).Take(1));

        return (
            FormatHeader(preAnchorBody.Take(Math.Min(16, preAnchorBody.Length))),
            FormatHeader(preAnchorBody.Skip(Math.Max(0, preAnchorBody.Length - 16)).Take(Math.Min(16, preAnchorBody.Length))),
            firstNonZeroOffset,
            firstNonZeroFieldHex,
            trailingLeadHex,
            preLeadHex,
            preTailFieldOffset,
            preTailFieldHex);
    }

    private static string FormatProtocol03Mode06PreAnchorFirstNonZeroWindow(
        IReadOnlyList<byte> preAnchorBody,
        int? firstNonZeroOffset)
    {
        if (preAnchorBody.Count == 0 || firstNonZeroOffset is null)
        {
            return string.Empty;
        }

        int windowStart = Math.Max(0, firstNonZeroOffset.Value - 6);
        return FormatHeader(preAnchorBody
            .Skip(windowStart)
            .Take(Math.Min(16, preAnchorBody.Count - windowStart)));
    }

    private static string FormatProtocol03Mode06PreAnchorNonZeroByteOffsets(
        IReadOnlyList<byte> preAnchorBody,
        int maxValues)
    {
        var entries = preAnchorBody
            .Select((value, offset) => new { value, offset })
            .Where(entry => entry.value != 0)
            .ToArray();
        if (entries.Length == 0)
        {
            return "-";
        }

        IEnumerable<string> formatted = entries
            .Take(maxValues)
            .Select(entry => string.Format(CultureInfo.InvariantCulture, "{0}:{1:x2}", entry.offset, entry.value));
        string suffix = entries.Length > maxValues
            ? string.Format(CultureInfo.InvariantCulture, " +{0} more", entries.Length - maxValues)
            : string.Empty;
        return string.Join(" ", formatted) + suffix;
    }

    private static string FormatProtocol03Mode06PreAnchorHeaderLikeOffsets(
        IReadOnlyList<byte> preAnchorBody,
        int maxValues)
    {
        var entries = Enumerable.Range(0, Math.Max(0, preAnchorBody.Count - 3))
            .Where(offset => LooksLikeProtocol03ObjectViewHeader(preAnchorBody, offset))
            .Select(offset => string.Format(
                CultureInfo.InvariantCulture,
                "{0}:{1}",
                offset,
                ClassifyProtocol03ObjectView(preAnchorBody[offset + 2], preAnchorBody[offset + 3])))
            .ToArray();
        if (entries.Length == 0)
        {
            return "-";
        }

        string suffix = entries.Length > maxValues
            ? string.Format(CultureInfo.InvariantCulture, " +{0} more", entries.Length - maxValues)
            : string.Empty;
        return string.Join(" | ", entries.Take(maxValues)) + suffix;
    }

    private static int? GetProtocol03Mode06LongTupleBodyTailAnchorBytes(string bodySuffixHex)
    {
        if (IsProtocol03Mode06Terminal96ByteTupleBodySuffix(bodySuffixHex))
        {
            return 96;
        }

        if (IsProtocol03Mode06GuardedZeroUpdate94ByteBodySuffix(bodySuffixHex) ||
            IsProtocol03Mode06Guarded94ByteObjectViewBodySuffix(bodySuffixHex))
        {
            return 94;
        }

        if (IsProtocol03Mode06Guarded57ByteObjectViewBodySuffix(bodySuffixHex) ||
            IsProtocol03Mode06Guarded57ByteMarker16ObjectViewBodySuffix(bodySuffixHex) ||
            IsProtocol03Mode06Guarded57ByteMarker15ObjectViewBodySuffix(bodySuffixHex) ||
            IsProtocol03Mode06Guarded57ByteMarker19ObjectViewBodySuffix(bodySuffixHex) ||
            IsProtocol03Mode06Guarded57ByteMarker19AltObjectViewBodySuffix(bodySuffixHex) ||
            IsProtocol03Mode06Guarded57ByteMarker1fObjectViewBodySuffix(bodySuffixHex) ||
            IsProtocol03Mode06Guarded57ByteMarker29And2aObjectViewBodySuffix(bodySuffixHex) ||
            IsProtocol03Mode06Guarded57ByteMarker24AltObjectViewBodySuffix(bodySuffixHex) ||
            IsProtocol03Mode06Guarded57ByteMarker1eAnd1fObjectViewBodySuffix(bodySuffixHex) ||
            IsProtocol03Mode06Guarded57ByteMarker24ObjectViewBodySuffix(bodySuffixHex) ||
            IsProtocol03Mode06Guarded57ByteMarker33ObjectViewBodySuffix(bodySuffixHex) ||
            IsProtocol03Mode06Guarded57ByteMarker13ObjectViewBodySuffix(bodySuffixHex) ||
            IsProtocol03Mode06Guarded57ByteMarker1dAnd1eObjectViewBodySuffix(bodySuffixHex) ||
            IsProtocol03Mode06Guarded57ByteMarker1eObjectViewBodySuffix(bodySuffixHex) ||
            IsProtocol03Mode06Guarded57ByteMarker1dObjectViewBodySuffix(bodySuffixHex) ||
            IsProtocol03Mode06Guarded57ByteMarker25ObjectViewBodySuffix(bodySuffixHex) ||
            IsProtocol03Mode06Guarded57ByteMarker1dB9ObjectViewBodySuffix(bodySuffixHex) ||
            IsProtocol03Mode06Guarded57ByteMarker15EcObjectViewBodySuffix(bodySuffixHex))
        {
            return 57;
        }

        if (IsProtocol03Mode06Guarded53ByteMarker34ObjectViewBodySuffix(bodySuffixHex))
        {
            return 53;
        }

        return null;
    }

    private static bool IsProtocol03Mode06Terminal96ByteTupleBodySuffix(string bodySuffixHex)
    {
        byte[] suffix = ParseHexBytes(bodySuffixHex).ToArray();
        return suffix.Length == 16 &&
            suffix[0] is 0x06 or 0x07 or 0x12 &&
            suffix[1] == 0x08 &&
            suffix[2] == 0x00 &&
            suffix[3] == 0x00 &&
            suffix[4] == 0x00 &&
            suffix[5] is 0x00 or 0x40 &&
            suffix[6] is 0x00 or 0x10 &&
            suffix[7] == 0x00 &&
            suffix[8] == 0x22 &&
            suffix.Skip(9).All(value => value == 0x00);
    }

    private static bool ContainsProtocol03Mode06ContinuationBytes(string bodySuffixHex)
    {
        byte[] suffix = ParseHexBytes(bodySuffixHex).ToArray();
        for (int i = 0; i <= suffix.Length - 2; i++)
        {
            if (suffix[i] == 0x80 && suffix[i + 1] == 0x80)
            {
                return true;
            }
        }

        return false;
    }

    private static int GetProtocol03NestedMovementMode06TupleBodyParserActionOrder(string parserAction)
    {
        return parserAction switch
        {
            "test consuming mode 06 zero-update tuple body" => 0,
            "test guarded mode 06 94-byte zero-update tuple body" => 1,
            "test guarded mode 06 57-byte object-view tuple body" => 2,
            "test guarded mode 06 57-byte marker-24-25 object-view tuple body" => 2,
            "test guarded mode 06 94-byte object-view tuple body" => 3,
            "test guarded mode 06 94-byte marker-34 object-view tuple body" => 3,
            "test guarded mode 06 70-byte object-view tuple body" => 4,
            "test guarded mode 06 57-byte marker-16 object-view tuple body" => 5,
            "test guarded mode 06 57-byte marker-16-17 object-view tuple body" => 6,
            "test guarded mode 06 57-byte marker-15 object-view tuple body" => 7,
            "test guarded mode 06 57-byte marker-19 object-view tuple body" => 8,
            "test guarded mode 06 57-byte marker-19-alt object-view tuple body" => 8,
            "test guarded mode 06 57-byte marker-1f object-view tuple body" => 8,
            "test guarded mode 06 57-byte marker-24-26 object-view tuple body" => 9,
            "test guarded mode 06 57-byte marker-29-2a object-view tuple body" => 10,
            "test guarded mode 06 57-byte marker-24-alt object-view tuple body" => 11,
            "test guarded mode 06 57-byte marker-1e-1f object-view tuple body" => 12,
            "test guarded mode 06 57-byte marker-24 object-view tuple body" => 13,
            "test guarded mode 06 57-byte marker-33 object-view tuple body" => 14,
            "test guarded mode 06 57-byte marker-13 object-view tuple body" => 15,
            "test guarded mode 06 57-byte marker-1d-1e object-view tuple body" => 16,
            "test guarded mode 06 57-byte marker-1e object-view tuple body" => 16,
            "test guarded mode 06 57-byte marker-1d object-view tuple body" => 17,
            "test guarded mode 06 57-byte marker-25 object-view tuple body" => 18,
            "test guarded mode 06 57-byte marker-1d-b9 object-view tuple body" => 19,
            "test guarded mode 06 57-byte marker-15-ec object-view tuple body" => 20,
            "test guarded mode 06 53-byte marker-34-b8-15 object-view tuple body" => 21,
            "test guarded mode 06 53-byte marker-34-62-0f object-view tuple body" => 22,
            "test guarded mode 06 53-byte marker-34-b8-1c object-view tuple body" => 23,
            "test guarded mode 06 unclassified tuple body" => 24,
            "test guarded mode 06 unclassified 53-byte tuple body" => 25,
            "test guarded mode 06 non-dominant zero-update tuple body" => 26,
            "test guarded mode 06 non-dominant 49-byte zero-update tuple body" => 27,
            "test guarded mode 06 long embedded marker-25 trailer body" => 28,
            "test terminal mode 06 96-byte tuple body" => 29,
            "test terminal mode 06 long embedded marker-34 trailer body" => 30,
            "test terminal mode 06 long embedded marker-04 trailer body" => 31,
            "test terminal mode 06 long embedded marker-2a trailer body" => 32,
            "test terminal mode 06 long embedded marker-24 trailer body" => 33,
            "hold mode 06 marker-34 long object-view tuple body as evidence only" => 34,
            _ => 35
        };
    }

    public static IEnumerable<Protocol03NestedMovementParserActionSummary> BuildProtocol03NestedMovementParserActionSummaries(
        IEnumerable<Protocol03NestedMovementBoundaryEvidenceSummary> summaries)
    {
        return summaries
            .GroupBy(summary => new
            {
                summary.ParserAction,
                summary.ActionReason
            })
            .Select(group =>
            {
                Protocol03NestedMovementBoundaryEvidenceSummary[] groupSummaries = group.ToArray();
                Protocol03NestedMovementBoundaryEvidenceSummary sample = groupSummaries[0];
                return new Protocol03NestedMovementParserActionSummary(
                    group.Key.ParserAction,
                    group.Key.ActionReason,
                    groupSummaries.Length,
                    groupSummaries.Sum(summary => summary.WindowCount),
                    groupSummaries.Sum(summary => summary.BoundedPrimaryWrapperCount),
                    groupSummaries.Sum(summary => summary.UnresolvedPayloadLeadCount),
                    CountProtocol03NestedMovementWeightedValues(
                        groupSummaries.Select(summary => new WeightedValue(summary.EvidenceDisposition, summary.WindowCount)),
                        8),
                    CountProtocol03NestedMovementWeightedValues(
                        groupSummaries.SelectMany(summary => summary.MarkerModes.Select(entry => new WeightedValue(entry.Key, entry.Value))),
                        8),
                    CountProtocol03NestedMovementWeightedValues(
                        groupSummaries.SelectMany(summary => summary.InnerSelectors.Select(entry => new WeightedValue(entry.Key, entry.Value))),
                        8),
                    CountProtocol03NestedMovementWeightedValues(
                        groupSummaries.SelectMany(summary => summary.OuterSelectors.Select(entry => new WeightedValue(entry.Key, entry.Value))),
                        12),
                    CountProtocol03NestedMovementWeightedValues(
                        groupSummaries.SelectMany(summary => summary.ObjectClassifications.Select(entry => new WeightedValue(entry.Key, entry.Value))),
                        12),
                    CountProtocol03NestedMovementWeightedValues(
                        groupSummaries.SelectMany(summary => summary.SuffixPrefixes.Select(entry => new WeightedValue(entry.Key, entry.Value))),
                        12),
                    CountProtocol03NestedMovementWeightedValues(
                        groupSummaries.SelectMany(summary => summary.PostSuffixPrefixes.Select(entry => new WeightedValue(entry.Key, entry.Value))),
                        12),
                    CountProtocol03NestedMovementWeightedValues(
                        groupSummaries.SelectMany(summary => summary.PayloadBytes.Select(entry => new WeightedValue(entry.Key, entry.Value))),
                        12),
                    CountProtocol03NestedMovementWeightedValues(
                        groupSummaries.SelectMany(summary => summary.PositionSamples.Select(entry => new WeightedValue(entry.Key, entry.Value))),
                        8),
                    sample.SampleFile,
                    sample.SampleLine);
            })
            .OrderBy(summary => GetProtocol03NestedMovementParserActionOrder(summary.ParserAction))
            .ThenByDescending(summary => summary.WindowCount)
            .ThenBy(summary => summary.ParserAction, StringComparer.OrdinalIgnoreCase);
    }

    public static IEnumerable<VendorBuyPriceLeadSummary> BuildVendorBuyPriceLeadSummaries(
        IReadOnlyList<PacketDumpFileSummary> dumpSummaries,
        IReadOnlyList<ItemCommandEntry> itemCommandEntries,
        IReadOnlyList<VendorPriceEntry> vendorPriceEntries,
        IReadOnlyList<VendorInventoryEntry> vendorInventoryEntries)
    {
        Dictionary<uint, ItemCommandEntry> itemsById = itemCommandEntries
            .GroupBy(entry => entry.ItemId)
            .ToDictionary(group => group.Key, group => group.First());
        Dictionary<uint, VendorPriceEntry> pricesByGoId = vendorPriceEntries
            .GroupBy(entry => entry.GoId)
            .ToDictionary(group => group.Key, group => group.First());
        ILookup<uint, VendorInventoryEntry> vendorRowsByItem = vendorInventoryEntries
            .SelectMany(entry => entry.Items.Select(item => new { Item = item, Entry = entry }))
            .ToLookup(entry => entry.Item, entry => entry.Entry);

        var transactions = dumpSummaries
            .SelectMany(file => (file.VendorBuyTransactions ?? Array.Empty<VendorBuyTransactionSample>())
                .Select(transaction => new { File = file.File, Transaction = transaction }));

        foreach (var group in transactions
            .GroupBy(entry => new
            {
                entry.Transaction.BuyPayloadPrefixHex,
                entry.Transaction.ItemIdFieldHex,
                entry.Transaction.ItemId
            })
            .OrderBy(group => pricesByGoId.ContainsKey(group.Key.ItemId) ? 0 : 1)
            .ThenBy(group => group.Key.ItemId))
        {
            uint itemId = group.Key.ItemId;
            itemsById.TryGetValue(itemId, out ItemCommandEntry? item);
            pricesByGoId.TryGetValue(itemId, out VendorPriceEntry? price);
            long[] cashDeltas = group.SelectMany(entry => entry.Transaction.CashDeltas).ToArray();
            uint[] buyValues = group.SelectMany(entry => entry.Transaction.BuyValues).ToArray();
            uint[] valueKinds = group.SelectMany(entry => entry.Transaction.ValueKinds).ToArray();
            ushort[] buyAckSequences = group
                .SelectMany(entry => entry.Transaction.BuyAcks)
                .Select(ack => ack.Sequence)
                .ToArray();
            string[] buyAckTailMarkers = group
                .SelectMany(entry => entry.Transaction.BuyAcks)
                .Select(FormatVendorBuyAckDetail)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            yield return new VendorBuyPriceLeadSummary(
                group.Key.BuyPayloadPrefixHex,
                group.Key.ItemIdFieldHex,
                itemId,
                item?.Symbol,
                item?.DisplayName,
                price?.VendorPrice,
                price?.CodeName,
                FormatVendorBuyPriceMatch(cashDeltas, buyValues, price?.VendorPrice),
                group.Count(),
                buyAckSequences,
                buyAckTailMarkers,
                cashDeltas,
                buyValues,
                valueKinds,
                vendorRowsByItem[itemId].Distinct().Count(),
                group.Select(entry => new VendorTransactionSampleLocation(
                        entry.File,
                        entry.Transaction.Line,
                        entry.Transaction.NextLine))
                    .Take(8)
                    .ToArray());
        }
    }

    public static IEnumerable<VendorSellPriceLeadSummary> BuildVendorSellPriceLeadSummaries(
        IReadOnlyList<PacketDumpFileSummary> dumpSummaries,
        IReadOnlyList<AbilityDefinition> abilityDefinitions,
        IReadOnlyList<ItemCommandEntry> itemCommandEntries,
        IReadOnlyList<VendorPriceEntry> vendorPriceEntries,
        IReadOnlyList<VendorInventoryEntry> vendorInventoryEntries)
    {
        Dictionary<uint, AbilityDefinition> abilitiesById = abilityDefinitions
            .GroupBy(entry => entry.AbilityId)
            .ToDictionary(group => group.Key, group => group.First());
        Dictionary<uint, ItemCommandEntry> itemsById = itemCommandEntries
            .GroupBy(entry => entry.ItemId)
            .ToDictionary(group => group.Key, group => group.First());
        Dictionary<uint, VendorPriceEntry> pricesByGoId = vendorPriceEntries
            .GroupBy(entry => entry.GoId)
            .ToDictionary(group => group.Key, group => group.First());
        ILookup<uint, VendorInventoryEntry> vendorRowsByItem = vendorInventoryEntries
            .SelectMany(entry => entry.Items.Select(item => new { Item = item, Entry = entry }))
            .ToLookup(entry => entry.Item, entry => entry.Entry);

        var transactions = dumpSummaries
            .SelectMany(file => (file.VendorSellTransactions ?? Array.Empty<VendorSellTransactionSample>())
                .SelectMany(transaction => EnumerateVendorSellPriceFields(transaction)
                    .Select(field => new { File = file.File, Transaction = transaction, Field = field })));

        foreach (var group in transactions
            .GroupBy(entry => new
            {
                entry.Transaction.SellPayloadPrefixHex,
                entry.Field.ResponseField,
                entry.Field.ResponseId,
                entry.Field.PriceId,
                entry.Field.IsAbility
            })
            .OrderBy(group => group.Key.IsAbility ? 1 : 0)
            .ThenBy(group => group.Key.PriceId))
        {
            VendorPriceEntry price = pricesByGoId[group.Key.PriceId];
            uint expectedSell = CalculateVendorSellPrice(group.Key.ResponseId, price.VendorPrice, price.NonSellable);
            long[] cashDeltas = group.SelectMany(entry => entry.Transaction.CashDeltas).ToArray();
            uint[] sellValues = group.SelectMany(entry => entry.Transaction.SellValues).ToArray();
            uint[] valueKinds = group.SelectMany(entry => entry.Transaction.ValueKinds).ToArray();
            ushort[] sellAckItemValues = group
                .SelectMany(entry => entry.Transaction.SellAcks)
                .Select(ack => ack.SellItemValue)
                .ToArray();
            byte[] sellAckFlags = group
                .SelectMany(entry => entry.Transaction.SellAcks)
                .Select(ack => ack.Flag)
                .ToArray();
            string? referenceCodeName = null;
            string? referenceDisplayName = null;
            if (group.Key.IsAbility && abilitiesById.TryGetValue(group.Key.PriceId, out AbilityDefinition? ability))
            {
                referenceCodeName = ability.CodeName;
                referenceDisplayName = ability.DisplayName;
            }
            else if (!group.Key.IsAbility && itemsById.TryGetValue(group.Key.PriceId, out ItemCommandEntry? item))
            {
                referenceCodeName = item.Symbol;
                referenceDisplayName = item.DisplayName;
            }

            yield return new VendorSellPriceLeadSummary(
                group.Key.SellPayloadPrefixHex,
                group.Key.ResponseField,
                group.Key.ResponseId,
                group.Key.PriceId,
                group.Key.IsAbility,
                referenceCodeName,
                referenceDisplayName,
                price.VendorPrice,
                price.CodeName,
                expectedSell,
                FormatVendorSellPriceMatch(cashDeltas, sellValues, expectedSell),
                group.Count(),
                sellAckItemValues,
                sellAckFlags,
                cashDeltas,
                sellValues,
                valueKinds,
                vendorRowsByItem[group.Key.PriceId].Distinct().Count(),
                group.Select(entry => new VendorTransactionSampleLocation(
                        entry.File,
                        entry.Transaction.Line,
                        entry.Transaction.NextLine))
                    .Take(8)
                    .ToArray());
        }

        IEnumerable<VendorSellPriceField> EnumerateVendorSellPriceFields(VendorSellTransactionSample transaction)
        {
            if ((transaction.NormalItemId & AbilityCodeFlag) == 0 && pricesByGoId.ContainsKey(transaction.NormalItemId))
            {
                yield return new VendorSellPriceField("+1 normal item u32", transaction.NormalItemId, transaction.NormalItemId, false);
            }

            if ((transaction.AbilityId & AbilityCodeFlag) != 0 && pricesByGoId.ContainsKey(transaction.AbilityBaseId))
            {
                yield return new VendorSellPriceField("+5 ability code u32", transaction.AbilityId, transaction.AbilityBaseId, true);
            }
        }
    }

    private static string FormatVendorBuyAckDetail(VendorBuyAckSample ack)
    {
        if (HeaderEquals(ack.Header, "69") && !string.IsNullOrWhiteSpace(ack.SecondaryFieldHex))
        {
            return $"69 +5 {ack.SecondaryFieldHex}";
        }

        return ack.TailMarkerHex;
    }

    private static string FormatVendorBuyPriceMatch(
        IReadOnlyList<long> cashDeltas,
        IReadOnlyList<uint> buyValues,
        uint? vendorPrice)
    {
        if (vendorPrice is null)
        {
            return "unpriced";
        }

        uint expected = vendorPrice.Value;
        bool cashMatches = cashDeltas.Any(delta => delta < 0 && Math.Abs(delta) == expected);
        bool buyValueMatches = buyValues.Any(value => value == expected);
        if (cashMatches && buyValueMatches)
        {
            return "cash and buy value";
        }

        if (cashMatches)
        {
            return "cash";
        }

        if (buyValueMatches)
        {
            return "buy value";
        }

        return "no observed match";
    }

    private static string FormatVendorSellPriceMatch(
        IReadOnlyList<long> cashDeltas,
        IReadOnlyList<uint> sellValues,
        uint expectedSell)
    {
        bool cashMatches = cashDeltas.Any(delta => delta > 0 && delta == expectedSell);
        bool sellValueMatches = sellValues.Any(value => value == expectedSell);
        if (cashMatches && sellValueMatches)
        {
            return "cash and sell value";
        }

        if (cashMatches)
        {
            return "cash";
        }

        if (sellValueMatches)
        {
            return "sell value";
        }

        return "no observed match";
    }

    private static uint CalculateVendorSellPrice(uint responseId, uint vendorPrice, bool nonSellable)
    {
        if ((responseId & AbilityCodeFlag) == 0)
        {
            return nonSellable ? 0 : vendorPrice / 10;
        }

        uint abilityCode = responseId & AbilityCodeMask;
        uint level = responseId & AbilityLevelMask;
        ulong buyback = vendorPrice;
        for (uint i = 1; i < level; i++)
        {
            ulong nextLevel = i + 1;
            buyback += nextLevel * nextLevel * 200;
        }

        if (!FullPriceAbilityCodes.Contains(abilityCode))
        {
            buyback /= 10;
        }

        return buyback > uint.MaxValue ? uint.MaxValue : (uint)buyback;
    }

    public static IEnumerable<RpcHeaderEntry> LoadLocalHeaders(string repositoryRoot)
    {
        foreach (string path in Directory.EnumerateFiles(repositoryRoot, "NetworkProtocolHeaders.cs", SearchOption.AllDirectories))
        {
            string protocol = InferProtocol(path);
            foreach (RpcHeaderEntry entry in ParseNetworkHeaderText(File.ReadAllLines(path), path, protocol))
            {
                yield return entry;
            }
        }
    }

    public static IEnumerable<RpcHeaderEntry> ParseNetworkHeaderText(
        IReadOnlyList<string> lines,
        string file,
        string protocol)
    {
        string enumName = string.Empty;
        string direction = string.Empty;

        for (int i = 0; i < lines.Count; i++)
        {
            string line = StripComment(lines[i]);
            Match enumMatch = EnumStartRegex().Match(line);
            if (enumMatch.Success)
            {
                enumName = enumMatch.Groups["name"].Value;
                direction = enumName.Contains("Request", StringComparison.OrdinalIgnoreCase)
                    ? "client"
                    : enumName.Contains("Response", StringComparison.OrdinalIgnoreCase)
                        ? "server"
                        : "unknown";
                continue;
            }

            if (line.Contains('}') && enumName.Length > 0)
            {
                enumName = string.Empty;
                direction = string.Empty;
                continue;
            }

            if (enumName.Length == 0)
            {
                continue;
            }

            Match entryMatch = EnumEntryRegex().Match(line);
            if (!entryMatch.Success)
            {
                continue;
            }

            int value = ParseNumber(entryMatch.Groups["value"].Value);
            yield return new RpcHeaderEntry(
                "mxo-hd",
                protocol,
                direction,
                enumName,
                entryMatch.Groups["name"].Value,
                value,
                FormatHeader(EncodeHeader(value, direction)),
                file,
                i + 1);
        }
    }

    public static IEnumerable<RajkoRpcEntry> LoadRajkoRpcHeaders(string rajkoSourceRoot)
    {
        string playerObject = Path.Combine(rajkoSourceRoot, "Reality", "Source", "PlayerObject.cpp");
        if (!File.Exists(playerObject))
        {
            yield break;
        }

        foreach (RajkoRpcEntry entry in ParseRajkoRpcMapText(File.ReadAllLines(playerObject), playerObject))
        {
            yield return entry;
        }
    }

    public static IEnumerable<HardcodedCommandExample> LoadRajkoHardcodedCommands(string rajkoSourceRoot)
    {
        string sourceRoot = Path.Combine(rajkoSourceRoot, "Reality", "Source");
        if (!Directory.Exists(sourceRoot))
        {
            yield break;
        }

        foreach (string path in Directory.EnumerateFiles(sourceRoot, "*.cpp", SearchOption.AllDirectories))
        {
            foreach (HardcodedCommandExample entry in ParseHardcodedCommandText(
                File.ReadAllLines(path),
                path,
                "rajko",
                RajkoHexGenericMsgRegex()))
            {
                yield return entry;
            }
        }
    }

    public static IEnumerable<HardcodedCommandExample> LoadLocalHardcodedCommands(string repositoryRoot)
    {
        foreach (string path in Directory.EnumerateFiles(repositoryRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}tools{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}tests{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)))
        {
            foreach (HardcodedCommandExample entry in ParseHardcodedCommandText(
                File.ReadAllLines(path),
                RelativePath(path, repositoryRoot),
                "mxo-hd",
                LocalQueuedRpcHexRegex()))
            {
                yield return entry;
            }
        }
    }

    public static IEnumerable<HardcodedCommandExample> ParseRajkoHardcodedCommandText(IReadOnlyList<string> lines, string file)
    {
        return ParseHardcodedCommandText(lines, file, "rajko", RajkoHexGenericMsgRegex());
    }

    public static IEnumerable<HardcodedCommandExample> ParseLocalQueuedRpcCommandText(IReadOnlyList<string> lines, string file)
    {
        return ParseHardcodedCommandText(lines, file, "mxo-hd", LocalQueuedRpcHexRegex());
    }

    public static IEnumerable<Protocol03HardcodedExample> LoadExternalProtocol03HardcodedExamples(string sourceRoot, string source)
    {
        string sourcePath = Path.Combine(sourceRoot, "Reality", "Source");
        if (!Directory.Exists(sourcePath))
        {
            yield break;
        }

        foreach (string path in Directory.EnumerateFiles(sourcePath, "*.cpp", SearchOption.AllDirectories))
        {
            foreach (Protocol03HardcodedExample entry in ParseProtocol03HardcodedText(
                File.ReadAllLines(path),
                path,
                source,
                RajkoHexGenericMsgRegex()))
            {
                yield return entry;
            }
        }
    }

    public static IEnumerable<Protocol03HardcodedExample> LoadLocalProtocol03HardcodedExamples(string repositoryRoot)
    {
        foreach (string path in Directory.EnumerateFiles(repositoryRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}tools{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}tests{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)))
        {
            foreach (Protocol03HardcodedExample entry in ParseProtocol03HardcodedText(
                File.ReadAllLines(path),
                RelativePath(path, repositoryRoot),
                "mxo-hd",
                LocalHexStringRegex()))
            {
                yield return entry;
            }
        }
    }

    public static IEnumerable<Protocol03HardcodedExample> ParseExternalProtocol03HardcodedText(IReadOnlyList<string> lines, string file, string source)
    {
        return ParseProtocol03HardcodedText(lines, file, source, RajkoHexGenericMsgRegex());
    }

    public static IEnumerable<Protocol03HardcodedExample> ParseLocalProtocol03HardcodedText(IReadOnlyList<string> lines, string file)
    {
        return ParseProtocol03HardcodedText(lines, file, "mxo-hd", LocalHexStringRegex());
    }

    public static IEnumerable<Protocol04InteractionCommandExample> LoadExternalProtocol04InteractionCommandExamples(string sourceRoot, string source)
    {
        string sourcePath = Path.Combine(sourceRoot, "Reality", "Source");
        if (!Directory.Exists(sourcePath))
        {
            yield break;
        }

        foreach (string path in Directory.EnumerateFiles(sourcePath, "*", SearchOption.AllDirectories).Where(IsExternalSourceTextFile))
        {
            string displayPath = Path.GetRelativePath(sourceRoot, path);
            foreach (Protocol04InteractionCommandExample entry in ParseProtocol04InteractionCommandText(File.ReadAllLines(path), displayPath, source))
            {
                yield return entry;
            }
        }
    }

    public static IEnumerable<Protocol04InteractionCommandExample> ParseProtocol04InteractionCommandText(
        IReadOnlyList<string> lines,
        string file,
        string source)
    {
        for (int i = 0; i < lines.Count; i++)
        {
            foreach (Match match in HandleCommandRegex().Matches(lines[i]))
            {
                byte[] bytes = ParseHexBytes(match.Groups["hex"].Value).ToArray();
                if (TryParseInteractionCommand(bytes, source, file, i + 1, out Protocol04InteractionCommandExample? example))
                {
                    yield return example!;
                }
            }
        }
    }

    public static IEnumerable<VendorInventoryEntry> LoadVendorInventoryEntries(string repositoryRoot)
    {
        string path = Path.Combine(repositoryRoot, "data", "vendor_items.csv");
        if (!File.Exists(path))
        {
            yield break;
        }

        foreach (VendorInventoryEntry entry in ParseVendorInventoryText(File.ReadAllLines(path), RelativePath(path, repositoryRoot)))
        {
            yield return entry;
        }
    }

    public static IEnumerable<VendorPriceEntry> LoadVendorPriceEntries(string repositoryRoot)
    {
        string path = Path.Combine(repositoryRoot, "data", "vendor_prices.csv");
        if (!File.Exists(path))
        {
            yield break;
        }

        foreach (VendorPriceEntry entry in ParseVendorPriceText(File.ReadAllLines(path), RelativePath(path, repositoryRoot)))
        {
            yield return entry;
        }
    }

    public static IEnumerable<ItemCommandEntry> LoadItemCommandEntries(string repositoryRoot)
    {
        string[] paths =
        {
            Path.Combine(repositoryRoot, "research", "community-data", "invitems_command.txt"),
            Path.Combine(repositoryRoot, "data", "invitems_command.txt")
        };

        HashSet<uint> seen = new();
        foreach (string path in paths.Where(File.Exists).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            foreach (ItemCommandEntry entry in ParseItemCommandText(File.ReadAllLines(path), RelativePath(path, repositoryRoot)))
            {
                if (seen.Add(entry.ItemId))
                {
                    yield return entry;
                }
            }
        }
    }

    public static IEnumerable<ItemCommandEntry> ParseItemCommandText(IReadOnlyList<string> lines, string file)
    {
        for (int i = 0; i < lines.Count; i++)
        {
            Match match = ItemCommandRegex().Match(lines[i]);
            if (!match.Success
                || !uint.TryParse(match.Groups["id"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint itemId))
            {
                continue;
            }

            string body = match.Groups["body"].Value.Trim();
            string symbol = body;
            string displayName = string.Empty;
            int quotedNameStart = body.IndexOf('"', StringComparison.Ordinal);
            if (quotedNameStart >= 0 && body.EndsWith("\"", StringComparison.Ordinal))
            {
                symbol = body[..quotedNameStart].Trim();
                displayName = body[(quotedNameStart + 1)..^1].Trim();
            }

            if (symbol.Length == 0)
            {
                continue;
            }

            yield return new ItemCommandEntry(
                itemId,
                symbol,
                displayName.Length == 0 ? null : displayName,
                file,
                i + 1);
        }
    }

    public static IEnumerable<VendorPriceEntry> ParseVendorPriceText(IReadOnlyList<string> lines, string file)
    {
        for (int i = 0; i < lines.Count; i++)
        {
            string line = lines[i].Trim();
            if (line.Length == 0 || line.StartsWith("go_id,", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string[] columns = line.Split(',', StringSplitOptions.TrimEntries);
            if (columns.Length < 4
                || !uint.TryParse(columns[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out uint goId)
                || columns[1].Length == 0
                || !uint.TryParse(columns[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out uint vendorPrice)
                || !bool.TryParse(columns[3], out bool nonSellable))
            {
                continue;
            }

            yield return new VendorPriceEntry(goId, columns[1], vendorPrice, nonSellable, file, i + 1);
        }
    }

    public static IEnumerable<AbilityDefinition> LoadAbilityDefinitions(string repositoryRoot)
    {
        string[] paths =
        {
            Path.Combine(repositoryRoot, "research", "community-data", "abilities.txt"),
            Path.Combine(repositoryRoot, "data", "abilityIDs.csv")
        };

        HashSet<uint> seen = new();
        foreach (string path in paths.Where(File.Exists).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            foreach (AbilityDefinition entry in ParseAbilityDefinitionText(File.ReadAllLines(path), RelativePath(path, repositoryRoot)))
            {
                if (seen.Add(entry.AbilityId))
                {
                    yield return entry;
                }
            }
        }
    }

    public static IEnumerable<AbilityDefinition> ParseAbilityDefinitionText(IReadOnlyList<string> lines, string file)
    {
        for (int i = 0; i < lines.Count; i++)
        {
            string line = lines[i].Trim();
            if (line.Length == 0)
            {
                continue;
            }

            Match csvMatch = AbilityDefinitionRegex().Match(line);
            if (csvMatch.Success
                && uint.TryParse(csvMatch.Groups["id"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint abilityId))
            {
                string codeName = csvMatch.Groups["code"].Value.Trim();
                string? displayName = csvMatch.Groups["display"].Success
                    ? csvMatch.Groups["display"].Value.Replace("\"\"", "\"", StringComparison.Ordinal).Trim()
                    : NormalizeOptionalText(csvMatch.Groups["plain"].Value);

                if (codeName.Length > 0)
                {
                    yield return new AbilityDefinition(
                        abilityId,
                        unchecked((int)abilityId),
                        codeName,
                        displayName,
                        file,
                        i + 1);
                }

                continue;
            }

            if (!line.Contains(';', StringComparison.Ordinal)
                || line.StartsWith("AbilityID;", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string[] columns = line.Split(';');
            if (columns.Length < 3
                || !int.TryParse(columns[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int signedGoId))
            {
                continue;
            }

            string dataCodeName = columns[2].Trim();
            if (dataCodeName.Length == 0 || dataCodeName.Equals("None", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            yield return new AbilityDefinition(
                unchecked((uint)signedGoId),
                signedGoId,
                dataCodeName,
                null,
                file,
                i + 1);
        }
    }

    public static IEnumerable<VendorInventoryEntry> ParseVendorInventoryText(IReadOnlyList<string> lines, string file)
    {
        for (int i = 0; i < lines.Count; i++)
        {
            string line = lines[i];
            if (line.Length == 0 || line.StartsWith("metr_id;", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string[] columns = line.Split(';');
            if (columns.Length < 3
                || !ushort.TryParse(columns[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out ushort metrId)
                || !uint.TryParse(columns[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out uint vendorStaticId))
            {
                continue;
            }

            uint[] items = columns[2]
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(value => uint.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint item) ? item : 0)
                .Where(item => item != 0)
                .ToArray();
            if (items.Length == 0)
            {
                continue;
            }

            yield return new VendorInventoryEntry(metrId, vendorStaticId, items, file, i + 1);
        }
    }

    public static IEnumerable<StaticObjectEntry> LoadStaticObjectEntries(string repositoryRoot, IEnumerable<uint> objectIds)
    {
        string dataRoot = Path.Combine(repositoryRoot, "data");
        if (!Directory.Exists(dataRoot))
        {
            yield break;
        }

        HashSet<uint> wantedObjectIds = objectIds.ToHashSet();
        if (wantedObjectIds.Count == 0)
        {
            yield break;
        }

        foreach (string path in Directory.EnumerateFiles(dataRoot, "staticObjects_*.csv", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            foreach (StaticObjectEntry entry in ParseStaticObjectText(File.ReadLines(path), RelativePath(path, repositoryRoot), wantedObjectIds))
            {
                yield return entry;
            }
        }
    }

    private static IEnumerable<uint> FindProtocol03SelectorFfField9Candidates(IEnumerable<PacketDumpFileSummary> dumpSummaries)
    {
        foreach (Protocol03VariableSelectorLead lead in dumpSummaries
            .SelectMany(summary => summary.Protocol03ObjectViews)
            .SelectMany(view => view.VariableSelectorLeads)
            .Where(lead => lead.Selector.Equals("ff", StringComparison.OrdinalIgnoreCase)))
        {
            byte[] bytes = ParseHexBytes(lead.PayloadHex).ToArray();
            if (bytes.Length >= 13)
            {
                yield return BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(9, 4));
            }
        }
    }

    public static IEnumerable<Protocol03SelectorFfRepeatListCandidate> BuildProtocol03SelectorFfRepeatListCandidates(IEnumerable<PacketDumpFileSummary> dumpSummaries)
    {
        foreach (PacketDumpFileSummary summary in dumpSummaries)
        {
            foreach (Protocol03ObjectViewSample view in summary.Protocol03ObjectViews)
            {
                foreach (Protocol03VariableSelectorLead lead in view.VariableSelectorLeads.Where(lead => lead.Selector.Equals("ff", StringComparison.OrdinalIgnoreCase)))
                {
                    foreach (Protocol03SelectorFfRepeatListCandidate candidate in CreateProtocol03SelectorFfRepeatListCandidates(summary.File, view, lead))
                    {
                        yield return candidate;
                    }
                }
            }
        }
    }

    public static IEnumerable<Protocol03NpcBaseDynamicCreationTailSummary> BuildProtocol03NpcBaseDynamicCreationTailSummaries(
        IEnumerable<PacketDumpFileSummary> dumpSummaries)
    {
        Protocol03NpcBaseDynamicCreationObservation[] observations = EnumerateProtocol03NpcBaseDynamicCreationObservations(dumpSummaries)
            .Where(observation => !string.IsNullOrWhiteSpace(observation.Record.PostCreationObjectViewDynamicTailPrefixHex))
            .ToArray();

        return observations
            .GroupBy(observation => new
            {
                TailFamily = ClassifyProtocol03NpcBaseDynamicCreationTailPrefix(observation.Record.PostCreationObjectViewDynamicTailPrefixHex),
                observation.Record.PostCreationObjectViewComplete,
                observation.Record.PostCreationObjectViewParseStatus
            })
            .Select(group =>
            {
                Protocol03NpcBaseDynamicCreationObservation sample = group.First();
                return new Protocol03NpcBaseDynamicCreationTailSummary(
                    group.Key.TailFamily,
                    group.Key.PostCreationObjectViewComplete,
                    group.Key.PostCreationObjectViewParseStatus,
                    group.Count(),
                    CountProtocol03NpcBaseDynamicCreationSummaryValues(group.Select(entry => entry.Record.PostCreationObjectViewDynamicTailAvailableBytes.ToString(CultureInfo.InvariantCulture)), 12),
                    CountProtocol03NpcBaseDynamicCreationSummaryValues(group.Select(entry => ParseHexBytes(entry.Record.PostCreationObjectViewDynamicTailPrefixHex).Count().ToString(CultureInfo.InvariantCulture)), 12),
                    CountProtocol03NpcBaseDynamicCreationSummaryValues(group.Select(entry => FormatProtocol03NpcBaseDynamicCreationParentPreValue(entry.Record)), 12),
                    CountProtocol03NpcBaseDynamicCreationSummaryValues(group.Select(entry => FormatProtocol03NpcBaseDynamicCreationSummaryHex(entry.Record.PostCreationObjectViewDynamicTailPrefixHex)), 8),
                    CountProtocol03NpcBaseDynamicCreationSummaryValues(group.SelectMany(entry => EnumerateProtocol03NpcBaseDynamicTailHeaderLikeLeads(entry.Record.PostCreationObjectViewDynamicTailPrefixHex)), 8),
                    CountProtocol03NpcBaseDynamicCreationSummaryValues(group.Select(entry => entry.Record.Text), 8),
                    sample.File,
                    sample.Line);
            })
            .OrderBy(summary => summary.FollowUpComplete)
            .ThenByDescending(summary => summary.CandidateCount)
            .ThenBy(summary => summary.TailFamily, StringComparer.OrdinalIgnoreCase)
            .ThenBy(summary => summary.ParseStatus, StringComparer.OrdinalIgnoreCase);
    }

    public static IEnumerable<Protocol03NpcBaseDynamicCreationHeaderLikeLeadSummary> BuildProtocol03NpcBaseDynamicCreationHeaderLikeLeadSummaries(
        IEnumerable<PacketDumpFileSummary> dumpSummaries)
    {
        var leadEntries = EnumerateProtocol03NpcBaseDynamicCreationObservations(dumpSummaries)
            .Where(observation => !string.IsNullOrWhiteSpace(observation.Record.PostCreationObjectViewDynamicTailPrefixHex))
            .SelectMany(observation => EnumerateProtocol03NpcBaseDynamicTailHeaderLikeLeadInfos(observation.Record.PostCreationObjectViewDynamicTailPrefixHex)
                .Select(lead => new
                {
                    Observation = observation,
                    Lead = lead
                }))
            .ToArray();

        return leadEntries
            .GroupBy(entry => new
            {
                entry.Lead.Offset,
                entry.Lead.ViewId,
                entry.Lead.UpdateCount,
                entry.Lead.FirstSelector
            })
            .Select(group =>
            {
                var sample = group.First();
                return new Protocol03NpcBaseDynamicCreationHeaderLikeLeadSummary(
                    group.Key.Offset,
                    group.Key.ViewId,
                    group.Key.UpdateCount.ToString("x2", CultureInfo.InvariantCulture),
                    group.Key.FirstSelector.ToString("x2", CultureInfo.InvariantCulture),
                    group.Count(),
                    CountProtocol03NpcBaseDynamicCreationSummaryValues(group.Select(entry => FormatProtocol03NpcBaseDynamicCreationBool(entry.Observation.Record.PostCreationObjectViewComplete)), 8),
                    CountProtocol03NpcBaseDynamicCreationSummaryValues(group.Select(entry => entry.Observation.Record.PostCreationObjectViewDynamicTailAvailableBytes.ToString(CultureInfo.InvariantCulture)), 12),
                    CountProtocol03NpcBaseDynamicCreationSummaryValues(group.Select(entry => entry.Observation.Record.PostCreationObjectViewParseStatus), 8),
                    CountProtocol03NpcBaseDynamicCreationSummaryValues(group.Select(entry => ClassifyProtocol03NpcBaseDynamicCreationTailPrefix(entry.Observation.Record.PostCreationObjectViewDynamicTailPrefixHex)), 8),
                    CountProtocol03NpcBaseDynamicCreationSummaryValues(group.Select(entry => FormatProtocol03NpcBaseDynamicCreationParentPreValue(entry.Observation.Record)), 12),
                    CountProtocol03NpcBaseDynamicCreationSummaryValues(group.Select(entry => FormatProtocol03NpcBaseDynamicCreationSummaryHex(entry.Observation.Record.PostCreationObjectViewDynamicTailPrefixHex)), 8),
                    CountProtocol03NpcBaseDynamicCreationSummaryValues(group.Select(entry => FormatProtocol03NpcBaseDynamicTailBodyHints(entry.Observation.Record.PostCreationObjectViewDynamicTailPrefixHex, entry.Lead)), 8),
                    CountProtocol03NpcBaseDynamicCreationSummaryValues(group.Select(entry => entry.Observation.Record.Text), 8),
                    sample.Observation.File,
                    sample.Observation.Line);
            })
            .OrderByDescending(summary => summary.CandidateCount)
            .ThenBy(summary => summary.LeadOffset)
            .ThenBy(summary => summary.ViewId)
            .ThenBy(summary => summary.UpdateCountHex, StringComparer.OrdinalIgnoreCase)
            .ThenBy(summary => summary.FirstSelector, StringComparer.OrdinalIgnoreCase);
    }

    public static IEnumerable<Protocol03NpcBaseDynamicCreationBodyAnchorSummary> BuildProtocol03NpcBaseDynamicCreationBodyAnchorSummaries(
        IEnumerable<PacketDumpFileSummary> dumpSummaries)
    {
        Protocol03NpcBaseDynamicCreationBodyAnchor[] anchors = EnumerateProtocol03NpcBaseDynamicCreationObservations(dumpSummaries)
            .Where(observation => !string.IsNullOrWhiteSpace(observation.Record.PostCreationObjectViewDynamicTailPrefixHex))
            .SelectMany(observation => EnumerateProtocol03NpcBaseDynamicTailHeaderLikeLeadInfos(observation.Record.PostCreationObjectViewDynamicTailPrefixHex)
                .Select(lead => TryCreateProtocol03NpcBaseDynamicTailBodyAnchor(observation, lead, out Protocol03NpcBaseDynamicCreationBodyAnchor? anchor)
                    ? anchor
                    : null)
                .Where(anchor => anchor is not null)
                .Select(anchor => anchor!))
            .ToArray();

        return anchors
            .GroupBy(anchor => new
            {
                anchor.Lead.Offset,
                anchor.Lead.ViewId,
                anchor.Lead.UpdateCount,
                anchor.Lead.FirstSelector,
                anchor.BodySeparatorOffset
            })
            .Select(group =>
            {
                Protocol03NpcBaseDynamicCreationBodyAnchor sample = group.First();
                return new Protocol03NpcBaseDynamicCreationBodyAnchorSummary(
                    group.Key.Offset,
                    group.Key.ViewId,
                    group.Key.UpdateCount.ToString("x2", CultureInfo.InvariantCulture),
                    group.Key.FirstSelector.ToString("x2", CultureInfo.InvariantCulture),
                    group.Key.BodySeparatorOffset,
                    group.Count(),
                    CountProtocol03NpcBaseDynamicCreationSummaryValues(group.Select(anchor => FormatProtocol03NpcBaseDynamicCreationBool(anchor.Observation.Record.PostCreationObjectViewComplete)), 8),
                    CountProtocol03NpcBaseDynamicCreationSummaryValues(group.Select(anchor => anchor.Observation.Record.PostCreationObjectViewDynamicTailAvailableBytes.ToString(CultureInfo.InvariantCulture)), 12),
                    CountProtocol03NpcBaseDynamicCreationSummaryValues(group.Select(anchor => anchor.Observation.Record.PostCreationObjectViewParseStatus), 8),
                    CountProtocol03NpcBaseDynamicCreationSummaryValues(group.Select(anchor => FormatProtocol03NpcBaseDynamicCreationSummaryHex(anchor.BodyPrefixHex)), 8),
                    CountProtocol03NpcBaseDynamicCreationSummaryValues(group.Select(anchor => FormatProtocol03NpcBaseDynamicCreationSummaryHex(anchor.PostSeparatorPrefixHex)), 8),
                    CountProtocol03NpcBaseDynamicCreationSummaryValues(group.Select(anchor => anchor.Observation.Record.Text), 8),
                    sample.Observation.File,
                    sample.Observation.Line);
            })
            .OrderByDescending(summary => summary.CandidateCount)
            .ThenBy(summary => summary.LeadOffset)
            .ThenBy(summary => summary.ViewId)
            .ThenBy(summary => summary.UpdateCountHex, StringComparer.OrdinalIgnoreCase)
            .ThenBy(summary => summary.FirstSelector, StringComparer.OrdinalIgnoreCase)
            .ThenBy(summary => summary.BodySeparatorOffset);
    }

    public static IEnumerable<Protocol03NpcBaseDynamicCreationExtendedBoundarySummary> BuildProtocol03NpcBaseDynamicCreationExtendedBoundarySummaries(
        IEnumerable<PacketDumpFileSummary> dumpSummaries)
    {
        Protocol03NpcBaseDynamicCreationObservation[] observations = EnumerateProtocol03NpcBaseDynamicCreationObservations(dumpSummaries)
            .Where(observation =>
                !observation.Record.PostCreationObjectViewComplete &&
                observation.Record.PostCreationObjectViewParseStatus.Equals("dynamic creation parsed; next boundary not found", StringComparison.OrdinalIgnoreCase) &&
                observation.Record.PostCreationObjectViewDynamicTailNextHeaderOffset is not null)
            .ToArray();

        return observations
            .GroupBy(observation => new
            {
                TailHeaderOffset = observation.Record.PostCreationObjectViewDynamicTailNextHeaderOffset!.Value,
                PreHeaderHex = FormatProtocol03NpcBaseDynamicCreationSummaryHex(observation.Record.PostCreationObjectViewDynamicTailPreHeaderHex),
                HeaderPrefixHex = FormatProtocol03NpcBaseDynamicCreationSummaryHex(observation.Record.PostCreationObjectViewDynamicTailNextHeaderHex),
                observation.Record.PostCreationObjectViewDynamicTailNextHeaderClassification
            })
            .Select(group =>
            {
                Protocol03NpcBaseDynamicCreationObservation sample = group.First();
                string boundaryInterpretation = ClassifyProtocol03NpcBaseDynamicCreationExtendedBoundaryCandidate(
                    group.Key.PreHeaderHex,
                    group.Key.HeaderPrefixHex);
                return new Protocol03NpcBaseDynamicCreationExtendedBoundarySummary(
                    group.Key.TailHeaderOffset,
                    group.Key.PreHeaderHex,
                    group.Key.HeaderPrefixHex,
                    group.Key.PostCreationObjectViewDynamicTailNextHeaderClassification,
                    boundaryInterpretation,
                    ClassifyProtocol03NpcBaseDynamicCreationExtendedBoundaryParserAction(
                        boundaryInterpretation,
                        group.Key.PostCreationObjectViewDynamicTailNextHeaderClassification,
                        group.Count()),
                    group.Count(),
                    CountProtocol03NpcBaseDynamicCreationSummaryValues(group.Select(entry => entry.Record.PostCreationObjectViewDynamicTailAvailableBytes.ToString(CultureInfo.InvariantCulture)), 12),
                    CountProtocol03NpcBaseDynamicCreationSummaryValues(group.Select(entry => ClassifyProtocol03NpcBaseDynamicCreationTailPrefix(entry.Record.PostCreationObjectViewDynamicTailPrefixHex)), 8),
                    CountProtocol03NpcBaseDynamicCreationSummaryValues(group.Select(entry => FormatProtocol03NpcBaseDynamicCreationParentPreValue(entry.Record)), 12),
                    CountProtocol03NpcBaseDynamicCreationSummaryValues(group.Select(entry => entry.Record.Text), 8),
                    sample.File,
                    sample.Line);
            })
            .OrderByDescending(summary => summary.CandidateCount)
            .ThenBy(summary => summary.TailHeaderOffset)
            .ThenBy(summary => summary.PreHeaderHex, StringComparer.OrdinalIgnoreCase)
            .ThenBy(summary => summary.HeaderPrefixHex, StringComparer.OrdinalIgnoreCase);
    }

    private static string ClassifyProtocol03NpcBaseDynamicCreationExtendedBoundaryCandidate(
        string preHeaderHex,
        string headerPrefixHex)
    {
        if (headerPrefixHex.Equals("cd ab", StringComparison.OrdinalIgnoreCase) ||
            headerPrefixHex.StartsWith("cd ab ", StringComparison.OrdinalIgnoreCase))
        {
            return "cd ab separator collision";
        }

        if (preHeaderHex.Contains("cd ab", StringComparison.OrdinalIgnoreCase))
        {
            return "post-separator later header candidate";
        }

        return "later header candidate";
    }

    private static string ClassifyProtocol03NpcBaseDynamicCreationExtendedBoundaryParserAction(
        string boundaryInterpretation,
        string headerClassification,
        int candidateCount)
    {
        if (boundaryInterpretation.Equals("cd ab separator collision", StringComparison.OrdinalIgnoreCase))
        {
            return "reject as separator collision";
        }

        if (boundaryInterpretation.Equals("post-separator later header candidate", StringComparison.OrdinalIgnoreCase))
        {
            return "evidence only; post-separator lead";
        }

        if (candidateCount >= 5 &&
            !headerClassification.Equals("unclassified object-view update", StringComparison.OrdinalIgnoreCase))
        {
            return "review for parser promotion";
        }

        return candidateCount > 1
            ? "evidence only; repeated later lead"
            : "evidence only; single-sample lead";
    }

    public static IEnumerable<Protocol03SelectorFfRepeatListFieldSummary> BuildProtocol03SelectorFfRepeatListFieldSummaries(
        IEnumerable<Protocol03SelectorFfRepeatListCandidate> candidates,
        IReadOnlyList<AbilityDefinition>? abilities = null,
        IReadOnlyList<ItemCommandEntry>? items = null,
        IReadOnlyList<GameObjectEntry>? gameObjects = null,
        IReadOnlyList<AnimationDefinition>? animations = null)
    {
        IReadOnlyDictionary<int, IReadOnlyList<string>> resourceReferencesByValue = BuildProtocol03SelectorFfRepeatListFieldResourceReferenceIndex(
            abilities ?? Array.Empty<AbilityDefinition>(),
            items ?? Array.Empty<ItemCommandEntry>(),
            gameObjects ?? Array.Empty<GameObjectEntry>(),
            animations ?? Array.Empty<AnimationDefinition>());

        Protocol03SelectorFfRepeatListFieldObservation[] observations = candidates
            .Where(candidate =>
                candidate.RepeatRecordCount == 2 &&
                candidate.ContinuationOffset == candidate.RepeatStride * 2 &&
                candidate.ContinuationMarkerHex.Equals("ff", StringComparison.OrdinalIgnoreCase))
            .SelectMany(candidate => new[]
            {
                new Protocol03SelectorFfRepeatListFieldObservation(
                    "entry 1",
                    1,
                    candidate.Entry1ZeroRun,
                    candidate.Entry1FieldOffset,
                    candidate.Entry1FieldHex,
                    candidate.Entry1FieldValue,
                    candidate.Entry1FieldLayoutKind,
                    candidate.Entry1LeadHex,
                    candidate),
                new Protocol03SelectorFfRepeatListFieldObservation(
                    "entry 2",
                    2,
                    candidate.Entry2ZeroRun,
                    candidate.Entry2FieldOffset,
                    candidate.Entry2FieldHex,
                    candidate.Entry2FieldValue,
                    candidate.Entry2FieldLayoutKind,
                    candidate.Entry2LeadHex,
                    candidate),
                new Protocol03SelectorFfRepeatListFieldObservation(
                    "entry 2 effective",
                    3,
                    candidate.Entry2ZeroRun,
                    candidate.Entry2EffectiveFieldOffset,
                    candidate.Entry2EffectiveFieldHex,
                    candidate.Entry2EffectiveFieldValue,
                    candidate.Entry2EffectiveFieldSource.Equals("secondary field", StringComparison.OrdinalIgnoreCase)
                        ? "effective secondary entry u16"
                        : $"effective {candidate.Entry2FieldLayoutKind}",
                    candidate.Entry2LeadHex,
                    candidate),
                new Protocol03SelectorFfRepeatListFieldObservation(
                    "post-marker",
                    4,
                    candidate.PostMarkerZeroRun,
                    candidate.PostMarkerFirstFieldOffset,
                    candidate.PostMarkerFirstFieldHex,
                    candidate.PostMarkerFirstFieldValue,
                    candidate.PostMarkerFieldLayoutKind,
                    candidate.PostMarkerLeadHex,
                    candidate)
            })
            .ToArray();

        return observations
            .GroupBy(observation => new
            {
                observation.FieldRole,
                observation.RoleOrder,
                observation.ZeroRun,
                observation.FieldOffset,
                observation.FieldHex,
                observation.FieldValue,
                observation.FieldLayoutKind
            })
            .Select(group =>
            {
                Protocol03SelectorFfRepeatListFieldObservation sample = group.First();
                return new Protocol03SelectorFfRepeatListFieldSummary(
                    group.Key.FieldRole,
                    group.Key.RoleOrder,
                    group.Key.ZeroRun,
                    group.Key.FieldOffset,
                    group.Key.FieldHex,
                    group.Key.FieldValue,
                    group.Key.FieldLayoutKind,
                    group.Select(observation => observation.LeadHex)
                        .Where(hex => !string.IsNullOrWhiteSpace(hex))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(hex => hex, StringComparer.OrdinalIgnoreCase)
                        .Take(12)
                        .ToArray(),
                    group.Count(),
                    CountProtocol03SelectorFfRepeatListFieldSummaryValues(group.Select(observation => observation.Candidate.UpdateCount.ToString(CultureInfo.InvariantCulture)), 12),
                    CountProtocol03SelectorFfRepeatListFieldSummaryValues(group.Select(observation => observation.Candidate.FirstSelector), 12),
                    CountProtocol03SelectorFfRepeatListFieldSummaryValues(group.Select(observation => observation.Candidate.ContinuationPrefixHex), 12),
                    CountProtocol03SelectorFfRepeatListFieldSummaryValues(group.Select(observation => observation.Candidate.SegmentClassification), 12),
                    FindProtocol03SelectorFfRepeatListFieldResourceReferences(group.Key.FieldValue, resourceReferencesByValue),
                    sample.Candidate.File,
                    sample.Candidate.Line,
                    sample.Candidate.PositionOffset,
                    sample.Candidate.PositionHex,
                    sample.Candidate.X,
                    sample.Candidate.Y,
                    sample.Candidate.Z);
            })
            .OrderBy(summary => summary.RoleOrder)
            .ThenByDescending(summary => summary.CandidateCount)
            .ThenBy(summary => summary.ZeroRun)
            .ThenBy(summary => summary.FieldOffset ?? int.MaxValue)
            .ThenBy(summary => summary.FieldHex, StringComparer.OrdinalIgnoreCase);
    }

    public static IEnumerable<Protocol03SelectorFfRepeatListShapeSummary> BuildProtocol03SelectorFfRepeatListShapeSummaries(
        IEnumerable<Protocol03SelectorFfRepeatListCandidate> candidates,
        IReadOnlyList<AbilityDefinition>? abilities = null,
        IReadOnlyList<ItemCommandEntry>? items = null,
        IReadOnlyList<GameObjectEntry>? gameObjects = null,
        IReadOnlyList<AnimationDefinition>? animations = null)
    {
        IReadOnlyDictionary<int, IReadOnlyList<string>> resourceReferencesByValue = BuildProtocol03SelectorFfRepeatListFieldResourceReferenceIndex(
            abilities ?? Array.Empty<AbilityDefinition>(),
            items ?? Array.Empty<ItemCommandEntry>(),
            gameObjects ?? Array.Empty<GameObjectEntry>(),
            animations ?? Array.Empty<AnimationDefinition>());

        return candidates
            .Where(candidate =>
                candidate.RepeatRecordCount == 2 &&
                candidate.ContinuationOffset == candidate.RepeatStride * 2 &&
                candidate.ContinuationMarkerHex.Equals("ff", StringComparison.OrdinalIgnoreCase))
            .GroupBy(candidate => new
            {
                candidate.RepeatStride,
                candidate.RepeatRecordCount,
                candidate.ContinuationOffset,
                candidate.ContinuationPrefixHex,
                candidate.ContinuationPrefixBeforeMarkerHex,
                candidate.ContinuationMarkerHex,
                candidate.Entry1ZeroRun,
                candidate.Entry1FieldOffset,
                candidate.Entry1FieldHex,
                candidate.Entry1FieldValue,
                candidate.Entry1FieldLayoutKind,
                candidate.Entry2ZeroRun,
                candidate.Entry2FieldOffset,
                candidate.Entry2FieldHex,
                candidate.Entry2FieldValue,
                candidate.Entry2FieldLayoutKind,
                candidate.PostMarkerZeroRun,
                candidate.PostMarkerFirstFieldOffset,
                candidate.PostMarkerFirstFieldHex,
                candidate.PostMarkerFirstFieldValue,
                candidate.PostMarkerFieldLayoutKind
            })
            .Select(group =>
            {
                Protocol03SelectorFfRepeatListCandidate sample = group.First();
                return new Protocol03SelectorFfRepeatListShapeSummary(
                    group.Key.RepeatStride,
                    group.Key.RepeatRecordCount,
                    group.Key.ContinuationOffset,
                    group.Key.ContinuationPrefixHex,
                    group.Key.ContinuationPrefixBeforeMarkerHex,
                    group.Key.ContinuationMarkerHex,
                    group.Key.Entry1ZeroRun,
                    group.Key.Entry1FieldOffset,
                    group.Key.Entry1FieldHex,
                    group.Key.Entry1FieldValue,
                    group.Key.Entry1FieldLayoutKind,
                    CountProtocol03SelectorFfRepeatListWeightedValues(
                        group.Select(candidate => new WeightedValue(
                            FormatProtocol03SelectorFfRepeatListOptionalFieldLabel(candidate.Entry1SecondaryFieldHex, candidate.Entry1SecondaryFieldValue),
                            1)),
                        12),
                    CountProtocol03SelectorFfRepeatListWeightedValues(
                        group.Select(candidate => new WeightedValue(
                            FormatProtocol03SelectorFfRepeatListFieldLabel(candidate.Entry1EffectiveFieldHex, candidate.Entry1EffectiveFieldValue),
                            1)),
                        12),
                    FindProtocol03SelectorFfRepeatListFieldResourceReferences(group.Key.Entry1FieldValue, resourceReferencesByValue),
                    group.Key.Entry2ZeroRun,
                    group.Key.Entry2FieldOffset,
                    group.Key.Entry2FieldHex,
                    group.Key.Entry2FieldValue,
                    group.Key.Entry2FieldLayoutKind,
                    CountProtocol03SelectorFfRepeatListWeightedValues(
                        group.Select(candidate => new WeightedValue(
                            FormatProtocol03SelectorFfRepeatListOptionalFieldLabel(candidate.Entry2SecondaryFieldHex, candidate.Entry2SecondaryFieldValue),
                            1)),
                        12),
                    CountProtocol03SelectorFfRepeatListWeightedValues(
                        group.Select(candidate => new WeightedValue(
                            FormatProtocol03SelectorFfRepeatListFieldLabel(candidate.Entry2EffectiveFieldHex, candidate.Entry2EffectiveFieldValue),
                            1)),
                        12),
                    FindProtocol03SelectorFfRepeatListFieldResourceReferences(group.Key.Entry2FieldValue, resourceReferencesByValue),
                    group.Key.PostMarkerZeroRun,
                    group.Key.PostMarkerFirstFieldOffset,
                    group.Key.PostMarkerFirstFieldHex,
                    group.Key.PostMarkerFirstFieldValue,
                    group.Key.PostMarkerFieldLayoutKind,
                    FindProtocol03SelectorFfRepeatListFieldResourceReferences(group.Key.PostMarkerFirstFieldValue, resourceReferencesByValue),
                    group.Count(),
                    CountProtocol03SelectorFfRepeatListFieldSummaryValues(group.Select(candidate => candidate.UpdateCount.ToString(CultureInfo.InvariantCulture)), 12),
                    CountProtocol03SelectorFfRepeatListFieldSummaryValues(group.Select(candidate => candidate.FirstSelector), 12),
                    CountProtocol03SelectorFfRepeatListFieldSummaryValues(group.Select(candidate => candidate.SegmentClassification), 12),
                    group
                        .GroupBy(candidate => new { candidate.File, candidate.Line, candidate.PositionOffset })
                        .Select(positionGroup => positionGroup.First())
                        .OrderBy(candidate => candidate.File, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(candidate => candidate.Line)
                        .ThenBy(candidate => candidate.PositionOffset)
                        .Take(5)
                        .Select(candidate => new Protocol03SelectorFfRepeatListShapePositionSample(
                            candidate.File,
                            candidate.Line,
                            candidate.PositionOffset,
                            candidate.PositionHex,
                            candidate.X,
                            candidate.Y,
                            candidate.Z))
                        .ToArray(),
                    sample.File,
                    sample.Line);
            })
            .OrderByDescending(summary => summary.CandidateCount)
            .ThenBy(summary => summary.Entry1ZeroRun)
            .ThenBy(summary => summary.Entry1FieldOffset ?? int.MaxValue)
            .ThenBy(summary => summary.Entry1FieldHex, StringComparer.OrdinalIgnoreCase)
            .ThenBy(summary => summary.Entry2ZeroRun)
            .ThenBy(summary => summary.Entry2FieldOffset ?? int.MaxValue)
            .ThenBy(summary => summary.Entry2FieldHex, StringComparer.OrdinalIgnoreCase)
            .ThenBy(summary => summary.ContinuationPrefixHex, StringComparer.OrdinalIgnoreCase)
            .ThenBy(summary => summary.PostMarkerZeroRun)
            .ThenBy(summary => summary.PostMarkerFirstFieldOffset ?? int.MaxValue)
            .ThenBy(summary => summary.PostMarkerFirstFieldHex, StringComparer.OrdinalIgnoreCase);
    }

    public static IEnumerable<Protocol03SelectorFfRepeatListLayoutSummary> BuildProtocol03SelectorFfRepeatListLayoutSummaries(
        IEnumerable<Protocol03SelectorFfRepeatListShapeSummary> shapeSummaries)
    {
        return shapeSummaries
            .GroupBy(summary => new
            {
                summary.Entry1FieldLayoutKind,
                summary.Entry2FieldLayoutKind,
                summary.ContinuationPrefixHex,
                summary.ContinuationPrefixBeforeMarkerHex,
                summary.ContinuationMarkerHex,
                summary.PostMarkerFieldLayoutKind
            })
            .Select(group =>
            {
                Protocol03SelectorFfRepeatListShapeSummary sample = group.First();
                return new Protocol03SelectorFfRepeatListLayoutSummary(
                    group.Key.Entry1FieldLayoutKind,
                    group.Key.Entry2FieldLayoutKind,
                    group.Key.ContinuationPrefixHex,
                    group.Key.ContinuationPrefixBeforeMarkerHex,
                    group.Key.ContinuationMarkerHex,
                    group.Key.PostMarkerFieldLayoutKind,
                    group.Sum(summary => summary.CandidateCount),
                    group.Count(),
                    CountProtocol03SelectorFfRepeatListWeightedValues(
                        group.Select(summary => new WeightedValue(
                            FormatProtocol03SelectorFfRepeatListFieldLabel(summary.Entry1FieldHex, summary.Entry1FieldValue),
                            summary.CandidateCount)),
                        12),
                    MergeProtocol03SelectorFfRepeatListCountDictionaries(group.Select(summary => summary.Entry1SecondaryFields), 12),
                    MergeProtocol03SelectorFfRepeatListCountDictionaries(group.Select(summary => summary.Entry1EffectiveFields), 12),
                    CountProtocol03SelectorFfRepeatListWeightedValues(
                        group.Select(summary => new WeightedValue(
                            FormatProtocol03SelectorFfRepeatListFieldLabel(summary.Entry2FieldHex, summary.Entry2FieldValue),
                            summary.CandidateCount)),
                        12),
                    MergeProtocol03SelectorFfRepeatListCountDictionaries(group.Select(summary => summary.Entry2SecondaryFields), 12),
                    MergeProtocol03SelectorFfRepeatListCountDictionaries(group.Select(summary => summary.Entry2EffectiveFields), 12),
                    CountProtocol03SelectorFfRepeatListWeightedValues(
                        group.Select(summary => new WeightedValue(
                            FormatProtocol03SelectorFfRepeatListFieldLabel(summary.PostMarkerFirstFieldHex, summary.PostMarkerFirstFieldValue),
                            summary.CandidateCount)),
                        12),
                    MergeProtocol03SelectorFfRepeatListCountDictionaries(group.Select(summary => summary.FirstSelectors), 12),
                    MergeProtocol03SelectorFfRepeatListCountDictionaries(group.Select(summary => summary.SegmentClassifications), 12),
                    sample.SampleFile,
                    sample.SampleLine);
            })
            .OrderByDescending(summary => summary.CandidateCount)
            .ThenBy(summary => summary.Entry1FieldLayoutKind, StringComparer.OrdinalIgnoreCase)
            .ThenBy(summary => summary.Entry2FieldLayoutKind, StringComparer.OrdinalIgnoreCase)
            .ThenBy(summary => summary.ContinuationPrefixHex, StringComparer.OrdinalIgnoreCase)
            .ThenBy(summary => summary.PostMarkerFieldLayoutKind, StringComparer.OrdinalIgnoreCase);
    }

    public static IEnumerable<Protocol03SelectorFfRepeatListEffectiveShapeSummary> BuildProtocol03SelectorFfRepeatListEffectiveShapeSummaries(
        IEnumerable<Protocol03SelectorFfRepeatListCandidate> candidates,
        IReadOnlyList<AbilityDefinition>? abilities = null,
        IReadOnlyList<ItemCommandEntry>? items = null,
        IReadOnlyList<GameObjectEntry>? gameObjects = null,
        IReadOnlyList<AnimationDefinition>? animations = null)
    {
        IReadOnlyDictionary<int, IReadOnlyList<string>> resourceReferencesByValue = BuildProtocol03SelectorFfRepeatListFieldResourceReferenceIndex(
            abilities ?? Array.Empty<AbilityDefinition>(),
            items ?? Array.Empty<ItemCommandEntry>(),
            gameObjects ?? Array.Empty<GameObjectEntry>(),
            animations ?? Array.Empty<AnimationDefinition>());

        return candidates
            .Where(candidate =>
                candidate.RepeatRecordCount == 2 &&
                candidate.ContinuationOffset == candidate.RepeatStride * 2 &&
                candidate.ContinuationMarkerHex.Equals("ff", StringComparison.OrdinalIgnoreCase))
            .GroupBy(candidate => new
            {
                candidate.RepeatStride,
                candidate.RepeatRecordCount,
                candidate.ContinuationOffset,
                candidate.ContinuationPrefixHex,
                candidate.ContinuationPrefixBeforeMarkerHex,
                candidate.ContinuationMarkerHex,
                candidate.Entry1ZeroRun,
                candidate.Entry1FieldOffset,
                candidate.Entry1FieldHex,
                candidate.Entry1FieldValue,
                candidate.Entry1FieldLayoutKind,
                candidate.Entry2EffectiveFieldHex,
                candidate.Entry2EffectiveFieldValue,
                candidate.PostMarkerZeroRun,
                candidate.PostMarkerFirstFieldOffset,
                candidate.PostMarkerFirstFieldHex,
                candidate.PostMarkerFirstFieldValue,
                candidate.PostMarkerFieldLayoutKind
            })
            .Select(group =>
            {
                Protocol03SelectorFfRepeatListCandidate sample = group.First();
                return new Protocol03SelectorFfRepeatListEffectiveShapeSummary(
                    group.Key.RepeatStride,
                    group.Key.RepeatRecordCount,
                    group.Key.ContinuationOffset,
                    group.Key.ContinuationPrefixHex,
                    group.Key.ContinuationPrefixBeforeMarkerHex,
                    group.Key.ContinuationMarkerHex,
                    group.Key.Entry1ZeroRun,
                    group.Key.Entry1FieldOffset,
                    group.Key.Entry1FieldHex,
                    group.Key.Entry1FieldValue,
                    group.Key.Entry1FieldLayoutKind,
                    FindProtocol03SelectorFfRepeatListFieldResourceReferences(group.Key.Entry1FieldValue, resourceReferencesByValue),
                    group.Key.Entry2EffectiveFieldHex,
                    group.Key.Entry2EffectiveFieldValue,
                    FindProtocol03SelectorFfRepeatListFieldResourceReferences(group.Key.Entry2EffectiveFieldValue, resourceReferencesByValue),
                    group.Key.PostMarkerZeroRun,
                    group.Key.PostMarkerFirstFieldOffset,
                    group.Key.PostMarkerFirstFieldHex,
                    group.Key.PostMarkerFirstFieldValue,
                    group.Key.PostMarkerFieldLayoutKind,
                    FindProtocol03SelectorFfRepeatListFieldResourceReferences(group.Key.PostMarkerFirstFieldValue, resourceReferencesByValue),
                    group.Count(),
                    CountProtocol03SelectorFfRepeatListFieldSummaryValues(group.Select(candidate => candidate.Entry2EffectiveFieldSource), 12),
                    CountProtocol03SelectorFfRepeatListWeightedValues(
                        group.Select(candidate => new WeightedValue(
                            FormatProtocol03SelectorFfRepeatListFieldLabel(candidate.Entry2FieldHex, candidate.Entry2FieldValue),
                            1)),
                        12),
                    CountProtocol03SelectorFfRepeatListWeightedValues(
                        group.Select(candidate => new WeightedValue(
                            FormatProtocol03SelectorFfRepeatListOptionalFieldLabel(candidate.Entry2SecondaryFieldHex, candidate.Entry2SecondaryFieldValue),
                            1)),
                        12),
                    CountProtocol03SelectorFfRepeatListFieldSummaryValues(group.Select(candidate => candidate.Entry2FieldLayoutKind), 12),
                    CountProtocol03SelectorFfRepeatListFieldSummaryValues(group.Select(candidate => candidate.UpdateCount.ToString(CultureInfo.InvariantCulture)), 12),
                    CountProtocol03SelectorFfRepeatListFieldSummaryValues(group.Select(candidate => candidate.FirstSelector), 12),
                    CountProtocol03SelectorFfRepeatListFieldSummaryValues(group.Select(candidate => candidate.SegmentClassification), 12),
                    group
                        .GroupBy(candidate => new { candidate.File, candidate.Line, candidate.PositionOffset })
                        .Select(positionGroup => positionGroup.First())
                        .OrderBy(candidate => candidate.File, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(candidate => candidate.Line)
                        .ThenBy(candidate => candidate.PositionOffset)
                        .Take(5)
                        .Select(candidate => new Protocol03SelectorFfRepeatListShapePositionSample(
                            candidate.File,
                            candidate.Line,
                            candidate.PositionOffset,
                            candidate.PositionHex,
                            candidate.X,
                            candidate.Y,
                            candidate.Z))
                        .ToArray(),
                    sample.File,
                    sample.Line);
            })
            .OrderByDescending(summary => summary.CandidateCount)
            .ThenBy(summary => summary.Entry1ZeroRun)
            .ThenBy(summary => summary.Entry1FieldOffset ?? int.MaxValue)
            .ThenBy(summary => summary.Entry1FieldHex, StringComparer.OrdinalIgnoreCase)
            .ThenBy(summary => summary.Entry2EffectiveFieldHex, StringComparer.OrdinalIgnoreCase)
            .ThenBy(summary => summary.ContinuationPrefixHex, StringComparer.OrdinalIgnoreCase)
            .ThenBy(summary => summary.PostMarkerZeroRun)
            .ThenBy(summary => summary.PostMarkerFirstFieldOffset ?? int.MaxValue)
            .ThenBy(summary => summary.PostMarkerFirstFieldHex, StringComparer.OrdinalIgnoreCase);
    }

    public static IEnumerable<Protocol03SelectorFfRepeatListContinuationMarkerSummary> BuildProtocol03SelectorFfRepeatListContinuationMarkerSummaries(
        IEnumerable<Protocol03SelectorFfRepeatListCandidate> candidates,
        IEnumerable<PacketDumpFileSummary>? dumpSummaries = null,
        IReadOnlyList<RpcHeaderEntry>? localHeaders = null,
        IReadOnlyList<AbilityDefinition>? abilities = null,
        IReadOnlyList<ItemCommandEntry>? items = null,
        IReadOnlyList<GameObjectEntry>? gameObjects = null,
        IReadOnlyList<AnimationDefinition>? animations = null)
    {
        IReadOnlyDictionary<int, IReadOnlyList<string>> resourceReferencesByValue = BuildProtocol03SelectorFfRepeatListFieldResourceReferenceIndex(
            abilities ?? Array.Empty<AbilityDefinition>(),
            items ?? Array.Empty<ItemCommandEntry>(),
            gameObjects ?? Array.Empty<GameObjectEntry>(),
            animations ?? Array.Empty<AnimationDefinition>());

        Protocol03SelectorFfRepeatListCandidate[] filteredCandidates = candidates
            .Where(candidate =>
                candidate.RepeatRecordCount == 2 &&
                candidate.ContinuationOffset == candidate.RepeatStride * 2 &&
                !string.IsNullOrWhiteSpace(candidate.ContinuationMarkerHex))
            .ToArray();
        ILookup<string, Protocol03SelectorFfRepeatListHeaderOccurrence> headerOccurrencesByCandidate = Array.Empty<Protocol03SelectorFfRepeatListHeaderOccurrence>()
            .ToLookup(_ => string.Empty, StringComparer.OrdinalIgnoreCase);
        if (dumpSummaries is not null && localHeaders is not null)
        {
            headerOccurrencesByCandidate = BuildProtocol03SelectorFfRepeatListHeaderOccurrences(
                    filteredCandidates,
                    dumpSummaries,
                    localHeaders)
                .ToLookup(
                    occurrence => CreateProtocol03SelectorFfRepeatListCandidateOccurrenceKey(occurrence.Candidate),
                    StringComparer.OrdinalIgnoreCase);
        }

        return filteredCandidates
            .GroupBy(candidate => new
            {
                candidate.ContinuationPrefixBeforeMarkerHex,
                candidate.ContinuationMarkerHex,
                candidate.PostMarkerZeroRun,
                candidate.PostMarkerFirstFieldOffset,
                candidate.PostMarkerFirstFieldHex,
                candidate.PostMarkerFirstFieldValue,
                candidate.PostMarkerFieldLayoutKind
            })
            .Select(group =>
            {
                Protocol03SelectorFfRepeatListCandidate[] groupCandidates = group.ToArray();
                Protocol03SelectorFfRepeatListCandidate sample = groupCandidates[0];
                Protocol03SelectorFfRepeatListHeaderOccurrence[] headerOccurrences = groupCandidates
                    .SelectMany(candidate => headerOccurrencesByCandidate[CreateProtocol03SelectorFfRepeatListCandidateOccurrenceKey(candidate)])
                    .ToArray();
                Protocol03SelectorFfRepeatListHeaderOccurrence[] distinctHeaderWindows = DistinctProtocol03SelectorFfRepeatListHeaderWindows(headerOccurrences).ToArray();
                Protocol03SelectorFfRepeatListHeaderOccurrence[] distinctVendorHeaderWindows = DistinctProtocol03SelectorFfRepeatListHeaderWindows(headerOccurrences.Where(occurrence => IsVendorHeader(occurrence.Header))).ToArray();
                HashSet<string> vendorCandidateKeys = headerOccurrences
                    .Where(occurrence => IsVendorHeader(occurrence.Header))
                    .Select(occurrence => CreateProtocol03SelectorFfRepeatListCandidateOccurrenceKey(occurrence.Candidate))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                Protocol03SelectorFfRepeatListCandidate[] vendorCandidates = groupCandidates
                    .Where(candidate => vendorCandidateKeys.Contains(CreateProtocol03SelectorFfRepeatListCandidateOccurrenceKey(candidate)))
                    .ToArray();
                return new Protocol03SelectorFfRepeatListContinuationMarkerSummary(
                    group.Key.ContinuationPrefixBeforeMarkerHex,
                    group.Key.ContinuationMarkerHex,
                    group.Key.PostMarkerZeroRun,
                    group.Key.PostMarkerFirstFieldOffset,
                    group.Key.PostMarkerFirstFieldHex,
                    group.Key.PostMarkerFirstFieldValue,
                    group.Key.PostMarkerFieldLayoutKind,
                    groupCandidates.Length,
                    groupCandidates
                        .Select(CreateProtocol03SelectorFfRepeatListEffectiveShapeKey)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Count(),
                    CountProtocol03SelectorFfRepeatListHeaderCandidates(headerOccurrences, vendorOnly: false),
                    CountProtocol03SelectorFfRepeatListHeaderCandidates(headerOccurrences, vendorOnly: true),
                    distinctHeaderWindows.Length,
                    distinctVendorHeaderWindows.Length,
                    CountProtocol03SelectorFfRepeatListHeaderOccurrenceValues(
                        distinctHeaderWindows,
                        occurrence => ClassifyProtocol03SelectorFfRepeatListHeaderRegion(occurrence.Candidate, occurrence.PayloadOffset),
                        1,
                        8),
                    BuildProtocol03SelectorFfRepeatListHeaderWindowSummaries(headerOccurrences, vendorOnly: false),
                    BuildProtocol03SelectorFfRepeatListHeaderWindowSummaries(headerOccurrences, vendorOnly: true),
                    CountProtocol03SelectorFfRepeatListFieldSummaryValues(groupCandidates.Select(candidate => candidate.ContinuationPrefixHex), 12),
                    CountProtocol03SelectorFfRepeatListWeightedValues(
                        groupCandidates.Select(candidate => new WeightedValue(
                            FormatProtocol03SelectorFfRepeatListFieldLabel(candidate.Entry1FieldHex, candidate.Entry1FieldValue),
                            1)),
                        12),
                    CountProtocol03SelectorFfRepeatListWeightedValues(
                        groupCandidates.Select(candidate => new WeightedValue(
                            FormatProtocol03SelectorFfRepeatListFieldLabel(candidate.Entry2FieldHex, candidate.Entry2FieldValue),
                            1)),
                        12),
                    CountProtocol03SelectorFfRepeatListWeightedValues(
                        groupCandidates.Select(candidate => new WeightedValue(
                            FormatProtocol03SelectorFfRepeatListOptionalFieldLabel(candidate.Entry2SecondaryFieldHex, candidate.Entry2SecondaryFieldValue),
                            1)),
                        12),
                    CountProtocol03SelectorFfRepeatListWeightedValues(
                        groupCandidates.Select(candidate => new WeightedValue(
                            FormatProtocol03SelectorFfRepeatListFieldLabel(candidate.Entry2EffectiveFieldHex, candidate.Entry2EffectiveFieldValue),
                            1)),
                        12),
                    CountProtocol03SelectorFfRepeatListFieldSummaryValues(groupCandidates.Select(candidate => candidate.Entry2EffectiveFieldSource), 12),
                    CountProtocol03SelectorFfRepeatListResourceReferences(groupCandidates, candidate => candidate.Entry1FieldValue, resourceReferencesByValue, 8),
                    CountProtocol03SelectorFfRepeatListResourceReferences(groupCandidates, candidate => candidate.Entry2EffectiveFieldValue, resourceReferencesByValue, 8),
                    CountProtocol03SelectorFfRepeatListResourceReferences(groupCandidates, candidate => candidate.PostMarkerFirstFieldValue, resourceReferencesByValue, 8),
                    CountProtocol03SelectorFfRepeatListResourceReferences(vendorCandidates, candidate => candidate.Entry1FieldValue, resourceReferencesByValue, 8),
                    CountProtocol03SelectorFfRepeatListResourceReferences(vendorCandidates, candidate => candidate.Entry2EffectiveFieldValue, resourceReferencesByValue, 8),
                    CountProtocol03SelectorFfRepeatListResourceReferences(vendorCandidates, candidate => candidate.PostMarkerFirstFieldValue, resourceReferencesByValue, 8),
                    CountProtocol03SelectorFfRepeatListFieldSummaryValues(groupCandidates.Select(candidate => candidate.PostMarkerLeadHex), 12),
                    CountProtocol03SelectorFfRepeatListFieldSummaryValues(groupCandidates.Select(candidate => candidate.FirstSelector), 12),
                    CountProtocol03SelectorFfRepeatListFieldSummaryValues(groupCandidates.Select(candidate => candidate.SegmentClassification), 12),
                    groupCandidates
                        .GroupBy(candidate => new { candidate.File, candidate.Line, candidate.PositionOffset })
                        .Select(positionGroup => positionGroup.First())
                        .OrderBy(candidate => candidate.File, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(candidate => candidate.Line)
                        .ThenBy(candidate => candidate.PositionOffset)
                        .Take(5)
                        .Select(candidate => new Protocol03SelectorFfRepeatListShapePositionSample(
                            candidate.File,
                            candidate.Line,
                            candidate.PositionOffset,
                            candidate.PositionHex,
                            candidate.X,
                            candidate.Y,
                            candidate.Z))
                        .ToArray(),
                    sample.File,
                    sample.Line);
            })
            .OrderByDescending(summary => summary.CandidateCount)
            .ThenByDescending(summary => summary.ShapeCount)
            .ThenBy(summary => summary.ContinuationPrefixBeforeMarkerHex, StringComparer.OrdinalIgnoreCase)
            .ThenBy(summary => summary.ContinuationMarkerHex, StringComparer.OrdinalIgnoreCase)
            .ThenBy(summary => summary.PostMarkerZeroRun)
            .ThenBy(summary => summary.PostMarkerFirstFieldOffset ?? int.MaxValue)
            .ThenBy(summary => summary.PostMarkerFirstFieldHex, StringComparer.OrdinalIgnoreCase);
    }

    public static IEnumerable<Protocol03SelectorFfRepeatListVendorContinuationBodyOffsetSummary> BuildProtocol03SelectorFfRepeatListVendorContinuationBodyOffsetSummaries(
        IEnumerable<Protocol03SelectorFfRepeatListCandidate> candidates,
        IEnumerable<PacketDumpFileSummary> dumpSummaries,
        IReadOnlyList<RpcHeaderEntry> localHeaders)
    {
        var continuationOccurrences = BuildProtocol03SelectorFfRepeatListHeaderOccurrences(
                candidates,
                dumpSummaries,
                localHeaders)
            .Where(occurrence =>
                IsVendorHeader(occurrence.Header) &&
                IsProtocol03SelectorFfContinuationBodyHeaderInterpretation(
                    ClassifyProtocol03SelectorFfRepeatListHeaderInterpretation(occurrence.Candidate, occurrence.PayloadOffset)))
            .Select(occurrence => new
            {
                Occurrence = occurrence,
                ContinuationOffset = GetProtocol03SelectorFfRepeatListContinuationRelativeOffset(occurrence.Candidate, occurrence.PayloadOffset)
            })
            .Where(entry => entry.ContinuationOffset is not null)
            .ToArray();
        if (continuationOccurrences.Length == 0)
        {
            return Enumerable.Empty<Protocol03SelectorFfRepeatListVendorContinuationBodyOffsetSummary>();
        }

        return continuationOccurrences
            .GroupBy(entry => entry.ContinuationOffset!.Value)
            .Select(group =>
            {
                Protocol03SelectorFfRepeatListHeaderOccurrence[] occurrences = group.Select(entry => entry.Occurrence).ToArray();
                Protocol03SelectorFfRepeatListHeaderOccurrence[] distinctWindows = DistinctProtocol03SelectorFfRepeatListHeaderWindows(occurrences).ToArray();
                Protocol03SelectorFfRepeatListHeaderOccurrence[] distinctCandidates = DistinctProtocol03SelectorFfRepeatListHeaderCandidates(occurrences).ToArray();
                Protocol03SelectorFfRepeatListHeaderOccurrence sample = distinctWindows.Length > 0 ? distinctWindows[0] : occurrences[0];

                return new Protocol03SelectorFfRepeatListVendorContinuationBodyOffsetSummary(
                    group.Key,
                    group.Key >= 4 ? group.Key - 4 : -1,
                    distinctCandidates.Length,
                    distinctWindows.Length,
                    CountProtocol03SelectorFfRepeatListDistinctHeaderShapes(occurrences),
                    CountProtocol03SelectorFfRepeatListWeightedValues(
                        distinctWindows.Select(occurrence => new WeightedValue(
                            $"{occurrence.Header.Name} [{occurrence.Header.EncodedHeader} {occurrence.Header.Direction}]",
                            1)),
                        10),
                    CountProtocol03SelectorFfRepeatListHeaderOccurrenceValues(
                        distinctWindows,
                        occurrence => $"+{occurrence.PayloadOffset.ToString(CultureInfo.InvariantCulture)}",
                        1,
                        10),
                    CountProtocol03SelectorFfRepeatListHeaderOccurrenceValues(
                        distinctWindows,
                        occurrence => $"+{occurrence.ObjectOffset.ToString(CultureInfo.InvariantCulture)}",
                        1,
                        10),
                    CountProtocol03SelectorFfRepeatListHeaderOccurrenceValues(distinctWindows, occurrence => occurrence.WindowHex, 1, 10),
                    CountProtocol03SelectorFfRepeatListHeaderOccurrenceValues(
                        distinctWindows,
                        occurrence => FormatProtocol03SelectorFfContinuationBodyContext(occurrence),
                        1,
                        10),
                    CountProtocol03SelectorFfRepeatListHeaderOccurrenceValues(
                        distinctWindows,
                        occurrence => FormatProtocol03SelectorFfContinuationTargetBodyHex(occurrence),
                        1,
                        10),
                    CountProtocol03SelectorFfRepeatListHeaderOccurrenceValues(
                        distinctWindows,
                        occurrence => occurrence.Candidate.ContinuationBytes.ToString(CultureInfo.InvariantCulture),
                        1,
                        10),
                    CountProtocol03SelectorFfRepeatListHeaderOccurrenceValues(
                        distinctWindows,
                        occurrence => Math.Max(0, occurrence.Candidate.ContinuationBytes - (group.Key + 2)).ToString(CultureInfo.InvariantCulture),
                        1,
                        10),
                    CountProtocol03SelectorFfRepeatListHeaderOccurrenceValues(
                        distinctWindows,
                        occurrence => ClassifyProtocol03SelectorFfContinuationHeaderTail(
                            Math.Max(0, occurrence.Candidate.ContinuationBytes - (group.Key + 2))),
                        1,
                        10),
                    CountProtocol03SelectorFfRepeatListHeaderOccurrenceValues(distinctWindows, occurrence => occurrence.Candidate.ContinuationPrefixHex, 1, 10),
                    CountProtocol03SelectorFfRepeatListHeaderOccurrenceValues(
                        distinctWindows,
                        occurrence => FormatProtocol03SelectorFfRepeatListFieldLabel(occurrence.Candidate.Entry1FieldHex, occurrence.Candidate.Entry1FieldValue),
                        1,
                        10),
                    CountProtocol03SelectorFfRepeatListHeaderOccurrenceValues(
                        distinctWindows,
                        occurrence => FormatProtocol03SelectorFfRepeatListFieldLabel(occurrence.Candidate.Entry2EffectiveFieldHex, occurrence.Candidate.Entry2EffectiveFieldValue),
                        1,
                        10),
                    CountProtocol03SelectorFfRepeatListHeaderOccurrenceValues(
                        distinctWindows,
                        occurrence => FormatProtocol03SelectorFfRepeatListFieldLabel(occurrence.Candidate.PostMarkerFirstFieldHex, occurrence.Candidate.PostMarkerFirstFieldValue),
                        1,
                        10),
                    CountProtocol03SelectorFfRepeatListHeaderOccurrenceValues(
                        distinctWindows,
                        occurrence => $"+{occurrence.Candidate.PositionOffset.ToString(CultureInfo.InvariantCulture)}",
                        1,
                        10),
                    CountProtocol03SelectorFfRepeatListHeaderOccurrenceValues(distinctWindows, occurrence => occurrence.Candidate.FirstSelector, 1, 10),
                    sample.Candidate.File,
                    sample.Candidate.Line);
            })
            .OrderByDescending(summary => summary.HeaderWindowCount)
            .ThenByDescending(summary => summary.CandidateCount)
            .ThenBy(summary => summary.ContinuationOffset);
    }

    public static IEnumerable<Protocol03SelectorFfRepeatListVendorPrePositionBodyOffsetSummary> BuildProtocol03SelectorFfRepeatListVendorPrePositionBodyOffsetSummaries(
        IEnumerable<Protocol03SelectorFfRepeatListCandidate> candidates,
        IEnumerable<PacketDumpFileSummary> dumpSummaries,
        IReadOnlyList<RpcHeaderEntry> localHeaders)
    {
        var prePositionOccurrences = BuildProtocol03SelectorFfRepeatListHeaderOccurrences(
                candidates,
                dumpSummaries,
                localHeaders)
            .Where(occurrence =>
                IsVendorHeader(occurrence.Header) &&
                IsProtocol03SelectorFfPrePositionHeaderInterpretation(
                    ClassifyProtocol03SelectorFfRepeatListHeaderInterpretation(occurrence.Candidate, occurrence.PayloadOffset)))
            .Select(occurrence => new
            {
                Occurrence = occurrence,
                PrePositionOffset = GetProtocol03SelectorFfRepeatListPrePositionRelativeOffset(occurrence.Candidate, occurrence.PayloadOffset)
            })
            .Where(entry => entry.PrePositionOffset is not null)
            .ToArray();
        if (prePositionOccurrences.Length == 0)
        {
            return Enumerable.Empty<Protocol03SelectorFfRepeatListVendorPrePositionBodyOffsetSummary>();
        }

        return prePositionOccurrences
            .GroupBy(entry => entry.PrePositionOffset!.Value)
            .Select(group =>
            {
                Protocol03SelectorFfRepeatListHeaderOccurrence[] occurrences = group.Select(entry => entry.Occurrence).ToArray();
                Protocol03SelectorFfRepeatListHeaderOccurrence[] distinctWindows = DistinctProtocol03SelectorFfRepeatListHeaderWindows(occurrences).ToArray();
                Protocol03SelectorFfRepeatListHeaderOccurrence[] distinctCandidates = DistinctProtocol03SelectorFfRepeatListHeaderCandidates(occurrences).ToArray();
                Protocol03SelectorFfRepeatListHeaderOccurrence sample = distinctWindows.Length > 0 ? distinctWindows[0] : occurrences[0];

                return new Protocol03SelectorFfRepeatListVendorPrePositionBodyOffsetSummary(
                    group.Key,
                    sample.Candidate.PositionOffset - sample.PayloadOffset,
                    CountProtocol03SelectorFfRepeatListHeaderOccurrenceValues(
                        distinctWindows,
                        occurrence => (occurrence.Candidate.PositionOffset - occurrence.PayloadOffset).ToString(CultureInfo.InvariantCulture),
                        1,
                        10),
                    CountProtocol03SelectorFfRepeatListHeaderOccurrenceValues(
                        distinctWindows,
                        occurrence => Math.Max(0, occurrence.Candidate.PositionOffset - (occurrence.PayloadOffset + 2)).ToString(CultureInfo.InvariantCulture),
                        1,
                        10),
                    CountProtocol03SelectorFfRepeatListHeaderOccurrenceValues(
                        distinctWindows,
                        occurrence => ClassifyProtocol03SelectorFfPrePositionHeaderGap(
                            Math.Max(0, occurrence.Candidate.PositionOffset - (occurrence.PayloadOffset + 2))),
                        1,
                        10),
                    distinctCandidates.Length,
                    distinctWindows.Length,
                    CountProtocol03SelectorFfRepeatListDistinctHeaderShapes(occurrences),
                    CountProtocol03SelectorFfRepeatListWeightedValues(
                        distinctWindows.Select(occurrence => new WeightedValue(
                            $"{occurrence.Header.Name} [{occurrence.Header.EncodedHeader} {occurrence.Header.Direction}]",
                            1)),
                        10),
                    CountProtocol03SelectorFfRepeatListHeaderOccurrenceValues(
                        distinctWindows,
                        occurrence => $"+{occurrence.PayloadOffset.ToString(CultureInfo.InvariantCulture)}",
                        1,
                        10),
                    CountProtocol03SelectorFfRepeatListHeaderOccurrenceValues(
                        distinctWindows,
                        occurrence => $"+{occurrence.ObjectOffset.ToString(CultureInfo.InvariantCulture)}",
                        1,
                        10),
                    CountProtocol03SelectorFfRepeatListHeaderOccurrenceValues(distinctWindows, occurrence => occurrence.WindowHex, 1, 10),
                    CountProtocol03SelectorFfRepeatListHeaderOccurrenceValues(
                        distinctWindows,
                        occurrence => FormatProtocol03SelectorFfPrePositionBodyContext(occurrence),
                        1,
                        10),
                    CountProtocol03SelectorFfRepeatListHeaderOccurrenceValues(
                        distinctWindows,
                        occurrence => FormatProtocol03SelectorFfPrePositionTargetBodyHex(occurrence),
                        1,
                        10),
                    CountProtocol03SelectorFfRepeatListHeaderOccurrenceValues(distinctWindows, occurrence => occurrence.Candidate.PrePositionHex, 1, 10),
                    CountProtocol03SelectorFfRepeatListHeaderOccurrenceValues(
                        distinctWindows,
                        occurrence => $"+{occurrence.Candidate.PositionOffset.ToString(CultureInfo.InvariantCulture)}",
                        1,
                        10),
                    CountProtocol03SelectorFfRepeatListHeaderOccurrenceValues(distinctWindows, occurrence => occurrence.Candidate.ContinuationPrefixHex, 1, 10),
                    CountProtocol03SelectorFfRepeatListHeaderOccurrenceValues(
                        distinctWindows,
                        occurrence => FormatProtocol03SelectorFfRepeatListFieldLabel(occurrence.Candidate.Entry1FieldHex, occurrence.Candidate.Entry1FieldValue),
                        1,
                        10),
                    CountProtocol03SelectorFfRepeatListHeaderOccurrenceValues(
                        distinctWindows,
                        occurrence => FormatProtocol03SelectorFfRepeatListFieldLabel(occurrence.Candidate.Entry2EffectiveFieldHex, occurrence.Candidate.Entry2EffectiveFieldValue),
                        1,
                        10),
                    CountProtocol03SelectorFfRepeatListHeaderOccurrenceValues(
                        distinctWindows,
                        occurrence => FormatProtocol03SelectorFfRepeatListFieldLabel(occurrence.Candidate.PostMarkerFirstFieldHex, occurrence.Candidate.PostMarkerFirstFieldValue),
                        1,
                        10),
                    CountProtocol03SelectorFfRepeatListHeaderOccurrenceValues(distinctWindows, occurrence => occurrence.Candidate.FirstSelector, 1, 10),
                    sample.Candidate.File,
                    sample.Candidate.Line);
            })
            .OrderByDescending(summary => summary.HeaderWindowCount)
            .ThenByDescending(summary => summary.CandidateCount)
            .ThenBy(summary => summary.PrePositionOffset);
    }

    public static IEnumerable<Protocol03SelectorFfRepeatListVendorBodyWindowProbeSummary> BuildProtocol03SelectorFfRepeatListVendorBodyWindowProbeSummaries(
        IEnumerable<Protocol03SelectorFfRepeatListCandidate> candidates,
        IEnumerable<PacketDumpFileSummary> dumpSummaries,
        IReadOnlyList<RpcHeaderEntry> localHeaders,
        IReadOnlyList<AbilityDefinition>? abilities = null,
        IReadOnlyList<ItemCommandEntry>? items = null,
        IReadOnlyList<GameObjectEntry>? gameObjects = null,
        IReadOnlyList<AnimationDefinition>? animations = null)
    {
        IReadOnlyDictionary<int, IReadOnlyList<string>> resourceReferencesByValue = BuildProtocol03SelectorFfRepeatListFieldResourceReferenceIndex(
            abilities ?? Array.Empty<AbilityDefinition>(),
            items ?? Array.Empty<ItemCommandEntry>(),
            gameObjects ?? Array.Empty<GameObjectEntry>(),
            animations ?? Array.Empty<AnimationDefinition>());

        var bodyOccurrences = BuildProtocol03SelectorFfRepeatListHeaderOccurrences(
                candidates,
                dumpSummaries,
                localHeaders)
            .Where(occurrence => IsVendorHeader(occurrence.Header))
            .Select(occurrence => new
            {
                Occurrence = occurrence,
                Interpretation = ClassifyProtocol03SelectorFfRepeatListHeaderInterpretation(occurrence.Candidate, occurrence.PayloadOffset),
                BodyRegion = GetProtocol03SelectorFfRepeatListBodyWindowRegion(occurrence.Candidate, occurrence.PayloadOffset),
                BodyOffset = GetProtocol03SelectorFfRepeatListBodyWindowOffset(occurrence.Candidate, occurrence.PayloadOffset),
                BytesBeforeHeader = GetProtocol03SelectorFfRepeatListBodyBytesBeforeHeader(occurrence.Candidate, occurrence.PayloadOffset),
                BytesAfterHeader = GetProtocol03SelectorFfRepeatListBodyBytesAfterHeader(occurrence.Candidate, occurrence.PayloadOffset)
            })
            .Where(entry =>
                entry.BodyRegion is not null &&
                entry.BodyOffset is not null &&
                entry.BytesBeforeHeader is not null &&
                entry.BytesAfterHeader is not null)
            .ToArray();
        if (bodyOccurrences.Length == 0)
        {
            return Enumerable.Empty<Protocol03SelectorFfRepeatListVendorBodyWindowProbeSummary>();
        }

        return bodyOccurrences
            .GroupBy(entry => new
            {
                BodyRegion = entry.BodyRegion!,
                ParserPriority = GetProtocol03SelectorFfRepeatListVendorBoundaryParserPriority(entry.Interpretation),
                entry.Interpretation,
                entry.Occurrence.Header.Name,
                entry.Occurrence.Header.Direction,
                entry.Occurrence.Header.EncodedHeader,
                TargetLocalOffset = FormatProtocol03SelectorFfRepeatListTargetLocalOffset(entry.Occurrence.Candidate, entry.Occurrence.PayloadOffset),
                BodyOffset = entry.BodyOffset!.Value
            })
            .Select(group =>
            {
                var entries = group.ToArray();
                Protocol03SelectorFfRepeatListHeaderOccurrence[] occurrences = entries.Select(entry => entry.Occurrence).ToArray();
                Protocol03SelectorFfRepeatListHeaderOccurrence[] distinctWindows = DistinctProtocol03SelectorFfRepeatListHeaderWindows(occurrences).ToArray();
                Protocol03SelectorFfRepeatListHeaderOccurrence[] distinctCandidates = DistinctProtocol03SelectorFfRepeatListHeaderCandidates(occurrences).ToArray();
                Protocol03SelectorFfRepeatListHeaderOccurrence sample = distinctWindows.Length > 0 ? distinctWindows[0] : occurrences[0];
                string[] protocols = entries
                    .Select(entry => entry.Occurrence.Header.Protocol)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(protocol => protocol, StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                return new Protocol03SelectorFfRepeatListVendorBodyWindowProbeSummary(
                    group.Key.BodyRegion,
                    group.Key.ParserPriority,
                    group.Key.Interpretation,
                    group.Key.Name,
                    group.Key.Direction,
                    group.Key.EncodedHeader,
                    protocols,
                    group.Key.TargetLocalOffset,
                    group.Key.BodyOffset,
                    MathMod(group.Key.BodyOffset, 2),
                    MathMod(group.Key.BodyOffset, 4),
                    distinctCandidates.Length,
                    distinctWindows.Length,
                    CountProtocol03SelectorFfRepeatListDistinctHeaderShapes(occurrences),
                    CountProtocol03SelectorFfRepeatListHeaderOccurrenceValues(
                        distinctWindows,
                        occurrence => GetProtocol03SelectorFfRepeatListBodyBytesBeforeHeader(occurrence.Candidate, occurrence.PayloadOffset)!.Value.ToString(CultureInfo.InvariantCulture),
                        1,
                        10),
                    CountProtocol03SelectorFfRepeatListHeaderOccurrenceValues(
                        distinctWindows,
                        occurrence => GetProtocol03SelectorFfRepeatListBodyBytesAfterHeader(occurrence.Candidate, occurrence.PayloadOffset)!.Value.ToString(CultureInfo.InvariantCulture),
                        1,
                        10),
                    CountProtocol03SelectorFfRepeatListHeaderOccurrenceValues(
                        distinctWindows,
                        occurrence => GetProtocol03SelectorFfRepeatListBodyByteCount(occurrence.Candidate, occurrence.PayloadOffset)!.Value.ToString(CultureInfo.InvariantCulture),
                        1,
                        10),
                    CountProtocol03SelectorFfRepeatListHeaderOccurrenceValues(
                        distinctWindows,
                        occurrence => FormatProtocol03SelectorFfRepeatListTargetBodySpan(occurrence.Candidate, occurrence.PayloadOffset),
                        1,
                        10),
                    CountProtocol03SelectorFfRepeatListHeaderOccurrenceValues(
                        distinctWindows,
                        FormatProtocol03SelectorFfRepeatListTargetBodyHex,
                        1,
                        10),
                    CountProtocol03SelectorFfRepeatListHeaderOccurrenceValues(
                        distinctWindows,
                        occurrence => $"+{occurrence.PayloadOffset.ToString(CultureInfo.InvariantCulture)}",
                        1,
                        10),
                    CountProtocol03SelectorFfRepeatListHeaderOccurrenceValues(
                        distinctWindows,
                        occurrence => $"+{occurrence.ObjectOffset.ToString(CultureInfo.InvariantCulture)}",
                        1,
                        10),
                    CountProtocol03SelectorFfRepeatListHeaderOccurrenceValues(distinctWindows, occurrence => occurrence.WindowHex, 1, 10),
                    CountProtocol03SelectorFfRepeatListHeaderOccurrenceValues(
                        distinctWindows,
                        occurrence => FormatProtocol03SelectorFfRepeatListWindowU32Value(occurrence.WindowHex),
                        1,
                        10),
                    CountProtocol03SelectorFfRepeatListHeaderOccurrenceValues(
                        distinctWindows,
                        occurrence => FormatProtocol03SelectorFfRepeatListFieldLabel(occurrence.Header.EncodedHeader, TryReadProtocol03U16FieldValue(occurrence.Header.EncodedHeader)),
                        1,
                        10),
                    CountProtocol03SelectorFfRepeatListWeightedValues(
                        distinctWindows.SelectMany(occurrence => EnumerateProtocol03SelectorFfRepeatListWindowHalfU16ResourceReferences(
                                occurrence,
                                resourceReferencesByValue)
                            .Select(value => new WeightedValue(value, 1))),
                        10),
                    CountProtocol03SelectorFfRepeatListWeightedValues(
                        distinctWindows.SelectMany(occurrence => EnumerateProtocol03SelectorFfRepeatListNearbyU16Values(occurrence)
                            .Select(value => new WeightedValue(value, 1))),
                        10),
                    CountProtocol03SelectorFfRepeatListWeightedValues(
                        distinctWindows.SelectMany(occurrence => EnumerateProtocol03SelectorFfRepeatListNearbyU16LengthMatches(occurrence)
                            .Select(value => new WeightedValue(value, 1))),
                        10),
                    CountProtocol03SelectorFfRepeatListHeaderOccurrenceValues(distinctWindows, occurrence => occurrence.Candidate.ContinuationPrefixHex, 1, 10),
                    CountProtocol03SelectorFfRepeatListHeaderOccurrenceValues(
                        distinctWindows,
                        occurrence => FormatProtocol03SelectorFfRepeatListFieldLabel(occurrence.Candidate.Entry1FieldHex, occurrence.Candidate.Entry1FieldValue),
                        1,
                        10),
                    CountProtocol03SelectorFfRepeatListHeaderOccurrenceValues(
                        distinctWindows,
                        occurrence => FormatProtocol03SelectorFfRepeatListFieldLabel(occurrence.Candidate.Entry2EffectiveFieldHex, occurrence.Candidate.Entry2EffectiveFieldValue),
                        1,
                        10),
                    CountProtocol03SelectorFfRepeatListHeaderOccurrenceValues(
                        distinctWindows,
                        occurrence => FormatProtocol03SelectorFfRepeatListFieldLabel(occurrence.Candidate.PostMarkerFirstFieldHex, occurrence.Candidate.PostMarkerFirstFieldValue),
                        1,
                        10),
                    CountProtocol03SelectorFfRepeatListHeaderOccurrenceValues(distinctWindows, occurrence => occurrence.Candidate.FirstSelector, 1, 10),
                    sample.Candidate.File,
                    sample.Candidate.Line);
            })
            .OrderBy(summary => IsProtocol03SelectorFfRepeatListModeledFieldOverlap(summary.TargetInterpretation))
            .ThenByDescending(summary => summary.HeaderWindowCount)
            .ThenBy(summary => summary.BodyRegion, StringComparer.OrdinalIgnoreCase)
            .ThenBy(summary => summary.BodyOffset)
            .ThenBy(summary => summary.Name, StringComparer.OrdinalIgnoreCase);
    }

    public static IEnumerable<Protocol03SelectorFfRepeatListVendorBodyWindowResourceCollisionSummary> BuildProtocol03SelectorFfRepeatListVendorBodyWindowResourceCollisionSummaries(
        IEnumerable<Protocol03SelectorFfRepeatListVendorBodyWindowProbeSummary> summaries)
    {
        List<(Protocol03SelectorFfRepeatListVendorBodyWindowProbeSummary Summary, Protocol03SelectorFfRepeatListWindowResourceReferenceKey Reference, int Count)> entries = new();
        foreach (Protocol03SelectorFfRepeatListVendorBodyWindowProbeSummary summary in summaries)
        {
            foreach (KeyValuePair<string, int> reference in summary.WindowHalfU16ResourceReferences)
            {
                Protocol03SelectorFfRepeatListWindowResourceReferenceKey? parsed = ParseProtocol03SelectorFfRepeatListWindowResourceReference(reference.Key);
                if (parsed is null)
                {
                    continue;
                }

                entries.Add((summary, parsed, reference.Value));
            }
        }

        return entries
            .GroupBy(entry => entry.Reference)
            .Select(group =>
            {
                var groupEntries = group.ToArray();
                Protocol03SelectorFfRepeatListVendorBodyWindowProbeSummary sample = groupEntries[0].Summary;
                return new Protocol03SelectorFfRepeatListVendorBodyWindowResourceCollisionSummary(
                    group.Key.WindowHalf,
                    group.Key.WindowU16,
                    group.Key.ResourceKind,
                    group.Key.ResourceReference,
                    groupEntries.Length,
                    groupEntries.Sum(entry => entry.Count),
                    groupEntries.Sum(entry => entry.Summary.CandidateCount),
                    groupEntries.Sum(entry => entry.Summary.HeaderWindowCount),
                    groupEntries.Sum(entry => entry.Summary.ShapeCount),
                    CountProtocol03SelectorFfRepeatListWeightedValues(
                        groupEntries.Select(entry => new WeightedValue(entry.Summary.BodyRegion, entry.Count)),
                        6),
                    CountProtocol03SelectorFfRepeatListWeightedValues(
                        groupEntries.Select(entry => new WeightedValue(entry.Summary.ParserPriority, entry.Count)),
                        6),
                    CountProtocol03SelectorFfRepeatListWeightedValues(
                        groupEntries.Select(entry => new WeightedValue(entry.Summary.TargetInterpretation, entry.Count)),
                        6),
                    CountProtocol03SelectorFfRepeatListWeightedValues(
                        groupEntries.Select(entry => new WeightedValue($"{entry.Summary.Name} {entry.Summary.EncodedHeader}", entry.Count)),
                        6),
                    CountProtocol03SelectorFfRepeatListWeightedValues(
                        groupEntries.Select(entry => new WeightedValue(entry.Summary.TargetLocalOffset, entry.Count)),
                        8),
                    CountProtocol03SelectorFfRepeatListWeightedValues(
                        groupEntries.Select(entry => new WeightedValue($"+{entry.Summary.BodyOffset.ToString(CultureInfo.InvariantCulture)}", entry.Count)),
                        8),
                    CountProtocol03SelectorFfRepeatListWeightedValues(
                        groupEntries.SelectMany(entry => entry.Summary.TargetBodySpanBytes.Select(span => new WeightedValue(span.Key, span.Value))),
                        8),
                    CountProtocol03SelectorFfRepeatListWeightedValues(
                        groupEntries.SelectMany(entry => entry.Summary.WindowHexes.Select(window => new WeightedValue(window.Key, window.Value))),
                        8),
                    CountProtocol03SelectorFfRepeatListWeightedValues(
                        groupEntries.SelectMany(entry => entry.Summary.HeaderU16Values.Select(header => new WeightedValue(header.Key, header.Value))),
                        8),
                    CountProtocol03SelectorFfRepeatListWeightedValues(
                        groupEntries.SelectMany(entry => entry.Summary.NearbyU16LengthMatches.Select(match => new WeightedValue(match.Key, match.Value))),
                        8),
                    sample.SampleFile,
                    sample.SampleLine);
            })
            .OrderBy(summary => summary.WindowHalf.Equals("low", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenByDescending(summary => summary.ReferenceWindowCount)
            .ThenBy(summary => summary.ResourceKind, StringComparer.OrdinalIgnoreCase)
            .ThenBy(summary => summary.WindowU16, StringComparer.OrdinalIgnoreCase)
            .ThenBy(summary => summary.ResourceReference, StringComparer.OrdinalIgnoreCase);
    }

    public static IEnumerable<Protocol03SelectorFfRepeatListVendorBodyWindowEvidenceSummary> BuildProtocol03SelectorFfRepeatListVendorBodyWindowEvidenceSummaries(
        IEnumerable<Protocol03SelectorFfRepeatListVendorBodyWindowProbeSummary> summaries)
    {
        return summaries
            .GroupBy(ClassifyProtocol03SelectorFfRepeatListVendorBodyWindowEvidence, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                Protocol03SelectorFfRepeatListVendorBodyWindowProbeSummary[] groupSummaries = group.ToArray();
                Protocol03SelectorFfRepeatListVendorBodyWindowProbeSummary sample = groupSummaries[0];
                return new Protocol03SelectorFfRepeatListVendorBodyWindowEvidenceSummary(
                    group.Key,
                    groupSummaries.Length,
                    groupSummaries.Sum(summary => summary.CandidateCount),
                    groupSummaries.Sum(summary => summary.HeaderWindowCount),
                    groupSummaries.Sum(summary => summary.ShapeCount),
                    CountProtocol03SelectorFfRepeatListWeightedValues(
                        groupSummaries.Select(summary => new WeightedValue(summary.BodyRegion, summary.HeaderWindowCount)),
                        6),
                    CountProtocol03SelectorFfRepeatListWeightedValues(
                        groupSummaries.Select(summary => new WeightedValue(summary.ParserPriority, summary.HeaderWindowCount)),
                        6),
                    CountProtocol03SelectorFfRepeatListWeightedValues(
                        groupSummaries.Select(summary => new WeightedValue(summary.TargetInterpretation, summary.HeaderWindowCount)),
                        6),
                    CountProtocol03SelectorFfRepeatListWeightedValues(
                        groupSummaries.Select(summary => new WeightedValue($"{summary.Name} {summary.EncodedHeader}", summary.HeaderWindowCount)),
                        8),
                    CountProtocol03SelectorFfRepeatListWeightedValues(
                        groupSummaries.Select(summary => new WeightedValue(summary.TargetLocalOffset, summary.HeaderWindowCount)),
                        8),
                    CountProtocol03SelectorFfRepeatListWeightedValues(
                        groupSummaries.Select(summary => new WeightedValue($"+{summary.BodyOffset.ToString(CultureInfo.InvariantCulture)}", summary.HeaderWindowCount)),
                        8),
                    CountProtocol03SelectorFfRepeatListWeightedValues(
                        groupSummaries.SelectMany(summary => summary.TargetBodySpanBytes.Select(span => new WeightedValue(span.Key, span.Value))),
                        8),
                    CountProtocol03SelectorFfRepeatListWeightedValues(
                        groupSummaries.SelectMany(summary => summary.WindowHexes.Select(window => new WeightedValue(window.Key, window.Value))),
                        8),
                    CountProtocol03SelectorFfRepeatListWeightedValues(
                        groupSummaries.SelectMany(summary => summary.WindowHalfU16ResourceReferences
                            .Where(reference => reference.Key.StartsWith("low ", StringComparison.OrdinalIgnoreCase))
                            .Select(reference => new WeightedValue(reference.Key, reference.Value))),
                        8),
                    CountProtocol03SelectorFfRepeatListWeightedValues(
                        groupSummaries.SelectMany(summary => summary.WindowHalfU16ResourceReferences
                            .Where(reference => reference.Key.StartsWith("high ", StringComparison.OrdinalIgnoreCase))
                            .Select(reference => new WeightedValue(reference.Key, reference.Value))),
                        8),
                    CountProtocol03SelectorFfRepeatListWeightedValues(
                        groupSummaries.SelectMany(summary => summary.NearbyU16LengthMatches.Select(match => new WeightedValue(match.Key, match.Value))),
                        8),
                    sample.SampleFile,
                    sample.SampleLine);
            })
            .OrderBy(summary => GetProtocol03SelectorFfRepeatListVendorBodyWindowEvidenceOrder(summary.EvidenceDisposition))
            .ThenByDescending(summary => summary.HeaderWindowCount)
            .ThenBy(summary => summary.EvidenceDisposition, StringComparer.OrdinalIgnoreCase);
    }

    public static IEnumerable<Protocol03SelectorFfRepeatListVendorBodyWindowParserActionSummary> BuildProtocol03SelectorFfRepeatListVendorBodyWindowParserActionSummaries(
        IEnumerable<Protocol03SelectorFfRepeatListVendorBodyWindowEvidenceSummary> summaries)
    {
        return summaries
            .GroupBy(summary => GetProtocol03SelectorFfRepeatListVendorBodyWindowParserAction(summary.EvidenceDisposition))
            .Select(group =>
            {
                Protocol03SelectorFfRepeatListVendorBodyWindowEvidenceSummary[] groupSummaries = group.ToArray();
                Protocol03SelectorFfRepeatListVendorBodyWindowEvidenceSummary sample = groupSummaries[0];
                return new Protocol03SelectorFfRepeatListVendorBodyWindowParserActionSummary(
                    group.Key.Action,
                    group.Key.Reason,
                    groupSummaries.Sum(summary => summary.ProbeRowCount),
                    groupSummaries.Sum(summary => summary.CandidateCount),
                    groupSummaries.Sum(summary => summary.HeaderWindowCount),
                    groupSummaries.Sum(summary => summary.ShapeCount),
                    CountProtocol03SelectorFfRepeatListWeightedValues(
                        groupSummaries.Select(summary => new WeightedValue(summary.EvidenceDisposition, summary.HeaderWindowCount)),
                        6),
                    CountProtocol03SelectorFfRepeatListWeightedValues(
                        groupSummaries.SelectMany(summary => summary.BodyRegions.Select(entry => new WeightedValue(entry.Key, entry.Value))),
                        6),
                    CountProtocol03SelectorFfRepeatListWeightedValues(
                        groupSummaries.SelectMany(summary => summary.ParserPriorities.Select(entry => new WeightedValue(entry.Key, entry.Value))),
                        6),
                    CountProtocol03SelectorFfRepeatListWeightedValues(
                        groupSummaries.SelectMany(summary => summary.TargetInterpretations.Select(entry => new WeightedValue(entry.Key, entry.Value))),
                        8),
                    CountProtocol03SelectorFfRepeatListWeightedValues(
                        groupSummaries.SelectMany(summary => summary.Headers.Select(entry => new WeightedValue(entry.Key, entry.Value))),
                        8),
                    CountProtocol03SelectorFfRepeatListWeightedValues(
                        groupSummaries.SelectMany(summary => summary.TargetLocalOffsets.Select(entry => new WeightedValue(entry.Key, entry.Value))),
                        8),
                    CountProtocol03SelectorFfRepeatListWeightedValues(
                        groupSummaries.SelectMany(summary => summary.TargetBodySpanBytes.Select(entry => new WeightedValue(entry.Key, entry.Value))),
                        8),
                    CountProtocol03SelectorFfRepeatListWeightedValues(
                        groupSummaries.SelectMany(summary => summary.WindowHexes.Select(entry => new WeightedValue(entry.Key, entry.Value))),
                        8),
                    CountProtocol03SelectorFfRepeatListWeightedValues(
                        groupSummaries.SelectMany(summary => summary.LowHalfResourceReferences.Select(entry => new WeightedValue(entry.Key, entry.Value))),
                        8),
                    CountProtocol03SelectorFfRepeatListWeightedValues(
                        groupSummaries.SelectMany(summary => summary.HighHalfResourceReferences.Select(entry => new WeightedValue(entry.Key, entry.Value))),
                        8),
                    CountProtocol03SelectorFfRepeatListWeightedValues(
                        groupSummaries.SelectMany(summary => summary.NearbyU16LengthMatches.Select(entry => new WeightedValue(entry.Key, entry.Value))),
                        8),
                    sample.SampleFile,
                    sample.SampleLine);
            })
            .OrderBy(summary => GetProtocol03SelectorFfRepeatListVendorBodyWindowParserActionOrder(summary.ParserAction))
            .ThenByDescending(summary => summary.HeaderWindowCount)
            .ThenBy(summary => summary.ParserAction, StringComparer.OrdinalIgnoreCase);
    }

    private static string ClassifyProtocol03SelectorFfRepeatListVendorBodyWindowEvidence(Protocol03SelectorFfRepeatListVendorBodyWindowProbeSummary summary)
    {
        if (summary.NearbyU16LengthMatches.Count > 0)
        {
            return "nearby length-match candidate";
        }

        if (summary.WindowHalfU16ResourceReferences.Keys.Any(reference =>
            reference.StartsWith("low ", StringComparison.OrdinalIgnoreCase) &&
            (reference.Contains("-> item:", StringComparison.OrdinalIgnoreCase) ||
             reference.Contains("-> gameobject:", StringComparison.OrdinalIgnoreCase))))
        {
            return "low-half item/gameobject collision control";
        }

        if (summary.WindowHalfU16ResourceReferences.Keys.Any(reference => reference.StartsWith("low ", StringComparison.OrdinalIgnoreCase)))
        {
            return "low-half resource collision control";
        }

        if (summary.WindowHalfU16ResourceReferences.Keys.Any(reference => reference.StartsWith("high ", StringComparison.OrdinalIgnoreCase)))
        {
            return "high-half header-byte collision control";
        }

        return "unexplained body-window candidate";
    }

    private static int GetProtocol03SelectorFfRepeatListVendorBodyWindowEvidenceOrder(string evidenceDisposition)
    {
        return evidenceDisposition switch
        {
            "nearby length-match candidate" => 0,
            "low-half item/gameobject collision control" => 1,
            "low-half resource collision control" => 2,
            "high-half header-byte collision control" => 3,
            _ => 4
        };
    }

    private static (string Action, string Reason) GetProtocol03SelectorFfRepeatListVendorBodyWindowParserAction(string evidenceDisposition)
    {
        return evidenceDisposition switch
        {
            "nearby length-match candidate" => (
                "test body-length boundary",
                "nearby u16 matches body span or offset evidence"),
            "low-half item/gameobject collision control" => (
                "suppress nested RPC and model as packed resource-field collision",
                "low half of the four-byte window resolves to item/gameobject resources"),
            "low-half resource collision control" => (
                "suppress nested RPC unless corroborated by capture context",
                "low half resolves only to lower-confidence resource labels"),
            "high-half header-byte collision control" => (
                "suppress nested RPC and treat high half as header-byte collision",
                "only the header-looking high half resolves, usually to animation ids"),
            _ => (
                "queue body-length parser investigation",
                "no length-match or resource-collision control explains the row")
        };
    }

    private static int GetProtocol03SelectorFfRepeatListVendorBodyWindowParserActionOrder(string parserAction)
    {
        return parserAction switch
        {
            "test body-length boundary" => 0,
            "queue body-length parser investigation" => 1,
            "suppress nested RPC and model as packed resource-field collision" => 2,
            "suppress nested RPC unless corroborated by capture context" => 3,
            "suppress nested RPC and treat high half as header-byte collision" => 4,
            _ => 5
        };
    }

    private static Protocol03SelectorFfRepeatListWindowResourceReferenceKey? ParseProtocol03SelectorFfRepeatListWindowResourceReference(string reference)
    {
        const string separator = " -> ";
        int separatorIndex = reference.IndexOf(separator, StringComparison.Ordinal);
        if (separatorIndex <= 0)
        {
            return null;
        }

        string left = reference[..separatorIndex].Trim();
        string resourceReference = reference[(separatorIndex + separator.Length)..].Trim();
        int firstSpace = left.IndexOf(' ');
        if (firstSpace <= 0 || string.IsNullOrWhiteSpace(resourceReference))
        {
            return null;
        }

        string windowHalf = left[..firstSpace].Trim();
        string windowU16 = left[(firstSpace + 1)..].Trim();
        if (string.IsNullOrWhiteSpace(windowHalf) || string.IsNullOrWhiteSpace(windowU16))
        {
            return null;
        }

        int resourceKindSeparator = resourceReference.IndexOf(':');
        string resourceKind = resourceKindSeparator > 0
            ? resourceReference[..resourceKindSeparator]
            : "unknown";
        return new Protocol03SelectorFfRepeatListWindowResourceReferenceKey(
            windowHalf,
            windowU16,
            resourceKind,
            resourceReference);
    }

    private static string ClassifyProtocol03SelectorFfContinuationHeaderTail(int bytesAfterHeader)
    {
        if (bytesAfterHeader == 0)
        {
            return "terminal after header";
        }

        if (bytesAfterHeader <= 8)
        {
            return "short tail after header";
        }

        if (bytesAfterHeader <= 64)
        {
            return "mid-body tail after header";
        }

        return "deep body tail after header";
    }

    private static string ClassifyProtocol03SelectorFfPrePositionHeaderGap(int bytesAfterHeaderBeforePosition)
    {
        if (bytesAfterHeaderBeforePosition <= 2)
        {
            return "near-position overlap";
        }

        if (bytesAfterHeaderBeforePosition <= 16)
        {
            return "short gap before position";
        }

        if (bytesAfterHeaderBeforePosition <= 64)
        {
            return "mid-body gap before position";
        }

        return "deep pre-position gap";
    }

    public static IEnumerable<Protocol03SelectorFfRepeatListEffectiveShapeHeaderSummary> BuildProtocol03SelectorFfRepeatListEffectiveShapeHeaderSummaries(
        IEnumerable<Protocol03SelectorFfRepeatListCandidate> candidates,
        IEnumerable<PacketDumpFileSummary> dumpSummaries,
        IReadOnlyList<RpcHeaderEntry> localHeaders)
    {
        Dictionary<string, RpcHeaderEntry[]> headersByEncoded = localHeaders
            .Where(header => header.EncodedHeader.Contains(" ", StringComparison.Ordinal))
            .GroupBy(header => header.EncodedHeader, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.OrdinalIgnoreCase);
        if (headersByEncoded.Count == 0)
        {
            return Enumerable.Empty<Protocol03SelectorFfRepeatListEffectiveShapeHeaderSummary>();
        }

        Dictionary<string, Protocol03VariableSelectorLead> leadsByCandidateKey = dumpSummaries
            .SelectMany(summary => summary.Protocol03ObjectViews.SelectMany(view => view.VariableSelectorLeads
                .Where(lead => lead.Selector.Equals("ff", StringComparison.OrdinalIgnoreCase))
                .Select(lead => new
                {
                    Key = CreateProtocol03SelectorFfRepeatListCandidateLeadKey(summary.File, view.Line, lead.Offset),
                    Lead = lead
                })))
            .GroupBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Lead, StringComparer.OrdinalIgnoreCase);

        return candidates
            .Where(candidate =>
                candidate.RepeatRecordCount == 2 &&
                candidate.ContinuationOffset == candidate.RepeatStride * 2 &&
                candidate.ContinuationMarkerHex.Equals("ff", StringComparison.OrdinalIgnoreCase))
            .GroupBy(candidate => new
            {
                candidate.RepeatStride,
                candidate.RepeatRecordCount,
                candidate.ContinuationOffset,
                candidate.ContinuationPrefixHex,
                candidate.ContinuationPrefixBeforeMarkerHex,
                candidate.ContinuationMarkerHex,
                candidate.Entry1ZeroRun,
                candidate.Entry1FieldOffset,
                candidate.Entry1FieldHex,
                candidate.Entry1FieldValue,
                candidate.Entry1FieldLayoutKind,
                candidate.Entry2EffectiveFieldHex,
                candidate.Entry2EffectiveFieldValue,
                candidate.PostMarkerZeroRun,
                candidate.PostMarkerFirstFieldOffset,
                candidate.PostMarkerFirstFieldHex,
                candidate.PostMarkerFirstFieldValue,
                candidate.PostMarkerFieldLayoutKind
            })
            .Select(group =>
            {
                Protocol03SelectorFfRepeatListHeaderOccurrence[] occurrences = group
                    .SelectMany(candidate => FindProtocol03SelectorFfRepeatListHeaderOccurrences(candidate, leadsByCandidateKey, headersByEncoded))
                    .ToArray();
                if (occurrences.Length == 0)
                {
                    return null;
                }

                Protocol03SelectorFfRepeatListCandidate sample = group.First();
                return new Protocol03SelectorFfRepeatListEffectiveShapeHeaderSummary(
                    group.Key.RepeatStride,
                    group.Key.RepeatRecordCount,
                    group.Key.ContinuationOffset,
                    group.Key.ContinuationPrefixHex,
                    group.Key.ContinuationPrefixBeforeMarkerHex,
                    group.Key.ContinuationMarkerHex,
                    group.Key.Entry1ZeroRun,
                    group.Key.Entry1FieldOffset,
                    group.Key.Entry1FieldHex,
                    group.Key.Entry1FieldValue,
                    group.Key.Entry1FieldLayoutKind,
                    group.Key.Entry2EffectiveFieldHex,
                    group.Key.Entry2EffectiveFieldValue,
                    group.Key.PostMarkerZeroRun,
                    group.Key.PostMarkerFirstFieldOffset,
                    group.Key.PostMarkerFirstFieldHex,
                    group.Key.PostMarkerFirstFieldValue,
                    group.Key.PostMarkerFieldLayoutKind,
                    group.Count(),
                    CountProtocol03SelectorFfRepeatListHeaderCandidates(occurrences, vendorOnly: false),
                    CountProtocol03SelectorFfRepeatListHeaderCandidates(occurrences, vendorOnly: true),
                    CountProtocol03SelectorFfRepeatListFieldSummaryValues(group.Select(candidate => candidate.Entry2EffectiveFieldSource), 12),
                    CountProtocol03SelectorFfRepeatListWeightedValues(
                        group.Select(candidate => new WeightedValue(
                            FormatProtocol03SelectorFfRepeatListFieldLabel(candidate.Entry2FieldHex, candidate.Entry2FieldValue),
                            1)),
                        12),
                    CountProtocol03SelectorFfRepeatListWeightedValues(
                        group.Select(candidate => new WeightedValue(
                            FormatProtocol03SelectorFfRepeatListOptionalFieldLabel(candidate.Entry2SecondaryFieldHex, candidate.Entry2SecondaryFieldValue),
                            1)),
                        12),
                    CountProtocol03SelectorFfRepeatListFieldSummaryValues(group.Select(candidate => candidate.FirstSelector), 12),
                    CountProtocol03SelectorFfRepeatListFieldSummaryValues(group.Select(candidate => candidate.SegmentClassification), 12),
                    BuildProtocol03SelectorFfRepeatListHeaderWindowSummaries(occurrences, vendorOnly: false),
                    BuildProtocol03SelectorFfRepeatListHeaderWindowSummaries(occurrences, vendorOnly: true),
                    group
                        .GroupBy(candidate => new { candidate.File, candidate.Line, candidate.PositionOffset })
                        .Select(positionGroup => positionGroup.First())
                        .OrderBy(candidate => candidate.File, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(candidate => candidate.Line)
                        .ThenBy(candidate => candidate.PositionOffset)
                        .Take(5)
                        .Select(candidate => new Protocol03SelectorFfRepeatListShapePositionSample(
                            candidate.File,
                            candidate.Line,
                            candidate.PositionOffset,
                            candidate.PositionHex,
                            candidate.X,
                            candidate.Y,
                            candidate.Z))
                        .ToArray(),
                    sample.File,
                    sample.Line);
            })
            .Where(summary => summary is not null)
            .Select(summary => summary!)
            .OrderByDescending(summary => summary.CandidatesWithVendorHeaderWindows)
            .ThenByDescending(summary => summary.VendorHeaderWindows.Sum(window => window.Count))
            .ThenByDescending(summary => summary.CandidatesWithHeaderWindows)
            .ThenByDescending(summary => summary.HeaderWindows.Sum(window => window.Count))
            .ThenByDescending(summary => summary.CandidateCount)
            .ThenBy(summary => summary.Entry1ZeroRun)
            .ThenBy(summary => summary.Entry1FieldOffset ?? int.MaxValue)
            .ThenBy(summary => summary.Entry1FieldHex, StringComparer.OrdinalIgnoreCase)
            .ThenBy(summary => summary.Entry2EffectiveFieldHex, StringComparer.OrdinalIgnoreCase)
            .ThenBy(summary => summary.ContinuationPrefixHex, StringComparer.OrdinalIgnoreCase)
            .ThenBy(summary => summary.PostMarkerZeroRun)
            .ThenBy(summary => summary.PostMarkerFirstFieldOffset ?? int.MaxValue)
            .ThenBy(summary => summary.PostMarkerFirstFieldHex, StringComparer.OrdinalIgnoreCase);
    }

    public static IEnumerable<Protocol03SelectorFfRepeatListHeaderNameSummary> BuildProtocol03SelectorFfRepeatListHeaderNameSummaries(
        IEnumerable<Protocol03SelectorFfRepeatListCandidate> candidates,
        IEnumerable<PacketDumpFileSummary> dumpSummaries,
        IReadOnlyList<RpcHeaderEntry> localHeaders)
    {
        Protocol03SelectorFfRepeatListHeaderOccurrence[] occurrences = BuildProtocol03SelectorFfRepeatListHeaderOccurrences(
            candidates,
            dumpSummaries,
            localHeaders).ToArray();
        if (occurrences.Length == 0)
        {
            return Enumerable.Empty<Protocol03SelectorFfRepeatListHeaderNameSummary>();
        }

        return occurrences
            .GroupBy(occurrence => new
            {
                occurrence.Header.Name,
                occurrence.Header.Direction,
                occurrence.Header.EncodedHeader
            })
            .Select(group =>
            {
                Protocol03SelectorFfRepeatListHeaderOccurrence sample = group.First();
                string[] protocols = group
                    .Select(occurrence => occurrence.Header.Protocol)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(protocol => protocol, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                Protocol03SelectorFfRepeatListHeaderOccurrence[] distinctWindows = DistinctProtocol03SelectorFfRepeatListHeaderWindows(group).ToArray();
                return new Protocol03SelectorFfRepeatListHeaderNameSummary(
                    group.Key.Name,
                    group.Key.Direction,
                    group.Key.EncodedHeader,
                    protocols,
                    CountProtocol03SelectorFfRepeatListDistinctHeaderCandidates(group),
                    distinctWindows.Length,
                    CountProtocol03SelectorFfRepeatListDistinctHeaderShapes(group),
                    group.Any(occurrence => IsVendorHeader(occurrence.Header)),
                    CountProtocol03SelectorFfRepeatListHeaderOccurrenceValues(distinctWindows, occurrence => $"+{occurrence.PayloadOffset.ToString(CultureInfo.InvariantCulture)}", 1, 12),
                    CountProtocol03SelectorFfRepeatListHeaderOccurrenceValues(distinctWindows, occurrence => $"+{occurrence.ObjectOffset.ToString(CultureInfo.InvariantCulture)}", 1, 12),
                    CountProtocol03SelectorFfRepeatListHeaderOccurrenceValues(distinctWindows, occurrence => occurrence.WindowHex, 1, 12),
                    CountProtocol03SelectorFfRepeatListHeaderOccurrenceValues(distinctWindows, occurrence => occurrence.Candidate.ContinuationPrefixHex, 1, 12),
                    CountProtocol03SelectorFfRepeatListHeaderOccurrenceValues(
                        distinctWindows,
                        occurrence => FormatProtocol03SelectorFfRepeatListFieldLabel(occurrence.Candidate.Entry1FieldHex, occurrence.Candidate.Entry1FieldValue),
                        1,
                        12),
                    CountProtocol03SelectorFfRepeatListHeaderOccurrenceValues(
                        distinctWindows,
                        occurrence => FormatProtocol03SelectorFfRepeatListFieldLabel(occurrence.Candidate.Entry2EffectiveFieldHex, occurrence.Candidate.Entry2EffectiveFieldValue),
                        1,
                        12),
                    CountProtocol03SelectorFfRepeatListHeaderOccurrenceValues(
                        distinctWindows,
                        occurrence => FormatProtocol03SelectorFfRepeatListFieldLabel(occurrence.Candidate.PostMarkerFirstFieldHex, occurrence.Candidate.PostMarkerFirstFieldValue),
                        1,
                        12),
                    CountProtocol03SelectorFfRepeatListHeaderOccurrenceValues(distinctWindows, occurrence => occurrence.Candidate.FirstSelector, 1, 12),
                    CountProtocol03SelectorFfRepeatListHeaderOccurrenceValues(distinctWindows, occurrence => occurrence.Candidate.SegmentClassification, 1, 12),
                    CountProtocol03SelectorFfRepeatListHeaderOccurrenceValues(
                        distinctWindows,
                        occurrence => ClassifyProtocol03SelectorFfRepeatListHeaderRegion(occurrence.Candidate, occurrence.PayloadOffset),
                        1,
                        12),
                    BuildProtocol03SelectorFfRepeatListHeaderNameShapeSummaries(group),
                    sample.Candidate.File,
                    sample.Candidate.Line);
            })
            .OrderByDescending(summary => summary.IsVendorHeader)
            .ThenByDescending(summary => summary.CandidateCount)
            .ThenByDescending(summary => summary.HeaderWindowCount)
            .ThenByDescending(summary => summary.ShapeCount)
            .ThenBy(summary => summary.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(summary => summary.Direction, StringComparer.OrdinalIgnoreCase);
    }

    public static IEnumerable<Protocol03SelectorFfRepeatListHeaderSample> BuildProtocol03SelectorFfRepeatListHeaderSamples(
        IEnumerable<Protocol03SelectorFfRepeatListCandidate> candidates,
        IEnumerable<PacketDumpFileSummary> dumpSummaries,
        IReadOnlyList<RpcHeaderEntry> localHeaders,
        int maxSamples = 120)
    {
        if (maxSamples <= 0)
        {
            return Enumerable.Empty<Protocol03SelectorFfRepeatListHeaderSample>();
        }

        Protocol03SelectorFfRepeatListHeaderOccurrence[] occurrences = BuildProtocol03SelectorFfRepeatListHeaderOccurrences(
            candidates,
            dumpSummaries,
            localHeaders).ToArray();
        if (occurrences.Length == 0)
        {
            return Enumerable.Empty<Protocol03SelectorFfRepeatListHeaderSample>();
        }

        return occurrences
            .GroupBy(CreateProtocol03SelectorFfRepeatListHeaderWindowOccurrenceKey, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                Protocol03SelectorFfRepeatListHeaderOccurrence occurrence = group.First();
                Protocol03SelectorFfRepeatListCandidate candidate = occurrence.Candidate;
                byte[] payload = ParseHexBytes(occurrence.Lead.PayloadHex).ToArray();
                string[] protocols = group
                    .Select(entry => entry.Header.Protocol)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(protocol => protocol, StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                return new Protocol03SelectorFfRepeatListHeaderSample(
                    occurrence.Header.Name,
                    occurrence.Header.Direction,
                    occurrence.Header.EncodedHeader,
                    protocols,
                    group.Any(entry => IsVendorHeader(entry.Header)),
                    candidate.File,
                    candidate.Line,
                    candidate.Direction,
                    candidate.PacketOffset,
                    candidate.ViewId,
                    candidate.UpdateCount,
                    candidate.FirstSelector,
                    candidate.Classification,
                    candidate.SegmentClassification,
                    candidate.SelectorOffset,
                    candidate.PayloadBytes,
                    occurrence.PayloadOffset,
                    occurrence.ObjectOffset,
                    occurrence.WindowHex,
                    ClassifyProtocol03SelectorFfRepeatListHeaderRegion(candidate, occurrence.PayloadOffset),
                    ClassifyProtocol03SelectorFfRepeatListHeaderInterpretation(candidate, occurrence.PayloadOffset),
                    FormatProtocol03SelectorFfRepeatListTargetLocalOffset(candidate, occurrence.PayloadOffset),
                    FormatProtocol03PayloadSlice(payload, 0, 64),
                    FormatProtocol03SelectorFfContextSlice(payload, occurrence.PayloadOffset, 16, 32),
                    FormatProtocol03PayloadSlice(payload, candidate.PositionOffset + 24, 80),
                    candidate.Entry1FieldHex,
                    candidate.Entry1FieldValue,
                    candidate.Entry2EffectiveFieldHex,
                    candidate.Entry2EffectiveFieldValue,
                    candidate.Entry2EffectiveFieldSource,
                    candidate.ContinuationPrefixHex,
                    candidate.PostMarkerFirstFieldHex,
                    candidate.PostMarkerFirstFieldValue,
                    candidate.PositionOffset,
                    candidate.PositionHex,
                    candidate.X,
                    candidate.Y,
                    candidate.Z);
            })
            .OrderByDescending(sample => sample.IsVendorHeader)
            .ThenBy(sample => sample.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(sample => sample.File, StringComparer.OrdinalIgnoreCase)
            .ThenBy(sample => sample.Line)
            .ThenBy(sample => sample.HeaderPayloadOffset)
            .Take(maxSamples);
    }

    public static IEnumerable<Protocol03SelectorFfRepeatListHeaderRegionSummary> BuildProtocol03SelectorFfRepeatListHeaderRegionSummaries(
        IEnumerable<Protocol03SelectorFfRepeatListCandidate> candidates,
        IEnumerable<PacketDumpFileSummary> dumpSummaries,
        IReadOnlyList<RpcHeaderEntry> localHeaders,
        IReadOnlyList<AbilityDefinition>? abilities = null,
        IReadOnlyList<ItemCommandEntry>? items = null,
        IReadOnlyList<GameObjectEntry>? gameObjects = null,
        IReadOnlyList<AnimationDefinition>? animations = null)
    {
        IReadOnlyDictionary<int, IReadOnlyList<string>> resourceReferencesByValue = BuildProtocol03SelectorFfRepeatListFieldResourceReferenceIndex(
            abilities ?? Array.Empty<AbilityDefinition>(),
            items ?? Array.Empty<ItemCommandEntry>(),
            gameObjects ?? Array.Empty<GameObjectEntry>(),
            animations ?? Array.Empty<AnimationDefinition>());

        Protocol03SelectorFfRepeatListHeaderOccurrence[] occurrences = BuildProtocol03SelectorFfRepeatListHeaderOccurrences(
            candidates,
            dumpSummaries,
            localHeaders).ToArray();
        if (occurrences.Length == 0)
        {
            return Enumerable.Empty<Protocol03SelectorFfRepeatListHeaderRegionSummary>();
        }

        return occurrences
            .GroupBy(occurrence => new
            {
                occurrence.Header.Name,
                occurrence.Header.Direction,
                occurrence.Header.EncodedHeader,
                HeaderRegion = ClassifyProtocol03SelectorFfRepeatListHeaderRegion(occurrence.Candidate, occurrence.PayloadOffset)
            })
            .Select(group =>
            {
                Protocol03SelectorFfRepeatListHeaderOccurrence sample = group.First();
                Protocol03SelectorFfRepeatListHeaderOccurrence[] distinctWindows = DistinctProtocol03SelectorFfRepeatListHeaderWindows(group).ToArray();
                string[] protocols = group
                    .Select(occurrence => occurrence.Header.Protocol)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(protocol => protocol, StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                return new Protocol03SelectorFfRepeatListHeaderRegionSummary(
                    group.Key.Name,
                    group.Key.Direction,
                    group.Key.EncodedHeader,
                    protocols,
                    group.Key.HeaderRegion,
                    CountProtocol03SelectorFfRepeatListDistinctHeaderCandidates(group),
                    distinctWindows.Length,
                    CountProtocol03SelectorFfRepeatListDistinctHeaderShapes(group),
                    group.Any(occurrence => IsVendorHeader(occurrence.Header)),
                    CountProtocol03SelectorFfRepeatListHeaderOccurrenceValues(distinctWindows, occurrence => $"+{occurrence.PayloadOffset.ToString(CultureInfo.InvariantCulture)}", 1, 10),
                    CountProtocol03SelectorFfRepeatListHeaderOccurrenceValues(distinctWindows, occurrence => $"+{occurrence.ObjectOffset.ToString(CultureInfo.InvariantCulture)}", 1, 10),
                    CountProtocol03SelectorFfRepeatListHeaderOccurrenceValues(distinctWindows, occurrence => occurrence.WindowHex, 1, 10),
                    CountProtocol03SelectorFfRepeatListHeaderOccurrenceValues(
                        distinctWindows,
                        occurrence => FormatProtocol03SelectorFfRepeatListFieldLabel(occurrence.Candidate.Entry1FieldHex, occurrence.Candidate.Entry1FieldValue),
                        1,
                        10),
                    CountProtocol03SelectorFfRepeatListHeaderOccurrenceValues(
                        distinctWindows,
                        occurrence => FormatProtocol03SelectorFfRepeatListFieldLabel(occurrence.Candidate.Entry2EffectiveFieldHex, occurrence.Candidate.Entry2EffectiveFieldValue),
                        1,
                        10),
                    CountProtocol03SelectorFfRepeatListHeaderOccurrenceValues(
                        distinctWindows,
                        occurrence => FormatProtocol03SelectorFfRepeatListFieldLabel(occurrence.Candidate.PostMarkerFirstFieldHex, occurrence.Candidate.PostMarkerFirstFieldValue),
                        1,
                        10),
                    CountProtocol03SelectorFfRepeatListHeaderOccurrenceResourceReferences(
                        distinctWindows,
                        occurrence => occurrence.Candidate.Entry1FieldValue,
                        resourceReferencesByValue,
                        8),
                    CountProtocol03SelectorFfRepeatListHeaderOccurrenceResourceReferences(
                        distinctWindows,
                        occurrence => occurrence.Candidate.Entry2EffectiveFieldValue,
                        resourceReferencesByValue,
                        8),
                    CountProtocol03SelectorFfRepeatListHeaderOccurrenceResourceReferences(
                        distinctWindows,
                        occurrence => occurrence.Candidate.PostMarkerFirstFieldValue,
                        resourceReferencesByValue,
                        8),
                    CountProtocol03SelectorFfRepeatListHeaderOccurrenceValues(distinctWindows, occurrence => occurrence.Candidate.FirstSelector, 1, 10),
                    CountProtocol03SelectorFfRepeatListHeaderOccurrenceValues(distinctWindows, occurrence => occurrence.Candidate.SegmentClassification, 1, 10),
                    BuildProtocol03SelectorFfRepeatListHeaderNameShapeSummaries(group),
                    sample.Candidate.File,
                    sample.Candidate.Line);
            })
            .OrderByDescending(summary => summary.IsVendorHeader)
            .ThenBy(summary => summary.Name, StringComparer.OrdinalIgnoreCase)
            .ThenByDescending(summary => summary.CandidateCount)
            .ThenByDescending(summary => summary.HeaderWindowCount)
            .ThenBy(summary => summary.HeaderRegion, StringComparer.OrdinalIgnoreCase);
    }

    public static IEnumerable<Protocol03SelectorFfRepeatListVendorBoundaryTargetSummary> BuildProtocol03SelectorFfRepeatListVendorBoundaryTargetSummaries(
        IEnumerable<Protocol03SelectorFfRepeatListCandidate> candidates,
        IEnumerable<PacketDumpFileSummary> dumpSummaries,
        IReadOnlyList<RpcHeaderEntry> localHeaders,
        IReadOnlyList<AbilityDefinition>? abilities = null,
        IReadOnlyList<ItemCommandEntry>? items = null,
        IReadOnlyList<GameObjectEntry>? gameObjects = null,
        IReadOnlyList<AnimationDefinition>? animations = null)
    {
        IReadOnlyDictionary<int, IReadOnlyList<string>> resourceReferencesByValue = BuildProtocol03SelectorFfRepeatListFieldResourceReferenceIndex(
            abilities ?? Array.Empty<AbilityDefinition>(),
            items ?? Array.Empty<ItemCommandEntry>(),
            gameObjects ?? Array.Empty<GameObjectEntry>(),
            animations ?? Array.Empty<AnimationDefinition>());

        Protocol03SelectorFfRepeatListHeaderOccurrence[] occurrences = BuildProtocol03SelectorFfRepeatListHeaderOccurrences(
                candidates,
                dumpSummaries,
                localHeaders)
            .Where(occurrence => IsVendorHeader(occurrence.Header))
            .ToArray();
        if (occurrences.Length == 0)
        {
            return Enumerable.Empty<Protocol03SelectorFfRepeatListVendorBoundaryTargetSummary>();
        }

        return occurrences
            .GroupBy(occurrence => new
            {
                occurrence.Header.Name,
                occurrence.Header.Direction,
                occurrence.Header.EncodedHeader,
                HeaderRegion = ClassifyProtocol03SelectorFfRepeatListHeaderRegion(occurrence.Candidate, occurrence.PayloadOffset),
                HeaderPayloadOffset = occurrence.PayloadOffset,
                HeaderObjectOffset = occurrence.ObjectOffset
            })
            .Select(group =>
            {
                Protocol03SelectorFfRepeatListHeaderOccurrence sample = group.First();
                Protocol03SelectorFfRepeatListHeaderOccurrence[] distinctWindows = DistinctProtocol03SelectorFfRepeatListHeaderWindows(group).ToArray();
                Protocol03SelectorFfRepeatListHeaderOccurrence[] distinctCandidates = DistinctProtocol03SelectorFfRepeatListHeaderCandidates(group).ToArray();
                string[] protocols = group
                    .Select(occurrence => occurrence.Header.Protocol)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(protocol => protocol, StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                return new Protocol03SelectorFfRepeatListVendorBoundaryTargetSummary(
                    group.Key.Name,
                    group.Key.Direction,
                    group.Key.EncodedHeader,
                    protocols,
                    group.Key.HeaderRegion,
                    group.Key.HeaderPayloadOffset,
                    group.Key.HeaderObjectOffset,
                    distinctCandidates.Length,
                    distinctWindows.Length,
                    CountProtocol03SelectorFfRepeatListDistinctHeaderShapes(group),
                    CountProtocol03SelectorFfRepeatListHeaderOccurrenceValues(
                        distinctWindows,
                        occurrence => ClassifyProtocol03SelectorFfRepeatListHeaderInterpretation(occurrence.Candidate, occurrence.PayloadOffset),
                        1,
                        10),
                    CountProtocol03SelectorFfRepeatListHeaderOccurrenceValues(
                        distinctWindows,
                        occurrence => FormatProtocol03SelectorFfRepeatListTargetLocalOffset(occurrence.Candidate, occurrence.PayloadOffset),
                        1,
                        10),
                    CountProtocol03SelectorFfRepeatListHeaderOccurrenceValues(
                        distinctWindows,
                        occurrence => FormatProtocol03SelectorFfRepeatListTargetBodySpan(occurrence.Candidate, occurrence.PayloadOffset),
                        1,
                        10),
                    CountProtocol03SelectorFfRepeatListHeaderOccurrenceValues(
                        distinctWindows,
                        FormatProtocol03SelectorFfRepeatListTargetBodyHex,
                        1,
                        10),
                    CountProtocol03SelectorFfRepeatListHeaderOccurrenceValues(
                        distinctWindows,
                        occurrence => $"+{occurrence.Candidate.PositionOffset.ToString(CultureInfo.InvariantCulture)}",
                        1,
                        10),
                    CountProtocol03SelectorFfRepeatListHeaderOccurrenceValues(distinctWindows, occurrence => occurrence.WindowHex, 1, 10),
                    CountProtocol03SelectorFfRepeatListHeaderOccurrenceValues(distinctWindows, occurrence => occurrence.Candidate.ContinuationPrefixHex, 1, 10),
                    CountProtocol03SelectorFfRepeatListHeaderOccurrenceValues(
                        distinctWindows,
                        occurrence => FormatProtocol03SelectorFfRepeatListFieldLabel(occurrence.Candidate.Entry1FieldHex, occurrence.Candidate.Entry1FieldValue),
                        1,
                        10),
                    CountProtocol03SelectorFfRepeatListHeaderOccurrenceResourceReferences(
                        distinctWindows,
                        occurrence => occurrence.Candidate.Entry1FieldValue,
                        resourceReferencesByValue,
                        8),
                    CountProtocol03SelectorFfRepeatListHeaderOccurrenceValues(
                        distinctWindows,
                        occurrence => FormatProtocol03SelectorFfRepeatListFieldLabel(occurrence.Candidate.Entry2EffectiveFieldHex, occurrence.Candidate.Entry2EffectiveFieldValue),
                        1,
                        10),
                    CountProtocol03SelectorFfRepeatListHeaderOccurrenceResourceReferences(
                        distinctWindows,
                        occurrence => occurrence.Candidate.Entry2EffectiveFieldValue,
                        resourceReferencesByValue,
                        8),
                    CountProtocol03SelectorFfRepeatListHeaderOccurrenceValues(
                        distinctWindows,
                        occurrence => FormatProtocol03SelectorFfRepeatListFieldLabel(occurrence.Candidate.PostMarkerFirstFieldHex, occurrence.Candidate.PostMarkerFirstFieldValue),
                        1,
                        10),
                    CountProtocol03SelectorFfRepeatListHeaderOccurrenceResourceReferences(
                        distinctWindows,
                        occurrence => occurrence.Candidate.PostMarkerFirstFieldValue,
                        resourceReferencesByValue,
                        8),
                    CountProtocol03SelectorFfRepeatListHeaderOccurrenceValues(distinctWindows, occurrence => occurrence.Candidate.FirstSelector, 1, 10),
                    CountProtocol03SelectorFfRepeatListHeaderOccurrenceValues(distinctWindows, occurrence => occurrence.Candidate.SegmentClassification, 1, 10),
                    BuildProtocol03SelectorFfRepeatListHeaderNameShapeSummaries(group),
                    sample.Candidate.File,
                    sample.Candidate.Line);
            })
            .OrderByDescending(summary => summary.CandidateCount)
            .ThenByDescending(summary => summary.HeaderWindowCount)
            .ThenByDescending(summary => summary.ShapeCount)
            .ThenBy(summary => summary.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(summary => summary.HeaderRegion, StringComparer.OrdinalIgnoreCase)
            .ThenBy(summary => summary.HeaderPayloadOffset)
            .ThenBy(summary => summary.HeaderObjectOffset);
    }

    public static IEnumerable<Protocol03SelectorFfRepeatListVendorBoundaryInterpretationSummary> BuildProtocol03SelectorFfRepeatListVendorBoundaryInterpretationSummaries(
        IEnumerable<Protocol03SelectorFfRepeatListVendorBoundaryTargetSummary> targetSummaries)
    {
        var expandedTargets = targetSummaries
            .SelectMany(target => target.TargetInterpretations.Select(entry => new
            {
                Target = target,
                Interpretation = entry.Key,
                Count = entry.Value
            }))
            .Where(entry => entry.Count > 0)
            .ToArray();
        if (expandedTargets.Length == 0)
        {
            return Enumerable.Empty<Protocol03SelectorFfRepeatListVendorBoundaryInterpretationSummary>();
        }

        return expandedTargets
            .GroupBy(entry => entry.Interpretation, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var entries = group.ToArray();
                return new Protocol03SelectorFfRepeatListVendorBoundaryInterpretationSummary(
                    group.Key,
                    GetProtocol03SelectorFfRepeatListVendorBoundaryParserPriority(group.Key),
                    IsProtocol03SelectorFfRepeatListModeledFieldOverlap(group.Key),
                    entries
                        .Select(entry => CreateProtocol03SelectorFfRepeatListVendorBoundaryTargetKey(entry.Target))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Count(),
                    entries.Sum(entry => entry.Count),
                    entries.Sum(entry => entry.Count),
                    entries.Sum(entry => entry.Target.ShapeCount),
                    CountProtocol03SelectorFfRepeatListWeightedValues(
                        entries.Select(entry => new WeightedValue(
                            $"{entry.Target.Name} [{entry.Target.EncodedHeader} {entry.Target.Direction}]",
                            entry.Count)),
                        10),
                    CountProtocol03SelectorFfRepeatListWeightedValues(
                        entries.Select(entry => new WeightedValue(entry.Target.HeaderRegion, entry.Count)),
                        10),
                    CountProtocol03SelectorFfRepeatListWeightedValues(
                        entries.Select(entry => new WeightedValue($"+{entry.Target.HeaderPayloadOffset.ToString(CultureInfo.InvariantCulture)}", entry.Count)),
                        10),
                    CountProtocol03SelectorFfRepeatListWeightedValues(
                        entries.Select(entry => new WeightedValue($"+{entry.Target.HeaderObjectOffset.ToString(CultureInfo.InvariantCulture)}", entry.Count)),
                        10),
                    CountProtocol03SelectorFfRepeatListWeightedValues(
                        entries.SelectMany(entry => entry.Target.TargetLocalOffsets.Select(offset => new WeightedValue(offset.Key, offset.Value))),
                        10),
                    CountProtocol03SelectorFfRepeatListWeightedValues(
                        entries.SelectMany(entry => FilterProtocol03SelectorFfRepeatListTargetBodySpanBytes(entry.Target.TargetBodySpanBytes, entry.Interpretation)
                            .Select(offset => new WeightedValue(offset.Key, offset.Value))),
                        10),
                    CountProtocol03SelectorFfRepeatListWeightedValues(
                        entries.SelectMany(entry => entry.Target.TargetBodyHexes.Select(hex => new WeightedValue(hex.Key, hex.Value))),
                        10),
                    CountProtocol03SelectorFfRepeatListWeightedValues(
                        entries.SelectMany(entry => entry.Target.PositionOffsets.Select(offset => new WeightedValue(offset.Key, offset.Value))),
                        10),
                    CountProtocol03SelectorFfRepeatListWeightedValues(
                        entries.SelectMany(entry => entry.Target.Entry1Fields.Select(field => new WeightedValue(field.Key, field.Value))),
                        10),
                    CountProtocol03SelectorFfRepeatListWeightedValues(
                        entries.SelectMany(entry => entry.Target.Entry2EffectiveFields.Select(field => new WeightedValue(field.Key, field.Value))),
                        10),
                    CountProtocol03SelectorFfRepeatListWeightedValues(
                        entries.SelectMany(entry => entry.Target.PostMarkerFields.Select(field => new WeightedValue(field.Key, field.Value))),
                        10),
                    entries
                        .OrderByDescending(entry => entry.Count)
                        .ThenByDescending(entry => entry.Target.CandidateCount)
                        .ThenByDescending(entry => entry.Target.ShapeCount)
                        .ThenBy(entry => entry.Target.Name, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(entry => entry.Target.HeaderPayloadOffset)
                        .ThenBy(entry => entry.Target.HeaderObjectOffset)
                        .Take(8)
                        .Select(entry => new Protocol03SelectorFfRepeatListVendorBoundaryInterpretationTargetSummary(
                            entry.Target.Name,
                            entry.Target.Direction,
                            entry.Target.EncodedHeader,
                            entry.Target.Protocols,
                            entry.Target.HeaderRegion,
                            entry.Target.HeaderPayloadOffset,
                            entry.Target.HeaderObjectOffset,
                            entry.Count,
                            entry.Count,
                            entry.Target.ShapeCount,
                            entry.Target.TargetLocalOffsets,
                            FilterProtocol03SelectorFfRepeatListTargetBodySpanBytes(entry.Target.TargetBodySpanBytes, entry.Interpretation),
                            entry.Target.TargetBodyHexes,
                            entry.Target.PositionOffsets,
                            entry.Target.Entry2EffectiveFields,
                            entry.Target.PostMarkerFields,
                            entry.Target.SampleFile,
                            entry.Target.SampleLine))
                        .ToArray());
            })
            .OrderBy(summary => summary.IsModeledFieldOverlap)
            .ThenByDescending(summary => summary.HeaderWindowCount)
            .ThenBy(summary => summary.TargetInterpretation, StringComparer.OrdinalIgnoreCase);
    }

    public static IEnumerable<Protocol03SelectorFfRepeatListVendorBoundaryPrioritySummary> BuildProtocol03SelectorFfRepeatListVendorBoundaryPrioritySummaries(
        IEnumerable<Protocol03SelectorFfRepeatListVendorBoundaryTargetSummary> targetSummaries)
    {
        var expandedTargets = targetSummaries
            .SelectMany(target => target.TargetInterpretations.Select(entry => new
            {
                Target = target,
                Interpretation = entry.Key,
                ParserPriority = GetProtocol03SelectorFfRepeatListVendorBoundaryParserPriority(entry.Key),
                IsModeledFieldOverlap = IsProtocol03SelectorFfRepeatListModeledFieldOverlap(entry.Key),
                Count = entry.Value
            }))
            .Where(entry => entry.Count > 0)
            .ToArray();
        if (expandedTargets.Length == 0)
        {
            return Enumerable.Empty<Protocol03SelectorFfRepeatListVendorBoundaryPrioritySummary>();
        }

        return expandedTargets
            .GroupBy(entry => new
            {
                entry.ParserPriority,
                entry.IsModeledFieldOverlap
            })
            .Select(group =>
            {
                var entries = group.ToArray();
                return new Protocol03SelectorFfRepeatListVendorBoundaryPrioritySummary(
                    group.Key.ParserPriority,
                    group.Key.IsModeledFieldOverlap,
                    entries
                        .Select(entry => CreateProtocol03SelectorFfRepeatListVendorBoundaryTargetKey(entry.Target))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Count(),
                    entries.Sum(entry => entry.Count),
                    entries.Sum(entry => entry.Count),
                    entries.Sum(entry => entry.Target.ShapeCount),
                    CountProtocol03SelectorFfRepeatListWeightedValues(
                        entries.Select(entry => new WeightedValue(entry.Interpretation, entry.Count)),
                        10),
                    CountProtocol03SelectorFfRepeatListWeightedValues(
                        entries.Select(entry => new WeightedValue(
                            $"{entry.Target.Name} [{entry.Target.EncodedHeader} {entry.Target.Direction}]",
                            entry.Count)),
                        10),
                    CountProtocol03SelectorFfRepeatListWeightedValues(
                        entries.Select(entry => new WeightedValue(entry.Target.HeaderRegion, entry.Count)),
                        10),
                    CountProtocol03SelectorFfRepeatListWeightedValues(
                        entries.Select(entry => new WeightedValue($"+{entry.Target.HeaderPayloadOffset.ToString(CultureInfo.InvariantCulture)}", entry.Count)),
                        10),
                    CountProtocol03SelectorFfRepeatListWeightedValues(
                        entries.Select(entry => new WeightedValue($"+{entry.Target.HeaderObjectOffset.ToString(CultureInfo.InvariantCulture)}", entry.Count)),
                        10),
                    CountProtocol03SelectorFfRepeatListWeightedValues(
                        entries.SelectMany(entry => entry.Target.TargetLocalOffsets.Select(offset => new WeightedValue(offset.Key, offset.Value))),
                        10),
                    CountProtocol03SelectorFfRepeatListWeightedValues(
                        entries.SelectMany(entry => FilterProtocol03SelectorFfRepeatListTargetBodySpanBytes(entry.Target.TargetBodySpanBytes, entry.Interpretation)
                            .Select(offset => new WeightedValue(offset.Key, offset.Value))),
                        10),
                    CountProtocol03SelectorFfRepeatListWeightedValues(
                        entries.SelectMany(entry => entry.Target.TargetBodyHexes.Select(hex => new WeightedValue(hex.Key, hex.Value))),
                        10),
                    CountProtocol03SelectorFfRepeatListWeightedValues(
                        entries.SelectMany(entry => entry.Target.PositionOffsets.Select(offset => new WeightedValue(offset.Key, offset.Value))),
                        10),
                    CountProtocol03SelectorFfRepeatListWeightedValues(
                        entries.SelectMany(entry => entry.Target.Entry1Fields.Select(field => new WeightedValue(field.Key, field.Value))),
                        10),
                    CountProtocol03SelectorFfRepeatListWeightedValues(
                        entries.SelectMany(entry => entry.Target.Entry2EffectiveFields.Select(field => new WeightedValue(field.Key, field.Value))),
                        10),
                    CountProtocol03SelectorFfRepeatListWeightedValues(
                        entries.SelectMany(entry => entry.Target.PostMarkerFields.Select(field => new WeightedValue(field.Key, field.Value))),
                        10),
                    entries
                        .OrderByDescending(entry => entry.Count)
                        .ThenByDescending(entry => entry.Target.CandidateCount)
                        .ThenByDescending(entry => entry.Target.ShapeCount)
                        .ThenBy(entry => entry.Interpretation, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(entry => entry.Target.Name, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(entry => entry.Target.HeaderPayloadOffset)
                        .ThenBy(entry => entry.Target.HeaderObjectOffset)
                        .Take(10)
                        .Select(entry => new Protocol03SelectorFfRepeatListVendorBoundaryInterpretationTargetSummary(
                            entry.Target.Name,
                            entry.Target.Direction,
                            entry.Target.EncodedHeader,
                            entry.Target.Protocols,
                            entry.Target.HeaderRegion,
                            entry.Target.HeaderPayloadOffset,
                            entry.Target.HeaderObjectOffset,
                            entry.Count,
                            entry.Count,
                            entry.Target.ShapeCount,
                            entry.Target.TargetLocalOffsets,
                            FilterProtocol03SelectorFfRepeatListTargetBodySpanBytes(entry.Target.TargetBodySpanBytes, entry.Interpretation),
                            entry.Target.TargetBodyHexes,
                            entry.Target.PositionOffsets,
                            entry.Target.Entry2EffectiveFields,
                            entry.Target.PostMarkerFields,
                            entry.Target.SampleFile,
                            entry.Target.SampleLine))
                        .ToArray());
            })
            .OrderBy(summary => summary.IsModeledFieldOverlap)
            .ThenByDescending(summary => summary.HeaderWindowCount)
            .ThenBy(summary => summary.ParserPriority, StringComparer.OrdinalIgnoreCase);
    }

    private static string CreateProtocol03SelectorFfRepeatListVendorBoundaryTargetKey(Protocol03SelectorFfRepeatListVendorBoundaryTargetSummary target)
    {
        return string.Join(
            '\u001f',
            target.Name,
            target.Direction,
            target.EncodedHeader,
            target.HeaderRegion,
            target.HeaderPayloadOffset.ToString(CultureInfo.InvariantCulture),
            target.HeaderObjectOffset.ToString(CultureInfo.InvariantCulture));
    }

    private static IReadOnlyDictionary<string, int> FilterProtocol03SelectorFfRepeatListTargetBodySpanBytes(
        IReadOnlyDictionary<string, int> targetBodySpanBytes,
        string targetInterpretation)
    {
        return targetBodySpanBytes
            .Where(entry => IsProtocol03SelectorFfRepeatListTargetBodySpanForInterpretation(entry.Key, targetInterpretation))
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsProtocol03SelectorFfRepeatListTargetBodySpanForInterpretation(string spanLabel, string targetInterpretation)
    {
        if (spanLabel.Equals("not body-span target", StringComparison.OrdinalIgnoreCase))
        {
            return !targetInterpretation.Contains("body", StringComparison.OrdinalIgnoreCase) &&
                !targetInterpretation.Contains("pre-position", StringComparison.OrdinalIgnoreCase) &&
                !targetInterpretation.Contains("continuation", StringComparison.OrdinalIgnoreCase);
        }

        if (TryParseProtocol03SelectorFfRepeatListTargetBodySpan(spanLabel, "continuation tail ", out int continuationTail))
        {
            if (!targetInterpretation.Contains("continuation", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (targetInterpretation.Contains("terminal-tail", StringComparison.OrdinalIgnoreCase))
            {
                return continuationTail == 0;
            }

            if (targetInterpretation.Contains("short-tail", StringComparison.OrdinalIgnoreCase))
            {
                return continuationTail > 0 && continuationTail <= 8;
            }

            if (targetInterpretation.Contains("mid-body", StringComparison.OrdinalIgnoreCase))
            {
                return continuationTail > 8 && continuationTail <= 64;
            }

            if (targetInterpretation.Contains("deep-body", StringComparison.OrdinalIgnoreCase))
            {
                return continuationTail > 64;
            }

            return targetInterpretation.Contains("unresolved body", StringComparison.OrdinalIgnoreCase) &&
                continuationTail > 8;
        }

        if (TryParseProtocol03SelectorFfRepeatListTargetBodySpan(spanLabel, "pre-position gap ", out int prePositionGap))
        {
            if (!targetInterpretation.Contains("pre-position", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (targetInterpretation.Contains("near-position", StringComparison.OrdinalIgnoreCase))
            {
                return prePositionGap <= 2;
            }

            if (targetInterpretation.Contains("short-gap", StringComparison.OrdinalIgnoreCase))
            {
                return prePositionGap > 2 && prePositionGap <= 16;
            }

            if (targetInterpretation.Contains("mid-body", StringComparison.OrdinalIgnoreCase))
            {
                return prePositionGap > 16 && prePositionGap <= 64;
            }

            if (targetInterpretation.Contains("deep-body", StringComparison.OrdinalIgnoreCase))
            {
                return prePositionGap > 64;
            }

            return targetInterpretation.Contains("unresolved body", StringComparison.OrdinalIgnoreCase) &&
                prePositionGap > 16;
        }

        return false;
    }

    private static bool TryParseProtocol03SelectorFfRepeatListTargetBodySpan(string spanLabel, string prefix, out int value)
    {
        value = 0;
        if (!spanLabel.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return int.TryParse(spanLabel[prefix.Length..], NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static string GetProtocol03SelectorFfRepeatListVendorBoundaryParserPriority(string targetInterpretation)
    {
        if (targetInterpretation.Contains("near-position overlap", StringComparison.OrdinalIgnoreCase))
        {
            return "near-position overlap";
        }

        if (targetInterpretation.Contains("terminal-tail overlap", StringComparison.OrdinalIgnoreCase))
        {
            return "terminal continuation overlap";
        }

        if (targetInterpretation.Contains("short-gap", StringComparison.OrdinalIgnoreCase))
        {
            return "short-span boundary candidate";
        }

        if (targetInterpretation.Contains("short-tail", StringComparison.OrdinalIgnoreCase))
        {
            return "short-span boundary candidate";
        }

        if (targetInterpretation.Contains("mid-body", StringComparison.OrdinalIgnoreCase))
        {
            return "mid-body boundary candidate";
        }

        if (targetInterpretation.Contains("deep-body", StringComparison.OrdinalIgnoreCase))
        {
            return "deep-body boundary candidate";
        }

        return IsProtocol03SelectorFfRepeatListModeledFieldOverlap(targetInterpretation)
            ? "modeled field overlap"
            : "unresolved boundary candidate";
    }

    private static bool IsProtocol03SelectorFfRepeatListModeledFieldOverlap(string targetInterpretation)
    {
        return targetInterpretation.Contains("overlap", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsProtocol03SelectorFfPrePositionHeaderInterpretation(string interpretation)
    {
        return interpretation.StartsWith("pre-position ", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsProtocol03SelectorFfContinuationBodyHeaderInterpretation(string interpretation)
    {
        return interpretation.Equals("continuation unresolved body", StringComparison.OrdinalIgnoreCase) ||
            interpretation.Equals("continuation short-tail body", StringComparison.OrdinalIgnoreCase) ||
            interpretation.Equals("continuation mid-body unresolved", StringComparison.OrdinalIgnoreCase) ||
            interpretation.Equals("continuation deep-body unresolved", StringComparison.OrdinalIgnoreCase) ||
            interpretation.Equals("continuation terminal-tail overlap", StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<Protocol03SelectorFfRepeatListHeaderOccurrence> BuildProtocol03SelectorFfRepeatListHeaderOccurrences(
        IEnumerable<Protocol03SelectorFfRepeatListCandidate> candidates,
        IEnumerable<PacketDumpFileSummary> dumpSummaries,
        IReadOnlyList<RpcHeaderEntry> localHeaders)
    {
        Dictionary<string, RpcHeaderEntry[]> headersByEncoded = localHeaders
            .Where(header => header.EncodedHeader.Contains(" ", StringComparison.Ordinal))
            .GroupBy(header => header.EncodedHeader, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.OrdinalIgnoreCase);
        if (headersByEncoded.Count == 0)
        {
            yield break;
        }

        Dictionary<string, Protocol03VariableSelectorLead> leadsByCandidateKey = dumpSummaries
            .SelectMany(summary => summary.Protocol03ObjectViews.SelectMany(view => view.VariableSelectorLeads
                .Where(lead => lead.Selector.Equals("ff", StringComparison.OrdinalIgnoreCase))
                .Select(lead => new
                {
                    Key = CreateProtocol03SelectorFfRepeatListCandidateLeadKey(summary.File, view.Line, lead.Offset),
                    Lead = lead
                })))
            .GroupBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Lead, StringComparer.OrdinalIgnoreCase);

        foreach (Protocol03SelectorFfRepeatListCandidate candidate in candidates
            .Where(candidate =>
                candidate.RepeatRecordCount == 2 &&
                candidate.ContinuationOffset == candidate.RepeatStride * 2 &&
                candidate.ContinuationMarkerHex.Equals("ff", StringComparison.OrdinalIgnoreCase)))
        {
            foreach (Protocol03SelectorFfRepeatListHeaderOccurrence occurrence in FindProtocol03SelectorFfRepeatListHeaderOccurrences(candidate, leadsByCandidateKey, headersByEncoded))
            {
                yield return occurrence;
            }
        }
    }

    private static IEnumerable<Protocol03SelectorFfRepeatListHeaderOccurrence> FindProtocol03SelectorFfRepeatListHeaderOccurrences(
        Protocol03SelectorFfRepeatListCandidate candidate,
        IReadOnlyDictionary<string, Protocol03VariableSelectorLead> leadsByCandidateKey,
        IReadOnlyDictionary<string, RpcHeaderEntry[]> headersByEncoded)
    {
        string key = CreateProtocol03SelectorFfRepeatListCandidateLeadKey(candidate.File, candidate.Line, candidate.SelectorOffset);
        if (!leadsByCandidateKey.TryGetValue(key, out Protocol03VariableSelectorLead? lead))
        {
            yield break;
        }

        byte[] bytes = ParseHexBytes(lead.PayloadHex).ToArray();
        for (int offset = 0; offset <= bytes.Length - 2; offset++)
        {
            string encodedHeader = FormatHeader(new[] { bytes[offset], bytes[offset + 1] });
            if (!headersByEncoded.TryGetValue(encodedHeader, out RpcHeaderEntry[]? headers))
            {
                continue;
            }

            string windowHex = FormatProtocol03SelectorFfRepeatListHeaderWindow(bytes, offset);
            foreach (RpcHeaderEntry header in headers)
            {
                yield return new Protocol03SelectorFfRepeatListHeaderOccurrence(candidate, offset, candidate.SelectorOffset + offset, windowHex, header, lead);
            }
        }
    }

    private static string FormatProtocol03SelectorFfRepeatListHeaderWindow(IReadOnlyList<byte> bytes, int headerOffset)
    {
        if (bytes.Count < 4)
        {
            return FormatHeader(bytes);
        }

        int start = headerOffset >= 2 ? headerOffset - 2 : headerOffset;
        if (start + 4 > bytes.Count)
        {
            start = bytes.Count - 4;
        }

        return FormatHeader(bytes.Skip(start).Take(4));
    }

    private static string FormatProtocol03SelectorFfContextSlice(IReadOnlyList<byte> payload, int offset, int beforeBytes, int afterBytes)
    {
        if (offset < 0 || offset >= payload.Count)
        {
            return string.Empty;
        }

        int start = Math.Max(0, offset - beforeBytes);
        int end = Math.Min(payload.Count, offset + 2 + afterBytes);
        if (end <= start)
        {
            return string.Empty;
        }

        return FormatHeader(payload.Skip(start).Take(end - start));
    }

    private static string FormatProtocol03SelectorFfContinuationBodyContext(Protocol03SelectorFfRepeatListHeaderOccurrence occurrence)
    {
        byte[] payload = ParseHexBytes(occurrence.Lead.PayloadHex).ToArray();
        return FormatProtocol03SelectorFfContextSlice(payload, occurrence.PayloadOffset, 6, 12);
    }

    private static string FormatProtocol03SelectorFfPrePositionBodyContext(Protocol03SelectorFfRepeatListHeaderOccurrence occurrence)
    {
        byte[] payload = ParseHexBytes(occurrence.Lead.PayloadHex).ToArray();
        return FormatProtocol03SelectorFfContextSlice(payload, occurrence.PayloadOffset, 8, 16);
    }

    private static string FormatProtocol03SelectorFfRepeatListTargetBodyHex(Protocol03SelectorFfRepeatListHeaderOccurrence occurrence)
    {
        string interpretation = ClassifyProtocol03SelectorFfRepeatListHeaderInterpretation(occurrence.Candidate, occurrence.PayloadOffset);
        if (IsProtocol03SelectorFfContinuationBodyHeaderInterpretation(interpretation))
        {
            return FormatProtocol03SelectorFfContinuationTargetBodyHex(occurrence);
        }

        if (IsProtocol03SelectorFfPrePositionHeaderInterpretation(interpretation))
        {
            return FormatProtocol03SelectorFfPrePositionTargetBodyHex(occurrence);
        }

        return "not body-span target";
    }

    private static string FormatProtocol03SelectorFfContinuationTargetBodyHex(Protocol03SelectorFfRepeatListHeaderOccurrence occurrence)
    {
        byte[] payload = ParseHexBytes(occurrence.Lead.PayloadHex).ToArray();
        int continuationStart = occurrence.Candidate.PositionOffset + 24 + occurrence.Candidate.ContinuationOffset;
        int continuationEnd = Math.Min(payload.Length, continuationStart + occurrence.Candidate.ContinuationBytes);
        if (occurrence.PayloadOffset < continuationStart || occurrence.PayloadOffset >= continuationEnd)
        {
            return string.Empty;
        }

        int start = Math.Max(continuationStart, occurrence.PayloadOffset - 4);
        int end = Math.Min(continuationEnd, occurrence.PayloadOffset + 14);
        return end <= start ? string.Empty : FormatHeader(payload.Skip(start).Take(end - start));
    }

    private static string FormatProtocol03SelectorFfPrePositionTargetBodyHex(Protocol03SelectorFfRepeatListHeaderOccurrence occurrence)
    {
        byte[] payload = ParseHexBytes(occurrence.Lead.PayloadHex).ToArray();
        const int prePositionBodyOffset = 13;
        int prePositionEnd = Math.Min(payload.Length, occurrence.Candidate.PositionOffset);
        if (occurrence.PayloadOffset < prePositionBodyOffset || occurrence.PayloadOffset >= prePositionEnd)
        {
            return string.Empty;
        }

        int start = Math.Max(prePositionBodyOffset, occurrence.PayloadOffset - 6);
        int end = Math.Min(prePositionEnd, occurrence.PayloadOffset + 14);
        return end <= start ? string.Empty : FormatHeader(payload.Skip(start).Take(end - start));
    }

    private static string ClassifyProtocol03SelectorFfRepeatListHeaderRegion(Protocol03SelectorFfRepeatListCandidate candidate, int payloadOffset)
    {
        int headerEnd = payloadOffset + 2;
        if (payloadOffset < 0)
        {
            return "unknown";
        }

        if (payloadOffset >= 9 && headerEnd <= 13)
        {
            return "field +9 u32";
        }

        if (headerEnd <= candidate.PositionOffset)
        {
            return payloadOffset < 9 ? "payload lead" : "pre-position body";
        }

        int positionEnd = candidate.PositionOffset + 24;
        if (payloadOffset < positionEnd)
        {
            return "position bytes";
        }

        int postPositionOffset = positionEnd;
        int relativeOffset = payloadOffset - postPositionOffset;
        if (relativeOffset < 0)
        {
            return "pre-repeat body";
        }

        if (candidate.RepeatStride > 0 && relativeOffset < candidate.RepeatStride)
        {
            return "repeat entry 1";
        }

        if (candidate.RepeatStride > 0 && relativeOffset < candidate.RepeatStride * 2)
        {
            return "repeat entry 2";
        }

        if (relativeOffset < candidate.ContinuationOffset)
        {
            return "repeat entry tail";
        }

        int continuationRelativeOffset = relativeOffset - candidate.ContinuationOffset;
        if (continuationRelativeOffset < 0)
        {
            return "pre-continuation";
        }

        if (continuationRelativeOffset < 4)
        {
            return "continuation prefix";
        }

        if (candidate.PostMarkerFirstFieldOffset is int postMarkerFieldOffset &&
            continuationRelativeOffset >= postMarkerFieldOffset &&
            continuationRelativeOffset < postMarkerFieldOffset + 2)
        {
            return "post-marker field";
        }

        if (continuationRelativeOffset < candidate.ContinuationBytes)
        {
            return "continuation body";
        }

        return "post-continuation tail";
    }

    private static string ClassifyProtocol03SelectorFfRepeatListHeaderInterpretation(Protocol03SelectorFfRepeatListCandidate candidate, int payloadOffset)
    {
        int headerEnd = payloadOffset + 2;
        if (payloadOffset < 0)
        {
            return "unknown";
        }

        if (payloadOffset >= 9 && headerEnd <= 13)
        {
            return "packed field +9 u32 overlap";
        }

        if (headerEnd <= candidate.PositionOffset)
        {
            if (payloadOffset < 9)
            {
                return "payload lead bytes";
            }

            int bytesAfterHeaderBeforePosition = Math.Max(0, candidate.PositionOffset - headerEnd);
            if (bytesAfterHeaderBeforePosition <= 2)
            {
                return "pre-position near-position overlap";
            }

            if (bytesAfterHeaderBeforePosition <= 16)
            {
                return "pre-position short-gap body";
            }

            if (bytesAfterHeaderBeforePosition <= 64)
            {
                return "pre-position mid-body unresolved";
            }

            return "pre-position deep-body unresolved";
        }

        int positionEnd = candidate.PositionOffset + 24;
        if (payloadOffset < positionEnd)
        {
            return "position bytes overlap";
        }

        int postPositionOffset = positionEnd;
        int relativeOffset = payloadOffset - postPositionOffset;
        if (relativeOffset < 0)
        {
            return "pre-repeat unresolved body";
        }

        if (candidate.RepeatStride > 0 && relativeOffset < candidate.RepeatStride)
        {
            return ClassifyProtocol03SelectorFfRepeatListEntryHeaderInterpretation(candidate, payloadOffset, postPositionOffset, 1);
        }

        if (candidate.RepeatStride > 0 && relativeOffset < candidate.RepeatStride * 2)
        {
            return ClassifyProtocol03SelectorFfRepeatListEntryHeaderInterpretation(candidate, payloadOffset, postPositionOffset + candidate.RepeatStride, 2);
        }

        if (relativeOffset < candidate.ContinuationOffset)
        {
            return "repeat-entry tail bytes";
        }

        int continuationOffset = postPositionOffset + candidate.ContinuationOffset;
        int continuationRelativeOffset = payloadOffset - continuationOffset;
        if (continuationRelativeOffset < 0)
        {
            return "pre-continuation bytes";
        }

        if (OverlapsProtocol03SelectorFfRepeatListHeaderField(payloadOffset, continuationOffset + 3, 1))
        {
            return "continuation marker overlap";
        }

        if (continuationRelativeOffset < 4)
        {
            return "continuation prefix bytes";
        }

        if (candidate.PostMarkerFirstFieldOffset is int postMarkerFieldOffset &&
            OverlapsProtocol03SelectorFfRepeatListHeaderField(payloadOffset, continuationOffset + postMarkerFieldOffset, 2))
        {
            return "post-marker field overlap";
        }

        if (continuationRelativeOffset < candidate.ContinuationBytes)
        {
            int bytesAfterHeader = Math.Max(0, candidate.ContinuationBytes - (continuationRelativeOffset + 2));
            if (bytesAfterHeader == 0)
            {
                return "continuation terminal-tail overlap";
            }

            if (bytesAfterHeader <= 8)
            {
                return "continuation short-tail body";
            }

            if (bytesAfterHeader <= 64)
            {
                return "continuation mid-body unresolved";
            }

            return "continuation deep-body unresolved";
        }

        return "post-continuation tail";
    }

    private static int? GetProtocol03SelectorFfRepeatListContinuationRelativeOffset(Protocol03SelectorFfRepeatListCandidate candidate, int payloadOffset)
    {
        int positionEnd = candidate.PositionOffset + 24;
        int continuationOffset = positionEnd + candidate.ContinuationOffset;
        int continuationRelativeOffset = payloadOffset - continuationOffset;
        return continuationRelativeOffset >= 0 && continuationRelativeOffset < candidate.ContinuationBytes
            ? continuationRelativeOffset
            : null;
    }

    private static int? GetProtocol03SelectorFfRepeatListPrePositionRelativeOffset(Protocol03SelectorFfRepeatListCandidate candidate, int payloadOffset)
    {
        const int prePositionBodyOffset = 13;
        int headerEnd = payloadOffset + 2;
        if (payloadOffset < 9 || headerEnd > candidate.PositionOffset)
        {
            return null;
        }

        return payloadOffset - prePositionBodyOffset;
    }

    private static string? GetProtocol03SelectorFfRepeatListBodyWindowRegion(Protocol03SelectorFfRepeatListCandidate candidate, int payloadOffset)
    {
        string interpretation = ClassifyProtocol03SelectorFfRepeatListHeaderInterpretation(candidate, payloadOffset);
        if (IsProtocol03SelectorFfContinuationBodyHeaderInterpretation(interpretation))
        {
            return "continuation body";
        }

        if (IsProtocol03SelectorFfPrePositionHeaderInterpretation(interpretation))
        {
            return "pre-position body";
        }

        return null;
    }

    private static int? GetProtocol03SelectorFfRepeatListBodyWindowOffset(Protocol03SelectorFfRepeatListCandidate candidate, int payloadOffset)
    {
        string? region = GetProtocol03SelectorFfRepeatListBodyWindowRegion(candidate, payloadOffset);
        if (region is null)
        {
            return null;
        }

        if (region.Equals("continuation body", StringComparison.OrdinalIgnoreCase))
        {
            return GetProtocol03SelectorFfRepeatListContinuationRelativeOffset(candidate, payloadOffset);
        }

        return GetProtocol03SelectorFfRepeatListPrePositionRelativeOffset(candidate, payloadOffset);
    }

    private static int? GetProtocol03SelectorFfRepeatListBodyBytesBeforeHeader(Protocol03SelectorFfRepeatListCandidate candidate, int payloadOffset)
    {
        return GetProtocol03SelectorFfRepeatListBodyWindowOffset(candidate, payloadOffset);
    }

    private static int? GetProtocol03SelectorFfRepeatListBodyBytesAfterHeader(Protocol03SelectorFfRepeatListCandidate candidate, int payloadOffset)
    {
        string? region = GetProtocol03SelectorFfRepeatListBodyWindowRegion(candidate, payloadOffset);
        if (region is null)
        {
            return null;
        }

        if (region.Equals("continuation body", StringComparison.OrdinalIgnoreCase))
        {
            int? continuationOffset = GetProtocol03SelectorFfRepeatListContinuationRelativeOffset(candidate, payloadOffset);
            return continuationOffset is int offset
                ? Math.Max(0, candidate.ContinuationBytes - (offset + 2))
                : null;
        }

        int headerEnd = payloadOffset + 2;
        return Math.Max(0, candidate.PositionOffset - headerEnd);
    }

    private static int? GetProtocol03SelectorFfRepeatListBodyByteCount(Protocol03SelectorFfRepeatListCandidate candidate, int payloadOffset)
    {
        string? region = GetProtocol03SelectorFfRepeatListBodyWindowRegion(candidate, payloadOffset);
        if (region is null)
        {
            return null;
        }

        return region.Equals("continuation body", StringComparison.OrdinalIgnoreCase)
            ? candidate.ContinuationBytes
            : Math.Max(0, candidate.PositionOffset - 13);
    }

    private static (int Start, int End)? GetProtocol03SelectorFfRepeatListBodyBounds(Protocol03SelectorFfRepeatListHeaderOccurrence occurrence)
    {
        string? region = GetProtocol03SelectorFfRepeatListBodyWindowRegion(occurrence.Candidate, occurrence.PayloadOffset);
        if (region is null)
        {
            return null;
        }

        byte[] payload = ParseHexBytes(occurrence.Lead.PayloadHex).ToArray();
        if (region.Equals("continuation body", StringComparison.OrdinalIgnoreCase))
        {
            int continuationStart = occurrence.Candidate.PositionOffset + 24 + occurrence.Candidate.ContinuationOffset;
            int continuationEnd = Math.Min(payload.Length, continuationStart + occurrence.Candidate.ContinuationBytes);
            return continuationStart < continuationEnd ? (continuationStart, continuationEnd) : null;
        }

        const int prePositionBodyOffset = 13;
        int prePositionEnd = Math.Min(payload.Length, occurrence.Candidate.PositionOffset);
        return prePositionBodyOffset < prePositionEnd ? (prePositionBodyOffset, prePositionEnd) : null;
    }

    private static IEnumerable<string> EnumerateProtocol03SelectorFfRepeatListNearbyU16Values(Protocol03SelectorFfRepeatListHeaderOccurrence occurrence)
    {
        byte[] payload = ParseHexBytes(occurrence.Lead.PayloadHex).ToArray();
        (int Start, int End)? bounds = GetProtocol03SelectorFfRepeatListBodyBounds(occurrence);
        if (bounds is null)
        {
            yield break;
        }

        foreach (int delta in new[] { -8, -6, -4, -2, 2, 4, 6, 8 })
        {
            int offset = occurrence.PayloadOffset + delta;
            if (offset < bounds.Value.Start || offset + 2 > bounds.Value.End || offset + 2 > payload.Length)
            {
                continue;
            }

            string hex = FormatHeader(payload.Skip(offset).Take(2));
            int value = BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(offset, 2));
            yield return $"{FormatRelativeOffset(delta)} {hex} / {value.ToString(CultureInfo.InvariantCulture)}";
        }
    }

    private static IEnumerable<string> EnumerateProtocol03SelectorFfRepeatListWindowHalfU16ResourceReferences(
        Protocol03SelectorFfRepeatListHeaderOccurrence occurrence,
        IReadOnlyDictionary<int, IReadOnlyList<string>> resourceReferencesByValue)
    {
        byte[] windowBytes = ParseHexBytes(occurrence.WindowHex).ToArray();
        if (windowBytes.Length != 4)
        {
            yield break;
        }

        foreach ((string Label, int Offset) half in new[] { ("low", 0), ("high", 2) })
        {
            string hex = FormatHeader(windowBytes.Skip(half.Offset).Take(2));
            int value = BinaryPrimitives.ReadUInt16LittleEndian(windowBytes.AsSpan(half.Offset, 2));
            foreach (string reference in FindProtocol03SelectorFfRepeatListFieldResourceReferences(value, resourceReferencesByValue))
            {
                yield return $"{half.Label} {hex} / {value.ToString(CultureInfo.InvariantCulture)} -> {reference}";
            }
        }
    }

    private static IEnumerable<string> EnumerateProtocol03SelectorFfRepeatListNearbyU16LengthMatches(Protocol03SelectorFfRepeatListHeaderOccurrence occurrence)
    {
        byte[] payload = ParseHexBytes(occurrence.Lead.PayloadHex).ToArray();
        (int Start, int End)? bounds = GetProtocol03SelectorFfRepeatListBodyBounds(occurrence);
        if (bounds is null)
        {
            yield break;
        }

        int? bodyOffset = GetProtocol03SelectorFfRepeatListBodyWindowOffset(occurrence.Candidate, occurrence.PayloadOffset);
        int? bytesAfter = GetProtocol03SelectorFfRepeatListBodyBytesAfterHeader(occurrence.Candidate, occurrence.PayloadOffset);
        int? bodyBytes = GetProtocol03SelectorFfRepeatListBodyByteCount(occurrence.Candidate, occurrence.PayloadOffset);
        if (bodyOffset is null || bytesAfter is null || bodyBytes is null)
        {
            yield break;
        }

        foreach (int delta in new[] { -8, -6, -4, -2, 2, 4, 6, 8 })
        {
            int offset = occurrence.PayloadOffset + delta;
            if (offset < bounds.Value.Start || offset + 2 > bounds.Value.End || offset + 2 > payload.Length)
            {
                continue;
            }

            string hex = FormatHeader(payload.Skip(offset).Take(2));
            int value = BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(offset, 2));
            string prefix = $"{FormatRelativeOffset(delta)} {hex} / {value.ToString(CultureInfo.InvariantCulture)}";
            if (value == bytesAfter.Value)
            {
                yield return $"{prefix} = bytes after header";
            }

            if (value == bytesAfter.Value + 2)
            {
                yield return $"{prefix} = header plus tail";
            }

            if (value == bodyOffset.Value)
            {
                yield return $"{prefix} = body offset";
            }

            if (value == bodyOffset.Value + 2)
            {
                yield return $"{prefix} = body offset plus header";
            }

            if (value == bodyBytes.Value)
            {
                yield return $"{prefix} = body bytes";
            }
        }
    }

    private static string FormatRelativeOffset(int offset)
    {
        return offset >= 0
            ? $"+{offset.ToString(CultureInfo.InvariantCulture)}"
            : offset.ToString(CultureInfo.InvariantCulture);
    }

    private static string FormatProtocol03SelectorFfRepeatListTargetLocalOffset(Protocol03SelectorFfRepeatListCandidate candidate, int payloadOffset)
    {
        int headerEnd = payloadOffset + 2;
        if (payloadOffset < 0)
        {
            return "unknown";
        }

        if (payloadOffset >= 9 && headerEnd <= 13)
        {
            return $"field +9 byte +{(payloadOffset - 9).ToString(CultureInfo.InvariantCulture)}";
        }

        if (headerEnd <= candidate.PositionOffset)
        {
            if (payloadOffset < 9)
            {
                return $"payload lead +{payloadOffset.ToString(CultureInfo.InvariantCulture)}";
            }

            const int prePositionBodyOffset = 13;
            int localOffset = payloadOffset - prePositionBodyOffset;
            return $"pre-position +{localOffset.ToString(CultureInfo.InvariantCulture)}";
        }

        int positionEnd = candidate.PositionOffset + 24;
        if (payloadOffset < positionEnd)
        {
            return $"position +{(payloadOffset - candidate.PositionOffset).ToString(CultureInfo.InvariantCulture)}";
        }

        int postPositionOffset = positionEnd;
        int relativeOffset = payloadOffset - postPositionOffset;
        if (relativeOffset < 0)
        {
            return "pre-repeat";
        }

        if (candidate.RepeatStride > 0 && relativeOffset < candidate.RepeatStride)
        {
            return $"repeat entry 1 +{relativeOffset.ToString(CultureInfo.InvariantCulture)}";
        }

        if (candidate.RepeatStride > 0 && relativeOffset < candidate.RepeatStride * 2)
        {
            return $"repeat entry 2 +{(relativeOffset - candidate.RepeatStride).ToString(CultureInfo.InvariantCulture)}";
        }

        if (relativeOffset < candidate.ContinuationOffset)
        {
            return $"repeat tail +{relativeOffset.ToString(CultureInfo.InvariantCulture)}";
        }

        int continuationRelativeOffset = relativeOffset - candidate.ContinuationOffset;
        if (continuationRelativeOffset < 0)
        {
            return "pre-continuation";
        }

        if (continuationRelativeOffset < candidate.ContinuationBytes)
        {
            return continuationRelativeOffset >= 4
                ? $"continuation +{continuationRelativeOffset.ToString(CultureInfo.InvariantCulture)} / post-marker +{(continuationRelativeOffset - 4).ToString(CultureInfo.InvariantCulture)}"
                : $"continuation +{continuationRelativeOffset.ToString(CultureInfo.InvariantCulture)}";
        }

        return $"post-continuation +{(continuationRelativeOffset - candidate.ContinuationBytes).ToString(CultureInfo.InvariantCulture)}";
    }

    private static string FormatProtocol03SelectorFfRepeatListTargetBodySpan(Protocol03SelectorFfRepeatListCandidate candidate, int payloadOffset)
    {
        int headerEnd = payloadOffset + 2;
        if (payloadOffset >= 9 && headerEnd <= candidate.PositionOffset)
        {
            int bytesAfterHeaderBeforePosition = Math.Max(0, candidate.PositionOffset - headerEnd);
            return $"pre-position gap {bytesAfterHeaderBeforePosition.ToString(CultureInfo.InvariantCulture)}";
        }

        int positionEnd = candidate.PositionOffset + 24;
        int continuationOffset = positionEnd + candidate.ContinuationOffset;
        int continuationRelativeOffset = payloadOffset - continuationOffset;
        if (continuationRelativeOffset >= 0 && continuationRelativeOffset < candidate.ContinuationBytes)
        {
            int bytesAfterHeader = Math.Max(0, candidate.ContinuationBytes - (continuationRelativeOffset + 2));
            return $"continuation tail {bytesAfterHeader.ToString(CultureInfo.InvariantCulture)}";
        }

        return "not body-span target";
    }

    private static string FormatProtocol03SelectorFfRepeatListWindowU32Value(string windowHex)
    {
        byte[] bytes = ParseHexBytes(windowHex).ToArray();
        return bytes.Length == 4
            ? $"{windowHex} / {BinaryPrimitives.ReadUInt32LittleEndian(bytes).ToString(CultureInfo.InvariantCulture)}"
            : windowHex;
    }

    private static int MathMod(int value, int modulus)
    {
        return ((value % modulus) + modulus) % modulus;
    }

    private static string ClassifyProtocol03SelectorFfRepeatListEntryHeaderInterpretation(
        Protocol03SelectorFfRepeatListCandidate candidate,
        int payloadOffset,
        int entryOffset,
        int entryIndex)
    {
        string prefix = entryIndex == 1 ? "repeat entry 1" : "repeat entry 2";
        if (entryIndex == 1)
        {
            if (candidate.Entry1FieldOffset is int fieldOffset &&
                OverlapsProtocol03SelectorFfRepeatListHeaderField(payloadOffset, entryOffset + fieldOffset, 2))
            {
                return $"{prefix} primary field overlap";
            }

            if (candidate.Entry1SecondaryFieldOffset is int secondaryFieldOffset &&
                OverlapsProtocol03SelectorFfRepeatListHeaderField(payloadOffset, entryOffset + secondaryFieldOffset, 2))
            {
                return $"{prefix} secondary field overlap";
            }

            if (candidate.Entry1EffectiveFieldOffset is int effectiveFieldOffset &&
                OverlapsProtocol03SelectorFfRepeatListHeaderField(payloadOffset, entryOffset + effectiveFieldOffset, 2))
            {
                return $"{prefix} effective field overlap";
            }
        }
        else
        {
            if (candidate.Entry2FieldOffset is int fieldOffset &&
                OverlapsProtocol03SelectorFfRepeatListHeaderField(payloadOffset, entryOffset + fieldOffset, 2))
            {
                return $"{prefix} primary field overlap";
            }

            if (candidate.Entry2SecondaryFieldOffset is int secondaryFieldOffset &&
                OverlapsProtocol03SelectorFfRepeatListHeaderField(payloadOffset, entryOffset + secondaryFieldOffset, 2))
            {
                return $"{prefix} secondary field overlap";
            }

            if (candidate.Entry2EffectiveFieldOffset is int effectiveFieldOffset &&
                OverlapsProtocol03SelectorFfRepeatListHeaderField(payloadOffset, entryOffset + effectiveFieldOffset, 2))
            {
                return $"{prefix} effective field overlap";
            }
        }

        return $"{prefix} unresolved bytes";
    }

    private static bool OverlapsProtocol03SelectorFfRepeatListHeaderField(int headerOffset, int fieldOffset, int fieldBytes)
    {
        const int headerBytes = 2;
        return headerOffset < fieldOffset + fieldBytes && headerOffset + headerBytes > fieldOffset;
    }

    private static Protocol03SelectorFfRepeatListHeaderWindowSummary[] BuildProtocol03SelectorFfRepeatListHeaderWindowSummaries(
        IEnumerable<Protocol03SelectorFfRepeatListHeaderOccurrence> occurrences,
        bool vendorOnly)
    {
        return occurrences
            .Where(occurrence => !vendorOnly || IsVendorHeader(occurrence.Header))
            .GroupBy(occurrence => new
            {
                occurrence.PayloadOffset,
                occurrence.Header.Name,
                occurrence.Header.Direction,
                occurrence.Header.EncodedHeader
            })
            .Select(group =>
            {
                string[] protocols = group
                    .Select(occurrence => occurrence.Header.Protocol)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(protocol => protocol, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                int protocolCount = Math.Max(1, protocols.Length);
                return new Protocol03SelectorFfRepeatListHeaderWindowSummary(
                    group.Key.PayloadOffset,
                    group.Key.Name,
                    group.Key.Direction,
                    group.Key.EncodedHeader,
                    protocols,
                    group.Count() / protocolCount,
                    CountProtocol03SelectorFfRepeatListHeaderOccurrenceValues(group, occurrence => $"+{occurrence.ObjectOffset.ToString(CultureInfo.InvariantCulture)}", protocolCount, 8),
                    CountProtocol03SelectorFfRepeatListHeaderOccurrenceValues(group, occurrence => occurrence.WindowHex, protocolCount, 8));
            })
            .Where(summary => summary.Count > 0)
            .OrderByDescending(summary => summary.Count)
            .ThenBy(summary => summary.PayloadOffset)
            .ThenBy(summary => summary.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(summary => summary.Direction, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyDictionary<string, int> CountProtocol03SelectorFfRepeatListHeaderOccurrenceValues(
        IEnumerable<Protocol03SelectorFfRepeatListHeaderOccurrence> occurrences,
        Func<Protocol03SelectorFfRepeatListHeaderOccurrence, string> selector,
        int protocolCount,
        int maxValues)
    {
        return occurrences
            .Select(selector)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .GroupBy(value => value, StringComparer.OrdinalIgnoreCase)
            .Select(group => new
            {
                Value = group.Key,
                Count = group.Count() / Math.Max(1, protocolCount)
            })
            .Where(group => group.Count > 0)
            .OrderByDescending(group => group.Count)
            .ThenBy(group => group.Value, StringComparer.OrdinalIgnoreCase)
            .Take(maxValues)
            .ToDictionary(group => group.Value, group => group.Count, StringComparer.OrdinalIgnoreCase);
    }

    private static int CountProtocol03SelectorFfRepeatListHeaderCandidates(
        IEnumerable<Protocol03SelectorFfRepeatListHeaderOccurrence> occurrences,
        bool vendorOnly)
    {
        return occurrences
            .Where(occurrence => !vendorOnly || IsVendorHeader(occurrence.Header))
            .Select(occurrence => CreateProtocol03SelectorFfRepeatListCandidateOccurrenceKey(occurrence.Candidate))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
    }

    private static int CountProtocol03SelectorFfRepeatListDistinctHeaderCandidates(
        IEnumerable<Protocol03SelectorFfRepeatListHeaderOccurrence> occurrences)
    {
        return occurrences
            .Select(occurrence => CreateProtocol03SelectorFfRepeatListCandidateOccurrenceKey(occurrence.Candidate))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
    }

    private static int CountProtocol03SelectorFfRepeatListDistinctHeaderShapes(
        IEnumerable<Protocol03SelectorFfRepeatListHeaderOccurrence> occurrences)
    {
        return occurrences
            .Select(occurrence => CreateProtocol03SelectorFfRepeatListEffectiveShapeKey(occurrence.Candidate))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
    }

    private static IEnumerable<Protocol03SelectorFfRepeatListHeaderOccurrence> DistinctProtocol03SelectorFfRepeatListHeaderCandidates(
        IEnumerable<Protocol03SelectorFfRepeatListHeaderOccurrence> occurrences)
    {
        return occurrences
            .GroupBy(occurrence => CreateProtocol03SelectorFfRepeatListCandidateOccurrenceKey(occurrence.Candidate), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First());
    }

    private static IEnumerable<Protocol03SelectorFfRepeatListHeaderOccurrence> DistinctProtocol03SelectorFfRepeatListHeaderWindows(
        IEnumerable<Protocol03SelectorFfRepeatListHeaderOccurrence> occurrences)
    {
        return occurrences
            .GroupBy(CreateProtocol03SelectorFfRepeatListHeaderWindowOccurrenceKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First());
    }

    private static Protocol03SelectorFfRepeatListHeaderNameShapeSummary[] BuildProtocol03SelectorFfRepeatListHeaderNameShapeSummaries(
        IEnumerable<Protocol03SelectorFfRepeatListHeaderOccurrence> occurrences)
    {
        return occurrences
            .GroupBy(occurrence => CreateProtocol03SelectorFfRepeatListEffectiveShapeKey(occurrence.Candidate), StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                Protocol03SelectorFfRepeatListHeaderOccurrence sample = group.First();
                Protocol03SelectorFfRepeatListCandidate candidate = sample.Candidate;
                Protocol03SelectorFfRepeatListHeaderOccurrence[] distinctCandidates = DistinctProtocol03SelectorFfRepeatListHeaderCandidates(group).ToArray();
                Protocol03SelectorFfRepeatListHeaderOccurrence[] distinctWindows = DistinctProtocol03SelectorFfRepeatListHeaderWindows(group).ToArray();
                return new Protocol03SelectorFfRepeatListHeaderNameShapeSummary(
                    candidate.RepeatStride,
                    candidate.RepeatRecordCount,
                    candidate.ContinuationOffset,
                    candidate.ContinuationPrefixHex,
                    candidate.ContinuationPrefixBeforeMarkerHex,
                    candidate.ContinuationMarkerHex,
                    candidate.Entry1ZeroRun,
                    candidate.Entry1FieldOffset,
                    candidate.Entry1FieldHex,
                    candidate.Entry1FieldValue,
                    candidate.Entry1FieldLayoutKind,
                    candidate.Entry2EffectiveFieldHex,
                    candidate.Entry2EffectiveFieldValue,
                    candidate.PostMarkerZeroRun,
                    candidate.PostMarkerFirstFieldOffset,
                    candidate.PostMarkerFirstFieldHex,
                    candidate.PostMarkerFirstFieldValue,
                    candidate.PostMarkerFieldLayoutKind,
                    distinctCandidates.Length,
                    distinctWindows.Length,
                    CountProtocol03SelectorFfRepeatListHeaderOccurrenceValues(distinctWindows, occurrence => $"+{occurrence.PayloadOffset.ToString(CultureInfo.InvariantCulture)}", 1, 8),
                    CountProtocol03SelectorFfRepeatListHeaderOccurrenceValues(distinctWindows, occurrence => $"+{occurrence.ObjectOffset.ToString(CultureInfo.InvariantCulture)}", 1, 8),
                    CountProtocol03SelectorFfRepeatListHeaderOccurrenceValues(distinctWindows, occurrence => occurrence.WindowHex, 1, 8),
                    CountProtocol03SelectorFfRepeatListHeaderOccurrenceValues(distinctCandidates, occurrence => occurrence.Candidate.Entry2EffectiveFieldSource, 1, 8),
                    CountProtocol03SelectorFfRepeatListHeaderOccurrenceValues(
                        distinctCandidates,
                        occurrence => FormatProtocol03SelectorFfRepeatListFieldLabel(occurrence.Candidate.Entry2FieldHex, occurrence.Candidate.Entry2FieldValue),
                        1,
                        8),
                    CountProtocol03SelectorFfRepeatListHeaderOccurrenceValues(
                        distinctCandidates,
                        occurrence => FormatProtocol03SelectorFfRepeatListOptionalFieldLabel(occurrence.Candidate.Entry2SecondaryFieldHex, occurrence.Candidate.Entry2SecondaryFieldValue),
                        1,
                        8),
                    CountProtocol03SelectorFfRepeatListHeaderOccurrenceValues(distinctCandidates, occurrence => occurrence.Candidate.FirstSelector, 1, 8),
                    CountProtocol03SelectorFfRepeatListHeaderOccurrenceValues(distinctCandidates, occurrence => occurrence.Candidate.SegmentClassification, 1, 8),
                    distinctCandidates
                        .Select(occurrence => occurrence.Candidate)
                        .GroupBy(candidateOccurrence => new { candidateOccurrence.File, candidateOccurrence.Line, candidateOccurrence.PositionOffset })
                        .Select(positionGroup => positionGroup.First())
                        .OrderBy(candidateOccurrence => candidateOccurrence.File, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(candidateOccurrence => candidateOccurrence.Line)
                        .ThenBy(candidateOccurrence => candidateOccurrence.PositionOffset)
                        .Take(5)
                        .Select(candidateOccurrence => new Protocol03SelectorFfRepeatListShapePositionSample(
                            candidateOccurrence.File,
                            candidateOccurrence.Line,
                            candidateOccurrence.PositionOffset,
                            candidateOccurrence.PositionHex,
                            candidateOccurrence.X,
                            candidateOccurrence.Y,
                            candidateOccurrence.Z))
                        .ToArray(),
                    candidate.File,
                    candidate.Line);
            })
            .OrderByDescending(summary => summary.CandidateCount)
            .ThenByDescending(summary => summary.HeaderWindowCount)
            .ThenBy(summary => summary.Entry1ZeroRun)
            .ThenBy(summary => summary.Entry1FieldOffset ?? int.MaxValue)
            .ThenBy(summary => summary.Entry1FieldHex, StringComparer.OrdinalIgnoreCase)
            .ThenBy(summary => summary.Entry2EffectiveFieldHex, StringComparer.OrdinalIgnoreCase)
            .ThenBy(summary => summary.ContinuationPrefixHex, StringComparer.OrdinalIgnoreCase)
            .ThenBy(summary => summary.PostMarkerZeroRun)
            .ThenBy(summary => summary.PostMarkerFirstFieldOffset ?? int.MaxValue)
            .ThenBy(summary => summary.PostMarkerFirstFieldHex, StringComparer.OrdinalIgnoreCase)
            .Take(12)
            .ToArray();
    }

    private static bool IsVendorHeader(RpcHeaderEntry header)
    {
        return header.Name.Contains("VENDOR", StringComparison.OrdinalIgnoreCase);
    }

    private static string CreateProtocol03SelectorFfRepeatListCandidateLeadKey(string file, int line, int selectorOffset)
    {
        return string.Join('\u001f', file, line.ToString(CultureInfo.InvariantCulture), selectorOffset.ToString(CultureInfo.InvariantCulture));
    }

    private static string CreateProtocol03SelectorFfRepeatListCandidateOccurrenceKey(Protocol03SelectorFfRepeatListCandidate candidate)
    {
        return string.Join(
            '\u001f',
            candidate.File,
            candidate.Line.ToString(CultureInfo.InvariantCulture),
            candidate.SelectorOffset.ToString(CultureInfo.InvariantCulture),
            candidate.PositionOffset.ToString(CultureInfo.InvariantCulture));
    }

    private static string CreateProtocol03SelectorFfRepeatListHeaderWindowOccurrenceKey(Protocol03SelectorFfRepeatListHeaderOccurrence occurrence)
    {
        return string.Join(
            '\u001f',
            CreateProtocol03SelectorFfRepeatListCandidateOccurrenceKey(occurrence.Candidate),
            occurrence.PayloadOffset.ToString(CultureInfo.InvariantCulture),
            occurrence.ObjectOffset.ToString(CultureInfo.InvariantCulture),
            occurrence.WindowHex,
            occurrence.Header.Name,
            occurrence.Header.Direction,
            occurrence.Header.EncodedHeader);
    }

    private static string CreateProtocol03SelectorFfRepeatListEffectiveShapeKey(Protocol03SelectorFfRepeatListCandidate candidate)
    {
        return string.Join(
            '\u001f',
            candidate.RepeatStride.ToString(CultureInfo.InvariantCulture),
            candidate.RepeatRecordCount.ToString(CultureInfo.InvariantCulture),
            candidate.ContinuationOffset.ToString(CultureInfo.InvariantCulture),
            candidate.ContinuationPrefixHex,
            candidate.ContinuationPrefixBeforeMarkerHex,
            candidate.ContinuationMarkerHex,
            candidate.Entry1ZeroRun.ToString(CultureInfo.InvariantCulture),
            candidate.Entry1FieldOffset?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            candidate.Entry1FieldHex,
            candidate.Entry1FieldValue?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            candidate.Entry1FieldLayoutKind,
            candidate.Entry2EffectiveFieldHex,
            candidate.Entry2EffectiveFieldValue?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            candidate.PostMarkerZeroRun.ToString(CultureInfo.InvariantCulture),
            candidate.PostMarkerFirstFieldOffset?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            candidate.PostMarkerFirstFieldHex,
            candidate.PostMarkerFirstFieldValue?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            candidate.PostMarkerFieldLayoutKind);
    }

    private static string FormatProtocol03SelectorFfRepeatListFieldLabel(string fieldHex, int? fieldValue)
    {
        if (string.IsNullOrWhiteSpace(fieldHex))
        {
            return "none";
        }

        return fieldValue is int value
            ? $"{fieldHex} / {value.ToString(CultureInfo.InvariantCulture)}"
            : fieldHex;
    }

    private static string FormatProtocol03SelectorFfRepeatListOptionalFieldLabel(string fieldHex, int? fieldValue)
    {
        return string.IsNullOrWhiteSpace(fieldHex)
            ? string.Empty
            : FormatProtocol03SelectorFfRepeatListFieldLabel(fieldHex, fieldValue);
    }

    private static IReadOnlyDictionary<string, int> CountProtocol03SelectorFfRepeatListWeightedValues(
        IEnumerable<WeightedValue> values,
        int maxValues)
    {
        return values
            .Where(value => !string.IsNullOrWhiteSpace(value.Value))
            .GroupBy(value => value.Value, StringComparer.OrdinalIgnoreCase)
            .Select(group => new { Value = group.Key, Count = group.Sum(value => value.Count) })
            .OrderByDescending(entry => entry.Count)
            .ThenBy(entry => entry.Value, StringComparer.OrdinalIgnoreCase)
            .Take(maxValues)
            .ToDictionary(entry => entry.Value, entry => entry.Count, StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyDictionary<string, int> MergeProtocol03SelectorFfRepeatListCountDictionaries(
        IEnumerable<IReadOnlyDictionary<string, int>> dictionaries,
        int maxValues)
    {
        Dictionary<string, int> counts = new(StringComparer.OrdinalIgnoreCase);
        foreach (IReadOnlyDictionary<string, int> dictionary in dictionaries)
        {
            foreach (KeyValuePair<string, int> entry in dictionary)
            {
                counts[entry.Key] = counts.GetValueOrDefault(entry.Key) + entry.Value;
            }
        }

        return counts
            .OrderByDescending(entry => entry.Value)
            .ThenBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
            .Take(maxValues)
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> FindProtocol03SelectorFfRepeatListFieldResourceReferences(
        int? fieldValue,
        IReadOnlyDictionary<int, IReadOnlyList<string>> resourceReferencesByValue)
    {
        return fieldValue is int value && resourceReferencesByValue.TryGetValue(value, out IReadOnlyList<string>? resourceReferences)
            ? resourceReferences
            : Array.Empty<string>();
    }

    private static IReadOnlyDictionary<string, int> CountProtocol03SelectorFfRepeatListResourceReferences(
        IEnumerable<Protocol03SelectorFfRepeatListCandidate> candidates,
        Func<Protocol03SelectorFfRepeatListCandidate, int?> fieldValueSelector,
        IReadOnlyDictionary<int, IReadOnlyList<string>> resourceReferencesByValue,
        int maxValues)
    {
        return CountProtocol03SelectorFfRepeatListWeightedValues(
            candidates.SelectMany(candidate => FindProtocol03SelectorFfRepeatListFieldResourceReferences(fieldValueSelector(candidate), resourceReferencesByValue)
                .Select(reference => new WeightedValue(reference, 1))),
            maxValues);
    }

    private static IReadOnlyDictionary<string, int> CountProtocol03SelectorFfRepeatListHeaderOccurrenceResourceReferences(
        IEnumerable<Protocol03SelectorFfRepeatListHeaderOccurrence> occurrences,
        Func<Protocol03SelectorFfRepeatListHeaderOccurrence, int?> fieldValueSelector,
        IReadOnlyDictionary<int, IReadOnlyList<string>> resourceReferencesByValue,
        int maxValues)
    {
        return CountProtocol03SelectorFfRepeatListWeightedValues(
            occurrences.SelectMany(occurrence => FindProtocol03SelectorFfRepeatListFieldResourceReferences(fieldValueSelector(occurrence), resourceReferencesByValue)
                .Select(reference => new WeightedValue(reference, 1))),
            maxValues);
    }

    private static IReadOnlyDictionary<int, IReadOnlyList<string>> BuildProtocol03SelectorFfRepeatListFieldResourceReferenceIndex(
        IReadOnlyList<AbilityDefinition> abilities,
        IReadOnlyList<ItemCommandEntry> items,
        IReadOnlyList<GameObjectEntry> gameObjects,
        IReadOnlyList<AnimationDefinition> animations)
    {
        Dictionary<int, List<string>> referencesByValue = new();

        void Add(int value, string reference)
        {
            if (value is < 0 or > ushort.MaxValue || string.IsNullOrWhiteSpace(reference))
            {
                return;
            }

            if (!referencesByValue.TryGetValue(value, out List<string>? references))
            {
                references = new List<string>();
                referencesByValue[value] = references;
            }

            if (!references.Contains(reference, StringComparer.OrdinalIgnoreCase))
            {
                references.Add(reference);
            }
        }

        foreach (ItemCommandEntry item in items.OrderBy(item => item.ItemId).ThenBy(item => item.Symbol, StringComparer.OrdinalIgnoreCase))
        {
            if (item.ItemId > ushort.MaxValue)
            {
                continue;
            }

            string display = string.IsNullOrWhiteSpace(item.DisplayName)
                ? string.Empty
                : $" \"{item.DisplayName}\"";
            Add((int)item.ItemId, $"item:{item.ItemId} {item.Symbol}{display}");
        }

        foreach (GameObjectEntry gameObject in gameObjects.OrderBy(entry => entry.GoId).ThenBy(entry => entry.CodeName, StringComparer.OrdinalIgnoreCase))
        {
            Add(gameObject.GoId, $"gameobject:{gameObject.GoId} {gameObject.CodeName}");
        }

        foreach (AnimationDefinition animation in animations.OrderBy(animation => animation.Value).ThenBy(animation => animation.Name, StringComparer.OrdinalIgnoreCase))
        {
            Add(animation.Value, $"animation:{FormatCompactHexAsSpacedHex(animation.LittleEndianHex)} {animation.Name}");
        }

        foreach (AbilityDefinition ability in abilities.OrderBy(ability => ability.AbilityId).ThenBy(ability => ability.CodeName, StringComparer.OrdinalIgnoreCase))
        {
            if (ability.AbilityId > ushort.MaxValue)
            {
                continue;
            }

            string display = string.IsNullOrWhiteSpace(ability.DisplayName)
                ? string.Empty
                : $" \"{ability.DisplayName}\"";
            Add((int)ability.AbilityId, $"ability:{ability.AbilityId} {ability.CodeName}{display}");
        }

        return referencesByValue.ToDictionary(
            entry => entry.Key,
            entry => (IReadOnlyList<string>)entry.Value.Take(8).ToArray());
    }

    private static IReadOnlyDictionary<string, int> CountProtocol03SelectorFfRepeatListFieldSummaryValues(IEnumerable<string?> values, int maxValues)
    {
        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .GroupBy(value => value!, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Take(maxValues)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<Protocol03NestedMovementObservation> EnumerateProtocol03NestedMovementObservations(
        IEnumerable<PacketDumpFileSummary> dumpSummaries)
    {
        foreach (PacketDumpFileSummary summary in dumpSummaries)
        {
            foreach (Protocol03ObjectViewSample view in summary.Protocol03ObjectViews)
            {
                foreach (Protocol03NestedMovementLead lead in view.NestedMovementLeads)
                {
                    yield return new Protocol03NestedMovementObservation(
                        summary.File,
                        view.Line,
                        view,
                        lead,
                        IsProtocol03NestedMovementBoundedPrimaryWrapper(view, lead));
                }
            }
        }
    }

    private static bool IsProtocol03NestedMovementBoundedPrimaryWrapper(
        Protocol03ObjectViewSample sample,
        Protocol03NestedMovementLead lead)
    {
        return sample.Segments.Any(segment =>
            segment.Classification.Equals("primary movement wrapper", StringComparison.Ordinal) &&
            segment.Selector.Equals(lead.OuterSelector, StringComparison.OrdinalIgnoreCase) &&
            segment.Offset + 1 == lead.Offset);
    }

    private static string ClassifyProtocol03NestedMovementEvidence(Protocol03NestedMovementObservation observation)
    {
        if (observation.IsBoundedPrimaryWrapper)
        {
            return "bounded primary movement wrapper";
        }

        if (observation.Lead.MarkerModeHex.Equals("06", StringComparison.OrdinalIgnoreCase))
        {
            return "unbounded mode 06 object-state movement lead";
        }

        if (!string.IsNullOrWhiteSpace(observation.Lead.SuffixHex))
        {
            return "unresolved nested movement suffix lead";
        }

        return "unresolved nested movement payload lead";
    }

    private static (string Action, string Reason) GetProtocol03NestedMovementParserAction(string evidenceDisposition)
    {
        return evidenceDisposition switch
        {
            "unbounded mode 06 object-state movement lead" => (
                "queue mode 06 object-state boundary investigation",
                "mode 06 windows are unresolved and have no bounded wrapper examples"),
            "unresolved nested movement suffix lead" => (
                "queue suffix boundary comparison",
                "unresolved movement window has bytes after the inner movement payload"),
            "unresolved nested movement payload lead" => (
                "keep as low-priority embedded movement lead",
                "no repeated suffix evidence distinguishes the current payload"),
            "bounded primary movement wrapper" => (
                "keep bounded primary movement wrapper",
                "known selector 12/13/14 wrapper length brackets the movement payload"),
            _ => (
                "review nested movement evidence",
                "unclassified nested movement evidence disposition")
        };
    }

    private static int GetProtocol03NestedMovementParserActionOrder(string parserAction)
    {
        return parserAction switch
        {
            "queue mode 06 object-state boundary investigation" => 0,
            "queue suffix boundary comparison" => 1,
            "keep as low-priority embedded movement lead" => 2,
            "keep bounded primary movement wrapper" => 3,
            _ => 4
        };
    }

    private static IReadOnlyDictionary<string, int> CountProtocol03NestedMovementValues(
        IEnumerable<string?> values,
        int maxValues)
    {
        return CountProtocol03SelectorFfRepeatListFieldSummaryValues(values, maxValues);
    }

    private static IReadOnlyDictionary<string, int> CountProtocol03NestedMovementWeightedValues(
        IEnumerable<WeightedValue> values,
        int maxValues)
    {
        return CountProtocol03SelectorFfRepeatListWeightedValues(values, maxValues);
    }

    private static string FormatProtocol03NestedMovementEmptyHex(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "-" : value;
    }

    private static string FormatProtocol03NestedMovementPosition(Protocol03NestedMovementLead lead)
    {
        return string.Format(CultureInfo.InvariantCulture, "{0:0.###},{1:0.###},{2:0.###}", lead.X, lead.Y, lead.Z);
    }

    private static string ClassifyProtocol03NestedMovementMode06TailPrefix(Protocol03NestedMovementLead lead)
    {
        byte[] tail = GetProtocol03NestedMovementTailByteValues(lead).ToArray();
        if (tail.Length == 0)
        {
            return "no tail after movement payload";
        }

        if (tail.Length >= 12 &&
            tail[0] == 0xff &&
            tail[1] == 0x00 &&
            tail[2] == 0x00 &&
            tail[3] == 0x00 &&
            tail[8] == 0x00 &&
            tail[9] == 0x00 &&
            tail[10] == 0x01 &&
            tail[11] == 0xff)
        {
            return "ff marker plus zero-padded ff continuation lead";
        }

        if (tail.Length >= 4 &&
            tail[0] == 0xff &&
            tail[1] == 0x00 &&
            tail[2] == 0x00 &&
            tail[3] == 0x00)
        {
            return "ff marker-prefixed tail";
        }

        if (tail.Length >= 6 && tail.Take(6).All(value => value >= 0x80))
        {
            return "packed high-bit tail prefix";
        }

        if (EnumerateProtocol03NestedMovementHeaderLikeTailLeads(tail).Any())
        {
            return "object-view header-like tail lead";
        }

        if (LooksLikeProtocol03SelectorLead(tail[0]))
        {
            return "selector-like tail prefix";
        }

        return "other tail prefix";
    }

    private static bool IsProtocol03NestedMovementMode06FfContinuationLead(IReadOnlyList<byte> tail)
    {
        return tail.Count >= 16 &&
            tail[0] == 0xff &&
            tail[1] == 0x00 &&
            tail[2] == 0x00 &&
            tail[3] == 0x00 &&
            tail[11] == 0xff;
    }

    private static int GetProtocol03NestedMovementTailStartOffset(Protocol03NestedMovementLead lead)
    {
        return lead.MarkerOffset + 3 + lead.InnerPayloadBytes;
    }

    private static int GetProtocol03NestedMovementCandidateCutOffset(Protocol03NestedMovementLead lead)
    {
        return GetProtocol03NestedMovementTailStartOffset(lead) + ParseHexBytes(lead.SuffixHex).Count();
    }

    private static int GetProtocol03NestedMovementTailByteCount(Protocol03NestedMovementLead lead)
    {
        return Math.Max(0, lead.PayloadBytes - GetProtocol03NestedMovementTailStartOffset(lead));
    }

    private static int? FindFirstNonZeroOffset(IReadOnlyList<byte> bytes)
    {
        for (int offset = 0; offset < bytes.Count; offset++)
        {
            if (bytes[offset] != 0)
            {
                return offset;
            }
        }

        return null;
    }

    private static string FormatProtocol03Byte(IReadOnlyList<byte> bytes, int offset)
    {
        return offset >= 0 && offset < bytes.Count
            ? bytes[offset].ToString("x2", CultureInfo.InvariantCulture)
            : string.Empty;
    }

    private static string ClassifyProtocol03NestedMovementMode06PostPrefixTuple(
        IReadOnlyList<byte> bytes,
        int offset,
        bool looksLikeObjectView,
        string postPrefixDisposition)
    {
        if (offset + 7 >= bytes.Count)
        {
            return "short post-prefix tuple";
        }

        if (looksLikeObjectView)
        {
            return postPrefixDisposition.Equals("unclassified object-view update", StringComparison.Ordinal)
                ? "unclassified object-view tuple"
                : "accepted object-view tuple";
        }

        if (bytes[offset + 2] == 0x00 &&
            bytes[offset + 3] == 0x00 &&
            bytes[offset + 4] == 0x00 &&
            bytes[offset + 5] == 0x00 &&
            bytes[offset + 6] == 0x00)
        {
            return "zero-update marker tuple";
        }

        if (bytes[offset + 4] == 0x00 &&
            bytes[offset + 5] == 0x00 &&
            bytes[offset + 6] == 0x00)
        {
            return "non-object marker tuple";
        }

        return "non-object post-prefix tuple";
    }

    private static IEnumerable<byte> GetProtocol03NestedMovementTailByteValues(Protocol03NestedMovementLead lead)
    {
        return ParseHexBytes($"{lead.SuffixHex} {lead.PostSuffixHex}");
    }

    private static IEnumerable<byte> GetProtocol03NestedMovementFullTailByteValues(Protocol03NestedMovementLead lead)
    {
        byte[] payload = ParseHexBytes(lead.PayloadHex).ToArray();
        return payload.Skip(GetProtocol03NestedMovementTailStartOffset(lead));
    }

    private static IEnumerable<string> EnumerateProtocol03NestedMovementHeaderLikeTailLeads(Protocol03NestedMovementLead lead)
    {
        return EnumerateProtocol03NestedMovementHeaderLikeTailLeads(GetProtocol03NestedMovementTailByteValues(lead).ToArray());
    }

    private static IEnumerable<string> EnumerateProtocol03NestedMovementHeaderLikeTailLeads(IReadOnlyList<byte> tail)
    {
        int maxCandidate = Math.Min(12, tail.Count - 4);
        for (int offset = 0; offset <= maxCandidate; offset++)
        {
            if (!LooksLikeProtocol03ObjectViewHeader(tail, offset))
            {
                continue;
            }

            int viewId = ReadUInt16LittleEndian(tail, offset);
            byte updateCount = tail[offset + 2];
            byte firstSelector = tail[offset + 3];
            string classification = ClassifyProtocol03ObjectView(updateCount, firstSelector);
            if (classification.Equals("unclassified object-view update", StringComparison.Ordinal))
            {
                continue;
            }

            yield return $"{offset}: view {viewId} count {updateCount:x2} selector {firstSelector:x2} {classification}";
        }
    }

    private static bool LooksLikeProtocol03SelectorLead(byte value)
    {
        return value is
            0x00 or 0x01 or 0x02 or 0x04 or 0x06 or 0x08 or 0x09 or 0x0a or 0x0c or 0x0e or
            0x12 or 0x13 or 0x14 or 0x28 or 0x2a or 0x2e or 0x3c or 0x57 or 0x80 or 0x9b or 0x9e or 0x9f or 0xa0;
    }

    private static IEnumerable<Protocol03NpcBaseDynamicCreationObservation> EnumerateProtocol03NpcBaseDynamicCreationObservations(
        IEnumerable<PacketDumpFileSummary> dumpSummaries)
    {
        foreach (PacketDumpFileSummary summary in dumpSummaries)
        {
            foreach (Protocol03ObjectViewSample view in summary.Protocol03ObjectViews)
            {
                foreach (Protocol03NamedProfileRecord record in view.NamedProfileRecords.Where(record => record.GameObjectId == 599))
                {
                    yield return new Protocol03NpcBaseDynamicCreationObservation(summary.File, view.Line, record);
                }
            }
        }
    }

    private static IReadOnlyDictionary<string, int> CountProtocol03NpcBaseDynamicCreationSummaryValues(
        IEnumerable<string?> values,
        int maxValues)
    {
        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .GroupBy(value => value!, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Take(maxValues)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
    }

    private static string FormatProtocol03NpcBaseDynamicCreationSummaryHex(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "-" : value;
    }

    private static string FormatProtocol03NpcBaseDynamicCreationBool(bool value)
    {
        return value ? "yes" : "no";
    }

    private static string FormatProtocol03NpcBaseDynamicCreationParentPreValue(Protocol03NamedProfileRecord record)
    {
        return TryReadProtocol03NpcBaseThreeByteZeroTailPreHeaderValue(record.PostCreationObjectViewPreHeaderHex)?.ToString("x2", CultureInfo.InvariantCulture) ?? "-";
    }

    private static int? TryReadProtocol03NpcBaseThreeByteZeroTailPreHeaderValue(string value)
    {
        byte[] bytes = ParseHexBytes(value).ToArray();
        return bytes.Length == 3 && bytes[1] == 0 && bytes[2] == 0 ? bytes[0] : null;
    }

    private static string ClassifyProtocol03NpcBaseDynamicCreationTailPrefix(string value)
    {
        byte[] bytes = ParseHexBytes(value).ToArray();
        if (bytes.Length >= 8 &&
            bytes[1] == 0x00 &&
            bytes[2] == 0x00 &&
            bytes[3] == 0x01 &&
            bytes[4] == 0x00 &&
            bytes[5] == 0x0c &&
            bytes[6] == 0x57 &&
            bytes[7] == 0x02)
        {
            return "next dynamic object-view boundary";
        }

        if (bytes.Length == 5 &&
            bytes[1] == 0x00 &&
            bytes[2] == 0x00 &&
            bytes[3] == 0x00 &&
            bytes[4] == 0x00)
        {
            return "five-byte zero tail lead";
        }

        if (bytes.Length >= 6 &&
            bytes[1] == 0x00 &&
            bytes[2] == 0x00 &&
            bytes[3] == 0x00 &&
            bytes[4] == 0x00 &&
            bytes[5] == 0x04 &&
            ContainsProtocol03NpcBaseDynamicCreationBytePair(bytes, 0x16, 0x80))
        {
            return "zero-extended 04/16 80 tail lead";
        }

        if (bytes.Length >= 2 && bytes[0] == 0x02 && bytes[1] == 0x03 && ContainsProtocol03NpcBaseDynamicCreationBytePair(bytes, 0x16, 0x80))
        {
            return "02 03/16 80 tail lead";
        }

        if (EnumerateProtocol03NpcBaseDynamicTailHeaderLikeLeadInfos(value).Any())
        {
            return "header-like object-view tail lead";
        }

        if (ContainsProtocol03NpcBaseDynamicCreationBytePair(bytes, 0x16, 0x80))
        {
            return "16 80-bearing tail lead";
        }

        return "other tail lead";
    }

    private static IEnumerable<string> EnumerateProtocol03NpcBaseDynamicTailHeaderLikeLeads(string value)
    {
        return EnumerateProtocol03NpcBaseDynamicTailHeaderLikeLeadInfos(value).Select(lead => lead.Label);
    }

    private static IEnumerable<Protocol03NpcBaseDynamicTailHeaderLikeLead> EnumerateProtocol03NpcBaseDynamicTailHeaderLikeLeadInfos(string value)
    {
        byte[] bytes = ParseHexBytes(value).ToArray();
        int maxCandidate = Math.Min(8, bytes.Length - 4);
        for (int offset = 0; offset <= maxCandidate; offset++)
        {
            if (!IsProtocol03NpcBaseDynamicTailHeaderLikeOffset(bytes, offset))
            {
                continue;
            }

            int viewId = bytes[offset] | (bytes[offset + 1] << 8);
            byte updateCount = bytes[offset + 2];
            byte firstSelector = bytes[offset + 3];
            if (viewId == 0 || updateCount == 0 || updateCount > 0x20)
            {
                continue;
            }

            yield return new Protocol03NpcBaseDynamicTailHeaderLikeLead(offset, viewId, updateCount, firstSelector);
        }
    }

    private static bool IsProtocol03NpcBaseDynamicTailHeaderLikeOffset(IReadOnlyList<byte> bytes, int offset)
    {
        return offset switch
        {
            0 => true,
            3 => bytes.Count > 2 && bytes[1] == 0x00 && bytes[2] == 0x00,
            4 => bytes.Count > 3 && bytes[1] == 0x00 && bytes[2] == 0x00 && bytes[3] == 0x00,
            7 => bytes.Count > 6 &&
                bytes[1] == 0x00 &&
                bytes[2] == 0x00 &&
                bytes[3] == 0x01 &&
                bytes[4] == 0x00 &&
                bytes[5] == 0x08 &&
                bytes[6] == 0x50 &&
                offset + 3 < bytes.Count &&
                bytes[offset + 3] == 0x80,
            _ => false
        };
    }

    private static string FormatProtocol03NpcBaseDynamicTailBodyHints(
        string value,
        Protocol03NpcBaseDynamicTailHeaderLikeLead lead)
    {
        byte[] bytes = ParseHexBytes(value).ToArray();
        int bodyOffset = lead.Offset + 4;
        if (bodyOffset >= bytes.Length)
        {
            return "-";
        }

        List<string> hints = new()
        {
            $"body {FormatHeader(bytes.Skip(bodyOffset).Take(Math.Min(8, bytes.Length - bodyOffset)))}"
        };

        string[] separators = EnumerateProtocol03NpcBaseDynamicCreationBytePairOffsets(bytes, bodyOffset, 0xcd, 0xab)
            .Take(3)
            .Select(offset => $"cd ab @+{offset - bodyOffset}")
            .ToArray();
        if (separators.Length > 0)
        {
            hints.Add(string.Join(", ", separators));
        }

        return string.Join("; ", hints);
    }

    private static bool TryCreateProtocol03NpcBaseDynamicTailBodyAnchor(
        Protocol03NpcBaseDynamicCreationObservation observation,
        Protocol03NpcBaseDynamicTailHeaderLikeLead lead,
        out Protocol03NpcBaseDynamicCreationBodyAnchor? anchor)
    {
        byte[] bytes = ParseHexBytes(observation.Record.PostCreationObjectViewDynamicTailPrefixHex).ToArray();
        int bodyOffset = lead.Offset + 4;
        if (bodyOffset >= bytes.Length)
        {
            anchor = null;
            return false;
        }

        foreach (int separatorOffset in EnumerateProtocol03NpcBaseDynamicCreationBytePairOffsets(bytes, bodyOffset, 0xcd, 0xab))
        {
            anchor = new Protocol03NpcBaseDynamicCreationBodyAnchor(
                observation,
                lead,
                separatorOffset - bodyOffset,
                FormatHeader(bytes.Skip(bodyOffset).Take(separatorOffset - bodyOffset)),
                FormatHeader(bytes.Skip(separatorOffset).Take(Math.Min(8, bytes.Length - separatorOffset))));
            return true;
        }

        anchor = null;
        return false;
    }

    private static IEnumerable<int> EnumerateProtocol03NpcBaseDynamicCreationBytePairOffsets(
        IReadOnlyList<byte> bytes,
        int offset,
        byte first,
        byte second)
    {
        for (int i = Math.Max(0, offset); i + 1 < bytes.Count; i++)
        {
            if (bytes[i] == first && bytes[i + 1] == second)
            {
                yield return i;
            }
        }
    }

    private static bool ContainsProtocol03NpcBaseDynamicCreationBytePair(IReadOnlyList<byte> bytes, byte first, byte second)
    {
        return EnumerateProtocol03NpcBaseDynamicCreationBytePairOffsets(bytes, 0, first, second).Any();
    }

    private static IEnumerable<Protocol03SelectorFfRepeatListCandidate> CreateProtocol03SelectorFfRepeatListCandidates(
        string file,
        Protocol03ObjectViewSample view,
        Protocol03VariableSelectorLead lead)
    {
        byte[] payload = ParseHexBytes(lead.PayloadHex).ToArray();
        if (payload.Length < 56)
        {
            yield break;
        }

        string segmentClassification = FindProtocol03VariableLeadSegment(view, lead)?.Classification ?? "variable selector payload";
        for (int positionOffset = 32; positionOffset <= payload.Length - 24; positionOffset++)
        {
            if (!TryReadDoubleLittleEndian(payload, positionOffset, out double x) ||
                !TryReadDoubleLittleEndian(payload, positionOffset + 8, out double y) ||
                !TryReadDoubleLittleEndian(payload, positionOffset + 16, out double z) ||
                !IsPlausibleProtocol03SelectorFfPosition(x, y, z))
            {
                continue;
            }

            int postPositionOffset = positionOffset + 24;
            byte[] postPositionTail = payload.Skip(postPositionOffset).ToArray();
            if (postPositionTail.Length == 0)
            {
                continue;
            }

            int postPositionScanOffset = 1;
            while (postPositionScanOffset < postPositionTail.Length && postPositionTail[postPositionScanOffset] == 0)
            {
                postPositionScanOffset++;
            }

            if (postPositionScanOffset >= postPositionTail.Length)
            {
                continue;
            }

            int? repeatStride = null;
            for (int nextOffset = postPositionScanOffset + 2; nextOffset < postPositionTail.Length; nextOffset++)
            {
                if (postPositionTail[nextOffset] == 0xff)
                {
                    repeatStride = nextOffset;
                    break;
                }
            }

            if (repeatStride is not int stride)
            {
                continue;
            }

            int repeatRecordCount = CountProtocol03SelectorFfPostPositionRepeatRecords(postPositionTail, stride);
            if (repeatRecordCount < 2)
            {
                continue;
            }

            Protocol03RepeatListEntryField? entry1 = ReadProtocol03SelectorFfPostPositionRepeatRecordField(postPositionTail, 0, stride);
            Protocol03RepeatListEntryField? entry2 = ReadProtocol03SelectorFfPostPositionRepeatRecordField(postPositionTail, stride, stride);
            if (entry1 is null || entry2 is null)
            {
                continue;
            }

            int continuationOffset = stride * repeatRecordCount;
            if (continuationOffset >= postPositionTail.Length)
            {
                continue;
            }

            byte[] continuation = postPositionTail.Skip(continuationOffset).ToArray();
            if (continuation.Length < 4 || continuation[3] != 0xff)
            {
                continue;
            }

            int postMarkerOffset = 4;
            int postMarkerZeroRun = 0;
            while (postMarkerOffset < continuation.Length && continuation[postMarkerOffset] == 0)
            {
                postMarkerZeroRun++;
                postMarkerOffset++;
            }

            int? postMarkerFirstFieldOffset = postMarkerOffset + 1 < continuation.Length
                ? postMarkerOffset
                : null;
            string postMarkerFirstFieldHex = postMarkerFirstFieldOffset is int fieldOffset
                ? FormatHeader(continuation.Skip(fieldOffset).Take(2))
                : string.Empty;
            string entry1FieldLayoutKind = ClassifyProtocol03SelectorFfRepeatListEntryFieldLayout(entry1.ZeroRun, entry1.FieldOffset, entry1.FieldHex);
            string entry2FieldLayoutKind = ClassifyProtocol03SelectorFfRepeatListEntryFieldLayout(entry2.ZeroRun, entry2.FieldOffset, entry2.FieldHex);
            string postMarkerFieldLayoutKind = ClassifyProtocol03SelectorFfRepeatListPostMarkerFieldLayout(postMarkerZeroRun, postMarkerFirstFieldOffset, postMarkerFirstFieldHex);
            Protocol03EffectiveRepeatListEntryField entry1Effective = SelectProtocol03SelectorFfEffectiveEntryField(entry1, entry1FieldLayoutKind);
            Protocol03EffectiveRepeatListEntryField entry2Effective = SelectProtocol03SelectorFfEffectiveEntryField(entry2, entry2FieldLayoutKind);

            yield return new Protocol03SelectorFfRepeatListCandidate(
                file,
                view.Line,
                view.Direction,
                view.PacketOffset,
                view.ViewId,
                view.UpdateCount,
                view.FirstSelector,
                view.Classification,
                segmentClassification,
                lead.Offset,
                lead.PayloadBytes,
                positionOffset,
                FormatHeader(payload.Take(Math.Min(9, payload.Length))),
                FormatProtocol03PayloadSlice(payload, 9, 4),
                FormatHeader(payload.Skip(Math.Max(0, positionOffset - 8)).Take(Math.Min(8, positionOffset))),
                FormatProtocol03PayloadSlice(payload, positionOffset, 24),
                x,
                y,
                z,
                postPositionTail.Length,
                stride,
                repeatRecordCount,
                continuationOffset,
                continuation.Length,
                entry1.EntryLeadHex,
                entry1.ZeroRun,
                entry1.FieldOffset,
                entry1.FieldHex,
                TryReadProtocol03U16FieldValue(entry1.FieldHex),
                entry1FieldLayoutKind,
                entry1.SecondaryFieldOffset,
                entry1.SecondaryFieldHex,
                TryReadProtocol03U16FieldValue(entry1.SecondaryFieldHex),
                entry1Effective.FieldOffset,
                entry1Effective.FieldHex,
                entry1Effective.FieldValue,
                entry1Effective.Source,
                entry2.EntryLeadHex,
                entry2.ZeroRun,
                entry2.FieldOffset,
                entry2.FieldHex,
                TryReadProtocol03U16FieldValue(entry2.FieldHex),
                entry2FieldLayoutKind,
                entry2.SecondaryFieldOffset,
                entry2.SecondaryFieldHex,
                TryReadProtocol03U16FieldValue(entry2.SecondaryFieldHex),
                entry2Effective.FieldOffset,
                entry2Effective.FieldHex,
                entry2Effective.FieldValue,
                entry2Effective.Source,
                FormatProtocol03PayloadSlice(continuation, 0, 4),
                FormatProtocol03PayloadSlice(continuation, 0, 3),
                FormatProtocol03PayloadSlice(continuation, 3, 1),
                FormatProtocol03PayloadSlice(continuation, 4, 8),
                postMarkerZeroRun,
                postMarkerFirstFieldOffset,
                postMarkerFirstFieldHex,
                TryReadProtocol03U16FieldValue(postMarkerFirstFieldHex),
                postMarkerFieldLayoutKind);
        }
    }

    private static string ClassifyProtocol03SelectorFfRepeatListEntryFieldLayout(int zeroRun, int? fieldOffset, string fieldHex)
    {
        if (fieldOffset is null || string.IsNullOrWhiteSpace(fieldHex))
        {
            return "no u16 field";
        }

        if (fieldOffset == 1 && StartsWithProtocol03SelectorFfMarkerByte(fieldHex))
        {
            return "marker-adjacent entry u16";
        }

        return zeroRun switch
        {
            0 => "immediate entry u16",
            >= 8 => "long-zero-padded entry u16",
            _ => "zero-padded entry u16"
        };
    }

    private static string ClassifyProtocol03SelectorFfRepeatListPostMarkerFieldLayout(int zeroRun, int? fieldOffset, string fieldHex)
    {
        if (fieldOffset is null || string.IsNullOrWhiteSpace(fieldHex))
        {
            return "no post-marker u16";
        }

        return zeroRun switch
        {
            0 => "immediate post-marker u16",
            4 => "four-zero post-marker u16",
            >= 8 => "long-zero post-marker u16",
            _ => "zero-padded post-marker u16"
        };
    }

    private static Protocol03EffectiveRepeatListEntryField SelectProtocol03SelectorFfEffectiveEntryField(
        Protocol03RepeatListEntryField field,
        string layoutKind)
    {
        if (layoutKind.Equals("marker-adjacent entry u16", StringComparison.OrdinalIgnoreCase) &&
            field.SecondaryFieldOffset is int secondaryOffset &&
            !string.IsNullOrWhiteSpace(field.SecondaryFieldHex))
        {
            return new Protocol03EffectiveRepeatListEntryField(
                secondaryOffset,
                field.SecondaryFieldHex,
                TryReadProtocol03U16FieldValue(field.SecondaryFieldHex),
                "secondary field");
        }

        return new Protocol03EffectiveRepeatListEntryField(
            field.FieldOffset,
            field.FieldHex,
            TryReadProtocol03U16FieldValue(field.FieldHex),
            "primary field");
    }

    private static bool StartsWithProtocol03SelectorFfMarkerByte(string fieldHex)
    {
        return fieldHex.Length >= 2 && fieldHex.StartsWith("ff", StringComparison.OrdinalIgnoreCase);
    }

    private static Protocol03ObjectUpdateSegment? FindProtocol03VariableLeadSegment(
        Protocol03ObjectViewSample view,
        Protocol03VariableSelectorLead lead)
    {
        return view.Segments.FirstOrDefault(segment =>
            segment.Selector.Equals(lead.Selector, StringComparison.OrdinalIgnoreCase) &&
            segment.Offset + 1 == lead.Offset);
    }

    private static string FormatProtocol03PayloadSlice(IReadOnlyList<byte> payload, int offset, int count)
    {
        if (offset < 0 || offset >= payload.Count || count <= 0)
        {
            return string.Empty;
        }

        return FormatHeader(payload.Skip(offset).Take(Math.Min(count, payload.Count - offset)));
    }

    private static bool IsPlausibleProtocol03SelectorFfPosition(double x, double y, double z)
    {
        return double.IsFinite(x) &&
            double.IsFinite(y) &&
            double.IsFinite(z) &&
            Math.Abs(x) <= 100000 &&
            Math.Abs(y) <= 10000 &&
            Math.Abs(z) <= 100000 &&
            Math.Abs(x) >= 100 &&
            Math.Abs(y) >= 100 &&
            Math.Abs(z) >= 100;
    }

    private static int CountProtocol03SelectorFfPostPositionRepeatRecords(IReadOnlyList<byte> tail, int stride)
    {
        if (stride <= 0)
        {
            return 0;
        }

        int count = 0;
        for (int offset = 0; offset < tail.Count; offset += stride)
        {
            if (tail[offset] != 0xff)
            {
                break;
            }

            count++;
        }

        return count;
    }

    private static Protocol03RepeatListEntryField? ReadProtocol03SelectorFfPostPositionRepeatRecordField(
        IReadOnlyList<byte> tail,
        int recordOffset,
        int stride)
    {
        if (recordOffset < 0 || recordOffset >= tail.Count || stride <= 0 || tail[recordOffset] != 0xff)
        {
            return null;
        }

        int recordEnd = Math.Min(recordOffset + stride, tail.Count);
        int fieldOffset = recordOffset + 1;
        int zeroRun = 0;
        while (fieldOffset < recordEnd && tail[fieldOffset] == 0)
        {
            zeroRun++;
            fieldOffset++;
        }

        string entryLeadHex = FormatHeader(tail.Skip(recordOffset).Take(Math.Min(8, tail.Count - recordOffset)));
        if (fieldOffset + 1 >= recordEnd)
        {
            return new Protocol03RepeatListEntryField(zeroRun, null, string.Empty, null, string.Empty, entryLeadHex);
        }

        int localFieldOffset = fieldOffset - recordOffset;
        int? secondaryFieldOffset = null;
        string secondaryFieldHex = string.Empty;
        const int fixedSecondaryFieldOffset = 6;
        int secondaryAbsoluteOffset = recordOffset + fixedSecondaryFieldOffset;
        if (localFieldOffset != fixedSecondaryFieldOffset &&
            secondaryAbsoluteOffset + 1 < recordEnd &&
            (tail[secondaryAbsoluteOffset] != 0 || tail[secondaryAbsoluteOffset + 1] != 0))
        {
            secondaryFieldOffset = fixedSecondaryFieldOffset;
            secondaryFieldHex = FormatHeader(tail.Skip(secondaryAbsoluteOffset).Take(2));
        }

        return new Protocol03RepeatListEntryField(
            zeroRun,
            localFieldOffset,
            FormatHeader(tail.Skip(fieldOffset).Take(2)),
            secondaryFieldOffset,
            secondaryFieldHex,
            entryLeadHex);
    }

    private static int? TryReadProtocol03U16FieldValue(string hex)
    {
        byte[] bytes = ParseHexBytes(hex).ToArray();
        return bytes.Length == 2
            ? BinaryPrimitives.ReadUInt16LittleEndian(bytes)
            : null;
    }

    public static IEnumerable<StaticObjectEntry> ParseStaticObjectText(
        IEnumerable<string> lines,
        string file,
        IReadOnlySet<uint>? wantedObjectIds = null)
    {
        int lineNumber = 0;
        foreach (string line in lines)
        {
            lineNumber++;
            if (lineNumber == 1 && line.StartsWith("metr_id,", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string[] columns = line.Split(',');
            if (columns.Length < 10
                || !ushort.TryParse(columns[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out ushort metrId)
                || !ushort.TryParse(columns[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out ushort sectorId)
                || !TryParseLittleEndianHexUInt32(columns[2].Trim(), out uint mxoId)
                || !TryParseLittleEndianHexUInt32(columns[3].Trim(), out uint staticId)
                || !TryParseLittleEndianHexUInt16(columns[4].Trim(), out ushort typeId)
                || !bool.TryParse(columns[5].Trim(), out bool exterior)
                || !double.TryParse(columns[6].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double x)
                || !double.TryParse(columns[7].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double y)
                || !double.TryParse(columns[8].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double z)
                || !double.TryParse(columns[9].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double rotation))
            {
                continue;
            }

            if (wantedObjectIds is not null && !wantedObjectIds.Contains(mxoId) && !wantedObjectIds.Contains(staticId))
            {
                continue;
            }

            yield return new StaticObjectEntry(
                metrId,
                sectorId,
                mxoId,
                columns[2].Trim().ToLowerInvariant(),
                staticId,
                columns[3].Trim().ToLowerInvariant(),
                typeId,
                columns[4].Trim().ToLowerInvariant(),
                exterior,
                x,
                y,
                z,
                rotation,
                file,
                lineNumber);
        }
    }

    private static IEnumerable<HardcodedCommandExample> ParseHardcodedCommandText(
        IReadOnlyList<string> lines,
        string file,
        string source,
        Regex regex)
    {
        for (int i = 0; i < lines.Count; i++)
        {
            foreach (Match match in regex.Matches(lines[i]))
            {
                byte[] bytes = ParseCompactHexBytes(match.Groups["hex"].Value).ToArray();
                if (bytes.Length == 0)
                {
                    continue;
                }

                int payloadOffset = bytes[0] >= 0x80 && bytes.Length >= 2
                    ? 2
                    : 1;
                byte[] header = bytes.Take(payloadOffset).ToArray();
                byte[] payload = bytes.Skip(payloadOffset).ToArray();
                yield return new HardcodedCommandExample(
                    source,
                    FormatHeader(header),
                    DecodeExternalHeader(header),
                    payload.Length,
                    FormatPayloadWord(payload, 0),
                    ReadUInt16LittleEndian(payload, 0),
                    FormatPayloadWord(payload, 2),
                    ReadUInt16LittleEndian(payload, 2),
                    FormatPayloadWord(payload, 4),
                    ReadUInt16LittleEndian(payload, 4),
                    FormatHeader(payload),
                    file,
                    i + 1);
            }
        }
    }

    private static IEnumerable<Protocol03HardcodedExample> ParseProtocol03HardcodedText(
        IReadOnlyList<string> lines,
        string file,
        string source,
        Regex regex)
    {
        for (int i = 0; i < lines.Count; i++)
        {
            foreach (Match match in regex.Matches(lines[i]))
            {
                byte[] bytes = ParseCompactHexBytes(match.Groups["hex"].Value).ToArray();
                if (bytes.Length == 0)
                {
                    continue;
                }

                Protocol03ObjectViewSample[] objectViews = DetectHardcodedProtocol03ObjectViews(bytes, i + 1).ToArray();
                if (objectViews.Length == 0)
                {
                    continue;
                }

                yield return new Protocol03HardcodedExample(
                    source,
                    file,
                    i + 1,
                    bytes.Length,
                    FormatHeader(bytes.Take(Math.Min(bytes.Length, 24))),
                    objectViews);
            }
        }
    }

    private static IEnumerable<Protocol03ObjectViewSample> DetectHardcodedProtocol03ObjectViews(
        IReadOnlyList<byte> bytes,
        int lineNumber)
    {
        Protocol03ObjectViewSample[] topLevel = DetectProtocol03ObjectViews(bytes, lineNumber, "server").ToArray();
        if (topLevel.Length > 0)
        {
            return topLevel;
        }

        byte[] rawWrapped = new byte[bytes.Count + 1];
        rawWrapped[0] = 0x03;
        for (int i = 0; i < bytes.Count; i++)
        {
            rawWrapped[i + 1] = bytes[i];
        }

        return DetectProtocol03ObjectViews(rawWrapped, lineNumber, "server");
    }

    public static IEnumerable<AttributeDefinition> LoadAttributeDefinitions(string repositoryRoot)
    {
        foreach (string path in Directory.EnumerateFiles(repositoryRoot, "*.cs", SearchOption.AllDirectories)
            .Where(IsGameObjectDefinitionFile))
        {
            foreach (AttributeDefinition entry in ParseAttributeDefinitionText(
                File.ReadAllLines(path),
                RelativePath(path, repositoryRoot),
                InferAttributeSource(path)))
            {
                yield return entry;
            }
        }
    }

    public static IEnumerable<AttributeDefinition> ParseAttributeDefinitionText(
        IReadOnlyList<string> lines,
        string file,
        string source)
    {
        string className = Path.GetFileNameWithoutExtension(file);
        int? classId = ParseTrailingNumber(className);
        Dictionary<string, (string Name, int Size, int Line)> attributes = new(StringComparer.Ordinal);

        for (int i = 0; i < lines.Count; i++)
        {
            Match classMatch = ClassRegex().Match(lines[i]);
            if (classMatch.Success)
            {
                className = classMatch.Groups["name"].Value;
                classId = ParseTrailingNumber(className);
                continue;
            }

            Match attributeMatch = AttributeDeclarationRegex().Match(lines[i]);
            if (attributeMatch.Success)
            {
                attributes[attributeMatch.Groups["var"].Value] = (
                    attributeMatch.Groups["name"].Value,
                    ParseNumber(attributeMatch.Groups["size"].Value),
                    i + 1);
                continue;
            }

            Match addMatch = AddAttributeRegex().Match(lines[i]);
            if (!addMatch.Success)
            {
                continue;
            }

            string variable = addMatch.Groups["var"].Value;
            if (!attributes.TryGetValue(variable, out var attribute))
            {
                continue;
            }

            yield return new AttributeDefinition(
                source,
                className,
                classId,
                attribute.Name,
                attribute.Size,
                ParseNumber(addMatch.Groups["creation"].Value),
                ParseNumber(addMatch.Groups["update"].Value),
                file,
                attribute.Line);
        }
    }

    public static IEnumerable<FxDefinition> LoadFxDefinitions(string repositoryRoot)
    {
        string[] fxListPaths =
        {
            Path.Combine(repositoryRoot, "research", "community-data", "fxlisthex.txt"),
            Path.Combine(repositoryRoot, "research", "community-data", "fxlistreal.txt"),
            Path.Combine(repositoryRoot, "data", "fxlisthex.txt"),
            Path.Combine(repositoryRoot, "data", "fxlistreal.txt")
        };

        HashSet<(string Little, string Big, string Name)> seen = new();
        foreach (string fxListPath in fxListPaths.Where(File.Exists).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            foreach (FxDefinition entry in ParseFxDefinitionText(File.ReadAllLines(fxListPath), RelativePath(fxListPath, repositoryRoot)))
            {
                if (seen.Add((entry.LittleEndianHex, entry.BigEndianHex, entry.Name)))
                {
                    yield return entry;
                }
            }
        }
    }

    public static IEnumerable<AnimationDefinition> LoadAnimationDefinitions(string repositoryRoot)
    {
        string[] animationListPaths =
        {
            Path.Combine(repositoryRoot, "research", "community-data", "animations.txt"),
            Path.Combine(repositoryRoot, "data", "animations.txt")
        };

        HashSet<(string Little, string Big, string Name)> seen = new();
        foreach (string animationListPath in animationListPaths.Where(File.Exists).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            foreach (AnimationDefinition entry in ParseAnimationDefinitionText(File.ReadAllLines(animationListPath), RelativePath(animationListPath, repositoryRoot)))
            {
                if (seen.Add((entry.LittleEndianHex, entry.BigEndianHex, entry.Name)))
                {
                    yield return entry;
                }
            }
        }
    }

    public static IEnumerable<GameObjectEntry> LoadGameObjectEntries(string repositoryRoot)
    {
        string[] gameObjectsPaths =
        {
            Path.Combine(repositoryRoot, "research", "community-data", "gameobjects-public.csv"),
            Path.Combine(repositoryRoot, "data", "gameobjects.csv")
        };

        HashSet<(string CodeName, int GoId)> seen = new();
        foreach (string gameObjectsPath in gameObjectsPaths.Where(File.Exists).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            foreach (GameObjectEntry entry in ParseGameObjectText(File.ReadAllLines(gameObjectsPath), RelativePath(gameObjectsPath, repositoryRoot)))
            {
                if (seen.Add((entry.CodeName, entry.GoId)))
                {
                    yield return entry;
                }
            }
        }
    }

    public static IEnumerable<GameObjectEntry> ParseGameObjectText(IReadOnlyList<string> lines, string file)
    {
        for (int i = 0; i < lines.Count; i++)
        {
            string line = lines[i].Trim();
            if (line.Length == 0)
            {
                continue;
            }

            string[] columns = line.Split(',');
            if (columns.Length < 2)
            {
                continue;
            }

            string codeName = columns[0].Trim();
            if (codeName.Length == 0
                || !int.TryParse(columns[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int goId))
            {
                continue;
            }

            yield return new GameObjectEntry(codeName, goId, file, i + 1);
        }
    }

    public static IEnumerable<WorldEntityEntry> LoadWorldEntityEntries(string repositoryRoot)
    {
        string mobPath = Path.Combine(repositoryRoot, "data", "mob_parsed_untouched.csv");
        if (File.Exists(mobPath))
        {
            foreach (WorldEntityEntry entry in ParseMobEntityText(File.ReadAllLines(mobPath), RelativePath(mobPath, repositoryRoot)))
            {
                yield return entry;
            }
        }

        string npcCollectionPath = Path.Combine(repositoryRoot, "data", "NPC_COLLECTION.xml");
        if (File.Exists(npcCollectionPath))
        {
            foreach (WorldEntityEntry entry in ParseNpcCollectionText(File.ReadAllLines(npcCollectionPath), RelativePath(npcCollectionPath, repositoryRoot)))
            {
                yield return entry;
            }
        }
    }

    public static IEnumerable<WorldEntityEntry> ParseMobEntityText(IReadOnlyList<string> lines, string file)
    {
        string source = Path.GetFileName(file);
        for (int i = 0; i < lines.Count; i++)
        {
            string line = lines[i].Trim();
            if (line.Length == 0)
            {
                continue;
            }

            string[] columns = line.Split(';');
            if (columns.Length < 7)
            {
                continue;
            }

            string name = columns[2].Trim();
            if (name.Length == 0)
            {
                continue;
            }

            yield return new WorldEntityEntry(
                source,
                name,
                NormalizeOptionalText(columns[1]),
                ParseOptionalInt(columns[3]),
                "mob spawn",
                NormalizeOptionalText(columns[6]),
                file,
                i + 1,
                ParseOptionalInt(columns.ElementAtOrDefault(5)) ?? ParseOptionalInt(columns.ElementAtOrDefault(4)),
                ParseOptionalInt(columns.ElementAtOrDefault(4)),
                ParseOptionalDouble(columns.ElementAtOrDefault(7)),
                ParseOptionalDouble(columns.ElementAtOrDefault(8)),
                ParseOptionalDouble(columns.ElementAtOrDefault(9)),
                ParseOptionalInt(columns.ElementAtOrDefault(17)),
                ParseOptionalInt(columns.ElementAtOrDefault(18)),
                NormalizeOptionalText(columns.ElementAtOrDefault(19)));
        }
    }

    public static IEnumerable<WorldEntityEntry> ParseNpcCollectionText(IReadOnlyList<string> lines, string file)
    {
        XDocument document;
        try
        {
            document = XDocument.Parse(string.Join("\n", lines), LoadOptions.SetLineInfo);
        }
        catch (XmlException)
        {
            yield break;
        }

        string source = Path.GetFileName(file);
        foreach (XElement element in document.Descendants().Where(element => element.Name.LocalName.StartsWith("npc", StringComparison.OrdinalIgnoreCase)))
        {
            string? name = NormalizeOptionalText(element.Attribute("handle")?.Value);
            if (name is null)
            {
                continue;
            }

            int line = 0;
            if (element is IXmlLineInfo lineInfo && lineInfo.HasLineInfo())
            {
                line = lineInfo.LineNumber;
            }

            yield return new WorldEntityEntry(
                source,
                name,
                NormalizeOptionalText(element.Attribute("DISTRICT")?.Value),
                ParseOptionalInt(element.Attribute("level")?.Value),
                NormalizeOptionalText(element.Attribute("type")?.Value),
                NormalizeOptionalText(element.Attribute("RSI")?.Value),
                file,
                line,
                null,
                null,
                ParseOptionalDouble(element.Attribute("x_pos")?.Value),
                ParseOptionalDouble(element.Attribute("y_pos")?.Value),
                ParseOptionalDouble(element.Attribute("z_pos")?.Value));
        }
    }

    public static IEnumerable<FxDefinition> ParseFxDefinitionText(IReadOnlyList<string> lines, string file)
    {
        for (int i = 0; i < lines.Count; i++)
        {
            Match match = FxDefinitionRegex().Match(lines[i]);
            if (match.Success)
            {
                yield return new FxDefinition(
                    match.Groups["little"].Value.ToLowerInvariant(),
                    match.Groups["big"].Value.ToLowerInvariant(),
                    match.Groups["name"].Value.Trim(),
                    file,
                    i + 1);
                continue;
            }

            match = SingleEndianFxDefinitionRegex().Match(lines[i]);
            if (match.Success)
            {
                string bigEndianHex = match.Groups["big"].Value.ToLowerInvariant();
                yield return new FxDefinition(
                    ReverseCompactHexByteOrder(bigEndianHex),
                    bigEndianHex,
                    match.Groups["name"].Value.Trim(),
                    file,
                    i + 1);
            }
        }
    }

    public static IEnumerable<AnimationDefinition> ParseAnimationDefinitionText(IReadOnlyList<string> lines, string file)
    {
        for (int i = 0; i < lines.Count; i++)
        {
            Match match = AnimationDefinitionRegex().Match(lines[i]);
            if (!match.Success)
            {
                continue;
            }

            string littleEndianHex = match.Groups["little"].Value.ToLowerInvariant();
            string bigEndianHex = match.Groups["big"].Value.ToLowerInvariant();
            int value = int.Parse(bigEndianHex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            yield return new AnimationDefinition(
                littleEndianHex,
                bigEndianHex,
                value,
                match.Groups["name"].Value.Trim(),
                file,
                i + 1);
        }
    }

    public static IEnumerable<RajkoRpcEntry> ParseRajkoRpcMapText(IReadOnlyList<string> lines, string file)
    {
        for (int i = 0; i < lines.Count; i++)
        {
            Match match = RajkoRpcRegex().Match(lines[i]);
            if (!match.Success)
            {
                continue;
            }

            string width = match.Groups["width"].Value;
            int rawValue = ParseNumber("0x" + match.Groups["hex"].Value);
            byte[] rawBytes = width == "byte"
                ? new[] { (byte)rawValue }
                : new[] { (byte)((rawValue >> 8) & 0xff), (byte)(rawValue & 0xff) };

            yield return new RajkoRpcEntry(
                width,
                rawValue,
                DecodeExternalHeader(rawBytes),
                FormatHeader(rawBytes),
                match.Groups["handler"].Value,
                file,
                i + 1);
        }
    }

    public static IEnumerable<RpcComparison> CompareRajkoToLocal(
        IEnumerable<RajkoRpcEntry> externalEntries,
        IEnumerable<RpcHeaderEntry> localEntries)
    {
        RpcHeaderEntry[] localRequests = localEntries
            .Where(entry => entry.Direction == "client")
            .ToArray();

        foreach (RajkoRpcEntry external in externalEntries.OrderBy(entry => entry.RawValue))
        {
            RpcHeaderEntry? local = localRequests.FirstOrDefault(entry => entry.EncodedHeader == external.RawHeader);
            if (local is not null)
            {
                yield return new RpcComparison(external, local, RpcMatchKind.ExactCommandBytes);
                continue;
            }

            local = localRequests.FirstOrDefault(entry => entry.Value == external.DecodedValue);
            if (local is not null)
            {
                yield return new RpcComparison(external, local, RpcMatchKind.DecodedValue);
                continue;
            }

            if (external.Width == "short" && external.RawValue < 0x8000)
            {
                int firstByte = (external.RawValue >> 8) & 0xff;
                local = localRequests.FirstOrDefault(entry => entry.Value == firstByte);
                if (local is not null)
                {
                    yield return new RpcComparison(external, local, RpcMatchKind.HeaderPrefix);
                    continue;
                }
            }

            yield return new RpcComparison(external, null, RpcMatchKind.Missing);
        }
    }

    public static IEnumerable<PacketDumpFileSummary> LoadPacketDumpSummaries(
        string packetDumpRoot,
        IReadOnlyList<RpcHeaderEntry> localHeaders,
        string repositoryRoot)
    {
        foreach (string path in Directory.EnumerateFiles(packetDumpRoot, "*", SearchOption.AllDirectories).Where(IsPacketDumpTextFile))
        {
            string displayPath = PacketDumpDisplayPath(path, packetDumpRoot, repositoryRoot);
            PacketDumpFileSummary summary = SummarizePacketDump(path, localHeaders, repositoryRoot, displayPath);
            if (summary.PacketLikeLines > 0)
            {
                yield return summary;
            }
        }
    }

    public static PacketDumpFileSummary SummarizePacketDump(
        string file,
        IReadOnlyList<RpcHeaderEntry> localHeaders,
        string repositoryRoot = "",
        string? displayPath = null)
    {
        Dictionary<string, int> protocols = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, int> rpcHeaders = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, int> knownHeaders = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, List<int>> knownHeaderSampleLines = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, KnownHeaderContextAccumulator> knownHeaderContexts = new(StringComparer.OrdinalIgnoreCase);
        List<Protocol04PacketSequenceSample> protocol04PacketSequences = new();
        List<VendorTransactionFlowSample> vendorTransactionFlows = new();
        List<VendorOpenItemListSample> vendorOpenItemLists = new();
        List<Protocol04ServerPayloadShapeSample> protocol04ServerPayloadShapes = new();
        List<Unknown8167PayloadSample> unknown8167Payloads = new();
        List<Unknown47PayloadSample> unknown47Payloads = new();
        List<Unknown6dPayloadSample> unknown6dPayloads = new();
        List<Unknown80c1PayloadSample> unknown80c1Payloads = new();
        List<LoadWorldPayloadSample> loadWorldPayloads = new();
        List<PlayerValuePayloadSample> playerValuePayloads = new();
        List<FlashTrafficPayloadSample> flashTrafficPayloads = new();
        List<FeatureEventPayloadSample> featureEventPayloads = new();
        List<FactionPlayerInfoPayloadSample> factionPlayerInfoPayloads = new();
        List<CrewMembersListPayloadSample> crewMembersListPayloads = new();
        List<FriendOnlinePayloadSample> friendOnlinePayloads = new();
        List<ChatMessageResponsePayloadSample> chatMessageResponsePayloads = new();
        List<WhereamiResponsePayloadSample> whereamiResponsePayloads = new();
        List<FactionNameResponsePayloadSample> factionNameResponsePayloads = new();
        List<Protocol03ObjectViewSample> protocol03ObjectViews = new();
        List<Protocol04InteractionPayloadSample> protocol04InteractionPayloads = new();
        List<PlayerAttributePayloadSample> playerAttributePayloads = new();
        List<AbilityUnloadPayloadSample> abilityUnloadPayloads = new();
        List<FriendListStatusPayloadSample> friendListStatusPayloads = new();
        List<CoderAttributePayloadSample> coderAttributePayloads = new();
        List<ManageBonusPayloadSample> manageBonusPayloads = new();
        Dictionary<string, RpcHeaderEntry[]> localByEncodedHeader = localHeaders
            .Where(entry => entry.EncodedHeader.Contains(" ", StringComparison.Ordinal))
            .GroupBy(entry => entry.EncodedHeader, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.OrdinalIgnoreCase);

        int packetLikeLines = 0;
        int lineNumber = 0;
        string? currentPacketDirection = null;
        int currentPacketStartLine = 0;
        List<byte> currentPacketBytes = new();
        int loosePacketStartLine = 0;
        List<byte> loosePacketBytes = new();
        foreach (string line in File.ReadLines(file))
        {
            lineNumber++;
            string? packetDirection = DetectPacketDirection(line);
            if (packetDirection is not null)
            {
                AnalyzeLoosePacket();
                AnalyzeCurrentPacket();
                currentPacketDirection = packetDirection;
                currentPacketStartLine = lineNumber;
                currentPacketBytes.Clear();
            }

            if (!TryParsePacketDumpLineBytes(line, out byte[] bytes))
            {
                if (currentPacketDirection is null)
                {
                    AnalyzeLoosePacket();
                }

                continue;
            }

            bool packetLikeLine = bytes.Length >= 4;
            if (packetLikeLine)
            {
                packetLikeLines++;
                string protocol = DetectProtocol(bytes);
                Increment(protocols, protocol);

                foreach (KnownEncodedHeaderHit hit in DetectKnownEncodedHeaderHits(bytes, localByEncodedHeader))
                {
                    string label = hit.Label;
                    Increment(knownHeaders, label);
                    if (!knownHeaderSampleLines.TryGetValue(label, out List<int>? lines))
                    {
                        lines = new List<int>();
                        knownHeaderSampleLines[label] = lines;
                    }

                    if (lines.Count < 5 && !lines.Contains(lineNumber))
                    {
                        lines.Add(lineNumber);
                    }

                    if (ShouldRecordKnownHeaderContext(label))
                    {
                        RecordKnownHeaderContext(hit, bytes, lineNumber);
                    }
                }
            }

            if (currentPacketDirection is not null)
            {
                currentPacketBytes.AddRange(bytes);
            }
            else if (packetLikeLine && LooksLikeLoosePacketStart(bytes))
            {
                AnalyzeLoosePacket();
                loosePacketStartLine = lineNumber;
                loosePacketBytes.Clear();
                loosePacketBytes.AddRange(bytes);
            }
            else if (loosePacketBytes.Count > 0)
            {
                loosePacketBytes.AddRange(bytes);
            }
            else if (packetLikeLine)
            {
                AnalyzeProtocolBytes(bytes, lineNumber, null);
            }
        }

        AnalyzeLoosePacket();
        AnalyzeCurrentPacket();

        VendorBuyTransactionSample[] vendorBuyTransactions = DetectVendorBuyTransactions(vendorTransactionFlows, protocol04PacketSequences).ToArray();
        VendorSellTransactionSample[] vendorSellTransactions = DetectVendorSellTransactions(vendorTransactionFlows, protocol04PacketSequences).ToArray();

        return new PacketDumpFileSummary(
            displayPath ?? RelativePath(file, repositoryRoot),
            packetLikeLines,
            protocols,
            rpcHeaders,
            knownHeaders,
            knownHeaderSampleLines.ToDictionary(
                entry => entry.Key,
                entry => (IReadOnlyList<int>)entry.Value,
                StringComparer.OrdinalIgnoreCase),
            protocol04PacketSequences,
            protocol03ObjectViews,
            protocol04InteractionPayloads,
            playerAttributePayloads,
            manageBonusPayloads)
        {
            Protocol04ServerPayloadShapes = protocol04ServerPayloadShapes.Count == 0 ? null : protocol04ServerPayloadShapes,
            VendorTransactionFlows = vendorTransactionFlows.Count == 0 ? null : vendorTransactionFlows,
            VendorOpenItemLists = vendorOpenItemLists.Count == 0 ? null : vendorOpenItemLists,
            VendorBuyTransactions = vendorBuyTransactions.Length == 0 ? null : vendorBuyTransactions,
            VendorSellTransactions = vendorSellTransactions.Length == 0 ? null : vendorSellTransactions,
            Unknown8167Payloads = unknown8167Payloads.Count == 0 ? null : unknown8167Payloads,
            Unknown47Payloads = unknown47Payloads.Count == 0 ? null : unknown47Payloads,
            Unknown6dPayloads = unknown6dPayloads.Count == 0 ? null : unknown6dPayloads,
            Unknown80c1Payloads = unknown80c1Payloads.Count == 0 ? null : unknown80c1Payloads,
            LoadWorldPayloads = loadWorldPayloads.Count == 0 ? null : loadWorldPayloads,
            PlayerValuePayloads = playerValuePayloads.Count == 0 ? null : playerValuePayloads,
            FlashTrafficPayloads = flashTrafficPayloads.Count == 0 ? null : flashTrafficPayloads,
            FeatureEventPayloads = featureEventPayloads.Count == 0 ? null : featureEventPayloads,
            FactionPlayerInfoPayloads = factionPlayerInfoPayloads.Count == 0 ? null : factionPlayerInfoPayloads,
            CrewMembersListPayloads = crewMembersListPayloads.Count == 0 ? null : crewMembersListPayloads,
            FriendOnlinePayloads = friendOnlinePayloads.Count == 0 ? null : friendOnlinePayloads,
            ChatMessageResponsePayloads = chatMessageResponsePayloads.Count == 0 ? null : chatMessageResponsePayloads,
            WhereamiResponsePayloads = whereamiResponsePayloads.Count == 0 ? null : whereamiResponsePayloads,
            FactionNameResponsePayloads = factionNameResponsePayloads.Count == 0 ? null : factionNameResponsePayloads,
            AbilityUnloadPayloads = abilityUnloadPayloads.Count == 0 ? null : abilityUnloadPayloads,
            FriendListStatusPayloads = friendListStatusPayloads.Count == 0 ? null : friendListStatusPayloads,
            CoderAttributePayloads = coderAttributePayloads.Count == 0 ? null : coderAttributePayloads,
            KnownHeaderContexts = knownHeaderContexts.Count == 0
                ? null
                : knownHeaderContexts.Values
                    .OrderBy(context => context.Label, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(context => context.PacketDirection ?? "unknown", StringComparer.OrdinalIgnoreCase)
                    .ThenBy(context => context.PacketProtocol, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(context => context.LineProtocol, StringComparer.OrdinalIgnoreCase)
                    .Select(context => context.ToSummary())
                    .ToArray()
        };

        void AnalyzeCurrentPacket()
        {
            if (currentPacketDirection is null || currentPacketBytes.Count == 0)
            {
                return;
            }

            AnalyzeProtocolBytes(currentPacketBytes, currentPacketStartLine, currentPacketDirection);
        }

        void AnalyzeLoosePacket()
        {
            if (loosePacketBytes.Count == 0)
            {
                return;
            }

            AnalyzeProtocolBytes(loosePacketBytes, loosePacketStartLine, null);
            loosePacketStartLine = 0;
            loosePacketBytes.Clear();
        }

        void AnalyzeProtocolBytes(IReadOnlyList<byte> bytes, int sampleLine, string? directionHint)
        {
            if (directionHint == "other")
            {
                return;
            }

            foreach (PacketDumpHeaderHit hit in DetectProtocol04Headers(bytes, localHeaders, directionHint))
            {
                string direction = hit.Direction is null ? "unknown" : hit.Direction;
                string label = hit.LocalName is null
                    ? $"{hit.RawHeader} [{direction}] (unknown)"
                    : $"{hit.RawHeader} [{direction}] {hit.LocalName}";
                Increment(rpcHeaders, label);
            }

            protocol04PacketSequences.AddRange(DetectProtocol04PacketSequences(bytes, localHeaders, sampleLine, directionHint));
            VendorTransactionFlowSample[] detectedVendorFlows = DetectVendorTransactionFlows(bytes, localHeaders, sampleLine, directionHint).ToArray();
            vendorTransactionFlows.AddRange(detectedVendorFlows);
            vendorOpenItemLists.AddRange(DetectVendorOpenItemLists(detectedVendorFlows));
            protocol04ServerPayloadShapes.AddRange(DetectProtocol04ServerPayloadShapes(bytes, localHeaders, sampleLine, directionHint));
            unknown8167Payloads.AddRange(DetectUnknown8167Payloads(bytes, sampleLine, directionHint));
            unknown47Payloads.AddRange(DetectUnknown47Payloads(bytes, sampleLine, directionHint));
            unknown6dPayloads.AddRange(DetectUnknown6dPayloads(bytes, sampleLine, directionHint));
            unknown80c1Payloads.AddRange(DetectUnknown80c1Payloads(bytes, sampleLine, directionHint));
            loadWorldPayloads.AddRange(DetectLoadWorldPayloads(bytes, sampleLine, directionHint));
            playerValuePayloads.AddRange(DetectPlayerValuePayloads(bytes, sampleLine, directionHint));
            flashTrafficPayloads.AddRange(DetectFlashTrafficPayloads(bytes, sampleLine, directionHint));
            featureEventPayloads.AddRange(DetectFeatureEventPayloads(bytes, sampleLine, directionHint));
            factionPlayerInfoPayloads.AddRange(DetectFactionPlayerInfoPayloads(bytes, sampleLine, directionHint));
            crewMembersListPayloads.AddRange(DetectCrewMembersListPayloads(bytes, sampleLine, directionHint));
            friendOnlinePayloads.AddRange(DetectFriendOnlinePayloads(bytes, sampleLine, directionHint));
            chatMessageResponsePayloads.AddRange(DetectChatMessageResponsePayloads(bytes, sampleLine, directionHint));
            whereamiResponsePayloads.AddRange(DetectWhereamiResponsePayloads(bytes, sampleLine, directionHint));
            factionNameResponsePayloads.AddRange(DetectFactionNameResponsePayloads(bytes, sampleLine, directionHint));
            protocol03ObjectViews.AddRange(DetectProtocol03ObjectViews(bytes, sampleLine, directionHint));
            protocol04InteractionPayloads.AddRange(DetectProtocol04InteractionPayloads(bytes, sampleLine, directionHint));
            playerAttributePayloads.AddRange(DetectPlayerAttributePayloads(bytes, sampleLine, directionHint));
            abilityUnloadPayloads.AddRange(DetectAbilityUnloadPayloads(bytes, sampleLine, directionHint));
            friendListStatusPayloads.AddRange(DetectFriendListStatusPayloads(bytes, sampleLine, directionHint));
            coderAttributePayloads.AddRange(DetectCoderAttributePayloads(bytes, sampleLine, directionHint));
            manageBonusPayloads.AddRange(DetectManageBonusPayloads(bytes, sampleLine, directionHint));
        }

        void RecordKnownHeaderContext(KnownEncodedHeaderHit hit, IReadOnlyList<byte> lineBytes, int sampleLine)
        {
            string label = hit.Label;
            string? packetDirection = currentPacketDirection;
            string packetProtocol;

            if (currentPacketDirection is not null)
            {
                packetProtocol = currentPacketBytes.Count == 0
                    ? DetectProtocol(lineBytes)
                    : DetectProtocol(currentPacketBytes);
            }
            else if (loosePacketBytes.Count > 0)
            {
                packetProtocol = DetectProtocol(loosePacketBytes);
            }
            else
            {
                packetProtocol = DetectProtocol(lineBytes);
            }

            string lineProtocol = DetectProtocol(lineBytes);
            string key = string.Join('\u001f', label, packetDirection ?? "", packetProtocol, lineProtocol);
            if (!knownHeaderContexts.TryGetValue(key, out KnownHeaderContextAccumulator? context))
            {
                context = new KnownHeaderContextAccumulator(label, packetDirection, packetProtocol, lineProtocol);
                knownHeaderContexts[key] = context;
            }

            int packetStartLine = currentPacketDirection is not null
                ? currentPacketStartLine
                : loosePacketStartLine > 0
                    ? loosePacketStartLine
                    : sampleLine;
            int packetOffset = currentPacketDirection is not null
                ? currentPacketBytes.Count + hit.Offset
                : loosePacketBytes.Count > 0
                    ? loosePacketBytes.Count + hit.Offset
                    : hit.Offset;
            context.Add(sampleLine, packetStartLine, packetOffset, lineBytes, hit.Offset, hit.Length);
        }
    }

    private sealed record KnownEncodedHeaderHit(string Label, int Offset, int Length);

    private sealed class KnownHeaderContextAccumulator
    {
        private readonly Dictionary<string, KnownHeaderWindowAccumulator> windows = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<int> sampleLines = new();

        public KnownHeaderContextAccumulator(string label, string? packetDirection, string packetProtocol, string lineProtocol)
        {
            Label = label;
            PacketDirection = packetDirection;
            PacketProtocol = packetProtocol;
            LineProtocol = lineProtocol;
        }

        public string Label { get; }

        public string? PacketDirection { get; }

        public string PacketProtocol { get; }

        public string LineProtocol { get; }

        public int Count { get; private set; }

        public void Add(int line, int packetStartLine, int packetOffset, IReadOnlyList<byte> lineBytes, int headerOffset, int headerLength)
        {
            Count++;
            if (sampleLines.Count < 5 && !sampleLines.Contains(line))
            {
                sampleLines.Add(line);
            }

            int prefixStart = Math.Max(0, headerOffset - 6);
            string prefixHex = FormatHeader(lineBytes.Skip(prefixStart).Take(headerOffset - prefixStart));
            int suffixStart = Math.Min(lineBytes.Count, headerOffset + headerLength);
            string suffixHex = FormatHeader(lineBytes.Skip(suffixStart).Take(Math.Min(10, lineBytes.Count - suffixStart)));
            string windowKey = string.Join('\u001f', headerOffset.ToString(CultureInfo.InvariantCulture), prefixHex, suffixHex);
            if (!windows.TryGetValue(windowKey, out KnownHeaderWindowAccumulator? window))
            {
                window = new KnownHeaderWindowAccumulator(headerOffset, prefixHex, suffixHex);
                windows[windowKey] = window;
            }

            window.Add(line, packetStartLine, packetOffset);
        }

        public KnownHeaderContextSummary ToSummary()
        {
            return new KnownHeaderContextSummary(Label, PacketDirection, PacketProtocol, LineProtocol, Count, sampleLines)
            {
                Windows = windows.Values
                    .OrderByDescending(window => window.Count)
                    .ThenBy(window => window.Offset)
                    .ThenBy(window => window.PrefixHex, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(window => window.SuffixHex, StringComparer.OrdinalIgnoreCase)
                    .Take(8)
                    .Select(window => window.ToSummary())
                    .ToArray()
            };
        }
    }

    private sealed class KnownHeaderWindowAccumulator
    {
        private readonly List<KnownHeaderWindowLocation> sampleLocations = new();
        private readonly List<int> samplePacketStartLines = new();
        private readonly List<int> sampleLines = new();

        public KnownHeaderWindowAccumulator(int offset, string prefixHex, string suffixHex)
        {
            Offset = offset;
            PrefixHex = prefixHex;
            SuffixHex = suffixHex;
        }

        public int Offset { get; }

        public string PrefixHex { get; }

        public string SuffixHex { get; }

        public int Count { get; private set; }

        public void Add(int line, int packetStartLine, int packetOffset)
        {
            Count++;
            if (sampleLines.Count < 5 && !sampleLines.Contains(line))
            {
                sampleLines.Add(line);
            }

            if (samplePacketStartLines.Count < 5 && !samplePacketStartLines.Contains(packetStartLine))
            {
                samplePacketStartLines.Add(packetStartLine);
            }

            if (sampleLocations.Count < 5 && !sampleLocations.Any(location =>
                    location.Line == line &&
                    location.PacketStartLine == packetStartLine &&
                    location.PacketOffset == packetOffset))
            {
                sampleLocations.Add(new KnownHeaderWindowLocation(line, packetStartLine, packetOffset));
            }
        }

        public KnownHeaderWindowSummary ToSummary()
        {
            return new KnownHeaderWindowSummary(Offset, PrefixHex, SuffixHex, Count, sampleLines, samplePacketStartLines)
            {
                SampleLocations = sampleLocations.ToArray()
            };
        }
    }

    public static IEnumerable<byte> ParseHexBytes(string text)
    {
        foreach (Match match in HexByteRegex().Matches(text))
        {
            yield return byte.Parse(match.Groups["hex"].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }
    }

    public static IEnumerable<byte> ParseCompactHexBytes(string text)
    {
        if (text.Length % 2 != 0 || !text.All(Uri.IsHexDigit))
        {
            yield break;
        }

        for (int i = 0; i < text.Length; i += 2)
        {
            yield return byte.Parse(text.Substring(i, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }
    }

    private static string ReverseCompactHexByteOrder(string hex)
    {
        return string.Concat(Enumerable.Range(0, hex.Length / 2)
            .Reverse()
            .Select(index => hex.Substring(index * 2, 2)));
    }

    private static string FormatCompactHexAsSpacedHex(string hex)
    {
        return string.Join(" ", Enumerable.Range(0, hex.Length / 2)
            .Select(index => hex.Substring(index * 2, 2)));
    }

    public static IEnumerable<PacketDumpHeaderHit> DetectProtocol04Headers(
        IReadOnlyList<byte> bytes,
        IReadOnlyDictionary<int, RpcHeaderEntry> localByValue)
    {
        Dictionary<int, RpcHeaderEntry[]> localByValueList = localByValue
            .ToDictionary(entry => entry.Key, entry => new[] { entry.Value });
        return DetectProtocol04Headers(bytes, localByValueList, null, Array.Empty<RpcHeaderEntry>());
    }

    public static IEnumerable<PacketDumpHeaderHit> DetectProtocol04Headers(
        IReadOnlyList<byte> bytes,
        IReadOnlyList<RpcHeaderEntry> localHeaders,
        string? directionHint)
    {
        Dictionary<int, RpcHeaderEntry[]> localByValue = localHeaders
            .GroupBy(entry => entry.Value)
            .ToDictionary(group => group.Key, group => group.ToArray());

        return DetectProtocol04Headers(bytes, localByValue, directionHint, localHeaders);
    }

    private static IEnumerable<PacketDumpHeaderHit> DetectProtocol04Headers(
        IReadOnlyList<byte> bytes,
        IReadOnlyDictionary<int, RpcHeaderEntry[]> localByValue,
        string? directionHint,
        IReadOnlyList<RpcHeaderEntry> localHeaders)
    {
        foreach (Protocol04RpcBlock block in EnumerateProtocol04RpcBlocks(bytes))
        {
            string? localName = FindLocalName(block.RawHeader, block.DecodedValue, localByValue, directionHint, localHeaders);
            yield return new PacketDumpHeaderHit(block.RawHeader, block.DecodedValue, directionHint, localName);
        }
    }

    private static bool ShouldRecordKnownHeaderContext(string label)
    {
        return label.Contains("VENDOR", StringComparison.OrdinalIgnoreCase);
    }

    public static IEnumerable<Protocol04PacketSequenceSample> DetectProtocol04PacketSequences(
        IReadOnlyList<byte> bytes,
        int lineNumber = 0,
        string? directionHint = null)
    {
        return DetectProtocol04PacketSequences(bytes, Array.Empty<RpcHeaderEntry>(), lineNumber, directionHint);
    }

    public static IEnumerable<Protocol04PacketSequenceSample> DetectProtocol04PacketSequences(
        IReadOnlyList<byte> bytes,
        IReadOnlyList<RpcHeaderEntry> localHeaders,
        int lineNumber = 0,
        string? directionHint = null)
    {
        string[] headers = EnumerateProtocol04RpcBlocks(bytes)
            .Select(block => block.RawHeader)
            .ToArray();
        if (headers.Length == 0)
        {
            yield break;
        }

        yield return new Protocol04PacketSequenceSample(lineNumber, directionHint, headers)
        {
            Blocks = ShouldRecordProtocol04SequenceBlocks(headers)
                ? BuildProtocol04RpcFlowBlockSamples(bytes, localHeaders, directionHint)
                : null
        };
    }

    private static bool ShouldRecordProtocol04SequenceBlocks(IReadOnlyList<string> headers)
    {
        return headers.Any(header => VendorAdjacentProtocol04Headers.Contains(header));
    }

    public static IEnumerable<VendorTransactionFlowSample> DetectVendorTransactionFlows(
        IReadOnlyList<byte> bytes,
        IReadOnlyList<RpcHeaderEntry> localHeaders,
        int lineNumber = 0,
        string? directionHint = null)
    {
        if (directionHint == "other")
        {
            yield break;
        }

        Protocol04RpcBlock[] blocks = EnumerateProtocol04RpcBlocks(bytes).ToArray();
        if (blocks.Length == 0)
        {
            yield break;
        }

        Dictionary<int, RpcHeaderEntry[]> localByValue = localHeaders
            .GroupBy(entry => entry.Value)
            .ToDictionary(group => group.Key, group => group.ToArray());
        Protocol04RpcFlowBlockSample[] blockSamples = BuildProtocol04RpcFlowBlockSamples(blocks, localByValue, localHeaders, directionHint);

        for (int i = 0; i < blocks.Length; i++)
        {
            Protocol04RpcBlock block = blocks[i];
            Protocol04RpcFlowBlockSample sample = blockSamples[i];
            if (!IsVendorProtocol04Block(block, sample.LocalName, localByValue, directionHint, localHeaders))
            {
                continue;
            }

            yield return new VendorTransactionFlowSample(
                lineNumber,
                directionHint,
                i,
                block.RawHeader,
                sample.LocalName,
                block.Payload.Length,
                sample.PayloadPrefixHex,
                sample.PayloadSuffixHex,
                blockSamples)
            {
                VendorPayloadHex = FormatHeader(block.Payload)
            };
        }
    }

    public static IEnumerable<VendorOpenItemListSample> DetectVendorOpenItemLists(IEnumerable<VendorTransactionFlowSample> flows)
    {
        foreach (VendorTransactionFlowSample flow in flows)
        {
            if (!string.Equals(flow.VendorHeader, "81 0d", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(flow.VendorPayloadHex))
            {
                continue;
            }

            byte[] payload = ParseHexBytes(flow.VendorPayloadHex).ToArray();
            if (payload.Length < 32)
            {
                continue;
            }

            ushort listOffset = BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(28, 2));
            ushort declaredCount = BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(30, 2));
            if (listOffset != 32)
            {
                continue;
            }

            int trailingBytes = payload.Length - listOffset;
            if (declaredCount == 0 || trailingBytes < 0 || trailingBytes % 4 != 0)
            {
                continue;
            }

            int availableIds = trailingBytes / 4;
            if (availableIds != declaredCount)
            {
                continue;
            }

            uint[] itemIds = Enumerable.Range(0, availableIds)
                .Select(index => BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(listOffset + index * 4, 4)))
                .ToArray();

            yield return new VendorOpenItemListSample(
                flow.Line,
                flow.Direction,
                flow.VendorHeader,
                flow.VendorLocalName,
                payload.Length,
                flow.VendorPayloadPrefixHex,
                FormatHeader(payload.Skip(30).Take(2)),
                declaredCount,
                itemIds.Length,
                itemIds,
                flow.VendorPayloadSuffixHex)
            {
                HeaderHex = FormatHeader(payload.Take(32)),
                ListOffsetFieldHex = FormatHeader(payload.Skip(28).Take(2)),
                ListOffset = listOffset,
                HeaderFloat0 = ReadSingleLittleEndian(payload, 0),
                HeaderFloat8 = ReadSingleLittleEndian(payload, 8),
                HeaderFloat16 = ReadSingleLittleEndian(payload, 16),
                HeaderFloat24 = ReadSingleLittleEndian(payload, 24),
                HeaderWord4Hex = FormatHeader(payload.Skip(4).Take(4)),
                HeaderWord12Hex = FormatHeader(payload.Skip(12).Take(4)),
                HeaderWord20Hex = FormatHeader(payload.Skip(20).Take(4))
            };
        }
    }

    public static IEnumerable<VendorBuyTransactionSample> DetectVendorBuyTransactions(
        IEnumerable<VendorTransactionFlowSample> flows,
        IReadOnlyList<Protocol04PacketSequenceSample> sequences)
    {
        foreach (VendorTransactionFlowSample flow in flows)
        {
            if (!HeaderEquals(flow.VendorHeader, "81 0e"))
            {
                continue;
            }

            byte[] buyPayload = ParseHexBytes(flow.VendorPayloadHex ?? flow.VendorPayloadPrefixHex).ToArray();
            if (buyPayload.Length < 4)
            {
                continue;
            }

            Protocol04PacketSequenceSample? next = FindNextProtocol04PacketSequence(flow, sequences);
            if (next is null)
            {
                continue;
            }

            uint itemId = BinaryPrimitives.ReadUInt32LittleEndian(buyPayload.AsSpan(0, 4));
            yield return new VendorBuyTransactionSample(
                flow.Line,
                flow.Direction,
                flow.VendorPayloadPrefixHex,
                FormatHeader(buyPayload.Take(4)),
                itemId,
                next.Line,
                next.Direction,
                next.Headers,
                ExtractVendorBuyAcks(next, buyPayload),
                ExtractVendorCashDeltas(next),
                ExtractVendorBuyValues(next),
                ExtractVendorValueKinds(next));
        }
    }

    public static IEnumerable<VendorSellTransactionSample> DetectVendorSellTransactions(
        IEnumerable<VendorTransactionFlowSample> flows,
        IReadOnlyList<Protocol04PacketSequenceSample> sequences)
    {
        foreach (VendorTransactionFlowSample flow in flows)
        {
            if (!HeaderEquals(flow.VendorHeader, "81 11"))
            {
                continue;
            }

            Protocol04PacketSequenceSample? next = FindNextProtocol04PacketSequence(flow, sequences);
            if (next is null)
            {
                continue;
            }

            long[] cashDeltas = ExtractVendorCashDeltas(next);
            uint[] sellValues = ExtractVendorSellValues(next);
            uint[] valueKinds = ExtractVendorValueKinds(next);

            foreach (Protocol04RpcFlowBlockSample block in next.Blocks?.Where(block => HeaderEquals(block.Header, "81 12")) ?? Array.Empty<Protocol04RpcFlowBlockSample>())
            {
                byte[] responsePayload = ParseHexBytes(block.PayloadPrefixHex).ToArray();
                if (responsePayload.Length < 13)
                {
                    continue;
                }

                uint normalItemId = BinaryPrimitives.ReadUInt32LittleEndian(responsePayload.AsSpan(1, 4));
                uint abilityId = BinaryPrimitives.ReadUInt32LittleEndian(responsePayload.AsSpan(5, 4));
                yield return new VendorSellTransactionSample(
                    flow.Line,
                    flow.Direction,
                    flow.VendorPayloadPrefixHex,
                    next.Line,
                    next.Direction,
                    next.Headers,
                    ExtractVendorSellAcks(next),
                    block.PayloadPrefixHex,
                    FormatHeader(responsePayload.Skip(1).Take(4)),
                    normalItemId,
                    FormatHeader(responsePayload.Skip(5).Take(4)),
                    abilityId,
                    abilityId & AbilityCodeMask,
                    cashDeltas,
                    sellValues,
                    valueKinds);
            }
        }
    }

    private static Protocol04PacketSequenceSample? FindNextProtocol04PacketSequence(
        VendorTransactionFlowSample flow,
        IReadOnlyList<Protocol04PacketSequenceSample> sequences)
    {
        return sequences
            .Where(sequence => sequence.Line > flow.Line && IsOppositeOrUnknownDirection(flow.Direction, sequence.Direction))
            .OrderBy(sequence => sequence.Line)
            .FirstOrDefault();
    }

    private static long[] ExtractVendorCashDeltas(Protocol04PacketSequenceSample sequence)
    {
        return sequence.Blocks?
            .Where(block => HeaderEquals(block.Header, "80 e7"))
            .Select(block => ParseHexBytes(block.PayloadPrefixHex).ToArray())
            .Where(bytes => bytes.Length >= 8)
            .Select(bytes => BinaryPrimitives.ReadInt64LittleEndian(bytes.AsSpan(0, 8)))
            .ToArray() ?? Array.Empty<long>();
    }

    private static VendorBuyAckSample[] ExtractVendorBuyAcks(Protocol04PacketSequenceSample sequence, byte[] requestPayload)
    {
        return sequence.Blocks?
            .SelectMany(block => ExtractVendorBuyAcks(block, requestPayload))
            .ToArray() ?? Array.Empty<VendorBuyAckSample>();
    }

    private static IEnumerable<VendorBuyAckSample> ExtractVendorBuyAcks(
        Protocol04RpcFlowBlockSample block,
        byte[] requestPayload)
    {
        byte[] bytes = ParseHexBytes(block.PayloadPrefixHex).ToArray();
        if (HeaderEquals(block.Header, "5e") && bytes.Length >= 11)
        {
            yield return new VendorBuyAckSample(
                FormatHeader(bytes),
                FormatHeader(bytes.Take(2)),
                BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(0, 2)),
                FormatHeader(bytes.Skip(2).Take(5)),
                requestPayload.Length >= 5 && bytes.AsSpan(2, 5).SequenceEqual(requestPayload.AsSpan(0, 5)),
                FormatHeader(bytes.Skip(9).Take(2)));
        }

        if (HeaderEquals(block.Header, "69") && bytes.Length >= 9)
        {
            yield return new VendorBuyAckSample(
                FormatHeader(bytes),
                FormatHeader(bytes.Skip(1).Take(2)),
                BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(1, 2)),
                FormatHeader(bytes.Skip(1).Take(4)),
                false,
                FormatHeader(bytes.Skip(5).Take(4)))
            {
                Header = "69",
                Layout = "69 candidate buy ack fields",
                SecondaryFieldHex = FormatHeader(bytes.Skip(5).Take(4)),
                SecondaryFieldValue = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(5, 4))
            };
        }
    }

    private static VendorSellAckSample[] ExtractVendorSellAcks(Protocol04PacketSequenceSample sequence)
    {
        return sequence.Blocks?
            .Where(block => HeaderEquals(block.Header, "5f"))
            .Select(block => ParseHexBytes(block.PayloadPrefixHex).ToArray())
            .Where(bytes => bytes.Length >= 3)
            .Select(bytes => new VendorSellAckSample(
                FormatHeader(bytes),
                FormatHeader(bytes.Take(2)),
                BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(0, 2)),
                FormatHeader(bytes.Skip(2).Take(1)),
                bytes[2]))
            .ToArray() ?? Array.Empty<VendorSellAckSample>();
    }

    private static uint[] ExtractVendorBuyValues(Protocol04PacketSequenceSample sequence)
    {
        return sequence.Blocks?
            .Where(block => HeaderEquals(block.Header, "81 0f"))
            .Select(block => ParseHexBytes(block.PayloadPrefixHex).ToArray())
            .Where(bytes => bytes.Length >= 12)
            .Select(bytes => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(8, 4)))
            .ToArray() ?? Array.Empty<uint>();
    }

    private static uint[] ExtractVendorSellValues(Protocol04PacketSequenceSample sequence)
    {
        return sequence.Blocks?
            .Where(block => HeaderEquals(block.Header, "81 12"))
            .Select(block => ParseHexBytes(block.PayloadPrefixHex).ToArray())
            .Where(bytes => bytes.Length >= 13)
            .Select(bytes => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(9, 4)))
            .ToArray() ?? Array.Empty<uint>();
    }

    private static uint[] ExtractVendorValueKinds(Protocol04PacketSequenceSample sequence)
    {
        return sequence.Blocks?
            .Where(block => HeaderEquals(block.Header, "80 e7") || HeaderEquals(block.Header, "80 e4"))
            .Select(block => ParseHexBytes(block.PayloadPrefixHex).ToArray())
            .SelectMany(EnumerateVendorValueKinds)
            .ToArray() ?? Array.Empty<uint>();
    }

    private static IEnumerable<uint> EnumerateVendorValueKinds(byte[] bytes)
    {
        if (bytes.Length >= 12)
        {
            yield return BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(8, 4));
        }
        else if (bytes.Length >= 8)
        {
            yield return BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4, 4));
        }
    }

    private static bool IsOppositeOrUnknownDirection(string? sourceDirection, string? adjacentDirection)
    {
        if (string.IsNullOrWhiteSpace(sourceDirection) || string.IsNullOrWhiteSpace(adjacentDirection))
        {
            return true;
        }

        return !sourceDirection.Equals(adjacentDirection, StringComparison.OrdinalIgnoreCase);
    }

    private static bool HeaderEquals(string? actual, string expected)
    {
        return string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
    }

    private static float ReadSingleLittleEndian(byte[] bytes, int offset)
    {
        return BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset, 4)));
    }

    private static Protocol04RpcFlowBlockSample[] BuildProtocol04RpcFlowBlockSamples(
        IReadOnlyList<byte> bytes,
        IReadOnlyList<RpcHeaderEntry> localHeaders,
        string? directionHint)
    {
        Dictionary<int, RpcHeaderEntry[]> localByValue = localHeaders
            .GroupBy(entry => entry.Value)
            .ToDictionary(group => group.Key, group => group.ToArray());
        return BuildProtocol04RpcFlowBlockSamples(
            EnumerateProtocol04RpcBlocks(bytes).ToArray(),
            localByValue,
            localHeaders,
            directionHint);
    }

    private static Protocol04RpcFlowBlockSample[] BuildProtocol04RpcFlowBlockSamples(
        IReadOnlyList<Protocol04RpcBlock> blocks,
        IReadOnlyDictionary<int, RpcHeaderEntry[]> localByValue,
        IReadOnlyList<RpcHeaderEntry> localHeaders,
        string? directionHint)
    {
        return blocks
            .Select((block, index) => new Protocol04RpcFlowBlockSample(
                index,
                block.RawHeader,
                FindLocalName(block.RawHeader, block.DecodedValue, localByValue, directionHint, localHeaders),
                block.Payload.Length,
                FormatHeader(block.Payload.Take(Math.Min(block.Payload.Length, 16))),
                block.Payload.Length > 16
                    ? FormatHeader(block.Payload.Skip(Math.Max(0, block.Payload.Length - 8)))
                    : "-",
                ExtractPrintableAsciiRuns(block.Payload)))
            .ToArray();
    }

    private static bool IsVendorProtocol04Block(
        Protocol04RpcBlock block,
        string? localName,
        IReadOnlyDictionary<int, RpcHeaderEntry[]> localByValue,
        string? directionHint,
        IReadOnlyList<RpcHeaderEntry> localHeaders)
    {
        if (!string.IsNullOrWhiteSpace(localName) && localName.Contains("VENDOR", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return FilterByDirection(FindLocalHeaderEntries(block.RawHeader, block.DecodedValue, localByValue, localHeaders), directionHint)
            .Any(entry => entry.Name.Contains("VENDOR", StringComparison.OrdinalIgnoreCase));
    }

    public static IEnumerable<Protocol04ServerPayloadShapeSample> DetectProtocol04ServerPayloadShapes(
        IReadOnlyList<byte> bytes,
        IReadOnlyList<RpcHeaderEntry> localHeaders,
        int lineNumber = 0,
        string? directionHint = null)
    {
        if (directionHint == "client")
        {
            yield break;
        }

        Dictionary<int, RpcHeaderEntry[]> localByValue = localHeaders
            .GroupBy(entry => entry.Value)
            .ToDictionary(group => group.Key, group => group.ToArray());

        foreach (Protocol04RpcBlock block in EnumerateProtocol04RpcBlocks(bytes))
        {
            if (block.Payload.Length == 0 || SpecializedProtocol04PayloadHeaders.Contains(block.RawHeader))
            {
                continue;
            }

            RpcHeaderEntry[] localMatches = FindLocalHeaderEntries(block.RawHeader, block.DecodedValue, localByValue, localHeaders)
                .Distinct()
                .ToArray();
            if (directionHint is null && localMatches.Length > 0 && !localMatches.Any(entry => entry.Direction == "server"))
            {
                continue;
            }

            string? localName = FindLocalName(block.RawHeader, block.DecodedValue, localByValue, directionHint, localHeaders);
            yield return new Protocol04ServerPayloadShapeSample(
                lineNumber,
                block.RawHeader,
                localName,
                block.Payload.Length,
                FormatPayloadWord(block.Payload, 0),
                ReadUInt16LittleEndian(block.Payload, 0),
                FormatPayloadWord(block.Payload, 2),
                ReadUInt16LittleEndian(block.Payload, 2),
                FormatPayloadWord(block.Payload, 4),
                ReadUInt16LittleEndian(block.Payload, 4),
                FormatHeader(block.Payload.Take(Math.Min(block.Payload.Length, 24))),
                block.Payload.Length > 24
                    ? FormatHeader(block.Payload.Skip(Math.Max(0, block.Payload.Length - 16)))
                    : "-",
                ExtractPrintableAsciiRuns(block.Payload));
        }
    }

    public static IEnumerable<Unknown8167PayloadSample> DetectUnknown8167Payloads(
        IReadOnlyList<byte> bytes,
        int lineNumber = 0,
        string? directionHint = null)
    {
        if (directionHint == "client")
        {
            yield break;
        }

        foreach (Protocol04RpcBlock block in EnumerateProtocol04RpcBlocks(bytes))
        {
            if (!block.RawHeader.Equals("81 67", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            (int? text0Bytes, string? text0) = DecodeLengthPrefixedAscii(block.Payload, 21, 23);
            (int? text1Bytes, string? text1) = DecodeLengthPrefixedAscii(block.Payload, 37, 39);

            yield return new Unknown8167PayloadSample(
                lineNumber,
                block.Payload.Length,
                FormatPayloadWord(block.Payload, 0),
                ReadUInt16LittleEndian(block.Payload, 0),
                FormatPayloadWord(block.Payload, 2),
                ReadUInt16LittleEndian(block.Payload, 2),
                FormatPayloadWord(block.Payload, 4),
                ReadUInt16LittleEndian(block.Payload, 4),
                FormatHeader(block.Payload.Take(Math.Min(21, block.Payload.Length))),
                text0Bytes,
                text0,
                text1Bytes,
                text1,
                FormatPayloadWord(block.Payload, 53),
                ReadUInt16LittleEndian(block.Payload, 53),
                FormatPayloadWord(block.Payload, 55),
                ReadUInt16LittleEndian(block.Payload, 55),
                FormatHeader(block.Payload));
        }
    }

    public static IEnumerable<Unknown47PayloadSample> DetectUnknown47Payloads(
        IReadOnlyList<byte> bytes,
        int lineNumber = 0,
        string? directionHint = null)
    {
        if (directionHint == "client")
        {
            yield break;
        }

        foreach (Protocol04RpcBlock block in EnumerateProtocol04RpcBlocks(bytes))
        {
            if (!block.RawHeader.Equals("47", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            yield return new Unknown47PayloadSample(
                lineNumber,
                block.Payload.Length,
                FormatPayloadWord(block.Payload, 0),
                ReadUInt16LittleEndian(block.Payload, 0),
                FormatPayloadWord(block.Payload, 2),
                ReadUInt16LittleEndian(block.Payload, 2),
                FormatPayloadWord(block.Payload, 4),
                ReadUInt16LittleEndian(block.Payload, 4),
                FormatPayloadWord(block.Payload, 19),
                ReadUInt16LittleEndian(block.Payload, 19),
                FormatPayloadWord(block.Payload, 21),
                ReadUInt16LittleEndian(block.Payload, 21),
                block.Payload.Length > 25 ? FormatHeader(block.Payload.Skip(25)) : "-",
                FormatHeader(block.Payload));
        }
    }

    public static IEnumerable<Unknown6dPayloadSample> DetectUnknown6dPayloads(
        IReadOnlyList<byte> bytes,
        int lineNumber = 0,
        string? directionHint = null)
    {
        if (directionHint == "client")
        {
            yield break;
        }

        foreach (Protocol04RpcBlock block in EnumerateProtocol04RpcBlocks(bytes))
        {
            if (!block.RawHeader.Equals("6d", StringComparison.OrdinalIgnoreCase) || block.Payload.Length < 9)
            {
                continue;
            }

            int listOffset = ReadUInt16LittleEndian(block.Payload, 4);
            int listPayloadOffset = PacketOffsetToPayloadOffset(listOffset);
            int? listFlag = null;
            int? entryCount = null;
            int? parsedEntryCount = null;
            List<int> entryIds = new();
            if (listPayloadOffset >= 0 && listPayloadOffset + 3 <= block.Payload.Length)
            {
                listFlag = block.Payload[listPayloadOffset];
                entryCount = ReadUInt16LittleEndian(block.Payload, listPayloadOffset + 1);
                int recordsOffset = listPayloadOffset + 3;
                int availableRecords = Math.Max(0, block.Payload.Length - recordsOffset) / 6;
                parsedEntryCount = Math.Min(entryCount.Value, availableRecords);
                for (int i = 0; i < parsedEntryCount.Value; i++)
                {
                    int recordOffset = recordsOffset + (i * 6);
                    entryIds.Add(ReadUInt16LittleEndian(block.Payload, recordOffset + 1));
                }
            }

            yield return new Unknown6dPayloadSample(
                lineNumber,
                block.Payload.Length,
                FormatPayloadWord(block.Payload, 0),
                ReadUInt16LittleEndian(block.Payload, 0),
                FormatPayloadWord(block.Payload, 2),
                ReadUInt16LittleEndian(block.Payload, 2),
                listOffset,
                listFlag,
                entryCount,
                parsedEntryCount,
                entryIds,
                FormatHeader(block.Payload));
        }
    }

    public static IEnumerable<Unknown80c1PayloadSample> DetectUnknown80c1Payloads(
        IReadOnlyList<byte> bytes,
        int lineNumber = 0,
        string? directionHint = null)
    {
        if (directionHint == "client")
        {
            yield break;
        }

        foreach (Protocol04RpcBlock block in EnumerateProtocol04RpcBlocks(bytes))
        {
            if (!block.RawHeader.Equals("80 c1", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            yield return new Unknown80c1PayloadSample(
                lineNumber,
                block.Payload.Length,
                FormatPayloadWord(block.Payload, 0),
                ReadUInt16LittleEndian(block.Payload, 0),
                FormatPayloadWord(block.Payload, 2),
                ReadUInt16LittleEndian(block.Payload, 2),
                FormatHeader(block.Payload));
        }
    }

    public static IEnumerable<LoadWorldPayloadSample> DetectLoadWorldPayloads(
        IReadOnlyList<byte> bytes,
        int lineNumber = 0,
        string? directionHint = null)
    {
        if (directionHint == "client")
        {
            yield break;
        }

        foreach (Protocol04RpcBlock block in EnumerateProtocol04RpcBlocks(bytes))
        {
            if (!block.RawHeader.Equals("06", StringComparison.OrdinalIgnoreCase) || block.Payload.Length < 15)
            {
                continue;
            }

            (int? worldPathBytes, string? worldPath, int environmentLengthOffset) = DecodeSizedTerminatedAscii(block.Payload, 13);
            (int? environmentBytes, string? environment, _) = environmentLengthOffset >= 0
                ? DecodeSizedTerminatedAscii(block.Payload, environmentLengthOffset)
                : (null, null, -1);

            yield return new LoadWorldPayloadSample(
                lineNumber,
                block.Payload.Length,
                FormatPayloadWord(block.Payload, 0),
                ReadUInt16LittleEndian(block.Payload, 0),
                ReadUInt32LittleEndian(block.Payload, 2),
                FormatHeader(block.Payload.Skip(6).Take(Math.Min(4, Math.Max(0, block.Payload.Length - 6)))),
                ReadUInt32LittleEndian(block.Payload, 6),
                block.Payload.Length > 10 ? FormatHeader(block.Payload.Skip(10).Take(1)) : "-",
                ReadUInt16LittleEndian(block.Payload, 11),
                worldPathBytes,
                worldPath,
                environmentBytes,
                environment,
                FormatHeader(block.Payload));
        }
    }

    public static IEnumerable<PlayerValuePayloadSample> DetectPlayerValuePayloads(
        IReadOnlyList<byte> bytes,
        int lineNumber = 0,
        string? directionHint = null)
    {
        if (directionHint == "client")
        {
            yield break;
        }

        foreach (Protocol04RpcBlock block in EnumerateProtocol04RpcBlocks(bytes))
        {
            if (!block.RawHeader.Equals("80 e4", StringComparison.OrdinalIgnoreCase)
                && !block.RawHeader.Equals("80 e5", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string localName = block.RawHeader.Equals("80 e4", StringComparison.OrdinalIgnoreCase)
                ? "SERVER_PLAYER_INFO"
                : "SERVER_PLAYER_EXP";
            yield return new PlayerValuePayloadSample(
                lineNumber,
                block.RawHeader,
                localName,
                block.Payload.Length,
                ReadUInt32LittleEndian(block.Payload, 0),
                FormatHeader(block.Payload.Take(Math.Min(4, block.Payload.Length))),
                block.Payload.Length > 4 ? FormatHeader(block.Payload.Skip(4)) : "-",
                FormatHeader(block.Payload));
        }
    }

    public static IEnumerable<FlashTrafficPayloadSample> DetectFlashTrafficPayloads(
        IReadOnlyList<byte> bytes,
        int lineNumber = 0,
        string? directionHint = null)
    {
        if (directionHint == "client")
        {
            yield break;
        }

        foreach (Protocol04RpcBlock block in EnumerateProtocol04RpcBlocks(bytes))
        {
            if (!block.RawHeader.Equals("81 a9", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            (int? urlBytes, string? url) = DecodeLengthPrefixedAscii(block.Payload, 5, 7);
            yield return new FlashTrafficPayloadSample(
                lineNumber,
                block.Payload.Length,
                FormatPayloadWord(block.Payload, 0),
                ReadUInt16LittleEndian(block.Payload, 0),
                FormatPayloadWord(block.Payload, 2),
                ReadUInt16LittleEndian(block.Payload, 2),
                block.Payload.Length > 4 ? FormatHeader(block.Payload.Skip(4).Take(1)) : "-",
                urlBytes,
                url,
                FormatHeader(block.Payload));
        }
    }

    public static IEnumerable<FeatureEventPayloadSample> DetectFeatureEventPayloads(
        IReadOnlyList<byte> bytes,
        int lineNumber = 0,
        string? directionHint = null)
    {
        if (directionHint == "client")
        {
            yield break;
        }

        foreach (Protocol04RpcBlock block in EnumerateProtocol04RpcBlocks(bytes))
        {
            if (!block.RawHeader.Equals("3a", StringComparison.OrdinalIgnoreCase) || block.Payload.Length < 6)
            {
                continue;
            }

            (int? keyBytes, string? key, int valueLengthOffset) = DecodeSizedTerminatedAscii(block.Payload, 4);
            int? valueBytes = null;
            string? valueText = null;
            uint? valueUInt32 = null;
            string valueHex = "-";
            if (valueLengthOffset >= 0 && valueLengthOffset + 2 <= block.Payload.Length)
            {
                valueBytes = ReadUInt16LittleEndian(block.Payload, valueLengthOffset);
                int valueOffset = valueLengthOffset + 2;
                int availableValueBytes = Math.Max(0, block.Payload.Length - valueOffset);
                int capturedValueBytes = Math.Min(valueBytes.Value, availableValueBytes);
                valueHex = capturedValueBytes > 0
                    ? FormatHeader(block.Payload.Skip(valueOffset).Take(capturedValueBytes))
                    : "-";
                if (valueBytes == 4 && availableValueBytes >= 4)
                {
                    valueUInt32 = ReadUInt32LittleEndian(block.Payload, valueOffset);
                }
                else if (valueBytes == 2 && availableValueBytes >= 2)
                {
                    valueUInt32 = (uint)ReadUInt16LittleEndian(block.Payload, valueOffset);
                }
                else if (valueBytes > 0 && availableValueBytes >= valueBytes)
                {
                    (_, valueText) = DecodeLengthPrefixedAscii(block.Payload, valueLengthOffset, valueOffset);
                }
            }

            yield return new FeatureEventPayloadSample(
                lineNumber,
                block.Payload.Length,
                FormatHeader(block.Payload.Take(Math.Min(2, block.Payload.Length))),
                ReadUInt16LittleEndian(block.Payload, 2),
                keyBytes,
                key,
                valueBytes,
                valueText,
                valueUInt32,
                valueHex,
                FormatHeader(block.Payload));
        }
    }

    public static IEnumerable<FactionPlayerInfoPayloadSample> DetectFactionPlayerInfoPayloads(
        IReadOnlyList<byte> bytes,
        int lineNumber = 0,
        string? directionHint = null)
    {
        if (directionHint == "client")
        {
            yield break;
        }

        foreach (Protocol04RpcBlock block in EnumerateProtocol04RpcBlocks(bytes))
        {
            if (!block.RawHeader.Equals("7c", StringComparison.OrdinalIgnoreCase) || block.Payload.Length < 16)
            {
                continue;
            }

            int mode = block.Payload[8];
            int dataBytes = ReadUInt16LittleEndian(block.Payload, 14);
            int? alignment = null;
            string? factionName = null;
            uint? masterCharacterId = null;
            uint? money = null;
            int? crewCount = null;
            List<string> crewNames = new();
            List<string> crewLeaderNames = new();

            if (mode == 1 && block.Payload.Length >= 68)
            {
                alignment = block.Payload[16];
                factionName = DecodeFixedAscii(block.Payload, 17, 43);
                masterCharacterId = ReadUInt32LittleEndian(block.Payload, 60);
                money = ReadUInt32LittleEndian(block.Payload, 64);
            }
            else if (mode == 2)
            {
                crewCount = dataBytes > 0 ? dataBytes / 81 : null;
                int recordCount = crewCount is null
                    ? 0
                    : Math.Min(crewCount.Value, Math.Max(0, block.Payload.Length - 16) / 81);
                for (int i = 0; i < recordCount; i++)
                {
                    int recordOffset = 16 + (i * 81);
                    string? crewName = DecodeFixedAscii(block.Payload, recordOffset + 4, 39);
                    if (!string.IsNullOrWhiteSpace(crewName))
                    {
                        crewNames.Add(crewName);
                    }

                    string? leaderName = DecodeFixedAscii(block.Payload, recordOffset + 47, 32);
                    if (!string.IsNullOrWhiteSpace(leaderName))
                    {
                        crewLeaderNames.Add(leaderName);
                    }
                }
            }

            yield return new FactionPlayerInfoPayloadSample(
                lineNumber,
                block.Payload.Length,
                ReadUInt32LittleEndian(block.Payload, 0),
                ReadUInt32LittleEndian(block.Payload, 4),
                FormatHeader(block.Payload.Skip(8).Take(Math.Min(6, Math.Max(0, block.Payload.Length - 8)))),
                mode,
                dataBytes,
                alignment,
                factionName,
                masterCharacterId,
                money,
                crewCount,
                crewNames,
                crewLeaderNames,
                FormatHeader(block.Payload));
        }
    }

    public static IEnumerable<CrewMembersListPayloadSample> DetectCrewMembersListPayloads(
        IReadOnlyList<byte> bytes,
        int lineNumber = 0,
        string? directionHint = null)
    {
        if (directionHint == "client")
        {
            yield break;
        }

        foreach (Protocol04RpcBlock block in EnumerateProtocol04RpcBlocks(bytes))
        {
            if (!block.RawHeader.Equals("80 86", StringComparison.OrdinalIgnoreCase) || block.Payload.Length < 31)
            {
                continue;
            }

            int crewNameOffset = ReadUInt16LittleEndian(block.Payload, 9);
            int memberListOffset = ReadUInt16LittleEndian(block.Payload, 23);
            var crewNameDecoded = DecodeSizedTerminatedAsciiAtPacketOffset(block.Payload, crewNameOffset);
            string? crewName = crewNameDecoded.Text;
            int? memberCount = null;
            List<string> memberHandles = new();
            int memberCountOffset = PacketOffsetToPayloadOffset(memberListOffset);
            if (memberCountOffset >= 0 && memberCountOffset + 1 < block.Payload.Length)
            {
                memberCount = ReadUInt16LittleEndian(block.Payload, memberCountOffset);
                int memberOffset = memberCountOffset + 2;
                int recordCount = Math.Min(memberCount.Value, Math.Max(0, block.Payload.Length - memberOffset) / 38);
                for (int i = 0; i < recordCount; i++)
                {
                    int recordOffset = memberOffset + (i * 38);
                    string? handle = DecodeFixedAscii(block.Payload, recordOffset + 5, 32);
                    if (!string.IsNullOrWhiteSpace(handle))
                    {
                        memberHandles.Add(handle);
                    }
                }
            }

            yield return new CrewMembersListPayloadSample(
                lineNumber,
                block.Payload.Length,
                ReadUInt32LittleEndian(block.Payload, 0),
                ReadUInt32LittleEndian(block.Payload, 4),
                block.Payload[8],
                crewNameOffset,
                ReadUInt32LittleEndian(block.Payload, 11),
                ReadUInt32LittleEndian(block.Payload, 15),
                ReadUInt32LittleEndian(block.Payload, 19),
                memberListOffset,
                FormatHeader(block.Payload.Skip(25).Take(Math.Min(4, Math.Max(0, block.Payload.Length - 25)))),
                ReadUInt16LittleEndian(block.Payload, 29),
                crewName,
                memberCount,
                memberHandles,
                FormatHeader(block.Payload));
        }
    }

    public static IEnumerable<FriendOnlinePayloadSample> DetectFriendOnlinePayloads(
        IReadOnlyList<byte> bytes,
        int lineNumber = 0,
        string? directionHint = null)
    {
        if (directionHint == "client")
        {
            yield break;
        }

        foreach (Protocol04RpcBlock block in EnumerateProtocol04RpcBlocks(bytes))
        {
            if (!block.RawHeader.Equals("80 de", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            (int? handleBytes, string? handle) = DecodeLengthPrefixedAscii(block.Payload, 3, 5);
            yield return new FriendOnlinePayloadSample(
                lineNumber,
                block.Payload.Length,
                ReadUInt16LittleEndian(block.Payload, 0),
                block.Payload.Length > 2 ? FormatHeader(block.Payload.Skip(2).Take(1)) : "-",
                handleBytes,
                handle,
                FormatHeader(block.Payload));
        }
    }

    public static IEnumerable<ChatMessageResponsePayloadSample> DetectChatMessageResponsePayloads(
        IReadOnlyList<byte> bytes,
        int lineNumber = 0,
        string? directionHint = null)
    {
        if (directionHint == "client")
        {
            yield break;
        }

        foreach (Protocol04RpcBlock block in EnumerateProtocol04RpcBlocks(bytes))
        {
            if (!block.RawHeader.Equals("2e", StringComparison.OrdinalIgnoreCase) || block.Payload.Length == 0)
            {
                continue;
            }

            (int? textBytes, string? text, _) = block.Payload.Length >= 37
                ? DecodeSizedTerminatedAscii(block.Payload, 35)
                : (null, null, -1);

            yield return new ChatMessageResponsePayloadSample(
                lineNumber,
                block.Payload.Length,
                FormatHeader(block.Payload.Take(1)),
                block.Payload[0],
                DescribeChatMessageType(block.Payload[0]),
                FormatPayloadWord(block.Payload, 12),
                ReadUInt16LittleEndian(block.Payload, 12),
                FormatPayloadWord(block.Payload, 15),
                ReadUInt16LittleEndian(block.Payload, 15),
                textBytes,
                text,
                FormatHeader(block.Payload));
        }
    }

    public static IEnumerable<WhereamiResponsePayloadSample> DetectWhereamiResponsePayloads(
        IReadOnlyList<byte> bytes,
        int lineNumber = 0,
        string? directionHint = null)
    {
        if (directionHint == "client")
        {
            yield break;
        }

        foreach (Protocol04RpcBlock block in EnumerateProtocol04RpcBlocks(bytes))
        {
            if (!block.RawHeader.Equals("81 54", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            float? x = TryReadSingleLittleEndian(block.Payload, 0, out float decodedX) ? decodedX : null;
            float? y = TryReadSingleLittleEndian(block.Payload, 4, out float decodedY) ? decodedY : null;
            float? z = TryReadSingleLittleEndian(block.Payload, 8, out float decodedZ) ? decodedZ : null;

            yield return new WhereamiResponsePayloadSample(
                lineNumber,
                block.Payload.Length,
                x,
                y,
                z,
                block.Payload.Length > 12 ? FormatHeader(block.Payload.Skip(12)) : "-",
                FormatHeader(block.Payload));
        }
    }

    public static IEnumerable<FactionNameResponsePayloadSample> DetectFactionNameResponsePayloads(
        IReadOnlyList<byte> bytes,
        int lineNumber = 0,
        string? directionHint = null)
    {
        if (directionHint == "client")
        {
            yield break;
        }

        foreach (Protocol04RpcBlock block in EnumerateProtocol04RpcBlocks(bytes))
        {
            if (!block.RawHeader.Equals("80 f5", StringComparison.OrdinalIgnoreCase) || block.Payload.Length < 4)
            {
                continue;
            }

            yield return new FactionNameResponsePayloadSample(
                lineNumber,
                block.Payload.Length,
                ReadUInt32LittleEndian(block.Payload, 0),
                DecodeFixedAscii(block.Payload, 4, 42),
                FormatHeader(block.Payload));
        }
    }

    public static IEnumerable<ManageBonusPayloadSample> DetectManageBonusPayloads(
        IReadOnlyList<byte> bytes,
        int lineNumber = 0,
        string? directionHint = null)
    {
        if (directionHint == "client")
        {
            yield break;
        }

        foreach (Protocol04RpcBlock block in EnumerateProtocol04RpcBlocks(bytes))
        {
            if (!block.RawHeader.Equals("80 bc", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            yield return new ManageBonusPayloadSample(
                lineNumber,
                block.Payload.Length,
                FormatPayloadWord(block.Payload, 0),
                ReadUInt16LittleEndian(block.Payload, 0),
                FormatPayloadWord(block.Payload, 2),
                ReadUInt16LittleEndian(block.Payload, 2),
                FormatPayloadWord(block.Payload, 4),
                ReadUInt16LittleEndian(block.Payload, 4),
                FormatHeader(block.Payload));
        }
    }

    public static IEnumerable<PlayerAttributePayloadSample> DetectPlayerAttributePayloads(
        IReadOnlyList<byte> bytes,
        int lineNumber = 0,
        string? directionHint = null)
    {
        if (directionHint == "client")
        {
            yield break;
        }

        foreach (Protocol04RpcBlock block in EnumerateProtocol04RpcBlocks(bytes))
        {
            if (!block.RawHeader.Equals("80 b2", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            yield return new PlayerAttributePayloadSample(
                lineNumber,
                block.Payload.Length,
                FormatPayloadWord(block.Payload, 0),
                ReadUInt16LittleEndian(block.Payload, 0),
                FormatPayloadWord(block.Payload, 2),
                ReadUInt16LittleEndian(block.Payload, 2),
                FormatPayloadWord(block.Payload, 4),
                ReadUInt16LittleEndian(block.Payload, 4),
                FormatHeader(block.Payload));
        }
    }

    public static IEnumerable<CoderAttributePayloadSample> DetectCoderAttributePayloads(
        IReadOnlyList<byte> bytes,
        int lineNumber = 0,
        string? directionHint = null)
    {
        if (directionHint == "client")
        {
            yield break;
        }

        foreach (Protocol04RpcBlock block in EnumerateProtocol04RpcBlocks(bytes))
        {
            if (!block.RawHeader.Equals("80 bd", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            yield return new CoderAttributePayloadSample(
                lineNumber,
                block.Payload.Length,
                FormatPayloadWord(block.Payload, 0),
                ReadUInt16LittleEndian(block.Payload, 0),
                FormatPayloadWord(block.Payload, 2),
                ReadUInt16LittleEndian(block.Payload, 2),
                FormatPayloadWord(block.Payload, 4),
                ReadUInt16LittleEndian(block.Payload, 4),
                FormatHeader(block.Payload));
        }
    }

    public static IEnumerable<FriendListStatusPayloadSample> DetectFriendListStatusPayloads(
        IReadOnlyList<byte> bytes,
        int lineNumber = 0,
        string? directionHint = null)
    {
        if (directionHint == "client")
        {
            yield break;
        }

        foreach (Protocol04RpcBlock block in EnumerateProtocol04RpcBlocks(bytes))
        {
            if (!block.RawHeader.Equals("80 d7", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            (int? textBytes, string? text) = DecodeLengthPrefixedAscii(block.Payload, 6, 8);

            yield return new FriendListStatusPayloadSample(
                lineNumber,
                block.Payload.Length,
                FormatPayloadWord(block.Payload, 0),
                ReadUInt16LittleEndian(block.Payload, 0),
                FormatPayloadWord(block.Payload, 2),
                ReadUInt16LittleEndian(block.Payload, 2),
                FormatPayloadWord(block.Payload, 4),
                ReadUInt16LittleEndian(block.Payload, 4),
                textBytes,
                text,
                FormatHeader(block.Payload));
        }
    }

    public static IEnumerable<AbilityUnloadPayloadSample> DetectAbilityUnloadPayloads(
        IReadOnlyList<byte> bytes,
        int lineNumber = 0,
        string? directionHint = null)
    {
        if (directionHint == "client")
        {
            yield break;
        }

        foreach (Protocol04RpcBlock block in EnumerateProtocol04RpcBlocks(bytes))
        {
            if (!block.RawHeader.Equals("80 b3", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            yield return new AbilityUnloadPayloadSample(
                lineNumber,
                block.Payload.Length,
                FormatPayloadWord(block.Payload, 0),
                ReadUInt16LittleEndian(block.Payload, 0),
                FormatHeader(block.Payload));
        }
    }

    public static IEnumerable<Protocol04InteractionPayloadSample> DetectProtocol04InteractionPayloads(
        IReadOnlyList<byte> bytes,
        int lineNumber = 0,
        string? directionHint = null)
    {
        if (directionHint == "server")
        {
            yield break;
        }

        foreach (Protocol04RpcBlock block in EnumerateProtocol04RpcBlocks(bytes))
        {
            if (!TryGetObjectInteractionKind(block.RawHeader, out string interactionKind)
                || block.Payload.Length < 5)
            {
                continue;
            }

            int objectType = block.Payload[4];
            yield return new Protocol04InteractionPayloadSample(
                lineNumber,
                directionHint,
                block.RawHeader,
                interactionKind,
                block.Payload.Length,
                ReadUInt32LittleEndian(block.Payload, 0),
                ReadUInt16LittleEndian(block.Payload, 2),
                objectType,
                GetInteractionObjectTypeName(interactionKind, objectType),
                FormatHeader(block.Payload));
        }
    }

    private static bool TryParseInteractionCommand(
        IReadOnlyList<byte> bytes,
        string source,
        string file,
        int lineNumber,
        out Protocol04InteractionCommandExample? example)
    {
        example = null;
        if (bytes.Count < 7)
        {
            return false;
        }

        int payloadOffset = bytes[0] >= 0x80
            ? 2
            : 1;
        if (bytes.Count <= payloadOffset)
        {
            return false;
        }

        string rawHeader = FormatHeader(bytes.Take(payloadOffset));
        if (!TryGetObjectInteractionKind(rawHeader, out string interactionKind))
        {
            return false;
        }

        byte[] payload = bytes.Skip(payloadOffset).ToArray();
        if (payload.Length < 5)
        {
            return false;
        }

        int objectType = payload[4];
        example = new Protocol04InteractionCommandExample(
            source,
            file,
            lineNumber,
            rawHeader,
            interactionKind,
            payload.Length,
            ReadUInt32LittleEndian(payload, 0),
            ReadUInt16LittleEndian(payload, 2),
            objectType,
            GetInteractionObjectTypeName(interactionKind, objectType),
            FormatHeader(payload));
        return true;
    }

    public static IEnumerable<Protocol03ObjectViewSample> DetectProtocol03ObjectViews(
        IReadOnlyList<byte> bytes,
        int lineNumber = 0,
        string? directionHint = null)
    {
        if (directionHint == "client")
        {
            yield break;
        }

        if (!TryFindProtocol03PayloadOffset(bytes, out int offset, out string transportHeader))
        {
            yield break;
        }

        int objectOffset = offset;
        while (TryParseProtocol03ObjectView(
            bytes,
            objectOffset,
            lineNumber,
            directionHint,
            transportHeader,
            out Protocol03ObjectViewSample? sample,
            out int nextObjectOffset))
        {
            if (sample is null)
            {
                yield break;
            }

            yield return sample;

            if (!sample.Complete || nextObjectOffset <= objectOffset || nextObjectOffset >= bytes.Count)
            {
                yield break;
            }

            objectOffset = nextObjectOffset;
        }
    }

    private static bool TryParseProtocol03ObjectView(
        IReadOnlyList<byte> bytes,
        int offset,
        int lineNumber,
        string? directionHint,
        string transportHeader,
        out Protocol03ObjectViewSample? sample,
        out int nextObjectOffset)
    {
        sample = null;
        nextObjectOffset = offset;
        if (offset + 3 >= bytes.Count)
        {
            return false;
        }

        int viewId = ReadUInt16LittleEndian(bytes, offset);
        byte updateCount = bytes[offset + 2];
        if (viewId == 0 || updateCount == 0 || updateCount > 0x20)
        {
            return false;
        }

        byte firstSelector = bytes[offset + 3];
        int cursor = offset + 3;
        List<Protocol03ObjectUpdateSegment> segments = new();
        List<Protocol03StringSample> stringSamples = new();
        List<Protocol03NamedProfileRecord> namedProfileRecords = new();
        List<Protocol03PlayerCharacterCreationRecord> playerCharacterCreationRecords = new();
        List<Protocol03EffectEmoteLead> effectEmoteLeads = new();
        List<Protocol03PositionLikeLead> positionLikeLeads = new();
        List<Protocol03NestedMovementLead> nestedMovementLeads = new();
        List<Protocol03NestedMovementConsumedBodyBoundary> nestedMovementConsumedBodyBoundaries = new();
        List<Protocol03MovementStateTailLead> movementStateTailLeads = new();
        List<Protocol03VariableSelectorLead> variableSelectorLeads = new();
        List<Protocol03SelfViewAttributeLead> selfViewAttributeLeads = new();
        List<Protocol03StaticObjectLead> staticObjectLeads = new();
        int unparsedBytes = 0;
        bool complete = true;

        for (int i = 0; i < updateCount; i++)
        {
            if (cursor >= bytes.Count)
            {
                complete = false;
                break;
            }

            int selectorOffset = cursor - offset;
            byte selector = bytes[cursor++];
            Protocol03SelectorLayout layout = GetProtocol03SelectorLayout(updateCount, i, firstSelector, selector, bytes, cursor);
            int payloadBytes = layout.PayloadBytes ?? Math.Max(0, bytes.Count - cursor);
            int availableBytes = Math.Max(0, bytes.Count - cursor);
            Protocol03SelfViewAttributeLead? selfViewAttributeLead = null;
            if (!layout.Classification.Contains("movement tail lead", StringComparison.Ordinal) &&
                selector == 0x80 &&
                TryCreateProtocol03SelfViewAttributeLead(selector, bytes, cursor, availableBytes, cursor - offset, out selfViewAttributeLead) &&
                selfViewAttributeLead is not null)
            {
                payloadBytes = selfViewAttributeLead.PayloadBytes;
                layout = new Protocol03SelectorLayout("self-view attribute mask/value block", payloadBytes);
            }

            int sampleBytes = Math.Min(payloadBytes, availableBytes);
            if (layout.NestedMovementConsumedBodyBoundary is not null)
            {
                nestedMovementConsumedBodyBoundaries.Add(layout.NestedMovementConsumedBodyBoundary with { Offset = selectorOffset });
            }

            segments.Add(new Protocol03ObjectUpdateSegment(
                selector.ToString("x2", CultureInfo.InvariantCulture),
                layout.Classification,
                payloadBytes,
                selectorOffset,
                FormatHeader(bytes.Skip(cursor).Take(Math.Min(sampleBytes, 16))),
                layout.PayloadBytes is not null));
            if (selector == 0x28)
            {
                effectEmoteLeads.Add(CreateProtocol03EffectEmoteLead(bytes, cursor, sampleBytes, cursor - offset));
            }

            if (selector is 0x2a or 0x2e &&
                TryCreateProtocol03PositionLikeLead(selector, bytes, cursor, sampleBytes, cursor - offset, out Protocol03PositionLikeLead? positionLikeLead) &&
                positionLikeLead is not null)
            {
                positionLikeLeads.Add(positionLikeLead);
            }

            if (selfViewAttributeLead is not null)
            {
                selfViewAttributeLeads.Add(selfViewAttributeLead);
            }
            else if (selector == 0x80 || layout.PayloadBytes is null)
            {
                variableSelectorLeads.Add(CreateProtocol03VariableSelectorLead(selector, bytes, cursor, sampleBytes, cursor - offset));
            }

            if (layout.Classification is "unknown selector payload" or "primary movement wrapper" ||
                layout.Classification.StartsWith("mode 06 ", StringComparison.Ordinal))
            {
                int nestedMovementEvidenceBytes = layout.NestedMovementConsumedBodyBoundary?.EvidencePayloadBytes ?? sampleBytes;
                nestedMovementLeads.AddRange(ExtractProtocol03NestedMovementLeads(selector, bytes, cursor, nestedMovementEvidenceBytes, cursor - offset));
            }

            if (layout.Classification.Contains("movement tail lead", StringComparison.Ordinal))
            {
                movementStateTailLeads.Add(CreateProtocol03MovementStateTailLead(firstSelector, selector, bytes, cursor, sampleBytes, cursor - offset));
            }

            if (IsProtocol03StaticObjectSelector(selector) &&
                TryCreateProtocol03StaticObjectLead(selector, bytes, cursor, sampleBytes, cursor - offset, out Protocol03StaticObjectLead? staticObjectLead) &&
                staticObjectLead is not null)
            {
                staticObjectLeads.Add(staticObjectLead);
            }

            stringSamples.AddRange(ExtractProtocol03AsciiStrings(bytes, cursor, sampleBytes, cursor - offset));
            if (updateCount == 0x0c && selector == 0x0c)
            {
                playerCharacterCreationRecords.AddRange(ExtractProtocol03PlayerCharacterCreationRecords(bytes, cursor, sampleBytes, cursor - offset, updateCount, selector));
            }

            if (updateCount == 0x0c && selector == 0x57)
            {
                namedProfileRecords.AddRange(ExtractProtocol03NamedProfileRecords(bytes, cursor, sampleBytes, cursor - offset, updateCount, selector));
            }

            if (layout.PayloadBytes is null || payloadBytes > availableBytes)
            {
                unparsedBytes = availableBytes;
                complete = false;
                cursor = bytes.Count;
                break;
            }

            cursor += payloadBytes;
        }

        int parsedUpdateCount = segments.Count(segment => segment.IsKnownLength);
        if (parsedUpdateCount != updateCount)
        {
            complete = false;
        }

        nextObjectOffset = cursor;
        sample = new Protocol03ObjectViewSample(
            lineNumber,
            directionHint,
            transportHeader,
            viewId,
            updateCount,
            firstSelector.ToString("x2", CultureInfo.InvariantCulture),
            ClassifyProtocol03ObjectView(updateCount, firstSelector),
            FormatHeader(bytes.Skip(offset).Take(Math.Min(24, bytes.Count - offset))),
            stringSamples
                .GroupBy(text => text.Text, StringComparer.Ordinal)
                .Select(group => group.First())
                .Take(4)
                .ToArray(),
            namedProfileRecords.ToArray(),
            playerCharacterCreationRecords.ToArray(),
            effectEmoteLeads.ToArray(),
            positionLikeLeads.ToArray(),
            nestedMovementLeads.ToArray(),
            movementStateTailLeads.ToArray(),
            variableSelectorLeads.ToArray(),
            selfViewAttributeLeads.ToArray(),
            staticObjectLeads.ToArray(),
            parsedUpdateCount,
            unparsedBytes,
            complete,
            segments)
        {
            PacketOffset = offset,
            NestedMovementConsumedBodyBoundaries = nestedMovementConsumedBodyBoundaries.ToArray()
        };
        return true;
    }

    private static Protocol03EffectEmoteLead CreateProtocol03EffectEmoteLead(
        IReadOnlyList<byte> bytes,
        int payloadOffset,
        int payloadBytes,
        int relativeOffset)
    {
        int payloadEnd = Math.Min(bytes.Count, payloadOffset + payloadBytes);
        byte[] payload = bytes.Skip(payloadOffset).Take(Math.Max(0, payloadEnd - payloadOffset)).ToArray();
        float? x = TryReadSingleLittleEndian(payload, 6, out float decodedX) ? decodedX : null;
        float? y = TryReadSingleLittleEndian(payload, 10, out float decodedY) ? decodedY : null;
        float? z = TryReadSingleLittleEndian(payload, 14, out float decodedZ) ? decodedZ : null;
        return new Protocol03EffectEmoteLead(
            relativeOffset,
            payload.Length,
            payload.Length > 0 ? FormatHeader(payload.Take(1)) : "-",
            payload.Length > 1 ? FormatHeader(payload.Skip(1).Take(1)) : "-",
            payload.Length >= 6 ? FormatHeader(payload.Skip(2).Take(4)) : "-",
            payload.Length >= 18 ? FormatHeader(payload.Skip(6).Take(12)) : "-",
            x,
            y,
            z,
            FormatHeader(payload));
    }

    private static bool TryCreateProtocol03PositionLikeLead(
        byte selector,
        IReadOnlyList<byte> bytes,
        int payloadOffset,
        int payloadBytes,
        int relativeOffset,
        out Protocol03PositionLikeLead? lead)
    {
        lead = null;
        int payloadEnd = Math.Min(bytes.Count, payloadOffset + payloadBytes);
        byte[] payload = bytes.Skip(payloadOffset).Take(Math.Max(0, payloadEnd - payloadOffset)).ToArray();
        if (!TryGetProtocol03PositionLikeOffset(selector, payload, out int positionOffset) ||
            !TryReadSingleLittleEndian(payload, positionOffset, out float x) ||
            !TryReadSingleLittleEndian(payload, positionOffset + 4, out float y) ||
            !TryReadSingleLittleEndian(payload, positionOffset + 8, out float z) ||
            !IsPlausibleProtocol03Position(x, y, z))
        {
            return false;
        }

        lead = new Protocol03PositionLikeLead(
            selector.ToString("x2", CultureInfo.InvariantCulture),
            relativeOffset,
            payload.Length,
            FormatHeader(payload.Take(Math.Min(6, payload.Length))),
            positionOffset,
            FormatHeader(payload.Skip(positionOffset).Take(12)),
            x,
            y,
            z,
            FormatHeader(payload));
        return true;
    }

    private static bool TryGetProtocol03PositionLikeOffset(byte selector, IReadOnlyList<byte> payload, out int positionOffset)
    {
        positionOffset = 0;
        if (payload.Count < 19)
        {
            return false;
        }

        if (StartsWith(payload, 0x00, 0x40, 0x00, 0x00, 0x00, 0x10) ||
            StartsWith(payload, 0x01, 0xc0, 0x00, 0x00, 0x96, 0x00))
        {
            positionOffset = selector == 0x2a ? 7 : 8;
            return positionOffset + 12 <= payload.Count;
        }

        if (selector == 0x2e && StartsWith(payload, 0x02, 0xc0, 0x00, 0x27, 0x31, 0x00))
        {
            positionOffset = 8;
            return positionOffset + 12 <= payload.Count;
        }

        if (payload.Count >= 6 &&
            payload[1] == 0x81 &&
            payload[2] == 0x00 &&
            payload[3] == 0x8c &&
            payload[4] == 0x2e &&
            payload[5] == 0x00)
        {
            positionOffset = selector == 0x2a ? 11 : 12;
            return positionOffset + 12 <= payload.Count;
        }

        if (selector == 0x2a &&
            payload.Count >= 11 &&
            payload[1] == 0x01 &&
            payload[2] == 0x0c &&
            payload[3] == 0x39 &&
            payload[4] is 0x00 or 0x81 &&
            payload[5] is 0x00 or 0x10 &&
            payload[6] == 0x0d &&
            payload[7] == 0x00 &&
            payload[8] == 0xbe &&
            payload[9] == 0x00 &&
            payload[10] == 0x00)
        {
            positionOffset = 11;
            return positionOffset + 12 <= payload.Count;
        }

        return false;
    }

    private static bool StartsWith(IReadOnlyList<byte> bytes, params byte[] prefix)
    {
        if (bytes.Count < prefix.Length)
        {
            return false;
        }

        for (int index = 0; index < prefix.Length; index++)
        {
            if (bytes[index] != prefix[index])
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsPlausibleProtocol03Position(float x, float y, float z)
    {
        return float.IsFinite(x) &&
            float.IsFinite(y) &&
            float.IsFinite(z) &&
            Math.Abs(x) < 100000 &&
            Math.Abs(y) < 5000 &&
            Math.Abs(z) < 100000;
    }

    private static Protocol03VariableSelectorLead CreateProtocol03VariableSelectorLead(
        byte selector,
        IReadOnlyList<byte> bytes,
        int payloadOffset,
        int payloadBytes,
        int relativeOffset)
    {
        int payloadEnd = Math.Min(bytes.Count, payloadOffset + payloadBytes);
        byte[] payload = bytes.Skip(payloadOffset).Take(Math.Max(0, payloadEnd - payloadOffset)).ToArray();
        return new Protocol03VariableSelectorLead(
            selector.ToString("x2", CultureInfo.InvariantCulture),
            relativeOffset,
            payload.Length,
            FormatHeader(payload.Take(Math.Min(8, payload.Length))),
            FormatHeader(payload));
    }

    private static bool TryCreateProtocol03SelfViewAttributeLead(
        byte selector,
        IReadOnlyList<byte> bytes,
        int payloadOffset,
        int payloadBytes,
        int relativeOffset,
        out Protocol03SelfViewAttributeLead? lead)
    {
        lead = null;
        int payloadEnd = Math.Min(bytes.Count, payloadOffset + payloadBytes);
        if (!TryParseObject12SelfViewAttributes(
                bytes,
                payloadOffset,
                payloadEnd,
                payloadOffset,
                relativeOffset,
                out IReadOnlyList<Protocol03CreationAttributeSample> attributes,
                out int consumedBytes) ||
            attributes.Count == 0 ||
            consumedBytes <= 0)
        {
            return false;
        }

        lead = new Protocol03SelfViewAttributeLead(
            selector.ToString("x2", CultureInfo.InvariantCulture),
            relativeOffset,
            consumedBytes,
            FormatHeader(bytes.Skip(payloadOffset).Take(Math.Min(consumedBytes, 8))),
            attributes,
            FormatHeader(bytes.Skip(payloadOffset + consumedBytes).Take(Math.Min(8, Math.Max(0, payloadEnd - payloadOffset - consumedBytes)))),
            FormatHeader(bytes.Skip(payloadOffset).Take(Math.Max(0, payloadEnd - payloadOffset))));
        return true;
    }

    private static bool TryParseObject12SelfViewAttributes(
        IReadOnlyList<byte> bytes,
        int maskOffset,
        int end,
        int payloadOffset,
        int relativeOffset,
        out IReadOnlyList<Protocol03CreationAttributeSample> attributes,
        out int consumedBytes)
    {
        List<Protocol03CreationAttributeSample> parsedAttributes = new();
        attributes = parsedAttributes;
        consumedBytes = 0;
        int cursor = maskOffset;
        for (int group = 0; group <= Object12SelfViewAttributes[^1].Index / 7 && cursor < end; group++)
        {
            byte mask = bytes[cursor++];
            bool hasMoreGroups = (mask & 0x80) != 0;
            int activeBits = mask & 0x7f;
            for (int bit = 0; bit < 7; bit++)
            {
                if ((activeBits & (1 << bit)) == 0)
                {
                    continue;
                }

                int index = group * 7 + bit;
                Protocol03AttributeDescriptor? descriptor = Object12SelfViewAttributes.FirstOrDefault(attribute => attribute.Index == index);
                if (descriptor is null || cursor + descriptor.Size > end)
                {
                    attributes = Array.Empty<Protocol03CreationAttributeSample>();
                    consumedBytes = 0;
                    return false;
                }

                parsedAttributes.Add(new Protocol03CreationAttributeSample(
                    descriptor.Index,
                    descriptor.Name,
                    descriptor.Size,
                    relativeOffset + cursor - payloadOffset,
                    FormatHeader(bytes.Skip(cursor).Take(descriptor.Size))));
                cursor += descriptor.Size;
            }

            if (!hasMoreGroups)
            {
                consumedBytes = cursor - maskOffset;
                return parsedAttributes.Count > 0;
            }
        }

        attributes = Array.Empty<Protocol03CreationAttributeSample>();
        consumedBytes = 0;
        return false;
    }

    private static IEnumerable<Protocol03NestedMovementLead> ExtractProtocol03NestedMovementLeads(
        byte outerSelector,
        IReadOnlyList<byte> bytes,
        int payloadOffset,
        int payloadBytes,
        int relativeOffset)
    {
        int payloadEnd = Math.Min(bytes.Count, payloadOffset + payloadBytes);
        byte[] payload = bytes.Skip(payloadOffset).Take(Math.Max(0, payloadEnd - payloadOffset)).ToArray();
        int maxMarkerOffset = Math.Min(16, payload.Length - 4);
        for (int markerOffset = 0; markerOffset <= maxMarkerOffset; markerOffset++)
        {
            if (payload[markerOffset] != 0x00 ||
                !IsProtocol03NestedMovementMode(payload[markerOffset + 1]) ||
                !TryGetProtocol03MovementPayloadInfo(payload[markerOffset + 2], out int innerPayloadBytes, out int innerPositionOffset))
            {
                continue;
            }

            int innerPayloadOffset = markerOffset + 3;
            int absolutePositionOffset = innerPayloadOffset + innerPositionOffset;
            int innerPayloadEnd = innerPayloadOffset + innerPayloadBytes;
            if (innerPayloadEnd > payload.Length ||
                !TryReadSingleLittleEndian(payload, absolutePositionOffset, out float x) ||
                !TryReadSingleLittleEndian(payload, absolutePositionOffset + 4, out float y) ||
                !TryReadSingleLittleEndian(payload, absolutePositionOffset + 8, out float z) ||
                !IsPlausibleProtocol03Position(x, y, z))
            {
                continue;
            }

            yield return new Protocol03NestedMovementLead(
                outerSelector.ToString("x2", CultureInfo.InvariantCulture),
                relativeOffset,
                payload.Length,
                markerOffset,
                FormatHeader(payload.Take(markerOffset)),
                payload[markerOffset + 1].ToString("x2", CultureInfo.InvariantCulture),
                payload[markerOffset + 2].ToString("x2", CultureInfo.InvariantCulture),
                innerPayloadBytes,
                innerPositionOffset,
                FormatHeader(payload.Skip(absolutePositionOffset).Take(12)),
                x,
                y,
                z,
                FormatHeader(payload.Skip(innerPayloadEnd).Take(Math.Min(8, Math.Max(0, payload.Length - innerPayloadEnd)))),
                FormatHeader(payload.Skip(innerPayloadEnd + 8).Take(Math.Min(8, Math.Max(0, payload.Length - innerPayloadEnd - 8)))),
                FormatHeader(payload));
            yield break;
        }
    }

    private static bool IsProtocol03NestedMovementMode(byte value)
    {
        return value is 0x01 or 0x02 or 0x03 or 0x04 or 0x06;
    }

    private static Protocol03MovementStateTailLead CreateProtocol03MovementStateTailLead(
        byte firstSelector,
        byte tailSelector,
        IReadOnlyList<byte> bytes,
        int payloadOffset,
        int payloadBytes,
        int relativeOffset)
    {
        int payloadEnd = Math.Min(bytes.Count, payloadOffset + payloadBytes);
        byte[] payload = bytes.Skip(payloadOffset).Take(Math.Max(0, payloadEnd - payloadOffset)).ToArray();
        uint tagValue = payload.Length >= 3
            ? (uint)(tailSelector | (payload[0] << 8) | (payload[1] << 16) | (payload[2] << 24))
            : tailSelector;
        int markerBytes = payload.Length >= 7 && payload[3] == 0xff && payload[4] == 0x01
            ? 4
            : Math.Min(2, Math.Max(0, payload.Length - 3));
        string markerHex = FormatHeader(payload.Skip(3).Take(markerBytes));
        string postMarkerPrefixHex = payload.Length > 3 + markerBytes
            ? FormatHeader(payload.Skip(3 + markerBytes).Take(Math.Min(8, payload.Length - 3 - markerBytes)))
            : string.Empty;

        return new Protocol03MovementStateTailLead(
            firstSelector.ToString("x2", CultureInfo.InvariantCulture),
            tailSelector.ToString("x2", CultureInfo.InvariantCulture),
            relativeOffset,
            payload.Length,
            tagValue.ToString("x8", CultureInfo.InvariantCulture),
            tagValue,
            FormatHeader(payload.Take(Math.Min(3, payload.Length))),
            markerHex,
            postMarkerPrefixHex,
            FormatHeader(payload.Take(Math.Min(8, payload.Length))),
            FormatHeader(payload));
    }

    private static bool TryGetProtocol03MovementPayloadInfo(byte selector, out int payloadBytes, out int positionOffset)
    {
        (payloadBytes, positionOffset) = selector switch
        {
            0x08 => (12, 0),
            0x0a or 0x0c => (13, 1),
            0x0e => (14, 2),
            _ => (-1, -1)
        };

        return payloadBytes > 0;
    }

    private static bool TryCreateProtocol03StaticObjectLead(
        byte selector,
        IReadOnlyList<byte> bytes,
        int payloadOffset,
        int payloadBytes,
        int relativeOffset,
        out Protocol03StaticObjectLead? lead)
    {
        lead = null;
        int payloadEnd = Math.Min(bytes.Count, payloadOffset + payloadBytes);
        byte[] payload = bytes.Skip(payloadOffset).Take(Math.Max(0, payloadEnd - payloadOffset)).ToArray();
        if (payload.Length < 10)
        {
            return false;
        }

        if (payload[6] != 0xcd || payload[7] != 0xab)
        {
            return false;
        }

        if (payload[8] != 0x03)
        {
            return false;
        }

        float? q0 = TryReadSingleLittleEndian(payload, 10, out float q0Value) ? q0Value : null;
        float? q1 = TryReadSingleLittleEndian(payload, 14, out float q1Value) ? q1Value : null;
        float? q2 = TryReadSingleLittleEndian(payload, 18, out float q2Value) ? q2Value : null;
        float? q3 = TryReadSingleLittleEndian(payload, 22, out float q3Value) ? q3Value : null;
        byte[] postQuaternion = payload.Skip(26).ToArray();
        double? postQuaternionX = TryReadDoubleLittleEndian(postQuaternion, 1, out double postQuaternionXValue) ? postQuaternionXValue : null;
        double? postQuaternionY = TryReadDoubleLittleEndian(postQuaternion, 9, out double postQuaternionYValue) ? postQuaternionYValue : null;
        double? postQuaternionZ = TryReadDoubleLittleEndian(postQuaternion, 17, out double postQuaternionZValue) ? postQuaternionZValue : null;
        byte[] postQuaternionTail = postQuaternion.Skip(25).ToArray();
        string postQuaternionTailField0Hex = postQuaternionTail.Length >= 4
            ? FormatHeader(postQuaternionTail.Take(4))
            : string.Empty;
        uint? postQuaternionTailField0 = postQuaternionTail.Length >= 4
            ? ReadUInt32LittleEndian(postQuaternionTail, 0)
            : null;
        string postQuaternionTailField1Hex = postQuaternionTail.Length >= 8
            ? FormatHeader(postQuaternionTail.Skip(4).Take(4))
            : string.Empty;
        uint? postQuaternionTailField1 = postQuaternionTail.Length >= 8
            ? ReadUInt32LittleEndian(postQuaternionTail, 4)
            : null;
        string postQuaternionTailSuffixHex = postQuaternionTail.Length > 8
            ? FormatHeader(postQuaternionTail.Skip(8))
            : string.Empty;

        lead = new Protocol03StaticObjectLead(
            selector.ToString("x2", CultureInfo.InvariantCulture),
            relativeOffset,
            payload.Length,
            FormatHeader(payload.Take(1)),
            FormatHeader(payload.Skip(1).Take(4)),
            ReadUInt32LittleEndian(payload, 1),
            FormatHeader(payload.Skip(5).Take(1)),
            FormatHeader(payload.Skip(6).Take(2)),
            payload[8],
            FormatHeader(payload.Skip(9).Take(1)),
            FormatHeader(payload.Skip(10).Take(Math.Min(16, Math.Max(0, payload.Length - 10)))),
            q0,
            q1,
            q2,
            q3,
            postQuaternion.Length,
            postQuaternion.Length > 0 ? FormatHeader(postQuaternion.Take(1)) : string.Empty,
            postQuaternionX,
            postQuaternionY,
            postQuaternionZ,
            FormatHeader(postQuaternionTail),
            postQuaternionTailField0Hex,
            postQuaternionTailField0,
            postQuaternionTailField1Hex,
            postQuaternionTailField1,
            postQuaternionTailSuffixHex,
            FormatHeader(postQuaternion),
            FormatHeader(payload));
        return true;
    }

    private static bool IsProtocol03StaticObjectSelector(byte selector)
    {
        return selector is 0x3c or 0x9e or 0x9f or 0xa0;
    }

    private static IEnumerable<Protocol03StringSample> ExtractProtocol03AsciiStrings(
        IReadOnlyList<byte> bytes,
        int offset,
        int length,
        int relativeOffset)
    {
        int end = Math.Min(bytes.Count, offset + length);
        List<Protocol03StringSample> samples = new();
        int cursor = offset;
        while (cursor < end)
        {
            if (!IsPrintableAscii(bytes[cursor]))
            {
                cursor++;
                continue;
            }

            int runStart = cursor;
            while (cursor < end && IsPrintableAscii(bytes[cursor]))
            {
                cursor++;
            }

            int runLength = cursor - runStart;
            if (runLength < 4)
            {
                continue;
            }

            string text = new string(Enumerable.Range(runStart, runLength)
                .Select(index => (char)bytes[index])
                .ToArray())
                .Trim();
            if (text.Length < 4 || !text.Any(char.IsLetter))
            {
                continue;
            }

            if (text.Length > 48)
            {
                text = text[..48];
            }

            int prefixStart = Math.Max(offset, runStart - 8);
            string prefixHex = FormatHeader(bytes.Skip(prefixStart).Take(runStart - prefixStart));
            string suffixHex = FormatHeader(bytes.Skip(cursor).Take(Math.Min(8, end - cursor)));
            samples.Add(new Protocol03StringSample(relativeOffset + runStart - offset, text, prefixHex, suffixHex));
            if (samples.Count >= 4)
            {
                break;
            }
        }

        return samples;
    }

    private static IEnumerable<Protocol03PlayerCharacterCreationRecord> ExtractProtocol03PlayerCharacterCreationRecords(
        IReadOnlyList<byte> bytes,
        int offset,
        int length,
        int relativeOffset,
        byte creationFlag,
        byte gameObjectIdLowByte)
    {
        int end = Math.Min(bytes.Count, offset + length);
        for (int cursor = offset; cursor + 6 < end; cursor++)
        {
            if (bytes[cursor] != 0x00
                || bytes[cursor + 2] != 0xcd
                || bytes[cursor + 3] != 0xab)
            {
                continue;
            }

            int gameObjectId = gameObjectIdLowByte | (bytes[cursor] << 8);
            if (gameObjectId != 12 || bytes[cursor + 4] == 0)
            {
                continue;
            }

            IReadOnlyList<Protocol03CreationAttributeSample> creationAttributes =
                ParseObject12CreationAttributes(bytes, cursor + 4, end, offset, relativeOffset, out int creationAttributeBytes);
            if (creationAttributes.Count == 0)
            {
                continue;
            }

            int postCreationAttributeOffset = cursor + 4 + creationAttributeBytes;
            yield return new Protocol03PlayerCharacterCreationRecord(
                relativeOffset + cursor - offset,
                FormatHeader(bytes.Skip(cursor).Take(6)),
                creationFlag.ToString("x2", CultureInfo.InvariantCulture),
                $"{gameObjectIdLowByte:x2} {bytes[cursor]:x2}",
                gameObjectId,
                bytes[cursor + 1].ToString("x2", CultureInfo.InvariantCulture),
                FormatHeader(bytes.Skip(cursor + 2).Take(2)),
                bytes[cursor + 4],
                bytes[cursor + 5].ToString("x2", CultureInfo.InvariantCulture),
                creationAttributeBytes,
                FormatHeader(bytes.Skip(postCreationAttributeOffset).Take(Math.Min(8, Math.Max(0, end - postCreationAttributeOffset)))),
                creationAttributes);
        }
    }

    private static IEnumerable<Protocol03NamedProfileRecord> ExtractProtocol03NamedProfileRecords(
        IReadOnlyList<byte> bytes,
        int offset,
        int length,
        int relativeOffset,
        byte creationFlag,
        byte gameObjectIdLowByte)
    {
        int end = Math.Min(bytes.Count, offset + length);
        for (int cursor = offset; cursor + 6 < end; cursor++)
        {
            if (bytes[cursor] != 0x02
                || bytes[cursor + 2] != 0xcd
                || bytes[cursor + 3] != 0xab
                || bytes[cursor + 5] != 0x8e)
            {
                continue;
            }

            int gameObjectId = gameObjectIdLowByte | (bytes[cursor] << 8);
            int textStart = cursor + 6;
            int textEnd = textStart;
            while (textEnd < end && IsPrintableAscii(bytes[textEnd]))
            {
                textEnd++;
            }

            int textLength = textEnd - textStart;
            if (textLength < 4)
            {
                continue;
            }

            string text = new string(Enumerable.Range(textStart, textLength)
                .Select(index => (char)bytes[index])
                .ToArray())
                .Trim();
            if (text.Length < 4 || !text.Any(char.IsLetter))
            {
                continue;
            }

            int creationAttributeBytes = 0;
            IReadOnlyList<Protocol03CreationAttributeSample> creationAttributes = gameObjectId == 599
                ? ParseObject599CreationAttributes(bytes, cursor + 4, end, offset, relativeOffset, out creationAttributeBytes)
                : Array.Empty<Protocol03CreationAttributeSample>();
            Protocol03CreationAttributeSample? titleAbility = creationAttributes.FirstOrDefault(attribute => attribute.Index == 2);
            Protocol03CreationAttributeSample? combatantMode = creationAttributes.FirstOrDefault(attribute => attribute.Index == 3);
            int suffixZeroBytes;
            int nameSlotBytes;
            int postNameSlotOffset;
            if (creationFlag == 0x0c
                && gameObjectId == 599
                && bytes[cursor + 5] == 0x8e
                && textStart + 32 <= end)
            {
                nameSlotBytes = 32;
                suffixZeroBytes = Math.Max(0, Math.Min(nameSlotBytes - textLength, CountZeroBytes(bytes, textEnd, textStart + nameSlotBytes)));
                postNameSlotOffset = textStart + nameSlotBytes;
            }
            else
            {
                suffixZeroBytes = CountZeroBytes(bytes, textEnd, end);
                nameSlotBytes = text.Length + suffixZeroBytes;
                postNameSlotOffset = textEnd + suffixZeroBytes;
            }

            int postCombatantModeOffset = Math.Min(end, postNameSlotOffset + 5);
            int postCreationAttributeOffset = cursor + 4 + creationAttributeBytes;
            TryFindProtocol03ObjectViewHeader(
                bytes,
                postCreationAttributeOffset,
                end,
                8,
                includeNpcBasePostCreationTailSelectors: true,
                out int postCreationObjectViewHeaderOffset);
            bool hasPostCreationObjectViewHeader = postCreationObjectViewHeaderOffset >= 0;
            string postCreationObjectViewHeaderClassification = hasPostCreationObjectViewHeader
                ? ClassifyProtocol03ObjectView(bytes[postCreationObjectViewHeaderOffset + 2], bytes[postCreationObjectViewHeaderOffset + 3])
                : string.Empty;
            int postCreationObjectViewParsedUpdateCount = 0;
            int postCreationObjectViewUnparsedBytes = 0;
            int postCreationObjectViewConsumedBytes = 0;
            string postCreationObjectViewParseStatus = string.Empty;
            string postCreationObjectViewDynamicTailPrefixHex = string.Empty;
            int postCreationObjectViewDynamicTailAvailableBytes = 0;
            int postCreationObjectViewDynamicTailNextHeaderOffset = -1;
            string postCreationObjectViewDynamicTailPreHeaderHex = string.Empty;
            string postCreationObjectViewDynamicTailNextHeaderHex = string.Empty;
            string postCreationObjectViewDynamicTailNextHeaderClassification = string.Empty;
            bool postCreationObjectViewComplete = false;
            if (hasPostCreationObjectViewHeader)
            {
                TryMeasureProtocol03ObjectView(
                    bytes,
                    postCreationObjectViewHeaderOffset,
                    out postCreationObjectViewParsedUpdateCount,
                    out postCreationObjectViewUnparsedBytes,
                    out postCreationObjectViewConsumedBytes,
                    out postCreationObjectViewParseStatus,
                    out postCreationObjectViewDynamicTailPrefixHex,
                    out postCreationObjectViewDynamicTailAvailableBytes,
                    out postCreationObjectViewDynamicTailNextHeaderOffset,
                    out postCreationObjectViewDynamicTailPreHeaderHex,
                    out postCreationObjectViewDynamicTailNextHeaderHex,
                    out postCreationObjectViewDynamicTailNextHeaderClassification,
                    out postCreationObjectViewComplete);
            }

            yield return new Protocol03NamedProfileRecord(
                relativeOffset + cursor - offset,
                text,
                text.Length,
                nameSlotBytes,
                FormatHeader(bytes.Skip(cursor).Take(6)),
                creationFlag.ToString("x2", CultureInfo.InvariantCulture),
                $"{gameObjectIdLowByte:x2} {bytes[cursor]:x2}",
                gameObjectId,
                bytes[cursor + 1].ToString("x2", CultureInfo.InvariantCulture),
                FormatHeader(bytes.Skip(cursor + 2).Take(2)),
                bytes[cursor + 4],
                bytes[cursor + 5].ToString("x2", CultureInfo.InvariantCulture),
                creationAttributeBytes,
                FormatHeader(bytes.Skip(postCreationAttributeOffset).Take(Math.Min(8, Math.Max(0, end - postCreationAttributeOffset)))),
                hasPostCreationObjectViewHeader ? postCreationObjectViewHeaderOffset - postCreationAttributeOffset : null,
                hasPostCreationObjectViewHeader
                    ? FormatHeader(bytes.Skip(postCreationAttributeOffset).Take(postCreationObjectViewHeaderOffset - postCreationAttributeOffset))
                    : string.Empty,
                hasPostCreationObjectViewHeader
                    ? FormatHeader(bytes.Skip(postCreationObjectViewHeaderOffset).Take(Math.Min(8, Math.Max(0, end - postCreationObjectViewHeaderOffset))))
                    : string.Empty,
                hasPostCreationObjectViewHeader
                    ? postCreationObjectViewHeaderClassification
                    : string.Empty,
                hasPostCreationObjectViewHeader
                    ? ClassifyProtocol03PostCreationBoundary(bytes, postCreationAttributeOffset, postCreationObjectViewHeaderOffset, postCreationObjectViewHeaderClassification)
                    : string.Empty,
                postCreationObjectViewParsedUpdateCount,
                postCreationObjectViewUnparsedBytes,
                postCreationObjectViewConsumedBytes,
                postCreationObjectViewParseStatus,
                postCreationObjectViewDynamicTailPrefixHex,
                postCreationObjectViewDynamicTailAvailableBytes,
                postCreationObjectViewDynamicTailNextHeaderOffset >= 0 ? postCreationObjectViewDynamicTailNextHeaderOffset : null,
                postCreationObjectViewDynamicTailPreHeaderHex,
                postCreationObjectViewDynamicTailNextHeaderHex,
                postCreationObjectViewDynamicTailNextHeaderClassification,
                postCreationObjectViewComplete,
                suffixZeroBytes,
                titleAbility?.ValueHex ?? FormatHeader(bytes.Skip(postNameSlotOffset).Take(Math.Min(4, end - postNameSlotOffset))),
                combatantMode?.ValueHex ?? (postNameSlotOffset + 4 < end
                    ? bytes[postNameSlotOffset + 4].ToString("x2", CultureInfo.InvariantCulture)
                    : string.Empty),
                FormatHeader(bytes.Skip(postNameSlotOffset).Take(Math.Min(8, end - postNameSlotOffset))),
                FormatHeader(bytes.Skip(postCombatantModeOffset).Take(Math.Min(8, end - postCombatantModeOffset))),
                creationAttributes);
        }
    }

    private static bool TryMeasureProtocol03ObjectView(
        IReadOnlyList<byte> bytes,
        int offset,
        out int parsedUpdateCount,
        out int unparsedBytes,
        out int consumedBytes,
        out string parseStatus,
        out string dynamicTailPrefixHex,
        out int dynamicTailAvailableBytes,
        out int dynamicTailNextHeaderOffset,
        out string dynamicTailPreHeaderHex,
        out string dynamicTailNextHeaderHex,
        out string dynamicTailNextHeaderClassification,
        out bool complete)
    {
        parsedUpdateCount = 0;
        unparsedBytes = 0;
        consumedBytes = 0;
        parseStatus = string.Empty;
        dynamicTailPrefixHex = string.Empty;
        dynamicTailAvailableBytes = 0;
        dynamicTailNextHeaderOffset = -1;
        dynamicTailPreHeaderHex = string.Empty;
        dynamicTailNextHeaderHex = string.Empty;
        dynamicTailNextHeaderClassification = string.Empty;
        complete = false;
        if (offset + 3 >= bytes.Count)
        {
            parseStatus = "object-view header truncated";
            return false;
        }

        int viewId = ReadUInt16LittleEndian(bytes, offset);
        byte updateCount = bytes[offset + 2];
        if (viewId == 0 || updateCount == 0 || updateCount > 0x20)
        {
            parseStatus = "invalid object-view header";
            return false;
        }

        byte firstSelector = bytes[offset + 3];
        string dynamicCreationStatus = string.Empty;
        if (TryMeasureProtocol03Object599DynamicCreationObjectView(
            bytes,
            offset,
            updateCount,
            firstSelector,
            out int dynamicCreationBytes,
            out dynamicCreationStatus,
            out string dynamicCreationTailPrefixHex,
            out int dynamicCreationTailAvailableBytes,
            out int dynamicCreationTailNextHeaderOffset,
            out string dynamicCreationTailPreHeaderHex,
            out string dynamicCreationTailNextHeaderHex,
            out string dynamicCreationTailNextHeaderClassification))
        {
            parsedUpdateCount = 1;
            consumedBytes = dynamicCreationBytes;
            parseStatus = dynamicCreationStatus;
            dynamicTailPrefixHex = dynamicCreationTailPrefixHex;
            dynamicTailAvailableBytes = dynamicCreationTailAvailableBytes;
            dynamicTailNextHeaderOffset = dynamicCreationTailNextHeaderOffset;
            dynamicTailPreHeaderHex = dynamicCreationTailPreHeaderHex;
            dynamicTailNextHeaderHex = dynamicCreationTailNextHeaderHex;
            dynamicTailNextHeaderClassification = dynamicCreationTailNextHeaderClassification;
            complete = true;
            return true;
        }
        else if (!string.IsNullOrEmpty(dynamicCreationTailPrefixHex))
        {
            dynamicTailPrefixHex = dynamicCreationTailPrefixHex;
            dynamicTailAvailableBytes = dynamicCreationTailAvailableBytes;
            dynamicTailNextHeaderOffset = dynamicCreationTailNextHeaderOffset;
            dynamicTailPreHeaderHex = dynamicCreationTailPreHeaderHex;
            dynamicTailNextHeaderHex = dynamicCreationTailNextHeaderHex;
            dynamicTailNextHeaderClassification = dynamicCreationTailNextHeaderClassification;
        }

        int cursor = offset + 3;
        complete = true;
        for (int i = 0; i < updateCount; i++)
        {
            if (cursor >= bytes.Count)
            {
                complete = false;
                break;
            }

            byte selector = bytes[cursor++];
            Protocol03SelectorLayout layout = GetProtocol03SelectorLayout(updateCount, i, firstSelector, selector, bytes, cursor);
            int payloadBytes = layout.PayloadBytes ?? Math.Max(0, bytes.Count - cursor);
            int availableBytes = Math.Max(0, bytes.Count - cursor);
            if (!layout.Classification.Contains("movement tail lead", StringComparison.Ordinal) &&
                selector == 0x80 &&
                TryCreateProtocol03SelfViewAttributeLead(selector, bytes, cursor, availableBytes, cursor - offset, out Protocol03SelfViewAttributeLead? selfViewAttributeLead) &&
                selfViewAttributeLead is not null)
            {
                payloadBytes = selfViewAttributeLead.PayloadBytes;
                layout = new Protocol03SelectorLayout("self-view attribute mask/value block", payloadBytes);
            }

            if (layout.PayloadBytes is null || payloadBytes > availableBytes)
            {
                unparsedBytes = availableBytes;
                complete = false;
                parseStatus = string.IsNullOrEmpty(dynamicCreationStatus)
                    ? $"{layout.Classification} unresolved"
                    : dynamicCreationStatus;
                break;
            }

            parsedUpdateCount++;
            cursor += payloadBytes;
        }

        if (parsedUpdateCount != updateCount)
        {
            complete = false;
        }

        consumedBytes = Math.Max(0, cursor - offset);
        if (string.IsNullOrEmpty(parseStatus))
        {
            parseStatus = complete
                ? "selector-length parse"
                : "selector-count incomplete";
        }

        return true;
    }

    private static bool TryMeasureProtocol03Object599DynamicCreationObjectView(
        IReadOnlyList<byte> bytes,
        int objectViewOffset,
        byte updateCount,
        byte firstSelector,
        out int consumedBytes,
        out string parseStatus,
        out string tailPrefixHex,
        out int tailAvailableBytes,
        out int tailNextHeaderOffset,
        out string tailPreHeaderHex,
        out string tailNextHeaderHex,
        out string tailNextHeaderClassification)
    {
        consumedBytes = 0;
        parseStatus = string.Empty;
        tailPrefixHex = string.Empty;
        tailAvailableBytes = 0;
        tailNextHeaderOffset = -1;
        tailPreHeaderHex = string.Empty;
        tailNextHeaderHex = string.Empty;
        tailNextHeaderClassification = string.Empty;
        if (updateCount != 0x0c || firstSelector != 0x57)
        {
            return false;
        }

        int payloadOffset = objectViewOffset + 4;
        if (payloadOffset + 5 >= bytes.Count ||
            bytes[payloadOffset] != 0x02 ||
            bytes[payloadOffset + 2] != 0xcd ||
            bytes[payloadOffset + 3] != 0xab ||
            bytes[payloadOffset + 5] != 0x8e)
        {
            parseStatus = "dynamic creation prefix mismatch";
            return false;
        }

        int textStart = payloadOffset + 6;
        if (textStart + 32 > bytes.Count)
        {
            parseStatus = "dynamic creation name slot incomplete";
            return false;
        }

        int textEnd = textStart;
        while (textEnd < textStart + 32 && IsPrintableAscii(bytes[textEnd]))
        {
            textEnd++;
        }

        int textLength = textEnd - textStart;
        if (textLength < 4)
        {
            parseStatus = "dynamic creation name missing";
            return false;
        }

        string text = new string(Enumerable.Range(textStart, textLength)
            .Select(index => (char)bytes[index])
            .ToArray())
            .Trim();
        if (text.Length < 4 || !text.Any(char.IsLetter))
        {
            parseStatus = "dynamic creation name missing";
            return false;
        }

        IReadOnlyList<Protocol03CreationAttributeSample> creationAttributes =
            ParseObject599CreationAttributes(bytes, payloadOffset + 4, bytes.Count, objectViewOffset, 0, out int creationAttributeBytes);
        if (creationAttributeBytes <= 0 || creationAttributes.Count != bytes[payloadOffset + 4])
        {
            parseStatus = "dynamic creation attribute parse incomplete";
            return false;
        }

        int postCreationAttributeOffset = payloadOffset + 4 + creationAttributeBytes;
        tailAvailableBytes = Math.Max(0, bytes.Count - postCreationAttributeOffset);
        tailPrefixHex = FormatHeader(bytes
            .Skip(postCreationAttributeOffset)
            .Take(Math.Min(Protocol03DynamicTailPrefixBytes, tailAvailableBytes)));
        if (!TryFindProtocol03ObjectViewHeader(
            bytes,
            postCreationAttributeOffset,
            bytes.Count,
            8,
            includeNpcBasePostCreationTailSelectors: true,
            out int nextObjectViewOffset))
        {
            if (IsProtocol03Object599DynamicCreationTerminalFiveByteZeroTail(bytes, postCreationAttributeOffset, tailAvailableBytes))
            {
                consumedBytes = bytes.Count - objectViewOffset;
                parseStatus = "dynamic creation bounded by terminal five-byte zero tail";
                return consumedBytes > 4;
            }

            if (TryFindProtocol03ObjectViewHeader(
                bytes,
                postCreationAttributeOffset,
                bytes.Count,
                Protocol03DynamicTailExtendedBoundarySearchBytes,
                includeNpcBasePostCreationTailSelectors: true,
                out int extendedObjectViewOffset))
            {
                tailNextHeaderOffset = extendedObjectViewOffset - postCreationAttributeOffset;
                tailPreHeaderHex = FormatHeader(bytes
                    .Skip(postCreationAttributeOffset)
                    .Take(tailNextHeaderOffset));
                tailNextHeaderHex = FormatHeader(bytes
                    .Skip(extendedObjectViewOffset)
                    .Take(Math.Min(8, Math.Max(0, bytes.Count - extendedObjectViewOffset))));
                tailNextHeaderClassification = ClassifyProtocol03ObjectView(bytes[extendedObjectViewOffset + 2], bytes[extendedObjectViewOffset + 3]);
            }

            parseStatus = "dynamic creation parsed; next boundary not found";
            return false;
        }

        consumedBytes = nextObjectViewOffset - objectViewOffset;
        parseStatus = "dynamic creation bounded by next object-view";
        return consumedBytes > 4;
    }

    private static bool IsProtocol03Object599DynamicCreationTerminalFiveByteZeroTail(
        IReadOnlyList<byte> bytes,
        int offset,
        int tailAvailableBytes)
    {
        return tailAvailableBytes == 5 &&
            offset + 4 < bytes.Count &&
            bytes[offset + 1] == 0x00 &&
            bytes[offset + 2] == 0x00 &&
            bytes[offset + 3] == 0x00 &&
            bytes[offset + 4] == 0x00;
    }

    private static bool TryFindProtocol03ObjectViewHeader(
        IReadOnlyList<byte> bytes,
        int offset,
        int end,
        int maxRelativeOffset,
        bool includeNpcBasePostCreationTailSelectors,
        out int headerOffset)
    {
        headerOffset = -1;
        int maxCandidate = Math.Min(end - 4, offset + maxRelativeOffset);
        for (int candidate = offset; candidate <= maxCandidate; candidate++)
        {
            if (!LooksLikeProtocol03ObjectViewHeader(bytes, candidate, includeNpcBasePostCreationTailSelectors))
            {
                continue;
            }

            string classification = ClassifyProtocol03ObjectView(bytes[candidate + 2], bytes[candidate + 3]);
            if (classification.Equals("unclassified object-view update", StringComparison.Ordinal))
            {
                continue;
            }

            headerOffset = candidate;
            return true;
        }

        return false;
    }

    private static string ClassifyProtocol03PostCreationBoundary(
        IReadOnlyList<byte> bytes,
        int postCreationAttributeOffset,
        int headerOffset,
        string headerClassification)
    {
        int preHeaderBytes = headerOffset - postCreationAttributeOffset;
        if (preHeaderBytes == 3 &&
            postCreationAttributeOffset + 2 < bytes.Count &&
            bytes[postCreationAttributeOffset + 1] == 0x00 &&
            bytes[postCreationAttributeOffset + 2] == 0x00)
        {
            return "three-byte zero-tail boundary";
        }

        return headerClassification switch
        {
            "single state marker update" => "state-marker boundary exception",
            "state marker/object update candidate" => "state-marker boundary exception",
            "delete view candidate" => "delete-view boundary exception",
            "appearance/attribute update candidate" => "appearance/attribute boundary exception",
            _ => "other boundary exception"
        };
    }

    private static IReadOnlyList<Protocol03CreationAttributeSample> ParseObject599CreationAttributes(
        IReadOnlyList<byte> bytes,
        int attributeCountOffset,
        int end,
        int payloadOffset,
        int relativeOffset,
        out int consumedBytes)
    {
        return ParseProtocol03CreationAttributes(bytes, attributeCountOffset, end, payloadOffset, relativeOffset, Object599CreationAttributes, out consumedBytes);
    }

    private static IReadOnlyList<Protocol03CreationAttributeSample> ParseObject12CreationAttributes(
        IReadOnlyList<byte> bytes,
        int attributeCountOffset,
        int end,
        int payloadOffset,
        int relativeOffset,
        out int consumedBytes)
    {
        return ParseProtocol03CreationAttributes(bytes, attributeCountOffset, end, payloadOffset, relativeOffset, Object12CreationAttributes, out consumedBytes);
    }

    private static IReadOnlyList<Protocol03CreationAttributeSample> ParseProtocol03CreationAttributes(
        IReadOnlyList<byte> bytes,
        int attributeCountOffset,
        int end,
        int payloadOffset,
        int relativeOffset,
        IReadOnlyList<Protocol03AttributeDescriptor> descriptors,
        out int consumedBytes)
    {
        List<Protocol03CreationAttributeSample> attributes = new();
        consumedBytes = 0;
        if (attributeCountOffset >= end || descriptors.Count == 0)
        {
            return attributes;
        }

        int cursor = attributeCountOffset + 1;
        int declaredAttributeCount = bytes[attributeCountOffset];
        if (declaredAttributeCount == 0)
        {
            return attributes;
        }

        for (int group = 0; group <= descriptors[^1].Index / 7 && cursor < end; group++)
        {
            byte mask = bytes[cursor++];
            bool hasMoreGroups = (mask & 0x80) != 0;
            int activeBits = mask & 0x7f;
            for (int bit = 0; bit < 7; bit++)
            {
                if ((activeBits & (1 << bit)) == 0)
                {
                    continue;
                }

                int index = group * 7 + bit;
                Protocol03AttributeDescriptor? descriptor = descriptors.FirstOrDefault(attribute => attribute.Index == index);
                if (descriptor is null || cursor + descriptor.Size > end)
                {
                    consumedBytes = attributes.Count > 0
                        ? cursor - attributeCountOffset
                        : 0;
                    return attributes;
                }

                attributes.Add(new Protocol03CreationAttributeSample(
                    descriptor.Index,
                    descriptor.Name,
                    descriptor.Size,
                    relativeOffset + cursor - payloadOffset,
                    FormatHeader(bytes.Skip(cursor).Take(descriptor.Size))));
                cursor += descriptor.Size;
                if (attributes.Count >= declaredAttributeCount)
                {
                    consumedBytes = cursor - attributeCountOffset;
                    return attributes;
                }
            }

            if (!hasMoreGroups)
            {
                break;
            }
        }

        consumedBytes = attributes.Count > 0
            ? cursor - attributeCountOffset
            : 0;
        return attributes;
    }

    private static int CountZeroBytes(IReadOnlyList<byte> bytes, int offset, int end)
    {
        int count = 0;
        while (offset + count < end && bytes[offset + count] == 0)
        {
            count++;
        }

        return count;
    }

    private static bool IsPrintableAscii(byte value)
    {
        return value is >= 0x20 and <= 0x7e;
    }

    private static (int? Bytes, string? Text) DecodeLengthPrefixedAscii(IReadOnlyList<byte> payload, int lengthOffset, int textOffset)
    {
        if (payload.Count < lengthOffset + 2)
        {
            return (null, null);
        }

        int bytes = ReadUInt16LittleEndian(payload, lengthOffset);
        if (bytes <= 0 || textOffset + bytes > payload.Count)
        {
            return (bytes, null);
        }

        int textBytes = bytes;
        while (textBytes > 0 && payload[textOffset + textBytes - 1] == 0)
        {
            textBytes--;
        }

        for (int i = 0; i < textBytes; i++)
        {
            if (!IsPrintableAscii(payload[textOffset + i]))
            {
                return (bytes, null);
            }
        }

        byte[] raw = new byte[textBytes];
        for (int i = 0; i < textBytes; i++)
        {
            raw[i] = payload[textOffset + i];
        }

        return (bytes, Encoding.ASCII.GetString(raw));
    }

    private static (int? Bytes, string? Text, int NextOffset) DecodeSizedTerminatedAscii(IReadOnlyList<byte> payload, int lengthOffset)
    {
        if (payload.Count < lengthOffset + 2)
        {
            return (null, null, -1);
        }

        int bytes = ReadUInt16LittleEndian(payload, lengthOffset);
        int textOffset = lengthOffset + 2;
        if (bytes <= 0 || textOffset + bytes > payload.Count)
        {
            return (bytes, null, -1);
        }

        int textBytes = bytes;
        while (textBytes > 0 && payload[textOffset + textBytes - 1] == 0)
        {
            textBytes--;
        }

        for (int i = 0; i < textBytes; i++)
        {
            if (!IsPrintableAscii(payload[textOffset + i]))
            {
                return (bytes, null, textOffset + bytes);
            }
        }

        byte[] raw = new byte[textBytes];
        for (int i = 0; i < textBytes; i++)
        {
            raw[i] = payload[textOffset + i];
        }

        return (bytes, Encoding.ASCII.GetString(raw), textOffset + bytes);
    }

    private static (int? Bytes, string? Text, int NextOffset) DecodeSizedTerminatedAsciiAtPacketOffset(IReadOnlyList<byte> payload, int packetOffset)
    {
        int payloadOffset = PacketOffsetToPayloadOffset(packetOffset);
        return payloadOffset >= 0
            ? DecodeSizedTerminatedAscii(payload, payloadOffset)
            : (null, null, -1);
    }

    private static string DescribeChatMessageType(byte type)
    {
        return type switch
        {
            0x02 => "CREW",
            0x03 => "FACTION",
            0x05 => "TEAM",
            0x07 => "SYSTEM",
            0x10 => "AREA",
            0x11 => "WHISPER",
            0x17 => "MODAL",
            0xc7 => "BROADCAST",
            0xd7 => "FRAMEMODAL",
            _ => "unknown"
        };
    }

    private static int PacketOffsetToPayloadOffset(int packetOffset)
    {
        // Local protocol 04 writers often store offsets that include the two-byte RPC header.
        return packetOffset >= 2 ? packetOffset - 2 : -1;
    }

    private static string? DecodeFixedAscii(IReadOnlyList<byte> payload, int offset, int bytes)
    {
        if (offset < 0 || bytes <= 0 || offset >= payload.Count)
        {
            return null;
        }

        int availableBytes = Math.Min(bytes, payload.Count - offset);
        int textBytes = 0;
        while (textBytes < availableBytes && payload[offset + textBytes] != 0)
        {
            textBytes++;
        }

        if (textBytes == 0)
        {
            return null;
        }

        for (int i = 0; i < textBytes; i++)
        {
            if (!IsPrintableAscii(payload[offset + i]))
            {
                return null;
            }
        }

        byte[] raw = new byte[textBytes];
        for (int i = 0; i < textBytes; i++)
        {
            raw[i] = payload[offset + i];
        }

        return Encoding.ASCII.GetString(raw);
    }

    private static IReadOnlyList<string> ExtractPrintableAsciiRuns(IReadOnlyList<byte> payload, int minimumLength = 4)
    {
        List<string> texts = new();
        int cursor = 0;
        while (cursor < payload.Count)
        {
            while (cursor < payload.Count && !IsPrintableAscii(payload[cursor]))
            {
                cursor++;
            }

            int start = cursor;
            while (cursor < payload.Count && IsPrintableAscii(payload[cursor]))
            {
                cursor++;
            }

            int length = cursor - start;
            if (length >= minimumLength)
            {
                byte[] raw = new byte[length];
                for (int i = 0; i < length; i++)
                {
                    raw[i] = payload[start + i];
                }

                string text = Encoding.ASCII.GetString(raw);
                if (!texts.Contains(text, StringComparer.OrdinalIgnoreCase))
                {
                    texts.Add(text);
                }
            }
        }

        return texts;
    }

    public static IEnumerable<string> DetectKnownEncodedHeaders(
        IReadOnlyList<byte> bytes,
        IReadOnlyDictionary<string, RpcHeaderEntry[]> localByEncodedHeader)
    {
        return DetectKnownEncodedHeaderHits(bytes, localByEncodedHeader).Select(hit => hit.Label);
    }

    private static IEnumerable<KnownEncodedHeaderHit> DetectKnownEncodedHeaderHits(
        IReadOnlyList<byte> bytes,
        IReadOnlyDictionary<string, RpcHeaderEntry[]> localByEncodedHeader)
    {
        for (int i = 0; i < bytes.Count - 1; i++)
        {
            string header = FormatHeader(new[] { bytes[i], bytes[i + 1] });
            if (!localByEncodedHeader.TryGetValue(header, out RpcHeaderEntry[]? entries))
            {
                continue;
            }

            foreach (RpcHeaderEntry entry in entries)
            {
                yield return new KnownEncodedHeaderHit($"{header} {entry.Name} [{entry.Protocol} {entry.Direction}]", i, 2);
            }
        }
    }

    private static IEnumerable<Protocol04RpcBlock> EnumerateProtocol04RpcBlocks(IReadOnlyList<byte> bytes)
    {
        if (bytes.Count <= 8 || bytes[0] != 0x02 || bytes[1] != 0x04)
        {
            yield break;
        }

        int offset = 2;
        if (!TryReadByte(bytes, ref offset, out byte blockCount))
        {
            yield break;
        }

        for (int blockIndex = 0; blockIndex < blockCount; blockIndex++)
        {
            if (!TrySkip(bytes, ref offset, 2) || !TryReadByte(bytes, ref offset, out byte rpcInside))
            {
                yield break;
            }

            for (int rpcIndex = 0; rpcIndex < rpcInside; rpcIndex++)
            {
                if (!TryReadPackedLength(bytes, ref offset, out int length) || length <= 0)
                {
                    yield break;
                }

                if (!TryReadByte(bytes, ref offset, out byte firstHeader))
                {
                    yield break;
                }

                length--;
                List<byte> rawHeader = new() { firstHeader };
                int decoded = firstHeader;
                if (firstHeader >= 0x80 && length > 0)
                {
                    if (!TryReadByte(bytes, ref offset, out byte secondHeader))
                    {
                        yield break;
                    }

                    length--;
                    rawHeader.Add(secondHeader);
                    decoded = DecodeExternalHeader(rawHeader);
                }

                string rawHeaderText = FormatHeader(rawHeader);
                if (length < 0 || offset + length > bytes.Count)
                {
                    yield break;
                }

                byte[] payload = bytes.Skip(offset).Take(length).ToArray();
                offset += length;
                yield return new Protocol04RpcBlock(rawHeaderText, decoded, payload);
            }
        }
    }

    private static bool TryFindProtocol03PayloadOffset(IReadOnlyList<byte> bytes, out int offset, out string transportHeader)
    {
        offset = 0;
        transportHeader = "-";
        if (bytes.Count == 0)
        {
            return false;
        }

        if (bytes[0] == 0x03)
        {
            offset = 1;
            transportHeader = "raw 03";
            return true;
        }

        if (bytes.Count > 5 && (bytes[0] is 0x82 or 0xc2) && bytes[5] == 0x03)
        {
            offset = 6;
            transportHeader = $"{bytes[0]:x2} + simtime";
            return true;
        }

        if (bytes.Count > 1 && (bytes[0] is 0x02 or 0x42 or 0x82 or 0xc2) && bytes[1] == 0x03)
        {
            offset = 2;
            transportHeader = bytes[0].ToString("x2", CultureInfo.InvariantCulture);
            return true;
        }

        return false;
    }

    private static string ClassifyProtocol03ObjectView(byte updateCount, byte firstSelector)
    {
        return (updateCount, firstSelector) switch
        {
            (0x01, 0x00) => "single state marker update",
            (0x01, 0x01) => "delete view candidate",
            (0x01, 0x08) => "single position update",
            (0x01, 0x28) => "emote update",
            (0x02, 0x00) => "state marker/object update candidate",
            (0x02, 0x08) => "position/object state update",
            (0x02, 0x0c) => "object state update",
            (0x02, 0x0e) => "object state update",
            (0x02, 0x60) => "name/profile update candidate",
            (0x02, 0x80) => "appearance/attribute update candidate",
            (0x03, 0x08) => "movement state bundle",
            (0x03, 0x09) => "movement state bundle",
            (0x03, 0x28) => "effect/emote state bundle",
            (0x04, 0x80) => "attribute/effect bundle",
            (0x08, 0x3c) => "static object/door spawn candidate",
            (0x08, _) when IsProtocol03NpcBasePostCreationTailSelector(firstSelector) => "NPC_BASE post-creation tail object-view candidate",
            (0x08, 0x9e) => "static object/door spawn candidate",
            (0x08, 0x9f) => "static object/door spawn candidate",
            (0x08, 0xa0) => "static object/door spawn candidate",
            (0x0c, 0x0c) => "player spawn/self-view candidate",
            (0x0c, 0x57) => "NPC_BASE dynamic creation candidate",
            _ => "unclassified object-view update"
        };
    }

    private static Protocol03SelectorLayout GetProtocol03SelectorLayout(
        byte updateCount,
        int segmentIndex,
        byte firstSelector,
        byte selector,
        IReadOnlyList<byte> bytes,
        int payloadOffset)
    {
        if (TryGetSecondaryMovementWrapperLayout(updateCount, segmentIndex, bytes, payloadOffset, out Protocol03SelectorLayout layout))
        {
            return layout;
        }

        if (TryGetAdjacentMovementWrapperLayout(updateCount, segmentIndex, selector, bytes, payloadOffset, out layout))
        {
            return layout;
        }

        if (selector is 0x12 or 0x13 or 0x14 &&
            TryGetPrimaryMovementWrapperLayout(updateCount, segmentIndex, bytes, payloadOffset, out layout))
        {
            return layout;
        }

        if (TryGetMovementStateTailLayout(updateCount, segmentIndex, firstSelector, bytes, payloadOffset, out layout))
        {
            return layout;
        }

        if (selector == 0x09 &&
            TryGetSelector09MovementLayout(updateCount, segmentIndex, bytes, payloadOffset, out layout))
        {
            return layout;
        }

        if (selector == 0x28 &&
            TryGetCompactEffectEmoteMarkerLayout(updateCount, segmentIndex, bytes, payloadOffset, out layout))
        {
            return layout;
        }

        if (TryGetMode06TupleBodyLayout(updateCount, segmentIndex, selector, bytes, payloadOffset, out layout))
        {
            return layout;
        }

        return selector switch
        {
            0x00 => new Protocol03SelectorLayout("one-byte state marker", 1),
            0x01 when updateCount == 1 => new Protocol03SelectorLayout("animation/delete payload", 4),
            0x02 => new Protocol03SelectorLayout("empty state marker", 0),
            0x04 => new Protocol03SelectorLayout("rotation", 1),
            0x06 => new Protocol03SelectorLayout("animation + rotation", 2),
            0x08 => new Protocol03SelectorLayout("position xyz", 12),
            0x0a => new Protocol03SelectorLayout("position xyz + flag", 13),
            0x0c when updateCount < 0x08 => new Protocol03SelectorLayout("position xyz + flag", 13),
            0x0e when updateCount < 0x08 => new Protocol03SelectorLayout("position xyz + two flags", 14),
            0x28 when updateCount == 1 => new Protocol03SelectorLayout("emote payload", 26),
            0x28 => new Protocol03SelectorLayout("effect/emote variable block", null),
            0x80 => new Protocol03SelectorLayout("appearance/attribute variable block", null),
            0x9b when updateCount == 1 => new Protocol03SelectorLayout("fixed selector 9b state block", 10),
            _ when IsProtocol03StaticObjectSelector(selector) => new Protocol03SelectorLayout("static object/door variable block", null),
            _ when updateCount >= 0x08 => new Protocol03SelectorLayout("spawn/profile variable block", null),
            _ => new Protocol03SelectorLayout("unknown selector payload", null)
        };
    }

    private static bool TryGetCompactEffectEmoteMarkerLayout(
        byte updateCount,
        int segmentIndex,
        IReadOnlyList<byte> bytes,
        int payloadOffset,
        out Protocol03SelectorLayout layout)
    {
        layout = null!;
        if (updateCount <= 1 ||
            payloadOffset + 4 != bytes.Count)
        {
            return false;
        }

        if (bytes[payloadOffset] != 0x01 ||
            bytes[payloadOffset + 1] is not (0x01 or 0x02 or 0x03) ||
            bytes[payloadOffset + 2] != 0x00 ||
            bytes[payloadOffset + 3] != 0x00)
        {
            return false;
        }

        layout = new Protocol03SelectorLayout("compact effect/emote terminal marker", 4);
        return true;
    }

    private static bool TryGetMode06TupleBodyLayout(
        byte updateCount,
        int segmentIndex,
        byte selector,
        IReadOnlyList<byte> bytes,
        int payloadOffset,
        out Protocol03SelectorLayout layout)
    {
        const int targetPrefixBytes = 31;
        const int postPrefixTailOffset = 16 + targetPrefixBytes;
        const int postTupleTailOffset = postPrefixTailOffset + 8;

        layout = null!;
        if (segmentIndex != updateCount - 1)
        {
            return false;
        }

        int availableBytes = Math.Max(0, bytes.Count - payloadOffset);
        if (availableBytes <= postTupleTailOffset + 4)
        {
            return false;
        }

        Protocol03NestedMovementLead? lead = ExtractProtocol03NestedMovementLeads(
                selector,
                bytes,
                payloadOffset,
                availableBytes,
                0)
            .FirstOrDefault();
        if (lead is null ||
            !lead.MarkerModeHex.Equals("06", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        int tailStartOffset = GetProtocol03NestedMovementTailStartOffset(lead);
        if (tailStartOffset < 0 || tailStartOffset >= availableBytes)
        {
            return false;
        }

        byte[] payload = bytes.Skip(payloadOffset).Take(availableBytes).ToArray();
        byte[] tail = payload.Skip(tailStartOffset).ToArray();
        if (!IsProtocol03NestedMovementMode06FfContinuationLead(tail) ||
            tail.Length <= postTupleTailOffset + 3)
        {
            return false;
        }

        bool postPrefixLooksLikeObjectView = LooksLikeProtocol03ObjectViewHeader(tail, postPrefixTailOffset);
        string postPrefixDisposition = postPrefixLooksLikeObjectView
            ? ClassifyProtocol03ObjectView(tail[postPrefixTailOffset + 2], tail[postPrefixTailOffset + 3])
            : "non-object-view post-prefix lead";
        string tupleLayoutKind = ClassifyProtocol03NestedMovementMode06PostPrefixTuple(tail, postPrefixTailOffset, postPrefixLooksLikeObjectView, postPrefixDisposition);
        bool hasBoundaryAfterTuple = TryFindProtocol03ObjectViewHeader(
            tail,
            postTupleTailOffset,
            tail.Length,
            Math.Max(0, tail.Length - postTupleTailOffset),
            includeNpcBasePostCreationTailSelectors: false,
            out int tailBoundaryOffset);

        bool terminalTupleBody = !hasBoundaryAfterTuple;
        if (terminalTupleBody)
        {
            tailBoundaryOffset = tail.Length;
        }

        int postTupleBodyBytes = tailBoundaryOffset - postTupleTailOffset;
        if (postTupleBodyBytes <= 0)
        {
            return false;
        }

        byte[] body = tail.Skip(postTupleTailOffset).Take(postTupleBodyBytes).ToArray();
        int? firstNonZeroOffset = FindFirstNonZeroOffset(body);
        string firstNonZeroFieldHex = firstNonZeroOffset is null
            ? string.Empty
            : FormatHeader(body.Skip(firstNonZeroOffset.Value).Take(Math.Min(2, body.Length - firstNonZeroOffset.Value)));
        if (firstNonZeroOffset != 6 ||
            !firstNonZeroFieldHex.Equals("ff 00", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        int payloadBytes = tailStartOffset + tailBoundaryOffset;
        int boundaryOffset = payloadOffset + payloadBytes;
        string boundaryClassification;
        string boundaryHeaderHex;
        if (terminalTupleBody)
        {
            boundaryClassification = "terminal tail end";
            boundaryHeaderHex = string.Empty;
        }
        else
        {
            if (!TryFindProtocol03ObjectViewHeader(
                    bytes,
                    boundaryOffset,
                    bytes.Count,
                    0,
                    includeNpcBasePostCreationTailSelectors: false,
                    out int acceptedBoundaryOffset) ||
                acceptedBoundaryOffset != boundaryOffset)
            {
                return false;
            }

            boundaryClassification = ClassifyProtocol03ObjectView(bytes[boundaryOffset + 2], bytes[boundaryOffset + 3]);
            boundaryHeaderHex = FormatHeader(bytes.Skip(boundaryOffset).Take(Math.Min(8, Math.Max(0, bytes.Count - boundaryOffset))));
        }
        byte[] prefix = tail.Skip(16).Take(targetPrefixBytes).ToArray();
        int? prefixFirstNonZeroOffset = FindFirstNonZeroOffset(prefix);
        string prefixFirstNonZeroFieldHex = prefixFirstNonZeroOffset is null
            ? string.Empty
            : FormatHeader(prefix.Skip(prefixFirstNonZeroOffset.Value).Take(Math.Min(2, prefix.Length - prefixFirstNonZeroOffset.Value)));
        string postPrefixLeadHex = FormatHeader(tail.Skip(postPrefixTailOffset).Take(Math.Min(8, tail.Length - postPrefixTailOffset)));
        string tailPrefixKind = ClassifyProtocol03NestedMovementMode06TailPrefix(lead);
        string bodySuffixHex = FormatHeader(body.Skip(Math.Max(0, body.Length - 16)).Take(Math.Min(16, body.Length)));

        string parserAction;
        string classification;
        if (tailPrefixKind.Equals("ff marker plus zero-padded ff continuation lead", StringComparison.Ordinal) &&
            TryClassifyProtocol03Mode06LongEmbeddedMovementTrailerBody(
                body,
                terminalTupleBody,
                bodySuffixHex,
                boundaryHeaderHex,
                out parserAction,
                out classification))
        {
        }
        else if (terminalTupleBody &&
            tailPrefixKind.Equals("ff marker plus zero-padded ff continuation lead", StringComparison.Ordinal) &&
            postTupleBodyBytes == 96 &&
            IsProtocol03Mode06Terminal96ByteTupleBody(body) &&
            ((tupleLayoutKind.Equals("unclassified object-view tuple", StringComparison.Ordinal) &&
                postPrefixDisposition.Equals("unclassified object-view update", StringComparison.Ordinal)) ||
                (tupleLayoutKind.Equals("zero-update marker tuple", StringComparison.Ordinal) &&
                postPrefixDisposition.Equals("non-object-view post-prefix lead", StringComparison.Ordinal))))
        {
            parserAction = "test terminal mode 06 96-byte tuple body";
            classification = "mode 06 terminal 96-byte tuple body";
        }
        else if (terminalTupleBody)
        {
            return false;
        }
        else if (tupleLayoutKind.Equals("zero-update marker tuple", StringComparison.Ordinal) &&
            postPrefixDisposition.Equals("non-object-view post-prefix lead", StringComparison.Ordinal) &&
            postTupleBodyBytes == 70)
        {
            parserAction = "test consuming mode 06 zero-update tuple body";
            classification = "mode 06 zero-update tuple body";
        }
        else if (tailPrefixKind.Equals("ff marker plus zero-padded ff continuation lead", StringComparison.Ordinal) &&
            tupleLayoutKind.Equals("zero-update marker tuple", StringComparison.Ordinal) &&
            postPrefixDisposition.Equals("non-object-view post-prefix lead", StringComparison.Ordinal) &&
            postTupleBodyBytes == 94 &&
            IsProtocol03Mode06GuardedZeroUpdate94ByteBodySuffix(bodySuffixHex))
        {
            parserAction = "test guarded mode 06 94-byte zero-update tuple body";
            classification = "mode 06 guarded 94-byte zero-update tuple body";
        }
        else if (tailPrefixKind.Equals("ff marker plus zero-padded ff continuation lead", StringComparison.Ordinal) &&
            IsProtocol03Mode06Guarded57ByteMarker24And25ObjectViewPrefixAndLead(prefixFirstNonZeroFieldHex, postPrefixLeadHex) &&
            tupleLayoutKind.Equals("unclassified object-view tuple", StringComparison.Ordinal) &&
            postPrefixDisposition.Equals("unclassified object-view update", StringComparison.Ordinal) &&
            postTupleBodyBytes == 57 &&
            IsProtocol03Mode06Guarded57ByteMarker24And25ObjectViewBodySuffix(bodySuffixHex) &&
            IsProtocol03Mode06Guarded57ByteMarker16ObjectViewBoundaryHeader(boundaryHeaderHex))
        {
            parserAction = "test guarded mode 06 57-byte marker-24-25 object-view tuple body";
            classification = "mode 06 guarded 57-byte marker-24/25 object-view tuple body";
        }
        else if (tailPrefixKind.Equals("ff marker plus zero-padded ff continuation lead", StringComparison.Ordinal) &&
            IsProtocol03Mode06Guarded57ByteMarker24And26ObjectViewPrefixAndLead(prefixFirstNonZeroFieldHex, postPrefixLeadHex) &&
            tupleLayoutKind.Equals("unclassified object-view tuple", StringComparison.Ordinal) &&
            postPrefixDisposition.Equals("unclassified object-view update", StringComparison.Ordinal) &&
            postTupleBodyBytes == 57 &&
            IsProtocol03Mode06Guarded57ByteMarker24And26ObjectViewBodySuffix(bodySuffixHex) &&
            IsProtocol03Mode06Guarded57ByteMarker16ObjectViewBoundaryHeader(boundaryHeaderHex))
        {
            parserAction = "test guarded mode 06 57-byte marker-24-26 object-view tuple body";
            classification = "mode 06 guarded 57-byte marker-24/26 object-view tuple body";
        }
        else if (tailPrefixKind.Equals("ff marker plus zero-padded ff continuation lead", StringComparison.Ordinal) &&
            IsProtocol03Mode06Guarded57ByteMarker29And2aObjectViewPrefixAndLead(prefixFirstNonZeroFieldHex, postPrefixLeadHex) &&
            tupleLayoutKind.Equals("unclassified object-view tuple", StringComparison.Ordinal) &&
            postPrefixDisposition.Equals("unclassified object-view update", StringComparison.Ordinal) &&
            postTupleBodyBytes == 57 &&
            IsProtocol03Mode06Guarded57ByteMarker29And2aObjectViewBodySuffix(bodySuffixHex) &&
            IsProtocol03Mode06Guarded57ByteMarker16ObjectViewBoundaryHeader(boundaryHeaderHex))
        {
            parserAction = "test guarded mode 06 57-byte marker-29-2a object-view tuple body";
            classification = "mode 06 guarded 57-byte marker-29/2a object-view tuple body";
        }
        else if (tailPrefixKind.Equals("ff marker plus zero-padded ff continuation lead", StringComparison.Ordinal) &&
            IsProtocol03Mode06Guarded57ByteMarker24AltObjectViewPrefixAndLead(prefixFirstNonZeroFieldHex, postPrefixLeadHex) &&
            tupleLayoutKind.Equals("unclassified object-view tuple", StringComparison.Ordinal) &&
            postPrefixDisposition.Equals("unclassified object-view update", StringComparison.Ordinal) &&
            postTupleBodyBytes == 57 &&
            IsProtocol03Mode06Guarded57ByteMarker24AltObjectViewBodySuffix(bodySuffixHex) &&
            IsProtocol03Mode06Guarded57ByteMarker16ObjectViewBoundaryHeader(boundaryHeaderHex))
        {
            parserAction = "test guarded mode 06 57-byte marker-24-alt object-view tuple body";
            classification = "mode 06 guarded 57-byte marker-24 alternate object-view tuple body";
        }
        else if (tailPrefixKind.Equals("ff marker plus zero-padded ff continuation lead", StringComparison.Ordinal) &&
            IsProtocol03Mode06Guarded57ByteMarker1eAnd1fObjectViewPrefixAndLead(prefixFirstNonZeroFieldHex, postPrefixLeadHex) &&
            tupleLayoutKind.Equals("unclassified object-view tuple", StringComparison.Ordinal) &&
            postPrefixDisposition.Equals("unclassified object-view update", StringComparison.Ordinal) &&
            postTupleBodyBytes == 57 &&
            IsProtocol03Mode06Guarded57ByteMarker1eAnd1fObjectViewBodySuffix(bodySuffixHex) &&
            IsProtocol03Mode06Guarded57ByteMarker16ObjectViewBoundaryHeader(boundaryHeaderHex))
        {
            parserAction = "test guarded mode 06 57-byte marker-1e-1f object-view tuple body";
            classification = "mode 06 guarded 57-byte marker-1e/1f object-view tuple body";
        }
        else if (tailPrefixKind.Equals("ff marker plus zero-padded ff continuation lead", StringComparison.Ordinal) &&
            tupleLayoutKind.Equals("unclassified object-view tuple", StringComparison.Ordinal) &&
            postPrefixDisposition.Equals("unclassified object-view update", StringComparison.Ordinal) &&
            postTupleBodyBytes == 57 &&
            IsProtocol03Mode06Guarded57ByteObjectViewBodySuffix(bodySuffixHex))
        {
            parserAction = "test guarded mode 06 57-byte object-view tuple body";
            classification = "mode 06 guarded 57-byte object-view tuple body";
        }
        else if (tailPrefixKind.Equals("ff marker plus zero-padded ff continuation lead", StringComparison.Ordinal) &&
            prefixFirstNonZeroFieldHex.Equals("03 ff", StringComparison.OrdinalIgnoreCase) &&
            tupleLayoutKind.Equals("unclassified object-view tuple", StringComparison.Ordinal) &&
            postPrefixDisposition.Equals("unclassified object-view update", StringComparison.Ordinal) &&
            postTupleBodyBytes == 94 &&
            postPrefixLeadHex.Equals("00 46 05 00 00 00 00 34", StringComparison.OrdinalIgnoreCase) &&
            IsProtocol03Mode06Guarded94ByteMarker34ObjectViewBodySuffix(bodySuffixHex))
        {
            parserAction = "test guarded mode 06 94-byte marker-34 object-view tuple body";
            classification = "mode 06 guarded 94-byte marker-34 object-view tuple body";
        }
        else if (tailPrefixKind.Equals("ff marker plus zero-padded ff continuation lead", StringComparison.Ordinal) &&
            tupleLayoutKind.Equals("unclassified object-view tuple", StringComparison.Ordinal) &&
            postPrefixDisposition.Equals("unclassified object-view update", StringComparison.Ordinal) &&
            postTupleBodyBytes == 94 &&
            IsProtocol03Mode06Guarded94ByteObjectViewBodySuffix(bodySuffixHex))
        {
            parserAction = "test guarded mode 06 94-byte object-view tuple body";
            classification = "mode 06 guarded 94-byte object-view tuple body";
        }
        else if (tailPrefixKind.Equals("ff marker plus zero-padded ff continuation lead", StringComparison.Ordinal) &&
            prefixFirstNonZeroFieldHex.Equals("01 ff", StringComparison.OrdinalIgnoreCase) &&
            tupleLayoutKind.Equals("unclassified object-view tuple", StringComparison.Ordinal) &&
            postPrefixDisposition.Equals("unclassified object-view update", StringComparison.Ordinal) &&
            postTupleBodyBytes == 70 &&
            IsProtocol03Mode06Guarded70ByteObjectViewBodySuffix(bodySuffixHex) &&
            postPrefixLeadHex.Equals("00 98 08 00 00 00 00 2a", StringComparison.OrdinalIgnoreCase) &&
            boundaryHeaderHex.Equals("00 68 01 00 4e 37 08 00", StringComparison.OrdinalIgnoreCase))
        {
            parserAction = "test guarded mode 06 70-byte object-view tuple body";
            classification = "mode 06 guarded 70-byte object-view tuple body";
        }
        else if (tailPrefixKind.Equals("ff marker plus zero-padded ff continuation lead", StringComparison.Ordinal) &&
            IsProtocol03Mode06Guarded57ByteMarker16And17ObjectViewPrefixAndLead(prefixFirstNonZeroFieldHex, postPrefixLeadHex) &&
            tupleLayoutKind.Equals("unclassified object-view tuple", StringComparison.Ordinal) &&
            postPrefixDisposition.Equals("unclassified object-view update", StringComparison.Ordinal) &&
            postTupleBodyBytes == 57 &&
            IsProtocol03Mode06Guarded57ByteMarker16And17ObjectViewBodySuffix(bodySuffixHex) &&
            IsProtocol03Mode06Guarded57ByteMarker16ObjectViewBoundaryHeader(boundaryHeaderHex))
        {
            parserAction = "test guarded mode 06 57-byte marker-16-17 object-view tuple body";
            classification = "mode 06 guarded 57-byte marker-16/17 object-view tuple body";
        }
        else if (tailPrefixKind.Equals("ff marker plus zero-padded ff continuation lead", StringComparison.Ordinal) &&
            IsProtocol03Mode06Guarded57ByteMarker16ObjectViewPrefixAndLead(prefixFirstNonZeroFieldHex, postPrefixLeadHex) &&
            tupleLayoutKind.Equals("unclassified object-view tuple", StringComparison.Ordinal) &&
            postPrefixDisposition.Equals("unclassified object-view update", StringComparison.Ordinal) &&
            postTupleBodyBytes == 57 &&
            IsProtocol03Mode06Guarded57ByteMarker16ObjectViewBodySuffix(bodySuffixHex) &&
            IsProtocol03Mode06Guarded57ByteMarker16ObjectViewBoundaryHeader(boundaryHeaderHex))
        {
            parserAction = "test guarded mode 06 57-byte marker-16 object-view tuple body";
            classification = "mode 06 guarded 57-byte marker-16 object-view tuple body";
        }
        else if (tailPrefixKind.Equals("ff marker plus zero-padded ff continuation lead", StringComparison.Ordinal) &&
            IsProtocol03Mode06Guarded57ByteMarker15ObjectViewPrefixAndLead(prefixFirstNonZeroFieldHex, postPrefixLeadHex) &&
            tupleLayoutKind.Equals("unclassified object-view tuple", StringComparison.Ordinal) &&
            postPrefixDisposition.Equals("unclassified object-view update", StringComparison.Ordinal) &&
            postTupleBodyBytes == 57 &&
            IsProtocol03Mode06Guarded57ByteMarker15ObjectViewBodySuffix(bodySuffixHex) &&
            boundaryHeaderHex.Equals("28 ff 01 00 00 00 00 00", StringComparison.OrdinalIgnoreCase))
        {
            parserAction = "test guarded mode 06 57-byte marker-15 object-view tuple body";
            classification = "mode 06 guarded 57-byte marker-15 object-view tuple body";
        }
        else if (tailPrefixKind.Equals("ff marker plus zero-padded ff continuation lead", StringComparison.Ordinal) &&
            IsProtocol03Mode06Guarded57ByteMarker19ObjectViewPrefixAndLead(prefixFirstNonZeroFieldHex, postPrefixLeadHex) &&
            tupleLayoutKind.Equals("unclassified object-view tuple", StringComparison.Ordinal) &&
            postPrefixDisposition.Equals("unclassified object-view update", StringComparison.Ordinal) &&
            postTupleBodyBytes == 57 &&
            IsProtocol03Mode06Guarded57ByteMarker19ObjectViewBodySuffix(bodySuffixHex) &&
            IsProtocol03Mode06Guarded57ByteMarker16ObjectViewBoundaryHeader(boundaryHeaderHex))
        {
            parserAction = "test guarded mode 06 57-byte marker-19 object-view tuple body";
            classification = "mode 06 guarded 57-byte marker-19 object-view tuple body";
        }
        else if (tailPrefixKind.Equals("ff marker plus zero-padded ff continuation lead", StringComparison.Ordinal) &&
            IsProtocol03Mode06Guarded57ByteMarker19AltObjectViewPrefixAndLead(prefixFirstNonZeroFieldHex, postPrefixLeadHex) &&
            tupleLayoutKind.Equals("unclassified object-view tuple", StringComparison.Ordinal) &&
            postPrefixDisposition.Equals("unclassified object-view update", StringComparison.Ordinal) &&
            postTupleBodyBytes == 57 &&
            IsProtocol03Mode06Guarded57ByteMarker19AltObjectViewBodySuffix(bodySuffixHex) &&
            boundaryHeaderHex.Equals("28 ff 01 00 00 00 00 00", StringComparison.OrdinalIgnoreCase))
        {
            parserAction = "test guarded mode 06 57-byte marker-19-alt object-view tuple body";
            classification = "mode 06 guarded 57-byte marker-19-alt object-view tuple body";
        }
        else if (tailPrefixKind.Equals("ff marker plus zero-padded ff continuation lead", StringComparison.Ordinal) &&
            IsProtocol03Mode06Guarded57ByteMarker1fObjectViewPrefixAndLead(prefixFirstNonZeroFieldHex, postPrefixLeadHex) &&
            tupleLayoutKind.Equals("unclassified object-view tuple", StringComparison.Ordinal) &&
            postPrefixDisposition.Equals("unclassified object-view update", StringComparison.Ordinal) &&
            postTupleBodyBytes == 57 &&
            IsProtocol03Mode06Guarded57ByteMarker1fObjectViewBodySuffix(bodySuffixHex) &&
            IsProtocol03Mode06Guarded57ByteMarker16ObjectViewBoundaryHeader(boundaryHeaderHex))
        {
            parserAction = "test guarded mode 06 57-byte marker-1f object-view tuple body";
            classification = "mode 06 guarded 57-byte marker-1f object-view tuple body";
        }
        else if (tailPrefixKind.Equals("ff marker plus zero-padded ff continuation lead", StringComparison.Ordinal) &&
            IsProtocol03Mode06Guarded57ByteMarker24ObjectViewPrefixAndLead(prefixFirstNonZeroFieldHex, postPrefixLeadHex) &&
            tupleLayoutKind.Equals("unclassified object-view tuple", StringComparison.Ordinal) &&
            postPrefixDisposition.Equals("unclassified object-view update", StringComparison.Ordinal) &&
            postTupleBodyBytes == 57 &&
            IsProtocol03Mode06Guarded57ByteMarker24ObjectViewBodySuffix(bodySuffixHex) &&
            IsProtocol03Mode06Guarded57ByteMarker16ObjectViewBoundaryHeader(boundaryHeaderHex))
        {
            parserAction = "test guarded mode 06 57-byte marker-24 object-view tuple body";
            classification = "mode 06 guarded 57-byte marker-24 object-view tuple body";
        }
        else if (tailPrefixKind.Equals("ff marker plus zero-padded ff continuation lead", StringComparison.Ordinal) &&
            IsProtocol03Mode06Guarded57ByteMarker33ObjectViewPrefixAndLead(prefixFirstNonZeroFieldHex, postPrefixLeadHex) &&
            tupleLayoutKind.Equals("unclassified object-view tuple", StringComparison.Ordinal) &&
            postPrefixDisposition.Equals("unclassified object-view update", StringComparison.Ordinal) &&
            postTupleBodyBytes == 57 &&
            IsProtocol03Mode06Guarded57ByteMarker33ObjectViewBodySuffix(bodySuffixHex) &&
            IsProtocol03Mode06Guarded57ByteMarker16ObjectViewBoundaryHeader(boundaryHeaderHex))
        {
            parserAction = "test guarded mode 06 57-byte marker-33 object-view tuple body";
            classification = "mode 06 guarded 57-byte marker-33 object-view tuple body";
        }
        else if (tailPrefixKind.Equals("ff marker plus zero-padded ff continuation lead", StringComparison.Ordinal) &&
            IsProtocol03Mode06Guarded57ByteMarker13ObjectViewPrefixAndLead(prefixFirstNonZeroFieldHex, postPrefixLeadHex) &&
            tupleLayoutKind.Equals("unclassified object-view tuple", StringComparison.Ordinal) &&
            postPrefixDisposition.Equals("unclassified object-view update", StringComparison.Ordinal) &&
            postTupleBodyBytes == 57 &&
            IsProtocol03Mode06Guarded57ByteMarker13ObjectViewBodySuffix(bodySuffixHex) &&
            boundaryHeaderHex.Equals("28 ff 02 00 00 00 00 00", StringComparison.OrdinalIgnoreCase))
        {
            parserAction = "test guarded mode 06 57-byte marker-13 object-view tuple body";
            classification = "mode 06 guarded 57-byte marker-13 object-view tuple body";
        }
        else if (tailPrefixKind.Equals("ff marker plus zero-padded ff continuation lead", StringComparison.Ordinal) &&
            IsProtocol03Mode06Guarded57ByteMarker1dAnd1eObjectViewPrefixAndLead(prefixFirstNonZeroFieldHex, postPrefixLeadHex) &&
            tupleLayoutKind.Equals("unclassified object-view tuple", StringComparison.Ordinal) &&
            postPrefixDisposition.Equals("unclassified object-view update", StringComparison.Ordinal) &&
            postTupleBodyBytes == 57 &&
            IsProtocol03Mode06Guarded57ByteMarker1dAnd1eObjectViewBodySuffix(bodySuffixHex) &&
            boundaryHeaderHex.Equals("28 ff 01 00 00 00 00 00", StringComparison.OrdinalIgnoreCase))
        {
            parserAction = "test guarded mode 06 57-byte marker-1d-1e object-view tuple body";
            classification = "mode 06 guarded 57-byte marker-1d/1e object-view tuple body";
        }
        else if (tailPrefixKind.Equals("ff marker plus zero-padded ff continuation lead", StringComparison.Ordinal) &&
            IsProtocol03Mode06Guarded57ByteMarker1eObjectViewPrefixAndLead(prefixFirstNonZeroFieldHex, postPrefixLeadHex) &&
            tupleLayoutKind.Equals("unclassified object-view tuple", StringComparison.Ordinal) &&
            postPrefixDisposition.Equals("unclassified object-view update", StringComparison.Ordinal) &&
            postTupleBodyBytes == 57 &&
            IsProtocol03Mode06Guarded57ByteMarker1eObjectViewBodySuffix(bodySuffixHex) &&
            boundaryHeaderHex.Equals("28 ff 01 00 00 00 00 00", StringComparison.OrdinalIgnoreCase))
        {
            parserAction = "test guarded mode 06 57-byte marker-1e object-view tuple body";
            classification = "mode 06 guarded 57-byte marker-1e object-view tuple body";
        }
        else if (tailPrefixKind.Equals("ff marker plus zero-padded ff continuation lead", StringComparison.Ordinal) &&
            IsProtocol03Mode06Guarded57ByteMarker1dObjectViewPrefixAndLead(prefixFirstNonZeroFieldHex, postPrefixLeadHex) &&
            tupleLayoutKind.Equals("unclassified object-view tuple", StringComparison.Ordinal) &&
            postPrefixDisposition.Equals("unclassified object-view update", StringComparison.Ordinal) &&
            postTupleBodyBytes == 57 &&
            IsProtocol03Mode06Guarded57ByteMarker1dObjectViewBodySuffix(bodySuffixHex) &&
            boundaryHeaderHex.Equals("28 ff 01 00 00 00 00 00", StringComparison.OrdinalIgnoreCase))
        {
            parserAction = "test guarded mode 06 57-byte marker-1d object-view tuple body";
            classification = "mode 06 guarded 57-byte marker-1d object-view tuple body";
        }
        else if (tailPrefixKind.Equals("ff marker plus zero-padded ff continuation lead", StringComparison.Ordinal) &&
            IsProtocol03Mode06Guarded57ByteMarker25ObjectViewPrefixAndLead(prefixFirstNonZeroFieldHex, postPrefixLeadHex) &&
            tupleLayoutKind.Equals("unclassified object-view tuple", StringComparison.Ordinal) &&
            postPrefixDisposition.Equals("unclassified object-view update", StringComparison.Ordinal) &&
            postTupleBodyBytes == 57 &&
            IsProtocol03Mode06Guarded57ByteMarker25ObjectViewBodySuffix(bodySuffixHex) &&
            IsProtocol03Mode06Guarded57ByteMarker16ObjectViewBoundaryHeader(boundaryHeaderHex))
        {
            parserAction = "test guarded mode 06 57-byte marker-25 object-view tuple body";
            classification = "mode 06 guarded 57-byte marker-25 object-view tuple body";
        }
        else if (tailPrefixKind.Equals("ff marker plus zero-padded ff continuation lead", StringComparison.Ordinal) &&
            IsProtocol03Mode06Guarded57ByteMarker1dB9ObjectViewPrefixAndLead(prefixFirstNonZeroFieldHex, postPrefixLeadHex) &&
            tupleLayoutKind.Equals("unclassified object-view tuple", StringComparison.Ordinal) &&
            postPrefixDisposition.Equals("unclassified object-view update", StringComparison.Ordinal) &&
            postTupleBodyBytes == 57 &&
            IsProtocol03Mode06Guarded57ByteMarker1dB9ObjectViewBodySuffix(bodySuffixHex) &&
            boundaryHeaderHex.Equals("28 ff 02 00 00 00 00 00", StringComparison.OrdinalIgnoreCase))
        {
            parserAction = "test guarded mode 06 57-byte marker-1d-b9 object-view tuple body";
            classification = "mode 06 guarded 57-byte marker-1d-b9 object-view tuple body";
        }
        else if (tailPrefixKind.Equals("ff marker plus zero-padded ff continuation lead", StringComparison.Ordinal) &&
            IsProtocol03Mode06Guarded57ByteMarker15EcObjectViewPrefixAndLead(prefixFirstNonZeroFieldHex, postPrefixLeadHex) &&
            tupleLayoutKind.Equals("unclassified object-view tuple", StringComparison.Ordinal) &&
            postPrefixDisposition.Equals("unclassified object-view update", StringComparison.Ordinal) &&
            postTupleBodyBytes == 57 &&
            IsProtocol03Mode06Guarded57ByteMarker15EcObjectViewBodySuffix(bodySuffixHex) &&
            boundaryHeaderHex.Equals("28 ff 01 00 00 00 00 00", StringComparison.OrdinalIgnoreCase))
        {
            parserAction = "test guarded mode 06 57-byte marker-15-ec object-view tuple body";
            classification = "mode 06 guarded 57-byte marker-15-ec object-view tuple body";
        }
        else if (tupleLayoutKind.Equals("unclassified object-view tuple", StringComparison.Ordinal) &&
            postPrefixDisposition.Equals("unclassified object-view update", StringComparison.Ordinal) &&
            postTupleBodyBytes is 57 or 70 or 94)
        {
            parserAction = "test guarded mode 06 unclassified tuple body";
            classification = "mode 06 unclassified tuple body";
        }
        else if (tailPrefixKind.Equals("ff marker plus zero-padded ff continuation lead", StringComparison.Ordinal) &&
            prefixFirstNonZeroFieldHex.Equals("02 ff", StringComparison.OrdinalIgnoreCase) &&
            tupleLayoutKind.Equals("unclassified object-view tuple", StringComparison.Ordinal) &&
            postPrefixDisposition.Equals("unclassified object-view update", StringComparison.Ordinal) &&
            postTupleBodyBytes == 53 &&
            postPrefixLeadHex.Equals("00 e9 07 00 00 00 00 34", StringComparison.OrdinalIgnoreCase) &&
            IsProtocol03Mode06Guarded53ByteMarker34ObjectViewBodySuffix(bodySuffixHex) &&
            boundaryHeaderHex.Equals("00 b8 01 00 28 ff 15 00", StringComparison.OrdinalIgnoreCase))
        {
            parserAction = "test guarded mode 06 53-byte marker-34-b8-15 object-view tuple body";
            classification = "mode 06 guarded 53-byte marker-34-b8-15 object-view tuple body";
        }
        else if (tailPrefixKind.Equals("ff marker plus zero-padded ff continuation lead", StringComparison.Ordinal) &&
            prefixFirstNonZeroFieldHex.Equals("02 ff", StringComparison.OrdinalIgnoreCase) &&
            tupleLayoutKind.Equals("unclassified object-view tuple", StringComparison.Ordinal) &&
            postPrefixDisposition.Equals("unclassified object-view update", StringComparison.Ordinal) &&
            postTupleBodyBytes == 53 &&
            postPrefixLeadHex.Equals("00 e9 07 00 00 00 00 34", StringComparison.OrdinalIgnoreCase) &&
            IsProtocol03Mode06Guarded53ByteMarker34ObjectViewBodySuffix(bodySuffixHex) &&
            boundaryHeaderHex.Equals("00 62 01 00 28 ff 0f 00", StringComparison.OrdinalIgnoreCase))
        {
            parserAction = "test guarded mode 06 53-byte marker-34-62-0f object-view tuple body";
            classification = "mode 06 guarded 53-byte marker-34-62-0f object-view tuple body";
        }
        else if (tailPrefixKind.Equals("ff marker plus zero-padded ff continuation lead", StringComparison.Ordinal) &&
            prefixFirstNonZeroFieldHex.Equals("02 ff", StringComparison.OrdinalIgnoreCase) &&
            tupleLayoutKind.Equals("unclassified object-view tuple", StringComparison.Ordinal) &&
            postPrefixDisposition.Equals("unclassified object-view update", StringComparison.Ordinal) &&
            postTupleBodyBytes == 53 &&
            postPrefixLeadHex.Equals("00 e9 07 00 00 00 00 34", StringComparison.OrdinalIgnoreCase) &&
            IsProtocol03Mode06Guarded53ByteMarker34ObjectViewBodySuffix(bodySuffixHex) &&
            boundaryHeaderHex.Equals("00 b8 01 00 28 ff 1c 00", StringComparison.OrdinalIgnoreCase))
        {
            parserAction = "test guarded mode 06 53-byte marker-34-b8-1c object-view tuple body";
            classification = "mode 06 guarded 53-byte marker-34-b8-1c object-view tuple body";
        }
        else if (tailPrefixKind.Equals("ff marker plus zero-padded ff continuation lead", StringComparison.Ordinal) &&
            prefixFirstNonZeroFieldHex.Equals("02 ff", StringComparison.OrdinalIgnoreCase) &&
            tupleLayoutKind.Equals("unclassified object-view tuple", StringComparison.Ordinal) &&
            postPrefixDisposition.Equals("unclassified object-view update", StringComparison.Ordinal) &&
            postTupleBodyBytes == 53 &&
            postPrefixLeadHex.Equals("00 e9 07 00 00 00 00 34", StringComparison.OrdinalIgnoreCase) &&
            boundaryClassification.Equals("single state marker update", StringComparison.Ordinal))
        {
            parserAction = "test guarded mode 06 unclassified 53-byte tuple body";
            classification = "mode 06 unclassified 53-byte tuple body";
        }
        else if (tailPrefixKind.Equals("ff marker-prefixed tail", StringComparison.Ordinal) &&
            prefixFirstNonZeroFieldHex.Equals("01 ff", StringComparison.OrdinalIgnoreCase) &&
            tupleLayoutKind.Equals("zero-update marker tuple", StringComparison.Ordinal) &&
            postPrefixDisposition.Equals("non-object-view post-prefix lead", StringComparison.Ordinal) &&
            postTupleBodyBytes == 57 &&
            postPrefixLeadHex.Equals("03 fa 00 00 00 00 00 03", StringComparison.OrdinalIgnoreCase) &&
            boundaryClassification.Equals("single state marker update", StringComparison.Ordinal) &&
            boundaryHeaderHex.Equals("28 ff 01 00 00 00 00 00", StringComparison.OrdinalIgnoreCase))
        {
            parserAction = "test guarded mode 06 non-dominant zero-update tuple body";
            classification = "mode 06 non-dominant zero-update tuple body";
        }
        else if (tailPrefixKind.Equals("ff marker-prefixed tail", StringComparison.Ordinal) &&
            prefixFirstNonZeroFieldHex.Equals("03 ff", StringComparison.OrdinalIgnoreCase) &&
            tupleLayoutKind.Equals("zero-update marker tuple", StringComparison.Ordinal) &&
            postPrefixDisposition.Equals("non-object-view post-prefix lead", StringComparison.Ordinal) &&
            postTupleBodyBytes == 49 &&
            postPrefixLeadHex.Equals("03 7d 00 00 00 00 00 03", StringComparison.OrdinalIgnoreCase) &&
            boundaryClassification.Equals("state marker/object update candidate", StringComparison.Ordinal) &&
            boundaryHeaderHex.Equals("00 bc 02 00 00 00 00 00", StringComparison.OrdinalIgnoreCase))
        {
            parserAction = "test guarded mode 06 non-dominant 49-byte zero-update tuple body";
            classification = "mode 06 non-dominant 49-byte zero-update tuple body";
        }
        else
        {
            return false;
        }

        int evidencePayloadBytes = Math.Min(availableBytes, payloadBytes + Math.Min(8, Math.Max(0, bytes.Count - boundaryOffset)));
        var boundary = new Protocol03NestedMovementConsumedBodyBoundary(
            parserAction,
            selector.ToString("x2", CultureInfo.InvariantCulture),
            0,
            payloadBytes,
            evidencePayloadBytes,
            tailStartOffset,
            tailBoundaryOffset,
            postTupleBodyBytes,
            payloadBytes,
            tupleLayoutKind,
            FormatHeader(body.Take(Math.Min(16, body.Length))),
            firstNonZeroOffset,
            firstNonZeroFieldHex,
            boundaryHeaderHex,
            boundaryClassification);

        layout = new Protocol03SelectorLayout(classification, payloadBytes, boundary);
        return true;
    }

    private static bool TryGetMovementStateTailLayout(
        byte updateCount,
        int segmentIndex,
        byte firstSelector,
        IReadOnlyList<byte> bytes,
        int payloadOffset,
        out Protocol03SelectorLayout layout)
    {
        layout = null!;
        if (updateCount != 3 ||
            segmentIndex != 1 ||
            firstSelector is not (0x08 or 0x09) ||
            payloadOffset + 5 > bytes.Count)
        {
            return false;
        }

        if ((bytes[payloadOffset + 3] == 0xff && bytes[payloadOffset + 4] == 0x01) ||
            (bytes[payloadOffset + 3] == 0x80 && bytes[payloadOffset + 4] == 0x80))
        {
            layout = new Protocol03SelectorLayout($"selector {firstSelector:x2} movement tail lead", null);
            return true;
        }

        return false;
    }

    private static bool TryGetSelector09MovementLayout(
        byte updateCount,
        int segmentIndex,
        IReadOnlyList<byte> bytes,
        int payloadOffset,
        out Protocol03SelectorLayout layout)
    {
        layout = null!;
        if (updateCount != 3 || segmentIndex != 0 || payloadOffset + 14 > bytes.Count)
        {
            return false;
        }

        if (bytes[payloadOffset] is not (0x00 or 0x08) ||
            bytes[payloadOffset + 1] != 0x00)
        {
            return false;
        }

        int positionOffset = payloadOffset + 2;
        if (!TryReadSingleLittleEndian(bytes, positionOffset, out float x) ||
            !TryReadSingleLittleEndian(bytes, positionOffset + 4, out float y) ||
            !TryReadSingleLittleEndian(bytes, positionOffset + 8, out float z) ||
            !IsPlausibleProtocol03Position(x, y, z))
        {
            return false;
        }

        layout = new Protocol03SelectorLayout("position xyz + two-byte prefix", 14);
        return true;
    }

    private static bool TryGetPrimaryMovementWrapperLayout(
        byte updateCount,
        int segmentIndex,
        IReadOnlyList<byte> bytes,
        int payloadOffset,
        out Protocol03SelectorLayout layout)
    {
        layout = null!;
        if (updateCount != 2 || segmentIndex != 0 || payloadOffset + 5 > bytes.Count)
        {
            return false;
        }

        if (bytes[payloadOffset] != 0x00 ||
            bytes[payloadOffset + 2] != 0x00 ||
            bytes[payloadOffset + 3] is not (0x01 or 0x02))
        {
            return false;
        }

        if (!TryGetProtocol03MovementPayloadInfo(bytes[payloadOffset + 4], out int innerPayloadBytes, out int innerPositionOffset))
        {
            return false;
        }

        int payloadBytes = 5 + innerPayloadBytes;
        int absolutePositionOffset = payloadOffset + 5 + innerPositionOffset;
        if (payloadOffset + payloadBytes > bytes.Count ||
            !TryReadSingleLittleEndian(bytes, absolutePositionOffset, out float x) ||
            !TryReadSingleLittleEndian(bytes, absolutePositionOffset + 4, out float y) ||
            !TryReadSingleLittleEndian(bytes, absolutePositionOffset + 8, out float z) ||
            !IsPlausibleProtocol03Position(x, y, z))
        {
            return false;
        }

        layout = new Protocol03SelectorLayout("primary movement wrapper", payloadBytes);
        return true;
    }

    private static bool TryGetSecondaryMovementWrapperLayout(
        byte updateCount,
        int segmentIndex,
        IReadOnlyList<byte> bytes,
        int payloadOffset,
        out Protocol03SelectorLayout layout)
    {
        layout = null!;
        if (updateCount != 2 || segmentIndex != 1 || payloadOffset + 5 > bytes.Count)
        {
            return false;
        }

        if (bytes[payloadOffset] != 0x00 || bytes[payloadOffset + 1] != 0x02)
        {
            return false;
        }

        int innerPayloadBytes = bytes[payloadOffset + 2] switch
        {
            0x08 => 12,
            0x0a or 0x0c => 13,
            0x0e => 14,
            _ => -1
        };
        if (innerPayloadBytes < 0)
        {
            return false;
        }

        int payloadBytes = 3 + innerPayloadBytes + 2;
        if (payloadOffset + payloadBytes > bytes.Count)
        {
            return false;
        }

        if (bytes[payloadOffset + payloadBytes - 2] != 0x00 || bytes[payloadOffset + payloadBytes - 1] != 0x00)
        {
            return false;
        }

        layout = new Protocol03SelectorLayout("secondary movement wrapper", payloadBytes);
        return true;
    }

    private static bool TryGetAdjacentMovementWrapperLayout(
        byte updateCount,
        int segmentIndex,
        byte selector,
        IReadOnlyList<byte> bytes,
        int payloadOffset,
        out Protocol03SelectorLayout layout)
    {
        layout = null!;
        if (updateCount != 2 ||
            segmentIndex != 1 ||
            IsKnownProtocol03DirectSelector(selector) ||
            payloadOffset + 3 > bytes.Count)
        {
            return false;
        }

        if (bytes[payloadOffset] != 0x00 ||
            bytes[payloadOffset + 1] != 0x02 ||
            !TryGetProtocol03MovementPayloadInfo(bytes[payloadOffset + 2], out int innerPayloadBytes, out int innerPositionOffset))
        {
            return false;
        }

        int payloadBytes = 3 + innerPayloadBytes;
        int nextObjectOffset = payloadOffset + payloadBytes;
        int absolutePositionOffset = payloadOffset + 3 + innerPositionOffset;
        if (nextObjectOffset >= bytes.Count ||
            !LooksLikeProtocol03ObjectViewHeader(bytes, nextObjectOffset) ||
            !TryReadSingleLittleEndian(bytes, absolutePositionOffset, out float x) ||
            !TryReadSingleLittleEndian(bytes, absolutePositionOffset + 4, out float y) ||
            !TryReadSingleLittleEndian(bytes, absolutePositionOffset + 8, out float z) ||
            !IsPlausibleProtocol03Position(x, y, z))
        {
            return false;
        }

        layout = new Protocol03SelectorLayout("adjacent movement wrapper", payloadBytes);
        return true;
    }

    private static bool IsKnownProtocol03DirectSelector(byte selector)
    {
        return selector is
            0x00 or 0x01 or 0x02 or 0x04 or 0x06 or 0x08 or 0x0a or 0x0c or 0x0e or
            0x28 or 0x3c or 0x80 or 0x9b or 0x9e or 0x9f or 0xa0;
    }

    private static bool LooksLikeProtocol03ObjectViewHeader(
        IReadOnlyList<byte> bytes,
        int offset,
        bool includeNpcBasePostCreationTailSelectors = false)
    {
        if (offset + 3 >= bytes.Count)
        {
            return false;
        }

        int viewId = ReadUInt16LittleEndian(bytes, offset);
        byte updateCount = bytes[offset + 2];
        byte firstSelector = bytes[offset + 3];
        if (viewId == 0 || updateCount == 0 || updateCount > 0x20)
        {
            return false;
        }

        return firstSelector is
            0x00 or 0x01 or 0x02 or 0x04 or 0x06 or 0x08 or 0x09 or 0x0a or 0x0c or 0x0e or
            0x12 or 0x13 or 0x14 or 0x28 or 0x2a or 0x2e or 0x3c or 0x57 or 0x80 or 0x9b or 0x9e or 0x9f or 0xa0 ||
            (includeNpcBasePostCreationTailSelectors && IsProtocol03NpcBasePostCreationTailSelector(firstSelector));
    }

    private static bool IsProtocol03NpcBasePostCreationTailSelector(byte firstSelector)
    {
        return firstSelector is 0x50 or 0x67 or 0x68 or 0x69 or 0x76 or 0x90 or 0xa3 or 0xd0;
    }

    public static byte[] EncodeHeader(int value, string direction)
    {
        if (direction == "client")
        {
            if (value < 0x80)
            {
                return new[] { (byte)value };
            }

            if (value < 0x200)
            {
                return new[] { (byte)(0x80 + (value >> 8)), (byte)(value & 0xff) };
            }
        }
        else if (direction == "server")
        {
            if (value < 0x80)
            {
                return new[] { (byte)value };
            }

            if (value < 0x200)
            {
                return new[] { (byte)0x80, (byte)(value & 0xff) };
            }
        }

        if (value <= 0xff)
        {
            return new[] { (byte)value };
        }

        return new[] { (byte)((value >> 8) & 0xff), (byte)(value & 0xff) };
    }

    public static int DecodeExternalHeader(IReadOnlyList<byte> rawBytes)
    {
        if (rawBytes.Count == 0)
        {
            return 0;
        }

        if (rawBytes.Count == 1)
        {
            return rawBytes[0];
        }

        byte first = rawBytes[0];
        byte second = rawBytes[1];
        if (first is 0x80 or 0x81)
        {
            return ((first - 0x80) << 8) + second;
        }

        if (first < 0x80)
        {
            return first;
        }

        return (first << 8) + second;
    }

    public static string FormatHeader(IEnumerable<byte> bytes)
    {
        return string.Join(" ", bytes.Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
    }

    private static string DetectProtocol(IReadOnlyList<byte> bytes)
    {
        if (bytes.Count > 1 && IsKnownProtocol(bytes[1]))
        {
            return $"0x{bytes[1]:x2}";
        }

        if (IsKnownProtocol(bytes[0]))
        {
            return $"0x{bytes[0]:x2}";
        }

        return "unknown";
    }

    private static string? DetectPacketDirection(string line)
    {
        if (!line.Contains("packet size", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (line.Contains("Client->GAME", StringComparison.OrdinalIgnoreCase)
            || line.Contains("Client->WORLD", StringComparison.OrdinalIgnoreCase))
        {
            return "client";
        }

        if (line.Contains("GAME->Client", StringComparison.OrdinalIgnoreCase)
            || line.Contains("WORLD->Client", StringComparison.OrdinalIgnoreCase))
        {
            return "server";
        }

        return line.Contains("->", StringComparison.Ordinal)
            ? "other"
            : null;
    }

    private static bool IsKnownProtocol(byte value)
    {
        return value is 0x03 or 0x04 or 0x05;
    }

    private static bool LooksLikeHexDumpLine(string line)
    {
        return HexDumpLineStartRegex().IsMatch(line);
    }

    private static bool LooksLikeLoosePacketStart(IReadOnlyList<byte> bytes)
    {
        if (bytes.Count == 0)
        {
            return false;
        }

        if (IsKnownProtocol(bytes[0]))
        {
            return true;
        }

        if (bytes.Count > 1 && (bytes[0] is 0x02 or 0x42) && IsKnownProtocol(bytes[1]))
        {
            return true;
        }

        if (bytes.Count > 5 && (bytes[0] is 0x82 or 0xc2) && IsKnownProtocol(bytes[5]))
        {
            return true;
        }

        return bytes.Count > 1 && (bytes[0] is 0x82 or 0xc2) && IsKnownProtocol(bytes[1]);
    }

    private static bool TryParsePacketDumpLineBytes(string line, out byte[] bytes)
    {
        string trimmed = StripComment(line).Trim();
        if (LooksLikeHexDumpLine(trimmed))
        {
            bytes = ParseHexBytes(trimmed).ToArray();
            return bytes.Length > 0;
        }

        if (trimmed.Length >= 8 && trimmed.Length % 2 == 0 && trimmed.All(Uri.IsHexDigit))
        {
            bytes = ParseCompactHexBytes(trimmed).ToArray();
            return bytes.Length >= 4;
        }

        if (TryParseLabeledCompactHexLine(trimmed, out bytes))
        {
            return true;
        }

        bytes = Array.Empty<byte>();
        return false;
    }

    private static bool TryParseLabeledCompactHexLine(string line, out byte[] bytes)
    {
        int separatorIndex = line.IndexOf(':', StringComparison.Ordinal);
        if (separatorIndex < 0 || separatorIndex == line.Length - 1)
        {
            bytes = Array.Empty<byte>();
            return false;
        }

        string compact = string.Concat(line[(separatorIndex + 1)..].Where(value => !char.IsWhiteSpace(value)));
        if (compact.Length < 8 || compact.Length % 2 != 0 || !compact.All(Uri.IsHexDigit))
        {
            bytes = Array.Empty<byte>();
            return false;
        }

        bytes = ParseCompactHexBytes(compact).ToArray();
        return bytes.Length >= 4;
    }

    private static string StripComment(string line)
    {
        int commentIndex = line.IndexOf("//", StringComparison.Ordinal);
        return commentIndex >= 0 ? line[..commentIndex] : line;
    }

    private static string InferProtocol(string path)
    {
        return path.Contains($"{Path.DirectorySeparatorChar}CR1{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
            ? "CR1"
            : path.Contains($"{Path.DirectorySeparatorChar}CR2{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                ? "CR2"
                : "unknown";
    }

    private static int ParseNumber(string value)
    {
        return value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? int.Parse(value[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture)
            : int.Parse(value, CultureInfo.InvariantCulture);
    }

    private static int? ParseOptionalInt(string? value)
    {
        return int.TryParse(value?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int result)
            ? result
            : null;
    }

    private static double? ParseOptionalDouble(string? value)
    {
        return double.TryParse(value?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double result)
            ? result
            : null;
    }

    private static string? NormalizeOptionalText(string? value)
    {
        string? trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private static string RelativePath(string file, string root)
    {
        if (string.IsNullOrWhiteSpace(root))
        {
            return file;
        }

        return Path.GetRelativePath(root, file);
    }

    private static string PacketDumpDisplayPath(string file, string packetDumpRoot, string repositoryRoot)
    {
        string repositoryRelative = Path.GetRelativePath(repositoryRoot, file);
        if (!repositoryRelative.StartsWith("..", StringComparison.Ordinal) && !Path.IsPathRooted(repositoryRelative))
        {
            return repositoryRelative;
        }

        string rootDisplay = DisplayPath(packetDumpRoot, repositoryRoot);
        string rootRelative = Path.GetRelativePath(packetDumpRoot, file);
        return rootRelative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(rootRelative)
            ? rootDisplay
            : Path.Combine(rootDisplay, rootRelative);
    }

    private static string InferExternalSourceName(string sourceRoot)
    {
        string directoryName = Path.GetFileName(sourceRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return string.IsNullOrWhiteSpace(directoryName)
            ? "external"
            : directoryName;
    }

    private static bool IsPacketDumpTextFile(string path)
    {
        string extension = Path.GetExtension(path);
        return extension.Equals(".txt", StringComparison.OrdinalIgnoreCase)
            || extension.StartsWith(".log", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsExternalSourceTextFile(string path)
    {
        string extension = Path.GetExtension(path);
        return extension.Equals(".cpp", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".h", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".hpp", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".log", StringComparison.OrdinalIgnoreCase)
            || extension.StartsWith(".log", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".txt", StringComparison.OrdinalIgnoreCase);
    }

    private static string DisplayPath(string path, string repositoryRoot)
    {
        string relative = Path.GetRelativePath(repositoryRoot, path);
        return relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative)
            ? $"{Path.GetFileName(path)} (external)"
            : relative;
    }

    private static void Increment(Dictionary<string, int> counts, string key)
    {
        counts[key] = counts.TryGetValue(key, out int count) ? count + 1 : 1;
    }

    private static bool IsGameObjectDefinitionFile(string path)
    {
        return path.Contains($"{Path.DirectorySeparatorChar}GameObjectDefinitions{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
            || path.Contains($"{Path.DirectorySeparatorChar}gameobjects{Path.DirectorySeparatorChar}definitions{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);
    }

    private static string InferAttributeSource(string path)
    {
        if (path.Contains($"{Path.DirectorySeparatorChar}CR1{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
        {
            return "CR1";
        }

        if (path.Contains($"{Path.DirectorySeparatorChar}CR2{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
        {
            return "CR2";
        }

        return "gameobjects.gob";
    }

    private static int? ParseTrailingNumber(string value)
    {
        Match match = TrailingNumberRegex().Match(value);
        return match.Success
            ? ParseNumber(match.Groups["number"].Value)
            : null;
    }

    private static string FormatPayloadWord(IReadOnlyList<byte> payload, int offset)
    {
        return offset + 1 < payload.Count
            ? FormatHeader(new[] { payload[offset], payload[offset + 1] })
            : "-";
    }

    private static int ReadUInt16LittleEndian(IReadOnlyList<byte> payload, int offset)
    {
        return offset + 1 < payload.Count
            ? payload[offset] + (payload[offset + 1] << 8)
            : -1;
    }

    private static uint ReadUInt32LittleEndian(IReadOnlyList<byte> payload, int offset)
    {
        return offset + 3 < payload.Count
            ? (uint)(payload[offset]
                | (payload[offset + 1] << 8)
                | (payload[offset + 2] << 16)
                | (payload[offset + 3] << 24))
            : 0;
    }

    private static bool TryParseLittleEndianHexUInt16(string hex, out ushort value)
    {
        value = 0;
        if (hex.Length < 4 || !hex.Take(4).All(Uri.IsHexDigit))
        {
            return false;
        }

        byte[] bytes = ParseCompactHexBytes(hex[..4]).ToArray();
        value = (ushort)(bytes[0] | (bytes[1] << 8));
        return true;
    }

    private static bool TryParseLittleEndianHexUInt32(string hex, out uint value)
    {
        value = 0;
        if (hex.Length != 8 || !hex.All(Uri.IsHexDigit))
        {
            return false;
        }

        byte[] bytes = ParseCompactHexBytes(hex).ToArray();
        value = (uint)(bytes[0]
            | (bytes[1] << 8)
            | (bytes[2] << 16)
            | (bytes[3] << 24));
        return true;
    }

    private static bool TryReadSingleLittleEndian(IReadOnlyList<byte> payload, int offset, out float value)
    {
        value = 0;
        if (offset + 3 >= payload.Count)
        {
            return false;
        }

        value = BitConverter.Int32BitsToSingle((int)ReadUInt32LittleEndian(payload, offset));
        return true;
    }

    private static bool TryReadDoubleLittleEndian(byte[] payload, int offset, out double value)
    {
        value = 0;
        if (offset + 7 >= payload.Length)
        {
            return false;
        }

        long bits = BinaryPrimitives.ReadInt64LittleEndian(payload.AsSpan(offset, 8));
        value = BitConverter.Int64BitsToDouble(bits);
        return true;
    }

    private static bool TryGetObjectInteractionKind(string rawHeader, out string interactionKind)
    {
        if (rawHeader.Equals("80 c7", StringComparison.OrdinalIgnoreCase))
        {
            interactionKind = "dynamic";
            return true;
        }

        if (rawHeader.Equals("80 c8", StringComparison.OrdinalIgnoreCase)
            || rawHeader.Equals("80 c3", StringComparison.OrdinalIgnoreCase))
        {
            interactionKind = "static";
            return true;
        }

        interactionKind = string.Empty;
        return false;
    }

    private static string GetInteractionObjectTypeName(string interactionKind, int objectType)
    {
        if (interactionKind.Equals("dynamic", StringComparison.OrdinalIgnoreCase))
        {
            return objectType switch
            {
                0x02 => "subway",
                0x05 => "loot",
                _ => "unknown dynamic"
            };
        }

        return objectType switch
        {
            0x00 => "environment",
            0x01 => "collector",
            0x02 => "human NPC / vendor path",
            0x03 => "door",
            0x04 => "hardline upload",
            0x05 => "hardline LA exit",
            0x08 => "hardline sync",
            _ => "unknown static"
        };
    }

    private static bool TryReadByte(IReadOnlyList<byte> bytes, ref int offset, out byte value)
    {
        if (offset >= bytes.Count)
        {
            value = 0;
            return false;
        }

        value = bytes[offset++];
        return true;
    }

    private static bool TrySkip(IReadOnlyList<byte> bytes, ref int offset, int count)
    {
        if (count < 0 || offset + count > bytes.Count)
        {
            return false;
        }

        offset += count;
        return true;
    }

    private static bool TryReadPackedLength(IReadOnlyList<byte> bytes, ref int offset, out int length)
    {
        length = 0;
        if (!TryReadByte(bytes, ref offset, out byte first))
        {
            return false;
        }

        if (first < 0x80)
        {
            length = first;
            return true;
        }

        if (!TryReadByte(bytes, ref offset, out byte second))
        {
            return false;
        }

        length = ((first - 0x80) << 8) + second;
        return true;
    }

    private static string? FindLocalName(
        string rawHeader,
        int decodedValue,
        IReadOnlyDictionary<int, RpcHeaderEntry[]> localByValue,
        string? directionHint,
        IReadOnlyList<RpcHeaderEntry> localHeaders)
    {
        RpcHeaderEntry[] matches = FilterByDirection(FindLocalHeaderEntries(rawHeader, decodedValue, localByValue, localHeaders), directionHint)
            .Distinct()
            .OrderBy(entry => entry.Protocol)
            .ThenBy(entry => entry.Name)
            .ToArray();

        return matches.Length == 0
            ? null
            : string.Join("/", matches.Select(entry => $"{entry.Protocol}:{entry.Name}").Distinct());
    }

    private static IEnumerable<RpcHeaderEntry> FindLocalHeaderEntries(
        string rawHeader,
        int decodedValue,
        IReadOnlyDictionary<int, RpcHeaderEntry[]> localByValue,
        IReadOnlyList<RpcHeaderEntry> localHeaders)
    {
        IEnumerable<RpcHeaderEntry> rawMatches = localHeaders
            .Where(entry => entry.EncodedHeader.Equals(rawHeader, StringComparison.OrdinalIgnoreCase));
        IEnumerable<RpcHeaderEntry> valueMatches = localByValue.TryGetValue(decodedValue, out RpcHeaderEntry[]? valueEntries)
            ? valueEntries
            : Array.Empty<RpcHeaderEntry>();

        return rawMatches.Concat(valueMatches);
    }

    private static IEnumerable<RpcHeaderEntry> FilterByDirection(IEnumerable<RpcHeaderEntry> entries, string? directionHint)
    {
        if (directionHint is null)
        {
            return entries;
        }

        return entries.Where(entry => entry.Direction == directionHint);
    }

    private sealed record Protocol03RepeatListEntryField(
        int ZeroRun,
        int? FieldOffset,
        string FieldHex,
        int? SecondaryFieldOffset,
        string SecondaryFieldHex,
        string EntryLeadHex);

    private sealed record Protocol03EffectiveRepeatListEntryField(
        int? FieldOffset,
        string FieldHex,
        int? FieldValue,
        string Source);

    private sealed record Protocol03SelectorFfRepeatListFieldObservation(
        string FieldRole,
        int RoleOrder,
        int ZeroRun,
        int? FieldOffset,
        string FieldHex,
        int? FieldValue,
        string FieldLayoutKind,
        string LeadHex,
        Protocol03SelectorFfRepeatListCandidate Candidate);

    private sealed record Protocol03SelectorFfRepeatListHeaderOccurrence(
        Protocol03SelectorFfRepeatListCandidate Candidate,
        int PayloadOffset,
        int ObjectOffset,
        string WindowHex,
        RpcHeaderEntry Header,
        Protocol03VariableSelectorLead Lead);

    private sealed record Protocol03SelectorFfRepeatListWindowResourceReferenceKey(
        string WindowHalf,
        string WindowU16,
        string ResourceKind,
        string ResourceReference);

    private sealed record Protocol03NestedMovementObservation(
        string File,
        int Line,
        Protocol03ObjectViewSample Sample,
        Protocol03NestedMovementLead Lead,
        bool IsBoundedPrimaryWrapper);

    private sealed record Protocol03NpcBaseDynamicCreationObservation(
        string File,
        int Line,
        Protocol03NamedProfileRecord Record);

    private sealed record Protocol03NpcBaseDynamicTailHeaderLikeLead(
        int Offset,
        int ViewId,
        byte UpdateCount,
        byte FirstSelector)
    {
        public string Label => $"{Offset}: view {ViewId} count {UpdateCount:x2} selector {FirstSelector:x2}";
    }

    private sealed record Protocol03NpcBaseDynamicCreationBodyAnchor(
        Protocol03NpcBaseDynamicCreationObservation Observation,
        Protocol03NpcBaseDynamicTailHeaderLikeLead Lead,
        int BodySeparatorOffset,
        string BodyPrefixHex,
        string PostSeparatorPrefixHex);

    private sealed record WeightedValue(string Value, int Count);

    private sealed record Protocol03SelectorLayout(
        string Classification,
        int? PayloadBytes,
        Protocol03NestedMovementConsumedBodyBoundary? NestedMovementConsumedBodyBoundary = null);

    private sealed record Protocol03AttributeDescriptor(int Index, string Name, int Size);

    private sealed record VendorSellPriceField(
        string ResponseField,
        uint ResponseId,
        uint PriceId,
        bool IsAbility);

    [GeneratedRegex(@"public\s+enum\s+(?<name>[A-Za-z0-9_]+)")]
    private static partial Regex EnumStartRegex();

    [GeneratedRegex(@"^\s*(?<name>[A-Z0-9_]+)\s*=\s*(?<value>0x[0-9a-fA-F]+|\d+)")]
    private static partial Regex EnumEntryRegex();

    [GeneratedRegex(@"m_RPC(?<width>byte|short)\[0x(?<hex>[0-9a-fA-F]+)\]\s*=\s*&PlayerObject::(?<handler>[A-Za-z0-9_]+)")]
    private static partial Regex RajkoRpcRegex();

    [GeneratedRegex(@"HexGenericMsg[^""]*""(?<hex>[0-9a-fA-F]+)""")]
    private static partial Regex RajkoHexGenericMsgRegex();

    [GeneratedRegex(@"addRpcMessage\s*\(\s*StringUtils\.hexStringToBytes\s*\(\s*""(?<hex>[0-9a-fA-F]+)""\s*\)")]
    private static partial Regex LocalQueuedRpcHexRegex();

    [GeneratedRegex(@"StringUtils\.hexStringToBytes\s*\(\s*""(?<hex>[0-9a-fA-F]+)""\s*\)")]
    private static partial Regex LocalHexStringRegex();

    [GeneratedRegex(@"HandleCommand:\s*(?<hex>(?:[0-9a-fA-F]{2}\s*)+)")]
    private static partial Regex HandleCommandRegex();

    [GeneratedRegex(@"class\s+(?<name>[A-Za-z0-9_]+)\s*:\s*GameObject")]
    private static partial Regex ClassRegex();

    [GeneratedRegex(@"public\s+Attribute\s+(?<var>[A-Za-z0-9_]+)\s*=\s*new\s+Attribute\s*\(\s*(?<size>\d+)\s*,\s*""(?<name>[^""]+)""\s*\)")]
    private static partial Regex AttributeDeclarationRegex();

    [GeneratedRegex(@"AddAttribute\s*\(\s*ref\s+(?<var>[A-Za-z0-9_]+)\s*,\s*(?<creation>-?\d+)\s*,\s*(?<update>-?\d+)\s*\)")]
    private static partial Regex AddAttributeRegex();

    [GeneratedRegex(@"^(?<little>[0-9a-fA-F]{8})\s+(?<big>[0-9a-fA-F]{8})\s+=\s+(?<name>.+?)\s*$")]
    private static partial Regex FxDefinitionRegex();

    [GeneratedRegex(@"^(?<big>[0-9a-fA-F]{8})\s+=\s+(?<name>.+?)\s*$")]
    private static partial Regex SingleEndianFxDefinitionRegex();

    [GeneratedRegex(@"^(?<little>[0-9a-fA-F]{4})\s+(?<big>[0-9a-fA-F]{4})\s+=\s+(?<name>.+?)\s*$")]
    private static partial Regex AnimationDefinitionRegex();

    [GeneratedRegex(@"^&gib\s+(?<id>\d+)\s+(?<body>.+?)\s*$")]
    private static partial Regex ItemCommandRegex();

    [GeneratedRegex(@"^\s*(?<id>\d+),(?<code>[^,]+),(?:""(?<display>(?:[^""]|"""")*)""|(?<plain>.*))\s*$")]
    private static partial Regex AbilityDefinitionRegex();

    [GeneratedRegex(@"(?<number>\d+)$")]
    private static partial Regex TrailingNumberRegex();

    [GeneratedRegex(@"(?<![0-9a-fA-F])(?<hex>[0-9a-fA-F]{2})(?![0-9a-fA-F])")]
    private static partial Regex HexByteRegex();

    [GeneratedRegex(@"^\s*[0-9a-fA-F]{2}(\s|$)")]
    private static partial Regex HexDumpLineStartRegex();

    private sealed record Protocol04RpcBlock(string RawHeader, int DecodedValue, byte[] Payload);
}
