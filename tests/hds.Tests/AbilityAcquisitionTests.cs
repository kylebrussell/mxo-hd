using hds;
namespace hds.Tests;
public class AbilityAcquisitionTests
{
    const uint Detect=0x80001c00;
    [Fact] public void GenuineFamilyPrecedesCanonicalLookup()
    {
        Assert.True(AbilityAcquisition.Genuine(Detect));
        Assert.True(AbilityAcquisition.Genuine(0x80401c03));
        Assert.False(AbilityAcquisition.Genuine(0x8404c180)); // CodeStorage2435 aliases known304.
        Assert.False(AbilityAcquisition.Genuine(0));
        Assert.False(AbilityAcquisition.Genuine(0x803ffc00));
    }
    [Fact] public void PurchaseUsesFreeBagAndCheckedWalletWithoutDeduplicatingReceipts()
    {
        var row=new AbilityAcquisition.BagRow(1,1,Detect,1);
        Assert.Equal(2,AbilityAcquisition.FreeBag(new[]{row}));
        Assert.Throws<AbilityAcquisition.Refusal>(()=>AbilityAcquisition.FreeBag(new[]{row,new AbilityAcquisition.BagRow(2,1,Detect,1)}));
        Assert.Throws<AbilityAcquisition.Refusal>(()=>AbilityAcquisition.FreeBag(Enumerable.Range(1,96).Select(i=>new AbilityAcquisition.BagRow((uint)i,i,Detect,1))));
        Assert.Equal(0u,AbilityAcquisition.BuyCash(10000,10000));
        Assert.Equal(10000u,AbilityAcquisition.BuyCash(20000,10000));
        Assert.Throws<AbilityAcquisition.Refusal>(()=>AbilityAcquisition.BuyCash(9999,10000));
        Assert.Throws<AbilityAcquisition.Refusal>(()=>AbilityAcquisition.BuyCash(-1,0));
        Assert.Throws<AbilityAcquisition.Refusal>(()=>AbilityAcquisition.BuyCash((long)int.MaxValue+1,0));
    }
    [Fact] public void LearningCanonicalDuplicatesExcludeCodeAndHiddenFamiliesButCountAllRealSlots()
    {
        var rows=new[]{new AbilityAcquisition.ArchiveRow(1,0,0x8404c180),new AbilityAcquisition.ArchiveRow(2,520,Detect)};
        Assert.Equal(1,AbilityAcquisition.LearnSlot(rows,Detect,null));
        Assert.Throws<AbilityAcquisition.Refusal>(()=>AbilityAcquisition.LearnSlot(new[]{new AbilityAcquisition.ArchiveRow(3,3,0x80401c03)},Detect,null));
        Assert.Throws<AbilityAcquisition.Refusal>(()=>AbilityAcquisition.LearnSlot(rows,Detect,520));
        Assert.Throws<AbilityAcquisition.Refusal>(()=>AbilityAcquisition.LearnSlot(rows,Detect,0));
        Assert.Equal(2,AbilityAcquisition.LearnSlot(rows,Detect,null,new ushort[]{1}));
    }
    [Fact] public void LastArchiveSlotIsUsableAndEveryConflictingSlotRefuses()
    {
        var rows=Enumerable.Range(0,519).Select(i=>new AbilityAcquisition.ArchiveRow((uint)i,i,0x84000000)).ToArray();
        Assert.Equal(519,AbilityAcquisition.LearnSlot(rows,Detect,null));
        Assert.Throws<AbilityAcquisition.Refusal>(()=>AbilityAcquisition.LearnSlot(rows.Append(new(600,519,0x84000000)),Detect,null));
        Assert.Throws<AbilityAcquisition.Refusal>(()=>AbilityAcquisition.LearnSlot(rows.Append(new AbilityAcquisition.ArchiveRow(999,0,0x84000000)),Detect,null));
    }
    [Fact] public void ExistingPositiveRetailBagAndBuyRecordBytesRemainExact()
    {
        Assert.Equal("5E58002E000000011C008001",Convert.ToHexString(ServerPackets.BuildAbilityCodeAdd(0x58,Detect)));
        Assert.Equal("810F2E000000011C00801027000000",Convert.ToHexString(ServerPackets.BuildAbilityCodeBuyAck(Detect,10000)));
    }
    [Fact] public void VendorOpenerRequiresExactOptInDisposableCsrAndLeavesStockUnmodified()
    {
        Assert.True(AbilityAcquisitionQa.Allowed("1","1","mxotest_owned",110,true,false));
        foreach(var bad in new[]{"reality_hd","mxotest_","mxotest_bad-name"})Assert.False(AbilityAcquisitionQa.Allowed("1","1",bad,110,true,false));
        Assert.False(AbilityAcquisitionQa.Allowed("","1","mxotest_owned",110,true,false));
        Assert.False(AbilityAcquisitionQa.Allowed("1","0","mxotest_owned",110,true,false));
        Assert.False(AbilityAcquisitionQa.Allowed("1","1","mxotest_owned",2,true,false));
        Assert.False(AbilityAcquisitionQa.Allowed("1","1","mxotest_owned",110,false,false));
        Assert.False(AbilityAcquisitionQa.Allowed("1","1","mxotest_owned",110,true,true));
        var mode=new NativeQa.Modes.AbilityAcquisitionMode();Assert.Equal("1",mode.Plan.EnvironmentOverrides["MXO_ABILITY_ACQUISITION_QA"]);
        Assert.True(mode.Plan.DevCommands);Assert.False(mode.Plan.GmCharacter110);Assert.False(mode.Plan.FixedCallbackRolls);
        Assert.Equal(new ushort[]{2,17},mode.Plan.Abilities);
        Assert.Equal("",NativeQa.NativeQaFixturePlan.EnvironmentFor("")["MXO_ABILITY_ACQUISITION_QA"]);
        foreach(ushort id in new ushort[]{1,5}){Assert.NotNull(AbilityCatalog.Find(id));Assert.True(AbilityProgression.MeetsCharacterLevel(id,50));}
    }
}
