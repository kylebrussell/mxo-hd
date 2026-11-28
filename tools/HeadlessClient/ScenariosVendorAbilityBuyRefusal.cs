using hds;

namespace HeadlessClient;

public static partial class Scenarios
{
    public const uint CalmMindCode = 0x80001400;

    private static async Task VendorAbilityBuyRefusal(ScenarioContext context)
    {
        var buyer = (await context.AddPlayersInWorldAsync(new CharacterSeed("AbilityBuyer")
        {
            X = 17000, Y = 495, Z = 2300, Info = 40000, Level = 50,
            Items = Enumerable.Range(1, 96).Select(slot => (slot, 11666u, 1)).ToArray()
        }))[0];
        await ProveVendorAbilityBuyRefusalAsync(buyer, context.Database);
    }

    public static async Task ProveVendorAbilityBuyRefusalAsync(HeadlessPlayer buyer, ThrowawayDatabase db, bool useQaOpener = false)
    {
        uint id = buyer.CharId;
        Task<long> Cash() => db.LongAsync($"SELECT cash FROM characters WHERE charId={id}");
        Task<long> Codes() => db.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={id} AND goid={CalmMindCode}");
        const string buy = "81 0e 00 14 00 80 00";
        const string refused = "81 10 00 00 00 00 00";

        async Task Open()
        {
            if (useQaOpener)
                buyer.SendChat("?abvendor");
            else
                buyer.SendRpc($"80 c8 {Le(AbilityAcquisitionQa.Vendor)} 02 00");
            var stock = await buyer.WaitForRpcAsync("81 0d");
            Check.That(Enumerable.Range(0, BitConverter.ToUInt16(stock.Bytes, 32))
                .Any(i => BitConverter.ToUInt32(stock.Bytes, 34 + i * 4) == CalmMindCode),
                "The ordinary ability vendor stocks Calm Mind Calm Body");
        }

        await Open();
        int before = buyer.World.RpcSnapshot().Count;
        buyer.SendRpc(buy);
        await buyer.WaitForTextAsync("Your inventory is full.");
        await buyer.WaitForRpcAsync(refused);
        Check.Equal(40000L, await Cash(), "Full bag keeps the wallet");
        Check.Equal(0L, await Codes(), "Full bag grants no ability code");
        Check.That(!buyer.World.RpcSnapshot().Skip(before).Any(r => r.Bytes is
            [0x5e, ..] or [0x80, 0xe4 or 0xe7, ..] or [0x81, 0x0f, ..]),
            "Full bag publishes only the purchase refusal");

        buyer.SendRpc("81 10 00 00 00 00 01");
        buyer.SendRpc("5d 0b 00 01 01");
        await buyer.WaitForRpcAsync("5f 0b 00 01");
        Check.Equal(40340L, await Cash(), "Recycling a shirt frees slot 11 and gives 340 Info");
        await Open();
        buyer.SendRpc(buy);
        await buyer.WaitForRpcAsync("5e 0b 00 2e 00 00 00 01 14 00 80 01");
        await buyer.WaitForRpcAsync("80 e7 c0 63 ff ff ff ff ff ff 08 00 00 00");
        await buyer.WaitForRpcAsync("80 e4 54 01 00 00 08 00 00 00");
        await buyer.WaitForRpcAsync("81 0f 2e 00 00 00 01 14 00 80 40 9c 00 00 00");
        Check.Equal(340L, await Cash(), "Retry charges the stock 40,000 Info once");
        Check.Equal(1L, await Codes(), "Retry commits exactly one ability code");
        Check.Equal(11L, await db.LongAsync($"SELECT slot FROM inventory WHERE charId={id} AND goid={CalmMindCode}"),
            "Retry fills the freed bag slot");
    }
}
