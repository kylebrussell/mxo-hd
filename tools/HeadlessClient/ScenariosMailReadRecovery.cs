namespace HeadlessClient;

public static partial class Scenarios
{
    public const string MailReadSubject = "Read Save Check", MailReadControl = "Read Control";
    public const string MailReadBody = "This body opens even if the read marker cannot be saved.";
    public const string MailReadControlBody = "The second message still opens.";
    public const string MailReadFault = "qa_mail_read_status";
    private const string MailReadWarning = "The message opened, but its read status could not be saved.";

    public static string MailReadSeedSql(uint owner) => $"""
        INSERT INTO mail(ownerCharId,sender,sentTime,status,subject,body,items)
        VALUES ({owner},CONCAT('SOE+MXO+', (SELECT name FROM worlds ORDER BY worldId LIMIT 1), '+QAFriend'),
                UNIX_TIMESTAMP()-10,2,'{MailReadSubject}','{MailReadBody}', '2399:0:1'),
               ({owner},CONCAT('SOE+MXO+', (SELECT name FROM worlds ORDER BY worldId LIMIT 1), '+QAFriend'),
                UNIX_TIMESTAMP()-20,2,'{MailReadControl}','{MailReadControlBody}', '');
        ALTER TABLE mail ADD CONSTRAINT {MailReadFault}
            CHECK (ownerCharId<>{owner} OR subject<>'{MailReadSubject}' OR status<>3);
        """;

    public static Task<string> RemoveMailReadFaultAsync(ThrowawayDatabase db) =>
        db.ExecuteAsync($"ALTER TABLE mail DROP CHECK {MailReadFault}");

    private static async Task EmailRecoversFromReadStatusFailure(ScenarioContext context)
    {
        var db = context.Database;
        var players = await context.AddPlayersInWorldAsync(new CharacterSeed("MailReader"),
            new CharacterSeed("QAFriend") { X = 17120 });
        var reader = players[0];
        await db.ExecuteAsync(MailReadSeedSql(reader.CharId));
        uint id = await ReadFailureFixtureMailIdAsync(db, reader.CharId);
        var other = players[1];
        other.SendRpc($"81 f0 {Le(id)}");
        await other.WaitForRpcAsync("81 f2 52 00 00 0e");
        await ProveMailReadRecoveryAsync(reader, db, id);
    }

    public static async Task<uint> ReadFailureFixtureMailIdAsync(ThrowawayDatabase db, uint owner) =>
        checked((uint)await db.LongAsync($"SELECT id FROM mail WHERE ownerCharId={owner} AND subject='{MailReadSubject}'"));

    public static async Task CheckReadMarkerAsync(ThrowawayDatabase db, uint owner, uint id, int status)
    {
        string owned = $"ownerCharId={owner} AND id={id}";
        Check.Equal((long)status, await db.LongAsync($"SELECT status FROM mail WHERE {owned}"), "The saved read marker matches the outcome");
        Check.Equal("2399:0:1", await db.ScalarAsync($"SELECT items FROM mail WHERE {owned}"), "The pill remains attached");
    }

    public static async Task ProveMailReadRecoveryAsync(HeadlessPlayer reader, ThrowawayDatabase db, uint id)
    {
        string request = $"81 f0 {Le(id)}";
        uint control = checked((uint)await db.LongAsync($"SELECT id FROM mail WHERE ownerCharId={reader.CharId} AND subject='{MailReadControl}'"));
        reader.SendRpc(request);
        await reader.WaitForTextAsync(MailReadWarning);
        await CheckMessageAsync(id, MailReadSubject, MailReadBody, 2, pill: true);
        await CheckReadMarkerAsync(db, reader.CharId, id, 2);

        reader.SendRpc($"81 f0 {Le(control)}");
        await CheckMessageAsync(control, MailReadControl, MailReadControlBody, 3, pill: false);

        await RemoveMailReadFaultAsync(db);
        reader.SendRpc(request);
        await CheckMessageAsync(id, MailReadSubject, MailReadBody, 3, pill: true);
        await CheckReadMarkerAsync(db, reader.CharId, id, 3);

        reader.SendRpc($"81 f4 {Le(id)} {Le(4u)}");
        await reader.WaitForRpcAsync($"81 f5 {MailSuccess}");
        reader.SendRpc(request);
        await CheckMessageAsync(id, MailReadSubject, MailReadBody, 4, pill: true);
        await CheckReadMarkerAsync(db, reader.CharId, id, 4);
        Check.Equal(0, reader.Received("81 f2").Count, "Owned messages open without a failure modal");

        async Task CheckMessageAsync(uint message, string subject, string body, uint status, bool pill)
        {
            var opened = await reader.WaitForRpcAsync($"81 f3 19 00 ?? ?? ?? ?? {Le(message)}");
            Check.Equal(status, BitConverter.ToUInt32(opened.Bytes, 16), "The read response carries the stored status");
            var chunks = MessageChunks(opened.Bytes);
            Check.Equal(subject, chunks[0], "The selected subject opens");
            Check.Equal(body, chunks[1], "The saved body opens");
            Check.Equal(pill, chunks[2].Contains("ClassData=\"2399\""), "The response preserves the saved attachment list");
        }
    }
}
