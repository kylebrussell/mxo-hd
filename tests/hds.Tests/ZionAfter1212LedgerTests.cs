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
        var r=ZionAfter1212RewardLedger.Reconcile(Records(),new[]{new ZionAfter1212LedgerOperation("actual kill","Defeat",2,4)},1000,1000,new uint[]{100},new uint[]{50},new HashSet<int>{4,5});
        Assert.Equal(1110u,r.Experience);Assert.Equal(10,r.FightingExperience);Assert.Equal(100,r.MissionExperience);Assert.Equal(50,r.MissionInformation);
        Assert.Equal(2,r.Events.Count(e=>e.Kind.StartsWith("Restored")));
    }
    [Fact] public void UnmarkedChangedOrNonBaselineRestoreIndicesAreRejected()
    {
        Assert.Throws<InvalidDataException>(()=>ZionAfter1212RewardLedger.Reconcile(Records(),new[]{new ZionAfter1212LedgerOperation("kill","Defeat",2,4)},1000,1000,new uint[]{100},new uint[]{50}));
        Assert.Throws<InvalidDataException>(()=>ZionAfter1212RewardLedger.Reconcile(Records(1009),new[]{new ZionAfter1212LedgerOperation("kill","Defeat",2,4)},1000,1000,new uint[]{100},new uint[]{50},new HashSet<int>{4,5}));
        Assert.Throws<InvalidDataException>(()=>ZionAfter1212RewardLedger.Reconcile(Records(),new[]{new ZionAfter1212LedgerOperation("kill","Loot",2,4)},1000,1000,new uint[]{100},new uint[]{50},new HashSet<int>{4,5}));
        Assert.Throws<InvalidDataException>(()=>ZionAfter1212RewardLedger.Reconcile(Records(),new[]{new ZionAfter1212LedgerOperation("kill","Defeat",2,4)},1000,1000,new uint[]{100},new uint[]{50},new HashSet<int>{2,4,5}));
    }
    [Fact]public void ClaimedIdentitiesMustBelongToThisActualWindowAndIncludeTheMissionItem()
    {
        byte[] id=Convert.FromHexString("00000008B31A0000");
        byte[] window=Convert.FromHexString("811900000000100000000000000000000100").Concat(id).ToArray();
        byte[] accepted=Convert.FromHexString("81200C0000000000000000000100").Concat(id).ToArray();
        Assert.True(ZionAfter1212RewardLedger.ClaimedFromWindow(window,accepted));
        var duplicateOffer=window.Take(16).Concat(new byte[]{2,0}).Concat(id).Concat(id).ToArray();Assert.False(ZionAfter1212RewardLedger.ClaimedFromWindow(duplicateOffer,accepted));
        Assert.False(ZionAfter1212RewardLedger.ClaimedFromWindow(window,Convert.FromHexString("81200C0000000000000000000000")));
        var wrong=accepted.ToArray();wrong[^1]=1;Assert.False(ZionAfter1212RewardLedger.ClaimedFromWindow(window,wrong));
        Assert.False(ZionAfter1212RewardLedger.ClaimedFromWindow(window,accepted.Concat(id).ToArray()));
        var repeated=Convert.FromHexString("81200C0000000000000000000200").Concat(id).Concat(id).ToArray();Assert.False(ZionAfter1212RewardLedger.ClaimedFromWindow(window,repeated));
    }

    [Fact]public void EarnedCorpseCashReconcilesWithActualClaimIdentityNotLegacyEmptyAcceptance()
    {
        var summary=new byte[26];summary[0]=0x80;summary[1]=0xa2;BitConverter.GetBytes(50u).CopyTo(summary,11);BitConverter.GetBytes(100u).CopyTo(summary,15);
        byte[] id=Convert.FromHexString("00000008B31A0000");
        byte[] window=Convert.FromHexString("811900000000100007000000000000000100").Concat(id).ToArray();
        byte[] accepted=Convert.FromHexString("81200C0000000000000000000100").Concat(id).ToArray();
        var records=new[]{Wallet(0xe5,1000,0),Wallet(0xe4,1000,0),window,Wallet(0xe4,1007,0),accepted,summary,Wallet(0xe6,100,2),Wallet(0xe5,1100,2),Wallet(0xe4,1057,2)};
        var op=new[]{new ZionAfter1212LedgerOperation("actual corpse claim","Loot",2,5)};
        var result=ZionAfter1212RewardLedger.Reconcile(records,op,1000,1000,new uint[]{100},new uint[]{50});Assert.Equal(7,result.LootInformation);Assert.Equal(1057u,result.Information);
        records[4]=Convert.FromHexString("81200C0000000000000000000000");Assert.Throws<InvalidDataException>(()=>ZionAfter1212RewardLedger.Reconcile(records,op,1000,1000,new uint[]{100},new uint[]{50}));
    }

}
