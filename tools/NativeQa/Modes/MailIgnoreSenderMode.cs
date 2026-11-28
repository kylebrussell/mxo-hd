using System.Text.Json;
using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class MailIgnoreSenderMode : NativeQaMode
{
    private const int X = 17000, Y = 495, Z = 2300, FriendX = 17120;
    private const uint Sender = 110;
    private const string SenderHandle = "CSR_Tester";

    public override string Name => "mail-ignore-sender";
    public override NativeQaModePlan Plan { get; } = new(new ushort[] { 2 }, X, Z,
        Array.Empty<string>(), new[] { $"""
            UPDATE characters SET y={Y},cash=500 WHERE charId={Sender};
            DELETE FROM mail WHERE ownerCharId={Sender};
            DELETE FROM inventory WHERE charId={Sender} AND slot BETWEEN 0 AND 96;
            INSERT INTO inventory(charId,goid,slot,count,purity) VALUES ({Sender},2399,3,1,0);
            """ }, new[] { $"""
            UPDATE characters SET x={FriendX},y={Y},z={Z},cash=50 WHERE handle='QAFriend';
            INSERT IGNORE INTO ignorelist(charId,handle) SELECT charId,'{SenderHandle}' FROM characters WHERE handle='QAFriend';
            """ },
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        readyMessages: new[] {
            "Verified Mara Central NAV CSR (17000,495,2300) and QAFriend (17120,495,2300). No teleport, world target or dev command. QAFriend's stored ignore list contains CSR_Tester.",
            "Phone > Email > New. Use bag slot 3 Health Pill > right-click Email this item. Compose to QAFriend, subject Ignore Check, nonempty body, and attach 200 Info. Send. Expect 81 e6 00 00 00 00 54 00 00 0e, refusal chat, 500 Info and the pill still in bag 3. Dismiss any Mail popup.",
            """Send {"actor":"QAFriend","action":"mail-ignore","step":"retry"}. The helper checks the pill, both wallets and empty inbox, then clears the ignore via 80 e2. Wait for "Ignore removed; pill and both wallets were kept".""",
            """Retry the same mail, recreating the compose window if needed. Expect 81 e6 success, 300 Info and bag 3 empty. Send {"actor":"QAFriend","action":"mail-ignore","step":"verify"} to verify one message containing the pill, with QAFriend at 250 Info."""
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var db = context.OwnedDatabase!;
        var server = context.ActualServer!;
        NavMesh.UseDirectory(Path.Combine(context.RepositoryRoot!, "data", "navmesh"));
        foreach (int x in new[] { X, FriendX })
            Check.That(NavMesh.Locate(1, x, Y, Z, out var stand) && !stand.indoors && stand.floorY + 95 == Y,
                $"Outdoor NAV at {x},{Y},{Z}");

        var recipient = await HeadlessPlayer.LoginAsync(context.Address, "QAFriend", "QAFriend");
        context.Helpers.Add("QAFriend", recipient);
        await recipient.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, recipient);
        await recipient.WaitForRpcAsync("80 e1 08 00 3d 00 00 8e");
        Check.Equal(1L, await db.LongAsync($"SELECT COUNT(*) FROM ignorelist WHERE charId={recipient.CharId} AND handle='{SenderHandle}'"),
            "QAFriend's stored ignore blocks CSR");
        await Scenarios.CheckIgnoredMailRetainedAsync(db, Sender, recipient.CharId);

        if (context.StockProbe is not { } sender) return Array.Empty<byte>();
        await sender.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, sender);
        await Scenarios.ProveMailIgnoreSenderAsync(sender, recipient, db);
        return Array.Empty<byte>();
    }

    public static async Task<object> SendActionAsync(HeadlessPlayer actor, JsonElement request, ThrowawayDatabase db)
    {
        if (actor.Handle != "QAFriend") throw new ArgumentException("Use QAFriend for this mail check.");
        switch (request.GetProperty("step").GetString())
        {
            case "retry":
                await Scenarios.CheckIgnoredMailRetainedAsync(db, Sender, actor.CharId);
                await Scenarios.AllowMailSenderAsync(actor, SenderHandle);
                return "Ignore removed; pill and both wallets were kept";
            case "verify":
                uint id = await Scenarios.CheckIgnoredMailDeliveredAsync(db, Sender, actor);
                return new { Message = id, SenderInfo = 300, ReceiverInfo = 250, AttachedPills = 1 };
            default:
                throw new ArgumentException("Use step retry or verify.");
        }
    }
}
