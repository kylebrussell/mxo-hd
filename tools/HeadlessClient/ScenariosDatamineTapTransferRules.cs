using hds;

namespace HeadlessClient;

public static partial class Scenarios
{
    private static async Task DatamineTapTransferRules(ScenarioContext context)
    {
        var players = await context.AddPlayersInWorldAsync(
            new CharacterSeed("TapRecipient") { X = 17000, Y = 495, Z = 6520, Info = 50 },
            new CharacterSeed("TapHolder")
            {
                X = 17000, Y = 495, Z = 6400, Level = 50, Info = 500,
                Items = new[] { (3, 7332u, 1), (4, 7333u, 1), (5, 47276u, 1) }
            });
        await ProveDatamineTapTransferRulesAsync(players[1], players[0], context.Database);
    }

    public static async Task ProveDatamineTapTransferRulesAsync(HeadlessPlayer holder,
        HeadlessPlayer recipient, ThrowawayDatabase database)
    {
        var view = await holder.WaitForViewNamedAsync(recipient.Handle);
        string start = $"81 82 {Le(view.ViewId)} {view.SpawnId:x2} 00 00 00 00 00 00";
        holder.SendRpc($"{start} 05 00");
        await holder.WaitForRpcAsync("81 83 ?? ?? 04 00 00 18");
        Check.Equal(0, recipient.Received("81 84").Count, "A tap does not open trade on the receiving side");

        holder.SendRpc($"{start} 03 00");
        await holder.WaitForRpcAsync("81 83 ?? ?? 00 00 00 00");
        var offer = await recipient.WaitForRpcAsync("81 84");
        Check.Equal(7332u, BitConverter.ToUInt32(offer.Bytes, 9), "The regular Code Bit opens trade");
        holder.SendRpc("81 85 00 00 00 00 00 00 05 00");
        await holder.WaitForTextAsync("This item cannot be traded.");
        Check.Equal(0, holder.Received("81 86 01 01 00 00 00 00 00 05").Count, "The tap does not enter the offer");
        Check.Equal(0, recipient.Received("81 86 01 00 00 00 00 00 00 6b ac b8 00 00").Count,
            "The partner never sees the tap in the offer");

        const string accept = "81 85 04 00 00 00 00 00 6b 00";
        holder.SendRpc(accept);
        recipient.SendRpc(accept);
        await holder.WaitForRpcAsync("81 87 19 00 00 8e 01 00 00 00");
        await recipient.WaitForRpcAsync("81 87 19 00 00 8e 00 00 00 00 01 00 00 00");
        Check.Equal(1L, await CountItem(recipient, 7332), "Only the admitted Code Bit trades");
        Check.Equal(1L, await CountItem(holder, 47276), "The holder keeps the tap after trade");

        const string mixed = """
            <MXO_Mail_OOB><Info Slot="200"/><ItemList><Item FromPI="True" Slot="4"/><Item FromPI="True" Slot="5"/></ItemList></MXO_Mail_OOB>
            """;
        SendMailWithAttachments(holder, recipient.Handle, "Tap refusal", "A mixed draft.", mixed);
        await holder.WaitForTextAsync("This item cannot be attached to mail.");
        await holder.WaitForRpcAsync("81 e6 00 00 00 00 68 00 00 0e");
        Check.Equal(0L, await database.LongAsync($"SELECT COUNT(*) FROM mail WHERE ownerCharId={recipient.CharId} AND subject='Tap refusal'"),
            "The mixed draft creates no recipient message");
        Check.Equal(1L, await CountItem(holder, 7333), "Mail refusal preserves the tradeable attachment too");
        Check.Equal(1L, await CountItem(holder, 47276), "Mail refusal preserves the tap");
        Check.Equal(500L, await Cash(holder), "Mail refusal costs no Info");
        Check.Equal(50L, await Cash(recipient), "Mail refusal transfers no Info");

        const string allowed = """
            <MXO_Mail_OOB><Info Slot="200"/><ItemList><Item FromPI="True" Slot="4"/></ItemList></MXO_Mail_OOB>
            """;
        SendMailWithAttachments(holder, recipient.Handle, "Bit control", "One Code Bit.", allowed);
        var sent = await holder.WaitForRpcAsync("81 e6 ?? ?? ?? ?? 84 00 00 8e");
        uint message = BitConverter.ToUInt32(sent.Bytes, 2);
        await recipient.WaitForRpcAsync($"81 ed 19 00 ?? ?? ?? ?? {Le(message)}");
        Check.Equal("7333:0:1", await database.ScalarAsync($"SELECT items FROM mail WHERE ownerCharId={recipient.CharId} AND id={message}"),
            "A later valid draft mails only the permitted item");
        Check.Equal(300L, await Cash(holder), "Successful draft debits Info once");
        Check.Equal(250L, await Cash(recipient), "Successful draft credits Info once");
        Check.Equal(0L, await CountItem(recipient, 47276), "The recipient obtains no tap");
        Check.Equal(1L, await CountItem(holder, 47276), "The tap stays with its carrier");
        Check.Equal(0, holder.World.Problems.Count + recipient.World.Problems.Count, "Trade and mail packets decode");

        Task<long> CountItem(HeadlessPlayer player, uint goid) =>
            database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={player.CharId} AND goid={goid}");
        Task<long> Cash(HeadlessPlayer player) =>
            database.LongAsync($"SELECT cash FROM characters WHERE charId={player.CharId}");
    }
}
