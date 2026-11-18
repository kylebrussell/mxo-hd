using System.Text.Json;
using HeadlessClient;
using hds;

namespace NativeQa.Modes;

public sealed class SecureTradeMode : NativeQaMode
{
    public const string Partner = "QAFriend";
    public override string Name => "secure-trade";

    public override NativeQaModePlan Plan { get; } = new(new ushort[] { 2 }, 17000, 2300,
        Array.Empty<string>(), new[] { """
            DELETE FROM inventory WHERE charId=110 AND slot BETWEEN 1 AND 96;
            INSERT INTO inventory (charId,goid,slot,count,purity) VALUES (110,7332,3,1,0),(110,7332,4,1,0);
            UPDATE characters SET y=495,cash=1000 WHERE charId=110;
            """ },
        new[] { """
            UPDATE characters SET x=17120,y=495,z=2300,cash=500 WHERE handle='QAFriend';
            INSERT INTO inventory (charId,goid,slot,count,purity)
                SELECT charId,7333,3,1,0 FROM characters WHERE handle='QAFriend';
            """ },
        readyMessages: new[] { "Secure Trade QA: QAFriend stands nearby with one Code Bit 2 and 500 information. "
            + "CSR_Tester starts with two Code Bit 1 slots and 1,000 information. "
            + "Use the real bag and trade window. Partner actions and expected balances are in docs/GAMEPLAY-BREADTH-SESSIONS.md." });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var database = context.OwnedDatabase ?? throw new InvalidOperationException("Secure Trade requires a disposable database.");
        var partner = await HeadlessPlayer.LoginAsync(context.Address, Partner, Partner);
        context.Helpers.Add(Partner, partner);
        await partner.EnterWorldAsync();
        Check.That(partner.World.CurrentOwnViewLife().OwnViewReady && partner.World.Problems.Count == 0,
            "Trade partner entered the world");
        Check.Equal(1L, await database.LongAsync(
            $"SELECT COUNT(*) FROM characters WHERE charId={partner.CharId} AND is_online=1 AND healthC>0 AND cash=500"),
            "Trade partner is alive and has seeded information");
        Check.Equal(1L, await database.LongAsync(
            $"SELECT COUNT(*) FROM inventory WHERE charId={partner.CharId} AND slot=3 AND goid=7333 AND count=1"),
            "Trade partner has the item for the incoming offer");
        Check.Equal(2L, await database.LongAsync(
            "SELECT COUNT(*) FROM inventory WHERE charId=110 AND slot IN (3,4) AND goid=7332 AND count=1"),
            "CSR_Tester has the items for outgoing offers");
        return Array.Empty<byte>();
    }

    public static async Task SendPartnerActionAsync(HeadlessPlayer partner, JsonElement request)
    {
        if (partner.Handle != Partner) throw new ArgumentException("Only QAFriend can drive the trade fixture.");
        string? action = request.GetProperty("step").GetString();
        if (action == "open")
        {
            var target = await partner.WaitForViewNamedAsync("CSR_Tester");
            partner.SendRpc(BuildPartnerOffer(request, target.ViewId, target.SpawnId));
            var reply = await partner.WaitForRpcAsync("81 83");
            uint status = BitConverter.ToUInt32(reply.Bytes, 4);
            if (status != TradePackets.RequestOk)
                throw new InvalidOperationException($"Trade open was refused (status 0x{status:x8}). Check the selected bag slot and trade state.");
            return;
        }

        byte code = action switch
        {
            "add-info" => TradeRequests.ActionAdd,
            "accept" => TradeRequests.ActionAccept,
            "reset" => TradeRequests.ActionResetAccept,
            "cancel" => TradeRequests.ActionCancel,
            _ => throw new ArgumentException("Trade helper step must be open, add-info, accept, reset or cancel.")
        };
        uint information = action == "add-info" ? request.GetProperty("information").GetUInt32() : 0;
        if (action == "add-info" && information is < 1 or > 500)
            throw new ArgumentException("Trade helper can offer between 1 and 500 information.");
        partner.SendRpc($"81 85 {code:x2} {(information == 0 ? 0 : 1):x2} {WireFormat.ToHex(BitConverter.GetBytes(information))} 6b 00");
    }

    public static string BuildPartnerOffer(JsonElement request, ushort viewId, byte spawnId)
    {
        byte slot = 3;
        if (request.TryGetProperty("slot", out var selection) &&
            (selection.ValueKind != JsonValueKind.Number || !selection.TryGetByte(out slot) || slot is < 1 or > 96))
            throw new ArgumentException("Trade open requires a bag slot from 1 to 96.");

        return $"81 82 {WireFormat.ToHex(BitConverter.GetBytes(viewId))} {spawnId:x2} 00 00 00 00 00 00 {slot:x2} 00";
    }
}
