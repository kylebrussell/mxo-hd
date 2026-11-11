using hds;
using hds.databases;

namespace hds.Tests;

/// <summary>Email records against retail's (mxopackets1_crashover2k_afterwhoruneo_proxy_026, packets 14653-14716) and
/// the mail service's flows. See docs/CLIENT-ACTIONS.md, "Email".</summary>
public class MailTests
{
    private static string H(byte[] bytes) => Convert.ToHexString(bytes).ToLowerInvariant();
    private static byte[] Rpc(string hex) => Convert.FromHexString(hex).Skip(2).ToArray();

    // Retail records, whole (opcode included)
    private const string RetailList = "81ea0d0032000000feffff7f01010000";
    private const string RetailListEnd = "81ef0c000a0000008400008e2500534f452b4d584f2b566563746f722d486f7374696c652b416674657257686f72754e656f00";
    private const string RetailHeader = "81ed190039003c00569c2b00cde73c4a0300000001004500001e00736f652b6d786f2b566563746f722d486f7374696c652b53797374656d00010000070053595354454d00160052656365697665204974656d204f766572666c6f7700";
    private const string RetailNotification = "81ed190039003c0029f82b00c1e3724a0100000000014500001e00534f452b4d584f2b566563746f722d486f7374696c652b53797374656d00010000070053595354454d0016004d65737361676520556e64656c6976657261626c6500";
    private const string RetailMessage = "81f3190039003c00569c2b00cde73c4a03000000004a0045001e00736f652b6d786f2b566563746f722d486f7374696c652b53797374656d00010000070053595354454d0003000000000300000900001e00005c00160052656365697665204974656d204f766572666c6f77003f004974656d20666f7277617264656420746f20656d61696c2073797374656d2064756520746f206c61636b206f6620696e76656e746f72792073706163652e0061003c3f786d6c2076657273696f6e3d22312e302220656e636f64696e673d227574662d3822203f3e0a3c4d584f5f4d61696c5f4f4f423e0a093c4974656d4c6973743e0a093c2f4974656d4c6973743e0a3c2f4d584f5f4d61696c5f4f4f423e0a00";
    private const string RetailChunkHeader = "81ab5e0000002c010000010103";
    private const string RetailChunk = "81ac04005e003c3f786d6c2076657273696f6e3d22312e302220656e636f64696e673d227574662d3822203f3e0a3c4d584f5f4d61696c5f4f4f423e0a093c46616374696f6e2069643d223234313133222f3e0a3c2f4d584f5f4d61696c5f4f4f423e0a";
    private const string RetailSend = "81e500000f0000000000011e0018000700504c41594552000400000000010400000c000029000042000000001e00736f652b6d786f2b766563746f722d686f7374696c652b73797374656d001a0052453a2052656365697665204974656d204f766572666c6f7700db005245504c59204d455353414745210a0a7b697d2d2d2d2d2d2d2d2d2d2d2d2d2d2d2d2d2d2d2d2d2d2d2d2d2d2d2d2d2d2d2d2d2d2d2d2d2d2d0a46726f6d3a2053797374656d0a53656e743a20536174204a756e2032302031353a34343a343520323030390a546f3a20416674657257686f72754e656f0a5375626a6563743a2052656365697665204974656d204f766572666c6f770a0a4974656d20666f7277617264656420746f20656d61696c2073797374656d2064756520746f206c61636b206f6620696e76656e746f72792073706163652e7b2f697d00";

    private static MailMessage OverflowMail(UInt32 status) => new MailMessage
    {
        Id = 0x2b9c56, Sender = "soe+mxo+Vector-Hostile+System", Category = MailPackets.CategorySystem, SentTime = 0x4a3ce7cd,
        Status = status, Subject = "Receive Item Overflow",
        Body = "Item forwarded to email system due to lack of inventory space."
    };

    [Fact]
    public void HeaderMatchesRetail()
    {
        Assert.Equal(RetailHeader, H(MailPackets.BuildHeader(OverflowMail(3), true, false)));
    }

    [Fact]
    public void NotificationMatchesRetail()
    {
        MailMessage undeliverable = new MailMessage
        {
            Id = 0x2bf829, Sender = "SOE+MXO+Vector-Hostile+System", Category = MailPackets.CategorySystem,
            SentTime = 0x4a72e3c1, Status = MailPackets.StatusNew, Subject = "Message Undeliverable"
        };
        Assert.Equal(RetailNotification, H(MailPackets.BuildHeader(undeliverable, false, true)));
    }

    [Fact]
    public void MessageMatchesRetail()
    {
        Assert.Equal(RetailMessage, H(MailPackets.BuildMessage(OverflowMail(3))));
    }

    [Fact]
    public void ListRecordsMatchRetail()
    {
        Assert.Equal("81eb2c010000", H(MailPackets.BuildInboxLimit()));
        Assert.Equal(RetailListEnd, H(MailPackets.BuildListEnd("SOE+MXO+Vector-Hostile+AfterWhoruNeo", 10)));
        Assert.Equal("81e6000000004100000e", H(MailPackets.BuildSendResult(0, MailPackets.RecipientUnknown)));
        Assert.Equal("81f78400008e", H(MailPackets.BuildStatus(0xf7, MailPackets.Success)));
    }

    [Fact]
    public void ParsesRetailRequests()
    {
        Assert.True(MailRequests.TryParseHeaderRequest(Rpc(RetailList), out var list));
        Assert.Equal("", list.Category);
        Assert.Equal(50u, list.MaxHeaders);
        Assert.Equal(0x7ffffffeu, list.StartTime);
        Assert.True(list.Descending);

        Assert.True(MailRequests.TryParseChunkHeader(Rpc(RetailChunkHeader), out var header));
        Assert.Equal(0x5eu, header.TotalLength);
        Assert.Equal(3, header.Index);
        Assert.True(MailRequests.TryParseChunk(Rpc(RetailChunk), out byte[] chunk));
        Assert.Equal(0x5e, chunk.Length);

        Assert.True(MailRequests.TryParseSend(Rpc(RetailSend), out var send));
        Assert.Equal("", send.SenderName);
        Assert.Equal("PLAYER", send.Category);
        Assert.True(send.EnforceInboxLimit);
        Assert.Equal(4, send.Chunks.Count);
        Assert.Equal("soe+mxo+vector-hostile+system", send.Chunk(0));
        Assert.Equal("RE: Receive Item Overflow", send.Chunk(1));
        Assert.StartsWith("REPLY MESSAGE!", send.Chunk(2));
        Assert.Null(send.Chunk(3));
        Assert.Equal(new[] { false, false, false, true }, send.AreChunked);
    }

    [Fact]
    public void ParsesComposeOob()
    {
        var oob = MailOob.ParseOutgoing("<?xml version=\"1.0\" encoding=\"utf-8\" ?>\n<MXO_Mail_OOB>\n\t<Faction id=\"24113\"/>" +
                                        "<RecipientList><Recipient Name=\"Bob\"/></RecipientList><Info Slot=\"250\"/>" +
                                        "<ItemList><Item FromPI=\"True\" Slot=\"4\"/><Item FromPI=\"False\" Slot=\"7\"/></ItemList></MXO_Mail_OOB>");
        Assert.Equal(250u, oob.Info);
        Assert.Equal(new[] { "Bob" }, oob.Recipients);
        Assert.Equal(new[] { (4, true), (7, false) }, oob.Items);
        Assert.Empty(MailOob.ParseOutgoing("not xml").Items);
    }

    [Fact]
    public void ReceivedOobCarriesItemsAsTheClientReadsThem()
    {
        // ClassData and InstanceData are read with atoi, so the descriptor goes out as a signed number
        string xml = MailOob.BuildReceived(new[] { new MailItem { Goid = 7332, Purity = 0xcc000000, Count = 2 } });
        Assert.Contains("<Item ClassData=\"7332\" InstanceData=\"" + unchecked((int)0xcc040000) + "\"/>", xml);
    }

    private class FakeHost : IMailHost
    {
        public readonly Dictionary<string, UInt32> Handles = new(StringComparer.OrdinalIgnoreCase) { ["Alice"] = 1, ["Bob"] = 2 };
        public readonly Dictionary<UInt32, List<byte[]>> Sent = new();
        public readonly Dictionary<UInt32, long> Info = new() { [1] = 1000, [2] = 0 };
        public readonly Dictionary<(UInt32, int), MailItem> Bags = new();
        public readonly HashSet<UInt32> Online = new() { 1, 2 };
        public int Free = 10;

        public UInt32 CharIdByHandle(string handle) => Handles.TryGetValue(handle, out UInt32 id) ? id : 0;
        public string HandleOf(UInt32 charId) => Handles.First(h => h.Value == charId).Key;
        public void Send(UInt32 charId, byte[] record)
        {
            if (!Online.Contains(charId)) return;
            if (!Sent.TryGetValue(charId, out var list)) Sent[charId] = list = new List<byte[]>();
            list.Add(record);
        }
        public bool IsDead(UInt32 charId) => false;
        public long InfoOf(UInt32 charId) => Info[charId];
        public void AddInfo(UInt32 charId, long change) => Info[charId] += change;
        public MailItem? ItemAt(UInt32 charId, int slot) => Bags.TryGetValue((charId, slot), out var item) ? item : null;
        public void RemoveItem(UInt32 charId, int slot) => Bags.Remove((charId, slot));
        public int FreeBagSlots(UInt32 charId) => Free;
        public void GiveItem(UInt32 charId, MailItem item) => Bags[(charId, 50 + Bags.Count)] = item;

        public List<string> Records(UInt32 charId) => Sent.TryGetValue(charId, out var list) ? list.Select(H).ToList() : new List<string>();
        public void Clear() => Sent.Clear();
    }

    private static (MailService, InMemoryMailStore, FakeHost) NewService()
    {
        var store = new InMemoryMailStore();
        var host = new FakeHost();
        UInt32 now = 1000;
        return (new MailService(store, host, () => "Test", () => now++), store, host);
    }

    private static MailRequests.Send Send(string to, string subject, string body, string? oob = null)
    {
        var send = new MailRequests.Send { Category = "PLAYER", EnforceInboxLimit = true };
        send.Chunks.AddRange(new[] { to, subject, body, oob });
        send.AreChunked.AddRange(new[] { false, false, false, false });
        return send;
    }

    [Fact]
    public void SendDeliversAndNotifies()
    {
        var (mail, store, host) = NewService();
        mail.OnSend(1, Send("Bob", "Hi", "Hello Bob"));

        var inbox = store.List(2);
        Assert.Single(inbox);
        Assert.Equal("SOE+MXO+Test+Alice", inbox[0].Sender);
        Assert.Equal(MailPackets.StatusNew, inbox[0].Status);
        Assert.Equal(H(MailPackets.BuildHeader(inbox[0], false, true)), Assert.Single(host.Records(2)));
        Assert.Equal(H(MailPackets.BuildSendResult(inbox[0].Id, MailPackets.Success)), Assert.Single(host.Records(1)));
    }

    [Fact]
    public void SendTakesTheOobFromChunks()
    {
        // Retail's reply: the OOB (chunk 3) came first in 81 ab / 81 ac
        var (mail, store, host) = NewService();
        Assert.True(MailRequests.TryParseChunkHeader(Rpc(RetailChunkHeader), out var header));
        Assert.True(MailRequests.TryParseChunk(Rpc(RetailChunk), out byte[] chunk));
        mail.OnChunkHeader(1, header);
        mail.OnChunk(1, chunk);
        Assert.True(MailRequests.TryParseSend(Rpc(RetailSend), out var send));
        send.Chunks[0] = "soe+mxo+Test+Bob";
        mail.OnSend(1, send);
        Assert.Equal("RE: Receive Item Overflow", Assert.Single(store.List(2)).Subject);
    }

    [Fact]
    public void UnknownRecipientFailsLikeRetail()
    {
        var (mail, store, host) = NewService();
        Assert.True(MailRequests.TryParseSend(Rpc(RetailSend), out var send));
        mail.OnSend(1, send);

        List<string> records = host.Records(1);
        Assert.Equal("81e6000000004100000e", records[0]);
        MailMessage notice = Assert.Single(store.List(1));
        Assert.Equal("Message Undeliverable", notice.Subject);
        Assert.Equal(MailPackets.CategorySystem, notice.Category);
        Assert.Equal(H(MailPackets.BuildHeader(notice, false, true)), records[1]);
    }

    [Fact]
    public void InboxListsNewestFirstWithMoreOnAllButTheLast()
    {
        var (mail, store, host) = NewService();
        mail.OnSend(1, Send("Bob", "one", "1"));
        mail.OnSend(1, Send("Bob", "two", "2"));
        host.Clear();
        mail.OnHeaderRequest(2, new MailRequests.HeaderRequest { MaxHeaders = 50, StartTime = 0x7ffffffe, Descending = true });

        List<string> records = host.Records(2);
        Assert.Equal(4, records.Count);
        Assert.Equal("81eb2c010000", records[0]);
        Assert.Equal(H(MailPackets.BuildListEnd("SOE+MXO+Test+Bob", 2)), records[1]);
        var inbox = store.List(2);
        Assert.Equal("two", inbox[0].Subject);
        Assert.Equal(H(MailPackets.BuildHeader(inbox[0], true, false)), records[2]);
        Assert.Equal(H(MailPackets.BuildHeader(inbox[1], false, false)), records[3]);
    }

    [Fact]
    public void ReadingMarksReadAndDeletingPurgesOnClose()
    {
        var (mail, store, host) = NewService();
        mail.OnSend(1, Send("Bob", "Hi", "Hello"));
        UInt32 id = store.List(2)[0].Id;

        mail.OnRead(2, id);
        Assert.Equal(MailPackets.StatusRead, store.Get(2, id)!.Status);
        Assert.StartsWith("81f3", host.Records(2).Last());

        mail.OnSetStatus(2, id, MailPackets.StatusDeleted);
        Assert.Equal("81f58400008e", host.Records(2).Last());
        Assert.Equal(MailPackets.StatusDeleted, store.Get(2, id)!.Status);
        mail.OnSetStatus(2, id, MailPackets.StatusRead);   // Undelete
        mail.OnSetStatus(2, id, MailPackets.StatusDeleted);

        mail.OnChangeAll(2, MailPackets.StatusDeleted, MailPackets.StatusPurged);
        Assert.Equal("81f78400008e", host.Records(2).Last());
        Assert.Null(store.Get(2, id));

        mail.OnRead(2, id);
        Assert.Equal(H(MailPackets.BuildReadFailed(MailPackets.MessageNotFound)), host.Records(2).Last());
    }

    [Fact]
    public void ClosingTheInboxMarksNewMailSeen()
    {
        var (mail, store, host) = NewService();
        mail.OnSend(1, Send("Bob", "Hi", "Hello"));
        mail.OnChangeAll(2, MailPackets.StatusNew, MailPackets.StatusUnread);
        Assert.Equal(MailPackets.StatusUnread, store.List(2)[0].Status);
    }

    [Fact]
    public void AttachmentsMoveOnSendAndTake()
    {
        var (mail, store, host) = NewService();
        host.Bags[(1, 4)] = new MailItem { Goid = 7332, Purity = 0, Count = 3 };
        mail.OnSend(1, Send("Bob", "Gift", "For you",
            "<MXO_Mail_OOB><Info Slot=\"250\"/><ItemList><Item FromPI=\"True\" Slot=\"4\"/></ItemList></MXO_Mail_OOB>"));

        Assert.False(host.Bags.ContainsKey((1, 4)));
        Assert.Equal(750, host.Info[1]);
        Assert.Equal(250, host.Info[2]);
        MailMessage gift = Assert.Single(store.List(2));
        MailItem attached = Assert.Single(gift.Items);
        Assert.Contains("ClassData=\"7332\"", MailOob.BuildReceived(gift.Items));

        host.Free = 0;
        mail.OnTake(2, new MailRequests.Take { Id = gift.Id, Items = { (attached.ClassData, attached.InstanceData) } });
        Assert.Equal(H(MailPackets.BuildStatus(0xfd, MailPackets.PersonalInventoryFull)), host.Records(2).Last());

        host.Free = 10;
        mail.OnTake(2, new MailRequests.Take { Id = gift.Id, Items = { (attached.ClassData, attached.InstanceData) } });
        Assert.Equal("81fd8400008e", host.Records(2).Last());
        Assert.Contains(host.Bags, b => b.Key.Item1 == 2 && b.Value.Goid == 7332 && b.Value.Count == 3);
        Assert.Empty(store.Get(2, gift.Id)!.Items);
    }

    [Fact]
    public void AttachmentsThatAreNotThereFailWithoutMovingAnything()
    {
        var (mail, store, host) = NewService();
        mail.OnSend(1, Send("Bob", "Gift", "For you",
            "<MXO_Mail_OOB><Info Slot=\"5000\"/></MXO_Mail_OOB>"));
        Assert.Equal(H(MailPackets.BuildSendResult(0, MailPackets.BadData)), host.Records(1).Last());
        Assert.Empty(store.List(2));
        Assert.Equal(1000, host.Info[1]);

        mail.OnSend(1, Send("Bob", "Gift", "For you",
            "<MXO_Mail_OOB><ItemList><Item FromPI=\"True\" Slot=\"9\"/></ItemList></MXO_Mail_OOB>"));
        Assert.Equal(H(MailPackets.BuildSendResult(0, MailPackets.BadData)), host.Records(1).Last());
    }

    [Fact]
    public void FullInboxRefuses()
    {
        var (mail, store, host) = NewService();
        for (int i = 0; i < MailPackets.InboxLimit; i++)
        {
            store.Add(new MailMessage { OwnerCharId = 2, Subject = "x" });
        }
        mail.OnSend(1, Send("Bob", "Hi", "Hello"));
        Assert.Equal(H(MailPackets.BuildSendResult(0, MailPackets.InboxFull)), host.Records(1).Last());
    }

    [Fact]
    public void MailBotAnswersAndGivesAttachmentsBack()
    {
        var (mail, store, host) = NewService();
        mail.BotEnabled = true;
        host.Bags[(1, 4)] = new MailItem { Goid = 7332, Count = 1 };
        mail.OnSend(1, Send("SOE+MXO+Test+MailBot", "Ping", "Are you there?",
            "<MXO_Mail_OOB><ItemList><Item FromPI=\"True\" Slot=\"4\"/></ItemList></MXO_Mail_OOB>"));

        MailMessage reply = Assert.Single(store.List(1));
        Assert.Equal("RE: Ping", reply.Subject);
        Assert.Equal("SOE+MXO+Test+MailBot", reply.Sender);
        Assert.Single(reply.Items);
        Assert.Contains("81e6", host.Records(1).Select(r => r.Substring(0, 4)));
    }

    [Fact]
    public void FoldersAndOldMail()
    {
        var (mail, store, host) = NewService();
        mail.OnSend(1, Send("Bob", "one", "1"));
        mail.OnSend(1, Send("Bob", "two", "2"));
        var inbox = store.List(2);

        var classify = new MailRequests.Classify { NewFolder = "Saved" };
        classify.Ids.Add(inbox[0].Id);
        mail.OnClassify(2, classify);
        Assert.Equal("81f98400008e", host.Records(2).Last());
        Assert.Equal("Saved", store.Get(2, inbox[0].Id)!.Folder);

        mail.OnDeleteOlder(2, new MailRequests.DeleteOlder { TimeOlderThan = inbox[0].SentTime });
        Assert.Equal(H(MailPackets.BuildDeleteResult("SOE+MXO+Test+Bob", 1, MailPackets.Success)), host.Records(2).Last());
        Assert.Equal("two", Assert.Single(store.List(2)).Subject);
    }

    [Fact]
    public void ParsesTakeAndClassify()
    {
        // 81 fc <id u32> <items offset u16>, there <count u16> then 00 <class u32> <instance u32> each
        byte[] take = Rpc("81fc070000000800010000a41c000000000c0c");
        Assert.True(MailRequests.TryParseTake(take, out var parsed));
        Assert.Equal(7u, parsed.Id);
        Assert.Equal((7332u, 0x0c0c0000u), Assert.Single(parsed.Items));

        // 81 f8 <name off> <ids off> <folder off>, then the name, the ids and the folder
        byte[] classify = Rpc("81f808000e0014000400426f62000100050000000600536176656400");
        Assert.True(MailRequests.TryParseClassify(classify, out var moved));
        Assert.Equal("Bob", moved.PlayerName);
        Assert.Equal(new[] { 5u }, moved.Ids);
        Assert.Equal("Saved", moved.NewFolder);
    }
}
