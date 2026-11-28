namespace HeadlessClient;

public static partial class Scenarios
{
    public const string IgnoredMailSubject = "Ignore Check";
    private const string MailIgnoring = "54 00 00 0e";
    private const string IgnoreMailRefusal = "A recipient is ignoring you. Your items and information were kept.";

    private static async Task EmailRespectsIgnoreList(ScenarioContext context)
    {
        var db = context.Database;
        var players = await context.AddPlayersInWorldAsync(
            new CharacterSeed("MailSender") { Info = 500, Items = new[] { (3, 2399u, 1) } },
            new CharacterSeed("QAFriend") { X = 17120, Info = 50 });
        var sender = players[0];
        var recipient = players[1];
        recipient.SendRpc(new byte[] { 0x80, 0xe0, 4, 0 }.Concat(Sized(sender.Handle)).ToArray());
        await recipient.WaitForRpcAsync("80 e1 08 00 84 00 00 8e");

        const uint offline = 1910;
        await db.ExecuteAsync($"""
            INSERT INTO characters(charId,userId,worldId,status,handle,created)
                VALUES ({offline},0,1,0,'OfflineMailbox',NOW());
            INSERT INTO ignorelist(charId,handle) VALUES ({offline},'{sender.Handle}');
            """);
        sender.SendRpc(MailSend($"{sender.Handle},OfflineMailbox", "Blocked group", "No partial delivery.", false));
        await sender.WaitForRpcAsync($"81 e6 00 00 00 00 {MailIgnoring}");
        Check.Equal(0L, await db.LongAsync("SELECT COUNT(*) FROM mail WHERE subject='Blocked group'"),
            "An offline recipient's ignore blocks the entire multi-recipient send");
        await ProveMailIgnoreSenderAsync(sender, recipient, db);
    }

    public static async Task AllowMailSenderAsync(HeadlessPlayer recipient, string handle)
    {
        recipient.SendRpc(new byte[] { 0x80, 0xe2, 4, 0 }.Concat(Sized(handle)).ToArray());
        await recipient.WaitForRpcAsync("80 e3 08 00 84 00 00 8e");
    }

    public static async Task ProveMailIgnoreSenderAsync(HeadlessPlayer sender, HeadlessPlayer recipient, ThrowawayDatabase db)
    {
        const string body = "One pill and 200 Info.";
        const string xml = """
            <MXO_Mail_OOB><Info Slot="200"/><ItemList><Item FromPI="True" Slot="3"/></ItemList></MXO_Mail_OOB>
            """;
        sender.SkipReceived();
        recipient.SkipReceived();
        SendMailWithAttachments(sender, recipient.Handle, IgnoredMailSubject, body, xml);
        await sender.WaitForTextAsync(IgnoreMailRefusal);
        await sender.WaitForRpcAsync($"81 e6 00 00 00 00 {MailIgnoring}");
        Check.Equal(0, recipient.Received("81 ed").Count, "No mail notification is published for a blocked send");
        Check.Equal(0, sender.Received("5f 03 00").Count, "A blocked send does not remove the sender's bag item");
        await CheckIgnoredMailRetainedAsync(db, sender.CharId, recipient.CharId);

        await AllowMailSenderAsync(recipient, sender.Handle);
        SendMailWithAttachments(sender, recipient.Handle, IgnoredMailSubject, body, xml);
        await sender.WaitForRpcAsync($"81 e6 ?? ?? ?? ?? {MailSuccess}");
        await CheckIgnoredMailDeliveredAsync(db, sender.CharId, recipient);
    }

    public static async Task CheckIgnoredMailRetainedAsync(ThrowawayDatabase db, uint sender, uint recipient)
    {
        Check.Equal(0L, await db.LongAsync($"SELECT COUNT(*) FROM mail WHERE ownerCharId={recipient} AND subject='{IgnoredMailSubject}'"),
            "A blocked send creates no inbox row");
        Check.Equal(500L, await db.LongAsync($"SELECT cash FROM characters WHERE charId={sender}"), "Sender keeps 500 Info");
        Check.Equal(50L, await db.LongAsync($"SELECT cash FROM characters WHERE charId={recipient}"), "Receiver keeps 50 Info");
        Check.Equal(1L, await db.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={sender} AND slot=3 AND goid=2399"),
            "Sender keeps the pill in slot 3");
    }

    public static async Task<uint> CheckIgnoredMailDeliveredAsync(ThrowawayDatabase db, uint sender, HeadlessPlayer recipient)
    {
        Check.Equal(1L, await db.LongAsync($"SELECT COUNT(*) FROM mail WHERE ownerCharId={recipient.CharId} AND subject='{IgnoredMailSubject}'"),
            "The retry delivers exactly one message");
        uint id = checked((uint)await db.LongAsync($"SELECT id FROM mail WHERE ownerCharId={recipient.CharId} AND subject='{IgnoredMailSubject}'"));
        await recipient.WaitForRpcAsync($"81 ed 19 00 ?? ?? ?? ?? {Le(id)}");
        Check.Equal("2399:0:1", await db.ScalarAsync($"SELECT items FROM mail WHERE id={id}"), "The pill is attached once");
        Check.Equal(0L, await db.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={sender} AND slot=3"), "The successful retry spends the pill");
        Check.Equal(300L, await db.LongAsync($"SELECT cash FROM characters WHERE charId={sender}"), "Sender paid 200 Info once");
        Check.Equal(250L, await db.LongAsync($"SELECT cash FROM characters WHERE charId={recipient.CharId}"), "Receiver got 200 Info once");
        return id;
    }
}
