using HeadlessClient;
namespace hds.Tests;

public class PrimaRewardLedgerTests
{
    // Independent literal records, not server packet builders. Summary precedes
    // mission gain/total/current cash, as observed in the retained live proof.
    static byte[][] Records()=>new[]{
        "80E5E803000000000000","80E46400000000000000",
        "80E61900000001000000","80E50104000001000000",
        "81190000000010001E00000000000000010000000008AF1A0000","80E48200000000000000","81200C0000000000000000000000",
        "80A2000100000000000000320000006400000000000000000007",
        "80E66400000002000000","80E56504000002000000","80E4B400000002000000"}.Select(Convert.FromHexString).ToArray();
    static PrimaLedgerOperation[] Operations()=>new[]{new PrimaLedgerOperation("fight","Defeat",2,4),new("take","Loot",4,7),new("finish","Upload",7,11)};
    static PrimaRewardLedgerResult Parse(byte[][]? records=null,PrimaLedgerOperation[]? operations=null)=>
        PrimaRewardLedger.Reconcile(records??Records(),operations??Operations(),1000,100,new uint[]{100},new uint[]{50});

    [Fact] public void LiteralLedgerKeepsMissionCombatAndLootSeparateAndReconcilesTotals()
    {
        var r=Parse();Assert.Equal((uint)1125,r.Experience);Assert.Equal((uint)180,r.Information);
        Assert.Equal(25,r.FightingExperience);Assert.Equal(100,r.MissionExperience);Assert.Equal(50,r.MissionInformation);Assert.Equal(30,r.LootInformation);
        var loot=Assert.Single(r.Events.Where(e=>e.Kind=="NPC loot cash"));Assert.Equal(5,loot.RpcIndex);Assert.Equal("take",loot.Operation);
        Assert.Equal(4,loot.NpcWindowIndex);Assert.Equal(6,loot.AcceptanceIndex);
        Assert.Equal(Records()[5],Convert.FromHexString(loot.Raw));Assert.Equal((1000u,100u),PrimaRewardLedger.ReadBaseline(Records()));
    }
    [Theory]
    [InlineData(2,"80E619000000010000")] // truncated
    [InlineData(2,"80E60000000001000000")] // zero gain
    [InlineData(2,"80E61900000003000000")] // unknown gain type
    [InlineData(2,"8118")] // missing gain
    [InlineData(3,"8118")] // missing matching total
    [InlineData(3,"80E61900000001000000")] // doubled gain while pending
    [InlineData(4,"80E50104000001000000")] // doubled total without gain
    [InlineData(3,"80E50204000001000000")] // arithmetic mismatch
    [InlineData(3,"80E50104000002000000")] // mismatched type
    [InlineData(4,"8119")] // malformed current NPC window
    [InlineData(4,"81190000000010001D00000000000000010000000008AF1A0000")] // window amount != credit
    [InlineData(5,"80E45A00000000000000")] // unexplained spend
    [InlineData(5,"80E48200000007000000")] // unexpected reason
    [InlineData(6,"8118")] // missing current acceptance
    [InlineData(7,"80A2000100000000000000330000006400000000000000000007")] // wrong literal summary information
    [InlineData(7,"80A2080100000000000000320000006400000000000000000007")] // failed status
    [InlineData(8,"80E66500000002000000")] // wrong literal mission gain
    [InlineData(10,"80E4B500000002000000")] // mission cash differs from literal summary
    public void MissingMalformedDuplicateOrUnexplainedEventsRefuse(int index,string replacement)
    {
        var r=Records();r[index]=Convert.FromHexString(replacement);Assert.Throws<InvalidDataException>(()=>Parse(r));
    }
    [Fact] public void FightingAndLootMustBeInsideTheirActualOperationWindows()
    {
        var o=Operations();o[0]=o[0] with{Kind="Talk"};Assert.Throws<InvalidDataException>(()=>Parse(operations:o));
        o=Operations();o[1]=o[1] with{Kind="Use"};Assert.Throws<InvalidDataException>(()=>Parse(operations:o));
        o=Operations();o[1]=o[1] with{RpcEnd=5};Assert.Throws<InvalidDataException>(()=>Parse(operations:o));
        o=Operations();o[1]=o[1] with{RpcStart=3};Assert.Throws<InvalidDataException>(()=>Parse(operations:o));
    }
    [Fact] public void IndependentLiteralCountsOrderAndInitialTotalsCannotBeInferredFromFinalDb()
    {
        Assert.Throws<InvalidDataException>(()=>PrimaRewardLedger.Reconcile(Records(),Operations(),1001,100,new uint[]{100},new uint[]{50}));
        Assert.Throws<InvalidDataException>(()=>PrimaRewardLedger.Reconcile(Records(),Operations(),1000,101,new uint[]{100},new uint[]{50}));
        Assert.Throws<InvalidDataException>(()=>PrimaRewardLedger.Reconcile(Records(),Operations(),1000,100,new uint[]{100,200},new uint[]{50,60}));
        Assert.Throws<InvalidDataException>(()=>PrimaRewardLedger.Reconcile(Records(),Operations(),1000,100,new uint[]{200,100},new uint[]{60,50}));
        var r=Records();r[1]=new byte[]{0x81,0x18};Assert.Throws<InvalidDataException>(()=>PrimaRewardLedger.ReadBaseline(r));
    }
    [Fact] public void SummaryAloneCannotSatisfyCompletionWaitAndExtrasReachStrictDecoder()
    {
        var r=Records();Assert.False(PrimaRewardLedger.HasCompleteMissionAwards(r.Take(8).ToArray(),1));
        Assert.False(PrimaRewardLedger.HasCompleteMissionAwards(r.Take(10).ToArray(),1));
        Assert.True(PrimaRewardLedger.HasCompleteMissionAwards(r,1));
        Assert.False(PrimaRewardLedger.HasCompleteMissionAwards(r,2));
        Assert.Throws<ArgumentOutOfRangeException>(()=>PrimaRewardLedger.HasCompleteMissionAwards(r,0));
        var duplicate=r.Concat(new[]{r[8]}).ToArray();Assert.True(PrimaRewardLedger.HasCompleteMissionAwards(duplicate,1));
        Assert.Throws<InvalidDataException>(()=>Parse(duplicate));
    }
    [Fact] public void ColdSessionReplayBeforeLaterResponseCursorIsStillRejected()
    {
        var all=new[]{Records()[0],Records()[7],Convert.FromHexString("8149")};
        Assert.False(PrimaRewardLedger.HasNoCompletionReplay(all));
        Assert.True(PrimaRewardLedger.HasNoCompletionReplay(all.Skip(2).ToArray())); // old cursor blind spot
        Assert.True(PrimaRewardLedger.HasNoCompletionReplay(new[]{Records()[0],Convert.FromHexString("8149")}));
    }
    [Fact] public void One5500WindowCannotCredit6500Then12000WithOneAcceptance()
    {
        var r=Records();r[1]=Convert.FromHexString("80E4E803000000000000"); // cash baseline1000
        r[4]=Convert.FromHexString("81190000000010007C15000000000000010000000008AF1A0000"); //5500
        r[5]=Convert.FromHexString("80E46419000000000000"); //6500
        r[10]=Convert.FromHexString("80E4122F000002000000"); //12050 after literal50 mission award
        var duplicate=r.Take(6).Concat(new[]{Convert.FromHexString("80E4E02E000000000000")}).Concat(r.Skip(6)).ToArray();
        var operations=new[]{new PrimaLedgerOperation("fight","Defeat",2,4),new("take","Loot",4,8),new("finish","Upload",8,12)};
        Assert.Throws<InvalidDataException>(()=>PrimaRewardLedger.Reconcile(duplicate,operations,1000,1000,new uint[]{100},new uint[]{50}));
    }
    [Fact] public void NextWindowBeforeAcceptanceAndAmbiguousAcceptancesRefuse()
    {
        var r=Records();r[10]=Convert.FromHexString("80E4D200000002000000"); //210 after two30 credits and50 mission
        var interleaved=r.Take(6).Concat(new[]{r[4],Convert.FromHexString("80E4A000000000000000")}).Concat(r.Skip(6)).ToArray();
        var operations=new[]{new PrimaLedgerOperation("fight","Defeat",2,4),new("take","Loot",4,9),new("finish","Upload",9,13)};
        Assert.Throws<InvalidDataException>(()=>Parse(interleaved,operations));
        r=Records();var doubleAck=r.Take(7).Concat(new[]{r[6]}).Concat(r.Skip(7)).ToArray();
        operations=new[]{new PrimaLedgerOperation("fight","Defeat",2,4),new("take","Loot",4,8),new("finish","Upload",8,12)};
        Assert.Throws<InvalidDataException>(()=>Parse(doubleAck,operations));
    }
    [Fact] public void DistinctWindowsEachNeedTheirOwnAcceptanceAndRecordExactIndices()
    {
        var r=Records();r[10]=Convert.FromHexString("80E4D200000002000000");
        var two=r.Take(7).Concat(new[]{r[4],Convert.FromHexString("80E4A000000000000000"),r[6]}).Concat(r.Skip(7)).ToArray();
        var operations=new[]{new PrimaLedgerOperation("fight","Defeat",2,4),new("take","Loot",4,10),new("finish","Upload",10,14)};
        var ledger=Parse(two,operations);Assert.Equal(60,ledger.LootInformation);
        var credits=ledger.Events.Where(e=>e.Kind=="NPC loot cash").ToArray();
        Assert.Equal(new int?[]{4,7},credits.Select(e=>e.NpcWindowIndex));Assert.Equal(new int?[]{6,9},credits.Select(e=>e.AcceptanceIndex));
        two[9]=Convert.FromHexString("8118");Assert.Throws<InvalidDataException>(()=>Parse(two,operations));
    }
}
