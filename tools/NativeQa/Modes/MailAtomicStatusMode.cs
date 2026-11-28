using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class MailAtomicStatusMode : NativeQaMode
{
    private const int X = 17000, Y = 495, Z = 2300, FriendX = 17120;

    public override string Name => "mail-status-atomic";
    public override NativeQaModePlan Plan { get; } = new(new ushort[] { 2 }, X, Z,
        Array.Empty<string>(), new[] { $"""
            UPDATE characters SET y={Y} WHERE charId=110;
            DELETE FROM mail WHERE ownerCharId=110;
            """ },
        new[] { $"UPDATE characters SET x={FriendX},y={Y},z={Z} WHERE handle='QAFriend';", Scenarios.MailStatusSeedSql(110) },
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        readyMessages: new[] {
            "Mara Central NAV CSR (17000,495,2300) and QAFriend (17120,495,2300). No teleport, world target or dev command. Phone > Email > Delete Check. Verify its body and one Health Pill attachment.",
            "Click Delete. The disclosed disposable CHECK refuses status 4 only for Delete Check. Expect 81 f5 68 00 00 0e, then the stock 81 ea and fresh 81 ed headers with the saved Read status. Chat says 'The message was kept.' Dismiss the Mail popup and reselect Delete Check. The pill remains attached. Expect 2/300 with 2 shown.",
            """Send {"actor":"QAFriend","action":"mail-status-retry"} and wait for "Mail status fixture CHECK removed". This removes only the named CHECK from the owned disposable mail table. In the same inbox click Delete again. Expect 81 f5 84 00 00 8e. Click Undelete and verify the same success and pill still attached.""",
            "Delete Check is disposable. Click Delete again and Close. Expect 81 f6 4->5 acknowledged by 81 f7 84 00 00 8e. Reopen Email. Delete Check is gone and Inbox Control still opens with body 'Keep this message.' Close normally."
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
        uint id = checked((uint)await db.LongAsync($"SELECT id FROM mail WHERE ownerCharId=110 AND subject='{Scenarios.MailDeleteSubject}'"));
        Check.That(id != 0, "Delete Check has a persistent ID");
        Check.Equal("2399:0:1", await db.ScalarAsync($"SELECT items FROM mail WHERE id={id} AND status=3"), "The target message has one pill and starts read");
        Check.Equal(1L, await db.LongAsync($"SELECT COUNT(*) FROM mail WHERE ownerCharId=110 AND subject='{Scenarios.MailControlSubject}'"),
            "The other inbox row is present");
        Check.Equal(1L, await db.LongAsync("SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS " +
            $"WHERE CONSTRAINT_SCHEMA=DATABASE() AND TABLE_NAME='mail' AND CONSTRAINT_NAME='{Scenarios.MailStatusFault}'"),
            "The disclosed mail status fault is armed");
        Console.WriteLine($"{Scenarios.MailDeleteSubject} persistent id={id}.");
        if (context.StockProbe is not { } reader) return Array.Empty<byte>();
        await reader.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, reader);
        await Scenarios.ProveAtomicMailStatusAsync(reader, db, id);
        return Array.Empty<byte>();
    }
}
