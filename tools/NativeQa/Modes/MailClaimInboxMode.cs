using HeadlessClient;
using hds;

namespace NativeQa.Modes;

public sealed class MailClaimInboxMode : NativeQaMode
{
    private const string Delivery = "Delivery Check", Other = "Read Check";
    private const string OtherBody = "This is the other message.";

    public override string Name => "mail-claim-inbox";
    public override NativeQaModePlan Plan { get; } = new(new ushort[] { 2 }, 17000, 2300,
        Array.Empty<string>(), new[] { """
            UPDATE characters SET y=495,rotation=128 WHERE charId=110;
            DELETE FROM inventory WHERE charId=110 AND slot BETWEEN 1 AND 96;
            DELETE FROM mail WHERE ownerCharId=110;
            INSERT INTO inventory(charId,goid,slot,count,purity)
            WITH RECURSIVE slots(slot) AS (SELECT 1 UNION ALL SELECT slot+1 FROM slots WHERE slot<96)
            SELECT 110,11666,slot,1,0 FROM slots WHERE slot<>3;
            """ }, new[] { """
            UPDATE characters SET x=17120,y=495,z=2300 WHERE handle='QAFriend';
            DELETE FROM inventory WHERE charId=(SELECT charId FROM characters WHERE handle='QAFriend') AND slot IN (5,6);
            INSERT INTO inventory(charId,goid,slot,count,purity)
            SELECT charId,2399,5,1,0 FROM characters WHERE handle='QAFriend'
            UNION ALL SELECT charId,2399,6,1,0 FROM characters WHERE handle='QAFriend';
            """ },
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        readyMessages: new[] {
            "Log in at Mara Central (17000,495,2300), no teleport needed. Phone > Email > Delivery Check. Confirm one Health Pill icon.",
            "Click Take All and dismiss any Mail popup with OK. Expect one pill in slot 3, the attachment icon gone without reopening, and Take All disabled for the emptied message.",
            "Select Read Check in the same inbox. Its body should say 'This is the other message.' Click Close and confirm the inbox goes away.",
            "Reopen Phone > Email > Read Check. The bag is now full. Click Take All and dismiss any Mail popup with OK. The pill should stay attached. Select Delivery Check, verify its grid is still empty, and click Close again.",
            "PacketLog: expect 5e + 81 fd 84 00 00 8e in one RPC list followed by 81 f3 for Delivery Check. The refused claim gets 81 fd ca 02 00 0c followed by 81 f3 for Read Check with its pill still present."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var db = context.OwnedDatabase ?? throw new InvalidOperationException("Mail QA requires a disposable database.");
        string data = Path.Combine(context.RepositoryRoot ?? throw new InvalidOperationException("Missing world data path."), "data");
        NavMesh.UseDirectory(Path.Combine(data, "navmesh"));
        Check.That(NavMesh.Locate(1, 17000, 495, 2300, out _) && NavMesh.Locate(1, 17120, 495, 2300, out _),
            "Both mail operators stand on Mara Central NAV ground");
        Check.Equal(95L, await db.LongAsync("SELECT COUNT(DISTINCT slot) FROM inventory WHERE charId=110 AND slot BETWEEN 1 AND 96"),
            "CSR has exactly one free bag slot");
        Check.Equal(0L, await db.LongAsync("SELECT COUNT(*) FROM inventory WHERE charId=110 AND slot=3"), "Slot 3 is the free slot");

        var helper = await HeadlessPlayer.LoginAsync(context.Address, "QAFriend", "QAFriend");
        context.Helpers.Add("QAFriend", helper);
        await helper.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, helper);
        foreach (var (subject, body, slot) in new[] { (Delivery, "One Health Pill.", 5), (Other, OtherBody, 6) })
        {
            string xml = $"""<MXO_Mail_OOB><ItemList><Item FromPI="True" Slot="{slot}"/></ItemList></MXO_Mail_OOB>""";
            Scenarios.SendMailWithAttachments(helper, "CSR_Tester", subject, body, xml);
            var sent = await helper.WaitForRpcAsync("81 e6 ?? ?? ?? ?? 84 00 00 8e");
            uint id = BitConverter.ToUInt32(sent.Bytes, 2);
            Check.Equal("2399:0:1", await db.ScalarAsync($"SELECT items FROM mail WHERE ownerCharId=110 AND id={id}"),
                $"{subject} contains one Health Pill");
            Console.WriteLine($"QAFriend seeded {subject} id={id} via 81 e5.");
        }
        return Array.Empty<byte>();
    }
}
