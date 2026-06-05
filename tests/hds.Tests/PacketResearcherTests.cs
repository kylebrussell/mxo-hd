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
    public void SummarizePacketDump_ParsesCompactHexPacketLines()
    {
        string tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllLines(tempFile, new[]
            {
                "Door 1 answer:",
                "0203010008A0010800F03480CDAB038400000000F30435BF00000000F304353F410000000080A6F0C000000000002062400000000000B3D040340800000400000000"
            });

            PacketDumpFileSummary summary = PacketResearcher.SummarizePacketDump(tempFile, Array.Empty<RpcHeaderEntry>());

            Assert.Equal(1, summary.PacketLikeLines);
            Protocol03ObjectViewSample sample = Assert.Single(summary.Protocol03ObjectViews);
            Protocol03StaticObjectLead lead = Assert.Single(sample.StaticObjectLeads);
            Assert.Equal("a0", lead.Selector);
            Assert.Equal(60, lead.PayloadBytes);
            Assert.Equal((uint)888143880, lead.ObjectId);
            Assert.Equal(34, lead.PostQuaternionBytes);
            Assert.Equal("41", lead.PostQuaternionMarkerHex);
            Assert.Equal(-68200.0, lead.PostQuaternionX);
            Assert.Equal(145.0, lead.PostQuaternionY);
            Assert.Equal(17100.0, lead.PostQuaternionZ);
            Assert.Equal("34 08 00 00 04 00 00 00 00", lead.PostQuaternionTailHex);
            Assert.Equal("34 08 00 00", lead.PostQuaternionTailField0Hex);
            Assert.Equal((uint)2100, lead.PostQuaternionTailField0);
            Assert.Equal("04 00 00 00", lead.PostQuaternionTailField1Hex);
            Assert.Equal((uint)4, lead.PostQuaternionTailField1);
            Assert.Equal("00", lead.PostQuaternionTailSuffixHex);
            Assert.Equal("41 00 00 00 00 80 a6 f0 c0 00 00 00 00 00 20 62 40 00 00 00 00 00 b3 d0 40 34 08 00 00 04 00 00 00 00", lead.PostQuaternionHex);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void SummarizePacketDump_ParsesLabeledCompactHexPacketLines()
    {
        string tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllLines(tempFile, new[]
            {
                "Answer 1:82773AD648 030100089E010D00F034EBCD AB038400000000F204353F00000000F40435BF41000000008074F0C00000000000206240000000000096C940340800001200000000"
            });

            PacketDumpFileSummary summary = PacketResearcher.SummarizePacketDump(tempFile, Array.Empty<RpcHeaderEntry>());

            Assert.Equal(1, summary.PacketLikeLines);
            Protocol03ObjectViewSample sample = Assert.Single(summary.Protocol03ObjectViews);
            Protocol03StaticObjectLead lead = Assert.Single(sample.StaticObjectLeads);
            Assert.Equal("82 + simtime", sample.TransportHeader);
            Assert.Equal("9e", lead.Selector);
            Assert.Equal((uint)888143885, lead.ObjectId);
            Assert.Equal("eb", lead.InstanceByteHex);
            Assert.Equal(60, lead.PayloadBytes);
            Assert.Equal(-67400.0, lead.PostQuaternionX);
            Assert.Equal(145.0, lead.PostQuaternionY);
            Assert.Equal(13100.0, lead.PostQuaternionZ);
            Assert.Equal("34 08 00 00 12 00 00 00 00", lead.PostQuaternionTailHex);
            Assert.Equal((uint)2100, lead.PostQuaternionTailField0);
            Assert.Equal((uint)18, lead.PostQuaternionTailField1);
            Assert.Equal("00", lead.PostQuaternionTailSuffixHex);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void SummarizePacketDump_StitchesLooseMultilinePacketBlocks()
    {
        string tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllLines(tempFile, new[]
            {
                "Answer 1:",
                "02 03 01 00 08 a0 01 08 00 f0 34 e5 cd ab 03 84 00 00 00 00 f3 04 35 bf 00 00 00 00 f3 04 35 3f",
                "41 00 00 00 00 80 a6 f0 c0 00 00 00 00 00 20 62 40 00 00 00 00 00 b3 d0 40 34 08 00 00 12 00 00",
                "00 00",
                "02 04 01 00 48 01 08 80 c8 08 00 f0 34 03 00"
            });

            PacketDumpFileSummary summary = PacketResearcher.SummarizePacketDump(tempFile, Array.Empty<RpcHeaderEntry>());

            Assert.Equal(3, summary.PacketLikeLines);
            Protocol03ObjectViewSample sample = Assert.Single(summary.Protocol03ObjectViews);
            Protocol03StaticObjectLead lead = Assert.Single(sample.StaticObjectLeads);
            Assert.Equal(2, sample.Line);
            Assert.Equal("a0", lead.Selector);
            Assert.Equal(60, lead.PayloadBytes);
            Assert.Equal("e5", lead.InstanceByteHex);
            Assert.Equal(34, lead.PostQuaternionBytes);
            Assert.Equal("34 08 00 00 12 00 00 00 00", lead.PostQuaternionTailHex);
            Assert.Equal((uint)18, lead.PostQuaternionTailField1);
            Assert.Single(summary.Protocol04InteractionPayloads);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void SummarizePacketDump_ParsesDirect82Protocol03Transport()
    {
        string tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllLines(tempFile, new[]
            {
                "82 03 01 00 08 a0 01 08 00 f0 34 1b cd ab 03 84 00 00 00 00 f3 04 35 bf 00 00 00 00 f3 04 35 3f",
                "41 00 00 00 00 80 a6 f0 c0 00 00 00 00 00 20 62 40 00 00 00 00 00 b3 d0 40 34 08 00 00 03 00 00",
                "00 00"
            });

            PacketDumpFileSummary summary = PacketResearcher.SummarizePacketDump(tempFile, Array.Empty<RpcHeaderEntry>());

            Protocol03ObjectViewSample sample = Assert.Single(summary.Protocol03ObjectViews);
            Protocol03StaticObjectLead lead = Assert.Single(sample.StaticObjectLeads);
            Assert.Equal("82", sample.TransportHeader);
            Assert.Equal("1b", lead.InstanceByteHex);
            Assert.Equal("34 08 00 00 03 00 00 00 00", lead.PostQuaternionTailHex);
            Assert.Equal((uint)3, lead.PostQuaternionTailField1);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void SummarizePacketDump_ParsesSelector3cStaticDoorPacketBlocks()
    {
        string tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllLines(tempFile, new[]
            {
                "Answer 1:",
                "02 03 01 00 08 3c 1b 06 00 f0 34 04 cd ab 03 84 00 00 00 00 00 00 80 3f 00 00 00 00 00 00 00 00",
                "41 00 00 00 00 40 a0 f0 c0 00 00 00 00 00 20 62 40 00 00 00 00 00 f2 d2 40 34 08 00 00 0e 00 00",
                "00 00"
            });

            PacketDumpFileSummary summary = PacketResearcher.SummarizePacketDump(tempFile, Array.Empty<RpcHeaderEntry>());

            Protocol03ObjectViewSample sample = Assert.Single(summary.Protocol03ObjectViews);
            Assert.Equal("static object/door spawn candidate", sample.Classification);
            Protocol03StaticObjectLead lead = Assert.Single(sample.StaticObjectLeads);
            Assert.Equal("3c", lead.Selector);
            Assert.Equal((uint)888143878, lead.ObjectId);
            Assert.Equal("04", lead.InstanceByteHex);
            Assert.Equal(60, lead.PayloadBytes);
            Assert.Equal(-68100.0, lead.PostQuaternionX);
            Assert.Equal(145.0, lead.PostQuaternionY);
            Assert.Equal(19400.0, lead.PostQuaternionZ);
            Assert.Equal((uint)2100, lead.PostQuaternionTailField0);
            Assert.Equal((uint)14, lead.PostQuaternionTailField1);
            Assert.Equal("00", lead.PostQuaternionTailSuffixHex);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void SummarizePacketDump_ParsesSelector9fLabeledStaticDoorPacket()
    {
        string tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllLines(tempFile, new[]
            {
                "Answer 1:829782D648 030100089F010E00F034F9CD AB038400000000F204353F00000000F40435BF41000000008074F0C0000000000020624000000000005ECA40340800001000000000"
            });

            PacketDumpFileSummary summary = PacketResearcher.SummarizePacketDump(tempFile, Array.Empty<RpcHeaderEntry>());

            Protocol03ObjectViewSample sample = Assert.Single(summary.Protocol03ObjectViews);
            Protocol03StaticObjectLead lead = Assert.Single(sample.StaticObjectLeads);
            Assert.Equal("82 + simtime", sample.TransportHeader);
            Assert.Equal("9f", lead.Selector);
            Assert.Equal((uint)888143886, lead.ObjectId);
            Assert.Equal("f9", lead.InstanceByteHex);
            Assert.Equal(60, lead.PayloadBytes);
            Assert.Equal(-67400.0, lead.PostQuaternionX);
            Assert.Equal(145.0, lead.PostQuaternionY);
            Assert.Equal(13500.0, lead.PostQuaternionZ);
            Assert.Equal((uint)2100, lead.PostQuaternionTailField0);
            Assert.Equal((uint)16, lead.PostQuaternionTailField1);
            Assert.Equal("00", lead.PostQuaternionTailSuffixHex);
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
    public void DetectCoderAttributePayloads_GroupsTopLevelPayloadFields()
    {
        byte[] bytes = PacketResearcher.ParseHexBytes(
            "02 04 01 00 00 01 08 80 bd 21 00 01 00 00 00")
            .ToArray();

        CoderAttributePayloadSample payload = PacketResearcher.DetectCoderAttributePayloads(bytes, 14, "server").Single();

        Assert.Equal(14, payload.Line);
        Assert.Equal(6, payload.PayloadLength);
        Assert.Equal("21 00", payload.Field0);
        Assert.Equal(0x21, payload.Field0Value);
        Assert.Equal("01 00", payload.Field1);
        Assert.Equal(0x01, payload.Field1Value);
        Assert.Equal("00 00", payload.Field2);
        Assert.Equal(0, payload.Field2Value);
        Assert.Equal("21 00 01 00 00 00", payload.PayloadHex);
    }

    [Fact]
    public void DetectAbilityUnloadPayloads_GroupsTopLevelPayloadFields()
    {
        byte[] bytes = PacketResearcher.ParseHexBytes(
            "02 04 01 00 00 01 04 80 b3 11 00")
            .ToArray();

        AbilityUnloadPayloadSample payload = PacketResearcher.DetectAbilityUnloadPayloads(bytes, 15, "server").Single();

        Assert.Equal(15, payload.Line);
        Assert.Equal(2, payload.PayloadLength);
        Assert.Equal("11 00", payload.Field0);
        Assert.Equal(0x11, payload.Field0Value);
        Assert.Equal("11 00", payload.PayloadHex);
    }

    [Fact]
    public void DetectFriendListStatusPayloads_GroupsTopLevelPayloadFields()
    {
        byte[] bytes = PacketResearcher.ParseHexBytes(
            "02 04 01 00 00 01 0f 80 d7 08 00 3c 00 00 8e 05 00 41 6c 69 63 65")
            .ToArray();

        FriendListStatusPayloadSample payload = PacketResearcher.DetectFriendListStatusPayloads(bytes, 16, "server").Single();

        Assert.Equal(16, payload.Line);
        Assert.Equal(13, payload.PayloadLength);
        Assert.Equal("08 00", payload.Field0);
        Assert.Equal(8, payload.Field0Value);
        Assert.Equal("3c 00", payload.Field1);
        Assert.Equal(0x3c, payload.Field1Value);
        Assert.Equal("00 8e", payload.Field2);
        Assert.Equal(0x8e00, payload.Field2Value);
        Assert.Equal(5, payload.TextBytes);
        Assert.Equal("Alice", payload.Text);
        Assert.Equal("08 00 3c 00 00 8e 05 00 41 6c 69 63 65", payload.PayloadHex);
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
    public void ParseItemCommandText_ExtractsSymbolsAndDisplayNames()
    {
        string[] lines =
        {
            "&gib 700 Revolver2 \"Royer Snub Nose\"",
            "&gib 46 Code Proxy \"Code\"",
            "&gib 671 Fclothing_Glasses_A3_C1 \"Black Brezza Sunglasses \"",
            "&gib 593 HCU_Base "
        };

        ItemCommandEntry[] entries = PacketResearcher.ParseItemCommandText(lines, "invitems_command.txt").ToArray();

        Assert.Equal(4, entries.Length);
        Assert.Equal((uint)700, entries[0].ItemId);
        Assert.Equal("Revolver2", entries[0].Symbol);
        Assert.Equal("Royer Snub Nose", entries[0].DisplayName);
        Assert.Equal("invitems_command.txt", entries[0].File);
        Assert.Equal(1, entries[0].Line);
        Assert.Equal((uint)46, entries[1].ItemId);
        Assert.Equal("Code Proxy", entries[1].Symbol);
        Assert.Equal("Code", entries[1].DisplayName);
        Assert.Equal((uint)671, entries[2].ItemId);
        Assert.Equal("Fclothing_Glasses_A3_C1", entries[2].Symbol);
        Assert.Equal("Black Brezza Sunglasses", entries[2].DisplayName);
        Assert.Equal((uint)593, entries[3].ItemId);
        Assert.Equal("HCU_Base", entries[3].Symbol);
        Assert.Null(entries[3].DisplayName);
    }

    [Fact]
    public void ParseStaticObjectText_ExtractsLittleEndianObjectAndTypeIds()
    {
        string[] lines =
        {
            "metr_id,sector_id,mxoId,staticId,type,exterior,pos_x,pos_y,pos_z,rotation,quat",
            "1,847,0800f034,08000000,a0010000,False,-69100.0,145.0,16200.0,-1.0,0000000000000000000000000000803f",
            "1,1250,0200204e,02000000,44020000,False,19100.0,545.0,7600.0,0.0,0000000000000000000000000000803f"
        };

        StaticObjectEntry[] entries = PacketResearcher
            .ParseStaticObjectText(lines, "data/staticObjects_slums_2.csv", new HashSet<uint> { 888143880 })
            .ToArray();

        StaticObjectEntry entry = Assert.Single(entries);
        Assert.Equal((ushort)1, entry.MetrId);
        Assert.Equal((ushort)847, entry.SectorId);
        Assert.Equal((uint)888143880, entry.MxoId);
        Assert.Equal("0800f034", entry.MxoIdHex);
        Assert.Equal((uint)8, entry.StaticId);
        Assert.Equal("08000000", entry.StaticIdHex);
        Assert.Equal((ushort)416, entry.TypeId);
        Assert.Equal("a0010000", entry.TypeHex);
        Assert.False(entry.Exterior);
        Assert.Equal(-69100.0, entry.X);
        Assert.Equal(145.0, entry.Y);
        Assert.Equal(16200.0, entry.Z);
        Assert.Equal(-1.0, entry.Rotation);
        Assert.Equal("data/staticObjects_slums_2.csv", entry.File);
        Assert.Equal(2, entry.Line);
    }

    [Fact]
    public void ParseGameObjectText_ExtractsSignedObjectIds()
    {
        string[] lines =
        {
            "Mclothing_Pants_A8_C1,1085,",
            "ReviveRSIAbility,-2147099648,"
        };

        GameObjectEntry[] entries = PacketResearcher.ParseGameObjectText(lines, "gameobjects.csv").ToArray();

        Assert.Equal(2, entries.Length);
        Assert.Equal("Mclothing_Pants_A8_C1", entries[0].CodeName);
        Assert.Equal(1085, entries[0].GoId);
        Assert.Equal("gameobjects.csv", entries[0].File);
        Assert.Equal(1, entries[0].Line);
        Assert.Equal("ReviveRSIAbility", entries[1].CodeName);
        Assert.Equal(-2147099648, entries[1].GoId);
    }

    [Fact]
    public void ParseMobEntityText_ExtractsNamesLevelsAndRsi()
    {
        string[] lines =
        {
            "3;it;Gold Blood Champion; 13;750;750;d7090058;37645;-1105;-14501;;false;false;34;00100000;611c00c6;;1;4;07080000;693;3;"
        };

        WorldEntityEntry entry = PacketResearcher.ParseMobEntityText(lines, "data/mob_parsed_untouched.csv").Single();

        Assert.Equal("mob_parsed_untouched.csv", entry.Source);
        Assert.Equal("Gold Blood Champion", entry.Name);
        Assert.Equal("it", entry.Zone);
        Assert.Equal(13, entry.Level);
        Assert.Equal("mob spawn", entry.Kind);
        Assert.Equal("d7090058", entry.Rsi);
        Assert.Equal("data/mob_parsed_untouched.csv", entry.File);
        Assert.Equal(1, entry.Line);
        Assert.Equal(750, entry.Health);
        Assert.Equal(750, entry.MaxHealth);
        Assert.Equal(37645, entry.X);
        Assert.Equal(-1105, entry.Y);
        Assert.Equal(-14501, entry.Z);
        Assert.Equal(1, entry.NpcRank);
        Assert.Equal(4, entry.DebuffState);
        Assert.Equal("07080000", entry.CurrentStateHex);
    }

    [Fact]
    public void ParseNpcCollectionText_ExtractsNpcHandles()
    {
        string[] lines =
        {
            "<?xml version=\"1.0\"?>",
            "<config>",
            "  <data NPC_NUMBER=\"1\">",
            "    <npc1 type=\"MISSION_CONTACT\" DISTRICT=\"1\" handle=\"Yttri\" RSI=\"250D0098\" />",
            "  </data>",
            "</config>"
        };

        WorldEntityEntry entry = PacketResearcher.ParseNpcCollectionText(lines, "data/NPC_COLLECTION.xml").Single();

        Assert.Equal("NPC_COLLECTION.xml", entry.Source);
        Assert.Equal("Yttri", entry.Name);
        Assert.Equal("1", entry.Zone);
        Assert.Null(entry.Level);
        Assert.Equal("MISSION_CONTACT", entry.Kind);
        Assert.Equal("250D0098", entry.Rsi);
        Assert.Equal(4, entry.Line);
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
    public void DetectProtocol04ServerPayloadShapes_GroupsAdditionalServerPayloads()
    {
        byte[] bytes = PacketResearcher.ParseHexBytes(
            "02 04 01 00 00 01 0a 81 68 17 00 27 00 41 42 43 44")
            .ToArray();
        RpcHeaderEntry[] localHeaders =
        {
            new("mxo-hd", "CR2", "server", "RPCResponseHeader", "SERVER_TEST", 0x0168, "81 68", "local.cs", 1)
        };

        Protocol04ServerPayloadShapeSample payload = PacketResearcher
            .DetectProtocol04ServerPayloadShapes(bytes, localHeaders, 26, "server")
            .Single();

        Assert.Equal(26, payload.Line);
        Assert.Equal("81 68", payload.Header);
        Assert.Equal("CR2:SERVER_TEST", payload.LocalName);
        Assert.Equal(8, payload.PayloadLength);
        Assert.Equal("17 00", payload.Field0);
        Assert.Equal(0x17, payload.Field0Value);
        Assert.Equal("27 00", payload.Field1);
        Assert.Equal(0x27, payload.Field1Value);
        Assert.Equal("41 42", payload.Field2);
        Assert.Equal(0x4241, payload.Field2Value);
        Assert.Equal("17 00 27 00 41 42 43 44", payload.PayloadPrefixHex);
        Assert.Equal("-", payload.PayloadSuffixHex);
        Assert.Equal(new[] { "ABCD" }, payload.Texts);
    }

    [Fact]
    public void DetectUnknown8167Payloads_DecodesDuplicatedTextAndTail()
    {
        byte[] bytes = PacketResearcher.ParseHexBytes(
            "02 04 01 00 00 01 41 81 67 17 00 27 00 1c 22 00 c6 01 11 00 00 00 00 00 00 00 37 00 00 08 0e 00 41 66 74 65 72 57 68 6f 72 75 4e 65 6f 00 0e 00 41 66 74 65 72 57 68 6f 72 75 4e 65 6f 00 02 00 4b 00 00 00 00 00 00 00")
            .ToArray();

        Unknown8167PayloadSample payload = PacketResearcher.DetectUnknown8167Payloads(bytes, 31, "server").Single();

        Assert.Equal(31, payload.Line);
        Assert.Equal(63, payload.PayloadLength);
        Assert.Equal("17 00", payload.Field0);
        Assert.Equal(0x17, payload.Field0Value);
        Assert.Equal("27 00", payload.Field1);
        Assert.Equal(0x27, payload.Field1Value);
        Assert.Equal("1c 22", payload.Field2);
        Assert.Equal(0x221c, payload.Field2Value);
        Assert.Equal("17 00 27 00 1c 22 00 c6 01 11 00 00 00 00 00 00 00 37 00 00 08", payload.PrefixHex);
        Assert.Equal(14, payload.Text0Bytes);
        Assert.Equal("AfterWhoruNeo", payload.Text0);
        Assert.Equal(14, payload.Text1Bytes);
        Assert.Equal("AfterWhoruNeo", payload.Text1);
        Assert.Equal("02 00", payload.TailField0);
        Assert.Equal(2, payload.TailField0Value);
        Assert.Equal("4b 00", payload.TailField1);
        Assert.Equal(0x4b, payload.TailField1Value);
    }

    [Fact]
    public void DetectUnknown47Payloads_DecodesStableFieldsAndTail()
    {
        byte[] bytes = BuildSingleRpcPacket("47", PacketResearcher.ParseHexBytes(
            "00 00 04 01 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 1a 00 06 00 00 00 01 00 00 00 00 01 01 00 00 00 00 00 80 00 00 00 00 00 00 00 00").ToArray());

        Unknown47PayloadSample payload = PacketResearcher.DetectUnknown47Payloads(bytes, 32, "server").Single();

        Assert.Equal(32, payload.Line);
        Assert.Equal(46, payload.PayloadLength);
        Assert.Equal("00 00", payload.Field0);
        Assert.Equal(0, payload.Field0Value);
        Assert.Equal("04 01", payload.Field1);
        Assert.Equal(0x0104, payload.Field1Value);
        Assert.Equal("00 00", payload.Field2);
        Assert.Equal(0, payload.Field2Value);
        Assert.Equal("1a 00", payload.MidField0);
        Assert.Equal(26, payload.MidField0Value);
        Assert.Equal("06 00", payload.MidField1);
        Assert.Equal(6, payload.MidField1Value);
        Assert.Equal("01 00 00 00 00 01 01 00 00 00 00 00 80 00 00 00 00 00 00 00 00", payload.TailHex);
    }

    [Fact]
    public void DetectUnknown6dPayloads_DecodesListDescriptorAndEntryIds()
    {
        byte[] payloadBytes = PacketResearcher.ParseHexBytes(
            "01 00 00 00 08 00 01 02 00 00 30 00 00 00 00 00 31 00 00 00 00")
            .ToArray();
        byte[] bytes = BuildSingleRpcPacket("6d", payloadBytes);

        Unknown6dPayloadSample payload = PacketResearcher.DetectUnknown6dPayloads(bytes, 33, "server").Single();

        Assert.Equal(33, payload.Line);
        Assert.Equal(21, payload.PayloadLength);
        Assert.Equal("01 00", payload.ModeHex);
        Assert.Equal(1, payload.ModeValue);
        Assert.Equal("00 00", payload.Field1);
        Assert.Equal(0, payload.Field1Value);
        Assert.Equal(8, payload.ListOffset);
        Assert.Equal(1, payload.ListFlag);
        Assert.Equal(2, payload.EntryCount);
        Assert.Equal(2, payload.ParsedEntryCount);
        Assert.Equal(new[] { 0x30, 0x31 }, payload.EntryIds);
    }

    [Fact]
    public void DetectUnknown80c1Payloads_DecodesTwoWords()
    {
        byte[] bytes = BuildSingleRpcPacket("80 c1", PacketResearcher.ParseHexBytes("00 00 c8 00").ToArray());

        Unknown80c1PayloadSample payload = PacketResearcher.DetectUnknown80c1Payloads(bytes, 34, "server").Single();

        Assert.Equal(34, payload.Line);
        Assert.Equal(4, payload.PayloadLength);
        Assert.Equal("00 00", payload.Field0);
        Assert.Equal(0, payload.Field0Value);
        Assert.Equal("c8 00", payload.Field1);
        Assert.Equal(200, payload.Field1Value);
    }

    [Fact]
    public void DetectLoadWorldPayloads_DecodesWorldPathAndEnvironment()
    {
        byte[] bytes = PacketResearcher.ParseHexBytes(
            "02 04 01 00 00 01 25 06 0e 00 03 00 00 00 04 db fa 48 01 1a 00 0b 00 77 6f 72 6c 64 2e 6d 65 74 72 00 08 00 57 65 61 74 68 65 72 00")
            .ToArray();

        LoadWorldPayloadSample payload = PacketResearcher.DetectLoadWorldPayloads(bytes, 51, "server").Single();

        Assert.Equal(51, payload.Line);
        Assert.Equal(36, payload.PayloadLength);
        Assert.Equal("0e 00", payload.MarkerHex);
        Assert.Equal(0x0e, payload.MarkerValue);
        Assert.Equal(3u, payload.DistrictId);
        Assert.Equal("04 db fa 48", payload.SimTimeHex);
        Assert.Equal(0x48fadb04u, payload.SimTimeValue);
        Assert.Equal("01", payload.FlagHex);
        Assert.Equal(26, payload.EnvironmentOffset);
        Assert.Equal(11, payload.WorldPathBytes);
        Assert.Equal("world.metr", payload.WorldPath);
        Assert.Equal(8, payload.EnvironmentBytes);
        Assert.Equal("Weather", payload.Environment);
    }

    [Fact]
    public void DetectPlayerValuePayloads_DecodesExpAndInfoValues()
    {
        byte[] bytes = PacketResearcher.ParseHexBytes(
            "02 04 01 00 00 02 0a 80 e5 e7 cb c0 12 00 00 00 00 0a 80 e4 bc 58 7a 00 00 00 00 00")
            .ToArray();

        PlayerValuePayloadSample[] payloads = PacketResearcher.DetectPlayerValuePayloads(bytes, 52, "server").ToArray();

        Assert.Equal(2, payloads.Length);
        Assert.Equal("80 e5", payloads[0].Header);
        Assert.Equal("SERVER_PLAYER_EXP", payloads[0].LocalName);
        Assert.Equal(0x12c0cbe7u, payloads[0].Value);
        Assert.Equal("e7 cb c0 12", payloads[0].ValueHex);
        Assert.Equal("00 00 00 00", payloads[0].TailHex);
        Assert.Equal("80 e4", payloads[1].Header);
        Assert.Equal("SERVER_PLAYER_INFO", payloads[1].LocalName);
        Assert.Equal(0x007a58bcu, payloads[1].Value);
        Assert.Equal("bc 58 7a 00", payloads[1].ValueHex);
        Assert.Equal("00 00 00 00", payloads[1].TailHex);
    }

    [Fact]
    public void DetectFlashTrafficPayloads_DecodesSizedUrl()
    {
        byte[] bytes = PacketResearcher.ParseHexBytes(
            "02 04 01 00 00 01 12 81 a9 00 00 07 00 05 09 00 68 74 74 70 3a 2f 2f 78 00")
            .ToArray();

        FlashTrafficPayloadSample payload = PacketResearcher.DetectFlashTrafficPayloads(bytes, 53, "server").Single();

        Assert.Equal(53, payload.Line);
        Assert.Equal(16, payload.PayloadLength);
        Assert.Equal("00 00", payload.Field0);
        Assert.Equal(0, payload.Field0Value);
        Assert.Equal("07 00", payload.Field1);
        Assert.Equal(7, payload.Field1Value);
        Assert.Equal("05", payload.MarkerHex);
        Assert.Equal(9, payload.UrlBytes);
        Assert.Equal("http://x", payload.Url);
    }

    [Fact]
    public void DetectFeatureEventPayloads_DecodesKeyAndNumericValue()
    {
        byte[] bytes = PacketResearcher.ParseHexBytes(
            "02 04 01 00 00 01 17 3a 05 00 11 00 0a 00 50 76 50 53 65 72 76 65 72 00 04 00 06 00 00 00")
            .ToArray();

        FeatureEventPayloadSample payload = PacketResearcher.DetectFeatureEventPayloads(bytes, 54, "server").Single();

        Assert.Equal(54, payload.Line);
        Assert.Equal(22, payload.PayloadLength);
        Assert.Equal("05 00", payload.PrefixHex);
        Assert.Equal(17, payload.DeclaredPayloadBytes);
        Assert.Equal(10, payload.KeyBytes);
        Assert.Equal("PvPServer", payload.Key);
        Assert.Equal(4, payload.ValueBytes);
        Assert.Null(payload.ValueText);
        Assert.Equal(6u, payload.ValueUInt32);
        Assert.Equal("06 00 00 00", payload.ValueHex);
    }

    [Fact]
    public void DetectFactionPlayerInfoPayloads_DecodesFactionSummary()
    {
        byte[] payload = PacketResearcher.ParseHexBytes("01 00 00 00 02 00 00 00 01 00 00 01 0f 00 34 00 01")
            .Concat(FixedAscii("Faction", 43))
            .Concat(PacketResearcher.ParseHexBytes("03 00 00 00 04 00 00 00"))
            .ToArray();
        byte[] bytes = BuildSingleRpcPacket("7c", payload);

        FactionPlayerInfoPayloadSample sample = PacketResearcher.DetectFactionPlayerInfoPayloads(bytes, 55, "server").Single();

        Assert.Equal(55, sample.Line);
        Assert.Equal(68, sample.PayloadLength);
        Assert.Equal(1u, sample.CharacterId);
        Assert.Equal(2u, sample.FactionId);
        Assert.Equal("01 00 00 01 0f 00", sample.PrefixHex);
        Assert.Equal(1, sample.Mode);
        Assert.Equal(52, sample.DataBytes);
        Assert.Equal(1, sample.Alignment);
        Assert.Equal("Faction", sample.FactionName);
        Assert.Equal(3u, sample.MasterCharacterId);
        Assert.Equal(4u, sample.Money);
    }

    [Fact]
    public void DetectCrewMembersListPayloads_DecodesCrewNameAndMemberHandles()
    {
        byte[] payload = PacketResearcher.ParseHexBytes("01 00 00 00 02 00 00 00 01 21 00 03 00 00 00 04 00 00 00 05 00 00 00 28 00 14 02 00 00 4e 00 05 00 43 72 65 77 00 01 00 00 06 00 00 00")
            .Concat(FixedAscii("Alice", 32))
            .Concat(new byte[] { 0x01 })
            .ToArray();
        byte[] bytes = BuildSingleRpcPacket("80 86", payload);

        CrewMembersListPayloadSample sample = PacketResearcher.DetectCrewMembersListPayloads(bytes, 56, "server").Single();

        Assert.Equal(56, sample.Line);
        Assert.Equal(78, sample.PayloadLength);
        Assert.Equal(1u, sample.CharacterId);
        Assert.Equal(2u, sample.CrewId);
        Assert.Equal(1, sample.Organization);
        Assert.Equal(33, sample.CrewNameOffset);
        Assert.Equal(3u, sample.CaptainCharacterId);
        Assert.Equal(4u, sample.FirstMateCharacterId);
        Assert.Equal(5u, sample.Money);
        Assert.Equal(40, sample.MemberListOffset);
        Assert.Equal("14 02 00 00", sample.ConstantHex);
        Assert.Equal(78, sample.FullSize);
        Assert.Equal("Crew", sample.CrewName);
        Assert.Equal(1, sample.MemberCount);
        Assert.Equal(new[] { "Alice" }, sample.MemberHandles);
    }

    [Fact]
    public void DetectFriendOnlinePayloads_DecodesSizedHandle()
    {
        byte[] bytes = PacketResearcher.ParseHexBytes(
            "02 04 01 00 00 01 0e 80 de 05 00 00 07 00 48 61 6e 64 6c 65 00")
            .ToArray();

        FriendOnlinePayloadSample sample = PacketResearcher.DetectFriendOnlinePayloads(bytes, 57, "server").Single();

        Assert.Equal(57, sample.Line);
        Assert.Equal(12, sample.PayloadLength);
        Assert.Equal(5, sample.StatusCode);
        Assert.Equal("00", sample.FlagHex);
        Assert.Equal(7, sample.HandleBytes);
        Assert.Equal("Handle", sample.Handle);
    }

    [Fact]
    public void DetectChatMessageResponsePayloads_DecodesSystemText()
    {
        byte[] payload = PacketResearcher.ParseHexBytes(
            "07 00 00 00 00 00 00 00 00 00 00 00 64 00 00 2e 00 24 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 0e 00 41 66 74 65 72 57 68 6f 72 75 4e 65 6f 00")
            .ToArray();
        byte[] bytes = BuildSingleRpcPacket("2e", payload);

        ChatMessageResponsePayloadSample sample = PacketResearcher.DetectChatMessageResponsePayloads(bytes, 58, "server").Single();

        Assert.Equal(58, sample.Line);
        Assert.Equal(51, sample.PayloadLength);
        Assert.Equal("07", sample.TypeHex);
        Assert.Equal(7, sample.TypeValue);
        Assert.Equal("SYSTEM", sample.TypeName);
        Assert.Equal("64 00", sample.Field12Hex);
        Assert.Equal(100, sample.Field12Value);
        Assert.Equal("2e 00", sample.Field15Hex);
        Assert.Equal(46, sample.Field15Value);
        Assert.Equal(14, sample.TextBytes);
        Assert.Equal("AfterWhoruNeo", sample.Text);
    }

    [Fact]
    public void DetectWhereamiResponsePayloads_DecodesCoordinates()
    {
        byte[] bytes = BuildSingleRpcPacket("81 54", PacketResearcher.ParseHexBytes(
            "00 00 20 c1 00 00 00 42 00 00 80 42 07 01 00").ToArray());

        WhereamiResponsePayloadSample sample = PacketResearcher.DetectWhereamiResponsePayloads(bytes, 59, "server").Single();

        Assert.Equal(59, sample.Line);
        Assert.Equal(15, sample.PayloadLength);
        Assert.Equal(-10f, sample.X);
        Assert.Equal(32f, sample.Y);
        Assert.Equal(64f, sample.Z);
        Assert.Equal("07 01 00", sample.TailHex);
    }

    [Fact]
    public void DetectFactionNameResponsePayloads_DecodesFixedName()
    {
        byte[] payload = PacketResearcher.ParseHexBytes("34 12 00 00")
            .Concat(FixedAscii("Faction", 42))
            .ToArray();
        byte[] bytes = BuildSingleRpcPacket("80 f5", payload);

        FactionNameResponsePayloadSample sample = PacketResearcher.DetectFactionNameResponsePayloads(bytes, 60, "server").Single();

        Assert.Equal(60, sample.Line);
        Assert.Equal(46, sample.PayloadLength);
        Assert.Equal(0x1234u, sample.FactionId);
        Assert.Equal("Faction", sample.FactionName);
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
            new[]
            {
                new AttributeDefinition("mxo-hd", "VendorLikeClass", 3827, "TalkActiveTracker", 1, 0x043d, -1, "VendorLikeClass.cs", 12)
            },
            Array.Empty<FxDefinition>(),
            new[]
            {
                new GameObjectEntry("Mclothing_Pants_A8_C1", 1085, "data/gameobjects.csv", 1),
                new GameObjectEntry("Mclothing_Pants_A6_C8", 1077, "data/gameobjects.csv", 2)
            },
            Array.Empty<WorldEntityEntry>(),
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
        Assert.Contains("### `80 bc` Fixed-Offset Schema Shapes", markdown);
        Assert.Contains("| `45 03` | `00 00 00` | `00 00` | `01 00` | 1 | 1 | 00 43 | 3d 04 | sample.txt (1) | 80 b2 -> 80 bc (1) | 1 | 0 | `sample.txt:10` |", markdown);
        Assert.Contains("| `55 00` | `00 00 00` | `01 00` | `00 00` | 1 | 1 | 00 62 | 15 04 | sample.txt (1) | 80 b2 -> 80 bc (1) | 0 | 0 | `sample.txt:11` |", markdown);
        Assert.Contains("### Hardcoded `80 bc` Family Coverage", markdown);
        Assert.Contains("| `45 03` | 1 | 1 | 1 | 1 | 1 | `3d 04` / `00 00` / `01 00` (1) | mxo-hd PlayerHandler.cs:122 |", markdown);
        Assert.Contains("### Linked Short/Long Field-Value Consistency", markdown);
        Assert.Contains("- Same-packet field links: 2", markdown);
        Assert.Contains("- Links with repeated `80 bc` byte 9 field: 1", markdown);
        Assert.Contains("- Links with matching `80 bc` byte 11 value: 1", markdown);
        Assert.Contains("| `35 04` | `00 00` | `15 04` | `01 00` | 1 | 1 | `sample.txt:11` |", markdown);
        Assert.Contains("### Linked State GameObject Symbol Matches", markdown);
        Assert.Contains("| `3d 04` (1085) | Mclothing_Pants_A8_C1 | 1 | 1 | `sample.txt:10` | `3d 04 00 00 08 02` | `45 03 3d 04 00 43 00 00 00 3d 04 00 00 00 00 01 00 00 00 00` |", markdown);
        Assert.Contains("### Vendor and Interaction `80 bc` Layout Leads", markdown);
        Assert.Contains("| `3d 04` (1085) | TalkActiveTracker | creation | 1 | 1 | 1 | 0 | `45 03` (1) | `sample.txt:10` |", markdown);
    }

    [Fact]
    public void ReportWriter_IncludesAdditionalProtocol04ServerPayloadShapes()
    {
        PacketDumpFileSummary dump = new(
            "state.txt",
            1,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            new[]
            {
                new Protocol04PacketSequenceSample(42, "server", new[] { "81 68" })
            },
            Array.Empty<Protocol03ObjectViewSample>(),
            Array.Empty<Protocol04InteractionPayloadSample>(),
            Array.Empty<PlayerAttributePayloadSample>(),
            Array.Empty<ManageBonusPayloadSample>())
        {
            Protocol04ServerPayloadShapes = new[]
            {
                new Protocol04ServerPayloadShapeSample(
                    42,
                    "81 68",
                    "CR2:SERVER_TEST",
                    8,
                    "17 00",
                    0x17,
                    "27 00",
                    0x27,
                    "41 42",
                    0x4241,
                    "17 00 27 00 41 42 43 44",
                    "-",
                    new[] { "ABCD" })
            }
        };
        PacketResearchReport report = new(
            ".",
            Array.Empty<string>(),
            null,
            Array.Empty<string>(),
            Array.Empty<RpcHeaderEntry>(),
            Array.Empty<AttributeDefinition>(),
            Array.Empty<FxDefinition>(),
            Array.Empty<GameObjectEntry>(),
            Array.Empty<WorldEntityEntry>(),
            Array.Empty<RajkoRpcEntry>(),
            Array.Empty<HardcodedCommandExample>(),
            Array.Empty<Protocol03HardcodedExample>(),
            Array.Empty<Protocol04InteractionCommandExample>(),
            Array.Empty<VendorInventoryEntry>(),
            Array.Empty<RpcComparison>(),
            new[] { dump });

        string markdown = ReportWriter.ToMarkdown(report);

        Assert.Contains("## Protocol 04 Additional Server Payload Shapes", markdown);
        Assert.Contains("| `81 68` | CR2:SERVER_TEST | 8 | `17 00` | `27 00` | `41 42` | 1 | ABCD | state.txt (1) | 81 68 (1) | `state.txt:42` | `17 00 27 00 41 42 43 44` | `-` |", markdown);
    }

    [Fact]
    public void ReportWriter_IncludesUnknown8167PayloadSummary()
    {
        PacketDumpFileSummary dump = new(
            "state.txt",
            1,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            new[]
            {
                new Protocol04PacketSequenceSample(42, "server", new[] { "81 67" })
            },
            Array.Empty<Protocol03ObjectViewSample>(),
            Array.Empty<Protocol04InteractionPayloadSample>(),
            Array.Empty<PlayerAttributePayloadSample>(),
            Array.Empty<ManageBonusPayloadSample>())
        {
            Unknown8167Payloads = new[]
            {
                new Unknown8167PayloadSample(
                    42,
                    63,
                    "17 00",
                    0x17,
                    "27 00",
                    0x27,
                    "1c 22",
                    0x221c,
                    "17 00 27 00 1c 22 00 c6 01 11 00 00 00 00 00 00 00 37 00 00 08",
                    14,
                    "AfterWhoruNeo",
                    14,
                    "AfterWhoruNeo",
                    "02 00",
                    2,
                    "4b 00",
                    0x4b,
                    "17 00 27 00 1c 22 00 c6 01 11 00 00 00 00 00 00 00 37 00 00 08 0e 00 41 66 74 65 72 57 68 6f 72 75 4e 65 6f 00 0e 00 41 66 74 65 72 57 68 6f 72 75 4e 65 6f 00 02 00 4b 00 00 00 00 00 00 00")
            }
        };
        PacketResearchReport report = new(
            ".",
            Array.Empty<string>(),
            null,
            Array.Empty<string>(),
            Array.Empty<RpcHeaderEntry>(),
            Array.Empty<AttributeDefinition>(),
            Array.Empty<FxDefinition>(),
            Array.Empty<GameObjectEntry>(),
            Array.Empty<WorldEntityEntry>(),
            Array.Empty<RajkoRpcEntry>(),
            Array.Empty<HardcodedCommandExample>(),
            Array.Empty<Protocol03HardcodedExample>(),
            Array.Empty<Protocol04InteractionCommandExample>(),
            Array.Empty<VendorInventoryEntry>(),
            Array.Empty<RpcComparison>(),
            new[] { dump });

        string markdown = ReportWriter.ToMarkdown(report);

        Assert.Contains("## Unknown 81 67 Payload Groups", markdown);
        Assert.Contains("| 63 | `17 00` | `27 00` | `1c 22` | 14 | AfterWhoruNeo | 14 | AfterWhoruNeo | `02 00` | `4b 00` | 1 | state.txt (1) | 81 67 (1) | `state.txt:42` | `17 00 27 00 1c 22 00 c6 01 11 00 00 00 00 00 00 00 37 00 00 08` |", markdown);
    }

    [Fact]
    public void ReportWriter_IncludesUnknown47PayloadSummary()
    {
        PacketDumpFileSummary dump = new(
            "state.txt",
            1,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            new[]
            {
                new Protocol04PacketSequenceSample(42, "server", new[] { "80 bc", "47", "2e" })
            },
            Array.Empty<Protocol03ObjectViewSample>(),
            Array.Empty<Protocol04InteractionPayloadSample>(),
            Array.Empty<PlayerAttributePayloadSample>(),
            Array.Empty<ManageBonusPayloadSample>())
        {
            Unknown47Payloads = new[]
            {
                new Unknown47PayloadSample(
                    42,
                    46,
                    "00 00",
                    0,
                    "04 01",
                    0x0104,
                    "00 00",
                    0,
                    "1a 00",
                    26,
                    "06 00",
                    6,
                    "01 00 00 00 00 01 01 00 00 00 00 00 80 00 00 00 00 00 00 00 00",
                    "00 00 04 01 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 1a 00 06 00 00 00 01 00 00 00 00 01 01 00 00 00 00 00 80 00 00 00 00 00 00 00 00")
            }
        };
        PacketResearchReport report = new(
            ".",
            Array.Empty<string>(),
            null,
            Array.Empty<string>(),
            Array.Empty<RpcHeaderEntry>(),
            Array.Empty<AttributeDefinition>(),
            Array.Empty<FxDefinition>(),
            Array.Empty<GameObjectEntry>(),
            Array.Empty<WorldEntityEntry>(),
            Array.Empty<RajkoRpcEntry>(),
            Array.Empty<HardcodedCommandExample>(),
            Array.Empty<Protocol03HardcodedExample>(),
            Array.Empty<Protocol04InteractionCommandExample>(),
            Array.Empty<VendorInventoryEntry>(),
            Array.Empty<RpcComparison>(),
            new[] { dump });

        string markdown = ReportWriter.ToMarkdown(report);

        Assert.Contains("- Protocol 04 unknown `47` payloads: 1", markdown);
        Assert.Contains("## Unknown `47` Protocol 04 Payload Groups", markdown);
        Assert.Contains("| 46 | `00 00` | `04 01` | `00 00` | `1a 00` | `06 00` | `01 00 00 00 00 01 01 00 00 00 00 00 80 00 00 00 00 00 00 00 00` | 1 | state.txt (1) | 80 bc -> 47 -> 2e (1) | `state.txt:42` |", markdown);
    }

    [Fact]
    public void ReportWriter_IncludesUnknown6dAnd80c1PayloadSummaries()
    {
        PacketDumpFileSummary dump = new(
            "state.txt",
            1,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            new[]
            {
                new Protocol04PacketSequenceSample(42, "server", new[] { "6d", "80 c1" })
            },
            Array.Empty<Protocol03ObjectViewSample>(),
            Array.Empty<Protocol04InteractionPayloadSample>(),
            Array.Empty<PlayerAttributePayloadSample>(),
            Array.Empty<ManageBonusPayloadSample>())
        {
            Unknown6dPayloads = new[]
            {
                new Unknown6dPayloadSample(
                    42,
                    21,
                    "01 00",
                    1,
                    "00 00",
                    0,
                    8,
                    1,
                    2,
                    2,
                    new[] { 0x30, 0x31 },
                    "01 00 00 00 08 00 01 02 00 00 30 00 00 00 00 00 31 00 00 00 00")
            },
            Unknown80c1Payloads = new[]
            {
                new Unknown80c1PayloadSample(
                    42,
                    4,
                    "00 00",
                    0,
                    "c8 00",
                    200,
                    "00 00 c8 00")
            }
        };
        PacketResearchReport report = new(
            ".",
            Array.Empty<string>(),
            null,
            Array.Empty<string>(),
            Array.Empty<RpcHeaderEntry>(),
            Array.Empty<AttributeDefinition>(),
            Array.Empty<FxDefinition>(),
            Array.Empty<GameObjectEntry>(),
            Array.Empty<WorldEntityEntry>(),
            Array.Empty<RajkoRpcEntry>(),
            Array.Empty<HardcodedCommandExample>(),
            Array.Empty<Protocol03HardcodedExample>(),
            Array.Empty<Protocol04InteractionCommandExample>(),
            Array.Empty<VendorInventoryEntry>(),
            Array.Empty<RpcComparison>(),
            new[] { dump });

        string markdown = ReportWriter.ToMarkdown(report);

        Assert.Contains("- Protocol 04 unknown `6d` payloads: 1", markdown);
        Assert.Contains("- Protocol 04 unknown `80 c1` payloads: 1", markdown);
        Assert.Contains("## Unknown `6d` Protocol 04 Payload Groups", markdown);
        Assert.Contains("| 21 | `01 00` | `00 00` | 8 | 1 | 2 | 2 | 48-49 | 1 | state.txt (1) | 6d -> 80 c1 (1) | `state.txt:42` |", markdown);
        Assert.Contains("## Unknown `80 c1` Protocol 04 Payload Groups", markdown);
        Assert.Contains("| 4 | `00 00` | `c8 00` | 1 | state.txt (1) | 6d -> 80 c1 (1) | `state.txt:42` | `00 00 c8 00` |", markdown);
    }

    [Fact]
    public void ReportWriter_IncludesLoadWorldAndPlayerValueSummaries()
    {
        PacketDumpFileSummary dump = new(
            "teleport.txt",
            1,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            new[]
            {
                new Protocol04PacketSequenceSample(42, "server", new[] { "06", "80 e5", "80 e4" })
            },
            Array.Empty<Protocol03ObjectViewSample>(),
            Array.Empty<Protocol04InteractionPayloadSample>(),
            Array.Empty<PlayerAttributePayloadSample>(),
            Array.Empty<ManageBonusPayloadSample>())
        {
            LoadWorldPayloads = new[]
            {
                new LoadWorldPayloadSample(
                    42,
                    36,
                    "0e 00",
                    0x0e,
                    3,
                    "04 db fa 48",
                    0x48fadb04,
                    "01",
                    26,
                    11,
                    "world.metr",
                    8,
                    "Weather",
                    "0e 00 03 00 00 00 04 db fa 48 01 1a 00 0b 00 77 6f 72 6c 64 2e 6d 65 74 72 00 08 00 57 65 61 74 68 65 72 00")
            },
            PlayerValuePayloads = new[]
            {
                new PlayerValuePayloadSample(42, "80 e5", "SERVER_PLAYER_EXP", 8, 0x12c0cbe7, "e7 cb c0 12", "00 00 00 00", "e7 cb c0 12 00 00 00 00"),
                new PlayerValuePayloadSample(42, "80 e4", "SERVER_PLAYER_INFO", 8, 0x007a58bc, "bc 58 7a 00", "00 00 00 00", "bc 58 7a 00 00 00 00 00")
            }
        };
        PacketResearchReport report = new(
            ".",
            Array.Empty<string>(),
            null,
            Array.Empty<string>(),
            Array.Empty<RpcHeaderEntry>(),
            Array.Empty<AttributeDefinition>(),
            Array.Empty<FxDefinition>(),
            Array.Empty<GameObjectEntry>(),
            Array.Empty<WorldEntityEntry>(),
            Array.Empty<RajkoRpcEntry>(),
            Array.Empty<HardcodedCommandExample>(),
            Array.Empty<Protocol03HardcodedExample>(),
            Array.Empty<Protocol04InteractionCommandExample>(),
            Array.Empty<VendorInventoryEntry>(),
            Array.Empty<RpcComparison>(),
            new[] { dump });

        string markdown = ReportWriter.ToMarkdown(report);

        Assert.Contains("- Protocol 04 load-world payloads: 1", markdown);
        Assert.Contains("- Protocol 04 player value payloads: 2", markdown);
        Assert.Contains("## SERVER_LOAD_WORLD Payload Groups", markdown);
        Assert.Contains("| 36 | `0e 00` | 3 | `01` | 26 | 11 | world.metr | 8 | Weather | 1 | 04 db fa 48 (1224399620) | teleport.txt (1) | 06 -> 80 e5 -> 80 e4 (1) | `teleport.txt:42` |", markdown);
        Assert.Contains("## SERVER_PLAYER_EXP / SERVER_PLAYER_INFO Payload Groups", markdown);
        Assert.Contains("| `80 e4` | SERVER_PLAYER_INFO | 8 | `bc 58 7a 00` | 8018108 | `00 00 00 00` | 1 | teleport.txt (1) | 06 -> 80 e5 -> 80 e4 (1) | `teleport.txt:42` | `bc 58 7a 00 00 00 00 00` |", markdown);
        Assert.Contains("| `80 e5` | SERVER_PLAYER_EXP | 8 | `e7 cb c0 12` | 314624999 | `00 00 00 00` | 1 | teleport.txt (1) | 06 -> 80 e5 -> 80 e4 (1) | `teleport.txt:42` | `e7 cb c0 12 00 00 00 00` |", markdown);
    }

    [Fact]
    public void ReportWriter_IncludesFlashTrafficAndFeatureEventSummaries()
    {
        PacketDumpFileSummary dump = new(
            "events.txt",
            1,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            new[]
            {
                new Protocol04PacketSequenceSample(42, "server", new[] { "3a", "81 a9" })
            },
            Array.Empty<Protocol03ObjectViewSample>(),
            Array.Empty<Protocol04InteractionPayloadSample>(),
            Array.Empty<PlayerAttributePayloadSample>(),
            Array.Empty<ManageBonusPayloadSample>())
        {
            FlashTrafficPayloads = new[]
            {
                new FlashTrafficPayloadSample(
                    42,
                    16,
                    "00 00",
                    0,
                    "07 00",
                    7,
                    "05",
                    9,
                    "http://x",
                    "00 00 07 00 05 09 00 68 74 74 70 3a 2f 2f 78 00")
            },
            FeatureEventPayloads = new[]
            {
                new FeatureEventPayloadSample(
                    42,
                    22,
                    "05 00",
                    17,
                    10,
                    "PvPServer",
                    4,
                    null,
                    6,
                    "06 00 00 00",
                    "05 00 11 00 0a 00 50 76 50 53 65 72 76 65 72 00 04 00 06 00 00 00")
            }
        };
        PacketResearchReport report = new(
            ".",
            Array.Empty<string>(),
            null,
            Array.Empty<string>(),
            Array.Empty<RpcHeaderEntry>(),
            Array.Empty<AttributeDefinition>(),
            Array.Empty<FxDefinition>(),
            Array.Empty<GameObjectEntry>(),
            Array.Empty<WorldEntityEntry>(),
            Array.Empty<RajkoRpcEntry>(),
            Array.Empty<HardcodedCommandExample>(),
            Array.Empty<Protocol03HardcodedExample>(),
            Array.Empty<Protocol04InteractionCommandExample>(),
            Array.Empty<VendorInventoryEntry>(),
            Array.Empty<RpcComparison>(),
            new[] { dump });

        string markdown = ReportWriter.ToMarkdown(report);

        Assert.Contains("- Protocol 04 flash traffic payloads: 1", markdown);
        Assert.Contains("- Protocol 04 feature/event payloads: 1", markdown);
        Assert.Contains("## SERVER_FLASH_TRAFFIC Payload Groups", markdown);
        Assert.Contains("| 16 | `00 00` | `07 00` | `05` | 9 | http://x | 1 | events.txt (1) | 3a -> 81 a9 (1) | `events.txt:42` | `00 00 07 00 05 09 00 68 74 74 70 3a 2f 2f 78 00` |", markdown);
        Assert.Contains("## SERVER_FEATURE_EVENT Payload Groups", markdown);
        Assert.Contains("| 22 | `05 00` | 17 | 10 | PvPServer | 4 | - | 6 | `06 00 00 00` | 1 | events.txt (1) | 3a -> 81 a9 (1) | `events.txt:42` | `05 00 11 00 0a 00 50 76 50 53 65 72 76 65 72 00 04 00 06 00 00 00` |", markdown);
    }

    [Fact]
    public void ReportWriter_IncludesFactionCrewAndFriendOnlineSummaries()
    {
        PacketDumpFileSummary dump = new(
            "social.txt",
            1,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            new[]
            {
                new Protocol04PacketSequenceSample(42, "server", new[] { "7c", "80 86", "80 de" })
            },
            Array.Empty<Protocol03ObjectViewSample>(),
            Array.Empty<Protocol04InteractionPayloadSample>(),
            Array.Empty<PlayerAttributePayloadSample>(),
            Array.Empty<ManageBonusPayloadSample>())
        {
            FactionPlayerInfoPayloads = new[]
            {
                new FactionPlayerInfoPayloadSample(
                    42,
                    68,
                    1,
                    2,
                    "01 00 00 01 0f 00",
                    1,
                    52,
                    1,
                    "Faction",
                    3,
                    4,
                    null,
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    "01 00 00 00 02 00 00 00 01 00 00 01 0f 00 34 00 01")
            },
            CrewMembersListPayloads = new[]
            {
                new CrewMembersListPayloadSample(
                    42,
                    78,
                    1,
                    2,
                    1,
                    33,
                    3,
                    4,
                    5,
                    40,
                    "14 02 00 00",
                    78,
                    "Crew",
                    1,
                    new[] { "Alice" },
                    "01 00 00 00 02 00 00 00 01")
            },
            FriendOnlinePayloads = new[]
            {
                new FriendOnlinePayloadSample(
                    42,
                    12,
                    5,
                    "00",
                    7,
                    "Handle",
                    "05 00 00 07 00 48 61 6e 64 6c 65 00")
            }
        };
        PacketResearchReport report = new(
            ".",
            Array.Empty<string>(),
            null,
            Array.Empty<string>(),
            Array.Empty<RpcHeaderEntry>(),
            Array.Empty<AttributeDefinition>(),
            Array.Empty<FxDefinition>(),
            Array.Empty<GameObjectEntry>(),
            Array.Empty<WorldEntityEntry>(),
            Array.Empty<RajkoRpcEntry>(),
            Array.Empty<HardcodedCommandExample>(),
            Array.Empty<Protocol03HardcodedExample>(),
            Array.Empty<Protocol04InteractionCommandExample>(),
            Array.Empty<VendorInventoryEntry>(),
            Array.Empty<RpcComparison>(),
            new[] { dump });

        string markdown = ReportWriter.ToMarkdown(report);

        Assert.Contains("- Protocol 04 faction/player-info payloads: 1", markdown);
        Assert.Contains("- Protocol 04 crew-member payloads: 1", markdown);
        Assert.Contains("- Protocol 04 friend-online payloads: 1", markdown);
        Assert.Contains("## SERVER_FACTION_PLAYER_INFO Payload Groups", markdown);
        Assert.Contains("| 68 | `01 00 00 01 0f 00` | 1 | 52 | 1 | Faction | - | - | - | social.txt (1) | 7c -> 80 86 -> 80 de (1) | `social.txt:42` |", markdown);
        Assert.Contains("## SERVER_CREW_MEMBERS_LIST Payload Groups", markdown);
        Assert.Contains("| 78 | 1 | 33 | 40 | `14 02 00 00` | 78 | Crew | 1 | 1 | Alice | social.txt (1) | 7c -> 80 86 -> 80 de (1) | `social.txt:42` |", markdown);
        Assert.Contains("## SERVER_FRIEND_ONLINE Payload Groups", markdown);
        Assert.Contains("| 12 | 5 | `00` | 7 | 1 | Handle | social.txt (1) | 7c -> 80 86 -> 80 de (1) | `social.txt:42` | `05 00 00 07 00 48 61 6e 64 6c 65 00` |", markdown);
    }

    [Fact]
    public void ReportWriter_IncludesChatWhereamiAndFactionNameSummaries()
    {
        PacketDumpFileSummary dump = new(
            "responses.txt",
            1,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            new[]
            {
                new Protocol04PacketSequenceSample(42, "server", new[] { "2e", "81 54", "80 f5" })
            },
            Array.Empty<Protocol03ObjectViewSample>(),
            Array.Empty<Protocol04InteractionPayloadSample>(),
            Array.Empty<PlayerAttributePayloadSample>(),
            Array.Empty<ManageBonusPayloadSample>())
        {
            ChatMessageResponsePayloads = new[]
            {
                new ChatMessageResponsePayloadSample(
                    42,
                    51,
                    "07",
                    7,
                    "SYSTEM",
                    "64 00",
                    100,
                    "2e 00",
                    46,
                    14,
                    "AfterWhoruNeo",
                    "07 00 00 00")
            },
            WhereamiResponsePayloads = new[]
            {
                new WhereamiResponsePayloadSample(
                    42,
                    15,
                    -10,
                    32,
                    64,
                    "07 01 00",
                    "00 00 20 c1 00 00 00 42 00 00 80 42 07 01 00")
            },
            FactionNameResponsePayloads = new[]
            {
                new FactionNameResponsePayloadSample(
                    42,
                    46,
                    0x1234,
                    "Faction",
                    "34 12 00 00 46 61 63 74 69 6f 6e 00")
            }
        };
        PacketResearchReport report = new(
            ".",
            Array.Empty<string>(),
            null,
            Array.Empty<string>(),
            Array.Empty<RpcHeaderEntry>(),
            Array.Empty<AttributeDefinition>(),
            Array.Empty<FxDefinition>(),
            Array.Empty<GameObjectEntry>(),
            Array.Empty<WorldEntityEntry>(),
            Array.Empty<RajkoRpcEntry>(),
            Array.Empty<HardcodedCommandExample>(),
            Array.Empty<Protocol03HardcodedExample>(),
            Array.Empty<Protocol04InteractionCommandExample>(),
            Array.Empty<VendorInventoryEntry>(),
            Array.Empty<RpcComparison>(),
            new[] { dump });

        string markdown = ReportWriter.ToMarkdown(report);

        Assert.Contains("- Protocol 04 chat message response payloads: 1", markdown);
        Assert.Contains("- Protocol 04 whereami response payloads: 1", markdown);
        Assert.Contains("- Protocol 04 faction-name response payloads: 1", markdown);
        Assert.Contains("## SERVER_CHAT_MESSAGE_RESPONSE Payload Groups", markdown);
        Assert.Contains("| 51 | `07` | SYSTEM | `64 00` | `2e 00` | 14 | 1 | AfterWhoruNeo | responses.txt (1) | 2e -> 81 54 -> 80 f5 (1) | `responses.txt:42` |", markdown);
        Assert.Contains("## SERVER_CHAT_WHEREAMI_RESPONSE Payload Groups", markdown);
        Assert.Contains("| 15 | `07 01 00` | 1 | -10,32,64 | responses.txt (1) | 2e -> 81 54 -> 80 f5 (1) | `responses.txt:42` | `00 00 20 c1 00 00 00 42 00 00 80 42 07 01 00` |", markdown);
        Assert.Contains("## SERVER_FACTION_NAME_RESPONSE Payload Groups", markdown);
        Assert.Contains("| 46 | 4660 | Faction | 1 | responses.txt (1) | 2e -> 81 54 -> 80 f5 (1) | `responses.txt:42` | `34 12 00 00 46 61 63 74 69 6f 6e 00` |", markdown);
    }

    [Fact]
    public void ReportWriter_IncludesCoderAttributePayloadSummary()
    {
        PacketDumpFileSummary dump = new(
            "state.txt",
            1,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            new[]
            {
                new Protocol04PacketSequenceSample(42, "server", new[] { "80 bc", "80 bd", "80 bc" })
            },
            Array.Empty<Protocol03ObjectViewSample>(),
            Array.Empty<Protocol04InteractionPayloadSample>(),
            Array.Empty<PlayerAttributePayloadSample>(),
            Array.Empty<ManageBonusPayloadSample>())
        {
            CoderAttributePayloads = new[]
            {
                new CoderAttributePayloadSample(42, 6, "21 00", 0x21, "01 00", 1, "00 00", 0, "21 00 01 00 00 00")
            }
        };
        PacketResearchReport report = new(
            ".",
            Array.Empty<string>(),
            null,
            Array.Empty<string>(),
            Array.Empty<RpcHeaderEntry>(),
            Array.Empty<AttributeDefinition>(),
            Array.Empty<FxDefinition>(),
            Array.Empty<GameObjectEntry>(),
            Array.Empty<WorldEntityEntry>(),
            Array.Empty<RajkoRpcEntry>(),
            Array.Empty<HardcodedCommandExample>(),
            Array.Empty<Protocol03HardcodedExample>(),
            Array.Empty<Protocol04InteractionCommandExample>(),
            Array.Empty<VendorInventoryEntry>(),
            Array.Empty<RpcComparison>(),
            new[] { dump });

        string markdown = ReportWriter.ToMarkdown(report);

        Assert.Contains("## SERVER_CODER_ATTRIBUTE_UNKNOWN Payload Groups", markdown);
        Assert.Contains("| 6 | `21 00` | `01 00` | `00 00` | 1 | state.txt (1) | 80 bc -> 80 bd -> 80 bc (1) | `state.txt:42` | `21 00 01 00 00 00` |", markdown);
    }

    [Fact]
    public void ReportWriter_IncludesAbilityUnloadPayloadSummary()
    {
        PacketDumpFileSummary dump = new(
            "state.txt",
            1,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            new[]
            {
                new Protocol04PacketSequenceSample(42, "server", new[] { "80 b3", "80 bc" })
            },
            Array.Empty<Protocol03ObjectViewSample>(),
            Array.Empty<Protocol04InteractionPayloadSample>(),
            Array.Empty<PlayerAttributePayloadSample>(),
            new[]
            {
                new ManageBonusPayloadSample(42, 20, "45 03", 0x0345, "11 00", 0x11, "00 02", 0x0200, "45 03 11 00 00 02 00 00 00 11 00 32 00 00 00 01 00 00 00 00")
            })
        {
            AbilityUnloadPayloads = new[]
            {
                new AbilityUnloadPayloadSample(42, 2, "11 00", 0x11, "11 00")
            }
        };
        PacketResearchReport report = new(
            ".",
            Array.Empty<string>(),
            null,
            Array.Empty<string>(),
            Array.Empty<RpcHeaderEntry>(),
            Array.Empty<AttributeDefinition>(),
            Array.Empty<FxDefinition>(),
            Array.Empty<GameObjectEntry>(),
            Array.Empty<WorldEntityEntry>(),
            Array.Empty<RajkoRpcEntry>(),
            Array.Empty<HardcodedCommandExample>(),
            Array.Empty<Protocol03HardcodedExample>(),
            Array.Empty<Protocol04InteractionCommandExample>(),
            Array.Empty<VendorInventoryEntry>(),
            Array.Empty<RpcComparison>(),
            new[] { dump });

        string markdown = ReportWriter.ToMarkdown(report);

        Assert.Contains("## SERVER_ABILITY_UNLOAD Payload Groups", markdown);
        Assert.Contains("| 2 | `11 00` | 1 | 1 | state.txt (1) | 80 b3 -> 80 bc (1) | `state.txt:42` | `11 00` |", markdown);
    }

    [Fact]
    public void ReportWriter_IncludesFriendListStatusPayloadSummary()
    {
        PacketDumpFileSummary dump = new(
            "state.txt",
            1,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            new[]
            {
                new Protocol04PacketSequenceSample(42, "server", new[] { "80 d7", "80 bc" })
            },
            Array.Empty<Protocol03ObjectViewSample>(),
            Array.Empty<Protocol04InteractionPayloadSample>(),
            Array.Empty<PlayerAttributePayloadSample>(),
            new[]
            {
                new ManageBonusPayloadSample(42, 20, "45 03", 0x0345, "3d 04", 0x043d, "00 02", 0x0200, "45 03 3d 04 00 02 00 00 00 3d 04 32 00 00 00 01 00 00 00 00")
            })
        {
            FriendListStatusPayloads = new[]
            {
                new FriendListStatusPayloadSample(42, 13, "08 00", 8, "3c 00", 0x3c, "00 8e", 0x8e00, 5, "Alice", "08 00 3c 00 00 8e 05 00 41 6c 69 63 65")
            }
        };
        PacketResearchReport report = new(
            ".",
            Array.Empty<string>(),
            null,
            Array.Empty<string>(),
            Array.Empty<RpcHeaderEntry>(),
            Array.Empty<AttributeDefinition>(),
            Array.Empty<FxDefinition>(),
            Array.Empty<GameObjectEntry>(),
            Array.Empty<WorldEntityEntry>(),
            Array.Empty<RajkoRpcEntry>(),
            Array.Empty<HardcodedCommandExample>(),
            Array.Empty<Protocol03HardcodedExample>(),
            Array.Empty<Protocol04InteractionCommandExample>(),
            Array.Empty<VendorInventoryEntry>(),
            Array.Empty<RpcComparison>(),
            new[] { dump });

        string markdown = ReportWriter.ToMarkdown(report);

        Assert.Contains("## SERVER_FRIENDLIST_STATUS Payload Groups", markdown);
        Assert.Contains("| 13 | `08 00` | `3c 00` | `00 8e` | 5 | Alice | 1 | 0 | state.txt (1) | 80 d7 -> 80 bc (1) | `state.txt:42` | `08 00 3c 00 00 8e 05 00 41 6c 69 63 65` |", markdown);
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
            new[]
            {
                new GameObjectEntry("Door SL Push Knob Metal1texvar", 416, "data/gameobjects.csv", 8284),
                new GameObjectEntry("Door SL Push Knob Fadedgreen02", 580, "data/gameobjects.csv", 5880)
            },
            Array.Empty<WorldEntityEntry>(),
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
            new[] { dump })
        {
            ItemCommandEntries = new[]
            {
                new ItemCommandEntry(700, "Revolver2", "Royer Snub Nose", "research/community-data/invitems_command.txt", 49),
                new ItemCommandEntry(698, "Submachinegun1", "Zatkin Machine Pistol", "research/community-data/invitems_command.txt", 47),
                new ItemCommandEntry(696, "Shotgun1", "MP-700 Pump Action", "research/community-data/invitems_command.txt", 45),
                new ItemCommandEntry(692, "AutomaticRifle1", "Bormann 5000", "research/community-data/invitems_command.txt", 41),
                new ItemCommandEntry(7873, "AutomaticRifle7", "NH2000 Assault Rifle", "research/community-data/invitems_command.txt", 3130),
                new ItemCommandEntry(2497, "DualPistol7", "Twin WestTek 49SAs", "research/community-data/invitems_command.txt", 556)
            },
            StaticObjectEntries = new[]
            {
                new StaticObjectEntry(1, 847, 888143880, "0800f034", 8, "08000000", 416, "a0010000", false, -69100.0, 145.0, 16200.0, -1.0, "data/staticObjects_slums_2.csv", 56222),
                new StaticObjectEntry(1, 1250, 1310720002, "0200204e", 2, "02000000", 580, "44020000", false, 19100.0, 545.0, 7600.0, 0.0, "data/staticObjects_slums_2.csv", 379838)
            }
        };

        string markdown = ReportWriter.ToMarkdown(report);

        Assert.Contains("- Protocol 04 interaction payloads: 1", markdown);
        Assert.Contains("- Inventory command item entries: 6", markdown);
        Assert.Contains("- Static object correlation rows: 2", markdown);
        Assert.Contains("- External object-interaction command examples: 1", markdown);
        Assert.Contains("## Protocol 04 Object Interaction Payloads", markdown);
        Assert.Contains("| `80 c8` | static | 3 | door | 1 | 1 | `doors.txt:4` | `08 00 f0 34 03 00` |", markdown);
        Assert.Contains("| 888143880 | 13552 | static | 3 | door | 1 | `doors.txt:4` |", markdown);
        Assert.Contains("## External Object Interaction Command Examples", markdown);
        Assert.Contains("| `80 c8` | static | 3 | door | 1 | 1 | `tasteewheat Reality.log:37` | `02 00 20 4e 03 00` |", markdown);
        Assert.Contains("## Static Object Table Correlations", markdown);
        Assert.Contains("| 888143880 | 1 | 0 | 1 | 1 | 1/847 | 416 Door SL Push Knob Metal1texvar (1) | 1/847 -69100,145,16200 type 416 | `data/staticObjects_slums_2.csv:56222` |", markdown);
        Assert.Contains("| 1310720002 | 0 | 1 | 1 | 1 | 1/1250 | 580 Door SL Push Knob Fadedgreen02 (1) | 1/1250 19100,545,7600 type 580 | `data/staticObjects_slums_2.csv:379838` |", markdown);
        Assert.Contains("## Vendor Inventory Object Correlations", markdown);
        Assert.Contains("| 888143880 | 1 | 1 | 1 | 0 | 2 | 7873 AutomaticRifle7: NH2000 Assault Rifle<br>2497 DualPistol7: Twin WestTek 49SAs | `data/vendor_items.csv:11` |", markdown);
        Assert.Contains("| 1310720002 | 1 | 1 | 0 | 1 | 4 | 700 Revolver2: Royer Snub Nose<br>698 Submachinegun1: Zatkin Machine Pistol<br>696 Shotgun1: MP-700 Pump Action<br>692 AutomaticRifle1: Bormann 5000 | `data/vendor_items.csv:10` |", markdown);
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
        Protocol03EffectEmoteLead lead = Assert.Single(sample.EffectEmoteLeads);
        Assert.Equal(4, lead.Offset);
        Assert.Equal("02", lead.LeadByte0Hex);
        Assert.Equal("40", lead.LeadByte1Hex);
        Assert.Equal("11 00 00 10", lead.Field2Hex);
        Assert.Equal("5b 62 84 c7 00 00 be 42 3a ed 7e 46", lead.PositionHex);
        Assert.NotNull(lead.X);
        Assert.NotNull(lead.Y);
        Assert.NotNull(lead.Z);
        Assert.InRange(lead.Y.Value, 94.9f, 95.1f);
    }

    [Fact]
    public void DetectProtocol03ObjectViews_ExtractsStaticObjectDoorLead()
    {
        byte[] bytes = PacketResearcher.ParseHexBytes(
            "02 03 01 00 08 a0 01 08 00 f0 34 80 cd ab 03 84 00 00 00 00 f3 04 35 bf 00 00 00 00 f3 04 35 3f")
            .ToArray();

        Protocol03ObjectViewSample sample = PacketResearcher.DetectProtocol03ObjectViews(bytes, 13, "server").Single();

        Assert.Equal("static object/door spawn candidate", sample.Classification);
        Protocol03StaticObjectLead lead = Assert.Single(sample.StaticObjectLeads);
        Assert.Equal("a0", lead.Selector);
        Assert.Equal(4, lead.Offset);
        Assert.Equal(26, lead.PayloadBytes);
        Assert.Equal("01", lead.LeadByteHex);
        Assert.Equal("08 00 f0 34", lead.ObjectIdHex);
        Assert.Equal((uint)888143880, lead.ObjectId);
        Assert.Equal("80", lead.InstanceByteHex);
        Assert.Equal("cd ab", lead.SeparatorHex);
        Assert.Equal(3, lead.ObjectType);
        Assert.Equal("84", lead.UnknownByteHex);
        Assert.Equal("00 00 00 00 f3 04 35 bf 00 00 00 00 f3 04 35 3f", lead.TransformHex);
        Assert.Equal(0, lead.Q0);
        Assert.InRange(lead.Q1!.Value, -0.708f, -0.706f);
        Assert.Equal(0, lead.Q2);
        Assert.InRange(lead.Q3!.Value, 0.706f, 0.708f);
        Assert.Equal(0, lead.PostQuaternionBytes);
        Assert.Equal(string.Empty, lead.PostQuaternionMarkerHex);
        Assert.Null(lead.PostQuaternionX);
        Assert.Null(lead.PostQuaternionY);
        Assert.Null(lead.PostQuaternionZ);
        Assert.Equal(string.Empty, lead.PostQuaternionTailHex);
        Assert.Equal(string.Empty, lead.PostQuaternionTailField0Hex);
        Assert.Null(lead.PostQuaternionTailField0);
        Assert.Equal(string.Empty, lead.PostQuaternionTailField1Hex);
        Assert.Null(lead.PostQuaternionTailField1);
        Assert.Equal(string.Empty, lead.PostQuaternionTailSuffixHex);
        Assert.Equal(string.Empty, lead.PostQuaternionHex);
    }

    [Fact]
    public void DetectProtocol03ObjectViews_ExtractsNestedMovementLeadFromUnknownSelector()
    {
        byte[] bytes = PacketResearcher.ParseHexBytes(
            "02 03 01 00 02 14 00 0b 00 02 0e 01 6a 48 b1 29 47 00 20 8a c4 20 73 e2 c3 00 00")
            .ToArray();

        Protocol03ObjectViewSample sample = PacketResearcher.DetectProtocol03ObjectViews(bytes, 520, "server").Single();

        Assert.True(sample.Complete);
        Assert.Collection(
            sample.Segments,
            segment =>
            {
                Assert.Equal("14", segment.Selector);
                Assert.Equal("primary movement wrapper", segment.Classification);
                Assert.Equal(19, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            },
            segment =>
            {
                Assert.Equal("00", segment.Selector);
                Assert.Equal("one-byte state marker", segment.Classification);
                Assert.Equal(1, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            });

        Protocol03NestedMovementLead lead = Assert.Single(sample.NestedMovementLeads);
        Assert.Equal("14", lead.OuterSelector);
        Assert.Equal(4, lead.Offset);
        Assert.Equal(19, lead.PayloadBytes);
        Assert.Equal(2, lead.MarkerOffset);
        Assert.Equal("00 0b", lead.LeadPrefixHex);
        Assert.Equal("02", lead.MarkerModeHex);
        Assert.Equal("0e", lead.InnerSelector);
        Assert.Equal(14, lead.InnerPayloadBytes);
        Assert.Equal(2, lead.InnerPositionOffset);
        Assert.Equal("48 b1 29 47 00 20 8a c4 20 73 e2 c3", lead.PositionHex);
        Assert.InRange(lead.X, 43441f, 43442f);
        Assert.InRange(lead.Y, -1105f, -1103f);
        Assert.InRange(lead.Z, -453f, -452f);
        Assert.Equal("", lead.SuffixHex);
    }

    [Fact]
    public void DetectProtocol03ObjectViews_ExtractsNestedMovementLeadWithNonDefaultMarkerMode()
    {
        byte[] bytes = PacketResearcher.ParseHexBytes(
            "02 03 01 00 02 0c 00 00 00 00 00 00 00 00 00 00 00 00 00 03 00 06 0e 01 18 58 3a f6 c6 00 00 be 42 44 35 af 46 ff 00 00 00 10 00 00 00 80 80 80 80 c0 4c 0a 00")
            .ToArray();

        Protocol03ObjectViewSample sample = PacketResearcher.DetectProtocol03ObjectViews(bytes, 529, "server").Single();

        Assert.False(sample.Complete);
        Protocol03NestedMovementLead lead = Assert.Single(sample.NestedMovementLeads);
        Assert.Equal("03", lead.OuterSelector);
        Assert.Equal(0, lead.MarkerOffset);
        Assert.Equal("", lead.LeadPrefixHex);
        Assert.Equal("06", lead.MarkerModeHex);
        Assert.Equal("0e", lead.InnerSelector);
        Assert.Equal(14, lead.InnerPayloadBytes);
        Assert.Equal(2, lead.InnerPositionOffset);
        Assert.Equal("58 3a f6 c6 00 00 be 42 44 35 af 46", lead.PositionHex);
        Assert.Equal("ff 00 00 00 10 00 00 00", lead.SuffixHex);
        Assert.Equal("80 80 80 80 c0 4c 0a 00", lead.PostSuffixHex);
        Assert.True(lead.X < 0);
        Assert.InRange(lead.Y, 94f, 96f);
        Assert.True(lead.Z > 0);
    }

    [Fact]
    public void DetectProtocol03ObjectViews_DecodesOneUpdateSelector9bStateBlock()
    {
        byte[] bytes = PacketResearcher.ParseHexBytes(
            "02 03 04 01 01 9b 01 08 80 fd 00 00 00 00 00 00")
            .ToArray();

        Protocol03ObjectViewSample sample = PacketResearcher.DetectProtocol03ObjectViews(bytes, 43420, "server").Single();

        Assert.True(sample.Complete);
        Assert.Equal(1, sample.ParsedUpdateCount);
        Protocol03ObjectUpdateSegment segment = Assert.Single(sample.Segments);
        Assert.Equal("9b", segment.Selector);
        Assert.Equal("fixed selector 9b state block", segment.Classification);
        Assert.Equal(10, segment.PayloadBytes);
        Assert.True(segment.IsKnownLength);
        Assert.Equal("01 08 80 fd 00 00 00 00 00 00", segment.PayloadPrefixHex);
    }

    [Fact]
    public void ReportWriter_IncludesProtocol03EffectEmoteLeads()
    {
        byte[] bytes = PacketResearcher.ParseHexBytes(
            "02 03 02 00 03 28 01 c0 00 98 d9 00 ff 11 18 47 00 20 8a c4 07 be 0c c6 6d 20 94 1e 80 80 80 80 30 1d 0a 00 28 01 00")
            .ToArray();
        Protocol03ObjectViewSample sample = PacketResearcher.DetectProtocol03ObjectViews(bytes, 6, "server").Single();
        byte[] nestedBytes = PacketResearcher.ParseHexBytes(
            "02 03 0b 00 04 80 80 80 80 c0 4c 0a 00 28 01 01 07 00 06 27 00 40 00 00 00 10 00 00 00 00 00 00")
            .ToArray();
        Protocol03ObjectViewSample nestedSample = PacketResearcher.DetectProtocol03ObjectViews(nestedBytes, 555, "server").Single();
        PacketDumpFileSummary dump = new(
            "effect.txt",
            1,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            Array.Empty<Protocol04PacketSequenceSample>(),
            new[] { sample, nestedSample },
            Array.Empty<Protocol04InteractionPayloadSample>(),
            Array.Empty<PlayerAttributePayloadSample>(),
            Array.Empty<ManageBonusPayloadSample>());
        PacketResearchReport report = new(
            ".",
            Array.Empty<string>(),
            null,
            Array.Empty<string>(),
            Array.Empty<RpcHeaderEntry>(),
            Array.Empty<AttributeDefinition>(),
            new[]
            {
                new FxDefinition("1d0a0028", "28000a1d", "FX_EVADE_COMBAT", "fxlisthex.txt", 10)
            },
            Array.Empty<GameObjectEntry>(),
            Array.Empty<WorldEntityEntry>(),
            Array.Empty<RajkoRpcEntry>(),
            Array.Empty<HardcodedCommandExample>(),
            Array.Empty<Protocol03HardcodedExample>(),
            Array.Empty<Protocol04InteractionCommandExample>(),
            Array.Empty<VendorInventoryEntry>(),
            Array.Empty<RpcComparison>(),
            new[] { dump });

        string markdown = ReportWriter.ToMarkdown(report);

        Assert.Contains("### Protocol 03 Selector 28 Effect/Emote Leads", markdown);
        Assert.Contains("Across 2 selector `28` payloads (1 first-selector records)", markdown);
        Assert.Contains("| effect/emote state bundle | 3 | `28` | `01` / `c0` | `00 98 d9 00` | 1 |", markdown);
        Assert.Contains("| attribute/effect bundle | 4 | `80` | `01` / `01` | `07 00 06 27` | 1 |", markdown);
        Assert.Contains("`1d0a0028` FX_EVADE_COMBAT (1)", markdown);
    }

    [Fact]
    public void ReportWriter_IncludesProtocol03StaticObjectDoorLeads()
    {
        byte[] bytes = PacketResearcher.ParseHexBytes(
            "02 03 01 00 08 a0 01 08 00 f0 34 80 cd ab 03 84 00 00 00 00 f3 04 35 bf 00 00 00 00 f3 04 35 3f")
            .ToArray();
        Protocol03ObjectViewSample sample = PacketResearcher.DetectProtocol03ObjectViews(bytes, 13, "server").Single();
        PacketDumpFileSummary dump = new(
            "doors.txt",
            1,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            Array.Empty<Protocol04PacketSequenceSample>(),
            new[] { sample },
            Array.Empty<Protocol04InteractionPayloadSample>(),
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
            new[]
            {
                new GameObjectEntry("Door SL Push Knob Metal1texvar", 416, "data/gameobjects.csv", 8284)
            },
            Array.Empty<WorldEntityEntry>(),
            Array.Empty<RajkoRpcEntry>(),
            Array.Empty<HardcodedCommandExample>(),
            Array.Empty<Protocol03HardcodedExample>(),
            Array.Empty<Protocol04InteractionCommandExample>(),
            new[]
            {
                new VendorInventoryEntry(1, 888143880, new uint[] { 7873 }, "data/vendor_items.csv", 40)
            },
            Array.Empty<RpcComparison>(),
            new[] { dump })
        {
            StaticObjectEntries = new[]
            {
                new StaticObjectEntry(1, 847, 888143880, "0800f034", 8, "08000000", 416, "a0010000", false, -69100.0, 145.0, 16200.0, -1.0, "data/staticObjects_slums_2.csv", 56222)
            }
        };

        string markdown = ReportWriter.ToMarkdown(report);

        Assert.Contains("### Protocol 03 Static Object/Door Spawn Leads", markdown);
        Assert.Contains("Across 1 selector `3c`/`9e`/`9f`/`a0` tails, 1 distinct static object ids appear.", markdown);
        Assert.Contains("| `a0` | 888143880 | 3 | 1 | 0 | 26 | 1 | 1 | 416 Door SL Push Knob Metal1texvar (1) | 80 | cd ab | 0,-0.707,0,0.707 | - | - | - | - | - | - | - | - | `doors.txt:13` |", markdown);
    }

    [Fact]
    public void ReportWriter_IncludesProtocol03NestedMovementLeads()
    {
        byte[] bytes = PacketResearcher.ParseHexBytes(
            "02 03 01 00 02 14 00 0b 00 02 0e 01 6a 48 b1 29 47 00 20 8a c4 20 73 e2 c3 00")
            .ToArray();
        Protocol03ObjectViewSample sample = PacketResearcher.DetectProtocol03ObjectViews(bytes, 520, "server").Single();
        byte[] mode06Bytes = PacketResearcher.ParseHexBytes(
            "02 03 01 00 02 0c 00 00 00 00 00 00 00 00 00 00 00 00 00 03 00 06 0e 01 18 58 3a f6 c6 00 00 be 42 44 35 af 46 ff 00 00 00 10 00 00 00 80 80 80 80 c0 4c 0a 00")
            .ToArray();
        Protocol03ObjectViewSample mode06Sample = PacketResearcher.DetectProtocol03ObjectViews(mode06Bytes, 529, "server").Single();
        PacketDumpFileSummary dump = new(
            "selector14.txt",
            1,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            Array.Empty<Protocol04PacketSequenceSample>(),
            new[] { sample, mode06Sample },
            Array.Empty<Protocol04InteractionPayloadSample>(),
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
            Array.Empty<GameObjectEntry>(),
            Array.Empty<WorldEntityEntry>(),
            Array.Empty<RajkoRpcEntry>(),
            Array.Empty<HardcodedCommandExample>(),
            Array.Empty<Protocol03HardcodedExample>(),
            Array.Empty<Protocol04InteractionCommandExample>(),
            Array.Empty<VendorInventoryEntry>(),
            Array.Empty<RpcComparison>(),
            new[] { dump });

        string markdown = ReportWriter.ToMarkdown(report);

        Assert.Contains("### Protocol 03 Embedded Movement Leads", markdown);
        Assert.Contains("Across 2 embedded movement windows, 2 outer selectors contain a plausible movement window.", markdown);
        Assert.Contains("| `02` | `0e` | 1 | 1 | 0 | unclassified object-view update | 14 | - | `selector14.txt:520` |", markdown);
        Assert.Contains("| `06` | `0e` | 1 | 0 | 1 | object state update | 03 | ff 00 00 00 10 00 00 00 | `selector14.txt:529` |", markdown);
        Assert.Contains("| `06` | `0e` | `ff 00 00 00 10 00 00 00` | `80 80 80 80 c0 4c 0a 00` | 1 | 0 | 1 | object state update | 03 | 33 | `selector14.txt:529` |", markdown);
        Assert.Contains("| `14` | unclassified object-view update | `00 0b` | 2 | `02` | `0e` | 1 | 19 |", markdown);
        Assert.Contains("| `03` | object state update | `-` | 0 | `06` | `0e` | 1 | 33 |", markdown);
    }

    [Fact]
    public void ReportWriter_IncludesProtocol03Selector80AttributeEffectLeads()
    {
        byte[] bytes = PacketResearcher.ParseHexBytes(
            "02 03 01 00 02 80 04 00 00 00")
            .ToArray();
        Protocol03ObjectViewSample sample = PacketResearcher.DetectProtocol03ObjectViews(bytes, 555, "server").Single();
        Protocol03VariableSelectorLead lead = Assert.Single(sample.VariableSelectorLeads);
        Assert.Equal("80", lead.Selector);
        Assert.Equal(4, lead.Offset);
        Assert.Equal("04 00 00 00", lead.PrefixHex);

        PacketDumpFileSummary dump = new(
            "selector80.txt",
            1,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            Array.Empty<Protocol04PacketSequenceSample>(),
            new[] { sample },
            Array.Empty<Protocol04InteractionPayloadSample>(),
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
            Array.Empty<GameObjectEntry>(),
            Array.Empty<WorldEntityEntry>(),
            Array.Empty<RajkoRpcEntry>(),
            Array.Empty<HardcodedCommandExample>(),
            Array.Empty<Protocol03HardcodedExample>(),
            Array.Empty<Protocol04InteractionCommandExample>(),
            Array.Empty<VendorInventoryEntry>(),
            Array.Empty<RpcComparison>(),
            new[] { dump });

        string markdown = ReportWriter.ToMarkdown(report);

        Assert.Contains("### Protocol 03 Selector 80 Attribute/Effect Leads", markdown);
        Assert.Contains("Across 1 selector `80` tails, 0 contain at least one imported FX ID window.", markdown);
        Assert.Contains("| appearance/attribute update candidate | 2 | `04 00 00 00` | 1 | 4 | - | - | `selector80.txt:555` |", markdown);
    }

    [Fact]
    public void DetectProtocol03ObjectViews_ExtractsSelector2a2ePositionLikeLeads()
    {
        byte[] selector2aBytes = PacketResearcher.ParseHexBytes(
            "02 03 1d 00 02 2a 01 c0 00 00 96 00 01 7e b9 2a 47 00 00 d2 c2 36 b3 70 47 07 00 02 08 bf 71 0e 47 f8 df ec 44 e4 02 5d 47 00 00")
            .ToArray();
        Protocol03ObjectViewSample selector2a = PacketResearcher.DetectProtocol03ObjectViews(selector2aBytes, 5, "server").Single();

        Protocol03PositionLikeLead lead2a = Assert.Single(selector2a.PositionLikeLeads);
        Assert.Equal("2a", lead2a.Selector);
        Assert.Equal(4, lead2a.Offset);
        Assert.Equal("01 c0 00 00 96 00", lead2a.LeadPrefixHex);
        Assert.Equal(7, lead2a.PositionOffset);
        Assert.Equal("7e b9 2a 47 00 00 d2 c2 36 b3 70 47", lead2a.PositionHex);
        Assert.InRange(lead2a.X, 43705f, 43706f);
        Assert.InRange(lead2a.Y, -106f, -104f);
        Assert.InRange(lead2a.Z, 61619f, 61620f);

        byte[] selector2eBytes = PacketResearcher.ParseHexBytes(
            "02 03 27 00 02 2e 01 81 00 8c 2e 00 19 00 7b 00 05 cb c0 67 84 c7 00 80 fc c3 7f fb 86 c7")
            .ToArray();
        Protocol03ObjectViewSample selector2e = PacketResearcher.DetectProtocol03ObjectViews(selector2eBytes, 4235, "server").Single();

        Protocol03PositionLikeLead lead2e = Assert.Single(selector2e.PositionLikeLeads);
        Assert.Equal("2e", lead2e.Selector);
        Assert.Equal("01 81 00 8c 2e 00", lead2e.LeadPrefixHex);
        Assert.Equal(12, lead2e.PositionOffset);
        Assert.InRange(lead2e.X, -67792f, -67791f);
        Assert.InRange(lead2e.Y, -506f, -504f);
        Assert.InRange(lead2e.Z, -69112f, -69110f);
    }

    [Fact]
    public void ReportWriter_IncludesProtocol03Selector2a2ePositionLeads()
    {
        byte[] selector2aBytes = PacketResearcher.ParseHexBytes(
            "02 03 1d 00 02 2a 01 c0 00 00 96 00 01 7e b9 2a 47 00 00 d2 c2 36 b3 70 47 07 00 02 08 bf 71 0e 47 f8 df ec 44 e4 02 5d 47 00 00")
            .ToArray();
        byte[] selector2eBytes = PacketResearcher.ParseHexBytes(
            "02 03 27 00 02 2e 01 81 00 8c 2e 00 19 00 7b 00 05 cb c0 67 84 c7 00 80 fc c3 7f fb 86 c7")
            .ToArray();
        Protocol03ObjectViewSample selector2a = PacketResearcher.DetectProtocol03ObjectViews(selector2aBytes, 5, "server").Single();
        Protocol03ObjectViewSample selector2e = PacketResearcher.DetectProtocol03ObjectViews(selector2eBytes, 4235, "server").Single();
        PacketDumpFileSummary dump = new(
            "selector2x.txt",
            2,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            Array.Empty<Protocol04PacketSequenceSample>(),
            new[] { selector2a, selector2e },
            Array.Empty<Protocol04InteractionPayloadSample>(),
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
            Array.Empty<GameObjectEntry>(),
            Array.Empty<WorldEntityEntry>(),
            Array.Empty<RajkoRpcEntry>(),
            Array.Empty<HardcodedCommandExample>(),
            Array.Empty<Protocol03HardcodedExample>(),
            Array.Empty<Protocol04InteractionCommandExample>(),
            Array.Empty<VendorInventoryEntry>(),
            Array.Empty<RpcComparison>(),
            new[] { dump });

        string markdown = ReportWriter.ToMarkdown(report);

        Assert.Contains("### Protocol 03 Selector 2a/2e Position-Like Leads", markdown);
        Assert.Contains("Across 2 selector `2a`/`2e` variable tails, 2 expose a known position-like lead.", markdown);
        Assert.Contains("| `2a` | unclassified object-view update | `01 c0 00 00 96 00` | 7 | 1 | 37 |", markdown);
        Assert.Contains("| `2e` | unclassified object-view update | `01 81 00 8c 2e 00` | 12 | 1 | 24 |", markdown);
    }

    [Fact]
    public void DetectProtocol03ObjectViews_ExtractsSelector80SelfViewAttributes()
    {
        byte[] bytes = PacketResearcher.ParseHexBytes(
            "02 03 0b 00 04 80 80 80 80 c0 4c 0a 00 28 01 01 07 00 06 27 00 40 00 00 00 10 00 00 00")
            .ToArray();

        Protocol03ObjectViewSample sample = PacketResearcher.DetectProtocol03ObjectViews(bytes, 555, "server").Single();

        Assert.False(sample.Complete);
        Assert.Equal(1, sample.ParsedUpdateCount);
        Assert.Equal(new[] { "80", "28" }, sample.Segments.Select(segment => segment.Selector));
        Assert.Equal("self-view attribute mask/value block", sample.Segments[0].Classification);
        Assert.Equal(7, sample.Segments[0].PayloadBytes);
        Protocol03SelfViewAttributeLead lead = Assert.Single(sample.SelfViewAttributeLeads);
        Assert.Equal("80", lead.Selector);
        Assert.Equal(4, lead.Offset);
        Assert.Equal(7, lead.PayloadBytes);
        Assert.Equal("80 80 80 c0 4c 0a 00", lead.MaskPrefixHex);
        Assert.Equal("28 01 01 07 00 06 27 00", lead.SuffixPrefixHex);
        Protocol03CreationAttributeSample attribute = Assert.Single(lead.Attributes);
        Assert.Equal(27, attribute.Index);
        Assert.Equal("Health", attribute.Name);
        Assert.Equal("4c 0a", attribute.ValueHex);
    }

    [Fact]
    public void DetectProtocol03ObjectViews_SegmentsTerminalSelector28Marker()
    {
        byte[] bytes = PacketResearcher.ParseHexBytes(
            "02 03 2b 00 04 80 80 80 80 c0 b5 07 00 28 01 02 00 00")
            .ToArray();

        Protocol03ObjectViewSample sample = PacketResearcher.DetectProtocol03ObjectViews(bytes, 4381, "server").Single();

        Assert.False(sample.Complete);
        Assert.Equal(2, sample.ParsedUpdateCount);
        Assert.Equal(new[] { "80", "28" }, sample.Segments.Select(segment => segment.Selector));
        Assert.Equal("self-view attribute mask/value block", sample.Segments[0].Classification);
        Assert.Equal("compact effect/emote terminal marker", sample.Segments[1].Classification);
        Assert.Equal(4, sample.Segments[1].PayloadBytes);
        Assert.True(sample.Segments[1].IsKnownLength);
        Protocol03EffectEmoteLead lead = Assert.Single(sample.EffectEmoteLeads);
        Assert.Equal(12, lead.Offset);
        Assert.Equal(4, lead.PayloadBytes);
        Assert.Equal("01", lead.LeadByte0Hex);
        Assert.Equal("02", lead.LeadByte1Hex);
    }

    [Fact]
    public void ReportWriter_IncludesProtocol03Selector80SelfViewAttributes()
    {
        byte[] bytes = PacketResearcher.ParseHexBytes(
            "02 03 0b 00 04 80 80 80 80 c0 4c 0a 00 28 01 01 07 00 06 27 00 40 00 00 00 10 00 00 00")
            .ToArray();
        Protocol03ObjectViewSample sample = PacketResearcher.DetectProtocol03ObjectViews(bytes, 555, "server").Single();
        PacketDumpFileSummary dump = new(
            "selector80.txt",
            1,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            Array.Empty<Protocol04PacketSequenceSample>(),
            new[] { sample },
            Array.Empty<Protocol04InteractionPayloadSample>(),
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
            Array.Empty<GameObjectEntry>(),
            Array.Empty<WorldEntityEntry>(),
            Array.Empty<RajkoRpcEntry>(),
            Array.Empty<HardcodedCommandExample>(),
            Array.Empty<Protocol03HardcodedExample>(),
            Array.Empty<Protocol04InteractionCommandExample>(),
            Array.Empty<VendorInventoryEntry>(),
            Array.Empty<RpcComparison>(),
            new[] { dump });

        string markdown = ReportWriter.ToMarkdown(report);

        Assert.Contains("### Protocol 03 Selector 80 Self-View Attribute Leads", markdown);
        Assert.Contains("Across 1 selector `80` self-view blocks, 1 fixed-width Object12 self-view attributes decode.", markdown);
        Assert.Contains("| attribute/effect bundle | 4 | 27 Health | 1 | 7 | Health=2636 | - | 28 01 01 07 00 06 27 00 | `selector80.txt:555` | `80 80 80 c0 4c 0a 00` |", markdown);
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
    public void DetectProtocol03ObjectViews_SegmentsSelector09MovementPrefix()
    {
        byte[] bytes = PacketResearcher.ParseHexBytes(
            "02 03 02 00 03 09 08 00 67 62 84 c7 95 8b 57 43 e2 ec 7e 46 4b b1 13 04 ff 01 64 01")
            .ToArray();

        Protocol03ObjectViewSample sample = PacketResearcher.DetectProtocol03ObjectViews(bytes, 14, "server").Single();

        Assert.Equal("movement state bundle", sample.Classification);
        Assert.Equal(3, sample.UpdateCount);
        Assert.Equal("09", sample.FirstSelector);
        Assert.False(sample.Complete);
        Assert.Equal(1, sample.ParsedUpdateCount);
        Assert.Equal(new[] { "09", "4b" }, sample.Segments.Select(segment => segment.Selector));
        Assert.Equal("position xyz + two-byte prefix", sample.Segments[0].Classification);
        Assert.Equal(14, sample.Segments[0].PayloadBytes);
        Assert.Equal("08 00 67 62 84 c7 95 8b 57 43 e2 ec 7e 46", sample.Segments[0].PayloadPrefixHex);
        Assert.True(sample.Segments[0].IsKnownLength);
        Assert.Equal("selector 09 movement tail lead", sample.Segments[1].Classification);
        Assert.False(sample.Segments[1].IsKnownLength);
        Protocol03MovementStateTailLead tail = Assert.Single(sample.MovementStateTailLeads);
        Assert.Equal("09", tail.FirstSelector);
        Assert.Equal("4b", tail.TailSelector);
        Assert.Equal("0413b14b", tail.TagHex);
        Assert.Equal((uint)0x0413b14b, tail.TagValue);
        Assert.Equal("b1 13 04", tail.TagPayloadHex);
        Assert.Equal("ff 01 64 01", tail.MarkerHex);
        Assert.Equal("", tail.PostMarkerPrefixHex);
    }

    [Fact]
    public void DetectProtocol03ObjectViews_SegmentsSelector08MovementTailLead()
    {
        byte[] bytes = PacketResearcher.ParseHexBytes(
            "02 03 02 00 03 08 73 62 84 c7 34 35 94 43 8f ec 7e 46 c8 b1 13 04 ff 01 64 01")
            .ToArray();

        Protocol03ObjectViewSample sample = PacketResearcher.DetectProtocol03ObjectViews(bytes, 30, "server").Single();

        Assert.Equal("movement state bundle", sample.Classification);
        Assert.Equal(3, sample.UpdateCount);
        Assert.Equal("08", sample.FirstSelector);
        Assert.False(sample.Complete);
        Assert.Equal(1, sample.ParsedUpdateCount);
        Assert.Equal(new[] { "08", "c8" }, sample.Segments.Select(segment => segment.Selector));
        Assert.Equal("position xyz", sample.Segments[0].Classification);
        Assert.Equal(12, sample.Segments[0].PayloadBytes);
        Assert.True(sample.Segments[0].IsKnownLength);
        Assert.Equal("selector 08 movement tail lead", sample.Segments[1].Classification);
        Assert.False(sample.Segments[1].IsKnownLength);
        Protocol03MovementStateTailLead tail = Assert.Single(sample.MovementStateTailLeads);
        Assert.Equal("08", tail.FirstSelector);
        Assert.Equal("c8", tail.TailSelector);
        Assert.Equal("0413b1c8", tail.TagHex);
        Assert.Equal((uint)0x0413b1c8, tail.TagValue);
        Assert.Equal("b1 13 04", tail.TagPayloadHex);
        Assert.Equal("ff 01 64 01", tail.MarkerHex);
        Assert.Equal("", tail.PostMarkerPrefixHex);
    }

    [Fact]
    public void DetectProtocol03ObjectViews_TreatsSelector08KnownByteCollisionAsTailLead()
    {
        byte[] bytes = PacketResearcher.ParseHexBytes(
            "02 03 02 00 03 08 9c d4 85 c7 93 69 22 46 2d e0 73 46 0e 6b e4 19 ff 01 4e 01 00 00 00 00 00 00 00 00 00")
            .ToArray();

        Protocol03ObjectViewSample sample = PacketResearcher.DetectProtocol03ObjectViews(bytes, 595, "server").Single();

        Assert.False(sample.Complete);
        Assert.Equal(1, sample.ParsedUpdateCount);
        Assert.Equal(new[] { "08", "0e" }, sample.Segments.Select(segment => segment.Selector));
        Assert.Equal("position xyz", sample.Segments[0].Classification);
        Assert.Equal("selector 08 movement tail lead", sample.Segments[1].Classification);
        Assert.False(sample.Segments[1].IsKnownLength);
        Protocol03MovementStateTailLead tail = Assert.Single(sample.MovementStateTailLeads);
        Assert.Equal("08", tail.FirstSelector);
        Assert.Equal("0e", tail.TailSelector);
        Assert.Equal("19e46b0e", tail.TagHex);
        Assert.Equal((uint)0x19e46b0e, tail.TagValue);
        Assert.Equal("6b e4 19", tail.TagPayloadHex);
        Assert.Equal("ff 01 4e 01", tail.MarkerHex);
        Assert.Equal("00 00 00 00 00 00 00 00", tail.PostMarkerPrefixHex);
    }

    [Fact]
    public void DetectProtocol03ObjectViews_SegmentsSelector09ZeroPrefixMovement()
    {
        byte[] bytes = PacketResearcher.ParseHexBytes(
            "02 03 02 00 03 09 00 00 5b 62 84 c7 00 00 be 42 3a ed 7e 46 97 b4 13 04 ff 01")
            .ToArray();

        Protocol03ObjectViewSample sample = PacketResearcher.DetectProtocol03ObjectViews(bytes, 126, "server").Single();

        Assert.False(sample.Complete);
        Assert.Equal(1, sample.ParsedUpdateCount);
        Assert.Equal("09", sample.Segments[0].Selector);
        Assert.Equal("position xyz + two-byte prefix", sample.Segments[0].Classification);
        Assert.Equal(14, sample.Segments[0].PayloadBytes);
        Assert.Equal("00 00 5b 62 84 c7 00 00 be 42 3a ed 7e 46", sample.Segments[0].PayloadPrefixHex);
        Assert.True(sample.Segments[0].IsKnownLength);
        Assert.Equal("selector 09 movement tail lead", sample.Segments[1].Classification);
        Assert.False(sample.Segments[1].IsKnownLength);
    }

    [Fact]
    public void DetectProtocol03ObjectViews_TreatsSelector09KnownByteCollisionAsTailLead()
    {
        byte[] bytes = PacketResearcher.ParseHexBytes(
            "02 03 02 00 03 09 08 00 6d 5b 73 c7 15 02 0d 43 64 6a 7e c7 08 ca 68 05 80 80 b8 14 00 71 f3 68 05 00")
            .ToArray();

        Protocol03ObjectViewSample sample = PacketResearcher.DetectProtocol03ObjectViews(bytes, 26855, "server").Single();

        Assert.False(sample.Complete);
        Assert.Equal(1, sample.ParsedUpdateCount);
        Assert.Equal(new[] { "09", "08" }, sample.Segments.Select(segment => segment.Selector));
        Assert.Equal("position xyz + two-byte prefix", sample.Segments[0].Classification);
        Assert.Equal("selector 09 movement tail lead", sample.Segments[1].Classification);
        Assert.False(sample.Segments[1].IsKnownLength);
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
    public void DetectProtocol03ObjectViews_SegmentsAdjacentMovementWrapper()
    {
        byte[] bytes = PacketResearcher.ParseHexBytes(
            "02 03 14 00 02 0c a2 ee dd 32 47 00 20 8a c4 5c c6 50 c5 11 00 02 0c 4c ff 5e 33 47 00 20 8a c4 4a 3d 1f c5 0a 00 02 0e 04 fa 0e 5c 32 47 00 20 8a c4 20 b2 44 c5 00 00")
            .ToArray();

        Protocol03ObjectViewSample[] samples = PacketResearcher.DetectProtocol03ObjectViews(bytes, 947, "server").ToArray();

        Assert.Equal(2, samples.Length);
        Assert.All(samples, sample => Assert.True(sample.Complete));
        Assert.Equal(20, samples[0].ViewId);
        Assert.Equal(10, samples[1].ViewId);
        Assert.Equal(new[] { "0c", "11" }, samples[0].Segments.Select(segment => segment.Selector));
        Assert.Equal(new[] { 13, 16 }, samples[0].Segments.Select(segment => segment.PayloadBytes));
        Assert.Equal("adjacent movement wrapper", samples[0].Segments[1].Classification);
        Assert.True(samples[0].Segments[1].IsKnownLength);
        Assert.Equal(new[] { "0e", "00" }, samples[1].Segments.Select(segment => segment.Selector));
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
        Protocol03StringSample text = Assert.Single(sample.Strings);
        Assert.Equal(16, text.Offset);
        Assert.Equal("Anderson", text.Text);
        Assert.Equal("23 9b 81 ff 71 02 00 00", text.PrefixHex);
        Assert.Equal(string.Empty, text.SuffixHex);
    }

    [Fact]
    public void DetectProtocol03ObjectViews_ExtractsPlayerCharacterCreationRecord()
    {
        List<byte> bytes = PacketResearcher.ParseHexBytes("02 03 01 00 0c 0c 00 11 cd ab 02 88").ToList();
        bytes.AddRange(FixedAscii("Anderson", 32));
        bytes.Add(0x10);
        bytes.AddRange(FixedAscii("Thomas", 32));

        Protocol03ObjectViewSample sample = PacketResearcher.DetectProtocol03ObjectViews(bytes, 12, "server").Single();

        Protocol03PlayerCharacterCreationRecord record = Assert.Single(sample.PlayerCharacterCreationRecords);
        Assert.Equal(4, record.Offset);
        Assert.Equal("00 11 cd ab 02 88", record.RecordPrefixHex);
        Assert.Equal("0c", record.CreationFlagHex);
        Assert.Equal("0c 00", record.GameObjectIdHex);
        Assert.Equal(12, record.GameObjectId);
        Assert.Equal("11", record.SpawnCounterHex);
        Assert.Equal("cd ab", record.SeparatorHex);
        Assert.Equal(2, record.CreationAttributeCount);
        Assert.Equal("88", record.FirstAttributeMaskHex);
        Assert.Equal(67, record.CreationAttributeBytes);
        Assert.Equal(string.Empty, record.PostCreationAttributePrefixHex);
        Assert.Equal(new[] { 3, 11 }, record.CreationAttributes.Select(attribute => attribute.Index));
        Assert.Equal("RealLastName", record.CreationAttributes[0].Name);
        Assert.Equal("RealFirstName", record.CreationAttributes[1].Name);
        Assert.Equal("41 6e 64 65 72 73 6f 6e 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00", record.CreationAttributes[0].ValueHex);
        Assert.Equal("54 68 6f 6d 61 73 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00", record.CreationAttributes[1].ValueHex);
    }

    [Fact]
    public void DetectProtocol03ObjectViews_ClassifiesNamedNpcProfileSelector()
    {
        byte[] bytes = PacketResearcher.ParseHexBytes(
            "02 03 01 00 0c 57 02 e2 cd ab 11 8e 47 6f 6c 64 20 42 6c 6f 6f 64 20 43 68 61 6d 70 69 6f 6e")
            .ToArray();

        Protocol03ObjectViewSample sample = PacketResearcher.DetectProtocol03ObjectViews(bytes, 57, "server").Single();

        Assert.False(sample.Complete);
        Assert.Equal("NPC_BASE dynamic creation candidate", sample.Classification);
        Protocol03StringSample text = Assert.Single(sample.Strings);
        Assert.Equal(10, text.Offset);
        Assert.Equal("Gold Blood Champion", text.Text);
        Assert.Equal("02 e2 cd ab 11 8e", text.PrefixHex);
        Assert.Equal(string.Empty, text.SuffixHex);
        Protocol03NamedProfileRecord record = Assert.Single(sample.NamedProfileRecords);
        Assert.Equal(4, record.Offset);
        Assert.Equal("Gold Blood Champion", record.Text);
        Assert.Equal(19, record.NameBytes);
        Assert.Equal(19, record.NameSlotBytes);
        Assert.Equal("02 e2 cd ab 11 8e", record.RecordPrefixHex);
        Assert.Equal("0c", record.CreationFlagHex);
        Assert.Equal("57 02", record.GameObjectIdHex);
        Assert.Equal(599, record.GameObjectId);
        Assert.Equal("e2", record.SpawnCounterHex);
        Assert.Equal("cd ab", record.SeparatorHex);
        Assert.Equal(17, record.CreationAttributeCount);
        Assert.Equal("8e", record.FirstAttributeMaskHex);
        Assert.Equal(0, record.SuffixZeroBytes);
        Assert.Equal(string.Empty, record.TitleAbilityHex);
        Assert.Equal(string.Empty, record.CombatantModeHex);
        Assert.Equal(string.Empty, record.PostNameSlotHex);
        Assert.Equal(string.Empty, record.PostCombatantModeHex);
    }

    [Fact]
    public void DetectProtocol03ObjectViews_CapturesNamedProfilePostSlotBytes()
    {
        byte[] bytes = PacketResearcher.ParseHexBytes(
            "02 03 01 00 0c 57 02 e2 cd ab 11 8e 47 6f 6c 64 20 42 6c 6f 6f 64 20 43 68 61 6d 70 69 6f 6e 00 00 00 00 00 00 00 00 00 00 00 00 00 00 10 00 00 22 12 34 56 78 9a bc")
            .ToArray();

        Protocol03ObjectViewSample sample = PacketResearcher.DetectProtocol03ObjectViews(bytes, 58, "server").Single();

        Protocol03NamedProfileRecord record = Assert.Single(sample.NamedProfileRecords);
        Assert.Equal(19, record.NameBytes);
        Assert.Equal(32, record.NameSlotBytes);
        Assert.Equal(13, record.SuffixZeroBytes);
        Assert.Equal("00 10 00 00", record.TitleAbilityHex);
        Assert.Equal("22", record.CombatantModeHex);
        Assert.Equal("00 10 00 00 22 12 34 56", record.PostNameSlotHex);
        Assert.Equal("12 34 56 78 9a bc", record.PostCombatantModeHex);
    }

    [Fact]
    public void DetectProtocol03ObjectViews_CapturesNamedProfilePostCreationAttributeSuffix()
    {
        List<byte> bytes = PacketResearcher.ParseHexBytes("02 03 01 00 0c 57 02 e2 cd ab 04 8e").ToList();
        bytes.AddRange(FixedAscii("Gold Blood Champion", 32));
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 10 00 00 22 40 01 de ad be ef"));

        Protocol03ObjectViewSample sample = PacketResearcher.DetectProtocol03ObjectViews(bytes, 59, "server").Single();

        Protocol03NamedProfileRecord record = Assert.Single(sample.NamedProfileRecords);
        Assert.Equal(4, record.CreationAttributeCount);
        Assert.Equal(4, record.CreationAttributes.Count);
        Assert.Equal(41, record.CreationAttributeBytes);
        Assert.Equal("de ad be ef", record.PostCreationAttributePrefixHex);
        Assert.Null(record.PostCreationObjectViewHeaderOffset);
        Assert.Equal(string.Empty, record.PostCreationObjectViewHeaderHex);
        Assert.Equal(string.Empty, record.PostCreationObjectViewHeaderClassification);
    }

    [Fact]
    public void DetectProtocol03ObjectViews_CapturesNamedProfilePostCreationObjectViewBoundary()
    {
        List<byte> bytes = PacketResearcher.ParseHexBytes("02 03 01 00 0c 57 02 e2 cd ab 04 8e").ToList();
        bytes.AddRange(FixedAscii("Gold Blood Champion", 32));
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 10 00 00 22 40 01 de ad be 01 00 0c 57 02 e3 cd ab 04 8e"));

        Protocol03ObjectViewSample sample = PacketResearcher.DetectProtocol03ObjectViews(bytes, 60, "server").Single();

        Protocol03NamedProfileRecord record = Assert.Single(sample.NamedProfileRecords);
        Assert.Equal("de ad be 01 00 0c 57 02", record.PostCreationAttributePrefixHex);
        Assert.Equal(3, record.PostCreationObjectViewHeaderOffset);
        Assert.Equal("01 00 0c 57 02 e3 cd ab", record.PostCreationObjectViewHeaderHex);
        Assert.Equal("NPC_BASE dynamic creation candidate", record.PostCreationObjectViewHeaderClassification);
    }

    [Fact]
    public void ReportWriter_IncludesProtocol03NamedProfileRecordCandidates()
    {
        byte[] bytes = PacketResearcher.ParseHexBytes(
            "02 03 01 00 0c 57 02 e2 cd ab 04 8e 47 6f 6c 64 20 42 6c 6f 6f 64 20 43 68 61 6d 70 69 6f 6e 00 00 00 00 00 00 00 00 00 00 00 00 00 00 10 00 00 22 40 01")
            .ToArray();
        byte[] secondBytes = PacketResearcher.ParseHexBytes(
            "02 03 02 00 0c 57 02 e2 cd ab 04 8e 47 6f 6c 64 20 42 6c 6f 6f 64 20 45 6e 74 68 75 73 69 61 73 74 00 00 00 00 00 00 00 00 00 00 00 00 10 00 00 22 40 01")
            .ToArray();
        Protocol03ObjectViewSample sample = PacketResearcher.DetectProtocol03ObjectViews(bytes, 57, "server").Single();
        Protocol03ObjectViewSample secondSample = PacketResearcher.DetectProtocol03ObjectViews(secondBytes, 58, "server").Single();
        PacketDumpFileSummary dump = new(
            "npc.txt",
            2,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            Array.Empty<Protocol04PacketSequenceSample>(),
            new[] { sample, secondSample },
            Array.Empty<Protocol04InteractionPayloadSample>(),
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
            new[]
            {
                new GameObjectEntry("NPC_BASE", 599, "data/gameobjects.csv", 32012)
            },
            new[]
            {
                new WorldEntityEntry("mob_parsed_untouched.csv", "Gold Blood Champion", "it", 13, "mob spawn", "d7090058", "data/mob_parsed_untouched.csv", 68),
                new WorldEntityEntry("mob_parsed_untouched.csv", "Gold Blood Enthusiast", "it", 13, "mob spawn", "d6090058", "data/mob_parsed_untouched.csv", 58)
            },
            Array.Empty<RajkoRpcEntry>(),
            Array.Empty<HardcodedCommandExample>(),
            Array.Empty<Protocol03HardcodedExample>(),
            Array.Empty<Protocol04InteractionCommandExample>(),
            Array.Empty<VendorInventoryEntry>(),
            Array.Empty<RpcComparison>(),
            new[] { dump });

        string markdown = ReportWriter.ToMarkdown(report);

        Assert.Contains("### Protocol 03 NPC_BASE Dynamic Creation Headers", markdown);
        Assert.Contains("| 0c | 57 02 | 599 | NPC_BASE | 4 | 8e | 2 | 2 | e2 | `npc.txt:57` | `0c 57 02 e2 cd ab 04 8e` |", markdown);
        Assert.Contains("### Protocol 03 NPC_BASE CharacterName Records", markdown);
        Assert.Contains("| Gold Blood Champion | 1 | 1 | e2 | 4 | 8e | 13 | `npc.txt:57` | `0c 57 02 e2 cd ab 04 8e` |", markdown);
        Assert.Contains("### Protocol 03 NPC_BASE Spawn Counter Reuse", markdown);
        Assert.Contains("| e2 | 2 | 2 | Gold Blood Champion<br>Gold Blood Enthusiast | 4 | 8e | 11<br>13 | `npc.txt:57` |", markdown);
        Assert.Contains("### Protocol 03 NPC_BASE Creation Attribute Count Distribution", markdown);
        Assert.Contains("| 4 | 2 | 2 | e2 | 19<br>21 | Gold Blood Champion<br>Gold Blood Enthusiast | `npc.txt:57` |", markdown);
        Assert.Contains("### Protocol 03 NPC_BASE CharacterName Slot Distribution", markdown);
        Assert.Contains("| 32 | 2 | 2 | 19<br>21 | 11<br>13 | Gold Blood Champion<br>Gold Blood Enthusiast | `npc.txt:57` |", markdown);
        Assert.Contains("### Protocol 03 NPC_BASE Post-CharacterName Attribute Bytes", markdown);
        Assert.Contains("### Protocol 03 NPC_BASE TitleAbility and CombatantMode Distribution", markdown);
        Assert.Contains("| `00 10 00 00` | 22 | 2 | 2 | 4 | 40 01 | Gold Blood Champion<br>Gold Blood Enthusiast | `npc.txt:57` |", markdown);
        Assert.Contains("### Protocol 03 NPC_BASE Creation Attribute Parse Consistency", markdown);
        Assert.Contains("| 4 | 4 | 2 | 2 | Gold Blood Champion<br>Gold Blood Enthusiast | `npc.txt:57` |", markdown);
        Assert.Contains("### Protocol 03 NPC_BASE Post-Creation Attribute Suffix Leads", markdown);
        Assert.Contains("Across 2 Object599 creation records, 0 have bytes after the declared attribute stream, and 0 include `00 0c 57 02` inside the first eight suffix bytes.", markdown);
        Assert.Contains("| 4 | 4 | 41 | `-` | 2 | 2 | Gold Blood Champion<br>Gold Blood Enthusiast | `npc.txt:57` |", markdown);
        Assert.Contains("### Protocol 03 NPC_BASE Creation Attribute Presence", markdown);
        Assert.Contains("| 13 | StopFollowActiveTracker | 1 | 2 | 1 | 01 | Gold Blood Champion<br>Gold Blood Enthusiast | `npc.txt:57` |", markdown);
    }

    [Fact]
    public void ReportWriter_IncludesProtocol03NpcBasePostCreationObjectViewBoundaryLeads()
    {
        List<byte> bytes = PacketResearcher.ParseHexBytes("02 03 01 00 0c 57 02 e2 cd ab 04 8e").ToList();
        bytes.AddRange(FixedAscii("Gold Blood Champion", 32));
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 10 00 00 22 40 01 de ad be 01 00 0c 57 02 e3 cd ab 04 8e"));
        Protocol03ObjectViewSample sample = PacketResearcher.DetectProtocol03ObjectViews(bytes, 60, "server").Single();
        PacketDumpFileSummary dump = new(
            "npc-boundary.txt",
            1,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            Array.Empty<Protocol04PacketSequenceSample>(),
            new[] { sample },
            Array.Empty<Protocol04InteractionPayloadSample>(),
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
            new[]
            {
                new GameObjectEntry("NPC_BASE", 599, "data/gameobjects.csv", 32012)
            },
            Array.Empty<WorldEntityEntry>(),
            Array.Empty<RajkoRpcEntry>(),
            Array.Empty<HardcodedCommandExample>(),
            Array.Empty<Protocol03HardcodedExample>(),
            Array.Empty<Protocol04InteractionCommandExample>(),
            Array.Empty<VendorInventoryEntry>(),
            Array.Empty<RpcComparison>(),
            new[] { dump });

        string markdown = ReportWriter.ToMarkdown(report);

        Assert.Contains("### Protocol 03 NPC_BASE Post-Creation Object-View Boundary Leads", markdown);
        Assert.Contains("Across 1 Object599 creation records, 1 expose a plausible next object-view header inside the first eight post-attribute suffix bytes.", markdown);
        Assert.Contains("| 3 | `01 00 0c 57 02 e3 cd ab` | NPC_BASE dynamic creation candidate | 1 | de ad be 01 00 0c 57 02 | 1 | Gold Blood Champion | `npc-boundary.txt:60` |", markdown);
    }

    [Fact]
    public void ReportWriter_CorrelatesNpcBaseCreationAttributesWithMobRows()
    {
        byte[] bytes = PacketResearcher.ParseHexBytes(
            "02 03 01 00 0c 57 02 e2 cd ab 0c 8e 47 6f 6c 64 20 42 6c 6f 6f 64 20 43 68 61 6d 70 69 6f 6e 00 00 00 00 00 00 00 00 00 00 00 00 00 00 10 00 00 22 c0 01 80 c1 d7 09 00 58 00 00 00 00 a0 61 e2 40 00 00 00 00 00 44 91 c0 00 00 00 00 80 52 cc c0 80 82 01 a8 0d ee 02 32 04 ee 02 07 08 00 00")
            .ToArray();
        Protocol03ObjectViewSample sample = PacketResearcher.DetectProtocol03ObjectViews(bytes, 57, "server").Single();
        PacketDumpFileSummary dump = new(
            "npc.txt",
            1,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            Array.Empty<Protocol04PacketSequenceSample>(),
            new[] { sample },
            Array.Empty<Protocol04InteractionPayloadSample>(),
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
            new[]
            {
                new GameObjectEntry("NPC_BASE", 599, "data/gameobjects.csv", 32012)
            },
            new[]
            {
                new WorldEntityEntry("mob_parsed_untouched.csv", "Gold Blood Champion", "it", 13, "mob spawn", "d7090058", "data/mob_parsed_untouched.csv", 68, 750, 750, 37645.0, -1105.0, -14501.0, 1, 4, "07080000")
            },
            Array.Empty<RajkoRpcEntry>(),
            Array.Empty<HardcodedCommandExample>(),
            Array.Empty<Protocol03HardcodedExample>(),
            Array.Empty<Protocol04InteractionCommandExample>(),
            Array.Empty<VendorInventoryEntry>(),
            Array.Empty<RpcComparison>(),
            new[] { dump });

        string markdown = ReportWriter.ToMarkdown(report);

        Assert.Contains("### Protocol 03 NPC_BASE Creation Attribute Entity Correlation", markdown);
        Assert.Contains("| Gold Blood Champion | 1 | 1 | 1/1 | 1/1 | 1/1 | 1/1 | 1/1 | 1/1 | 1/1 | 1/1 | 0 | L13 HP750/750 RSI d7090058 rank 1 debuff 4 state 07080000 pos 37645,-1105,-14501 | `npc.txt:57` |", markdown);
    }

    [Fact]
    public void ReportWriter_ResolvesNpcBaseCreationEquipmentAndEffectSymbols()
    {
        byte[] bytes = PacketResearcher.ParseHexBytes(
            "02 03 01 00 0c 57 02 e2 cd ab 07 8e 47 6f 6c 64 20 42 6c 6f 6f 64 20 43 68 61 6d 70 69 6f 6e 00 00 00 00 00 00 00 00 00 00 00 00 00 00 10 00 00 22 c1 05 01 80 82 b5 07 00 28 80 80 80 80 01 b5 02 00 00")
            .ToArray();
        Protocol03ObjectViewSample sample = PacketResearcher.DetectProtocol03ObjectViews(bytes, 57, "server").Single();
        PacketDumpFileSummary dump = new(
            "npc.txt",
            1,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            Array.Empty<Protocol04PacketSequenceSample>(),
            new[] { sample },
            Array.Empty<Protocol04InteractionPayloadSample>(),
            Array.Empty<PlayerAttributePayloadSample>(),
            Array.Empty<ManageBonusPayloadSample>());
        PacketResearchReport report = new(
            ".",
            Array.Empty<string>(),
            null,
            Array.Empty<string>(),
            Array.Empty<RpcHeaderEntry>(),
            Array.Empty<AttributeDefinition>(),
            new[]
            {
                new FxDefinition("b5070028", "280007b5", "FX_VIRUSCAST_SPLIT_FAST_HEALING_FASTHEALING1", "fxlisthex.txt", 735)
            },
            new[]
            {
                new GameObjectEntry("NPC_BASE", 599, "data/gameobjects.csv", 32012),
                new GameObjectEntry("AutomaticRifle2", 693, "data/gameobjects.csv", 42)
            },
            Array.Empty<WorldEntityEntry>(),
            Array.Empty<RajkoRpcEntry>(),
            Array.Empty<HardcodedCommandExample>(),
            Array.Empty<Protocol03HardcodedExample>(),
            Array.Empty<Protocol04InteractionCommandExample>(),
            Array.Empty<VendorInventoryEntry>(),
            Array.Empty<RpcComparison>(),
            new[] { dump })
        {
            ItemCommandEntries = new[]
            {
                new ItemCommandEntry(693, "AutomaticRifle2", "Reit-Tek BR", "research/community-data/invitems_command.txt", 42)
            }
        };

        string markdown = ReportWriter.ToMarkdown(report);

        Assert.Contains("### Protocol 03 NPC_BASE Equipment and Effect Symbol Leads", markdown);
        Assert.Contains("| `b5 02 00 00` | 693 | AutomaticRifle2 | 693 AutomaticRifle2: Reit-Tek BR | 1 | Gold Blood Champion | `npc.txt:57` |", markdown);
        Assert.Contains("| `b5 07 00 28` | FX_VIRUSCAST_SPLIT_FAST_HEALING_FASTHEALING1 | 05 | 1 | Gold Blood Champion | `npc.txt:57` |", markdown);
    }

    [Fact]
    public void ReportWriter_IncludesProtocol03VariableBlockStringLeads()
    {
        byte[] bytes = PacketResearcher.ParseHexBytes(
            "02 03 01 00 0c 0c 00 dd cd ab 23 9b 81 ff 71 02 00 00 41 6e 64 65 72 73 6f 6e")
            .ToArray();
        Protocol03ObjectViewSample sample = PacketResearcher.DetectProtocol03ObjectViews(bytes, 11, "server").Single();
        PacketDumpFileSummary dump = new(
            "spawn.txt",
            1,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            Array.Empty<Protocol04PacketSequenceSample>(),
            new[] { sample },
            Array.Empty<Protocol04InteractionPayloadSample>(),
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
            Array.Empty<GameObjectEntry>(),
            new[]
            {
                new WorldEntityEntry("mob_parsed_untouched.csv", "Anderson", "slums", 22, "mob spawn", "87090058", "data/mob_parsed_untouched.csv", 123)
            },
            Array.Empty<RajkoRpcEntry>(),
            Array.Empty<HardcodedCommandExample>(),
            Array.Empty<Protocol03HardcodedExample>(),
            Array.Empty<Protocol04InteractionCommandExample>(),
            Array.Empty<VendorInventoryEntry>(),
            Array.Empty<RpcComparison>(),
            new[] { dump });

        string markdown = ReportWriter.ToMarkdown(report);

        Assert.Contains("### Protocol 03 Variable Block String Leads", markdown);
        Assert.Contains("| player spawn/self-view candidate | 12 | `0c` | Anderson | 1 | `spawn.txt:11` | 16 |", markdown);
        Assert.Contains("### Protocol 03 String Entity Correlations", markdown);
        Assert.Contains("| Anderson | 1 | 1 | mob_parsed_untouched.csv | 22 | slums / mob spawn | 87090058 | `spawn.txt:11` | `23 9b 81 ff 71 02 00 00` | `` | `data/mob_parsed_untouched.csv:123` |", markdown);
    }

    [Fact]
    public void ReportWriter_IncludesProtocol03PlayerCharacterCreationRecords()
    {
        List<byte> bytes = PacketResearcher.ParseHexBytes("02 03 01 00 0c 0c 00 11 cd ab 02 88").ToList();
        bytes.AddRange(FixedAscii("Anderson", 32));
        bytes.Add(0x10);
        bytes.AddRange(FixedAscii("Thomas", 32));
        Protocol03ObjectViewSample sample = PacketResearcher.DetectProtocol03ObjectViews(bytes, 12, "server").Single();
        PacketDumpFileSummary dump = new(
            "player.txt",
            1,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            Array.Empty<Protocol04PacketSequenceSample>(),
            new[] { sample },
            Array.Empty<Protocol04InteractionPayloadSample>(),
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
            new[]
            {
                new GameObjectEntry("PlayerCharacter", 12, "data/gameobjects.csv", 12733)
            },
            Array.Empty<WorldEntityEntry>(),
            Array.Empty<RajkoRpcEntry>(),
            Array.Empty<HardcodedCommandExample>(),
            Array.Empty<Protocol03HardcodedExample>(),
            Array.Empty<Protocol04InteractionCommandExample>(),
            Array.Empty<VendorInventoryEntry>(),
            Array.Empty<RpcComparison>(),
            new[] { dump });

        string markdown = ReportWriter.ToMarkdown(report);

        Assert.Contains("### Protocol 03 PlayerCharacter Creation Records", markdown);
        Assert.Contains("Across 1 PlayerCharacter creation records, 0 include a decoded `CharacterName` attribute.", markdown);
        Assert.Contains("| 0c | 0c 00 | 12 | PlayerCharacter | 2 | 88 | 2 | 1 | Anderson | Thomas | - | name Thomas Anderson | `player.txt:12` | `0c 0c 00 11 cd ab 02 88` |", markdown);
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

    [Fact]
    public void ParseFxDefinitionText_LoadsSingleEndianRealFxNames()
    {
        string[] lines =
        {
            "04000066 = fx_npc.fxl | oligarchs_spawns_activeaccelerated"
        };

        FxDefinition fx = PacketResearcher.ParseFxDefinitionText(lines, "fxlistreal.txt").Single();

        Assert.Equal("66000004", fx.LittleEndianHex);
        Assert.Equal("04000066", fx.BigEndianHex);
        Assert.Equal("fx_npc.fxl | oligarchs_spawns_activeaccelerated", fx.Name);
        Assert.Equal(1, fx.Line);
    }

    private static byte[] FixedAscii(string value, int size)
    {
        byte[] bytes = new byte[size];
        byte[] textBytes = System.Text.Encoding.ASCII.GetBytes(value);
        Array.Copy(textBytes, bytes, Math.Min(textBytes.Length, size));
        return bytes;
    }

    private static byte[] BuildSingleRpcPacket(string headerHex, IReadOnlyList<byte> payload)
    {
        byte[] header = PacketResearcher.ParseHexBytes(headerHex).ToArray();
        int rpcBytes = header.Length + payload.Count;
        Assert.InRange(rpcBytes, 1, 127);
        return new byte[] { 0x02, 0x04, 0x01, 0x00, 0x00, 0x01, (byte)rpcBytes }
            .Concat(header)
            .Concat(payload)
            .ToArray();
    }
}
