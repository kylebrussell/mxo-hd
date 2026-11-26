using System.Text;
using HeadlessClient;
using hds;

namespace NativeQa.Modes;

public sealed class MailAtomicTransfersMode : NativeQaMode
{
    private const string Incoming = "Bag Check";
    private const string Outgoing = "Gift";
    private const uint CodeBit = 7332, Pill = 2399;
    private uint incomingId;

    public override string Name => "mail-atomic-transfers";
    public override NativeQaModePlan Plan { get; } = new(new ushort[] { 2 }, 17000, 2300,
        Array.Empty<string>(), new[] { """
            UPDATE characters SET y=495,cash=500,rotation=128 WHERE charId=110;
            DELETE FROM inventory WHERE charId=110 AND slot<=96;
            DELETE FROM mail WHERE ownerCharId=110;
            INSERT INTO inventory(charId,goid,slot,count,purity)
            WITH RECURSIVE slots(slot) AS (SELECT 1 UNION ALL SELECT slot+1 FROM slots WHERE slot<96)
            SELECT 110,IF(slot=3,7332,11666),slot,1,0 FROM slots;
            """ }, new[] { """
            UPDATE characters SET x=17120,y=495,z=2300,cash=50 WHERE handle='QAFriend';
            DELETE FROM inventory WHERE charId=(SELECT charId FROM characters WHERE handle='QAFriend') AND slot<=96;
            DELETE FROM mail WHERE ownerCharId=(SELECT charId FROM characters WHERE handle='QAFriend');
            INSERT INTO inventory(charId,goid,slot,count,purity)
            SELECT charId,2399,5,1,0 FROM characters WHERE handle='QAFriend';
            """ },
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        readyMessages: new[] {
            "Mara Central (17000,495,2300), no teleport required. Use /mail and open QAFriend's Bag Check. Try to take its Health Pill while all 96 bag slots are filled. Expect the full-bag refusal and the pill still attached. If the stock UI blocks locally, record that and whether it sent 81 fc.",
            "Compose to QAFriend with subject Gift and a short body. Attach Code Bit 1 from bag slot 3 and 200 Info, then Send. Expect slot 3 empty and 300 Info.",
            """Before retrying Bag Check, submit {"actor":"QAFriend","action":"mail-verify"}. This confirms the first pill is still in CSR's message. QAFriend reads Gift, receives 250 Info and claims one Code Bit 1. The helper verifies both wallets and checks the database.""",
            "Reopen Bag Check and take the pill again. Expect 81 fd success and one Health Pill in the freed slot 3."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var db = context.OwnedDatabase ?? throw new InvalidOperationException("Mail QA requires a disposable database.");
        string data = Path.Combine(context.RepositoryRoot ?? throw new InvalidOperationException("Missing world data path."), "data");
        NavMesh.UseDirectory(Path.Combine(data, "navmesh"));
        Check.That(NavMesh.Locate(1, 17000, 495, 2300, out _) && NavMesh.Locate(1, 17120, 495, 2300, out _),
            "Both mail operators stand on Mara Central NAV ground");
        Check.Equal(96L, await db.LongAsync("SELECT COUNT(DISTINCT slot) FROM inventory WHERE charId=110 AND slot BETWEEN 1 AND 96"),
            "CSR has a full bag before the first claim");
        Check.Equal(1L, await db.LongAsync("SELECT COUNT(*) FROM inventory WHERE charId=110 AND slot=3 AND goid=7332"),
            "CSR has the outgoing Code Bit in slot 3");
        var helper = await HeadlessPlayer.LoginAsync(context.Address, "QAFriend", "QAFriend");
        context.Helpers.Add("QAFriend", helper);
        await helper.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, helper);
        Scenarios.SendMailWithAttachments(helper, "CSR_Tester", Incoming, "One Health Pill.",
            """<MXO_Mail_OOB><ItemList><Item FromPI="True" Slot="5"/></ItemList></MXO_Mail_OOB>""");
        var sent = await helper.WaitForRpcAsync("81 e6 ?? ?? ?? ?? 84 00 00 8e");
        incomingId = BitConverter.ToUInt32(sent.Bytes, 2);
        Check.Equal("2399:0:1", await db.ScalarAsync($"SELECT items FROM mail WHERE ownerCharId=110 AND id={incomingId}"),
            "Bag Check contains the incoming Health Pill");
        Console.WriteLine($"QAFriend seeded Bag Check id={incomingId} via 81 e5. CSR has 96 occupied bag slots.");
        return Array.Empty<byte>();
    }

    public async Task<object> VerifyAsync(HeadlessPlayer actor, ThrowawayDatabase db)
    {
        if (actor.Handle != "QAFriend") throw new ArgumentException("Use mail-verify with QAFriend.");
        Check.Equal("2399:0:1", await db.ScalarAsync($"SELECT items FROM mail WHERE ownerCharId=110 AND id={incomingId}"),
            "Full-bag refusal retained CSR's pill in the message");
        Check.Equal(0L, await db.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId=110 AND goid={Pill} AND slot BETWEEN 1 AND 96"),
            "Full-bag refusal granted no pill");
        uint id = (uint)await db.LongAsync($"SELECT id FROM mail WHERE ownerCharId={actor.CharId} AND subject='{Outgoing}'");
        Check.Equal("7332:0:1", await db.ScalarAsync($"SELECT items FROM mail WHERE id={id}"), "Gift contains one Code Bit");
        await actor.WaitForRpcAsync($"81 ed 19 00 ?? ?? ?? ?? {Le(id)}");
        await actor.WaitForRpcAsync($"80 e4 {Le(250)}");
        actor.SendRpc($"81 f0 {Le(id)}");
        var opened = await actor.WaitForRpcAsync($"81 f3 19 00 ?? ?? ?? ?? {Le(id)}");
        Check.That(Encoding.UTF8.GetString(opened.Bytes).Contains("<Item ClassData=\"7332\""), "QAFriend can read the attachment");
        actor.SendRpc($"81 fc {Le(id)} 08 00 01 00 00 {Le(CodeBit)} {Le(TradePackets.Descriptor(0, 1))}");
        await actor.WaitForRpcAsync("5e 01 00 a4 1c 00 00");
        await actor.WaitForRpcAsync("81 fd 84 00 00 8e");
        Check.Equal("", await db.ScalarAsync($"SELECT items FROM mail WHERE id={id}"), "The Gift attachment was claimed");
        Check.Equal(1L, await db.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={actor.CharId} AND goid={CodeBit} AND slot=1"),
            "QAFriend received the Code Bit once");
        Check.Equal(0L, await db.LongAsync("SELECT COUNT(*) FROM inventory WHERE charId=110 AND slot=3"), "CSR mailed the slotted Code Bit");
        Check.Equal(300L, await db.LongAsync("SELECT cash FROM characters WHERE charId=110"), "CSR sent 200 Info");
        Check.Equal(250L, await db.LongAsync($"SELECT cash FROM characters WHERE charId={actor.CharId}"), "QAFriend received 200 Info");
        return new { Gift = id, SenderInfo = 300, ReceiverInfo = 250, ClaimedCodeBits = 1, IncomingPillRetained = incomingId };
    }

    private static string Le(uint value) => WireFormat.ToHex(BitConverter.GetBytes(value));
}
