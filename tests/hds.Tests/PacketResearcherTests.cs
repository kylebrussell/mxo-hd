using System.Globalization;
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
    public void SummarizePacketDump_TreatsWorldDirectionAsGameDirection()
    {
        string tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllLines(tempFile, new[]
            {
                "WORLD->Client [15:30:56 07/31/09] packet size: 13",
                "PSS: 7f LocalSeq: 1039 RemoteSeq: 1099",
                "02 04 01 00 01 01 06 81 0d 11 00 00 02",
                ".  .  .  .  .  .  .  .  .  .  .  .  ."
            });
            RpcHeaderEntry[] local =
            {
                new("mxo-hd", "CR2", "server", "RPCResponseHeaders", "SERVER_VENDOR_OPEN", 0x810d, "81 0d", "local.cs", 1)
            };

            PacketDumpFileSummary summary = PacketResearcher.SummarizePacketDump(tempFile, local);

            string rpcLabel = "81 0d [server] CR2:SERVER_VENDOR_OPEN";
            string knownLabel = "81 0d SERVER_VENDOR_OPEN [CR2 server]";
            Assert.Equal(1, summary.PacketLikeLines);
            Assert.Equal(1, summary.RpcHeaderCounts[rpcLabel]);
            Assert.Equal(1, summary.KnownHeaderCounts[knownLabel]);

            KnownHeaderContextSummary context = Assert.Single(summary.KnownHeaderContexts!);
            Assert.Equal(knownLabel, context.Label);
            Assert.Equal("server", context.PacketDirection);
            Assert.Equal("0x04", context.PacketProtocol);
            Assert.Equal("0x04", context.LineProtocol);
            Assert.Equal(1, context.Count);
            Assert.Equal(new[] { 3 }, context.SampleLines);
            KnownHeaderWindowSummary window = Assert.Single(context.Windows!);
            Assert.Equal(7, window.Offset);
            Assert.Equal("04 01 00 01 01 06", window.PrefixHex);
            Assert.Equal("11 00 00 02", window.SuffixHex);
            Assert.Equal(1, window.Count);
            Assert.Equal(new[] { 3 }, window.SampleLines);
            Assert.Equal(new[] { 1 }, window.SamplePacketStartLines);
            KnownHeaderWindowLocation location = Assert.Single(window.SampleLocations!);
            Assert.Equal(3, location.Line);
            Assert.Equal(1, location.PacketStartLine);
            Assert.Equal(7, location.PacketOffset);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void SummarizePacketDump_RecordsTopLevelVendorTransactionFlows()
    {
        string tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllLines(tempFile, new[]
            {
                "Client->WORLD [15:30:56 07/31/09] packet size: 11",
                "02 04 01 00 57 01 04 81 11 00 58",
                "WORLD->Client [15:30:57 07/31/09] packet size: 9",
                "02 04 01 00 01 01 02 5e 99"
            });
            RpcHeaderEntry[] local =
            {
                new("mxo-hd", "CR2", "client", "RPCRequestHeader", "CLIENT_VENDOR_SELL", 0x111, "81 11", "local.cs", 1)
            };

            PacketDumpFileSummary summary = PacketResearcher.SummarizePacketDump(tempFile, local);

            Assert.Equal(2, summary.Protocol04PacketSequences.Count);
            Assert.Equal("client", summary.Protocol04PacketSequences[0].Direction);
            Assert.Equal(new[] { "81 11" }, summary.Protocol04PacketSequences[0].Headers);
            Assert.Equal("server", summary.Protocol04PacketSequences[1].Direction);
            Assert.Equal(new[] { "5e" }, summary.Protocol04PacketSequences[1].Headers);

            VendorTransactionFlowSample flow = Assert.Single(summary.VendorTransactionFlows!);
            Assert.Equal(1, flow.Line);
            Assert.Equal("client", flow.Direction);
            Assert.Equal("81 11", flow.VendorHeader);
            Assert.Equal("CR2:CLIENT_VENDOR_SELL", flow.VendorLocalName);
            Assert.Equal(2, flow.VendorPayloadLength);
            Assert.Equal("00 58", flow.VendorPayloadPrefixHex);
            Assert.Equal("-", flow.VendorPayloadSuffixHex);
            Assert.Equal("00 58", flow.VendorPayloadHex);
            Protocol04RpcFlowBlockSample block = Assert.Single(flow.Blocks);
            Assert.Equal(0, block.Index);
            Assert.Equal("81 11", block.Header);
            Assert.Equal(2, block.PayloadLength);
            Assert.Equal("00 58", block.PayloadPrefixHex);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void SummarizePacketDump_RecordsVendorOpenItemLists()
    {
        string tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllLines(tempFile, new[]
            {
                "WORLD->Client [15:30:56 07/31/09] packet size: 49",
                "02 04 01 00 01 01 2a 81 0d 7c ad d9 43 00 00 00 00 dd c8 ef c0 00 00 00 00 00 c0 57 40 00 00 00 e0 45 a8 d2 40 20 00 02 00 c1 1e 00 00 c1 09 00 00"
            });
            RpcHeaderEntry[] local =
            {
                new("mxo-hd", "CR2", "server", "RPCResponseHeaders", "SERVER_VENDOR_OPEN", 0x810d, "81 0d", "local.cs", 1)
            };

            PacketDumpFileSummary summary = PacketResearcher.SummarizePacketDump(tempFile, local);

            VendorOpenItemListSample openList = Assert.Single(summary.VendorOpenItemLists!);
            Assert.Equal(1, openList.Line);
            Assert.Equal("server", openList.Direction);
            Assert.Equal("81 0d", openList.VendorHeader);
            Assert.Equal("CR2:SERVER_VENDOR_OPEN", openList.VendorLocalName);
            Assert.Equal(40, openList.PayloadLength);
            Assert.Equal("7c ad d9 43 00 00 00 00 dd c8 ef c0 00 00 00 00 00 c0 57 40 00 00 00 e0 45 a8 d2 40 20 00 02 00", openList.HeaderHex);
            Assert.Equal("20 00", openList.ListOffsetFieldHex);
            Assert.Equal((ushort)32, openList.ListOffset!.Value);
            Assert.InRange(openList.HeaderFloat0!.Value, 435.355f, 435.356f);
            Assert.InRange(openList.HeaderFloat8!.Value, -7.494f, -7.493f);
            Assert.InRange(openList.HeaderFloat16!.Value, 3.371f, 3.372f);
            Assert.InRange(openList.HeaderFloat24!.Value, 6.583f, 6.584f);
            Assert.Equal("00 00 00 00", openList.HeaderWord4Hex);
            Assert.Equal("00 00 00 00", openList.HeaderWord12Hex);
            Assert.Equal("00 00 00 e0", openList.HeaderWord20Hex);
            Assert.Equal("02 00", openList.CountFieldHex);
            Assert.Equal(2, openList.DeclaredCount);
            Assert.Equal(2, openList.ParsedIdCount);
            Assert.Equal(new uint[] { 7873, 2497 }, openList.ItemIds);
            Assert.Equal("c1 1e 00 00 c1 09 00 00", openList.PayloadSuffixHex);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void SummarizePacketDump_RecordsVendorBuyTransactions()
    {
        string tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllLines(tempFile, new[]
            {
                "Client->WORLD [15:30:56 07/31/09] packet size: 14",
                "02 04 01 00 57 01 07 81 0e c1 09 00 00 00",
                "WORLD->Client [15:30:57 07/31/09] packet size: 58",
                "02 04 01 00 01 04 0c 5e 01 00 c1 09 00 00 00 00 00 10 01 0e 80 e7 d0 8a ff ff ff ff ff ff 08 00 00 00 0a 80 e4 9c 27 52 ee 08 00 00 00 0f 81 0f c1 09 00 00 00 00 00 10 30 75 00 00 00"
            });
            RpcHeaderEntry[] local =
            {
                new("mxo-hd", "CR2", "client", "RPCRequestHeader", "CLIENT_VENDOR_BUY", 0x010e, "81 0e", "local.cs", 1)
            };

            PacketDumpFileSummary summary = PacketResearcher.SummarizePacketDump(tempFile, local);

            VendorBuyTransactionSample buy = Assert.Single(summary.VendorBuyTransactions!);
            Assert.Equal(1, buy.Line);
            Assert.Equal("client", buy.Direction);
            Assert.Equal("c1 09 00 00 00", buy.BuyPayloadPrefixHex);
            Assert.Equal("c1 09 00 00", buy.ItemIdFieldHex);
            Assert.Equal((uint)2497, buy.ItemId);
            Assert.Equal(3, buy.NextLine);
            Assert.Equal("server", buy.NextDirection);
            Assert.Equal(new[] { "5e", "80 e7", "80 e4", "81 0f" }, buy.NextHeaders);
            VendorBuyAckSample ack = Assert.Single(buy.BuyAcks);
            Assert.Equal("5e", ack.Header);
            Assert.Equal("5e buy payload echo", ack.Layout);
            Assert.Equal("01 00 c1 09 00 00 00 00 00 10 01", ack.PayloadPrefixHex);
            Assert.Equal("01 00", ack.SequenceFieldHex);
            Assert.Equal((ushort)1, ack.Sequence);
            Assert.Equal("c1 09 00 00 00", ack.EchoFieldHex);
            Assert.True(ack.EchoMatchesRequest);
            Assert.Equal("10 01", ack.TailMarkerHex);
            Assert.Equal(new long[] { -30000 }, buy.CashDeltas);
            Assert.Equal(new uint[] { 30000 }, buy.BuyValues);
            Assert.Equal(new uint[] { 8, 8 }, buy.ValueKinds);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void SummarizePacketDump_RecordsVendorBuyTransactionsWithHeader69Ack()
    {
        string tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllLines(tempFile, new[]
            {
                "Client->WORLD [15:30:56 07/31/09] packet size: 14",
                "02 04 01 00 59 01 07 81 0e 01 a8 00 00 00",
                "WORLD->Client [15:30:57 07/31/09] packet size: 58",
                "02 04 01 01 77 04 0a 69 01 36 00 00 00 04 00 00 00 0e 80 e7 9c ff ff ff ff ff ff ff 08 00 00 00 0a 80 e4 1a a6 f2 cc 08 00 00 00 0f 81 0f 01 a8 00 00 00 00 00 00 64 00 00 00 00"
            });
            RpcHeaderEntry[] local =
            {
                new("mxo-hd", "CR2", "client", "RPCRequestHeader", "CLIENT_VENDOR_BUY", 0x010e, "81 0e", "local.cs", 1)
            };

            PacketDumpFileSummary summary = PacketResearcher.SummarizePacketDump(tempFile, local);

            VendorBuyTransactionSample buy = Assert.Single(summary.VendorBuyTransactions!);
            Assert.Equal("01 a8 00 00 00", buy.BuyPayloadPrefixHex);
            Assert.Equal("01 a8 00 00", buy.ItemIdFieldHex);
            Assert.Equal((uint)43009, buy.ItemId);
            Assert.Equal(new[] { "69", "80 e7", "80 e4", "81 0f" }, buy.NextHeaders);
            VendorBuyAckSample ack = Assert.Single(buy.BuyAcks);
            Assert.Equal("69", ack.Header);
            Assert.Equal("69 candidate buy ack fields", ack.Layout);
            Assert.Equal("01 36 00 00 00 04 00 00 00", ack.PayloadPrefixHex);
            Assert.Equal("36 00", ack.SequenceFieldHex);
            Assert.Equal((ushort)54, ack.Sequence);
            Assert.Equal("36 00 00 00", ack.EchoFieldHex);
            Assert.False(ack.EchoMatchesRequest);
            Assert.Equal("04 00 00 00", ack.TailMarkerHex);
            Assert.Equal("04 00 00 00", ack.SecondaryFieldHex);
            Assert.Equal((uint)4, ack.SecondaryFieldValue);
            Assert.Equal(new long[] { -100 }, buy.CashDeltas);
            Assert.Equal(new uint[] { 100 }, buy.BuyValues);
            Assert.Equal(new uint[] { 8, 8 }, buy.ValueKinds);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void SummarizePacketDump_RecordsVendorSellTransactions()
    {
        string tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllLines(tempFile, new[]
            {
                "Client->WORLD [15:30:56 07/31/09] packet size: 11",
                "02 04 01 00 57 01 04 81 11 00 58",
                "WORLD->Client [15:30:57 07/31/09] packet size: 51",
                "02 04 01 00 01 04 0e 80 e7 88 13 00 00 00 00 00 00 07 00 00 00 0a 80 e4 97 e1 7a 00 07 00 00 00 04 5f 58 00 01 0f 81 12 00 34 a8 00 00 00 00 00 34 88 13 00 00"
            });
            RpcHeaderEntry[] local =
            {
                new("mxo-hd", "CR2", "client", "RPCRequestHeader", "CLIENT_VENDOR_SELL", 0x0111, "81 11", "local.cs", 1)
            };

            PacketDumpFileSummary summary = PacketResearcher.SummarizePacketDump(tempFile, local);

            VendorSellTransactionSample sell = Assert.Single(summary.VendorSellTransactions!);
            Assert.Equal(1, sell.Line);
            Assert.Equal("client", sell.Direction);
            Assert.Equal("00 58", sell.SellPayloadPrefixHex);
            Assert.Equal(3, sell.NextLine);
            Assert.Equal("server", sell.NextDirection);
            Assert.Equal(new[] { "80 e7", "80 e4", "5f", "81 12" }, sell.NextHeaders);
            VendorSellAckSample ack = Assert.Single(sell.SellAcks);
            Assert.Equal("58 00 01", ack.PayloadPrefixHex);
            Assert.Equal("58 00", ack.SellItemFieldHex);
            Assert.Equal((ushort)88, ack.SellItemValue);
            Assert.Equal("01", ack.FlagHex);
            Assert.Equal((byte)1, ack.Flag);
            Assert.Equal("00 34 a8 00 00 00 00 00 34 88 13 00 00", sell.SellResponsePayloadPrefixHex);
            Assert.Equal("34 a8 00 00", sell.NormalItemFieldHex);
            Assert.Equal((uint)43060, sell.NormalItemId);
            Assert.Equal("00 00 00 34", sell.AbilityFieldHex);
            Assert.Equal((uint)872415232, sell.AbilityId);
            Assert.Equal((uint)872415232, sell.AbilityBaseId);
            Assert.Equal(new long[] { 5000 }, sell.CashDeltas);
            Assert.Equal(new uint[] { 5000 }, sell.SellValues);
            Assert.Equal(new uint[] { 7, 7 }, sell.ValueKinds);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void BuildVendorBuyPriceLeadSummaries_LabelsHeader69AckDetail()
    {
        PacketDumpFileSummary dump = CreateEmptyPacketDump("buy-flow-69.txt")
            with
            {
                VendorBuyTransactions = new[]
                {
                    new VendorBuyTransactionSample(
                        20,
                        "client",
                        "01 a8 00 00 00",
                        "01 a8 00 00",
                        43009,
                        22,
                        "server",
                        new[] { "69", "80 e7", "80 e4", "81 0f" },
                        new[]
                        {
                            new VendorBuyAckSample(
                                "01 36 00 00 00 04 00 00 00",
                                "36 00",
                                54,
                                "36 00 00 00",
                                false,
                                "04 00 00 00")
                            {
                                Header = "69",
                                Layout = "69 candidate buy ack fields",
                                SecondaryFieldHex = "04 00 00 00",
                                SecondaryFieldValue = 4
                            }
                        },
                        new long[] { -100 },
                        new uint[] { 100 },
                        new uint[] { 8, 8 })
                }
            };

        VendorBuyPriceLeadSummary summary = Assert.Single(PacketResearcher.BuildVendorBuyPriceLeadSummaries(
            new[] { dump },
            new[] { new ItemCommandEntry(43009, "WinterSnowballIcy", "Icy Snowball", "items.txt", 1) },
            new[] { new VendorPriceEntry(43009, "WinterSnowballIcy", 100, true, "prices.csv", 2) },
            new[] { new VendorInventoryEntry(1, 888143880, new uint[] { 43009 }, "vendor_items.csv", 3) }));

        Assert.Equal("01 a8 00 00 00", summary.BuyPayloadPrefixHex);
        Assert.Equal((uint)43009, summary.ItemId);
        Assert.Equal("WinterSnowballIcy", summary.ItemSymbol);
        Assert.Equal("cash and buy value", summary.PriceMatch);
        Assert.Equal(new ushort[] { 54 }, summary.BuyAckSequences);
        Assert.Equal(new[] { "69 +5 04 00 00 00" }, summary.BuyAckTailMarkers);
        Assert.Equal(new long[] { -100 }, summary.CashDeltas);
        Assert.Equal(new uint[] { 100 }, summary.BuyValues);
        Assert.Equal(new uint[] { 8, 8 }, summary.ValueKinds);
    }

    [Fact]
    public void BuildVendorBuyPriceLeadSummaries_JoinsTransactionEvidenceToPrices()
    {
        PacketDumpFileSummary dump = CreateEmptyPacketDump("buy-flow.txt")
            with
            {
                VendorBuyTransactions = new[]
                {
                    new VendorBuyTransactionSample(
                        20,
                        "client",
                        "c1 09 00 00 00",
                        "c1 09 00 00",
                        2497,
                        22,
                        "server",
                        new[] { "5e", "80 e7", "80 e4", "81 0f" },
                        new[]
                        {
                            new VendorBuyAckSample(
                                "01 00 c1 09 00 00 00 00 00 10 01",
                                "01 00",
                                1,
                                "c1 09 00 00 00",
                                true,
                                "10 01")
                        },
                        new long[] { -30000 },
                        new uint[] { 30000 },
                        new uint[] { 8, 8 })
                }
            };

        VendorBuyPriceLeadSummary summary = Assert.Single(PacketResearcher.BuildVendorBuyPriceLeadSummaries(
            new[] { dump },
            new[] { new ItemCommandEntry(2497, "DualPistol7", "Twin WestTek 49SAs", "items.txt", 1) },
            new[] { new VendorPriceEntry(2497, "DualPistol7", 30000, false, "prices.csv", 2) },
            new[] { new VendorInventoryEntry(1, 888143880, new uint[] { 2497 }, "vendor_items.csv", 3) }));

        Assert.Equal("c1 09 00 00 00", summary.BuyPayloadPrefixHex);
        Assert.Equal("c1 09 00 00", summary.ItemIdFieldHex);
        Assert.Equal((uint)2497, summary.ItemId);
        Assert.Equal("DualPistol7", summary.ItemSymbol);
        Assert.Equal("Twin WestTek 49SAs", summary.ItemDisplayName);
        Assert.Equal((uint)30000, summary.VendorPrice);
        Assert.Equal("cash and buy value", summary.PriceMatch);
        Assert.Equal(1, summary.TransactionCount);
        Assert.Equal(new ushort[] { 1 }, summary.BuyAckSequences);
        Assert.Equal(new[] { "10 01" }, summary.BuyAckTailMarkers);
        Assert.Equal(new long[] { -30000 }, summary.CashDeltas);
        Assert.Equal(new uint[] { 30000 }, summary.BuyValues);
        Assert.Equal(new uint[] { 8, 8 }, summary.ValueKinds);
        Assert.Equal(1, summary.VendorInventoryRowCount);
        VendorTransactionSampleLocation sample = Assert.Single(summary.Samples);
        Assert.Equal("buy-flow.txt", sample.File);
        Assert.Equal(20, sample.Line);
        Assert.Equal(22, sample.NextLine);
    }

    [Fact]
    public void BuildVendorSellPriceLeadSummaries_JoinsTransactionEvidenceToBuybackPrices()
    {
        PacketDumpFileSummary dump = CreateEmptyPacketDump("sell-flow.txt")
            with
            {
                VendorSellTransactions = new[]
                {
                    new VendorSellTransactionSample(
                        20,
                        "client",
                        "00 58",
                        22,
                        "server",
                        new[] { "80 e7", "80 e4", "5f", "81 12" },
                        new[]
                        {
                            new VendorSellAckSample("58 00 01", "58 00", 88, "01", 1)
                        },
                        "00 34 a8 00 00 00 00 00 34 88 13 00 00",
                        "34 a8 00 00",
                        43060,
                        "00 00 00 34",
                        872415232,
                        872415232,
                        new long[] { 5000 },
                        new uint[] { 5000 },
                        new uint[] { 7, 7 }),
                    new VendorSellTransactionSample(
                        40,
                        "client",
                        "00 58",
                        42,
                        "server",
                        new[] { "80 e7", "80 e4", "5f", "81 12" },
                        new[]
                        {
                            new VendorSellAckSample("58 00 01", "58 00", 88, "01", 1)
                        },
                        "00 2e 00 00 00 01 08 00 80 0a 00 00 00",
                        "2e 00 00 00",
                        46,
                        "01 08 00 80",
                        2147485697,
                        2147485696,
                        new long[] { 10 },
                        new uint[] { 10 },
                        new uint[] { 7, 7 })
                }
            };

        VendorSellPriceLeadSummary[] summaries = PacketResearcher.BuildVendorSellPriceLeadSummaries(
                new[] { dump },
                new[] { new AbilityDefinition(2147485696, -2147481600, "AwakenedAbility", "Awakened", "abilities.txt", 1) },
                new[] { new ItemCommandEntry(43060, "Submachinegun11", "FM-13SK", "items.txt", 2) },
                new[]
                {
                    new VendorPriceEntry(43060, "Submachinegun11", 50000, false, "prices.csv", 3),
                    new VendorPriceEntry(2147485696, "AwakenedAbility", 100, false, "prices.csv", 4)
                },
                new[] { new VendorInventoryEntry(1, 888143880, new uint[] { 43060 }, "vendor_items.csv", 5) })
            .ToArray();

        VendorSellPriceLeadSummary normalItem = Assert.Single(summaries, summary => !summary.IsAbility);
        Assert.Equal("+1 normal item u32", normalItem.ResponseField);
        Assert.Equal((uint)43060, normalItem.ResponseId);
        Assert.Equal((uint)43060, normalItem.PriceId);
        Assert.Equal("Submachinegun11", normalItem.ReferenceCodeName);
        Assert.Equal("FM-13SK", normalItem.ReferenceDisplayName);
        Assert.Equal((uint)50000, normalItem.VendorPrice);
        Assert.Equal((uint)5000, normalItem.ExpectedSell);
        Assert.Equal("cash and sell value", normalItem.PriceMatch);
        Assert.Equal(1, normalItem.VendorInventoryRowCount);
        Assert.Equal(new ushort[] { 88 }, normalItem.SellAckItemValues);
        Assert.Equal(new byte[] { 1 }, normalItem.SellAckFlags);

        VendorSellPriceLeadSummary ability = Assert.Single(summaries, summary => summary.IsAbility);
        Assert.Equal("+5 ability code u32", ability.ResponseField);
        Assert.Equal((uint)2147485697, ability.ResponseId);
        Assert.Equal((uint)2147485696, ability.PriceId);
        Assert.Equal("AwakenedAbility", ability.ReferenceCodeName);
        Assert.Equal("Awakened", ability.ReferenceDisplayName);
        Assert.Equal((uint)100, ability.VendorPrice);
        Assert.Equal((uint)10, ability.ExpectedSell);
        Assert.Equal("cash and sell value", ability.PriceMatch);
        Assert.Equal(0, ability.VendorInventoryRowCount);
        Assert.Equal(new ushort[] { 88 }, ability.SellAckItemValues);
        Assert.Equal(new byte[] { 1 }, ability.SellAckFlags);
    }

    [Fact]
    public void BuildVendorSellRequestAddressingSummaries_ClassifiesCrossFileStableFieldAsGlobalIdentifier()
    {
        VendorSellTransactionSample MakeSell(string requestPrefix, uint normalItemId) => new VendorSellTransactionSample(
            20,
            "client",
            requestPrefix,
            22,
            "server",
            new[] { "80 e7", "80 e4", "5f", "81 12" },
            new[] { new VendorSellAckSample("58 00 01", "58 00", 88, "01", 1) },
            "00 00 00 00 00 00 00 00 00 00 00 00 00",
            "field",
            normalItemId,
            "00 00 00 00",
            0,
            0,
            new long[] { 5000 },
            new uint[] { 5000 },
            new uint[] { 7, 7 });

        // Request field "00 55" resolves to item 43060 in BOTH files (cross-file stable -> global).
        // Request field "00 52" resolves to a different item 43061 (single file).
        PacketDumpFileSummary fileA = CreateEmptyPacketDump("session-a.txt")
            with
            {
                VendorSellTransactions = new[]
                {
                    MakeSell("00 55", 43060),
                    MakeSell("00 52", 43061)
                }
            };
        PacketDumpFileSummary fileB = CreateEmptyPacketDump("session-b.txt")
            with
            {
                VendorSellTransactions = new[]
                {
                    MakeSell("00 55", 43060)
                }
            };

        VendorSellRequestAddressingSummary[] summaries = PacketResearcher.BuildVendorSellRequestAddressingSummaries(
                new[] { fileA, fileB },
                Array.Empty<AbilityDefinition>(),
                new[]
                {
                    new ItemCommandEntry(43060, "Submachinegun11", "FM-13SK", "items.txt", 2),
                    new ItemCommandEntry(43061, "Pistol07", "Beretta", "items.txt", 3)
                })
            .ToArray();

        VendorSellRequestAddressingSummary stable = Assert.Single(summaries, summary => summary.RequestFieldHex == "00 55");
        Assert.Equal(0x55, stable.RequestFieldSecondByte);
        Assert.Equal(1, stable.DistinctItemCount);
        Assert.Equal(2, stable.DistinctFileCount);
        Assert.Equal(2, stable.TransactionCount);
        Assert.Equal("global item identifier", stable.Classification);
        Assert.Equal(new[] { "FM-13SK" }, stable.ResolvedItemNames);

        VendorSellRequestAddressingSummary singleFile = Assert.Single(summaries, summary => summary.RequestFieldHex == "00 52");
        Assert.Equal(0x52, singleFile.RequestFieldSecondByte);
        Assert.Equal(1, singleFile.DistinctItemCount);
        Assert.Equal(1, singleFile.DistinctFileCount);
        Assert.Equal("stable-single-file", singleFile.Classification);

        // Ordered by second byte: 0x52 before 0x55.
        Assert.Equal(new[] { "00 52", "00 55" }, summaries.Select(s => s.RequestFieldHex).ToArray());
    }

    [Fact]
    public void ReportWriter_SeparatesVendorTopLevelRpcHitsFromEmbeddedByteHits()
    {
        const string rpcLabel = "81 0d [server] CR2:SERVER_VENDOR_OPEN";
        const string knownLabel = "81 0d SERVER_VENDOR_OPEN [CR2 server]";
        PacketDumpFileSummary dump = new(
            "vendor.txt",
            2,
            new Dictionary<string, int>(),
            new Dictionary<string, int> { [rpcLabel] = 1 },
            new Dictionary<string, int> { [knownLabel] = 2 },
            new Dictionary<string, IReadOnlyList<int>> { [knownLabel] = new[] { 7, 8 } },
            Array.Empty<Protocol04PacketSequenceSample>(),
            Array.Empty<Protocol03ObjectViewSample>(),
            Array.Empty<Protocol04InteractionPayloadSample>(),
            Array.Empty<PlayerAttributePayloadSample>(),
            Array.Empty<ManageBonusPayloadSample>())
        {
            KnownHeaderContexts = new[]
            {
                new KnownHeaderContextSummary(knownLabel, "server", "0x03", "unknown", 2, new[] { 7, 8 })
                {
                    Windows = new[]
                    {
                        new KnownHeaderWindowSummary(5, "02 03 2f 00 01", "e5 c6 00 48", 2, new[] { 7, 8 }, new[] { 6 })
                        {
                            SampleLocations = new[]
                            {
                                new KnownHeaderWindowLocation(7, 6, 5),
                                new KnownHeaderWindowLocation(8, 6, 5)
                            }
                        }
                    }
                }
            }
        };
        Protocol03ObjectViewSample protocol03 = new(
            6,
            "server",
            "02",
            37,
            3,
            "0c",
            "unclassified object-view update",
            "25 00 03 0c 93 78",
            Array.Empty<Protocol03StringSample>(),
            Array.Empty<Protocol03NamedProfileRecord>(),
            Array.Empty<Protocol03PlayerCharacterCreationRecord>(),
            Array.Empty<Protocol03EffectEmoteLead>(),
            Array.Empty<Protocol03PositionLikeLead>(),
            Array.Empty<Protocol03NestedMovementLead>(),
            Array.Empty<Protocol03MovementStateTailLead>(),
            new[]
            {
                new Protocol03VariableSelectorLead(
                    "ff",
                    18,
                    96,
                    "01 43 00 00 00 00 00 00",
                    "01 43 00 00 00 00 00 00 00 e0 2d 81 0d 22 89 20 00 0b 0c 11 79 80 00 00 01 00 00 00 00 12 01 00 02 ff 00 00 00 00 ba 13 63 00 00 87 43 00 00 c3 96 63 07 00 00 00 c0 e2 2e d1 40 00 00 00 00 00 f0 7e 40 00 00 00 00 02 1f a3 40 ff 00 00 00 00 00 00 00 00 00 00 a0 13 00 00 00 00 00 ff ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 01 00 ff 00 00 00 00 00 00 00 00 00 00 08 00")
            },
            Array.Empty<Protocol03SelfViewAttributeLead>(),
            Array.Empty<Protocol03StaticObjectLead>(),
            1,
            155,
            false,
            new[]
            {
                new Protocol03ObjectUpdateSegment("0c", "position xyz + flag", 13, 3, "93 78", true),
                new Protocol03ObjectUpdateSegment("ff", "unknown selector payload", 155, 17, "01 43 00 00 00 00 00 00 00 e0 2d 81 0d", false)
            })
        {
            PacketOffset = 0
        };
        Protocol03ObjectViewSample playerProtocol03 = new(
            9,
            "server",
            "02",
            38,
            12,
            "0c",
            "player spawn/self-view candidate",
            "26 00 0c 0c",
            Array.Empty<Protocol03StringSample>(),
            Array.Empty<Protocol03NamedProfileRecord>(),
            new[]
            {
                new Protocol03PlayerCharacterCreationRecord(
                    0,
                    "0c 0c 00 01 cd ab 02 80",
                    "0c",
                    "0c 00",
                    12,
                    "01",
                    "cd ab",
                    2,
                    "80",
                    37,
                    string.Empty,
                    new[]
                    {
                        new Protocol03CreationAttributeSample(37, "CharacterName", 32, 0, "56 65 6e 64 6f 72 54 65 73 74 65 72 00"),
                        new Protocol03CreationAttributeSample(49, "Position", 24, 32, "00 00 00 c0 e2 2e d1 40 00 00 00 00 00 f0 7e 40 00 00 00 00 02 1f a3 40")
                    })
            },
            Array.Empty<Protocol03EffectEmoteLead>(),
            Array.Empty<Protocol03PositionLikeLead>(),
            Array.Empty<Protocol03NestedMovementLead>(),
            Array.Empty<Protocol03MovementStateTailLead>(),
            Array.Empty<Protocol03VariableSelectorLead>(),
            Array.Empty<Protocol03SelfViewAttributeLead>(),
            Array.Empty<Protocol03StaticObjectLead>(),
            1,
            0,
            true,
            Array.Empty<Protocol03ObjectUpdateSegment>())
        {
            PacketOffset = 0
        };
        PacketResearchReport report = new(
            ".",
            Array.Empty<string>(),
            null,
            Array.Empty<string>(),
            new[]
            {
                new RpcHeaderEntry("mxo-hd", "CR2", "server", "RPCResponseHeaders", "SERVER_VENDOR_OPEN", 0x810d, "81 0d", "headers.cs", 42)
            },
            Array.Empty<AttributeDefinition>(),
            Array.Empty<FxDefinition>(),
            Array.Empty<GameObjectEntry>(),
            new[]
            {
                new WorldEntityEntry("mob_parsed_untouched.csv", "Vendor Stall", "slums", null, "npc spawn", null, "data/mob.csv", 12, null, null, 17596.0, 495.0, 2447.5)
            },
            Array.Empty<RajkoRpcEntry>(),
            Array.Empty<HardcodedCommandExample>(),
            Array.Empty<Protocol03HardcodedExample>(),
            Array.Empty<Protocol04InteractionCommandExample>(),
            Array.Empty<VendorInventoryEntry>(),
            Array.Empty<RpcComparison>(),
            new[]
            {
                dump with { Protocol03ObjectViews = new[] { protocol03, playerProtocol03 } }
            });

        string markdown = ReportWriter.ToMarkdown(report);

        Assert.Contains("Parsed top-level RPC hits come from protocol 04 block parsing.", markdown);
        Assert.Contains("| Protocol | Local name | Direction | Value | Encoded header | Parsed top-level RPC hits | Embedded byte hits | Top embedded contexts | Sample files |", markdown);
        Assert.Contains("| CR2 | SERVER_VENDOR_OPEN | server | `810d` | `81 0d` | 1 | 2 | server packet 0x03, line unknown: 2 | `vendor.txt:7,8` |", markdown);
        Assert.Contains("### Vendor Embedded Byte Windows", markdown);
        Assert.Contains("| Protocols | Local name | Direction | Context | Offset | Prefix | Header | Suffix | Count | Containing protocol 03 segment | Sample lines |", markdown);
        Assert.Contains("| CR2 | SERVER_VENDOR_OPEN | server | server packet 0x03, line unknown | 5 | `02 03 2f 00 01` | `81 0d` | `e5 c6 00 48` | 2 | count 3, first 0c, unclassified object-view update; segment 0c position xyz + flag at +5 | `vendor.txt:7`<br>`vendor.txt:8` |", markdown);
        Assert.Contains("### Protocol 03 Variable Selector Tail Families", markdown);
        Assert.Contains("Across 1 non-`80` variable selector tails, 1 are still classified as unknown selector payloads, 1 contain at least one known header window, and 1 contain an embedded vendor header window.", markdown);
        Assert.Contains("| `ff` | unknown selector payload | 3 | `0c` | `01 43 00 00 00 00 00 00 00 e0 2d 81 0d 22 89 20` | 1 | 96 (1) | payload +11/object +29 (1) SERVER_VENDOR_OPEN [CR2 server] `81 0d` (1) | payload +9 `e0 2d 81 0d` SERVER_VENDOR_OPEN (1) | `vendor.txt:6` |", markdown);
        Assert.Contains("### Protocol 03 Selector ff Packed Field Leads", markdown);
        Assert.Contains("Across 1 selector `ff` tails, 1 are still classified as unknown selector payloads and 1 contain at least one known header window.", markdown);
        Assert.Contains("| unknown selector payload | 3 | `0c` | `01 43 00 00 00 00 00 00 00` | `e0 2d 81 0d` / 226569696 | - | `22 89 20` | `00 0b 0c 11 79 80 00 00` | `01 00 00 00 00 12 01 00` | `00 02 ff 00 00 00 00 ba` | 1 | 96 (1) | payload +11/object +29 (1) SERVER_VENDOR_OPEN [CR2 server] `81 0d` (1) | `vendor.txt:6` |", markdown);
        Assert.Contains("### Protocol 03 Selector ff Position-Like Leads", markdown);
        Assert.Contains("Across 1 selector `ff` tails, 1 expose at least one plausible little-endian xyz triple.", markdown);
        Assert.Contains("| Segment classification | Update count | First selector | Lead +0 | u32 +9 | Position offset | Pre-position bytes | Post-position bytes | Tail after position | Records | Payload bytes | Position samples | Nearest decoded-position hints | Header windows | Sample |", markdown);
        Assert.Contains("| unknown selector payload | 3 | `0c` | `01 43 00 00 00 00 00 00 00` | `e0 2d 81 0d` / 226569696 | 51 | `87 43 00 00 c3 96 63 07` |", markdown);
        Assert.Contains("17595.543,495,2447.504 -> player VendorTester 0 @ 17595.543,495,2447.504; world Vendor Stall slums / npc spawn 0.5 @ 17596,495,2447.5", markdown);
        Assert.Contains("### Protocol 03 Selector ff Post-Position Tail Leads", markdown);
        Assert.Contains("Across 1 selector `ff` position leads, 1 have bytes after the xyz triple.", markdown);
        Assert.Contains("| unknown selector payload | 3 | `0c` | 51 | `ff` | 10 | 11 | `a0 13` / 5024 |", markdown);
        Assert.Contains("`ff ff 00 00 00 00 00 00`", markdown);
        Assert.Contains("### Protocol 03 Selector ff Post-Position Repeat-Stride Leads", markdown);
        Assert.Contains("Across 1 selector `ff` position tails with suffix bytes, 1 expose at least two `ff` markers at a consistent post-position stride.", markdown);
        Assert.Contains("| Segment classification | Update count | First selector | Position offset | Repeat stride | First record u16 | Second record lead | Repeat records | Record field sequence | Tail after position | Records | Position samples | Header windows | Sample |", markdown);
        Assert.Contains("| unknown selector payload | 3 | `0c` | 51 | 18 | `a0 13` / 5024 | `ff ff 00 00 00 00 00 00` | 2 (1) |", markdown);
        Assert.Contains("+0 z10 +11 'a0 13'/5024 -> +18 z0 +1 'ff 00'/255", markdown);
        Assert.Contains("### Protocol 03 Selector ff Post-Position Repeat Second-Entry Field Leads", markdown);
        Assert.Contains("Across 1 repeat-stride tails, 1 expose a second-entry local field within the repeated stride.", markdown);
        Assert.Contains("| unknown selector payload | 3 | `0c` | 51 | 18 | `a0 13` / 5024 | 0 | 1 | `ff 00` / 255 | ff ff 00 00 00 00 00 00 | 2 (1) | 1 | 17595.543,495,2447.504 | payload +11/object +29 (1) SERVER_VENDOR_OPEN [CR2 server] `81 0d` (1) | `vendor.txt:6` |", markdown);
        Assert.Contains("### Protocol 03 Selector ff Post-Position Repeat Entry Transition Leads", markdown);
        Assert.Contains("Across 1 repeat-stride tails, 1 expose paired entry-1 and entry-2 local fields.", markdown);
        Assert.Contains("| Segment classification | Update count | First selector | Position offset | Repeat stride | First record u16 | Entry 1 field | Entry 2 field | Entry 1 lead | Entry 2 lead | Repeat records | Records | Position samples | Header windows | Sample |", markdown);
        Assert.Contains("| unknown selector payload | 3 | `0c` | 51 | 18 | `a0 13` / 5024 | z10 +11 `a0 13` / 5024 | z0 +1 `ff 00` / 255 | ff 00 00 00 00 00 00 00 | ff ff 00 00 00 00 00 00 | 2 (1) | 1 | 17595.543,495,2447.504 | payload +11/object +29 (1) SERVER_VENDOR_OPEN [CR2 server] `81 0d` (1) | `vendor.txt:6` |", markdown);
        Assert.Contains("### Protocol 03 Selector ff Post-Position Repeat Continuation Leads", markdown);
        Assert.Contains("Across 1 paired repeat-stride tails, 1 have bytes after their repeated records.", markdown);
        Assert.Contains("| Segment classification | Update count | First selector | Position offset | Repeat stride | Entry 1 field | Entry 2 field | Repeat records | Continuation offset | Continuation bytes | Continuation prefix | Records | Position samples | Header windows | Sample |", markdown);
        Assert.Contains("| unknown selector payload | 3 | `0c` | 51 | 18 | z10 +11 `a0 13` / 5024 | z0 +1 `ff 00` / 255 | 2 (1) | 36 | 16 (1) | `00 01 00 ff 00 00 00 00 00 00 00 00 00 00 08 00` | 1 | 17595.543,495,2447.504 | payload +11/object +29 (1) SERVER_VENDOR_OPEN [CR2 server] `81 0d` (1) | `vendor.txt:6` |", markdown);
        Assert.Contains("### Protocol 03 Selector ff Post-Position Repeat Continuation Field Leads", markdown);
        Assert.Contains("Across 1 continuation bodies with at least four bytes, 1 have continuation byte `+3 = ff`.", markdown);
        Assert.Contains("| Segment classification | Update count | First selector | Position offset | Repeat stride | Entry 1 field | Entry 2 field | Continuation +0..+3 | u16 +4 | u16 +6 | u16 +8 | u16 +10 | u16 +12 | u16 +14 | Continuation bytes | Records | Position samples | Header windows | Sample |", markdown);
        Assert.Contains("| unknown selector payload | 3 | `0c` | 51 | 18 | z10 +11 `a0 13` / 5024 | z0 +1 `ff 00` / 255 | `00 01 00 ff` | `00 00` / 0 | `00 00` / 0 | `00 00` / 0 | `00 00` / 0 | `00 00` / 0 | `08 00` / 8 | 16 (1) | 1 | 17595.543,495,2447.504 | payload +11/object +29 (1) SERVER_VENDOR_OPEN [CR2 server] `81 0d` (1) | `vendor.txt:6` |", markdown);
        Assert.Contains("### Protocol 03 Selector ff Post-Position Repeat Continuation Marker Tail Leads", markdown);
        Assert.Contains("Across 1 continuation bodies with byte `+3 = ff`, 1 expose a post-marker u16 field.", markdown);
        Assert.Contains("| Segment classification | Update count | First selector | Position offset | Repeat stride | Entry 1 field | Entry 2 field | Continuation +0..+2 | Marker | Post-marker zero run | First post-marker field offset | First post-marker field | Post-marker lead | Continuation bytes | Records | Position samples | Header windows | Sample |", markdown);
        Assert.Contains("| unknown selector payload | 3 | `0c` | 51 | 18 | z10 +11 `a0 13` / 5024 | z0 +1 `ff 00` / 255 | `00 01 00` | `ff` | 10 | 14 | `08 00` / 8 | `00 00 00 00 00 00 00 00` | 16 (1) | 1 | 17595.543,495,2447.504 | payload +11/object +29 (1) SERVER_VENDOR_OPEN [CR2 server] `81 0d` (1) | `vendor.txt:6` |", markdown);
        Assert.Contains("### Protocol 03 Selector ff Post-Position Repeat Continuation Prefix Families", markdown);
        Assert.Contains("Across 1 continuation bodies with byte `+3 = ff`, 1 prefix/post-marker families appear and 1 expose a post-marker u16 field.", markdown);
        Assert.Contains("| Continuation +0..+2 | Marker | Post-marker zero run | First post-marker field offset | First post-marker field | Post-marker lead | Continuation bodies | Records | Segment classifications | Update counts | First selectors | Entry 1 fields | Entry 2 fields | Position samples | Header windows | Sample |", markdown);
        Assert.Contains("| `00 01 00` | `ff` | 10 | 14 | `08 00` / 8 | `00 00 00 00 00 00 00 00` | 1 | 1 | unknown selector payload | 3 (1) | 0c (1) | z10 +11 'a0 13' / 5024 | z0 +1 'ff 00' / 255 | 17595.543,495,2447.504 | payload +11/object +29 (1) SERVER_VENDOR_OPEN [CR2 server] `81 0d` (1) | `vendor.txt:6` |", markdown);
        Assert.Contains("### Protocol 03 Selector ff Post-Position Repeat List Candidate Leads", markdown);
        Assert.Contains("Across 1 continuation bodies with byte `+3 = ff`, 1 have exactly two repeated entries before the continuation body and 1 expose a post-marker u16 field.", markdown);
        Assert.Contains("| Repeat stride | Repeat entries | Continuation offset | Entry 1 field | Entry 2 field | Continuation +0..+2 | Marker | Post-marker zero run | First post-marker field offset | First post-marker field | Post-marker leads | Continuation bodies | Records | Segment classifications | Update counts | First selectors | Position samples | Header windows | Sample |", markdown);
        Assert.Contains("| 18 | 2 (1) | 36 | z10 +11 `a0 13` / 5024 | z0 +1 `ff 00` / 255 | `00 01 00` | `ff` | 10 | 14 | `08 00` / 8 | 00 00 00 00 00 00 00 00 | 1 | 1 | unknown selector payload | 3 (1) | 0c (1) | 17595.543,495,2447.504 | payload +11/object +29 (1) SERVER_VENDOR_OPEN [CR2 server] `81 0d` (1) | `vendor.txt:6` |", markdown);
        Assert.Contains("### Protocol 03 Selector ff Post-Position Repeat Entry Field Matrix", markdown);
        Assert.Contains("Across 1 repeat-stride tails, 2 repeated-entry field observations appear across the first 2 entries.", markdown);
        Assert.Contains("| Entry index | Segment classification | Update count | First selector | Position offset | Repeat stride | First record u16 | Zero run | Field offset | Field u16 | Entry leads | Repeat records | Records | Position samples | Header windows | Sample |", markdown);
        Assert.Contains("| 1 | unknown selector payload | 3 | `0c` | 51 | 18 | `a0 13` / 5024 | 10 | 11 | `a0 13` / 5024 | ff 00 00 00 00 00 00 00 | 2 (1) | 1 | 17595.543,495,2447.504 | payload +11/object +29 (1) SERVER_VENDOR_OPEN [CR2 server] `81 0d` (1) | `vendor.txt:6` |", markdown);
        Assert.Contains("| 2 | unknown selector payload | 3 | `0c` | 51 | 18 | `a0 13` / 5024 | 0 | 1 | `ff 00` / 255 | ff ff 00 00 00 00 00 00 | 2 (1) | 1 | 17595.543,495,2447.504 | payload +11/object +29 (1) SERVER_VENDOR_OPEN [CR2 server] `81 0d` (1) | `vendor.txt:6` |", markdown);
    }

    [Fact]
    public void ReportWriter_GroupsVendorTransactionFlowsWithAdjacentPackets()
    {
        const string vendorName = "CR1:CLIENT_VENDOR_SELL/CR2:CLIENT_VENDOR_SELL";
        PacketDumpFileSummary dump = new(
            "vendor-flow.txt",
            3,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            new[]
            {
                new Protocol04PacketSequenceSample(10, "server", new[] { "5e" })
                {
                    Blocks = new[]
                    {
                        new Protocol04RpcFlowBlockSample(0, "5e", "CR2:SERVER_VENDOR_BUY_ACK", 1, "99", "-", Array.Empty<string>())
                    }
                },
                new Protocol04PacketSequenceSample(20, "client", new[] { "81 11" }),
                new Protocol04PacketSequenceSample(22, "server", new[] { "80 e7", "80 e4", "5f", "81 12" })
                {
                    Blocks = new[]
                    {
                        new Protocol04RpcFlowBlockSample(0, "80 e7", "CR2:SERVER_VENDOR_SELL_ACK", 12, "0a 00 00 00 00 00 00 00 07 00 00 00", "-", Array.Empty<string>()),
                        new Protocol04RpcFlowBlockSample(1, "80 e4", "CR2:SERVER_VENDOR_INVENTORY_UPDATE", 8, "05 06 07 08 07 00 00 00", "-", new[] { "OK" }),
                        new Protocol04RpcFlowBlockSample(2, "5f", "CR2:SERVER_VENDOR_SELL_ACK_2", 3, "58 00 01", "-", Array.Empty<string>()),
                        new Protocol04RpcFlowBlockSample(3, "81 12", "CR2:SERVER_VENDOR_SELL_RESULT", 13, "00 2e 00 00 00 01 08 00 80 0a 00 00 00", "-", Array.Empty<string>())
                    }
                }
            },
            Array.Empty<Protocol03ObjectViewSample>(),
            Array.Empty<Protocol04InteractionPayloadSample>(),
            Array.Empty<PlayerAttributePayloadSample>(),
            Array.Empty<ManageBonusPayloadSample>())
        {
            VendorTransactionFlows = new[]
            {
                new VendorTransactionFlowSample(
                    20,
                    "client",
                    0,
                    "81 11",
                    vendorName,
                    2,
                    "00 58",
                    "-",
                    new[]
                    {
                        new Protocol04RpcFlowBlockSample(0, "81 11", vendorName, 2, "00 58", "-", Array.Empty<string>())
                    })
            }
        };
        PacketResearchReport report = new(
            ".",
            Array.Empty<string>(),
            null,
            Array.Empty<string>(),
            new[]
            {
                new RpcHeaderEntry("mxo-hd", "CR2", "client", "RPCRequestHeader", "CLIENT_VENDOR_SELL", 0x111, "81 11", "headers.cs", 42)
            },
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

        Assert.Contains("- Protocol 04 top-level vendor transaction flows: 1", markdown);
        Assert.Contains("### Top-Level Vendor Transaction Flows", markdown);
        Assert.Contains("| Vendor RPC | Direction | Packet header sequence | Vendor payload bytes | Vendor payload prefix | Vendor payload suffix | Count | Adjacent protocol 04 packets | Samples |", markdown);
        Assert.Contains("| `81 11` CR1:CLIENT_VENDOR_SELL/CR2:CLIENT_VENDOR_SELL | client | `81 11` | 2 | `00 58` | `-` | 1 | prev 10 `5e`; next 22 `80 e7 -> 80 e4 -> 5f -> 81 12` | `vendor-flow.txt:20` |", markdown);
        Assert.Contains("### Vendor Adjacent Payload Shapes", markdown);
        Assert.Contains("| Vendor RPC | Direction | Vendor payload prefix | Adjacent packet | Adjacent sequence | Adjacent block | Payload bytes | Prefix | Suffix | Count | Texts | Samples |", markdown);
        Assert.Contains("| `81 11` CR1:CLIENT_VENDOR_SELL/CR2:CLIENT_VENDOR_SELL | client | `00 58` | next server line | `80 e7 -> 80 e4 -> 5f -> 81 12` | `80 e7` CR2:SERVER_VENDOR_SELL_ACK | 12 | `0a 00 00 00 00 00 00 00 07 00 00 00` | `-` | 1 | - | `vendor-flow.txt:20 -> next 22` |", markdown);
        Assert.Contains("| `81 11` CR1:CLIENT_VENDOR_SELL/CR2:CLIENT_VENDOR_SELL | client | `00 58` | prev server line | `5e` | `5e` CR2:SERVER_VENDOR_BUY_ACK | 1 | `99` | `-` | 1 | - | `vendor-flow.txt:20 -> prev 10` |", markdown);
        Assert.Contains("### Vendor Transaction Field Leads", markdown);
        Assert.Contains("| Vendor RPC | Direction | Vendor payload prefix | Adjacent block | Field lead | Value | Inference | Count | Samples |", markdown);
        Assert.Contains("| `81 11` CR1:CLIENT_VENDOR_SELL/CR2:CLIENT_VENDOR_SELL | client | `00 58` | `80 e7` CR2:SERVER_VENDOR_SELL_ACK | candidate cash delta s64 +0 | `0a 00 00 00 00 00 00 00` / 10 | negative on buy and positive on sell in current captures | 1 | `vendor-flow.txt:20 -> next 22` |", markdown);
        Assert.Contains("| `81 11` CR1:CLIENT_VENDOR_SELL/CR2:CLIENT_VENDOR_SELL | client | `00 58` | `81 12` CR2:SERVER_VENDOR_SELL_RESULT | candidate sell value u32 +9 | `0a 00 00 00` / 10 | matches the positive sell cash delta in the adjacent 80 e7 block | 1 | `vendor-flow.txt:20 -> next 22` |", markdown);
    }

    [Fact]
    public void ReportWriter_JoinsVendorBuyPayloadsToItemPrices()
    {
        const string vendorName = "CR2:CLIENT_VENDOR_BUY";
        PacketDumpFileSummary dump = new(
            "buy-flow.txt",
            2,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            new[]
            {
                new Protocol04PacketSequenceSample(20, "client", new[] { "81 0e" }),
                new Protocol04PacketSequenceSample(22, "server", new[] { "5e", "80 e7", "80 e4", "81 0f" })
                {
                    Blocks = new[]
                    {
                        new Protocol04RpcFlowBlockSample(0, "5e", "CR2:SERVER_VENDOR_BUY_ACK", 11, "01 00 c1 09 00 00 00 00 00 10 01", "-", Array.Empty<string>()),
                        new Protocol04RpcFlowBlockSample(1, "80 e7", "CR2:SERVER_VENDOR_CASH_DELTA", 12, "d0 8a ff ff ff ff ff ff 08 00 00 00", "-", Array.Empty<string>()),
                        new Protocol04RpcFlowBlockSample(2, "80 e4", "CR2:SERVER_VENDOR_VALUE_KIND", 8, "00 00 00 00 08 00 00 00", "-", Array.Empty<string>()),
                        new Protocol04RpcFlowBlockSample(3, "81 0f", "CR2:SERVER_VENDOR_BUY_RESULT", 13, "c1 09 00 00 00 00 00 10 30 75 00 00 00", "-", Array.Empty<string>())
                    }
                }
            },
            Array.Empty<Protocol03ObjectViewSample>(),
            Array.Empty<Protocol04InteractionPayloadSample>(),
            Array.Empty<PlayerAttributePayloadSample>(),
            Array.Empty<ManageBonusPayloadSample>())
        {
            VendorTransactionFlows = new[]
            {
                new VendorTransactionFlowSample(
                    20,
                    "client",
                    0,
                    "81 0e",
                    vendorName,
                    5,
                    "c1 09 00 00 00",
                    "-",
                    new[]
                    {
                        new Protocol04RpcFlowBlockSample(0, "81 0e", vendorName, 5, "c1 09 00 00 00", "-", Array.Empty<string>())
                    })
            }
        };
        PacketResearchReport report = new(
            ".",
            Array.Empty<string>(),
            null,
            Array.Empty<string>(),
            new[]
            {
                new RpcHeaderEntry("mxo-hd", "CR2", "client", "RPCRequestHeader", "CLIENT_VENDOR_BUY", 0x10e, "81 0e", "headers.cs", 42)
            },
            Array.Empty<AttributeDefinition>(),
            Array.Empty<FxDefinition>(),
            Array.Empty<GameObjectEntry>(),
            Array.Empty<WorldEntityEntry>(),
            Array.Empty<RajkoRpcEntry>(),
            Array.Empty<HardcodedCommandExample>(),
            Array.Empty<Protocol03HardcodedExample>(),
            Array.Empty<Protocol04InteractionCommandExample>(),
            new[]
            {
                new VendorInventoryEntry(1, 888143880, new uint[] { 2497 }, "data/vendor_items.csv", 11)
            },
            Array.Empty<RpcComparison>(),
            new[] { dump })
        {
            ItemCommandEntries = new[]
            {
                new ItemCommandEntry(2497, "DualPistol7", "Twin WestTek 49SAs", "research/community-data/invitems_command.txt", 556)
            },
            VendorPriceEntries = new[]
            {
                new VendorPriceEntry(2497, "DualPistol7", 30000, false, "data/vendor_prices.csv", 21963)
            }
        };

        string markdown = ReportWriter.ToMarkdown(report);

        Assert.Contains("### Vendor Buy Item Price Leads", markdown);
        Assert.Contains("| Buy payload prefix | Item id | Item reference | Vendor price | Price match | Ack sequences | Ack detail fields | Observed cash deltas | Observed buy values | Value kinds | Vendor inventory rows | Samples |", markdown);
        Assert.Contains("| `c1 09 00 00 00` | 2497 | 2497 DualPistol7: Twin WestTek 49SAs | 30000 DualPistol7 | cash and buy value | 1 (1) | `10 01` (1) | -30000 (1) | 30000 (1) | 8 (2) | 1 | `buy-flow.txt:20 -> next 22` |", markdown);
    }

    [Fact]
    public void ReportWriter_JoinsVendorBuyHeader69AckToItemPrices()
    {
        const string vendorName = "CR2:CLIENT_VENDOR_BUY";
        PacketDumpFileSummary dump = new(
            "buy-flow-69.txt",
            2,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            new[]
            {
                new Protocol04PacketSequenceSample(20, "client", new[] { "81 0e" }),
                new Protocol04PacketSequenceSample(22, "server", new[] { "69", "80 e7", "80 e4", "81 0f" })
                {
                    Blocks = new[]
                    {
                        new Protocol04RpcFlowBlockSample(0, "69", null, 9, "01 36 00 00 00 04 00 00 00", "-", Array.Empty<string>()),
                        new Protocol04RpcFlowBlockSample(1, "80 e7", "CR2:SERVER_VENDOR_CASH_DELTA", 12, "9c ff ff ff ff ff ff ff 08 00 00 00", "-", Array.Empty<string>()),
                        new Protocol04RpcFlowBlockSample(2, "80 e4", "CR2:SERVER_VENDOR_VALUE_KIND", 8, "1a a6 f2 cc 08 00 00 00", "-", Array.Empty<string>()),
                        new Protocol04RpcFlowBlockSample(3, "81 0f", "CR2:SERVER_VENDOR_BUY_RESULT", 13, "01 a8 00 00 00 00 00 00 64 00 00 00 00", "-", Array.Empty<string>())
                    }
                }
            },
            Array.Empty<Protocol03ObjectViewSample>(),
            Array.Empty<Protocol04InteractionPayloadSample>(),
            Array.Empty<PlayerAttributePayloadSample>(),
            Array.Empty<ManageBonusPayloadSample>())
        {
            VendorTransactionFlows = new[]
            {
                new VendorTransactionFlowSample(
                    20,
                    "client",
                    0,
                    "81 0e",
                    vendorName,
                    5,
                    "01 a8 00 00 00",
                    "-",
                    new[]
                    {
                        new Protocol04RpcFlowBlockSample(0, "81 0e", vendorName, 5, "01 a8 00 00 00", "-", Array.Empty<string>())
                    })
            }
        };
        PacketResearchReport report = new(
            ".",
            Array.Empty<string>(),
            null,
            Array.Empty<string>(),
            new[]
            {
                new RpcHeaderEntry("mxo-hd", "CR2", "client", "RPCRequestHeader", "CLIENT_VENDOR_BUY", 0x10e, "81 0e", "headers.cs", 42)
            },
            Array.Empty<AttributeDefinition>(),
            Array.Empty<FxDefinition>(),
            Array.Empty<GameObjectEntry>(),
            Array.Empty<WorldEntityEntry>(),
            Array.Empty<RajkoRpcEntry>(),
            Array.Empty<HardcodedCommandExample>(),
            Array.Empty<Protocol03HardcodedExample>(),
            Array.Empty<Protocol04InteractionCommandExample>(),
            new[]
            {
                new VendorInventoryEntry(1, 888143880, new uint[] { 43009 }, "data/vendor_items.csv", 11)
            },
            Array.Empty<RpcComparison>(),
            new[] { dump })
        {
            ItemCommandEntries = new[]
            {
                new ItemCommandEntry(43009, "WinterSnowballIcy", "Icy Snowball", "research/community-data/invitems_command.txt", 556)
            },
            VendorPriceEntries = new[]
            {
                new VendorPriceEntry(43009, "WinterSnowballIcy", 100, true, "data/vendor_prices.csv", 21963)
            }
        };

        string markdown = ReportWriter.ToMarkdown(report);

        Assert.Contains("| `01 a8 00 00 00` | 43009 | 43009 WinterSnowballIcy: Icy Snowball | 100 WinterSnowballIcy non-sellable | cash and buy value | 54 (1) | `69 +5 04 00 00 00` (1) | -100 (1) | 100 (1) | 8 (2) | 1 | `buy-flow-69.txt:20 -> next 22` |", markdown);
    }

    [Fact]
    public void ReportWriter_JoinsVendorSellResponsesToBuybackPrices()
    {
        const string vendorName = "CR2:CLIENT_VENDOR_SELL";
        PacketDumpFileSummary dump = new(
            "sell-flow.txt",
            4,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            new[]
            {
                new Protocol04PacketSequenceSample(20, "client", new[] { "81 11" }),
                new Protocol04PacketSequenceSample(22, "server", new[] { "80 e7", "80 e4", "5f", "81 12" })
                {
                    Blocks = new[]
                    {
                        new Protocol04RpcFlowBlockSample(0, "80 e7", "CR2:SERVER_VENDOR_SELL_ACK", 12, "88 13 00 00 00 00 00 00 07 00 00 00", "-", Array.Empty<string>()),
                        new Protocol04RpcFlowBlockSample(1, "80 e4", "CR2:SERVER_VENDOR_VALUE_KIND", 8, "97 e1 7a 00 07 00 00 00", "-", Array.Empty<string>()),
                        new Protocol04RpcFlowBlockSample(2, "5f", "CR2:SERVER_VENDOR_SELL_ACK_2", 3, "58 00 01", "-", Array.Empty<string>()),
                        new Protocol04RpcFlowBlockSample(3, "81 12", "CR2:SERVER_VENDOR_SELL_RESULT", 13, "00 34 a8 00 00 00 00 00 34 88 13 00 00", "-", Array.Empty<string>())
                    }
                },
                new Protocol04PacketSequenceSample(40, "client", new[] { "81 11" }),
                new Protocol04PacketSequenceSample(42, "server", new[] { "80 e7", "80 e4", "5f", "81 12" })
                {
                    Blocks = new[]
                    {
                        new Protocol04RpcFlowBlockSample(0, "80 e7", "CR2:SERVER_VENDOR_SELL_ACK", 12, "0a 00 00 00 00 00 00 00 07 00 00 00", "-", Array.Empty<string>()),
                        new Protocol04RpcFlowBlockSample(1, "80 e4", "CR2:SERVER_VENDOR_VALUE_KIND", 8, "e3 e0 7a 00 07 00 00 00", "-", Array.Empty<string>()),
                        new Protocol04RpcFlowBlockSample(2, "5f", "CR2:SERVER_VENDOR_SELL_ACK_2", 3, "58 00 01", "-", Array.Empty<string>()),
                        new Protocol04RpcFlowBlockSample(3, "81 12", "CR2:SERVER_VENDOR_SELL_RESULT", 13, "00 2e 00 00 00 01 08 00 80 0a 00 00 00", "-", Array.Empty<string>())
                    }
                }
            },
            Array.Empty<Protocol03ObjectViewSample>(),
            Array.Empty<Protocol04InteractionPayloadSample>(),
            Array.Empty<PlayerAttributePayloadSample>(),
            Array.Empty<ManageBonusPayloadSample>())
        {
            VendorTransactionFlows = new[]
            {
                new VendorTransactionFlowSample(
                    20,
                    "client",
                    0,
                    "81 11",
                    vendorName,
                    2,
                    "00 58",
                    "-",
                    new[]
                    {
                        new Protocol04RpcFlowBlockSample(0, "81 11", vendorName, 2, "00 58", "-", Array.Empty<string>())
                    }),
                new VendorTransactionFlowSample(
                    40,
                    "client",
                    0,
                    "81 11",
                    vendorName,
                    2,
                    "00 58",
                    "-",
                    new[]
                    {
                        new Protocol04RpcFlowBlockSample(0, "81 11", vendorName, 2, "00 58", "-", Array.Empty<string>())
                    })
            }
        };
        PacketResearchReport report = new(
            ".",
            Array.Empty<string>(),
            null,
            Array.Empty<string>(),
            new[]
            {
                new RpcHeaderEntry("mxo-hd", "CR2", "client", "RPCRequestHeader", "CLIENT_VENDOR_SELL", 0x111, "81 11", "headers.cs", 42)
            },
            Array.Empty<AttributeDefinition>(),
            Array.Empty<FxDefinition>(),
            Array.Empty<GameObjectEntry>(),
            Array.Empty<WorldEntityEntry>(),
            Array.Empty<RajkoRpcEntry>(),
            Array.Empty<HardcodedCommandExample>(),
            Array.Empty<Protocol03HardcodedExample>(),
            Array.Empty<Protocol04InteractionCommandExample>(),
            new[]
            {
                new VendorInventoryEntry(1, 207620115, new uint[] { 2147485696 }, "data/vendor_items.csv", 21)
            },
            Array.Empty<RpcComparison>(),
            new[] { dump })
        {
            AbilityDefinitions = new[]
            {
                new AbilityDefinition(2147485696, -2147481600, "AwakenedAbility", "Awakened", "research/community-data/abilities.txt", 2)
            },
            ItemCommandEntries = new[]
            {
                new ItemCommandEntry(43060, "Submachinegun11", "FM-13SK", "research/community-data/invitems_command.txt", 36717)
            },
            VendorPriceEntries = new[]
            {
                new VendorPriceEntry(43060, "Submachinegun11", 50000, false, "data/vendor_prices.csv", 9651),
                new VendorPriceEntry(2147485696, "AwakenedAbility", 100, false, "data/vendor_prices.csv", 28599)
            }
        };

        string markdown = ReportWriter.ToMarkdown(report);

        Assert.Contains("### Vendor Sell Item Price Leads", markdown);
        Assert.Contains("| Sell payload prefix | Response field | Response id | Price id / reference | Vendor price | Expected sell | Price match | Ack item values | Ack flags | Observed cash deltas | Observed sell values | Value kinds | Vendor inventory rows | Samples |", markdown);
        Assert.Contains("| `00 58` | +1 normal item u32 | 43060 | 43060 Submachinegun11: FM-13SK | 50000 Submachinegun11 | 5000 | cash and sell value | 88 (1) | 1 (1) | 5000 (1) | 5000 (1) | 7 (2) | 0 | `sell-flow.txt:20 -> next 22` |", markdown);
        Assert.Contains("| `00 58` | +5 ability code u32 | 2147485697 | 2147485696 AwakenedAbility: Awakened | 100 AwakenedAbility | 10 | cash and sell value | 88 (1) | 1 (1) | 10 (1) | 10 (1) | 7 (2) | 1 | `sell-flow.txt:40 -> next 42` |", markdown);
    }

    [Fact]
    public void ReportWriter_ParsesVendorOpenItemLists()
    {
        const string vendorName = "CR2:SERVER_VENDOR_OPEN";
        const string payloadHex = "7c ad d9 43 00 00 00 00 dd c8 ef c0 00 00 00 00 00 c0 57 40 00 00 00 e0 45 a8 d2 40 20 00 02 00 c1 1e 00 00 c1 09 00 00";
        PacketDumpFileSummary dump = new(
            "open-flow.txt",
            1,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            new[]
            {
                new Protocol04PacketSequenceSample(18, "client", new[] { "80 c7" })
                {
                    Blocks = new[]
                    {
                        new Protocol04RpcFlowBlockSample(
                            0,
                            "80 c7",
                            "CR2:CLIENT_OBJECTINTERACTION_DYNAMIC",
                            6,
                            "0d 00 7f 00 02 00",
                            "0d 00 7f 00 02 00",
                            Array.Empty<string>())
                    }
                },
                new Protocol04PacketSequenceSample(20, "server", new[] { "81 0d" })
            },
            Array.Empty<Protocol03ObjectViewSample>(),
            Array.Empty<Protocol04InteractionPayloadSample>(),
            Array.Empty<PlayerAttributePayloadSample>(),
            Array.Empty<ManageBonusPayloadSample>())
        {
            VendorTransactionFlows = new[]
            {
                new VendorTransactionFlowSample(
                    20,
                    "server",
                    0,
                    "81 0d",
                    vendorName,
                    40,
                    "7c ad d9 43 00 00 00 00 dd c8 ef c0 00 00 00 00",
                    "c1 1e 00 00 c1 09 00 00",
                    new[]
                    {
                        new Protocol04RpcFlowBlockSample(
                            0,
                            "81 0d",
                            vendorName,
                            40,
                            "7c ad d9 43 00 00 00 00 dd c8 ef c0 00 00 00 00",
                            "c1 1e 00 00 c1 09 00 00",
                            Array.Empty<string>())
                    })
                {
                    VendorPayloadHex = payloadHex
                }
            }
        };
        PacketResearchReport report = new(
            ".",
            Array.Empty<string>(),
            null,
            Array.Empty<string>(),
            new[]
            {
                new RpcHeaderEntry("mxo-hd", "CR2", "server", "RPCResponseHeaders", "SERVER_VENDOR_OPEN", 0x810d, "81 0d", "headers.cs", 42)
            },
            Array.Empty<AttributeDefinition>(),
            Array.Empty<FxDefinition>(),
            Array.Empty<GameObjectEntry>(),
            Array.Empty<WorldEntityEntry>(),
            Array.Empty<RajkoRpcEntry>(),
            Array.Empty<HardcodedCommandExample>(),
            Array.Empty<Protocol03HardcodedExample>(),
            Array.Empty<Protocol04InteractionCommandExample>(),
            new[]
            {
                new VendorInventoryEntry(1, 888143880, new uint[] { 7873, 2497, 43060 }, "data/vendor_items.csv", 11)
            },
            Array.Empty<RpcComparison>(),
            new[] { dump })
        {
            ItemCommandEntries = new[]
            {
                new ItemCommandEntry(7873, "NH2000", "Assault Rifle", "research/community-data/invitems_command.txt", 1),
                new ItemCommandEntry(2497, "DualPistol7", "Twin WestTek 49SAs", "research/community-data/invitems_command.txt", 2)
            },
            VendorPriceEntries = new[]
            {
                new VendorPriceEntry(7873, "NH2000", 30000, false, "data/vendor_prices.csv", 1),
                new VendorPriceEntry(2497, "DualPistol7", 30000, false, "data/vendor_prices.csv", 2)
            }
        };

        string markdown = ReportWriter.ToMarkdown(report);

        Assert.Contains("### Vendor Open Header Field Leads", markdown);
        Assert.Contains("| Vendor open payload prefix | Payload bytes | List offset +28 | Count +30 | Float32 candidates +0/+8/+16/+24 | Raw words +4/+12/+20 | Prev client interaction | Samples |", markdown);
        Assert.Contains("| `7c ad d9 43 00 00 00 00 dd c8 ef c0 00 00 00 00` | 40 | `20 00` / 32 | `02 00` / 2 | +0 435.355<br>+8 -7.493<br>+16 3.371<br>+24 6.583 | +4 `00 00 00 00`<br>+12 `00 00 00 00`<br>+20 `00 00 00 e0` | prev 18 `80 c7` `0d 00 7f 00 02 00` | `open-flow.txt:20` |", markdown);
        Assert.Contains("### Vendor Open Item List Leads", markdown);
        Assert.Contains("| Vendor open payload prefix | Payload bytes | Count field +30 | Parsed ids | Known refs | Price rows | Best vendor inventory matches | Samples |", markdown);
        Assert.Contains("| `7c ad d9 43 00 00 00 00 dd c8 ef c0 00 00 00 00` | 40 | `02 00` / 2 | 2 | 7873 NH2000: Assault Rifle<br>2497 DualPistol7: Twin WestTek 49SAs | 7873 NH2000 30000<br>2497 DualPistol7 30000 | vendor_items.csv:11 static 888143880: 2/2 open ids, 2/3 row ids | `open-flow.txt:20` |", markdown);
    }

    [Fact]
    public void LoadPacketDumpSummaries_LabelsExternalDumpFilesByRoot()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), $"packet-research-{Guid.NewGuid():N}");
        string repoRoot = Path.Combine(tempRoot, "repo");
        string externalRoot = Path.Combine(tempRoot, "warboy");
        try
        {
            string packetDir = Path.Combine(externalRoot, "ProxyM1");
            Directory.CreateDirectory(repoRoot);
            Directory.CreateDirectory(packetDir);
            string packetFile = Path.Combine(packetDir, "m1_packets.log");
            File.WriteAllLines(packetFile, new[]
            {
                "WORLD->Client [15:30:56 07/31/09] packet size: 13",
                "02 04 01 00 01 01 06 81 0d 11 00 00 02"
            });

            PacketDumpFileSummary summary = Assert.Single(PacketResearcher.LoadPacketDumpSummaries(
                externalRoot,
                Array.Empty<RpcHeaderEntry>(),
                repoRoot));

            Assert.Equal(Path.Combine("warboy (external)", "ProxyM1", "m1_packets.log"), summary.File);
            Assert.Equal(1, summary.PacketLikeLines);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
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
    public void DetectManageBonusTradeStatePayloads_DecodesSerializedTradeStateRows()
    {
        byte[] bytes = PacketResearcher.ParseHexBytes(
            "02 04 01 00 00 01 16 80 bc 45 03 11 00 00 02 00 00 00 11 00 32 00 00 00 01 00 00 00 00")
            .ToArray();

        ManageBonusTradeStatePayloadSample payload = PacketResearcher.DetectManageBonusTradeStatePayloads(bytes, 12, "server").Single();

        Assert.Equal(12, payload.Line);
        Assert.Equal(20, payload.PayloadLength);
        Assert.Equal("45 03", payload.TradeField0);
        Assert.Equal("11 00", payload.TradeField1);
        Assert.Equal(17, payload.TradeField1Value);
        Assert.Equal("00 02", payload.TradeField2);
        Assert.Equal(512, payload.TradeField2Value);
        Assert.Equal("11 00", payload.RepeatedTradeField1);
        Assert.Equal(17, payload.RepeatedTradeField1Value);
        Assert.Equal("32 00", payload.VariantValue);
        Assert.Equal(50, payload.VariantValueInt);
        Assert.Equal("dominant serialized trade-state transition variant", payload.VariantRole);
        Assert.Equal("01 00", payload.Byte15Value);
        Assert.Equal(1, payload.Byte15ValueInt);
        Assert.Equal("on", payload.StateName);
        Assert.Equal("45 03 11 00 00 02 00 00 00 11 00 32 00 00 00 01 00 00 00 00", payload.PayloadHex);
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
    public void ParseVendorPriceText_ExtractsPriceRows()
    {
        string[] lines =
        {
            "go_id,codename,vendor_price,non_sellable",
            "2497,DualPistol7,30000,False",
            "39231,DungeonShinjukoFShirt2_Old,1000,True"
        };

        VendorPriceEntry[] entries = PacketResearcher.ParseVendorPriceText(lines, "vendor_prices.csv").ToArray();

        Assert.Equal(2, entries.Length);
        Assert.Equal((uint)2497, entries[0].GoId);
        Assert.Equal("DualPistol7", entries[0].CodeName);
        Assert.Equal((uint)30000, entries[0].VendorPrice);
        Assert.False(entries[0].NonSellable);
        Assert.Equal("vendor_prices.csv", entries[0].File);
        Assert.Equal(2, entries[0].Line);
        Assert.Equal((uint)39231, entries[1].GoId);
        Assert.True(entries[1].NonSellable);
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
    public void ParseAbilityDefinitionText_ExtractsPublicDisplayNames()
    {
        string[] lines =
        {
            "2147488768,CalmMindCalmBodyAbility,\"Calm Mind, Calm Body\"",
            "2147494912,HyperJumpAbility,\"Hyper-Jump\""
        };

        AbilityDefinition[] entries = PacketResearcher.ParseAbilityDefinitionText(lines, "abilities.txt").ToArray();

        Assert.Equal(2, entries.Length);
        Assert.Equal(2147488768u, entries[0].AbilityId);
        Assert.Equal(unchecked((int)2147488768u), entries[0].SignedGoId);
        Assert.Equal("CalmMindCalmBodyAbility", entries[0].CodeName);
        Assert.Equal("Calm Mind, Calm Body", entries[0].DisplayName);
        Assert.Equal("abilities.txt", entries[0].File);
        Assert.Equal(1, entries[0].Line);
        Assert.Equal("HyperJumpAbility", entries[1].CodeName);
        Assert.Equal("Hyper-Jump", entries[1].DisplayName);
    }

    [Fact]
    public void ParseAbilityDefinitionText_ExtractsLegacyAbilityIds()
    {
        string[] lines =
        {
            "AbilityID;GOID;AbilityName;isCastAble",
            "11;-2147472384;HyperJumpAbility;False",
            "12;-2147295232;HyperSpeedAbility;False"
        };

        AbilityDefinition[] entries = PacketResearcher.ParseAbilityDefinitionText(lines, "abilityIDs.csv").ToArray();

        Assert.Equal(2, entries.Length);
        Assert.Equal(2147494912u, entries[0].AbilityId);
        Assert.Equal(-2147472384, entries[0].SignedGoId);
        Assert.Equal("HyperJumpAbility", entries[0].CodeName);
        Assert.Null(entries[0].DisplayName);
        Assert.Equal("abilityIDs.csv", entries[0].File);
        Assert.Equal(2, entries[0].Line);
    }

    [Fact]
    public void ParseAnimationDefinitionText_ExtractsEndianValues()
    {
        string[] lines =
        {
            "Magic: 04001a1a",
            "ba13 13ba = NPCDJAtTurnTable_Idle4",
            "6600 0066 = AD_Arc_L40"
        };

        AnimationDefinition[] entries = PacketResearcher.ParseAnimationDefinitionText(lines, "animations.txt").ToArray();

        Assert.Equal(2, entries.Length);
        Assert.Equal("ba13", entries[0].LittleEndianHex);
        Assert.Equal("13ba", entries[0].BigEndianHex);
        Assert.Equal(5050, entries[0].Value);
        Assert.Equal("NPCDJAtTurnTable_Idle4", entries[0].Name);
        Assert.Equal("animations.txt", entries[0].File);
        Assert.Equal(2, entries[0].Line);
        Assert.Equal(102, entries[1].Value);
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
        Assert.Equal(2, payload.EntryRecords.Count);
        Assert.Equal("00", payload.EntryRecords[0].PrefixHex);
        Assert.Equal(0, payload.EntryRecords[0].PrefixValue);
        Assert.Equal("30 00", payload.EntryRecords[0].EntryIdHex);
        Assert.Equal(0x30, payload.EntryRecords[0].EntryId);
        Assert.Equal("00 00 00", payload.EntryRecords[0].TailHex);
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

        ManageBonusStateIdLongFormFieldLeadSummary lead = Assert.Single(PacketResearcher.BuildManageBonusStateIdLongFormFieldLeadSummaries(
            new[] { dump },
            report.AttributeDefinitions,
            report.GameObjectEntries));
        Assert.Equal("45 03", lead.Field0);
        Assert.Equal("3d 04", lead.Field1);
        Assert.Equal(1085, lead.Field1Value);
        Assert.Equal(new[] { "creation TalkActiveTracker (1)", "go Mclothing_Pants_A8_C1" }, lead.FieldReferences);
        Assert.Equal(1, lead.PayloadCount);
        Assert.Equal(1, lead.SamePacketB2Matches);
        Assert.Equal(1, lead.SamePacketB2ValueMirrors);
        Assert.Equal(0, lead.SamePacketB3Matches);
        Assert.Equal(1, lead.Field2Values["00 43"]);
        Assert.Equal(1, lead.Byte11Values["00 00"]);
        Assert.Equal(1, lead.Byte15Values["01 00"]);
        Assert.Equal(1, lead.TopPacketSequences["80 b2 -> 80 bc"]);

        ManageBonusStateIdLongFormSemanticFamilySummary family = Assert.Single(
            PacketResearcher.BuildManageBonusStateIdLongFormSemanticFamilySummaries(new[] { lead }));
        Assert.Equal("interaction state", family.SemanticFamily);
        Assert.Equal(1, family.DistinctFields);
        Assert.Equal(1, family.PayloadCount);
        Assert.Equal(1, family.SamePacketB2Matches);
        Assert.Equal(1, family.SamePacketB2ValueMirrors);
        Assert.Equal(0, family.SamePacketB3Matches);
        Assert.Equal(1, family.TopFieldReferences["creation TalkActiveTracker (1)"]);
        Assert.Equal(1, family.TopFieldReferences["go Mclothing_Pants_A8_C1"]);
        Assert.Equal(1, family.TopFields["45 03/3d 04 (1085) creation TalkActiveTracker (1); go Mclothing_Pants_A8_C1"]);

        string markdown = ReportWriter.ToMarkdown(report);

        Assert.Contains("### `80 bc` Long-Form Semantic Families", markdown);
        Assert.Contains("interaction state", markdown);
        Assert.Contains("field id overlaps talk, active, loot, use, or interaction attributes", markdown);
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
        Assert.Contains("### `80 bc` Semantic Field Family Leads", markdown);
        Assert.Contains("| `45 03` | state-id long-form candidate<br>links to 80 b2 short state<br>interaction attribute-index candidates | 1 | 1 | 1 | 0 | 1 | 0 | 0 | 1 | `3d 04` (1085) creation TalkActiveTracker (1); go Mclothing_Pants_A8_C1 (1) | 80 b2 -> 80 bc (1) | `sample.txt:10` |", markdown);
        Assert.Contains("### `80 bc` Long-Form State Field Leads", markdown);
        Assert.Contains("| `45 03` | `3d 04` (1085) | `3d 04` (1085) creation TalkActiveTracker (1); go Mclothing_Pants_A8_C1 (1) | 1 | 1 | 1 | 0 | 00 43 (1) | 00 00 (1) | 01 00 (1) | 0 | 80 b2 -> 80 bc (1) | `sample.txt:10` |", markdown);
    }

    [Fact]
    public void ReportWriter_IncludesManageBonusInteractionSequenceContext()
    {
        PacketDumpFileSummary dump = new(
            "community-intake/trade/vendor_trade_sample.txt",
            1,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            new[]
            {
                new Protocol04PacketSequenceSample(42, "server", new[] { "81 0d", "80 bc", "80 e7" })
            },
            Array.Empty<Protocol03ObjectViewSample>(),
            Array.Empty<Protocol04InteractionPayloadSample>(),
            new[]
            {
                new PlayerAttributePayloadSample(42, 6, "11 00", 0x11, "32 00", 0x32, "08 02", 0x0208, "11 00 32 00 08 02")
            },
            new[]
            {
                new ManageBonusPayloadSample(42, 20, "45 03", 0x0345, "11 00", 0x11, "00 02", 0x0200, "45 03 11 00 00 02 00 00 00 11 00 32 00 00 00 01 00 00 00 00")
            })
        {
            ManageBonusTradeStatePayloads = new[]
            {
                new ManageBonusTradeStatePayloadSample(
                    42,
                    20,
                    "45 03",
                    "11 00",
                    0x11,
                    "00 02",
                    0x0200,
                    "11 00",
                    0x11,
                    "32 00",
                    0x32,
                    "dominant serialized trade-state transition variant",
                    "01 00",
                    1,
                    "on",
                    "45 03 11 00 00 02 00 00 00 11 00 32 00 00 00 01 00 00 00 00")
            },
            AbilityUnloadPayloads = new[]
            {
                new AbilityUnloadPayloadSample(42, 2, "11 00", 0x11, "11 00")
            }
        };
        AttributeDefinition[] attributes =
        {
            new("mxo-hd", "TradeState", 17, "SecureTradeFlag", 1, 0x11, -1, "TradeState.cs", 24),
            new("mxo-hd", "TradeState", 17, "TradeField2Lead", 1, 0x0200, -1, "TradeState.cs", 25),
            new("mxo-hd", "TradeState", 17, "TradeByte11Lead", 1, 0x32, -1, "TradeState.cs", 26)
        };
        PacketResearchReport report = new(
            ".",
            Array.Empty<string>(),
            null,
            Array.Empty<string>(),
            Array.Empty<RpcHeaderEntry>(),
            attributes,
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

        ManageBonusTradeStateValueSummary valueSummary = Assert.Single(PacketResearcher.BuildManageBonusTradeStateValueSummaries(
            new[] { dump },
            attributes: attributes));
        Assert.Equal("45 03", valueSummary.Field0);
        Assert.Equal("11 00", valueSummary.Field1);
        Assert.Equal("00 02", valueSummary.Field2);
        Assert.Equal(0x0200, valueSummary.Field2Value);
        Assert.Equal(1, valueSummary.Field2References["00 02 (512) creation TradeField2Lead (1)"]);
        Assert.Equal("32 00", valueSummary.Byte11Value);
        Assert.Equal(0x32, valueSummary.Byte11ValueInt);
        Assert.Equal(1, valueSummary.Byte11References["32 00 (50) creation TradeByte11Lead (1)"]);
        Assert.Equal("01 00", valueSummary.Byte15Value);
        Assert.Equal(1, valueSummary.Byte15ValueInt);
        Assert.Equal(1, valueSummary.PayloadCount);
        Assert.Equal(1, valueSummary.SamePacketB2Matches);
        Assert.Equal(1, valueSummary.SamePacketB2ValueMirrors);
        Assert.Equal(1, valueSummary.SamePacketB2Field1Values["32 00"]);
        Assert.Equal(1, valueSummary.SamePacketB2Field2Values["08 02"]);
        Assert.Equal(0, valueSummary.SamePacketOppositeByte15Rows);
        Assert.Equal(1, valueSummary.SamePacketB3Matches);
        Assert.Equal(1, valueSummary.SamePacketVendorOrMarketHeaders);
        Assert.Equal(0, valueSummary.SamePacketObjectInteractionHeaders);
        Assert.Equal(1, valueSummary.CaptureScopes["vendor/market"]);
        Assert.Equal(1, valueSummary.TopPacketSequences["81 0d -> 80 bc -> 80 e7"]);

        ManageBonusTradeStatePayloadDecodeSummary payloadDecode =
            Assert.Single(PacketResearcher.BuildManageBonusTradeStatePayloadDecodeSummaries(new[] { dump }));
        Assert.Equal("32 00", payloadDecode.VariantValue);
        Assert.Equal(50, payloadDecode.VariantValueInt);
        Assert.Equal("dominant serialized trade-state transition variant", payloadDecode.VariantRole);
        Assert.Equal("01 00", payloadDecode.Byte15Value);
        Assert.Equal("on", payloadDecode.StateName);
        Assert.Equal(1, payloadDecode.PayloadCount);
        Assert.Equal(1, payloadDecode.CaptureScopes["vendor/market"]);

        string markdown = ReportWriter.ToMarkdown(report);

        Assert.Contains("### Vendor and Interaction `80 bc` Sequence Context", markdown);
        Assert.Contains("| `11 00` (17) | SecureTradeFlag | creation | 1 | 1 | 1 | 0 | 1 | vendor/market (1) | 81 0d -> 80 bc -> 80 e7 (1) | `community-intake/trade/vendor_trade_sample.txt:42` |", markdown);
        Assert.Contains("### `80 bc` Trade-State Value Leads", markdown);
        Assert.Contains("| `00 02` (512) | 00 02 (512) creation TradeField2Lead (1) (1) | `32 00` (50) | 32 00 (50) creation TradeByte11Lead (1) (1) | `01 00` (1) | 1 | 1 | 1 | 32 00 (1) | 08 02 (1) | 0 | 1 | 1 | 0 | vendor/market (1) | 81 0d -> 80 bc -> 80 e7 (1) | community-intake/trade/vendor_trade_sample.txt (1) | `community-intake/trade/vendor_trade_sample.txt:42` `45 03 11 00 00 02 00 00 00 11 00 32 00 00 00 01 00 00 00 00` |", markdown);
        Assert.Contains("### `80 bc` Trade-State Payload Decoder", markdown);
        Assert.Contains("| `32 00` (50) | dominant serialized trade-state transition variant | `01 00` (1) | on | 1 | vendor/market (1) | `community-intake/trade/vendor_trade_sample.txt:42` | `45 03 11 00 00 02 00 00 00 11 00 32 00 00 00 01 00 00 00 00` |", markdown);
    }

    [Fact]
    public void BuildManageBonusTradeStateByte15PairSummaries_GroupsOppositeByte15Rows()
    {
        PacketDumpFileSummary dump = new(
            "trade-pair.txt",
            1,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            new[]
            {
                new Protocol04PacketSequenceSample(42, "server", new[] { "80 bc", "80 b2", "80 bc" })
                {
                    Blocks = new[]
                    {
                        new Protocol04RpcFlowBlockSample(0, "80 bc", null, 20, "45 03 11 00 00 02 00 00 00 11 00 32 00 00 00 00", "00 00 00 00 00 00 00 00", Array.Empty<string>()),
                        new Protocol04RpcFlowBlockSample(1, "80 b2", null, 6, "11 00 32 00 08 02", "-", Array.Empty<string>()),
                        new Protocol04RpcFlowBlockSample(2, "80 bc", null, 20, "45 03 11 00 00 02 00 00 00 11 00 32 00 00 00 01", "01 00 00 00 00 00 00 00", Array.Empty<string>())
                    }
                }
            },
            Array.Empty<Protocol03ObjectViewSample>(),
            Array.Empty<Protocol04InteractionPayloadSample>(),
            new[]
            {
                new PlayerAttributePayloadSample(42, 6, "11 00", 0x11, "32 00", 0x32, "08 02", 0x0208, "11 00 32 00 08 02")
            },
            new[]
            {
                new ManageBonusPayloadSample(42, 20, "45 03", 0x0345, "11 00", 0x11, "00 02", 0x0200, "45 03 11 00 00 02 00 00 00 11 00 32 00 00 00 00 00 00 00 00"),
                new ManageBonusPayloadSample(42, 20, "45 03", 0x0345, "11 00", 0x11, "00 02", 0x0200, "45 03 11 00 00 02 00 00 00 11 00 32 00 00 00 01 00 00 00 00")
            });

        ManageBonusTradeStateValueSummary[] values = PacketResearcher.BuildManageBonusTradeStateValueSummaries(new[] { dump }).ToArray();
        ManageBonusTradeStateByte15PairSummary pair = Assert.Single(PacketResearcher.BuildManageBonusTradeStateByte15PairSummaries(values));

        Assert.Equal("32 00", pair.Byte11Value);
        Assert.Equal(1, pair.OffPayloadCount);
        Assert.Equal(1, pair.OnPayloadCount);
        Assert.Equal(1, pair.SamePacketPairedRows);
        Assert.Equal(1, pair.OffSamePacketB2ValueMirrors);
        Assert.Equal(1, pair.OnSamePacketB2ValueMirrors);
        Assert.Equal(2, pair.LinkedB2Field1Values["32 00"]);
        Assert.Equal(2, pair.LinkedB2Field2Values["08 02"]);

        ManageBonusTradeStateByte15TransitionSummary transition = Assert.Single(PacketResearcher.BuildManageBonusTradeStateByte15TransitionSummaries(new[] { dump }));

        Assert.Equal("32 00", transition.Byte11Value);
        Assert.Equal(1, transition.PairedPacketCount);
        Assert.Equal(1, transition.PairCombinationCount);
        Assert.Equal(1, transition.OffBeforeOnPairs);
        Assert.Equal(0, transition.OnBeforeOffPairs);
        Assert.Equal(1, transition.AdjacentPairCombinations);
        Assert.Equal(1, transition.MinManageBonusRowDistance);
        Assert.Equal(1, transition.MaxManageBonusRowDistance);
        Assert.Equal(0, transition.AdjacentProtocolBlockPairs);
        Assert.Equal(2, transition.MinProtocolBlockDistance);
        Assert.Equal(2, transition.MaxProtocolBlockDistance);
        Assert.Equal(1, transition.SamePacketB2Packets);
        Assert.Equal(0, transition.SamePacketB3Packets);
        Assert.Equal(1, transition.BetweenPairB2MirrorPairs);
        Assert.Equal(0, transition.PrecedingB3MatchPairs);
        Assert.Equal(0, transition.CompleteStateRefreshPairs);
        Assert.Equal(1, transition.B2OnlyStateRefreshPairs);
        Assert.Equal(0, transition.UnmatchedStateRefreshPairs);
        Assert.Equal(1, transition.BetweenPairB2Values["11 00/32 00/08 02"]);
        Assert.Equal(1, transition.PrecedingB3Values["none"]);
        Assert.Equal(1, transition.CaptureScopes["trade"]);
        Assert.Equal(1, transition.TopPacketSequences["80 bc -> 80 b2 -> 80 bc"]);
        Assert.Equal(1, transition.TopPairHeaderWindows["80 bc -> 80 b2 -> 80 bc"]);
    }

    [Fact]
    public void BuildManageBonusTradeStateByte15TransitionSummaries_GroupsImmediateCompanionRows()
    {
        PacketDumpFileSummary dump = new(
            "trade-companion.txt",
            1,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            new[]
            {
                new Protocol04PacketSequenceSample(42, "server", new[] { "80 b3", "80 bc", "80 bc", "80 b2", "80 bc", "80 bc" })
                {
                    Blocks = new[]
                    {
                        new Protocol04RpcFlowBlockSample(0, "80 b3", null, 2, "11 00", "-", Array.Empty<string>()),
                        new Protocol04RpcFlowBlockSample(1, "80 bc", null, 20, "45 03 11 00 00 02 00 00 00 11 00 32 00 00 00 01", "00 00 00 01 00 00 00 00", Array.Empty<string>()),
                        new Protocol04RpcFlowBlockSample(2, "80 bc", null, 20, "55 00 c0 00 00 02 00 00 00 cc 00 32 00 00 00 01", "00 00 00 01 00 00 00 00", Array.Empty<string>()),
                        new Protocol04RpcFlowBlockSample(3, "80 b2", null, 6, "11 00 32 00 08 02", "-", Array.Empty<string>()),
                        new Protocol04RpcFlowBlockSample(4, "80 bc", null, 20, "45 03 11 00 00 02 00 00 00 11 00 32 00 00 00 00", "00 00 00 00 00 00 00 00", Array.Empty<string>()),
                        new Protocol04RpcFlowBlockSample(5, "80 bc", null, 20, "55 00 d4 00 00 02 00 00 00 cc 00 32 00 00 00 00", "00 00 00 00 00 00 00 00", Array.Empty<string>())
                    }
                },
                new Protocol04PacketSequenceSample(43, "server", new[] { "80 b3", "80 bc", "80 bc", "80 b2", "80 bc", "80 bc" })
                {
                    Blocks = new[]
                    {
                        new Protocol04RpcFlowBlockSample(0, "80 b3", null, 2, "11 00", "-", Array.Empty<string>()),
                        new Protocol04RpcFlowBlockSample(1, "80 bc", null, 20, "45 03 11 00 00 02 00 00 00 11 00 32 00 00 00 01", "00 00 00 01 00 00 00 00", Array.Empty<string>()),
                        new Protocol04RpcFlowBlockSample(2, "80 bc", null, 20, "45 00 aa 00 00 02 00 00 00 cc 00 04 00 00 00 01", "00 00 00 01 00 00 00 00", Array.Empty<string>()),
                        new Protocol04RpcFlowBlockSample(3, "80 b2", null, 6, "11 00 32 00 08 02", "-", Array.Empty<string>()),
                        new Protocol04RpcFlowBlockSample(4, "80 bc", null, 20, "45 03 11 00 00 02 00 00 00 11 00 32 00 00 00 00", "00 00 00 00 00 00 00 00", Array.Empty<string>()),
                        new Protocol04RpcFlowBlockSample(5, "80 bc", null, 20, "45 00 bb 00 00 02 00 00 00 cc 00 05 00 00 00 00", "00 00 00 00 00 00 00 00", Array.Empty<string>())
                    }
                }
            },
            Array.Empty<Protocol03ObjectViewSample>(),
            Array.Empty<Protocol04InteractionPayloadSample>(),
            new[]
            {
                new PlayerAttributePayloadSample(42, 6, "11 00", 0x11, "32 00", 0x32, "08 02", 0x0208, "11 00 32 00 08 02")
            },
            new[]
            {
                new ManageBonusPayloadSample(42, 20, "45 03", 0x0345, "11 00", 0x11, "00 02", 0x0200, "45 03 11 00 00 02 00 00 00 11 00 32 00 00 00 01 00 00 00 00"),
                new ManageBonusPayloadSample(42, 20, "45 03", 0x0345, "11 00", 0x11, "00 02", 0x0200, "45 03 11 00 00 02 00 00 00 11 00 32 00 00 00 00 00 00 00 00"),
                new ManageBonusPayloadSample(43, 20, "45 03", 0x0345, "11 00", 0x11, "00 02", 0x0200, "45 03 11 00 00 02 00 00 00 11 00 32 00 00 00 01 00 00 00 00"),
                new ManageBonusPayloadSample(43, 20, "45 03", 0x0345, "11 00", 0x11, "00 02", 0x0200, "45 03 11 00 00 02 00 00 00 11 00 32 00 00 00 00 00 00 00 00")
            });

        ManageBonusTradeStateByte15TransitionSummary transition = Assert.Single(PacketResearcher.BuildManageBonusTradeStateByte15TransitionSummaries(new[] { dump }));

        Assert.Equal(0, transition.OffBeforeOnPairs);
        Assert.Equal(2, transition.OnBeforeOffPairs);
        Assert.Equal(3, transition.MinProtocolBlockDistance);
        Assert.Equal(3, transition.MaxProtocolBlockDistance);
        Assert.Equal(2, transition.CompleteStateRefreshPairs);
        Assert.Equal(2, transition.OnCompanionRows);
        Assert.Equal(2, transition.OffCompanionRows);
        Assert.Equal(2, transition.BothCompanionRows);
        Assert.Equal(2, transition.OnCompanionByte15Matches);
        Assert.Equal(2, transition.OffCompanionByte15Matches);
        Assert.Equal(0, transition.SameCompanionField1Pairs);
        Assert.Equal(1, transition.SameCompanionByte11Pairs);
        Assert.Equal(2, transition.TopPairHeaderWindows["80 b3 -> 80 bc x2 -> 80 b2 -> 80 bc x2"]);
        Assert.Equal(1, transition.OnCompanionField0Values["55 00"]);
        Assert.Equal(1, transition.OnCompanionField0Values["45 00"]);
        Assert.Equal(1, transition.OffCompanionField0Values["55 00"]);
        Assert.Equal(1, transition.OffCompanionField0Values["45 00"]);
        Assert.Equal(1, transition.CompanionField0Pairs["55 00 -> 55 00"]);
        Assert.Equal(1, transition.CompanionField0Pairs["45 00 -> 45 00"]);
        Assert.Equal(1, transition.OnCompanionField1Values["c0 00"]);
        Assert.Equal(1, transition.OnCompanionField1Values["aa 00"]);
        Assert.Equal(1, transition.OffCompanionField1Values["d4 00"]);
        Assert.Equal(1, transition.OffCompanionField1Values["bb 00"]);
        Assert.Equal(1, transition.CompanionField1Pairs["c0 00 -> d4 00"]);
        Assert.Equal(1, transition.CompanionField1Pairs["aa 00 -> bb 00"]);
        Assert.Equal(1, transition.OnCompanionByte11Values["32 00"]);
        Assert.Equal(1, transition.OnCompanionByte11Values["04 00"]);
        Assert.Equal(1, transition.OffCompanionByte11Values["32 00"]);
        Assert.Equal(1, transition.OffCompanionByte11Values["05 00"]);
        Assert.Equal(1, transition.CompanionByte11Pairs["32 00 -> 32 00"]);
        Assert.Equal(1, transition.CompanionByte11Pairs["04 00 -> 05 00"]);
        Assert.Equal(1, transition.OnCompanionShapes["55 00/c0 00/00 02/32 00/01 00"]);
        Assert.Equal(1, transition.OnCompanionShapes["45 00/aa 00/00 02/04 00/01 00"]);
        Assert.Equal(1, transition.OffCompanionShapes["55 00/d4 00/00 02/32 00/00 00"]);
        Assert.Equal(1, transition.OffCompanionShapes["45 00/bb 00/00 02/05 00/00 00"]);
        Assert.Equal(1, transition.CompanionShapePairs["55 00/c0 00/00 02/32 00/01 00 -> 55 00/d4 00/00 02/32 00/00 00"]);
        Assert.Equal(1, transition.CompanionShapePairs["45 00/aa 00/00 02/04 00/01 00 -> 45 00/bb 00/00 02/05 00/00 00"]);

        AttributeDefinition[] attributes =
        {
            new("fixture", "CompanionState", 1, "OnCompanionState", 1, 0xc0, -1, "CompanionState.cs", 12),
            new("fixture", "CompanionState", 1, "OffCompanionState", 1, -1, 0xd4, "CompanionState.cs", 13),
            new("fixture", "OtherCompanionState", 1, "OtherOnCompanionState", 1, 0xaa, -1, "OtherCompanionState.cs", 12),
            new("fixture", "OtherCompanionState", 1, "OtherOffCompanionState", 1, -1, 0xbb, "OtherCompanionState.cs", 13),
            new("fixture", "TradeCompanionByte", 1, "TradeCompanionByte11", 1, 0x32, 0x32, "TradeCompanionByte.cs", 12),
            new("fixture", "OtherCompanionByte", 1, "OtherOnCompanionByte11", 1, 0x04, -1, "OtherCompanionByte.cs", 12),
            new("fixture", "OtherCompanionByte", 1, "OtherOffCompanionByte11", 1, -1, 0x05, "OtherCompanionByte.cs", 13)
        };
        ManageBonusTradeStateCompanionMapSummary[] maps = PacketResearcher.BuildManageBonusTradeStateCompanionMapSummaries(
            new[] { transition },
            attributes: attributes).ToArray();
        Assert.Equal(2, maps.Length);
        ManageBonusTradeStateCompanionMapSummary map = Assert.Single(maps, summary =>
            summary.OnCompanionByte11Value.Equals("32 00", StringComparison.OrdinalIgnoreCase) &&
            summary.OffCompanionByte11Value.Equals("32 00", StringComparison.OrdinalIgnoreCase));
        ManageBonusTradeStateCompanionMapSummary otherMap = Assert.Single(maps, summary =>
            summary.OnCompanionByte11Value.Equals("04 00", StringComparison.OrdinalIgnoreCase) &&
            summary.OffCompanionByte11Value.Equals("05 00", StringComparison.OrdinalIgnoreCase));

        Assert.Equal("32 00", map.TradeByte11Value);
        Assert.Equal(0x32, map.TradeByte11ValueInt);
        Assert.Equal("32 00", map.OnCompanionByte11Value);
        Assert.Equal(0x32, map.OnCompanionByte11ValueInt);
        Assert.Equal("32 00", map.OffCompanionByte11Value);
        Assert.Equal(0x32, map.OffCompanionByte11ValueInt);
        Assert.Equal(1, map.CompanionPairCombinationCount);
        Assert.Equal(2, map.TransitionPairCombinationCount);
        Assert.Equal(1, map.SameCompanionByte11Pairs);
        Assert.Equal(2, map.TransitionCompleteStateRefreshPairs);
        Assert.Equal(0, map.SameCompanionField1Pairs);
        Assert.Equal(1, map.CompanionField0PairDistinctCount);
        Assert.Equal(1, map.CompanionField1PairDistinctCount);
        Assert.Equal(1, map.ListedCompanionField1PairCount);
        Assert.Equal(1, map.OnCompanionField1DistinctCount);
        Assert.Equal(1, map.OffCompanionField1DistinctCount);
        Assert.Equal(1, map.CompanionField1PairRepeatBuckets["1 hit"]);
        Assert.Equal(1, map.CompanionField0Pairs["55 00 -> 55 00"]);
        Assert.Equal(1, map.CompanionField1Pairs["c0 00 -> d4 00"]);
        Assert.False(map.CompanionField0Pairs.ContainsKey("45 00 -> 45 00"));
        Assert.False(map.CompanionField1Pairs.ContainsKey("aa 00 -> bb 00"));
        Assert.Equal(1, map.CompanionField1PairReferences["c0 00 (192) creation OnCompanionState (1) -> d4 00 (212) update OffCompanionState (1)"]);
        Assert.Equal(1, map.OnCompanionField1References["c0 00 (192) creation OnCompanionState (1)"]);
        Assert.Equal(1, map.OffCompanionField1References["d4 00 (212) update OffCompanionState (1)"]);
        Assert.Equal(1, map.CompanionByte11PairReferences["32 00 (50) creation TradeCompanionByte11 (1); update TradeCompanionByte11 (1) -> 32 00 (50) creation TradeCompanionByte11 (1); update TradeCompanionByte11 (1)"]);
        Assert.Equal(1, map.OnCompanionByte11References["32 00 (50) creation TradeCompanionByte11 (1)"]);
        Assert.Equal(1, map.OnCompanionByte11References["32 00 (50) update TradeCompanionByte11 (1)"]);
        Assert.Equal(1, map.OffCompanionByte11References["32 00 (50) creation TradeCompanionByte11 (1)"]);
        Assert.Equal(1, map.OffCompanionByte11References["32 00 (50) update TradeCompanionByte11 (1)"]);
        Assert.Equal(1, otherMap.CompanionField0Pairs["45 00 -> 45 00"]);
        Assert.Equal(1, otherMap.CompanionField1Pairs["aa 00 -> bb 00"]);
        Assert.Equal(1, otherMap.CompanionField0PairDistinctCount);
        Assert.Equal(1, otherMap.CompanionField1PairDistinctCount);
        Assert.Equal(1, otherMap.ListedCompanionField1PairCount);
        Assert.Equal(1, otherMap.OnCompanionField1DistinctCount);
        Assert.Equal(1, otherMap.OffCompanionField1DistinctCount);
        Assert.Equal(1, otherMap.CompanionField1PairRepeatBuckets["1 hit"]);
        Assert.False(otherMap.CompanionField0Pairs.ContainsKey("55 00 -> 55 00"));
        Assert.False(otherMap.CompanionField1Pairs.ContainsKey("c0 00 -> d4 00"));
        Assert.Equal(1, otherMap.CompanionField1PairReferences["aa 00 (170) creation OtherOnCompanionState (1) -> bb 00 (187) update OtherOffCompanionState (1)"]);
        Assert.Equal(1, otherMap.CompanionByte11PairReferences["04 00 (4) creation OtherOnCompanionByte11 (1) -> 05 00 (5) update OtherOffCompanionByte11 (1)"]);
        Assert.Equal(1, otherMap.OnCompanionByte11References["04 00 (4) creation OtherOnCompanionByte11 (1)"]);
        Assert.Equal(1, otherMap.OffCompanionByte11References["05 00 (5) update OtherOffCompanionByte11 (1)"]);

        ManageBonusTradeStateCompanionMapSummary dominantMap = map with
        {
            CompanionPairCombinationCount = 60,
            TransitionPairCombinationCount = 60,
            SameCompanionByte11Pairs = 60,
            TransitionCompleteStateRefreshPairs = 54,
            TransitionB2OnlyStateRefreshPairs = 6,
            TransitionUnmatchedStateRefreshPairs = 0,
            CompanionField1PairDistinctCount = 12,
            CompanionField1PairRepeatBuckets = new Dictionary<string, int> { ["1 hit"] = 8, ["4 hits"] = 4 },
            CompanionField0Pairs = new Dictionary<string, int> { ["55 00 -> 55 00"] = 60 },
            CaptureScopes = new Dictionary<string, int> { ["vendor/market"] = 6, ["static interaction"] = 4 },
            TopPairHeaderWindows = new Dictionary<string, int> { ["80 b3 -> 80 bc x2 -> 80 b2 -> 80 bc x2"] = 54 }
        };
        ManageBonusTradeStateParserActionSummary[] actionSummaries = PacketResearcher.BuildManageBonusTradeStateParserActionSummaries(
                new[] { dominantMap, map, otherMap })
            .ToArray();
        Assert.Equal(3, actionSummaries.Length);
        Assert.Equal("promote serialized trade-state transition candidate", actionSummaries[0].ParserAction);
        Assert.Equal(60, actionSummaries[0].CompanionPairCombinationCount);
        Assert.Equal(60, actionSummaries[0].StableCompanionByte11Pairs);
        Assert.Equal(54, actionSummaries[0].CompleteStateRefreshPairs);
        Assert.Equal(6, actionSummaries[0].B2OnlyStateRefreshPairs);
        Assert.Equal(0, actionSummaries[0].UnmatchedStateRefreshPairs);
        Assert.Equal(60, actionSummaries[0].TradeByte11Values["32 00 / 50"]);
        Assert.Equal(60, actionSummaries[0].CompanionByte11Pairs["32 00 / 50 -> 32 00 / 50"]);
        Assert.Equal(60, actionSummaries[0].CompanionField0Pairs["55 00 -> 55 00"]);
        Assert.Equal(8, actionSummaries[0].CompanionField1PairRepeatBuckets["1 hit"]);
        Assert.Equal(6, actionSummaries[0].CaptureScopes["vendor/market"]);
        Assert.Equal(54, actionSummaries[0].TopPairHeaderWindows["80 b3 -> 80 bc x2 -> 80 b2 -> 80 bc x2"]);
        Assert.Equal("retain low-count trade-state transition control", actionSummaries[1].ParserAction);
        Assert.Equal(1, actionSummaries[1].CompanionPairCombinationCount);
        Assert.Equal("collect before/after capture for companion byte-11 variant", actionSummaries[2].ParserAction);
        Assert.Equal(0, actionSummaries[2].StableCompanionByte11Pairs);
        Assert.Equal(1, actionSummaries[2].CompanionByte11Pairs["04 00 / 4 -> 05 00 / 5"]);

        ManageBonusTradeStateSerializedTransitionSummary[] serializedSummaries =
            PacketResearcher.BuildManageBonusTradeStateSerializedTransitionSummaries(
                    new[] { transition },
                    new[] { dominantMap, map, otherMap })
                .ToArray();
        Assert.Equal(3, serializedSummaries.Length);
        Assert.Equal("promote serialized trade-state transition candidate", serializedSummaries[0].ParserAction);
        Assert.Equal("45 03/11 00/00 02/32 00/01 00", serializedSummaries[0].TradeOnShape);
        Assert.Equal("45 03/11 00/00 02/32 00/00 00", serializedSummaries[0].TradeOffShape);
        Assert.Equal("32 00", serializedSummaries[0].OnCompanionByte11Value);
        Assert.Equal("32 00", serializedSummaries[0].OffCompanionByte11Value);
        Assert.Equal("mixed complete and 80 b2-only refresh", serializedSummaries[0].RefreshClassification);
        Assert.Equal(60, serializedSummaries[0].TransitionPairCombinationCount);
        Assert.Equal(60, serializedSummaries[0].CompanionPairCombinationCount);
        Assert.Equal(2, serializedSummaries[0].BetweenPairB2Values["11 00/32 00/08 02"]);
        Assert.Equal(2, serializedSummaries[0].PrecedingB3Values["11 00"]);
        Assert.Equal("collect before/after capture for companion byte-11 variant", serializedSummaries[2].ParserAction);
        Assert.Equal("04 00", serializedSummaries[2].OnCompanionByte11Value);
        Assert.Equal("05 00", serializedSummaries[2].OffCompanionByte11Value);

        ManageBonusTradeStateSerializedTransitionSample[] serializedSamples =
            PacketResearcher.BuildManageBonusTradeStateSerializedTransitionSamples(
                new[] { dump },
                new[] { dominantMap, map, otherMap })
            .ToArray();
        Assert.Equal(2, serializedSamples.Length);
        ManageBonusTradeStateSerializedTransitionSample serializedSample = serializedSamples[0];
        Assert.Equal("promote serialized trade-state transition candidate", serializedSample.ParserAction);
        Assert.Equal("complete short-state refresh", serializedSample.RefreshClassification);
        Assert.Equal(1, serializedSample.ObservedPairCount);
        Assert.Equal("45 03/11 00/00 02/32 00/01 00", serializedSample.TradeOnShape);
        Assert.Equal("45 03/11 00/00 02/32 00/00 00", serializedSample.TradeOffShape);
        Assert.Equal("32 00", serializedSample.OnCompanionByte11Value);
        Assert.Equal("32 00", serializedSample.OffCompanionByte11Value);
        Assert.Equal("55 00/c0 00/00 02/32 00/01 00", serializedSample.OnCompanionShape);
        Assert.Equal("55 00/d4 00/00 02/32 00/00 00", serializedSample.OffCompanionShape);
        Assert.Equal(3, serializedSample.ProtocolBlockDistance);
        Assert.Equal("80 b3 -> 80 bc x2 -> 80 b2 -> 80 bc x2", serializedSample.PairHeaderWindow);
        Assert.Equal(new[] { "11 00/32 00/08 02" }, serializedSample.BetweenPairB2Values);
        Assert.Equal("11 00", serializedSample.PrecedingB3Value);
        Assert.Equal("collect before/after capture for companion byte-11 variant", serializedSamples[1].ParserAction);
        Assert.Equal("04 00", serializedSamples[1].OnCompanionByte11Value);
        Assert.Equal("05 00", serializedSamples[1].OffCompanionByte11Value);

        ManageBonusTradeStateSerializedTransitionParserCoverageSummary[] coverageSummaries =
            PacketResearcher.BuildManageBonusTradeStateSerializedTransitionParserCoverageSummaries(serializedSamples)
                .ToArray();
        Assert.Equal(2, coverageSummaries.Length);
        Assert.Equal("promote serialized trade-state transition candidate; complete short-state refresh", coverageSummaries[0].ParserCase);
        Assert.Equal(1, coverageSummaries[0].ObservedPairCount);
        Assert.Equal(1, coverageSummaries[0].TradeByte11Values["32 00 / 50"]);
        Assert.Equal(1, coverageSummaries[0].CompanionByte11Pairs["32 00 / 50 -> 32 00 / 50"]);
        Assert.Equal(1, coverageSummaries[0].TradeShapePairs["45 03/11 00/00 02/32 00/01 00 -> 45 03/11 00/00 02/32 00/00 00"]);
        Assert.Equal(1, coverageSummaries[0].BetweenPairB2Values["11 00/32 00/08 02"]);
        Assert.Equal(1, coverageSummaries[0].PrecedingB3Values["11 00"]);
        Assert.Equal(1, coverageSummaries[0].PairHeaderWindows["80 b3 -> 80 bc x2 -> 80 b2 -> 80 bc x2"]);
        Assert.Equal("collect before/after capture for companion byte-11 variant; complete short-state refresh", coverageSummaries[1].ParserCase);
        Assert.Equal(1, coverageSummaries[1].CompanionByte11Pairs["04 00 / 4 -> 05 00 / 5"]);

        ManageBonusTradeStateSerializedTransitionDecoderRow[] decoderRows =
            PacketResearcher.BuildManageBonusTradeStateSerializedTransitionDecoderRows(serializedSamples)
                .ToArray();
        Assert.Equal(2, decoderRows.Length);
        ManageBonusTradeStateSerializedTransitionDecoderRow decoderRow = decoderRows[0];
        Assert.Equal("promote serialized trade-state transition candidate; complete short-state refresh", decoderRow.ParserCase);
        Assert.Equal("45 03", decoderRow.TradeField0);
        Assert.Equal("11 00", decoderRow.TradeField1);
        Assert.Equal(17, decoderRow.TradeField1Value);
        Assert.Equal("00 02", decoderRow.TradeField2);
        Assert.Equal(512, decoderRow.TradeField2Value);
        Assert.Equal("32 00", decoderRow.TradeByte11Value);
        Assert.Equal(50, decoderRow.TradeByte11ValueInt);
        Assert.Equal("01 00", decoderRow.OnTradeByte15Value);
        Assert.Equal(1, decoderRow.OnTradeByte15ValueInt);
        Assert.Equal("00 00", decoderRow.OffTradeByte15Value);
        Assert.Equal(0, decoderRow.OffTradeByte15ValueInt);
        Assert.Equal("11 00", decoderRow.BetweenPairB2Field0);
        Assert.Equal(17, decoderRow.BetweenPairB2Field0Value);
        Assert.Equal("32 00", decoderRow.BetweenPairB2Field1);
        Assert.Equal(50, decoderRow.BetweenPairB2Field1Value);
        Assert.Equal("08 02", decoderRow.BetweenPairB2Field2);
        Assert.Equal(520, decoderRow.BetweenPairB2Field2Value);
        Assert.Equal("11 00", decoderRow.PrecedingB3Value);
        Assert.Equal(17, decoderRow.PrecedingB3ValueInt);
        Assert.Equal("55 00", decoderRow.OnCompanionField0);
        Assert.Equal("55 00", decoderRow.OffCompanionField0);
        Assert.Equal("32 00", decoderRow.OnCompanionByte11Value);
        Assert.Equal(50, decoderRow.OnCompanionByte11ValueInt);
        Assert.Equal("32 00", decoderRow.OffCompanionByte11Value);
        Assert.Equal(50, decoderRow.OffCompanionByte11ValueInt);
        Assert.Equal("01 00", decoderRow.OnCompanionByte15Value);
        Assert.Equal(1, decoderRow.OnCompanionByte15ValueInt);
        Assert.Equal("00 00", decoderRow.OffCompanionByte15Value);
        Assert.Equal(0, decoderRow.OffCompanionByte15ValueInt);

        ManageBonusTradeStateSerializedTransitionDetection[] detections =
            PacketResearcher.BuildManageBonusTradeStateSerializedTransitionDetections(new[] { dump })
                .ToArray();
        Assert.Equal(2, detections.Length);
        ManageBonusTradeStateSerializedTransitionDetection detection = detections[0];
        Assert.Equal("decode dominant serialized trade-state transition", detection.ParserAction);
        Assert.Equal("complete short-state refresh", detection.RefreshClassification);
        Assert.Equal("decode dominant serialized trade-state transition; complete short-state refresh", detection.ParserCase);
        Assert.Equal("32 00", detection.VariantValue);
        Assert.Equal(50, detection.VariantValueInt);
        Assert.Equal("dominant serialized trade-state transition variant", detection.VariantRole);
        Assert.Equal("45 03/11 00/00 02/32 00/01 00", detection.TradeOnShape);
        Assert.Equal("45 03/11 00/00 02/32 00/00 00", detection.TradeOffShape);
        Assert.Equal(-1, detection.RowDistance);
        Assert.Equal(3, detection.ProtocolBlockDistance);
        Assert.Equal("80 b3 -> 80 bc x2 -> 80 b2 -> 80 bc x2", detection.PairHeaderWindow);
        Assert.Equal(new[] { "11 00/32 00/08 02" }, detection.BetweenPairB2Values);
        Assert.Equal("11 00", detection.PrecedingB3Value);
        Assert.Equal("55 00/c0 00/00 02/32 00/01 00", detection.OnCompanionShape);
        Assert.Equal("55 00/d4 00/00 02/32 00/00 00", detection.OffCompanionShape);
        Assert.Equal("trade", detection.CaptureScope);
        Assert.True(detection.HasSamePacketB2);
        Assert.True(detection.HasSamePacketB3);
        Assert.Equal(0, detection.OnManageBonusIndex);
        Assert.Equal(1, detection.OffManageBonusIndex);
        Assert.Equal("review serialized trade-state transition candidate", detections[1].ParserAction);
        Assert.Equal("45 00/aa 00/00 02/04 00/01 00", detections[1].OnCompanionShape);
        Assert.Equal("45 00/bb 00/00 02/05 00/00 00", detections[1].OffCompanionShape);

        ManageBonusTradeStateSerializedTransitionDetectionActionSummary[] detectionActions =
            PacketResearcher.BuildManageBonusTradeStateSerializedTransitionDetectionActionSummaries(detections)
                .ToArray();
        Assert.Equal(2, detectionActions.Length);
        ManageBonusTradeStateSerializedTransitionDetectionActionSummary dominantDetectionAction = detectionActions[0];
        Assert.Equal("decode dominant serialized trade-state transition", dominantDetectionAction.ParserAction);
        Assert.Equal("complete short-state refresh", dominantDetectionAction.RefreshClassification);
        Assert.Equal("decode dominant serialized trade-state transition; complete short-state refresh", dominantDetectionAction.ParserCase);
        Assert.Equal(1, dominantDetectionAction.DetectionCount);
        Assert.Equal(1, dominantDetectionAction.DetectionsWithCompanionShapes);
        Assert.Equal(1, dominantDetectionAction.SamePacketB2Count);
        Assert.Equal(1, dominantDetectionAction.SamePacketB3Count);
        Assert.Equal(1, dominantDetectionAction.VariantValues["32 00 / 50"]);
        Assert.Equal(1, dominantDetectionAction.VariantRoles["dominant serialized trade-state transition variant"]);
        Assert.Equal(1, dominantDetectionAction.TradeShapePairs["45 03/11 00/00 02/32 00/01 00 -> 45 03/11 00/00 02/32 00/00 00"]);
        Assert.Equal(1, dominantDetectionAction.CompanionShapePairs["55 00/c0 00/00 02/32 00/01 00 -> 55 00/d4 00/00 02/32 00/00 00"]);
        Assert.Equal(1, dominantDetectionAction.BetweenPairB2Values["11 00/32 00/08 02"]);
        Assert.Equal(1, dominantDetectionAction.PrecedingB3Values["11 00"]);
        Assert.Equal(1, dominantDetectionAction.PairHeaderWindows["80 b3 -> 80 bc x2 -> 80 b2 -> 80 bc x2"]);
        Assert.Equal(1, dominantDetectionAction.CaptureScopes["trade"]);
        ManageBonusTradeStateSerializedTransitionDetectionActionSummary reviewDetectionAction = detectionActions[1];
        Assert.Equal("review serialized trade-state transition candidate", reviewDetectionAction.ParserAction);
        Assert.Equal(1, reviewDetectionAction.DetectionCount);
        Assert.Equal(1, reviewDetectionAction.CompanionShapePairs["45 00/aa 00/00 02/04 00/01 00 -> 45 00/bb 00/00 02/05 00/00 00"]);

        ManageBonusTradeStateSerializedTransitionFieldRoleSummary[] fieldRoles =
            PacketResearcher.BuildManageBonusTradeStateSerializedTransitionFieldRoleSummaries(serializedSamples, maps)
                .ToArray();
        Assert.Equal(11, fieldRoles.Length);
        ManageBonusTradeStateSerializedTransitionFieldRoleSummary tradeByte15Role =
            Assert.Single(fieldRoles, role => role.FieldRole == "trade byte 15");
        Assert.Equal(2, tradeByte15Role.ObservedPairCount);
        Assert.Equal(2, tradeByte15Role.Values["01 00 -> 00 00"]);
        ManageBonusTradeStateSerializedTransitionFieldRoleSummary companionByte11Role =
            Assert.Single(fieldRoles, role => role.FieldRole == "companion byte 11");
        Assert.Equal(1, companionByte11Role.Values["32 00 / 50 -> 32 00 / 50"]);
        Assert.Equal(1, companionByte11Role.Values["04 00 / 4 -> 05 00 / 5"]);
        ManageBonusTradeStateSerializedTransitionFieldRoleSummary precedingB3Role =
            Assert.Single(fieldRoles, role => role.FieldRole == "preceding 80 b3");
        Assert.Equal(2, precedingB3Role.Values["11 00"]);

        PacketResearchReport report = new(
            ".",
            Array.Empty<string>(),
            null,
            Array.Empty<string>(),
            Array.Empty<RpcHeaderEntry>(),
            attributes,
            Array.Empty<FxDefinition>(),
            Array.Empty<GameObjectEntry>(),
            Array.Empty<WorldEntityEntry>(),
            Array.Empty<RajkoRpcEntry>(),
            Array.Empty<HardcodedCommandExample>(),
            Array.Empty<Protocol03HardcodedExample>(),
            Array.Empty<Protocol04InteractionCommandExample>(),
            Array.Empty<VendorInventoryEntry>(),
            Array.Empty<RpcComparison>(),
            new[] { dump })
        {
            ManageBonusTradeStateByte15TransitionSummaries = new[] { transition },
            ManageBonusTradeStateCompanionMapSummaries = maps,
            ManageBonusTradeStateParserActionSummaries = actionSummaries,
            ManageBonusTradeStateSerializedTransitionSummaries = serializedSummaries,
            ManageBonusTradeStateSerializedTransitionSamples = serializedSamples,
            ManageBonusTradeStateSerializedTransitionParserCoverageSummaries = coverageSummaries,
            ManageBonusTradeStateSerializedTransitionFieldRoleSummaries = fieldRoles,
            ManageBonusTradeStateSerializedTransitionDecoderRows = decoderRows,
            ManageBonusTradeStateSerializedTransitionDetections = detections,
            ManageBonusTradeStateSerializedTransitionDetectionActionSummaries = detectionActions
        };
        string json = System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        string markdown = ReportWriter.ToMarkdown(report);

        Assert.Contains("\"ManageBonusTradeStateSerializedTransitionDetectionActionSummaries\"", json);
        Assert.Contains("### `80 bc` Trade-State Parser Action Leads", markdown);
        Assert.Contains("| promote serialized trade-state transition candidate | dominant adjacent off/on rows have stable companion byte-11, short-state mirrors, and broad companion field-1 tails | 1 | 60 | 60 | 60 | 54 | 6 | 0 | 0 | 12 | 32 00 / 50 (60) | 32 00 / 50 -> 32 00 / 50 (60) | 55 00 -> 55 00 (60) | 1 hit (8)<br>4 hits (4) | vendor/market (6); static interaction (4) | 80 b3 -> 80 bc x2 -> 80 b2 -> 80 bc x2 (54) |", markdown);
        Assert.Contains("### `80 bc` Serialized Trade-State Transition Layouts", markdown);
        Assert.Contains("| promote serialized trade-state transition candidate | `45 03/11 00/00 02/32 00/01 00` | `45 03/11 00/00 02/32 00/00 00` | `32 00` (50) -> `32 00` (50) | mixed complete and 80 b2-only refresh | 60 | 60 | 60 | 54 | 6 | 0 | 11 00/32 00/08 02 (2) | 11 00 (2) | 55 00 -> 55 00 (60) | c0 00 -> d4 00 (1) | 1 hit (8)<br>4 hits (4) | vendor/market (6); static interaction (4) | 80 b3 -> 80 bc x2 -> 80 b2 -> 80 bc x2 (54) |", markdown);
        Assert.Contains("### `80 bc` Serialized Trade-State Transition Samples", markdown);
        Assert.Contains("| promote serialized trade-state transition candidate | complete short-state refresh | 1 | `45 03/11 00/00 02/32 00/01 00` | `45 03/11 00/00 02/32 00/00 00` | `32 00` (50) -> `32 00` (50) | 80 b3 -> 80 bc x2 -> 80 b2 -> 80 bc x2 | rows -1; blocks 3 | 11 00/32 00/08 02 | 11 00 | on `55 00/c0 00/00 02/32 00/01 00`<br>off `55 00/d4 00/00 02/32 00/00 00` | trade; B2 yes; B3 yes<br>80 b3 -> 80 bc x2 -> 80 b2 -> 80 bc x2 | `trade-companion.txt:42` off index 1, on index 0 |", markdown);
        Assert.Contains("| collect before/after capture for companion byte-11 variant | complete short-state refresh | 1 | `45 03/11 00/00 02/32 00/01 00` | `45 03/11 00/00 02/32 00/00 00` | `04 00` (4) -> `05 00` (5) |", markdown);
        Assert.Contains("### `80 bc` Serialized Trade-State Parser Coverage", markdown);
        Assert.Contains("| promote serialized trade-state transition candidate; complete short-state refresh | 1 | 1 | 32 00 / 50 (1) | 32 00 / 50 -> 32 00 / 50 (1) | 45 03/11 00/00 02/32 00/01 00 -> 45 03/11 00/00 02/32 00/00 00 (1) | 11 00/32 00/08 02 (1) | 11 00 (1) | 80 b3 -> 80 bc x2 -> 80 b2 -> 80 bc x2 (1) | trade (1) | `trade-companion.txt:42` |", markdown);
        Assert.Contains("| collect before/after capture for companion byte-11 variant; complete short-state refresh | 1 | 1 | 32 00 / 50 (1) | 04 00 / 4 -> 05 00 / 5 (1) |", markdown);
        Assert.Contains("### `80 bc` Serialized Trade-State Decoder Rows", markdown);
        Assert.Contains("| promote serialized trade-state transition candidate; complete short-state refresh | 1 | field0 `45 03`; field1 `11 00` (17); field2 `00 02` (512); byte11 `32 00` (50) | `01 00` (1) -> `00 00` (0) | `11 00` (17) / `32 00` (50) / `08 02` (520) | `11 00` (17) | field0 `55 00` -> `55 00`; byte11 `32 00` (50) -> `32 00` (50); byte15 `01 00` (1) -> `00 00` (0) | 80 b3 -> 80 bc x2 -> 80 b2 -> 80 bc x2; blocks 3 | `trade-companion.txt:42` |", markdown);
        Assert.Contains("### `80 bc` Serialized Trade-State Detection Actions", markdown);
        Assert.Contains("| decode dominant serialized trade-state transition; complete short-state refresh | 1 | 1 with companion shapes | same-packet B2 1<br>same-packet B3 1 | values 32 00 / 50 (1)<br>roles dominant serialized trade-state transition variant (1) | 45 03/11 00/00 02/32 00/01 00 -> 45 03/11 00/00 02/32 00/00 00 (1) | 55 00/c0 00/00 02/32 00/01 00 -> 55 00/d4 00/00 02/32 00/00 00 (1) | B2 11 00/32 00/08 02 (1)<br>B3 11 00 (1) | 80 b3 -> 80 bc x2 -> 80 b2 -> 80 bc x2 (1) | trade (1) | `trade-companion.txt:42` |", markdown);
        Assert.Contains("### `80 bc` Serialized Trade-State Transition Detections", markdown);
        Assert.Contains("| decode dominant serialized trade-state transition; complete short-state refresh | `32 00` (50)<br>dominant serialized trade-state transition variant | 80 b3 -> 80 bc x2 -> 80 b2 -> 80 bc x2 | rows -1; blocks 3 | B2 11 00/32 00/08 02; B3 11 00 | on `55 00/c0 00/00 02/32 00/01 00`<br>off `55 00/d4 00/00 02/32 00/00 00` | trade; B2 yes; B3 yes<br>80 b3 -> 80 bc x2 -> 80 b2 -> 80 bc x2 | `trade-companion.txt:42` on index 0, off index 1 |", markdown);
        Assert.Contains("| review serialized trade-state transition candidate; complete short-state refresh | `32 00` (50)<br>dominant serialized trade-state transition variant | 80 b3 -> 80 bc x2 -> 80 b2 -> 80 bc x2 | rows -1; blocks 3 | B2 11 00/32 00/08 02; B3 11 00 | on `45 00/aa 00/00 02/04 00/01 00`<br>off `45 00/bb 00/00 02/05 00/00 00` | trade; B2 yes; B3 yes<br>80 b3 -> 80 bc x2 -> 80 b2 -> 80 bc x2 | `trade-companion.txt:43` on index 0, off index 1 |", markdown);
        Assert.Contains("### `80 bc` Serialized Trade-State Field Roles", markdown);
        Assert.Contains("| trade byte 15 | serialized on/off flag; current pairs are on row 01 00 followed by off row 00 00 | 2 | 01 00 -> 00 00 (2) |", markdown);
        Assert.Contains("promote serialized trade-state transition candidate; complete short-state refresh (1)", markdown);
        Assert.Contains("collect before/after capture for companion byte-11 variant; complete short-state refresh (1)", markdown);
        Assert.Contains("| companion byte 11 | immediate non-trade 80 bc companion byte-11 pair after each on/off row | 2 |", markdown);
        Assert.Contains("32 00 / 50 -> 32 00 / 50 (1)", markdown);
        Assert.Contains("04 00 / 4 -> 05 00 / 5 (1)", markdown);
        Assert.Contains("### `80 bc` Trade-State Companion Byte-11 Map Leads", markdown);
        Assert.Contains("| `32 00` (50) | `32 00` (50) | `32 00` (50) | 32 00 (50) creation TradeCompanionByte11 (1); update TradeCompanionByte11 (1) -> 32 00 (50) creation TradeCompanionByte11 (1); update TradeCompanionByte11 (1) (1) | 32 00 (50) creation TradeCompanionByte11 (1) (1)<br>32 00 (50) update TradeCompanionByte11 (1) (1) | 32 00 (50) creation TradeCompanionByte11 (1) (1)<br>32 00 (50) update TradeCompanionByte11 (1) (1) | 1 | 2 | 1 | 2 | 0 | 0 | 0 | field0 pairs 1; field1 pairs 1; listed field1 pairs 1/1; on values 1; off values 1 | 1 hit (1) | 55 00 -> 55 00 (1) | c0 00 -> d4 00 (1) | c0 00 (192) creation OnCompanionState (1) -> d4 00 (212) update OffCompanionState (1) (1) | c0 00 (1) | d4 00 (1) | c0 00 (192) creation OnCompanionState (1) (1) | d4 00 (212) update OffCompanionState (1) (1) |", markdown);
        Assert.Contains("| `32 00` (50) | `04 00` (4) | `05 00` (5) | 04 00 (4) creation OtherOnCompanionByte11 (1) -> 05 00 (5) update OtherOffCompanionByte11 (1) (1) | 04 00 (4) creation OtherOnCompanionByte11 (1) (1) | 05 00 (5) update OtherOffCompanionByte11 (1) (1) | 1 | 2 | 0 | 2 | 0 | 0 | 0 | field0 pairs 1; field1 pairs 1; listed field1 pairs 1/1; on values 1; off values 1 | 1 hit (1) | 45 00 -> 45 00 (1) | aa 00 -> bb 00 (1) | aa 00 (170) creation OtherOnCompanionState (1) -> bb 00 (187) update OtherOffCompanionState (1) (1) | aa 00 (1) | bb 00 (1) | aa 00 (170) creation OtherOnCompanionState (1) (1) | bb 00 (187) update OtherOffCompanionState (1) (1) |", markdown);
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
                new Protocol04PacketSequenceSample(42, "server", new[] { "6d", "80 c1", "80 b3", "80 bc", "80 b2" })
            },
            Array.Empty<Protocol03ObjectViewSample>(),
            Array.Empty<Protocol04InteractionPayloadSample>(),
            new[]
            {
                new PlayerAttributePayloadSample(
                    42,
                    6,
                    "11 00",
                    17,
                    "c8 00",
                    200,
                    "08 02",
                    520,
                    "11 00 c8 00 08 02")
            },
            new[]
            {
                new ManageBonusPayloadSample(
                    42,
                    20,
                    "45 03",
                    837,
                    "11 00",
                    17,
                    "00 02",
                    512,
                    "45 03 11 00 00 02 00 00 00 11 00 c8 00 00 00 01 00 00 00 00")
            })
        {
            AbilityUnloadPayloads = new[]
            {
                new AbilityUnloadPayloadSample(
                    42,
                    2,
                    "11 00",
                    17,
                    "11 00")
            },
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
                    new[]
                    {
                        new Unknown6dEntryRecord("00", 0, "30 00", 0x30, "00 00 00"),
                        new Unknown6dEntryRecord("00", 0, "31 00", 0x31, "00 00 01")
                    },
                    "01 00 00 00 08 00 01 02 00 00 30 00 00 00 00 00 31 00 00 00 01")
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
        AttributeDefinition[] attributes =
        {
            new("test", "Object48", 48, "CreationAttr48", 1, 48, -1, "attributes.cs", 10),
            new("test", "Object49", 49, "UpdateAttr49", 1, -1, 49, "attributes.cs", 11),
            new("test", "Object200", 200, "StateMarker200", 1, 200, -1, "attributes.cs", 12)
        };
        AbilityDefinition[] abilities =
        {
            new(48, 48, "Ability48", "Ability Display", "abilities.csv", 3)
        };
        ItemCommandEntry[] items =
        {
            new(48, "TestItem", "Test Display", "items.txt", 4)
        };
        GameObjectEntry[] gameObjects =
        {
            new("TestObject", 49, "gameobjects.csv", 5),
            new("StateObject200", 200, "gameobjects.csv", 6)
        };
        AnimationDefinition[] animations =
        {
            new("3000", "0030", 48, "TestAnimation", "animations.txt", 6)
        };
        PacketResearchReport report = new(
            ".",
            Array.Empty<string>(),
            null,
            Array.Empty<string>(),
            Array.Empty<RpcHeaderEntry>(),
            attributes,
            Array.Empty<FxDefinition>(),
            gameObjects,
            Array.Empty<WorldEntityEntry>(),
            Array.Empty<RajkoRpcEntry>(),
            Array.Empty<HardcodedCommandExample>(),
            Array.Empty<Protocol03HardcodedExample>(),
            Array.Empty<Protocol04InteractionCommandExample>(),
            Array.Empty<VendorInventoryEntry>(),
            Array.Empty<RpcComparison>(),
            new[] { dump })
        {
            AbilityDefinitions = abilities,
            AnimationDefinitions = animations,
            ItemCommandEntries = items
        };

        Unknown6dListRecordShapeSummary unknown6dSummary =
            Assert.Single(PacketResearcher.BuildUnknown6dListRecordShapeSummaries(
                new[] { dump },
                attributes,
                abilities,
                items,
                gameObjects,
                animations));
        Assert.Equal(21, unknown6dSummary.PayloadLength);
        Assert.Equal("01 00", unknown6dSummary.ModeHex);
        Assert.Equal("00 00", unknown6dSummary.Field1);
        Assert.Equal(8, unknown6dSummary.ListOffset);
        Assert.Equal(1, unknown6dSummary.ListFlag);
        Assert.Equal(2, unknown6dSummary.EntryCount);
        Assert.Equal(2, unknown6dSummary.ParsedEntryCount);
        Assert.Equal(2, unknown6dSummary.TotalRecordCount);
        Assert.Equal(2, unknown6dSummary.DistinctEntryIdCount);
        Assert.Equal(2, unknown6dSummary.RecordPrefixes["00"]);
        Assert.Equal(1, unknown6dSummary.RecordTails["00 00 00"]);
        Assert.Equal(1, unknown6dSummary.RecordTails["00 00 01"]);
        Assert.Equal(2, unknown6dSummary.RecordTailU16Offset0Values["00 00 / 0"]);
        Assert.Equal(1, unknown6dSummary.RecordTailU16Offset1Values["00 00 / 0"]);
        Assert.Equal(1, unknown6dSummary.RecordTailU16Offset1Values["00 01 / 256"]);
        Assert.Equal(1, unknown6dSummary.EntryIds["48"]);
        Assert.Equal(1, unknown6dSummary.EntryIds["49"]);
        Assert.Equal(1, unknown6dSummary.EntryIdResourceReferences["48 -> ability:48 Ability48 \"Ability Display\""]);
        Assert.Equal(1, unknown6dSummary.EntryIdResourceReferences["48 -> animation:30 00 TestAnimation"]);
        Assert.Equal(1, unknown6dSummary.EntryIdResourceReferences["48 -> item:48 TestItem \"Test Display\""]);
        Assert.Equal(1, unknown6dSummary.EntryIdResourceReferences["49 -> gameobject:49 TestObject"]);
        Assert.Equal(1, unknown6dSummary.EntryIdAttributeReferences["48 -> creation CreationAttr48 (1)"]);
        Assert.Equal(1, unknown6dSummary.EntryIdAttributeReferences["49 -> update UpdateAttr49 (1)"]);
        Unknown6dEntryIdSummary[] entryIdSummaries = PacketResearcher.BuildUnknown6dEntryIdSummaries(
            new[] { dump },
            attributes,
            abilities,
            items,
            gameObjects,
            animations).ToArray();
        Unknown6dEntryIdSummary entry48 = entryIdSummaries.Single(summary => summary.EntryId == 0x30);
        Assert.Equal(1, entry48.PayloadCount);
        Assert.Equal(1, entry48.TotalRecordCount);
        Assert.Equal(1, entry48.EntryIdHexes["30 00"]);
        Assert.Equal(1, entry48.Modes["01 00"]);
        Assert.Equal(1, entry48.PayloadShapes["01 00 / 21 bytes / count 2"]);
        Assert.Equal(1, entry48.RecordPrefixes["00"]);
        Assert.Equal(1, entry48.RecordTails["00 00 00"]);
        Assert.Equal(1, entry48.RecordTailU16Offset1Values["00 00 / 0"]);
        Assert.Equal(1, entry48.ResourceReferences["ability:48 Ability48 \"Ability Display\""]);
        Assert.Equal(1, entry48.ResourceReferences["animation:30 00 TestAnimation"]);
        Assert.Equal(1, entry48.ResourceReferences["item:48 TestItem \"Test Display\""]);
        Assert.Equal(1, entry48.AttributeReferences["creation CreationAttr48 (1)"]);
        Unknown6dEntryTailFlagSummary tailFlagSummary = Assert.Single(PacketResearcher.BuildUnknown6dEntryTailFlagSummaries(
            new[] { dump },
            attributes,
            abilities,
            items,
            gameObjects,
            animations));
        Assert.Equal(0x31, tailFlagSummary.EntryId);
        Assert.Equal("00 00 01", tailFlagSummary.TailHex);
        Assert.Equal(1, tailFlagSummary.FlagRecordCount);
        Assert.Equal(1, tailFlagSummary.TotalEntryRecordCount);
        Assert.Equal(1, tailFlagSummary.FlagPayloadCount);
        Assert.Equal(1, tailFlagSummary.TotalEntryPayloadCount);
        Assert.Equal(1, tailFlagSummary.TailU16Offset1Values["00 01 / 256"]);
        Assert.Equal(1, tailFlagSummary.ResourceReferences["gameobject:49 TestObject"]);
        Assert.Equal(1, tailFlagSummary.AttributeReferences["update UpdateAttr49 (1)"]);
        Unknown6dPacketContextSummary contextSummary = Assert.Single(PacketResearcher.BuildUnknown6dPacketContextSummaries(
            new[] { dump },
            attributes,
            abilities,
            items,
            gameObjects,
            animations));
        Assert.Equal("01 00", contextSummary.ModeHex);
        Assert.Equal("00 00", contextSummary.Field1);
        Assert.Equal(1, contextSummary.PayloadCount);
        Assert.Equal(1, contextSummary.PacketCount);
        Assert.Equal(2, contextSummary.TotalRecordCount);
        Assert.Equal(2, contextSummary.DistinctEntryIdCount);
        Assert.Equal(1, contextSummary.NonzeroTailRecordCount);
        Assert.Equal(1, contextSummary.SamePacket80c1PayloadCount);
        Assert.Equal(1, contextSummary.EntryCounts["2"]);
        Assert.Equal(1, contextSummary.PreviousHeaders["packet start"]);
        Assert.Equal(1, contextSummary.NextHeaders["80 c1"]);
        Assert.Equal(1, contextSummary.SamePacket80c1Fields["00 00 -> c8 00 / 200"]);
        Assert.Equal(1, contextSummary.RecordTails["00 00 01"]);
        Assert.Equal(1, contextSummary.EntryIdResourceReferences["48 -> animation:30 00 TestAnimation"]);
        Assert.Equal(1, contextSummary.EntryIdAttributeReferences["49 -> update UpdateAttr49 (1)"]);
        Unknown80c1PacketContextSummary unknown80c1ContextSummary =
            Assert.Single(PacketResearcher.BuildUnknown80c1PacketContextSummaries(
                new[] { dump },
                attributes,
                abilities,
                items,
                gameObjects,
                animations));
        Assert.Equal("00 00", unknown80c1ContextSummary.Field0);
        Assert.Equal("c8 00", unknown80c1ContextSummary.Field1);
        Assert.Equal(1, unknown80c1ContextSummary.PayloadCount);
        Assert.Equal(1, unknown80c1ContextSummary.PacketCount);
        Assert.Equal(1, unknown80c1ContextSummary.SamePacket6dPayloadCount);
        Assert.Equal(1, unknown80c1ContextSummary.SamePacket80b2PayloadCount);
        Assert.Equal(1, unknown80c1ContextSummary.SamePacket80b3PayloadCount);
        Assert.Equal(1, unknown80c1ContextSummary.SamePacket80bcPayloadCount);
        Assert.Equal(1, unknown80c1ContextSummary.PayloadShapes["4 bytes / 00 00 -> c8 00 / 200"]);
        Assert.Equal(1, unknown80c1ContextSummary.PreviousHeaders["6d"]);
        Assert.Equal(1, unknown80c1ContextSummary.NextHeaders["80 b3"]);
        Assert.Equal(1, unknown80c1ContextSummary.SamePacket6dShapes["01 00 / 21 bytes / count 2"]);
        Assert.Equal(1, unknown80c1ContextSummary.SamePacket80b2Fields["11 00 -> c8 00 / 200 -> 08 02"]);
        Assert.Equal(1, unknown80c1ContextSummary.SamePacket80b3Fields["11 00 / 17"]);
        Assert.Equal(1, unknown80c1ContextSummary.SamePacket80bcShapes["45 03/11 00/00 02/byte11 c8 00/byte15 01 00"]);
        Assert.Empty(unknown80c1ContextSummary.Field0LocalReferences);
        Assert.Equal(1, unknown80c1ContextSummary.Field1LocalReferences["creation StateMarker200 (1)"]);
        Assert.Equal(1, unknown80c1ContextSummary.Field1LocalReferences["gameobject:200 StateObject200"]);

        string markdown = ReportWriter.ToMarkdown(report);

        Assert.Contains("- Protocol 04 unknown `6d` payloads: 1", markdown);
        Assert.Contains("- Protocol 04 unknown `80 c1` payloads: 1", markdown);
        Assert.Contains("## Unknown `6d` Protocol 04 Payload Groups", markdown);
        Assert.Contains("Record tails", markdown);
        Assert.Contains("Tail u16 +0", markdown);
        Assert.Contains("Entry id resources", markdown);
        Assert.Contains("Entry id attributes", markdown);
        Assert.Contains("00 00 00 (1)", markdown);
        Assert.Contains("00 00 01 (1)", markdown);
        Assert.Contains("48 -> animation:30 00 TestAnimation (1)", markdown);
        Assert.Contains("49 -> update UpdateAttr49 (1) (1)", markdown);
        Assert.Contains("## Unknown `6d` Packet Context Rollup", markdown);
        Assert.Contains("| `01 00` | `00 00` | 1 | 1 | 2 | 2 | 2 (1) | 1 | 1 | 00 00 -> c8 00 / 200 (1) | packet start (1) | 80 c1 (1) | 00 00 00 (1)<br>00 00 01 (1)", markdown);
        Assert.Contains("## Unknown `6d` Entry Id Correlations", markdown);
        Assert.Contains("| 48 | 30 00 (1) | 1 | 1 | 01 00 (1) | 01 00 / 21 bytes / count 2 (1)", markdown);
        Assert.Contains("ability:48 Ability48 \"Ability Display\" (1)<br>animation:30 00 TestAnimation (1)<br>item:48 TestItem \"Test Display\" (1)", markdown);
        Assert.Contains("creation CreationAttr48 (1) (1) | other (1) | 6d -> 80 c1 -> 80 b3 -> 80 bc -> 80 b2 (1) | `state.txt:42` |", markdown);
        Assert.Contains("## Unknown `6d` Nonzero Tail Flags", markdown);
        Assert.Contains("| 49 | 31 00 (1) | `00 00 01` | 00 00 / 0 (1) | 00 01 / 256 (1) | 1 | 1 | 1 | 1 | 01 00 (1) | 01 00 / 21 bytes / count 2 (1) | gameobject:49 TestObject (1) | update UpdateAttr49 (1) (1) | other (1) | 6d -> 80 c1 -> 80 b3 -> 80 bc -> 80 b2 (1) | `state.txt:42` |", markdown);
        Assert.Contains("## Unknown `80 c1` Protocol 04 Payload Groups", markdown);
        Assert.Contains("| 4 | `00 00` | `c8 00` | 1 | state.txt (1) | 6d -> 80 c1 -> 80 b3 -> 80 bc -> 80 b2 (1) | `state.txt:42` | `00 00 c8 00` |", markdown);
        Assert.Contains("## Unknown `80 c1` Packet Context Rollup", markdown);
        Assert.Contains("| `00 00` | `c8 00` (200) | 1 | 1 | 1 | 1 | 1 | 1 | 4 bytes / 00 00 -> c8 00 / 200 (1) | 6d (1) | 80 b3 (1) | 01 00 / 21 bytes / count 2 (1) | 11 00 -> c8 00 / 200 -> 08 02 (1) | 11 00 / 17 (1) | 45 03/11 00/00 02/byte11 c8 00/byte15 01 00 (1) | - | creation StateMarker200 (1) (1)<br>gameobject:200 StateObject200 (1) | other (1) | 6d -> 80 c1 -> 80 b3 -> 80 bc -> 80 b2 (1) | `state.txt:42` |", markdown);
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
    public void DetectProtocol03ObjectViews_RecognizesSingleStateMarkerSignature()
    {
        byte[] bytes = PacketResearcher.ParseHexBytes("02 03 00 04 01 00 4f").ToArray();

        Protocol03ObjectViewSample sample = PacketResearcher.DetectProtocol03ObjectViews(bytes, 5, "server").Single();

        Assert.Equal(1024, sample.ViewId);
        Assert.Equal(1, sample.UpdateCount);
        Assert.Equal("00", sample.FirstSelector);
        Assert.Equal("single state marker update", sample.Classification);
        Assert.True(sample.Complete);
        Protocol03ObjectUpdateSegment segment = Assert.Single(sample.Segments);
        Assert.Equal("00", segment.Selector);
        Assert.Equal("one-byte state marker", segment.Classification);
        Assert.Equal(1, segment.PayloadBytes);
        Assert.True(segment.IsKnownLength);
        Assert.Equal("4f", segment.PayloadPrefixHex);
    }

    [Fact]
    public void DetectProtocol03ObjectViews_RecognizesTwoUpdateStateMarkerLead()
    {
        byte[] bytes = PacketResearcher.ParseHexBytes("02 03 00 04 02 00 3a 09 16 80 bc").ToArray();

        Protocol03ObjectViewSample sample = PacketResearcher.DetectProtocol03ObjectViews(bytes, 6, "server").Single();

        Assert.Equal(1024, sample.ViewId);
        Assert.Equal(2, sample.UpdateCount);
        Assert.Equal("00", sample.FirstSelector);
        Assert.Equal("state marker/object update candidate", sample.Classification);
        Assert.False(sample.Complete);
        Assert.Equal(1, sample.ParsedUpdateCount);
        Assert.Equal(3, sample.UnparsedBytes);
        Assert.Collection(
            sample.Segments,
            segment =>
            {
                Assert.Equal("00", segment.Selector);
                Assert.Equal("one-byte state marker", segment.Classification);
                Assert.Equal(1, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
                Assert.Equal("3a", segment.PayloadPrefixHex);
            },
            segment =>
            {
                Assert.Equal("09", segment.Selector);
                Assert.Equal("unknown selector payload", segment.Classification);
                Assert.Equal(3, segment.PayloadBytes);
                Assert.False(segment.IsKnownLength);
                Assert.Equal("16 80 bc", segment.PayloadPrefixHex);
            });
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
            "02 03 01 00 02 0c 00 00 00 00 00 00 00 00 00 00 00 00 00 03 00 06 0e 01 18 58 3a f6 c6 00 00 be 42 44 35 af 46 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 02 ff 00 00 00 00 00 00 00 01 00 02 0e 05 93 a7 a6")
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
        Protocol03NestedMovementBoundaryEvidenceSummary[] evidenceSummaries =
            PacketResearcher.BuildProtocol03NestedMovementBoundaryEvidenceSummaries(new[] { dump })
                .ToArray();
        Protocol03NestedMovementParserActionSummary[] actionSummaries =
            PacketResearcher.BuildProtocol03NestedMovementParserActionSummaries(evidenceSummaries)
                .ToArray();
        Protocol03NestedMovementMode06BoundaryCandidateSummary[] mode06CandidateSummaries =
            PacketResearcher.BuildProtocol03NestedMovementMode06BoundaryCandidateSummaries(new[] { dump })
                .ToArray();
        Protocol03NestedMovementMode06FfContinuationSummary[] ffContinuationSummaries =
            PacketResearcher.BuildProtocol03NestedMovementMode06FfContinuationSummaries(new[] { dump })
                .ToArray();
        Protocol03NestedMovementMode06PostContinuationBoundarySummary[] postContinuationBoundarySummaries =
            PacketResearcher.BuildProtocol03NestedMovementMode06PostContinuationBoundarySummaries(new[] { dump })
                .ToArray();
        Protocol03NestedMovementMode06PostContinuationBodySummary[] postContinuationBodySummaries =
            PacketResearcher.BuildProtocol03NestedMovementMode06PostContinuationBodySummaries(new[] { dump })
                .ToArray();
        Protocol03NestedMovementMode06PostContinuationPrefixSummary[] postContinuationPrefixSummaries =
            PacketResearcher.BuildProtocol03NestedMovementMode06PostContinuationPrefixSummaries(new[] { dump })
                .ToArray();
        Protocol03NestedMovementMode06PostContinuationRemainderSummary[] postContinuationRemainderSummaries =
            PacketResearcher.BuildProtocol03NestedMovementMode06PostContinuationRemainderSummaries(new[] { dump })
                .ToArray();
        Protocol03NestedMovementMode06PostContinuationTupleSummary[] postContinuationTupleSummaries =
            PacketResearcher.BuildProtocol03NestedMovementMode06PostContinuationTupleSummaries(new[] { dump })
                .ToArray();
        Protocol03NestedMovementMode06PostContinuationTupleBoundarySummary[] postContinuationTupleBoundarySummaries =
            PacketResearcher.BuildProtocol03NestedMovementMode06PostContinuationTupleBoundarySummaries(new[] { dump })
                .ToArray();
        Assert.Equal(2, evidenceSummaries.Length);
        Assert.Equal("unbounded mode 06 object-state movement lead", evidenceSummaries[0].EvidenceDisposition);
        Assert.Equal("queue mode 06 object-state boundary investigation", evidenceSummaries[0].ParserAction);
        Assert.Equal(1, evidenceSummaries[0].WindowCount);
        Assert.Equal(0, evidenceSummaries[0].BoundedPrimaryWrapperCount);
        Assert.Equal(1, evidenceSummaries[0].UnresolvedPayloadLeadCount);
        Assert.Equal(1, evidenceSummaries[0].MarkerModes["06"]);
        Assert.Equal(1, evidenceSummaries[0].InnerSelectors["0e"]);
        Assert.Equal(1, evidenceSummaries[0].SuffixPrefixes["ff 00 00 00 10 00 00 00"]);
        Assert.Equal("bounded primary movement wrapper", evidenceSummaries[1].EvidenceDisposition);
        Assert.Equal("keep bounded primary movement wrapper", evidenceSummaries[1].ParserAction);
        Assert.Equal(1, evidenceSummaries[1].BoundedPrimaryWrapperCount);
        Assert.Equal(2, actionSummaries.Length);
        Assert.Equal("queue mode 06 object-state boundary investigation", actionSummaries[0].ParserAction);
        Assert.Equal(1, actionSummaries[0].WindowCount);
        Assert.Equal(1, actionSummaries[0].EvidenceDispositions["unbounded mode 06 object-state movement lead"]);
        Protocol03NestedMovementMode06BoundaryCandidateSummary mode06CandidateSummary = Assert.Single(mode06CandidateSummaries);
        Assert.Equal("ff marker plus zero-padded ff continuation lead", mode06CandidateSummary.TailPrefixKind);
        Assert.Equal("ff 00 00 00 10 00 00 00", mode06CandidateSummary.SuffixPrefixHex);
        Assert.Equal("00 00 01 ff 00 00 00 00", mode06CandidateSummary.PostSuffixPrefixHex);
        Assert.Equal(1, mode06CandidateSummary.WindowCount);
        Assert.Equal(1, mode06CandidateSummary.TailStartOffsets["17"]);
        Assert.Equal(1, mode06CandidateSummary.CandidateCutOffsets["25"]);
        Assert.Equal(1, mode06CandidateSummary.TailBytes["55"]);
        Protocol03NestedMovementMode06FfContinuationSummary ffContinuationSummary = Assert.Single(ffContinuationSummaries);
        Assert.Equal("ff marker plus zero-padded ff continuation lead", ffContinuationSummary.TailPrefixKind);
        Assert.Equal("ff 00 00 00", ffContinuationSummary.MarkerPrefixHex);
        Assert.Equal("10 00", ffContinuationSummary.TailU16Offset4Hex);
        Assert.Equal("00 00", ffContinuationSummary.TailU16Offset6Hex);
        Assert.Equal("00 00 01", ffContinuationSummary.ContinuationPrefixHex);
        Assert.Equal("ff", ffContinuationSummary.ContinuationMarkerHex);
        Assert.Equal("00 00 00 00", ffContinuationSummary.PostMarkerPrefixHex);
        Assert.Equal(1, ffContinuationSummary.TailStartOffsets["17"]);
        Assert.Equal(1, ffContinuationSummary.SuffixCutOffsets["25"]);
        Assert.Equal(1, ffContinuationSummary.ContinuationCutOffsets["33"]);
        Assert.Equal(1, ffContinuationSummary.TailBytes["55"]);
        Protocol03NestedMovementMode06PostContinuationBoundarySummary postContinuationBoundarySummary = Assert.Single(postContinuationBoundarySummaries);
        Assert.Equal("later object-view candidate after continuation", postContinuationBoundarySummary.BoundaryDisposition);
        Assert.Equal("ff marker plus zero-padded ff continuation lead", postContinuationBoundarySummary.TailPrefixKind);
        Assert.Equal(47, postContinuationBoundarySummary.TailBoundaryOffset);
        Assert.Equal(31, postContinuationBoundarySummary.PostContinuationOffset);
        Assert.Equal("00 00 00 00 00 00 00 00", postContinuationBoundarySummary.PreBoundaryPrefixHex);
        Assert.Equal("01 00 02 0e 05 93 a7 a6", postContinuationBoundarySummary.HeaderHex);
        Assert.Equal("object state update", postContinuationBoundarySummary.HeaderClassification);
        Assert.Equal(1, postContinuationBoundarySummary.TailStartOffsets["17"]);
        Assert.Equal(1, postContinuationBoundarySummary.ContinuationCutOffsets["33"]);
        Assert.Equal(1, postContinuationBoundarySummary.BoundaryCutOffsets["64"]);
        Assert.Equal(1, postContinuationBoundarySummary.TailBytes["55"]);
        Protocol03NestedMovementMode06PostContinuationBodySummary postContinuationBodySummary = Assert.Single(postContinuationBodySummaries);
        Assert.Equal("ff marker plus zero-padded ff continuation lead", postContinuationBodySummary.TailPrefixKind);
        Assert.Equal("nonzero body before next object-view", postContinuationBodySummary.BodyDisposition);
        Assert.Equal(31, postContinuationBodySummary.PostContinuationBodyBytes);
        Assert.Equal("00 00 00 00 00 00 00 00", postContinuationBodySummary.BodyPrefixHex);
        Assert.Equal("ff 00 00 00 00 00 00 00", postContinuationBodySummary.BodySuffixHex);
        Assert.Equal(22, postContinuationBodySummary.FirstNonZeroOffset);
        Assert.Equal("02 ff", postContinuationBodySummary.FirstNonZeroFieldHex);
        Assert.Equal(1, postContinuationBodySummary.TailStartOffsets["17"]);
        Assert.Equal(1, postContinuationBodySummary.ContinuationCutOffsets["33"]);
        Assert.Equal(1, postContinuationBodySummary.BoundaryCutOffsets["64"]);
        Assert.Equal(1, postContinuationBodySummary.HeaderClassifications["object state update"]);
        Assert.Equal(1, postContinuationBodySummary.HeaderPrefixes["01 00 02 0e 05 93 a7 a6"]);
        Protocol03NestedMovementMode06PostContinuationPrefixSummary postContinuationPrefixSummary = Assert.Single(postContinuationPrefixSummaries);
        Assert.Equal("ff marker plus zero-padded ff continuation lead", postContinuationPrefixSummary.TailPrefixKind);
        Assert.Equal("31-byte post-continuation prefix available", postContinuationPrefixSummary.PrefixDisposition);
        Assert.Equal(31, postContinuationPrefixSummary.PrefixBytes);
        Assert.Equal("00 00 00 00 00 00 00 00", postContinuationPrefixSummary.PrefixLeadHex);
        Assert.Equal("ff 00 00 00 00 00 00 00", postContinuationPrefixSummary.PrefixSuffixHex);
        Assert.Equal(22, postContinuationPrefixSummary.FirstNonZeroOffset);
        Assert.Equal("02 ff", postContinuationPrefixSummary.FirstNonZeroFieldHex);
        Assert.Equal(1, postContinuationPrefixSummary.BoundaryDispositions["later object-view candidate at +31"]);
        Assert.Equal(1, postContinuationPrefixSummary.PostContinuationBoundaryOffsets["31"]);
        Assert.Equal(1, postContinuationPrefixSummary.BytesAfterPrefix["8"]);
        Assert.Equal(1, postContinuationPrefixSummary.TailStartOffsets["17"]);
        Assert.Equal(1, postContinuationPrefixSummary.ContinuationCutOffsets["33"]);
        Assert.Equal(1, postContinuationPrefixSummary.BoundaryCutOffsets["64"]);
        Protocol03NestedMovementMode06PostContinuationRemainderSummary postContinuationRemainderSummary = Assert.Single(postContinuationRemainderSummaries);
        Assert.Equal("ff marker plus zero-padded ff continuation lead", postContinuationRemainderSummary.TailPrefixKind);
        Assert.Equal("02 ff", postContinuationRemainderSummary.PrefixFirstNonZeroFieldHex);
        Assert.Equal("object state update", postContinuationRemainderSummary.PostPrefixDisposition);
        Assert.Equal("01 00 02 0e 05 93 a7 a6", postContinuationRemainderSummary.PostPrefixLeadHex);
        Assert.Equal("01 00", postContinuationRemainderSummary.PostPrefixU16Offset0Hex);
        Assert.Equal("05 93", postContinuationRemainderSummary.PostPrefixU16Offset4Hex);
        Assert.Equal("a7 a6", postContinuationRemainderSummary.PostPrefixU16Offset6Hex);
        Assert.Equal(1, postContinuationRemainderSummary.BoundaryDispositions["later object-view candidate at +31"]);
        Assert.Equal(1, postContinuationRemainderSummary.PostContinuationBoundaryOffsets["31"]);
        Assert.Equal(1, postContinuationRemainderSummary.BytesAfterPrefix["8"]);
        Assert.Equal(1, postContinuationRemainderSummary.TailStartOffsets["17"]);
        Assert.Equal(1, postContinuationRemainderSummary.ContinuationCutOffsets["33"]);
        Assert.Equal(1, postContinuationRemainderSummary.BoundaryCutOffsets["64"]);
        Protocol03NestedMovementMode06PostContinuationTupleSummary postContinuationTupleSummary = Assert.Single(postContinuationTupleSummaries);
        Assert.Equal("ff marker plus zero-padded ff continuation lead", postContinuationTupleSummary.TailPrefixKind);
        Assert.Equal("02 ff", postContinuationTupleSummary.PrefixFirstNonZeroFieldHex);
        Assert.Equal("accepted object-view tuple", postContinuationTupleSummary.TupleLayoutKind);
        Assert.Equal("object state update", postContinuationTupleSummary.PostPrefixDisposition);
        Assert.Equal("01", postContinuationTupleSummary.Byte0Hex);
        Assert.Equal(1, postContinuationTupleSummary.Byte1Values["00"]);
        Assert.Equal("02", postContinuationTupleSummary.UpdateCountHex);
        Assert.Equal("0e", postContinuationTupleSummary.FirstSelectorHex);
        Assert.Equal("05 93 a7", postContinuationTupleSummary.MiddleBytesHex);
        Assert.Equal("a6", postContinuationTupleSummary.MarkerByteHex);
        Assert.Equal(1, postContinuationTupleSummary.BoundaryDispositions["later object-view candidate at +31"]);
        Assert.Equal(1, postContinuationTupleSummary.PostContinuationBoundaryOffsets["31"]);
        Assert.Equal(1, postContinuationTupleSummary.BytesAfterPrefix["8"]);
        Assert.Equal(1, postContinuationTupleSummary.PostPrefixLeads["01 00 02 0e 05 93 a7 a6"]);
        Assert.Equal(1, postContinuationTupleSummary.TailStartOffsets["17"]);
        Assert.Equal(1, postContinuationTupleSummary.ContinuationCutOffsets["33"]);
        Assert.Equal(1, postContinuationTupleSummary.BoundaryCutOffsets["64"]);
        Protocol03NestedMovementMode06PostContinuationTupleBoundarySummary postContinuationTupleBoundarySummary = Assert.Single(postContinuationTupleBoundarySummaries);
        Assert.Equal("ff marker plus zero-padded ff continuation lead", postContinuationTupleBoundarySummary.TailPrefixKind);
        Assert.Equal("02 ff", postContinuationTupleBoundarySummary.PrefixFirstNonZeroFieldHex);
        Assert.Equal("accepted object-view tuple", postContinuationTupleBoundarySummary.TupleLayoutKind);
        Assert.Equal("object state update", postContinuationTupleBoundarySummary.PostPrefixDisposition);
        Assert.Equal("tuple itself is accepted object-view boundary", postContinuationTupleBoundarySummary.BoundaryDisposition);
        Assert.Equal(47, postContinuationTupleBoundarySummary.TailBoundaryOffset);
        Assert.Equal(31, postContinuationTupleBoundarySummary.PostContinuationBoundaryOffset);
        Assert.Equal(0, postContinuationTupleBoundarySummary.PostTupleBodyBytes);
        Assert.Equal("01 00 02 0e 05 93 a7 a6", postContinuationTupleBoundarySummary.HeaderHex);
        Assert.Equal("object state update", postContinuationTupleBoundarySummary.HeaderClassification);
        Assert.Equal(1, postContinuationTupleBoundarySummary.BytesAfterPrefix["8"]);
        Assert.Equal(1, postContinuationTupleBoundarySummary.PostPrefixLeads["01 00 02 0e 05 93 a7 a6"]);
        Assert.Equal(1, postContinuationTupleBoundarySummary.Byte1Values["00"]);
        Assert.Equal(1, postContinuationTupleBoundarySummary.MarkerBytes["a6"]);
        Assert.Equal(1, postContinuationTupleBoundarySummary.TailStartOffsets["17"]);
        Assert.Equal(1, postContinuationTupleBoundarySummary.ContinuationCutOffsets["33"]);
        Assert.Equal(1, postContinuationTupleBoundarySummary.BoundaryCutOffsets["64"]);
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
            new[] { dump })
        {
            Protocol03NestedMovementBoundaryEvidenceSummaries = evidenceSummaries,
            Protocol03NestedMovementMode06BoundaryCandidateSummaries = mode06CandidateSummaries,
            Protocol03NestedMovementMode06FfContinuationSummaries = ffContinuationSummaries,
            Protocol03NestedMovementMode06PostContinuationBoundarySummaries = postContinuationBoundarySummaries,
            Protocol03NestedMovementMode06PostContinuationBodySummaries = postContinuationBodySummaries,
            Protocol03NestedMovementMode06PostContinuationPrefixSummaries = postContinuationPrefixSummaries,
            Protocol03NestedMovementMode06PostContinuationRemainderSummaries = postContinuationRemainderSummaries,
            Protocol03NestedMovementMode06PostContinuationTupleSummaries = postContinuationTupleSummaries,
            Protocol03NestedMovementMode06PostContinuationTupleBoundarySummaries = postContinuationTupleBoundarySummaries,
            Protocol03NestedMovementParserActionSummaries = actionSummaries
        };

        string markdown = ReportWriter.ToMarkdown(report);

        Assert.Contains("### Protocol 03 Embedded Movement Leads", markdown);
        Assert.Contains("Across 2 embedded movement windows, 2 outer selectors contain a plausible movement window.", markdown);
        Assert.Contains("| `02` | `0e` | 1 | 1 | 0 | unclassified object-view update | 14 | - | `selector14.txt:520` |", markdown);
        Assert.Contains("| `06` | `0e` | 1 | 0 | 1 | object state update | 03 | ff 00 00 00 10 00 00 00 | `selector14.txt:529` |", markdown);
        Assert.Contains("| `06` | `0e` | `ff 00 00 00 10 00 00 00` | `00 00 01 ff 00 00 00 00` | 1 | 0 | 1 | object state update | 03 | 72 | `selector14.txt:529` |", markdown);
        Assert.Contains("| `14` | unclassified object-view update | `00 0b` | 2 | `02` | `0e` | 1 | 19 |", markdown);
        Assert.Contains("| `03` | object state update | `-` | 0 | `06` | `0e` | 1 | 72 |", markdown);
        Assert.Contains("### Protocol 03 Embedded Movement Parser Actions", markdown);
        Assert.Contains("| queue mode 06 object-state boundary investigation | mode 06 windows are unresolved and have no bounded wrapper examples | 1 | 1 | 0 | 1 | unbounded mode 06 object-state movement lead (1) | 06 (1) | 0e (1) | 03 (1) | object state update (1) | ff 00 00 00 10 00 00 00 (1) | 00 00 01 ff 00 00 00 00 (1) | 72 (1) |", markdown);
        Assert.Contains("| keep bounded primary movement wrapper | known selector 12/13/14 wrapper length brackets the movement payload | 1 | 1 | 1 | 0 | bounded primary movement wrapper (1) | 02 (1) | 0e (1) | 14 (1) | unclassified object-view update (1) | - (1) | - (1) | 19 (1) |", markdown);
        Assert.Contains("### Protocol 03 Embedded Movement Boundary Evidence", markdown);
        Assert.Contains("| unbounded mode 06 object-state movement lead | queue mode 06 object-state boundary investigation | mode 06 windows are unresolved and have no bounded wrapper examples | 1 | 0 | 1 | 06 (1) | 0e (1) | 03 (1) | object state update (1) | ff 00 00 00 10 00 00 00 (1) | 00 00 01 ff 00 00 00 00 (1) | 72 (1) |", markdown);
        Assert.Contains("### Protocol 03 Mode 06 Movement Boundary Candidates", markdown);
        Assert.Contains("| ff marker plus zero-padded ff continuation lead | `ff 00 00 00 10 00 00 00` | `00 00 01 ff 00 00 00 00` | 1 | 17 (1) | 25 (1) | 55 (1) | - (1) | 0e (1) | 03 (1) | object state update (1) | 72 (1) | - (1) |", markdown);
        Assert.Contains("### Protocol 03 Mode 06 FF Continuation Fields", markdown);
        Assert.Contains("| ff marker plus zero-padded ff continuation lead | `ff 00 00 00` | `10 00` / 16 | `00 00` / 0 | `00 00 01` | `ff` | `00 00 00 00` | 1 | 17 (1) | 25 (1) | 33 (1) | 55 (1) | 0e (1) | 03 (1) | object state update (1) | 72 (1) |", markdown);
        Assert.Contains("### Protocol 03 Mode 06 Post-Continuation Boundary Leads", markdown);
        Assert.Contains("| ff marker plus zero-padded ff continuation lead | later object-view candidate after continuation | 47 | 31 | `00 00 00 00 00 00 00 00` | `01 00 02 0e 05 93 a7 a6` | object state update | 1 | 17 (1) | 33 (1) | 64 (1) | 55 (1) | 0e (1) | 03 (1) | object state update (1) | 72 (1) |", markdown);
        Assert.Contains("### Protocol 03 Mode 06 Post-Continuation Body Shapes", markdown);
        Assert.Contains("| ff marker plus zero-padded ff continuation lead | nonzero body before next object-view | 31 | `00 00 00 00 00 00 00 00` | `ff 00 00 00 00 00 00 00` | 22 | `02 ff` | 1 | 17 (1) | 33 (1) | 64 (1) | object state update (1) | 01 00 02 0e 05 93 a7 a6 (1) | 0e (1) | 03 (1) | object state update (1) | 72 (1) |", markdown);
        Assert.Contains("### Protocol 03 Mode 06 Post-Continuation Prefix Shapes", markdown);
        Assert.Contains("| ff marker plus zero-padded ff continuation lead | 31-byte post-continuation prefix available | 31 | `00 00 00 00 00 00 00 00` | `ff 00 00 00 00 00 00 00` | 22 | `02 ff` | 1 | later object-view candidate at +31 (1) | 31 (1) | 8 (1) | 17 (1) | 33 (1) | 64 (1) | 0e (1) | 03 (1) | object state update (1) | 72 (1) |", markdown);
        Assert.Contains("### Protocol 03 Mode 06 Post-Continuation Remainder Leads", markdown);
        Assert.Contains("| ff marker plus zero-padded ff continuation lead | `02 ff` | object state update | `01 00 02 0e 05 93 a7 a6` | `01 00` / 1 | `05 93` / 37637 | `a7 a6` / 42663 | 1 | later object-view candidate at +31 (1) | 31 (1) | 8 (1) | 17 (1) | 33 (1) | 64 (1) | 0e (1) | 03 (1) | object state update (1) | 72 (1) |", markdown);
        Assert.Contains("### Protocol 03 Mode 06 Post-Continuation Tuple Fields", markdown);
        Assert.Contains("| ff marker plus zero-padded ff continuation lead | `02 ff` | accepted object-view tuple | object state update | `01` | 00 (1) | `02` | `0e` | `05 93 a7` | `a6` | 1 | later object-view candidate at +31 (1) | 31 (1) | 8 (1) | 01 00 02 0e 05 93 a7 a6 (1) | 17 (1) | 33 (1) | 64 (1) | 0e (1) | 03 (1) | object state update (1) | 72 (1) |", markdown);
        Assert.Contains("### Protocol 03 Mode 06 Post-Prefix Tuple Boundary Scan", markdown);
        Assert.Contains("| ff marker plus zero-padded ff continuation lead | `02 ff` | accepted object-view tuple | object state update | tuple itself is accepted object-view boundary | 47 | 31 | 0 | `01 00 02 0e 05 93 a7 a6` | object state update | 1 | 8 (1) | 01 00 02 0e 05 93 a7 a6 (1) | 00 (1) | a6 (1) | 17 (1) | 33 (1) | 64 (1) | 0e (1) | 03 (1) | object state update (1) | 72 (1) |", markdown);
    }

    [Fact]
    public void BuildProtocol03NestedMovementMode06PostContinuationTupleBoundarySummaries_FindsLateBoundaryAfterZeroUpdateTuple()
    {
        byte[] mode06Bytes = PacketResearcher.ParseHexBytes(
            "02 03 01 00 02 0c 00 00 00 00 00 00 00 00 00 00 00 00 00 03 00 06 0e 01 18 58 3a f6 c6 00 00 be 42 44 35 af 46 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 03 ff 00 00 00 00 00 00 00 00 4b 00 00 00 00 00 01 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 01 00 02 0e 05 93 a7 a6")
            .ToArray();
        Protocol03ObjectViewSample mode06Sample = PacketResearcher.DetectProtocol03ObjectViews(mode06Bytes, 530, "server").Single();
        PacketDumpFileSummary dump = new(
            "mode06-long.txt",
            1,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            Array.Empty<Protocol04PacketSequenceSample>(),
            new[] { mode06Sample },
            Array.Empty<Protocol04InteractionPayloadSample>(),
            Array.Empty<PlayerAttributePayloadSample>(),
            Array.Empty<ManageBonusPayloadSample>());

        Protocol03NestedMovementMode06PostContinuationBoundarySummary postContinuationBoundarySummary =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06PostContinuationBoundarySummaries(new[] { dump }));
        Assert.Equal("no object-view candidate within 64 bytes after continuation", postContinuationBoundarySummary.BoundaryDisposition);
        Assert.Null(postContinuationBoundarySummary.TailBoundaryOffset);

        Protocol03NestedMovementMode06PostContinuationTupleBoundarySummary tupleBoundarySummary =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06PostContinuationTupleBoundarySummaries(new[] { dump }));
        Assert.Equal("zero-update marker tuple", tupleBoundarySummary.TupleLayoutKind);
        Assert.Equal("non-object-view post-prefix lead", tupleBoundarySummary.PostPrefixDisposition);
        Assert.Equal("later accepted object-view boundary after tuple", tupleBoundarySummary.BoundaryDisposition);
        Assert.Equal(91, tupleBoundarySummary.TailBoundaryOffset);
        Assert.Equal(75, tupleBoundarySummary.PostContinuationBoundaryOffset);
        Assert.Equal(36, tupleBoundarySummary.PostTupleBodyBytes);
        Assert.Equal("01 00 02 0e 05 93 a7 a6", tupleBoundarySummary.HeaderHex);
        Assert.Equal("object state update", tupleBoundarySummary.HeaderClassification);
        Assert.Equal(1, tupleBoundarySummary.PostPrefixLeads["00 4b 00 00 00 00 00 01"]);
        Assert.Equal(1, tupleBoundarySummary.Byte1Values["4b"]);
        Assert.Equal(1, tupleBoundarySummary.MarkerBytes["01"]);

        Protocol03NestedMovementMode06PostContinuationTupleBodySummary tupleBodySummary =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06PostContinuationTupleBodySummaries(new[] { dump }));
        Assert.Equal("zero-update marker tuple", tupleBodySummary.TupleLayoutKind);
        Assert.Equal("non-object-view post-prefix lead", tupleBodySummary.PostPrefixDisposition);
        Assert.Equal("zero-filled post-tuple body", tupleBodySummary.BodyDisposition);
        Assert.Equal(36, tupleBodySummary.PostTupleBodyBytes);
        Assert.Equal("00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00", tupleBodySummary.BodyPrefixHex);
        Assert.Equal("00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00", tupleBodySummary.BodySuffixHex);
        Assert.Null(tupleBodySummary.FirstNonZeroOffset);
        Assert.Equal(string.Empty, tupleBodySummary.FirstNonZeroFieldHex);
        Assert.Equal(1, tupleBodySummary.HeaderClassifications["object state update"]);
        Assert.Equal(1, tupleBodySummary.HeaderPrefixes["01 00 02 0e 05 93 a7 a6"]);
        Assert.Equal(1, tupleBodySummary.PostContinuationBoundaryOffsets["75"]);
        Assert.Equal(1, tupleBodySummary.PostPrefixLeads["00 4b 00 00 00 00 00 01"]);
        Assert.Equal(1, tupleBodySummary.Byte1Values["4b"]);
        Assert.Equal(1, tupleBodySummary.MarkerBytes["01"]);

        Protocol03NestedMovementMode06TupleBodyParserActionSummary parserAction =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06TupleBodyParserActionSummaries(new[] { tupleBodySummary }));
        Assert.Equal("hold mode 06 tuple body as evidence only", parserAction.ParserAction);
        Assert.Equal("low-count or non-dominant tuple body shape", parserAction.ActionReason);
        Assert.Equal(1, parserAction.EvidenceRowCount);
        Assert.Equal(1, parserAction.WindowCount);
        Assert.Equal(1, parserAction.BodyByteLengths["36"]);
        Assert.Equal(1, parserAction.FirstNonZeroOffsets["-"]);
        Assert.Equal(1, parserAction.FirstNonZeroFields["-"]);

        Protocol03NestedMovementMode06HeldTupleBodySummary heldBodySummary =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06HeldTupleBodySummaries(new[] { tupleBodySummary }));
        Assert.Equal("hold mode 06 tuple body as evidence only", heldBodySummary.ParserAction);
        Assert.Equal("bounded before accepted object-view header", heldBodySummary.SourceDisposition);
        Assert.Equal(36, heldBodySummary.BodyBytes);
        Assert.Equal(1, heldBodySummary.WindowCount);
        Assert.Equal(1, heldBodySummary.BodySuffixes["00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00"]);
        Assert.Equal(1, heldBodySummary.FirstNonZeroOffsets["-"]);
        Assert.Equal(1, heldBodySummary.HeaderClassifications["object state update"]);
        Assert.Equal(1, heldBodySummary.PostPrefixLeads["00 4b 00 00 00 00 00 01"]);

        Protocol03NestedMovementMode06HeldTupleBodySummary marker34HeldBodySummary = heldBodySummary with
        {
            ParserAction = "hold mode 06 marker-34 long object-view tuple body as evidence only",
            ActionReason = "variable-length marker-34 object-view body needs body-length rule",
            TupleLayoutKind = "unclassified object-view tuple",
            PostPrefixDisposition = "unclassified object-view update",
            BodyDisposition = "nonzero post-tuple body",
            BodyBytes = 123,
            BodySuffixes = new Dictionary<string, int>
            {
                ["01 00 02 0e 05 93 a7 a6 00 00 00 00 00 00 00 00"] = 1
            },
            FirstNonZeroOffsets = new Dictionary<string, int>
            {
                ["0"] = 1
            },
            FirstNonZeroFields = new Dictionary<string, int>
            {
                ["01 00"] = 1
            },
            HeaderClassifications = new Dictionary<string, int>
            {
                ["object state update"] = 1
            },
            HeaderPrefixes = new Dictionary<string, int>
            {
                ["01 00 02 0e 05 93 a7 a6"] = 1
            },
            PostPrefixLeads = new Dictionary<string, int>
            {
                ["00 e9 07 00 00 00 00 34"] = 1
            },
            MarkerBytes = new Dictionary<string, int>
            {
                ["34"] = 1
            },
            InnerSelectors = new Dictionary<string, int>
            {
                ["0e"] = 1
            }
        };
        Protocol03NestedMovementMode06HeldTupleBodyPrioritySummary[] prioritySummaries =
            PacketResearcher.BuildProtocol03NestedMovementMode06HeldTupleBodyPrioritySummaries(new[] { heldBodySummary, marker34HeldBodySummary })
                .ToArray();
        Assert.Collection(
            prioritySummaries,
            priority =>
            {
                Assert.Equal("investigate marker-34 long body length rule", priority.PriorityAction);
                Assert.Equal("singleton variable-length marker-34 object-view bodies remain bounded but unpromoted", priority.PriorityReason);
                Assert.Equal(1, priority.HeldRowCount);
                Assert.Equal(1, priority.WindowCount);
                Assert.Equal(1, priority.ParserActions["hold mode 06 marker-34 long object-view tuple body as evidence only"]);
                Assert.Equal(1, priority.BodyByteLengths["123"]);
                Assert.Equal(1, priority.PostPrefixLeads["00 e9 07 00 00 00 00 34"]);
                Assert.Equal(1, priority.MarkerBytes["34"]);
            },
            priority =>
            {
                Assert.Equal("retain low-count or non-dominant mode 06 controls", priority.PriorityAction);
                Assert.Equal("held tuple bodies lack repeated exact body-length evidence for parser promotion", priority.PriorityReason);
                Assert.Equal(1, priority.HeldRowCount);
                Assert.Equal(1, priority.WindowCount);
                Assert.Equal(1, priority.ParserActions["hold mode 06 tuple body as evidence only"]);
                Assert.Equal(1, priority.BodyByteLengths["36"]);
                Assert.Equal(1, priority.PostPrefixLeads["00 4b 00 00 00 00 00 01"]);
            });

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
            new[] { dump })
        {
            Protocol03NestedMovementMode06PostContinuationTupleBoundarySummaries = new[] { tupleBoundarySummary },
            Protocol03NestedMovementMode06PostContinuationTupleBodySummaries = new[] { tupleBodySummary },
            Protocol03NestedMovementMode06HeldTupleBodySummaries = new[] { heldBodySummary },
            Protocol03NestedMovementMode06HeldTupleBodyPrioritySummaries = prioritySummaries,
            Protocol03NestedMovementMode06TupleBodyParserActionSummaries = new[] { parserAction }
        };
        string markdown = ReportWriter.ToMarkdown(report);
        Assert.Contains("### Protocol 03 Mode 06 Post-Prefix Tuple Body Shapes", markdown);
        Assert.Contains("| ff marker plus zero-padded ff continuation lead | `03 ff` | zero-update marker tuple | non-object-view post-prefix lead | zero-filled post-tuple body | 36 | `00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00` | `00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00` | - | `-` | 1 | object state update (1) | 01 00 02 0e 05 93 a7 a6 (1) | 75 (1) |", markdown);
        Assert.Contains("### Protocol 03 Mode 06 Tuple Body Parser Actions", markdown);
        Assert.Contains("| hold mode 06 tuple body as evidence only | low-count or non-dominant tuple body shape | 1 | 1 |", markdown);
        Assert.Contains("### Protocol 03 Mode 06 Held Tuple Body Priority Rollup", markdown);
        Assert.Contains("| investigate marker-34 long body length rule | singleton variable-length marker-34 object-view bodies remain bounded but unpromoted | 1 | 1 |", markdown);
        Assert.Contains("hold mode 06 marker-34 long object-view tuple body as evidence only (1)", markdown);
        Assert.Contains("00 e9 07 00 00 00 00 34 (1)", markdown);
        Assert.Contains("| retain low-count or non-dominant mode 06 controls | held tuple bodies lack repeated exact body-length evidence for parser promotion | 1 | 1 |", markdown);
        Assert.Contains("hold mode 06 tuple body as evidence only (1)", markdown);
        Assert.Contains("00 4b 00 00 00 00 00 01 (1)", markdown);
        Assert.Contains("### Protocol 03 Mode 06 Held Tuple Body Rows", markdown);
        Assert.Contains("| hold mode 06 tuple body as evidence only | low-count or non-dominant tuple body shape | bounded before accepted object-view header | 1 | 36 |", markdown);
    }

    [Fact]
    public void BuildProtocol03NestedMovementMode06PostContinuationTerminalTupleBodySummaries_FindsTerminalBodyWithoutLateBoundary()
    {
        byte[] suffix = PacketResearcher.ParseHexBytes("07 08 00 00 00 00 00 00 22 00 00 00 00 00 00 00")
            .ToArray();
        byte[] body = Enumerable.Repeat((byte)0x00, 6)
            .Concat(new byte[] { 0xff, 0x00 })
            .Concat(Enumerable.Repeat((byte)0x00, 72))
            .Concat(suffix)
            .ToArray();
        var bytes = new List<byte>
        {
            0x02, 0x03,
            0x01, 0x00, 0x02, 0x0c
        };
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 13));
        bytes.Add(0x03);
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 06 0e 01 18 58 3a f6 c6 00 00 be 42 44 35 af 46"));
        bytes.AddRange(PacketResearcher.ParseHexBytes("ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 22));
        bytes.AddRange(PacketResearcher.ParseHexBytes("02 ff"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 7));
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 e9 07 00 00 00 00 34"));
        bytes.AddRange(body);

        Protocol03ObjectViewSample[] samples = PacketResearcher.DetectProtocol03ObjectViews(bytes, 531, "server").ToArray();

        Assert.NotEmpty(samples);
        Assert.True(
            samples[0].Complete,
            $"complete={samples[0].Complete} parsed={samples[0].ParsedUpdateCount} unparsed={samples[0].UnparsedBytes} " +
            $"segments={string.Join("; ", samples[0].Segments.Select(segment => $"{segment.Selector}:{segment.Classification}:{segment.PayloadBytes}"))} " +
            $"boundaries={string.Join("; ", samples[0].NestedMovementConsumedBodyBoundaries.Select(boundary => $"{boundary.ParserAction}:{boundary.PayloadBytes}:{boundary.PostTupleBodyBytes}:{boundary.BoundaryHeaderHex}"))}");
        Assert.Equal(0, samples[0].UnparsedBytes);
        Assert.Contains(samples[0].Segments, segment => segment.Classification == "mode 06 terminal 96-byte tuple body");
        Protocol03NestedMovementConsumedBodyBoundary consumedBoundary =
            Assert.Single(samples[0].NestedMovementConsumedBodyBoundaries);
        Assert.Equal("test terminal mode 06 96-byte tuple body", consumedBoundary.ParserAction);
        Assert.Equal(96, consumedBoundary.PostTupleBodyBytes);
        Assert.Equal("unclassified object-view tuple", consumedBoundary.TupleLayoutKind);
        Assert.Equal("terminal tail end", consumedBoundary.BoundaryHeaderClassification);
        Assert.Equal(string.Empty, consumedBoundary.BoundaryHeaderHex);

        PacketDumpFileSummary dump = new(
            "mode06-terminal-tuple-body.txt",
            1,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            Array.Empty<Protocol04PacketSequenceSample>(),
            new[] { samples[0] },
            Array.Empty<Protocol04InteractionPayloadSample>(),
            Array.Empty<PlayerAttributePayloadSample>(),
            Array.Empty<ManageBonusPayloadSample>());

        Protocol03NestedMovementMode06PostContinuationTupleBoundarySummary boundary =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06PostContinuationTupleBoundarySummaries(new[] { dump }));
        Assert.Equal("terminal 96-byte tuple body at tail end", boundary.BoundaryDisposition);
        Assert.Null(boundary.PostTupleBodyBytes);

        Assert.Empty(PacketResearcher.BuildProtocol03NestedMovementMode06PostContinuationTupleBodySummaries(new[] { dump }));

        Protocol03NestedMovementMode06PostContinuationTerminalTupleBodySummary terminalBody =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06PostContinuationTerminalTupleBodySummaries(new[] { dump }));
        Assert.Equal("ff marker plus zero-padded ff continuation lead", terminalBody.TailPrefixKind);
        Assert.Equal("02 ff", terminalBody.PrefixFirstNonZeroFieldHex);
        Assert.Equal("unclassified object-view tuple", terminalBody.TupleLayoutKind);
        Assert.Equal("unclassified object-view update", terminalBody.PostPrefixDisposition);
        Assert.Equal("nonzero terminal post-tuple body", terminalBody.BodyDisposition);
        Assert.Equal(96, terminalBody.TerminalPostTupleBodyBytes);
        Assert.Equal(1, terminalBody.WindowCount);
        Assert.Equal(1, terminalBody.BodyPrefixes["00 00 00 00 00 00 ff 00 00 00 00 00 00 00 00 00"]);
        Assert.Equal(1, terminalBody.BodySuffixes["07 08 00 00 00 00 00 00 22 00 00 00 00 00 00 00"]);
        Assert.Equal(1, terminalBody.PreTailFieldOffsets["-"]);
        Assert.Equal(1, terminalBody.PreTailFieldBytes["-"]);
        Assert.Equal(1, terminalBody.FirstNonZeroOffsets["6"]);
        Assert.Equal(1, terminalBody.FirstNonZeroFields["ff 00"]);
        Assert.Equal(1, terminalBody.BytesAfterPrefix["104"]);
        Assert.Equal(1, terminalBody.PostPrefixLeads["00 e9 07 00 00 00 00 34"]);
        Assert.Equal(1, terminalBody.Byte1Values["e9"]);
        Assert.Equal(1, terminalBody.MarkerBytes["34"]);

        Protocol03NestedMovementMode06TupleBodyParserActionSummary parserAction =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06TupleBodyParserActionSummaries(
                Array.Empty<Protocol03NestedMovementMode06PostContinuationTupleBodySummary>(),
                new[] { terminalBody }));
        Assert.Equal("test terminal mode 06 96-byte tuple body", parserAction.ParserAction);
        Assert.Equal(1, parserAction.BodyByteLengths["96"]);
        Assert.Equal(1, parserAction.HeaderClassifications["terminal tail end"]);
        Assert.Equal(1, parserAction.HeaderPrefixes["-"]);

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
            new[] { dump })
        {
            Protocol03NestedMovementMode06PostContinuationTupleBoundarySummaries = new[] { boundary },
            Protocol03NestedMovementMode06PostContinuationTerminalTupleBodySummaries = new[] { terminalBody }
        };
        string markdown = ReportWriter.ToMarkdown(report);
        Assert.Contains("### Protocol 03 Mode 06 Terminal Tuple Body Shapes", markdown);
        Assert.Contains("| ff marker plus zero-padded ff continuation lead | `02 ff` | unclassified object-view tuple | unclassified object-view update | nonzero terminal post-tuple body | 96 | 1 | 00 00 00 00 00 00 ff 00 00 00 00 00 00 00 00 00 (1) | 07 08 00 00 00 00 00 00 22 00 00 00 00 00 00 00 (1) | - (1) | - (1) | 6 (1) | ff 00 (1) | 104 (1) | 00 e9 07 00 00 00 00 34 (1) |", markdown);
    }

    [Fact]
    public void DetectProtocol03ObjectViews_BoundsTerminalMode0696ByteMarker04ZeroUpdateTupleBody()
    {
        byte[] bytes = PacketResearcher.ParseHexBytes("""
            82 ab 4c a1 47 03 37 00 02 0e 01 1a 30 63 8b 47 00 00 be 42 8d 05 3f 47 2f 00 02 0c 3b 86 6c 88
            47 00 00 be 42 bf 0a 41 47 29 00 02 0e 04 ff d8 cb 92 47 00 00 be 42 b1 fc 31 47 25 00 06 0e 04
            38 58 90 8d 47 00 00 be 42 ca 5c 3d 47 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00
            00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 03 ff 00 00 00 00 00 00 00 00 96 00 00
            00 00 00 04 00 00 00 00 00 00 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
            00 00 00 00 00 00 00 00 00 01 00 96 00 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 ff 00
            00 00 00 00 00 00 00 00 00 00 00 39 03 00 08 37 08 00 00 1f 42 21 00 00 00 00 00 00 22 00 00 00
            00 00 00 00
            """).ToArray();

        Protocol03ObjectViewSample[] samples = PacketResearcher.DetectProtocol03ObjectViews(bytes, 541, "server").ToArray();

        const string expectedParserAction = "test terminal mode 06 96-byte marker-04 zero-update tuple body";
        Protocol03ObjectViewSample sample = Assert.Single(
            samples,
            candidate => candidate.NestedMovementConsumedBodyBoundaries.Any(boundary => boundary.ParserAction == expectedParserAction));
        Assert.True(sample.Complete);
        Assert.Equal(0, sample.UnparsedBytes);
        Assert.Contains(
            sample.Segments,
            segment =>
                segment.Selector == "25" &&
                segment.Classification == "mode 06 terminal 96-byte marker-04 zero-update tuple body" &&
                segment.PayloadBytes == 168 &&
                segment.IsKnownLength);

        Protocol03NestedMovementConsumedBodyBoundary consumedBoundary = Assert.Single(sample.NestedMovementConsumedBodyBoundaries);
        Assert.Equal(expectedParserAction, consumedBoundary.ParserAction);
        Assert.Equal("25", consumedBoundary.Selector);
        Assert.Equal(168, consumedBoundary.PayloadBytes);
        Assert.Equal(168, consumedBoundary.EvidencePayloadBytes);
        Assert.Equal(17, consumedBoundary.TailStartOffset);
        Assert.Equal(151, consumedBoundary.TailBoundaryOffset);
        Assert.Equal(96, consumedBoundary.PostTupleBodyBytes);
        Assert.Equal("zero-update marker tuple", consumedBoundary.TupleLayoutKind);
        Assert.Equal(6, consumedBoundary.FirstNonZeroOffset);
        Assert.Equal("ff 00", consumedBoundary.FirstNonZeroFieldHex);
        Assert.Equal(string.Empty, consumedBoundary.BoundaryHeaderHex);
        Assert.Equal("terminal tail end", consumedBoundary.BoundaryHeaderClassification);

        PacketDumpFileSummary dump = new(
            "mode06-terminal-marker-04-zero-update-96.txt",
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

        Protocol03NestedMovementMode06PostContinuationTerminalTupleBodySummary terminalBody =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06PostContinuationTerminalTupleBodySummaries(new[] { dump }));
        Assert.Equal("ff marker plus zero-padded ff continuation lead", terminalBody.TailPrefixKind);
        Assert.Equal("03 ff", terminalBody.PrefixFirstNonZeroFieldHex);
        Assert.Equal("zero-update marker tuple", terminalBody.TupleLayoutKind);
        Assert.Equal("non-object-view post-prefix lead", terminalBody.PostPrefixDisposition);
        Assert.Equal(96, terminalBody.TerminalPostTupleBodyBytes);
        Assert.Equal(1, terminalBody.BodyPrefixes["00 00 00 00 00 00 ff 00 00 00 00 00 00 00 00 00"]);
        Assert.Equal(1, terminalBody.BodySuffixes["42 21 00 00 00 00 00 00 22 00 00 00 00 00 00 00"]);
        Assert.Equal(1, terminalBody.PostPrefixLeads["00 96 00 00 00 00 00 04"]);
        Assert.Equal(1, terminalBody.MarkerBytes["04"]);
        Assert.Equal(1, terminalBody.InnerSelectors["0e"]);
        Assert.Equal(1, terminalBody.OuterSelectors["25"]);

        Protocol03NestedMovementMode06TupleBodyParserActionSummary parserAction =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06TupleBodyParserActionSummaries(
                Array.Empty<Protocol03NestedMovementMode06PostContinuationTupleBodySummary>(),
                new[] { terminalBody }));
        Assert.Equal(expectedParserAction, parserAction.ParserAction);
        Assert.Equal("terminal 96-byte marker-04 zero-update body with shared suffix", parserAction.ActionReason);
        Assert.Equal(1, parserAction.BodyByteLengths["96"]);
        Assert.Equal(1, parserAction.MarkerBytes["04"]);
    }

    [Fact]
    public void BuildProtocol03NestedMovementMode06PostContinuationTerminalTupleBodySummaries_SplitsMixedTerminal96Suffixes()
    {
        const string acceptedSuffixHex = "07 08 00 00 00 00 00 00 22 00 00 00 00 00 00 00";
        const string unsupportedSuffixHex = "13 08 00 00 00 00 00 00 22 00 00 00 00 00 00 00";
        Protocol03ObjectViewSample acceptedSample = DetectSample(acceptedSuffixHex, 531);
        Protocol03ObjectViewSample unsupportedSample = DetectSample(unsupportedSuffixHex, 532);
        PacketDumpFileSummary dump = new(
            "mode06-terminal-mixed-suffixes.txt",
            2,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            Array.Empty<Protocol04PacketSequenceSample>(),
            new[] { acceptedSample, unsupportedSample },
            Array.Empty<Protocol04InteractionPayloadSample>(),
            Array.Empty<PlayerAttributePayloadSample>(),
            Array.Empty<ManageBonusPayloadSample>());

        Protocol03NestedMovementMode06PostContinuationTerminalTupleBodySummary[] terminalBodies =
            PacketResearcher.BuildProtocol03NestedMovementMode06PostContinuationTerminalTupleBodySummaries(new[] { dump }).ToArray();

        Assert.Equal(2, terminalBodies.Length);
        Protocol03NestedMovementMode06PostContinuationTerminalTupleBodySummary acceptedBody =
            Assert.Single(terminalBodies, summary => summary.BodySuffixes.ContainsKey(acceptedSuffixHex));
        Protocol03NestedMovementMode06PostContinuationTerminalTupleBodySummary unsupportedBody =
            Assert.Single(terminalBodies, summary => summary.BodySuffixes.ContainsKey(unsupportedSuffixHex));
        Assert.Equal(1, acceptedBody.WindowCount);
        Assert.Equal(1, unsupportedBody.WindowCount);

        Protocol03NestedMovementMode06TupleBodyParserActionSummary[] parserActions =
            PacketResearcher.BuildProtocol03NestedMovementMode06TupleBodyParserActionSummaries(
                Array.Empty<Protocol03NestedMovementMode06PostContinuationTupleBodySummary>(),
                terminalBodies).ToArray();
        Protocol03NestedMovementMode06TupleBodyParserActionSummary acceptedAction =
            Assert.Single(parserActions, summary => summary.ParserAction == "test terminal mode 06 96-byte tuple body");
        Protocol03NestedMovementMode06TupleBodyParserActionSummary heldAction =
            Assert.Single(parserActions, summary => summary.ParserAction == "hold mode 06 tuple body as evidence only");
        Assert.Equal(1, acceptedAction.WindowCount);
        Assert.Equal(1, acceptedAction.BodyByteLengths["96"]);
        Assert.Equal(1, heldAction.WindowCount);
        Assert.Equal(1, heldAction.BodyByteLengths["96"]);

        static Protocol03ObjectViewSample DetectSample(string suffixHex, int line)
        {
            byte[] suffix = PacketResearcher.ParseHexBytes(suffixHex).ToArray();
            byte[] body = Enumerable.Repeat((byte)0x00, 6)
                .Concat(new byte[] { 0xff, 0x00 })
                .Concat(Enumerable.Repeat((byte)0x00, 72))
                .Concat(suffix)
                .ToArray();
            var bytes = new List<byte>
            {
                0x02, 0x03,
                0x01, 0x00, 0x02, 0x0c
            };
            bytes.AddRange(Enumerable.Repeat((byte)0x00, 13));
            bytes.Add(0x03);
            bytes.AddRange(PacketResearcher.ParseHexBytes("00 06 0e 01 18 58 3a f6 c6 00 00 be 42 44 35 af 46"));
            bytes.AddRange(PacketResearcher.ParseHexBytes("ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00"));
            bytes.AddRange(Enumerable.Repeat((byte)0x00, 22));
            bytes.AddRange(PacketResearcher.ParseHexBytes("02 ff"));
            bytes.AddRange(Enumerable.Repeat((byte)0x00, 7));
            bytes.AddRange(PacketResearcher.ParseHexBytes("00 e9 07 00 00 00 00 34"));
            bytes.AddRange(body);

            return PacketResearcher.DetectProtocol03ObjectViews(bytes, line, "server").First();
        }
    }

    [Fact]
    public void BuildProtocol03NestedMovementMode06LongTupleBodyTailAnchorSummaries_PreservesPreAnchorSamples()
    {
        byte[] suffix = PacketResearcher.ParseHexBytes("07 08 00 00 00 00 00 00 22 00 00 00 00 00 00 00")
            .ToArray();
        byte[] terminal96Body = Enumerable.Repeat((byte)0x00, 6)
            .Concat(new byte[] { 0xff, 0x00 })
            .Concat(Enumerable.Repeat((byte)0x00, 72))
            .Concat(suffix)
            .ToArray();
        byte[] preAnchor = PacketResearcher.ParseHexBytes("00 00 00 00 00 00 ff 00 00 e9 07 00 00 00 00 34").ToArray();
        byte[] body = preAnchor.Concat(terminal96Body).ToArray();
        var bytes = new List<byte>
        {
            0x02, 0x03,
            0x01, 0x00, 0x02, 0x0c
        };
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 13));
        bytes.Add(0x03);
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 06 0e 01 18 58 3a f6 c6 00 00 be 42 44 35 af 46"));
        bytes.AddRange(PacketResearcher.ParseHexBytes("ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 22));
        bytes.AddRange(PacketResearcher.ParseHexBytes("02 ff"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 7));
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 e9 07 00 00 00 00 34"));
        bytes.AddRange(body);

        Protocol03ObjectViewSample[] samples = PacketResearcher.DetectProtocol03ObjectViews(bytes, 531, "server").ToArray();
        Assert.NotEmpty(samples);

        PacketDumpFileSummary dump = new(
            "mode06-long-terminal-tuple-body.txt",
            1,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            Array.Empty<Protocol04PacketSequenceSample>(),
            new[] { samples[0] },
            Array.Empty<Protocol04InteractionPayloadSample>(),
            Array.Empty<PlayerAttributePayloadSample>(),
            Array.Empty<ManageBonusPayloadSample>());

        Protocol03NestedMovementMode06PostContinuationTerminalTupleBodySummary terminalBody =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06PostContinuationTerminalTupleBodySummaries(new[] { dump }));
        Assert.Equal(112, terminalBody.TerminalPostTupleBodyBytes);
        Assert.Equal(1, terminalBody.PreAnchorBodyPrefixes["00 00 00 00 00 00 ff 00 00 e9 07 00 00 00 00 34"]);
        Assert.Equal(1, terminalBody.PreAnchorBodySuffixes["00 00 00 00 00 00 ff 00 00 e9 07 00 00 00 00 34"]);
        Assert.Equal(1, terminalBody.PreAnchorFirstNonZeroOffsets["6"]);
        Assert.Equal(1, terminalBody.PreAnchorFirstNonZeroFields["ff 00"]);
        Assert.Equal(1, terminalBody.PreAnchorTrailingLeads["00 e9 07 00 00 00 00 34"]);
        Assert.Equal(1, terminalBody.PreAnchorPreLeadBytes["00 00 00 00 00 00 ff 00"]);

        Protocol03NestedMovementMode06LongTupleBodyTailAnchorSummary longAnchor =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06LongTupleBodyTailAnchorSummaries(
                Array.Empty<Protocol03NestedMovementMode06PostContinuationTupleBodySummary>(),
                new[] { terminalBody }));
        Assert.Equal("terminal tail end", longAnchor.SourceDisposition);
        Assert.Equal(112, longAnchor.BodyBytes);
        Assert.Equal(1, longAnchor.TailAnchorKinds["trailing terminal-96 state suffix"]);
        Assert.Equal(1, longAnchor.InferredTrailingBodyBytes["96"]);
        Assert.Equal(1, longAnchor.PreAnchorBodyBytes["16"]);
        Assert.Equal(1, longAnchor.PreAnchorBodyPrefixes["00 00 00 00 00 00 ff 00 00 e9 07 00 00 00 00 34"]);
        Assert.Equal(1, longAnchor.PreAnchorBodySuffixes["00 00 00 00 00 00 ff 00 00 e9 07 00 00 00 00 34"]);
        Assert.Equal(1, longAnchor.PreAnchorFirstNonZeroOffsets["6"]);
        Assert.Equal(1, longAnchor.PreAnchorFirstNonZeroFields["ff 00"]);
        Assert.Equal(1, longAnchor.PreAnchorTrailingLeads["00 e9 07 00 00 00 00 34"]);
        Assert.Equal(1, longAnchor.PreAnchorPreLeadBytes["00 00 00 00 00 00 ff 00"]);

        Protocol03NestedMovementMode06LongTupleBodyPreAnchorLeadSummary preAnchorLead =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06LongTupleBodyPreAnchorLeadSummaries(new[] { longAnchor }));
        Assert.Equal("hold mode 06 pre-anchor lead as evidence only", preAnchorLead.ParserTarget);
        Assert.Equal("singleton pre-anchor lead or no repeated known trailing-body family", preAnchorLead.ActionReason);
        Assert.Equal("00 e9 07 00 00 00 00 34", preAnchorLead.PreAnchorTrailingLeadHex);
        Assert.Equal("00 00 00 00 00 00 ff 00", preAnchorLead.PreAnchorPreLeadHex);
        Assert.Equal("trailing terminal-96 state suffix", preAnchorLead.TailAnchorKind);
        Assert.Equal(1, preAnchorLead.WindowCount);
        Assert.Equal(1, preAnchorLead.BodyByteLengths["112"]);
        Assert.Equal(1, preAnchorLead.PreAnchorBodyBytes["16"]);

        Protocol03NestedMovementMode06LongTupleBodyPreAnchorBodySummary preAnchorBody =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06LongTupleBodyPreAnchorBodySummaries(new[] { dump }));
        Assert.Equal("hold mode 06 pre-anchor lead as evidence only", preAnchorBody.ParserTarget);
        Assert.Equal("terminal tail end", preAnchorBody.SourceDisposition);
        Assert.Equal("trailing terminal-96 state suffix", preAnchorBody.TailAnchorKind);
        Assert.Equal("00 e9 07 00 00 00 00 34", preAnchorBody.PreAnchorTrailingLeadHex);
        Assert.Equal("00 00 00 00 00 00 ff 00", preAnchorBody.PreAnchorPreLeadHex);
        Assert.Equal(112, preAnchorBody.BodyBytes);
        Assert.Equal(16, preAnchorBody.PreAnchorBodyBytes);
        Assert.Equal(96, preAnchorBody.InferredTrailingBodyBytes);
        Assert.Equal(1, preAnchorBody.WindowCount);
        Assert.Equal(1, preAnchorBody.PreAnchorNonZeroByteOffsets["6:ff 9:e9 10:07 15:34"]);
        Assert.Equal(1, preAnchorBody.PreAnchorHeaderLikeOffsets["8:unclassified object-view update"]);
        Assert.Equal(1, preAnchorBody.PreAnchorPrefixes["00 00 00 00 00 00 ff 00 00 e9 07 00 00 00 00 34"]);
        Assert.Equal(1, preAnchorBody.PreAnchorFirstNonZeroWindows["00 00 00 00 00 00 ff 00 00 e9 07 00 00 00 00 34"]);
        Assert.Equal(1, preAnchorBody.PreAnchorSuffixes["00 00 00 00 00 00 ff 00 00 e9 07 00 00 00 00 34"]);

        Protocol03NestedMovementMode06LongTupleBodyPreAnchorOffsetSummary[] preAnchorOffsets =
            PacketResearcher.BuildProtocol03NestedMovementMode06LongTupleBodyPreAnchorOffsetSummaries(new[] { dump }).ToArray();
        Protocol03NestedMovementMode06LongTupleBodyPreAnchorOffsetSummary offset6 =
            Assert.Single(preAnchorOffsets, summary => summary.Offset == 6);
        Assert.Equal(1, offset6.WindowCount);
        Assert.Equal(1, offset6.NonZeroCount);
        Assert.Equal(0, offset6.HeaderLikeCount);
        Assert.Equal(1, offset6.ByteValues["ff"]);
        Assert.Equal(1, offset6.TwoByteValues["ff 00"]);

        Protocol03NestedMovementMode06LongTupleBodyPreAnchorOffsetSummary offset8 =
            Assert.Single(preAnchorOffsets, summary => summary.Offset == 8);
        Assert.Equal(1, offset8.WindowCount);
        Assert.Equal(0, offset8.NonZeroCount);
        Assert.Equal(1, offset8.HeaderLikeCount);
        Assert.Equal(1, offset8.HeaderClassifications["unclassified object-view update"]);

        Protocol03NestedMovementMode06LongTupleBodyPreAnchorHeaderCandidateSummary headerCandidate =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06LongTupleBodyPreAnchorHeaderCandidateSummaries(new[] { dump }));
        Assert.Equal(8, headerCandidate.Offset);
        Assert.Equal("singleton pre-anchor header-like collision", headerCandidate.CandidateDisposition);
        Assert.Equal(1, headerCandidate.WindowCount);
        Assert.Equal(1, headerCandidate.ViewIds["59648"]);
        Assert.Equal(1, headerCandidate.UpdateCounts["07"]);
        Assert.Equal(1, headerCandidate.FirstSelectors["00"]);
        Assert.Equal(1, headerCandidate.HeaderClassifications["unclassified object-view update"]);
        Assert.Equal(1, headerCandidate.FourByteWindows["00 e9 07 00"]);
        Assert.Equal(1, headerCandidate.NextHeaderLikeOffsets["-"]);
        Assert.Equal(1, headerCandidate.BytesToNextHeaderLike["-"]);
        Assert.Equal(1, headerCandidate.BytesToPreAnchorEnd["8"]);
    }

    [Fact]
    public void BuildProtocol03NestedMovementMode06LongTupleBodyPreAnchorEmbeddedMovementTrailerSummaries_GroupsSharedTrailer()
    {
        byte[] suffix = PacketResearcher.ParseHexBytes("07 08 00 00 00 00 00 00 22 00 00 00 00 00 00 00")
            .ToArray();
        byte[] terminal96Body = Enumerable.Repeat((byte)0x00, 6)
            .Concat(new byte[] { 0xff, 0x00 })
            .Concat(Enumerable.Repeat((byte)0x00, 72))
            .Concat(suffix)
            .ToArray();
        byte[] preAnchor = new byte[167];
        preAnchor[6] = 0xff;
        PacketResearcher.ParseHexBytes("00 06 0e 00 00 00 00 80 3f 00 00 00 40 00 00 40 40")
            .ToArray()
            .CopyTo(preAnchor, 95);
        PacketResearcher.ParseHexBytes("ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00")
            .ToArray()
            .CopyTo(preAnchor, 112);
        preAnchor[150] = 0x02;
        PacketResearcher.ParseHexBytes("ff cd cc 4c be 00 00 00 00 e9 07 00 00 00 00 34")
            .ToArray()
            .CopyTo(preAnchor, 151);
        byte[] body = preAnchor.Concat(terminal96Body).ToArray();
        var bytes = new List<byte>
        {
            0x02, 0x03,
            0x01, 0x00, 0x02, 0x0c
        };
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 13));
        bytes.Add(0x03);
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 06 0e 01 18 58 3a f6 c6 00 00 be 42 44 35 af 46"));
        bytes.AddRange(PacketResearcher.ParseHexBytes("ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 22));
        bytes.AddRange(PacketResearcher.ParseHexBytes("02 ff"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 7));
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 e9 07 00 00 00 00 34"));
        bytes.AddRange(body);

        Protocol03ObjectViewSample[] samples = PacketResearcher.DetectProtocol03ObjectViews(bytes, 531, "server").ToArray();
        Protocol03ObjectViewSample sample = Assert.Single(samples);
        Assert.True(sample.Complete);
        Assert.Equal(0, sample.UnparsedBytes);
        Assert.Contains(
            sample.Segments,
            segment => segment.Classification == "mode 06 terminal long embedded movement marker-34 trailer body" &&
                segment.PayloadBytes == 335 &&
                segment.IsKnownLength);

        Protocol03NestedMovementConsumedBodyBoundary consumedBoundary = Assert.Single(sample.NestedMovementConsumedBodyBoundaries);
        Assert.Equal("test terminal mode 06 long embedded marker-34 trailer body", consumedBoundary.ParserAction);
        Assert.Equal("03", consumedBoundary.Selector);
        Assert.Equal(17, consumedBoundary.Offset);
        Assert.Equal(335, consumedBoundary.PayloadBytes);
        Assert.Equal(335, consumedBoundary.EvidencePayloadBytes);
        Assert.Equal(17, consumedBoundary.TailStartOffset);
        Assert.Equal(318, consumedBoundary.TailBoundaryOffset);
        Assert.Equal(263, consumedBoundary.PostTupleBodyBytes);
        Assert.Equal("unclassified object-view tuple", consumedBoundary.TupleLayoutKind);
        Assert.Equal(6, consumedBoundary.FirstNonZeroOffset);
        Assert.Equal("ff 00", consumedBoundary.FirstNonZeroFieldHex);
        Assert.Equal(string.Empty, consumedBoundary.BoundaryHeaderHex);
        Assert.Equal("terminal tail end", consumedBoundary.BoundaryHeaderClassification);

        PacketDumpFileSummary dump = new(
            "mode06-long-terminal-tuple-body-with-embedded-movement.txt",
            1,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            Array.Empty<Protocol04PacketSequenceSample>(),
            new[] { samples[0] },
            Array.Empty<Protocol04InteractionPayloadSample>(),
            Array.Empty<PlayerAttributePayloadSample>(),
            Array.Empty<ManageBonusPayloadSample>());

        Protocol03NestedMovementMode06LongTupleBodyPreAnchorEmbeddedMovementTrailerSummary summary =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06LongTupleBodyPreAnchorEmbeddedMovementTrailerSummaries(new[] { dump }));
        Assert.Equal(95, summary.MarkerOffset);
        Assert.Equal("decoded embedded mode 06 movement window", summary.MovementDisposition);
        Assert.Equal("shared ff/10 post-movement continuation lead", summary.TrailerDisposition);
        Assert.Equal("ff 00 00 00 10 00 00 00", summary.PostMovementLeadHex);
        Assert.Equal(1, summary.InnerSelectors["0e"]);
        Assert.Equal(1, summary.PreAnchorPostMovementOffsets["112"]);
        Assert.Equal(1, summary.BytesRemainingAfterMovement["55"]);
        Assert.Equal(1, summary.PostMovementNextLeads["00 00 01 ff 00 00 00 00"]);
        Assert.Equal(1, summary.PostMovementTailLeads["00 e9 07 00 00 00 00 34"]);
        Assert.Equal(1, summary.PreTailFieldOffsets["38"]);
        Assert.Equal(1, summary.PreTailFieldBytes["02"]);
        Assert.Equal(1, summary.PreAnchorPreLeads["ff cd cc 4c be 00 00 00"]);
        string nonZeroOffsets = Assert.Single(summary.PostMovementNonZeroByteOffsets.Keys);
        Assert.Contains("0:ff", nonZeroOffsets);
        Assert.Contains("4:10", nonZeroOffsets);
        Assert.Contains("10:01", nonZeroOffsets);
        Assert.Contains("11:ff", nonZeroOffsets);
        Assert.Contains("38:02", nonZeroOffsets);
        Assert.Contains("48:e9", nonZeroOffsets);
        Assert.Contains("54:34", nonZeroOffsets);

        Protocol03NestedMovementMode06LongTupleBodyPreAnchorEmbeddedMovementTrailerShapeSummary shape =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06LongTupleBodyPreAnchorEmbeddedMovementTrailerShapeSummaries(new[] { dump }));
        Assert.Equal("singleton decoded shared trailer shape", shape.ShapeDisposition);
        Assert.Equal("shared ff/10 post-movement continuation lead", shape.TrailerDisposition);
        Assert.Equal("ff 00 00 00 10 00 00 00", shape.PostMovementLeadHex);
        Assert.Equal("00 00 01 ff 00 00 00 00", shape.PostMovementNextLeadHex);
        Assert.Equal(55, shape.BytesRemainingAfterMovement);
        Assert.Equal(38, shape.PreTailFieldOffset);
        Assert.Equal("02", shape.PreTailFieldHex);
        Assert.Equal("ff cd cc 4c be 00 00 00", shape.PreAnchorPreLeadHex);
        Assert.Equal("00 e9 07 00 00 00 00 34", shape.PreAnchorTrailingLeadHex);
        Assert.Equal("trailing terminal-96 state suffix", shape.TailAnchorKind);
        Assert.Equal(1, shape.InnerSelectors["0e"]);
        Assert.Equal(1, shape.PreAnchorPostMovementOffsets["112"]);

        Protocol03NestedMovementMode06PostContinuationTerminalTupleBodySummary terminalBody =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06PostContinuationTerminalTupleBodySummaries(new[] { dump }));
        Assert.Equal(263, terminalBody.TerminalPostTupleBodyBytes);
        Assert.Equal(1, terminalBody.PreTailFieldOffsets["150"]);
        Assert.Equal(1, terminalBody.PreTailFieldBytes["02"]);
        Assert.Equal(1, terminalBody.PreAnchorPreLeadBytes["ff cd cc 4c be 00 00 00"]);
        Assert.Equal(1, terminalBody.PreAnchorTrailingLeads["00 e9 07 00 00 00 00 34"]);

        Protocol03NestedMovementMode06TupleBodyParserActionSummary parserAction =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06TupleBodyParserActionSummaries(
                Array.Empty<Protocol03NestedMovementMode06PostContinuationTupleBodySummary>(),
                new[] { terminalBody }));
        Assert.Equal("test terminal mode 06 long embedded marker-34 trailer body", parserAction.ParserAction);
        Assert.Equal(1, parserAction.BodyByteLengths["263"]);
        Assert.Equal(1, parserAction.MarkerBytes["34"]);
    }

    [Fact]
    public void BuildProtocol03NestedMovementMode06UnsupportedEmbeddedControlSelectorSummaries_GroupsUnsupportedControlsBySelector()
    {
        var shapes = new[]
        {
            new Protocol03NestedMovementMode06LongTupleBodyPreAnchorEmbeddedMovementTrailerShapeSummary(
                95,
                "unsupported selector trailer control",
                "unsupported embedded mode 06 movement selector",
                "non-shared post-movement lead",
                "02 c0 00 27 31 00 6e f3",
                "32 c7 00 80 93 43 77 d6",
                73,
                56,
                "03",
                "ff 00 00 00 00 00 00 00",
                "00 07 03 00 00 00 00 1d",
                "trailing guarded 57-byte object-view suffix",
                1,
                new Dictionary<string, int> { ["28"] = 1 },
                new Dictionary<string, int> { ["98"] = 1 },
                new Dictionary<string, int> { ["candidate mode 06 stacked pre-anchor lead before guarded object-view body"] = 1 },
                new Dictionary<string, int> { ["171"] = 1 },
                new Dictionary<string, int> { ["228"] = 1 },
                new Dictionary<string, int> { ["0:02 1:c0 3:27"] = 1 },
                "sample-a.log",
                10),
            new Protocol03NestedMovementMode06LongTupleBodyPreAnchorEmbeddedMovementTrailerShapeSummary(
                95,
                "unsupported selector trailer control",
                "unsupported embedded mode 06 movement selector",
                "non-shared post-movement lead",
                "00 40 00 00 00 10 90 82",
                "98 c3 f8 df ec 44 ab e6",
                73,
                56,
                "03",
                "ff 00 00 00 00 00 00 00",
                "00 cf 03 00 00 00 00 25",
                "trailing guarded 57-byte object-view suffix",
                1,
                new Dictionary<string, int> { ["28"] = 1 },
                new Dictionary<string, int> { ["98"] = 1 },
                new Dictionary<string, int> { ["candidate mode 06 stacked pre-anchor lead before guarded object-view body"] = 1 },
                new Dictionary<string, int> { ["171"] = 1 },
                new Dictionary<string, int> { ["228"] = 1 },
                new Dictionary<string, int> { ["0:40 5:10 6:90"] = 1 },
                "sample-b.log",
                20),
            new Protocol03NestedMovementMode06LongTupleBodyPreAnchorEmbeddedMovementTrailerShapeSummary(
                95,
                "unsupported selector trailer control",
                "unsupported embedded mode 06 movement selector",
                "non-shared post-movement lead",
                "02 c0 00 27 31 00 05 e2",
                "6e f3 32 c7 00 80 93 43",
                75,
                58,
                "03",
                "ff 00 00 00 00 00 00 00",
                "00 07 03 00 00 00 00 1d",
                "trailing guarded 57-byte object-view suffix",
                1,
                new Dictionary<string, int> { ["2e"] = 1 },
                new Dictionary<string, int> { ["98"] = 1 },
                new Dictionary<string, int> { ["candidate mode 06 stacked pre-anchor lead before guarded object-view body"] = 1 },
                new Dictionary<string, int> { ["173"] = 1 },
                new Dictionary<string, int> { ["230"] = 1 },
                new Dictionary<string, int> { ["0:02 1:c0 3:27"] = 1 },
                "sample-c.log",
                30)
        };

        Protocol03NestedMovementMode06UnsupportedEmbeddedControlSelectorSummary[] summaries =
            PacketResearcher.BuildProtocol03NestedMovementMode06UnsupportedEmbeddedControlSelectorSummaries(shapes).ToArray();

        Protocol03NestedMovementMode06UnsupportedEmbeddedControlSelectorSummary selector28 = summaries[0];
        Assert.Equal("28", selector28.InnerSelectorHex);
        Assert.Equal(2, selector28.WindowCount);
        Assert.Equal(2, selector28.ShapeDispositions["unsupported selector trailer control"]);
        Assert.Equal(2, selector28.TrailerDispositions["non-shared post-movement lead"]);
        Assert.Equal(1, selector28.PostMovementLeads["02 c0 00 27 31 00 6e f3"]);
        Assert.Equal(1, selector28.PostMovementLeads["00 40 00 00 00 10 90 82"]);
        Assert.Equal(2, selector28.PreTailFields["03"]);
        Assert.Equal(1, selector28.PreAnchorTrailingLeads["00 07 03 00 00 00 00 1d"]);
        Assert.Equal(1, selector28.PreAnchorTrailingLeads["00 cf 03 00 00 00 00 25"]);
        Assert.Equal(2, selector28.ParserTargets["candidate mode 06 stacked pre-anchor lead before guarded object-view body"]);
        Assert.Equal("sample-a.log", selector28.SampleFile);
        Assert.Equal(10, selector28.SampleLine);

        Protocol03NestedMovementMode06UnsupportedEmbeddedControlSelectorSummary selector2e =
            Assert.Single(summaries, summary => summary.InnerSelectorHex.Equals("2e", StringComparison.Ordinal));
        Assert.Equal(1, selector2e.WindowCount);
        Assert.Equal(1, selector2e.BodyBytes["230"]);
        Assert.Equal(1, selector2e.BytesRemainingAfterMovement["75"]);
    }

    [Theory]
    [InlineData(
        "03",
        "0e",
        "03",
        "ff cd cc 4c be 00 00 00",
        "00 46 05 00 00 00 00 34",
        "00 46 05 00 00 00 00 34",
        "",
        "mode 06 terminal long embedded movement marker-34-46 trailer body",
        "test terminal mode 06 long embedded marker-34-46 trailer body",
        "unclassified object-view tuple",
        335,
        318,
        263,
        335,
        "terminal tail end")]
    [InlineData(
        "03",
        "0e",
        "03",
        "ff 00 00 00 00 00 00 00",
        "00 96 00 00 00 00 00 04",
        "00 96 00 00 00 00 00 04",
        "",
        "mode 06 terminal long embedded movement marker-04 trailer body",
        "test terminal mode 06 long embedded marker-04 trailer body",
        "zero-update marker tuple",
        335,
        318,
        263,
        335,
        "terminal tail end")]
    [InlineData(
        "03",
        "0c",
        "03",
        "ff 00 00 00 00 00 00 00",
        "00 b6 03 00 00 00 00 24",
        "00 b6 03 00 00 00 00 24",
        "",
        "mode 06 terminal long embedded movement marker-24 trailer body",
        "test terminal mode 06 long embedded marker-24 trailer body",
        "unclassified object-view tuple",
        334,
        317,
        262,
        334,
        "terminal tail end")]
    [InlineData(
        "03",
        "0e",
        "03",
        "ff 00 00 00 00 00 00 00",
        "00 b6 03 00 00 00 00 24",
        "00 b6 03 00 00 00 00 24",
        "",
        "mode 06 terminal long embedded movement marker-24 trailer body",
        "test terminal mode 06 long embedded marker-24 trailer body",
        "unclassified object-view tuple",
        335,
        318,
        263,
        335,
        "terminal tail end")]
    [InlineData(
        "01",
        "0c",
        "01",
        "ff 00 00 00 00 00 00 00",
        "00 b0 04 00 00 00 00 16",
        "00 b0 04 00 00 00 00 16",
        "2e 00 02 0e 01 d7 ec 85",
        "mode 06 guarded 94-byte long embedded movement marker-16 trailer body",
        "test guarded mode 06 long embedded marker-16 trailer body",
        "unclassified object-view tuple",
        332,
        315,
        260,
        340,
        "object state update")]
    [InlineData(
        "03",
        "0e",
        "03",
        "ff 00 00 00 00 00 00 00",
        "00 b6 03 00 00 00 00 24",
        "00 b6 03 00 00 00 00 24",
        "28 ff 01 00 00 00 00 00",
        "mode 06 guarded 57-byte long embedded movement marker-24 trailer body",
        "test guarded mode 06 long embedded marker-24 trailer body",
        "unclassified object-view tuple",
        296,
        279,
        224,
        304,
        "single state marker update")]
    [InlineData(
        "03",
        "0e",
        "03",
        "ff 00 00 00 00 00 00 00",
        "00 4c 04 00 00 00 00 2a",
        "00 4c 04 00 00 00 00 2a",
        "",
        "mode 06 terminal long embedded movement marker-2a trailer body",
        "test terminal mode 06 long embedded marker-2a trailer body",
        "unclassified object-view tuple",
        335,
        318,
        263,
        335,
        "terminal tail end")]
    [InlineData(
        "03",
        "0e",
        "03",
        "ff 00 00 00 00 00 00 00",
        "00 cf 03 00 00 00 00 25",
        "00 cf 03 00 00 00 00 25",
        "28 ff 01 00 00 00 00 00",
        "mode 06 guarded 57-byte long embedded movement marker-25 trailer body",
        "test guarded mode 06 long embedded marker-25 trailer body",
        "unclassified object-view tuple",
        296,
        279,
        224,
        304,
        "single state marker update")]
    public void DetectProtocol03ObjectViews_BoundsMode06LongEmbeddedMovementTrailerBodies(
        string outerPrefixFieldHex,
        string innerSelectorHex,
        string preTailFieldHex,
        string preAnchorPreLeadHex,
        string preAnchorTrailingLeadHex,
        string outerPostPrefixLeadHex,
        string boundaryHeaderHex,
        string expectedClassification,
        string expectedParserAction,
        string expectedTupleLayoutKind,
        int expectedPayloadBytes,
        int expectedTailBoundaryOffset,
        int expectedPostTupleBodyBytes,
        int expectedEvidencePayloadBytes,
        string expectedBoundaryClassification)
    {
        byte innerSelector = byte.Parse(innerSelectorHex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        byte[] preAnchor = BuildProtocol03Mode06LongPreAnchor(
            innerSelector,
            preTailFieldHex,
            preAnchorPreLeadHex,
            preAnchorTrailingLeadHex);
        byte[] anchorBody = string.IsNullOrEmpty(boundaryHeaderHex)
            ? BuildProtocol03Mode06Terminal96Body("07 08 00 00 00 00 00 00 22 00 00 00 00 00 00 00")
            : BuildProtocol03Mode06Guarded57Body("ff 00 00 00 00 00 00 00 00 d4 1e 00 00 4c 0a 00");
        if (!string.IsNullOrEmpty(boundaryHeaderHex) &&
            expectedPostTupleBodyBytes == 260)
        {
            anchorBody = BuildProtocol03Mode06Guarded94Body("00 1f 07 08 00 00 00 00 00 00 22 00 00 00 00 00");
        }

        byte[] body = preAnchor.Concat(anchorBody).ToArray();
        byte[] bytes = BuildProtocol03Mode06LongEmbeddedTrailerObjectView(
            body,
            outerPrefixFieldHex,
            outerPostPrefixLeadHex,
            boundaryHeaderHex);

        Protocol03ObjectViewSample[] samples = PacketResearcher.DetectProtocol03ObjectViews(bytes, 531, "server").ToArray();

        if (string.IsNullOrEmpty(boundaryHeaderHex))
        {
            Assert.Single(samples);
        }
        else
        {
            Assert.Equal(2, samples.Length);
            if (expectedBoundaryClassification.Equals("single state marker update", StringComparison.Ordinal))
            {
                Assert.True(samples[1].Complete);
            }

            Assert.Equal(expectedBoundaryClassification, samples[1].Classification);
        }

        Assert.True(samples[0].Complete);
        Assert.Equal(0, samples[0].UnparsedBytes);
        Assert.Contains(
            samples[0].Segments,
            segment => segment.Classification == expectedClassification &&
                segment.PayloadBytes == expectedPayloadBytes &&
                segment.IsKnownLength);

        Protocol03NestedMovementConsumedBodyBoundary boundary = Assert.Single(samples[0].NestedMovementConsumedBodyBoundaries);
        Assert.Equal(expectedParserAction, boundary.ParserAction);
        Assert.Equal("03", boundary.Selector);
        Assert.Equal(17, boundary.Offset);
        Assert.Equal(expectedPayloadBytes, boundary.PayloadBytes);
        Assert.Equal(expectedEvidencePayloadBytes, boundary.EvidencePayloadBytes);
        Assert.Equal(17, boundary.TailStartOffset);
        Assert.Equal(expectedTailBoundaryOffset, boundary.TailBoundaryOffset);
        Assert.Equal(expectedPostTupleBodyBytes, boundary.PostTupleBodyBytes);
        Assert.Equal(expectedTupleLayoutKind, boundary.TupleLayoutKind);
        Assert.Equal(6, boundary.FirstNonZeroOffset);
        Assert.Equal("ff 00", boundary.FirstNonZeroFieldHex);
        Assert.Equal(boundaryHeaderHex, boundary.BoundaryHeaderHex);
        Assert.Equal(expectedBoundaryClassification, boundary.BoundaryHeaderClassification);

        if (!string.IsNullOrEmpty(boundaryHeaderHex))
        {
            PacketDumpFileSummary dump = new(
                "mode06-guarded-long-embedded-trailer-body.txt",
                1,
                new Dictionary<string, int>(),
                new Dictionary<string, int>(),
                new Dictionary<string, int>(),
                new Dictionary<string, IReadOnlyList<int>>(),
                Array.Empty<Protocol04PacketSequenceSample>(),
                new[] { samples[0] },
                Array.Empty<Protocol04InteractionPayloadSample>(),
                Array.Empty<PlayerAttributePayloadSample>(),
                Array.Empty<ManageBonusPayloadSample>());
            Protocol03NestedMovementMode06PostContinuationTupleBodySummary tupleBody =
                Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06PostContinuationTupleBodySummaries(new[] { dump }));
            Assert.Equal(expectedTupleLayoutKind, tupleBody.TupleLayoutKind);
            Assert.Equal(expectedPostTupleBodyBytes, tupleBody.PostTupleBodyBytes);

            Protocol03NestedMovementMode06TupleBodyParserActionSummary parserAction =
                Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06TupleBodyParserActionSummaries(new[] { tupleBody }));
            Assert.Equal(expectedParserAction, parserAction.ParserAction);
            Assert.Equal(1, parserAction.BodyByteLengths[expectedPostTupleBodyBytes.ToString(CultureInfo.InvariantCulture)]);
        }

        if (string.IsNullOrEmpty(boundaryHeaderHex))
        {
            PacketDumpFileSummary dump = new(
                "mode06-long-terminal-tuple-body-with-embedded-movement-marker04.txt",
                1,
                new Dictionary<string, int>(),
                new Dictionary<string, int>(),
                new Dictionary<string, int>(),
                new Dictionary<string, IReadOnlyList<int>>(),
                Array.Empty<Protocol04PacketSequenceSample>(),
                new[] { samples[0] },
                Array.Empty<Protocol04InteractionPayloadSample>(),
                Array.Empty<PlayerAttributePayloadSample>(),
                Array.Empty<ManageBonusPayloadSample>());
            Protocol03NestedMovementMode06PostContinuationTerminalTupleBodySummary terminalBody =
                Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06PostContinuationTerminalTupleBodySummaries(new[] { dump }));
            Assert.Equal(expectedPostTupleBodyBytes, terminalBody.TerminalPostTupleBodyBytes);
            Assert.Equal(1, terminalBody.PreTailFieldBytes[preTailFieldHex]);
            Assert.Equal(1, terminalBody.PreAnchorPreLeadBytes[preAnchorPreLeadHex]);
            Assert.Equal(1, terminalBody.PreAnchorTrailingLeads[preAnchorTrailingLeadHex]);

            Protocol03NestedMovementMode06TupleBodyParserActionSummary parserAction =
                Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06TupleBodyParserActionSummaries(
                    Array.Empty<Protocol03NestedMovementMode06PostContinuationTupleBodySummary>(),
                    new[] { terminalBody }));
            Assert.Equal(expectedParserAction, parserAction.ParserAction);
            Assert.Equal(1, parserAction.BodyByteLengths[expectedPostTupleBodyBytes.ToString(CultureInfo.InvariantCulture)]);
        }
    }

    [Fact]
    public void DetectProtocol03ObjectViews_BoundsDominantMode06ZeroUpdateTupleBody()
    {
        string body70 = string.Join(" ", Enumerable.Repeat("00", 6)
            .Concat(new[] { "ff", "00" })
            .Concat(Enumerable.Repeat("00", 62)));
        byte[] bytes = PacketResearcher.ParseHexBytes(
            "02 03 01 00 02 0c 00 00 00 00 00 00 00 00 00 00 00 00 00 03 " +
            "00 06 0e 01 18 58 3a f6 c6 00 00 be 42 44 35 af 46 " +
            "ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 " +
            "00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 " +
            "03 ff 00 00 00 00 00 00 00 00 4b 00 00 00 00 00 01 " +
            body70 +
            " 01 00 01 00 00")
            .ToArray();

        Protocol03ObjectViewSample[] samples = PacketResearcher.DetectProtocol03ObjectViews(bytes, 531, "server").ToArray();

        Assert.Equal(2, samples.Length);
        Assert.True(samples[0].Complete);
        Assert.Equal(2, samples[0].ParsedUpdateCount);
        Assert.Equal(0, samples[0].UnparsedBytes);
        Assert.Collection(
            samples[0].Segments,
            segment =>
            {
                Assert.Equal("0c", segment.Selector);
                Assert.Equal("position xyz + flag", segment.Classification);
                Assert.Equal(13, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            },
            segment =>
            {
                Assert.Equal("03", segment.Selector);
                Assert.Equal("mode 06 zero-update tuple body", segment.Classification);
                Assert.Equal(142, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            });
        Assert.True(samples[1].Complete);
        Assert.Equal(1, samples[1].ViewId);
        Assert.Equal("single state marker update", samples[1].Classification);

        Protocol03NestedMovementConsumedBodyBoundary boundary = Assert.Single(samples[0].NestedMovementConsumedBodyBoundaries);
        Assert.Equal("test consuming mode 06 zero-update tuple body", boundary.ParserAction);
        Assert.Equal("03", boundary.Selector);
        Assert.Equal(17, boundary.Offset);
        Assert.Equal(142, boundary.PayloadBytes);
        Assert.Equal(147, boundary.EvidencePayloadBytes);
        Assert.Equal(17, boundary.TailStartOffset);
        Assert.Equal(125, boundary.TailBoundaryOffset);
        Assert.Equal(70, boundary.PostTupleBodyBytes);
        Assert.Equal("zero-update marker tuple", boundary.TupleLayoutKind);
        Assert.Equal(6, boundary.FirstNonZeroOffset);
        Assert.Equal("ff 00", boundary.FirstNonZeroFieldHex);
        Assert.Equal("01 00 01 00 00", boundary.BoundaryHeaderHex);
        Assert.Equal("single state marker update", boundary.BoundaryHeaderClassification);

        PacketDumpFileSummary dump = new(
            "mode06-bounded.txt",
            1,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            Array.Empty<Protocol04PacketSequenceSample>(),
            new[] { samples[0] },
            Array.Empty<Protocol04InteractionPayloadSample>(),
            Array.Empty<PlayerAttributePayloadSample>(),
            Array.Empty<ManageBonusPayloadSample>());

        Protocol03NestedMovementMode06PostContinuationTupleBodySummary tupleBody =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06PostContinuationTupleBodySummaries(new[] { dump }));
        Assert.Equal("zero-update marker tuple", tupleBody.TupleLayoutKind);
        Assert.Equal(70, tupleBody.PostTupleBodyBytes);
        Assert.Equal(6, tupleBody.FirstNonZeroOffset);
        Assert.Equal("ff 00", tupleBody.FirstNonZeroFieldHex);
        Assert.Equal(1, tupleBody.HeaderClassifications["single state marker update"]);

        Protocol03NestedMovementMode06TupleBodyParserActionSummary parserAction =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06TupleBodyParserActionSummaries(new[] { tupleBody }));
        Assert.Equal("test consuming mode 06 zero-update tuple body", parserAction.ParserAction);
        Assert.Equal(1, parserAction.BodyByteLengths["70"]);
    }

    [Theory]
    [InlineData(
        "00 1f 07 08 00 00 00 00 00 00 22 00 00 00 00 00",
        "00 96 00 00 00 00 00 04",
        "28 ff 01 00 00 00 00 00",
        "single state marker update",
        true)]
    [InlineData(
        "00 1f 42 21 00 00 00 00 00 00 22 00 00 00 00 00",
        "00 96 00 00 00 00 00 04",
        "15 00 02 0c 5f 22 ac 84",
        "object state update",
        false)]
    [InlineData(
        "00 1f 42 21 00 00 00 00 00 00 22 00 00 00 00 00",
        "00 c8 00 00 00 00 00 06",
        "05 00 02 0e 00 1c 17 e0",
        "object state update",
        false)]
    public void DetectProtocol03ObjectViews_BoundsGuardedMode06ZeroUpdate94ByteTupleBody(
        string bodySuffixHex,
        string postPrefixLeadHex,
        string boundaryHeaderHex,
        string boundaryClassification,
        bool boundaryComplete)
    {
        byte[] body = Enumerable.Repeat((byte)0x00, 6)
            .Concat(new byte[] { 0xff, 0x00 })
            .Concat(Enumerable.Repeat((byte)0x00, 70))
            .Concat(PacketResearcher.ParseHexBytes(bodySuffixHex))
            .ToArray();
        var bytes = new List<byte>
        {
            0x02, 0x03,
            0x01, 0x00, 0x02, 0x0c
        };
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 13));
        bytes.Add(0x03);
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 06 0e 01 18 58 3a f6 c6 00 00 be 42 44 35 af 46"));
        bytes.AddRange(PacketResearcher.ParseHexBytes("ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00"));
        bytes.AddRange(PacketResearcher.ParseHexBytes("03 ff"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 29));
        bytes.AddRange(PacketResearcher.ParseHexBytes(postPrefixLeadHex));
        bytes.AddRange(body);
        bytes.AddRange(PacketResearcher.ParseHexBytes(boundaryHeaderHex));

        Protocol03ObjectViewSample[] samples = PacketResearcher.DetectProtocol03ObjectViews(bytes, 537, "server").ToArray();

        const int expectedPayloadBytes = 17 + 16 + 31 + 8 + 94;
        Assert.Equal(2, samples.Length);
        Assert.True(samples[0].Complete);
        Assert.Equal(2, samples[0].ParsedUpdateCount);
        Assert.Equal(0, samples[0].UnparsedBytes);
        Assert.Collection(
            samples[0].Segments,
            segment =>
            {
                Assert.Equal("0c", segment.Selector);
                Assert.Equal("position xyz + flag", segment.Classification);
                Assert.Equal(13, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            },
            segment =>
            {
                Assert.Equal("03", segment.Selector);
                Assert.Equal("mode 06 guarded 94-byte zero-update tuple body", segment.Classification);
                Assert.Equal(expectedPayloadBytes, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            });
        Assert.Equal(boundaryComplete, samples[1].Complete);
        Assert.Equal(boundaryClassification, samples[1].Classification);

        Protocol03NestedMovementConsumedBodyBoundary boundary = Assert.Single(samples[0].NestedMovementConsumedBodyBoundaries);
        Assert.Equal("test guarded mode 06 94-byte zero-update tuple body", boundary.ParserAction);
        Assert.Equal("03", boundary.Selector);
        Assert.Equal(17, boundary.Offset);
        Assert.Equal(expectedPayloadBytes, boundary.PayloadBytes);
        Assert.Equal(expectedPayloadBytes + 8, boundary.EvidencePayloadBytes);
        Assert.Equal(17, boundary.TailStartOffset);
        Assert.Equal(149, boundary.TailBoundaryOffset);
        Assert.Equal(94, boundary.PostTupleBodyBytes);
        Assert.Equal("zero-update marker tuple", boundary.TupleLayoutKind);
        Assert.Equal(6, boundary.FirstNonZeroOffset);
        Assert.Equal("ff 00", boundary.FirstNonZeroFieldHex);
        Assert.Equal(boundaryHeaderHex, boundary.BoundaryHeaderHex);
        Assert.Equal(boundaryClassification, boundary.BoundaryHeaderClassification);

        PacketDumpFileSummary dump = new(
            "mode06-guarded-zero-update-94.txt",
            1,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            Array.Empty<Protocol04PacketSequenceSample>(),
            new[] { samples[0] },
            Array.Empty<Protocol04InteractionPayloadSample>(),
            Array.Empty<PlayerAttributePayloadSample>(),
            Array.Empty<ManageBonusPayloadSample>());

        Protocol03NestedMovementMode06PostContinuationTupleBodySummary tupleBody =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06PostContinuationTupleBodySummaries(new[] { dump }));
        Assert.Equal("ff marker plus zero-padded ff continuation lead", tupleBody.TailPrefixKind);
        Assert.Equal("03 ff", tupleBody.PrefixFirstNonZeroFieldHex);
        Assert.Equal("zero-update marker tuple", tupleBody.TupleLayoutKind);
        Assert.Equal("non-object-view post-prefix lead", tupleBody.PostPrefixDisposition);
        Assert.Equal(94, tupleBody.PostTupleBodyBytes);
        Assert.Equal(bodySuffixHex, tupleBody.BodySuffixHex);
        Assert.Equal(6, tupleBody.FirstNonZeroOffset);
        Assert.Equal("ff 00", tupleBody.FirstNonZeroFieldHex);
        Assert.Equal(1, tupleBody.HeaderClassifications[boundaryClassification]);
        Assert.Equal(1, tupleBody.HeaderPrefixes[boundaryHeaderHex]);
        Assert.Equal(1, tupleBody.PostPrefixLeads[postPrefixLeadHex]);

        Protocol03NestedMovementMode06TupleBodyParserActionSummary parserAction =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06TupleBodyParserActionSummaries(new[] { tupleBody }));
        Assert.Equal("test guarded mode 06 94-byte zero-update tuple body", parserAction.ParserAction);
        Assert.Equal(1, parserAction.BodyByteLengths["94"]);
    }

    [Theory]
    [InlineData("17 00 02 0c c5 29 44 85")]
    [InlineData("1d 00 02 0c 19 bc 15 93")]
    public void DetectProtocol03ObjectViews_BoundsGuardedMode06Marker04_94ByteZeroUpdateTupleBody(
        string boundaryHeaderHex)
    {
        const string bodySuffixHex = "00 1f f9 07 00 00 00 00 00 00 22 00 00 00 00 00";
        const string postPrefixLeadHex = "00 96 00 00 00 00 00 04";
        byte[] body = Enumerable.Repeat((byte)0x00, 6)
            .Concat(new byte[] { 0xff, 0x00 })
            .Concat(Enumerable.Repeat((byte)0x00, 70))
            .Concat(PacketResearcher.ParseHexBytes(bodySuffixHex))
            .ToArray();
        var bytes = new List<byte>
        {
            0x02, 0x03,
            0x01, 0x00, 0x02, 0x0c
        };
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 13));
        bytes.Add(0x03);
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 06 0e 01 18 58 3a f6 c6 00 00 be 42 44 35 af 46"));
        bytes.AddRange(PacketResearcher.ParseHexBytes("ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00"));
        bytes.AddRange(PacketResearcher.ParseHexBytes("03 ff"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 29));
        bytes.AddRange(PacketResearcher.ParseHexBytes(postPrefixLeadHex));
        bytes.AddRange(body);
        bytes.AddRange(PacketResearcher.ParseHexBytes(boundaryHeaderHex));

        Protocol03ObjectViewSample[] samples = PacketResearcher.DetectProtocol03ObjectViews(bytes, 537, "server").ToArray();

        const int expectedPayloadBytes = 17 + 16 + 31 + 8 + 94;
        Assert.Equal(2, samples.Length);
        Assert.True(samples[0].Complete);
        Assert.Equal(2, samples[0].ParsedUpdateCount);
        Assert.Equal(0, samples[0].UnparsedBytes);
        Assert.Collection(
            samples[0].Segments,
            segment =>
            {
                Assert.Equal("0c", segment.Selector);
                Assert.Equal("position xyz + flag", segment.Classification);
                Assert.Equal(13, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            },
            segment =>
            {
                Assert.Equal("03", segment.Selector);
                Assert.Equal("mode 06 guarded 94-byte marker-04 zero-update tuple body", segment.Classification);
                Assert.Equal(expectedPayloadBytes, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            });
        Assert.False(samples[1].Complete);
        Assert.Equal("object state update", samples[1].Classification);

        Protocol03NestedMovementConsumedBodyBoundary boundary = Assert.Single(samples[0].NestedMovementConsumedBodyBoundaries);
        Assert.Equal("test guarded mode 06 94-byte marker-04 zero-update tuple body", boundary.ParserAction);
        Assert.Equal("03", boundary.Selector);
        Assert.Equal(17, boundary.Offset);
        Assert.Equal(expectedPayloadBytes, boundary.PayloadBytes);
        Assert.Equal(expectedPayloadBytes + 8, boundary.EvidencePayloadBytes);
        Assert.Equal(17, boundary.TailStartOffset);
        Assert.Equal(149, boundary.TailBoundaryOffset);
        Assert.Equal(94, boundary.PostTupleBodyBytes);
        Assert.Equal("zero-update marker tuple", boundary.TupleLayoutKind);
        Assert.Equal(6, boundary.FirstNonZeroOffset);
        Assert.Equal("ff 00", boundary.FirstNonZeroFieldHex);
        Assert.Equal(boundaryHeaderHex, boundary.BoundaryHeaderHex);
        Assert.Equal("object state update", boundary.BoundaryHeaderClassification);

        PacketDumpFileSummary dump = new(
            "mode06-guarded-marker-04-zero-update-94.txt",
            1,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            Array.Empty<Protocol04PacketSequenceSample>(),
            new[] { samples[0] },
            Array.Empty<Protocol04InteractionPayloadSample>(),
            Array.Empty<PlayerAttributePayloadSample>(),
            Array.Empty<ManageBonusPayloadSample>());

        Protocol03NestedMovementMode06PostContinuationTupleBodySummary tupleBody =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06PostContinuationTupleBodySummaries(new[] { dump }));
        Assert.Equal("ff marker plus zero-padded ff continuation lead", tupleBody.TailPrefixKind);
        Assert.Equal("03 ff", tupleBody.PrefixFirstNonZeroFieldHex);
        Assert.Equal("zero-update marker tuple", tupleBody.TupleLayoutKind);
        Assert.Equal("non-object-view post-prefix lead", tupleBody.PostPrefixDisposition);
        Assert.Equal(94, tupleBody.PostTupleBodyBytes);
        Assert.Equal(bodySuffixHex, tupleBody.BodySuffixHex);
        Assert.Equal(6, tupleBody.FirstNonZeroOffset);
        Assert.Equal("ff 00", tupleBody.FirstNonZeroFieldHex);
        Assert.Equal(1, tupleBody.HeaderClassifications["object state update"]);
        Assert.Equal(1, tupleBody.HeaderPrefixes[boundaryHeaderHex]);
        Assert.Equal(1, tupleBody.PostPrefixLeads[postPrefixLeadHex]);

        Protocol03NestedMovementMode06TupleBodyParserActionSummary parserAction =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06TupleBodyParserActionSummaries(new[] { tupleBody }));
        Assert.Equal("test guarded mode 06 94-byte marker-04 zero-update tuple body", parserAction.ParserAction);
        Assert.Equal("94-byte marker-04 zero-update body before object-state boundary", parserAction.ActionReason);
        Assert.Equal(1, parserAction.BodyByteLengths["94"]);
    }

    [Fact]
    public void DetectProtocol03ObjectViews_BoundsGuardedMode06Marker04_95ByteZeroUpdateTupleBody()
    {
        const string bodySuffixHex = "1f 42 21 00 00 00 00 00 00 22 00 00 00 00 00 25";
        const string postPrefixLeadHex = "00 96 00 00 00 00 00 04";
        const string boundaryHeaderHex = "00 02 02 00 1d 00 02 0c";
        byte[] body = Enumerable.Repeat((byte)0x00, 6)
            .Concat(new byte[] { 0xff, 0x00 })
            .Concat(Enumerable.Repeat((byte)0x00, 71))
            .Concat(PacketResearcher.ParseHexBytes(bodySuffixHex))
            .ToArray();
        var bytes = new List<byte>
        {
            0x02, 0x03,
            0x01, 0x00, 0x02, 0x0c
        };
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 13));
        bytes.Add(0x03);
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 06 0e 01 18 58 3a f6 c6 00 00 be 42 44 35 af 46"));
        bytes.AddRange(PacketResearcher.ParseHexBytes("ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00"));
        bytes.AddRange(PacketResearcher.ParseHexBytes("03 ff"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 29));
        bytes.AddRange(PacketResearcher.ParseHexBytes(postPrefixLeadHex));
        bytes.AddRange(body);
        bytes.AddRange(PacketResearcher.ParseHexBytes(boundaryHeaderHex));

        Protocol03ObjectViewSample[] samples = PacketResearcher.DetectProtocol03ObjectViews(bytes, 537, "server").ToArray();

        const int expectedPayloadBytes = 17 + 16 + 31 + 8 + 95;
        Assert.Equal(2, samples.Length);
        Assert.True(samples[0].Complete);
        Assert.Equal(2, samples[0].ParsedUpdateCount);
        Assert.Equal(0, samples[0].UnparsedBytes);
        Assert.Collection(
            samples[0].Segments,
            segment =>
            {
                Assert.Equal("0c", segment.Selector);
                Assert.Equal("position xyz + flag", segment.Classification);
                Assert.Equal(13, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            },
            segment =>
            {
                Assert.Equal("03", segment.Selector);
                Assert.Equal("mode 06 guarded 95-byte marker-04 zero-update tuple body", segment.Classification);
                Assert.Equal(expectedPayloadBytes, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            });
        Assert.True(samples[1].Complete);
        Assert.Equal("state marker/object update candidate", samples[1].Classification);

        Protocol03NestedMovementConsumedBodyBoundary boundary = Assert.Single(samples[0].NestedMovementConsumedBodyBoundaries);
        Assert.Equal("test guarded mode 06 95-byte marker-04 zero-update tuple body", boundary.ParserAction);
        Assert.Equal("03", boundary.Selector);
        Assert.Equal(17, boundary.Offset);
        Assert.Equal(expectedPayloadBytes, boundary.PayloadBytes);
        Assert.Equal(expectedPayloadBytes + 8, boundary.EvidencePayloadBytes);
        Assert.Equal(17, boundary.TailStartOffset);
        Assert.Equal(150, boundary.TailBoundaryOffset);
        Assert.Equal(95, boundary.PostTupleBodyBytes);
        Assert.Equal("zero-update marker tuple", boundary.TupleLayoutKind);
        Assert.Equal(6, boundary.FirstNonZeroOffset);
        Assert.Equal("ff 00", boundary.FirstNonZeroFieldHex);
        Assert.Equal(boundaryHeaderHex, boundary.BoundaryHeaderHex);
        Assert.Equal("state marker/object update candidate", boundary.BoundaryHeaderClassification);

        PacketDumpFileSummary dump = new(
            "mode06-guarded-marker-04-zero-update-95.txt",
            1,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            Array.Empty<Protocol04PacketSequenceSample>(),
            new[] { samples[0] },
            Array.Empty<Protocol04InteractionPayloadSample>(),
            Array.Empty<PlayerAttributePayloadSample>(),
            Array.Empty<ManageBonusPayloadSample>());

        Protocol03NestedMovementMode06PostContinuationTupleBodySummary tupleBody =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06PostContinuationTupleBodySummaries(new[] { dump }));
        Assert.Equal("ff marker plus zero-padded ff continuation lead", tupleBody.TailPrefixKind);
        Assert.Equal("03 ff", tupleBody.PrefixFirstNonZeroFieldHex);
        Assert.Equal("zero-update marker tuple", tupleBody.TupleLayoutKind);
        Assert.Equal("non-object-view post-prefix lead", tupleBody.PostPrefixDisposition);
        Assert.Equal(95, tupleBody.PostTupleBodyBytes);
        Assert.Equal(bodySuffixHex, tupleBody.BodySuffixHex);
        Assert.Equal(6, tupleBody.FirstNonZeroOffset);
        Assert.Equal("ff 00", tupleBody.FirstNonZeroFieldHex);
        Assert.Equal(1, tupleBody.HeaderClassifications["state marker/object update candidate"]);
        Assert.Equal(1, tupleBody.HeaderPrefixes[boundaryHeaderHex]);
        Assert.Equal(1, tupleBody.PostPrefixLeads[postPrefixLeadHex]);

        Protocol03NestedMovementMode06TupleBodyParserActionSummary parserAction =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06TupleBodyParserActionSummaries(new[] { tupleBody }));
        Assert.Equal("test guarded mode 06 95-byte marker-04 zero-update tuple body", parserAction.ParserAction);
        Assert.Equal("95-byte marker-04 zero-update body before state/object boundary", parserAction.ActionReason);
        Assert.Equal(1, parserAction.BodyByteLengths["95"]);
    }

    [Theory]
    [InlineData("00 5d 01 00 88 37 08 00")]
    [InlineData("00 5e 01 00 88 37 08 00")]
    public void DetectProtocol03ObjectViews_BoundsGuardedMode06Marker04_237ByteZeroUpdateTupleBody(
        string boundaryHeaderHex)
    {
        const string bodyPrefixHex = "00 00 00 00 00 00 ff 00 00 00 00 00 00 00 00 00";
        const string bodySuffixHex = "00 00 00 00 ff 00 00 00 00 00 00 00 00 00 00 00";
        const string postPrefixLeadHex = "00 96 00 00 00 00 00 04";
        byte[] body = Enumerable.Repeat((byte)0x00, 6)
            .Concat(new byte[] { 0xff, 0x00 })
            .Concat(Enumerable.Repeat((byte)0x00, 213))
            .Concat(PacketResearcher.ParseHexBytes(bodySuffixHex))
            .ToArray();
        var bytes = new List<byte>
        {
            0x02, 0x03,
            0x01, 0x00, 0x02, 0x0c
        };
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 13));
        bytes.Add(0x03);
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 06 0e 01 18 58 3a f6 c6 00 00 be 42 44 35 af 46"));
        bytes.AddRange(PacketResearcher.ParseHexBytes("ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00"));
        bytes.AddRange(PacketResearcher.ParseHexBytes("03 ff"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 29));
        bytes.AddRange(PacketResearcher.ParseHexBytes(postPrefixLeadHex));
        bytes.AddRange(body);
        bytes.AddRange(PacketResearcher.ParseHexBytes(boundaryHeaderHex));

        Protocol03ObjectViewSample[] samples = PacketResearcher.DetectProtocol03ObjectViews(bytes, 537, "server").ToArray();

        const int expectedPayloadBytes = 17 + 16 + 31 + 8 + 237;
        Assert.Equal(2, samples.Length);
        Assert.True(samples[0].Complete);
        Assert.Equal(2, samples[0].ParsedUpdateCount);
        Assert.Equal(0, samples[0].UnparsedBytes);
        Assert.Collection(
            samples[0].Segments,
            segment =>
            {
                Assert.Equal("0c", segment.Selector);
                Assert.Equal("position xyz + flag", segment.Classification);
                Assert.Equal(13, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            },
            segment =>
            {
                Assert.Equal("03", segment.Selector);
                Assert.Equal("mode 06 guarded 237-byte marker-04 zero-update tuple body", segment.Classification);
                Assert.Equal(expectedPayloadBytes, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            });
        Assert.Equal("single state marker update", samples[1].Classification);

        Protocol03NestedMovementConsumedBodyBoundary boundary = Assert.Single(samples[0].NestedMovementConsumedBodyBoundaries);
        Assert.Equal("test guarded mode 06 237-byte marker-04 zero-update tuple body", boundary.ParserAction);
        Assert.Equal("03", boundary.Selector);
        Assert.Equal(17, boundary.Offset);
        Assert.Equal(expectedPayloadBytes, boundary.PayloadBytes);
        Assert.Equal(expectedPayloadBytes + 8, boundary.EvidencePayloadBytes);
        Assert.Equal(17, boundary.TailStartOffset);
        Assert.Equal(292, boundary.TailBoundaryOffset);
        Assert.Equal(237, boundary.PostTupleBodyBytes);
        Assert.Equal("zero-update marker tuple", boundary.TupleLayoutKind);
        Assert.Equal(6, boundary.FirstNonZeroOffset);
        Assert.Equal("ff 00", boundary.FirstNonZeroFieldHex);
        Assert.Equal(boundaryHeaderHex, boundary.BoundaryHeaderHex);
        Assert.Equal("single state marker update", boundary.BoundaryHeaderClassification);

        PacketDumpFileSummary dump = new(
            "mode06-guarded-marker-04-zero-update-237.txt",
            1,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            Array.Empty<Protocol04PacketSequenceSample>(),
            new[] { samples[0] },
            Array.Empty<Protocol04InteractionPayloadSample>(),
            Array.Empty<PlayerAttributePayloadSample>(),
            Array.Empty<ManageBonusPayloadSample>());

        Protocol03NestedMovementMode06PostContinuationTupleBodySummary tupleBody =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06PostContinuationTupleBodySummaries(new[] { dump }));
        Assert.Equal("ff marker plus zero-padded ff continuation lead", tupleBody.TailPrefixKind);
        Assert.Equal("03 ff", tupleBody.PrefixFirstNonZeroFieldHex);
        Assert.Equal("zero-update marker tuple", tupleBody.TupleLayoutKind);
        Assert.Equal("non-object-view post-prefix lead", tupleBody.PostPrefixDisposition);
        Assert.Equal(237, tupleBody.PostTupleBodyBytes);
        Assert.Equal(bodyPrefixHex, tupleBody.BodyPrefixHex);
        Assert.Equal(bodySuffixHex, tupleBody.BodySuffixHex);
        Assert.Equal(6, tupleBody.FirstNonZeroOffset);
        Assert.Equal("ff 00", tupleBody.FirstNonZeroFieldHex);
        Assert.Equal(1, tupleBody.HeaderClassifications["single state marker update"]);
        Assert.Equal(1, tupleBody.HeaderPrefixes[boundaryHeaderHex]);
        Assert.Equal(1, tupleBody.PostPrefixLeads[postPrefixLeadHex]);

        Protocol03NestedMovementMode06TupleBodyParserActionSummary parserAction =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06TupleBodyParserActionSummaries(new[] { tupleBody }));
        Assert.Equal("test guarded mode 06 237-byte marker-04 zero-update tuple body", parserAction.ParserAction);
        Assert.Equal("237-byte marker-04 zero-update body before paired state-marker boundary", parserAction.ActionReason);
        Assert.Equal(1, parserAction.BodyByteLengths["237"]);
    }

    [Fact]
    public void DetectProtocol03ObjectViews_BoundsGuardedMode0678ByteMarkerFfNonObjectTupleBody()
    {
        byte[] bytes = PacketResearcher.ParseHexBytes("""
            02 03 3f 00 02 0e 05 62 6f ba 06 47 00 20 8a c4 10 b4 59 c4 21 00 06 0c 29 28 96 0f 47 00 20 94
            c4 16 38 1f c5 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
            00 00 00 00 00 00 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 32 32 00 00 00 00 ff 00 00 00 00
            00 00 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
            00 01 00 32 32 ff 04 00 00 00 00 00 00 00 22 b6 00 00 cd 08 00 28 ff 06 00 00 00 00 00 00 00 00
            00 00 00 6e 06 00 40 37 08 00 00 1f 01 08 00 00 00 10 00 00 22 00 00 00 00 00 00 00
            """).ToArray();

        Protocol03ObjectViewSample[] samples = PacketResearcher.DetectProtocol03ObjectViews(bytes, 538, "server").ToArray();

        const string expectedParserAction = "test guarded mode 06 78-byte marker-ff non-object tuple body";
        const int expectedPayloadBytes = 16 + 55 + 78;
        Assert.Equal(2, samples.Length);
        Assert.True(samples[0].Complete);
        Assert.Equal(2, samples[0].ParsedUpdateCount);
        Assert.Equal(0, samples[0].UnparsedBytes);
        Assert.Collection(
            samples[0].Segments,
            segment =>
            {
                Assert.Equal("0e", segment.Selector);
                Assert.Equal("position xyz + two flags", segment.Classification);
                Assert.Equal(14, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            },
            segment =>
            {
                Assert.Equal("21", segment.Selector);
                Assert.Equal("mode 06 guarded 78-byte marker-ff non-object tuple body", segment.Classification);
                Assert.Equal(expectedPayloadBytes, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            });
        Assert.Equal("single position update", samples[1].Classification);

        Protocol03NestedMovementConsumedBodyBoundary boundary = Assert.Single(samples[0].NestedMovementConsumedBodyBoundaries);
        Assert.Equal(expectedParserAction, boundary.ParserAction);
        Assert.Equal("21", boundary.Selector);
        Assert.Equal(18, boundary.Offset);
        Assert.Equal(expectedPayloadBytes, boundary.PayloadBytes);
        Assert.Equal(expectedPayloadBytes + 8, boundary.EvidencePayloadBytes);
        Assert.Equal(16, boundary.TailStartOffset);
        Assert.Equal(133, boundary.TailBoundaryOffset);
        Assert.Equal(78, boundary.PostTupleBodyBytes);
        Assert.Equal("non-object marker tuple", boundary.TupleLayoutKind);
        Assert.Equal(6, boundary.FirstNonZeroOffset);
        Assert.Equal("ff 00", boundary.FirstNonZeroFieldHex);
        Assert.Equal("00 1f 01 08 00 00 00 10", boundary.BoundaryHeaderHex);
        Assert.Equal("single position update", boundary.BoundaryHeaderClassification);

        PacketDumpFileSummary dump = new(
            "mode06-guarded-marker-ff-non-object-78.txt",
            1,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            Array.Empty<Protocol04PacketSequenceSample>(),
            new[] { samples[0] },
            Array.Empty<Protocol04InteractionPayloadSample>(),
            Array.Empty<PlayerAttributePayloadSample>(),
            Array.Empty<ManageBonusPayloadSample>());

        Protocol03NestedMovementMode06PostContinuationTupleBodySummary tupleBody =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06PostContinuationTupleBodySummaries(new[] { dump }));
        Assert.Equal("ff marker plus zero-padded ff continuation lead", tupleBody.TailPrefixKind);
        Assert.Equal("01 ff", tupleBody.PrefixFirstNonZeroFieldHex);
        Assert.Equal("non-object marker tuple", tupleBody.TupleLayoutKind);
        Assert.Equal("non-object-view post-prefix lead", tupleBody.PostPrefixDisposition);
        Assert.Equal(78, tupleBody.PostTupleBodyBytes);
        Assert.Equal("00 00 00 00 00 00 ff 00 00 00 00 00 00 00 00 00", tupleBody.BodyPrefixHex);
        Assert.Equal("00 00 00 00 00 00 00 00 00 6e 06 00 40 37 08 00", tupleBody.BodySuffixHex);
        Assert.Equal(6, tupleBody.FirstNonZeroOffset);
        Assert.Equal("ff 00", tupleBody.FirstNonZeroFieldHex);
        Assert.Equal(1, tupleBody.HeaderClassifications["single position update"]);
        Assert.Equal(1, tupleBody.HeaderPrefixes["00 1f 01 08 00 00 00 10"]);
        Assert.Equal(1, tupleBody.PostPrefixLeads["00 32 32 00 00 00 00 ff"]);
        Assert.Equal(1, tupleBody.InnerSelectors["0c"]);
        Assert.Equal(1, tupleBody.OuterSelectors["21"]);

        Protocol03NestedMovementMode06TupleBodyParserActionSummary parserAction =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06TupleBodyParserActionSummaries(new[] { tupleBody }));
        Assert.Equal(expectedParserAction, parserAction.ParserAction);
        Assert.Equal("78-byte marker-ff non-object body before single-position boundary", parserAction.ActionReason);
        Assert.Equal(1, parserAction.BodyByteLengths["78"]);
        Assert.Empty(PacketResearcher.BuildProtocol03NestedMovementMode06HeldTupleBodySummaries(new[] { tupleBody }));
    }

    [Fact]
    public void DetectProtocol03ObjectViews_BoundsGuardedMode06119ByteMarker04ZeroUpdateTupleBody()
    {
        byte[] bytes = PacketResearcher.ParseHexBytes("""
            02 03 39 00 02 0e 04 ed 50 fa 83 47 00 00 be 42 a3 e3 44 47 33 00 06 0e 01 2d 5e d1 82 47 00 00
            be 42 80 e7 1c 47 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00
            00 00 00 00 00 00 00 00 00 00 00 00 03 ff 00 00 00 00 00 00 00 00 96 00 00 00 00 00 04 00 00 00
            00 00 00 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
            00 00 01 00 96 00 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 ff 00 00 00 00 00 00 00 00
            00 00 00 00 32 03 00 08 37 08 00 00 1f 42 21 00 00 00 00 00 00 22 00 00 00 00 00 25 00 02 2a 02
            40 00 25 ac 00 00 10 00 00 9a 8d 47 00 00 be 42 00 48 3c 47 1d 00 02 0e 00 76 bc 15 93 47 00 00
            be 42 a8 fa 2f 47 0f 00 02 0e 00 30 df 18 8f 47 00 00 be 42 44 1d 43 47 0d 00 02 0e 00 8d 75 40
            8b 47 00 00 be 42 12 c6 3e 47 05 00 02 0c 74 18 63 92 47 00 00 be 42 d0 a8 42 47 00 00
            """).ToArray();

        Protocol03ObjectViewSample[] samples = PacketResearcher.DetectProtocol03ObjectViews(bytes, 539, "server").ToArray();

        const string expectedParserAction = "test guarded mode 06 119-byte marker-04 zero-update tuple body";
        const int expectedPayloadBytes = 17 + 55 + 119;
        Assert.True(samples.Length >= 2);
        Assert.True(samples[0].Complete);
        Assert.Equal(2, samples[0].ParsedUpdateCount);
        Assert.Equal(0, samples[0].UnparsedBytes);
        Assert.Collection(
            samples[0].Segments,
            segment =>
            {
                Assert.Equal("0e", segment.Selector);
                Assert.Equal("position xyz + two flags", segment.Classification);
                Assert.Equal(14, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            },
            segment =>
            {
                Assert.Equal("33", segment.Selector);
                Assert.Equal("mode 06 guarded 119-byte marker-04 zero-update tuple body", segment.Classification);
                Assert.Equal(expectedPayloadBytes, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            });
        Assert.Equal("object state update", samples[1].Classification);

        Protocol03NestedMovementConsumedBodyBoundary boundary = Assert.Single(samples[0].NestedMovementConsumedBodyBoundaries);
        Assert.Equal(expectedParserAction, boundary.ParserAction);
        Assert.Equal("33", boundary.Selector);
        Assert.Equal(18, boundary.Offset);
        Assert.Equal(expectedPayloadBytes, boundary.PayloadBytes);
        Assert.Equal(expectedPayloadBytes + 8, boundary.EvidencePayloadBytes);
        Assert.Equal(17, boundary.TailStartOffset);
        Assert.Equal(174, boundary.TailBoundaryOffset);
        Assert.Equal(119, boundary.PostTupleBodyBytes);
        Assert.Equal("zero-update marker tuple", boundary.TupleLayoutKind);
        Assert.Equal(6, boundary.FirstNonZeroOffset);
        Assert.Equal("ff 00", boundary.FirstNonZeroFieldHex);
        Assert.Equal("1d 00 02 0e 00 76 bc 15", boundary.BoundaryHeaderHex);
        Assert.Equal("object state update", boundary.BoundaryHeaderClassification);

        PacketDumpFileSummary dump = new(
            "mode06-guarded-marker-04-zero-update-119.txt",
            1,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            Array.Empty<Protocol04PacketSequenceSample>(),
            new[] { samples[0] },
            Array.Empty<Protocol04InteractionPayloadSample>(),
            Array.Empty<PlayerAttributePayloadSample>(),
            Array.Empty<ManageBonusPayloadSample>());

        Protocol03NestedMovementMode06PostContinuationTupleBodySummary tupleBody =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06PostContinuationTupleBodySummaries(new[] { dump }));
        Assert.Equal("ff marker plus zero-padded ff continuation lead", tupleBody.TailPrefixKind);
        Assert.Equal("03 ff", tupleBody.PrefixFirstNonZeroFieldHex);
        Assert.Equal("zero-update marker tuple", tupleBody.TupleLayoutKind);
        Assert.Equal("non-object-view post-prefix lead", tupleBody.PostPrefixDisposition);
        Assert.Equal(119, tupleBody.PostTupleBodyBytes);
        Assert.Equal("00 00 00 00 00 00 ff 00 00 00 00 00 00 00 00 00", tupleBody.BodyPrefixHex);
        Assert.Equal("00 00 10 00 00 9a 8d 47 00 00 be 42 00 48 3c 47", tupleBody.BodySuffixHex);
        Assert.Equal(6, tupleBody.FirstNonZeroOffset);
        Assert.Equal("ff 00", tupleBody.FirstNonZeroFieldHex);
        Assert.Equal(1, tupleBody.HeaderClassifications["object state update"]);
        Assert.Equal(1, tupleBody.HeaderPrefixes["1d 00 02 0e 00 76 bc 15"]);
        Assert.Equal(1, tupleBody.PostPrefixLeads["00 96 00 00 00 00 00 04"]);
        Assert.Equal(1, tupleBody.InnerSelectors["0e"]);
        Assert.Equal(1, tupleBody.OuterSelectors["33"]);

        Protocol03NestedMovementMode06TupleBodyParserActionSummary parserAction =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06TupleBodyParserActionSummaries(new[] { tupleBody }));
        Assert.Equal(expectedParserAction, parserAction.ParserAction);
        Assert.Equal("119-byte marker-04 zero-update body before object-state boundary", parserAction.ActionReason);
        Assert.Equal(1, parserAction.BodyByteLengths["119"]);
        Assert.Empty(PacketResearcher.BuildProtocol03NestedMovementMode06HeldTupleBodySummaries(new[] { tupleBody }));
    }

    [Fact]
    public void DetectProtocol03ObjectViews_BoundsGuardedMode06138ByteMarker4fObjectViewTupleBody()
    {
        byte[] bytes = PacketResearcher.ParseHexBytes("""
            02 03 45 00 02 0e 04 60 86 d6 39 47 00 20 8a c4 00 26 d7 c4 39 00 06 0e 04 75 e1 c9 25 47 00 20
            8a c4 4d 2c ad c5 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00
            00 00 00 00 00 00 00 00 00 00 00 00 03 ff 00 00 00 00 00 00 00 00 e9 07 00 00 00 00 4f 00 00 00
            00 00 00 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
            00 00 01 00 27 07 ff 00 00 00 00 00 00 00 00 3e b9 00 00 4c 0a 00 28 ff 05 00 00 00 00 00 00 00
            00 00 00 00 61 00 00 40 37 08 00 00 1f 07 08 00 00 00 40 00 00 22 00 00 00 00 00 37 00 02 28 00
            40 00 00 00 10 b8 0a 34 47 00 20 8a c4 6c 13 55 c5 31 00 02 28 00 40 00 00 00 10 cd 47 24 47 00
            20 8a c4 85 7c c9 c5 07 00 02 08 ce 41 38 47 00 20 8a c4 9e ca 9a c5 00 00
            """).ToArray();

        Protocol03ObjectViewSample[] samples = PacketResearcher.DetectProtocol03ObjectViews(bytes, 540, "server").ToArray();

        const string expectedParserAction = "test guarded mode 06 138-byte marker-4f object-view tuple body";
        const int expectedPayloadBytes = 210;
        Assert.Equal(2, samples.Length);
        Assert.True(samples[0].Complete);
        Assert.Equal(2, samples[0].ParsedUpdateCount);
        Assert.Equal(0, samples[0].UnparsedBytes);
        Assert.Collection(
            samples[0].Segments,
            segment =>
            {
                Assert.Equal("0e", segment.Selector);
                Assert.Equal("position xyz + two flags", segment.Classification);
                Assert.Equal(14, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            },
            segment =>
            {
                Assert.Equal("39", segment.Selector);
                Assert.Equal("mode 06 guarded 138-byte marker-4f object-view tuple body", segment.Classification);
                Assert.Equal(expectedPayloadBytes, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            });
        Assert.Equal("position/object state update", samples[1].Classification);

        Protocol03NestedMovementConsumedBodyBoundary boundary = Assert.Single(samples[0].NestedMovementConsumedBodyBoundaries);
        Assert.Equal(expectedParserAction, boundary.ParserAction);
        Assert.Equal("39", boundary.Selector);
        Assert.Equal(18, boundary.Offset);
        Assert.Equal(expectedPayloadBytes, boundary.PayloadBytes);
        Assert.Equal(expectedPayloadBytes + 8, boundary.EvidencePayloadBytes);
        Assert.Equal(17, boundary.TailStartOffset);
        Assert.Equal(193, boundary.TailBoundaryOffset);
        Assert.Equal(138, boundary.PostTupleBodyBytes);
        Assert.Equal("unclassified object-view tuple", boundary.TupleLayoutKind);
        Assert.Equal(6, boundary.FirstNonZeroOffset);
        Assert.Equal("ff 00", boundary.FirstNonZeroFieldHex);
        Assert.Equal("07 00 02 08 ce 41 38 47", boundary.BoundaryHeaderHex);
        Assert.Equal("position/object state update", boundary.BoundaryHeaderClassification);

        PacketDumpFileSummary dump = new(
            "mode06-guarded-marker-4f-object-view-138.txt",
            1,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            Array.Empty<Protocol04PacketSequenceSample>(),
            new[] { samples[0] },
            Array.Empty<Protocol04InteractionPayloadSample>(),
            Array.Empty<PlayerAttributePayloadSample>(),
            Array.Empty<ManageBonusPayloadSample>());

        Protocol03NestedMovementMode06PostContinuationTupleBodySummary tupleBody =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06PostContinuationTupleBodySummaries(new[] { dump }));
        Assert.Equal("ff marker plus zero-padded ff continuation lead", tupleBody.TailPrefixKind);
        Assert.Equal("03 ff", tupleBody.PrefixFirstNonZeroFieldHex);
        Assert.Equal("unclassified object-view tuple", tupleBody.TupleLayoutKind);
        Assert.Equal("unclassified object-view update", tupleBody.PostPrefixDisposition);
        Assert.Equal(138, tupleBody.PostTupleBodyBytes);
        Assert.Equal("00 00 00 00 00 00 ff 00 00 00 00 00 00 00 00 00", tupleBody.BodyPrefixHex);
        Assert.Equal("00 00 00 10 cd 47 24 47 00 20 8a c4 85 7c c9 c5", tupleBody.BodySuffixHex);
        Assert.Equal(6, tupleBody.FirstNonZeroOffset);
        Assert.Equal("ff 00", tupleBody.FirstNonZeroFieldHex);
        Assert.Equal(1, tupleBody.HeaderClassifications["position/object state update"]);
        Assert.Equal(1, tupleBody.HeaderPrefixes["07 00 02 08 ce 41 38 47"]);
        Assert.Equal(1, tupleBody.PostPrefixLeads["00 e9 07 00 00 00 00 4f"]);
        Assert.Equal(1, tupleBody.InnerSelectors["0e"]);
        Assert.Equal(1, tupleBody.OuterSelectors["39"]);

        Protocol03NestedMovementMode06TupleBodyParserActionSummary parserAction =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06TupleBodyParserActionSummaries(new[] { tupleBody }));
        Assert.Equal(expectedParserAction, parserAction.ParserAction);
        Assert.Equal("138-byte marker-4f object-view body before object-state boundary", parserAction.ActionReason);
        Assert.Equal(1, parserAction.BodyByteLengths["138"]);
        Assert.Empty(PacketResearcher.BuildProtocol03NestedMovementMode06HeldTupleBodySummaries(new[] { tupleBody }));
    }

    [Theory]
    [InlineData("00 04 01 01 73 01 07 80")]
    [InlineData("00 04 01 01 74 01 08 80")]
    public void DetectProtocol03ObjectViews_BoundsGuardedMode06Marker2a_261ByteDeleteViewObjectViewTupleBody(
        string boundaryHeaderHex)
    {
        const string bodyPrefixHex = "00 00 00 00 00 00 ff 00 00 00 00 00 00 00 00 00";
        const string bodySuffixHex = "1f 07 08 00 00 00 00 00 00 22 00 00 00 00 00 00";
        const string postPrefixLeadHex = "00 4c 04 00 00 00 00 2a";
        byte[] body = Enumerable.Repeat((byte)0x00, 6)
            .Concat(new byte[] { 0xff, 0x00 })
            .Concat(Enumerable.Repeat((byte)0x00, 237))
            .Concat(PacketResearcher.ParseHexBytes(bodySuffixHex))
            .ToArray();
        var bytes = new List<byte>
        {
            0x02, 0x03,
            0x01, 0x00, 0x02, 0x0c
        };
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 13));
        bytes.Add(0x03);
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 06 0e 01 18 58 3a f6 c6 00 00 be 42 44 35 af 46"));
        bytes.AddRange(PacketResearcher.ParseHexBytes("ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00"));
        bytes.AddRange(PacketResearcher.ParseHexBytes("03 ff"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 29));
        bytes.AddRange(PacketResearcher.ParseHexBytes(postPrefixLeadHex));
        bytes.AddRange(body);
        bytes.AddRange(PacketResearcher.ParseHexBytes(boundaryHeaderHex));

        Protocol03ObjectViewSample[] samples = PacketResearcher.DetectProtocol03ObjectViews(bytes, 537, "server").ToArray();

        const int expectedPayloadBytes = 17 + 16 + 31 + 8 + 261;
        Assert.Equal(2, samples.Length);
        Assert.True(samples[0].Complete);
        Assert.Equal(2, samples[0].ParsedUpdateCount);
        Assert.Equal(0, samples[0].UnparsedBytes);
        Assert.Collection(
            samples[0].Segments,
            segment =>
            {
                Assert.Equal("0c", segment.Selector);
                Assert.Equal("position xyz + flag", segment.Classification);
                Assert.Equal(13, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            },
            segment =>
            {
                Assert.Equal("03", segment.Selector);
                Assert.Equal("mode 06 guarded 261-byte marker-2a delete-view object-view tuple body", segment.Classification);
                Assert.Equal(expectedPayloadBytes, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            });
        Assert.Equal("delete view candidate", samples[1].Classification);

        Protocol03NestedMovementConsumedBodyBoundary boundary = Assert.Single(samples[0].NestedMovementConsumedBodyBoundaries);
        Assert.Equal("test guarded mode 06 261-byte marker-2a delete-view object-view tuple body", boundary.ParserAction);
        Assert.Equal("03", boundary.Selector);
        Assert.Equal(17, boundary.Offset);
        Assert.Equal(expectedPayloadBytes, boundary.PayloadBytes);
        Assert.Equal(expectedPayloadBytes + 8, boundary.EvidencePayloadBytes);
        Assert.Equal(17, boundary.TailStartOffset);
        Assert.Equal(316, boundary.TailBoundaryOffset);
        Assert.Equal(261, boundary.PostTupleBodyBytes);
        Assert.Equal("unclassified object-view tuple", boundary.TupleLayoutKind);
        Assert.Equal(6, boundary.FirstNonZeroOffset);
        Assert.Equal("ff 00", boundary.FirstNonZeroFieldHex);
        Assert.Equal(boundaryHeaderHex, boundary.BoundaryHeaderHex);
        Assert.Equal("delete view candidate", boundary.BoundaryHeaderClassification);

        PacketDumpFileSummary dump = new(
            "mode06-guarded-marker-2a-delete-view-object-view-261.txt",
            1,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            Array.Empty<Protocol04PacketSequenceSample>(),
            new[] { samples[0] },
            Array.Empty<Protocol04InteractionPayloadSample>(),
            Array.Empty<PlayerAttributePayloadSample>(),
            Array.Empty<ManageBonusPayloadSample>());

        Protocol03NestedMovementMode06PostContinuationTupleBodySummary tupleBody =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06PostContinuationTupleBodySummaries(new[] { dump }));
        Assert.Equal("ff marker plus zero-padded ff continuation lead", tupleBody.TailPrefixKind);
        Assert.Equal("03 ff", tupleBody.PrefixFirstNonZeroFieldHex);
        Assert.Equal("unclassified object-view tuple", tupleBody.TupleLayoutKind);
        Assert.Equal("unclassified object-view update", tupleBody.PostPrefixDisposition);
        Assert.Equal(261, tupleBody.PostTupleBodyBytes);
        Assert.Equal(bodyPrefixHex, tupleBody.BodyPrefixHex);
        Assert.Equal(bodySuffixHex, tupleBody.BodySuffixHex);
        Assert.Equal(6, tupleBody.FirstNonZeroOffset);
        Assert.Equal("ff 00", tupleBody.FirstNonZeroFieldHex);
        Assert.Equal(1, tupleBody.HeaderClassifications["delete view candidate"]);
        Assert.Equal(1, tupleBody.HeaderPrefixes[boundaryHeaderHex]);
        Assert.Equal(1, tupleBody.PostPrefixLeads[postPrefixLeadHex]);

        Protocol03NestedMovementMode06TupleBodyParserActionSummary parserAction =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06TupleBodyParserActionSummaries(new[] { tupleBody }));
        Assert.Equal("test guarded mode 06 261-byte marker-2a delete-view object-view tuple body", parserAction.ParserAction);
        Assert.Equal("261-byte marker-2a object-view body before paired delete-view boundary", parserAction.ActionReason);
        Assert.Equal(1, parserAction.BodyByteLengths["261"]);
    }

    [Fact]
    public void DetectProtocol03ObjectViews_BoundsGuardedMode0657ByteObjectViewTupleBody()
    {
        byte[] body = Enumerable.Repeat((byte)0x00, 6)
            .Concat(new byte[] { 0xff, 0x00 })
            .Concat(Enumerable.Repeat((byte)0x00, 33))
            .Concat(PacketResearcher.ParseHexBytes("ff 00 00 00 00 00 00 00 00 c1 1e 00 00 4c 0a 00"))
            .ToArray();
        var bytes = new List<byte>
        {
            0x02, 0x03,
            0x01, 0x00, 0x02, 0x0c
        };
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 13));
        bytes.Add(0x03);
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 06 0e 01 18 58 3a f6 c6 00 00 be 42 44 35 af 46"));
        bytes.AddRange(PacketResearcher.ParseHexBytes("ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 22));
        bytes.AddRange(PacketResearcher.ParseHexBytes("01 ff"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 7));
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 72 06 00 00 00 00 1f"));
        bytes.AddRange(body);
        bytes.AddRange(PacketResearcher.ParseHexBytes("28 ff 01 00 00 00 00 00"));

        Protocol03ObjectViewSample[] samples = PacketResearcher.DetectProtocol03ObjectViews(bytes, 532, "server").ToArray();

        const int expectedPayloadBytes = 17 + 55 + 57;
        Assert.Equal(2, samples.Length);
        Assert.True(samples[0].Complete);
        Assert.Equal(2, samples[0].ParsedUpdateCount);
        Assert.Equal(0, samples[0].UnparsedBytes);
        Assert.Collection(
            samples[0].Segments,
            segment =>
            {
                Assert.Equal("0c", segment.Selector);
                Assert.Equal("position xyz + flag", segment.Classification);
                Assert.Equal(13, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            },
            segment =>
            {
                Assert.Equal("03", segment.Selector);
                Assert.Equal("mode 06 guarded 57-byte object-view tuple body", segment.Classification);
                Assert.Equal(expectedPayloadBytes, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            });
        Assert.True(samples[1].Complete);
        Assert.Equal(0xff28, samples[1].ViewId);
        Assert.Equal("single state marker update", samples[1].Classification);

        Protocol03NestedMovementConsumedBodyBoundary boundary = Assert.Single(samples[0].NestedMovementConsumedBodyBoundaries);
        Assert.Equal("test guarded mode 06 57-byte object-view tuple body", boundary.ParserAction);
        Assert.Equal("03", boundary.Selector);
        Assert.Equal(17, boundary.Offset);
        Assert.Equal(expectedPayloadBytes, boundary.PayloadBytes);
        Assert.Equal(expectedPayloadBytes + 8, boundary.EvidencePayloadBytes);
        Assert.Equal(17, boundary.TailStartOffset);
        Assert.Equal(112, boundary.TailBoundaryOffset);
        Assert.Equal(57, boundary.PostTupleBodyBytes);
        Assert.Equal("unclassified object-view tuple", boundary.TupleLayoutKind);
        Assert.Equal(6, boundary.FirstNonZeroOffset);
        Assert.Equal("ff 00", boundary.FirstNonZeroFieldHex);
        Assert.Equal("28 ff 01 00 00 00 00 00", boundary.BoundaryHeaderHex);
        Assert.Equal("single state marker update", boundary.BoundaryHeaderClassification);

        PacketDumpFileSummary dump = new(
            "mode06-guarded-object-view-57.txt",
            1,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            Array.Empty<Protocol04PacketSequenceSample>(),
            new[] { samples[0] },
            Array.Empty<Protocol04InteractionPayloadSample>(),
            Array.Empty<PlayerAttributePayloadSample>(),
            Array.Empty<ManageBonusPayloadSample>());

        Protocol03NestedMovementMode06PostContinuationTupleBodySummary tupleBody =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06PostContinuationTupleBodySummaries(new[] { dump }));
        Assert.Equal("ff marker plus zero-padded ff continuation lead", tupleBody.TailPrefixKind);
        Assert.Equal("01 ff", tupleBody.PrefixFirstNonZeroFieldHex);
        Assert.Equal("unclassified object-view tuple", tupleBody.TupleLayoutKind);
        Assert.Equal("unclassified object-view update", tupleBody.PostPrefixDisposition);
        Assert.Equal(57, tupleBody.PostTupleBodyBytes);
        Assert.Equal("ff 00 00 00 00 00 00 00 00 c1 1e 00 00 4c 0a 00", tupleBody.BodySuffixHex);
        Assert.Equal(6, tupleBody.FirstNonZeroOffset);
        Assert.Equal("ff 00", tupleBody.FirstNonZeroFieldHex);
        Assert.Equal(1, tupleBody.HeaderClassifications["single state marker update"]);
        Assert.Equal(1, tupleBody.HeaderPrefixes["28 ff 01 00 00 00 00 00"]);
        Assert.Equal(1, tupleBody.PostPrefixLeads["00 72 06 00 00 00 00 1f"]);

        Protocol03NestedMovementMode06TupleBodyParserActionSummary parserAction =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06TupleBodyParserActionSummaries(new[] { tupleBody }));
        Assert.Equal("test guarded mode 06 57-byte object-view tuple body", parserAction.ParserAction);
        Assert.Equal(1, parserAction.BodyByteLengths["57"]);
    }

    [Fact]
    public void DetectProtocol03ObjectViews_BoundsGuardedMode0694ByteObjectViewTupleBody()
    {
        byte[] body = Enumerable.Repeat((byte)0x00, 6)
            .Concat(new byte[] { 0xff, 0x00 })
            .Concat(Enumerable.Repeat((byte)0x00, 70))
            .Concat(PacketResearcher.ParseHexBytes("00 1f 07 08 00 00 00 00 00 00 22 00 00 00 00 00"))
            .ToArray();
        var bytes = new List<byte>
        {
            0x02, 0x03,
            0x01, 0x00, 0x02, 0x0c
        };
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 13));
        bytes.Add(0x03);
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 06 0e 01 18 58 3a f6 c6 00 00 be 42 44 35 af 46"));
        bytes.AddRange(PacketResearcher.ParseHexBytes("ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 22));
        bytes.AddRange(PacketResearcher.ParseHexBytes("01 ff"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 7));
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 98 08 00 00 00 00 2a"));
        bytes.AddRange(body);
        bytes.AddRange(PacketResearcher.ParseHexBytes("28 ff 01 00 00 00 00 00"));

        Protocol03ObjectViewSample[] samples = PacketResearcher.DetectProtocol03ObjectViews(bytes, 538, "server").ToArray();

        const int expectedPayloadBytes = 17 + 55 + 94;
        Assert.Equal(2, samples.Length);
        Assert.True(samples[0].Complete);
        Assert.Equal(2, samples[0].ParsedUpdateCount);
        Assert.Equal(0, samples[0].UnparsedBytes);
        Assert.Collection(
            samples[0].Segments,
            segment =>
            {
                Assert.Equal("0c", segment.Selector);
                Assert.Equal("position xyz + flag", segment.Classification);
                Assert.Equal(13, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            },
            segment =>
            {
                Assert.Equal("03", segment.Selector);
                Assert.Equal("mode 06 guarded 94-byte object-view tuple body", segment.Classification);
                Assert.Equal(expectedPayloadBytes, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            });
        Assert.True(samples[1].Complete);
        Assert.Equal(0xff28, samples[1].ViewId);
        Assert.Equal("single state marker update", samples[1].Classification);

        Protocol03NestedMovementConsumedBodyBoundary boundary = Assert.Single(samples[0].NestedMovementConsumedBodyBoundaries);
        Assert.Equal("test guarded mode 06 94-byte object-view tuple body", boundary.ParserAction);
        Assert.Equal("03", boundary.Selector);
        Assert.Equal(17, boundary.Offset);
        Assert.Equal(expectedPayloadBytes, boundary.PayloadBytes);
        Assert.Equal(expectedPayloadBytes + 8, boundary.EvidencePayloadBytes);
        Assert.Equal(17, boundary.TailStartOffset);
        Assert.Equal(149, boundary.TailBoundaryOffset);
        Assert.Equal(94, boundary.PostTupleBodyBytes);
        Assert.Equal("unclassified object-view tuple", boundary.TupleLayoutKind);
        Assert.Equal(6, boundary.FirstNonZeroOffset);
        Assert.Equal("ff 00", boundary.FirstNonZeroFieldHex);
        Assert.Equal("28 ff 01 00 00 00 00 00", boundary.BoundaryHeaderHex);
        Assert.Equal("single state marker update", boundary.BoundaryHeaderClassification);

        PacketDumpFileSummary dump = new(
            "mode06-guarded-object-view-94.txt",
            1,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            Array.Empty<Protocol04PacketSequenceSample>(),
            new[] { samples[0] },
            Array.Empty<Protocol04InteractionPayloadSample>(),
            Array.Empty<PlayerAttributePayloadSample>(),
            Array.Empty<ManageBonusPayloadSample>());

        Protocol03NestedMovementMode06PostContinuationTupleBodySummary tupleBody =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06PostContinuationTupleBodySummaries(new[] { dump }));
        Assert.Equal("ff marker plus zero-padded ff continuation lead", tupleBody.TailPrefixKind);
        Assert.Equal("01 ff", tupleBody.PrefixFirstNonZeroFieldHex);
        Assert.Equal("unclassified object-view tuple", tupleBody.TupleLayoutKind);
        Assert.Equal("unclassified object-view update", tupleBody.PostPrefixDisposition);
        Assert.Equal(94, tupleBody.PostTupleBodyBytes);
        Assert.Equal("00 1f 07 08 00 00 00 00 00 00 22 00 00 00 00 00", tupleBody.BodySuffixHex);
        Assert.Equal(6, tupleBody.FirstNonZeroOffset);
        Assert.Equal("ff 00", tupleBody.FirstNonZeroFieldHex);
        Assert.Equal(1, tupleBody.HeaderClassifications["single state marker update"]);
        Assert.Equal(1, tupleBody.HeaderPrefixes["28 ff 01 00 00 00 00 00"]);
        Assert.Equal(1, tupleBody.PostPrefixLeads["00 98 08 00 00 00 00 2a"]);

        Protocol03NestedMovementMode06TupleBodyParserActionSummary parserAction =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06TupleBodyParserActionSummaries(new[] { tupleBody }));
        Assert.Equal("test guarded mode 06 94-byte object-view tuple body", parserAction.ParserAction);
        Assert.Equal(1, parserAction.BodyByteLengths["94"]);
    }

    [Fact]
    public void DetectProtocol03ObjectViews_BoundsGuardedMode0694ByteMarker34ObjectViewTupleBody()
    {
        byte[] body = Enumerable.Repeat((byte)0x00, 6)
            .Concat(new byte[] { 0xff, 0x00 })
            .Concat(Enumerable.Repeat((byte)0x00, 70))
            .Concat(PacketResearcher.ParseHexBytes("00 1f 06 08 00 00 00 00 00 00 22 00 00 00 00 00"))
            .ToArray();
        var bytes = new List<byte>
        {
            0x02, 0x03,
            0x01, 0x00, 0x02, 0x0c
        };
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 13));
        bytes.Add(0x03);
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 06 0e 01 18 58 3a f6 c6 00 00 be 42 44 35 af 46"));
        bytes.AddRange(PacketResearcher.ParseHexBytes("ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 22));
        bytes.AddRange(PacketResearcher.ParseHexBytes("03 ff"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 7));
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 46 05 00 00 00 00 34"));
        bytes.AddRange(body);
        bytes.AddRange(PacketResearcher.ParseHexBytes("03 00 01 00 01 fe 09 00"));

        Protocol03ObjectViewSample[] samples = PacketResearcher.DetectProtocol03ObjectViews(bytes, 538, "server").ToArray();

        const int expectedPayloadBytes = 17 + 55 + 94;
        Assert.Equal(2, samples.Length);
        Assert.True(samples[0].Complete);
        Assert.Equal(2, samples[0].ParsedUpdateCount);
        Assert.Equal(0, samples[0].UnparsedBytes);
        Assert.Collection(
            samples[0].Segments,
            segment =>
            {
                Assert.Equal("0c", segment.Selector);
                Assert.Equal("position xyz + flag", segment.Classification);
                Assert.Equal(13, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            },
            segment =>
            {
                Assert.Equal("03", segment.Selector);
                Assert.Equal("mode 06 guarded 94-byte marker-34 object-view tuple body", segment.Classification);
                Assert.Equal(expectedPayloadBytes, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            });
        Assert.True(samples[1].Complete);
        Assert.Equal(3, samples[1].ViewId);
        Assert.Equal("single state marker update", samples[1].Classification);

        Protocol03NestedMovementConsumedBodyBoundary boundary = Assert.Single(samples[0].NestedMovementConsumedBodyBoundaries);
        Assert.Equal("test guarded mode 06 94-byte marker-34 object-view tuple body", boundary.ParserAction);
        Assert.Equal("03", boundary.Selector);
        Assert.Equal(17, boundary.Offset);
        Assert.Equal(expectedPayloadBytes, boundary.PayloadBytes);
        Assert.Equal(expectedPayloadBytes + 8, boundary.EvidencePayloadBytes);
        Assert.Equal(17, boundary.TailStartOffset);
        Assert.Equal(149, boundary.TailBoundaryOffset);
        Assert.Equal(94, boundary.PostTupleBodyBytes);
        Assert.Equal("unclassified object-view tuple", boundary.TupleLayoutKind);
        Assert.Equal(6, boundary.FirstNonZeroOffset);
        Assert.Equal("ff 00", boundary.FirstNonZeroFieldHex);
        Assert.Equal("03 00 01 00 01 fe 09 00", boundary.BoundaryHeaderHex);
        Assert.Equal("single state marker update", boundary.BoundaryHeaderClassification);

        PacketDumpFileSummary dump = new(
            "mode06-guarded-marker-34-object-view-94.txt",
            1,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            Array.Empty<Protocol04PacketSequenceSample>(),
            new[] { samples[0] },
            Array.Empty<Protocol04InteractionPayloadSample>(),
            Array.Empty<PlayerAttributePayloadSample>(),
            Array.Empty<ManageBonusPayloadSample>());

        Protocol03NestedMovementMode06PostContinuationTupleBodySummary tupleBody =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06PostContinuationTupleBodySummaries(new[] { dump }));
        Assert.Equal("ff marker plus zero-padded ff continuation lead", tupleBody.TailPrefixKind);
        Assert.Equal("03 ff", tupleBody.PrefixFirstNonZeroFieldHex);
        Assert.Equal("unclassified object-view tuple", tupleBody.TupleLayoutKind);
        Assert.Equal("unclassified object-view update", tupleBody.PostPrefixDisposition);
        Assert.Equal(94, tupleBody.PostTupleBodyBytes);
        Assert.Equal("00 1f 06 08 00 00 00 00 00 00 22 00 00 00 00 00", tupleBody.BodySuffixHex);
        Assert.Equal(6, tupleBody.FirstNonZeroOffset);
        Assert.Equal("ff 00", tupleBody.FirstNonZeroFieldHex);
        Assert.Equal(1, tupleBody.HeaderClassifications["single state marker update"]);
        Assert.Equal(1, tupleBody.HeaderPrefixes["03 00 01 00 01 fe 09 00"]);
        Assert.Equal(1, tupleBody.PostPrefixLeads["00 46 05 00 00 00 00 34"]);

        Protocol03NestedMovementMode06TupleBodyParserActionSummary parserAction =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06TupleBodyParserActionSummaries(new[] { tupleBody }));
        Assert.Equal("test guarded mode 06 94-byte marker-34 object-view tuple body", parserAction.ParserAction);
        Assert.Equal(1, parserAction.BodyByteLengths["94"]);
        Assert.Equal(1, parserAction.MarkerBytes["34"]);
    }

    [Theory]
    [InlineData(
        "01 ff",
        "00 d2 0f 00 00 00 00 4f",
        "00 1f 06 08 00 00 08 00 10 00 22 01 b9 00 00 00",
        "b3 00 02 80 80 10 ce 14",
        "appearance/attribute update candidate",
        "4f")]
    [InlineData(
        "02 ff",
        "00 e9 07 00 00 00 00 34",
        "00 1f 06 08 00 00 02 00 10 00 22 00 00 00 00 00",
        "18 00 02 0c 81 66 f2 48",
        "object state update",
        "34")]
    public void DetectProtocol03ObjectViews_BoundsGuardedMode0694ByteResidualExactObjectViewTupleBody(
        string prefixHex,
        string postPrefixLeadHex,
        string bodySuffixHex,
        string boundaryHeaderHex,
        string boundaryHeaderClassification,
        string markerByte)
    {
        byte[] body = Enumerable.Repeat((byte)0x00, 6)
            .Concat(new byte[] { 0xff, 0x00 })
            .Concat(Enumerable.Repeat((byte)0x00, 70))
            .Concat(PacketResearcher.ParseHexBytes(bodySuffixHex))
            .ToArray();
        var bytes = new List<byte>
        {
            0x02, 0x03,
            0x01, 0x00, 0x02, 0x0c
        };
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 13));
        bytes.Add(0x03);
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 06 0e 01 18 58 3a f6 c6 00 00 be 42 44 35 af 46"));
        bytes.AddRange(PacketResearcher.ParseHexBytes("ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 22));
        bytes.AddRange(PacketResearcher.ParseHexBytes(prefixHex));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 7));
        bytes.AddRange(PacketResearcher.ParseHexBytes(postPrefixLeadHex));
        bytes.AddRange(body);
        bytes.AddRange(PacketResearcher.ParseHexBytes(boundaryHeaderHex));

        Protocol03ObjectViewSample[] samples = PacketResearcher.DetectProtocol03ObjectViews(bytes, 80192, "server").ToArray();

        const string expectedParserAction = "test guarded mode 06 94-byte residual exact object-view tuple body";
        const int expectedPayloadBytes = 17 + 55 + 94;
        Assert.Equal(2, samples.Length);
        Assert.True(samples[0].Complete);
        Assert.Equal(2, samples[0].ParsedUpdateCount);
        Assert.Equal(0, samples[0].UnparsedBytes);
        Assert.Collection(
            samples[0].Segments,
            segment =>
            {
                Assert.Equal("0c", segment.Selector);
                Assert.Equal("position xyz + flag", segment.Classification);
                Assert.Equal(13, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            },
            segment =>
            {
                Assert.Equal("03", segment.Selector);
                Assert.Equal("mode 06 guarded 94-byte residual exact object-view tuple body", segment.Classification);
                Assert.Equal(expectedPayloadBytes, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            });
        Assert.Equal(boundaryHeaderClassification, samples[1].Classification);

        Protocol03NestedMovementConsumedBodyBoundary boundary = Assert.Single(samples[0].NestedMovementConsumedBodyBoundaries);
        Assert.Equal(expectedParserAction, boundary.ParserAction);
        Assert.Equal("03", boundary.Selector);
        Assert.Equal(17, boundary.Offset);
        Assert.Equal(expectedPayloadBytes, boundary.PayloadBytes);
        Assert.Equal(expectedPayloadBytes + 8, boundary.EvidencePayloadBytes);
        Assert.Equal(17, boundary.TailStartOffset);
        Assert.Equal(149, boundary.TailBoundaryOffset);
        Assert.Equal(94, boundary.PostTupleBodyBytes);
        Assert.Equal("unclassified object-view tuple", boundary.TupleLayoutKind);
        Assert.Equal(6, boundary.FirstNonZeroOffset);
        Assert.Equal("ff 00", boundary.FirstNonZeroFieldHex);
        Assert.Equal(boundaryHeaderHex, boundary.BoundaryHeaderHex);
        Assert.Equal(boundaryHeaderClassification, boundary.BoundaryHeaderClassification);

        PacketDumpFileSummary dump = new(
            "mode06-guarded-residual-exact-object-view-94.txt",
            1,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            Array.Empty<Protocol04PacketSequenceSample>(),
            new[] { samples[0] },
            Array.Empty<Protocol04InteractionPayloadSample>(),
            Array.Empty<PlayerAttributePayloadSample>(),
            Array.Empty<ManageBonusPayloadSample>());

        Protocol03NestedMovementMode06PostContinuationTupleBodySummary tupleBody =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06PostContinuationTupleBodySummaries(new[] { dump }));
        Assert.Equal("ff marker plus zero-padded ff continuation lead", tupleBody.TailPrefixKind);
        Assert.Equal(prefixHex, tupleBody.PrefixFirstNonZeroFieldHex);
        Assert.Equal("unclassified object-view tuple", tupleBody.TupleLayoutKind);
        Assert.Equal("unclassified object-view update", tupleBody.PostPrefixDisposition);
        Assert.Equal(94, tupleBody.PostTupleBodyBytes);
        Assert.Equal(bodySuffixHex, tupleBody.BodySuffixHex);
        Assert.Equal(6, tupleBody.FirstNonZeroOffset);
        Assert.Equal("ff 00", tupleBody.FirstNonZeroFieldHex);
        Assert.Equal(1, tupleBody.HeaderClassifications[boundaryHeaderClassification]);
        Assert.Equal(1, tupleBody.HeaderPrefixes[boundaryHeaderHex]);
        Assert.Equal(1, tupleBody.PostPrefixLeads[postPrefixLeadHex]);

        Protocol03NestedMovementMode06TupleBodyParserActionSummary parserAction =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06TupleBodyParserActionSummaries(new[] { tupleBody }));
        Assert.Equal(expectedParserAction, parserAction.ParserAction);
        Assert.Equal(1, parserAction.BodyByteLengths["94"]);
        Assert.Equal(1, parserAction.MarkerBytes[markerByte]);
    }

    [Fact]
    public void DetectProtocol03ObjectViews_BoundsGuardedMode0657ByteMarker24And25ObjectViewTupleBody()
    {
        byte[] body = Enumerable.Repeat((byte)0x00, 6)
            .Concat(new byte[] { 0xff, 0x00 })
            .Concat(Enumerable.Repeat((byte)0x00, 33))
            .Concat(PacketResearcher.ParseHexBytes("ff 00 00 00 00 00 00 00 00 d4 1e 00 00 4c 0a 00"))
            .ToArray();
        var bytes = new List<byte>
        {
            0x02, 0x03,
            0x01, 0x00, 0x02, 0x0c
        };
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 13));
        bytes.Add(0x03);
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 06 0e 01 18 58 3a f6 c6 00 00 be 42 44 35 af 46"));
        bytes.AddRange(PacketResearcher.ParseHexBytes("ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 22));
        bytes.AddRange(PacketResearcher.ParseHexBytes("03 ff"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 7));
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 b6 03 00 00 00 00 24"));
        bytes.AddRange(body);
        bytes.AddRange(PacketResearcher.ParseHexBytes("28 ff 01 00 00 00 00 00"));

        Protocol03ObjectViewSample[] samples = PacketResearcher.DetectProtocol03ObjectViews(bytes, 544, "server").ToArray();

        const int expectedPayloadBytes = 17 + 55 + 57;
        Assert.Equal(2, samples.Length);
        Assert.True(samples[0].Complete);
        Assert.Equal(2, samples[0].ParsedUpdateCount);
        Assert.Equal(0, samples[0].UnparsedBytes);
        Assert.Collection(
            samples[0].Segments,
            segment =>
            {
                Assert.Equal("0c", segment.Selector);
                Assert.Equal("position xyz + flag", segment.Classification);
                Assert.Equal(13, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            },
            segment =>
            {
                Assert.Equal("03", segment.Selector);
                Assert.Equal("mode 06 guarded 57-byte marker-24/25 object-view tuple body", segment.Classification);
                Assert.Equal(expectedPayloadBytes, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            });
        Assert.True(samples[1].Complete);
        Assert.Equal(0xff28, samples[1].ViewId);
        Assert.Equal("single state marker update", samples[1].Classification);

        Protocol03NestedMovementConsumedBodyBoundary boundary = Assert.Single(samples[0].NestedMovementConsumedBodyBoundaries);
        Assert.Equal("test guarded mode 06 57-byte marker-24-25 object-view tuple body", boundary.ParserAction);
        Assert.Equal("03", boundary.Selector);
        Assert.Equal(17, boundary.Offset);
        Assert.Equal(expectedPayloadBytes, boundary.PayloadBytes);
        Assert.Equal(expectedPayloadBytes + 8, boundary.EvidencePayloadBytes);
        Assert.Equal(17, boundary.TailStartOffset);
        Assert.Equal(112, boundary.TailBoundaryOffset);
        Assert.Equal(57, boundary.PostTupleBodyBytes);
        Assert.Equal("unclassified object-view tuple", boundary.TupleLayoutKind);
        Assert.Equal(6, boundary.FirstNonZeroOffset);
        Assert.Equal("ff 00", boundary.FirstNonZeroFieldHex);
        Assert.Equal("28 ff 01 00 00 00 00 00", boundary.BoundaryHeaderHex);
        Assert.Equal("single state marker update", boundary.BoundaryHeaderClassification);

        PacketDumpFileSummary dump = new(
            "mode06-guarded-marker-24-25-object-view-57.txt",
            1,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            Array.Empty<Protocol04PacketSequenceSample>(),
            new[] { samples[0] },
            Array.Empty<Protocol04InteractionPayloadSample>(),
            Array.Empty<PlayerAttributePayloadSample>(),
            Array.Empty<ManageBonusPayloadSample>());

        Protocol03NestedMovementMode06PostContinuationTupleBodySummary tupleBody =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06PostContinuationTupleBodySummaries(new[] { dump }));
        Assert.Equal("ff marker plus zero-padded ff continuation lead", tupleBody.TailPrefixKind);
        Assert.Equal("03 ff", tupleBody.PrefixFirstNonZeroFieldHex);
        Assert.Equal("unclassified object-view tuple", tupleBody.TupleLayoutKind);
        Assert.Equal("unclassified object-view update", tupleBody.PostPrefixDisposition);
        Assert.Equal(57, tupleBody.PostTupleBodyBytes);
        Assert.Equal("ff 00 00 00 00 00 00 00 00 d4 1e 00 00 4c 0a 00", tupleBody.BodySuffixHex);
        Assert.Equal(6, tupleBody.FirstNonZeroOffset);
        Assert.Equal("ff 00", tupleBody.FirstNonZeroFieldHex);
        Assert.Equal(1, tupleBody.HeaderClassifications["single state marker update"]);
        Assert.Equal(1, tupleBody.HeaderPrefixes["28 ff 01 00 00 00 00 00"]);
        Assert.Equal(1, tupleBody.PostPrefixLeads["00 b6 03 00 00 00 00 24"]);

        Protocol03NestedMovementMode06TupleBodyParserActionSummary parserAction =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06TupleBodyParserActionSummaries(new[] { tupleBody }));
        Assert.Equal("test guarded mode 06 57-byte marker-24-25 object-view tuple body", parserAction.ParserAction);
        Assert.Equal(1, parserAction.BodyByteLengths["57"]);
        Assert.Equal(1, parserAction.MarkerBytes["24"]);
    }

    [Fact]
    public void DetectProtocol03ObjectViews_BoundsGuardedMode0657ByteMarker24And26ObjectViewTupleBody()
    {
        byte[] body = Enumerable.Repeat((byte)0x00, 6)
            .Concat(new byte[] { 0xff, 0x00 })
            .Concat(Enumerable.Repeat((byte)0x00, 33))
            .Concat(PacketResearcher.ParseHexBytes("ff 00 00 00 00 00 00 00 00 c2 1e 00 00 4c 0a 00"))
            .ToArray();
        var bytes = new List<byte>
        {
            0x02, 0x03,
            0x01, 0x00, 0x02, 0x0c
        };
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 13));
        bytes.Add(0x03);
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 06 0e 01 18 58 3a f6 c6 00 00 be 42 44 35 af 46"));
        bytes.AddRange(PacketResearcher.ParseHexBytes("ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 22));
        bytes.AddRange(PacketResearcher.ParseHexBytes("03 ff"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 7));
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 e8 03 00 00 00 00 26"));
        bytes.AddRange(body);
        bytes.AddRange(PacketResearcher.ParseHexBytes("28 ff 02 00 00 00 00 00"));

        Protocol03ObjectViewSample[] samples = PacketResearcher.DetectProtocol03ObjectViews(bytes, 557, "server").ToArray();

        const int expectedPayloadBytes = 17 + 55 + 57;
        Assert.Equal(2, samples.Length);
        Assert.True(samples[0].Complete);
        Assert.Equal(2, samples[0].ParsedUpdateCount);
        Assert.Equal(0, samples[0].UnparsedBytes);
        Assert.Collection(
            samples[0].Segments,
            segment =>
            {
                Assert.Equal("0c", segment.Selector);
                Assert.Equal("position xyz + flag", segment.Classification);
                Assert.Equal(13, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            },
            segment =>
            {
                Assert.Equal("03", segment.Selector);
                Assert.Equal("mode 06 guarded 57-byte marker-24/26 object-view tuple body", segment.Classification);
                Assert.Equal(expectedPayloadBytes, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            });
        Assert.True(samples[1].Complete);
        Assert.Equal(0xff28, samples[1].ViewId);
        Assert.Equal("state marker/object update candidate", samples[1].Classification);

        Protocol03NestedMovementConsumedBodyBoundary boundary = Assert.Single(samples[0].NestedMovementConsumedBodyBoundaries);
        Assert.Equal("test guarded mode 06 57-byte marker-24-26 object-view tuple body", boundary.ParserAction);
        Assert.Equal("03", boundary.Selector);
        Assert.Equal(17, boundary.Offset);
        Assert.Equal(expectedPayloadBytes, boundary.PayloadBytes);
        Assert.Equal(expectedPayloadBytes + 8, boundary.EvidencePayloadBytes);
        Assert.Equal(17, boundary.TailStartOffset);
        Assert.Equal(112, boundary.TailBoundaryOffset);
        Assert.Equal(57, boundary.PostTupleBodyBytes);
        Assert.Equal("unclassified object-view tuple", boundary.TupleLayoutKind);
        Assert.Equal(6, boundary.FirstNonZeroOffset);
        Assert.Equal("ff 00", boundary.FirstNonZeroFieldHex);
        Assert.Equal("28 ff 02 00 00 00 00 00", boundary.BoundaryHeaderHex);
        Assert.Equal("state marker/object update candidate", boundary.BoundaryHeaderClassification);

        PacketDumpFileSummary dump = new(
            "mode06-guarded-marker-24-26-object-view-57.txt",
            1,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            Array.Empty<Protocol04PacketSequenceSample>(),
            new[] { samples[0] },
            Array.Empty<Protocol04InteractionPayloadSample>(),
            Array.Empty<PlayerAttributePayloadSample>(),
            Array.Empty<ManageBonusPayloadSample>());

        Protocol03NestedMovementMode06PostContinuationTupleBodySummary tupleBody =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06PostContinuationTupleBodySummaries(new[] { dump }));
        Assert.Equal("ff marker plus zero-padded ff continuation lead", tupleBody.TailPrefixKind);
        Assert.Equal("03 ff", tupleBody.PrefixFirstNonZeroFieldHex);
        Assert.Equal("unclassified object-view tuple", tupleBody.TupleLayoutKind);
        Assert.Equal("unclassified object-view update", tupleBody.PostPrefixDisposition);
        Assert.Equal(57, tupleBody.PostTupleBodyBytes);
        Assert.Equal("ff 00 00 00 00 00 00 00 00 c2 1e 00 00 4c 0a 00", tupleBody.BodySuffixHex);
        Assert.Equal(6, tupleBody.FirstNonZeroOffset);
        Assert.Equal("ff 00", tupleBody.FirstNonZeroFieldHex);
        Assert.Equal(1, tupleBody.HeaderClassifications["state marker/object update candidate"]);
        Assert.Equal(1, tupleBody.HeaderPrefixes["28 ff 02 00 00 00 00 00"]);
        Assert.Equal(1, tupleBody.PostPrefixLeads["00 e8 03 00 00 00 00 26"]);

        Protocol03NestedMovementMode06TupleBodyParserActionSummary parserAction =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06TupleBodyParserActionSummaries(new[] { tupleBody }));
        Assert.Equal("test guarded mode 06 57-byte marker-24-26 object-view tuple body", parserAction.ParserAction);
        Assert.Equal(1, parserAction.BodyByteLengths["57"]);
        Assert.Equal(1, parserAction.MarkerBytes["26"]);
    }

    [Theory]
    [InlineData("ff 00 00 00 00 00 00 00 00 c2 1e 00 00 4c 0a 00")]
    [InlineData("ff 00 00 00 00 00 00 00 00 cf 1e 00 00 4c 0a 00")]
    public void DetectProtocol03ObjectViews_BoundsGuardedMode0657ByteMarker24And26AltObjectViewTupleBody(
        string bodySuffixHex)
    {
        Protocol03ObjectViewSample[] marker24Samples = DetectSample("00 91 05 00 00 00 00 24", bodySuffixHex);
        Protocol03ObjectViewSample[] marker26Samples = DetectSample("00 dc 05 00 00 00 00 26", bodySuffixHex);

        AssertConsumedBoundary(marker24Samples);
        AssertConsumedBoundary(marker26Samples);

        PacketDumpFileSummary dump = new(
            "mode06-guarded-marker-24-26-alt-object-view-57.txt",
            2,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            Array.Empty<Protocol04PacketSequenceSample>(),
            new[] { marker24Samples[0], marker26Samples[0] },
            Array.Empty<Protocol04InteractionPayloadSample>(),
            Array.Empty<PlayerAttributePayloadSample>(),
            Array.Empty<ManageBonusPayloadSample>());

        Protocol03NestedMovementMode06PostContinuationTupleBodySummary tupleBody =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06PostContinuationTupleBodySummaries(new[] { dump }));
        Assert.Equal("ff marker plus zero-padded ff continuation lead", tupleBody.TailPrefixKind);
        Assert.Equal("02 ff", tupleBody.PrefixFirstNonZeroFieldHex);
        Assert.Equal("unclassified object-view tuple", tupleBody.TupleLayoutKind);
        Assert.Equal("unclassified object-view update", tupleBody.PostPrefixDisposition);
        Assert.Equal(57, tupleBody.PostTupleBodyBytes);
        Assert.Equal(bodySuffixHex, tupleBody.BodySuffixHex);
        Assert.Equal(6, tupleBody.FirstNonZeroOffset);
        Assert.Equal("ff 00", tupleBody.FirstNonZeroFieldHex);
        Assert.Equal(2, tupleBody.HeaderClassifications["single state marker update"]);
        Assert.Equal(2, tupleBody.HeaderPrefixes["28 ff 01 00 00 00 00 00"]);
        Assert.Equal(1, tupleBody.PostPrefixLeads["00 91 05 00 00 00 00 24"]);
        Assert.Equal(1, tupleBody.PostPrefixLeads["00 dc 05 00 00 00 00 26"]);

        Protocol03NestedMovementMode06TupleBodyParserActionSummary parserAction =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06TupleBodyParserActionSummaries(new[] { tupleBody }));
        Assert.Equal("test guarded mode 06 57-byte marker-24-26-alt object-view tuple body", parserAction.ParserAction);
        Assert.Equal(2, parserAction.BodyByteLengths["57"]);
        Assert.Equal(1, parserAction.MarkerBytes["24"]);
        Assert.Equal(1, parserAction.MarkerBytes["26"]);

        static Protocol03ObjectViewSample[] DetectSample(string postPrefixLeadHex, string bodySuffixHex)
        {
            byte[] body = Enumerable.Repeat((byte)0x00, 6)
                .Concat(new byte[] { 0xff, 0x00 })
                .Concat(Enumerable.Repeat((byte)0x00, 33))
                .Concat(PacketResearcher.ParseHexBytes(bodySuffixHex))
                .ToArray();
            var bytes = new List<byte>
            {
                0x02, 0x03,
                0x01, 0x00, 0x02, 0x0c
            };
            bytes.AddRange(Enumerable.Repeat((byte)0x00, 13));
            bytes.Add(0x03);
            bytes.AddRange(PacketResearcher.ParseHexBytes("00 06 0e 01 18 58 3a f6 c6 00 00 be 42 44 35 af 46"));
            bytes.AddRange(PacketResearcher.ParseHexBytes("ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00"));
            bytes.AddRange(Enumerable.Repeat((byte)0x00, 22));
            bytes.AddRange(PacketResearcher.ParseHexBytes("02 ff"));
            bytes.AddRange(Enumerable.Repeat((byte)0x00, 7));
            bytes.AddRange(PacketResearcher.ParseHexBytes(postPrefixLeadHex));
            bytes.AddRange(body);
            bytes.AddRange(PacketResearcher.ParseHexBytes("28 ff 01 00 00 00 00 00"));

            return PacketResearcher.DetectProtocol03ObjectViews(bytes, 568, "server").ToArray();
        }

        static void AssertConsumedBoundary(Protocol03ObjectViewSample[] samples)
        {
            const int expectedPayloadBytes = 17 + 55 + 57;
            Assert.Equal(2, samples.Length);
            Assert.True(samples[0].Complete);
            Assert.Equal(2, samples[0].ParsedUpdateCount);
            Assert.Equal(0, samples[0].UnparsedBytes);
            Assert.Collection(
                samples[0].Segments,
                segment =>
                {
                    Assert.Equal("0c", segment.Selector);
                    Assert.Equal("position xyz + flag", segment.Classification);
                    Assert.Equal(13, segment.PayloadBytes);
                    Assert.True(segment.IsKnownLength);
                },
                segment =>
                {
                    Assert.Equal("03", segment.Selector);
                    Assert.Equal("mode 06 guarded 57-byte marker-24/26 alternate object-view tuple body", segment.Classification);
                    Assert.Equal(expectedPayloadBytes, segment.PayloadBytes);
                    Assert.True(segment.IsKnownLength);
                });
            Assert.True(samples[1].Complete);
            Assert.Equal(0xff28, samples[1].ViewId);
            Assert.Equal("single state marker update", samples[1].Classification);

            Protocol03NestedMovementConsumedBodyBoundary boundary = Assert.Single(samples[0].NestedMovementConsumedBodyBoundaries);
            Assert.Equal("test guarded mode 06 57-byte marker-24-26-alt object-view tuple body", boundary.ParserAction);
            Assert.Equal("03", boundary.Selector);
            Assert.Equal(17, boundary.Offset);
            Assert.Equal(expectedPayloadBytes, boundary.PayloadBytes);
            Assert.Equal(expectedPayloadBytes + 8, boundary.EvidencePayloadBytes);
            Assert.Equal(17, boundary.TailStartOffset);
            Assert.Equal(112, boundary.TailBoundaryOffset);
            Assert.Equal(57, boundary.PostTupleBodyBytes);
            Assert.Equal("unclassified object-view tuple", boundary.TupleLayoutKind);
            Assert.Equal(6, boundary.FirstNonZeroOffset);
            Assert.Equal("ff 00", boundary.FirstNonZeroFieldHex);
            Assert.Equal("28 ff 01 00 00 00 00 00", boundary.BoundaryHeaderHex);
            Assert.Equal("single state marker update", boundary.BoundaryHeaderClassification);
        }
    }

    [Fact]
    public void DetectProtocol03ObjectViews_BoundsGuardedMode0657ByteMarker29And2aObjectViewTupleBody()
    {
        byte[] body = Enumerable.Repeat((byte)0x00, 6)
            .Concat(new byte[] { 0xff, 0x00 })
            .Concat(Enumerable.Repeat((byte)0x00, 33))
            .Concat(PacketResearcher.ParseHexBytes("ff 00 00 00 00 00 00 00 00 d5 1e 00 00 4c 0a 00"))
            .ToArray();
        var bytes = new List<byte>
        {
            0x02, 0x03,
            0x01, 0x00, 0x02, 0x0c
        };
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 13));
        bytes.Add(0x03);
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 06 0e 01 18 58 3a f6 c6 00 00 be 42 44 35 af 46"));
        bytes.AddRange(PacketResearcher.ParseHexBytes("ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 22));
        bytes.AddRange(PacketResearcher.ParseHexBytes("03 ff"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 7));
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 4c 04 00 00 00 00 2a"));
        bytes.AddRange(body);
        bytes.AddRange(PacketResearcher.ParseHexBytes("28 ff 02 00 00 00 00 00"));

        Protocol03ObjectViewSample[] samples = PacketResearcher.DetectProtocol03ObjectViews(bytes, 558, "server").ToArray();

        const int expectedPayloadBytes = 17 + 55 + 57;
        Assert.Equal(2, samples.Length);
        Assert.True(samples[0].Complete);
        Assert.Equal(2, samples[0].ParsedUpdateCount);
        Assert.Equal(0, samples[0].UnparsedBytes);
        Assert.Collection(
            samples[0].Segments,
            segment =>
            {
                Assert.Equal("0c", segment.Selector);
                Assert.Equal("position xyz + flag", segment.Classification);
                Assert.Equal(13, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            },
            segment =>
            {
                Assert.Equal("03", segment.Selector);
                Assert.Equal("mode 06 guarded 57-byte marker-29/2a object-view tuple body", segment.Classification);
                Assert.Equal(expectedPayloadBytes, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            });
        Assert.True(samples[1].Complete);
        Assert.Equal(0xff28, samples[1].ViewId);
        Assert.Equal("state marker/object update candidate", samples[1].Classification);

        Protocol03NestedMovementConsumedBodyBoundary boundary = Assert.Single(samples[0].NestedMovementConsumedBodyBoundaries);
        Assert.Equal("test guarded mode 06 57-byte marker-29-2a object-view tuple body", boundary.ParserAction);
        Assert.Equal("03", boundary.Selector);
        Assert.Equal(17, boundary.Offset);
        Assert.Equal(expectedPayloadBytes, boundary.PayloadBytes);
        Assert.Equal(expectedPayloadBytes + 8, boundary.EvidencePayloadBytes);
        Assert.Equal(17, boundary.TailStartOffset);
        Assert.Equal(112, boundary.TailBoundaryOffset);
        Assert.Equal(57, boundary.PostTupleBodyBytes);
        Assert.Equal("unclassified object-view tuple", boundary.TupleLayoutKind);
        Assert.Equal(6, boundary.FirstNonZeroOffset);
        Assert.Equal("ff 00", boundary.FirstNonZeroFieldHex);
        Assert.Equal("28 ff 02 00 00 00 00 00", boundary.BoundaryHeaderHex);
        Assert.Equal("state marker/object update candidate", boundary.BoundaryHeaderClassification);

        PacketDumpFileSummary dump = new(
            "mode06-guarded-marker-29-2a-object-view-57.txt",
            1,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            Array.Empty<Protocol04PacketSequenceSample>(),
            new[] { samples[0] },
            Array.Empty<Protocol04InteractionPayloadSample>(),
            Array.Empty<PlayerAttributePayloadSample>(),
            Array.Empty<ManageBonusPayloadSample>());

        Protocol03NestedMovementMode06PostContinuationTupleBodySummary tupleBody =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06PostContinuationTupleBodySummaries(new[] { dump }));
        Assert.Equal("ff marker plus zero-padded ff continuation lead", tupleBody.TailPrefixKind);
        Assert.Equal("03 ff", tupleBody.PrefixFirstNonZeroFieldHex);
        Assert.Equal("unclassified object-view tuple", tupleBody.TupleLayoutKind);
        Assert.Equal("unclassified object-view update", tupleBody.PostPrefixDisposition);
        Assert.Equal(57, tupleBody.PostTupleBodyBytes);
        Assert.Equal("ff 00 00 00 00 00 00 00 00 d5 1e 00 00 4c 0a 00", tupleBody.BodySuffixHex);
        Assert.Equal(6, tupleBody.FirstNonZeroOffset);
        Assert.Equal("ff 00", tupleBody.FirstNonZeroFieldHex);
        Assert.Equal(1, tupleBody.HeaderClassifications["state marker/object update candidate"]);
        Assert.Equal(1, tupleBody.HeaderPrefixes["28 ff 02 00 00 00 00 00"]);
        Assert.Equal(1, tupleBody.PostPrefixLeads["00 4c 04 00 00 00 00 2a"]);

        Protocol03NestedMovementMode06TupleBodyParserActionSummary parserAction =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06TupleBodyParserActionSummaries(new[] { tupleBody }));
        Assert.Equal("test guarded mode 06 57-byte marker-29-2a object-view tuple body", parserAction.ParserAction);
        Assert.Equal(1, parserAction.BodyByteLengths["57"]);
        Assert.Equal(1, parserAction.MarkerBytes["2a"]);
    }

    [Theory]
    [InlineData("00 66 08 00 00 00 00 29", "29", "28 ff 01 00 00 00 00 00", "single state marker update")]
    [InlineData("00 98 08 00 00 00 00 2a", "2a", "28 ff 02 00 00 00 00 00", "state marker/object update candidate")]
    public void DetectProtocol03ObjectViews_BoundsGuardedMode0657ByteMarker29And2aAltObjectViewTupleBody(
        string postPrefixLeadHex,
        string expectedMarkerByte,
        string boundaryHeaderHex,
        string expectedBoundaryClassification)
    {
        byte[] body = Enumerable.Repeat((byte)0x00, 6)
            .Concat(new byte[] { 0xff, 0x00 })
            .Concat(Enumerable.Repeat((byte)0x00, 33))
            .Concat(PacketResearcher.ParseHexBytes("ff 00 00 00 00 00 00 00 00 da 1e 00 00 4c 0a 00"))
            .ToArray();
        var bytes = new List<byte>
        {
            0x02, 0x03,
            0x01, 0x00, 0x02, 0x0c
        };
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 13));
        bytes.Add(0x03);
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 06 0e 01 18 58 3a f6 c6 00 00 be 42 44 35 af 46"));
        bytes.AddRange(PacketResearcher.ParseHexBytes("ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 22));
        bytes.AddRange(PacketResearcher.ParseHexBytes("01 ff"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 7));
        bytes.AddRange(PacketResearcher.ParseHexBytes(postPrefixLeadHex));
        bytes.AddRange(body);
        bytes.AddRange(PacketResearcher.ParseHexBytes(boundaryHeaderHex));

        Protocol03ObjectViewSample[] samples = PacketResearcher.DetectProtocol03ObjectViews(bytes, 558, "server").ToArray();

        const int expectedPayloadBytes = 17 + 55 + 57;
        Assert.Equal(2, samples.Length);
        Assert.True(samples[0].Complete);
        Assert.Equal(2, samples[0].ParsedUpdateCount);
        Assert.Equal(0, samples[0].UnparsedBytes);
        Assert.Collection(
            samples[0].Segments,
            segment =>
            {
                Assert.Equal("0c", segment.Selector);
                Assert.Equal("position xyz + flag", segment.Classification);
                Assert.Equal(13, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            },
            segment =>
            {
                Assert.Equal("03", segment.Selector);
                Assert.Equal("mode 06 guarded 57-byte marker-29/2a alternate object-view tuple body", segment.Classification);
                Assert.Equal(expectedPayloadBytes, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            });
        Assert.True(samples[1].Complete);
        Assert.Equal(0xff28, samples[1].ViewId);
        Assert.Equal(expectedBoundaryClassification, samples[1].Classification);

        Protocol03NestedMovementConsumedBodyBoundary boundary = Assert.Single(samples[0].NestedMovementConsumedBodyBoundaries);
        Assert.Equal("test guarded mode 06 57-byte marker-29-2a-alt object-view tuple body", boundary.ParserAction);
        Assert.Equal("03", boundary.Selector);
        Assert.Equal(17, boundary.Offset);
        Assert.Equal(expectedPayloadBytes, boundary.PayloadBytes);
        Assert.Equal(expectedPayloadBytes + 8, boundary.EvidencePayloadBytes);
        Assert.Equal(17, boundary.TailStartOffset);
        Assert.Equal(112, boundary.TailBoundaryOffset);
        Assert.Equal(57, boundary.PostTupleBodyBytes);
        Assert.Equal("unclassified object-view tuple", boundary.TupleLayoutKind);
        Assert.Equal(6, boundary.FirstNonZeroOffset);
        Assert.Equal("ff 00", boundary.FirstNonZeroFieldHex);
        Assert.Equal(boundaryHeaderHex, boundary.BoundaryHeaderHex);
        Assert.Equal(expectedBoundaryClassification, boundary.BoundaryHeaderClassification);

        PacketDumpFileSummary dump = new(
            "mode06-guarded-marker-29-2a-alt-object-view-57.txt",
            1,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            Array.Empty<Protocol04PacketSequenceSample>(),
            new[] { samples[0] },
            Array.Empty<Protocol04InteractionPayloadSample>(),
            Array.Empty<PlayerAttributePayloadSample>(),
            Array.Empty<ManageBonusPayloadSample>());

        Protocol03NestedMovementMode06PostContinuationTupleBodySummary tupleBody =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06PostContinuationTupleBodySummaries(new[] { dump }));
        Assert.Equal("ff marker plus zero-padded ff continuation lead", tupleBody.TailPrefixKind);
        Assert.Equal("01 ff", tupleBody.PrefixFirstNonZeroFieldHex);
        Assert.Equal("unclassified object-view tuple", tupleBody.TupleLayoutKind);
        Assert.Equal("unclassified object-view update", tupleBody.PostPrefixDisposition);
        Assert.Equal(57, tupleBody.PostTupleBodyBytes);
        Assert.Equal("ff 00 00 00 00 00 00 00 00 da 1e 00 00 4c 0a 00", tupleBody.BodySuffixHex);
        Assert.Equal(6, tupleBody.FirstNonZeroOffset);
        Assert.Equal("ff 00", tupleBody.FirstNonZeroFieldHex);
        Assert.Equal(1, tupleBody.HeaderClassifications[expectedBoundaryClassification]);
        Assert.Equal(1, tupleBody.HeaderPrefixes[boundaryHeaderHex]);
        Assert.Equal(1, tupleBody.PostPrefixLeads[postPrefixLeadHex]);

        Protocol03NestedMovementMode06TupleBodyParserActionSummary parserAction =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06TupleBodyParserActionSummaries(new[] { tupleBody }));
        Assert.Equal("test guarded mode 06 57-byte marker-29-2a-alt object-view tuple body", parserAction.ParserAction);
        Assert.Equal(1, parserAction.BodyByteLengths["57"]);
        Assert.Equal(1, parserAction.MarkerBytes[expectedMarkerByte]);
    }

    [Fact]
    public void DetectProtocol03ObjectViews_BoundsGuardedMode0657ByteMarker24AltObjectViewTupleBody()
    {
        byte[] body = Enumerable.Repeat((byte)0x00, 6)
            .Concat(new byte[] { 0xff, 0x00 })
            .Concat(Enumerable.Repeat((byte)0x00, 33))
            .Concat(PacketResearcher.ParseHexBytes("ff 00 00 00 00 00 00 00 00 d9 1e 00 00 4c 0a 00"))
            .ToArray();
        var bytes = new List<byte>
        {
            0x02, 0x03,
            0x01, 0x00, 0x02, 0x0c
        };
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 13));
        bytes.Add(0x03);
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 06 0e 01 18 58 3a f6 c6 00 00 be 42 44 35 af 46"));
        bytes.AddRange(PacketResearcher.ParseHexBytes("ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 22));
        bytes.AddRange(PacketResearcher.ParseHexBytes("02 ff"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 7));
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 91 05 00 00 00 00 24"));
        bytes.AddRange(body);
        bytes.AddRange(PacketResearcher.ParseHexBytes("28 ff 02 00 00 00 00 00"));

        Protocol03ObjectViewSample[] samples = PacketResearcher.DetectProtocol03ObjectViews(bytes, 561, "server").ToArray();

        const int expectedPayloadBytes = 17 + 55 + 57;
        Assert.Equal(2, samples.Length);
        Assert.True(samples[0].Complete);
        Assert.Equal(2, samples[0].ParsedUpdateCount);
        Assert.Equal(0, samples[0].UnparsedBytes);
        Assert.Collection(
            samples[0].Segments,
            segment =>
            {
                Assert.Equal("0c", segment.Selector);
                Assert.Equal("position xyz + flag", segment.Classification);
                Assert.Equal(13, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            },
            segment =>
            {
                Assert.Equal("03", segment.Selector);
                Assert.Equal("mode 06 guarded 57-byte marker-24 alternate object-view tuple body", segment.Classification);
                Assert.Equal(expectedPayloadBytes, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            });
        Assert.True(samples[1].Complete);
        Assert.Equal(0xff28, samples[1].ViewId);
        Assert.Equal("state marker/object update candidate", samples[1].Classification);

        Protocol03NestedMovementConsumedBodyBoundary boundary = Assert.Single(samples[0].NestedMovementConsumedBodyBoundaries);
        Assert.Equal("test guarded mode 06 57-byte marker-24-alt object-view tuple body", boundary.ParserAction);
        Assert.Equal("03", boundary.Selector);
        Assert.Equal(17, boundary.Offset);
        Assert.Equal(expectedPayloadBytes, boundary.PayloadBytes);
        Assert.Equal(expectedPayloadBytes + 8, boundary.EvidencePayloadBytes);
        Assert.Equal(17, boundary.TailStartOffset);
        Assert.Equal(112, boundary.TailBoundaryOffset);
        Assert.Equal(57, boundary.PostTupleBodyBytes);
        Assert.Equal("unclassified object-view tuple", boundary.TupleLayoutKind);
        Assert.Equal(6, boundary.FirstNonZeroOffset);
        Assert.Equal("ff 00", boundary.FirstNonZeroFieldHex);
        Assert.Equal("28 ff 02 00 00 00 00 00", boundary.BoundaryHeaderHex);
        Assert.Equal("state marker/object update candidate", boundary.BoundaryHeaderClassification);

        PacketDumpFileSummary dump = new(
            "mode06-guarded-marker-24-alt-object-view-57.txt",
            1,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            Array.Empty<Protocol04PacketSequenceSample>(),
            new[] { samples[0] },
            Array.Empty<Protocol04InteractionPayloadSample>(),
            Array.Empty<PlayerAttributePayloadSample>(),
            Array.Empty<ManageBonusPayloadSample>());

        Protocol03NestedMovementMode06PostContinuationTupleBodySummary tupleBody =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06PostContinuationTupleBodySummaries(new[] { dump }));
        Assert.Equal("ff marker plus zero-padded ff continuation lead", tupleBody.TailPrefixKind);
        Assert.Equal("02 ff", tupleBody.PrefixFirstNonZeroFieldHex);
        Assert.Equal("unclassified object-view tuple", tupleBody.TupleLayoutKind);
        Assert.Equal("unclassified object-view update", tupleBody.PostPrefixDisposition);
        Assert.Equal(57, tupleBody.PostTupleBodyBytes);
        Assert.Equal("ff 00 00 00 00 00 00 00 00 d9 1e 00 00 4c 0a 00", tupleBody.BodySuffixHex);
        Assert.Equal(6, tupleBody.FirstNonZeroOffset);
        Assert.Equal("ff 00", tupleBody.FirstNonZeroFieldHex);
        Assert.Equal(1, tupleBody.HeaderClassifications["state marker/object update candidate"]);
        Assert.Equal(1, tupleBody.HeaderPrefixes["28 ff 02 00 00 00 00 00"]);
        Assert.Equal(1, tupleBody.PostPrefixLeads["00 91 05 00 00 00 00 24"]);

        Protocol03NestedMovementMode06TupleBodyParserActionSummary parserAction =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06TupleBodyParserActionSummaries(new[] { tupleBody }));
        Assert.Equal("test guarded mode 06 57-byte marker-24-alt object-view tuple body", parserAction.ParserAction);
        Assert.Equal(1, parserAction.BodyByteLengths["57"]);
        Assert.Equal(1, parserAction.MarkerBytes["24"]);
    }

    [Fact]
    public void DetectProtocol03ObjectViews_BoundsGuardedMode0657ByteMarker1eAnd1fObjectViewTupleBody()
    {
        byte[] body = Enumerable.Repeat((byte)0x00, 6)
            .Concat(new byte[] { 0xff, 0x00 })
            .Concat(Enumerable.Repeat((byte)0x00, 33))
            .Concat(PacketResearcher.ParseHexBytes("ff 00 00 00 00 00 00 00 00 c1 09 00 00 4c 0a 00"))
            .ToArray();
        var bytes = new List<byte>
        {
            0x02, 0x03,
            0x01, 0x00, 0x02, 0x0c
        };
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 13));
        bytes.Add(0x03);
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 06 0e 01 18 58 3a f6 c6 00 00 be 42 44 35 af 46"));
        bytes.AddRange(PacketResearcher.ParseHexBytes("ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 22));
        bytes.AddRange(PacketResearcher.ParseHexBytes("02 ff"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 7));
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 d5 04 00 00 00 00 1f"));
        bytes.AddRange(body);
        bytes.AddRange(PacketResearcher.ParseHexBytes("28 ff 02 00 00 00 00 00"));

        Protocol03ObjectViewSample[] samples = PacketResearcher.DetectProtocol03ObjectViews(bytes, 559, "server").ToArray();

        const int expectedPayloadBytes = 17 + 55 + 57;
        Assert.Equal(2, samples.Length);
        Assert.True(samples[0].Complete);
        Assert.Equal(2, samples[0].ParsedUpdateCount);
        Assert.Equal(0, samples[0].UnparsedBytes);
        Assert.Collection(
            samples[0].Segments,
            segment =>
            {
                Assert.Equal("0c", segment.Selector);
                Assert.Equal("position xyz + flag", segment.Classification);
                Assert.Equal(13, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            },
            segment =>
            {
                Assert.Equal("03", segment.Selector);
                Assert.Equal("mode 06 guarded 57-byte marker-1e/1f object-view tuple body", segment.Classification);
                Assert.Equal(expectedPayloadBytes, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            });
        Assert.True(samples[1].Complete);
        Assert.Equal(0xff28, samples[1].ViewId);
        Assert.Equal("state marker/object update candidate", samples[1].Classification);

        Protocol03NestedMovementConsumedBodyBoundary boundary = Assert.Single(samples[0].NestedMovementConsumedBodyBoundaries);
        Assert.Equal("test guarded mode 06 57-byte marker-1e-1f object-view tuple body", boundary.ParserAction);
        Assert.Equal("03", boundary.Selector);
        Assert.Equal(17, boundary.Offset);
        Assert.Equal(expectedPayloadBytes, boundary.PayloadBytes);
        Assert.Equal(expectedPayloadBytes + 8, boundary.EvidencePayloadBytes);
        Assert.Equal(17, boundary.TailStartOffset);
        Assert.Equal(112, boundary.TailBoundaryOffset);
        Assert.Equal(57, boundary.PostTupleBodyBytes);
        Assert.Equal("unclassified object-view tuple", boundary.TupleLayoutKind);
        Assert.Equal(6, boundary.FirstNonZeroOffset);
        Assert.Equal("ff 00", boundary.FirstNonZeroFieldHex);
        Assert.Equal("28 ff 02 00 00 00 00 00", boundary.BoundaryHeaderHex);
        Assert.Equal("state marker/object update candidate", boundary.BoundaryHeaderClassification);

        PacketDumpFileSummary dump = new(
            "mode06-guarded-marker-1e-1f-object-view-57.txt",
            1,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            Array.Empty<Protocol04PacketSequenceSample>(),
            new[] { samples[0] },
            Array.Empty<Protocol04InteractionPayloadSample>(),
            Array.Empty<PlayerAttributePayloadSample>(),
            Array.Empty<ManageBonusPayloadSample>());

        Protocol03NestedMovementMode06PostContinuationTupleBodySummary tupleBody =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06PostContinuationTupleBodySummaries(new[] { dump }));
        Assert.Equal("ff marker plus zero-padded ff continuation lead", tupleBody.TailPrefixKind);
        Assert.Equal("02 ff", tupleBody.PrefixFirstNonZeroFieldHex);
        Assert.Equal("unclassified object-view tuple", tupleBody.TupleLayoutKind);
        Assert.Equal("unclassified object-view update", tupleBody.PostPrefixDisposition);
        Assert.Equal(57, tupleBody.PostTupleBodyBytes);
        Assert.Equal("ff 00 00 00 00 00 00 00 00 c1 09 00 00 4c 0a 00", tupleBody.BodySuffixHex);
        Assert.Equal(6, tupleBody.FirstNonZeroOffset);
        Assert.Equal("ff 00", tupleBody.FirstNonZeroFieldHex);
        Assert.Equal(1, tupleBody.HeaderClassifications["state marker/object update candidate"]);
        Assert.Equal(1, tupleBody.HeaderPrefixes["28 ff 02 00 00 00 00 00"]);
        Assert.Equal(1, tupleBody.PostPrefixLeads["00 d5 04 00 00 00 00 1f"]);

        Protocol03NestedMovementMode06TupleBodyParserActionSummary parserAction =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06TupleBodyParserActionSummaries(new[] { tupleBody }));
        Assert.Equal("test guarded mode 06 57-byte marker-1e-1f object-view tuple body", parserAction.ParserAction);
        Assert.Equal(1, parserAction.BodyByteLengths["57"]);
        Assert.Equal(1, parserAction.MarkerBytes["1f"]);
    }

    [Fact]
    public void DetectProtocol03ObjectViews_BoundsGuardedMode0657ByteMarker1dAnd1eObjectViewTupleBody()
    {
        byte[] body = Enumerable.Repeat((byte)0x00, 6)
            .Concat(new byte[] { 0xff, 0x00 })
            .Concat(Enumerable.Repeat((byte)0x00, 33))
            .Concat(PacketResearcher.ParseHexBytes("ff 00 00 00 00 00 00 00 ff 00 00 00 00 1d 0a 00"))
            .ToArray();
        var bytes = new List<byte>
        {
            0x02, 0x03,
            0x01, 0x00, 0x02, 0x0c
        };
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 13));
        bytes.Add(0x03);
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 06 0e 01 18 58 3a f6 c6 00 00 be 42 44 35 af 46"));
        bytes.AddRange(PacketResearcher.ParseHexBytes("ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 22));
        bytes.AddRange(PacketResearcher.ParseHexBytes("03 ff"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 7));
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 20 03 00 00 00 00 1e"));
        bytes.AddRange(body);
        bytes.AddRange(PacketResearcher.ParseHexBytes("28 ff 01 00 00 00 00 00"));

        Protocol03ObjectViewSample[] samples = PacketResearcher.DetectProtocol03ObjectViews(bytes, 560, "server").ToArray();

        const int expectedPayloadBytes = 17 + 55 + 57;
        Assert.Equal(2, samples.Length);
        Assert.True(samples[0].Complete);
        Assert.Equal(2, samples[0].ParsedUpdateCount);
        Assert.Equal(0, samples[0].UnparsedBytes);
        Assert.Collection(
            samples[0].Segments,
            segment =>
            {
                Assert.Equal("0c", segment.Selector);
                Assert.Equal("position xyz + flag", segment.Classification);
                Assert.Equal(13, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            },
            segment =>
            {
                Assert.Equal("03", segment.Selector);
                Assert.Equal("mode 06 guarded 57-byte marker-1d/1e object-view tuple body", segment.Classification);
                Assert.Equal(expectedPayloadBytes, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            });
        Assert.True(samples[1].Complete);
        Assert.Equal(0xff28, samples[1].ViewId);
        Assert.Equal("single state marker update", samples[1].Classification);

        Protocol03NestedMovementConsumedBodyBoundary boundary = Assert.Single(samples[0].NestedMovementConsumedBodyBoundaries);
        Assert.Equal("test guarded mode 06 57-byte marker-1d-1e object-view tuple body", boundary.ParserAction);
        Assert.Equal("03", boundary.Selector);
        Assert.Equal(17, boundary.Offset);
        Assert.Equal(expectedPayloadBytes, boundary.PayloadBytes);
        Assert.Equal(expectedPayloadBytes + 8, boundary.EvidencePayloadBytes);
        Assert.Equal(17, boundary.TailStartOffset);
        Assert.Equal(112, boundary.TailBoundaryOffset);
        Assert.Equal(57, boundary.PostTupleBodyBytes);
        Assert.Equal("unclassified object-view tuple", boundary.TupleLayoutKind);
        Assert.Equal(6, boundary.FirstNonZeroOffset);
        Assert.Equal("ff 00", boundary.FirstNonZeroFieldHex);
        Assert.Equal("28 ff 01 00 00 00 00 00", boundary.BoundaryHeaderHex);
        Assert.Equal("single state marker update", boundary.BoundaryHeaderClassification);

        PacketDumpFileSummary dump = new(
            "mode06-guarded-marker-1d-1e-object-view-57.txt",
            1,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            Array.Empty<Protocol04PacketSequenceSample>(),
            new[] { samples[0] },
            Array.Empty<Protocol04InteractionPayloadSample>(),
            Array.Empty<PlayerAttributePayloadSample>(),
            Array.Empty<ManageBonusPayloadSample>());

        Protocol03NestedMovementMode06PostContinuationTupleBodySummary tupleBody =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06PostContinuationTupleBodySummaries(new[] { dump }));
        Assert.Equal("ff marker plus zero-padded ff continuation lead", tupleBody.TailPrefixKind);
        Assert.Equal("03 ff", tupleBody.PrefixFirstNonZeroFieldHex);
        Assert.Equal("unclassified object-view tuple", tupleBody.TupleLayoutKind);
        Assert.Equal("unclassified object-view update", tupleBody.PostPrefixDisposition);
        Assert.Equal(57, tupleBody.PostTupleBodyBytes);
        Assert.Equal("ff 00 00 00 00 00 00 00 ff 00 00 00 00 1d 0a 00", tupleBody.BodySuffixHex);
        Assert.Equal(6, tupleBody.FirstNonZeroOffset);
        Assert.Equal("ff 00", tupleBody.FirstNonZeroFieldHex);
        Assert.Equal(1, tupleBody.HeaderClassifications["single state marker update"]);
        Assert.Equal(1, tupleBody.HeaderPrefixes["28 ff 01 00 00 00 00 00"]);
        Assert.Equal(1, tupleBody.PostPrefixLeads["00 20 03 00 00 00 00 1e"]);

        Protocol03NestedMovementMode06TupleBodyParserActionSummary parserAction =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06TupleBodyParserActionSummaries(new[] { tupleBody }));
        Assert.Equal("test guarded mode 06 57-byte marker-1d-1e object-view tuple body", parserAction.ParserAction);
        Assert.Equal(1, parserAction.BodyByteLengths["57"]);
        Assert.Equal(1, parserAction.MarkerBytes["1e"]);
    }

    [Fact]
    public void DetectProtocol03ObjectViews_BoundsGuardedMode0670ByteObjectViewTupleBody()
    {
        byte[] body = Enumerable.Repeat((byte)0x00, 6)
            .Concat(new byte[] { 0xff, 0x00 })
            .Concat(Enumerable.Repeat((byte)0x00, 46))
            .Concat(PacketResearcher.ParseHexBytes("66 00 00 04 ff 08 00 00 00 00 00 00 00 00 00 00"))
            .ToArray();
        var bytes = new List<byte>
        {
            0x02, 0x03,
            0x01, 0x00, 0x02, 0x0c
        };
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 13));
        bytes.Add(0x03);
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 06 0e 01 18 58 3a f6 c6 00 00 be 42 44 35 af 46"));
        bytes.AddRange(PacketResearcher.ParseHexBytes("ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 22));
        bytes.AddRange(PacketResearcher.ParseHexBytes("01 ff"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 7));
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 98 08 00 00 00 00 2a"));
        bytes.AddRange(body);
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 68 01 00 4e 37 08 00"));

        Protocol03ObjectViewSample[] samples = PacketResearcher.DetectProtocol03ObjectViews(bytes, 539, "server").ToArray();

        const int expectedPayloadBytes = 17 + 55 + 70;
        Assert.Equal(2, samples.Length);
        Assert.True(samples[0].Complete);
        Assert.Equal(2, samples[0].ParsedUpdateCount);
        Assert.Equal(0, samples[0].UnparsedBytes);
        Assert.Collection(
            samples[0].Segments,
            segment =>
            {
                Assert.Equal("0c", segment.Selector);
                Assert.Equal("position xyz + flag", segment.Classification);
                Assert.Equal(13, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            },
            segment =>
            {
                Assert.Equal("03", segment.Selector);
                Assert.Equal("mode 06 guarded 70-byte object-view tuple body", segment.Classification);
                Assert.Equal(expectedPayloadBytes, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            });
        Assert.True(samples[1].Complete);
        Assert.Equal(0x6800, samples[1].ViewId);
        Assert.Equal("single state marker update", samples[1].Classification);

        Protocol03NestedMovementConsumedBodyBoundary boundary = Assert.Single(samples[0].NestedMovementConsumedBodyBoundaries);
        Assert.Equal("test guarded mode 06 70-byte object-view tuple body", boundary.ParserAction);
        Assert.Equal("03", boundary.Selector);
        Assert.Equal(17, boundary.Offset);
        Assert.Equal(expectedPayloadBytes, boundary.PayloadBytes);
        Assert.Equal(expectedPayloadBytes + 8, boundary.EvidencePayloadBytes);
        Assert.Equal(17, boundary.TailStartOffset);
        Assert.Equal(125, boundary.TailBoundaryOffset);
        Assert.Equal(70, boundary.PostTupleBodyBytes);
        Assert.Equal("unclassified object-view tuple", boundary.TupleLayoutKind);
        Assert.Equal(6, boundary.FirstNonZeroOffset);
        Assert.Equal("ff 00", boundary.FirstNonZeroFieldHex);
        Assert.Equal("00 68 01 00 4e 37 08 00", boundary.BoundaryHeaderHex);
        Assert.Equal("single state marker update", boundary.BoundaryHeaderClassification);

        PacketDumpFileSummary dump = new(
            "mode06-guarded-object-view-70.txt",
            1,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            Array.Empty<Protocol04PacketSequenceSample>(),
            new[] { samples[0] },
            Array.Empty<Protocol04InteractionPayloadSample>(),
            Array.Empty<PlayerAttributePayloadSample>(),
            Array.Empty<ManageBonusPayloadSample>());

        Protocol03NestedMovementMode06PostContinuationTupleBodySummary tupleBody =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06PostContinuationTupleBodySummaries(new[] { dump }));
        Assert.Equal("ff marker plus zero-padded ff continuation lead", tupleBody.TailPrefixKind);
        Assert.Equal("01 ff", tupleBody.PrefixFirstNonZeroFieldHex);
        Assert.Equal("unclassified object-view tuple", tupleBody.TupleLayoutKind);
        Assert.Equal("unclassified object-view update", tupleBody.PostPrefixDisposition);
        Assert.Equal(70, tupleBody.PostTupleBodyBytes);
        Assert.Equal("66 00 00 04 ff 08 00 00 00 00 00 00 00 00 00 00", tupleBody.BodySuffixHex);
        Assert.Equal(6, tupleBody.FirstNonZeroOffset);
        Assert.Equal("ff 00", tupleBody.FirstNonZeroFieldHex);
        Assert.Equal(1, tupleBody.HeaderClassifications["single state marker update"]);
        Assert.Equal(1, tupleBody.HeaderPrefixes["00 68 01 00 4e 37 08 00"]);
        Assert.Equal(1, tupleBody.PostPrefixLeads["00 98 08 00 00 00 00 2a"]);

        Protocol03NestedMovementMode06TupleBodyParserActionSummary parserAction =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06TupleBodyParserActionSummaries(new[] { tupleBody }));
        Assert.Equal("test guarded mode 06 70-byte object-view tuple body", parserAction.ParserAction);
        Assert.Equal(1, parserAction.BodyByteLengths["70"]);
    }

    [Fact]
    public void DetectProtocol03ObjectViews_BoundsGuardedMode0657ByteMarker16ObjectViewTupleBody()
    {
        byte[] body = Enumerable.Repeat((byte)0x00, 6)
            .Concat(new byte[] { 0xff, 0x00 })
            .Concat(Enumerable.Repeat((byte)0x00, 33))
            .Concat(PacketResearcher.ParseHexBytes("ff 00 00 00 00 00 00 00 00 c7 09 00 00 4c 0a 00"))
            .ToArray();
        var bytes = new List<byte>
        {
            0x02, 0x03,
            0x01, 0x00, 0x02, 0x0c
        };
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 13));
        bytes.Add(0x03);
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 06 0e 01 18 58 3a f6 c6 00 00 be 42 44 35 af 46"));
        bytes.AddRange(PacketResearcher.ParseHexBytes("ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 22));
        bytes.AddRange(PacketResearcher.ParseHexBytes("02 ff"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 7));
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 84 03 00 00 00 00 16"));
        bytes.AddRange(body);
        bytes.AddRange(PacketResearcher.ParseHexBytes("28 ff 01 00 00 00 00 00"));

        Protocol03ObjectViewSample[] samples = PacketResearcher.DetectProtocol03ObjectViews(bytes, 540, "server").ToArray();

        const int expectedPayloadBytes = 17 + 55 + 57;
        Assert.Equal(2, samples.Length);
        Assert.True(samples[0].Complete);
        Assert.Equal(2, samples[0].ParsedUpdateCount);
        Assert.Equal(0, samples[0].UnparsedBytes);
        Assert.Collection(
            samples[0].Segments,
            segment =>
            {
                Assert.Equal("0c", segment.Selector);
                Assert.Equal("position xyz + flag", segment.Classification);
                Assert.Equal(13, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            },
            segment =>
            {
                Assert.Equal("03", segment.Selector);
                Assert.Equal("mode 06 guarded 57-byte marker-16 object-view tuple body", segment.Classification);
                Assert.Equal(expectedPayloadBytes, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            });
        Assert.True(samples[1].Complete);
        Assert.Equal(0xff28, samples[1].ViewId);
        Assert.Equal("single state marker update", samples[1].Classification);

        Protocol03NestedMovementConsumedBodyBoundary boundary = Assert.Single(samples[0].NestedMovementConsumedBodyBoundaries);
        Assert.Equal("test guarded mode 06 57-byte marker-16 object-view tuple body", boundary.ParserAction);
        Assert.Equal("03", boundary.Selector);
        Assert.Equal(17, boundary.Offset);
        Assert.Equal(expectedPayloadBytes, boundary.PayloadBytes);
        Assert.Equal(expectedPayloadBytes + 8, boundary.EvidencePayloadBytes);
        Assert.Equal(17, boundary.TailStartOffset);
        Assert.Equal(112, boundary.TailBoundaryOffset);
        Assert.Equal(57, boundary.PostTupleBodyBytes);
        Assert.Equal("unclassified object-view tuple", boundary.TupleLayoutKind);
        Assert.Equal(6, boundary.FirstNonZeroOffset);
        Assert.Equal("ff 00", boundary.FirstNonZeroFieldHex);
        Assert.Equal("28 ff 01 00 00 00 00 00", boundary.BoundaryHeaderHex);
        Assert.Equal("single state marker update", boundary.BoundaryHeaderClassification);

        PacketDumpFileSummary dump = new(
            "mode06-guarded-marker-16-object-view-57.txt",
            1,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            Array.Empty<Protocol04PacketSequenceSample>(),
            new[] { samples[0] },
            Array.Empty<Protocol04InteractionPayloadSample>(),
            Array.Empty<PlayerAttributePayloadSample>(),
            Array.Empty<ManageBonusPayloadSample>());

        Protocol03NestedMovementMode06PostContinuationTupleBodySummary tupleBody =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06PostContinuationTupleBodySummaries(new[] { dump }));
        Assert.Equal("ff marker plus zero-padded ff continuation lead", tupleBody.TailPrefixKind);
        Assert.Equal("02 ff", tupleBody.PrefixFirstNonZeroFieldHex);
        Assert.Equal("unclassified object-view tuple", tupleBody.TupleLayoutKind);
        Assert.Equal("unclassified object-view update", tupleBody.PostPrefixDisposition);
        Assert.Equal(57, tupleBody.PostTupleBodyBytes);
        Assert.Equal("ff 00 00 00 00 00 00 00 00 c7 09 00 00 4c 0a 00", tupleBody.BodySuffixHex);
        Assert.Equal(6, tupleBody.FirstNonZeroOffset);
        Assert.Equal("ff 00", tupleBody.FirstNonZeroFieldHex);
        Assert.Equal(1, tupleBody.HeaderClassifications["single state marker update"]);
        Assert.Equal(1, tupleBody.HeaderPrefixes["28 ff 01 00 00 00 00 00"]);
        Assert.Equal(1, tupleBody.PostPrefixLeads["00 84 03 00 00 00 00 16"]);

        Protocol03NestedMovementMode06TupleBodyParserActionSummary parserAction =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06TupleBodyParserActionSummaries(new[] { tupleBody }));
        Assert.Equal("test guarded mode 06 57-byte marker-16 object-view tuple body", parserAction.ParserAction);
        Assert.Equal(1, parserAction.BodyByteLengths["57"]);
    }

    [Fact]
    public void DetectProtocol03ObjectViews_BoundsGuardedMode0657ByteMarker16And17ObjectViewTupleBody()
    {
        byte[] body = Enumerable.Repeat((byte)0x00, 6)
            .Concat(new byte[] { 0xff, 0x00 })
            .Concat(Enumerable.Repeat((byte)0x00, 33))
            .Concat(PacketResearcher.ParseHexBytes("ff 00 00 00 00 00 00 00 00 c7 09 00 00 b5 07 00"))
            .ToArray();
        var bytes = new List<byte>
        {
            0x02, 0x03,
            0x01, 0x00, 0x02, 0x0c
        };
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 13));
        bytes.Add(0x03);
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 06 0e 01 18 58 3a f6 c6 00 00 be 42 44 35 af 46"));
        bytes.AddRange(PacketResearcher.ParseHexBytes("ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 22));
        bytes.AddRange(PacketResearcher.ParseHexBytes("01 ff"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 7));
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 e2 04 00 00 00 00 17"));
        bytes.AddRange(body);
        bytes.AddRange(PacketResearcher.ParseHexBytes("28 ff 02 00 00 00 00 00"));

        Protocol03ObjectViewSample[] samples = PacketResearcher.DetectProtocol03ObjectViews(bytes, 556, "server").ToArray();

        const int expectedPayloadBytes = 17 + 55 + 57;
        Assert.Equal(2, samples.Length);
        Assert.True(samples[0].Complete);
        Assert.Equal(2, samples[0].ParsedUpdateCount);
        Assert.Equal(0, samples[0].UnparsedBytes);
        Assert.Collection(
            samples[0].Segments,
            segment =>
            {
                Assert.Equal("0c", segment.Selector);
                Assert.Equal("position xyz + flag", segment.Classification);
                Assert.Equal(13, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            },
            segment =>
            {
                Assert.Equal("03", segment.Selector);
                Assert.Equal("mode 06 guarded 57-byte marker-16/17 object-view tuple body", segment.Classification);
                Assert.Equal(expectedPayloadBytes, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            });
        Assert.True(samples[1].Complete);
        Assert.Equal(0xff28, samples[1].ViewId);
        Assert.Equal("state marker/object update candidate", samples[1].Classification);

        Protocol03NestedMovementConsumedBodyBoundary boundary = Assert.Single(samples[0].NestedMovementConsumedBodyBoundaries);
        Assert.Equal("test guarded mode 06 57-byte marker-16-17 object-view tuple body", boundary.ParserAction);
        Assert.Equal("03", boundary.Selector);
        Assert.Equal(17, boundary.Offset);
        Assert.Equal(expectedPayloadBytes, boundary.PayloadBytes);
        Assert.Equal(expectedPayloadBytes + 8, boundary.EvidencePayloadBytes);
        Assert.Equal(17, boundary.TailStartOffset);
        Assert.Equal(112, boundary.TailBoundaryOffset);
        Assert.Equal(57, boundary.PostTupleBodyBytes);
        Assert.Equal("unclassified object-view tuple", boundary.TupleLayoutKind);
        Assert.Equal(6, boundary.FirstNonZeroOffset);
        Assert.Equal("ff 00", boundary.FirstNonZeroFieldHex);
        Assert.Equal("28 ff 02 00 00 00 00 00", boundary.BoundaryHeaderHex);
        Assert.Equal("state marker/object update candidate", boundary.BoundaryHeaderClassification);

        PacketDumpFileSummary dump = new(
            "mode06-guarded-marker-16-17-object-view-57.txt",
            1,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            Array.Empty<Protocol04PacketSequenceSample>(),
            new[] { samples[0] },
            Array.Empty<Protocol04InteractionPayloadSample>(),
            Array.Empty<PlayerAttributePayloadSample>(),
            Array.Empty<ManageBonusPayloadSample>());

        Protocol03NestedMovementMode06PostContinuationTupleBodySummary tupleBody =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06PostContinuationTupleBodySummaries(new[] { dump }));
        Assert.Equal("ff marker plus zero-padded ff continuation lead", tupleBody.TailPrefixKind);
        Assert.Equal("01 ff", tupleBody.PrefixFirstNonZeroFieldHex);
        Assert.Equal("unclassified object-view tuple", tupleBody.TupleLayoutKind);
        Assert.Equal("unclassified object-view update", tupleBody.PostPrefixDisposition);
        Assert.Equal(57, tupleBody.PostTupleBodyBytes);
        Assert.Equal("ff 00 00 00 00 00 00 00 00 c7 09 00 00 b5 07 00", tupleBody.BodySuffixHex);
        Assert.Equal(6, tupleBody.FirstNonZeroOffset);
        Assert.Equal("ff 00", tupleBody.FirstNonZeroFieldHex);
        Assert.Equal(1, tupleBody.HeaderClassifications["state marker/object update candidate"]);
        Assert.Equal(1, tupleBody.HeaderPrefixes["28 ff 02 00 00 00 00 00"]);
        Assert.Equal(1, tupleBody.PostPrefixLeads["00 e2 04 00 00 00 00 17"]);

        Protocol03NestedMovementMode06TupleBodyParserActionSummary parserAction =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06TupleBodyParserActionSummaries(new[] { tupleBody }));
        Assert.Equal("test guarded mode 06 57-byte marker-16-17 object-view tuple body", parserAction.ParserAction);
        Assert.Equal(1, parserAction.BodyByteLengths["57"]);
    }

    [Theory]
    [InlineData("00 5e 03 00 00 00 00 15")]
    [InlineData("00 1a 04 00 00 00 00 1a")]
    public void DetectProtocol03ObjectViews_BoundsGuardedMode0657ByteMarker15ObjectViewTupleBody(string postPrefixLeadHex)
    {
        byte[] body = Enumerable.Repeat((byte)0x00, 6)
            .Concat(new byte[] { 0xff, 0x00 })
            .Concat(Enumerable.Repeat((byte)0x00, 33))
            .Concat(PacketResearcher.ParseHexBytes("ff 00 00 00 00 00 00 00 00 00 00 00 00 4c 0a 00"))
            .ToArray();
        var bytes = new List<byte>
        {
            0x02, 0x03,
            0x01, 0x00, 0x02, 0x0c
        };
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 13));
        bytes.Add(0x03);
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 06 0e 01 18 58 3a f6 c6 00 00 be 42 44 35 af 46"));
        bytes.AddRange(PacketResearcher.ParseHexBytes("ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 22));
        bytes.AddRange(PacketResearcher.ParseHexBytes("02 ff"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 7));
        bytes.AddRange(PacketResearcher.ParseHexBytes(postPrefixLeadHex));
        bytes.AddRange(body);
        bytes.AddRange(PacketResearcher.ParseHexBytes("28 ff 01 00 00 00 00 00"));

        Protocol03ObjectViewSample[] samples = PacketResearcher.DetectProtocol03ObjectViews(bytes, 541, "server").ToArray();

        const int expectedPayloadBytes = 17 + 55 + 57;
        Assert.Equal(2, samples.Length);
        Assert.True(samples[0].Complete);
        Assert.Equal(2, samples[0].ParsedUpdateCount);
        Assert.Equal(0, samples[0].UnparsedBytes);
        Assert.Collection(
            samples[0].Segments,
            segment =>
            {
                Assert.Equal("0c", segment.Selector);
                Assert.Equal("position xyz + flag", segment.Classification);
                Assert.Equal(13, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            },
            segment =>
            {
                Assert.Equal("03", segment.Selector);
                Assert.Equal("mode 06 guarded 57-byte marker-15 object-view tuple body", segment.Classification);
                Assert.Equal(expectedPayloadBytes, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            });
        Assert.True(samples[1].Complete);
        Assert.Equal(0xff28, samples[1].ViewId);
        Assert.Equal("single state marker update", samples[1].Classification);

        Protocol03NestedMovementConsumedBodyBoundary boundary = Assert.Single(samples[0].NestedMovementConsumedBodyBoundaries);
        Assert.Equal("test guarded mode 06 57-byte marker-15 object-view tuple body", boundary.ParserAction);
        Assert.Equal("03", boundary.Selector);
        Assert.Equal(17, boundary.Offset);
        Assert.Equal(expectedPayloadBytes, boundary.PayloadBytes);
        Assert.Equal(expectedPayloadBytes + 8, boundary.EvidencePayloadBytes);
        Assert.Equal(17, boundary.TailStartOffset);
        Assert.Equal(112, boundary.TailBoundaryOffset);
        Assert.Equal(57, boundary.PostTupleBodyBytes);
        Assert.Equal("unclassified object-view tuple", boundary.TupleLayoutKind);
        Assert.Equal(6, boundary.FirstNonZeroOffset);
        Assert.Equal("ff 00", boundary.FirstNonZeroFieldHex);
        Assert.Equal("28 ff 01 00 00 00 00 00", boundary.BoundaryHeaderHex);
        Assert.Equal("single state marker update", boundary.BoundaryHeaderClassification);

        PacketDumpFileSummary dump = new(
            "mode06-guarded-marker-15-object-view-57.txt",
            1,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            Array.Empty<Protocol04PacketSequenceSample>(),
            new[] { samples[0] },
            Array.Empty<Protocol04InteractionPayloadSample>(),
            Array.Empty<PlayerAttributePayloadSample>(),
            Array.Empty<ManageBonusPayloadSample>());

        Protocol03NestedMovementMode06PostContinuationTupleBodySummary tupleBody =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06PostContinuationTupleBodySummaries(new[] { dump }));
        Assert.Equal("ff marker plus zero-padded ff continuation lead", tupleBody.TailPrefixKind);
        Assert.Equal("02 ff", tupleBody.PrefixFirstNonZeroFieldHex);
        Assert.Equal("unclassified object-view tuple", tupleBody.TupleLayoutKind);
        Assert.Equal("unclassified object-view update", tupleBody.PostPrefixDisposition);
        Assert.Equal(57, tupleBody.PostTupleBodyBytes);
        Assert.Equal("ff 00 00 00 00 00 00 00 00 00 00 00 00 4c 0a 00", tupleBody.BodySuffixHex);
        Assert.Equal(6, tupleBody.FirstNonZeroOffset);
        Assert.Equal("ff 00", tupleBody.FirstNonZeroFieldHex);
        Assert.Equal(1, tupleBody.HeaderClassifications["single state marker update"]);
        Assert.Equal(1, tupleBody.HeaderPrefixes["28 ff 01 00 00 00 00 00"]);
        Assert.Equal(1, tupleBody.PostPrefixLeads[postPrefixLeadHex]);

        Protocol03NestedMovementMode06TupleBodyParserActionSummary parserAction =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06TupleBodyParserActionSummaries(new[] { tupleBody }));
        Assert.Equal("test guarded mode 06 57-byte marker-15 object-view tuple body", parserAction.ParserAction);
        Assert.Equal(1, parserAction.BodyByteLengths["57"]);
    }

    [Fact]
    public void DetectProtocol03ObjectViews_BoundsGuardedMode0657ByteMarker19ObjectViewTupleBody()
    {
        byte[] body = Enumerable.Repeat((byte)0x00, 6)
            .Concat(new byte[] { 0xff, 0x00 })
            .Concat(Enumerable.Repeat((byte)0x00, 33))
            .Concat(PacketResearcher.ParseHexBytes("ff 00 00 00 00 00 00 00 00 d7 1e 00 00 4c 0a 00"))
            .ToArray();
        var bytes = new List<byte>
        {
            0x02, 0x03,
            0x01, 0x00, 0x02, 0x0c
        };
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 13));
        bytes.Add(0x03);
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 06 0e 01 18 58 3a f6 c6 00 00 be 42 44 35 af 46"));
        bytes.AddRange(PacketResearcher.ParseHexBytes("ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 22));
        bytes.AddRange(PacketResearcher.ParseHexBytes("01 ff"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 7));
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 46 05 00 00 00 00 19"));
        bytes.AddRange(body);
        bytes.AddRange(PacketResearcher.ParseHexBytes("28 ff 02 00 00 00 00 00"));

        Protocol03ObjectViewSample[] samples = PacketResearcher.DetectProtocol03ObjectViews(bytes, 563, "server").ToArray();

        const int expectedPayloadBytes = 17 + 55 + 57;
        Assert.Equal(2, samples.Length);
        Assert.True(samples[0].Complete);
        Assert.Equal(2, samples[0].ParsedUpdateCount);
        Assert.Equal(0, samples[0].UnparsedBytes);
        Assert.Collection(
            samples[0].Segments,
            segment =>
            {
                Assert.Equal("0c", segment.Selector);
                Assert.Equal("position xyz + flag", segment.Classification);
                Assert.Equal(13, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            },
            segment =>
            {
                Assert.Equal("03", segment.Selector);
                Assert.Equal("mode 06 guarded 57-byte marker-19 object-view tuple body", segment.Classification);
                Assert.Equal(expectedPayloadBytes, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            });
        Assert.True(samples[1].Complete);
        Assert.Equal(0xff28, samples[1].ViewId);
        Assert.Equal("state marker/object update candidate", samples[1].Classification);

        Protocol03NestedMovementConsumedBodyBoundary boundary = Assert.Single(samples[0].NestedMovementConsumedBodyBoundaries);
        Assert.Equal("test guarded mode 06 57-byte marker-19 object-view tuple body", boundary.ParserAction);
        Assert.Equal("03", boundary.Selector);
        Assert.Equal(17, boundary.Offset);
        Assert.Equal(expectedPayloadBytes, boundary.PayloadBytes);
        Assert.Equal(expectedPayloadBytes + 8, boundary.EvidencePayloadBytes);
        Assert.Equal(17, boundary.TailStartOffset);
        Assert.Equal(112, boundary.TailBoundaryOffset);
        Assert.Equal(57, boundary.PostTupleBodyBytes);
        Assert.Equal("unclassified object-view tuple", boundary.TupleLayoutKind);
        Assert.Equal(6, boundary.FirstNonZeroOffset);
        Assert.Equal("ff 00", boundary.FirstNonZeroFieldHex);
        Assert.Equal("28 ff 02 00 00 00 00 00", boundary.BoundaryHeaderHex);
        Assert.Equal("state marker/object update candidate", boundary.BoundaryHeaderClassification);

        PacketDumpFileSummary dump = new(
            "mode06-guarded-marker-19-object-view-57.txt",
            1,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            Array.Empty<Protocol04PacketSequenceSample>(),
            new[] { samples[0] },
            Array.Empty<Protocol04InteractionPayloadSample>(),
            Array.Empty<PlayerAttributePayloadSample>(),
            Array.Empty<ManageBonusPayloadSample>());

        Protocol03NestedMovementMode06PostContinuationTupleBodySummary tupleBody =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06PostContinuationTupleBodySummaries(new[] { dump }));
        Assert.Equal("ff marker plus zero-padded ff continuation lead", tupleBody.TailPrefixKind);
        Assert.Equal("01 ff", tupleBody.PrefixFirstNonZeroFieldHex);
        Assert.Equal("unclassified object-view tuple", tupleBody.TupleLayoutKind);
        Assert.Equal("unclassified object-view update", tupleBody.PostPrefixDisposition);
        Assert.Equal(57, tupleBody.PostTupleBodyBytes);
        Assert.Equal("ff 00 00 00 00 00 00 00 00 d7 1e 00 00 4c 0a 00", tupleBody.BodySuffixHex);
        Assert.Equal(6, tupleBody.FirstNonZeroOffset);
        Assert.Equal("ff 00", tupleBody.FirstNonZeroFieldHex);
        Assert.Equal(1, tupleBody.HeaderClassifications["state marker/object update candidate"]);
        Assert.Equal(1, tupleBody.HeaderPrefixes["28 ff 02 00 00 00 00 00"]);
        Assert.Equal(1, tupleBody.PostPrefixLeads["00 46 05 00 00 00 00 19"]);

        Protocol03NestedMovementMode06TupleBodyParserActionSummary parserAction =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06TupleBodyParserActionSummaries(new[] { tupleBody }));
        Assert.Equal("test guarded mode 06 57-byte marker-19 object-view tuple body", parserAction.ParserAction);
        Assert.Equal(1, parserAction.BodyByteLengths["57"]);
    }

    [Fact]
    public void DetectProtocol03ObjectViews_BoundsGuardedMode0657ByteMarker1aObjectViewTupleBody()
    {
        byte[] body = Enumerable.Repeat((byte)0x00, 6)
            .Concat(new byte[] { 0xff, 0x00 })
            .Concat(Enumerable.Repeat((byte)0x00, 33))
            .Concat(PacketResearcher.ParseHexBytes("ff 00 00 00 00 00 00 00 00 ca 1e 00 00 4c 0a 00"))
            .ToArray();
        var bytes = new List<byte>
        {
            0x02, 0x03,
            0x01, 0x00, 0x02, 0x0c
        };
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 13));
        bytes.Add(0x03);
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 06 0e 01 18 58 3a f6 c6 00 00 be 42 44 35 af 46"));
        bytes.AddRange(PacketResearcher.ParseHexBytes("ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 22));
        bytes.AddRange(PacketResearcher.ParseHexBytes("01 ff"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 7));
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 78 05 00 00 00 00 1a"));
        bytes.AddRange(body);
        bytes.AddRange(PacketResearcher.ParseHexBytes("28 ff 01 00 00 00 00 00"));

        Protocol03ObjectViewSample[] samples = PacketResearcher.DetectProtocol03ObjectViews(bytes, 568, "server").ToArray();

        const int expectedPayloadBytes = 17 + 55 + 57;
        Assert.Equal(2, samples.Length);
        Assert.True(samples[0].Complete);
        Assert.Equal(2, samples[0].ParsedUpdateCount);
        Assert.Equal(0, samples[0].UnparsedBytes);
        Assert.Collection(
            samples[0].Segments,
            segment =>
            {
                Assert.Equal("0c", segment.Selector);
                Assert.Equal("position xyz + flag", segment.Classification);
                Assert.Equal(13, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            },
            segment =>
            {
                Assert.Equal("03", segment.Selector);
                Assert.Equal("mode 06 guarded 57-byte marker-1a object-view tuple body", segment.Classification);
                Assert.Equal(expectedPayloadBytes, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            });
        Assert.True(samples[1].Complete);
        Assert.Equal(0xff28, samples[1].ViewId);
        Assert.Equal("single state marker update", samples[1].Classification);

        Protocol03NestedMovementConsumedBodyBoundary boundary = Assert.Single(samples[0].NestedMovementConsumedBodyBoundaries);
        Assert.Equal("test guarded mode 06 57-byte marker-1a object-view tuple body", boundary.ParserAction);
        Assert.Equal("03", boundary.Selector);
        Assert.Equal(17, boundary.Offset);
        Assert.Equal(expectedPayloadBytes, boundary.PayloadBytes);
        Assert.Equal(expectedPayloadBytes + 8, boundary.EvidencePayloadBytes);
        Assert.Equal(17, boundary.TailStartOffset);
        Assert.Equal(112, boundary.TailBoundaryOffset);
        Assert.Equal(57, boundary.PostTupleBodyBytes);
        Assert.Equal("unclassified object-view tuple", boundary.TupleLayoutKind);
        Assert.Equal(6, boundary.FirstNonZeroOffset);
        Assert.Equal("ff 00", boundary.FirstNonZeroFieldHex);
        Assert.Equal("28 ff 01 00 00 00 00 00", boundary.BoundaryHeaderHex);
        Assert.Equal("single state marker update", boundary.BoundaryHeaderClassification);

        PacketDumpFileSummary dump = new(
            "mode06-guarded-marker-1a-object-view-57.txt",
            1,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            Array.Empty<Protocol04PacketSequenceSample>(),
            new[] { samples[0] },
            Array.Empty<Protocol04InteractionPayloadSample>(),
            Array.Empty<PlayerAttributePayloadSample>(),
            Array.Empty<ManageBonusPayloadSample>());

        Protocol03NestedMovementMode06PostContinuationTupleBodySummary tupleBody =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06PostContinuationTupleBodySummaries(new[] { dump }));
        Assert.Equal("ff marker plus zero-padded ff continuation lead", tupleBody.TailPrefixKind);
        Assert.Equal("01 ff", tupleBody.PrefixFirstNonZeroFieldHex);
        Assert.Equal("unclassified object-view tuple", tupleBody.TupleLayoutKind);
        Assert.Equal("unclassified object-view update", tupleBody.PostPrefixDisposition);
        Assert.Equal(57, tupleBody.PostTupleBodyBytes);
        Assert.Equal("ff 00 00 00 00 00 00 00 00 ca 1e 00 00 4c 0a 00", tupleBody.BodySuffixHex);
        Assert.Equal(6, tupleBody.FirstNonZeroOffset);
        Assert.Equal("ff 00", tupleBody.FirstNonZeroFieldHex);
        Assert.Equal(1, tupleBody.HeaderClassifications["single state marker update"]);
        Assert.Equal(1, tupleBody.HeaderPrefixes["28 ff 01 00 00 00 00 00"]);
        Assert.Equal(1, tupleBody.PostPrefixLeads["00 78 05 00 00 00 00 1a"]);

        Protocol03NestedMovementMode06TupleBodyParserActionSummary parserAction =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06TupleBodyParserActionSummaries(new[] { tupleBody }));
        Assert.Equal("test guarded mode 06 57-byte marker-1a object-view tuple body", parserAction.ParserAction);
        Assert.Equal(1, parserAction.BodyByteLengths["57"]);
        Assert.Equal(1, parserAction.MarkerBytes["1a"]);
    }

    [Theory]
    [InlineData("ff 00 00 00 00 00 00 00 00 d2 1e 00 00 1d 0a 00")]
    [InlineData("ff 00 00 00 00 00 00 00 ff 00 00 00 00 1d 0a 00")]
    public void DetectProtocol03ObjectViews_BoundsGuardedMode0657ByteMarker19AltObjectViewTupleBody(string bodySuffixHex)
    {
        byte[] body = Enumerable.Repeat((byte)0x00, 6)
            .Concat(new byte[] { 0xff, 0x00 })
            .Concat(Enumerable.Repeat((byte)0x00, 33))
            .Concat(PacketResearcher.ParseHexBytes(bodySuffixHex))
            .ToArray();
        var bytes = new List<byte>
        {
            0x02, 0x03,
            0x01, 0x00, 0x02, 0x0c
        };
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 13));
        bytes.Add(0x03);
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 06 0e 01 18 58 3a f6 c6 00 00 be 42 44 35 af 46"));
        bytes.AddRange(PacketResearcher.ParseHexBytes("ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 22));
        bytes.AddRange(PacketResearcher.ParseHexBytes("02 ff"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 7));
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 f4 03 00 00 00 00 19"));
        bytes.AddRange(body);
        bytes.AddRange(PacketResearcher.ParseHexBytes("28 ff 01 00 00 00 00 00"));

        Protocol03ObjectViewSample[] samples = PacketResearcher.DetectProtocol03ObjectViews(bytes, 564, "server").ToArray();

        const int expectedPayloadBytes = 17 + 55 + 57;
        Assert.Equal(2, samples.Length);
        Assert.True(samples[0].Complete);
        Assert.Equal(2, samples[0].ParsedUpdateCount);
        Assert.Equal(0, samples[0].UnparsedBytes);
        Assert.Collection(
            samples[0].Segments,
            segment =>
            {
                Assert.Equal("0c", segment.Selector);
                Assert.Equal("position xyz + flag", segment.Classification);
                Assert.Equal(13, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            },
            segment =>
            {
                Assert.Equal("03", segment.Selector);
                Assert.Equal("mode 06 guarded 57-byte marker-19-alt object-view tuple body", segment.Classification);
                Assert.Equal(expectedPayloadBytes, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            });
        Assert.True(samples[1].Complete);
        Assert.Equal(0xff28, samples[1].ViewId);
        Assert.Equal("single state marker update", samples[1].Classification);

        Protocol03NestedMovementConsumedBodyBoundary boundary = Assert.Single(samples[0].NestedMovementConsumedBodyBoundaries);
        Assert.Equal("test guarded mode 06 57-byte marker-19-alt object-view tuple body", boundary.ParserAction);
        Assert.Equal("03", boundary.Selector);
        Assert.Equal(17, boundary.Offset);
        Assert.Equal(expectedPayloadBytes, boundary.PayloadBytes);
        Assert.Equal(expectedPayloadBytes + 8, boundary.EvidencePayloadBytes);
        Assert.Equal(17, boundary.TailStartOffset);
        Assert.Equal(112, boundary.TailBoundaryOffset);
        Assert.Equal(57, boundary.PostTupleBodyBytes);
        Assert.Equal("unclassified object-view tuple", boundary.TupleLayoutKind);
        Assert.Equal(6, boundary.FirstNonZeroOffset);
        Assert.Equal("ff 00", boundary.FirstNonZeroFieldHex);
        Assert.Equal("28 ff 01 00 00 00 00 00", boundary.BoundaryHeaderHex);
        Assert.Equal("single state marker update", boundary.BoundaryHeaderClassification);

        PacketDumpFileSummary dump = new(
            "mode06-guarded-marker-19-alt-object-view-57.txt",
            1,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            Array.Empty<Protocol04PacketSequenceSample>(),
            new[] { samples[0] },
            Array.Empty<Protocol04InteractionPayloadSample>(),
            Array.Empty<PlayerAttributePayloadSample>(),
            Array.Empty<ManageBonusPayloadSample>());

        Protocol03NestedMovementMode06PostContinuationTupleBodySummary tupleBody =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06PostContinuationTupleBodySummaries(new[] { dump }));
        Assert.Equal("ff marker plus zero-padded ff continuation lead", tupleBody.TailPrefixKind);
        Assert.Equal("02 ff", tupleBody.PrefixFirstNonZeroFieldHex);
        Assert.Equal("unclassified object-view tuple", tupleBody.TupleLayoutKind);
        Assert.Equal("unclassified object-view update", tupleBody.PostPrefixDisposition);
        Assert.Equal(57, tupleBody.PostTupleBodyBytes);
        Assert.Equal(bodySuffixHex, tupleBody.BodySuffixHex);
        Assert.Equal(6, tupleBody.FirstNonZeroOffset);
        Assert.Equal("ff 00", tupleBody.FirstNonZeroFieldHex);
        Assert.Equal(1, tupleBody.HeaderClassifications["single state marker update"]);
        Assert.Equal(1, tupleBody.HeaderPrefixes["28 ff 01 00 00 00 00 00"]);
        Assert.Equal(1, tupleBody.PostPrefixLeads["00 f4 03 00 00 00 00 19"]);

        Protocol03NestedMovementMode06TupleBodyParserActionSummary parserAction =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06TupleBodyParserActionSummaries(new[] { tupleBody }));
        Assert.Equal("test guarded mode 06 57-byte marker-19-alt object-view tuple body", parserAction.ParserAction);
        Assert.Equal(1, parserAction.BodyByteLengths["57"]);
    }

    [Fact]
    public void DetectProtocol03ObjectViews_BoundsGuardedMode0657ByteMarker1fObjectViewTupleBody()
    {
        byte[] body = Enumerable.Repeat((byte)0x00, 6)
            .Concat(new byte[] { 0xff, 0x00 })
            .Concat(Enumerable.Repeat((byte)0x00, 33))
            .Concat(PacketResearcher.ParseHexBytes("ff 00 00 00 00 00 00 00 00 cb 1e 00 00 4c 0a 00"))
            .ToArray();
        var bytes = new List<byte>
        {
            0x02, 0x03,
            0x01, 0x00, 0x02, 0x0c
        };
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 13));
        bytes.Add(0x03);
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 06 0e 01 18 58 3a f6 c6 00 00 be 42 44 35 af 46"));
        bytes.AddRange(PacketResearcher.ParseHexBytes("ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 22));
        bytes.AddRange(PacketResearcher.ParseHexBytes("03 ff"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 7));
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 39 03 00 00 00 00 1f"));
        bytes.AddRange(body);
        bytes.AddRange(PacketResearcher.ParseHexBytes("28 ff 02 00 00 00 00 00"));

        Protocol03ObjectViewSample[] samples = PacketResearcher.DetectProtocol03ObjectViews(bytes, 542, "server").ToArray();

        const int expectedPayloadBytes = 17 + 55 + 57;
        Assert.Equal(2, samples.Length);
        Assert.True(samples[0].Complete);
        Assert.Equal(2, samples[0].ParsedUpdateCount);
        Assert.Equal(0, samples[0].UnparsedBytes);
        Assert.Collection(
            samples[0].Segments,
            segment =>
            {
                Assert.Equal("0c", segment.Selector);
                Assert.Equal("position xyz + flag", segment.Classification);
                Assert.Equal(13, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            },
            segment =>
            {
                Assert.Equal("03", segment.Selector);
                Assert.Equal("mode 06 guarded 57-byte marker-1f object-view tuple body", segment.Classification);
                Assert.Equal(expectedPayloadBytes, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            });
        Assert.True(samples[1].Complete);
        Assert.Equal(0xff28, samples[1].ViewId);
        Assert.Equal("state marker/object update candidate", samples[1].Classification);

        Protocol03NestedMovementConsumedBodyBoundary boundary = Assert.Single(samples[0].NestedMovementConsumedBodyBoundaries);
        Assert.Equal("test guarded mode 06 57-byte marker-1f object-view tuple body", boundary.ParserAction);
        Assert.Equal("03", boundary.Selector);
        Assert.Equal(17, boundary.Offset);
        Assert.Equal(expectedPayloadBytes, boundary.PayloadBytes);
        Assert.Equal(expectedPayloadBytes + 8, boundary.EvidencePayloadBytes);
        Assert.Equal(17, boundary.TailStartOffset);
        Assert.Equal(112, boundary.TailBoundaryOffset);
        Assert.Equal(57, boundary.PostTupleBodyBytes);
        Assert.Equal("unclassified object-view tuple", boundary.TupleLayoutKind);
        Assert.Equal(6, boundary.FirstNonZeroOffset);
        Assert.Equal("ff 00", boundary.FirstNonZeroFieldHex);
        Assert.Equal("28 ff 02 00 00 00 00 00", boundary.BoundaryHeaderHex);
        Assert.Equal("state marker/object update candidate", boundary.BoundaryHeaderClassification);

        PacketDumpFileSummary dump = new(
            "mode06-guarded-marker-1f-object-view-57.txt",
            1,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            Array.Empty<Protocol04PacketSequenceSample>(),
            new[] { samples[0] },
            Array.Empty<Protocol04InteractionPayloadSample>(),
            Array.Empty<PlayerAttributePayloadSample>(),
            Array.Empty<ManageBonusPayloadSample>());

        Protocol03NestedMovementMode06PostContinuationTupleBodySummary tupleBody =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06PostContinuationTupleBodySummaries(new[] { dump }));
        Assert.Equal("ff marker plus zero-padded ff continuation lead", tupleBody.TailPrefixKind);
        Assert.Equal("03 ff", tupleBody.PrefixFirstNonZeroFieldHex);
        Assert.Equal("unclassified object-view tuple", tupleBody.TupleLayoutKind);
        Assert.Equal("unclassified object-view update", tupleBody.PostPrefixDisposition);
        Assert.Equal(57, tupleBody.PostTupleBodyBytes);
        Assert.Equal("ff 00 00 00 00 00 00 00 00 cb 1e 00 00 4c 0a 00", tupleBody.BodySuffixHex);
        Assert.Equal(6, tupleBody.FirstNonZeroOffset);
        Assert.Equal("ff 00", tupleBody.FirstNonZeroFieldHex);
        Assert.Equal(1, tupleBody.HeaderClassifications["state marker/object update candidate"]);
        Assert.Equal(1, tupleBody.HeaderPrefixes["28 ff 02 00 00 00 00 00"]);
        Assert.Equal(1, tupleBody.PostPrefixLeads["00 39 03 00 00 00 00 1f"]);

        Protocol03NestedMovementMode06TupleBodyParserActionSummary parserAction =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06TupleBodyParserActionSummaries(new[] { tupleBody }));
        Assert.Equal("test guarded mode 06 57-byte marker-1f object-view tuple body", parserAction.ParserAction);
        Assert.Equal(1, parserAction.BodyByteLengths["57"]);
    }

    [Fact]
    public void DetectProtocol03ObjectViews_BoundsGuardedMode0657ByteMarker1fAnd1eAltObjectViewTupleBody()
    {
        Protocol03ObjectViewSample[] marker1fSamples = DetectSample(
            "00 39 03 00 00 00 00 1f",
            "28 ff 02 00 00 00 00 00");
        Protocol03ObjectViewSample[] marker1eSamples = DetectSample(
            "00 20 03 00 00 00 00 1e",
            "28 ff 01 00 00 00 00 00");

        AssertConsumedBoundary(
            marker1fSamples,
            "test guarded mode 06 57-byte marker-1f object-view tuple body",
            "mode 06 guarded 57-byte marker-1f object-view tuple body",
            "state marker/object update candidate",
            "28 ff 02 00 00 00 00 00");
        AssertConsumedBoundary(
            marker1eSamples,
            "test guarded mode 06 57-byte marker-1f-1e-alt object-view tuple body",
            "mode 06 guarded 57-byte marker-1f/1e alternate object-view tuple body",
            "single state marker update",
            "28 ff 01 00 00 00 00 00");

        PacketDumpFileSummary dump = new(
            "mode06-guarded-marker-1f-1e-alt-object-view-57.txt",
            2,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            Array.Empty<Protocol04PacketSequenceSample>(),
            new[] { marker1fSamples[0], marker1eSamples[0] },
            Array.Empty<Protocol04InteractionPayloadSample>(),
            Array.Empty<PlayerAttributePayloadSample>(),
            Array.Empty<ManageBonusPayloadSample>());

        Protocol03NestedMovementMode06PostContinuationTupleBodySummary tupleBody =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06PostContinuationTupleBodySummaries(new[] { dump }));
        Assert.Equal("ff marker plus zero-padded ff continuation lead", tupleBody.TailPrefixKind);
        Assert.Equal("03 ff", tupleBody.PrefixFirstNonZeroFieldHex);
        Assert.Equal("unclassified object-view tuple", tupleBody.TupleLayoutKind);
        Assert.Equal("unclassified object-view update", tupleBody.PostPrefixDisposition);
        Assert.Equal(57, tupleBody.PostTupleBodyBytes);
        Assert.Equal("ff 00 00 00 00 00 00 00 00 cb 1e 00 00 4c 0a 00", tupleBody.BodySuffixHex);
        Assert.Equal(6, tupleBody.FirstNonZeroOffset);
        Assert.Equal("ff 00", tupleBody.FirstNonZeroFieldHex);
        Assert.Equal(1, tupleBody.HeaderClassifications["state marker/object update candidate"]);
        Assert.Equal(1, tupleBody.HeaderClassifications["single state marker update"]);
        Assert.Equal(1, tupleBody.HeaderPrefixes["28 ff 02 00 00 00 00 00"]);
        Assert.Equal(1, tupleBody.HeaderPrefixes["28 ff 01 00 00 00 00 00"]);
        Assert.Equal(1, tupleBody.PostPrefixLeads["00 39 03 00 00 00 00 1f"]);
        Assert.Equal(1, tupleBody.PostPrefixLeads["00 20 03 00 00 00 00 1e"]);

        Protocol03NestedMovementMode06TupleBodyParserActionSummary parserAction =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06TupleBodyParserActionSummaries(new[] { tupleBody }));
        Assert.Equal("test guarded mode 06 57-byte marker-1f-1e-alt object-view tuple body", parserAction.ParserAction);
        Assert.Equal(2, parserAction.BodyByteLengths["57"]);
        Assert.Equal(1, parserAction.MarkerBytes["1f"]);
        Assert.Equal(1, parserAction.MarkerBytes["1e"]);

        static Protocol03ObjectViewSample[] DetectSample(string postPrefixLeadHex, string boundaryHeaderHex)
        {
            byte[] body = Enumerable.Repeat((byte)0x00, 6)
                .Concat(new byte[] { 0xff, 0x00 })
                .Concat(Enumerable.Repeat((byte)0x00, 33))
                .Concat(PacketResearcher.ParseHexBytes("ff 00 00 00 00 00 00 00 00 cb 1e 00 00 4c 0a 00"))
                .ToArray();
            var bytes = new List<byte>
            {
                0x02, 0x03,
                0x01, 0x00, 0x02, 0x0c
            };
            bytes.AddRange(Enumerable.Repeat((byte)0x00, 13));
            bytes.Add(0x03);
            bytes.AddRange(PacketResearcher.ParseHexBytes("00 06 0e 01 18 58 3a f6 c6 00 00 be 42 44 35 af 46"));
            bytes.AddRange(PacketResearcher.ParseHexBytes("ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00"));
            bytes.AddRange(Enumerable.Repeat((byte)0x00, 22));
            bytes.AddRange(PacketResearcher.ParseHexBytes("03 ff"));
            bytes.AddRange(Enumerable.Repeat((byte)0x00, 7));
            bytes.AddRange(PacketResearcher.ParseHexBytes(postPrefixLeadHex));
            bytes.AddRange(body);
            bytes.AddRange(PacketResearcher.ParseHexBytes(boundaryHeaderHex));

            return PacketResearcher.DetectProtocol03ObjectViews(bytes, 567, "server").ToArray();
        }

        static void AssertConsumedBoundary(
            Protocol03ObjectViewSample[] samples,
            string expectedParserAction,
            string expectedSegmentClassification,
            string expectedBoundaryClassification,
            string expectedBoundaryHeaderHex)
        {
            const int expectedPayloadBytes = 17 + 55 + 57;
            Assert.Equal(2, samples.Length);
            Assert.True(samples[0].Complete);
            Assert.Equal(2, samples[0].ParsedUpdateCount);
            Assert.Equal(0, samples[0].UnparsedBytes);
            Assert.Collection(
                samples[0].Segments,
                segment =>
                {
                    Assert.Equal("0c", segment.Selector);
                    Assert.Equal("position xyz + flag", segment.Classification);
                    Assert.Equal(13, segment.PayloadBytes);
                    Assert.True(segment.IsKnownLength);
                },
                segment =>
                {
                    Assert.Equal("03", segment.Selector);
                    Assert.Equal(expectedSegmentClassification, segment.Classification);
                    Assert.Equal(expectedPayloadBytes, segment.PayloadBytes);
                    Assert.True(segment.IsKnownLength);
                });
            Assert.True(samples[1].Complete);
            Assert.Equal(0xff28, samples[1].ViewId);
            Assert.Equal(expectedBoundaryClassification, samples[1].Classification);

            Protocol03NestedMovementConsumedBodyBoundary boundary = Assert.Single(samples[0].NestedMovementConsumedBodyBoundaries);
            Assert.Equal(expectedParserAction, boundary.ParserAction);
            Assert.Equal("03", boundary.Selector);
            Assert.Equal(17, boundary.Offset);
            Assert.Equal(expectedPayloadBytes, boundary.PayloadBytes);
            Assert.Equal(expectedPayloadBytes + 8, boundary.EvidencePayloadBytes);
            Assert.Equal(17, boundary.TailStartOffset);
            Assert.Equal(112, boundary.TailBoundaryOffset);
            Assert.Equal(57, boundary.PostTupleBodyBytes);
            Assert.Equal("unclassified object-view tuple", boundary.TupleLayoutKind);
            Assert.Equal(6, boundary.FirstNonZeroOffset);
            Assert.Equal("ff 00", boundary.FirstNonZeroFieldHex);
            Assert.Equal(expectedBoundaryHeaderHex, boundary.BoundaryHeaderHex);
            Assert.Equal(expectedBoundaryClassification, boundary.BoundaryHeaderClassification);
        }
    }

    [Fact]
    public void DetectProtocol03ObjectViews_BoundsGuardedMode0657ByteMarker24ObjectViewTupleBody()
    {
        byte[] body = Enumerable.Repeat((byte)0x00, 6)
            .Concat(new byte[] { 0xff, 0x00 })
            .Concat(Enumerable.Repeat((byte)0x00, 33))
            .Concat(PacketResearcher.ParseHexBytes("ff 00 00 00 00 00 00 00 00 c7 1e 00 00 4c 0a 00"))
            .ToArray();
        var bytes = new List<byte>
        {
            0x02, 0x03,
            0x01, 0x00, 0x02, 0x0c
        };
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 13));
        bytes.Add(0x03);
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 06 0e 01 18 58 3a f6 c6 00 00 be 42 44 35 af 46"));
        bytes.AddRange(PacketResearcher.ParseHexBytes("ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 22));
        bytes.AddRange(PacketResearcher.ParseHexBytes("01 ff"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 7));
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 6c 07 00 00 00 00 24"));
        bytes.AddRange(body);
        bytes.AddRange(PacketResearcher.ParseHexBytes("28 ff 02 00 00 00 00 00"));

        Protocol03ObjectViewSample[] samples = PacketResearcher.DetectProtocol03ObjectViews(bytes, 543, "server").ToArray();

        const int expectedPayloadBytes = 17 + 55 + 57;
        Assert.Equal(2, samples.Length);
        Assert.True(samples[0].Complete);
        Assert.Equal(2, samples[0].ParsedUpdateCount);
        Assert.Equal(0, samples[0].UnparsedBytes);
        Assert.Collection(
            samples[0].Segments,
            segment =>
            {
                Assert.Equal("0c", segment.Selector);
                Assert.Equal("position xyz + flag", segment.Classification);
                Assert.Equal(13, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            },
            segment =>
            {
                Assert.Equal("03", segment.Selector);
                Assert.Equal("mode 06 guarded 57-byte marker-24 object-view tuple body", segment.Classification);
                Assert.Equal(expectedPayloadBytes, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            });
        Assert.True(samples[1].Complete);
        Assert.Equal(0xff28, samples[1].ViewId);
        Assert.Equal("state marker/object update candidate", samples[1].Classification);

        Protocol03NestedMovementConsumedBodyBoundary boundary = Assert.Single(samples[0].NestedMovementConsumedBodyBoundaries);
        Assert.Equal("test guarded mode 06 57-byte marker-24 object-view tuple body", boundary.ParserAction);
        Assert.Equal("03", boundary.Selector);
        Assert.Equal(17, boundary.Offset);
        Assert.Equal(expectedPayloadBytes, boundary.PayloadBytes);
        Assert.Equal(expectedPayloadBytes + 8, boundary.EvidencePayloadBytes);
        Assert.Equal(17, boundary.TailStartOffset);
        Assert.Equal(112, boundary.TailBoundaryOffset);
        Assert.Equal(57, boundary.PostTupleBodyBytes);
        Assert.Equal("unclassified object-view tuple", boundary.TupleLayoutKind);
        Assert.Equal(6, boundary.FirstNonZeroOffset);
        Assert.Equal("ff 00", boundary.FirstNonZeroFieldHex);
        Assert.Equal("28 ff 02 00 00 00 00 00", boundary.BoundaryHeaderHex);
        Assert.Equal("state marker/object update candidate", boundary.BoundaryHeaderClassification);

        PacketDumpFileSummary dump = new(
            "mode06-guarded-marker-24-object-view-57.txt",
            1,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            Array.Empty<Protocol04PacketSequenceSample>(),
            new[] { samples[0] },
            Array.Empty<Protocol04InteractionPayloadSample>(),
            Array.Empty<PlayerAttributePayloadSample>(),
            Array.Empty<ManageBonusPayloadSample>());

        Protocol03NestedMovementMode06PostContinuationTupleBodySummary tupleBody =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06PostContinuationTupleBodySummaries(new[] { dump }));
        Assert.Equal("ff marker plus zero-padded ff continuation lead", tupleBody.TailPrefixKind);
        Assert.Equal("01 ff", tupleBody.PrefixFirstNonZeroFieldHex);
        Assert.Equal("unclassified object-view tuple", tupleBody.TupleLayoutKind);
        Assert.Equal("unclassified object-view update", tupleBody.PostPrefixDisposition);
        Assert.Equal(57, tupleBody.PostTupleBodyBytes);
        Assert.Equal("ff 00 00 00 00 00 00 00 00 c7 1e 00 00 4c 0a 00", tupleBody.BodySuffixHex);
        Assert.Equal(6, tupleBody.FirstNonZeroOffset);
        Assert.Equal("ff 00", tupleBody.FirstNonZeroFieldHex);
        Assert.Equal(1, tupleBody.HeaderClassifications["state marker/object update candidate"]);
        Assert.Equal(1, tupleBody.HeaderPrefixes["28 ff 02 00 00 00 00 00"]);
        Assert.Equal(1, tupleBody.PostPrefixLeads["00 6c 07 00 00 00 00 24"]);

        Protocol03NestedMovementMode06TupleBodyParserActionSummary parserAction =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06TupleBodyParserActionSummaries(new[] { tupleBody }));
        Assert.Equal("test guarded mode 06 57-byte marker-24 object-view tuple body", parserAction.ParserAction);
        Assert.Equal(1, parserAction.BodyByteLengths["57"]);
    }

    [Fact]
    public void DetectProtocol03ObjectViews_BoundsGuardedMode0657ByteMarker33ObjectViewTupleBody()
    {
        byte[] body = Enumerable.Repeat((byte)0x00, 6)
            .Concat(new byte[] { 0xff, 0x00 })
            .Concat(Enumerable.Repeat((byte)0x00, 33))
            .Concat(PacketResearcher.ParseHexBytes("ff 00 00 00 00 00 00 00 00 22 b6 00 00 cd 08 00"))
            .ToArray();
        var bytes = new List<byte>
        {
            0x02, 0x03,
            0x01, 0x00, 0x02, 0x0c
        };
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 13));
        bytes.Add(0x03);
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 06 0e 01 18 58 3a f6 c6 00 00 be 42 44 35 af 46"));
        bytes.AddRange(PacketResearcher.ParseHexBytes("ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 22));
        bytes.AddRange(PacketResearcher.ParseHexBytes("01 ff"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 7));
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 5a 0a 00 00 00 00 33"));
        bytes.AddRange(body);
        bytes.AddRange(PacketResearcher.ParseHexBytes("28 ff 02 00 00 00 00 00"));

        Protocol03ObjectViewSample[] samples = PacketResearcher.DetectProtocol03ObjectViews(bytes, 543, "server").ToArray();

        const int expectedPayloadBytes = 17 + 55 + 57;
        Assert.Equal(2, samples.Length);
        Assert.True(samples[0].Complete);
        Assert.Equal(2, samples[0].ParsedUpdateCount);
        Assert.Equal(0, samples[0].UnparsedBytes);
        Assert.Collection(
            samples[0].Segments,
            segment =>
            {
                Assert.Equal("0c", segment.Selector);
                Assert.Equal("position xyz + flag", segment.Classification);
                Assert.Equal(13, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            },
            segment =>
            {
                Assert.Equal("03", segment.Selector);
                Assert.Equal("mode 06 guarded 57-byte marker-33 object-view tuple body", segment.Classification);
                Assert.Equal(expectedPayloadBytes, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            });
        Assert.True(samples[1].Complete);
        Assert.Equal(0xff28, samples[1].ViewId);
        Assert.Equal("state marker/object update candidate", samples[1].Classification);

        Protocol03NestedMovementConsumedBodyBoundary boundary = Assert.Single(samples[0].NestedMovementConsumedBodyBoundaries);
        Assert.Equal("test guarded mode 06 57-byte marker-33 object-view tuple body", boundary.ParserAction);
        Assert.Equal("03", boundary.Selector);
        Assert.Equal(17, boundary.Offset);
        Assert.Equal(expectedPayloadBytes, boundary.PayloadBytes);
        Assert.Equal(expectedPayloadBytes + 8, boundary.EvidencePayloadBytes);
        Assert.Equal(17, boundary.TailStartOffset);
        Assert.Equal(112, boundary.TailBoundaryOffset);
        Assert.Equal(57, boundary.PostTupleBodyBytes);
        Assert.Equal("unclassified object-view tuple", boundary.TupleLayoutKind);
        Assert.Equal(6, boundary.FirstNonZeroOffset);
        Assert.Equal("ff 00", boundary.FirstNonZeroFieldHex);
        Assert.Equal("28 ff 02 00 00 00 00 00", boundary.BoundaryHeaderHex);
        Assert.Equal("state marker/object update candidate", boundary.BoundaryHeaderClassification);

        PacketDumpFileSummary dump = new(
            "mode06-guarded-marker-33-object-view-57.txt",
            1,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            Array.Empty<Protocol04PacketSequenceSample>(),
            new[] { samples[0] },
            Array.Empty<Protocol04InteractionPayloadSample>(),
            Array.Empty<PlayerAttributePayloadSample>(),
            Array.Empty<ManageBonusPayloadSample>());

        Protocol03NestedMovementMode06PostContinuationTupleBodySummary tupleBody =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06PostContinuationTupleBodySummaries(new[] { dump }));
        Assert.Equal("ff marker plus zero-padded ff continuation lead", tupleBody.TailPrefixKind);
        Assert.Equal("01 ff", tupleBody.PrefixFirstNonZeroFieldHex);
        Assert.Equal("unclassified object-view tuple", tupleBody.TupleLayoutKind);
        Assert.Equal("unclassified object-view update", tupleBody.PostPrefixDisposition);
        Assert.Equal(57, tupleBody.PostTupleBodyBytes);
        Assert.Equal("ff 00 00 00 00 00 00 00 00 22 b6 00 00 cd 08 00", tupleBody.BodySuffixHex);
        Assert.Equal(6, tupleBody.FirstNonZeroOffset);
        Assert.Equal("ff 00", tupleBody.FirstNonZeroFieldHex);
        Assert.Equal(1, tupleBody.HeaderClassifications["state marker/object update candidate"]);
        Assert.Equal(1, tupleBody.HeaderPrefixes["28 ff 02 00 00 00 00 00"]);
        Assert.Equal(1, tupleBody.PostPrefixLeads["00 5a 0a 00 00 00 00 33"]);

        Protocol03NestedMovementMode06TupleBodyParserActionSummary parserAction =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06TupleBodyParserActionSummaries(new[] { tupleBody }));
        Assert.Equal("test guarded mode 06 57-byte marker-33 object-view tuple body", parserAction.ParserAction);
        Assert.Equal(1, parserAction.BodyByteLengths["57"]);
    }

    [Theory]
    [InlineData(
        "00 be 0a 00 00 00 00 35",
        "ff 00 00 00 00 00 00 00 00 00 00 00 00 03 08 00")]
    [InlineData(
        "00 eb 0f 00 00 00 00 35",
        "ff 00 00 00 00 00 00 00 00 1f ba 00 00 a1 07 00")]
    public void DetectProtocol03ObjectViews_BoundsGuardedMode0657ByteMarker35AltObjectViewTupleBody(
        string postPrefixLeadHex,
        string bodySuffixHex)
    {
        byte[] body = Enumerable.Repeat((byte)0x00, 6)
            .Concat(new byte[] { 0xff, 0x00 })
            .Concat(Enumerable.Repeat((byte)0x00, 33))
            .Concat(PacketResearcher.ParseHexBytes(bodySuffixHex))
            .ToArray();
        var bytes = new List<byte>
        {
            0x02, 0x03,
            0x01, 0x00, 0x02, 0x0c
        };
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 13));
        bytes.Add(0x03);
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 06 0e 01 18 58 3a f6 c6 00 00 be 42 44 35 af 46"));
        bytes.AddRange(PacketResearcher.ParseHexBytes("ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 22));
        bytes.AddRange(PacketResearcher.ParseHexBytes("01 ff"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 7));
        bytes.AddRange(PacketResearcher.ParseHexBytes(postPrefixLeadHex));
        bytes.AddRange(body);
        bytes.AddRange(PacketResearcher.ParseHexBytes("28 ff 02 00 00 00 00 00"));

        Protocol03ObjectViewSample[] samples = PacketResearcher.DetectProtocol03ObjectViews(bytes, 65355, "server").ToArray();

        const int expectedPayloadBytes = 17 + 55 + 57;
        Assert.Equal(2, samples.Length);
        Assert.True(samples[0].Complete);
        Assert.Equal(2, samples[0].ParsedUpdateCount);
        Assert.Equal(0, samples[0].UnparsedBytes);
        Assert.Collection(
            samples[0].Segments,
            segment =>
            {
                Assert.Equal("0c", segment.Selector);
                Assert.Equal("position xyz + flag", segment.Classification);
                Assert.Equal(13, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            },
            segment =>
            {
                Assert.Equal("03", segment.Selector);
                Assert.Equal("mode 06 guarded 57-byte marker-35-alt object-view tuple body", segment.Classification);
                Assert.Equal(expectedPayloadBytes, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            });
        Assert.True(samples[1].Complete);
        Assert.Equal(0xff28, samples[1].ViewId);
        Assert.Equal("state marker/object update candidate", samples[1].Classification);

        Protocol03NestedMovementConsumedBodyBoundary boundary = Assert.Single(samples[0].NestedMovementConsumedBodyBoundaries);
        Assert.Equal("test guarded mode 06 57-byte marker-35-alt object-view tuple body", boundary.ParserAction);
        Assert.Equal("03", boundary.Selector);
        Assert.Equal(17, boundary.Offset);
        Assert.Equal(expectedPayloadBytes, boundary.PayloadBytes);
        Assert.Equal(expectedPayloadBytes + 8, boundary.EvidencePayloadBytes);
        Assert.Equal(17, boundary.TailStartOffset);
        Assert.Equal(112, boundary.TailBoundaryOffset);
        Assert.Equal(57, boundary.PostTupleBodyBytes);
        Assert.Equal("unclassified object-view tuple", boundary.TupleLayoutKind);
        Assert.Equal(6, boundary.FirstNonZeroOffset);
        Assert.Equal("ff 00", boundary.FirstNonZeroFieldHex);
        Assert.Equal("28 ff 02 00 00 00 00 00", boundary.BoundaryHeaderHex);
        Assert.Equal("state marker/object update candidate", boundary.BoundaryHeaderClassification);

        PacketDumpFileSummary dump = new(
            "mode06-guarded-marker-35-alt-object-view-57.txt",
            1,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            Array.Empty<Protocol04PacketSequenceSample>(),
            new[] { samples[0] },
            Array.Empty<Protocol04InteractionPayloadSample>(),
            Array.Empty<PlayerAttributePayloadSample>(),
            Array.Empty<ManageBonusPayloadSample>());

        Protocol03NestedMovementMode06PostContinuationTupleBodySummary tupleBody =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06PostContinuationTupleBodySummaries(new[] { dump }));
        Assert.Equal("ff marker plus zero-padded ff continuation lead", tupleBody.TailPrefixKind);
        Assert.Equal("01 ff", tupleBody.PrefixFirstNonZeroFieldHex);
        Assert.Equal("unclassified object-view tuple", tupleBody.TupleLayoutKind);
        Assert.Equal("unclassified object-view update", tupleBody.PostPrefixDisposition);
        Assert.Equal(57, tupleBody.PostTupleBodyBytes);
        Assert.Equal(bodySuffixHex, tupleBody.BodySuffixHex);
        Assert.Equal(6, tupleBody.FirstNonZeroOffset);
        Assert.Equal("ff 00", tupleBody.FirstNonZeroFieldHex);
        Assert.Equal(1, tupleBody.HeaderClassifications["state marker/object update candidate"]);
        Assert.Equal(1, tupleBody.HeaderPrefixes["28 ff 02 00 00 00 00 00"]);
        Assert.Equal(1, tupleBody.PostPrefixLeads[postPrefixLeadHex]);

        Protocol03NestedMovementMode06TupleBodyParserActionSummary parserAction =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06TupleBodyParserActionSummaries(new[] { tupleBody }));
        Assert.Equal("test guarded mode 06 57-byte marker-35-alt object-view tuple body", parserAction.ParserAction);
        Assert.Equal(1, parserAction.BodyByteLengths["57"]);
        Assert.Equal(1, parserAction.MarkerBytes["35"]);
    }

    [Fact]
    public void DetectProtocol03ObjectViews_BoundsGuardedMode0657ByteMarker13ObjectViewTupleBody()
    {
        byte[] body = Enumerable.Repeat((byte)0x00, 6)
            .Concat(new byte[] { 0xff, 0x00 })
            .Concat(Enumerable.Repeat((byte)0x00, 33))
            .Concat(PacketResearcher.ParseHexBytes("ff 00 00 00 00 00 00 00 00 e5 1a 00 00 4c 0a 00"))
            .ToArray();
        var bytes = new List<byte>
        {
            0x02, 0x03,
            0x01, 0x00, 0x02, 0x0c
        };
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 13));
        bytes.Add(0x03);
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 06 0e 01 18 58 3a f6 c6 00 00 be 42 44 35 af 46"));
        bytes.AddRange(PacketResearcher.ParseHexBytes("ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 22));
        bytes.AddRange(PacketResearcher.ParseHexBytes("02 ff"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 7));
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 13 03 00 00 00 00 13"));
        bytes.AddRange(body);
        bytes.AddRange(PacketResearcher.ParseHexBytes("28 ff 02 00 00 00 00 00"));

        Protocol03ObjectViewSample[] samples = PacketResearcher.DetectProtocol03ObjectViews(bytes, 543, "server").ToArray();

        const int expectedPayloadBytes = 17 + 55 + 57;
        Assert.Equal(2, samples.Length);
        Assert.True(samples[0].Complete);
        Assert.Equal(2, samples[0].ParsedUpdateCount);
        Assert.Equal(0, samples[0].UnparsedBytes);
        Assert.Collection(
            samples[0].Segments,
            segment =>
            {
                Assert.Equal("0c", segment.Selector);
                Assert.Equal("position xyz + flag", segment.Classification);
                Assert.Equal(13, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            },
            segment =>
            {
                Assert.Equal("03", segment.Selector);
                Assert.Equal("mode 06 guarded 57-byte marker-13 object-view tuple body", segment.Classification);
                Assert.Equal(expectedPayloadBytes, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            });
        Assert.True(samples[1].Complete);
        Assert.Equal(0xff28, samples[1].ViewId);
        Assert.Equal("state marker/object update candidate", samples[1].Classification);

        Protocol03NestedMovementConsumedBodyBoundary boundary = Assert.Single(samples[0].NestedMovementConsumedBodyBoundaries);
        Assert.Equal("test guarded mode 06 57-byte marker-13 object-view tuple body", boundary.ParserAction);
        Assert.Equal("03", boundary.Selector);
        Assert.Equal(17, boundary.Offset);
        Assert.Equal(expectedPayloadBytes, boundary.PayloadBytes);
        Assert.Equal(expectedPayloadBytes + 8, boundary.EvidencePayloadBytes);
        Assert.Equal(17, boundary.TailStartOffset);
        Assert.Equal(112, boundary.TailBoundaryOffset);
        Assert.Equal(57, boundary.PostTupleBodyBytes);
        Assert.Equal("unclassified object-view tuple", boundary.TupleLayoutKind);
        Assert.Equal(6, boundary.FirstNonZeroOffset);
        Assert.Equal("ff 00", boundary.FirstNonZeroFieldHex);
        Assert.Equal("28 ff 02 00 00 00 00 00", boundary.BoundaryHeaderHex);
        Assert.Equal("state marker/object update candidate", boundary.BoundaryHeaderClassification);

        PacketDumpFileSummary dump = new(
            "mode06-guarded-marker-13-object-view-57.txt",
            1,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            Array.Empty<Protocol04PacketSequenceSample>(),
            new[] { samples[0] },
            Array.Empty<Protocol04InteractionPayloadSample>(),
            Array.Empty<PlayerAttributePayloadSample>(),
            Array.Empty<ManageBonusPayloadSample>());

        Protocol03NestedMovementMode06PostContinuationTupleBodySummary tupleBody =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06PostContinuationTupleBodySummaries(new[] { dump }));
        Assert.Equal("ff marker plus zero-padded ff continuation lead", tupleBody.TailPrefixKind);
        Assert.Equal("02 ff", tupleBody.PrefixFirstNonZeroFieldHex);
        Assert.Equal("unclassified object-view tuple", tupleBody.TupleLayoutKind);
        Assert.Equal("unclassified object-view update", tupleBody.PostPrefixDisposition);
        Assert.Equal(57, tupleBody.PostTupleBodyBytes);
        Assert.Equal("ff 00 00 00 00 00 00 00 00 e5 1a 00 00 4c 0a 00", tupleBody.BodySuffixHex);
        Assert.Equal(6, tupleBody.FirstNonZeroOffset);
        Assert.Equal("ff 00", tupleBody.FirstNonZeroFieldHex);
        Assert.Equal(1, tupleBody.HeaderClassifications["state marker/object update candidate"]);
        Assert.Equal(1, tupleBody.HeaderPrefixes["28 ff 02 00 00 00 00 00"]);
        Assert.Equal(1, tupleBody.PostPrefixLeads["00 13 03 00 00 00 00 13"]);

        Protocol03NestedMovementMode06TupleBodyParserActionSummary parserAction =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06TupleBodyParserActionSummaries(new[] { tupleBody }));
        Assert.Equal("test guarded mode 06 57-byte marker-13 object-view tuple body", parserAction.ParserAction);
        Assert.Equal(1, parserAction.BodyByteLengths["57"]);
    }

    [Fact]
    public void DetectProtocol03ObjectViews_BoundsGuardedMode0657ByteMarker1eObjectViewTupleBody()
    {
        byte[] body = Enumerable.Repeat((byte)0x00, 6)
            .Concat(new byte[] { 0xff, 0x00 })
            .Concat(Enumerable.Repeat((byte)0x00, 33))
            .Concat(PacketResearcher.ParseHexBytes("ff 00 00 00 00 00 00 00 00 d3 1e 00 00 1d 0a 00"))
            .ToArray();
        var bytes = new List<byte>
        {
            0x02, 0x03,
            0x01, 0x00, 0x02, 0x0c
        };
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 13));
        bytes.Add(0x03);
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 06 0e 01 18 58 3a f6 c6 00 00 be 42 44 35 af 46"));
        bytes.AddRange(PacketResearcher.ParseHexBytes("ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 22));
        bytes.AddRange(PacketResearcher.ParseHexBytes("03 ff"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 7));
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 20 03 00 00 00 00 1e"));
        bytes.AddRange(body);
        bytes.AddRange(PacketResearcher.ParseHexBytes("28 ff 01 00 00 00 00 00"));

        Protocol03ObjectViewSample[] samples = PacketResearcher.DetectProtocol03ObjectViews(bytes, 543, "server").ToArray();

        const int expectedPayloadBytes = 17 + 55 + 57;
        Assert.Equal(2, samples.Length);
        Assert.True(samples[0].Complete);
        Assert.Equal(2, samples[0].ParsedUpdateCount);
        Assert.Equal(0, samples[0].UnparsedBytes);
        Assert.Collection(
            samples[0].Segments,
            segment =>
            {
                Assert.Equal("0c", segment.Selector);
                Assert.Equal("position xyz + flag", segment.Classification);
                Assert.Equal(13, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            },
            segment =>
            {
                Assert.Equal("03", segment.Selector);
                Assert.Equal("mode 06 guarded 57-byte marker-1e object-view tuple body", segment.Classification);
                Assert.Equal(expectedPayloadBytes, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            });
        Assert.True(samples[1].Complete);
        Assert.Equal(0xff28, samples[1].ViewId);
        Assert.Equal("single state marker update", samples[1].Classification);

        Protocol03NestedMovementConsumedBodyBoundary boundary = Assert.Single(samples[0].NestedMovementConsumedBodyBoundaries);
        Assert.Equal("test guarded mode 06 57-byte marker-1e object-view tuple body", boundary.ParserAction);
        Assert.Equal("03", boundary.Selector);
        Assert.Equal(17, boundary.Offset);
        Assert.Equal(expectedPayloadBytes, boundary.PayloadBytes);
        Assert.Equal(expectedPayloadBytes + 8, boundary.EvidencePayloadBytes);
        Assert.Equal(17, boundary.TailStartOffset);
        Assert.Equal(112, boundary.TailBoundaryOffset);
        Assert.Equal(57, boundary.PostTupleBodyBytes);
        Assert.Equal("unclassified object-view tuple", boundary.TupleLayoutKind);
        Assert.Equal(6, boundary.FirstNonZeroOffset);
        Assert.Equal("ff 00", boundary.FirstNonZeroFieldHex);
        Assert.Equal("28 ff 01 00 00 00 00 00", boundary.BoundaryHeaderHex);
        Assert.Equal("single state marker update", boundary.BoundaryHeaderClassification);

        PacketDumpFileSummary dump = new(
            "mode06-guarded-marker-1e-object-view-57.txt",
            1,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            Array.Empty<Protocol04PacketSequenceSample>(),
            new[] { samples[0] },
            Array.Empty<Protocol04InteractionPayloadSample>(),
            Array.Empty<PlayerAttributePayloadSample>(),
            Array.Empty<ManageBonusPayloadSample>());

        Protocol03NestedMovementMode06PostContinuationTupleBodySummary tupleBody =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06PostContinuationTupleBodySummaries(new[] { dump }));
        Assert.Equal("ff marker plus zero-padded ff continuation lead", tupleBody.TailPrefixKind);
        Assert.Equal("03 ff", tupleBody.PrefixFirstNonZeroFieldHex);
        Assert.Equal("unclassified object-view tuple", tupleBody.TupleLayoutKind);
        Assert.Equal("unclassified object-view update", tupleBody.PostPrefixDisposition);
        Assert.Equal(57, tupleBody.PostTupleBodyBytes);
        Assert.Equal("ff 00 00 00 00 00 00 00 00 d3 1e 00 00 1d 0a 00", tupleBody.BodySuffixHex);
        Assert.Equal(6, tupleBody.FirstNonZeroOffset);
        Assert.Equal("ff 00", tupleBody.FirstNonZeroFieldHex);
        Assert.Equal(1, tupleBody.HeaderClassifications["single state marker update"]);
        Assert.Equal(1, tupleBody.HeaderPrefixes["28 ff 01 00 00 00 00 00"]);
        Assert.Equal(1, tupleBody.PostPrefixLeads["00 20 03 00 00 00 00 1e"]);

        Protocol03NestedMovementMode06TupleBodyParserActionSummary parserAction =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06TupleBodyParserActionSummaries(new[] { tupleBody }));
        Assert.Equal("test guarded mode 06 57-byte marker-1e object-view tuple body", parserAction.ParserAction);
        Assert.Equal(1, parserAction.BodyByteLengths["57"]);
    }

    [Fact]
    public void DetectProtocol03ObjectViews_BoundsGuardedMode0657ByteMarker1dObjectViewTupleBody()
    {
        byte[] body = Enumerable.Repeat((byte)0x00, 6)
            .Concat(new byte[] { 0xff, 0x00 })
            .Concat(Enumerable.Repeat((byte)0x00, 33))
            .Concat(PacketResearcher.ParseHexBytes("ff 00 00 00 00 00 00 00 00 c0 09 00 00 4c 0a 00"))
            .ToArray();
        var bytes = new List<byte>
        {
            0x02, 0x03,
            0x01, 0x00, 0x02, 0x0c
        };
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 13));
        bytes.Add(0x03);
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 06 0e 01 18 58 3a f6 c6 00 00 be 42 44 35 af 46"));
        bytes.AddRange(PacketResearcher.ParseHexBytes("ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 22));
        bytes.AddRange(PacketResearcher.ParseHexBytes("02 ff"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 7));
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 8a 04 00 00 00 00 1d"));
        bytes.AddRange(body);
        bytes.AddRange(PacketResearcher.ParseHexBytes("28 ff 01 00 00 00 00 00"));

        Protocol03ObjectViewSample[] samples = PacketResearcher.DetectProtocol03ObjectViews(bytes, 543, "server").ToArray();

        const int expectedPayloadBytes = 17 + 55 + 57;
        Assert.Equal(2, samples.Length);
        Assert.True(samples[0].Complete);
        Assert.Equal(2, samples[0].ParsedUpdateCount);
        Assert.Equal(0, samples[0].UnparsedBytes);
        Assert.Collection(
            samples[0].Segments,
            segment =>
            {
                Assert.Equal("0c", segment.Selector);
                Assert.Equal("position xyz + flag", segment.Classification);
                Assert.Equal(13, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            },
            segment =>
            {
                Assert.Equal("03", segment.Selector);
                Assert.Equal("mode 06 guarded 57-byte marker-1d object-view tuple body", segment.Classification);
                Assert.Equal(expectedPayloadBytes, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            });
        Assert.True(samples[1].Complete);
        Assert.Equal(0xff28, samples[1].ViewId);
        Assert.Equal("single state marker update", samples[1].Classification);

        Protocol03NestedMovementConsumedBodyBoundary boundary = Assert.Single(samples[0].NestedMovementConsumedBodyBoundaries);
        Assert.Equal("test guarded mode 06 57-byte marker-1d object-view tuple body", boundary.ParserAction);
        Assert.Equal("03", boundary.Selector);
        Assert.Equal(17, boundary.Offset);
        Assert.Equal(expectedPayloadBytes, boundary.PayloadBytes);
        Assert.Equal(expectedPayloadBytes + 8, boundary.EvidencePayloadBytes);
        Assert.Equal(17, boundary.TailStartOffset);
        Assert.Equal(112, boundary.TailBoundaryOffset);
        Assert.Equal(57, boundary.PostTupleBodyBytes);
        Assert.Equal("unclassified object-view tuple", boundary.TupleLayoutKind);
        Assert.Equal(6, boundary.FirstNonZeroOffset);
        Assert.Equal("ff 00", boundary.FirstNonZeroFieldHex);
        Assert.Equal("28 ff 01 00 00 00 00 00", boundary.BoundaryHeaderHex);
        Assert.Equal("single state marker update", boundary.BoundaryHeaderClassification);

        PacketDumpFileSummary dump = new(
            "mode06-guarded-marker-1d-object-view-57.txt",
            1,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            Array.Empty<Protocol04PacketSequenceSample>(),
            new[] { samples[0] },
            Array.Empty<Protocol04InteractionPayloadSample>(),
            Array.Empty<PlayerAttributePayloadSample>(),
            Array.Empty<ManageBonusPayloadSample>());

        Protocol03NestedMovementMode06PostContinuationTupleBodySummary tupleBody =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06PostContinuationTupleBodySummaries(new[] { dump }));
        Assert.Equal("ff marker plus zero-padded ff continuation lead", tupleBody.TailPrefixKind);
        Assert.Equal("02 ff", tupleBody.PrefixFirstNonZeroFieldHex);
        Assert.Equal("unclassified object-view tuple", tupleBody.TupleLayoutKind);
        Assert.Equal("unclassified object-view update", tupleBody.PostPrefixDisposition);
        Assert.Equal(57, tupleBody.PostTupleBodyBytes);
        Assert.Equal("ff 00 00 00 00 00 00 00 00 c0 09 00 00 4c 0a 00", tupleBody.BodySuffixHex);
        Assert.Equal(6, tupleBody.FirstNonZeroOffset);
        Assert.Equal("ff 00", tupleBody.FirstNonZeroFieldHex);
        Assert.Equal(1, tupleBody.HeaderClassifications["single state marker update"]);
        Assert.Equal(1, tupleBody.HeaderPrefixes["28 ff 01 00 00 00 00 00"]);
        Assert.Equal(1, tupleBody.PostPrefixLeads["00 8a 04 00 00 00 00 1d"]);

        Protocol03NestedMovementMode06TupleBodyParserActionSummary parserAction =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06TupleBodyParserActionSummaries(new[] { tupleBody }));
        Assert.Equal("test guarded mode 06 57-byte marker-1d object-view tuple body", parserAction.ParserAction);
        Assert.Equal(1, parserAction.BodyByteLengths["57"]);
    }

    [Fact]
    public void DetectProtocol03ObjectViews_BoundsGuardedMode0657ByteMarker1dObjectViewTupleBodyWithMarker19Lead()
    {
        byte[] body = Enumerable.Repeat((byte)0x00, 6)
            .Concat(new byte[] { 0xff, 0x00 })
            .Concat(Enumerable.Repeat((byte)0x00, 33))
            .Concat(PacketResearcher.ParseHexBytes("ff 00 00 00 00 00 00 00 00 c0 09 00 00 4c 0a 00"))
            .ToArray();
        var bytes = new List<byte>
        {
            0x02, 0x03,
            0x01, 0x00, 0x02, 0x0c
        };
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 13));
        bytes.Add(0x03);
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 06 0e 01 18 58 3a f6 c6 00 00 be 42 44 35 af 46"));
        bytes.AddRange(PacketResearcher.ParseHexBytes("ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 22));
        bytes.AddRange(PacketResearcher.ParseHexBytes("02 ff"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 7));
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 f4 03 00 00 00 00 19"));
        bytes.AddRange(body);
        bytes.AddRange(PacketResearcher.ParseHexBytes("28 ff 01 00 00 00 00 00"));

        Protocol03ObjectViewSample[] samples = PacketResearcher.DetectProtocol03ObjectViews(bytes, 562, "server").ToArray();

        const int expectedPayloadBytes = 17 + 55 + 57;
        Assert.Equal(2, samples.Length);
        Assert.True(samples[0].Complete);
        Assert.Equal(2, samples[0].ParsedUpdateCount);
        Assert.Equal(0, samples[0].UnparsedBytes);
        Assert.Collection(
            samples[0].Segments,
            segment =>
            {
                Assert.Equal("0c", segment.Selector);
                Assert.Equal("position xyz + flag", segment.Classification);
                Assert.Equal(13, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            },
            segment =>
            {
                Assert.Equal("03", segment.Selector);
                Assert.Equal("mode 06 guarded 57-byte marker-1d object-view tuple body", segment.Classification);
                Assert.Equal(expectedPayloadBytes, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            });

        Protocol03NestedMovementConsumedBodyBoundary boundary = Assert.Single(samples[0].NestedMovementConsumedBodyBoundaries);
        Assert.Equal("test guarded mode 06 57-byte marker-1d object-view tuple body", boundary.ParserAction);
        Assert.Equal("28 ff 01 00 00 00 00 00", boundary.BoundaryHeaderHex);
        Assert.Equal("single state marker update", boundary.BoundaryHeaderClassification);

        PacketDumpFileSummary dump = new(
            "mode06-guarded-marker-1d-marker-19-lead-object-view-57.txt",
            1,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            Array.Empty<Protocol04PacketSequenceSample>(),
            new[] { samples[0] },
            Array.Empty<Protocol04InteractionPayloadSample>(),
            Array.Empty<PlayerAttributePayloadSample>(),
            Array.Empty<ManageBonusPayloadSample>());

        Protocol03NestedMovementMode06PostContinuationTupleBodySummary tupleBody =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06PostContinuationTupleBodySummaries(new[] { dump }));
        Assert.Equal("02 ff", tupleBody.PrefixFirstNonZeroFieldHex);
        Assert.Equal(57, tupleBody.PostTupleBodyBytes);
        Assert.Equal("ff 00 00 00 00 00 00 00 00 c0 09 00 00 4c 0a 00", tupleBody.BodySuffixHex);
        Assert.Equal(1, tupleBody.PostPrefixLeads["00 f4 03 00 00 00 00 19"]);

        Protocol03NestedMovementMode06TupleBodyParserActionSummary parserAction =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06TupleBodyParserActionSummaries(new[] { tupleBody }));
        Assert.Equal("test guarded mode 06 57-byte marker-1d object-view tuple body", parserAction.ParserAction);
        Assert.Equal(1, parserAction.MarkerBytes["19"]);
    }

    [Fact]
    public void DetectProtocol03ObjectViews_BoundsGuardedMode0657ByteMarker1dAltObjectViewTupleBody()
    {
        byte[] body = Enumerable.Repeat((byte)0x00, 6)
            .Concat(new byte[] { 0xff, 0x00 })
            .Concat(Enumerable.Repeat((byte)0x00, 33))
            .Concat(PacketResearcher.ParseHexBytes("ff 00 00 00 00 00 00 00 00 d2 1e 00 00 1d 0a 00"))
            .ToArray();
        var bytes = new List<byte>
        {
            0x02, 0x03,
            0x01, 0x00, 0x02, 0x0c
        };
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 13));
        bytes.Add(0x03);
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 06 0e 01 18 58 3a f6 c6 00 00 be 42 44 35 af 46"));
        bytes.AddRange(PacketResearcher.ParseHexBytes("ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 22));
        bytes.AddRange(PacketResearcher.ParseHexBytes("03 ff"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 7));
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 07 03 00 00 00 00 1d"));
        bytes.AddRange(body);
        bytes.AddRange(PacketResearcher.ParseHexBytes("28 ff 01 00 00 00 00 00"));

        Protocol03ObjectViewSample[] samples = PacketResearcher.DetectProtocol03ObjectViews(bytes, 569, "server").ToArray();

        const int expectedPayloadBytes = 17 + 55 + 57;
        Assert.Equal(2, samples.Length);
        Assert.True(samples[0].Complete);
        Assert.Equal(2, samples[0].ParsedUpdateCount);
        Assert.Equal(0, samples[0].UnparsedBytes);
        Assert.Collection(
            samples[0].Segments,
            segment =>
            {
                Assert.Equal("0c", segment.Selector);
                Assert.Equal("position xyz + flag", segment.Classification);
                Assert.Equal(13, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            },
            segment =>
            {
                Assert.Equal("03", segment.Selector);
                Assert.Equal("mode 06 guarded 57-byte marker-1d alternate object-view tuple body", segment.Classification);
                Assert.Equal(expectedPayloadBytes, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            });
        Assert.True(samples[1].Complete);
        Assert.Equal(0xff28, samples[1].ViewId);
        Assert.Equal("single state marker update", samples[1].Classification);

        Protocol03NestedMovementConsumedBodyBoundary boundary = Assert.Single(samples[0].NestedMovementConsumedBodyBoundaries);
        Assert.Equal("test guarded mode 06 57-byte marker-1d-alt object-view tuple body", boundary.ParserAction);
        Assert.Equal("03", boundary.Selector);
        Assert.Equal(17, boundary.Offset);
        Assert.Equal(expectedPayloadBytes, boundary.PayloadBytes);
        Assert.Equal(expectedPayloadBytes + 8, boundary.EvidencePayloadBytes);
        Assert.Equal(17, boundary.TailStartOffset);
        Assert.Equal(112, boundary.TailBoundaryOffset);
        Assert.Equal(57, boundary.PostTupleBodyBytes);
        Assert.Equal("unclassified object-view tuple", boundary.TupleLayoutKind);
        Assert.Equal(6, boundary.FirstNonZeroOffset);
        Assert.Equal("ff 00", boundary.FirstNonZeroFieldHex);
        Assert.Equal("28 ff 01 00 00 00 00 00", boundary.BoundaryHeaderHex);
        Assert.Equal("single state marker update", boundary.BoundaryHeaderClassification);

        PacketDumpFileSummary dump = new(
            "mode06-guarded-marker-1d-alt-object-view-57.txt",
            1,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            Array.Empty<Protocol04PacketSequenceSample>(),
            new[] { samples[0] },
            Array.Empty<Protocol04InteractionPayloadSample>(),
            Array.Empty<PlayerAttributePayloadSample>(),
            Array.Empty<ManageBonusPayloadSample>());

        Protocol03NestedMovementMode06PostContinuationTupleBodySummary tupleBody =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06PostContinuationTupleBodySummaries(new[] { dump }));
        Assert.Equal("ff marker plus zero-padded ff continuation lead", tupleBody.TailPrefixKind);
        Assert.Equal("03 ff", tupleBody.PrefixFirstNonZeroFieldHex);
        Assert.Equal("unclassified object-view tuple", tupleBody.TupleLayoutKind);
        Assert.Equal("unclassified object-view update", tupleBody.PostPrefixDisposition);
        Assert.Equal(57, tupleBody.PostTupleBodyBytes);
        Assert.Equal("ff 00 00 00 00 00 00 00 00 d2 1e 00 00 1d 0a 00", tupleBody.BodySuffixHex);
        Assert.Equal(6, tupleBody.FirstNonZeroOffset);
        Assert.Equal("ff 00", tupleBody.FirstNonZeroFieldHex);
        Assert.Equal(1, tupleBody.HeaderClassifications["single state marker update"]);
        Assert.Equal(1, tupleBody.HeaderPrefixes["28 ff 01 00 00 00 00 00"]);
        Assert.Equal(1, tupleBody.PostPrefixLeads["00 07 03 00 00 00 00 1d"]);

        Protocol03NestedMovementMode06TupleBodyParserActionSummary parserAction =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06TupleBodyParserActionSummaries(new[] { tupleBody }));
        Assert.Equal("test guarded mode 06 57-byte marker-1d-alt object-view tuple body", parserAction.ParserAction);
        Assert.Equal(1, parserAction.BodyByteLengths["57"]);
        Assert.Equal(1, parserAction.MarkerBytes["1d"]);
    }

    [Theory]
    [InlineData("00 9e 07 00 00 00 00 25", "28 ff 01 00 00 00 00 00", "single state marker update")]
    [InlineData("00 6c 07 00 00 00 00 24", "28 ff 02 00 00 00 00 00", "state marker/object update candidate")]
    public void DetectProtocol03ObjectViews_BoundsGuardedMode0657ByteMarker25ObjectViewTupleBody(
        string postPrefixLeadHex,
        string boundaryHeaderHex,
        string boundaryHeaderClassification)
    {
        byte[] body = Enumerable.Repeat((byte)0x00, 6)
            .Concat(new byte[] { 0xff, 0x00 })
            .Concat(Enumerable.Repeat((byte)0x00, 33))
            .Concat(PacketResearcher.ParseHexBytes("ff 00 00 00 00 00 00 00 00 d9 1e 00 00 4c 0a 00"))
            .ToArray();
        var bytes = new List<byte>
        {
            0x02, 0x03,
            0x01, 0x00, 0x02, 0x0c
        };
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 13));
        bytes.Add(0x03);
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 06 0e 01 18 58 3a f6 c6 00 00 be 42 44 35 af 46"));
        bytes.AddRange(PacketResearcher.ParseHexBytes("ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 22));
        bytes.AddRange(PacketResearcher.ParseHexBytes("01 ff"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 7));
        bytes.AddRange(PacketResearcher.ParseHexBytes(postPrefixLeadHex));
        bytes.AddRange(body);
        bytes.AddRange(PacketResearcher.ParseHexBytes(boundaryHeaderHex));

        Protocol03ObjectViewSample[] samples = PacketResearcher.DetectProtocol03ObjectViews(bytes, 543, "server").ToArray();

        const int expectedPayloadBytes = 17 + 55 + 57;
        Assert.Equal(2, samples.Length);
        Assert.True(samples[0].Complete);
        Assert.Equal(2, samples[0].ParsedUpdateCount);
        Assert.Equal(0, samples[0].UnparsedBytes);
        Assert.Collection(
            samples[0].Segments,
            segment =>
            {
                Assert.Equal("0c", segment.Selector);
                Assert.Equal("position xyz + flag", segment.Classification);
                Assert.Equal(13, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            },
            segment =>
            {
                Assert.Equal("03", segment.Selector);
                Assert.Equal("mode 06 guarded 57-byte marker-25 object-view tuple body", segment.Classification);
                Assert.Equal(expectedPayloadBytes, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            });
        Assert.True(samples[1].Complete);
        Assert.Equal(0xff28, samples[1].ViewId);
        Assert.Equal(boundaryHeaderClassification, samples[1].Classification);

        Protocol03NestedMovementConsumedBodyBoundary boundary = Assert.Single(samples[0].NestedMovementConsumedBodyBoundaries);
        Assert.Equal("test guarded mode 06 57-byte marker-25 object-view tuple body", boundary.ParserAction);
        Assert.Equal("03", boundary.Selector);
        Assert.Equal(17, boundary.Offset);
        Assert.Equal(expectedPayloadBytes, boundary.PayloadBytes);
        Assert.Equal(expectedPayloadBytes + 8, boundary.EvidencePayloadBytes);
        Assert.Equal(17, boundary.TailStartOffset);
        Assert.Equal(112, boundary.TailBoundaryOffset);
        Assert.Equal(57, boundary.PostTupleBodyBytes);
        Assert.Equal("unclassified object-view tuple", boundary.TupleLayoutKind);
        Assert.Equal(6, boundary.FirstNonZeroOffset);
        Assert.Equal("ff 00", boundary.FirstNonZeroFieldHex);
        Assert.Equal(boundaryHeaderHex, boundary.BoundaryHeaderHex);
        Assert.Equal(boundaryHeaderClassification, boundary.BoundaryHeaderClassification);

        PacketDumpFileSummary dump = new(
            "mode06-guarded-marker-25-object-view-57.txt",
            1,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            Array.Empty<Protocol04PacketSequenceSample>(),
            new[] { samples[0] },
            Array.Empty<Protocol04InteractionPayloadSample>(),
            Array.Empty<PlayerAttributePayloadSample>(),
            Array.Empty<ManageBonusPayloadSample>());

        Protocol03NestedMovementMode06PostContinuationTupleBodySummary tupleBody =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06PostContinuationTupleBodySummaries(new[] { dump }));
        Assert.Equal("ff marker plus zero-padded ff continuation lead", tupleBody.TailPrefixKind);
        Assert.Equal("01 ff", tupleBody.PrefixFirstNonZeroFieldHex);
        Assert.Equal("unclassified object-view tuple", tupleBody.TupleLayoutKind);
        Assert.Equal("unclassified object-view update", tupleBody.PostPrefixDisposition);
        Assert.Equal(57, tupleBody.PostTupleBodyBytes);
        Assert.Equal("ff 00 00 00 00 00 00 00 00 d9 1e 00 00 4c 0a 00", tupleBody.BodySuffixHex);
        Assert.Equal(6, tupleBody.FirstNonZeroOffset);
        Assert.Equal("ff 00", tupleBody.FirstNonZeroFieldHex);
        Assert.Equal(1, tupleBody.HeaderClassifications[boundaryHeaderClassification]);
        Assert.Equal(1, tupleBody.HeaderPrefixes[boundaryHeaderHex]);
        Assert.Equal(1, tupleBody.PostPrefixLeads[postPrefixLeadHex]);

        Protocol03NestedMovementMode06TupleBodyParserActionSummary parserAction =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06TupleBodyParserActionSummaries(new[] { tupleBody }));
        Assert.Equal("test guarded mode 06 57-byte marker-25 object-view tuple body", parserAction.ParserAction);
        Assert.Equal(1, parserAction.BodyByteLengths["57"]);
    }

    [Theory]
    [InlineData("ff 00 00 00 00 00 00 00 00 6a ba 00 00 4c 0a 00", "28 ff 01 00 00 00 00 00", "single state marker update")]
    [InlineData("ff 00 00 00 00 00 00 00 00 57 ba 00 00 cd 08 00", "28 ff 01 00 00 00 00 00", "single state marker update")]
    [InlineData("ff 00 00 00 00 00 00 00 00 6a ba 00 00 cd 08 00", "28 ff 01 00 00 00 00 00", "single state marker update")]
    [InlineData("ff 00 00 00 00 00 00 00 00 6a ba 00 00 eb 09 00", "28 ff 02 00 00 00 00 00", "state marker/object update candidate")]
    public void DetectProtocol03ObjectViews_BoundsGuardedMode0657ByteMarker4fObjectViewTupleBody(
        string bodySuffixHex,
        string boundaryHeaderHex,
        string boundaryHeaderClassification)
    {
        byte[] body = Enumerable.Repeat((byte)0x00, 6)
            .Concat(new byte[] { 0xff, 0x00 })
            .Concat(Enumerable.Repeat((byte)0x00, 33))
            .Concat(PacketResearcher.ParseHexBytes(bodySuffixHex))
            .ToArray();
        var bytes = new List<byte>
        {
            0x02, 0x03,
            0x01, 0x00, 0x02, 0x0c
        };
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 13));
        bytes.Add(0x03);
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 06 0e 01 18 58 3a f6 c6 00 00 be 42 44 35 af 46"));
        bytes.AddRange(PacketResearcher.ParseHexBytes("ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 22));
        bytes.AddRange(PacketResearcher.ParseHexBytes("01 ff"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 7));
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 d2 0f 00 00 00 00 4f"));
        bytes.AddRange(body);
        bytes.AddRange(PacketResearcher.ParseHexBytes(boundaryHeaderHex));

        Protocol03ObjectViewSample[] samples = PacketResearcher.DetectProtocol03ObjectViews(bytes, 565, "server").ToArray();

        const int expectedPayloadBytes = 17 + 55 + 57;
        Assert.Equal(2, samples.Length);
        Assert.True(samples[0].Complete);
        Assert.Equal(2, samples[0].ParsedUpdateCount);
        Assert.Equal(0, samples[0].UnparsedBytes);
        Assert.Collection(
            samples[0].Segments,
            segment =>
            {
                Assert.Equal("0c", segment.Selector);
                Assert.Equal("position xyz + flag", segment.Classification);
                Assert.Equal(13, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            },
            segment =>
            {
                Assert.Equal("03", segment.Selector);
                Assert.Equal("mode 06 guarded 57-byte marker-4f object-view tuple body", segment.Classification);
                Assert.Equal(expectedPayloadBytes, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            });
        Assert.True(samples[1].Complete);
        Assert.Equal(0xff28, samples[1].ViewId);
        Assert.Equal(boundaryHeaderClassification, samples[1].Classification);

        Protocol03NestedMovementConsumedBodyBoundary boundary = Assert.Single(samples[0].NestedMovementConsumedBodyBoundaries);
        Assert.Equal("test guarded mode 06 57-byte marker-4f object-view tuple body", boundary.ParserAction);
        Assert.Equal("03", boundary.Selector);
        Assert.Equal(17, boundary.Offset);
        Assert.Equal(expectedPayloadBytes, boundary.PayloadBytes);
        Assert.Equal(expectedPayloadBytes + 8, boundary.EvidencePayloadBytes);
        Assert.Equal(17, boundary.TailStartOffset);
        Assert.Equal(112, boundary.TailBoundaryOffset);
        Assert.Equal(57, boundary.PostTupleBodyBytes);
        Assert.Equal("unclassified object-view tuple", boundary.TupleLayoutKind);
        Assert.Equal(6, boundary.FirstNonZeroOffset);
        Assert.Equal("ff 00", boundary.FirstNonZeroFieldHex);
        Assert.Equal(boundaryHeaderHex, boundary.BoundaryHeaderHex);
        Assert.Equal(boundaryHeaderClassification, boundary.BoundaryHeaderClassification);

        PacketDumpFileSummary dump = new(
            "mode06-guarded-marker-4f-object-view-57.txt",
            1,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            Array.Empty<Protocol04PacketSequenceSample>(),
            new[] { samples[0] },
            Array.Empty<Protocol04InteractionPayloadSample>(),
            Array.Empty<PlayerAttributePayloadSample>(),
            Array.Empty<ManageBonusPayloadSample>());

        Protocol03NestedMovementMode06PostContinuationTupleBodySummary tupleBody =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06PostContinuationTupleBodySummaries(new[] { dump }));
        Assert.Equal("ff marker plus zero-padded ff continuation lead", tupleBody.TailPrefixKind);
        Assert.Equal("01 ff", tupleBody.PrefixFirstNonZeroFieldHex);
        Assert.Equal("unclassified object-view tuple", tupleBody.TupleLayoutKind);
        Assert.Equal("unclassified object-view update", tupleBody.PostPrefixDisposition);
        Assert.Equal(57, tupleBody.PostTupleBodyBytes);
        Assert.Equal(bodySuffixHex, tupleBody.BodySuffixHex);
        Assert.Equal(6, tupleBody.FirstNonZeroOffset);
        Assert.Equal("ff 00", tupleBody.FirstNonZeroFieldHex);
        Assert.Equal(1, tupleBody.HeaderClassifications[boundaryHeaderClassification]);
        Assert.Equal(1, tupleBody.HeaderPrefixes[boundaryHeaderHex]);
        Assert.Equal(1, tupleBody.PostPrefixLeads["00 d2 0f 00 00 00 00 4f"]);

        Protocol03NestedMovementMode06TupleBodyParserActionSummary parserAction =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06TupleBodyParserActionSummaries(new[] { tupleBody }));
        Assert.Equal("test guarded mode 06 57-byte marker-4f object-view tuple body", parserAction.ParserAction);
        Assert.Equal(1, parserAction.BodyByteLengths["57"]);
        Assert.Equal(1, parserAction.MarkerBytes["4f"]);
    }

    [Fact]
    public void DetectProtocol03ObjectViews_BoundsGuardedMode0657ByteMarker64ObjectViewTupleBody()
    {
        byte[] body = Enumerable.Repeat((byte)0x00, 6)
            .Concat(new byte[] { 0xff, 0x00 })
            .Concat(Enumerable.Repeat((byte)0x00, 33))
            .Concat(PacketResearcher.ParseHexBytes("ff 00 00 00 00 00 00 00 00 5f b9 00 00 4c 0a 00"))
            .ToArray();
        var bytes = new List<byte>
        {
            0x02, 0x03,
            0x01, 0x00, 0x02, 0x0c
        };
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 13));
        bytes.Add(0x03);
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 06 0e 01 18 58 3a f6 c6 00 00 be 42 44 35 af 46"));
        bytes.AddRange(PacketResearcher.ParseHexBytes("ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 22));
        bytes.AddRange(PacketResearcher.ParseHexBytes("01 01"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 7));
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 ec 13 00 00 00 00 64"));
        bytes.AddRange(body);
        bytes.AddRange(PacketResearcher.ParseHexBytes("28 ff 02 00 00 00 00 00"));

        Protocol03ObjectViewSample[] samples = PacketResearcher.DetectProtocol03ObjectViews(bytes, 565, "server").ToArray();

        const int expectedPayloadBytes = 17 + 55 + 57;
        Assert.Equal(2, samples.Length);
        Assert.True(samples[0].Complete);
        Assert.Equal(2, samples[0].ParsedUpdateCount);
        Assert.Equal(0, samples[0].UnparsedBytes);
        Assert.Collection(
            samples[0].Segments,
            segment =>
            {
                Assert.Equal("0c", segment.Selector);
                Assert.Equal("position xyz + flag", segment.Classification);
                Assert.Equal(13, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            },
            segment =>
            {
                Assert.Equal("03", segment.Selector);
                Assert.Equal("mode 06 guarded 57-byte marker-64 object-view tuple body", segment.Classification);
                Assert.Equal(expectedPayloadBytes, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            });
        Assert.True(samples[1].Complete);
        Assert.Equal(0xff28, samples[1].ViewId);
        Assert.Equal("state marker/object update candidate", samples[1].Classification);

        Protocol03NestedMovementConsumedBodyBoundary boundary = Assert.Single(samples[0].NestedMovementConsumedBodyBoundaries);
        Assert.Equal("test guarded mode 06 57-byte marker-64 object-view tuple body", boundary.ParserAction);
        Assert.Equal("03", boundary.Selector);
        Assert.Equal(17, boundary.Offset);
        Assert.Equal(expectedPayloadBytes, boundary.PayloadBytes);
        Assert.Equal(expectedPayloadBytes + 8, boundary.EvidencePayloadBytes);
        Assert.Equal(17, boundary.TailStartOffset);
        Assert.Equal(112, boundary.TailBoundaryOffset);
        Assert.Equal(57, boundary.PostTupleBodyBytes);
        Assert.Equal("unclassified object-view tuple", boundary.TupleLayoutKind);
        Assert.Equal(6, boundary.FirstNonZeroOffset);
        Assert.Equal("ff 00", boundary.FirstNonZeroFieldHex);
        Assert.Equal("28 ff 02 00 00 00 00 00", boundary.BoundaryHeaderHex);
        Assert.Equal("state marker/object update candidate", boundary.BoundaryHeaderClassification);

        PacketDumpFileSummary dump = new(
            "mode06-guarded-marker-64-object-view-57.txt",
            1,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            Array.Empty<Protocol04PacketSequenceSample>(),
            new[] { samples[0] },
            Array.Empty<Protocol04InteractionPayloadSample>(),
            Array.Empty<PlayerAttributePayloadSample>(),
            Array.Empty<ManageBonusPayloadSample>());

        Protocol03NestedMovementMode06PostContinuationTupleBodySummary tupleBody =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06PostContinuationTupleBodySummaries(new[] { dump }));
        Assert.Equal("ff marker plus zero-padded ff continuation lead", tupleBody.TailPrefixKind);
        Assert.Equal("01 01", tupleBody.PrefixFirstNonZeroFieldHex);
        Assert.Equal("unclassified object-view tuple", tupleBody.TupleLayoutKind);
        Assert.Equal("unclassified object-view update", tupleBody.PostPrefixDisposition);
        Assert.Equal(57, tupleBody.PostTupleBodyBytes);
        Assert.Equal("ff 00 00 00 00 00 00 00 00 5f b9 00 00 4c 0a 00", tupleBody.BodySuffixHex);
        Assert.Equal(6, tupleBody.FirstNonZeroOffset);
        Assert.Equal("ff 00", tupleBody.FirstNonZeroFieldHex);
        Assert.Equal(1, tupleBody.HeaderClassifications["state marker/object update candidate"]);
        Assert.Equal(1, tupleBody.HeaderPrefixes["28 ff 02 00 00 00 00 00"]);
        Assert.Equal(1, tupleBody.PostPrefixLeads["00 ec 13 00 00 00 00 64"]);

        Protocol03NestedMovementMode06TupleBodyParserActionSummary parserAction =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06TupleBodyParserActionSummaries(new[] { tupleBody }));
        Assert.Equal("test guarded mode 06 57-byte marker-64 object-view tuple body", parserAction.ParserAction);
        Assert.Equal(1, parserAction.BodyByteLengths["57"]);
        Assert.Equal(1, parserAction.MarkerBytes["64"]);
    }

    [Theory]
    [InlineData(
        "02 ff",
        "00 08 07 00 00 00 00 2e",
        "ff 00 00 00 00 00 00 00 00 63 9a 00 00 4c 0a 00",
        "28 ff 01 00 00 00 00 00",
        "single state marker update",
        "marker-2e",
        "2e")]
    [InlineData(
        "03 ff",
        "00 b0 04 00 00 00 00 2e",
        "ff 00 00 00 00 00 00 00 00 63 9a 00 00 4c 0a 00",
        "28 ff 01 00 00 00 00 00",
        "single state marker update",
        "marker-2e-alt",
        "2e")]
    [InlineData(
        "01 ff",
        "00 60 09 00 00 00 00 2e",
        "ff 00 00 00 00 00 00 00 00 63 9a 00 00 4c 0a 00",
        "28 ff 01 00 00 00 00 00",
        "single state marker update",
        "marker-2e-60",
        "2e")]
    [InlineData(
        "03 ff",
        "00 c9 04 00 00 00 00 2f",
        "ff 00 00 00 00 00 00 00 00 c4 1e 00 00 4c 0a 00",
        "28 ff 02 00 00 00 00 00",
        "state marker/object update candidate",
        "marker-2f",
        "2f")]
    public void DetectProtocol03ObjectViews_BoundsGuardedMode0657ByteMarker2e2fAnd2eAltObjectViewTupleBody(
        string prefixHex,
        string postPrefixLeadHex,
        string bodySuffixHex,
        string boundaryHeaderHex,
        string boundaryHeaderClassification,
        string markerName,
        string markerByte)
    {
        byte[] body = Enumerable.Repeat((byte)0x00, 6)
            .Concat(new byte[] { 0xff, 0x00 })
            .Concat(Enumerable.Repeat((byte)0x00, 33))
            .Concat(PacketResearcher.ParseHexBytes(bodySuffixHex))
            .ToArray();
        var bytes = new List<byte>
        {
            0x02, 0x03,
            0x01, 0x00, 0x02, 0x0c
        };
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 13));
        bytes.Add(0x03);
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 06 0e 01 18 58 3a f6 c6 00 00 be 42 44 35 af 46"));
        bytes.AddRange(PacketResearcher.ParseHexBytes("ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 22));
        bytes.AddRange(PacketResearcher.ParseHexBytes(prefixHex));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 7));
        bytes.AddRange(PacketResearcher.ParseHexBytes(postPrefixLeadHex));
        bytes.AddRange(body);
        bytes.AddRange(PacketResearcher.ParseHexBytes(boundaryHeaderHex));

        Protocol03ObjectViewSample[] samples = PacketResearcher.DetectProtocol03ObjectViews(bytes, 566, "server").ToArray();

        string expectedParserAction = $"test guarded mode 06 57-byte {markerName} object-view tuple body";
        string expectedClassification = $"mode 06 guarded 57-byte {markerName} object-view tuple body";
        const int expectedPayloadBytes = 17 + 55 + 57;
        Assert.Equal(2, samples.Length);
        Assert.True(samples[0].Complete);
        Assert.Equal(2, samples[0].ParsedUpdateCount);
        Assert.Equal(0, samples[0].UnparsedBytes);
        Assert.Collection(
            samples[0].Segments,
            segment =>
            {
                Assert.Equal("0c", segment.Selector);
                Assert.Equal("position xyz + flag", segment.Classification);
                Assert.Equal(13, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            },
            segment =>
            {
                Assert.Equal("03", segment.Selector);
                Assert.Equal(expectedClassification, segment.Classification);
                Assert.Equal(expectedPayloadBytes, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            });
        Assert.True(samples[1].Complete);
        Assert.Equal(0xff28, samples[1].ViewId);
        Assert.Equal(boundaryHeaderClassification, samples[1].Classification);

        Protocol03NestedMovementConsumedBodyBoundary boundary = Assert.Single(samples[0].NestedMovementConsumedBodyBoundaries);
        Assert.Equal(expectedParserAction, boundary.ParserAction);
        Assert.Equal("03", boundary.Selector);
        Assert.Equal(17, boundary.Offset);
        Assert.Equal(expectedPayloadBytes, boundary.PayloadBytes);
        Assert.Equal(expectedPayloadBytes + 8, boundary.EvidencePayloadBytes);
        Assert.Equal(17, boundary.TailStartOffset);
        Assert.Equal(112, boundary.TailBoundaryOffset);
        Assert.Equal(57, boundary.PostTupleBodyBytes);
        Assert.Equal("unclassified object-view tuple", boundary.TupleLayoutKind);
        Assert.Equal(6, boundary.FirstNonZeroOffset);
        Assert.Equal("ff 00", boundary.FirstNonZeroFieldHex);
        Assert.Equal(boundaryHeaderHex, boundary.BoundaryHeaderHex);
        Assert.Equal(boundaryHeaderClassification, boundary.BoundaryHeaderClassification);

        PacketDumpFileSummary dump = new(
            $"mode06-guarded-{markerName}-object-view-57.txt",
            1,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            Array.Empty<Protocol04PacketSequenceSample>(),
            new[] { samples[0] },
            Array.Empty<Protocol04InteractionPayloadSample>(),
            Array.Empty<PlayerAttributePayloadSample>(),
            Array.Empty<ManageBonusPayloadSample>());

        Protocol03NestedMovementMode06PostContinuationTupleBodySummary tupleBody =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06PostContinuationTupleBodySummaries(new[] { dump }));
        Assert.Equal("ff marker plus zero-padded ff continuation lead", tupleBody.TailPrefixKind);
        Assert.Equal(prefixHex, tupleBody.PrefixFirstNonZeroFieldHex);
        Assert.Equal("unclassified object-view tuple", tupleBody.TupleLayoutKind);
        Assert.Equal("unclassified object-view update", tupleBody.PostPrefixDisposition);
        Assert.Equal(57, tupleBody.PostTupleBodyBytes);
        Assert.Equal(bodySuffixHex, tupleBody.BodySuffixHex);
        Assert.Equal(6, tupleBody.FirstNonZeroOffset);
        Assert.Equal("ff 00", tupleBody.FirstNonZeroFieldHex);
        Assert.Equal(1, tupleBody.HeaderClassifications[boundaryHeaderClassification]);
        Assert.Equal(1, tupleBody.HeaderPrefixes[boundaryHeaderHex]);
        Assert.Equal(1, tupleBody.PostPrefixLeads[postPrefixLeadHex]);

        Protocol03NestedMovementMode06TupleBodyParserActionSummary parserAction =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06TupleBodyParserActionSummaries(new[] { tupleBody }));
        Assert.Equal(expectedParserAction, parserAction.ParserAction);
        Assert.Equal(1, parserAction.BodyByteLengths["57"]);
        Assert.Equal(1, parserAction.MarkerBytes[markerByte]);
    }

    [Theory]
    [InlineData(
        "01 ff",
        "00 60 09 00 00 00 00 2e",
        "ff 00 00 00 00 00 00 00 00 63 9a 00 00 11 0a 00",
        "28 ff 02 00 00 00 00 00",
        "state marker/object update candidate",
        "2e")]
    [InlineData(
        "01 ff",
        "00 40 06 00 00 00 00 1e",
        "ff 00 00 00 00 00 00 00 00 c1 09 00 00 4c 0a 00",
        "28 ff 01 00 00 00 00 00",
        "single state marker update",
        "1e")]
    [InlineData(
        "01 ff",
        "00 e8 03 00 00 00 00 12",
        "ff 00 00 00 00 00 00 00 00 ef 1a 00 00 4c 0a 00",
        "28 ff 02 00 00 00 00 00",
        "state marker/object update candidate",
        "12")]
    [InlineData(
        "01 ff",
        "00 f0 0a 00 00 00 00 36",
        "ff 00 00 00 00 00 00 00 00 fb b9 00 00 a1 07 00",
        "28 ff 01 00 00 00 00 00",
        "single state marker update",
        "36")]
    [InlineData(
        "03 ff",
        "00 07 03 00 00 00 00 1d",
        "ff 00 00 00 00 00 00 00 00 ca 1e 00 00 4c 0a 00",
        "28 ff 01 00 00 00 00 00",
        "single state marker update",
        "1d")]
    [InlineData(
        "03 ff",
        "00 20 03 00 00 00 00 1e",
        "ff 00 00 00 00 00 00 00 ff 00 00 00 00 b9 07 00",
        "28 ff 02 00 00 00 00 00",
        "state marker/object update candidate",
        "1e")]
    [InlineData(
        "03 ff",
        "00 07 03 00 00 00 00 1d",
        "ff 00 00 00 00 00 00 00 ff d2 1e 00 00 1d 0a 00",
        "28 ff 01 00 00 00 00 00",
        "single state marker update",
        "1d")]
    public void DetectProtocol03ObjectViews_BoundsGuardedMode0657ByteResidualExactObjectViewTupleBody(
        string prefixHex,
        string postPrefixLeadHex,
        string bodySuffixHex,
        string boundaryHeaderHex,
        string boundaryHeaderClassification,
        string markerByte)
    {
        byte[] body = Enumerable.Repeat((byte)0x00, 6)
            .Concat(new byte[] { 0xff, 0x00 })
            .Concat(Enumerable.Repeat((byte)0x00, 33))
            .Concat(PacketResearcher.ParseHexBytes(bodySuffixHex))
            .ToArray();
        var bytes = new List<byte>
        {
            0x02, 0x03,
            0x01, 0x00, 0x02, 0x0c
        };
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 13));
        bytes.Add(0x03);
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 06 0e 01 18 58 3a f6 c6 00 00 be 42 44 35 af 46"));
        bytes.AddRange(PacketResearcher.ParseHexBytes("ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 22));
        bytes.AddRange(PacketResearcher.ParseHexBytes(prefixHex));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 7));
        bytes.AddRange(PacketResearcher.ParseHexBytes(postPrefixLeadHex));
        bytes.AddRange(body);
        bytes.AddRange(PacketResearcher.ParseHexBytes(boundaryHeaderHex));

        Protocol03ObjectViewSample[] samples = PacketResearcher.DetectProtocol03ObjectViews(bytes, 84097, "server").ToArray();

        const string expectedParserAction = "test guarded mode 06 57-byte residual exact object-view tuple body";
        const int expectedPayloadBytes = 17 + 55 + 57;
        Assert.Equal(2, samples.Length);
        Assert.True(samples[0].Complete);
        Assert.Equal(2, samples[0].ParsedUpdateCount);
        Assert.Equal(0, samples[0].UnparsedBytes);
        Assert.Collection(
            samples[0].Segments,
            segment =>
            {
                Assert.Equal("0c", segment.Selector);
                Assert.Equal("position xyz + flag", segment.Classification);
                Assert.Equal(13, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            },
            segment =>
            {
                Assert.Equal("03", segment.Selector);
                Assert.Equal("mode 06 guarded 57-byte residual exact object-view tuple body", segment.Classification);
                Assert.Equal(expectedPayloadBytes, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            });
        Assert.True(samples[1].Complete);
        Assert.Equal(0xff28, samples[1].ViewId);
        Assert.Equal(boundaryHeaderClassification, samples[1].Classification);

        Protocol03NestedMovementConsumedBodyBoundary boundary = Assert.Single(samples[0].NestedMovementConsumedBodyBoundaries);
        Assert.Equal(expectedParserAction, boundary.ParserAction);
        Assert.Equal("03", boundary.Selector);
        Assert.Equal(17, boundary.Offset);
        Assert.Equal(expectedPayloadBytes, boundary.PayloadBytes);
        Assert.Equal(expectedPayloadBytes + 8, boundary.EvidencePayloadBytes);
        Assert.Equal(17, boundary.TailStartOffset);
        Assert.Equal(112, boundary.TailBoundaryOffset);
        Assert.Equal(57, boundary.PostTupleBodyBytes);
        Assert.Equal("unclassified object-view tuple", boundary.TupleLayoutKind);
        Assert.Equal(6, boundary.FirstNonZeroOffset);
        Assert.Equal("ff 00", boundary.FirstNonZeroFieldHex);
        Assert.Equal(boundaryHeaderHex, boundary.BoundaryHeaderHex);
        Assert.Equal(boundaryHeaderClassification, boundary.BoundaryHeaderClassification);

        PacketDumpFileSummary dump = new(
            "mode06-guarded-residual-exact-object-view-57.txt",
            1,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            Array.Empty<Protocol04PacketSequenceSample>(),
            new[] { samples[0] },
            Array.Empty<Protocol04InteractionPayloadSample>(),
            Array.Empty<PlayerAttributePayloadSample>(),
            Array.Empty<ManageBonusPayloadSample>());

        Protocol03NestedMovementMode06PostContinuationTupleBodySummary tupleBody =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06PostContinuationTupleBodySummaries(new[] { dump }));
        Assert.Equal("ff marker plus zero-padded ff continuation lead", tupleBody.TailPrefixKind);
        Assert.Equal(prefixHex, tupleBody.PrefixFirstNonZeroFieldHex);
        Assert.Equal("unclassified object-view tuple", tupleBody.TupleLayoutKind);
        Assert.Equal("unclassified object-view update", tupleBody.PostPrefixDisposition);
        Assert.Equal(57, tupleBody.PostTupleBodyBytes);
        Assert.Equal(bodySuffixHex, tupleBody.BodySuffixHex);
        Assert.Equal(6, tupleBody.FirstNonZeroOffset);
        Assert.Equal("ff 00", tupleBody.FirstNonZeroFieldHex);
        Assert.Equal(1, tupleBody.HeaderClassifications[boundaryHeaderClassification]);
        Assert.Equal(1, tupleBody.HeaderPrefixes[boundaryHeaderHex]);
        Assert.Equal(1, tupleBody.PostPrefixLeads[postPrefixLeadHex]);

        Protocol03NestedMovementMode06TupleBodyParserActionSummary parserAction =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06TupleBodyParserActionSummaries(new[] { tupleBody }));
        Assert.Equal(expectedParserAction, parserAction.ParserAction);
        Assert.Equal(1, parserAction.BodyByteLengths["57"]);
        Assert.Equal(1, parserAction.MarkerBytes[markerByte]);
    }

    [Theory]
    [InlineData(
        "01 ff",
        "00 8c 0a 00 00 00 00 34",
        "ff 00 00 00 00 00 00 00 00 22 b6 00 00 4c 0a 00",
        "28 ff 01 00 00 00 00 00",
        "single state marker update",
        "marker-34",
        "34")]
    [InlineData(
        "02 ff",
        "00 e9 07 00 00 00 00 34",
        "ff 00 00 00 00 00 00 00 00 00 00 00 00 9c 05 00",
        "28 ff 02 00 00 00 00 00",
        "state marker/object update candidate",
        "marker-34-9c",
        "34")]
    [InlineData(
        "03 ff",
        "00 46 05 00 00 00 00 34",
        "ff 00 00 00 00 00 00 00 00 00 00 00 00 9c 05 00",
        "28 ff 02 00 00 00 00 00",
        "state marker/object update candidate",
        "marker-34-9c",
        "34")]
    [InlineData(
        "01 ff",
        "00 8c 0a 00 00 00 00 34",
        "ff 00 00 00 00 00 00 00 00 fd b9 00 00 f4 07 00",
        "28 ff 01 00 00 00 00 00",
        "single state marker update",
        "marker-34-alt",
        "34")]
    [InlineData(
        "03 ff",
        "00 46 05 00 00 00 00 34",
        "ff 00 00 00 00 00 00 00 00 00 00 00 00 0d 0a 00",
        "28 ff 02 00 00 00 00 00",
        "state marker/object update candidate",
        "marker-34-alt",
        "34")]
    [InlineData(
        "03 ff",
        "00 fb 04 00 00 00 00 31",
        "ff 00 00 00 00 00 00 00 00 d6 1e 00 00 4c 0a 00",
        "28 ff 01 00 00 00 00 00",
        "single state marker update",
        "marker-31",
        "31")]
    public void DetectProtocol03ObjectViews_BoundsGuardedMode0657ByteMarker34And31ObjectViewTupleBody(
        string prefixHex,
        string postPrefixLeadHex,
        string bodySuffixHex,
        string boundaryHeaderHex,
        string boundaryHeaderClassification,
        string markerName,
        string markerByte)
    {
        byte[] body = Enumerable.Repeat((byte)0x00, 6)
            .Concat(new byte[] { 0xff, 0x00 })
            .Concat(Enumerable.Repeat((byte)0x00, 33))
            .Concat(PacketResearcher.ParseHexBytes(bodySuffixHex))
            .ToArray();
        var bytes = new List<byte>
        {
            0x02, 0x03,
            0x01, 0x00, 0x02, 0x0c
        };
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 13));
        bytes.Add(0x03);
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 06 0e 01 18 58 3a f6 c6 00 00 be 42 44 35 af 46"));
        bytes.AddRange(PacketResearcher.ParseHexBytes("ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 22));
        bytes.AddRange(PacketResearcher.ParseHexBytes(prefixHex));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 7));
        bytes.AddRange(PacketResearcher.ParseHexBytes(postPrefixLeadHex));
        bytes.AddRange(body);
        bytes.AddRange(PacketResearcher.ParseHexBytes(boundaryHeaderHex));

        Protocol03ObjectViewSample[] samples = PacketResearcher.DetectProtocol03ObjectViews(bytes, 567, "server").ToArray();

        string expectedParserAction = $"test guarded mode 06 57-byte {markerName} object-view tuple body";
        string expectedClassification = $"mode 06 guarded 57-byte {markerName} object-view tuple body";
        const int expectedPayloadBytes = 17 + 55 + 57;
        Assert.Equal(2, samples.Length);
        Assert.True(samples[0].Complete);
        Assert.Equal(2, samples[0].ParsedUpdateCount);
        Assert.Equal(0, samples[0].UnparsedBytes);
        Assert.Collection(
            samples[0].Segments,
            segment =>
            {
                Assert.Equal("0c", segment.Selector);
                Assert.Equal("position xyz + flag", segment.Classification);
                Assert.Equal(13, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            },
            segment =>
            {
                Assert.Equal("03", segment.Selector);
                Assert.Equal(expectedClassification, segment.Classification);
                Assert.Equal(expectedPayloadBytes, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            });
        Assert.True(samples[1].Complete);
        Assert.Equal(0xff28, samples[1].ViewId);
        Assert.Equal(boundaryHeaderClassification, samples[1].Classification);

        Protocol03NestedMovementConsumedBodyBoundary boundary = Assert.Single(samples[0].NestedMovementConsumedBodyBoundaries);
        Assert.Equal(expectedParserAction, boundary.ParserAction);
        Assert.Equal("03", boundary.Selector);
        Assert.Equal(17, boundary.Offset);
        Assert.Equal(expectedPayloadBytes, boundary.PayloadBytes);
        Assert.Equal(expectedPayloadBytes + 8, boundary.EvidencePayloadBytes);
        Assert.Equal(17, boundary.TailStartOffset);
        Assert.Equal(112, boundary.TailBoundaryOffset);
        Assert.Equal(57, boundary.PostTupleBodyBytes);
        Assert.Equal("unclassified object-view tuple", boundary.TupleLayoutKind);
        Assert.Equal(6, boundary.FirstNonZeroOffset);
        Assert.Equal("ff 00", boundary.FirstNonZeroFieldHex);
        Assert.Equal(boundaryHeaderHex, boundary.BoundaryHeaderHex);
        Assert.Equal(boundaryHeaderClassification, boundary.BoundaryHeaderClassification);

        PacketDumpFileSummary dump = new(
            $"mode06-guarded-{markerName}-object-view-57.txt",
            1,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            Array.Empty<Protocol04PacketSequenceSample>(),
            new[] { samples[0] },
            Array.Empty<Protocol04InteractionPayloadSample>(),
            Array.Empty<PlayerAttributePayloadSample>(),
            Array.Empty<ManageBonusPayloadSample>());

        Protocol03NestedMovementMode06PostContinuationTupleBodySummary tupleBody =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06PostContinuationTupleBodySummaries(new[] { dump }));
        Assert.Equal("ff marker plus zero-padded ff continuation lead", tupleBody.TailPrefixKind);
        Assert.Equal(prefixHex, tupleBody.PrefixFirstNonZeroFieldHex);
        Assert.Equal("unclassified object-view tuple", tupleBody.TupleLayoutKind);
        Assert.Equal("unclassified object-view update", tupleBody.PostPrefixDisposition);
        Assert.Equal(57, tupleBody.PostTupleBodyBytes);
        Assert.Equal(bodySuffixHex, tupleBody.BodySuffixHex);
        Assert.Equal(6, tupleBody.FirstNonZeroOffset);
        Assert.Equal("ff 00", tupleBody.FirstNonZeroFieldHex);
        Assert.Equal(1, tupleBody.HeaderClassifications[boundaryHeaderClassification]);
        Assert.Equal(1, tupleBody.HeaderPrefixes[boundaryHeaderHex]);
        Assert.Equal(1, tupleBody.PostPrefixLeads[postPrefixLeadHex]);

        Protocol03NestedMovementMode06TupleBodyParserActionSummary parserAction =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06TupleBodyParserActionSummaries(new[] { tupleBody }));
        Assert.Equal(expectedParserAction, parserAction.ParserAction);
        Assert.Equal(1, parserAction.BodyByteLengths["57"]);
        Assert.Equal(1, parserAction.MarkerBytes[markerByte]);
    }

    [Theory]
    [InlineData(
        "02 ff",
        "00 27 06 00 00 00 00 28",
        "ff 00 00 00 00 00 00 00 00 99 95 00 00 4c 0a 00",
        "28 ff 02 00 00 00 00 00",
        "state marker/object update candidate",
        "marker-28")]
    [InlineData(
        "01 ff",
        "00 34 08 00 00 00 00 28",
        "ff 00 00 00 00 00 00 00 00 99 95 00 00 4c 0a 00",
        "28 ff 02 00 00 00 00 00",
        "state marker/object update candidate",
        "marker-28-alt")]
    [InlineData(
        "02 ff",
        "00 27 06 00 00 00 00 28",
        "ff 00 00 00 00 00 00 00 00 99 95 00 00 fa 09 00",
        "28 ff 01 00 00 00 00 00",
        "single state marker update",
        "marker-28-alt")]
    public void DetectProtocol03ObjectViews_BoundsGuardedMode0657ByteMarker28ObjectViewTupleBody(
        string prefixHex,
        string postPrefixLeadHex,
        string bodySuffixHex,
        string boundaryHeaderHex,
        string boundaryHeaderClassification,
        string markerName)
    {
        byte[] body = Enumerable.Repeat((byte)0x00, 6)
            .Concat(new byte[] { 0xff, 0x00 })
            .Concat(Enumerable.Repeat((byte)0x00, 33))
            .Concat(PacketResearcher.ParseHexBytes(bodySuffixHex))
            .ToArray();
        var bytes = new List<byte>
        {
            0x02, 0x03,
            0x01, 0x00, 0x02, 0x0c
        };
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 13));
        bytes.Add(0x03);
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 06 0e 01 18 58 3a f6 c6 00 00 be 42 44 35 af 46"));
        bytes.AddRange(PacketResearcher.ParseHexBytes("ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 22));
        bytes.AddRange(PacketResearcher.ParseHexBytes(prefixHex));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 7));
        bytes.AddRange(PacketResearcher.ParseHexBytes(postPrefixLeadHex));
        bytes.AddRange(body);
        bytes.AddRange(PacketResearcher.ParseHexBytes(boundaryHeaderHex));

        Protocol03ObjectViewSample[] samples = PacketResearcher.DetectProtocol03ObjectViews(bytes, 568, "server").ToArray();

        string expectedParserAction = $"test guarded mode 06 57-byte {markerName} object-view tuple body";
        string expectedClassification = $"mode 06 guarded 57-byte {markerName} object-view tuple body";
        const int expectedPayloadBytes = 17 + 55 + 57;
        Assert.Equal(2, samples.Length);
        Assert.True(samples[0].Complete);
        Assert.Equal(2, samples[0].ParsedUpdateCount);
        Assert.Equal(0, samples[0].UnparsedBytes);
        Assert.Collection(
            samples[0].Segments,
            segment =>
            {
                Assert.Equal("0c", segment.Selector);
                Assert.Equal("position xyz + flag", segment.Classification);
                Assert.Equal(13, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            },
            segment =>
            {
                Assert.Equal("03", segment.Selector);
                Assert.Equal(expectedClassification, segment.Classification);
                Assert.Equal(expectedPayloadBytes, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            });
        Assert.True(samples[1].Complete);
        Assert.Equal(0xff28, samples[1].ViewId);
        Assert.Equal(boundaryHeaderClassification, samples[1].Classification);

        Protocol03NestedMovementConsumedBodyBoundary boundary = Assert.Single(samples[0].NestedMovementConsumedBodyBoundaries);
        Assert.Equal(expectedParserAction, boundary.ParserAction);
        Assert.Equal("03", boundary.Selector);
        Assert.Equal(17, boundary.Offset);
        Assert.Equal(expectedPayloadBytes, boundary.PayloadBytes);
        Assert.Equal(expectedPayloadBytes + 8, boundary.EvidencePayloadBytes);
        Assert.Equal(17, boundary.TailStartOffset);
        Assert.Equal(112, boundary.TailBoundaryOffset);
        Assert.Equal(57, boundary.PostTupleBodyBytes);
        Assert.Equal("unclassified object-view tuple", boundary.TupleLayoutKind);
        Assert.Equal(6, boundary.FirstNonZeroOffset);
        Assert.Equal("ff 00", boundary.FirstNonZeroFieldHex);
        Assert.Equal(boundaryHeaderHex, boundary.BoundaryHeaderHex);
        Assert.Equal(boundaryHeaderClassification, boundary.BoundaryHeaderClassification);

        PacketDumpFileSummary dump = new(
            $"mode06-guarded-{markerName}-object-view-57.txt",
            1,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            Array.Empty<Protocol04PacketSequenceSample>(),
            new[] { samples[0] },
            Array.Empty<Protocol04InteractionPayloadSample>(),
            Array.Empty<PlayerAttributePayloadSample>(),
            Array.Empty<ManageBonusPayloadSample>());

        Protocol03NestedMovementMode06PostContinuationTupleBodySummary tupleBody =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06PostContinuationTupleBodySummaries(new[] { dump }));
        Assert.Equal("ff marker plus zero-padded ff continuation lead", tupleBody.TailPrefixKind);
        Assert.Equal(prefixHex, tupleBody.PrefixFirstNonZeroFieldHex);
        Assert.Equal("unclassified object-view tuple", tupleBody.TupleLayoutKind);
        Assert.Equal("unclassified object-view update", tupleBody.PostPrefixDisposition);
        Assert.Equal(57, tupleBody.PostTupleBodyBytes);
        Assert.Equal(bodySuffixHex, tupleBody.BodySuffixHex);
        Assert.Equal(6, tupleBody.FirstNonZeroOffset);
        Assert.Equal("ff 00", tupleBody.FirstNonZeroFieldHex);
        Assert.Equal(1, tupleBody.HeaderClassifications[boundaryHeaderClassification]);
        Assert.Equal(1, tupleBody.HeaderPrefixes[boundaryHeaderHex]);
        Assert.Equal(1, tupleBody.PostPrefixLeads[postPrefixLeadHex]);

        Protocol03NestedMovementMode06TupleBodyParserActionSummary parserAction =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06TupleBodyParserActionSummaries(new[] { tupleBody }));
        Assert.Equal(expectedParserAction, parserAction.ParserAction);
        Assert.Equal(1, parserAction.BodyByteLengths["57"]);
        Assert.Equal(1, parserAction.MarkerBytes["28"]);
    }

    [Fact]
    public void DetectProtocol03ObjectViews_BoundsGuardedMode0657ByteMarker1dB9ObjectViewTupleBody()
    {
        byte[] body = Enumerable.Repeat((byte)0x00, 6)
            .Concat(new byte[] { 0xff, 0x00 })
            .Concat(Enumerable.Repeat((byte)0x00, 33))
            .Concat(PacketResearcher.ParseHexBytes("ff 00 00 00 00 00 00 00 00 ca 1e 00 00 b9 07 00"))
            .ToArray();
        var bytes = new List<byte>
        {
            0x02, 0x03,
            0x01, 0x00, 0x02, 0x0c
        };
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 13));
        bytes.Add(0x03);
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 06 0e 01 18 58 3a f6 c6 00 00 be 42 44 35 af 46"));
        bytes.AddRange(PacketResearcher.ParseHexBytes("ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 22));
        bytes.AddRange(PacketResearcher.ParseHexBytes("03 ff"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 7));
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 07 03 00 00 00 00 1d"));
        bytes.AddRange(body);
        bytes.AddRange(PacketResearcher.ParseHexBytes("28 ff 02 00 00 00 00 00"));

        Protocol03ObjectViewSample[] samples = PacketResearcher.DetectProtocol03ObjectViews(bytes, 543, "server").ToArray();

        const int expectedPayloadBytes = 17 + 55 + 57;
        Assert.Equal(2, samples.Length);
        Assert.True(samples[0].Complete);
        Assert.Equal(2, samples[0].ParsedUpdateCount);
        Assert.Equal(0, samples[0].UnparsedBytes);
        Assert.Collection(
            samples[0].Segments,
            segment =>
            {
                Assert.Equal("0c", segment.Selector);
                Assert.Equal("position xyz + flag", segment.Classification);
                Assert.Equal(13, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            },
            segment =>
            {
                Assert.Equal("03", segment.Selector);
                Assert.Equal("mode 06 guarded 57-byte marker-1d-b9 object-view tuple body", segment.Classification);
                Assert.Equal(expectedPayloadBytes, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            });
        Assert.True(samples[1].Complete);
        Assert.Equal(0xff28, samples[1].ViewId);
        Assert.Equal("state marker/object update candidate", samples[1].Classification);

        Protocol03NestedMovementConsumedBodyBoundary boundary = Assert.Single(samples[0].NestedMovementConsumedBodyBoundaries);
        Assert.Equal("test guarded mode 06 57-byte marker-1d-b9 object-view tuple body", boundary.ParserAction);
        Assert.Equal("03", boundary.Selector);
        Assert.Equal(17, boundary.Offset);
        Assert.Equal(expectedPayloadBytes, boundary.PayloadBytes);
        Assert.Equal(expectedPayloadBytes + 8, boundary.EvidencePayloadBytes);
        Assert.Equal(17, boundary.TailStartOffset);
        Assert.Equal(112, boundary.TailBoundaryOffset);
        Assert.Equal(57, boundary.PostTupleBodyBytes);
        Assert.Equal("unclassified object-view tuple", boundary.TupleLayoutKind);
        Assert.Equal(6, boundary.FirstNonZeroOffset);
        Assert.Equal("ff 00", boundary.FirstNonZeroFieldHex);
        Assert.Equal("28 ff 02 00 00 00 00 00", boundary.BoundaryHeaderHex);
        Assert.Equal("state marker/object update candidate", boundary.BoundaryHeaderClassification);

        PacketDumpFileSummary dump = new(
            "mode06-guarded-marker-1d-b9-object-view-57.txt",
            1,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            Array.Empty<Protocol04PacketSequenceSample>(),
            new[] { samples[0] },
            Array.Empty<Protocol04InteractionPayloadSample>(),
            Array.Empty<PlayerAttributePayloadSample>(),
            Array.Empty<ManageBonusPayloadSample>());

        Protocol03NestedMovementMode06PostContinuationTupleBodySummary tupleBody =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06PostContinuationTupleBodySummaries(new[] { dump }));
        Assert.Equal("ff marker plus zero-padded ff continuation lead", tupleBody.TailPrefixKind);
        Assert.Equal("03 ff", tupleBody.PrefixFirstNonZeroFieldHex);
        Assert.Equal("unclassified object-view tuple", tupleBody.TupleLayoutKind);
        Assert.Equal("unclassified object-view update", tupleBody.PostPrefixDisposition);
        Assert.Equal(57, tupleBody.PostTupleBodyBytes);
        Assert.Equal("ff 00 00 00 00 00 00 00 00 ca 1e 00 00 b9 07 00", tupleBody.BodySuffixHex);
        Assert.Equal(6, tupleBody.FirstNonZeroOffset);
        Assert.Equal("ff 00", tupleBody.FirstNonZeroFieldHex);
        Assert.Equal(1, tupleBody.HeaderClassifications["state marker/object update candidate"]);
        Assert.Equal(1, tupleBody.HeaderPrefixes["28 ff 02 00 00 00 00 00"]);
        Assert.Equal(1, tupleBody.PostPrefixLeads["00 07 03 00 00 00 00 1d"]);

        Protocol03NestedMovementMode06TupleBodyParserActionSummary parserAction =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06TupleBodyParserActionSummaries(new[] { tupleBody }));
        Assert.Equal("test guarded mode 06 57-byte marker-1d-b9 object-view tuple body", parserAction.ParserAction);
        Assert.Equal(1, parserAction.BodyByteLengths["57"]);
    }

    [Fact]
    public void DetectProtocol03ObjectViews_BoundsGuardedMode0657ByteMarker1eB9ObjectViewTupleBody()
    {
        byte[] body = Enumerable.Repeat((byte)0x00, 6)
            .Concat(new byte[] { 0xff, 0x00 })
            .Concat(Enumerable.Repeat((byte)0x00, 33))
            .Concat(PacketResearcher.ParseHexBytes("ff 00 00 00 00 00 00 00 00 c1 09 00 00 b9 07 00"))
            .ToArray();
        var bytes = new List<byte>
        {
            0x02, 0x03,
            0x01, 0x00, 0x02, 0x0c
        };
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 13));
        bytes.Add(0x03);
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 06 0e 01 18 58 3a f6 c6 00 00 be 42 44 35 af 46"));
        bytes.AddRange(PacketResearcher.ParseHexBytes("ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 22));
        bytes.AddRange(PacketResearcher.ParseHexBytes("02 ff"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 7));
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 b0 04 00 00 00 00 1e"));
        bytes.AddRange(body);
        bytes.AddRange(PacketResearcher.ParseHexBytes("28 ff 02 00 00 00 00 00"));

        Protocol03ObjectViewSample[] samples = PacketResearcher.DetectProtocol03ObjectViews(bytes, 569, "server").ToArray();

        const int expectedPayloadBytes = 17 + 55 + 57;
        Assert.Equal(2, samples.Length);
        Assert.True(samples[0].Complete);
        Assert.Equal(2, samples[0].ParsedUpdateCount);
        Assert.Equal(0, samples[0].UnparsedBytes);
        Assert.Collection(
            samples[0].Segments,
            segment =>
            {
                Assert.Equal("0c", segment.Selector);
                Assert.Equal("position xyz + flag", segment.Classification);
                Assert.Equal(13, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            },
            segment =>
            {
                Assert.Equal("03", segment.Selector);
                Assert.Equal("mode 06 guarded 57-byte marker-1e-b9 object-view tuple body", segment.Classification);
                Assert.Equal(expectedPayloadBytes, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            });
        Assert.True(samples[1].Complete);
        Assert.Equal(0xff28, samples[1].ViewId);
        Assert.Equal("state marker/object update candidate", samples[1].Classification);

        Protocol03NestedMovementConsumedBodyBoundary boundary = Assert.Single(samples[0].NestedMovementConsumedBodyBoundaries);
        Assert.Equal("test guarded mode 06 57-byte marker-1e-b9 object-view tuple body", boundary.ParserAction);
        Assert.Equal("03", boundary.Selector);
        Assert.Equal(17, boundary.Offset);
        Assert.Equal(expectedPayloadBytes, boundary.PayloadBytes);
        Assert.Equal(expectedPayloadBytes + 8, boundary.EvidencePayloadBytes);
        Assert.Equal(17, boundary.TailStartOffset);
        Assert.Equal(112, boundary.TailBoundaryOffset);
        Assert.Equal(57, boundary.PostTupleBodyBytes);
        Assert.Equal("unclassified object-view tuple", boundary.TupleLayoutKind);
        Assert.Equal(6, boundary.FirstNonZeroOffset);
        Assert.Equal("ff 00", boundary.FirstNonZeroFieldHex);
        Assert.Equal("28 ff 02 00 00 00 00 00", boundary.BoundaryHeaderHex);
        Assert.Equal("state marker/object update candidate", boundary.BoundaryHeaderClassification);

        PacketDumpFileSummary dump = new(
            "mode06-guarded-marker-1e-b9-object-view-57.txt",
            1,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            Array.Empty<Protocol04PacketSequenceSample>(),
            new[] { samples[0] },
            Array.Empty<Protocol04InteractionPayloadSample>(),
            Array.Empty<PlayerAttributePayloadSample>(),
            Array.Empty<ManageBonusPayloadSample>());

        Protocol03NestedMovementMode06PostContinuationTupleBodySummary tupleBody =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06PostContinuationTupleBodySummaries(new[] { dump }));
        Assert.Equal("ff marker plus zero-padded ff continuation lead", tupleBody.TailPrefixKind);
        Assert.Equal("02 ff", tupleBody.PrefixFirstNonZeroFieldHex);
        Assert.Equal("unclassified object-view tuple", tupleBody.TupleLayoutKind);
        Assert.Equal("unclassified object-view update", tupleBody.PostPrefixDisposition);
        Assert.Equal(57, tupleBody.PostTupleBodyBytes);
        Assert.Equal("ff 00 00 00 00 00 00 00 00 c1 09 00 00 b9 07 00", tupleBody.BodySuffixHex);
        Assert.Equal(6, tupleBody.FirstNonZeroOffset);
        Assert.Equal("ff 00", tupleBody.FirstNonZeroFieldHex);
        Assert.Equal(1, tupleBody.HeaderClassifications["state marker/object update candidate"]);
        Assert.Equal(1, tupleBody.HeaderPrefixes["28 ff 02 00 00 00 00 00"]);
        Assert.Equal(1, tupleBody.PostPrefixLeads["00 b0 04 00 00 00 00 1e"]);

        Protocol03NestedMovementMode06TupleBodyParserActionSummary parserAction =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06TupleBodyParserActionSummaries(new[] { tupleBody }));
        Assert.Equal("test guarded mode 06 57-byte marker-1e-b9 object-view tuple body", parserAction.ParserAction);
        Assert.Equal(1, parserAction.BodyByteLengths["57"]);
        Assert.Equal(1, parserAction.MarkerBytes["1e"]);
    }

    [Fact]
    public void DetectProtocol03ObjectViews_BoundsGuardedMode0657ByteMarker15EcObjectViewTupleBody()
    {
        byte[] body = Enumerable.Repeat((byte)0x00, 6)
            .Concat(new byte[] { 0xff, 0x00 })
            .Concat(Enumerable.Repeat((byte)0x00, 33))
            .Concat(PacketResearcher.ParseHexBytes("ff 00 00 00 00 00 00 00 00 ec 1a 00 00 4c 0a 00"))
            .ToArray();
        var bytes = new List<byte>
        {
            0x02, 0x03,
            0x01, 0x00, 0x02, 0x0c
        };
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 13));
        bytes.Add(0x03);
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 06 0e 01 18 58 3a f6 c6 00 00 be 42 44 35 af 46"));
        bytes.AddRange(PacketResearcher.ParseHexBytes("ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 22));
        bytes.AddRange(PacketResearcher.ParseHexBytes("02 ff"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 7));
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 5e 03 00 00 00 00 15"));
        bytes.AddRange(body);
        bytes.AddRange(PacketResearcher.ParseHexBytes("28 ff 01 00 00 00 00 00"));

        Protocol03ObjectViewSample[] samples = PacketResearcher.DetectProtocol03ObjectViews(bytes, 543, "server").ToArray();

        const int expectedPayloadBytes = 17 + 55 + 57;
        Assert.Equal(2, samples.Length);
        Assert.True(samples[0].Complete);
        Assert.Equal(2, samples[0].ParsedUpdateCount);
        Assert.Equal(0, samples[0].UnparsedBytes);
        Assert.Collection(
            samples[0].Segments,
            segment =>
            {
                Assert.Equal("0c", segment.Selector);
                Assert.Equal("position xyz + flag", segment.Classification);
                Assert.Equal(13, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            },
            segment =>
            {
                Assert.Equal("03", segment.Selector);
                Assert.Equal("mode 06 guarded 57-byte marker-15-ec object-view tuple body", segment.Classification);
                Assert.Equal(expectedPayloadBytes, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            });
        Assert.True(samples[1].Complete);
        Assert.Equal(0xff28, samples[1].ViewId);
        Assert.Equal("single state marker update", samples[1].Classification);

        Protocol03NestedMovementConsumedBodyBoundary boundary = Assert.Single(samples[0].NestedMovementConsumedBodyBoundaries);
        Assert.Equal("test guarded mode 06 57-byte marker-15-ec object-view tuple body", boundary.ParserAction);
        Assert.Equal("03", boundary.Selector);
        Assert.Equal(17, boundary.Offset);
        Assert.Equal(expectedPayloadBytes, boundary.PayloadBytes);
        Assert.Equal(expectedPayloadBytes + 8, boundary.EvidencePayloadBytes);
        Assert.Equal(17, boundary.TailStartOffset);
        Assert.Equal(112, boundary.TailBoundaryOffset);
        Assert.Equal(57, boundary.PostTupleBodyBytes);
        Assert.Equal("unclassified object-view tuple", boundary.TupleLayoutKind);
        Assert.Equal(6, boundary.FirstNonZeroOffset);
        Assert.Equal("ff 00", boundary.FirstNonZeroFieldHex);
        Assert.Equal("28 ff 01 00 00 00 00 00", boundary.BoundaryHeaderHex);
        Assert.Equal("single state marker update", boundary.BoundaryHeaderClassification);

        PacketDumpFileSummary dump = new(
            "mode06-guarded-marker-15-ec-object-view-57.txt",
            1,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            Array.Empty<Protocol04PacketSequenceSample>(),
            new[] { samples[0] },
            Array.Empty<Protocol04InteractionPayloadSample>(),
            Array.Empty<PlayerAttributePayloadSample>(),
            Array.Empty<ManageBonusPayloadSample>());

        Protocol03NestedMovementMode06PostContinuationTupleBodySummary tupleBody =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06PostContinuationTupleBodySummaries(new[] { dump }));
        Assert.Equal("ff marker plus zero-padded ff continuation lead", tupleBody.TailPrefixKind);
        Assert.Equal("02 ff", tupleBody.PrefixFirstNonZeroFieldHex);
        Assert.Equal("unclassified object-view tuple", tupleBody.TupleLayoutKind);
        Assert.Equal("unclassified object-view update", tupleBody.PostPrefixDisposition);
        Assert.Equal(57, tupleBody.PostTupleBodyBytes);
        Assert.Equal("ff 00 00 00 00 00 00 00 00 ec 1a 00 00 4c 0a 00", tupleBody.BodySuffixHex);
        Assert.Equal(6, tupleBody.FirstNonZeroOffset);
        Assert.Equal("ff 00", tupleBody.FirstNonZeroFieldHex);
        Assert.Equal(1, tupleBody.HeaderClassifications["single state marker update"]);
        Assert.Equal(1, tupleBody.HeaderPrefixes["28 ff 01 00 00 00 00 00"]);
        Assert.Equal(1, tupleBody.PostPrefixLeads["00 5e 03 00 00 00 00 15"]);

        Protocol03NestedMovementMode06TupleBodyParserActionSummary parserAction =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06TupleBodyParserActionSummaries(new[] { tupleBody }));
        Assert.Equal("test guarded mode 06 57-byte marker-15-ec object-view tuple body", parserAction.ParserAction);
        Assert.Equal(1, parserAction.BodyByteLengths["57"]);
    }

    [Theory]
    [InlineData("02 ff", "00 e9 07 00 00 00 00 34", "00 b8 01 00 28 ff 15 00", "01 00 98 05 ff 00 00 00 00 00 00 00 00 00 00 00", "mode 06 guarded 53-byte marker-34-b8-15 object-view tuple body", "test guarded mode 06 53-byte marker-34-b8-15 object-view tuple body")]
    [InlineData("02 ff", "00 e9 07 00 00 00 00 34", "00 62 01 00 28 ff 0f 00", "01 00 9a 04 ff 00 00 00 00 00 00 00 00 00 00 00", "mode 06 guarded 53-byte marker-34-62-0f object-view tuple body", "test guarded mode 06 53-byte marker-34-62-0f object-view tuple body")]
    [InlineData("02 ff", "00 e9 07 00 00 00 00 34", "00 b8 01 00 28 ff 1c 00", "01 00 6a 04 ff 00 00 00 00 00 00 00 00 00 00 00", "mode 06 guarded 53-byte marker-34-b8-1c object-view tuple body", "test guarded mode 06 53-byte marker-34-b8-1c object-view tuple body")]
    [InlineData("02 ff", "00 e9 07 00 00 00 00 34", "00 62 01 00 28 ff 08 00", "01 00 17 07 ff 00 00 00 00 00 00 00 00 00 00 00", "mode 06 guarded 53-byte marker-34-62-alt object-view tuple body", "test guarded mode 06 53-byte marker-34-62-alt object-view tuple body")]
    [InlineData("02 ff", "00 e9 07 00 00 00 00 34", "00 62 01 00 28 ff 15 00", "01 00 9e 03 ff 00 00 00 00 00 00 00 00 00 00 00", "mode 06 guarded 53-byte marker-34-62-alt object-view tuple body", "test guarded mode 06 53-byte marker-34-62-alt object-view tuple body")]
    [InlineData("02 ff", "00 e9 07 00 00 00 00 34", "00 62 01 00 28 ff 16 00", "01 00 b8 07 ff 00 00 00 00 00 00 00 00 00 00 00", "mode 06 guarded 53-byte marker-34-62-alt object-view tuple body", "test guarded mode 06 53-byte marker-34-62-alt object-view tuple body")]
    [InlineData("03 ff", "00 46 05 00 00 00 00 34", "00 62 01 00 28 ff 01 00", "01 00 46 05 ff 00 00 00 00 00 00 00 00 00 00 00", "mode 06 guarded 53-byte marker-34-46 object-view tuple body", "test guarded mode 06 53-byte marker-34-46 object-view tuple body")]
    [InlineData("03 ff", "00 46 05 00 00 00 00 34", "00 b7 01 00 28 ff 0b 00", "01 00 67 01 ff 00 00 00 00 00 00 00 00 00 00 00", "mode 06 guarded 53-byte marker-34-46 object-view tuple body", "test guarded mode 06 53-byte marker-34-46 object-view tuple body")]
    [InlineData("03 ff", "00 46 05 00 00 00 00 34", "00 b7 01 00 28 ff 0b 00", "01 00 c6 01 ff 00 00 00 00 00 00 00 00 00 00 00", "mode 06 guarded 53-byte marker-34-46 object-view tuple body", "test guarded mode 06 53-byte marker-34-46 object-view tuple body")]
    [InlineData("02 ff", "00 78 07 00 00 00 00 31", "00 c1 01 00 28 ff 03 00", "01 00 8f 05 ff 00 00 00 00 00 00 00 00 db 1e 00", "mode 06 guarded 53-byte marker-31 object-view tuple body", "test guarded mode 06 53-byte marker-31 object-view tuple body")]
    [InlineData("02 ff", "00 78 07 00 00 00 00 31", "00 c1 01 00 28 ff 07 00", "01 00 c0 03 ff 00 00 00 00 00 00 00 00 db 1e 00", "mode 06 guarded 53-byte marker-31 object-view tuple body", "test guarded mode 06 53-byte marker-31 object-view tuple body")]
    public void DetectProtocol03ObjectViews_BoundsGuardedMode0653ByteMarker34ObjectViewTupleBody(
        string prefixFirstNonZeroFieldHex,
        string postPrefixLeadHex,
        string boundaryHeaderHex,
        string bodySuffixHex,
        string expectedClassification,
        string expectedParserAction)
    {
        byte[] body = Enumerable.Repeat((byte)0x00, 6)
            .Concat(new byte[] { 0xff, 0x00 })
            .Concat(Enumerable.Repeat((byte)0x00, 29))
            .Concat(PacketResearcher.ParseHexBytes(bodySuffixHex))
            .ToArray();
        var bytes = new List<byte>
        {
            0x02, 0x03,
            0x01, 0x00, 0x02, 0x0c
        };
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 13));
        bytes.Add(0x03);
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 06 0e 01 18 58 3a f6 c6 00 00 be 42 44 35 af 46"));
        bytes.AddRange(PacketResearcher.ParseHexBytes("ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 22));
        bytes.AddRange(PacketResearcher.ParseHexBytes(prefixFirstNonZeroFieldHex));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 7));
        bytes.AddRange(PacketResearcher.ParseHexBytes(postPrefixLeadHex));
        bytes.AddRange(body);
        bytes.AddRange(PacketResearcher.ParseHexBytes(boundaryHeaderHex));

        Protocol03ObjectViewSample[] samples = PacketResearcher.DetectProtocol03ObjectViews(bytes, 543, "server").ToArray();

        const int expectedPayloadBytes = 17 + 55 + 53;
        Assert.Equal(2, samples.Length);
        Assert.True(samples[0].Complete);
        Assert.Equal(2, samples[0].ParsedUpdateCount);
        Assert.Equal(0, samples[0].UnparsedBytes);
        Assert.Collection(
            samples[0].Segments,
            segment =>
            {
                Assert.Equal("0c", segment.Selector);
                Assert.Equal("position xyz + flag", segment.Classification);
                Assert.Equal(13, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            },
            segment =>
            {
                Assert.Equal("03", segment.Selector);
                Assert.Equal(expectedClassification, segment.Classification);
                Assert.Equal(expectedPayloadBytes, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            });
        Assert.True(samples[1].Complete);
        Assert.Equal("single state marker update", samples[1].Classification);

        Protocol03NestedMovementConsumedBodyBoundary boundary = Assert.Single(samples[0].NestedMovementConsumedBodyBoundaries);
        Assert.Equal(expectedParserAction, boundary.ParserAction);
        Assert.Equal("03", boundary.Selector);
        Assert.Equal(17, boundary.Offset);
        Assert.Equal(expectedPayloadBytes, boundary.PayloadBytes);
        Assert.Equal(expectedPayloadBytes + 8, boundary.EvidencePayloadBytes);
        Assert.Equal(17, boundary.TailStartOffset);
        Assert.Equal(108, boundary.TailBoundaryOffset);
        Assert.Equal(53, boundary.PostTupleBodyBytes);
        Assert.Equal("unclassified object-view tuple", boundary.TupleLayoutKind);
        Assert.Equal(6, boundary.FirstNonZeroOffset);
        Assert.Equal("ff 00", boundary.FirstNonZeroFieldHex);
        Assert.Equal(boundaryHeaderHex, boundary.BoundaryHeaderHex);
        Assert.Equal("single state marker update", boundary.BoundaryHeaderClassification);

        PacketDumpFileSummary dump = new(
            "mode06-guarded-marker-34-object-view-53.txt",
            1,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            Array.Empty<Protocol04PacketSequenceSample>(),
            new[] { samples[0] },
            Array.Empty<Protocol04InteractionPayloadSample>(),
            Array.Empty<PlayerAttributePayloadSample>(),
            Array.Empty<ManageBonusPayloadSample>());

        Protocol03NestedMovementMode06PostContinuationTupleBodySummary tupleBody =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06PostContinuationTupleBodySummaries(new[] { dump }));
        Assert.Equal("ff marker plus zero-padded ff continuation lead", tupleBody.TailPrefixKind);
        Assert.Equal(prefixFirstNonZeroFieldHex, tupleBody.PrefixFirstNonZeroFieldHex);
        Assert.Equal("unclassified object-view tuple", tupleBody.TupleLayoutKind);
        Assert.Equal("unclassified object-view update", tupleBody.PostPrefixDisposition);
        Assert.Equal(53, tupleBody.PostTupleBodyBytes);
        Assert.Equal(bodySuffixHex, tupleBody.BodySuffixHex);
        Assert.Equal(6, tupleBody.FirstNonZeroOffset);
        Assert.Equal("ff 00", tupleBody.FirstNonZeroFieldHex);
        Assert.Equal(1, tupleBody.HeaderClassifications["single state marker update"]);
        Assert.Equal(1, tupleBody.HeaderPrefixes[boundaryHeaderHex]);
        Assert.Equal(1, tupleBody.PostPrefixLeads[postPrefixLeadHex]);

        Protocol03NestedMovementMode06TupleBodyParserActionSummary parserAction =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06TupleBodyParserActionSummaries(new[] { tupleBody }));
        Assert.Equal(expectedParserAction, parserAction.ParserAction);
        Assert.Equal(1, parserAction.BodyByteLengths["53"]);
    }

    [Fact]
    public void BuildProtocol03NestedMovementMode06Marker34LongBodyFixtureSummaries_ExportsHeldLongBodyBytes()
    {
        byte[] suffix = PacketResearcher.ParseHexBytes("00 00 04 01 06 fa 01 3e 81 67 17 00 27 00 fa 00")
            .ToArray();
        byte[] body = Enumerable.Repeat((byte)0x00, 6)
            .Concat(new byte[] { 0xff, 0x00 })
            .Concat(Enumerable.Repeat((byte)0x00, 86))
            .Concat(suffix)
            .ToArray();
        var bytes = new List<byte>
        {
            0x02, 0x03,
            0x01, 0x00, 0x02, 0x0c
        };
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 13));
        bytes.Add(0x03);
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 06 0e 01 18 58 3a f6 c6 00 00 be 42 44 35 af 46"));
        bytes.AddRange(PacketResearcher.ParseHexBytes("ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 22));
        bytes.AddRange(PacketResearcher.ParseHexBytes("02 ff"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 7));
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 e9 07 00 00 00 00 34"));
        bytes.AddRange(body);
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 7c 01 00 10 00 00 00"));

        Protocol03ObjectViewSample[] samples = PacketResearcher.DetectProtocol03ObjectViews(bytes, 544, "server").ToArray();

        Assert.NotEmpty(samples);
        PacketDumpFileSummary dump = new(
            "mode06-marker-34-held-long-body.txt",
            1,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            Array.Empty<Protocol04PacketSequenceSample>(),
            new[] { samples[0] },
            Array.Empty<Protocol04InteractionPayloadSample>(),
            Array.Empty<PlayerAttributePayloadSample>(),
            Array.Empty<ManageBonusPayloadSample>());

        Protocol03NestedMovementMode06PostContinuationTupleBodySummary tupleBody =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06PostContinuationTupleBodySummaries(new[] { dump }));
        Assert.Equal(110, tupleBody.PostTupleBodyBytes);
        Assert.Equal("02 ff", tupleBody.PrefixFirstNonZeroFieldHex);
        Assert.Equal("00 e9 07 00 00 00 00 34", Assert.Single(tupleBody.PostPrefixLeads).Key);

        Protocol03NestedMovementMode06TupleBodyParserActionSummary parserAction =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06TupleBodyParserActionSummaries(new[] { tupleBody }));
        Assert.Equal("hold mode 06 marker-34 long object-view tuple body as evidence only", parserAction.ParserAction);
        Assert.Equal("variable-length marker-34 object-view body needs body-length rule", parserAction.ActionReason);

        Protocol03NestedMovementMode06Marker34LongBodyFixtureSummary fixture =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06Marker34LongBodyFixtureSummaries(new[] { dump }));
        Assert.Equal(parserAction.ParserAction, fixture.ParserAction);
        Assert.Equal(parserAction.ActionReason, fixture.ActionReason);
        Assert.Equal(110, fixture.BodyBytes);
        Assert.Equal(PacketResearcher.FormatHeader(body), fixture.BodyHex);
        Assert.Equal("00 00 00 00 00 00 ff 00 00 00 00 00 00 00 00 00", fixture.BodyPrefixHex);
        Assert.Equal("00 00 04 01 06 fa 01 3e 81 67 17 00 27 00 fa 00", fixture.BodySuffixHex);
        Assert.Equal(6, fixture.FirstNonZeroOffset);
        Assert.Equal("ff 00", fixture.FirstNonZeroFieldHex);
        Assert.Contains("6:ff", fixture.BodyNonZeroOffsets, StringComparison.Ordinal);
        Assert.Contains("96:04", fixture.BodyNonZeroOffsets, StringComparison.Ordinal);
        Assert.Equal(17, fixture.TailStartOffset);
        Assert.Equal(33, fixture.ContinuationCutOffset);
        Assert.Equal(72, fixture.BodyOffset);
        Assert.Equal(182, fixture.BoundaryCutOffset);
        Assert.Equal(149, fixture.PostContinuationBoundaryOffset);
        Assert.Equal("unclassified object-view tuple", fixture.TupleLayoutKind);
        Assert.Equal("unclassified object-view update", fixture.PostPrefixDisposition);
        Assert.Equal("single state marker update", fixture.BoundaryHeaderClassification);
        Assert.Equal("00 7c 01 00 10 00 00 00", fixture.BoundaryHeaderHex);

        byte[] interiorBody = Enumerable.Repeat((byte)0x00, 160).ToArray();
        interiorBody[6] = 0xff;
        interiorBody[37] = 0x01;
        interiorBody[39] = 0xd4;
        interiorBody[40] = 0x06;
        interiorBody[41] = 0xff;
        byte[] knownFxWindow = PacketResearcher.ParseHexBytes("30 04 00 28 ff 14").ToArray();
        Array.Copy(knownFxWindow, 0, interiorBody, 54, knownFxWindow.Length);
        interiorBody[71] = 0x76;
        interiorBody[72] = 0x0c;
        interiorBody[74] = 0x58;
        interiorBody[75] = 0x37;
        interiorBody[76] = 0x08;
        interiorBody[79] = 0x1f;
        interiorBody[80] = 0x06;
        interiorBody[81] = 0x08;
        interiorBody[88] = 0x22;
        byte[] embeddedMovementWindow = PacketResearcher.ParseHexBytes("0e 00 06 0c 13 51 10 3b c7 00 a7 8c 42 00 42 f4 c6").ToArray();
        Array.Copy(embeddedMovementWindow, 0, interiorBody, 94, embeddedMovementWindow.Length);
        interiorBody[148] = 0x00;
        interiorBody[149] = 0x02;
        interiorBody[150] = 0xff;
        Protocol03NestedMovementMode06Marker34LongBodyFixtureSummary interiorFixture = fixture with
        {
            BodyBytes = interiorBody.Length,
            BodyHex = PacketResearcher.FormatHeader(interiorBody),
            BodyPrefixHex = PacketResearcher.FormatHeader(interiorBody.Take(16)),
            BodySuffixHex = PacketResearcher.FormatHeader(interiorBody.Skip(interiorBody.Length - 16)),
            BoundaryHeaderClassification = "attribute/effect bundle",
            BoundaryHeaderHex = "fb 02 04 80 b3 d5 04 08",
            InnerSelectorHex = "0c",
            OuterSelectorHex = "10",
            Position = "-47802.75,36.102,-31265"
        };
        Protocol03NestedMovementMode06Marker34LongBodyOffsetSummary[] offsetSummaries =
            PacketResearcher.BuildProtocol03NestedMovementMode06Marker34LongBodyOffsetSummaries(new[] { fixture, interiorFixture })
                .ToArray();
        Protocol03NestedMovementMode06Marker34LongBodyOffsetSummary offset6 =
            Assert.Single(offsetSummaries, summary => summary.BodyOffset == 6);
        Assert.Equal("stable nonzero byte", offset6.OffsetDisposition);
        Assert.Equal(2, offset6.PresentFixtureCount);
        Assert.Equal(2, offset6.NonZeroFixtureCount);
        Assert.Equal(2, offset6.ByteValues["ff"]);
        Assert.Equal(2, offset6.BodyByteLengths.Count);
        Protocol03NestedMovementMode06Marker34LongBodyOffsetSummary offset96 =
            Assert.Single(offsetSummaries, summary => summary.BodyOffset == 96);
        Assert.Equal("present nonzero variable byte", offset96.OffsetDisposition);
        Assert.Equal(1, offset96.ByteValues["04"]);
        Assert.Equal(1, offset96.ByteValues["06"]);

        Protocol03NestedMovementMode06Marker34LongBodyInteriorWindowSummary[] interiorWindows =
            PacketResearcher.BuildProtocol03NestedMovementMode06Marker34LongBodyInteriorWindowSummaries(new[] { fixture, interiorFixture })
                .ToArray();
        Assert.Collection(
            interiorWindows,
            window =>
            {
                Assert.Equal("embedded mode 06 movement window", window.WindowDisposition);
                Assert.Equal(95, window.BodyOffset);
                Assert.Equal("00 06 0c", window.WindowHex);
                Assert.Equal(1, window.FixtureCount);
                Assert.Equal(1, window.BodyByteLengths["160"]);
            },
            window =>
            {
                Assert.Equal("embedded marker-34 prefix-field lead", window.WindowDisposition);
                Assert.Equal(148, window.BodyOffset);
                Assert.Equal("00 02 ff", window.WindowHex);
                Assert.Equal(1, window.FixtureCount);
                Assert.Equal(1, window.BoundaryHeaderClassifications["attribute/effect bundle"]);
            });

        FxDefinition fx = new("30040028", "28000430", "FX_CHARACTER_HEALTH_DRAIN", "fxlisthex.txt", 20);
        Protocol03NestedMovementMode06Marker34LongBodyKnownFxWindowSummary knownFx =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06Marker34LongBodyKnownFxWindowSummaries(new[] { fixture, interiorFixture }, new[] { fx }));
        Assert.Equal(54, knownFx.BodyOffset);
        Assert.Equal("30 04 00 28", knownFx.FxHex);
        Assert.Equal("FX_CHARACTER_HEALTH_DRAIN", knownFx.FxName);
        Assert.Equal(1, knownFx.FixtureCount);
        Assert.Equal(1, knownFx.FollowupBytes["ff 14"]);
        Assert.Equal(1, knownFx.BodyByteLengths["160"]);
        Assert.Equal(1, knownFx.BoundaryHeaderClassifications["attribute/effect bundle"]);

        byte[] nestedBody = Enumerable.Repeat((byte)0x00, 265).ToArray();
        byte[] nestedPrefix = PacketResearcher.ParseHexBytes("00 02 ff cd cc 4c be 00 00 00 00 e9 07 00 00 00 00 34").ToArray();
        Array.Copy(nestedPrefix, 0, nestedBody, 148, nestedPrefix.Length);
        int nestedBodyOffset = 148 + nestedPrefix.Length;
        nestedBody[nestedBodyOffset + 6] = 0xff;
        nestedBody[nestedBodyOffset + 37] = 0x01;
        nestedBody[nestedBodyOffset + 39] = 0x83;
        nestedBody[nestedBodyOffset + 40] = 0x07;
        nestedBody[nestedBodyOffset + 41] = 0xff;
        Array.Copy(knownFxWindow, 0, nestedBody, nestedBodyOffset + 54, knownFxWindow.Length);
        nestedBody[nestedBodyOffset + 71] = 0x76;
        nestedBody[nestedBodyOffset + 72] = 0x0c;
        nestedBody[nestedBodyOffset + 74] = 0x58;
        nestedBody[nestedBodyOffset + 75] = 0x37;
        nestedBody[nestedBodyOffset + 76] = 0x08;
        nestedBody[nestedBodyOffset + 79] = 0x1f;
        nestedBody[nestedBodyOffset + 80] = 0x06;
        nestedBody[nestedBodyOffset + 81] = 0x08;
        nestedBody[nestedBodyOffset + 88] = 0x22;
        Protocol03NestedMovementMode06Marker34LongBodyFixtureSummary nestedFixture = fixture with
        {
            BodyBytes = nestedBody.Length,
            BodyHex = PacketResearcher.FormatHeader(nestedBody),
            BodyPrefixHex = PacketResearcher.FormatHeader(nestedBody.Take(16)),
            BodySuffixHex = PacketResearcher.FormatHeader(nestedBody.Skip(nestedBody.Length - 16)),
            BoundaryHeaderClassification = "attribute/effect bundle",
            BoundaryHeaderHex = "fb 02 04 80 b3 d5 04 08",
            InnerSelectorHex = "0c",
            OuterSelectorHex = "10",
            Position = "-47802.75,36.102,-31265"
        };
        Protocol03NestedMovementMode06Marker34LongBodyNestedReplaySummary nestedReplay =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06Marker34LongBodyNestedReplaySummaries(new[] { nestedFixture }, new[] { fx }));
        Assert.Equal("nested marker-34 body replay with stable scaffold", nestedReplay.ReplayDisposition);
        Assert.Equal(148, nestedReplay.PrefixBodyOffset);
        Assert.Equal(nestedBodyOffset, nestedReplay.NestedBodyOffset);
        Assert.Equal(99, nestedReplay.AvailableNestedBodyBytes);
        Assert.Equal(PacketResearcher.FormatHeader(nestedPrefix), nestedReplay.PrefixHex);
        Assert.Equal("01 00 83 07 ff", nestedReplay.NestedTagWindowHex);
        Assert.Equal("30 04 00 28", nestedReplay.NestedKnownFxHex);
        Assert.Equal("FX_CHARACTER_HEALTH_DRAIN", nestedReplay.NestedKnownFxName);
        Assert.Equal("ff 14", nestedReplay.NestedKnownFxFollowupBytes);
        Assert.Equal(12, nestedReplay.StableOffsetMatchCount);
        Assert.Equal(12, nestedReplay.StableOffsetProbeCount);
        Assert.Contains("88:22", nestedReplay.MatchedStableOffsets, StringComparison.Ordinal);

        Protocol03NestedMovementMode06Marker34LongBodyFxFieldRoleSummary[] fxFieldRoles =
            PacketResearcher.BuildProtocol03NestedMovementMode06Marker34LongBodyFxFieldRoleSummaries(new[] { interiorFixture, nestedFixture }, new[] { fx })
                .ToArray();
        Protocol03NestedMovementMode06Marker34LongBodyFxFieldRoleSummary outerFxRole =
            Assert.Single(fxFieldRoles, summary => summary.Marker34BodyScope == "outer marker-34 body");
        Assert.Equal("marker-34 effect field at relative offset 54", outerFxRole.FieldDisposition);
        Assert.Equal(0, outerFxRole.Marker34BodyOffset);
        Assert.Equal(54, outerFxRole.BodyOffset);
        Assert.Equal(54, outerFxRole.Marker34RelativeOffset);
        Assert.Equal("30 04 00 28", outerFxRole.FxHex);
        Assert.Equal("FX_CHARACTER_HEALTH_DRAIN", outerFxRole.FxName);
        Assert.Equal(1, outerFxRole.FollowupBytes["ff 14"]);
        Protocol03NestedMovementMode06Marker34LongBodyFxFieldRoleSummary nestedFxRole =
            Assert.Single(fxFieldRoles, summary => summary.Marker34BodyScope == "nested marker-34 replay body");
        Assert.Equal(nestedBodyOffset, nestedFxRole.Marker34BodyOffset);
        Assert.Equal(nestedBodyOffset + 54, nestedFxRole.BodyOffset);
        Assert.Equal(54, nestedFxRole.Marker34RelativeOffset);
        Assert.Equal("30 04 00 28", nestedFxRole.FxHex);
        Assert.Equal(1, nestedFxRole.FollowupBytes["ff 14"]);

        Protocol03NestedMovementMode06Marker34LongBodyStructuralRoleSummary[] structuralRoles =
            PacketResearcher.BuildProtocol03NestedMovementMode06Marker34LongBodyStructuralRoleSummaries(new[] { interiorFixture, nestedFixture }, new[] { fx })
                .ToArray();
        Assert.Equal(8, structuralRoles.Length);
        Protocol03NestedMovementMode06Marker34LongBodyStructuralRoleSummary outerMarkerLead =
            Assert.Single(structuralRoles, summary =>
                summary.Marker34BodyScope == "outer marker-34 body" &&
                summary.Marker34RelativeOffset == 6);
        Assert.Equal("marker-34 marker lead", outerMarkerLead.FieldDisposition);
        Assert.Equal(6, outerMarkerLead.BodyOffset);
        Assert.Equal(2, outerMarkerLead.WindowBytes);
        Assert.Equal(1, outerMarkerLead.WindowHexes["ff 00"]);
        Protocol03NestedMovementMode06Marker34LongBodyStructuralRoleSummary outerTag =
            Assert.Single(structuralRoles, summary =>
                summary.Marker34BodyScope == "outer marker-34 body" &&
                summary.Marker34RelativeOffset == 37);
        Assert.Equal(1, outerTag.Interpretations["tag-le 1748"]);
        Protocol03NestedMovementMode06Marker34LongBodyStructuralRoleSummary outerScaffold =
            Assert.Single(structuralRoles, summary =>
                summary.Marker34BodyScope == "outer marker-34 body" &&
                summary.Marker34RelativeOffset == 71);
        Assert.Equal("marker-34 stable scaffold field", outerScaffold.FieldDisposition);
        Assert.Equal(18, outerScaffold.WindowBytes);
        Protocol03NestedMovementMode06Marker34LongBodyStructuralRoleSummary nestedMarkerLead =
            Assert.Single(structuralRoles, summary =>
                summary.Marker34BodyScope == "nested marker-34 replay body" &&
                summary.Marker34RelativeOffset == 6);
        Assert.Equal(nestedBodyOffset + 6, nestedMarkerLead.BodyOffset);
        Protocol03NestedMovementMode06Marker34LongBodyStructuralRoleSummary nestedTagRole =
            Assert.Single(structuralRoles, summary =>
                summary.Marker34BodyScope == "nested marker-34 replay body" &&
                summary.Marker34RelativeOffset == 37);
        Assert.Equal(nestedBodyOffset + 37, nestedTagRole.BodyOffset);
        Assert.Equal(1, nestedTagRole.Interpretations["tag-le 1923"]);
        Protocol03NestedMovementMode06Marker34LongBodyStructuralRoleSummary nestedFxRoleSummary =
            Assert.Single(structuralRoles, summary =>
                summary.Marker34BodyScope == "nested marker-34 replay body" &&
                summary.Marker34RelativeOffset == 54);
        Assert.Equal(nestedBodyOffset + 54, nestedFxRoleSummary.BodyOffset);
        Assert.Equal(1, nestedFxRoleSummary.Interpretations["FX_CHARACTER_HEALTH_DRAIN"]);
        Protocol03NestedMovementMode06Marker34LongBodyStructuralRoleSummary nestedScaffoldRole =
            Assert.Single(structuralRoles, summary =>
                summary.Marker34BodyScope == "nested marker-34 replay body" &&
                summary.Marker34RelativeOffset == 71);
        Assert.Equal(nestedBodyOffset + 71, nestedScaffoldRole.BodyOffset);
        Assert.Equal(1, nestedScaffoldRole.Interpretations["stable scaffold through marker byte 22"]);

        Protocol03NestedMovementMode06Marker34LongBodyFieldWindowSummary[] fieldWindows =
            PacketResearcher.BuildProtocol03NestedMovementMode06Marker34LongBodyFieldWindowSummaries(new[] { interiorFixture, nestedFixture }, new[] { fx })
                .ToArray();
        Protocol03NestedMovementMode06Marker34LongBodyFieldWindowSummary fxWindow =
            Assert.Single(fieldWindows, summary =>
                summary.WindowDisposition == "known effect-style field window" &&
                summary.BodyOffset == 54);
        Assert.Equal(6, fxWindow.WindowBytes);
        Assert.Equal(1, fxWindow.FixtureCount);
        Assert.Equal(1, fxWindow.Interpretations["FX_CHARACTER_HEALTH_DRAIN"]);
        Protocol03NestedMovementMode06Marker34LongBodyFieldWindowSummary movementWindow =
            Assert.Single(fieldWindows, summary =>
                summary.WindowDisposition == "embedded movement payload window" &&
                summary.BodyOffset == 94);
        Assert.Equal(17, movementWindow.WindowBytes);
        Assert.Equal(1, movementWindow.Interpretations["mode 06 selector 0c"]);
        Protocol03NestedMovementMode06Marker34LongBodyFieldWindowSummary replayPrefixWindow =
            Assert.Single(fieldWindows, summary =>
                summary.WindowDisposition == "nested marker-34 replay prefix" &&
                summary.BodyOffset == 148);
        Assert.Equal(nestedPrefix.Length, replayPrefixWindow.WindowBytes);
        Assert.Equal(1, replayPrefixWindow.Interpretations["replay prefix starts nested marker-34 body"]);
        Protocol03NestedMovementMode06Marker34LongBodyFieldWindowSummary nestedTagWindow =
            Assert.Single(fieldWindows, summary =>
                summary.WindowDisposition == "marker-34 tag window" &&
                summary.BodyOffset == nestedBodyOffset + 37);
        Assert.Equal(1, nestedTagWindow.Interpretations["tag-le 1923"]);
        Protocol03NestedMovementMode06Marker34LongBodyFieldWindowSummary nestedScaffoldWindow =
            Assert.Single(fieldWindows, summary =>
                summary.WindowDisposition == "marker-34 stable scaffold window" &&
                summary.BodyOffset == nestedBodyOffset + 71);
        Assert.Equal(1, nestedScaffoldWindow.Interpretations["stable scaffold through marker byte 22"]);

        Protocol03NestedMovementMode06Marker34LongBodyEmbeddedMovementVectorSummary vectorSummary =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06Marker34LongBodyEmbeddedMovementVectorSummaries(new[] { interiorFixture }));
        Assert.Equal("decoded marker-34 embedded movement vector", vectorSummary.MovementDisposition);
        Assert.Equal(94, vectorSummary.LeadBodyOffset);
        Assert.Equal(95, vectorSummary.MarkerBodyOffset);
        Assert.Equal("0e", vectorSummary.LeadByteHex);
        Assert.Equal("0c", vectorSummary.SelectorHex);
        Assert.Equal(13, vectorSummary.InnerPayloadBytes);
        Assert.Equal(1, vectorSummary.InnerPositionOffset);
        Assert.Equal(99, vectorSummary.BodyPositionOffset);
        Assert.Equal(1, vectorSummary.PrePositionBytes["13"]);
        Assert.Equal(1, vectorSummary.PositionHexes["51 10 3b c7 00 a7 8c 42 00 42 f4 c6"]);
        Assert.Equal(1, vectorSummary.DecodedPositions["-47888.32,70.326,-31265"]);
        Assert.Equal(1, vectorSummary.OuterPositionRelations["near outer movement position"]);

        Protocol03NestedMovementMode06Marker34LongBodyLayoutSummary[] layoutSummaries =
            PacketResearcher.BuildProtocol03NestedMovementMode06Marker34LongBodyLayoutSummaries(new[] { interiorFixture, nestedFixture }, new[] { fx })
                .ToArray();
        Protocol03NestedMovementMode06Marker34LongBodyLayoutSummary movementLayout =
            Assert.Single(layoutSummaries, summary => summary.BodyBytes == 160);
        Assert.Equal("marker-34 effect and embedded movement layout", movementLayout.LayoutDisposition);
        Assert.Equal("1748", movementLayout.MarkerTagValue);
        Assert.Equal("01 00 d4 06 ff", movementLayout.MarkerTagWindowHex);
        Assert.Equal("30 04 00 28", movementLayout.MarkerFxHex);
        Assert.Equal("FX_CHARACTER_HEALTH_DRAIN", movementLayout.MarkerFxName);
        Assert.Equal("ff 14", movementLayout.MarkerFxFollowupBytes);
        Assert.Equal("0c", movementLayout.EmbeddedMovementSelectorHex);
        Assert.Equal("-47888.32,70.326,-31265", movementLayout.EmbeddedMovementPosition);
        Assert.Equal("near outer movement position", movementLayout.EmbeddedMovementRelation);
        Assert.Equal("92.157", movementLayout.EmbeddedMovementDistance);
        Assert.Equal(-1, movementLayout.NestedReplayPrefixOffset);
        Assert.Equal(-1, movementLayout.NestedMarker34BodyOffset);
        Protocol03NestedMovementMode06Marker34LongBodyLayoutSummary replayLayout =
            Assert.Single(layoutSummaries, summary => summary.BodyBytes == 265);
        Assert.Equal("marker-34 nested replay layout", replayLayout.LayoutDisposition);
        Assert.Equal(148, replayLayout.NestedReplayPrefixOffset);
        Assert.Equal(nestedBodyOffset, replayLayout.NestedMarker34BodyOffset);
        Assert.Equal("1923", replayLayout.NestedTagValue);
        Assert.Equal("01 00 83 07 ff", replayLayout.NestedTagWindowHex);
        Assert.Equal("30 04 00 28", replayLayout.NestedFxHex);
        Assert.Equal("FX_CHARACTER_HEALTH_DRAIN", replayLayout.NestedFxName);
        Assert.Equal("ff 14", replayLayout.NestedFxFollowupBytes);
        Assert.Equal("76 0c 00 58 37 08 00 00 1f 06 08 00 00 00 00 00 00 22", replayLayout.NestedScaffoldHex);
        Protocol03NestedMovementMode06Marker34LongBodyParserReadinessSummary[] readinessSummaries =
            PacketResearcher.BuildProtocol03NestedMovementMode06Marker34LongBodyParserReadinessSummaries(layoutSummaries)
                .ToArray();
        Assert.Equal(2, readinessSummaries.Length);
        Protocol03NestedMovementMode06Marker34LongBodyParserReadinessSummary movementReadiness =
            Assert.Single(readinessSummaries, summary =>
                summary.ReadinessDisposition == "layout-ready marker-34 embedded movement parser candidate");
        Assert.Equal("singleton layout still needs exact body-length/suffix guard", movementReadiness.PromotionBlocker);
        Assert.Equal(1, movementReadiness.FixtureCount);
        Assert.Equal(1, movementReadiness.BodyByteLengths["160"]);
        Assert.Single(movementReadiness.BodySuffixes);
        Assert.Equal(1, movementReadiness.PostPrefixLeads["00 e9 07 00 00 00 00 34"]);
        Assert.Equal(1, movementReadiness.MarkerBytes["34"]);
        Assert.Equal(1, movementReadiness.MarkerFxs["30 04 00 28 / FX_CHARACTER_HEALTH_DRAIN / ff 14"]);
        Assert.Equal(1, movementReadiness.EmbeddedMovementSelectors["0c"]);
        Protocol03NestedMovementMode06Marker34LongBodyParserReadinessSummary replayReadiness =
            Assert.Single(readinessSummaries, summary =>
                summary.ReadinessDisposition == "layout-ready marker-34 nested replay parser candidate");
        Assert.Equal(1, replayReadiness.NestedReplayOffsets["prefix 148; body 166"]);
        Assert.Equal(1, replayReadiness.PostPrefixLeads["00 e9 07 00 00 00 00 34"]);
        Assert.Equal(1, replayReadiness.MarkerBytes["34"]);

        PacketResearchReport report = new(
            ".",
            Array.Empty<string>(),
            null,
            Array.Empty<string>(),
            Array.Empty<RpcHeaderEntry>(),
            Array.Empty<AttributeDefinition>(),
            new[] { fx },
            Array.Empty<GameObjectEntry>(),
            Array.Empty<WorldEntityEntry>(),
            Array.Empty<RajkoRpcEntry>(),
            Array.Empty<HardcodedCommandExample>(),
            Array.Empty<Protocol03HardcodedExample>(),
            Array.Empty<Protocol04InteractionCommandExample>(),
            Array.Empty<VendorInventoryEntry>(),
            Array.Empty<RpcComparison>(),
            new[] { dump })
        {
            Protocol03NestedMovementMode06Marker34LongBodyFixtureSummaries = new[] { fixture },
            Protocol03NestedMovementMode06Marker34LongBodyOffsetSummaries = offsetSummaries,
            Protocol03NestedMovementMode06Marker34LongBodyInteriorWindowSummaries = interiorWindows,
            Protocol03NestedMovementMode06Marker34LongBodyKnownFxWindowSummaries = new[] { knownFx },
            Protocol03NestedMovementMode06Marker34LongBodyFxFieldRoleSummaries = fxFieldRoles,
            Protocol03NestedMovementMode06Marker34LongBodyStructuralRoleSummaries = structuralRoles,
            Protocol03NestedMovementMode06Marker34LongBodyLayoutSummaries = layoutSummaries,
            Protocol03NestedMovementMode06Marker34LongBodyParserReadinessSummaries = readinessSummaries,
            Protocol03NestedMovementMode06Marker34LongBodyNestedReplaySummaries = new[] { nestedReplay },
            Protocol03NestedMovementMode06Marker34LongBodyFieldWindowSummaries = fieldWindows,
            Protocol03NestedMovementMode06Marker34LongBodyEmbeddedMovementVectorSummaries = new[] { vectorSummary }
        };
        string markdown = ReportWriter.ToMarkdown(report);
        Assert.Contains("### Protocol 03 Mode 06 Marker-34 Long Body Fixtures", markdown);
        Assert.Contains("| hold mode 06 marker-34 long object-view tuple body as evidence only | variable-length marker-34 object-view body needs body-length rule | 110 |", markdown);
        Assert.Contains("tail 17; continuation 33; body 72; boundary 182; post-continuation 149", markdown);
        Assert.Contains("### Protocol 03 Mode 06 Marker-34 Long Body Offset Scaffold", markdown);
        Assert.Contains("| 6 | stable nonzero byte | 2 | 2 | ff (2) |", markdown);
        Assert.Contains("### Protocol 03 Mode 06 Marker-34 Long Body Interior Windows", markdown);
        Assert.Contains("| embedded mode 06 movement window | 95 | `00 06 0c` | 1 | 160 (1) |", markdown);
        Assert.Contains("| embedded marker-34 prefix-field lead | 148 | `00 02 ff` | 1 | 160 (1) |", markdown);
        Assert.Contains("### Protocol 03 Mode 06 Marker-34 Long Body Known FX Windows", markdown);
        Assert.Contains("| 54 | `30 04 00 28` | FX_CHARACTER_HEALTH_DRAIN | 1 | ff 14 (1) |", markdown);
        Assert.Contains("### Protocol 03 Mode 06 Marker-34 Long Body FX Field Roles", markdown);
        Assert.Contains("| marker-34 effect field at relative offset 54 | outer marker-34 body @ 0 | 54 | 54 | `30 04 00 28` | FX_CHARACTER_HEALTH_DRAIN | 1 |", markdown);
        Assert.Contains("| marker-34 effect field at relative offset 54 | nested marker-34 replay body @ 166 | 220 | 54 | `30 04 00 28` | FX_CHARACTER_HEALTH_DRAIN | 1 |", markdown);
        Assert.Contains("### Protocol 03 Mode 06 Marker-34 Long Body Structural Roles", markdown);
        Assert.Contains("| marker-34 marker lead | outer marker-34 body @ 0 | 6 | 6 | 2 | 1 | ff 00 (1) |", markdown);
        Assert.Contains("| marker-34 tag field | nested marker-34 replay body @ 166 | 203 | 37 | 5 | 1 | 01 00 83 07 ff (1) | tag-le 1923 (1) |", markdown);
        Assert.Contains("| marker-34 stable scaffold field | nested marker-34 replay body @ 166 | 237 | 71 | 18 | 1 |", markdown);
        Assert.Contains("### Protocol 03 Mode 06 Marker-34 Long Body Layouts", markdown);
        Assert.Contains("| marker-34 effect and embedded movement layout | hold mode 06 marker-34 long object-view tuple body as evidence only | variable-length marker-34 object-view body needs body-length rule | 160 | 1748 / `01 00 d4 06 ff` | `30 04 00 28` / FX_CHARACTER_HEALTH_DRAIN / `ff 14` |", markdown);
        Assert.Contains("selector `0c` / -47888.32,70.326,-31265 / near outer movement position / 92.157", markdown);
        Assert.Contains("| marker-34 nested replay layout | hold mode 06 marker-34 long object-view tuple body as evidence only | variable-length marker-34 object-view body needs body-length rule | 265 | - / `-` | - | `-` | - | prefix 148; body 166 | 1923 / `01 00 83 07 ff` | `30 04 00 28` / FX_CHARACTER_HEALTH_DRAIN / `ff 14` |", markdown);
        Assert.Contains("### Protocol 03 Mode 06 Marker-34 Long Body Parser Readiness", markdown);
        Assert.Contains("| layout-ready marker-34 embedded movement parser candidate | singleton layout still needs exact body-length/suffix guard | 1 |", markdown);
        Assert.Contains("| layout-ready marker-34 nested replay parser candidate | singleton layout still needs exact body-length/suffix guard | 1 |", markdown);
        Assert.Contains("### Protocol 03 Mode 06 Marker-34 Long Body Nested Replay Candidates", markdown);
        Assert.Contains("| nested marker-34 body replay with stable scaffold | 148 | 166 | 99 |", markdown);
        Assert.Contains("12/12: 6:ff 37:01 41:ff 58:ff", markdown);
        Assert.Contains("### Protocol 03 Mode 06 Marker-34 Long Body Field Windows", markdown);
        Assert.Contains("| embedded movement payload window | 94 | 17 | 1 |", markdown);
        Assert.Contains("| nested marker-34 replay prefix | 148 | 18 | 1 |", markdown);
        Assert.Contains("### Protocol 03 Mode 06 Marker-34 Long Body Embedded Movement Vectors", markdown);
        Assert.Contains("| decoded marker-34 embedded movement vector | 94 | 95 | `0e` | `0c` | 13 | 99 | 1 |", markdown);
    }

    [Fact]
    public void DetectProtocol03ObjectViews_BoundsGuardedMode06Marker34EffectScaffoldObjectViewTupleBody()
    {
        byte[] body = new byte[110];
        body[6] = 0xff;
        body[37] = 0x01;
        body[39] = 0x79;
        body[40] = 0x07;
        body[41] = 0xff;
        body[54] = 0x9c;
        body[55] = 0x05;
        body[57] = 0x28;
        body[58] = 0xff;
        body[59] = 0x0e;
        body[71] = 0x76;
        body[72] = 0x0c;
        body[74] = 0x58;
        body[75] = 0x37;
        body[76] = 0x08;
        body[79] = 0x1f;
        body[80] = 0x06;
        body[81] = 0x08;
        body[88] = 0x22;
        byte[] suffix = PacketResearcher.ParseHexBytes("00 00 04 01 06 fa 01 3e 81 67 17 00 27 00 fa 00").ToArray();
        Array.Copy(suffix, 0, body, body.Length - suffix.Length, suffix.Length);

        var bytes = new List<byte>
        {
            0x02, 0x03,
            0x01, 0x00, 0x02, 0x0c
        };
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 13));
        bytes.Add(0x03);
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 06 0e 01 18 58 3a f6 c6 00 00 be 42 44 35 af 46"));
        bytes.AddRange(PacketResearcher.ParseHexBytes("ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 22));
        bytes.AddRange(PacketResearcher.ParseHexBytes("02 ff"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 7));
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 e9 07 00 00 00 00 34"));
        bytes.AddRange(body);
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 7c 01 00 10 00 00 00"));

        Protocol03ObjectViewSample[] samples = PacketResearcher.DetectProtocol03ObjectViews(bytes, 545, "server").ToArray();

        const string expectedParserAction = "test guarded mode 06 marker-34 effect/scaffold object-view tuple body";
        const int expectedPayloadBytes = 17 + 55 + 110;
        Assert.Equal(2, samples.Length);
        Assert.True(samples[0].Complete);
        Assert.Equal(2, samples[0].ParsedUpdateCount);
        Assert.Equal(0, samples[0].UnparsedBytes);
        Assert.Collection(
            samples[0].Segments,
            segment =>
            {
                Assert.Equal("0c", segment.Selector);
                Assert.Equal("position xyz + flag", segment.Classification);
                Assert.Equal(13, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            },
            segment =>
            {
                Assert.Equal("03", segment.Selector);
                Assert.Equal("mode 06 guarded marker-34 effect/scaffold object-view tuple body", segment.Classification);
                Assert.Equal(expectedPayloadBytes, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            });

        Protocol03NestedMovementConsumedBodyBoundary boundary = Assert.Single(samples[0].NestedMovementConsumedBodyBoundaries);
        Assert.Equal(expectedParserAction, boundary.ParserAction);
        Assert.Equal("03", boundary.Selector);
        Assert.Equal(17, boundary.Offset);
        Assert.Equal(expectedPayloadBytes, boundary.PayloadBytes);
        Assert.Equal(expectedPayloadBytes + 8, boundary.EvidencePayloadBytes);
        Assert.Equal(17, boundary.TailStartOffset);
        Assert.Equal(165, boundary.TailBoundaryOffset);
        Assert.Equal(110, boundary.PostTupleBodyBytes);
        Assert.Equal("unclassified object-view tuple", boundary.TupleLayoutKind);
        Assert.Equal(6, boundary.FirstNonZeroOffset);
        Assert.Equal("ff 00", boundary.FirstNonZeroFieldHex);
        Assert.Equal("00 7c 01 00 10 00 00 00", boundary.BoundaryHeaderHex);
        Assert.Equal("single state marker update", boundary.BoundaryHeaderClassification);

        static IReadOnlyDictionary<string, int> SingleValue(string value)
        {
            return new Dictionary<string, int> { [value] = 1 };
        }

        Protocol03NestedMovementMode06PostContinuationTupleBodySummary tupleBody = new(
            "ff marker plus zero-padded ff continuation lead",
            "02 ff",
            "unclassified object-view tuple",
            "unclassified object-view update",
            "nonzero post-tuple body",
            110,
            "00 00 00 00 00 00 ff 00 00 00 00 00 00 00 00 00",
            "00 00 04 01 06 fa 01 3e 81 67 17 00 27 00 fa 00",
            "-",
            "-",
            null,
            "-",
            "-",
            "-",
            6,
            "ff 00",
            1,
            SingleValue("single state marker update"),
            SingleValue("00 7c 01 00 10 00 00 00"),
            SingleValue("165"),
            SingleValue("143"),
            SingleValue("00 e9 07 00 00 00 00 34"),
            SingleValue("e9"),
            SingleValue("34"),
            SingleValue("17"),
            SingleValue("33"),
            SingleValue("182"),
            SingleValue("0e"),
            SingleValue("09"),
            SingleValue("unclassified object-view update"),
            SingleValue("237"),
            SingleValue("-47888.32,70.326,-31265"),
            "mode06-guarded-marker-34-effect-scaffold.txt",
            1);

        Protocol03NestedMovementMode06TupleBodyParserActionSummary parserAction =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06TupleBodyParserActionSummaries(new[] { tupleBody }));
        Assert.Equal(expectedParserAction, parserAction.ParserAction);
        Assert.Equal("marker-34 effect/scaffold body before exact state-marker boundary", parserAction.ActionReason);
        Assert.Equal(1, parserAction.BodyByteLengths["110"]);
        Assert.Equal(1, parserAction.OuterSelectors["09"]);
    }

    [Fact]
    public void DetectProtocol03ObjectViews_BoundsGuardedMode06Marker34NestedReplayObjectViewTupleBody()
    {
        byte[] bytes = PacketResearcher.ParseHexBytes(
            "02 03 11 00 02 0e 00 5a da 63 52 c7 00 00 be 42 54 7b d2 c6 10 00 06 0c ff c1 ba 3a c7 00 68 10 " +
            "42 00 42 f4 c6 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 " +
            "00 00 00 00 00 00 00 00 00 00 00 02 ff cd cc 4c be 00 00 00 00 e9 07 00 00 00 00 34 00 00 00 00 " +
            "00 00 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 " +
            "00 01 00 d4 06 ff 00 00 00 00 00 00 00 00 00 00 00 00 30 04 00 28 ff 09 00 00 00 00 00 00 00 00 " +
            "00 00 00 76 0c 00 58 37 08 00 00 1f 06 08 00 00 00 00 10 00 22 00 00 00 00 00 0e 00 06 0c 13 51 " +
            "10 3b c7 00 a7 8c 42 00 42 f4 c6 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 " +
            "00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 02 ff cd cc 4c be 00 00 00 00 e9 07 00 00 00 " +
            "00 34 00 00 00 00 00 00 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 " +
            "00 00 00 00 00 00 00 01 00 83 07 ff 00 00 00 00 00 00 00 00 00 00 00 00 9c 05 00 28 ff 0e 00 00 " +
            "00 00 00 00 00 00 00 00 00 76 0c 00 58 37 08 00 00 1f 06 08 00 00 00 00 00 00 22 00 00 00 00 00 " +
            "00 00 04 01 06 fb 02 04 80 b3 d5 04 08 80 b2 d5 04 00 00 08 02")
            .ToArray();

        Protocol03ObjectViewSample[] samples = PacketResearcher.DetectProtocol03ObjectViews(bytes, 546, "server").ToArray();

        const string expectedParserAction = "test guarded mode 06 marker-34 nested replay object-view tuple body";
        Protocol03ObjectUpdateSegment segment = Assert.Single(
            samples.SelectMany(sample => sample.Segments),
            segment => segment.Classification.Equals("mode 06 guarded marker-34 nested replay object-view tuple body", StringComparison.Ordinal));
        Assert.Equal("10", segment.Selector);
        Assert.True(segment.IsKnownLength);

        Protocol03NestedMovementConsumedBodyBoundary boundary = Assert.Single(
            samples.SelectMany(sample => sample.NestedMovementConsumedBodyBoundaries),
            boundary => boundary.ParserAction.Equals(expectedParserAction, StringComparison.Ordinal));
        Assert.Equal(expectedParserAction, boundary.ParserAction);
        Assert.Equal("10", boundary.Selector);
        Assert.Equal(18, boundary.Offset);
        Assert.Equal(336, boundary.PayloadBytes);
        Assert.Equal(344, boundary.EvidencePayloadBytes);
        Assert.Equal(16, boundary.TailStartOffset);
        Assert.Equal(320, boundary.TailBoundaryOffset);
        Assert.Equal(265, boundary.PostTupleBodyBytes);
        Assert.Equal("unclassified object-view tuple", boundary.TupleLayoutKind);
        Assert.Equal(6, boundary.FirstNonZeroOffset);
        Assert.Equal("ff 00", boundary.FirstNonZeroFieldHex);
        Assert.Equal("fb 02 04 80 b3 d5 04 08", boundary.BoundaryHeaderHex);
        Assert.Equal("attribute/effect bundle", boundary.BoundaryHeaderClassification);

        PacketDumpFileSummary dump = new(
            "mode06-guarded-marker-34-nested-replay.txt",
            1,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            Array.Empty<Protocol04PacketSequenceSample>(),
            samples,
            Array.Empty<Protocol04InteractionPayloadSample>(),
            Array.Empty<PlayerAttributePayloadSample>(),
            Array.Empty<ManageBonusPayloadSample>());

        Protocol03NestedMovementMode06PostContinuationTupleBodySummary tupleBody =
            Assert.Single(
                PacketResearcher.BuildProtocol03NestedMovementMode06PostContinuationTupleBodySummaries(new[] { dump }),
                summary => summary.PostTupleBodyBytes == 265);
        Assert.Equal("ff marker plus zero-padded ff continuation lead", tupleBody.TailPrefixKind);
        Assert.Equal("02 ff", tupleBody.PrefixFirstNonZeroFieldHex);
        Assert.Equal("unclassified object-view tuple", tupleBody.TupleLayoutKind);
        Assert.Equal("unclassified object-view update", tupleBody.PostPrefixDisposition);
        Assert.Equal(265, tupleBody.PostTupleBodyBytes);
        Assert.Equal("00 00 00 00 00 22 00 00 00 00 00 00 00 04 01 06", tupleBody.BodySuffixHex);
        Assert.Equal("fb 02 04 80 b3 d5 04 08", Assert.Single(tupleBody.HeaderPrefixes).Key);

        Protocol03NestedMovementMode06TupleBodyParserActionSummary parserAction =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06TupleBodyParserActionSummaries(new[] { tupleBody }));
        Assert.Equal(expectedParserAction, parserAction.ParserAction);
        Assert.Equal("265-byte marker-34 nested replay body before exact attribute/effect boundary", parserAction.ActionReason);
        Assert.Equal(1, parserAction.BodyByteLengths["265"]);
        Assert.Equal(1, parserAction.OuterSelectors["10"]);
    }

    [Fact]
    public void DetectProtocol03ObjectViews_BoundsGuardedMode06123ByteMarker34EmbeddedMovementObjectViewTupleBody()
    {
        byte[] body = new byte[123];
        body[6] = 0xff;
        body[37] = 0x01;
        body[38] = 0x00;
        body[39] = 0xf5;
        body[40] = 0x06;
        body[41] = 0xff;
        body[71] = 0x75;
        body[72] = 0x0c;
        body[74] = 0x58;
        body[75] = 0x37;
        body[76] = 0x08;
        body[79] = 0x1f;
        body[80] = 0x06;
        body[81] = 0x08;
        body[88] = 0x22;
        body[94] = 0x21;
        body[95] = 0x00;
        body[96] = 0x06;
        body[97] = 0x0e;
        body[98] = 0x05;
        body[99] = 0x98;
        byte[] movementAndSuffix = PacketResearcher.ParseHexBytes(
            "26 35 45 47 00 20 8a c4 02 4c 3d c7 80 80 80 80 80 80 01 12 08 00 00")
            .ToArray();
        Array.Copy(movementAndSuffix, 0, body, 100, movementAndSuffix.Length);

        var bytes = new List<byte>
        {
            0x02, 0x03,
            0x01, 0x00, 0x02, 0x0c
        };
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 13));
        bytes.Add(0x03);
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 06 0e 01 18 58 3a f6 c6 00 00 be 42 44 35 af 46"));
        bytes.AddRange(PacketResearcher.ParseHexBytes("ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 22));
        bytes.AddRange(PacketResearcher.ParseHexBytes("02 ff"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 7));
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 e9 07 00 00 00 00 34"));
        bytes.AddRange(body);
        bytes.AddRange(PacketResearcher.ParseHexBytes("15 00 01 00 04 be 08 3c"));

        Protocol03ObjectViewSample[] samples = PacketResearcher.DetectProtocol03ObjectViews(bytes, 544, "server").ToArray();

        const string expectedParserAction = "test guarded mode 06 123-byte marker-34 embedded movement object-view tuple body";
        const int expectedPayloadBytes = 17 + 55 + 123;
        Assert.Equal(2, samples.Length);
        Assert.True(samples[0].Complete);
        Assert.Equal(2, samples[0].ParsedUpdateCount);
        Assert.Equal(0, samples[0].UnparsedBytes);
        Assert.Collection(
            samples[0].Segments,
            segment =>
            {
                Assert.Equal("0c", segment.Selector);
                Assert.Equal("position xyz + flag", segment.Classification);
                Assert.Equal(13, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            },
            segment =>
            {
                Assert.Equal("03", segment.Selector);
                Assert.Equal("mode 06 guarded 123-byte marker-34 embedded movement object-view tuple body", segment.Classification);
                Assert.Equal(expectedPayloadBytes, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            });

        Protocol03NestedMovementConsumedBodyBoundary boundary = Assert.Single(samples[0].NestedMovementConsumedBodyBoundaries);
        Assert.Equal(expectedParserAction, boundary.ParserAction);
        Assert.Equal("03", boundary.Selector);
        Assert.Equal(17, boundary.Offset);
        Assert.Equal(expectedPayloadBytes, boundary.PayloadBytes);
        Assert.Equal(expectedPayloadBytes + 8, boundary.EvidencePayloadBytes);
        Assert.Equal(17, boundary.TailStartOffset);
        Assert.Equal(178, boundary.TailBoundaryOffset);
        Assert.Equal(123, boundary.PostTupleBodyBytes);
        Assert.Equal("unclassified object-view tuple", boundary.TupleLayoutKind);
        Assert.Equal(6, boundary.FirstNonZeroOffset);
        Assert.Equal("ff 00", boundary.FirstNonZeroFieldHex);
        Assert.Equal("15 00 01 00 04 be 08 3c", boundary.BoundaryHeaderHex);
        Assert.Equal("single state marker update", boundary.BoundaryHeaderClassification);

        static IReadOnlyDictionary<string, int> SingleValue(string value)
        {
            return new Dictionary<string, int> { [value] = 1 };
        }

        Protocol03NestedMovementMode06PostContinuationTupleBodySummary tupleBody = new(
            "ff marker plus zero-padded ff continuation lead",
            "02 ff",
            "unclassified object-view tuple",
            "unclassified object-view update",
            "nonzero post-tuple body",
            123,
            "00 00 00 00 00 00 ff 00 00 00 00 00 00 00 00 00",
            "c4 02 4c 3d c7 80 80 80 80 80 80 01 12 08 00 00",
            "-",
            "-",
            null,
            "-",
            "-",
            "-",
            6,
            "ff 00",
            1,
            SingleValue("single state marker update"),
            SingleValue("15 00 01 00 04 be 08 3c"),
            SingleValue("178"),
            SingleValue("156"),
            SingleValue("00 e9 07 00 00 00 00 34"),
            SingleValue("e9"),
            SingleValue("34"),
            SingleValue("17"),
            SingleValue("33"),
            SingleValue("195"),
            SingleValue("0e"),
            SingleValue("0b"),
            SingleValue("movement/state update"),
            SingleValue("195"),
            SingleValue("50485.15,-1105,-48460.01"),
            "mode06-guarded-marker-34-embedded-movement-123.txt",
            1);

        Protocol03NestedMovementMode06TupleBodyParserActionSummary parserAction =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06TupleBodyParserActionSummaries(new[] { tupleBody }));
        Assert.Equal(expectedParserAction, parserAction.ParserAction);
        Assert.Equal("123-byte marker-34 body with decoded embedded movement before paired state-marker boundary", parserAction.ActionReason);
        Assert.Equal(1, parserAction.BodyByteLengths["123"]);
        Assert.Equal(1, parserAction.OuterSelectors["0b"]);
    }

    [Fact]
    public void DetectProtocol03ObjectViews_BoundsGuardedMode06158ByteMarker34ObjectViewTupleBody()
    {
        byte[] body = Enumerable.Repeat((byte)0x00, 6)
            .Concat(new byte[] { 0xff, 0x00 })
            .Concat(Enumerable.Repeat((byte)0x00, 134))
            .Concat(PacketResearcher.ParseHexBytes("00 00 00 00 00 00 00 02 ff 00 00 00 00 00 00 00"))
            .ToArray();
        var bytes = new List<byte>
        {
            0x02, 0x03,
            0x01, 0x00, 0x02, 0x0c
        };
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 13));
        bytes.Add(0x03);
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 06 0e 01 18 58 3a f6 c6 00 00 be 42 44 35 af 46"));
        bytes.AddRange(PacketResearcher.ParseHexBytes("ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 22));
        bytes.AddRange(PacketResearcher.ParseHexBytes("02 ff"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 7));
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 e9 07 00 00 00 00 34"));
        bytes.AddRange(body);
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 58 02 00 00 00 00 0e"));

        Protocol03ObjectViewSample[] samples = PacketResearcher.DetectProtocol03ObjectViews(bytes, 543, "server").ToArray();

        const string expectedParserAction = "test guarded mode 06 158-byte marker-34 object-view tuple body";
        const int expectedPayloadBytes = 17 + 55 + 158;
        Assert.Equal(2, samples.Length);
        Assert.True(samples[0].Complete);
        Assert.Equal(2, samples[0].ParsedUpdateCount);
        Assert.Equal(0, samples[0].UnparsedBytes);
        Assert.Collection(
            samples[0].Segments,
            segment =>
            {
                Assert.Equal("0c", segment.Selector);
                Assert.Equal("position xyz + flag", segment.Classification);
                Assert.Equal(13, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            },
            segment =>
            {
                Assert.Equal("03", segment.Selector);
                Assert.Equal("mode 06 guarded 158-byte marker-34 object-view tuple body", segment.Classification);
                Assert.Equal(expectedPayloadBytes, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            });
        Assert.True(samples[1].Complete);
        Assert.Equal(0x5800, samples[1].ViewId);
        Assert.Equal("state marker/object update candidate", samples[1].Classification);

        Protocol03NestedMovementConsumedBodyBoundary boundary = Assert.Single(samples[0].NestedMovementConsumedBodyBoundaries);
        Assert.Equal(expectedParserAction, boundary.ParserAction);
        Assert.Equal("03", boundary.Selector);
        Assert.Equal(17, boundary.Offset);
        Assert.Equal(expectedPayloadBytes, boundary.PayloadBytes);
        Assert.Equal(expectedPayloadBytes + 8, boundary.EvidencePayloadBytes);
        Assert.Equal(17, boundary.TailStartOffset);
        Assert.Equal(213, boundary.TailBoundaryOffset);
        Assert.Equal(158, boundary.PostTupleBodyBytes);
        Assert.Equal("unclassified object-view tuple", boundary.TupleLayoutKind);
        Assert.Equal(6, boundary.FirstNonZeroOffset);
        Assert.Equal("ff 00", boundary.FirstNonZeroFieldHex);
        Assert.Equal("00 58 02 00 00 00 00 0e", boundary.BoundaryHeaderHex);
        Assert.Equal("state marker/object update candidate", boundary.BoundaryHeaderClassification);

        PacketDumpFileSummary dump = new(
            "mode06-guarded-marker-34-object-view-158.txt",
            1,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            Array.Empty<Protocol04PacketSequenceSample>(),
            new[] { samples[0] },
            Array.Empty<Protocol04InteractionPayloadSample>(),
            Array.Empty<PlayerAttributePayloadSample>(),
            Array.Empty<ManageBonusPayloadSample>());

        Protocol03NestedMovementMode06PostContinuationTupleBodySummary tupleBody =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06PostContinuationTupleBodySummaries(new[] { dump }));
        Assert.Equal("ff marker plus zero-padded ff continuation lead", tupleBody.TailPrefixKind);
        Assert.Equal("02 ff", tupleBody.PrefixFirstNonZeroFieldHex);
        Assert.Equal("unclassified object-view tuple", tupleBody.TupleLayoutKind);
        Assert.Equal("unclassified object-view update", tupleBody.PostPrefixDisposition);
        Assert.Equal(158, tupleBody.PostTupleBodyBytes);
        Assert.Equal("00 00 00 00 00 00 00 02 ff 00 00 00 00 00 00 00", tupleBody.BodySuffixHex);
        Assert.Equal(6, tupleBody.FirstNonZeroOffset);
        Assert.Equal("ff 00", tupleBody.FirstNonZeroFieldHex);
        Assert.Equal(1, tupleBody.HeaderPrefixes["00 58 02 00 00 00 00 0e"]);
        Assert.Equal(1, tupleBody.PostPrefixLeads["00 e9 07 00 00 00 00 34"]);

        Protocol03NestedMovementMode06TupleBodyParserActionSummary parserAction =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06TupleBodyParserActionSummaries(new[] { tupleBody }));
        Assert.Equal(expectedParserAction, parserAction.ParserAction);
        Assert.Equal("158-byte marker-34 object-view body before 00 58 state/object boundary", parserAction.ActionReason);
        Assert.Equal(1, parserAction.BodyByteLengths["158"]);

        Protocol03NestedMovementMode06LongTupleBodyTailAnchorSummary longAnchor =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06LongTupleBodyTailAnchorSummaries(
                new[] { tupleBody },
                Array.Empty<Protocol03NestedMovementMode06PostContinuationTerminalTupleBodySummary>()));
        Assert.Equal("bounded before accepted object-view header", longAnchor.SourceDisposition);
        Assert.Equal(158, longAnchor.BodyBytes);
        Assert.Equal(1, longAnchor.WindowCount);
        Assert.Equal(1, longAnchor.TailAnchorKinds["unknown long tuple body suffix"]);
        Assert.Equal(1, longAnchor.InferredTrailingBodyBytes["-"]);
        Assert.Equal(1, longAnchor.PreAnchorBodyBytes["-"]);
        Assert.Equal(1, longAnchor.PreAnchorBodyPrefixes["-"]);
        Assert.Equal(1, longAnchor.PreAnchorBodySuffixes["-"]);
        Assert.Equal(1, longAnchor.PreAnchorFirstNonZeroOffsets["-"]);
        Assert.Equal(1, longAnchor.PreAnchorFirstNonZeroFields["-"]);
        Assert.Equal(1, longAnchor.PreAnchorTrailingLeads["-"]);
        Assert.Equal(1, longAnchor.PreAnchorPreLeadBytes["-"]);
        Assert.Equal(1, longAnchor.BodySuffixes["00 00 00 00 00 00 00 02 ff 00 00 00 00 00 00 00"]);
        Assert.Equal(1, longAnchor.HeaderPrefixes["00 58 02 00 00 00 00 0e"]);
    }

    [Fact]
    public void DetectProtocol03ObjectViews_BoundsGuardedMode06158ByteMarker16ObjectViewTupleBody()
    {
        byte[] body = Enumerable.Repeat((byte)0x00, 6)
            .Concat(new byte[] { 0xff, 0x00 })
            .Concat(Enumerable.Repeat((byte)0x00, 134))
            .Concat(PacketResearcher.ParseHexBytes("00 00 00 00 00 00 00 03 ff 00 00 00 00 00 00 00"))
            .ToArray();
        var bytes = new List<byte>
        {
            0x02, 0x03,
            0x01, 0x00, 0x02, 0x0c
        };
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 13));
        bytes.Add(0x03);
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 06 0e 01 18 58 3a f6 c6 00 00 be 42 44 35 af 46"));
        bytes.AddRange(PacketResearcher.ParseHexBytes("ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 22));
        bytes.AddRange(PacketResearcher.ParseHexBytes("01 ff"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 7));
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 b0 04 00 00 00 00 16"));
        bytes.AddRange(body);
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 58 02 00 00 00 00 16"));

        Protocol03ObjectViewSample[] samples = PacketResearcher.DetectProtocol03ObjectViews(bytes, 544, "server").ToArray();

        const string expectedParserAction = "test guarded mode 06 158-byte marker-16 object-view tuple body";
        const int expectedPayloadBytes = 17 + 55 + 158;
        Assert.Equal(2, samples.Length);
        Assert.True(samples[0].Complete);
        Assert.Equal(2, samples[0].ParsedUpdateCount);
        Assert.Equal(0, samples[0].UnparsedBytes);
        Assert.Collection(
            samples[0].Segments,
            segment =>
            {
                Assert.Equal("0c", segment.Selector);
                Assert.Equal("position xyz + flag", segment.Classification);
                Assert.Equal(13, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            },
            segment =>
            {
                Assert.Equal("03", segment.Selector);
                Assert.Equal("mode 06 guarded 158-byte marker-16 object-view tuple body", segment.Classification);
                Assert.Equal(expectedPayloadBytes, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            });
        Assert.Equal(0x5800, samples[1].ViewId);
        Assert.Equal("state marker/object update candidate", samples[1].Classification);

        Protocol03NestedMovementConsumedBodyBoundary boundary = Assert.Single(samples[0].NestedMovementConsumedBodyBoundaries);
        Assert.Equal(expectedParserAction, boundary.ParserAction);
        Assert.Equal("03", boundary.Selector);
        Assert.Equal(17, boundary.Offset);
        Assert.Equal(expectedPayloadBytes, boundary.PayloadBytes);
        Assert.Equal(expectedPayloadBytes + 8, boundary.EvidencePayloadBytes);
        Assert.Equal(17, boundary.TailStartOffset);
        Assert.Equal(213, boundary.TailBoundaryOffset);
        Assert.Equal(158, boundary.PostTupleBodyBytes);
        Assert.Equal("unclassified object-view tuple", boundary.TupleLayoutKind);
        Assert.Equal(6, boundary.FirstNonZeroOffset);
        Assert.Equal("ff 00", boundary.FirstNonZeroFieldHex);
        Assert.Equal("00 58 02 00 00 00 00 16", boundary.BoundaryHeaderHex);
        Assert.Equal("state marker/object update candidate", boundary.BoundaryHeaderClassification);

        PacketDumpFileSummary dump = new(
            "mode06-guarded-marker-16-object-view-158.txt",
            1,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            Array.Empty<Protocol04PacketSequenceSample>(),
            new[] { samples[0] },
            Array.Empty<Protocol04InteractionPayloadSample>(),
            Array.Empty<PlayerAttributePayloadSample>(),
            Array.Empty<ManageBonusPayloadSample>());

        Protocol03NestedMovementMode06PostContinuationTupleBodySummary tupleBody =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06PostContinuationTupleBodySummaries(new[] { dump }));
        Assert.Equal("ff marker plus zero-padded ff continuation lead", tupleBody.TailPrefixKind);
        Assert.Equal("01 ff", tupleBody.PrefixFirstNonZeroFieldHex);
        Assert.Equal("unclassified object-view tuple", tupleBody.TupleLayoutKind);
        Assert.Equal("unclassified object-view update", tupleBody.PostPrefixDisposition);
        Assert.Equal(158, tupleBody.PostTupleBodyBytes);
        Assert.Equal("00 00 00 00 00 00 00 03 ff 00 00 00 00 00 00 00", tupleBody.BodySuffixHex);
        Assert.Equal(6, tupleBody.FirstNonZeroOffset);
        Assert.Equal("ff 00", tupleBody.FirstNonZeroFieldHex);
        Assert.Equal(1, tupleBody.HeaderPrefixes["00 58 02 00 00 00 00 16"]);
        Assert.Equal(1, tupleBody.PostPrefixLeads["00 b0 04 00 00 00 00 16"]);

        Protocol03NestedMovementMode06TupleBodyParserActionSummary parserAction =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06TupleBodyParserActionSummaries(new[] { tupleBody }));
        Assert.Equal(expectedParserAction, parserAction.ParserAction);
        Assert.Equal("158-byte marker-16 object-view body before 00 58 state/object boundary", parserAction.ActionReason);
        Assert.Equal(1, parserAction.BodyByteLengths["158"]);

        Protocol03NestedMovementMode06LongTupleBodyTailAnchorSummary longAnchor =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06LongTupleBodyTailAnchorSummaries(
                new[] { tupleBody },
                Array.Empty<Protocol03NestedMovementMode06PostContinuationTerminalTupleBodySummary>()));
        Assert.Equal("bounded before accepted object-view header", longAnchor.SourceDisposition);
        Assert.Equal(158, longAnchor.BodyBytes);
        Assert.Equal(1, longAnchor.WindowCount);
        Assert.Equal(1, longAnchor.BodySuffixes["00 00 00 00 00 00 00 03 ff 00 00 00 00 00 00 00"]);
        Assert.Equal(1, longAnchor.HeaderPrefixes["00 58 02 00 00 00 00 16"]);
    }

    [Theory]
    [InlineData("01 ff", "00 b0 04 00 00 00 00 16", "00 58 02 00 00 00 00 16", 0x5800)]
    [InlineData("02 ff", "00 cf 03 00 00 00 00 18", "00 8a 02 00 00 00 00 18", 0x8a00)]
    public void DetectProtocol03ObjectViews_BoundsGuardedMode06159ByteMarker16And18ObjectViewTupleBody(
        string prefixFirstNonZeroFieldHex,
        string postPrefixLeadHex,
        string boundaryHeaderHex,
        int expectedBoundaryViewId)
    {
        byte[] body = Enumerable.Repeat((byte)0x00, 6)
            .Concat(new byte[] { 0xff, 0x00 })
            .Concat(Enumerable.Repeat((byte)0x00, 135))
            .Concat(PacketResearcher.ParseHexBytes("00 00 00 00 00 00 00 03 ff 00 00 00 00 00 00 00"))
            .ToArray();
        var bytes = new List<byte>
        {
            0x02, 0x03,
            0x01, 0x00, 0x02, 0x0c
        };
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 13));
        bytes.Add(0x03);
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 06 0e 01 18 58 3a f6 c6 00 00 be 42 44 35 af 46"));
        bytes.AddRange(PacketResearcher.ParseHexBytes("ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 22));
        bytes.AddRange(PacketResearcher.ParseHexBytes(prefixFirstNonZeroFieldHex));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 7));
        bytes.AddRange(PacketResearcher.ParseHexBytes(postPrefixLeadHex));
        bytes.AddRange(body);
        bytes.AddRange(PacketResearcher.ParseHexBytes(boundaryHeaderHex));

        Protocol03ObjectViewSample[] samples = PacketResearcher.DetectProtocol03ObjectViews(bytes, 544, "server").ToArray();

        const string expectedParserAction = "test guarded mode 06 159-byte marker-16-18 object-view tuple body";
        const int expectedPayloadBytes = 17 + 55 + 159;
        Assert.Equal(2, samples.Length);
        Assert.True(samples[0].Complete);
        Assert.Equal(2, samples[0].ParsedUpdateCount);
        Assert.Equal(0, samples[0].UnparsedBytes);
        Assert.Collection(
            samples[0].Segments,
            segment =>
            {
                Assert.Equal("0c", segment.Selector);
                Assert.Equal("position xyz + flag", segment.Classification);
                Assert.Equal(13, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            },
            segment =>
            {
                Assert.Equal("03", segment.Selector);
                Assert.Equal("mode 06 guarded 159-byte marker-16/18 object-view tuple body", segment.Classification);
                Assert.Equal(expectedPayloadBytes, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            });
        Assert.Equal(expectedBoundaryViewId, samples[1].ViewId);
        Assert.Equal("state marker/object update candidate", samples[1].Classification);

        Protocol03NestedMovementConsumedBodyBoundary boundary = Assert.Single(samples[0].NestedMovementConsumedBodyBoundaries);
        Assert.Equal(expectedParserAction, boundary.ParserAction);
        Assert.Equal("03", boundary.Selector);
        Assert.Equal(17, boundary.Offset);
        Assert.Equal(expectedPayloadBytes, boundary.PayloadBytes);
        Assert.Equal(expectedPayloadBytes + 8, boundary.EvidencePayloadBytes);
        Assert.Equal(17, boundary.TailStartOffset);
        Assert.Equal(214, boundary.TailBoundaryOffset);
        Assert.Equal(159, boundary.PostTupleBodyBytes);
        Assert.Equal("unclassified object-view tuple", boundary.TupleLayoutKind);
        Assert.Equal(6, boundary.FirstNonZeroOffset);
        Assert.Equal("ff 00", boundary.FirstNonZeroFieldHex);
        Assert.Equal(boundaryHeaderHex, boundary.BoundaryHeaderHex);
        Assert.Equal("state marker/object update candidate", boundary.BoundaryHeaderClassification);

        PacketDumpFileSummary dump = new(
            "mode06-guarded-marker-16-18-object-view-159.txt",
            1,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            Array.Empty<Protocol04PacketSequenceSample>(),
            new[] { samples[0] },
            Array.Empty<Protocol04InteractionPayloadSample>(),
            Array.Empty<PlayerAttributePayloadSample>(),
            Array.Empty<ManageBonusPayloadSample>());

        Protocol03NestedMovementMode06PostContinuationTupleBodySummary tupleBody =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06PostContinuationTupleBodySummaries(new[] { dump }));
        Assert.Equal("ff marker plus zero-padded ff continuation lead", tupleBody.TailPrefixKind);
        Assert.Equal(prefixFirstNonZeroFieldHex, tupleBody.PrefixFirstNonZeroFieldHex);
        Assert.Equal("unclassified object-view tuple", tupleBody.TupleLayoutKind);
        Assert.Equal("unclassified object-view update", tupleBody.PostPrefixDisposition);
        Assert.Equal(159, tupleBody.PostTupleBodyBytes);
        Assert.Equal("00 00 00 00 00 00 00 03 ff 00 00 00 00 00 00 00", tupleBody.BodySuffixHex);
        Assert.Equal(6, tupleBody.FirstNonZeroOffset);
        Assert.Equal("ff 00", tupleBody.FirstNonZeroFieldHex);
        Assert.Equal(1, tupleBody.HeaderPrefixes[boundaryHeaderHex]);
        Assert.Equal(1, tupleBody.PostPrefixLeads[postPrefixLeadHex]);

        Protocol03NestedMovementMode06TupleBodyParserActionSummary parserAction =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06TupleBodyParserActionSummaries(new[] { tupleBody }));
        Assert.Equal(expectedParserAction, parserAction.ParserAction);
        Assert.Equal("159-byte marker-16/18 object-view body before accepted state/object boundary", parserAction.ActionReason);
        Assert.Equal(1, parserAction.BodyByteLengths["159"]);
    }

    [Fact]
    public void DetectProtocol03ObjectViews_BoundsGuardedMode0649ByteMarker0eObjectViewTupleBody()
    {
        byte[] body = Enumerable.Repeat((byte)0x00, 6)
            .Concat(new byte[] { 0xff, 0x00 })
            .Concat(Enumerable.Repeat((byte)0x00, 25))
            .Concat(PacketResearcher.ParseHexBytes("00 00 00 00 01 00 20 03 ff 00 00 00 00 00 00 00"))
            .ToArray();
        var bytes = new List<byte>
        {
            0x02, 0x03,
            0x01, 0x00, 0x02, 0x0c
        };
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 13));
        bytes.Add(0x03);
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 06 0e 01 18 58 3a f6 c6 00 00 be 42 44 35 af 46"));
        bytes.AddRange(PacketResearcher.ParseHexBytes("ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 22));
        bytes.AddRange(PacketResearcher.ParseHexBytes("01 ff"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 7));
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 20 03 00 00 00 00 0e"));
        bytes.AddRange(body);
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 b5 02 00 00 4c 0a 00"));

        Protocol03ObjectViewSample[] samples = PacketResearcher.DetectProtocol03ObjectViews(bytes, 545, "server").ToArray();

        const string expectedParserAction = "test guarded mode 06 49-byte marker-0e object-view tuple body";
        const int expectedPayloadBytes = 17 + 55 + 49;
        Assert.Equal(2, samples.Length);
        Assert.True(samples[0].Complete);
        Assert.Equal(2, samples[0].ParsedUpdateCount);
        Assert.Equal(0, samples[0].UnparsedBytes);
        Assert.Collection(
            samples[0].Segments,
            segment =>
            {
                Assert.Equal("0c", segment.Selector);
                Assert.Equal("position xyz + flag", segment.Classification);
                Assert.Equal(13, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            },
            segment =>
            {
                Assert.Equal("03", segment.Selector);
                Assert.Equal("mode 06 guarded 49-byte marker-0e object-view tuple body", segment.Classification);
                Assert.Equal(expectedPayloadBytes, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            });
        Assert.Equal(0xb500, samples[1].ViewId);
        Assert.Equal("state marker/object update candidate", samples[1].Classification);

        Protocol03NestedMovementConsumedBodyBoundary boundary = Assert.Single(samples[0].NestedMovementConsumedBodyBoundaries);
        Assert.Equal(expectedParserAction, boundary.ParserAction);
        Assert.Equal("03", boundary.Selector);
        Assert.Equal(17, boundary.Offset);
        Assert.Equal(expectedPayloadBytes, boundary.PayloadBytes);
        Assert.Equal(expectedPayloadBytes + 8, boundary.EvidencePayloadBytes);
        Assert.Equal(17, boundary.TailStartOffset);
        Assert.Equal(104, boundary.TailBoundaryOffset);
        Assert.Equal(49, boundary.PostTupleBodyBytes);
        Assert.Equal("unclassified object-view tuple", boundary.TupleLayoutKind);
        Assert.Equal(6, boundary.FirstNonZeroOffset);
        Assert.Equal("ff 00", boundary.FirstNonZeroFieldHex);
        Assert.Equal("00 b5 02 00 00 4c 0a 00", boundary.BoundaryHeaderHex);
        Assert.Equal("state marker/object update candidate", boundary.BoundaryHeaderClassification);

        PacketDumpFileSummary dump = new(
            "mode06-guarded-marker-0e-object-view-49.txt",
            1,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            Array.Empty<Protocol04PacketSequenceSample>(),
            new[] { samples[0] },
            Array.Empty<Protocol04InteractionPayloadSample>(),
            Array.Empty<PlayerAttributePayloadSample>(),
            Array.Empty<ManageBonusPayloadSample>());

        Protocol03NestedMovementMode06PostContinuationTupleBodySummary tupleBody =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06PostContinuationTupleBodySummaries(new[] { dump }));
        Assert.Equal("ff marker plus zero-padded ff continuation lead", tupleBody.TailPrefixKind);
        Assert.Equal("01 ff", tupleBody.PrefixFirstNonZeroFieldHex);
        Assert.Equal("unclassified object-view tuple", tupleBody.TupleLayoutKind);
        Assert.Equal("unclassified object-view update", tupleBody.PostPrefixDisposition);
        Assert.Equal(49, tupleBody.PostTupleBodyBytes);
        Assert.Equal("00 00 00 00 01 00 20 03 ff 00 00 00 00 00 00 00", tupleBody.BodySuffixHex);
        Assert.Equal(6, tupleBody.FirstNonZeroOffset);
        Assert.Equal("ff 00", tupleBody.FirstNonZeroFieldHex);
        Assert.Equal(1, tupleBody.HeaderClassifications["state marker/object update candidate"]);
        Assert.Equal(1, tupleBody.HeaderPrefixes["00 b5 02 00 00 4c 0a 00"]);
        Assert.Equal(1, tupleBody.PostPrefixLeads["00 20 03 00 00 00 00 0e"]);

        Protocol03NestedMovementMode06TupleBodyParserActionSummary parserAction =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06TupleBodyParserActionSummaries(new[] { tupleBody }));
        Assert.Equal(expectedParserAction, parserAction.ParserAction);
        Assert.Equal("49-byte marker-0e object-view body before 00 b5 state/object boundary", parserAction.ActionReason);
        Assert.Equal(1, parserAction.BodyByteLengths["49"]);
    }

    [Fact]
    public void DetectProtocol03ObjectViews_BoundsGuardedMode06NonDominantZeroUpdateTupleBody()
    {
        byte[] body = Enumerable.Repeat((byte)0x00, 6)
            .Concat(new byte[] { 0xff, 0x00 })
            .Concat(Enumerable.Repeat((byte)0x00, 49))
            .ToArray();
        var bytes = new List<byte>
        {
            0x02, 0x03,
            0x01, 0x00, 0x02, 0x0c
        };
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 13));
        bytes.Add(0x03);
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 06 0e 01 18 58 3a f6 c6 00 00 be 42 44 35 af 46"));
        bytes.AddRange(PacketResearcher.ParseHexBytes("ff 00 00 00 18 00 00 00 01 00 00 ff 00 00 00 00"));
        bytes.AddRange(PacketResearcher.ParseHexBytes("01 ff"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 29));
        bytes.AddRange(PacketResearcher.ParseHexBytes("03 fa 00 00 00 00 00 03"));
        bytes.AddRange(body);
        bytes.AddRange(PacketResearcher.ParseHexBytes("28 ff 01 00 00 00 00 00"));

        Protocol03ObjectViewSample[] samples = PacketResearcher.DetectProtocol03ObjectViews(bytes, 533, "server").ToArray();

        const int expectedPayloadBytes = 17 + 16 + 31 + 8 + 57;
        Assert.Equal(2, samples.Length);
        Assert.True(samples[0].Complete);
        Assert.Equal(2, samples[0].ParsedUpdateCount);
        Assert.Equal(0, samples[0].UnparsedBytes);
        Assert.Collection(
            samples[0].Segments,
            segment =>
            {
                Assert.Equal("0c", segment.Selector);
                Assert.Equal("position xyz + flag", segment.Classification);
                Assert.Equal(13, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            },
            segment =>
            {
                Assert.Equal("03", segment.Selector);
                Assert.Equal("mode 06 non-dominant zero-update tuple body", segment.Classification);
                Assert.Equal(expectedPayloadBytes, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            });
        Assert.True(samples[1].Complete);
        Assert.Equal(0xff28, samples[1].ViewId);
        Assert.Equal("single state marker update", samples[1].Classification);

        Protocol03NestedMovementConsumedBodyBoundary boundary = Assert.Single(samples[0].NestedMovementConsumedBodyBoundaries);
        Assert.Equal("test guarded mode 06 non-dominant zero-update tuple body", boundary.ParserAction);
        Assert.Equal("03", boundary.Selector);
        Assert.Equal(17, boundary.Offset);
        Assert.Equal(expectedPayloadBytes, boundary.PayloadBytes);
        Assert.Equal(expectedPayloadBytes + 8, boundary.EvidencePayloadBytes);
        Assert.Equal(17, boundary.TailStartOffset);
        Assert.Equal(112, boundary.TailBoundaryOffset);
        Assert.Equal(57, boundary.PostTupleBodyBytes);
        Assert.Equal("zero-update marker tuple", boundary.TupleLayoutKind);
        Assert.Equal(6, boundary.FirstNonZeroOffset);
        Assert.Equal("ff 00", boundary.FirstNonZeroFieldHex);
        Assert.Equal("28 ff 01 00 00 00 00 00", boundary.BoundaryHeaderHex);
        Assert.Equal("single state marker update", boundary.BoundaryHeaderClassification);

        PacketDumpFileSummary dump = new(
            "mode06-nondominant-zero-update.txt",
            1,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            Array.Empty<Protocol04PacketSequenceSample>(),
            new[] { samples[0] },
            Array.Empty<Protocol04InteractionPayloadSample>(),
            Array.Empty<PlayerAttributePayloadSample>(),
            Array.Empty<ManageBonusPayloadSample>());

        Protocol03NestedMovementMode06PostContinuationTupleBodySummary tupleBody =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06PostContinuationTupleBodySummaries(new[] { dump }));
        Assert.Equal("ff marker-prefixed tail", tupleBody.TailPrefixKind);
        Assert.Equal("01 ff", tupleBody.PrefixFirstNonZeroFieldHex);
        Assert.Equal("zero-update marker tuple", tupleBody.TupleLayoutKind);
        Assert.Equal("non-object-view post-prefix lead", tupleBody.PostPrefixDisposition);
        Assert.Equal(57, tupleBody.PostTupleBodyBytes);
        Assert.Equal(6, tupleBody.FirstNonZeroOffset);
        Assert.Equal("ff 00", tupleBody.FirstNonZeroFieldHex);
        Assert.Equal(1, tupleBody.HeaderClassifications["single state marker update"]);
        Assert.Equal(1, tupleBody.HeaderPrefixes["28 ff 01 00 00 00 00 00"]);
        Assert.Equal(1, tupleBody.PostPrefixLeads["03 fa 00 00 00 00 00 03"]);

        Protocol03NestedMovementMode06TupleBodyParserActionSummary parserAction =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06TupleBodyParserActionSummaries(new[] { tupleBody }));
        Assert.Equal("test guarded mode 06 non-dominant zero-update tuple body", parserAction.ParserAction);
        Assert.Equal(1, parserAction.BodyByteLengths["57"]);
    }

    [Fact]
    public void DetectProtocol03ObjectViews_BoundsGuardedMode06NonDominant49ByteZeroUpdateTupleBody()
    {
        byte[] body = Enumerable.Repeat((byte)0x00, 6)
            .Concat(new byte[] { 0xff, 0x00 })
            .Concat(Enumerable.Repeat((byte)0x00, 41))
            .ToArray();
        var bytes = new List<byte>
        {
            0x02, 0x03,
            0x01, 0x00, 0x02, 0x0c
        };
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 13));
        bytes.Add(0x03);
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 06 0e 01 18 58 3a f6 c6 00 00 be 42 44 35 af 46"));
        bytes.AddRange(PacketResearcher.ParseHexBytes("ff 00 00 00 18 00 00 00 01 00 00 ff 00 00 00 00"));
        bytes.AddRange(PacketResearcher.ParseHexBytes("03 ff"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 29));
        bytes.AddRange(PacketResearcher.ParseHexBytes("03 7d 00 00 00 00 00 03"));
        bytes.AddRange(body);
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 bc 02 00 00 00 00 00"));

        Protocol03ObjectViewSample[] samples = PacketResearcher.DetectProtocol03ObjectViews(bytes, 535, "server").ToArray();

        const int expectedPayloadBytes = 17 + 16 + 31 + 8 + 49;
        Assert.Equal(2, samples.Length);
        Assert.True(samples[0].Complete);
        Assert.Equal(2, samples[0].ParsedUpdateCount);
        Assert.Equal(0, samples[0].UnparsedBytes);
        Assert.Collection(
            samples[0].Segments,
            segment =>
            {
                Assert.Equal("0c", segment.Selector);
                Assert.Equal("position xyz + flag", segment.Classification);
                Assert.Equal(13, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            },
            segment =>
            {
                Assert.Equal("03", segment.Selector);
                Assert.Equal("mode 06 non-dominant 49-byte zero-update tuple body", segment.Classification);
                Assert.Equal(expectedPayloadBytes, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            });
        Assert.True(samples[1].Complete);
        Assert.Equal(0xbc00, samples[1].ViewId);
        Assert.Equal("state marker/object update candidate", samples[1].Classification);

        Protocol03NestedMovementConsumedBodyBoundary boundary = Assert.Single(samples[0].NestedMovementConsumedBodyBoundaries);
        Assert.Equal("test guarded mode 06 non-dominant 49-byte zero-update tuple body", boundary.ParserAction);
        Assert.Equal("03", boundary.Selector);
        Assert.Equal(17, boundary.Offset);
        Assert.Equal(expectedPayloadBytes, boundary.PayloadBytes);
        Assert.Equal(expectedPayloadBytes + 8, boundary.EvidencePayloadBytes);
        Assert.Equal(17, boundary.TailStartOffset);
        Assert.Equal(104, boundary.TailBoundaryOffset);
        Assert.Equal(49, boundary.PostTupleBodyBytes);
        Assert.Equal("zero-update marker tuple", boundary.TupleLayoutKind);
        Assert.Equal(6, boundary.FirstNonZeroOffset);
        Assert.Equal("ff 00", boundary.FirstNonZeroFieldHex);
        Assert.Equal("00 bc 02 00 00 00 00 00", boundary.BoundaryHeaderHex);
        Assert.Equal("state marker/object update candidate", boundary.BoundaryHeaderClassification);

        PacketDumpFileSummary dump = new(
            "mode06-nondominant-zero-update-49.txt",
            1,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            Array.Empty<Protocol04PacketSequenceSample>(),
            new[] { samples[0] },
            Array.Empty<Protocol04InteractionPayloadSample>(),
            Array.Empty<PlayerAttributePayloadSample>(),
            Array.Empty<ManageBonusPayloadSample>());

        Protocol03NestedMovementMode06PostContinuationTupleBodySummary tupleBody =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06PostContinuationTupleBodySummaries(new[] { dump }));
        Assert.Equal("ff marker-prefixed tail", tupleBody.TailPrefixKind);
        Assert.Equal("03 ff", tupleBody.PrefixFirstNonZeroFieldHex);
        Assert.Equal("zero-update marker tuple", tupleBody.TupleLayoutKind);
        Assert.Equal("non-object-view post-prefix lead", tupleBody.PostPrefixDisposition);
        Assert.Equal(49, tupleBody.PostTupleBodyBytes);
        Assert.Equal(6, tupleBody.FirstNonZeroOffset);
        Assert.Equal("ff 00", tupleBody.FirstNonZeroFieldHex);
        Assert.Equal(1, tupleBody.HeaderClassifications["state marker/object update candidate"]);
        Assert.Equal(1, tupleBody.HeaderPrefixes["00 bc 02 00 00 00 00 00"]);
        Assert.Equal(1, tupleBody.PostPrefixLeads["03 7d 00 00 00 00 00 03"]);

        Protocol03NestedMovementMode06TupleBodyParserActionSummary parserAction =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06TupleBodyParserActionSummaries(new[] { tupleBody }));
        Assert.Equal("test guarded mode 06 non-dominant 49-byte zero-update tuple body", parserAction.ParserAction);
        Assert.Equal(1, parserAction.BodyByteLengths["49"]);
    }

    [Fact]
    public void DetectProtocol03ObjectViews_BoundsGuardedMode06NonDominant53ByteMarker32ObjectViewTupleBody()
    {
        byte[] body = Enumerable.Repeat((byte)0x00, 6)
            .Concat(new byte[] { 0xff, 0x00 })
            .Concat(Enumerable.Repeat((byte)0x00, 29))
            .Concat(PacketResearcher.ParseHexBytes("00 00 9e 07 ff 00 00 00 82 77 00 00 ae 00 00 00"))
            .ToArray();
        var bytes = new List<byte>
        {
            0x02, 0x03,
            0x01, 0x00, 0x02, 0x0c
        };
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 13));
        bytes.Add(0x03);
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 06 0e 01 18 58 3a f6 c6 00 00 be 42 44 35 af 46"));
        bytes.AddRange(PacketResearcher.ParseHexBytes("ff 00 00 00 18 00 00 00 01 00 00 ff 00 00 00 00"));
        bytes.AddRange(PacketResearcher.ParseHexBytes("03 02"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 29));
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 9e 07 00 00 00 00 32"));
        bytes.AddRange(body);
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 50 01 00 28 ff 06 00"));

        Protocol03ObjectViewSample[] samples = PacketResearcher.DetectProtocol03ObjectViews(bytes, 536, "server").ToArray();

        const int expectedPayloadBytes = 17 + 16 + 31 + 8 + 53;
        Assert.Equal(2, samples.Length);
        Assert.True(samples[0].Complete);
        Assert.Equal(2, samples[0].ParsedUpdateCount);
        Assert.Equal(0, samples[0].UnparsedBytes);
        Assert.Collection(
            samples[0].Segments,
            segment =>
            {
                Assert.Equal("0c", segment.Selector);
                Assert.Equal("position xyz + flag", segment.Classification);
                Assert.Equal(13, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            },
            segment =>
            {
                Assert.Equal("03", segment.Selector);
                Assert.Equal("mode 06 non-dominant 53-byte marker-32 object-view tuple body", segment.Classification);
                Assert.Equal(expectedPayloadBytes, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            });
        Assert.True(samples[1].Complete);
        Assert.Equal(0x5000, samples[1].ViewId);
        Assert.Equal("single state marker update", samples[1].Classification);

        Protocol03NestedMovementConsumedBodyBoundary boundary = Assert.Single(samples[0].NestedMovementConsumedBodyBoundaries);
        Assert.Equal("test guarded mode 06 non-dominant 53-byte marker-32 object-view tuple body", boundary.ParserAction);
        Assert.Equal("03", boundary.Selector);
        Assert.Equal(17, boundary.Offset);
        Assert.Equal(expectedPayloadBytes, boundary.PayloadBytes);
        Assert.Equal(expectedPayloadBytes + 8, boundary.EvidencePayloadBytes);
        Assert.Equal(17, boundary.TailStartOffset);
        Assert.Equal(108, boundary.TailBoundaryOffset);
        Assert.Equal(53, boundary.PostTupleBodyBytes);
        Assert.Equal("unclassified object-view tuple", boundary.TupleLayoutKind);
        Assert.Equal(6, boundary.FirstNonZeroOffset);
        Assert.Equal("ff 00", boundary.FirstNonZeroFieldHex);
        Assert.Equal("00 50 01 00 28 ff 06 00", boundary.BoundaryHeaderHex);
        Assert.Equal("single state marker update", boundary.BoundaryHeaderClassification);

        PacketDumpFileSummary dump = new(
            "mode06-nondominant-marker-32-object-view-53.txt",
            1,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            Array.Empty<Protocol04PacketSequenceSample>(),
            new[] { samples[0] },
            Array.Empty<Protocol04InteractionPayloadSample>(),
            Array.Empty<PlayerAttributePayloadSample>(),
            Array.Empty<ManageBonusPayloadSample>());

        Protocol03NestedMovementMode06PostContinuationTupleBodySummary tupleBody =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06PostContinuationTupleBodySummaries(new[] { dump }));
        Assert.Equal("ff marker-prefixed tail", tupleBody.TailPrefixKind);
        Assert.Equal("03 02", tupleBody.PrefixFirstNonZeroFieldHex);
        Assert.Equal("unclassified object-view tuple", tupleBody.TupleLayoutKind);
        Assert.Equal("unclassified object-view update", tupleBody.PostPrefixDisposition);
        Assert.Equal(53, tupleBody.PostTupleBodyBytes);
        Assert.Equal("00 00 9e 07 ff 00 00 00 82 77 00 00 ae 00 00 00", tupleBody.BodySuffixHex);
        Assert.Equal(6, tupleBody.FirstNonZeroOffset);
        Assert.Equal("ff 00", tupleBody.FirstNonZeroFieldHex);
        Assert.Equal(1, tupleBody.HeaderClassifications["single state marker update"]);
        Assert.Equal(1, tupleBody.HeaderPrefixes["00 50 01 00 28 ff 06 00"]);
        Assert.Equal(1, tupleBody.PostPrefixLeads["00 9e 07 00 00 00 00 32"]);

        Protocol03NestedMovementMode06TupleBodyParserActionSummary parserAction =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06TupleBodyParserActionSummaries(new[] { tupleBody }));
        Assert.Equal("test guarded mode 06 non-dominant 53-byte marker-32 object-view tuple body", parserAction.ParserAction);
        Assert.Equal("non-dominant 53-byte marker-32 object-view body before shared 00 50 state-marker boundary", parserAction.ActionReason);
        Assert.Equal(1, parserAction.BodyByteLengths["53"]);
    }

    [Fact]
    public void DetectProtocol03ObjectViews_BoundsGuardedMode06NonDominant70ByteMarker32ObjectViewTupleBody()
    {
        byte[] bytes = PacketResearcher.ParseHexBytes("""
            02 03 02 00 02 80 04 1e 05 00 01 00 04 c0 25 11 46 00 80 93 43 26 4e 85 c6 03 00 06 0e 00 71 a4
            f2 0e 46 00 80 93 43 0a 57 82 c6 ff 00 00 00 10 00 00 01 01 00 01 ff 00 00 00 00 00 00 00 00 00
            00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 01 ff 00 00 00 00 00 f1 02 02 28 0a 00 00 00
            00 32 00 00 00 00 00 00 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
            00 00 00 00 00 00 00 00 00 28 0a ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 ff 00 00 00
            00 00 00 00 00 00 01 00 00 86 01 00 58 37 08 00 00 1f 0a 08 00 00 00 00 00 00 22 00 00 00 00 00
            00 00 04 01 00 69 07 46 80 a0 00 06 00 01 3e 00 4d 6f 76 65 20 74 68 65 20 63 61 6d 65 72 61 20
            61 72 6f 75 6e 64 20 62 79 20 68 6f 6c 64 69 6e 67 20 64 6f 77 6e 20 74 68 65 20 72 69 67 68 74
            20 6d 6f 75 73 65 20 62 75 74 74 6f 6e 00 06 81 45 75 08 00 86 1d 80 a0 01 06 00 00 15 00 4d 6f
            76 65 20 61 72 6f 75 6e 64 20 74 68 65 20 72 6f 6f 6d 00 1d 80 a0 01 06 00 01 15 00 4d 6f 76 65
            20 61 72 6f 75 6e 64 20 74 68 65 20 72 6f 6f 6d 00 05 81 02 0e 00 01 06 81 45 79 08 00 86 2d 80
            a0 02 06 00 00 25 00 54 61 6c 6b 20 74 6f 20 41 6d 6d 69 74 20 77 69 74 68 20 64 6f 75 62 6c 65
            2d 6c 65 66 74 20 63 6c 69 63 6b 00
            """).ToArray();

        Protocol03ObjectViewSample[] samples = PacketResearcher.DetectProtocol03ObjectViews(bytes, 536, "server").ToArray();

        PacketDumpFileSummary dump = new(
            "mode06-nondominant-marker-32-object-view-70.txt",
            1,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            Array.Empty<Protocol04PacketSequenceSample>(),
            samples,
            Array.Empty<Protocol04InteractionPayloadSample>(),
            Array.Empty<PlayerAttributePayloadSample>(),
            Array.Empty<ManageBonusPayloadSample>());

        Protocol03NestedMovementMode06PostContinuationTupleBodySummary[] tupleBodies =
            PacketResearcher.BuildProtocol03NestedMovementMode06PostContinuationTupleBodySummaries(new[] { dump }).ToArray();
        Protocol03NestedMovementMode06PostContinuationTupleBodySummary tupleBody = Assert.Single(tupleBodies, tupleBody =>
            tupleBody.PostTupleBodyBytes == 70 &&
            tupleBody.PostPrefixLeads.ContainsKey("02 28 0a 00 00 00 00 32"));
        Assert.Equal("ff marker-prefixed tail", tupleBody.TailPrefixKind);
        Assert.Equal("01 ff", tupleBody.PrefixFirstNonZeroFieldHex);
        Assert.Equal("unclassified object-view tuple", tupleBody.TupleLayoutKind);
        Assert.Equal("unclassified object-view update", tupleBody.PostPrefixDisposition);
        Assert.Equal(70, tupleBody.PostTupleBodyBytes);
        Assert.Equal("00 00 00 00 ff 00 00 00 00 00 00 00 00 00 01 00", tupleBody.BodySuffixHex);
        Assert.Equal(6, tupleBody.FirstNonZeroOffset);
        Assert.Equal("ff 00", tupleBody.FirstNonZeroFieldHex);
        Assert.Equal(1, tupleBody.HeaderClassifications["single state marker update"]);
        Assert.Equal(1, tupleBody.HeaderPrefixes["00 86 01 00 58 37 08 00"]);
        Assert.Equal(1, tupleBody.PostPrefixLeads["02 28 0a 00 00 00 00 32"]);

        Protocol03NestedMovementMode06TupleBodyParserActionSummary parserAction =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06TupleBodyParserActionSummaries(new[] { tupleBody }));
        Assert.Equal("test guarded mode 06 non-dominant 70-byte marker-32 object-view tuple body", parserAction.ParserAction);
        Assert.Equal("non-dominant 70-byte marker-32 object-view body before shared 00 86 state-marker boundary", parserAction.ActionReason);
        Assert.Equal(1, parserAction.BodyByteLengths["70"]);
        Assert.Empty(PacketResearcher.BuildProtocol03NestedMovementMode06HeldTupleBodySummaries(new[] { tupleBody }));
    }

    [Fact]
    public void DetectProtocol03ObjectViews_BoundsGuardedMode06Marker02_49ByteZeroUpdateTupleBody()
    {
        byte[] body = Enumerable.Repeat((byte)0x00, 6)
            .Concat(new byte[] { 0xff, 0x00 })
            .Concat(Enumerable.Repeat((byte)0x00, 25))
            .Concat(PacketResearcher.ParseHexBytes("00 00 00 00 00 00 c8 00 ff 00 00 00 00 00 00 00"))
            .ToArray();
        var bytes = new List<byte>
        {
            0x02, 0x03,
            0x01, 0x00, 0x02, 0x0c
        };
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 13));
        bytes.Add(0x03);
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 06 0e 01 18 58 3a f6 c6 00 00 be 42 44 35 af 46"));
        bytes.AddRange(PacketResearcher.ParseHexBytes("ff 00 00 00 18 00 00 00 01 00 00 ff 00 00 00 00"));
        bytes.AddRange(PacketResearcher.ParseHexBytes("01 ff"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 29));
        bytes.AddRange(PacketResearcher.ParseHexBytes("02 c8 00 00 00 00 00 02"));
        bytes.AddRange(body);
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 b4 02 00 00 00 00 00"));

        Protocol03ObjectViewSample[] samples = PacketResearcher.DetectProtocol03ObjectViews(bytes, 536, "server").ToArray();

        const int expectedPayloadBytes = 17 + 16 + 31 + 8 + 49;
        Assert.Equal(2, samples.Length);
        Assert.True(samples[0].Complete);
        Assert.Equal(2, samples[0].ParsedUpdateCount);
        Assert.Equal(0, samples[0].UnparsedBytes);
        Assert.Collection(
            samples[0].Segments,
            segment =>
            {
                Assert.Equal("0c", segment.Selector);
                Assert.Equal("position xyz + flag", segment.Classification);
                Assert.Equal(13, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            },
            segment =>
            {
                Assert.Equal("03", segment.Selector);
                Assert.Equal("mode 06 guarded 49-byte marker-02 zero-update tuple body", segment.Classification);
                Assert.Equal(expectedPayloadBytes, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            });
        Assert.True(samples[1].Complete);
        Assert.Equal(0xb400, samples[1].ViewId);
        Assert.Equal("state marker/object update candidate", samples[1].Classification);

        Protocol03NestedMovementConsumedBodyBoundary boundary = Assert.Single(samples[0].NestedMovementConsumedBodyBoundaries);
        Assert.Equal("test guarded mode 06 49-byte marker-02 zero-update tuple body", boundary.ParserAction);
        Assert.Equal("03", boundary.Selector);
        Assert.Equal(17, boundary.Offset);
        Assert.Equal(expectedPayloadBytes, boundary.PayloadBytes);
        Assert.Equal(expectedPayloadBytes + 8, boundary.EvidencePayloadBytes);
        Assert.Equal(17, boundary.TailStartOffset);
        Assert.Equal(104, boundary.TailBoundaryOffset);
        Assert.Equal(49, boundary.PostTupleBodyBytes);
        Assert.Equal("zero-update marker tuple", boundary.TupleLayoutKind);
        Assert.Equal(6, boundary.FirstNonZeroOffset);
        Assert.Equal("ff 00", boundary.FirstNonZeroFieldHex);
        Assert.Equal("00 b4 02 00 00 00 00 00", boundary.BoundaryHeaderHex);
        Assert.Equal("state marker/object update candidate", boundary.BoundaryHeaderClassification);

        PacketDumpFileSummary dump = new(
            "mode06-marker02-zero-update-49.txt",
            1,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            Array.Empty<Protocol04PacketSequenceSample>(),
            new[] { samples[0] },
            Array.Empty<Protocol04InteractionPayloadSample>(),
            Array.Empty<PlayerAttributePayloadSample>(),
            Array.Empty<ManageBonusPayloadSample>());

        Protocol03NestedMovementMode06PostContinuationTupleBodySummary tupleBody =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06PostContinuationTupleBodySummaries(new[] { dump }));
        Assert.Equal("ff marker-prefixed tail", tupleBody.TailPrefixKind);
        Assert.Equal("01 ff", tupleBody.PrefixFirstNonZeroFieldHex);
        Assert.Equal("zero-update marker tuple", tupleBody.TupleLayoutKind);
        Assert.Equal("non-object-view post-prefix lead", tupleBody.PostPrefixDisposition);
        Assert.Equal(49, tupleBody.PostTupleBodyBytes);
        Assert.Equal(6, tupleBody.FirstNonZeroOffset);
        Assert.Equal("ff 00", tupleBody.FirstNonZeroFieldHex);
        Assert.Equal("00 00 00 00 00 00 c8 00 ff 00 00 00 00 00 00 00", tupleBody.BodySuffixHex);
        Assert.Equal(1, tupleBody.HeaderClassifications["state marker/object update candidate"]);
        Assert.Equal(1, tupleBody.HeaderPrefixes["00 b4 02 00 00 00 00 00"]);
        Assert.Equal(1, tupleBody.PostPrefixLeads["02 c8 00 00 00 00 00 02"]);

        Protocol03NestedMovementMode06TupleBodyParserActionSummary parserAction =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06TupleBodyParserActionSummaries(new[] { tupleBody }));
        Assert.Equal("test guarded mode 06 49-byte marker-02 zero-update tuple body", parserAction.ParserAction);
        Assert.Equal(1, parserAction.BodyByteLengths["49"]);
    }

    [Theory]
    [InlineData("02 fa 00 00 00 00 00 03")]
    [InlineData("03 fa 00 00 00 00 00 03")]
    public void DetectProtocol03ObjectViews_BoundsGuardedMode06Marker03_49ByteZeroUpdateTupleBody(
        string postPrefixLeadHex)
    {
        byte[] body = Enumerable.Repeat((byte)0x00, 6)
            .Concat(new byte[] { 0xff, 0x00 })
            .Concat(Enumerable.Repeat((byte)0x00, 25))
            .Concat(PacketResearcher.ParseHexBytes("00 00 00 00 00 00 fa 00 ff 00 00 00 00 00 00 00"))
            .ToArray();
        var bytes = new List<byte>
        {
            0x02, 0x03,
            0x01, 0x00, 0x02, 0x0c
        };
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 13));
        bytes.Add(0x03);
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 06 0e 01 18 58 3a f6 c6 00 00 be 42 44 35 af 46"));
        bytes.AddRange(PacketResearcher.ParseHexBytes("ff 00 00 00 18 00 00 00 01 00 00 ff 00 00 00 00"));
        bytes.AddRange(PacketResearcher.ParseHexBytes("01 ff"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 29));
        bytes.AddRange(PacketResearcher.ParseHexBytes(postPrefixLeadHex));
        bytes.AddRange(body);
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 b4 02 00 00 00 00 00"));

        Protocol03ObjectViewSample[] samples = PacketResearcher.DetectProtocol03ObjectViews(bytes, 536, "server").ToArray();

        const int expectedPayloadBytes = 17 + 16 + 31 + 8 + 49;
        Assert.Equal(2, samples.Length);
        Assert.True(samples[0].Complete);
        Assert.Equal(2, samples[0].ParsedUpdateCount);
        Assert.Equal(0, samples[0].UnparsedBytes);
        Assert.Collection(
            samples[0].Segments,
            segment =>
            {
                Assert.Equal("0c", segment.Selector);
                Assert.Equal("position xyz + flag", segment.Classification);
                Assert.Equal(13, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            },
            segment =>
            {
                Assert.Equal("03", segment.Selector);
                Assert.Equal("mode 06 guarded 49-byte marker-03 zero-update tuple body", segment.Classification);
                Assert.Equal(expectedPayloadBytes, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            });
        Assert.True(samples[1].Complete);
        Assert.Equal(0xb400, samples[1].ViewId);
        Assert.Equal("state marker/object update candidate", samples[1].Classification);

        Protocol03NestedMovementConsumedBodyBoundary boundary = Assert.Single(samples[0].NestedMovementConsumedBodyBoundaries);
        Assert.Equal("test guarded mode 06 49-byte marker-03 zero-update tuple body", boundary.ParserAction);
        Assert.Equal("03", boundary.Selector);
        Assert.Equal(17, boundary.Offset);
        Assert.Equal(expectedPayloadBytes, boundary.PayloadBytes);
        Assert.Equal(expectedPayloadBytes + 8, boundary.EvidencePayloadBytes);
        Assert.Equal(17, boundary.TailStartOffset);
        Assert.Equal(104, boundary.TailBoundaryOffset);
        Assert.Equal(49, boundary.PostTupleBodyBytes);
        Assert.Equal("zero-update marker tuple", boundary.TupleLayoutKind);
        Assert.Equal(6, boundary.FirstNonZeroOffset);
        Assert.Equal("ff 00", boundary.FirstNonZeroFieldHex);
        Assert.Equal("00 b4 02 00 00 00 00 00", boundary.BoundaryHeaderHex);
        Assert.Equal("state marker/object update candidate", boundary.BoundaryHeaderClassification);

        PacketDumpFileSummary dump = new(
            "mode06-marker03-zero-update-49.txt",
            1,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            Array.Empty<Protocol04PacketSequenceSample>(),
            new[] { samples[0] },
            Array.Empty<Protocol04InteractionPayloadSample>(),
            Array.Empty<PlayerAttributePayloadSample>(),
            Array.Empty<ManageBonusPayloadSample>());

        Protocol03NestedMovementMode06PostContinuationTupleBodySummary tupleBody =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06PostContinuationTupleBodySummaries(new[] { dump }));
        Assert.Equal("ff marker-prefixed tail", tupleBody.TailPrefixKind);
        Assert.Equal("01 ff", tupleBody.PrefixFirstNonZeroFieldHex);
        Assert.Equal("zero-update marker tuple", tupleBody.TupleLayoutKind);
        Assert.Equal("non-object-view post-prefix lead", tupleBody.PostPrefixDisposition);
        Assert.Equal(49, tupleBody.PostTupleBodyBytes);
        Assert.Equal(6, tupleBody.FirstNonZeroOffset);
        Assert.Equal("ff 00", tupleBody.FirstNonZeroFieldHex);
        Assert.Equal("00 00 00 00 00 00 fa 00 ff 00 00 00 00 00 00 00", tupleBody.BodySuffixHex);
        Assert.Equal(1, tupleBody.HeaderClassifications["state marker/object update candidate"]);
        Assert.Equal(1, tupleBody.HeaderPrefixes["00 b4 02 00 00 00 00 00"]);
        Assert.Equal(1, tupleBody.PostPrefixLeads[postPrefixLeadHex]);

        Protocol03NestedMovementMode06TupleBodyParserActionSummary parserAction =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06TupleBodyParserActionSummaries(new[] { tupleBody }));
        Assert.Equal("test guarded mode 06 49-byte marker-03 zero-update tuple body", parserAction.ParserAction);
        Assert.Equal(1, parserAction.BodyByteLengths["49"]);
    }

    [Fact]
    public void DetectProtocol03ObjectViews_BoundsGuardedMode06Unclassified53ByteTupleBody()
    {
        byte[] body = Enumerable.Repeat((byte)0x00, 6)
            .Concat(new byte[] { 0xff, 0x00 })
            .Concat(Enumerable.Repeat((byte)0x00, 45))
            .ToArray();
        var bytes = new List<byte>
        {
            0x02, 0x03,
            0x01, 0x00, 0x02, 0x0c
        };
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 13));
        bytes.Add(0x03);
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 06 0e 01 18 58 3a f6 c6 00 00 be 42 44 35 af 46"));
        bytes.AddRange(PacketResearcher.ParseHexBytes("ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00"));
        bytes.AddRange(PacketResearcher.ParseHexBytes("02 ff"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 29));
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 e9 07 00 00 00 00 34"));
        bytes.AddRange(body);
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 b8 01 00 28 ff 15 00"));

        Protocol03ObjectViewSample[] samples = PacketResearcher.DetectProtocol03ObjectViews(bytes, 534, "server").ToArray();

        const int expectedPayloadBytes = 17 + 16 + 31 + 8 + 53;
        Assert.Equal(2, samples.Length);
        Assert.True(samples[0].Complete);
        Assert.Equal(2, samples[0].ParsedUpdateCount);
        Assert.Equal(0, samples[0].UnparsedBytes);
        Assert.Collection(
            samples[0].Segments,
            segment =>
            {
                Assert.Equal("0c", segment.Selector);
                Assert.Equal("position xyz + flag", segment.Classification);
                Assert.Equal(13, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            },
            segment =>
            {
                Assert.Equal("03", segment.Selector);
                Assert.Equal("mode 06 unclassified 53-byte tuple body", segment.Classification);
                Assert.Equal(expectedPayloadBytes, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            });
        Assert.True(samples[1].Complete);
        Assert.Equal(0xb800, samples[1].ViewId);
        Assert.Equal("single state marker update", samples[1].Classification);

        Protocol03NestedMovementConsumedBodyBoundary boundary = Assert.Single(samples[0].NestedMovementConsumedBodyBoundaries);
        Assert.Equal("test guarded mode 06 unclassified 53-byte tuple body", boundary.ParserAction);
        Assert.Equal("03", boundary.Selector);
        Assert.Equal(17, boundary.Offset);
        Assert.Equal(expectedPayloadBytes, boundary.PayloadBytes);
        Assert.Equal(expectedPayloadBytes + 8, boundary.EvidencePayloadBytes);
        Assert.Equal(17, boundary.TailStartOffset);
        Assert.Equal(108, boundary.TailBoundaryOffset);
        Assert.Equal(53, boundary.PostTupleBodyBytes);
        Assert.Equal("unclassified object-view tuple", boundary.TupleLayoutKind);
        Assert.Equal(6, boundary.FirstNonZeroOffset);
        Assert.Equal("ff 00", boundary.FirstNonZeroFieldHex);
        Assert.Equal("00 b8 01 00 28 ff 15 00", boundary.BoundaryHeaderHex);
        Assert.Equal("single state marker update", boundary.BoundaryHeaderClassification);

        PacketDumpFileSummary dump = new(
            "mode06-unclassified-53.txt",
            1,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            Array.Empty<Protocol04PacketSequenceSample>(),
            new[] { samples[0] },
            Array.Empty<Protocol04InteractionPayloadSample>(),
            Array.Empty<PlayerAttributePayloadSample>(),
            Array.Empty<ManageBonusPayloadSample>());

        Protocol03NestedMovementMode06PostContinuationTupleBodySummary tupleBody =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06PostContinuationTupleBodySummaries(new[] { dump }));
        Assert.Equal("ff marker plus zero-padded ff continuation lead", tupleBody.TailPrefixKind);
        Assert.Equal("02 ff", tupleBody.PrefixFirstNonZeroFieldHex);
        Assert.Equal("unclassified object-view tuple", tupleBody.TupleLayoutKind);
        Assert.Equal("unclassified object-view update", tupleBody.PostPrefixDisposition);
        Assert.Equal(53, tupleBody.PostTupleBodyBytes);
        Assert.Equal(6, tupleBody.FirstNonZeroOffset);
        Assert.Equal("ff 00", tupleBody.FirstNonZeroFieldHex);
        Assert.Equal(1, tupleBody.HeaderClassifications["single state marker update"]);
        Assert.Equal(1, tupleBody.HeaderPrefixes["00 b8 01 00 28 ff 15 00"]);
        Assert.Equal(1, tupleBody.PostPrefixLeads["00 e9 07 00 00 00 00 34"]);

        Protocol03NestedMovementMode06TupleBodyParserActionSummary parserAction =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06TupleBodyParserActionSummaries(new[] { tupleBody }));
        Assert.Equal("test guarded mode 06 unclassified 53-byte tuple body", parserAction.ParserAction);
        Assert.Equal(1, parserAction.BodyByteLengths["53"]);
    }

    [Theory]
    [InlineData(57)]
    [InlineData(70)]
    [InlineData(94)]
    public void DetectProtocol03ObjectViews_BoundsGuardedMode06UnclassifiedTupleBody(int postTupleBodyBytes)
    {
        byte[] body = Enumerable.Repeat((byte)0x00, 6)
            .Concat(new byte[] { 0xff, 0x00 })
            .Concat(Enumerable.Repeat((byte)0x00, postTupleBodyBytes - 8))
            .ToArray();
        var bytes = new List<byte>
        {
            0x02, 0x03,
            0x01, 0x00, 0x02, 0x0c
        };
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 13));
        bytes.Add(0x03);
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 06 0e 01 18 58 3a f6 c6 00 00 be 42 44 35 af 46"));
        bytes.AddRange(PacketResearcher.ParseHexBytes("ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 22));
        bytes.AddRange(new byte[] { 0x03, 0xff });
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 7));
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 b6 03 00 00 00 00 24"));
        bytes.AddRange(body);
        bytes.AddRange(PacketResearcher.ParseHexBytes("28 ff 01 00 00"));

        Protocol03ObjectViewSample[] samples = PacketResearcher.DetectProtocol03ObjectViews(bytes, 532, "server").ToArray();

        int expectedPayloadBytes = 17 + 55 + postTupleBodyBytes;
        Assert.Equal(2, samples.Length);
        Assert.True(samples[0].Complete);
        Assert.Equal(2, samples[0].ParsedUpdateCount);
        Assert.Equal(0, samples[0].UnparsedBytes);
        Assert.Collection(
            samples[0].Segments,
            segment =>
            {
                Assert.Equal("0c", segment.Selector);
                Assert.Equal("position xyz + flag", segment.Classification);
                Assert.Equal(13, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            },
            segment =>
            {
                Assert.Equal("03", segment.Selector);
                Assert.Equal("mode 06 unclassified tuple body", segment.Classification);
                Assert.Equal(expectedPayloadBytes, segment.PayloadBytes);
                Assert.True(segment.IsKnownLength);
            });
        Assert.True(samples[1].Complete);
        Assert.Equal(0xff28, samples[1].ViewId);
        Assert.Equal("single state marker update", samples[1].Classification);

        Protocol03NestedMovementConsumedBodyBoundary boundary = Assert.Single(samples[0].NestedMovementConsumedBodyBoundaries);
        Assert.Equal("test guarded mode 06 unclassified tuple body", boundary.ParserAction);
        Assert.Equal("03", boundary.Selector);
        Assert.Equal(17, boundary.Offset);
        Assert.Equal(expectedPayloadBytes, boundary.PayloadBytes);
        Assert.Equal(expectedPayloadBytes + 5, boundary.EvidencePayloadBytes);
        Assert.Equal(17, boundary.TailStartOffset);
        Assert.Equal(55 + postTupleBodyBytes, boundary.TailBoundaryOffset);
        Assert.Equal(postTupleBodyBytes, boundary.PostTupleBodyBytes);
        Assert.Equal("unclassified object-view tuple", boundary.TupleLayoutKind);
        Assert.Equal(6, boundary.FirstNonZeroOffset);
        Assert.Equal("ff 00", boundary.FirstNonZeroFieldHex);
        Assert.Equal("28 ff 01 00 00", boundary.BoundaryHeaderHex);
        Assert.Equal("single state marker update", boundary.BoundaryHeaderClassification);

        PacketDumpFileSummary dump = new(
            "mode06-guarded.txt",
            1,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            Array.Empty<Protocol04PacketSequenceSample>(),
            new[] { samples[0] },
            Array.Empty<Protocol04InteractionPayloadSample>(),
            Array.Empty<PlayerAttributePayloadSample>(),
            Array.Empty<ManageBonusPayloadSample>());

        Protocol03NestedMovementMode06PostContinuationTupleBodySummary tupleBody =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06PostContinuationTupleBodySummaries(new[] { dump }));
        Assert.Equal("unclassified object-view tuple", tupleBody.TupleLayoutKind);
        Assert.Equal("unclassified object-view update", tupleBody.PostPrefixDisposition);
        Assert.Equal(postTupleBodyBytes, tupleBody.PostTupleBodyBytes);
        Assert.Equal(6, tupleBody.FirstNonZeroOffset);
        Assert.Equal("ff 00", tupleBody.FirstNonZeroFieldHex);
        Assert.Equal(1, tupleBody.HeaderClassifications["single state marker update"]);

        Protocol03NestedMovementMode06TupleBodyParserActionSummary parserAction =
            Assert.Single(PacketResearcher.BuildProtocol03NestedMovementMode06TupleBodyParserActionSummaries(new[] { tupleBody }));
        Assert.Equal("test guarded mode 06 unclassified tuple body", parserAction.ParserAction);
        Assert.Equal(1, parserAction.BodyByteLengths[postTupleBodyBytes.ToString(CultureInfo.InvariantCulture)]);
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
    public void DetectProtocol03ObjectViews_CapturesUnknownVariableSelectorTailLead()
    {
        byte[] bytes = PacketResearcher.ParseHexBytes(
            "02 03 25 00 03 0c 93 78 00 00 00 00 00 00 00 00 00 00 00 ff 01 43 00 00 00 00 00 00 00 e0 2d 81 0d 22 89 20")
            .ToArray();

        Protocol03ObjectViewSample sample = PacketResearcher.DetectProtocol03ObjectViews(bytes, 6, "server").Single();

        Protocol03VariableSelectorLead lead = Assert.Single(sample.VariableSelectorLeads);
        Assert.Equal("ff", lead.Selector);
        Assert.Equal(18, lead.Offset);
        Assert.Equal(16, lead.PayloadBytes);
        Assert.Equal("01 43 00 00 00 00 00 00", lead.PrefixHex);
        Assert.Equal("01 43 00 00 00 00 00 00 00 e0 2d 81 0d 22 89 20", lead.PayloadHex);
    }

    [Fact]
    public void BuildProtocol03SelectorFfRepeatListCandidates_ExportsMachineReadableCandidate()
    {
        byte[] bytes = PacketResearcher.ParseHexBytes(
            "02 03 25 00 03 0c 93 78 00 00 00 00 00 00 00 00 00 00 00 ff " +
            "01 43 00 00 00 00 00 00 00 e0 2d 81 0d 22 89 20 00 0b 0c 11 79 80 00 00 01 00 00 00 00 12 01 00 02 ff 00 00 00 00 ba 13 63 00 00 87 43 00 00 c3 96 63 07 00 00 00 c0 e2 2e d1 40 00 00 00 00 00 f0 7e 40 00 00 00 00 02 1f a3 40 ff 00 00 00 00 00 00 00 00 00 00 a0 13 00 00 00 00 00 ff ff 00 00 00 00 66 00 00 00 00 00 00 00 00 00 00 00 00 01 00 ff 00 00 00 00 00 00 00 00 00 00 08 00")
            .ToArray();
        Protocol03ObjectViewSample sample = PacketResearcher.DetectProtocol03ObjectViews(bytes, 6, "server").Single();
        PacketDumpFileSummary dump = new(
            "vendor.txt",
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

        Protocol03SelectorFfRepeatListCandidate candidate = Assert.Single(PacketResearcher.BuildProtocol03SelectorFfRepeatListCandidates(new[] { dump }));

        Assert.Equal("vendor.txt", candidate.File);
        Assert.Equal(6, candidate.Line);
        Assert.Equal("server", candidate.Direction);
        Assert.Equal(18, candidate.SelectorOffset);
        Assert.Equal(51, candidate.PositionOffset);
        Assert.Equal("00 00 00 c0 e2 2e d1 40 00 00 00 00 00 f0 7e 40 00 00 00 00 02 1f a3 40", candidate.PositionHex);
        Assert.Equal(17595.543, candidate.X, 3);
        Assert.Equal(18, candidate.RepeatStride);
        Assert.Equal(2, candidate.RepeatRecordCount);
        Assert.Equal(36, candidate.ContinuationOffset);
        Assert.Equal(16, candidate.ContinuationBytes);
        Assert.Equal(10, candidate.Entry1ZeroRun);
        Assert.Equal(11, candidate.Entry1FieldOffset);
        Assert.Equal("a0 13", candidate.Entry1FieldHex);
        Assert.Equal(5024, candidate.Entry1FieldValue);
        Assert.Equal("long-zero-padded entry u16", candidate.Entry1FieldLayoutKind);
        Assert.Equal(0, candidate.Entry2ZeroRun);
        Assert.Equal(1, candidate.Entry2FieldOffset);
        Assert.Equal("ff 00", candidate.Entry2FieldHex);
        Assert.Equal(255, candidate.Entry2FieldValue);
        Assert.Equal("marker-adjacent entry u16", candidate.Entry2FieldLayoutKind);
        Assert.Equal(6, candidate.Entry2SecondaryFieldOffset);
        Assert.Equal("66 00", candidate.Entry2SecondaryFieldHex);
        Assert.Equal(102, candidate.Entry2SecondaryFieldValue);
        Assert.Equal(6, candidate.Entry2EffectiveFieldOffset);
        Assert.Equal("66 00", candidate.Entry2EffectiveFieldHex);
        Assert.Equal(102, candidate.Entry2EffectiveFieldValue);
        Assert.Equal("secondary field", candidate.Entry2EffectiveFieldSource);
        Assert.Equal("00 01 00 ff", candidate.ContinuationPrefixHex);
        Assert.Equal("00 01 00", candidate.ContinuationPrefixBeforeMarkerHex);
        Assert.Equal("ff", candidate.ContinuationMarkerHex);
        Assert.Equal(10, candidate.PostMarkerZeroRun);
        Assert.Equal(14, candidate.PostMarkerFirstFieldOffset);
        Assert.Equal("08 00", candidate.PostMarkerFirstFieldHex);
        Assert.Equal(8, candidate.PostMarkerFirstFieldValue);
        Assert.Equal("long-zero post-marker u16", candidate.PostMarkerFieldLayoutKind);

        Protocol03SelectorFfRepeatListDecodedRow decodedRow = Assert.Single(
            PacketResearcher.BuildProtocol03SelectorFfRepeatListDecodedRows(new[] { candidate }));
        Assert.Equal("decode two-entry stride-18 list before continuation marker", decodedRow.ParserAction);
        Assert.Equal("continuation-local +3 marker/flag candidate", decodedRow.ContinuationMarkerRole);
        Assert.Equal("vendor.txt", decodedRow.File);
        Assert.Equal(6, decodedRow.Line);
        Assert.Equal("server", decodedRow.Direction);
        Assert.Equal(18, decodedRow.RepeatStride);
        Assert.Equal(2, decodedRow.RepeatRecordCount);
        Assert.Equal(36, decodedRow.ContinuationOffset);
        Assert.Equal(16, decodedRow.ContinuationBytes);
        Assert.Equal(3, decodedRow.ContinuationMarkerOffset);
        Assert.Equal("ff", decodedRow.ContinuationMarkerHex);
        Assert.Equal("00 01 00", decodedRow.ContinuationPrefixBeforeMarkerHex);
        Assert.Equal("00 01 00 ff", decodedRow.ContinuationPrefixHex);
        Assert.Equal(11, decodedRow.Entry1FieldOffset);
        Assert.Equal("a0 13", decodedRow.Entry1FieldHex);
        Assert.Equal(5024, decodedRow.Entry1FieldValue);
        Assert.Equal(1, decodedRow.Entry2FieldOffset);
        Assert.Equal("ff 00", decodedRow.Entry2FieldHex);
        Assert.Equal(255, decodedRow.Entry2FieldValue);
        Assert.Equal(6, decodedRow.Entry2SecondaryFieldOffset);
        Assert.Equal("66 00", decodedRow.Entry2SecondaryFieldHex);
        Assert.Equal(102, decodedRow.Entry2SecondaryFieldValue);
        Assert.Equal(6, decodedRow.Entry2EffectiveFieldOffset);
        Assert.Equal("66 00", decodedRow.Entry2EffectiveFieldHex);
        Assert.Equal(102, decodedRow.Entry2EffectiveFieldValue);
        Assert.Equal("secondary field", decodedRow.Entry2EffectiveFieldSource);
        Assert.Equal(14, decodedRow.PostMarkerFirstFieldOffset);
        Assert.Equal("08 00", decodedRow.PostMarkerFirstFieldHex);
        Assert.Equal(8, decodedRow.PostMarkerFirstFieldValue);

        AbilityDefinition[] abilityDefinitions = { new(8, 8, "EightAbility", null, "abilities.txt", 1) };
        ItemCommandEntry[] itemEntries = { new(5024, "TestItem", "Test Display", "items.txt", 1) };
        GameObjectEntry[] gameObjectEntries = { new("TestObject", 5024, "gameobjects.csv", 1) };
        AnimationDefinition[] animationDefinitions =
        {
            new("a013", "13a0", 5024, "TestAnimation", "animations.txt", 1),
            new("6600", "0066", 102, "EffectiveAnimation", "animations.txt", 2)
        };

        Protocol03SelectorFfRepeatListFieldSummary[] summaries = PacketResearcher.BuildProtocol03SelectorFfRepeatListFieldSummaries(
            new[] { candidate },
            abilityDefinitions,
            itemEntries,
            gameObjectEntries,
            animationDefinitions).ToArray();
        Assert.Equal(4, summaries.Length);
        Protocol03SelectorFfRepeatListFieldSummary entry1 = Assert.Single(summaries, summary => summary.FieldRole == "entry 1");
        Assert.Equal(1, entry1.RoleOrder);
        Assert.Equal(10, entry1.ZeroRun);
        Assert.Equal(11, entry1.FieldOffset);
        Assert.Equal("a0 13", entry1.FieldHex);
        Assert.Equal(5024, entry1.FieldValue);
        Assert.Equal("long-zero-padded entry u16", entry1.FieldLayoutKind);
        Assert.Equal(new[] { "ff 00 00 00 00 00 00 00" }, entry1.LeadHexes);
        Assert.Equal(1, entry1.CandidateCount);
        Assert.Equal(1, entry1.UpdateCounts["3"]);
        Assert.Equal(1, entry1.FirstSelectors["0c"]);
        Assert.Equal(1, entry1.ContinuationPrefixes["00 01 00 ff"]);
        Assert.Equal(1, entry1.SegmentClassifications["unknown selector payload"]);
        Assert.Contains("item:5024 TestItem \"Test Display\"", entry1.ResourceReferences);
        Assert.Contains("gameobject:5024 TestObject", entry1.ResourceReferences);
        Assert.Contains("animation:a0 13 TestAnimation", entry1.ResourceReferences);
        Assert.Equal("vendor.txt", entry1.SampleFile);
        Assert.Equal(6, entry1.SampleLine);
        Assert.Equal(51, entry1.SamplePositionOffset);
        Assert.Equal(candidate.PositionHex, entry1.SamplePositionHex);

        Protocol03SelectorFfRepeatListFieldSummary entry2 = Assert.Single(summaries, summary => summary.FieldRole == "entry 2");
        Assert.Equal(2, entry2.RoleOrder);
        Assert.Equal("ff 00", entry2.FieldHex);
        Assert.Equal(255, entry2.FieldValue);
        Assert.Equal("marker-adjacent entry u16", entry2.FieldLayoutKind);

        Protocol03SelectorFfRepeatListFieldSummary entry2Effective = Assert.Single(summaries, summary => summary.FieldRole == "entry 2 effective");
        Assert.Equal(3, entry2Effective.RoleOrder);
        Assert.Equal(0, entry2Effective.ZeroRun);
        Assert.Equal(6, entry2Effective.FieldOffset);
        Assert.Equal("66 00", entry2Effective.FieldHex);
        Assert.Equal(102, entry2Effective.FieldValue);
        Assert.Equal("effective secondary entry u16", entry2Effective.FieldLayoutKind);
        Assert.Equal(new[] { "ff ff 00 00 00 00 66 00" }, entry2Effective.LeadHexes);
        Assert.Equal(1, entry2Effective.CandidateCount);
        Assert.Equal(new[] { "animation:66 00 EffectiveAnimation" }, entry2Effective.ResourceReferences);

        Protocol03SelectorFfRepeatListFieldSummary postMarker = Assert.Single(summaries, summary => summary.FieldRole == "post-marker");
        Assert.Equal(4, postMarker.RoleOrder);
        Assert.Equal(10, postMarker.ZeroRun);
        Assert.Equal(14, postMarker.FieldOffset);
        Assert.Equal("08 00", postMarker.FieldHex);
        Assert.Equal(8, postMarker.FieldValue);
        Assert.Equal("long-zero post-marker u16", postMarker.FieldLayoutKind);
        Assert.Equal(new[] { "ability:8 EightAbility" }, postMarker.ResourceReferences);

        Protocol03SelectorFfRepeatListShapeSummary shape = Assert.Single(PacketResearcher.BuildProtocol03SelectorFfRepeatListShapeSummaries(
            new[] { candidate },
            abilityDefinitions,
            itemEntries,
            gameObjectEntries,
            animationDefinitions));
        Assert.Equal(18, shape.RepeatStride);
        Assert.Equal(2, shape.RepeatRecordCount);
        Assert.Equal(36, shape.ContinuationOffset);
        Assert.Equal("00 01 00 ff", shape.ContinuationPrefixHex);
        Assert.Equal("a0 13", shape.Entry1FieldHex);
        Assert.Equal(5024, shape.Entry1FieldValue);
        Assert.Equal("long-zero-padded entry u16", shape.Entry1FieldLayoutKind);
        Assert.Contains("item:5024 TestItem \"Test Display\"", shape.Entry1ResourceReferences);
        Assert.Contains("gameobject:5024 TestObject", shape.Entry1ResourceReferences);
        Assert.Contains("animation:a0 13 TestAnimation", shape.Entry1ResourceReferences);
        Assert.Equal("ff 00", shape.Entry2FieldHex);
        Assert.Equal(255, shape.Entry2FieldValue);
        Assert.Equal("marker-adjacent entry u16", shape.Entry2FieldLayoutKind);
        Assert.Equal(1, shape.Entry2SecondaryFields["66 00 / 102"]);
        Assert.Equal(1, shape.Entry2EffectiveFields["66 00 / 102"]);
        Assert.Empty(shape.Entry2ResourceReferences);
        Assert.Equal("08 00", shape.PostMarkerFirstFieldHex);
        Assert.Equal(8, shape.PostMarkerFirstFieldValue);
        Assert.Equal("long-zero post-marker u16", shape.PostMarkerFieldLayoutKind);
        Assert.Equal(new[] { "ability:8 EightAbility" }, shape.PostMarkerResourceReferences);
        Assert.Equal(1, shape.CandidateCount);
        Assert.Equal(1, shape.UpdateCounts["3"]);
        Assert.Equal(1, shape.FirstSelectors["0c"]);
        Protocol03SelectorFfRepeatListShapePositionSample positionSample = Assert.Single(shape.PositionSamples);
        Assert.Equal(candidate.PositionHex, positionSample.PositionHex);
        Assert.Equal(17595.543, positionSample.X, 3);

        Protocol03SelectorFfRepeatListLayoutSummary layout = Assert.Single(PacketResearcher.BuildProtocol03SelectorFfRepeatListLayoutSummaries(new[] { shape }));
        Assert.Equal("long-zero-padded entry u16", layout.Entry1FieldLayoutKind);
        Assert.Equal("marker-adjacent entry u16", layout.Entry2FieldLayoutKind);
        Assert.Equal("00 01 00 ff", layout.ContinuationPrefixHex);
        Assert.Equal("long-zero post-marker u16", layout.PostMarkerFieldLayoutKind);
        Assert.Equal(1, layout.CandidateCount);
        Assert.Equal(1, layout.ShapeCount);
        Assert.Equal(1, layout.Entry1Fields["a0 13 / 5024"]);
        Assert.Equal(1, layout.Entry2Fields["ff 00 / 255"]);
        Assert.Equal(1, layout.Entry2SecondaryFields["66 00 / 102"]);
        Assert.Equal(1, layout.Entry2EffectiveFields["66 00 / 102"]);
        Assert.Equal(1, layout.PostMarkerFields["08 00 / 8"]);
        Assert.Equal(1, layout.FirstSelectors["0c"]);

        Protocol03SelectorFfRepeatListEffectiveShapeSummary effectiveShape = Assert.Single(PacketResearcher.BuildProtocol03SelectorFfRepeatListEffectiveShapeSummaries(
            new[] { candidate },
            abilityDefinitions,
            itemEntries,
            gameObjectEntries,
            animationDefinitions));
        Assert.Equal("a0 13", effectiveShape.Entry1FieldHex);
        Assert.Equal("66 00", effectiveShape.Entry2EffectiveFieldHex);
        Assert.Equal(102, effectiveShape.Entry2EffectiveFieldValue);
        Assert.Equal("08 00", effectiveShape.PostMarkerFirstFieldHex);
        Assert.Equal(1, effectiveShape.Entry2EffectiveSources["secondary field"]);
        Assert.Equal(1, effectiveShape.Entry2PrimaryFields["ff 00 / 255"]);
        Assert.Equal(1, effectiveShape.Entry2SecondaryFields["66 00 / 102"]);
        Assert.Equal(1, effectiveShape.Entry2FieldLayouts["marker-adjacent entry u16"]);
        Assert.Equal(new[] { "animation:66 00 EffectiveAnimation" }, effectiveShape.Entry2EffectiveResourceReferences);
        Assert.Contains("item:5024 TestItem \"Test Display\"", effectiveShape.Entry1ResourceReferences);
        Assert.Equal(new[] { "ability:8 EightAbility" }, effectiveShape.PostMarkerResourceReferences);

        Protocol03SelectorFfRepeatListEntryPairSummary entryPair = Assert.Single(PacketResearcher.BuildProtocol03SelectorFfRepeatListEntryPairSummaries(
            new[] { candidate },
            abilityDefinitions,
            itemEntries,
            gameObjectEntries,
            animationDefinitions));
        Assert.Equal(18, entryPair.RepeatStride);
        Assert.Equal(2, entryPair.RepeatRecordCount);
        Assert.Equal(36, entryPair.ContinuationOffset);
        Assert.Equal("00 01 00 ff", entryPair.ContinuationPrefixHex);
        Assert.Equal("a0 13", entryPair.Entry1FieldHex);
        Assert.Equal(5024, entryPair.Entry1FieldValue);
        Assert.Equal("66 00", entryPair.Entry2EffectiveFieldHex);
        Assert.Equal(102, entryPair.Entry2EffectiveFieldValue);
        Assert.Equal(1, entryPair.CandidateCount);
        Assert.Equal(1, entryPair.DistinctPostMarkerFieldCount);
        Assert.Equal(1, entryPair.Entry2EffectiveSources["secondary field"]);
        Assert.Equal(1, entryPair.Entry1Layouts["long-zero-padded entry u16"]);
        Assert.Equal(1, entryPair.Entry2Layouts["marker-adjacent entry u16"]);
        Assert.Equal(1, entryPair.PostMarkerFields["08 00 / 8"]);
        Assert.Equal(1, entryPair.PostMarkerLayouts["long-zero post-marker u16"]);
        Assert.Equal(1, entryPair.CaptureScopes["vendor/market"]);
        Assert.Contains("gameobject:5024 TestObject", entryPair.Entry1ResourceReferences);
        Assert.Equal(1, entryPair.Entry2EffectiveResourceReferences["animation:66 00 EffectiveAnimation"]);
        Assert.Equal(1, entryPair.PostMarkerResourceReferences["ability:8 EightAbility"]);

        Protocol03SelectorFfRepeatListContinuationMarkerSummary markerSummary = Assert.Single(PacketResearcher.BuildProtocol03SelectorFfRepeatListContinuationMarkerSummaries(
            new[] { candidate }));
        Assert.Equal("00 01 00", markerSummary.ContinuationPrefixBeforeMarkerHex);
        Assert.Equal("ff", markerSummary.ContinuationMarkerHex);
        Assert.Equal(10, markerSummary.PostMarkerZeroRun);
        Assert.Equal(14, markerSummary.PostMarkerFirstFieldOffset);
        Assert.Equal("08 00", markerSummary.PostMarkerFirstFieldHex);
        Assert.Equal(8, markerSummary.PostMarkerFirstFieldValue);
        Assert.Equal("long-zero post-marker u16", markerSummary.PostMarkerFieldLayoutKind);
        Assert.Equal(1, markerSummary.CandidateCount);
        Assert.Equal(1, markerSummary.ShapeCount);
        Assert.Equal(0, markerSummary.CandidatesWithHeaderWindows);
        Assert.Equal(0, markerSummary.CandidatesWithVendorHeaderWindows);
        Assert.Equal(0, markerSummary.HeaderWindowCount);
        Assert.Equal(0, markerSummary.VendorHeaderWindowCount);
        Assert.Empty(markerSummary.HeaderRegions);
        Assert.Empty(markerSummary.HeaderWindows);
        Assert.Empty(markerSummary.VendorHeaderWindows);
        Assert.Equal(1, markerSummary.ContinuationPrefixes["00 01 00 ff"]);
        Assert.Equal(1, markerSummary.Entry1Fields["a0 13 / 5024"]);
        Assert.Equal(1, markerSummary.Entry2PrimaryFields["ff 00 / 255"]);
        Assert.Equal(1, markerSummary.Entry2SecondaryFields["66 00 / 102"]);
        Assert.Equal(1, markerSummary.Entry2EffectiveFields["66 00 / 102"]);
        Assert.Equal(1, markerSummary.Entry2EffectiveSources["secondary field"]);
        Assert.Empty(markerSummary.Entry1ResourceReferences);
        Assert.Empty(markerSummary.Entry2EffectiveResourceReferences);
        Assert.Empty(markerSummary.PostMarkerResourceReferences);
        Assert.Empty(markerSummary.VendorEntry1ResourceReferences);
        Assert.Empty(markerSummary.VendorEntry2EffectiveResourceReferences);
        Assert.Empty(markerSummary.VendorPostMarkerResourceReferences);
        Assert.Equal(1, markerSummary.PostMarkerLeadHexes["00 00 00 00 00 00 00 00"]);
        Assert.Equal(1, markerSummary.FirstSelectors["0c"]);
        Protocol03SelectorFfRepeatListShapePositionSample markerPosition = Assert.Single(markerSummary.PositionSamples);
        Assert.Equal(candidate.PositionHex, markerPosition.PositionHex);

        RpcHeaderEntry[] localHeaders =
        {
            new("local", "03", "server", "SERVER_VENDOR_OPEN", "SERVER_VENDOR_OPEN", 102, "66 00", "NetworkProtocolHeaders.cs", 1)
        };
        Protocol03SelectorFfRepeatListContinuationMarkerSummary markerHeaderSummary = Assert.Single(PacketResearcher.BuildProtocol03SelectorFfRepeatListContinuationMarkerSummaries(
            new[] { candidate },
            new[] { dump },
            localHeaders,
            abilityDefinitions,
            itemEntries,
            gameObjectEntries,
            animationDefinitions));
        Assert.Equal(1, markerHeaderSummary.CandidatesWithHeaderWindows);
        Assert.Equal(1, markerHeaderSummary.CandidatesWithVendorHeaderWindows);
        Assert.Equal(1, markerHeaderSummary.HeaderWindowCount);
        Assert.Equal(1, markerHeaderSummary.VendorHeaderWindowCount);
        Assert.Equal(1, markerHeaderSummary.HeaderRegions["repeat entry 2"]);
        Assert.Equal(1, markerHeaderSummary.Entry1ResourceReferences["item:5024 TestItem \"Test Display\""]);
        Assert.Equal(1, markerHeaderSummary.Entry1ResourceReferences["gameobject:5024 TestObject"]);
        Assert.Equal(1, markerHeaderSummary.Entry1ResourceReferences["animation:a0 13 TestAnimation"]);
        Assert.Equal(1, markerHeaderSummary.Entry2EffectiveResourceReferences["animation:66 00 EffectiveAnimation"]);
        Assert.Equal(1, markerHeaderSummary.PostMarkerResourceReferences["ability:8 EightAbility"]);
        Assert.Equal(1, markerHeaderSummary.VendorEntry1ResourceReferences["item:5024 TestItem \"Test Display\""]);
        Assert.Equal(1, markerHeaderSummary.VendorEntry1ResourceReferences["gameobject:5024 TestObject"]);
        Assert.Equal(1, markerHeaderSummary.VendorEntry1ResourceReferences["animation:a0 13 TestAnimation"]);
        Assert.Equal(1, markerHeaderSummary.VendorEntry2EffectiveResourceReferences["animation:66 00 EffectiveAnimation"]);
        Assert.Equal(1, markerHeaderSummary.VendorPostMarkerResourceReferences["ability:8 EightAbility"]);
        Protocol03SelectorFfRepeatListHeaderWindowSummary markerVendorWindow = Assert.Single(markerHeaderSummary.VendorHeaderWindows);
        Assert.Equal("SERVER_VENDOR_OPEN", markerVendorWindow.Name);
        Assert.Equal(99, markerVendorWindow.PayloadOffset);
        Assert.Equal(1, markerVendorWindow.Count);

        Protocol03SelectorFfRepeatListContinuationParserActionSummary continuationAction = Assert.Single(
            PacketResearcher.BuildProtocol03SelectorFfRepeatListContinuationParserActionSummaries(new[] { markerHeaderSummary }));
        Assert.Equal("model +3 ff as continuation marker; keep vendor windows as collision controls", continuationAction.ParserAction);
        Assert.Equal("stable marker plus vendor-looking byte windows that prior boundary work treats as collision controls", continuationAction.ActionReason);
        Assert.Equal(1, continuationAction.MarkerRowCount);
        Assert.Equal(1, continuationAction.CandidateCount);
        Assert.Equal(1, continuationAction.ShapeCount);
        Assert.Equal(1, continuationAction.CandidatesWithHeaderWindows);
        Assert.Equal(1, continuationAction.CandidatesWithVendorHeaderWindows);
        Assert.Equal(1, continuationAction.HeaderWindowCount);
        Assert.Equal(1, continuationAction.VendorHeaderWindowCount);
        Assert.Equal(1, continuationAction.ContinuationPrefixBeforeMarkers["00 01 00"]);
        Assert.Equal(1, continuationAction.ContinuationPrefixes["00 01 00 ff"]);
        Assert.Equal(1, continuationAction.PostMarkerLayouts["long-zero post-marker u16"]);
        Assert.Equal(1, continuationAction.PostMarkerFields["08 00 / 8"]);
        Assert.Equal(1, continuationAction.Entry1Fields["a0 13 / 5024"]);
        Assert.Equal(1, continuationAction.Entry2PrimaryFields["ff 00 / 255"]);
        Assert.Equal(1, continuationAction.Entry2SecondaryFields["66 00 / 102"]);
        Assert.Equal(1, continuationAction.Entry2EffectiveFields["66 00 / 102"]);
        Assert.Equal(1, continuationAction.Entry2EffectiveSources["secondary field"]);
        Assert.Equal(1, continuationAction.HeaderRegions["repeat entry 2"]);
        Assert.Equal(1, continuationAction.VendorHeaderWindows["SERVER_VENDOR_OPEN [66 00 server]"]);
        Assert.Equal(1, continuationAction.FirstSelectors["0c"]);
        Assert.Equal("vendor.txt", continuationAction.SampleFile);
        Assert.Equal(6, continuationAction.SampleLine);

        Protocol03SelectorFfRepeatListParserCoverageSummary parserCoverage = Assert.Single(
            PacketResearcher.BuildProtocol03SelectorFfRepeatListParserCoverageSummaries(new[] { continuationAction }));
        Assert.Equal("decode selector-ff two-entry repeat list plus +3 ff continuation marker", parserCoverage.ParserCase);
        Assert.Equal("covered with vendor-window collision controls", parserCoverage.CoverageDisposition);
        Assert.Equal(1, parserCoverage.ActionRowCount);
        Assert.Equal(1, parserCoverage.MarkerRowCount);
        Assert.Equal(1, parserCoverage.CoveredCandidateCount);
        Assert.Equal(1, parserCoverage.ShapeCount);
        Assert.Equal(1, parserCoverage.CandidatesWithHeaderWindows);
        Assert.Equal(1, parserCoverage.CandidatesWithVendorHeaderWindows);
        Assert.Equal(1, parserCoverage.HeaderWindowCount);
        Assert.Equal(1, parserCoverage.VendorHeaderWindowCount);
        Assert.Equal(1, parserCoverage.ParserActions["model +3 ff as continuation marker; keep vendor windows as collision controls"]);
        Assert.Equal(1, parserCoverage.ActionReasons["stable marker plus vendor-looking byte windows that prior boundary work treats as collision controls"]);
        Assert.Equal(1, parserCoverage.ContinuationPrefixBeforeMarkers["00 01 00"]);
        Assert.Equal(1, parserCoverage.ContinuationPrefixes["00 01 00 ff"]);
        Assert.Equal(1, parserCoverage.PostMarkerLayouts["long-zero post-marker u16"]);
        Assert.Equal(1, parserCoverage.PostMarkerFields["08 00 / 8"]);
        Assert.Equal(1, parserCoverage.Entry1Fields["a0 13 / 5024"]);
        Assert.Equal(1, parserCoverage.Entry2EffectiveFields["66 00 / 102"]);
        Assert.Equal(1, parserCoverage.Entry2EffectiveSources["secondary field"]);
        Assert.Equal(1, parserCoverage.HeaderRegions["repeat entry 2"]);
        Assert.Equal(1, parserCoverage.VendorHeaderWindows["SERVER_VENDOR_OPEN [66 00 server]"]);
        Assert.Equal(1, parserCoverage.FirstSelectors["0c"]);
        Assert.Equal("vendor.txt", parserCoverage.SampleFile);
        Assert.Equal(6, parserCoverage.SampleLine);

        Protocol03SelectorFfRepeatListEffectiveShapeHeaderSummary headerShape = Assert.Single(PacketResearcher.BuildProtocol03SelectorFfRepeatListEffectiveShapeHeaderSummaries(
            new[] { candidate },
            new[] { dump },
            localHeaders));
        Assert.Equal("a0 13", headerShape.Entry1FieldHex);
        Assert.Equal("66 00", headerShape.Entry2EffectiveFieldHex);
        Assert.Equal("08 00", headerShape.PostMarkerFirstFieldHex);
        Assert.Equal(1, headerShape.CandidateCount);
        Assert.Equal(1, headerShape.CandidatesWithHeaderWindows);
        Assert.Equal(1, headerShape.CandidatesWithVendorHeaderWindows);
        Protocol03SelectorFfRepeatListHeaderWindowSummary vendorWindow = Assert.Single(headerShape.VendorHeaderWindows);
        Assert.Equal(99, vendorWindow.PayloadOffset);
        Assert.Equal("SERVER_VENDOR_OPEN", vendorWindow.Name);
        Assert.Equal("server", vendorWindow.Direction);
        Assert.Equal("66 00", vendorWindow.EncodedHeader);
        Assert.Equal(new[] { "03" }, vendorWindow.Protocols);
        Assert.Equal(1, vendorWindow.Count);
        Assert.Equal(1, vendorWindow.ObjectOffsets["+117"]);
        Assert.Equal(1, vendorWindow.WindowHexes["00 00 66 00"]);

        Protocol03SelectorFfRepeatListHeaderNameSummary headerName = Assert.Single(PacketResearcher.BuildProtocol03SelectorFfRepeatListHeaderNameSummaries(
            new[] { candidate },
            new[] { dump },
            localHeaders));
        Assert.Equal("SERVER_VENDOR_OPEN", headerName.Name);
        Assert.Equal("server", headerName.Direction);
        Assert.Equal("66 00", headerName.EncodedHeader);
        Assert.Equal(new[] { "03" }, headerName.Protocols);
        Assert.Equal(1, headerName.CandidateCount);
        Assert.Equal(1, headerName.HeaderWindowCount);
        Assert.Equal(1, headerName.ShapeCount);
        Assert.True(headerName.IsVendorHeader);
        Assert.Equal(1, headerName.PayloadOffsets["+99"]);
        Assert.Equal(1, headerName.ObjectOffsets["+117"]);
        Assert.Equal(1, headerName.WindowHexes["00 00 66 00"]);
        Assert.Equal(1, headerName.Entry1Fields["a0 13 / 5024"]);
        Assert.Equal(1, headerName.Entry2EffectiveFields["66 00 / 102"]);
        Assert.Equal(1, headerName.PostMarkerFields["08 00 / 8"]);
        Assert.Equal(1, headerName.HeaderRegions["repeat entry 2"]);
        Protocol03SelectorFfRepeatListHeaderNameShapeSummary headerNameShape = Assert.Single(headerName.TopShapes);
        Assert.Equal("a0 13", headerNameShape.Entry1FieldHex);
        Assert.Equal("66 00", headerNameShape.Entry2EffectiveFieldHex);
        Assert.Equal("08 00", headerNameShape.PostMarkerFirstFieldHex);
        Assert.Equal(1, headerNameShape.CandidateCount);
        Assert.Equal(1, headerNameShape.HeaderWindowCount);

        Protocol03SelectorFfRepeatListHeaderRegionSummary headerRegion = Assert.Single(PacketResearcher.BuildProtocol03SelectorFfRepeatListHeaderRegionSummaries(
            new[] { candidate },
            new[] { dump },
            localHeaders,
            abilityDefinitions,
            itemEntries,
            gameObjectEntries,
            animationDefinitions));
        Assert.Equal("SERVER_VENDOR_OPEN", headerRegion.Name);
        Assert.Equal("server", headerRegion.Direction);
        Assert.Equal("66 00", headerRegion.EncodedHeader);
        Assert.Equal(new[] { "03" }, headerRegion.Protocols);
        Assert.Equal("repeat entry 2", headerRegion.HeaderRegion);
        Assert.Equal(1, headerRegion.CandidateCount);
        Assert.Equal(1, headerRegion.HeaderWindowCount);
        Assert.Equal(1, headerRegion.ShapeCount);
        Assert.True(headerRegion.IsVendorHeader);
        Assert.Equal(1, headerRegion.PayloadOffsets["+99"]);
        Assert.Equal(1, headerRegion.ObjectOffsets["+117"]);
        Assert.Equal(1, headerRegion.WindowHexes["00 00 66 00"]);
        Assert.Equal(1, headerRegion.Entry1Fields["a0 13 / 5024"]);
        Assert.Equal(1, headerRegion.Entry2EffectiveFields["66 00 / 102"]);
        Assert.Equal(1, headerRegion.PostMarkerFields["08 00 / 8"]);
        Assert.Equal(1, headerRegion.Entry1ResourceReferences["item:5024 TestItem \"Test Display\""]);
        Assert.Equal(1, headerRegion.Entry1ResourceReferences["gameobject:5024 TestObject"]);
        Assert.Equal(1, headerRegion.Entry1ResourceReferences["animation:a0 13 TestAnimation"]);
        Assert.Equal(1, headerRegion.Entry2EffectiveResourceReferences["animation:66 00 EffectiveAnimation"]);
        Assert.Equal(1, headerRegion.PostMarkerResourceReferences["ability:8 EightAbility"]);
        Protocol03SelectorFfRepeatListHeaderNameShapeSummary headerRegionShape = Assert.Single(headerRegion.TopShapes);
        Assert.Equal("a0 13", headerRegionShape.Entry1FieldHex);
        Assert.Equal("66 00", headerRegionShape.Entry2EffectiveFieldHex);

        Protocol03SelectorFfRepeatListVendorBoundaryTargetSummary vendorTarget = Assert.Single(PacketResearcher.BuildProtocol03SelectorFfRepeatListVendorBoundaryTargetSummaries(
            new[] { candidate },
            new[] { dump },
            localHeaders,
            abilityDefinitions,
            itemEntries,
            gameObjectEntries,
            animationDefinitions));
        Assert.Equal("SERVER_VENDOR_OPEN", vendorTarget.Name);
        Assert.Equal("server", vendorTarget.Direction);
        Assert.Equal("66 00", vendorTarget.EncodedHeader);
        Assert.Equal(new[] { "03" }, vendorTarget.Protocols);
        Assert.Equal("repeat entry 2", vendorTarget.HeaderRegion);
        Assert.Equal(99, vendorTarget.HeaderPayloadOffset);
        Assert.Equal(117, vendorTarget.HeaderObjectOffset);
        Assert.Equal(1, vendorTarget.CandidateCount);
        Assert.Equal(1, vendorTarget.HeaderWindowCount);
        Assert.Equal(1, vendorTarget.ShapeCount);
        Assert.Equal(1, vendorTarget.TargetInterpretations["repeat entry 2 secondary field overlap"]);
        Assert.Equal(1, vendorTarget.TargetLocalOffsets["repeat entry 2 +6"]);
        Assert.Equal(1, vendorTarget.TargetBodySpanBytes["not body-span target"]);
        Assert.Equal(1, vendorTarget.PositionOffsets["+51"]);
        Assert.Equal(1, vendorTarget.WindowHexes["00 00 66 00"]);
        Assert.Equal(1, vendorTarget.ContinuationPrefixes["00 01 00 ff"]);
        Assert.Equal(1, vendorTarget.Entry1Fields["a0 13 / 5024"]);
        Assert.Equal(1, vendorTarget.Entry1ResourceReferences["item:5024 TestItem \"Test Display\""]);
        Assert.Equal(1, vendorTarget.Entry2EffectiveFields["66 00 / 102"]);
        Assert.Equal(1, vendorTarget.Entry2EffectiveResourceReferences["animation:66 00 EffectiveAnimation"]);
        Assert.Equal(1, vendorTarget.PostMarkerFields["08 00 / 8"]);
        Assert.Equal(1, vendorTarget.PostMarkerResourceReferences["ability:8 EightAbility"]);
        Assert.Equal(1, vendorTarget.FirstSelectors["0c"]);
        Protocol03SelectorFfRepeatListHeaderNameShapeSummary vendorTargetShape = Assert.Single(vendorTarget.TopShapes);
        Assert.Equal("a0 13", vendorTargetShape.Entry1FieldHex);
        Assert.Equal("66 00", vendorTargetShape.Entry2EffectiveFieldHex);

        Protocol03SelectorFfRepeatListVendorBoundaryInterpretationSummary vendorInterpretation = Assert.Single(PacketResearcher.BuildProtocol03SelectorFfRepeatListVendorBoundaryInterpretationSummaries(
            new[] { vendorTarget }));
        Assert.Equal("repeat entry 2 secondary field overlap", vendorInterpretation.TargetInterpretation);
        Assert.Equal("modeled field overlap", vendorInterpretation.ParserPriority);
        Assert.True(vendorInterpretation.IsModeledFieldOverlap);
        Assert.Equal(1, vendorInterpretation.TargetCount);
        Assert.Equal(1, vendorInterpretation.CandidateCount);
        Assert.Equal(1, vendorInterpretation.HeaderWindowCount);
        Assert.Equal(1, vendorInterpretation.ShapeCount);
        Assert.Equal(1, vendorInterpretation.Headers["SERVER_VENDOR_OPEN [66 00 server]"]);
        Assert.Equal(1, vendorInterpretation.HeaderRegions["repeat entry 2"]);
        Assert.Equal(1, vendorInterpretation.PayloadOffsets["+99"]);
        Assert.Equal(1, vendorInterpretation.ObjectOffsets["+117"]);
        Assert.Equal(1, vendorInterpretation.TargetLocalOffsets["repeat entry 2 +6"]);
        Assert.Equal(1, vendorInterpretation.TargetBodySpanBytes["not body-span target"]);
        Assert.Equal(1, vendorInterpretation.PositionOffsets["+51"]);
        Assert.Equal(1, vendorInterpretation.Entry1Fields["a0 13 / 5024"]);
        Assert.Equal(1, vendorInterpretation.Entry2EffectiveFields["66 00 / 102"]);
        Assert.Equal(1, vendorInterpretation.PostMarkerFields["08 00 / 8"]);
        Protocol03SelectorFfRepeatListVendorBoundaryInterpretationTargetSummary interpretationTarget = Assert.Single(vendorInterpretation.TopTargets);
        Assert.Equal("SERVER_VENDOR_OPEN", interpretationTarget.Name);
        Assert.Equal("repeat entry 2", interpretationTarget.HeaderRegion);
        Assert.Equal(99, interpretationTarget.HeaderPayloadOffset);
        Assert.Equal(117, interpretationTarget.HeaderObjectOffset);
        Assert.Equal(1, interpretationTarget.TargetLocalOffsets["repeat entry 2 +6"]);
        Assert.Equal(1, interpretationTarget.TargetBodySpanBytes["not body-span target"]);
        Assert.Equal(1, interpretationTarget.PositionOffsets["+51"]);

        Protocol03SelectorFfRepeatListVendorBoundaryPrioritySummary vendorPriority = Assert.Single(PacketResearcher.BuildProtocol03SelectorFfRepeatListVendorBoundaryPrioritySummaries(
            new[] { vendorTarget }));
        Assert.Equal("modeled field overlap", vendorPriority.ParserPriority);
        Assert.True(vendorPriority.IsModeledFieldOverlap);
        Assert.Equal(1, vendorPriority.TargetCount);
        Assert.Equal(1, vendorPriority.CandidateCount);
        Assert.Equal(1, vendorPriority.HeaderWindowCount);
        Assert.Equal(1, vendorPriority.ShapeCount);
        Assert.Equal(1, vendorPriority.TargetInterpretations["repeat entry 2 secondary field overlap"]);
        Assert.Equal(1, vendorPriority.Headers["SERVER_VENDOR_OPEN [66 00 server]"]);
        Assert.Equal(1, vendorPriority.TargetLocalOffsets["repeat entry 2 +6"]);
        Assert.Equal(1, vendorPriority.TargetBodySpanBytes["not body-span target"]);
        Assert.Equal(1, vendorPriority.Entry2EffectiveFields["66 00 / 102"]);

        Protocol03SelectorFfRepeatListVendorBoundaryDecisionRow vendorDecision = Assert.Single(
            PacketResearcher.BuildProtocol03SelectorFfRepeatListVendorBoundaryDecisionRows(new[] { vendorTarget }));
        Assert.Equal("suppress nested vendor RPC as modeled selector-ff field overlap", vendorDecision.ParserDecision);
        Assert.Equal("modeled field overlap", vendorDecision.EvidenceDisposition);
        Assert.Equal("modeled field overlap", vendorDecision.ParserPriority);
        Assert.True(vendorDecision.IsModeledFieldOverlap);
        Assert.Equal("repeat entry 2 secondary field overlap", vendorDecision.TargetInterpretation);
        Assert.Equal("SERVER_VENDOR_OPEN", vendorDecision.Name);
        Assert.Equal(99, vendorDecision.HeaderPayloadOffset);
        Assert.Equal(117, vendorDecision.HeaderObjectOffset);
        Assert.Equal(1, vendorDecision.TargetLocalOffsets["repeat entry 2 +6"]);
        Assert.Equal(1, vendorDecision.TargetBodySpanBytes["not body-span target"]);
        Assert.Empty(vendorDecision.LowHalfResourceReferences);
        Assert.Empty(vendorDecision.NearbyU16LengthMatches);

        Protocol03SelectorFfRepeatListHeaderSample headerSample = Assert.Single(PacketResearcher.BuildProtocol03SelectorFfRepeatListHeaderSamples(
            new[] { candidate },
            new[] { dump },
            localHeaders));
        Assert.Equal("SERVER_VENDOR_OPEN", headerSample.Name);
        Assert.Equal("server", headerSample.Direction);
        Assert.Equal("66 00", headerSample.EncodedHeader);
        Assert.Equal(new[] { "03" }, headerSample.Protocols);
        Assert.True(headerSample.IsVendorHeader);
        Assert.Equal("vendor.txt", headerSample.File);
        Assert.Equal(6, headerSample.Line);
        Assert.Equal("server", headerSample.PacketDirection);
        Assert.Equal(18, headerSample.SelectorOffset);
        Assert.Equal(99, headerSample.HeaderPayloadOffset);
        Assert.Equal(117, headerSample.HeaderObjectOffset);
        Assert.Equal("00 00 66 00", headerSample.HeaderWindowHex);
        Assert.Equal("repeat entry 2", headerSample.HeaderRegion);
        Assert.Equal("repeat entry 2 secondary field overlap", headerSample.HeaderInterpretation);
        Assert.Equal("repeat entry 2 +6", headerSample.TargetLocalOffset);
        Assert.StartsWith("01 43 00 00", headerSample.PayloadPrefixHex, StringComparison.Ordinal);
        Assert.Contains("66 00", headerSample.HeaderContextHex, StringComparison.Ordinal);
        Assert.StartsWith("ff 00 00 00", headerSample.RepeatListContextHex, StringComparison.Ordinal);
        Assert.Equal("a0 13", headerSample.Entry1FieldHex);
        Assert.Equal("66 00", headerSample.Entry2EffectiveFieldHex);
        Assert.Equal("secondary field", headerSample.Entry2EffectiveFieldSource);
        Assert.Equal("00 01 00 ff", headerSample.ContinuationPrefixHex);
        Assert.Equal("08 00", headerSample.PostMarkerFirstFieldHex);
        Assert.Equal(51, headerSample.PositionOffset);
        Assert.Equal(candidate.PositionHex, headerSample.PositionHex);

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
            new[] { dump })
        {
            Protocol03SelectorFfRepeatListCandidates = new[] { candidate },
            Protocol03SelectorFfRepeatListDecodedRows = new[] { decodedRow },
            Protocol03SelectorFfRepeatListFieldSummaries = summaries,
            Protocol03SelectorFfRepeatListShapeSummaries = new[] { shape },
            Protocol03SelectorFfRepeatListEffectiveShapeSummaries = new[] { effectiveShape },
            Protocol03SelectorFfRepeatListEntryPairSummaries = new[] { entryPair },
            Protocol03SelectorFfRepeatListContinuationMarkerSummaries = new[] { markerHeaderSummary },
            Protocol03SelectorFfRepeatListContinuationParserActionSummaries = new[] { continuationAction },
            Protocol03SelectorFfRepeatListParserCoverageSummaries = new[] { parserCoverage },
            Protocol03SelectorFfRepeatListEffectiveShapeHeaderSummaries = new[] { headerShape },
            Protocol03SelectorFfRepeatListHeaderNameSummaries = new[] { headerName },
            Protocol03SelectorFfRepeatListHeaderRegionSummaries = new[] { headerRegion },
            Protocol03SelectorFfRepeatListVendorBoundaryTargetSummaries = new[] { vendorTarget },
            Protocol03SelectorFfRepeatListVendorBoundaryInterpretationSummaries = new[] { vendorInterpretation },
            Protocol03SelectorFfRepeatListVendorBoundaryPrioritySummaries = new[] { vendorPriority },
            Protocol03SelectorFfRepeatListVendorBoundaryDecisionRows = new[] { vendorDecision },
            Protocol03SelectorFfRepeatListHeaderSamples = new[] { headerSample },
            Protocol03SelectorFfRepeatListLayoutSummaries = new[] { layout }
        };
        string json = System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

        Assert.Contains("\"Protocol03SelectorFfRepeatListCandidates\"", json);
        Assert.Contains("\"Protocol03SelectorFfRepeatListDecodedRows\"", json);
        Assert.Contains("\"Unknown6dPacketContextSummaries\"", json);
        Assert.Contains("\"Unknown80c1PacketContextSummaries\"", json);
        Assert.Contains("\"Protocol03SelectorFfRepeatListFieldSummaries\"", json);
        Assert.Contains("\"Protocol03SelectorFfRepeatListShapeSummaries\"", json);
        Assert.Contains("\"Protocol03SelectorFfRepeatListEffectiveShapeSummaries\"", json);
        Assert.Contains("\"Protocol03SelectorFfRepeatListEntryPairSummaries\"", json);
        Assert.Contains("\"Protocol03SelectorFfRepeatListContinuationMarkerSummaries\"", json);
        Assert.Contains("\"Protocol03SelectorFfRepeatListContinuationParserActionSummaries\"", json);
        Assert.Contains("\"Protocol03SelectorFfRepeatListParserCoverageSummaries\"", json);
        Assert.Contains("\"Protocol03SelectorFfRepeatListEffectiveShapeHeaderSummaries\"", json);
        Assert.Contains("\"Protocol03SelectorFfRepeatListHeaderNameSummaries\"", json);
        Assert.Contains("\"Protocol03SelectorFfRepeatListHeaderRegionSummaries\"", json);
        Assert.Contains("\"Protocol03SelectorFfRepeatListVendorBoundaryTargetSummaries\"", json);
        Assert.Contains("\"Protocol03SelectorFfRepeatListVendorBoundaryInterpretationSummaries\"", json);
        Assert.Contains("\"Protocol03SelectorFfRepeatListVendorBoundaryPrioritySummaries\"", json);
        Assert.Contains("\"Protocol03SelectorFfRepeatListVendorBoundaryDecisionRows\"", json);
        Assert.Contains("\"Protocol03SelectorFfRepeatListVendorContinuationBodyOffsetSummaries\"", json);
        Assert.Contains("\"Protocol03SelectorFfRepeatListVendorPrePositionBodyOffsetSummaries\"", json);
        Assert.Contains("\"Protocol03SelectorFfRepeatListVendorBodyWindowProbeSummaries\"", json);
        Assert.Contains("\"Protocol03SelectorFfRepeatListVendorBodyWindowResourceCollisionSummaries\"", json);
        Assert.Contains("\"Protocol03SelectorFfRepeatListVendorBodyWindowEvidenceSummaries\"", json);
        Assert.Contains("\"Protocol03SelectorFfRepeatListVendorBodyWindowParserActionSummaries\"", json);
        Assert.Contains("\"Protocol03SelectorFfRepeatListHeaderSamples\"", json);
        Assert.Contains("\"Protocol03SelectorFfRepeatListLayoutSummaries\"", json);
        Assert.Contains("\"Protocol03NestedMovementBoundaryEvidenceSummaries\"", json);
        Assert.Contains("\"Protocol03NestedMovementMode06BoundaryCandidateSummaries\"", json);
        Assert.Contains("\"Protocol03NestedMovementMode06FfContinuationSummaries\"", json);
        Assert.Contains("\"Protocol03NestedMovementMode06PostContinuationBoundarySummaries\"", json);
        Assert.Contains("\"Protocol03NestedMovementMode06PostContinuationBodySummaries\"", json);
        Assert.Contains("\"Protocol03NestedMovementMode06PostContinuationPrefixSummaries\"", json);
        Assert.Contains("\"Protocol03NestedMovementMode06PostContinuationRemainderSummaries\"", json);
        Assert.Contains("\"Protocol03NestedMovementMode06PostContinuationTupleSummaries\"", json);
        Assert.Contains("\"Protocol03NestedMovementMode06PostContinuationTupleBoundarySummaries\"", json);
        Assert.Contains("\"Protocol03NestedMovementMode06PostContinuationTupleBodySummaries\"", json);
        Assert.Contains("\"Protocol03NestedMovementMode06PostContinuationTerminalTupleBodySummaries\"", json);
        Assert.Contains("\"Protocol03NestedMovementMode06LongTupleBodyTailAnchorSummaries\"", json);
        Assert.Contains("\"Protocol03NestedMovementMode06LongTupleBodyPreAnchorLeadSummaries\"", json);
        Assert.Contains("\"Protocol03NestedMovementMode06LongTupleBodyPreAnchorBodySummaries\"", json);
        Assert.Contains("\"Protocol03NestedMovementMode06LongTupleBodyPreAnchorOffsetSummaries\"", json);
        Assert.Contains("\"Protocol03NestedMovementMode06LongTupleBodyPreAnchorHeaderCandidateSummaries\"", json);
        Assert.Contains("\"Protocol03NestedMovementMode06LongTupleBodyPreAnchorEmbeddedMovementSummaries\"", json);
        Assert.Contains("\"Protocol03NestedMovementMode06LongTupleBodyPreAnchorEmbeddedMovementTrailerSummaries\"", json);
        Assert.Contains("\"Protocol03NestedMovementMode06LongTupleBodyPreAnchorEmbeddedMovementTrailerShapeSummaries\"", json);
        Assert.Contains("\"Protocol03NestedMovementMode06UnsupportedEmbeddedControlSelectorSummaries\"", json);
        Assert.Contains("\"Protocol03NestedMovementMode06HeldTupleBodySummaries\"", json);
        Assert.Contains("\"Protocol03NestedMovementMode06Marker34LongBodyFixtureSummaries\"", json);
        Assert.Contains("\"Protocol03NestedMovementMode06Marker34LongBodyOffsetSummaries\"", json);
        Assert.Contains("\"Protocol03NestedMovementMode06Marker34LongBodyInteriorWindowSummaries\"", json);
        Assert.Contains("\"Protocol03NestedMovementMode06Marker34LongBodyKnownFxWindowSummaries\"", json);
        Assert.Contains("\"Protocol03NestedMovementMode06Marker34LongBodyFxFieldRoleSummaries\"", json);
        Assert.Contains("\"Protocol03NestedMovementMode06Marker34LongBodyStructuralRoleSummaries\"", json);
        Assert.Contains("\"Protocol03NestedMovementMode06Marker34LongBodyLayoutSummaries\"", json);
        Assert.Contains("\"Protocol03NestedMovementMode06Marker34LongBodyParserReadinessSummaries\"", json);
        Assert.Contains("\"Protocol03NestedMovementMode06Marker34LongBodyNestedReplaySummaries\"", json);
        Assert.Contains("\"Protocol03NestedMovementMode06Marker34LongBodyFieldWindowSummaries\"", json);
        Assert.Contains("\"Protocol03NestedMovementMode06Marker34LongBodyEmbeddedMovementVectorSummaries\"", json);
        Assert.Contains("\"Protocol03NestedMovementMode06HeldTupleBodyPrioritySummaries\"", json);
        Assert.Contains("\"Protocol03NestedMovementMode06TupleBodyParserActionSummaries\"", json);
        Assert.Contains("\"Protocol03NestedMovementParserActionSummaries\"", json);
        Assert.Contains("\"TargetInterpretations\"", json);
        Assert.Contains("\"TargetBodySpanBytes\"", json);
        Assert.Contains("\"TargetBodyHexes\"", json);
        Assert.Contains("\"ParserPriority\": \"modeled field overlap\"", json);
        Assert.Contains("\"TargetLocalOffsets\"", json);
        Assert.Contains("\"PositionOffsets\"", json);
        Assert.Contains("repeat entry 2 \\u002B6", json);
        Assert.Contains("repeat entry 2 secondary field overlap", json);
        Assert.Contains("\"RepeatStride\": 18", json);
        Assert.Contains("\"PostMarkerFirstFieldValue\": 8", json);
        Assert.Contains("\"FieldRole\": \"post-marker\"", json);
        Assert.Contains("\"FieldRole\": \"entry 2 effective\"", json);
        Assert.Contains("\"FieldValue\": 8", json);
        Assert.Contains("\"FieldValue\": 102", json);
        Assert.Contains("\"FieldLayoutKind\": \"effective secondary entry u16\"", json);
        Assert.Contains("\"FieldLayoutKind\": \"marker-adjacent entry u16\"", json);
        Assert.Contains("\"Entry2SecondaryFieldHex\": \"66 00\"", json);
        Assert.Contains("\"Entry2EffectiveFieldHex\": \"66 00\"", json);
        Assert.Contains("\"Entry2EffectiveFields\"", json);
        Assert.Contains("\"Entry2SecondaryFields\"", json);
        Assert.Contains("\"ResourceReferences\"", json);
        Assert.Contains("\"Entry1ResourceReferences\"", json);
        Assert.Contains("animation:a0 13 TestAnimation", json);
    }

    [Fact]
    public void BuildProtocol03SelectorFfRepeatListVendorContinuationBodyOffsetSummaries_GroupsUnresolvedTargets()
    {
        byte[] bytes = PacketResearcher.ParseHexBytes(
            "02 03 25 00 03 0c 93 78 00 00 00 00 00 00 00 00 00 00 00 ff " +
            "01 43 00 00 00 00 00 00 00 e0 2d 81 0d 22 89 20 00 0b 0c 11 79 80 00 00 01 00 00 00 00 12 01 00 02 ff 00 00 00 00 ba 13 63 00 00 87 43 00 00 c3 96 63 07 00 00 00 c0 e2 2e d1 40 00 00 00 00 00 f0 7e 40 00 00 00 00 02 1f a3 40 ff 00 00 00 00 00 00 00 00 00 00 a0 13 00 00 00 00 00 ff ff 00 00 00 00 66 00 00 00 00 00 00 00 00 00 00 00 00 01 00 ff 00 00 00 00 00 00 00 00 00 00 08 00 99 88")
            .ToArray();
        Protocol03ObjectViewSample sample = PacketResearcher.DetectProtocol03ObjectViews(bytes, 6, "server").Single();
        PacketDumpFileSummary dump = new(
            "vendor-continuation.txt",
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
        Protocol03SelectorFfRepeatListCandidate candidate = Assert.Single(PacketResearcher.BuildProtocol03SelectorFfRepeatListCandidates(new[] { dump }));
        RpcHeaderEntry[] localHeaders =
        {
            new("local", "03", "server", "SERVER_VENDOR_TAIL", "SERVER_VENDOR_TAIL", 0x8899, "99 88", "NetworkProtocolHeaders.cs", 1)
        };

        Protocol03SelectorFfRepeatListVendorContinuationBodyOffsetSummary summary = Assert.Single(
            PacketResearcher.BuildProtocol03SelectorFfRepeatListVendorContinuationBodyOffsetSummaries(
                new[] { candidate },
                new[] { dump },
                localHeaders));

        Assert.Equal(16, summary.ContinuationOffset);
        Assert.Equal(12, summary.PostMarkerOffset);
        Assert.Equal(1, summary.CandidateCount);
        Assert.Equal(1, summary.HeaderWindowCount);
        Assert.Equal(1, summary.ShapeCount);
        Assert.Equal(1, summary.Headers["SERVER_VENDOR_TAIL [99 88 server]"]);
        Assert.Equal(1, summary.PayloadOffsets["+127"]);
        Assert.Equal(1, summary.ObjectOffsets["+145"]);
        Assert.Equal(1, summary.WindowHexes["08 00 99 88"]);
        Assert.Equal(1, summary.TargetBodyHexes["00 00 08 00 99 88"]);
        Assert.Equal(1, summary.ContinuationBytes["18"]);
        Assert.Equal(1, summary.BytesAfterHeaderInContinuation["0"]);
        Assert.Equal(1, summary.HeaderTailClasses["terminal after header"]);
        Assert.Equal(1, summary.ContinuationPrefixes["00 01 00 ff"]);
        Assert.Equal(1, summary.Entry1Fields["a0 13 / 5024"]);
        Assert.Equal(1, summary.Entry2EffectiveFields["66 00 / 102"]);
        Assert.Equal(1, summary.PostMarkerFields["08 00 / 8"]);
        Assert.Equal(1, summary.PositionOffsets["+51"]);
        Assert.Equal(1, summary.FirstSelectors["0c"]);

        Protocol03SelectorFfRepeatListVendorBoundaryTargetSummary terminalTarget = Assert.Single(PacketResearcher.BuildProtocol03SelectorFfRepeatListVendorBoundaryTargetSummaries(
            new[] { candidate },
            new[] { dump },
            localHeaders));
        Assert.Equal(1, terminalTarget.TargetInterpretations["continuation terminal-tail overlap"]);
        Assert.Equal(1, terminalTarget.TargetLocalOffsets["continuation +16 / post-marker +12"]);
        Assert.Equal(1, terminalTarget.TargetBodySpanBytes["continuation tail 0"]);
        Assert.Equal(1, terminalTarget.TargetBodyHexes["00 00 08 00 99 88"]);

        Protocol03SelectorFfRepeatListVendorBoundaryInterpretationSummary terminalInterpretation = Assert.Single(PacketResearcher.BuildProtocol03SelectorFfRepeatListVendorBoundaryInterpretationSummaries(
            new[] { terminalTarget }));
        Assert.Equal("continuation terminal-tail overlap", terminalInterpretation.TargetInterpretation);
        Assert.Equal("terminal continuation overlap", terminalInterpretation.ParserPriority);
        Assert.True(terminalInterpretation.IsModeledFieldOverlap);
        Assert.Equal(1, terminalInterpretation.TargetBodySpanBytes["continuation tail 0"]);
        Assert.Equal(1, terminalInterpretation.TargetBodyHexes["00 00 08 00 99 88"]);
        Protocol03SelectorFfRepeatListVendorBoundaryInterpretationTargetSummary terminalInterpretationTarget = Assert.Single(terminalInterpretation.TopTargets);
        Assert.Equal(1, terminalInterpretationTarget.TargetBodyHexes["00 00 08 00 99 88"]);
    }

    [Fact]
    public void BuildProtocol03SelectorFfRepeatListVendorBoundaryInterpretationSummaries_SplitsContinuationBodySpans()
    {
        byte[] bytes = PacketResearcher.ParseHexBytes(
            "02 03 25 00 03 0c 93 78 00 00 00 00 00 00 00 00 00 00 00 ff " +
            "01 43 00 00 00 00 00 00 00 e0 2d 81 0d 22 89 20 00 0b 0c 11 79 80 00 00 01 00 00 00 00 12 01 00 02 ff 00 00 00 00 ba 13 63 00 00 87 43 00 00 c3 96 63 07 00 00 00 c0 e2 2e d1 40 00 00 00 00 00 f0 7e 40 00 00 00 00 02 1f a3 40 ff 00 00 00 00 00 00 00 00 00 00 a0 13 00 00 00 00 00 ff ff 00 00 00 00 66 00 00 00 00 00 00 00 00 00 00 00 00 01 00 ff 00 00 00 00 00 00 00 00 00 00 08 00 99 88")
            .ToArray();
        Protocol03ObjectViewSample parsedSample = PacketResearcher.DetectProtocol03ObjectViews(bytes, 6, "server").Single();
        PacketDumpFileSummary parsedDump = new(
            "vendor-continuation-base.txt",
            1,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            Array.Empty<Protocol04PacketSequenceSample>(),
            new[] { parsedSample },
            Array.Empty<Protocol04InteractionPayloadSample>(),
            Array.Empty<PlayerAttributePayloadSample>(),
            Array.Empty<ManageBonusPayloadSample>());
        Protocol03SelectorFfRepeatListCandidate parsedCandidate = Assert.Single(PacketResearcher.BuildProtocol03SelectorFfRepeatListCandidates(new[] { parsedDump }));

        byte[] payload = new byte[180];
        payload[96] = 0x4e;
        payload[97] = 0x00;
        payload[98] = 0x01;
        payload[99] = 0x00;
        payload[100] = 0xaa;
        payload[101] = 0xbb;
        payload[130] = 0xcc;
        payload[131] = 0xdd;
        string payloadHex = string.Join(" ", payload.Select(value => value.ToString("x2")));
        Protocol03VariableSelectorLead lead = new("ff", 18, payload.Length, "00 ff", payloadHex);
        Protocol03ObjectViewSample sample = new(
            7,
            "server",
            "02 03",
            25,
            3,
            "0c",
            "synthetic",
            "02 03 25",
            Array.Empty<Protocol03StringSample>(),
            Array.Empty<Protocol03NamedProfileRecord>(),
            Array.Empty<Protocol03PlayerCharacterCreationRecord>(),
            Array.Empty<Protocol03EffectEmoteLead>(),
            Array.Empty<Protocol03PositionLikeLead>(),
            Array.Empty<Protocol03NestedMovementLead>(),
            Array.Empty<Protocol03MovementStateTailLead>(),
            new[] { lead },
            Array.Empty<Protocol03SelfViewAttributeLead>(),
            Array.Empty<Protocol03StaticObjectLead>(),
            1,
            0,
            false,
            Array.Empty<Protocol03ObjectUpdateSegment>());
        PacketDumpFileSummary dump = new(
            "vendor-continuation-spans.txt",
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
        Protocol03SelectorFfRepeatListCandidate candidate = parsedCandidate with
        {
            File = "vendor-continuation-spans.txt",
            Line = 7,
            SelectorOffset = 18,
            PositionOffset = 20,
            RepeatStride = 18,
            RepeatRecordCount = 2,
            ContinuationOffset = 36,
            ContinuationBytes = 100,
            ContinuationMarkerHex = "ff",
            PostMarkerFirstFieldOffset = null,
            PostMarkerFirstFieldHex = string.Empty,
            PostMarkerFirstFieldValue = null
        };
        RpcHeaderEntry[] localHeaders =
        {
            new("local", "03", "server", "SERVER_VENDOR_DEEP_BODY", "SERVER_VENDOR_DEEP_BODY", 0xbbaa, "aa bb", "NetworkProtocolHeaders.cs", 1),
            new("local", "03", "server", "SERVER_VENDOR_MID_BODY", "SERVER_VENDOR_MID_BODY", 0xddcc, "cc dd", "NetworkProtocolHeaders.cs", 2)
        };

        Protocol03SelectorFfRepeatListVendorBoundaryTargetSummary[] targets = PacketResearcher.BuildProtocol03SelectorFfRepeatListVendorBoundaryTargetSummaries(
                new[] { candidate },
                new[] { dump },
                localHeaders)
            .ToArray();
        Assert.Equal(2, targets.Length);

        Protocol03SelectorFfRepeatListVendorBoundaryInterpretationSummary[] interpretations = PacketResearcher.BuildProtocol03SelectorFfRepeatListVendorBoundaryInterpretationSummaries(targets)
            .OrderBy(summary => summary.TargetInterpretation, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        Assert.Equal(2, interpretations.Length);
        Assert.Equal("continuation deep-body unresolved", interpretations[0].TargetInterpretation);
        Assert.Equal("deep-body boundary candidate", interpretations[0].ParserPriority);
        Assert.Equal(1, interpretations[0].TargetLocalOffsets["continuation +20 / post-marker +16"]);
        Assert.Equal(1, interpretations[0].TargetBodySpanBytes["continuation tail 78"]);
        Assert.Equal(1, interpretations[0].TargetBodyHexes["4e 00 01 00 aa bb 00 00 00 00 00 00 00 00 00 00 00 00"]);
        Assert.Equal("continuation mid-body unresolved", interpretations[1].TargetInterpretation);
        Assert.Equal("mid-body boundary candidate", interpretations[1].ParserPriority);
        Assert.Equal(1, interpretations[1].TargetLocalOffsets["continuation +50 / post-marker +46"]);
        Assert.Equal(1, interpretations[1].TargetBodySpanBytes["continuation tail 48"]);
        Assert.Equal(1, interpretations[1].TargetBodyHexes["00 00 00 00 cc dd 00 00 00 00 00 00 00 00 00 00 00 00"]);

        Protocol03SelectorFfRepeatListVendorBoundaryPrioritySummary[] priorities = PacketResearcher.BuildProtocol03SelectorFfRepeatListVendorBoundaryPrioritySummaries(targets)
            .OrderBy(summary => summary.ParserPriority, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        Assert.Equal(2, priorities.Length);
        Assert.Equal("deep-body boundary candidate", priorities[0].ParserPriority);
        Assert.False(priorities[0].IsModeledFieldOverlap);
        Assert.Equal(1, priorities[0].TargetInterpretations["continuation deep-body unresolved"]);
        Assert.Equal(1, priorities[0].TargetBodySpanBytes["continuation tail 78"]);
        Assert.Equal(1, priorities[0].TargetBodyHexes["4e 00 01 00 aa bb 00 00 00 00 00 00 00 00 00 00 00 00"]);
        Assert.Equal("mid-body boundary candidate", priorities[1].ParserPriority);
        Assert.False(priorities[1].IsModeledFieldOverlap);
        Assert.Equal(1, priorities[1].TargetInterpretations["continuation mid-body unresolved"]);
        Assert.Equal(1, priorities[1].TargetBodySpanBytes["continuation tail 48"]);
        Assert.Equal(1, priorities[1].TargetBodyHexes["00 00 00 00 cc dd 00 00 00 00 00 00 00 00 00 00 00 00"]);

        Protocol03SelectorFfRepeatListVendorBodyWindowProbeSummary[] probes = PacketResearcher.BuildProtocol03SelectorFfRepeatListVendorBodyWindowProbeSummaries(
                new[] { candidate },
                new[] { dump },
                localHeaders,
                gameObjects: new[] { new GameObjectEntry("SyntheticWindowField", 1, "gameobjects.csv", 1) })
            .OrderBy(summary => summary.BodyOffset)
            .ToArray();
        Assert.Equal(2, probes.Length);
        Assert.Equal("continuation body", probes[0].BodyRegion);
        Assert.Equal("deep-body boundary candidate", probes[0].ParserPriority);
        Assert.Equal("continuation deep-body unresolved", probes[0].TargetInterpretation);
        Assert.Equal("SERVER_VENDOR_DEEP_BODY", probes[0].Name);
        Assert.Equal("continuation +20 / post-marker +16", probes[0].TargetLocalOffset);
        Assert.Equal(20, probes[0].BodyOffset);
        Assert.Equal(0, probes[0].BodyOffsetModulo2);
        Assert.Equal(0, probes[0].BodyOffsetModulo4);
        Assert.Equal(1, probes[0].BytesBeforeHeaderInBody["20"]);
        Assert.Equal(1, probes[0].BytesAfterHeaderInBody["78"]);
        Assert.Equal(1, probes[0].BodyByteCounts["100"]);
        Assert.Equal(1, probes[0].TargetBodySpanBytes["continuation tail 78"]);
        Assert.Equal(1, probes[0].TargetBodyHexes["4e 00 01 00 aa bb 00 00 00 00 00 00 00 00 00 00 00 00"]);
        Assert.Equal(1, probes[0].WindowU32Values["01 00 aa bb / 3148480513"]);
        Assert.Equal(1, probes[0].HeaderU16Values["aa bb / 48042"]);
        Assert.Equal(1, probes[0].WindowHalfU16ResourceReferences["low 01 00 / 1 -> gameobject:1 SyntheticWindowField"]);
        Assert.Equal(1, probes[0].NearbyU16Values["-4 4e 00 / 78"]);
        Assert.Equal(1, probes[0].NearbyU16LengthMatches["-4 4e 00 / 78 = bytes after header"]);

        Assert.Equal("continuation body", probes[1].BodyRegion);
        Assert.Equal("mid-body boundary candidate", probes[1].ParserPriority);
        Assert.Equal("continuation mid-body unresolved", probes[1].TargetInterpretation);
        Assert.Equal("SERVER_VENDOR_MID_BODY", probes[1].Name);
        Assert.Equal("continuation +50 / post-marker +46", probes[1].TargetLocalOffset);
        Assert.Equal(50, probes[1].BodyOffset);
        Assert.Equal(0, probes[1].BodyOffsetModulo2);
        Assert.Equal(2, probes[1].BodyOffsetModulo4);
        Assert.Equal(1, probes[1].BytesBeforeHeaderInBody["50"]);
        Assert.Equal(1, probes[1].BytesAfterHeaderInBody["48"]);
        Assert.Equal(1, probes[1].BodyByteCounts["100"]);
        Assert.Equal(1, probes[1].TargetBodySpanBytes["continuation tail 48"]);
        Assert.Equal(1, probes[1].TargetBodyHexes["00 00 00 00 cc dd 00 00 00 00 00 00 00 00 00 00 00 00"]);
        Assert.Equal(1, probes[1].WindowU32Values["00 00 cc dd / 3721134080"]);
        Assert.Equal(1, probes[1].HeaderU16Values["cc dd / 56780"]);

        Protocol03SelectorFfRepeatListVendorBodyWindowResourceCollisionSummary collision = Assert.Single(
            PacketResearcher.BuildProtocol03SelectorFfRepeatListVendorBodyWindowResourceCollisionSummaries(probes));
        Assert.Equal("low", collision.WindowHalf);
        Assert.Equal("01 00 / 1", collision.WindowU16);
        Assert.Equal("gameobject", collision.ResourceKind);
        Assert.Equal("gameobject:1 SyntheticWindowField", collision.ResourceReference);
        Assert.Equal(1, collision.ProbeRowCount);
        Assert.Equal(1, collision.ReferenceWindowCount);
        Assert.Equal(1, collision.BodyRegions["continuation body"]);
        Assert.Equal(1, collision.ParserPriorities["deep-body boundary candidate"]);
        Assert.Equal(1, collision.Headers["SERVER_VENDOR_DEEP_BODY aa bb"]);
        Assert.Equal(1, collision.TargetLocalOffsets["continuation +20 / post-marker +16"]);
        Assert.Equal(1, collision.BodyOffsets["+20"]);
        Assert.Equal(1, collision.TargetBodySpanBytes["continuation tail 78"]);
        Assert.Equal(1, collision.WindowHexes["01 00 aa bb"]);
        Assert.Equal(1, collision.NearbyU16LengthMatches["-4 4e 00 / 78 = bytes after header"]);

        Protocol03SelectorFfRepeatListVendorBodyWindowEvidenceSummary[] evidenceSummaries = PacketResearcher.BuildProtocol03SelectorFfRepeatListVendorBodyWindowEvidenceSummaries(probes)
            .ToArray();
        Assert.Equal(2, evidenceSummaries.Length);
        Assert.Equal("nearby length-match candidate", evidenceSummaries[0].EvidenceDisposition);
        Assert.Equal(1, evidenceSummaries[0].ProbeRowCount);
        Assert.Equal(1, evidenceSummaries[0].HeaderWindowCount);
        Assert.Equal(1, evidenceSummaries[0].LowHalfResourceReferences["low 01 00 / 1 -> gameobject:1 SyntheticWindowField"]);
        Assert.Equal(1, evidenceSummaries[0].NearbyU16LengthMatches["-4 4e 00 / 78 = bytes after header"]);
        Assert.Equal("unexplained body-window candidate", evidenceSummaries[1].EvidenceDisposition);
        Assert.Equal(1, evidenceSummaries[1].ProbeRowCount);
        Assert.Equal(1, evidenceSummaries[1].HeaderWindowCount);
        Assert.Empty(evidenceSummaries[1].LowHalfResourceReferences);
        Assert.Empty(evidenceSummaries[1].HighHalfResourceReferences);
        Assert.Empty(evidenceSummaries[1].NearbyU16LengthMatches);

        Protocol03SelectorFfRepeatListVendorBodyWindowParserActionSummary[] actionSummaries =
            PacketResearcher.BuildProtocol03SelectorFfRepeatListVendorBodyWindowParserActionSummaries(evidenceSummaries)
                .ToArray();
        Assert.Equal(2, actionSummaries.Length);
        Assert.Equal("test body-length boundary", actionSummaries[0].ParserAction);
        Assert.Equal("nearby u16 matches body span or offset evidence", actionSummaries[0].ActionReason);
        Assert.Equal(1, actionSummaries[0].ProbeRowCount);
        Assert.Equal(1, actionSummaries[0].EvidenceDispositions["nearby length-match candidate"]);
        Assert.Equal(1, actionSummaries[0].NearbyU16LengthMatches["-4 4e 00 / 78 = bytes after header"]);
        Assert.Equal("queue body-length parser investigation", actionSummaries[1].ParserAction);
        Assert.Equal("no length-match or resource-collision control explains the row", actionSummaries[1].ActionReason);
        Assert.Equal(1, actionSummaries[1].ProbeRowCount);
        Assert.Equal(1, actionSummaries[1].EvidenceDispositions["unexplained body-window candidate"]);

        Protocol03SelectorFfRepeatListVendorBoundaryDecisionRow[] decisionRows =
            PacketResearcher.BuildProtocol03SelectorFfRepeatListVendorBoundaryDecisionRows(targets, probes)
                .OrderBy(row => row.ParserDecision, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        Assert.Equal(2, decisionRows.Length);

        Protocol03SelectorFfRepeatListVendorBoundaryDecisionRow queueDecision =
            Assert.Single(decisionRows, row => row.ParserDecision == "queue selector-ff body-length investigation");
        Assert.Equal("unexplained body-window candidate", queueDecision.EvidenceDisposition);
        Assert.Equal("mid-body boundary candidate", queueDecision.ParserPriority);
        Assert.Equal("continuation mid-body unresolved", queueDecision.TargetInterpretation);
        Assert.Equal("SERVER_VENDOR_MID_BODY", queueDecision.Name);
        Assert.Equal(1, queueDecision.TargetBodySpanBytes["continuation tail 48"]);
        Assert.Empty(queueDecision.LowHalfResourceReferences);
        Assert.Empty(queueDecision.NearbyU16LengthMatches);

        Protocol03SelectorFfRepeatListVendorBoundaryDecisionRow lengthDecision =
            Assert.Single(decisionRows, row => row.ParserDecision == "test selector-ff body-length boundary");
        Assert.Equal("nearby length-match candidate", lengthDecision.EvidenceDisposition);
        Assert.Equal("deep-body boundary candidate", lengthDecision.ParserPriority);
        Assert.Equal("continuation deep-body unresolved", lengthDecision.TargetInterpretation);
        Assert.Equal("SERVER_VENDOR_DEEP_BODY", lengthDecision.Name);
        Assert.Equal(1, lengthDecision.TargetBodySpanBytes["continuation tail 78"]);
        Assert.Equal(1, lengthDecision.LowHalfResourceReferences["low 01 00 / 1 -> gameobject:1 SyntheticWindowField"]);
        Assert.Equal(1, lengthDecision.NearbyU16LengthMatches["-4 4e 00 / 78 = bytes after header"]);

        Protocol03SelectorFfRepeatListVendorBodyWindowEvidenceSummary collisionOnlyEvidence = Assert.Single(
            PacketResearcher.BuildProtocol03SelectorFfRepeatListVendorBodyWindowEvidenceSummaries(
                new[]
                {
                    probes[0] with { NearbyU16LengthMatches = new Dictionary<string, int>() }
                }));
        Protocol03SelectorFfRepeatListVendorBodyWindowParserActionSummary collisionOnlyAction = Assert.Single(
            PacketResearcher.BuildProtocol03SelectorFfRepeatListVendorBodyWindowParserActionSummaries(new[] { collisionOnlyEvidence }));
        Assert.Equal("suppress nested RPC and model as packed resource-field collision", collisionOnlyAction.ParserAction);
        Assert.Equal("low half of the four-byte window resolves to item/gameobject resources", collisionOnlyAction.ActionReason);
        Assert.Equal(1, collisionOnlyAction.EvidenceDispositions["low-half item/gameobject collision control"]);
        Assert.Equal(1, collisionOnlyAction.LowHalfResourceReferences["low 01 00 / 1 -> gameobject:1 SyntheticWindowField"]);
    }

    [Fact]
    public void BuildProtocol03SelectorFfRepeatListVendorBoundaryInterpretationSummaries_SplitsPrePositionBodySpans()
    {
        byte[] bytes = PacketResearcher.ParseHexBytes(
            "02 03 25 00 03 0c 93 78 00 00 00 00 00 00 00 00 00 00 00 ff " +
            "01 43 00 00 00 00 00 00 00 e0 2d 81 0d 22 89 20 00 0b 0c 11 79 80 00 00 01 00 00 00 00 12 01 00 02 ff 00 00 00 00 ba 13 63 00 00 87 43 00 00 c3 96 63 07 00 00 00 c0 e2 2e d1 40 00 00 00 00 00 f0 7e 40 00 00 00 00 02 1f a3 40 ff 00 00 00 00 00 00 00 00 00 00 a0 13 00 00 00 00 00 ff ff 00 00 00 00 66 00 00 00 00 00 00 00 00 00 00 00 00 01 00 ff 00 00 00 00 00 00 00 00 00 00 08 00 99 88")
            .ToArray();
        Protocol03ObjectViewSample parsedSample = PacketResearcher.DetectProtocol03ObjectViews(bytes, 6, "server").Single();
        PacketDumpFileSummary parsedDump = new(
            "vendor-pre-position-base.txt",
            1,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            Array.Empty<Protocol04PacketSequenceSample>(),
            new[] { parsedSample },
            Array.Empty<Protocol04InteractionPayloadSample>(),
            Array.Empty<PlayerAttributePayloadSample>(),
            Array.Empty<ManageBonusPayloadSample>());
        Protocol03SelectorFfRepeatListCandidate parsedCandidate = Assert.Single(PacketResearcher.BuildProtocol03SelectorFfRepeatListCandidates(new[] { parsedDump }));

        byte[] payload = new byte[220];
        payload[28] = 0xaa;
        payload[29] = 0xbb;
        payload[98] = 0xcc;
        payload[99] = 0xdd;
        string payloadHex = string.Join(" ", payload.Select(value => value.ToString("x2")));
        Protocol03VariableSelectorLead lead = new("ff", 18, payload.Length, "00 ff", payloadHex);
        byte[] nearPayload = (byte[])payload.Clone();
        nearPayload[98] = 0x00;
        nearPayload[99] = 0x00;
        string nearPayloadHex = string.Join(" ", nearPayload.Select(value => value.ToString("x2")));
        Protocol03VariableSelectorLead nearLead = new("ff", 18, nearPayload.Length, "00 ff", nearPayloadHex);
        Protocol03ObjectViewSample sample = new(
            8,
            "server",
            "02 03",
            25,
            3,
            "0c",
            "synthetic",
            "02 03 25",
            Array.Empty<Protocol03StringSample>(),
            Array.Empty<Protocol03NamedProfileRecord>(),
            Array.Empty<Protocol03PlayerCharacterCreationRecord>(),
            Array.Empty<Protocol03EffectEmoteLead>(),
            Array.Empty<Protocol03PositionLikeLead>(),
            Array.Empty<Protocol03NestedMovementLead>(),
            Array.Empty<Protocol03MovementStateTailLead>(),
            new[] { lead },
            Array.Empty<Protocol03SelfViewAttributeLead>(),
            Array.Empty<Protocol03StaticObjectLead>(),
            1,
            0,
            false,
            Array.Empty<Protocol03ObjectUpdateSegment>());
        PacketDumpFileSummary dump = new(
            "vendor-pre-position-spans.txt",
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
        Protocol03ObjectViewSample nearPositionSample = sample with { Line = 9, VariableSelectorLeads = new[] { nearLead } };
        PacketDumpFileSummary nearPositionDump = new(
            "vendor-pre-position-spans-near.txt",
            1,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            Array.Empty<Protocol04PacketSequenceSample>(),
            new[] { nearPositionSample },
            Array.Empty<Protocol04InteractionPayloadSample>(),
            Array.Empty<PlayerAttributePayloadSample>(),
            Array.Empty<ManageBonusPayloadSample>());
        Protocol03SelectorFfRepeatListCandidate candidate = parsedCandidate with
        {
            File = "vendor-pre-position-spans.txt",
            Line = 8,
            SelectorOffset = 18,
            PositionOffset = 150,
            RepeatStride = 18,
            RepeatRecordCount = 2,
            ContinuationOffset = 36,
            ContinuationMarkerHex = "ff"
        };
        Protocol03SelectorFfRepeatListCandidate nearPositionCandidate = candidate with
        {
            File = "vendor-pre-position-spans-near.txt",
            Line = 9,
            PositionOffset = 31
        };
        RpcHeaderEntry[] localHeaders =
        {
            new("local", "03", "server", "SERVER_VENDOR_DEEP_PRE_POSITION", "SERVER_VENDOR_DEEP_PRE_POSITION", 0xbbaa, "aa bb", "NetworkProtocolHeaders.cs", 1),
            new("local", "03", "server", "SERVER_VENDOR_MID_PRE_POSITION", "SERVER_VENDOR_MID_PRE_POSITION", 0xddcc, "cc dd", "NetworkProtocolHeaders.cs", 2)
        };

        Protocol03SelectorFfRepeatListVendorBoundaryTargetSummary[] targets = PacketResearcher.BuildProtocol03SelectorFfRepeatListVendorBoundaryTargetSummaries(
                new[] { candidate, nearPositionCandidate },
                new[] { dump, nearPositionDump },
                localHeaders)
            .ToArray();
        Assert.Equal(2, targets.Length);

        Protocol03SelectorFfRepeatListVendorBoundaryInterpretationSummary[] interpretations = PacketResearcher.BuildProtocol03SelectorFfRepeatListVendorBoundaryInterpretationSummaries(targets)
            .OrderBy(summary => summary.TargetInterpretation, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        Assert.Equal(3, interpretations.Length);
        Assert.Equal("pre-position deep-body unresolved", interpretations[0].TargetInterpretation);
        Assert.Equal("deep-body boundary candidate", interpretations[0].ParserPriority);
        Assert.Equal(2, interpretations[0].TargetLocalOffsets["pre-position +15"]);
        Assert.Equal(1, interpretations[0].TargetBodySpanBytes["pre-position gap 120"]);
        Assert.Equal(1, interpretations[0].TargetBodyHexes["00 00 00 00 00 00 aa bb 00 00 00 00 00 00 00 00 00 00 00 00"]);
        Assert.Equal("pre-position mid-body unresolved", interpretations[1].TargetInterpretation);
        Assert.Equal("mid-body boundary candidate", interpretations[1].ParserPriority);
        Assert.Equal(1, interpretations[1].TargetLocalOffsets["pre-position +85"]);
        Assert.Equal(1, interpretations[1].TargetBodySpanBytes["pre-position gap 50"]);
        Assert.Equal(1, interpretations[1].TargetBodyHexes["00 00 00 00 00 00 cc dd 00 00 00 00 00 00 00 00 00 00 00 00"]);
        Assert.Equal("pre-position near-position overlap", interpretations[2].TargetInterpretation);
        Assert.Equal("near-position overlap", interpretations[2].ParserPriority);
        Assert.Equal(2, interpretations[2].TargetLocalOffsets["pre-position +15"]);
        Assert.Equal(1, interpretations[2].TargetBodySpanBytes["pre-position gap 1"]);
        Assert.DoesNotContain("pre-position gap 1", interpretations[0].TargetBodySpanBytes.Keys);
        Assert.DoesNotContain("pre-position gap 120", interpretations[2].TargetBodySpanBytes.Keys);

        Protocol03SelectorFfRepeatListVendorBoundaryPrioritySummary[] priorities = PacketResearcher.BuildProtocol03SelectorFfRepeatListVendorBoundaryPrioritySummaries(targets)
            .OrderBy(summary => summary.ParserPriority, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        Assert.Equal(3, priorities.Length);
        Assert.Equal("deep-body boundary candidate", priorities[0].ParserPriority);
        Assert.False(priorities[0].IsModeledFieldOverlap);
        Assert.Equal(1, priorities[0].TargetInterpretations["pre-position deep-body unresolved"]);
        Assert.Equal(1, priorities[0].TargetBodySpanBytes["pre-position gap 120"]);
        Assert.Equal(1, priorities[0].TargetBodyHexes["00 00 00 00 00 00 aa bb 00 00 00 00 00 00 00 00 00 00 00 00"]);
        Assert.Equal("mid-body boundary candidate", priorities[1].ParserPriority);
        Assert.False(priorities[1].IsModeledFieldOverlap);
        Assert.Equal(1, priorities[1].TargetInterpretations["pre-position mid-body unresolved"]);
        Assert.Equal(1, priorities[1].TargetBodySpanBytes["pre-position gap 50"]);
        Assert.Equal(1, priorities[1].TargetBodyHexes["00 00 00 00 00 00 cc dd 00 00 00 00 00 00 00 00 00 00 00 00"]);
        Assert.Equal("near-position overlap", priorities[2].ParserPriority);
        Assert.True(priorities[2].IsModeledFieldOverlap);
        Assert.Equal(1, priorities[2].TargetInterpretations["pre-position near-position overlap"]);
        Assert.Equal(1, priorities[2].TargetBodySpanBytes["pre-position gap 1"]);
        Assert.DoesNotContain("pre-position gap 120", priorities[2].TargetBodySpanBytes.Keys);

        Protocol03SelectorFfRepeatListVendorBodyWindowProbeSummary[] probes = PacketResearcher.BuildProtocol03SelectorFfRepeatListVendorBodyWindowProbeSummaries(
                new[] { candidate, nearPositionCandidate },
                new[] { dump, nearPositionDump },
                localHeaders)
            .ToArray();
        Assert.Equal(3, probes.Length);

        Protocol03SelectorFfRepeatListVendorBodyWindowProbeSummary deepProbe = Assert.Single(probes, summary => summary.TargetInterpretation == "pre-position deep-body unresolved");
        Assert.Equal("pre-position body", deepProbe.BodyRegion);
        Assert.Equal("deep-body boundary candidate", deepProbe.ParserPriority);
        Assert.Equal("SERVER_VENDOR_DEEP_PRE_POSITION", deepProbe.Name);
        Assert.Equal("pre-position +15", deepProbe.TargetLocalOffset);
        Assert.Equal(15, deepProbe.BodyOffset);
        Assert.Equal(1, deepProbe.BodyOffsetModulo2);
        Assert.Equal(3, deepProbe.BodyOffsetModulo4);
        Assert.Equal(1, deepProbe.BytesBeforeHeaderInBody["15"]);
        Assert.Equal(1, deepProbe.BytesAfterHeaderInBody["120"]);
        Assert.Equal(1, deepProbe.BodyByteCounts["137"]);
        Assert.Equal(1, deepProbe.TargetBodySpanBytes["pre-position gap 120"]);
        Assert.Equal(1, deepProbe.TargetBodyHexes["00 00 00 00 00 00 aa bb 00 00 00 00 00 00 00 00 00 00 00 00"]);
        Assert.Equal(1, deepProbe.WindowU32Values["00 00 aa bb / 3148480512"]);
        Assert.Equal(1, deepProbe.HeaderU16Values["aa bb / 48042"]);

        Protocol03SelectorFfRepeatListVendorBodyWindowProbeSummary midProbe = Assert.Single(probes, summary => summary.TargetInterpretation == "pre-position mid-body unresolved");
        Assert.Equal("pre-position body", midProbe.BodyRegion);
        Assert.Equal("mid-body boundary candidate", midProbe.ParserPriority);
        Assert.Equal("SERVER_VENDOR_MID_PRE_POSITION", midProbe.Name);
        Assert.Equal("pre-position +85", midProbe.TargetLocalOffset);
        Assert.Equal(85, midProbe.BodyOffset);
        Assert.Equal(1, midProbe.BodyOffsetModulo2);
        Assert.Equal(1, midProbe.BodyOffsetModulo4);
        Assert.Equal(1, midProbe.BytesBeforeHeaderInBody["85"]);
        Assert.Equal(1, midProbe.BytesAfterHeaderInBody["50"]);
        Assert.Equal(1, midProbe.BodyByteCounts["137"]);
        Assert.Equal(1, midProbe.TargetBodySpanBytes["pre-position gap 50"]);
        Assert.Equal(1, midProbe.TargetBodyHexes["00 00 00 00 00 00 cc dd 00 00 00 00 00 00 00 00 00 00 00 00"]);
        Assert.Equal(1, midProbe.WindowU32Values["00 00 cc dd / 3721134080"]);
        Assert.Equal(1, midProbe.HeaderU16Values["cc dd / 56780"]);

        Protocol03SelectorFfRepeatListVendorBodyWindowProbeSummary nearProbe = Assert.Single(probes, summary => summary.TargetInterpretation == "pre-position near-position overlap");
        Assert.Equal("pre-position body", nearProbe.BodyRegion);
        Assert.Equal("near-position overlap", nearProbe.ParserPriority);
        Assert.Equal("SERVER_VENDOR_DEEP_PRE_POSITION", nearProbe.Name);
        Assert.Equal("pre-position +15", nearProbe.TargetLocalOffset);
        Assert.Equal(15, nearProbe.BodyOffset);
        Assert.Equal(1, nearProbe.BodyOffsetModulo2);
        Assert.Equal(3, nearProbe.BodyOffsetModulo4);
        Assert.Equal(1, nearProbe.BytesBeforeHeaderInBody["15"]);
        Assert.Equal(1, nearProbe.BytesAfterHeaderInBody["1"]);
        Assert.Equal(1, nearProbe.BodyByteCounts["18"]);
        Assert.Equal(1, nearProbe.TargetBodySpanBytes["pre-position gap 1"]);
        Assert.Equal(1, nearProbe.WindowU32Values["00 00 aa bb / 3148480512"]);
        Assert.Equal(1, nearProbe.HeaderU16Values["aa bb / 48042"]);
    }

    [Fact]
    public void BuildProtocol03SelectorFfRepeatListVendorPrePositionBodyOffsetSummaries_GroupsUnresolvedTargets()
    {
        byte[] bytes = PacketResearcher.ParseHexBytes(
            "02 03 25 00 03 0c 93 78 00 00 00 00 00 00 00 00 00 00 00 ff " +
            "01 43 00 00 00 00 00 00 00 e0 2d 81 0d 22 89 20 00 0b 0c 11 79 80 00 00 01 00 00 00 00 12 01 00 02 ff 99 88 00 00 ba 13 63 00 00 87 43 00 00 c3 96 63 07 00 00 00 c0 e2 2e d1 40 00 00 00 00 00 f0 7e 40 00 00 00 00 02 1f a3 40 ff 00 00 00 00 00 00 00 00 00 00 a0 13 00 00 00 00 00 ff ff 00 00 00 00 66 00 00 00 00 00 00 00 00 00 00 00 00 01 00 ff 00 00 00 00 00 00 00 00 00 00 08 00")
            .ToArray();
        Protocol03ObjectViewSample sample = PacketResearcher.DetectProtocol03ObjectViews(bytes, 6, "server").Single();
        PacketDumpFileSummary dump = new(
            "vendor-pre-position.txt",
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
        Protocol03SelectorFfRepeatListCandidate candidate = Assert.Single(PacketResearcher.BuildProtocol03SelectorFfRepeatListCandidates(new[] { dump }));
        RpcHeaderEntry[] localHeaders =
        {
            new("local", "03", "server", "SERVER_VENDOR_TAIL", "SERVER_VENDOR_TAIL", 0x8899, "99 88", "NetworkProtocolHeaders.cs", 1)
        };

        Protocol03SelectorFfRepeatListVendorPrePositionBodyOffsetSummary summary = Assert.Single(
            PacketResearcher.BuildProtocol03SelectorFfRepeatListVendorPrePositionBodyOffsetSummaries(
                new[] { candidate },
                new[] { dump },
                localHeaders));

        Assert.Equal(21, summary.PrePositionOffset);
        Assert.Equal(17, summary.BytesBeforePosition);
        Assert.Equal(1, summary.BytesBeforePositions["17"]);
        Assert.Equal(1, summary.BytesAfterHeaderBeforePosition["15"]);
        Assert.Equal(1, summary.PositionGapClasses["short gap before position"]);
        Assert.Equal(1, summary.CandidateCount);
        Assert.Equal(1, summary.HeaderWindowCount);
        Assert.Equal(1, summary.ShapeCount);
        Assert.Equal(1, summary.Headers["SERVER_VENDOR_TAIL [99 88 server]"]);
        Assert.Equal(1, summary.PayloadOffsets["+34"]);
        Assert.Equal(1, summary.ObjectOffsets["+52"]);
        Assert.Equal(1, summary.WindowHexes["02 ff 99 88"]);
        Assert.Equal(1, summary.TargetBodyHexes["00 12 01 00 02 ff 99 88 00 00 ba 13 63 00 00 87 43 00 00 c3"]);
        Assert.Single(summary.ContextHexes);
        Assert.Single(summary.PrePositionHexes);
        Assert.Equal(1, summary.PositionOffsets["+51"]);
        Assert.Equal(1, summary.ContinuationPrefixes["00 01 00 ff"]);
        Assert.Equal(1, summary.Entry1Fields["a0 13 / 5024"]);
        Assert.Equal(1, summary.Entry2EffectiveFields["66 00 / 102"]);
        Assert.Equal(1, summary.PostMarkerFields["08 00 / 8"]);
        Assert.Equal(1, summary.FirstSelectors["0c"]);

        Protocol03SelectorFfRepeatListVendorBoundaryTargetSummary shortGapTarget = Assert.Single(PacketResearcher.BuildProtocol03SelectorFfRepeatListVendorBoundaryTargetSummaries(
            new[] { candidate },
            new[] { dump },
            localHeaders));
        Assert.Equal(1, shortGapTarget.TargetInterpretations["pre-position short-gap body"]);
        Assert.Equal(1, shortGapTarget.TargetLocalOffsets["pre-position +21"]);
        Assert.Equal(1, shortGapTarget.TargetBodyHexes["00 12 01 00 02 ff 99 88 00 00 ba 13 63 00 00 87 43 00 00 c3"]);

        Protocol03SelectorFfRepeatListVendorBoundaryInterpretationSummary shortGapInterpretation = Assert.Single(PacketResearcher.BuildProtocol03SelectorFfRepeatListVendorBoundaryInterpretationSummaries(
            new[] { shortGapTarget }));
        Assert.Equal("pre-position short-gap body", shortGapInterpretation.TargetInterpretation);
        Assert.Equal("short-span boundary candidate", shortGapInterpretation.ParserPriority);
        Assert.False(shortGapInterpretation.IsModeledFieldOverlap);
        Assert.Equal(1, shortGapInterpretation.TargetBodyHexes["00 12 01 00 02 ff 99 88 00 00 ba 13 63 00 00 87 43 00 00 c3"]);

        RpcHeaderEntry[] nearPositionHeaders =
        {
            new("local", "03", "server", "SERVER_VENDOR_NEAR_POSITION", "SERVER_VENDOR_NEAR_POSITION", 0x0763, "63 07", "NetworkProtocolHeaders.cs", 1)
        };
        Protocol03SelectorFfRepeatListVendorBoundaryTargetSummary nearPositionTarget = Assert.Single(PacketResearcher.BuildProtocol03SelectorFfRepeatListVendorBoundaryTargetSummaries(
            new[] { candidate },
            new[] { dump },
            nearPositionHeaders));
        Assert.Equal(1, nearPositionTarget.TargetInterpretations["pre-position near-position overlap"]);
        Assert.Equal(1, nearPositionTarget.TargetLocalOffsets["pre-position +36"]);

        Protocol03SelectorFfRepeatListVendorBoundaryInterpretationSummary nearPositionInterpretation = Assert.Single(PacketResearcher.BuildProtocol03SelectorFfRepeatListVendorBoundaryInterpretationSummaries(
            new[] { nearPositionTarget }));
        Assert.Equal("pre-position near-position overlap", nearPositionInterpretation.TargetInterpretation);
        Assert.Equal("near-position overlap", nearPositionInterpretation.ParserPriority);
        Assert.True(nearPositionInterpretation.IsModeledFieldOverlap);
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
        Assert.Equal(string.Empty, record.PostCreationObjectViewPreHeaderHex);
        Assert.Equal(string.Empty, record.PostCreationObjectViewHeaderHex);
        Assert.Equal(string.Empty, record.PostCreationObjectViewHeaderClassification);
        Assert.Equal(0, record.PostCreationObjectViewParsedUpdateCount);
        Assert.Equal(0, record.PostCreationObjectViewUnparsedBytes);
        Assert.Equal(0, record.PostCreationObjectViewConsumedBytes);
        Assert.Equal(string.Empty, record.PostCreationObjectViewParseStatus);
        Assert.Equal(string.Empty, record.PostCreationObjectViewDynamicTailPrefixHex);
        Assert.False(record.PostCreationObjectViewComplete);
    }

    [Fact]
    public void DetectProtocol03ObjectViews_CapturesNamedProfilePostCreationObjectViewBoundary()
    {
        List<byte> bytes = PacketResearcher.ParseHexBytes("02 03 01 00 0c 57 02 e2 cd ab 04 8e").ToList();
        bytes.AddRange(FixedAscii("Gold Blood Champion", 32));
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 10 00 00 22 40 01 17 00 00 01 00 0c 57 02 e3 cd ab 04 8e"));

        Protocol03ObjectViewSample sample = PacketResearcher.DetectProtocol03ObjectViews(bytes, 60, "server").Single();

        Protocol03NamedProfileRecord record = Assert.Single(sample.NamedProfileRecords);
        Assert.Equal("17 00 00 01 00 0c 57 02", record.PostCreationAttributePrefixHex);
        Assert.Equal(3, record.PostCreationObjectViewHeaderOffset);
        Assert.Equal("17 00 00", record.PostCreationObjectViewPreHeaderHex);
        Assert.Equal("01 00 0c 57 02 e3 cd ab", record.PostCreationObjectViewHeaderHex);
        Assert.Equal("NPC_BASE dynamic creation candidate", record.PostCreationObjectViewHeaderClassification);
        Assert.Equal("three-byte zero-tail boundary", record.PostCreationObjectViewBoundaryFamily);
        Assert.Equal(0, record.PostCreationObjectViewParsedUpdateCount);
        Assert.Equal(6, record.PostCreationObjectViewUnparsedBytes);
        Assert.Equal(4, record.PostCreationObjectViewConsumedBytes);
        Assert.Equal("dynamic creation name slot incomplete", record.PostCreationObjectViewParseStatus);
        Assert.Equal(string.Empty, record.PostCreationObjectViewDynamicTailPrefixHex);
        Assert.False(record.PostCreationObjectViewComplete);
    }

    [Fact]
    public void DetectProtocol03ObjectViews_MeasuresNestedNpcBasePostCreationObjectViewBoundary()
    {
        List<byte> bytes = PacketResearcher.ParseHexBytes("02 03 01 00 0c 57 02 e2 cd ab 04 8e").ToList();
        bytes.AddRange(FixedAscii("Gold Blood Champion", 32));
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 10 00 00 22 40 01 17 00 00 01 00 0c 57 02 e3 cd ab 04 8e"));
        bytes.AddRange(FixedAscii("Gold Blood Enforcer", 32));
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 10 00 00 22 40 01 03 00 00 00 00 04 01 01 20 08 3e 3a"));

        Protocol03ObjectViewSample sample = PacketResearcher.DetectProtocol03ObjectViews(bytes, 62, "server").Single();

        Protocol03NamedProfileRecord record = Assert.Single(sample.NamedProfileRecords, record => record.Text == "Gold Blood Champion");
        Protocol03NamedProfileRecord nestedRecord = Assert.Single(sample.NamedProfileRecords, record => record.Text == "Gold Blood Enforcer");
        Assert.Equal(3, record.PostCreationObjectViewHeaderOffset);
        Assert.Equal("NPC_BASE dynamic creation candidate", record.PostCreationObjectViewHeaderClassification);
        Assert.Equal("three-byte zero-tail boundary", record.PostCreationObjectViewBoundaryFamily);
        Assert.Equal(1, record.PostCreationObjectViewParsedUpdateCount);
        Assert.Equal(0, record.PostCreationObjectViewUnparsedBytes);
        Assert.Equal(53, record.PostCreationObjectViewConsumedBytes);
        Assert.Equal("dynamic creation bounded by next object-view", record.PostCreationObjectViewParseStatus);
        Assert.Equal("03 00 00 00 00 04 01 01 20 08 3e 3a", record.PostCreationObjectViewDynamicTailPrefixHex);
        Assert.Equal(12, record.PostCreationObjectViewDynamicTailAvailableBytes);
        Assert.True(record.PostCreationObjectViewComplete);
        Assert.Equal("delete-view boundary exception", nestedRecord.PostCreationObjectViewBoundaryFamily);
        Assert.Equal("selector-length parse", nestedRecord.PostCreationObjectViewParseStatus);
        Assert.Equal(string.Empty, nestedRecord.PostCreationObjectViewDynamicTailPrefixHex);
        Assert.Equal(0, nestedRecord.PostCreationObjectViewDynamicTailAvailableBytes);
        Assert.True(nestedRecord.PostCreationObjectViewComplete);
    }

    [Fact]
    public void DetectProtocol03ObjectViews_BoundsNestedNpcBaseTerminalFiveByteZeroTail()
    {
        List<byte> bytes = PacketResearcher.ParseHexBytes("02 03 01 00 0c 57 02 e2 cd ab 04 8e").ToList();
        bytes.AddRange(FixedAscii("Gold Blood Champion", 32));
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 10 00 00 22 40 01 17 00 00 01 00 0c 57 02 e3 cd ab 04 8e"));
        bytes.AddRange(FixedAscii("Gold Blood Enforcer", 32));
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 10 00 00 22 40 01 06 00 00 00 00"));

        Protocol03ObjectViewSample sample = PacketResearcher.DetectProtocol03ObjectViews(bytes, 63, "server").Single();

        Protocol03NamedProfileRecord record = Assert.Single(sample.NamedProfileRecords, record => record.Text == "Gold Blood Champion");
        Assert.Equal(3, record.PostCreationObjectViewHeaderOffset);
        Assert.Equal("NPC_BASE dynamic creation candidate", record.PostCreationObjectViewHeaderClassification);
        Assert.Equal("dynamic creation bounded by terminal five-byte zero tail", record.PostCreationObjectViewParseStatus);
        Assert.Equal(1, record.PostCreationObjectViewParsedUpdateCount);
        Assert.Equal(0, record.PostCreationObjectViewUnparsedBytes);
        Assert.Equal("06 00 00 00 00", record.PostCreationObjectViewDynamicTailPrefixHex);
        Assert.Equal(5, record.PostCreationObjectViewDynamicTailAvailableBytes);
        Assert.Null(record.PostCreationObjectViewDynamicTailNextHeaderOffset);
        Assert.Equal(string.Empty, record.PostCreationObjectViewDynamicTailPreHeaderHex);
        Assert.Equal(string.Empty, record.PostCreationObjectViewDynamicTailNextHeaderHex);
        Assert.Equal(string.Empty, record.PostCreationObjectViewDynamicTailNextHeaderClassification);
        Assert.True(record.PostCreationObjectViewComplete);

        PacketDumpFileSummary dump = new(
            "npc-terminal-tail.txt",
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
        Protocol03NpcBaseDynamicCreationTailSummary tailSummary = Assert.Single(PacketResearcher.BuildProtocol03NpcBaseDynamicCreationTailSummaries(new[] { dump }));
        Assert.Equal("five-byte zero tail lead", tailSummary.TailFamily);
        Assert.True(tailSummary.FollowUpComplete);
        Assert.Equal("dynamic creation bounded by terminal five-byte zero tail", tailSummary.ParseStatus);
        Assert.Equal(1, tailSummary.TailAvailableBytes["5"]);
        Assert.Equal(1, tailSummary.TailPrefixes["06 00 00 00 00"]);
    }

    [Fact]
    public void DetectProtocol03ObjectViews_CapturesExtendedNestedNpcBaseTailBoundaryLead()
    {
        List<byte> bytes = PacketResearcher.ParseHexBytes("02 03 01 00 0c 57 02 e2 cd ab 04 8e").ToList();
        bytes.AddRange(FixedAscii("Gold Blood Champion", 32));
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 10 00 00 22 40 01 17 00 00 01 00 0c 57 02 e3 cd ab 04 8e"));
        bytes.AddRange(FixedAscii("Gold Blood Enforcer", 32));
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 10 00 00 22 40 01 aa bb cc dd ee ff 11 22 33 00 04 01 01 20 08 3e 3a"));

        Protocol03ObjectViewSample sample = PacketResearcher.DetectProtocol03ObjectViews(bytes, 63, "server").Single();

        Protocol03NamedProfileRecord record = Assert.Single(sample.NamedProfileRecords, record => record.Text == "Gold Blood Champion");
        Assert.Equal("dynamic creation parsed; next boundary not found", record.PostCreationObjectViewParseStatus);
        Assert.Equal("aa bb cc dd ee ff 11 22 33 00 04 01 01 20 08 3e 3a", record.PostCreationObjectViewDynamicTailPrefixHex);
        Assert.Equal(17, record.PostCreationObjectViewDynamicTailAvailableBytes);
        Assert.Equal(9, record.PostCreationObjectViewDynamicTailNextHeaderOffset);
        Assert.Equal("aa bb cc dd ee ff 11 22 33", record.PostCreationObjectViewDynamicTailPreHeaderHex);
        Assert.Equal("00 04 01 01 20 08 3e 3a", record.PostCreationObjectViewDynamicTailNextHeaderHex);
        Assert.Equal("delete view candidate", record.PostCreationObjectViewDynamicTailNextHeaderClassification);
        Assert.False(record.PostCreationObjectViewComplete);
    }

    [Fact]
    public void DetectProtocol03ObjectViews_BoundsReviewedNpcBasePostSeparatorBody()
    {
        const string reviewedBodyHex =
            "09 00 00 01 00 08 2f 94 d9 2a 20 2b b8 cd ab 02 05 00 00 00 00 00 65 fd c0 00 00 00 40 c8 b5 85 40 00 00 00 00 80 d5 ed c0 00 00 00 00 d2 4a c4 bd 00 00 00 00 49 d2 7e 3f 07 00 00";
        const string reviewedNextHeaderHex = "01 00 08 50 02 19 00 30";
        List<byte> bytes = PacketResearcher.ParseHexBytes("02 03 01 00 0c 57 02 e2 cd ab 04 8e").ToList();
        bytes.AddRange(FixedAscii("Gold Blood Champion", 32));
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 10 00 00 22 40 01 17 00 00 01 00 0c 57 02 e3 cd ab 04 8e"));
        bytes.AddRange(FixedAscii("Gold Blood Enforcer", 32));
        bytes.AddRange(PacketResearcher.ParseHexBytes($"00 10 00 00 22 40 01 {reviewedBodyHex} {reviewedNextHeaderHex}"));

        Protocol03ObjectViewSample sample = PacketResearcher.DetectProtocol03ObjectViews(bytes, 64, "server").Single();

        Protocol03NamedProfileRecord record = Assert.Single(sample.NamedProfileRecords, record => record.Text == "Gold Blood Champion");
        Assert.Equal("dynamic creation bounded by reviewed post-separator body", record.PostCreationObjectViewParseStatus);
        Assert.True(record.PostCreationObjectViewComplete);
        Assert.Equal(1, record.PostCreationObjectViewParsedUpdateCount);
        Assert.Equal(0, record.PostCreationObjectViewUnparsedBytes);
        Assert.Equal(60, record.PostCreationObjectViewDynamicTailNextHeaderOffset);
        Assert.Equal(reviewedBodyHex, record.PostCreationObjectViewDynamicTailPreHeaderHex);
        Assert.Equal(reviewedNextHeaderHex, record.PostCreationObjectViewDynamicTailNextHeaderHex);
        Assert.Equal("NPC_BASE post-creation tail object-view candidate", record.PostCreationObjectViewDynamicTailNextHeaderClassification);

        PacketDumpFileSummary dump = new(
            "npc-reviewed-post-separator.txt",
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
        Protocol03NpcBaseDynamicCreationExtendedBoundarySummary extendedBoundarySummary =
            Assert.Single(PacketResearcher.BuildProtocol03NpcBaseDynamicCreationExtendedBoundarySummaries(new[] { dump }));
        Assert.Equal(60, extendedBoundarySummary.TailHeaderOffset);
        Assert.Equal(reviewedBodyHex, extendedBoundarySummary.PreHeaderHex);
        Assert.Equal(reviewedNextHeaderHex, extendedBoundarySummary.HeaderPrefixHex);
        Assert.Equal("post-separator later header candidate", extendedBoundarySummary.BoundaryInterpretation);
        Assert.Equal("evidence only; post-separator lead", extendedBoundarySummary.ParserAction);
    }

    [Fact]
    public void DetectProtocol03ObjectViews_BoundsKnownNpcBasePostCreationTailSelectors()
    {
        List<byte> bytes = PacketResearcher.ParseHexBytes("02 03 01 00 0c 57 02 e2 cd ab 04 8e").ToList();
        bytes.AddRange(FixedAscii("Gold Blood Champion", 32));
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 10 00 00 22 40 01 17 00 00 01 00 0c 57 02 e3 cd ab 04 8e"));
        bytes.AddRange(FixedAscii("Gold Blood Enforcer", 32));
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 10 00 00 22 40 01 05 00 00 01 00 08 d0 20 fc 09 e0 03 10 cd ab 08"));

        Protocol03ObjectViewSample sample = PacketResearcher.DetectProtocol03ObjectViews(bytes, 64, "server").Single();

        Protocol03NamedProfileRecord nestedRecord = Assert.Single(sample.NamedProfileRecords, record => record.Text == "Gold Blood Enforcer");
        Assert.Equal(3, nestedRecord.PostCreationObjectViewHeaderOffset);
        Assert.Equal("05 00 00", nestedRecord.PostCreationObjectViewPreHeaderHex);
        Assert.Equal("01 00 08 d0 20 fc 09 e0", nestedRecord.PostCreationObjectViewHeaderHex);
        Assert.Equal("NPC_BASE post-creation tail object-view candidate", nestedRecord.PostCreationObjectViewHeaderClassification);
        Assert.Equal("three-byte zero-tail boundary", nestedRecord.PostCreationObjectViewBoundaryFamily);
        Assert.Equal(0, nestedRecord.PostCreationObjectViewParsedUpdateCount);
        Assert.Equal(9, nestedRecord.PostCreationObjectViewUnparsedBytes);
        Assert.Equal(4, nestedRecord.PostCreationObjectViewConsumedBytes);
        Assert.Equal("spawn/profile variable block unresolved", nestedRecord.PostCreationObjectViewParseStatus);
        Assert.Equal(string.Empty, nestedRecord.PostCreationObjectViewDynamicTailPrefixHex);
        Assert.Null(nestedRecord.PostCreationObjectViewDynamicTailNextHeaderOffset);
        Assert.False(nestedRecord.PostCreationObjectViewComplete);

    }

    [Fact]
    public void DetectProtocol03ObjectViews_LeavesSelector60NpcBaseTailAsEvidenceOnly()
    {
        List<byte> bytes = PacketResearcher.ParseHexBytes("02 03 01 00 0c 57 02 e2 cd ab 04 8e").ToList();
        bytes.AddRange(FixedAscii("Gold Blood Champion", 32));
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 10 00 00 22 40 01 17 00 00 01 00 0c 57 02 e3 cd ab 04 8e"));
        bytes.AddRange(FixedAscii("Gold Blood Enforcer", 32));
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 10 00 00 22 40 01 1b 00 00 01 00 08 60 09 01 00 f0 30 c4 cd ab 02 09 00 00 00 00 00 38 c8 40 00 00 00 00 00 e8 84"));

        Protocol03ObjectViewSample sample = PacketResearcher.DetectProtocol03ObjectViews(bytes, 65, "server").Single();

        Protocol03NamedProfileRecord record = Assert.Single(sample.NamedProfileRecords, record => record.Text == "Gold Blood Champion");
        Assert.Equal("dynamic creation bounded by next object-view", record.PostCreationObjectViewParseStatus);
        Assert.Equal("1b 00 00 01 00 08 60 09 01 00 f0 30 c4 cd ab 02 09 00 00 00 00 00 38 c8 40 00 00 00 00 00 e8 84", record.PostCreationObjectViewDynamicTailPrefixHex);
        Assert.Equal(32, record.PostCreationObjectViewDynamicTailAvailableBytes);
        Assert.Null(record.PostCreationObjectViewDynamicTailNextHeaderOffset);
        Assert.True(record.PostCreationObjectViewComplete);

        PacketDumpFileSummary dump = new(
            "npc-selector60-tail.txt",
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
        Protocol03NpcBaseDynamicCreationHeaderLikeLeadSummary headerLikeSummary = Assert.Single(PacketResearcher.BuildProtocol03NpcBaseDynamicCreationHeaderLikeLeadSummaries(new[] { dump }));
        Assert.Equal(3, headerLikeSummary.LeadOffset);
        Assert.Equal(1, headerLikeSummary.ViewId);
        Assert.Equal("08", headerLikeSummary.UpdateCountHex);
        Assert.Equal("60", headerLikeSummary.FirstSelector);
        Assert.Equal(1, headerLikeSummary.CandidateCount);
        Assert.Equal(1, headerLikeSummary.CompleteParents["yes"]);
        Assert.Equal(1, headerLikeSummary.TailAvailableBytes["32"]);
        Assert.Equal(1, headerLikeSummary.ParseStatuses["dynamic creation bounded by next object-view"]);
        Assert.Equal(1, headerLikeSummary.BodyHints["body 09 01 00 f0 30 c4 cd ab; cd ab @+6"]);
    }

    [Fact]
    public void ReportWriter_IncludesProtocol03NpcBaseDynamicCreationTailLeads()
    {
        List<byte> bytes = PacketResearcher.ParseHexBytes("02 03 01 00 0c 57 02 e2 cd ab 04 8e").ToList();
        bytes.AddRange(FixedAscii("Gold Blood Champion", 32));
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 10 00 00 22 40 01 17 00 00 01 00 0c 57 02 e3 cd ab 04 8e"));
        bytes.AddRange(FixedAscii("Gold Blood Enforcer", 32));
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 10 00 00 22 40 01 05 00 00 01 00 08 18 20 fc 09 e0 03 10 cd ab 08"));
        Protocol03ObjectViewSample sample = PacketResearcher.DetectProtocol03ObjectViews(bytes, 63, "server").Single();
        PacketDumpFileSummary dump = new(
            "npc-tail.txt",
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
        Protocol03NpcBaseDynamicCreationTailSummary tailSummary = Assert.Single(PacketResearcher.BuildProtocol03NpcBaseDynamicCreationTailSummaries(new[] { dump }));
        Assert.Equal("header-like object-view tail lead", tailSummary.TailFamily);
        Assert.False(tailSummary.FollowUpComplete);
        Assert.Equal("dynamic creation parsed; next boundary not found", tailSummary.ParseStatus);
        Assert.Equal(1, tailSummary.CandidateCount);
        Assert.Equal(1, tailSummary.TailAvailableBytes["16"]);
        Assert.Equal(1, tailSummary.CapturedBytes["16"]);
        Assert.Equal(1, tailSummary.ParentPreValues["17"]);
        Assert.Equal(1, tailSummary.HeaderLikeLeads["3: view 1 count 08 selector 18"]);
        Protocol03NpcBaseDynamicCreationHeaderLikeLeadSummary headerLikeSummary = Assert.Single(PacketResearcher.BuildProtocol03NpcBaseDynamicCreationHeaderLikeLeadSummaries(new[] { dump }));
        Assert.Equal(3, headerLikeSummary.LeadOffset);
        Assert.Equal(1, headerLikeSummary.ViewId);
        Assert.Equal("08", headerLikeSummary.UpdateCountHex);
        Assert.Equal("18", headerLikeSummary.FirstSelector);
        Assert.Equal(1, headerLikeSummary.BodyHints["body 20 fc 09 e0 03 10 cd ab; cd ab @+6"]);
        Protocol03NpcBaseDynamicCreationBodyAnchorSummary bodyAnchorSummary = Assert.Single(PacketResearcher.BuildProtocol03NpcBaseDynamicCreationBodyAnchorSummaries(new[] { dump }));
        Assert.Equal(6, bodyAnchorSummary.BodySeparatorOffset);
        Assert.Equal(1, bodyAnchorSummary.BodyPrefixes["20 fc 09 e0 03 10"]);
        Assert.Equal(1, bodyAnchorSummary.PostSeparatorPrefixes["cd ab 08"]);
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
            new[] { dump })
        {
            Protocol03NpcBaseDynamicCreationTailSummaries = new[] { tailSummary },
            Protocol03NpcBaseDynamicCreationHeaderLikeLeadSummaries = new[] { headerLikeSummary },
            Protocol03NpcBaseDynamicCreationBodyAnchorSummaries = new[] { bodyAnchorSummary }
        };

        string markdown = ReportWriter.ToMarkdown(report);

        Assert.Contains("Dynamic creation tail prefixes start after a nested Object599 dynamic creation body has parsed its declared attribute stream.", markdown);
        Assert.Contains("Across 1 measured nested dynamic creations, 1 parsed the Object599 body but did not expose another object-view header inside the current eight-byte search window; 0 are bounded by the reviewed post-separator body rule.", markdown);
        Assert.Contains("| header-like object-view tail lead | no | dynamic creation parsed; next boundary not found | 16 (1) | 16 (1) | 1 | 17 (1) | 05 00 00 01 00 08 18 20 fc 09 e0 03 10 cd ab 08 | 3: view 1 count 08 selector 18 | 1 | Gold Blood Champion | `npc-tail.txt:63` |", markdown);
        Assert.Contains("| 3 | 1 | `08` | `18` | 1 | no (1) | 16 (1) | dynamic creation parsed; next boundary not found | header-like object-view tail lead | 17 (1) | 05 00 00 01 00 08 18 20 fc 09 e0 03 10 cd ab 08 | body 20 fc 09 e0 03 10 cd ab; cd ab @+6 | 1 | Gold Blood Champion | `npc-tail.txt:63` |", markdown);
        Assert.Contains("| 3 | 1 | `08` | `18` | 6 | 1 | no (1) | 16 (1) | dynamic creation parsed; next boundary not found | 20 fc 09 e0 03 10 | cd ab 08 | 1 | Gold Blood Champion | `npc-tail.txt:63` |", markdown);
        Assert.Contains("### Protocol 03 NPC_BASE Dynamic Creation Summary Exports", markdown);
        Assert.Contains("| header-like object-view tail lead | no | dynamic creation parsed; next boundary not found | 1 | 16 (1) | 16 (1) | 17 (1) | 05 00 00 01 00 08 18 20 fc 09 e0 03 10 cd ab 08 (1) | 3: view 1 count 08 selector 18 (1) | Gold Blood Champion (1) | `npc-tail.txt:63` |", markdown);
    }

    [Fact]
    public void ReportWriter_IncludesProtocol03NpcBaseDynamicCreationExtendedBoundaryCandidates()
    {
        List<byte> bytes = PacketResearcher.ParseHexBytes("02 03 01 00 0c 57 02 e2 cd ab 04 8e").ToList();
        bytes.AddRange(FixedAscii("Gold Blood Champion", 32));
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 10 00 00 22 40 01 17 00 00 01 00 0c 57 02 e3 cd ab 04 8e"));
        bytes.AddRange(FixedAscii("Gold Blood Enforcer", 32));
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 10 00 00 22 40 01 aa bb cc dd ee ff 11 22 33 00 04 01 01 20 08 3e 3a"));
        Protocol03ObjectViewSample sample = PacketResearcher.DetectProtocol03ObjectViews(bytes, 63, "server").Single();
        PacketDumpFileSummary dump = new(
            "npc-extended-tail.txt",
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
        Protocol03NpcBaseDynamicCreationExtendedBoundarySummary extendedBoundarySummary = Assert.Single(PacketResearcher.BuildProtocol03NpcBaseDynamicCreationExtendedBoundarySummaries(new[] { dump }));
        Assert.Equal(9, extendedBoundarySummary.TailHeaderOffset);
        Assert.Equal("aa bb cc dd ee ff 11 22 33", extendedBoundarySummary.PreHeaderHex);
        Assert.Equal("00 04 01 01 20 08 3e 3a", extendedBoundarySummary.HeaderPrefixHex);
        Assert.Equal("delete view candidate", extendedBoundarySummary.HeaderClassification);
        Assert.Equal("later header candidate", extendedBoundarySummary.BoundaryInterpretation);
        Assert.Equal("evidence only; single-sample lead", extendedBoundarySummary.ParserAction);
        Assert.Equal(1, extendedBoundarySummary.TailAvailableBytes["17"]);
        Protocol03NpcBaseDynamicCreationExtendedBoundaryParserActionSummary parserActionSummary =
            Assert.Single(PacketResearcher.BuildProtocol03NpcBaseDynamicCreationExtendedBoundaryParserActionSummaries(new[] { extendedBoundarySummary }));
        Assert.Equal("evidence only; single-sample lead", parserActionSummary.ParserAction);
        Assert.Equal(1, parserActionSummary.BoundaryRowCount);
        Assert.Equal(1, parserActionSummary.CandidateCount);
        Assert.Equal(1, parserActionSummary.BoundaryInterpretations["later header candidate"]);
        Assert.Equal(1, parserActionSummary.HeaderClassifications["delete view candidate"]);
        Assert.Equal(1, parserActionSummary.TailHeaderOffsets["9"]);
        Assert.Equal(1, parserActionSummary.HeaderPrefixes["00 04 01 01 20 08 3e 3a"]);
        Assert.Equal(1, parserActionSummary.PreHeaderPrefixes["aa bb cc dd ee ff 11 22 33"]);
        Assert.Equal(1, parserActionSummary.TailAvailableBytes["17"]);
        Assert.Equal(1, parserActionSummary.TailFamilies["other tail lead"]);
        Assert.Equal(1, parserActionSummary.ParentPreValues["17"]);
        Assert.Equal(1, parserActionSummary.TextSamples["Gold Blood Champion"]);
        Protocol03NpcBaseDynamicCreationExtendedBoundaryBodyShapeSummary bodyShapeSummary =
            Assert.Single(PacketResearcher.BuildProtocol03NpcBaseDynamicCreationExtendedBoundaryBodyShapeSummaries(new[] { extendedBoundarySummary }));
        Assert.Equal("later header candidate", bodyShapeSummary.BoundaryInterpretation);
        Assert.Equal("evidence only; single-sample lead", bodyShapeSummary.ParserAction);
        Assert.Equal(9, bodyShapeSummary.BodyByteCount);
        Assert.Equal("-", bodyShapeSummary.SeparatorOffset);
        Assert.Equal(9, bodyShapeSummary.PreSeparatorByteCount);
        Assert.Equal(0, bodyShapeSummary.PostSeparatorByteCount);
        Assert.Equal(1, bodyShapeSummary.BoundaryRowCount);
        Assert.Equal(1, bodyShapeSummary.CandidateCount);
        Assert.Equal(1, bodyShapeSummary.PreSeparatorPrefixes["aa bb cc dd ee ff 11 22 33"]);
        Assert.Equal(1, bodyShapeSummary.PostSeparatorPrefixes["-"]);

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
            new[] { dump })
        {
            Protocol03NpcBaseDynamicCreationExtendedBoundarySummaries = new[] { extendedBoundarySummary },
            Protocol03NpcBaseDynamicCreationExtendedBoundaryParserActionSummaries = new[] { parserActionSummary },
            Protocol03NpcBaseDynamicCreationExtendedBoundaryBodyShapeSummaries = new[] { bodyShapeSummary }
        };

        string json = System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        string markdown = ReportWriter.ToMarkdown(report);

        Assert.Contains("\"Protocol03NpcBaseDynamicCreationExtendedBoundaryParserActionSummaries\"", json);
        Assert.Contains("\"Protocol03NpcBaseDynamicCreationExtendedBoundaryBodyShapeSummaries\"", json);
        Assert.Contains("### Protocol 03 NPC_BASE Dynamic Creation Extended Boundary Candidates", markdown);
        Assert.Contains("Across 1 extended-boundary dynamic creation tails (1 unbounded and 0 reviewed post-separator bodies), 1 expose a later plausible object-view header between byte offsets 9 and 9 after the parsed nested creation attribute stream.", markdown);
        Assert.Contains("| evidence only; single-sample lead | 1 | 1 | later header candidate (1) | delete view candidate (1) | 9 (1) | 00 04 01 01 20 08 3e 3a (1) | aa bb cc dd ee ff 11 22 33 (1) | 17 (1) | other tail lead (1) | 17 (1) | Gold Blood Champion (1) | `npc-extended-tail.txt:63` |", markdown);
        Assert.Contains("| later header candidate | evidence only; single-sample lead | 9 | - | 9 | 0 | 1 | 1 | delete view candidate (1) | 9 (1) | 00 04 01 01 20 08 3e 3a (1) | aa bb cc dd ee ff 11 22 33 (1) | - (1) | 17 (1) | other tail lead (1) | 17 (1) | Gold Blood Champion (1) | `npc-extended-tail.txt:63` |", markdown);
        Assert.Contains("| 9 | `aa bb cc dd ee ff 11 22 33` | `00 04 01 01 20 08 3e 3a` | delete view candidate | later header candidate | evidence only; single-sample lead | 1 | 17 (1) |", markdown);
        Assert.Contains("| 9 | `aa bb cc dd ee ff 11 22 33` | `00 04 01 01 20 08 3e 3a` | delete view candidate | later header candidate | evidence only; single-sample lead | 1 | 17 (1) | other tail lead (1) | 17 (1) | Gold Blood Champion (1) | `npc-extended-tail.txt:63` |", markdown);
    }

    [Fact]
    public void ReportWriter_ClassifiesProtocol03NpcBaseDynamicCreationExtendedBoundarySeparatorCollisions()
    {
        List<byte> bytes = PacketResearcher.ParseHexBytes("02 03 01 00 0c 57 02 e2 cd ab 04 8e").ToList();
        bytes.AddRange(FixedAscii("Gold Blood Champion", 32));
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 10 00 00 22 40 01 17 00 00 01 00 0c 57 02 e3 cd ab 04 8e"));
        bytes.AddRange(FixedAscii("Gold Blood Enforcer", 32));
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 10 00 00 22 40 01 05 00 00 01 00 08 18 20 fc 09 e0 03 10 cd ab 01 01 00"));
        Protocol03ObjectViewSample sample = PacketResearcher.DetectProtocol03ObjectViews(bytes, 63, "server").Single();
        PacketDumpFileSummary dump = new(
            "npc-extended-tail.txt",
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
        Protocol03NpcBaseDynamicCreationExtendedBoundarySummary extendedBoundarySummary = Assert.Single(PacketResearcher.BuildProtocol03NpcBaseDynamicCreationExtendedBoundarySummaries(new[] { dump }));
        Assert.Equal(13, extendedBoundarySummary.TailHeaderOffset);
        Assert.Equal("05 00 00 01 00 08 18 20 fc 09 e0 03 10", extendedBoundarySummary.PreHeaderHex);
        Assert.Equal("cd ab 01 01 00", extendedBoundarySummary.HeaderPrefixHex);
        Assert.Equal("delete view candidate", extendedBoundarySummary.HeaderClassification);
        Assert.Equal("cd ab separator collision", extendedBoundarySummary.BoundaryInterpretation);
        Assert.Equal("reject as separator collision", extendedBoundarySummary.ParserAction);
        Protocol03NpcBaseDynamicCreationExtendedBoundaryBodyShapeSummary bodyShapeSummary =
            Assert.Single(PacketResearcher.BuildProtocol03NpcBaseDynamicCreationExtendedBoundaryBodyShapeSummaries(new[] { extendedBoundarySummary }));
        Assert.Equal("header+0", bodyShapeSummary.SeparatorOffset);
        Assert.Equal(13, bodyShapeSummary.BodyByteCount);
        Assert.Equal(13, bodyShapeSummary.PreSeparatorByteCount);
        Assert.Equal(3, bodyShapeSummary.PostSeparatorByteCount);
        Assert.Equal(1, bodyShapeSummary.PreSeparatorPrefixes["05 00 00 01 00 08 18 20 fc 09 e0 03 10"]);
        Assert.Equal(1, bodyShapeSummary.PostSeparatorPrefixes["cd ab 01 01 00"]);

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
            new[] { dump })
        {
            Protocol03NpcBaseDynamicCreationExtendedBoundarySummaries = new[] { extendedBoundarySummary }
        };

        string markdown = ReportWriter.ToMarkdown(report);

        Assert.Contains("| 13 | `05 00 00 01 00 08 18 20 fc 09 e0 03 10` | `cd ab 01 01 00` | delete view candidate | cd ab separator collision | reject as separator collision | 1 | 18 (1) |", markdown);
        Assert.Contains("| cd ab separator collision | reject as separator collision | 13 | header+0 | 13 | 3 | 1 | 1 | delete view candidate (1) | 13 (1) | cd ab 01 01 00 (1) | 05 00 00 01 00 08 18 20 fc 09 e0 03 10 (1) | cd ab 01 01 00 (1) | 18 (1) |", markdown);
    }

    [Fact]
    public void ReportWriter_IncludesProtocol03NpcBaseDynamicCreationPostSeparatorFieldLeads()
    {
        Protocol03NpcBaseDynamicCreationExtendedBoundarySummary extendedBoundarySummary = new(
            60,
            "09 00 00 01 00 08 2f 94 d9 2a 20 2b b8 cd ab 02 05 00 00 00 00 00 65 fd c0 00 00 00 40 c8 b5 85 40 00 00 00 00 80 d5 ed c0 00 00 00 00 d2 4a c4 bd 00 00 00 00 49 d2 7e 3f 07 00 00",
            "01 00 08 50 02 19 00 30",
            "NPC_BASE post-creation tail object-view candidate",
            "post-separator later header candidate",
            "evidence only; post-separator lead",
            2,
            new Dictionary<string, int> { ["519"] = 2 },
            new Dictionary<string, int> { ["header-like object-view tail lead"] = 2 },
            new Dictionary<string, int> { ["0b"] = 2 },
            new Dictionary<string, int> { ["Sleeper Sergeant"] = 2 },
            "npc-post-separator.txt",
            19750);

        Protocol03NpcBaseDynamicCreationExtendedBoundaryPostSeparatorFieldSummary fieldSummary =
            Assert.Single(PacketResearcher.BuildProtocol03NpcBaseDynamicCreationExtendedBoundaryPostSeparatorFieldSummaries(new[] { extendedBoundarySummary }));
        Assert.Equal("post-separator later header candidate", fieldSummary.BoundaryInterpretation);
        Assert.Equal("evidence only; post-separator lead", fieldSummary.ParserAction);
        Assert.Equal(60, fieldSummary.BodyByteCount);
        Assert.Equal(13, fieldSummary.SeparatorOffset);
        Assert.Equal(45, fieldSummary.PostSeparatorByteCount);
        Assert.Equal("02", fieldSummary.PostSeparatorLeadByteHex);
        Assert.Equal("05", fieldSummary.PostSeparatorSecondByteHex);
        Assert.Equal(5, fieldSummary.ZeroRunAfterSecondByte);
        Assert.Equal(7, fieldSummary.FirstNonZeroAfterSecondByteOffset);
        Assert.Equal(2, fieldSummary.CandidateCount);
        Assert.Equal(1, fieldSummary.BoundaryRowCount);
        Assert.Equal(2, fieldSummary.HeaderClassifications["NPC_BASE post-creation tail object-view candidate"]);
        Assert.Equal(2, fieldSummary.TailHeaderOffsets["60"]);
        Assert.Equal(2, fieldSummary.HeaderPrefixes["01 00 08 50 02 19 00 30"]);
        Assert.Equal(2, fieldSummary.PostSeparatorPrefixes["02 05 00 00 00 00 00 65 fd c0 00 00 00 40 c8 b5"]);
        Assert.Equal(2, fieldSummary.FirstNonZeroWindows["65 fd c0 00 00 00 40 c8"]);
        Protocol03NpcBaseDynamicCreationExtendedBoundaryPostSeparatorVectorProbeSummary vectorSummary =
            Assert.Single(PacketResearcher.BuildProtocol03NpcBaseDynamicCreationExtendedBoundaryPostSeparatorVectorProbeSummaries(new[] { extendedBoundarySummary }));
        Assert.Equal("post-separator later header candidate", vectorSummary.BoundaryInterpretation);
        Assert.Equal("evidence only; post-separator lead", vectorSummary.ParserAction);
        Assert.Equal(60, vectorSummary.BodyByteCount);
        Assert.Equal(13, vectorSummary.SeparatorOffset);
        Assert.Equal(45, vectorSummary.PostSeparatorByteCount);
        Assert.Equal("02", vectorSummary.PostSeparatorLeadByteHex);
        Assert.Equal("05", vectorSummary.PostSeparatorSecondByteHex);
        Assert.Equal(5, vectorSummary.ZeroRunAfterSecondByte);
        Assert.Equal(9, vectorSummary.VectorOffset);
        Assert.Equal(7, vectorSummary.PreVectorBridgeOffset);
        Assert.Equal(2, vectorSummary.PreVectorBridgeByteCount);
        Assert.Equal("be f64 x3", vectorSummary.VectorEncoding);
        Assert.Equal("bounded finite triple", vectorSummary.VectorDisposition);
        Assert.Equal(2, vectorSummary.CandidateCount);
        Assert.Equal(1, vectorSummary.BoundaryRowCount);
        Assert.Equal(2, vectorSummary.PreVectorBridges["65 fd"]);
        Assert.Equal(2, vectorSummary.VectorSamples["-2,2,-2"]);
        Assert.Equal(2, vectorSummary.VectorWindows["c0 00 00 00 40 c8 b5 85 40 00 00 00 00 80 d5 ed c0 00 00 00 00 d2 4a c4"]);
        Assert.Equal(2, vectorSummary.FirstNonZeroWindows["65 fd c0 00 00 00 40 c8"]);
        Protocol03NpcBaseDynamicCreationExtendedBoundaryPostSeparatorVectorTailSummary vectorTailSummary =
            Assert.Single(PacketResearcher.BuildProtocol03NpcBaseDynamicCreationExtendedBoundaryPostSeparatorVectorTailSummaries(new[] { extendedBoundarySummary }));
        Assert.Equal("post-separator later header candidate", vectorTailSummary.BoundaryInterpretation);
        Assert.Equal("evidence only; post-separator lead", vectorTailSummary.ParserAction);
        Assert.Equal(60, vectorTailSummary.BodyByteCount);
        Assert.Equal(13, vectorTailSummary.SeparatorOffset);
        Assert.Equal(45, vectorTailSummary.PostSeparatorByteCount);
        Assert.Equal("02", vectorTailSummary.PostSeparatorLeadByteHex);
        Assert.Equal("05", vectorTailSummary.PostSeparatorSecondByteHex);
        Assert.Equal(5, vectorTailSummary.ZeroRunAfterSecondByte);
        Assert.Equal(9, vectorTailSummary.VectorOffset);
        Assert.Equal(7, vectorTailSummary.PreVectorBridgeOffset);
        Assert.Equal(2, vectorTailSummary.PreVectorBridgeByteCount);
        Assert.Equal("be f64 x3", vectorTailSummary.VectorEncoding);
        Assert.Equal("bounded finite triple", vectorTailSummary.VectorDisposition);
        Assert.Equal(33, vectorTailSummary.TailOffset);
        Assert.Equal(12, vectorTailSummary.TailByteCount);
        Assert.Equal("be f32 x3", vectorTailSummary.TailFloatEncoding);
        Assert.Equal("bounded be f32 triple tail", vectorTailSummary.TailDisposition);
        Assert.Equal(2, vectorTailSummary.CandidateCount);
        Assert.Equal(1, vectorTailSummary.BoundaryRowCount);
        Assert.Equal(2, vectorTailSummary.PreVectorBridges["65 fd"]);
        Assert.Equal(2, vectorTailSummary.VectorSamples["-2,2,-2"]);
        Assert.Equal(2, vectorTailSummary.TailHexes["bd 00 00 00 00 49 d2 7e 3f 07 00 00"]);
        Assert.Equal(2, vectorTailSummary.TailFloatSamples["-0.031,0,0.527"]);
        Protocol03NpcBaseDynamicCreationExtendedBoundaryPostSeparatorLayoutActionSummary layoutActionSummary =
            Assert.Single(PacketResearcher.BuildProtocol03NpcBaseDynamicCreationExtendedBoundaryPostSeparatorLayoutActionSummaries(new[] { vectorTailSummary }));
        Assert.Equal("review bounded vector-tail body", layoutActionSummary.ParserAction);
        Assert.Equal("bounded be f64 vector followed by bounded be f32 tail fields", layoutActionSummary.ActionReason);
        Assert.Equal(1, layoutActionSummary.LayoutRowCount);
        Assert.Equal(2, layoutActionSummary.CandidateCount);
        Assert.Equal(2, layoutActionSummary.PreVectorBridgeByteCounts["2"]);
        Assert.Equal(2, layoutActionSummary.PreVectorBridges["65 fd"]);
        Assert.Equal(2, layoutActionSummary.VectorSamples["-2,2,-2"]);
        Assert.Equal(2, layoutActionSummary.TailDispositions["bounded be f32 triple tail"]);
        Assert.Equal(2, layoutActionSummary.TailFloatSamples["-0.031,0,0.527"]);
        Protocol03NpcBaseDynamicCreationExtendedBoundaryPostSeparatorReviewedBodyLayoutSummary reviewedBodyLayoutSummary =
            Assert.Single(PacketResearcher.BuildProtocol03NpcBaseDynamicCreationExtendedBoundaryPostSeparatorReviewedBodyLayoutSummaries(new[] { vectorTailSummary }));
        Assert.Equal("review bounded vector-tail body", reviewedBodyLayoutSummary.LayoutAction);
        Assert.Equal("bounded be f64 vector followed by bounded be f32 tail fields", reviewedBodyLayoutSummary.LayoutInterpretation);
        Assert.Equal("post[0]=lead 02; post[1]=subtype 05; post[2..6]=00 x5; post[7..8]=bridge 2 bytes; post[9..32]=be f64 x3; post[33..44]=be f32 x3", reviewedBodyLayoutSummary.FieldLayout);
        Assert.Equal(60, reviewedBodyLayoutSummary.BodyByteCount);
        Assert.Equal(13, reviewedBodyLayoutSummary.SeparatorOffset);
        Assert.Equal(45, reviewedBodyLayoutSummary.PostSeparatorByteCount);
        Assert.Equal("02", reviewedBodyLayoutSummary.PostSeparatorLeadByteHex);
        Assert.Equal("05", reviewedBodyLayoutSummary.PostSeparatorSecondByteHex);
        Assert.Equal(5, reviewedBodyLayoutSummary.ZeroRunAfterSecondByte);
        Assert.Equal(7, reviewedBodyLayoutSummary.PreVectorBridgeOffset);
        Assert.Equal(2, reviewedBodyLayoutSummary.PreVectorBridgeByteCount);
        Assert.Equal(9, reviewedBodyLayoutSummary.VectorOffset);
        Assert.Equal("be f64 x3", reviewedBodyLayoutSummary.VectorEncoding);
        Assert.Equal(33, reviewedBodyLayoutSummary.TailOffset);
        Assert.Equal(12, reviewedBodyLayoutSummary.TailByteCount);
        Assert.Equal("be f32 x3", reviewedBodyLayoutSummary.TailFloatEncoding);
        Assert.Equal("bounded be f32 triple tail", reviewedBodyLayoutSummary.TailDisposition);
        Assert.Equal(1, reviewedBodyLayoutSummary.LayoutRowCount);
        Assert.Equal(2, reviewedBodyLayoutSummary.CandidateCount);
        Assert.Equal(2, reviewedBodyLayoutSummary.BoundaryInterpretations["post-separator later header candidate"]);
        Assert.Equal(2, reviewedBodyLayoutSummary.SourceParserActions["evidence only; post-separator lead"]);
        Assert.Equal(2, reviewedBodyLayoutSummary.PreVectorBridges["65 fd"]);
        Assert.Equal(2, reviewedBodyLayoutSummary.VectorSamples["-2,2,-2"]);
        Assert.Equal(2, reviewedBodyLayoutSummary.TailHexes["bd 00 00 00 00 49 d2 7e 3f 07 00 00"]);
        Assert.Equal(2, reviewedBodyLayoutSummary.TailFloatSamples["-0.031,0,0.527"]);
        Protocol03NpcBaseDynamicCreationExtendedBoundaryPostSeparatorVectorSemanticSummary vectorSemanticSummary = new(
            "treat as non-position vector body field",
            "reviewed be f64 vector is bounded but does not match decoded parent Position",
            "different from parent Position",
            2,
            new Dictionary<string, int>
            {
                ["post[0]=lead 02; post[1]=subtype 05; post[2..6]=00 x5; post[7..8]=bridge 2 bytes; post[9..32]=be f64 x3; post[33..44]=be f32 x3"] = 2
            },
            new Dictionary<string, int> { ["60"] = 2 },
            new Dictionary<string, int> { ["02"] = 2 },
            new Dictionary<string, int> { ["05"] = 2 },
            new Dictionary<string, int> { ["5"] = 2 },
            new Dictionary<string, int> { ["2"] = 2 },
            new Dictionary<string, int> { ["65 fd"] = 2 },
            new Dictionary<string, int> { ["-2,2,-2"] = 2 },
            new Dictionary<string, int> { ["-58639,95,-111833"] = 2 },
            new Dictionary<string, int> { [">5k"] = 2 },
            new Dictionary<string, int> { ["126243.973"] = 2 },
            new Dictionary<string, int> { ["bounded be f32 triple tail"] = 2 },
            new Dictionary<string, int> { ["-0.031,0,0.527"] = 2 },
            new Dictionary<string, int> { ["NPC_BASE post-creation tail object-view candidate"] = 2 },
            new Dictionary<string, int> { ["01 00 08 50 02 19 00 30"] = 2 },
            new Dictionary<string, int> { ["Sleeper Sergeant"] = 2 },
            "npc-post-separator.txt",
            19750);
        Protocol03NpcBaseDynamicCreationExtendedBoundaryPostSeparatorTailSemanticSummary tailSemanticSummary = new(
            "review planar direction-like f32 tail",
            "bounded f32 triple has a zero middle component and sub-unit-scale magnitude",
            "be f32 x3",
            2,
            new Dictionary<string, int>
            {
                ["post[0]=lead 02; post[1]=subtype 05; post[2..6]=00 x5; post[7..8]=bridge 2 bytes; post[9..32]=be f64 x3; post[33..44]=be f32 x3"] = 2
            },
            new Dictionary<string, int> { ["12"] = 2 },
            new Dictionary<string, int> { ["be f32 x3"] = 2 },
            new Dictionary<string, int> { ["bounded be f32 triple tail"] = 2 },
            new Dictionary<string, int> { ["x-/y0/z+"] = 2 },
            new Dictionary<string, int> { ["y0"] = 2 },
            new Dictionary<string, int> { ["<=10"] = 2 },
            new Dictionary<string, int> { ["0.528"] = 2 },
            new Dictionary<string, int> { ["-0.031,0,0.527"] = 2 },
            new Dictionary<string, int> { ["-2,2,-2"] = 2 },
            new Dictionary<string, int> { ["65 fd"] = 2 },
            new Dictionary<string, int> { ["NPC_BASE post-creation tail object-view candidate"] = 2 },
            new Dictionary<string, int> { ["01 00 08 50 02 19 00 30"] = 2 },
            new Dictionary<string, int> { ["Sleeper Sergeant"] = 2 },
            "npc-post-separator.txt",
            19750);
        Protocol03NpcBaseDynamicCreationExtendedBoundaryPostSeparatorControlSemanticSummary controlSemanticSummary = new(
            "hold no-tail control",
            "bounded be f64 vector reaches the later header with no post-vector tail",
            "different from parent Position",
            2,
            new Dictionary<string, int>
            {
                ["post[0]=lead 02; post[1]=subtype 05; post[2..4]=00 x3; post[5..8]=bridge 4 bytes; post[9..32]=be f64 x3; no post-vector tail"] = 2
            },
            new Dictionary<string, int> { ["44"] = 2 },
            new Dictionary<string, int> { ["02"] = 2 },
            new Dictionary<string, int> { ["05"] = 2 },
            new Dictionary<string, int> { ["3"] = 2 },
            new Dictionary<string, int> { ["4"] = 2 },
            new Dictionary<string, int> { ["8c 2b c6 d0"] = 2 },
            new Dictionary<string, int> { ["2,2,7.141"] = 2 },
            new Dictionary<string, int> { ["-58639,95,-111833"] = 2 },
            new Dictionary<string, int> { [">5k"] = 2 },
            new Dictionary<string, int> { ["126243.973"] = 2 },
            new Dictionary<string, int> { ["0"] = 2 },
            new Dictionary<string, int> { ["-"] = 2 },
            new Dictionary<string, int> { ["no post-vector tail"] = 2 },
            new Dictionary<string, int> { ["-"] = 2 },
            new Dictionary<string, int> { ["NPC_BASE dynamic creation candidate"] = 2 },
            new Dictionary<string, int> { ["01 00 0c 57 02 95 cd ab"] = 2 },
            new Dictionary<string, int> { ["Hariel"] = 2 },
            "npc-post-separator.txt",
            19750);

        byte[] bytes = PacketResearcher.ParseHexBytes(
            "02 03 01 00 0c 57 02 e2 cd ab 04 8e 53 6c 65 65 70 65 72 20 53 65 72 67 65 61 6e 74 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 10 00 00 22 40 01")
            .ToArray();
        Protocol03ObjectViewSample sample = PacketResearcher.DetectProtocol03ObjectViews(bytes, 19750, "server").Single();
        PacketDumpFileSummary dump = new(
            "npc-post-separator.txt",
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
            new[] { dump })
        {
            Protocol03NpcBaseDynamicCreationExtendedBoundarySummaries = new[] { extendedBoundarySummary },
            Protocol03NpcBaseDynamicCreationExtendedBoundaryPostSeparatorFieldSummaries = new[] { fieldSummary },
            Protocol03NpcBaseDynamicCreationExtendedBoundaryPostSeparatorVectorProbeSummaries = new[] { vectorSummary },
            Protocol03NpcBaseDynamicCreationExtendedBoundaryPostSeparatorVectorTailSummaries = new[] { vectorTailSummary },
            Protocol03NpcBaseDynamicCreationExtendedBoundaryPostSeparatorLayoutActionSummaries = new[] { layoutActionSummary },
            Protocol03NpcBaseDynamicCreationExtendedBoundaryPostSeparatorReviewedBodyLayoutSummaries = new[] { reviewedBodyLayoutSummary },
            Protocol03NpcBaseDynamicCreationExtendedBoundaryPostSeparatorVectorSemanticSummaries = new[] { vectorSemanticSummary },
            Protocol03NpcBaseDynamicCreationExtendedBoundaryPostSeparatorTailSemanticSummaries = new[] { tailSemanticSummary },
            Protocol03NpcBaseDynamicCreationExtendedBoundaryPostSeparatorControlSemanticSummaries = new[] { controlSemanticSummary }
        };

        string json = System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        string markdown = ReportWriter.ToMarkdown(report);

        Assert.Contains("\"Protocol03NpcBaseDynamicCreationExtendedBoundaryPostSeparatorFieldSummaries\"", json);
        Assert.Contains("\"Protocol03NpcBaseDynamicCreationExtendedBoundaryPostSeparatorVectorProbeSummaries\"", json);
        Assert.Contains("\"Protocol03NpcBaseDynamicCreationExtendedBoundaryPostSeparatorVectorTailSummaries\"", json);
        Assert.Contains("\"Protocol03NpcBaseDynamicCreationExtendedBoundaryPostSeparatorLayoutActionSummaries\"", json);
        Assert.Contains("\"Protocol03NpcBaseDynamicCreationExtendedBoundaryPostSeparatorReviewedBodyLayoutSummaries\"", json);
        Assert.Contains("\"Protocol03NpcBaseDynamicCreationExtendedBoundaryPostSeparatorVectorSemanticSummaries\"", json);
        Assert.Contains("\"Protocol03NpcBaseDynamicCreationExtendedBoundaryPostSeparatorTailSemanticSummaries\"", json);
        Assert.Contains("\"Protocol03NpcBaseDynamicCreationExtendedBoundaryPostSeparatorControlSemanticSummaries\"", json);
        Assert.Contains("| Boundary interpretation | Parser action | Body bytes | Separator offset | Post-separator bytes | Lead byte | Second byte | Zero-run after second | First nonzero offset |", markdown);
        Assert.Contains("| post-separator later header candidate | evidence only; post-separator lead | 60 | 13 | 45 | `02` | `05` | 5 | 7 | 1 | 2 |", markdown);
        Assert.Contains("02 05 00 00 00 00 00 65 fd c0 00 00 00 40 c8 b5 (2)", markdown);
        Assert.Contains("65 fd c0 00 00 00 40 c8 (2)", markdown);
        Assert.Contains("| Parser action | Reason | Layout rows | Candidates | Boundary interpretations | Source parser actions | Body bytes |", markdown);
        Assert.Contains("| review bounded vector-tail body | bounded be f64 vector followed by bounded be f32 tail fields | 1 | 2 | post-separator later header candidate (2) | evidence only; post-separator lead (2) | 60 (2) |", markdown);
        Assert.Contains("| Layout action | Interpretation | Field layout | Body bytes | Separator offset | Post-separator bytes | Lead byte | Second byte | Zero-run | Bridge offset | Bridge bytes | Vector offset | Vector encoding | Tail offset | Tail bytes | Tail encoding | Tail disposition |", markdown);
        Assert.Contains("| review bounded vector-tail body | bounded be f64 vector followed by bounded be f32 tail fields | post[0]=lead 02; post[1]=subtype 05; post[2..6]=00 x5; post[7..8]=bridge 2 bytes; post[9..32]=be f64 x3; post[33..44]=be f32 x3 | 60 | 13 | 45 | `02` | `05` | 5 | 7 | 2 | 9 | be f64 x3 | 33 | 12 | be f32 x3 | bounded be f32 triple tail | 1 | 2 | post-separator later header candidate (2) | evidence only; post-separator lead (2) | 65 fd (2) | -2,2,-2 (2) |", markdown);
        Assert.Contains("| Semantic action | Reason | Parent position relation | Candidates | Field layouts | Body bytes | Lead bytes | Second bytes | Zero-runs | Bridge byte counts | Pre-vector bridges | Vector samples | Parent position samples | Vector-parent distances |", markdown);
        Assert.Contains("| treat as non-position vector body field | reviewed be f64 vector is bounded but does not match decoded parent Position | different from parent Position | 2 | post[0]=lead 02; post[1]=subtype 05; post[2..6]=00 x5; post[7..8]=bridge 2 bytes; post[9..32]=be f64 x3; post[33..44]=be f32 x3 (2) | 60 (2) | 02 (2) | 05 (2) | 5 (2) | 2 (2) | 65 fd (2) | -2,2,-2 (2) | -58639,95,-111833 (2) | >5k (2) | 126243.973 (2) | bounded be f32 triple tail (2) | -0.031,0,0.527 (2) |", markdown);
        Assert.Contains("| Tail semantic action | Reason | Tail float kind | Candidates | Field layouts | Tail bytes | Tail encodings | Tail dispositions | Axis patterns | Planar axes | Magnitudes |", markdown);
        Assert.Contains("| review planar direction-like f32 tail | bounded f32 triple has a zero middle component and sub-unit-scale magnitude | be f32 x3 | 2 | post[0]=lead 02; post[1]=subtype 05; post[2..6]=00 x5; post[7..8]=bridge 2 bytes; post[9..32]=be f64 x3; post[33..44]=be f32 x3 (2) | 12 (2) | be f32 x3 (2) | bounded be f32 triple tail (2) | x-/y0/z+ (2) | y0 (2) | <=10 (2) | 0.528 (2) | -0.031,0,0.527 (2) | -2,2,-2 (2) | 65 fd (2) |", markdown);
        Assert.Contains("| Control action | Reason | Parent position relation | Candidates | Field layouts | Body bytes | Lead bytes | Second bytes | Zero-runs | Bridge byte counts | Pre-vector bridges | Vector samples | Parent position samples | Vector-parent distances | Distance samples | Tail bytes | Tail encodings | Tail dispositions | Tail float samples | Header classifications | Header prefixes | Text samples | Sample |", markdown);
        Assert.Contains("| hold no-tail control | bounded be f64 vector reaches the later header with no post-vector tail | different from parent Position | 2 | post[0]=lead 02; post[1]=subtype 05; post[2..4]=00 x3; post[5..8]=bridge 4 bytes; post[9..32]=be f64 x3; no post-vector tail (2) | 44 (2) | 02 (2) | 05 (2) | 3 (2) | 4 (2) | 8c 2b c6 d0 (2) | 2,2,7.141 (2) | -58639,95,-111833 (2) | >5k (2) | 126243.973 (2) | 0 (2) | - (2) | no post-vector tail (2) | - (2) | NPC_BASE dynamic creation candidate (2) | 01 00 0c 57 02 95 cd ab (2) | Hariel (2) | `npc-post-separator.txt:19750` |", markdown);
        Assert.Contains("| Boundary interpretation | Parser action | Body bytes | Separator offset | Post-separator bytes | Lead byte | Second byte | Zero-run after second | Bridge offset | Bridge bytes | Vector offset | Encoding | Disposition |", markdown);
        Assert.Contains("| post-separator later header candidate | evidence only; post-separator lead | 60 | 13 | 45 | `02` | `05` | 5 | 7 | 2 | 9 | be f64 x3 | bounded finite triple | 1 | 2 | 65 fd (2) | -2,2,-2 (2) |", markdown);
        Assert.Contains("c0 00 00 00 40 c8 b5 85 40 00 00 00 00 80 d5 ed c0 00 00 00 00 d2 4a c4 (2)", markdown);
        Assert.Contains("| Boundary interpretation | Parser action | Body bytes | Separator offset | Post-separator bytes | Lead byte | Second byte | Zero-run after second | Bridge offset | Bridge bytes | Vector offset | Vector encoding | Vector disposition | Tail offset | Tail bytes | Tail encoding | Tail disposition |", markdown);
        Assert.Contains("| post-separator later header candidate | evidence only; post-separator lead | 60 | 13 | 45 | `02` | `05` | 5 | 7 | 2 | 9 | be f64 x3 | bounded finite triple | 33 | 12 | be f32 x3 | bounded be f32 triple tail | 1 | 2 | 65 fd (2) | -2,2,-2 (2) |", markdown);
        Assert.Contains("bd 00 00 00 00 49 d2 7e 3f 07 00 00 (2)", markdown);
        Assert.Contains("-0.031,0,0.527 (2)", markdown);
        Assert.Contains("`npc-post-separator.txt:19750`", markdown);
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
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 10 00 00 22 40 01 17 00 00 01 00 0c 57 02 e3 cd ab 04 8e"));
        List<byte> secondBytes = PacketResearcher.ParseHexBytes("02 03 01 00 0c 57 02 e2 cd ab 04 8e").ToList();
        secondBytes.AddRange(FixedAscii("Gold Blood Enthusiast", 32));
        secondBytes.AddRange(PacketResearcher.ParseHexBytes("00 10 00 00 22 40 01 03 00 00 00 00 04 01 01 20 08 3e 3a"));
        Protocol03ObjectViewSample sample = PacketResearcher.DetectProtocol03ObjectViews(bytes, 60, "server").Single();
        Protocol03ObjectViewSample secondSample = PacketResearcher.DetectProtocol03ObjectViews(secondBytes, 61, "server").Single();
        Protocol03NamedProfileRecord deleteBoundaryRecord = Assert.Single(secondSample.NamedProfileRecords);
        Assert.Equal("delete-view boundary exception", deleteBoundaryRecord.PostCreationObjectViewBoundaryFamily);
        Assert.Equal(1, deleteBoundaryRecord.PostCreationObjectViewParsedUpdateCount);
        Assert.Equal(0, deleteBoundaryRecord.PostCreationObjectViewUnparsedBytes);
        Assert.Equal(8, deleteBoundaryRecord.PostCreationObjectViewConsumedBytes);
        Assert.Equal("selector-length parse", deleteBoundaryRecord.PostCreationObjectViewParseStatus);
        Assert.True(deleteBoundaryRecord.PostCreationObjectViewComplete);
        PacketDumpFileSummary dump = new(
            "npc-boundary.txt",
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
        Assert.Contains("Across 2 Object599 creation records, 2 expose a plausible next object-view header inside the first eight post-attribute suffix bytes, with 2 distinct bounded pre-header byte sequences; 1 of those boundary leads uses a three-byte `<value> 00 00` pre-header.", markdown);
        Assert.Contains("Selector-length plus dynamic-creation boundary parsing fully accounts for 1 of those follow-up object-view header without consuming them into the parent record.", markdown);
        Assert.Contains("Complete follow-up consumed byte lengths: 8 (1).", markdown);
        Assert.Contains("| three-byte zero-tail boundary | 1 | 0 | 3 (1) | NPC_BASE dynamic creation candidate | 17 00 00 | 1 | Gold Blood Champion | `npc-boundary.txt:60` |", markdown);
        Assert.Contains("| delete-view boundary exception | 1 | 1 | 4 (1) | delete view candidate | 03 00 00 00 | 1 | Gold Blood Enthusiast | `npc-boundary.txt:61` |", markdown);
        Assert.Contains("| no | dynamic creation name slot incomplete | 1 | 0 | 4 | 6 (1) | NPC_BASE dynamic creation candidate | three-byte zero-tail boundary | 17 00 00 | 01 00 0c 57 02 e3 cd ab | 0 (1) | - | 1 | Gold Blood Champion | `npc-boundary.txt:60` |", markdown);
        Assert.Contains("| yes | selector-length parse | 1 | 1 | 8 | 0 (1) | delete view candidate | delete-view boundary exception | 03 00 00 00 | 00 04 01 01 20 08 3e 3a | 0 (1) | - | 1 | Gold Blood Enthusiast | `npc-boundary.txt:61` |", markdown);
        Assert.Contains("The three-byte zero-tail pre-header values range from `17` to `17`; 1 boundary lead uses another pre-header shape.", markdown);
        Assert.Contains("| `17` | `17 00 00` | 1 | NPC_BASE dynamic creation candidate | 1 | 1 | Gold Blood Champion | `npc-boundary.txt:60` |", markdown);
        Assert.Contains("Non-zero-tail pre-header exceptions remain separate because they do not match the dominant `<value> 00 00` tail-field shape.", markdown);
        Assert.Contains("Exception classifications: delete view candidate (1). Header offsets: 4 (1).", markdown);
        Assert.Contains("| 4 | `03 00 00 00` | 1 | delete view candidate | 00 04 01 01 20 08 3e 3a | 1 | Gold Blood Enthusiast | `npc-boundary.txt:61` |", markdown);
        Assert.Contains("| 3 | `17 00 00` | 1 | NPC_BASE dynamic creation candidate | 1 | 1 | Gold Blood Champion | `npc-boundary.txt:60` |", markdown);
        Assert.Contains("| 3 | `17 00 00` | `01 00 0c 57 02 e3 cd ab` | NPC_BASE dynamic creation candidate | 1 | 17 00 00 01 00 0c 57 02 | 1 | Gold Blood Champion | `npc-boundary.txt:60` |", markdown);
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

    private static byte[] BuildProtocol03Mode06LongEmbeddedTrailerObjectView(
        IReadOnlyList<byte> body,
        string outerPrefixFieldHex,
        string outerPostPrefixLeadHex,
        string boundaryHeaderHex,
        byte selector = 0x03)
    {
        var bytes = new List<byte>
        {
            0x02, 0x03,
            0x01, 0x00, 0x02, 0x0c
        };
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 13));
        bytes.Add(selector);
        bytes.AddRange(PacketResearcher.ParseHexBytes("00 06 0e 01 18 58 3a f6 c6 00 00 be 42 44 35 af 46"));
        bytes.AddRange(PacketResearcher.ParseHexBytes("ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 22));
        bytes.AddRange(PacketResearcher.ParseHexBytes($"{outerPrefixFieldHex} ff"));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 7));
        bytes.AddRange(PacketResearcher.ParseHexBytes(outerPostPrefixLeadHex));
        bytes.AddRange(body);
        if (!string.IsNullOrEmpty(boundaryHeaderHex))
        {
            bytes.AddRange(PacketResearcher.ParseHexBytes(boundaryHeaderHex));
        }

        return bytes.ToArray();
    }

    private static byte[] BuildProtocol03Mode06LongPreAnchor(
        byte innerSelector,
        string preTailFieldHex,
        string preAnchorPreLeadHex,
        string preAnchorTrailingLeadHex)
    {
        byte[] movement = innerSelector switch
        {
            0x08 => PacketResearcher.ParseHexBytes("00 06 08 00 00 80 3f 00 00 00 40 00 00 40 40").ToArray(),
            0x0c => PacketResearcher.ParseHexBytes("00 06 0c 00 00 00 80 3f 00 00 00 40 00 00 40 40").ToArray(),
            0x0e => PacketResearcher.ParseHexBytes("00 06 0e 00 00 00 00 80 3f 00 00 00 40 00 00 40 40").ToArray(),
            _ => throw new ArgumentOutOfRangeException(nameof(innerSelector))
        };
        byte[] trailer = PacketResearcher.ParseHexBytes(
            "ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 " +
            string.Join(" ", Enumerable.Repeat("00", 22)) + " " +
            preTailFieldHex + " " +
            preAnchorPreLeadHex + " " +
            preAnchorTrailingLeadHex)
            .ToArray();
        byte[] preAnchor = new byte[95 + movement.Length + trailer.Length];
        preAnchor[6] = 0xff;
        movement.CopyTo(preAnchor, 95);
        trailer.CopyTo(preAnchor, 95 + movement.Length);
        return preAnchor;
    }

    private static byte[] BuildProtocol03Mode06Terminal96Body(string suffixHex)
    {
        return Enumerable.Repeat((byte)0x00, 6)
            .Concat(new byte[] { 0xff, 0x00 })
            .Concat(Enumerable.Repeat((byte)0x00, 72))
            .Concat(PacketResearcher.ParseHexBytes(suffixHex))
            .ToArray();
    }

    private static byte[] BuildProtocol03Mode06Guarded57Body(string suffixHex)
    {
        return Enumerable.Repeat((byte)0x00, 6)
            .Concat(new byte[] { 0xff, 0x00 })
            .Concat(Enumerable.Repeat((byte)0x00, 33))
            .Concat(PacketResearcher.ParseHexBytes(suffixHex))
            .ToArray();
    }

    private static byte[] BuildProtocol03Mode06Guarded94Body(string suffixHex)
    {
        return Enumerable.Repeat((byte)0x00, 6)
            .Concat(new byte[] { 0xff, 0x00 })
            .Concat(Enumerable.Repeat((byte)0x00, 70))
            .Concat(PacketResearcher.ParseHexBytes(suffixHex))
            .ToArray();
    }

    private static PacketDumpFileSummary CreateEmptyPacketDump(string file)
    {
        return new PacketDumpFileSummary(
            file,
            0,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            Array.Empty<Protocol04PacketSequenceSample>(),
            Array.Empty<Protocol03ObjectViewSample>(),
            Array.Empty<Protocol04InteractionPayloadSample>(),
            Array.Empty<PlayerAttributePayloadSample>(),
            Array.Empty<ManageBonusPayloadSample>());
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
