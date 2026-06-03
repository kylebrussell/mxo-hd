using MxoHd.PacketResearch;

public class PacketResearcherTests
{
    [Fact]
    public void ParseNetworkHeaderText_EncodesClientHeadersLikeProtocol04()
    {
        string[] lines =
        {
            "public enum RPCRequestHeader",
            "{",
            "    CLIENT_CHAT = 0x28,",
            "    CLIENT_OBJECTINTERACTION_STATIC = 0xc8,",
            "    CLIENT_CMD_WHO = 0x152,",
            "}",
            "public enum RPCResponseHeaders",
            "{",
            "    SERVER_CHAT_MESSAGE_RESPONSE = 0x2e,",
            "    SERVER_MANAGE_BONUS = 0xbc,",
            "    SERVER_VENDOR_OPEN = 0x810d,",
            "}"
        };

        List<RpcHeaderEntry> entries = PacketResearcher.ParseNetworkHeaderText(lines, "NetworkProtocolHeaders.cs", "CR2").ToList();

        Assert.Equal("28", entries.Single(entry => entry.Name == "CLIENT_CHAT").EncodedHeader);
        Assert.Equal("80 c8", entries.Single(entry => entry.Name == "CLIENT_OBJECTINTERACTION_STATIC").EncodedHeader);
        Assert.Equal("81 52", entries.Single(entry => entry.Name == "CLIENT_CMD_WHO").EncodedHeader);
        Assert.Equal("2e", entries.Single(entry => entry.Name == "SERVER_CHAT_MESSAGE_RESPONSE").EncodedHeader);
        Assert.Equal("80 bc", entries.Single(entry => entry.Name == "SERVER_MANAGE_BONUS").EncodedHeader);
        Assert.Equal("81 0d", entries.Single(entry => entry.Name == "SERVER_VENDOR_OPEN").EncodedHeader);
    }

    [Fact]
    public void CompareRajkoToLocal_MatchesExactDecodedAndPrefixForms()
    {
        RpcHeaderEntry[] local =
        {
            new("mxo-hd", "CR2", "client", "RPCRequestHeader", "CLIENT_OBJECTINTERACTION_STATIC", 0xc8, "80 c8", "local.cs", 1),
            new("mxo-hd", "CR2", "client", "RPCRequestHeader", "CLIENT_CHAT", 0x28, "28", "local.cs", 2)
        };

        string[] rajkoLines =
        {
            "m_RPCshort[0x80c8] = &PlayerObject::RPC_HandleStaticObjInteraction;",
            "m_RPCshort[0x2810] = &PlayerObject::RPC_HandleChat;"
        };
        List<RajkoRpcEntry> rajko = PacketResearcher.ParseRajkoRpcMapText(rajkoLines, "PlayerObject.cpp").ToList();

        List<RpcComparison> comparisons = PacketResearcher.CompareRajkoToLocal(rajko, local).ToList();

        Assert.Equal(RpcMatchKind.DecodedValue, comparisons[0].MatchKind);
        Assert.Equal("CLIENT_CHAT", comparisons[0].Local?.Name);
        Assert.Equal(RpcMatchKind.ExactCommandBytes, comparisons[1].MatchKind);
        Assert.Equal("CLIENT_OBJECTINTERACTION_STATIC", comparisons[1].Local?.Name);
    }

    [Fact]
    public void ParseRajkoHardcodedCommandText_ExtractsManageBonusFields()
    {
        string[] lines =
        {
            "/* m_parent.QueueCommand(make_shared<HexGenericMsg>(\"80bc4503110000020000001100010000000000000000\")); */"
        };

        HardcodedCommandExample command = PacketResearcher.ParseRajkoHardcodedCommandText(lines, "PlayerObject.cpp").Single();

        Assert.Equal("rajko", command.Source);
        Assert.Equal("80 bc", command.RawHeader);
        Assert.Equal(0xbc, command.DecodedValue);
        Assert.Equal(20, command.PayloadLength);
        Assert.Equal("45 03", command.Field0);
        Assert.Equal(0x0345, command.Field0Value);
        Assert.Equal("11 00", command.Field1);
        Assert.Equal(0x0011, command.Field1Value);
        Assert.Equal("00 02", command.Field2);
        Assert.Equal(0x0200, command.Field2Value);
    }

    [Fact]
    public void ParseLocalQueuedRpcCommandText_ExtractsQueuedManageBonusFields()
    {
        string[] lines =
        {
            "Store.currentClient.messageQueue.addRpcMessage(StringUtils.hexStringToBytes(\"80bc4503110000020000001100110000000000000000\"));"
        };

        HardcodedCommandExample command = PacketResearcher.ParseLocalQueuedRpcCommandText(lines, "PlayerHandler.cs").Single();

        Assert.Equal("mxo-hd", command.Source);
        Assert.Equal("80 bc", command.RawHeader);
        Assert.Equal(0xbc, command.DecodedValue);
        Assert.Equal(20, command.PayloadLength);
        Assert.Equal("45 03", command.Field0);
        Assert.Equal("11 00", command.Field1);
        Assert.Equal("00 02", command.Field2);
        Assert.Equal("45 03 11 00 00 02 00 00 00 11 00 11 00 00 00 00 00 00 00 00", command.PayloadHex);
    }

    [Fact]
    public void ParseExternalProtocol03HardcodedText_ExtractsBareObjectMessageExamples()
    {
        string[] lines =
        {
            "m_parent.QueueCommand(make_shared<HexGenericMsg>(\"010001080102030405060708090a0b0c\"));"
        };

        Protocol03HardcodedExample example = PacketResearcher.ParseExternalProtocol03HardcodedText(lines, "PlayerObject.cpp", "tasteewheat").Single();

        Assert.Equal("tasteewheat", example.Source);
        Assert.Equal(16, example.PayloadLength);
        Protocol03ObjectViewSample sample = example.ObjectViews.Single();
        Assert.True(sample.Complete);
        Assert.Equal(1, sample.ViewId);
        Assert.Equal("08", sample.FirstSelector);
        Assert.Equal("single position update", sample.Classification);
    }

    [Fact]
    public void ParseLocalProtocol03HardcodedText_ExtractsTopLevelObjectMessageExamples()
    {
        string[] lines =
        {
            "Store.currentClient.messageQueue.addObjectMessage(StringUtils.hexStringToBytes(\"0203010001080102030405060708090a0b0c\"), false);"
        };

        Protocol03HardcodedExample example = PacketResearcher.ParseLocalProtocol03HardcodedText(lines, "ChatHelper.cs").Single();

        Assert.Equal("mxo-hd", example.Source);
        Assert.Equal(18, example.PayloadLength);
        Assert.Equal("02 03 01 00 01 08 01 02 03 04 05 06 07 08 09 0a 0b 0c", example.PrefixHex);
        Assert.True(example.ObjectViews.Single().Complete);
    }

    [Fact]
    public void DetectProtocol04Headers_DecodesKnownHeaderFromPacketDumpLine()
    {
        byte[] bytes = PacketResearcher.ParseHexBytes("02 04 01 00 48 01 08 80 c8 08 00 f0 34 03 00").ToArray();
        Dictionary<int, RpcHeaderEntry> localByValue = new()
        {
            [0xc8] = new RpcHeaderEntry("mxo-hd", "CR2", "client", "RPCRequestHeader", "CLIENT_OBJECTINTERACTION_STATIC", 0xc8, "80 c8", "local.cs", 1)
        };

        PacketDumpHeaderHit hit = PacketResearcher.DetectProtocol04Headers(bytes, localByValue).Single();

        Assert.Equal("80 c8", hit.RawHeader);
        Assert.Equal(0xc8, hit.DecodedValue);
        Assert.Equal("CR2:CLIENT_OBJECTINTERACTION_STATIC", hit.LocalName);
    }

    [Fact]
    public void SummarizePacketDump_FindsEmbeddedKnownServerHeaders()
    {
        string tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllLines(tempFile, new[]
            {
                "not a packet",
                "b5 00 21 00 00 00 01 00 00 00 00 16 80 bc 56 00 81 0d 00 c3 b2 00"
            });
            RpcHeaderEntry[] local =
            {
                new("mxo-hd", "CR2", "server", "RPCResponseHeaders", "SERVER_VENDOR_OPEN", 0x810d, "81 0d", "local.cs", 1)
            };

            PacketDumpFileSummary summary = PacketResearcher.SummarizePacketDump(tempFile, local);

            string label = "81 0d SERVER_VENDOR_OPEN [CR2 server]";
            Assert.Equal(1, summary.PacketLikeLines);
            Assert.Equal(1, summary.KnownHeaderCounts[label]);
            Assert.Equal(new[] { 2 }, summary.KnownHeaderSampleLines[label]);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void DetectManageBonusPayloads_GroupsTopLevelPayloadFields()
    {
        byte[] bytes = PacketResearcher.ParseHexBytes(
            "02 04 01 00 00 01 16 80 bc 56 00 81 0d 00 c3 b2 00 00 89 02 02 00 00 00 01 00 00 00 00")
            .ToArray();

        ManageBonusPayloadSample payload = PacketResearcher.DetectManageBonusPayloads(bytes, 12, "server").Single();

        Assert.Equal(12, payload.Line);
        Assert.Equal(20, payload.PayloadLength);
        Assert.Equal("56 00", payload.Field0);
        Assert.Equal(0x56, payload.Field0Value);
        Assert.Equal("81 0d", payload.Field1);
        Assert.Equal(0x0d81, payload.Field1Value);
        Assert.Equal("00 c3", payload.Field2);
        Assert.Equal(0xc300, payload.Field2Value);
        Assert.Equal("56 00 81 0d 00 c3 b2 00 00 89 02 02 00 00 00 01 00 00 00 00", payload.PayloadHex);
    }

    [Fact]
    public void DetectPlayerAttributePayloads_GroupsTopLevelPayloadFields()
    {
        byte[] bytes = PacketResearcher.ParseHexBytes(
            "02 04 01 00 00 01 08 80 b2 4e 00 08 00 08 02")
            .ToArray();

        PlayerAttributePayloadSample payload = PacketResearcher.DetectPlayerAttributePayloads(bytes, 9, "server").Single();

        Assert.Equal(9, payload.Line);
        Assert.Equal(6, payload.PayloadLength);
        Assert.Equal("4e 00", payload.Field0);
        Assert.Equal(0x4e, payload.Field0Value);
        Assert.Equal("08 00", payload.Field1);
        Assert.Equal(0x08, payload.Field1Value);
        Assert.Equal("08 02", payload.Field2);
        Assert.Equal(0x0208, payload.Field2Value);
        Assert.Equal("4e 00 08 00 08 02", payload.PayloadHex);
    }

    [Fact]
    public void DetectProtocol04InteractionPayloads_DecodesStaticObjectShape()
    {
        byte[] bytes = PacketResearcher.ParseHexBytes(
            "02 04 01 00 49 01 08 80 c8 f0 01 30 39 02 00")
            .ToArray();

        Protocol04InteractionPayloadSample payload = PacketResearcher.DetectProtocol04InteractionPayloads(bytes, 15, "client").Single();

        Assert.Equal(15, payload.Line);
        Assert.Equal("client", payload.Direction);
        Assert.Equal("80 c8", payload.RawHeader);
        Assert.Equal("static", payload.InteractionKind);
        Assert.Equal((uint)0x393001f0, payload.ObjectId);
        Assert.Equal(0x3930, payload.SectorId);
        Assert.Equal(2, payload.ObjectType);
        Assert.Equal("human NPC / vendor path", payload.ObjectTypeName);
        Assert.Equal("f0 01 30 39 02 00", payload.PayloadHex);
    }

    [Fact]
    public void DetectProtocol04InteractionPayloads_IncludesCr1StaticHeader()
    {
        byte[] bytes = PacketResearcher.ParseHexBytes(
            "02 04 01 00 49 01 08 80 c3 f0 01 30 39 03 00")
            .ToArray();

        Protocol04InteractionPayloadSample payload = PacketResearcher.DetectProtocol04InteractionPayloads(bytes, 16, "client").Single();

        Assert.Equal("80 c3", payload.RawHeader);
        Assert.Equal("static", payload.InteractionKind);
        Assert.Equal(3, payload.ObjectType);
        Assert.Equal("door", payload.ObjectTypeName);
    }

    [Fact]
    public void ParseProtocol04InteractionCommandText_DecodesExternalHandleCommand()
    {
        string[] lines =
        {
            "[01.06.2010 00:20:33] DEBUG:  HandleCommand: 80 c8 02 00 20 4e 03 00"
        };

        Protocol04InteractionCommandExample example =
            PacketResearcher.ParseProtocol04InteractionCommandText(lines, "Reality.log", "tasteewheat").Single();

        Assert.Equal("tasteewheat", example.Source);
        Assert.Equal("Reality.log", example.File);
        Assert.Equal(1, example.Line);
        Assert.Equal("80 c8", example.RawHeader);
        Assert.Equal("static", example.InteractionKind);
        Assert.Equal(6, example.PayloadLength);
        Assert.Equal((uint)0x4e200002, example.ObjectId);
        Assert.Equal(0x4e20, example.SectorId);
        Assert.Equal(3, example.ObjectType);
        Assert.Equal("door", example.ObjectTypeName);
        Assert.Equal("02 00 20 4e 03 00", example.PayloadHex);
    }

    [Fact]
    public void ParseVendorInventoryText_ExtractsStaticObjectItemRows()
    {
        string[] lines =
        {
            "metr_id;vendor_static_id;items;x;y;z",
            "1;1310720002;700,698,696,692;16995;495;195"
        };

        VendorInventoryEntry entry = PacketResearcher.ParseVendorInventoryText(lines, "vendor_items.csv").Single();

        Assert.Equal((ushort)1, entry.MetrId);
        Assert.Equal((uint)1310720002, entry.VendorStaticId);
        Assert.Equal(new uint[] { 700, 698, 696, 692 }, entry.Items);
        Assert.Equal("vendor_items.csv", entry.File);
        Assert.Equal(2, entry.Line);
    }

    [Fact]
    public void DetectProtocol04PacketSequences_PreservesServerHeaderOrder()
    {
        byte[] bytes = PacketResearcher.ParseHexBytes(
            "02 04 01 00 00 02 08 80 b2 4e 00 08 00 08 02 16 80 bc 45 03 11 00 00 02 00 00 00 11 00 32 00 00 00 01 00 00 00 00")
            .ToArray();

        Protocol04PacketSequenceSample sequence = PacketResearcher.DetectProtocol04PacketSequences(bytes, 25, "server").Single();

        Assert.Equal(25, sequence.Line);
        Assert.Equal("server", sequence.Direction);
        Assert.Equal(new[] { "80 b2", "80 bc" }, sequence.Headers);
    }

    [Fact]
    public void ReportWriter_LinksB2Field0ToBcField1InSamePacket()
    {
        PacketDumpFileSummary dump = new(
            "sample.txt",
            1,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            new[]
            {
                new Protocol04PacketSequenceSample(10, "server", new[] { "80 b2", "80 bc" }),
                new Protocol04PacketSequenceSample(11, "server", new[] { "80 b2", "80 bc" })
            },
            Array.Empty<Protocol03ObjectViewSample>(),
            Array.Empty<Protocol04InteractionPayloadSample>(),
            new[]
            {
                new PlayerAttributePayloadSample(10, 6, "3d 04", 0x043d, "00 00", 0, "08 02", 0x0208, "3d 04 00 00 08 02"),
                new PlayerAttributePayloadSample(11, 6, "35 04", 0x0435, "00 00", 0, "08 02", 0x0208, "35 04 00 00 08 02")
            },
            new[]
            {
                new ManageBonusPayloadSample(10, 20, "45 03", 0x0345, "3d 04", 0x043d, "00 43", 0x4300, "45 03 3d 04 00 43 00 00 00 3d 04 00 00 00 00 01 00 00 00 00"),
                new ManageBonusPayloadSample(11, 20, "55 00", 0x0055, "35 04", 0x0435, "00 62", 0x6200, "55 00 35 04 00 62 00 00 00 15 04 01 00 00 00 00 00 00 00 00")
            });
        PacketResearchReport report = new(
            ".",
            Array.Empty<string>(),
            null,
            Array.Empty<string>(),
            Array.Empty<RpcHeaderEntry>(),
            Array.Empty<AttributeDefinition>(),
            Array.Empty<FxDefinition>(),
            Array.Empty<RajkoRpcEntry>(),
            new[]
            {
                new HardcodedCommandExample("mxo-hd", "80 bc", 0xbc, 20, "45 03", 0x0345, "3d 04", 0x043d, "00 43", 0x4300, "45 03 3d 04 00 43 00 00 00 3d 04 00 00 00 00 01 00 00 00 00", "PlayerHandler.cs", 122)
            },
            Array.Empty<Protocol03HardcodedExample>(),
            Array.Empty<Protocol04InteractionCommandExample>(),
            Array.Empty<VendorInventoryEntry>(),
            Array.Empty<RpcComparison>(),
            new[] { dump });

        string markdown = ReportWriter.ToMarkdown(report);

        Assert.Contains("### Cross-Family State Field Links", markdown);
        Assert.Contains("`3d 04` (1085)", markdown);
        Assert.Contains("`sample.txt:10`", markdown);
        Assert.Contains("### `80 bc` Field Layout by Leading Field", markdown);
        Assert.Contains("| `45 03` | 1 | 1 | 0 | `3d 04` / `00 00` / `01 00` | 1 | `sample.txt:10` |", markdown);
        Assert.Contains("| `55 00` | 1 | 0 | 0 | `15 04` / `01 00` / `00 00` | 1 | `sample.txt:11` |", markdown);
        Assert.Contains("### Hardcoded `80 bc` Family Coverage", markdown);
        Assert.Contains("| `45 03` | 1 | 1 | 1 | 1 | 1 | `3d 04` / `00 00` / `01 00` (1) | mxo-hd PlayerHandler.cs:122 |", markdown);
        Assert.Contains("### Linked Short/Long Field-Value Consistency", markdown);
        Assert.Contains("- Same-packet field links: 2", markdown);
        Assert.Contains("- Links with repeated `80 bc` byte 9 field: 1", markdown);
        Assert.Contains("- Links with matching `80 bc` byte 11 value: 1", markdown);
        Assert.Contains("| `35 04` | `00 00` | `15 04` | `01 00` | 1 | 1 | `sample.txt:11` |", markdown);
    }

    [Fact]
    public void ReportWriter_IncludesProtocol04InteractionPayloadSummary()
    {
        PacketDumpFileSummary dump = new(
            "doors.txt",
            1,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            Array.Empty<Protocol04PacketSequenceSample>(),
            Array.Empty<Protocol03ObjectViewSample>(),
            new[]
            {
                new Protocol04InteractionPayloadSample(4, "client", "80 c8", "static", 6, 888143880, 13552, 3, "door", "08 00 f0 34 03 00")
            },
            Array.Empty<PlayerAttributePayloadSample>(),
            Array.Empty<ManageBonusPayloadSample>());
        PacketResearchReport report = new(
            ".",
            Array.Empty<string>(),
            null,
            Array.Empty<string>(),
            Array.Empty<RpcHeaderEntry>(),
            Array.Empty<AttributeDefinition>(),
            Array.Empty<FxDefinition>(),
            Array.Empty<RajkoRpcEntry>(),
            Array.Empty<HardcodedCommandExample>(),
            Array.Empty<Protocol03HardcodedExample>(),
            new[]
            {
                new Protocol04InteractionCommandExample("tasteewheat", "Reality.log", 37, "80 c8", "static", 6, 1310720002, 20000, 3, "door", "02 00 20 4e 03 00")
            },
            new[]
            {
                new VendorInventoryEntry(1, 1310720002, new uint[] { 700, 698, 696, 692 }, "data/vendor_items.csv", 10),
                new VendorInventoryEntry(1, 888143880, new uint[] { 7873, 2497 }, "data/vendor_items.csv", 11)
            },
            Array.Empty<RpcComparison>(),
            new[] { dump });

        string markdown = ReportWriter.ToMarkdown(report);

        Assert.Contains("- Protocol 04 interaction payloads: 1", markdown);
        Assert.Contains("- External object-interaction command examples: 1", markdown);
        Assert.Contains("## Protocol 04 Object Interaction Payloads", markdown);
        Assert.Contains("| `80 c8` | static | 3 | door | 1 | 1 | `doors.txt:4` | `08 00 f0 34 03 00` |", markdown);
        Assert.Contains("| 888143880 | 13552 | static | 3 | door | 1 | `doors.txt:4` |", markdown);
        Assert.Contains("## External Object Interaction Command Examples", markdown);
        Assert.Contains("| `80 c8` | static | 3 | door | 1 | 1 | `tasteewheat Reality.log:37` | `02 00 20 4e 03 00` |", markdown);
        Assert.Contains("## Vendor Inventory Object Correlations", markdown);
        Assert.Contains("| 888143880 | 1 | 1 | 1 | 0 | 2 | `7873, 2497` | `data/vendor_items.csv:11` |", markdown);
        Assert.Contains("| 1310720002 | 1 | 1 | 0 | 1 | 4 | `700, 698, 696, 692` | `data/vendor_items.csv:10` |", markdown);
    }

    [Fact]
    public void DetectProtocol03ObjectViews_RecognizesEmoteSignature()
    {
        byte[] bytes = PacketResearcher.ParseHexBytes(
            "02 03 02 00 01 28 02 40 11 00 00 10 5b 62 84 c7 00 00 be 42 3a ed 7e 46 ed 63 15 04 00 00")
            .ToArray();

        Protocol03ObjectViewSample sample = PacketResearcher.DetectProtocol03ObjectViews(bytes, 6, "server").Single();

        Assert.Equal(6, sample.Line);
        Assert.Equal("server", sample.Direction);
        Assert.Equal("02", sample.TransportHeader);
        Assert.Equal(2, sample.ViewId);
        Assert.Equal(1, sample.UpdateCount);
        Assert.Equal("28", sample.FirstSelector);
        Assert.Equal("emote update", sample.Classification);
    }

    [Fact]
    public void DetectProtocol03ObjectViews_RecognizesTimedMovementSignature()
    {
        byte[] bytes = PacketResearcher.ParseHexBytes(
            "82 f3 bb f6 48 03 09 00 02 08 46 32 65 c7 00 80 fc c3 13 3d 6e c7 00 00")
            .ToArray();

        Protocol03ObjectViewSample sample = PacketResearcher.DetectProtocol03ObjectViews(bytes, 59, null).Single();

        Assert.Equal("82 + simtime", sample.TransportHeader);
        Assert.Equal(9, sample.ViewId);
        Assert.Equal(2, sample.UpdateCount);
        Assert.Equal("08", sample.FirstSelector);
        Assert.Equal("position/object state update", sample.Classification);
    }

    [Fact]
    public void DetectProtocol03ObjectViews_SegmentsKnownSelectorSizes()
    {
        byte[] bytes = PacketResearcher.ParseHexBytes(
            "02 03 0b 00 02 0c c4 f2 18 95 47 00 60 8b c4 e6 59 e3 44 0a 01 02 03 04 05 06 07 08 09 0a 0b 0c 0d")
            .ToArray();

        Protocol03ObjectViewSample sample = PacketResearcher.DetectProtocol03ObjectViews(bytes, 42, "server").Single();

        Assert.True(sample.Complete);
        Assert.Equal(2, sample.ParsedUpdateCount);
        Assert.Equal(0, sample.UnparsedBytes);
        Assert.Equal(new[] { "0c", "0a" }, sample.Segments.Select(segment => segment.Selector));
        Assert.Equal(new[] { 13, 13 }, sample.Segments.Select(segment => segment.PayloadBytes));
        Assert.All(sample.Segments, segment => Assert.True(segment.IsKnownLength));
    }

    [Fact]
    public void DetectProtocol03ObjectViews_TreatsSelector00AsOneByteStateMarker()
    {
        byte[] bytes = PacketResearcher.ParseHexBytes(
            "02 03 0f 00 02 0e 04 8c 0c 6c 96 47 00 60 8b c4 9e bb c7 44 00 00")
            .ToArray();

        Protocol03ObjectViewSample sample = PacketResearcher.DetectProtocol03ObjectViews(bytes, 107, "server").Single();

        Assert.True(sample.Complete);
        Assert.Equal(2, sample.ParsedUpdateCount);
        Assert.Equal(new[] { "0e", "00" }, sample.Segments.Select(segment => segment.Selector));
        Assert.Equal(new[] { 14, 1 }, sample.Segments.Select(segment => segment.PayloadBytes));
        Assert.Equal("one-byte state marker", sample.Segments[1].Classification);
    }

    [Fact]
    public void DetectProtocol03ObjectViews_SegmentsSecondaryMovementWrapper()
    {
        byte[] bytes = PacketResearcher.ParseHexBytes(
            "02 03 0f 00 02 0c 96 0c 6c 96 47 00 60 8b c4 9e bb c7 44 0b 00 02 0c 26 56 43 95 47 00 60 8b c4 65 7c ec 44 00 00")
            .ToArray();

        Protocol03ObjectViewSample sample = PacketResearcher.DetectProtocol03ObjectViews(bytes, 111, "server").Single();

        Assert.True(sample.Complete);
        Assert.Equal(2, sample.ParsedUpdateCount);
        Assert.Equal(new[] { "0c", "0b" }, sample.Segments.Select(segment => segment.Selector));
        Assert.Equal(new[] { 13, 18 }, sample.Segments.Select(segment => segment.PayloadBytes));
        Assert.Equal("secondary movement wrapper", sample.Segments[1].Classification);
    }

    [Fact]
    public void DetectProtocol03ObjectViews_ContinuesAcrossCompleteObjectMessages()
    {
        byte[] bytes = PacketResearcher.ParseHexBytes(
            "02 03 01 00 01 08 01 02 03 04 05 06 07 08 09 0a 0b 0c 02 00 01 01 10 11 12 13")
            .ToArray();

        Protocol03ObjectViewSample[] samples = PacketResearcher.DetectProtocol03ObjectViews(bytes, 7, "server").ToArray();

        Assert.Equal(2, samples.Length);
        Assert.All(samples, sample => Assert.True(sample.Complete));
        Assert.Equal(1, samples[0].ViewId);
        Assert.Equal("08", samples[0].FirstSelector);
        Assert.Equal(2, samples[1].ViewId);
        Assert.Equal("01", samples[1].FirstSelector);
    }

    [Fact]
    public void DetectProtocol03ObjectViews_LeavesVariableSelectorsPartial()
    {
        byte[] bytes = PacketResearcher.ParseHexBytes(
            "02 03 01 00 0c 0c 00 dd cd ab 23 9b 81 ff 71 02 00 00 41 6e 64 65 72 73 6f 6e")
            .ToArray();

        Protocol03ObjectViewSample sample = PacketResearcher.DetectProtocol03ObjectViews(bytes, 11, "server").Single();

        Assert.False(sample.Complete);
        Assert.Equal(0, sample.ParsedUpdateCount);
        Assert.Equal(20, sample.UnparsedBytes);
        Assert.Equal("spawn/profile variable block", sample.Segments.Single().Classification);
    }

    [Fact]
    public void ParseAttributeDefinitionText_LoadsCreationAndUpdateIndices()
    {
        string[] lines =
        {
            "public class AttributeClass3829 :GameObject",
            "{",
            "    public Attribute BuyActiveTracker = new Attribute(1, \"BuyActiveTracker\");",
            "    public Attribute TalkActiveTracker = new Attribute(1, \"TalkActiveTracker\");",
            "    public AttributeClass3829(string name, UInt16 _goid) : base(67, 48, name, _goid, 0xFFFFFFFF)",
            "    {",
            "        AddAttribute(ref BuyActiveTracker, 60, 47);",
            "        AddAttribute(ref TalkActiveTracker, 64, 4);",
            "    }",
            "}"
        };

        List<AttributeDefinition> definitions = PacketResearcher.ParseAttributeDefinitionText(lines, "AttributeClass3829.cs", "gameobjects.gob").ToList();

        AttributeDefinition buy = definitions.Single(definition => definition.AttributeName == "BuyActiveTracker");
        Assert.Equal("AttributeClass3829", buy.ClassName);
        Assert.Equal(3829, buy.ClassId);
        Assert.Equal(1, buy.Size);
        Assert.Equal(60, buy.CreationIndex);
        Assert.Equal(47, buy.UpdateIndex);
    }

    [Fact]
    public void ParseFxDefinitionText_LoadsEndianIdsAndNames()
    {
        string[] lines =
        {
            "LITTLE      BIG             SYMBOLICNAME",
            "d2000028 280000d2 = FX_CHARACTER_EXIT_THE_MATRIX"
        };

        FxDefinition fx = PacketResearcher.ParseFxDefinitionText(lines, "fxlisthex.txt").Single();

        Assert.Equal("d2000028", fx.LittleEndianHex);
        Assert.Equal("280000d2", fx.BigEndianHex);
        Assert.Equal("FX_CHARACTER_EXIT_THE_MATRIX", fx.Name);
        Assert.Equal(2, fx.Line);
    }
}
