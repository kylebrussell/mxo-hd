using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;

namespace hds
{
    /// <summary>One attached item, as the client's inventory records carry it: game object and 4-byte descriptor.</summary>
    public class MailItem
    {
        public UInt32 Goid;
        public UInt32 Purity;
        public int Count = 1;

        /// <summary>The ClassData / InstanceData pair the client reads from a message's ItemList.</summary>
        public UInt32 ClassData => Goid;
        public UInt32 InstanceData => TradePackets.Descriptor(Purity, Count);
    }

    /// <summary>A stored message (the client calls them persistent messages).</summary>
    public class MailMessage
    {
        public UInt32 Id;
        public UInt32 OwnerCharId;
        public string Sender = "";
        public string Folder = "";
        public string Category = MailPackets.CategoryPlayer;
        public UInt32 SentTime;
        public UInt32 Status = MailPackets.StatusNew;
        public string Subject = "";
        public string Body = "";
        public List<MailItem> Items = new List<MailItem>();

        public MailMessage Copy()
        {
            MailMessage copy = (MailMessage)MemberwiseClone();
            copy.Items = Items.Select(i => new MailItem { Goid = i.Goid, Purity = i.Purity, Count = i.Count }).ToList();
            return copy;
        }
    }

    /// <summary>
    /// In-game email records (the client's persistent messages, part of its SOE chat service). Layouts are from
    /// client.dll's RPC classes; the field values and the order of the records are retail's
    /// (mxopackets1_crashover2k_afterwhoruneo_proxy_026: list, read, reply, close). See docs/CLIENT-ACTIONS.md.
    /// </summary>
    public static class MailPackets
    {
        public const string CategoryPlayer = "PLAYER";
        public const string CategorySystem = "SYSTEM";

        // Message status, from the client's inbox (0x620cb690 toggles 3 and 4 for Delete / Undelete) and retail's
        // 81 f6 records sent when the inbox closes (4 -> 5, 1 -> 2).
        public const UInt32 StatusNew = 1;        // the 81 ed handler lights the new-mail indicator for it
        public const UInt32 StatusUnread = 2;     // listed, not opened
        public const UInt32 StatusRead = 3;
        public const UInt32 StatusDeleted = 4;    // shown with Undelete
        public const UInt32 StatusPurged = 5;     // gone

        /// <summary>Retail's 81 eb InboxLimit.</summary>
        public const UInt32 InboxLimit = 300;

        // LTOBJSRV_SOECHAT_* (category 0x0e; the top bit marks success). The client prints the failures by name.
        public const UInt32 Success = 0x8e000084;
        public const UInt32 RecipientUnknown = 0x0e000041;     // DESTAVATARDOESNTEXIST, retail's answer to a bad address
        public const UInt32 MessageNotFound = 0x0e000052;      // PMSGNOTFOUND
        public const UInt32 Ignoring = 0x0e000054;             // IGNORING
        public const UInt32 NothingToDo = 0x0e000077;          // NOTHINGTODO
        public const UInt32 InboxFull = 0x0e000081;            // INBOXLIMITEXCEEDED
        public const UInt32 DuplicateRecipient = 0x0e000082;   // DUPLICATEDESTINATION
        public const UInt32 BadData = 0x0e000068;              // SUCCESSBADDATA
        public const UInt32 PersonalInventoryFull = 0x0c0002ca; // LTPS_SPPERSONALINVENTORYFULL

        /// <summary>Retail's address form: "SOE+MXO+&lt;server&gt;+&lt;handle&gt;".</summary>
        public static string Address(string serverName, string handle) => "SOE+MXO+" + serverName + "+" + handle;

        /// <summary>The handle in an address, or the text itself when it has no prefix.</summary>
        public static string HandleOf(string address)
        {
            if (address == null)
            {
                return "";
            }
            string trimmed = address.Trim();
            int plus = trimmed.LastIndexOf('+');
            return plus >= 0 ? trimmed.Substring(plus + 1) : trimmed;
        }

        /// <summary><c>81 eb &lt;InboxLimit u32&gt;</c>: first answer to a header request.</summary>
        public static byte[] BuildInboxLimit(UInt32 limit = InboxLimit)
        {
            return new MailRecord(0xeb).U32(limit).ToArray();
        }

        /// <summary>
        /// <c>81 ed</c> (0x6214b1c0, size 0x19): +2 Sender, +4 Folder, +6 Category (string offsets), +8 PersistentMessageID,
        /// +0c SentTime, +10 Status, +14 More, +15 Notification, +16 ChunkString (the subject), +18 IsChunked.
        /// More is 1 on every header but the last. Notification 1 makes the client say who sent it and play a sound.
        /// </summary>
        public static byte[] BuildHeader(MailMessage message, bool more, bool notification)
        {
            MailRecord r = new MailRecord(0xed);
            int sender = r.Offset(), folder = r.Offset(), category = r.Offset();
            r.U32(message.Id).U32(message.SentTime).U32(message.Status).U8(more ? 1 : 0).U8(notification ? 1 : 0);
            int subject = r.Offset();
            r.U8(0);
            r.String(sender, message.Sender).String(folder, message.Folder).String(category, message.Category)
                .String(subject, message.Subject);
            return r.ToArray();
        }

        /// <summary><c>81 ef</c> (size 0x0c): +2 PlayerName, +4 PersistentMessageCount, +8 Status. Ends a header list.</summary>
        public static byte[] BuildListEnd(string playerAddress, UInt32 count, UInt32 status = Success)
        {
            MailRecord r = new MailRecord(0xef);
            int name = r.Offset();
            r.U32(count).U32(status);
            return r.String(name, playerAddress).ToArray();
        }

        /// <summary><c>81 ec &lt;Status u32&gt;</c>: the header request failed ("Failed to retrieve mail headers").</summary>
        public static byte[] BuildListFailed(UInt32 status) => new MailRecord(0xec).U32(status).ToArray();

        /// <summary>
        /// <c>81 f3</c> (0x6214b610, size 0x19): the header fields to +14 More, then +15 ChunkStrings and +17 AreChunked
        /// (array offsets). Retail's chunks: subject, body, and the out-of-band XML with the attachments.
        /// </summary>
        public static byte[] BuildMessage(MailMessage message)
        {
            MailRecord r = new MailRecord(0xf3);
            int sender = r.Offset(), folder = r.Offset(), category = r.Offset();
            r.U32(message.Id).U32(message.SentTime).U32(message.Status).U8(0);
            int chunks = r.Offset(), chunked = r.Offset();
            r.String(sender, message.Sender).String(folder, message.Folder).String(category, message.Category);
            string[] strings = { message.Subject, message.Body, MailOob.BuildReceived(message.Items) };
            r.Bytes(chunked, new byte[strings.Length]);
            r.Strings(chunks, strings);
            return r.ToArray();
        }

        /// <summary><c>81 f2 &lt;Status u32&gt;</c>: a message could not be read. Retail sent nothing else on success.</summary>
        public static byte[] BuildReadFailed(UInt32 status) => new MailRecord(0xf2).U32(status).ToArray();

        /// <summary><c>81 e6 &lt;PersistentMessageID u32&gt; &lt;Status u32&gt;</c>: the answer to a send.</summary>
        public static byte[] BuildSendResult(UInt32 messageId, UInt32 status)
        {
            return new MailRecord(0xe6).U32(messageId).U32(status).ToArray();
        }

        /// <summary><c>81 f5</c> (one status changed), <c>81 f7</c> (all of one status), <c>81 f9</c> (moved to a folder),
        /// <c>81 fd</c> (attachments taken): <c>&lt;Status u32&gt;</c>.</summary>
        public static byte[] BuildStatus(byte opcode, UInt32 status) => new MailRecord(opcode).U32(status).ToArray();

        /// <summary><c>81 fb</c> (size 0x0c): +2 PlayerName, +4 NumDeleted, +8 Status. The client then asks for the headers again.</summary>
        public static byte[] BuildDeleteResult(string playerAddress, UInt32 deleted, UInt32 status)
        {
            MailRecord r = new MailRecord(0xfb);
            int name = r.Offset();
            r.U32(deleted).U32(status);
            return r.String(name, playerAddress).ToArray();
        }

        public static string FormatTime(UInt32 unixTime)
        {
            return DateTimeOffset.FromUnixTimeSeconds(unixTime).UtcDateTime.ToString("ddd MMM dd HH:mm:ss yyyy", CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    /// Writes one record: a fixed part with u16 offsets (counted from the record's first byte) to data placed after it.
    /// A string there is <c>&lt;u16 length with the NUL&gt; text 00</c>; an array is <c>&lt;u16 count&gt;</c> then its
    /// elements. Each element of a string array is three bytes, <c>00 &lt;u16 offset from the element's start&gt;</c>
    /// (0 = none), with the strings after the elements (retail 81 f3 and the client's 81 e5).
    /// </summary>
    internal sealed class MailRecord
    {
        private readonly MemoryStream stream = new MemoryStream();
        private readonly BinaryWriter writer;

        public MailRecord(byte opcode)
        {
            writer = new BinaryWriter(stream);
            writer.Write((byte)0x81);
            writer.Write(opcode);
        }

        public int Offset()
        {
            int position = (int)stream.Position;
            writer.Write((UInt16)0);
            return position;
        }

        public MailRecord U8(int value) { writer.Write((byte)value); return this; }
        public MailRecord U32(UInt32 value) { writer.Write(value); return this; }

        private void Patch(int at, int value)
        {
            long end = stream.Position;
            stream.Position = at;
            writer.Write((UInt16)value);
            stream.Position = end;
        }

        private void WriteSized(string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text ?? "");
            writer.Write((UInt16)(bytes.Length + 1));
            writer.Write(bytes);
            writer.Write((byte)0);
        }

        public MailRecord String(int offsetField, string text)
        {
            Patch(offsetField, (int)stream.Position);
            WriteSized(text);
            return this;
        }

        public MailRecord Bytes(int offsetField, byte[] values)
        {
            Patch(offsetField, (int)stream.Position);
            writer.Write((UInt16)values.Length);
            writer.Write(values);
            return this;
        }

        public MailRecord Strings(int offsetField, IList<string> values)
        {
            Patch(offsetField, (int)stream.Position);
            writer.Write((UInt16)values.Count);
            int first = (int)stream.Position;
            for (int i = 0; i < values.Count; i++)
            {
                writer.Write((byte)0);
                writer.Write((UInt16)0);
            }
            for (int i = 0; i < values.Count; i++)
            {
                if (values[i] == null)
                {
                    continue;
                }
                int element = first + i * 3;
                Patch(element + 1, (int)stream.Position - element);
                WriteSized(values[i]);
            }
            return this;
        }

        public byte[] ToArray() => stream.ToArray();
    }

    /// <summary>
    /// The client's mail requests. <c>rpcData</c> is the record after its two opcode bytes, so a field the client
    /// puts at record offset N is rpcData[N - 2], and so are the offsets the record holds.
    /// </summary>
    public static class MailRequests
    {
        public class Send
        {
            public string SenderName = "";
            public string Category = "";
            public UInt32 CategoryLimit;
            public bool EnforceInboxLimit;
            public List<string> Chunks = new List<string>();
            public List<bool> AreChunked = new List<bool>();

            public string Chunk(int index) => index < Chunks.Count ? Chunks[index] : null;
            public bool IsChunked(int index) => index < AreChunked.Count && AreChunked[index];
        }

        public class HeaderRequest
        {
            public string Category = "";
            public UInt32 MaxHeaders;
            public UInt32 StartTime;
            public bool Descending;
        }

        public class ChunkHeader
        {
            public UInt32 TotalLength;
            public UInt32 ChunkSize;
            public bool IsWide;
            public bool IsMulti;
            public byte Index;
        }

        public class Classify
        {
            public string PlayerName = "";
            public List<UInt32> Ids = new List<UInt32>();
            public string NewFolder = "";
        }

        public class DeleteOlder
        {
            public string PlayerName = "";
            public string Category = "";
            public UInt32 TimeOlderThan;
        }

        public class Take
        {
            public UInt32 Id;
            public List<(UInt32 ClassData, UInt32 InstanceData)> Items = new List<(UInt32, UInt32)>();
        }

        private static int Offset(byte[] data, int recordOffset) => data[recordOffset - 2] | (data[recordOffset - 1] << 8);

        /// <summary>A string whose offset field is at record offset <paramref name="field"/>; offset 0 is an empty string.</summary>
        private static bool TryString(byte[] data, int field, out string value)
        {
            value = "";
            if (data.Length < field)
            {
                return false;
            }
            int offset = Offset(data, field);
            return offset == 0 || SocialRequests.TryReadSized(data, offset - 2, out value);
        }

        /// <summary><c>81 e5</c> (0x621f3250): +2 SenderName, +4 Category, +6 CategoryLimit, +0a EnforceInboxLimit,
        /// +0b ChunkStrings, +0d AreChunked. A chunk marked chunked arrived before it in 81 ab / 81 ac.</summary>
        public static bool TryParseSend(byte[] data, out Send send)
        {
            send = new Send();
            if (data == null || data.Length < 0x0d)
            {
                return false;
            }
            if (!TryString(data, 0x02, out send.SenderName) || !TryString(data, 0x04, out send.Category))
            {
                return false;
            }
            send.CategoryLimit = BitConverter.ToUInt32(data, 0x06 - 2);
            send.EnforceInboxLimit = data[0x0a - 2] != 0;

            int chunks = Offset(data, 0x0b) - 2, chunked = Offset(data, 0x0d) - 2;
            if (chunks < 0 || chunks + 2 > data.Length)
            {
                return false;
            }
            int count = data[chunks] | (data[chunks + 1] << 8);
            for (int i = 0; i < count; i++)
            {
                int element = chunks + 2 + i * 3;
                if (element + 3 > data.Length)
                {
                    return false;
                }
                int relative = data[element + 1] | (data[element + 2] << 8);
                string text = null;
                if (relative != 0 && !SocialRequests.TryReadSized(data, element + relative, out text))
                {
                    return false;
                }
                send.Chunks.Add(text);
            }
            if (chunked >= 0 && chunked + 2 <= data.Length)
            {
                int flags = data[chunked] | (data[chunked + 1] << 8);
                for (int i = 0; i < flags && chunked + 2 + i < data.Length; i++)
                {
                    send.AreChunked.Add(data[chunked + 2 + i] != 0);
                }
            }
            return true;
        }

        /// <summary><c>81 ea</c> (0x621f3690, size 0x0d): +2 Category, +4 MaxHeaders, +8 StartTime, +0c DescendingOrder.
        /// Retail's client: category "", 50 headers, start 0x7ffffffe, descending.</summary>
        public static bool TryParseHeaderRequest(byte[] data, out HeaderRequest request)
        {
            request = new HeaderRequest();
            if (data == null || data.Length < 0x0b || !TryString(data, 0x02, out request.Category))
            {
                return false;
            }
            request.MaxHeaders = BitConverter.ToUInt32(data, 0x04 - 2);
            request.StartTime = BitConverter.ToUInt32(data, 0x08 - 2);
            request.Descending = data[0x0c - 2] != 0;
            return true;
        }

        /// <summary><c>81 f0</c>, <c>81 fe</c>: <c>&lt;PersistentMessageID u32&gt;</c>.</summary>
        public static bool TryParseId(byte[] data, out UInt32 id)
        {
            id = 0;
            if (data == null || data.Length < 4)
            {
                return false;
            }
            id = BitConverter.ToUInt32(data, 0);
            return true;
        }

        /// <summary><c>81 f4 &lt;PersistentMessageID u32&gt; &lt;NewStatus u32&gt;</c> and
        /// <c>81 f6 &lt;OldStatus u32&gt; &lt;NewStatus u32&gt;</c>.</summary>
        public static bool TryParsePair(byte[] data, out UInt32 first, out UInt32 second)
        {
            first = second = 0;
            if (data == null || data.Length < 8)
            {
                return false;
            }
            first = BitConverter.ToUInt32(data, 0);
            second = BitConverter.ToUInt32(data, 4);
            return true;
        }

        /// <summary><c>81 ab</c> (size 0x0d): TotalLength u32, ChunkSize u32, IsWide, IsMulti, Index (of the chunk it fills).</summary>
        public static bool TryParseChunkHeader(byte[] data, out ChunkHeader header)
        {
            header = new ChunkHeader();
            if (data == null || data.Length < 11)
            {
                return false;
            }
            header.TotalLength = BitConverter.ToUInt32(data, 0);
            header.ChunkSize = BitConverter.ToUInt32(data, 4);
            header.IsWide = data[8] != 0;
            header.IsMulti = data[9] != 0;
            header.Index = data[10];
            return true;
        }

        /// <summary><c>81 ac</c>: +2 Chunk (array offset), there <c>&lt;u16 count&gt;</c> and the bytes.</summary>
        public static bool TryParseChunk(byte[] data, out byte[] chunk)
        {
            chunk = null;
            if (data == null || data.Length < 2)
            {
                return false;
            }
            int at = Offset(data, 0x02) - 2;
            if (at < 0 || at + 2 > data.Length)
            {
                return false;
            }
            int count = data[at] | (data[at + 1] << 8);
            if (at + 2 + count > data.Length)
            {
                return false;
            }
            chunk = new byte[count];
            Array.Copy(data, at + 2, chunk, 0, count);
            return true;
        }

        /// <summary><c>81 f8</c> (0x621f3ca0): +2 PlayerName, +4 PersistentMessageIDs (u32 array), +6 NewFolder.</summary>
        public static bool TryParseClassify(byte[] data, out Classify classify)
        {
            classify = new Classify();
            if (data == null || data.Length < 6 || !TryString(data, 0x02, out classify.PlayerName) ||
                !TryString(data, 0x06, out classify.NewFolder))
            {
                return false;
            }
            int at = Offset(data, 0x04) - 2;
            if (at < 0 || at + 2 > data.Length)
            {
                return false;
            }
            int count = data[at] | (data[at + 1] << 8);
            for (int i = 0; i < count && at + 2 + i * 4 + 4 <= data.Length; i++)
            {
                classify.Ids.Add(BitConverter.ToUInt32(data, at + 2 + i * 4));
            }
            return true;
        }

        /// <summary><c>81 fa</c> (0x621f3de0): +2 PlayerName, +4 Category, +6 TimeOlderThan.</summary>
        public static bool TryParseDeleteOlder(byte[] data, out DeleteOlder delete)
        {
            delete = new DeleteOlder();
            if (data == null || data.Length < 8 || !TryString(data, 0x02, out delete.PlayerName) ||
                !TryString(data, 0x04, out delete.Category))
            {
                return false;
            }
            delete.TimeOlderThan = BitConverter.ToUInt32(data, 0x06 - 2);
            return true;
        }

        /// <summary><c>81 fc</c> (0x621f41e0, size 8): +2 PersistentMessageID, +6 Items. Each item is nine bytes,
        /// <c>00 &lt;ClassData u32&gt; &lt;InstanceData u32&gt;</c> (0x621f6757), from the message's ItemList.</summary>
        public static bool TryParseTake(byte[] data, out Take take)
        {
            take = new Take();
            if (data == null || data.Length < 6)
            {
                return false;
            }
            take.Id = BitConverter.ToUInt32(data, 0);
            int at = Offset(data, 0x06) - 2;
            if (at < 0 || at + 2 > data.Length)
            {
                return true;
            }
            int count = data[at] | (data[at + 1] << 8);
            for (int i = 0; i < count && at + 2 + i * 9 + 9 <= data.Length; i++)
            {
                int element = at + 2 + i * 9;
                UInt32 classData = BitConverter.ToUInt32(data, element + 1);
                UInt32 instanceData = BitConverter.ToUInt32(data, element + 5);
                take.Items.Add((classData, instanceData));
            }
            return true;
        }
    }

    /// <summary>
    /// The out-of-band XML that rides along with a message. The client writes, on send (0x620c8b40):
    /// <c>&lt;MXO_Mail_OOB&gt; &lt;Faction id="n"/&gt; &lt;RecipientList&gt;&lt;Recipient Name=".."/&gt;..&lt;/RecipientList&gt;
    /// &lt;Info Slot="amount"/&gt; &lt;ItemList&gt;&lt;Item FromPI="True" Slot="n"/&gt;..&lt;/ItemList&gt;</c>
    /// (the Info amount really is in an attribute called Slot). On reading it looks for <c>ItemList</c> children with
    /// ClassData and InstanceData, read with atoi (0x621f5400).
    /// </summary>
    public static class MailOob
    {
        public class Outgoing
        {
            public UInt32 Info;
            public List<string> Recipients = new List<string>();
            public List<(int Slot, bool FromPersonalInventory)> Items = new List<(int, bool)>();
        }

        public static Outgoing ParseOutgoing(string xml)
        {
            Outgoing outgoing = new Outgoing();
            if (string.IsNullOrWhiteSpace(xml))
            {
                return outgoing;
            }
            XmlDocument document = new XmlDocument();
            try
            {
                document.LoadXml(xml);
            }
            catch (XmlException)
            {
                return outgoing;
            }
            foreach (XmlElement info in document.GetElementsByTagName("Info").OfType<XmlElement>())
            {
                if (UInt32.TryParse(FirstNumber(info), NumberStyles.Integer, CultureInfo.InvariantCulture, out UInt32 amount))
                {
                    outgoing.Info += amount;
                }
            }
            foreach (XmlElement recipient in document.GetElementsByTagName("Recipient").OfType<XmlElement>())
            {
                string name = recipient.GetAttribute("Name");
                if (name.Length > 0)
                {
                    outgoing.Recipients.Add(name);
                }
            }
            foreach (XmlElement item in document.GetElementsByTagName("Item").OfType<XmlElement>())
            {
                if (int.TryParse(item.GetAttribute("Slot"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int slot))
                {
                    bool personal = !string.Equals(item.GetAttribute("FromPI"), "False", StringComparison.OrdinalIgnoreCase);
                    outgoing.Items.Add((slot, personal));
                }
            }
            return outgoing;
        }

        private static string FirstNumber(XmlElement element)
        {
            string slot = element.GetAttribute("Slot");
            if (slot.Length > 0)
            {
                return slot;
            }
            return element.Attributes.Count > 0 ? element.Attributes[0].Value : element.InnerText;
        }

        /// <summary>Retail's received OOB, with the attachments as the client's reader wants them.</summary>
        public static string BuildReceived(IEnumerable<MailItem> items)
        {
            StringBuilder xml = new StringBuilder();
            xml.Append("<?xml version=\"1.0\" encoding=\"utf-8\" ?>\n<MXO_Mail_OOB>\n\t<ItemList>\n");
            foreach (MailItem item in items ?? Enumerable.Empty<MailItem>())
            {
                xml.Append("\t\t<Item ClassData=\"").Append(unchecked((int)item.ClassData).ToString(CultureInfo.InvariantCulture))
                    .Append("\" InstanceData=\"").Append(unchecked((int)item.InstanceData).ToString(CultureInfo.InvariantCulture))
                    .Append("\"/>\n");
            }
            xml.Append("\t</ItemList>\n</MXO_Mail_OOB>\n");
            return xml.ToString();
        }
    }
}
