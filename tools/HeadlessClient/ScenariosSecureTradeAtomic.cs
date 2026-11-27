namespace HeadlessClient;

public static partial class Scenarios
{
    public const string TradeRollbackFixtureSql =
        "ALTER TABLE characters ADD CONSTRAINT qa_trade_second_wallet CHECK (handle <> 'QAFriend' OR cash <> 300);";

    private static async Task SecureTradeAtomic(ScenarioContext context)
    {
        var players = await context.AddPlayersInWorldAsync(
            new CharacterSeed("TradeOwner") { X = 17000, Y = 495, Z = 6400, Info = 1000, Items = new[] { (3, 7332u, 1) } },
            new CharacterSeed("QAFriend") { X = 17000, Y = 495, Z = 6520, Info = 500, Items = new[] { (4, 7333u, 2) } });
        await context.Database.ExecuteAsync(TradeRollbackFixtureSql);
        await ProveSecureTradeAtomicAsync(players[0], players[1], context.Database);
    }

    public static async Task ProveSecureTradeAtomicAsync(HeadlessPlayer owner, HeadlessPlayer partner, ThrowawayDatabase db)
    {
        var target = await owner.WaitForViewNamedAsync(partner.Handle);
        string open = $"81 82 {Le(target.ViewId)} {target.SpawnId:x2} 00 00 00 00 00 00 03 00";
        const string addOneBit = "81 85 00 00 00 00 00 00 04 01";
        const string accept = "81 85 04 00 00 00 00 00 6b 00";
        const string complete = "81 87 19 00 00 8e";
        const string abort = "81 87 23 00 00 0e";

        await OpenAndOffer(200);
        int ownerAt = owner.World.RpcSnapshot().Count, partnerAt = partner.World.RpcSnapshot().Count;
        BothAccept();
        await owner.WaitForTextAsync("Your items and information were kept.");
        await partner.WaitForTextAsync("Your items and information were kept.");
        await owner.WaitForRpcAsync(abort);
        await partner.WaitForRpcAsync(abort);
        Check.That(owner.World.RpcSnapshot().Skip(ownerAt).Concat(partner.World.RpcSnapshot().Skip(partnerAt))
            .All(record => record.Bytes[0] != 0x5e && record.Bytes[0] != 0x5f && record.Bytes[0] != 0x69),
            "A failed final wallet write publishes no inventory changes");
        Check.Equal(0, owner.Received(complete).Count + partner.Received(complete).Count, "Failure publishes no completion");
        Check.Equal(0, owner.Received($"80 e4 {Le(1200u)}").Count + partner.Received($"80 e4 {Le(300u)}").Count,
            "Failure publishes no tentative balance");
        await OriginalBalancesAndBags();

        // Replace the row while preserving the item shown in the offer.
        await OpenAndOffer(250);
        await db.ExecuteAsync($"DELETE FROM inventory WHERE charId={owner.CharId} AND slot=3;" +
            $"INSERT INTO inventory(charId,goid,slot,count,purity) VALUES({owner.CharId},7332,3,1,0)");
        BothAccept();
        await owner.WaitForTextAsync("An offered item changed.");
        await owner.WaitForRpcAsync(abort);
        await partner.WaitForRpcAsync(abort);
        await OriginalBalancesAndBags();

        await OpenAndOffer(250);
        BothAccept();
        var paid = await owner.WaitForRpcAsync(complete);
        var charged = await partner.WaitForRpcAsync(complete);
        Check.Equal(250L, BitConverter.ToInt64(paid.Bytes, 14), "Owner completion summary");
        Check.Equal(-250L, BitConverter.ToInt64(charged.Bytes, 14), "Partner completion summary");
        Check.Equal(1, owner.Received("5f 03 00 01").Count, "Owner item deletion published once");
        Check.Equal(1, owner.Received($"5e ?? ?? {Le(7333u)}").Count, "Owner receives one of the partner's two Bits");
        Check.Equal(1, partner.Received($"5e ?? ?? {Le(7332u)}").Count, "Partner receives the owner's Bit");
        Check.Equal(1, partner.Received("69 01 04 00 00 00 00 00 00 00").Count, "Partner's stack goes from two to one");
        Check.Equal(1, owner.Received($"80 e4 {Le(1250u)}").Count, "Owner receives committed Info");
        Check.Equal(1, partner.Received($"80 e4 {Le(250u)}").Count, "Partner pays committed Info");
        Check.Equal(0L, await Count(owner, 7332), "Owner's outgoing Bit is absent");
        Check.Equal(1L, await Count(owner, 7333), "Owner received exactly one Bit");
        Check.Equal(1L, await Count(partner, 7332), "Partner received exactly one Bit");
        Check.Equal(1L, await Count(partner, 7333), "Partner kept the other Bit");
        Check.Equal(1250L, await Cash(owner), "Owner's durable balance");
        Check.Equal(250L, await Cash(partner), "Partner's durable balance");

        BothAccept();
        // A reliable reply to a fresh request acts as a barrier after the duplicate accepts.
        owner.SendRpc("81 82 ff 7f 01 00 00 00 00 00 00 03 00");
        await owner.WaitForRpcAsync("81 83 ?? ?? 05 00 00 18");
        Check.Equal(1, owner.Received(complete).Count, "Repeating accept cannot settle the closed offer");
        Check.Equal(0, owner.World.Problems.Count + partner.World.Problems.Count, "Trade records decode");

        async Task OpenAndOffer(uint info)
        {
            owner.SkipReceived();
            partner.SkipReceived();
            owner.SendRpc(open);
            await owner.WaitForRpcAsync("81 83 ?? ?? 00 00 00 00");
            await partner.WaitForRpcAsync("81 84");
            partner.SendRpc(addOneBit);
            await owner.WaitForRpcAsync("81 86 01 00 00 00 00 00 00 6b " + Le(7333u));
            partner.SendRpc($"81 85 00 01 {Le(info)} 6b 00");
            await owner.WaitForRpcAsync($"81 86 01 00 01 {Le(info)} 6b");
        }

        void BothAccept()
        {
            owner.SendRpc(accept);
            partner.SendRpc(accept);
        }

        async Task OriginalBalancesAndBags()
        {
            Check.Equal(1L, await Count(owner, 7332), "Owner's offered Bit stays in the bag");
            Check.Equal(0L, await Count(owner, 7333), "Partner's Bit is absent from owner's bag");
            Check.Equal(0L, await Count(partner, 7332), "Owner's Bit is absent from partner's bag");
            Check.Equal(2L, await Count(partner, 7333), "Partner's offered stack is untouched");
            Check.Equal(1000L, await Cash(owner), "Owner's Info stays put");
            Check.Equal(500L, await Cash(partner), "Partner's Info stays put");
        }

        Task<long> Cash(HeadlessPlayer player) =>
            db.LongAsync($"SELECT cash FROM characters WHERE charId={player.CharId}");
        Task<long> Count(HeadlessPlayer player, uint goid) =>
            db.LongAsync($"SELECT COALESCE(SUM(GREATEST(count,1)),0) FROM inventory WHERE charId={player.CharId} AND goid={goid}");
    }
}
