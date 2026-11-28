namespace HeadlessClient;

public static partial class Scenarios
{
    public const string MailRecipientSubject = "Address Check";
    public const string MailForeignWorld = "Vector";

    private static async Task EmailValidatesRecipientWorld(ScenarioContext context)
    {
        var players = await context.AddPlayersInWorldAsync(
            new CharacterSeed("MailSender") { Info = 500, Items = new[] { (3, 2399u, 1) } },
            new CharacterSeed("QAFriend") { X = 17120, Info = 50 });
        await ProveMailRecipientWorldAsync(players[0], players[1], context.Database);
    }

    public static async Task ProveMailRecipientWorldAsync(HeadlessPlayer sender, HeadlessPlayer recipient, ThrowawayDatabase db)
    {
        string world = await MailFixtureWorldAsync(db);
        string local = $"soe+mxo+{world.ToUpperInvariant()}+{recipient.Handle}";
        string foreign = $"SOE+MXO+{MailForeignWorld}+{recipient.Handle}";
        const string body = "For the addressed recipient only.";
        sender.SkipReceived();
        recipient.SkipReceived();

        async Task Refused()
        {
            await sender.WaitForTextAsync("That address is invalid or on another world.");
            await sender.WaitForRpcAsync("81 e6 00 00 00 00 41 00 00 0e");
            var bounce = await sender.WaitForRpcAsync("81 ed 19 00");
            Check.Equal("Message Undeliverable", StringAtOffset(bounce.Bytes, 0x16), "Foreign address bounce");
            Check.Equal(0, recipient.Received("81 ed").Count, "The local namesake gets no notification");
            await CheckMailRecipientDeliveryAsync(db, sender.CharId, recipient.CharId, delivered: false);
        }

        // The stock compose path supplies the qualified recipient in out-of-band XML.
        SendMailWithAttachments(sender, foreign, MailRecipientSubject, body, $"""
            <MXO_Mail_OOB><RecipientList><Recipient Name="{foreign}"/></RecipientList><Info Slot="200"/><ItemList><Item FromPI="True" Slot="3"/></ItemList></MXO_Mail_OOB>
            """);
        await Refused();
        Check.Equal(0, sender.Received("5f 03 00").Count, "A foreign address does not remove the bag item");

        // A local recipient first in the list must not receive part of a refused group send.
        sender.SendRpc(MailSend($"{local},{foreign}", MailRecipientSubject, body, false));
        await Refused();

        // Other service/game prefixes and malformed routes must not fall back to the local handle.
        foreach (string invalid in new[] { $"Other+MXO+{world}+{recipient.Handle}", $"SOE+Other+{world}+{recipient.Handle}",
                     $"SOE+MXO+{world}+Extra+{recipient.Handle}" })
        {
            sender.SendRpc(MailSend(invalid, MailRecipientSubject, body, false));
            await Refused();
        }

        SendMailWithAttachments(sender, local, MailRecipientSubject, body, $"""
            <MXO_Mail_OOB><RecipientList><Recipient Name="{local}"/><Recipient Name="{recipient.Handle}"/></RecipientList></MXO_Mail_OOB>
            """);
        await sender.WaitForRpcAsync($"81 e6 ?? ?? ?? ?? {MailSuccess}");
        await recipient.WaitForRpcAsync("81 ed 19 00");
        await CheckMailRecipientDeliveryAsync(db, sender.CharId, recipient.CharId, delivered: true);
        Check.Equal(body, await db.ScalarAsync($"SELECT body FROM mail WHERE ownerCharId={recipient.CharId}"),
            "The resolved local recipient gets the original body once");
    }

    public static async Task<string> MailFixtureWorldAsync(ThrowawayDatabase db)
    {
        string world = await db.ScalarAsync("SELECT name FROM worlds ORDER BY worldId LIMIT 1");
        Check.That(!string.IsNullOrWhiteSpace(world) && !world.Equals(MailForeignWorld, StringComparison.OrdinalIgnoreCase),
            "The fixture's auth world is distinct from the foreign recipient's world");
        return world;
    }

    public static async Task CheckMailRecipientDeliveryAsync(ThrowawayDatabase db, uint sender, uint recipient, bool delivered)
    {
        Check.Equal(delivered ? 1L : 0L, await db.LongAsync($"SELECT COUNT(*) FROM mail WHERE ownerCharId={recipient}"),
            delivered ? "The local retry delivers one message" : "The local namesake has no mail");
        if (delivered)
            Check.Equal(MailRecipientSubject, await db.ScalarAsync($"SELECT subject FROM mail WHERE ownerCharId={recipient}"),
                "The local recipient gets the fixture message");
        Check.Equal(500L, await db.LongAsync($"SELECT cash FROM characters WHERE charId={sender}"), "Sender keeps 500 Info");
        Check.Equal(50L, await db.LongAsync($"SELECT cash FROM characters WHERE charId={recipient}"), "Recipient keeps 50 Info");
        Check.Equal(1L, await db.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={sender} AND slot=3 AND goid=2399 AND count=1"),
            "Sender keeps the Health Pill");
    }
}
