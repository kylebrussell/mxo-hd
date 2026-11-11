using System.Text;
using hds;

namespace HeadlessClient;

public static partial class Scenarios
{
    private const string MailSuccess = "84 00 00 8e";

    /// <summary>
    /// The client's 81 e5 as retail's sends it (docs/CLIENT-ACTIONS.md, "Email"): sender, category, limit, enforce,
    /// then the chunk array (00 &lt;u16 offset from the element&gt; each) and the chunked flags. A chunk left null here
    /// comes from 81 ab / 81 ac.
    /// </summary>
    private static byte[] MailSend(string to, string subject, string body, bool oobChunked)
    {
        string[] chunks = { to, subject, body };
        List<byte> record = new List<byte> { 0x81, 0xe5 };
        record.AddRange(BitConverter.GetBytes((ushort)0));        // SenderName: none, the server knows
        record.AddRange(BitConverter.GetBytes((ushort)0x0f));     // Category
        record.AddRange(BitConverter.GetBytes(0u));               // CategoryLimit
        record.Add(1);                                            // EnforceInboxLimit
        record.AddRange(BitConverter.GetBytes((ushort)0x1e));     // ChunkStrings
        record.AddRange(BitConverter.GetBytes((ushort)0x18));     // AreChunked
        record.AddRange(Sized("PLAYER"));
        record.AddRange(new byte[] { 4, 0, 0, 0, 0, (byte)(oobChunked ? 1 : 0) });
        record.AddRange(BitConverter.GetBytes((ushort)4));
        int elements = record.Count;
        record.AddRange(new byte[12]);
        for (int i = 0; i < chunks.Length; i++)
        {
            int element = elements + i * 3;
            byte[] relative = BitConverter.GetBytes((ushort)(record.Count - element));
            record[element + 1] = relative[0];
            record[element + 2] = relative[1];
            record.AddRange(Sized(chunks[i]));
        }
        return record.ToArray();
    }

    /// <summary>81 ab then 81 ac with the out-of-band XML for chunk 3, as retail's client sent ahead of its reply.</summary>
    private static void SendMailOob(HeadlessPlayer player, string xml)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(xml);
        player.SendRpc($"81 ab {Le((uint)bytes.Length)} {Le(300u)} 01 01 03");
        player.SendRpc(new byte[] { 0x81, 0xac, 4, 0 }.Concat(BitConverter.GetBytes((ushort)bytes.Length)).Concat(bytes).ToArray());
    }

    /// <summary>The strings of an 81 f3 (the array at +15): subject, body, out-of-band XML.</summary>
    private static List<string> MessageChunks(byte[] record)
    {
        int array = BitConverter.ToUInt16(record, 0x15);
        int count = BitConverter.ToUInt16(record, array);
        List<string> chunks = new List<string>();
        for (int i = 0; i < count; i++)
        {
            int element = array + 2 + i * 3;
            int at = element + BitConverter.ToUInt16(record, element + 1);
            int size = BitConverter.ToUInt16(record, at);
            chunks.Add(Encoding.ASCII.GetString(record, at + 2, size - 1));
        }
        return chunks;
    }

    /// <summary>Alice mails Bob, who is told, lists, reads, deletes and empties; then a send to nobody fails like retail's.</summary>
    private static async Task EmailSendReadDelete(ScenarioContext context)
    {
        HeadlessPlayer[] players = await context.AddPlayersInWorldAsync(new CharacterSeed("Alice"), new CharacterSeed("Bob") { X = 17120 });
        HeadlessPlayer alice = players[0], bob = players[1];

        // The login handshake's 81 48 gets retail's feature list
        alice.SendRpc("81 48");
        await alice.WaitForRpcAsync("81 49 10 00 00 00 00 00");

        alice.SendRpc(MailSend("Bob", "Hello", "Hi Bob, this is a test.", false));
        RpcRecord sent = await alice.WaitForRpcAsync($"81 e6 ?? ?? ?? ?? {MailSuccess}");
        uint id = BitConverter.ToUInt32(sent.Bytes, 2);
        Check.That(id != 0, "The send names the new message");

        RpcRecord notice = await bob.WaitForRpcAsync("81 ed 19 00");
        Check.Equal(id, BitConverter.ToUInt32(notice.Bytes, 8), "Notification PersistentMessageID");
        Check.Equal(1u, BitConverter.ToUInt32(notice.Bytes, 0x10), "Notification Status (new)");
        Check.Equal((byte)1, notice.Bytes[0x15], "Notification flag");
        Check.Equal("SOE+MXO+", StringAtOffset(notice.Bytes, 2).Substring(0, 8), "Sender address prefix");
        Check.That(StringAtOffset(notice.Bytes, 2).EndsWith("+Alice"), "Sender address ends with the handle");
        Check.Equal("Hello", StringAtOffset(notice.Bytes, 0x16), "Subject");

        // The inbox, as the client asks for it: 81 eb, 81 ef, the headers
        bob.SendRpc($"81 ea 0d 00 {Le(50u)} {Le(0x7ffffffeu)} 01 01 00 00");
        await bob.WaitForRpcAsync("81 eb 2c 01 00 00");
        RpcRecord end = await bob.WaitForRpcAsync($"81 ef 0c 00 {Le(1u)} {MailSuccess}");
        Check.That(StringAtOffset(end.Bytes, 2).EndsWith("+Bob"), "81 ef PlayerName is Bob's address");
        RpcRecord header = await bob.WaitForRpcAsync($"81 ed 19 00 ?? ?? ?? ?? {Le(id)}");
        Check.Equal((byte)0, header.Bytes[0x14], "The last header has More 0");
        Check.Equal((byte)0, header.Bytes[0x15], "A listed header is not a notification");

        bob.SendRpc($"81 f0 {Le(id)}");
        RpcRecord message = await bob.WaitForRpcAsync($"81 f3 19 00 ?? ?? ?? ?? {Le(id)}");
        Check.Equal(3u, BitConverter.ToUInt32(message.Bytes, 0x10), "Read message status");
        List<string> chunks = MessageChunks(message.Bytes);
        Check.Equal("Hello", chunks[0], "Subject chunk");
        Check.Equal("Hi Bob, this is a test.", chunks[1], "Body chunk");
        Check.That(chunks[2].Contains("<MXO_Mail_OOB>"), "Out-of-band chunk");

        // Delete, then the inbox closes: 4 -> 5 empties it
        bob.SendRpc($"81 f4 {Le(id)} {Le(4u)}");
        await bob.WaitForRpcAsync($"81 f5 {MailSuccess}");
        Check.Equal(4L, await context.Database.LongAsync($"SELECT status FROM mail WHERE id={id}"), "Deleted status in the database");
        bob.SendRpc($"81 f6 {Le(4u)} {Le(5u)}");
        await bob.WaitForRpcAsync($"81 f7 {MailSuccess}");
        bob.SendRpc($"81 f6 {Le(1u)} {Le(2u)}");
        await bob.WaitForRpcAsync($"81 f7 {MailSuccess}");
        Check.Equal(0L, await context.Database.LongAsync($"SELECT COUNT(*) FROM mail WHERE ownerCharId={bob.CharId}"), "Bob's mail after emptying");

        // A recipient that doesn't exist: retail's DESTAVATARDOESNTEXIST, then a system mail
        alice.SendRpc(MailSend("soe+mxo+nowhere+Nobody", "Lost", "Nobody reads this.", false));
        await alice.WaitForRpcAsync("81 e6 00 00 00 00 41 00 00 0e");
        RpcRecord undeliverable = await alice.WaitForRpcAsync("81 ed 19 00");
        Check.Equal("SYSTEM", StringAtOffset(undeliverable.Bytes, 6), "Undeliverable notice category");
        Check.Equal("Message Undeliverable", StringAtOffset(undeliverable.Bytes, 0x16), "Undeliverable notice subject");
    }

    /// <summary>Alice sends Bob a Code Bit and information; Bob takes the item. Then ?mailbot and a mail to MailBot.</summary>
    private static async Task EmailAttachmentsAndMailBot(ScenarioContext context)
    {
        HeadlessPlayer[] players = await context.AddPlayersInWorldAsync(
            new CharacterSeed("Alice") { Items = new[] { (3, NpcLoot.CodeBit1, 1) }, Info = 500 },
            new CharacterSeed("Bob") { X = 17120, Info = 50 });
        HeadlessPlayer alice = players[0], bob = players[1];

        SendMailOob(alice, "<?xml version=\"1.0\" encoding=\"utf-8\" ?>\n<MXO_Mail_OOB>\n\t<Info Slot=\"200\"/>\n" +
                           "\t<ItemList>\n\t\t<Item FromPI=\"True\" Slot=\"3\"/>\n\t</ItemList>\n</MXO_Mail_OOB>\n");
        alice.SendRpc(MailSend("Bob", "Gift", "A Code Bit and some info.", true));
        RpcRecord sent = await alice.WaitForRpcAsync($"81 e6 ?? ?? ?? ?? {MailSuccess}");
        uint id = BitConverter.ToUInt32(sent.Bytes, 2);
        // The bag and information updates go out ahead of the answer
        Check.That(alice.Received("5f 03 00 01").Count == 1, "Alice's client told the item left slot 3");
        Check.That(alice.Received($"80 e4 {Le(300u)}").Count == 1, "Alice's information after sending 200");
        await bob.WaitForRpcAsync("81 ed 19 00");
        await bob.WaitForRpcAsync($"80 e4 {Le(250u)}");
        Check.Equal(0L, await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={alice.CharId} AND goid={NpcLoot.CodeBit1}"), "Alice's Code Bit left her bag");

        bob.SendRpc($"81 f0 {Le(id)}");
        RpcRecord message = await bob.WaitForRpcAsync($"81 f3 19 00 ?? ?? ?? ?? {Le(id)}");
        string oob = MessageChunks(message.Bytes)[2];
        Check.That(oob.Contains($"<Item ClassData=\"{NpcLoot.CodeBit1}\""), "The attachment in the message's ItemList");
        int instanceAt = oob.IndexOf("InstanceData=\"", StringComparison.Ordinal) + 14;
        uint instance = unchecked((uint)int.Parse(oob.Substring(instanceAt, oob.IndexOf('"', instanceAt) - instanceAt)));

        // 81 fc <id> <items offset>, there count then 00 <class u32> <instance u32>
        bob.SendRpc($"81 fc {Le(id)} 08 00 01 00 00 {Le(NpcLoot.CodeBit1)} {Le(instance)}");
        await bob.WaitForRpcAsync($"81 fd {MailSuccess}");
        RpcRecord added = bob.Received("5e").Single();
        Check.Equal(NpcLoot.CodeBit1, BitConverter.ToUInt32(added.Bytes, 3), "The item Bob took");
        Check.Equal(1L, await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={bob.CharId} AND goid={NpcLoot.CodeBit1}"), "Bob's Code Bit in the database");
        Check.Equal("", await context.Database.ScalarAsync($"SELECT items FROM mail WHERE id={id}"), "The message has no attachments left");
        Check.Equal(250L, await context.Database.LongAsync($"SELECT cash FROM characters WHERE charId={bob.CharId}"), "Bob's information in the database");

        // Taking again: nothing there
        bob.SendRpc($"81 fc {Le(id)} 08 00 01 00 00 {Le(NpcLoot.CodeBit1)} {Le(instance)}");
        await bob.WaitForRpcAsync("81 fd 77 00 00 0e");

        // Dev helpers: ?mailbot, and a mail to MailBot comes back
        bob.SendChat("?mailbot Testing");
        RpcRecord botMail = await bob.WaitForRpcAsync("81 ed 19 00");
        Check.That(StringAtOffset(botMail.Bytes, 2).EndsWith("+MailBot"), "?mailbot's sender");
        Check.Equal("Testing", StringAtOffset(botMail.Bytes, 0x16), "?mailbot's subject");

        alice.SendRpc(MailSend("MailBot", "Ping", "Anyone there?", false));
        await alice.WaitForRpcAsync($"81 e6 ?? ?? ?? ?? {MailSuccess}");
        RpcRecord reply = await alice.WaitForRpcAsync("81 ed 19 00");
        Check.Equal("RE: Ping", StringAtOffset(reply.Bytes, 0x16), "MailBot's reply");
    }
}
