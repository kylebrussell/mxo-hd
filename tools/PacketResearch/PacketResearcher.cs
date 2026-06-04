using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace MxoHd.PacketResearch;

public static partial class PacketResearcher
{
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
        List<GameObjectEntry> gameObjectEntries = LoadGameObjectEntries(repositoryRoot).ToList();
        List<ItemCommandEntry> itemCommandEntries = LoadItemCommandEntries(repositoryRoot).ToList();
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
            .Distinct()
            .ToArray();
        List<StaticObjectEntry> staticObjectEntries = LoadStaticObjectEntries(repositoryRoot, interactionObjectIds).ToList();

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
            ItemCommandEntries = itemCommandEntries,
            StaticObjectEntries = staticObjectEntries
        };
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

            if (wantedObjectIds is not null && !wantedObjectIds.Contains(mxoId))
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

    public static IEnumerable<GameObjectEntry> LoadGameObjectEntries(string repositoryRoot)
    {
        string gameObjectsPath = Path.Combine(repositoryRoot, "data", "gameobjects.csv");
        if (!File.Exists(gameObjectsPath))
        {
            yield break;
        }

        foreach (GameObjectEntry entry in ParseGameObjectText(File.ReadAllLines(gameObjectsPath), RelativePath(gameObjectsPath, repositoryRoot)))
        {
            yield return entry;
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
            PacketDumpFileSummary summary = SummarizePacketDump(path, localHeaders, repositoryRoot);
            if (summary.PacketLikeLines > 0)
            {
                yield return summary;
            }
        }
    }

    public static PacketDumpFileSummary SummarizePacketDump(
        string file,
        IReadOnlyList<RpcHeaderEntry> localHeaders,
        string repositoryRoot = "")
    {
        Dictionary<string, int> protocols = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, int> rpcHeaders = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, int> knownHeaders = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, List<int>> knownHeaderSampleLines = new(StringComparer.OrdinalIgnoreCase);
        List<Protocol04PacketSequenceSample> protocol04PacketSequences = new();
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
        foreach (string line in File.ReadLines(file))
        {
            lineNumber++;
            string? packetDirection = DetectPacketDirection(line);
            if (packetDirection is not null)
            {
                AnalyzeCurrentPacket();
                currentPacketDirection = packetDirection;
                currentPacketStartLine = lineNumber;
                currentPacketBytes.Clear();
            }

            if (!LooksLikeHexDumpLine(line))
            {
                continue;
            }

            byte[] bytes = ParseHexBytes(line).ToArray();
            if (bytes.Length < 4)
            {
                continue;
            }

            packetLikeLines++;
            string protocol = DetectProtocol(bytes);
            Increment(protocols, protocol);

            foreach (string label in DetectKnownEncodedHeaders(bytes, localByEncodedHeader))
            {
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
            }

            if (currentPacketDirection is not null)
            {
                currentPacketBytes.AddRange(bytes);
            }
            else
            {
                AnalyzeProtocolBytes(bytes, lineNumber, null);
            }
        }

        AnalyzeCurrentPacket();

        return new PacketDumpFileSummary(
            RelativePath(file, repositoryRoot),
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
            AbilityUnloadPayloads = abilityUnloadPayloads.Count == 0 ? null : abilityUnloadPayloads,
            FriendListStatusPayloads = friendListStatusPayloads.Count == 0 ? null : friendListStatusPayloads,
            CoderAttributePayloads = coderAttributePayloads.Count == 0 ? null : coderAttributePayloads
        };

        void AnalyzeCurrentPacket()
        {
            if (currentPacketDirection is null || currentPacketBytes.Count == 0)
            {
                return;
            }

            AnalyzeProtocolBytes(currentPacketBytes, currentPacketStartLine, currentPacketDirection);
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

            protocol04PacketSequences.AddRange(DetectProtocol04PacketSequences(bytes, sampleLine, directionHint));
            protocol03ObjectViews.AddRange(DetectProtocol03ObjectViews(bytes, sampleLine, directionHint));
            protocol04InteractionPayloads.AddRange(DetectProtocol04InteractionPayloads(bytes, sampleLine, directionHint));
            playerAttributePayloads.AddRange(DetectPlayerAttributePayloads(bytes, sampleLine, directionHint));
            abilityUnloadPayloads.AddRange(DetectAbilityUnloadPayloads(bytes, sampleLine, directionHint));
            friendListStatusPayloads.AddRange(DetectFriendListStatusPayloads(bytes, sampleLine, directionHint));
            coderAttributePayloads.AddRange(DetectCoderAttributePayloads(bytes, sampleLine, directionHint));
            manageBonusPayloads.AddRange(DetectManageBonusPayloads(bytes, sampleLine, directionHint));
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

    public static IEnumerable<Protocol04PacketSequenceSample> DetectProtocol04PacketSequences(
        IReadOnlyList<byte> bytes,
        int lineNumber = 0,
        string? directionHint = null)
    {
        if (directionHint == "client")
        {
            yield break;
        }

        string[] headers = EnumerateProtocol04RpcBlocks(bytes)
            .Select(block => block.RawHeader)
            .ToArray();
        if (headers.Length == 0)
        {
            yield break;
        }

        yield return new Protocol04PacketSequenceSample(lineNumber, directionHint, headers);
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
            else if (selector == 0x80)
            {
                variableSelectorLeads.Add(CreateProtocol03VariableSelectorLead(selector, bytes, cursor, sampleBytes, cursor - offset));
            }

            if (layout.Classification is "unknown selector payload" or "primary movement wrapper")
            {
                nestedMovementLeads.AddRange(ExtractProtocol03NestedMovementLeads(selector, bytes, cursor, sampleBytes, cursor - offset));
            }

            if (layout.Classification.Contains("movement tail lead", StringComparison.Ordinal))
            {
                movementStateTailLeads.Add(CreateProtocol03MovementStateTailLead(firstSelector, selector, bytes, cursor, sampleBytes, cursor - offset));
            }

            if (selector is 0x9e or 0xa0 &&
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
            segments);
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
        string markerHex = payload.Length >= 7 && payload[3] == 0xff && payload[4] == 0x01
            ? FormatHeader(payload.Skip(3).Take(4))
            : FormatHeader(payload.Skip(3).Take(Math.Min(2, Math.Max(0, payload.Length - 3))));

        return new Protocol03MovementStateTailLead(
            firstSelector.ToString("x2", CultureInfo.InvariantCulture),
            tailSelector.ToString("x2", CultureInfo.InvariantCulture),
            relativeOffset,
            payload.Length,
            tagValue.ToString("x8", CultureInfo.InvariantCulture),
            tagValue,
            markerHex,
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

        float? q0 = TryReadSingleLittleEndian(payload, 10, out float q0Value) ? q0Value : null;
        float? q1 = TryReadSingleLittleEndian(payload, 14, out float q1Value) ? q1Value : null;
        float? q2 = TryReadSingleLittleEndian(payload, 18, out float q2Value) ? q2Value : null;
        float? q3 = TryReadSingleLittleEndian(payload, 22, out float q3Value) ? q3Value : null;

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
            FormatHeader(payload));
        return true;
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
                ParseObject12CreationAttributes(bytes, cursor + 4, end, offset, relativeOffset);
            if (creationAttributes.Count == 0)
            {
                continue;
            }

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

            IReadOnlyList<Protocol03CreationAttributeSample> creationAttributes = gameObjectId == 599
                ? ParseObject599CreationAttributes(bytes, cursor + 4, end, offset, relativeOffset)
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

    private static IReadOnlyList<Protocol03CreationAttributeSample> ParseObject599CreationAttributes(
        IReadOnlyList<byte> bytes,
        int attributeCountOffset,
        int end,
        int payloadOffset,
        int relativeOffset)
    {
        return ParseProtocol03CreationAttributes(bytes, attributeCountOffset, end, payloadOffset, relativeOffset, Object599CreationAttributes);
    }

    private static IReadOnlyList<Protocol03CreationAttributeSample> ParseObject12CreationAttributes(
        IReadOnlyList<byte> bytes,
        int attributeCountOffset,
        int end,
        int payloadOffset,
        int relativeOffset)
    {
        return ParseProtocol03CreationAttributes(bytes, attributeCountOffset, end, payloadOffset, relativeOffset, Object12CreationAttributes);
    }

    private static IReadOnlyList<Protocol03CreationAttributeSample> ParseProtocol03CreationAttributes(
        IReadOnlyList<byte> bytes,
        int attributeCountOffset,
        int end,
        int payloadOffset,
        int relativeOffset,
        IReadOnlyList<Protocol03AttributeDescriptor> descriptors)
    {
        List<Protocol03CreationAttributeSample> attributes = new();
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
                    return attributes;
                }
            }

            if (!hasMoreGroups)
            {
                break;
            }
        }

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

    public static IEnumerable<string> DetectKnownEncodedHeaders(
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
                yield return $"{header} {entry.Name} [{entry.Protocol} {entry.Direction}]";
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

        if (bytes.Count > 1 && (bytes[0] is 0x02 or 0x42) && bytes[1] == 0x03)
        {
            offset = 2;
            transportHeader = bytes[0].ToString("x2", CultureInfo.InvariantCulture);
            return true;
        }

        if (bytes.Count > 5 && (bytes[0] is 0x82 or 0xc2) && bytes[5] == 0x03)
        {
            offset = 6;
            transportHeader = $"{bytes[0]:x2} + simtime";
            return true;
        }

        return false;
    }

    private static string ClassifyProtocol03ObjectView(byte updateCount, byte firstSelector)
    {
        return (updateCount, firstSelector) switch
        {
            (0x01, 0x01) => "delete view candidate",
            (0x01, 0x08) => "single position update",
            (0x01, 0x28) => "emote update",
            (0x02, 0x08) => "position/object state update",
            (0x02, 0x0c) => "object state update",
            (0x02, 0x0e) => "object state update",
            (0x02, 0x60) => "name/profile update candidate",
            (0x02, 0x80) => "appearance/attribute update candidate",
            (0x03, 0x08) => "movement state bundle",
            (0x03, 0x09) => "movement state bundle",
            (0x03, 0x28) => "effect/emote state bundle",
            (0x04, 0x80) => "attribute/effect bundle",
            (0x08, 0x9e) => "static object/door spawn candidate",
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
            0x9e or 0xa0 => new Protocol03SelectorLayout("static object/door variable block", null),
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
            0x28 or 0x80 or 0x9b or 0x9e or 0xa0;
    }

    private static bool LooksLikeProtocol03ObjectViewHeader(IReadOnlyList<byte> bytes, int offset)
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
            0x12 or 0x13 or 0x14 or 0x28 or 0x2a or 0x2e or 0x57 or 0x80 or 0x9b or 0x9e or 0xa0;
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

        if (line.Contains("Client->GAME", StringComparison.OrdinalIgnoreCase))
        {
            return "client";
        }

        if (line.Contains("GAME->Client", StringComparison.OrdinalIgnoreCase))
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
        IEnumerable<RpcHeaderEntry> rawMatches = localHeaders
            .Where(entry => entry.EncodedHeader.Equals(rawHeader, StringComparison.OrdinalIgnoreCase));
        IEnumerable<RpcHeaderEntry> valueMatches = localByValue.TryGetValue(decodedValue, out RpcHeaderEntry[]? valueEntries)
            ? valueEntries
            : Array.Empty<RpcHeaderEntry>();

        RpcHeaderEntry[] matches = FilterByDirection(rawMatches.Concat(valueMatches), directionHint)
            .Distinct()
            .OrderBy(entry => entry.Protocol)
            .ThenBy(entry => entry.Name)
            .ToArray();

        return matches.Length == 0
            ? null
            : string.Join("/", matches.Select(entry => $"{entry.Protocol}:{entry.Name}").Distinct());
    }

    private static IEnumerable<RpcHeaderEntry> FilterByDirection(IEnumerable<RpcHeaderEntry> entries, string? directionHint)
    {
        if (directionHint is null)
        {
            return entries;
        }

        return entries.Where(entry => entry.Direction == directionHint);
    }

    private sealed record Protocol03SelectorLayout(string Classification, int? PayloadBytes);

    private sealed record Protocol03AttributeDescriptor(int Index, string Name, int Size);

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

    [GeneratedRegex(@"^&gib\s+(?<id>\d+)\s+(?<body>.+?)\s*$")]
    private static partial Regex ItemCommandRegex();

    [GeneratedRegex(@"(?<number>\d+)$")]
    private static partial Regex TrailingNumberRegex();

    [GeneratedRegex(@"(?<![0-9a-fA-F])(?<hex>[0-9a-fA-F]{2})(?![0-9a-fA-F])")]
    private static partial Regex HexByteRegex();

    [GeneratedRegex(@"^\s*[0-9a-fA-F]{2}(\s|$)")]
    private static partial Regex HexDumpLineStartRegex();

    private sealed record Protocol04RpcBlock(string RawHeader, int DecodedValue, byte[] Payload);
}
