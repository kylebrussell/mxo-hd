using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class MailReadRecoveryMode : NativeQaMode
{
    private const int X = 17000, Y = 495, Z = 2300, FriendX = 17120;
    private const uint Reader = 110;

    public override string Name => "mail-read-recovery";
    public override NativeQaModePlan Plan { get; } = new(new ushort[] { 2 }, X, Z,
        Array.Empty<string>(), new[] { $"""
            UPDATE characters SET y={Y} WHERE charId={Reader};
            DELETE FROM mail WHERE ownerCharId={Reader};
            """ },
        new[] { $"UPDATE characters SET x={FriendX},y={Y},z={Z} WHERE handle='QAFriend';", Scenarios.MailReadSeedSql(Reader) },
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        readyMessages: new[] {
            "Mara Central NAV CSR (17000,495,2300), QAFriend (17120,495,2300). No teleport, world target or dev command. Open Phone > Email. Read Save Check and Read Control start unread.",
            "Select Read Save Check. The disclosed disposable CHECK rejects only its status-3 write. Expect [MAIL] Read status could not be saved, 'The message opened, but its read status could not be saved.' in chat, and 81 f3 with saved status 2. The body and its Health Pill appear, no Mail modal. The stock UI may label the open row Read locally.",
            """Select Read Control. Expect 'The second message still opens.' and 81 f3 with status 3. Close Email. Send {"actor":"QAFriend","action":"mail-read-retry"} and wait for "Read marker stayed unread; fixture CHECK removed". The helper checks the saved pill and status 2, then removes only the fixture CHECK.""",
            """Relog to clear the stock body cache. Open Email and select Read Save Check. Expect 81 f3 with status 3 and the pill. Send {"actor":"QAFriend","action":"mail-read-verify"} to confirm durable status 3 and the attachment."""
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var db = context.OwnedDatabase!;
        var server = context.ActualServer!;
        NavMesh.UseDirectory(Path.Combine(context.RepositoryRoot!, "data", "navmesh"));
        foreach (int x in new[] { X, FriendX })
            Check.That(NavMesh.Locate(1, x, Y, Z, out var stand) && !stand.indoors && stand.floorY + 95 == Y,
                $"Outdoor NAV at {x},{Y},{Z}");

        var helper = await HeadlessPlayer.LoginAsync(context.Address, "QAFriend", "QAFriend");
        context.Helpers.Add("QAFriend", helper);
        await helper.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, helper);
        uint id = await Scenarios.ReadFailureFixtureMailIdAsync(db, Reader);
        await Scenarios.CheckReadMarkerAsync(db, Reader, id, 2);
        Check.Equal(2L, await db.LongAsync($"SELECT COUNT(*) FROM mail WHERE ownerCharId={Reader} AND status=2"),
            "Both inbox rows start unread");
        Check.Equal(1L, await db.LongAsync("SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS " +
            $"WHERE CONSTRAINT_SCHEMA=DATABASE() AND TABLE_NAME='mail' AND CONSTRAINT_NAME='{Scenarios.MailReadFault}'"),
            "The disclosed read-status failure is armed");
        Console.WriteLine($"{Scenarios.MailReadSubject} persistent id={id}.");
        if (context.StockProbe is not { } reader) return Array.Empty<byte>();

        await reader.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, reader);
        await Scenarios.ProveMailReadRecoveryAsync(reader, db, id);
        return Array.Empty<byte>();
    }

    internal static async Task<string> CheckReadAsync(HeadlessPlayer actor, ThrowawayDatabase db, bool retry)
    {
        if (actor.Handle != "QAFriend") throw new ArgumentException("Use QAFriend for the mail read check.");
        uint id = await Scenarios.ReadFailureFixtureMailIdAsync(db, Reader);
        await Scenarios.CheckReadMarkerAsync(db, Reader, id, retry ? 2 : 3);
        if (!retry) return "Read marker saved; pill still attached";

        await Scenarios.RemoveMailReadFaultAsync(db);
        return "Read marker stayed unread; fixture CHECK removed";
    }
}
