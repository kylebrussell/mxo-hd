using HeadlessClient;

namespace hds.Tests;

public class MervZeroCashLootLedgerTests
{
    // Exact session1 local141..147 / absolute323..329 from the retained
    // 9dbc run5c6f6adf55 (raw SHA8FB4B3C5192C67FBC6C007339CB09773058868EA5AAAACD9BB191AF1955542CA).
    // The equal balance at absolute308 was the previous +13000 claim; the
    // distinct current claim at324 advertises cash0 and accepts item6782.
    static readonly string[] RawClaim =
    {
        "811900000000100000000000000000000100000000087E1A0000",
        "80E4BC28020000000000",
        "5E04007E1A00000030008201",
        "80A0040600012C004765742074686520666F75727468204461746120467261676D656E742066726F6D20746865204167656E7400",
        "80A1C00C0028310000B4140000000000170001000100006C00457863656C6C656E742C2057617272696F722C2074686174206163636F756E747320666F7220616C6C20666F75722046726167732E204E6F77206C6561766520746865206275696C64696E6720616E642075706C6F6164207468656D206174206120686172646C696E652E00",
        "80A1700A00281400002614000C00002617000101010000010000",
        "81200C0000000000000000000100000000087E1A0000"
    };
    static byte[] Wallet(byte op,uint amount,uint reason)=>new byte[]{0x80,op}.Concat(BitConverter.GetBytes(amount)).Concat(BitConverter.GetBytes(reason)).ToArray();
    static (List<byte[]> Records,MervLedgerOperation Operation) Fixture()
    {
        var records=new List<byte[]>{Wallet(0xe5,1000,0),Wallet(0xe4,141500,0)};
        records.AddRange(RawClaim.Select(Convert.FromHexString));
        var summary=new byte[26];summary[0]=0x80;summary[1]=0xa2;
        BitConverter.GetBytes(50u).CopyTo(summary,11);BitConverter.GetBytes(100u).CopyTo(summary,15);
        records.AddRange(new[]{summary,Wallet(0xe6,100,2),Wallet(0xe5,1100,2),Wallet(0xe4,141550,2)});
        return(records,new("literal claim323..329","Loot",2,9));
    }
    static MervRewardLedgerResult Reconcile(List<byte[]> records,MervLedgerOperation operation)=>
        MervRewardLedger.Reconcile(records,new[]{operation},1000,141500,new uint[]{100},new uint[]{50});

    [Fact]public void ActualZeroCashClaimIsAnAbsoluteBalanceRefreshAndAddsNoCredit()
    {
        var (records,operation)=Fixture();var result=Reconcile(records,operation);
        Assert.Equal(0,result.LootInformation);Assert.Equal(50,result.MissionInformation);Assert.Equal(141550u,result.Information);
        var refresh=Assert.Single(result.Events.Where(e=>e.Kind=="NPC zero-cash balance refresh"));
        Assert.Equal(0,refresh.Amount);Assert.Equal(141500u,refresh.Total);Assert.Equal(3,refresh.RpcIndex);
        Assert.Equal(2,refresh.NpcWindowIndex);Assert.Equal(8,refresh.AcceptanceIndex);
        Assert.Equal("80E4BC28020000000000",refresh.Raw);
    }
    [Theory]
    [InlineData("missing-window")][InlineData("malformed-window")][InlineData("stale-window")]
    [InlineData("positive-window")][InlineData("missing-acceptance")][InlineData("wrong-item")]
    [InlineData("empty-acceptance")][InlineData("repeated-acceptance")][InlineData("repeated-refresh")]
    [InlineData("spend")][InlineData("unadvertised-increase")][InlineData("wrong-reason")]
    [InlineData("zero-mission-credit")]
    public void ZeroCashDoesNotRelaxCurrentWindowAcceptanceOrEconomicAccounting(string mutation)
    {
        var (records,operation)=Fixture();
        switch(mutation)
        {
            case "missing-window":records[2]=new byte[]{0};break;
            case "malformed-window":records[2][12]=1;break;
            case "stale-window":operation=operation with{RpcStart=3};break;
            case "positive-window":records[2][8]=1;break;
            case "missing-acceptance":records[8]=new byte[]{0};break;
            case "wrong-item":records[8][18]^=1;break;
            case "empty-acceptance":records[8]=Convert.FromHexString("81200C0000000000000000000000");break;
            case "repeated-acceptance":records[7]=records[8].ToArray();break;
            case "repeated-refresh":records[4]=records[3].ToArray();break;
            case "spend":records[3]=Wallet(0xe4,141499,0);break;
            case "unadvertised-increase":records[3]=Wallet(0xe4,141501,0);break;
            case "wrong-reason":records[3]=Wallet(0xe4,141500,1);break;
            case "zero-mission-credit":records[^1]=Wallet(0xe4,141500,2);break;
            default:throw new ArgumentOutOfRangeException(nameof(mutation));
        }
        Assert.Throws<InvalidDataException>(()=>Reconcile(records,operation));
    }
}
