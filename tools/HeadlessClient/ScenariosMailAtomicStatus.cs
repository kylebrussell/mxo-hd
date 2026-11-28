namespace HeadlessClient;

public static partial class Scenarios
{
    public const string MailDeleteSubject = "Delete Check", MailControlSubject = "Inbox Control";
    public const string MailStatusFault = "qa_mail_status";
    private const string MailBadData = "68 00 00 0e";

    public static string MailStatusSeedSql(uint owner) => $"""
        INSERT INTO mail(ownerCharId,sender,sentTime,status,subject,body,items)
        VALUES ({owner},CONCAT('SOE+MXO+', (SELECT name FROM worlds ORDER BY worldId LIMIT 1), '+QAFriend'),
                UNIX_TIMESTAMP()-10,3,'{MailDeleteSubject}','One Health Pill.', '2399:0:1'),
               ({owner},CONCAT('SOE+MXO+', (SELECT name FROM worlds ORDER BY worldId LIMIT 1), '+QAFriend'),
                UNIX_TIMESTAMP()-20,3,'{MailControlSubject}','Keep this message.', '');
        ALTER TABLE mail ADD CONSTRAINT {MailStatusFault}
            CHECK (ownerCharId<>{owner} OR subject<>'{MailDeleteSubject}' OR status<>4);
        """;

    public static Task<string> ReleaseMailStatusFaultAsync(ThrowawayDatabase db) =>
        db.ExecuteAsync($"ALTER TABLE mail DROP CHECK {MailStatusFault}");

    private static async Task EmailAtomicStatus(ScenarioContext context)
    {
        var db = context.Database;
        var players = await context.AddPlayersInWorldAsync(new CharacterSeed("MailReader"),
            new CharacterSeed("QAFriend") { X = 17120 });
        var reader = players[0];
        var other = players[1];
        await db.ExecuteAsync(MailStatusSeedSql(reader.CharId));
        uint id = checked((uint)await db.LongAsync($"SELECT id FROM mail WHERE ownerCharId={reader.CharId} AND subject='{MailDeleteSubject}'"));
        other.SendRpc($"81 f4 {Le(id)} {Le(4u)}");
        await other.WaitForRpcAsync("81 f5 52 00 00 0e");
        await ProveAtomicMailStatusAsync(reader, db, id);

        await db.ExecuteAsync($"""
            INSERT INTO mail(ownerCharId,sentTime,status,subject,body)
            VALUES ({reader.CharId},UNIX_TIMESTAMP()-10,1,'New first','a'),
                   ({reader.CharId},UNIX_TIMESTAMP()-20,1,'New second','b'),
                   ({other.CharId},UNIX_TIMESTAMP(),1,'Other mailbox','c');
            ALTER TABLE mail ADD CONSTRAINT qa_mail_bulk_status
                CHECK (ownerCharId<>{reader.CharId} OR subject<>'New second' OR status<>2);
            """);
        string closeSeen = $"81 f6 {Le(1u)} {Le(2u)}";
        reader.SendRpc(closeSeen);
        await reader.WaitForRpcAsync($"81 f7 {MailBadData}");
        await reader.WaitForTextAsync("The inbox status could not be changed. Your messages were kept.");
        Check.Equal(2L, await CountReaderStatusAsync(1), "A failed close keeps both new rows");
        await db.ExecuteAsync("ALTER TABLE mail DROP CHECK qa_mail_bulk_status");
        reader.SendRpc(closeSeen);
        await reader.WaitForRpcAsync($"81 f7 {MailSuccess}");
        Check.Equal(2L, await CountReaderStatusAsync(2), "The retry marks both rows seen");
        Check.Equal(1L, await db.LongAsync($"SELECT status FROM mail WHERE ownerCharId={other.CharId} AND subject='Other mailbox'"),
            "Bulk status is scoped to the inbox owner");

        Task<long> CountReaderStatusAsync(int status) =>
            db.LongAsync($"SELECT COUNT(*) FROM mail WHERE ownerCharId={reader.CharId} AND status={status}");
    }

    public static async Task ProveAtomicMailStatusAsync(HeadlessPlayer reader, ThrowawayDatabase db, uint id)
    {
        string delete = $"81 f4 {Le(id)} {Le(4u)}";
        string undelete = $"81 f4 {Le(id)} {Le(3u)}";
        string purge = $"81 f6 {Le(4u)} {Le(5u)}";
        string owned = $"ownerCharId={reader.CharId} AND id={id}";
        reader.SendRpc(delete);
        await reader.WaitForRpcAsync($"81 f5 {MailBadData}");
        await reader.WaitForTextAsync("The mail status could not be changed. The message was kept.");
        Check.Equal(0, reader.Received("81 f3").Count, "The failure sends no full message ahead of the stock inbox rebuild");

        // The stock 81 f5 failure consumer clears its cached headers and requests the list again.
        reader.SendRpc($"81 ea 0d 00 {Le(50u)} {Le(0x7ffffffeu)} 01 01 00 00");
        await reader.WaitForRpcAsync($"81 ef 0c 00 {Le(2u)} {MailSuccess}");
        var header = await reader.WaitForRpcAsync($"81 ed 19 00 ?? ?? ?? ?? {Le(id)}");
        Check.Equal(3u, BitConverter.ToUInt32(header.Bytes, 16), "The fresh inbox restores the saved status");
        reader.SendRpc($"81 f0 {Le(id)}");
        var restored = await reader.WaitForRpcAsync($"81 f3 19 00 ?? ?? ?? ?? {Le(id)}");
        var chunks = MessageChunks(restored.Bytes);
        Check.Equal(MailDeleteSubject, chunks[0], "The restored row is the selected message");
        Check.That(chunks[2].Contains("ClassData=\"2399\""), "The restored message still shows its attached pill");
        await CheckStoredMessageAsync(3);

        // An inbox close after a refused delete cannot purge the persisted message.
        reader.SendRpc(purge);
        await reader.WaitForRpcAsync($"81 f7 {MailSuccess}");
        await CheckStoredMessageAsync(3);

        await ReleaseMailStatusFaultAsync(db);
        reader.SendRpc(delete);
        await reader.WaitForRpcAsync($"81 f5 {MailSuccess}");
        await CheckStoredMessageAsync(4);
        reader.SendRpc(undelete);
        await reader.WaitForRpcAsync($"81 f5 {MailSuccess}");
        await CheckStoredMessageAsync(3);

        reader.SendRpc(delete);
        await reader.WaitForRpcAsync($"81 f5 {MailSuccess}");
        reader.SendRpc(purge);
        await reader.WaitForRpcAsync($"81 f7 {MailSuccess}");
        Check.Equal(0L, await db.LongAsync($"SELECT COUNT(*) FROM mail WHERE {owned}"), "Close purges the committed delete");
        Check.Equal("Keep this message.", await db.ScalarAsync($"SELECT body FROM mail WHERE ownerCharId={reader.CharId} AND subject='{MailControlSubject}'"),
            "Closing the inbox keeps the other message");

        async Task CheckStoredMessageAsync(int status)
        {
            Check.Equal((long)status, await db.LongAsync($"SELECT status FROM mail WHERE {owned}"), "The saved status matches the request outcome");
            Check.Equal("2399:0:1", await db.ScalarAsync($"SELECT items FROM mail WHERE {owned}"), "Status changes never consume the attachment");
        }
    }
}
