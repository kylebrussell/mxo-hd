using HeadlessClient;
namespace hds.Tests;
public class ZionAfter1212LedgerTests
{
    static byte[] Wallet(byte op,uint value,uint type)=>new byte[]{0x80,op}.Concat(BitConverter.GetBytes(value)).Concat(BitConverter.GetBytes(type)).ToArray();
    static byte[][] Records(uint restoredXp=1010)
    {
        var summary=new byte[26];summary[0]=0x80;summary[1]=0xa2;BitConverter.GetBytes(50u).CopyTo(summary,11);BitConverter.GetBytes(100u).CopyTo(summary,15);
        return new[]{Wallet(0xe5,1000,0),Wallet(0xe4,1000,0),Wallet(0xe6,10,1),Wallet(0xe5,1010,1),Wallet(0xe5,restoredXp,0),Wallet(0xe4,1000,0),summary,Wallet(0xe6,100,2),Wallet(0xe5,1110,2),Wallet(0xe4,1050,2)};
    }
    [Fact] public void ExplicitRestoreBoundariesReconcileEarnedEventsWithoutDroppingBaselineRecords()
    {
        var r=PrimaRewardLedger.Reconcile(Records(),new[]{new PrimaLedgerOperation("actual kill","Defeat",2,4)},1000,1000,new uint[]{100},new uint[]{50},new HashSet<int>{4,5});
        Assert.Equal(1110u,r.Experience);Assert.Equal(10,r.FightingExperience);Assert.Equal(100,r.MissionExperience);Assert.Equal(50,r.MissionInformation);
        Assert.Equal(2,r.Events.Count(e=>e.Kind.StartsWith("Restored")));
    }
    [Fact] public void UnmarkedChangedOrNonBaselineRestoreIndicesAreRejected()
    {
        Assert.Throws<InvalidDataException>(()=>PrimaRewardLedger.Reconcile(Records(),new[]{new PrimaLedgerOperation("kill","Defeat",2,4)},1000,1000,new uint[]{100},new uint[]{50}));
        Assert.Throws<InvalidDataException>(()=>PrimaRewardLedger.Reconcile(Records(1009),new[]{new PrimaLedgerOperation("kill","Defeat",2,4)},1000,1000,new uint[]{100},new uint[]{50},new HashSet<int>{4,5}));
        Assert.Throws<InvalidDataException>(()=>PrimaRewardLedger.Reconcile(Records(),new[]{new PrimaLedgerOperation("kill","Loot",2,4)},1000,1000,new uint[]{100},new uint[]{50},new HashSet<int>{4,5}));
        Assert.Throws<InvalidDataException>(()=>PrimaRewardLedger.Reconcile(Records(),new[]{new PrimaLedgerOperation("kill","Defeat",2,4)},1000,1000,new uint[]{100},new uint[]{50},new HashSet<int>{2,4,5}));
    }
}
