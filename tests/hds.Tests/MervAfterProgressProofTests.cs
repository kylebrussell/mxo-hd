using HeadlessClient;
using hds;
namespace hds.Tests;
public sealed class MervAfterProgressProofTests
{
    static MissionProgress.SavedState State(int phase=1)=>new(){Version=2,Mission=301210,Phase=phase,Instance=73,Identity=new string('a',32),Started=100,Done=new[]{1,3},EverDone=new[]{1,2,3}};
    static byte[] Ack(int goal)=>new byte[]{0x80,0xa0,(byte)goal,6,0,1};
    static byte[] Summary(){var b=new byte[26];b[0]=0x80;b[1]=0xa2;return b;}
    [Fact]public void DelayedRepeatedNumericGoalCannotProveOptionalSearchWithoutCurrentPhaseDone()
    {
        var scope=MervAfterProgressProof.Capture(State());var records=new[]{Ack(3)};var current=State();current.Done=new[]{1};
        Assert.False(MervAfterProgressProof.GoalDone(scope,1,2,3,true,current,null,0,records,0,0));
        current.Done=new[]{1,3};Assert.True(MervAfterProgressProof.GoalDone(scope,1,2,3,true,current,null,0,records,0,0));
        Assert.False(MervAfterProgressProof.GoalDone(scope,1,2,3,true,current,null,0,records,1,0));
        current.Phase=2;Assert.False(MervAfterProgressProof.GoalDone(scope,1,2,3,true,current,null,0,records,0,0));
    }
    [Theory][InlineData("mission")][InlineData("phase")][InlineData("identity")][InlineData("started")][InlineData("instance")]
    public void ReplacementAcceptanceOrPhaseCannotBorrowTheCurrentGoalAcknowledgement(string field)
    {
        var original=State();var scope=MervAfterProgressProof.Capture(original);var current=State();
        switch(field){case "mission":current.Mission++;break;case "phase":current.Phase--;break;case "identity":current.Identity=new string('b',32);break;case "started":current.Started++;break;case "instance":current.Instance++;break;}
        Assert.False(MervAfterProgressProof.GoalDone(scope,1,2,3,true,current,null,0,new[]{Ack(3)},0,0));
        current.Phase=2;Assert.Equal(field=="phase",MervAfterProgressProof.Advanced(scope,1,2,current,null,0,Array.Empty<byte[]>(),0));
    }
    [Fact]public void TerminalGoalNeedsExactAcceptanceCasReceiptOneCompletionAndFreshWire()
    {
        var receipt=State(2);receipt.Done=new[]{1};var scope=MervAfterProgressProof.Capture(receipt);var records=new[]{Ack(1),Summary()};
        Assert.True(MervAfterProgressProof.GoalDone(scope,2,2,1,false,null,receipt,1,records,0,0));
        Assert.False(MervAfterProgressProof.GoalDone(scope,2,2,1,false,null,receipt,1,records,1,0));
        Assert.False(MervAfterProgressProof.GoalDone(scope,2,2,1,false,null,receipt,1,records,0,2));
        Assert.False(MervAfterProgressProof.GoalDone(scope,2,2,1,false,null,receipt,2,records,0,0));
        receipt.Identity=new string('b',32);Assert.False(MervAfterProgressProof.GoalDone(scope,2,2,1,false,null,receipt,1,records,0,0));
        receipt.Identity=scope.Identity;receipt.Done=Array.Empty<int>();Assert.False(MervAfterProgressProof.GoalDone(scope,2,2,1,false,null,receipt,1,records,0,0));
    }
    [Fact]public void ActualMissionItemUsesOneInventoryRowAndTheExistingNonstackableStoredCountZero()
    {
        var row=new MervAfterProgressProof.InventoryRow(1001,7,6827,0);
        Assert.Equal(row,MervAfterProgressProof.RequireOwnedItem(new[]{row},1001,6827,7));
        Assert.Throws<InvalidDataException>(()=>MervAfterProgressProof.RequireOwnedItem(Array.Empty<MervAfterProgressProof.InventoryRow>(),1001,6827,7));
        Assert.Throws<InvalidDataException>(()=>MervAfterProgressProof.RequireOwnedItem(new[]{row,row with{Slot=8}},1001,6827,7));
        foreach(var wrong in new[]{row with{Character=1002},row with{Slot=8},row with{Goid=6828},row with{Count=1}})
            Assert.Throws<InvalidDataException>(()=>MervAfterProgressProof.RequireOwnedItem(new[]{wrong},1001,6827,7));
    }
}
