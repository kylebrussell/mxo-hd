using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class MailRecipientWorldMode : NativeQaMode
{
    private const int X = 17000, Y = 495, Z = 2300, FriendX = 17120;
    private const uint Sender = 110;

    public override string Name => "mail-recipient-world";
    public override NativeQaModePlan Plan { get; } = new(new ushort[] { 2 }, X, Z,
        Array.Empty<string>(), new[] { $"""
            UPDATE characters SET y={Y},cash=500 WHERE charId={Sender};
            DELETE FROM inventory WHERE charId={Sender} AND slot BETWEEN 0 AND 96;
            INSERT INTO inventory(charId,goid,slot,count,purity) VALUES ({Sender},2399,3,1,0);
            """ }, new[] { $"""
            UPDATE characters SET x={FriendX},y={Y},z={Z},cash=50 WHERE handle='QAFriend';
            DELETE FROM mail WHERE ownerCharId={Sender} OR ownerCharId IN (SELECT charId FROM characters WHERE handle='QAFriend');
            """ },
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        readyMessages: new[] {
            "Mara Central NAV CSR (17000,495,2300), QAFriend (17120,495,2300). No teleport, world targeting or dev commands. QAFriend is the local same-name control.",
            $"Phone > Email > New. To {Scenarios.MailForeignWorld}.QAFriend, Subject {Scenarios.MailRecipientSubject}, body Foreign check, no attachments. Send. Expect 81 e6 00 00 00 00 41 00 00 0e, refusal in chat and a System Message Undeliverable. Dismiss any Mail popup.",
            """Send {"actor":"QAFriend","action":"mail-address-refused"}. Expect "Local inbox empty; pill and both wallets kept".""",
            """
            Reopen Compose if needed. To QAFriend, subject Address Check, body Local check, no attachments. Expect 81 e6 success. Send {"actor":"QAFriend","action":"mail-address-delivered"} and expect "Local inbox received one message; pill and both wallets kept".
            """
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
        string world = await Scenarios.MailFixtureWorldAsync(db);
        Console.WriteLine($"Local mail world is {world}, foreign compose address is {Scenarios.MailForeignWorld}.QAFriend.");
        await Scenarios.CheckMailRecipientDeliveryAsync(db, Sender, recipient.CharId, delivered: false);

        if (context.StockProbe is not { } sender) return Array.Empty<byte>();
        await sender.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, sender);
        await Scenarios.ProveMailRecipientWorldAsync(sender, recipient, db);
        return Array.Empty<byte>();
    }

    internal static async Task<string> CheckDeliveryAsync(HeadlessPlayer actor, ThrowawayDatabase db, bool delivered)
    {
        if (actor.Handle != "QAFriend") throw new ArgumentException("Use QAFriend for the mail address check.");
        await Scenarios.CheckMailRecipientDeliveryAsync(db, Sender, actor.CharId, delivered);
        return delivered ? "Local inbox received one message; pill and both wallets kept" :
            "Local inbox empty; pill and both wallets kept";
    }
}
