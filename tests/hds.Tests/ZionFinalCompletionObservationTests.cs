using HeadlessClient;
namespace hds.Tests;

public class ZionFinalCompletionObservationTests
{
    // Actual 101213 summary from failed ee073d08: Upload ACK and exactly one
    // earned receipt, NULL payload, no intermediate "Phase3 goals done" log.
    static byte[] Captured()=>Convert.FromHexString("80A2000900000065000000786301008E0C010000000000000007");
    [Theory]
    [InlineData("valid")][InlineData("earlier-phase")][InlineData("no-phases")][InlineData("negative-phase")]
    [InlineData("still-active")][InlineData("no-receipt")][InlineData("duplicate-receipt")]
    [InlineData("no-summary")][InlineData("duplicate-summary")][InlineData("truncated")]
    [InlineData("wrong-opcode")][InlineData("wrong-status")][InlineData("wrong-xp")][InlineData("wrong-info")]
    public void DirectFinalFinishRequiresCurrentExactReceiptStateAndTypedLiteralReward(string change)
    {
        int phase=3,count=4;string active="NULL";long receipts=1;uint xp=68750,info=91000;
        var summaries=new List<byte[]>{Captured()};
        switch(change)
        {
            case "earlier-phase":phase=2;break;case "no-phases":count=0;break;case "negative-phase":phase=-1;break;
            case "still-active":active="101213:3";break;case "no-receipt":receipts=0;break;case "duplicate-receipt":receipts=2;break;
            case "no-summary":summaries.Clear();break;case "duplicate-summary":summaries.Add(Captured());break;
            case "truncated":summaries[0]=summaries[0][..25];break;case "wrong-opcode":summaries[0][1]=0xa1;break;
            case "wrong-status":summaries[0][2]=1;break;case "wrong-xp":xp++;break;case "wrong-info":info++;break;
        }
        Assert.Equal(change=="valid",ZionFinalCompletionObservation.Matches(phase,count,active,receipts,summaries,xp,info));
    }
    [Fact]public void SummaryBeforeCurrentMissionCursorDoesNotQualifyEvenWithOneReceipt()
    {
        var old=new List<byte[]>{Captured()};
        var current=ZionAfter1212Fixture.CompletionSummaries(old,Array.Empty<byte[]>(),1);
        Assert.Empty(current);
        Assert.False(ZionFinalCompletionObservation.Matches(3,4,"NULL",1,current,68750,91000));
    }
}
