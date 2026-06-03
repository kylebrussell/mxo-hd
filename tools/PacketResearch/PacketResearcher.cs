using System.Globalization;
using System.Text.RegularExpressions;

namespace MxoHd.PacketResearch;

public static partial class PacketResearcher
{
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

        return new PacketResearchReport(
            ".",
            dumpRoots.Select(root => DisplayPath(root, repositoryRoot)).ToArray(),
            normalizedRajkoRoot is null ? null : DisplayPath(normalizedRajkoRoot, repositoryRoot),
            normalizedExternalRoots.Select(root => DisplayPath(root, repositoryRoot)).ToArray(),
            localHeaders,
            attributeDefinitions,
            fxDefinitions,
            rajkoRpcHeaders,
            hardcodedCommands,
            hardcodedProtocol03Examples,
            protocol04InteractionCommandExamples,
            vendorInventoryEntries,
            comparisons,
            dumpSummaries);
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
        string fxListPath = Path.Combine(repositoryRoot, "research", "community-data", "fxlisthex.txt");
        if (!File.Exists(fxListPath))
        {
            yield break;
        }

        foreach (FxDefinition entry in ParseFxDefinitionText(File.ReadAllLines(fxListPath), RelativePath(fxListPath, repositoryRoot)))
        {
            yield return entry;
        }
    }

    public static IEnumerable<FxDefinition> ParseFxDefinitionText(IReadOnlyList<string> lines, string file)
    {
        for (int i = 0; i < lines.Count; i++)
        {
            Match match = FxDefinitionRegex().Match(lines[i]);
            if (!match.Success)
            {
                continue;
            }

            yield return new FxDefinition(
                match.Groups["little"].Value.ToLowerInvariant(),
                match.Groups["big"].Value.ToLowerInvariant(),
                match.Groups["name"].Value,
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
            manageBonusPayloads);

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
            Protocol03SelectorLayout layout = GetProtocol03SelectorLayout(updateCount, i, selector, bytes, cursor);
            int payloadBytes = layout.PayloadBytes ?? Math.Max(0, bytes.Count - cursor);
            int availableBytes = Math.Max(0, bytes.Count - cursor);
            int sampleBytes = Math.Min(payloadBytes, availableBytes);

            segments.Add(new Protocol03ObjectUpdateSegment(
                selector.ToString("x2", CultureInfo.InvariantCulture),
                layout.Classification,
                payloadBytes,
                selectorOffset,
                FormatHeader(bytes.Skip(cursor).Take(Math.Min(sampleBytes, 16))),
                layout.PayloadBytes is not null));

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
            parsedUpdateCount,
            unparsedBytes,
            complete,
            segments);
        return true;
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
            _ => "unclassified object-view update"
        };
    }

    private static Protocol03SelectorLayout GetProtocol03SelectorLayout(
        byte updateCount,
        int segmentIndex,
        byte selector,
        IReadOnlyList<byte> bytes,
        int payloadOffset)
    {
        if (TryGetSecondaryMovementWrapperLayout(updateCount, segmentIndex, bytes, payloadOffset, out Protocol03SelectorLayout layout))
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
            0x9e or 0xa0 => new Protocol03SelectorLayout("static object/door variable block", null),
            _ when updateCount >= 0x08 => new Protocol03SelectorLayout("spawn/profile variable block", null),
            _ => new Protocol03SelectorLayout("unknown selector payload", null)
        };
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

    [GeneratedRegex(@"^(?<little>[0-9a-fA-F]{8})\s+(?<big>[0-9a-fA-F]{8})\s+=\s+(?<name>\S+)")]
    private static partial Regex FxDefinitionRegex();

    [GeneratedRegex(@"(?<number>\d+)$")]
    private static partial Regex TrailingNumberRegex();

    [GeneratedRegex(@"(?<![0-9a-fA-F])(?<hex>[0-9a-fA-F]{2})(?![0-9a-fA-F])")]
    private static partial Regex HexByteRegex();

    [GeneratedRegex(@"^\s*[0-9a-fA-F]{2}(\s|$)")]
    private static partial Regex HexDumpLineStartRegex();

    private sealed record Protocol04RpcBlock(string RawHeader, int DecodedValue, byte[] Payload);
}
