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
    [Fact] public void BulkUploadSkipsSlotZeroEquipmentAndNonPlayerAbilityFamilies()
    {
        // Native fixtures contain this slot0/count0 row; normalized snapshots must not
        // turn it into an intended upload or produce a misleading transaction refusal.
        Assert.False(AbilityAcquisition.UploadCandidate(new Crafting.InventoryItem(0,0x800d9800,0)));
        Assert.False(AbilityAcquisition.UploadCandidate(new Crafting.InventoryItem(0,Detect,1)));
        Assert.False(AbilityAcquisition.UploadCandidate(new Crafting.InventoryItem(97,Detect,1)));
        Assert.False(AbilityAcquisition.UploadCandidate(new Crafting.InventoryItem(0x8001,Detect,1)));
        Assert.False(AbilityAcquisition.UploadCandidate(new Crafting.InventoryItem(1,0x80013800,1))); // Focus/class1.
        Assert.False(AbilityAcquisition.UploadCandidate(new Crafting.InventoryItem(1,0x8404c180,1)));
        Assert.False(AbilityAcquisition.UploadCandidate(new Crafting.InventoryItem(1,7332,1)));
        Assert.False(AbilityAcquisition.UploadCandidate(new Crafting.InventoryItem { Slot=1,Goid=Detect,Count=0 }));
        Assert.True(AbilityAcquisition.UploadCandidate(new Crafting.InventoryItem(1,Detect,1)));
        Assert.True(AbilityAcquisition.UploadCandidate(new Crafting.InventoryItem(96,0x80401c03,1)));
        Assert.True(AbilityAcquisition.UploadCandidate(new Crafting.InventoryItem(1,Detect,2))); // Transaction must still refuse malformed intended sources.
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
    [Fact] public void StoredRawDescriptorsAcceptOnlySignedOrUnsigned32AndCannotAliasCorrupt64()
    {
        Assert.True(AbilityAcquisition.TryDescriptor(unchecked((int)Detect),out uint signed));Assert.Equal(Detect,signed);
        Assert.True(AbilityAcquisition.TryDescriptor(Detect,out uint unsigned));Assert.Equal(Detect,unsigned);
        Assert.True(AbilityAcquisition.TryDescriptor(int.MinValue,out _));Assert.True(AbilityAcquisition.TryDescriptor(uint.MaxValue,out _));
        Assert.False(AbilityAcquisition.TryDescriptor((long)int.MinValue-1,out _));
        Assert.False(AbilityAcquisition.TryDescriptor((long)uint.MaxValue+1,out _));
        foreach(long raw in new[]{(long)unchecked((int)Detect),(long)Detect,(long)0x80401c03})
            Assert.Throws<AbilityAcquisition.Refusal>(()=>AbilityAcquisition.LearnSlot(new[]{new AbilityAcquisition.ArchiveRow(1,0,raw)},Detect,null));
        foreach(long corrupt in new[]{(long)Detect+(1L<<32),(long)unchecked((int)Detect)-(1L<<32)})
            Assert.Equal(1,AbilityAcquisition.LearnSlot(new[]{new AbilityAcquisition.ArchiveRow(1,0,corrupt)},Detect,null));
        Assert.False(AbilityAcquisition.Genuine(0x80000000u|(78u<<10))); // Focus attribute, class1.
        Assert.False(AbilityAcquisition.Genuine(0x80000000u|(108u<<10))); // Current Health internal, class2.
    }
    static WorldClient PublicationClient(uint district=1)
    {
        var c=(WorldClient)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        c.Alive=true;typeof(WorldClient).GetField("lastUsedTime",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.SetValue(c,TimeUtils.getUnixTimeUint32());
        c.playerData=new ClientData();c.playerData.setCharID(110);c.playerData.setDistrictId(district);c.playerData.setOnWorld(district!=901);c.messageQueue=new();
        typeof(WorldClient).GetField("<TransportFence>k__BackingField",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.SetValue(c,new WorldSendFence());return c;
    }
    [Fact] public void PostCommitPublicationKeepsPacketOrderAndAllowsCapturedLoadingAreaState()
    {
        foreach(uint district in new uint[]{1,901})
        {
            var c=PublicationClient(district);var stamp=new AbilityAcquisition.Publication(c);
            byte[][] packets={ServerPackets.BuildAbilityCodeAdd(1,Detect),ServerPackets.BuildInfoDelta(-10000,8),ServerPackets.BuildInfoCurrent(10000,8),ServerPackets.BuildAbilityCodeBuyAck(Detect,10000)};
            Assert.True(stamp.Publish(c,packets));Assert.Equal(4,c.messageQueue.RPCMessagesQueue.Count);
            for(int i=0;i<packets.Length;i++){var message=(SequencedMessage)c.messageQueue.RPCMessagesQueue[i]!;Assert.Equal(packets[i],message.content.Skip(1));Assert.Equal(c.playerData.worldGeneration,message.RpcWorldGeneration);Assert.Equal(-1,message.rpcNumber);}
        }
    }
    [Fact] public void StaleSessionRefusesBothSuccessAndRefusalWithoutTouchingUnrelatedRpc()
    {
        for(int change=0;change<9;change++)
        {
            var c=PublicationClient();c.messageQueue.addRpcMessage(new byte[]{0x81,0x49});var stamp=new AbilityAcquisition.Publication(c);
            if(change==0)c.playerData.lifeGeneration++;
            if(change==1)c.playerData.setOnWorld(false);
            if(change==2)c.playerData.setCharID(111);
            if(change==3)c.playerData.setDistrictId(901);
            if(change==4)c.playerData=new ClientData();
            if(change==5)c.TransportFence.PublishIncoming(true,()=>{});
            if(change==6)c.playerData.waitForRPCShutDown=true;
            if(change==7)c.playerData.worldChangePending=true;
            if(change==8)c.Alive=false;
            Assert.False(stamp.Publish(c,ServerPackets.BuildAbilityCodeBuyAck(Detect,10000)));
            Assert.False(stamp.Publish(c,ServerPackets.BuildSystemChatMessage("Refused","SYSTEM")));
            Assert.Single(c.messageQueue.RPCMessagesQueue);
        }
    }
    [Fact] public void ResetAfterQueuePrunesOnlyUnassignedAcquisitionRecordsBeforeNumbering()
    {
        var c=PublicationClient();c.messageQueue.addRpcMessage(new byte[]{0x81,0x49});var stamp=new AbilityAcquisition.Publication(c);
        Assert.True(stamp.Publish(c,ServerPackets.BuildAbilityCodeAdd(1,Detect),ServerPackets.BuildAbilityCodeBuyAck(Detect,10000)));
        c.TransportFence.PublishIncoming(true,()=>{});long epoch=c.TransportFence.Capture();
        Assert.True(c.TransportFence.ApplyReset(epoch,()=>{}));c.TransportFence.Resume(epoch);
        Assert.True(c.TransportFence.TryRun(epoch,()=>c.messageQueue.PruneUnassignedWorldRpcs(c.playerData.worldGeneration,epoch)));
        var remaining=Assert.IsType<SequencedMessage>(Assert.Single(c.messageQueue.RPCMessagesQueue.Cast<object>()));
        Assert.Equal(new byte[]{2,0x81,0x49},remaining.content);Assert.Equal(-1,remaining.rpcNumber);
        Assert.False(stamp.Publish(c,ServerPackets.BuildAbilityCodeBuyAck(Detect,10000)));
    }
    [Fact] public void WalletCacheChangesOnlyInsideCurrentPurchasePublication()
    {
        foreach(bool reset in new[]{false,true})
        {
            var c=PublicationClient();c.playerData.setInfo(777);var stamp=new AbilityAcquisition.Publication(c);
            if(reset){c.TransportFence.PublishIncoming(true,()=>{});long epoch=c.TransportFence.Capture();Assert.True(c.TransportFence.ApplyReset(epoch,()=>{}));c.TransportFence.Resume(epoch);}
            else c.playerData.setCharID(111);
            Assert.False(stamp.PublishPurchase(c,123,ServerPackets.BuildAbilityCodeBuyAck(Detect,10000)));
            Assert.Equal(777,c.playerData.getInfo());Assert.Empty(c.messageQueue.RPCMessagesQueue);
        }
        var current=PublicationClient();current.playerData.setInfo(777);var valid=new AbilityAcquisition.Publication(current);
        Assert.True(valid.PublishPurchase(current,123,ServerPackets.BuildAbilityCodeBuyAck(Detect,10000)));Assert.Equal(123,current.playerData.getInfo());
        current=PublicationClient();current.playerData.worldChangePending=true;var pending=new AbilityAcquisition.Publication(current);
        Assert.False(pending.PublishPurchase(current,123,ServerPackets.BuildAbilityCodeBuyAck(Detect,10000)));Assert.Equal(0,current.playerData.getInfo());
    }
}
