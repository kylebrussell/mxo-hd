using hds;
using hds.shared;
using System.Reflection;
using System.Runtime.CompilerServices;
namespace hds.Tests;
public class VendorBuybackTests
{
    [Theory]
    [InlineData("0000000001",true)]
    [InlineData("0100000001",false)]
    [InlineData("0000000000",false)]
    [InlineData("00000001",false)]
    [InlineData("000000000100",false)]
    public void NativeBuyBackCarriesNoSelectedItem(string body,bool expected)=>
        Assert.Equal(expected,VendorBuyback.TryBuyback(Convert.FromHexString(body)));
    [Fact] public void RawZeroCountAndPurityAreRetainedWithoutRewritingTheirDatabaseValues()
    {
        var item=new VendorBuyback.Item(7,43060,0,0x34000000,88,"0000-00-00 00:00:00","2026-01-01 00:00:00");
        Assert.True(VendorBuyback.TryDescriptor(item,out var goid,out var descriptor));
        Assert.Equal(43060u,goid);Assert.Equal("00000010",Convert.ToHexString(descriptor));
        Assert.Equal(0,item.RawCount);Assert.Equal(0x34000000,item.Purity);Assert.Equal(7u,item.OriginalId);
        var sale=VendorBuyback.Packets(new(true,item,8053143,5000),false);
        Assert.Equal("81120034A800000000001088130000",Convert.ToHexString(sale[^1]));
    }
    [Theory] [InlineData(-2147476477L)] [InlineData(2147490819L)]
    public void SignedAndUnsignedAbilityDescriptorsKeepExactLevel(long raw)
    {
        var item=new VendorBuyback.Item(4,raw,0,123,1);
        Assert.True(VendorBuyback.TryDescriptor(item,out var goid,out var descriptor));
        Assert.Equal(unchecked((uint)raw),goid);
        Assert.Equal(AbilityProgression.AbilityCodeValue(goid),BitConverter.ToUInt32(descriptor));
        Assert.Equal(raw,item.RawGoid);Assert.Equal(123,item.Purity);
    }
    [Theory] [InlineData(4294967297L,1)] [InlineData(-2147483649L,1)] [InlineData(11666L,65)] [InlineData(11666L,-1)]
    public void UnrepresentableRowsCannotBeDestructivelySold(long raw,int count)=>
        Assert.False(VendorBuyback.TryDescriptor(new(1,raw,count,0,1),out _,out _));
    [Theory] [InlineData(0,false)] [InlineData(1,true)] [InlineData(96,true)] [InlineData(97,false)]
    public void OnlyRealBagSlotsCanCreateASaleReceipt(ushort slot,bool expected)=>
        Assert.Equal(expected,VendorBuyback.ReceiptSlot(slot));
    [Theory]
    [InlineData(0x8404c180u)] // Stored-code family: its masked ID aliases an ordinary ability.
    [InlineData(0x8002c400u)] // DebuffAreaRadius177 is a class2 attribute, not an ordinary learned ability.
    [InlineData(0x80013800u)] // Focus78 is the actual class1 attribute template.
    [InlineData(0x83fffc01u)] // Genuine family but unknown canonical template4095.
    public void HighBitDoesNotMakeCodeAttributesOrUnknownTemplatesOrdinaryAbilityItems(uint raw)
    {
        Assert.False(VendorBuyback.TryDescriptor(new(1,raw,0,0,1),out _,out _));
        Assert.False(VendorBuyback.TryDescriptor(new(1,unchecked((int)raw),0,0,1),out _,out _));
    }
    [Theory] [InlineData(0x80059000u)] [InlineData(0x80459000u)] [InlineData(0x80859000u)]
    public void GenuineKnownClassZeroCanonicalAliasesRetainExactRawIdentity(uint raw)
    {
        Assert.Equal((ushort)356,AbilityCatalog.Canonical(AbilityCatalog.IdOf(raw)));
        Assert.True(VendorBuyback.TryDescriptor(new(1,raw,0,123,1),out var goid,out var descriptor));
        Assert.Equal(raw,goid);Assert.Equal(raw|1,BitConverter.ToUInt32(descriptor));
        Assert.True(VendorBuyback.TryDescriptor(new(1,unchecked((int)raw),0,123,1),out goid,out descriptor));
        Assert.Equal(raw,goid);Assert.Equal(raw|1,BitConverter.ToUInt32(descriptor));
    }
    [Theory] [InlineData(32,"7C")] [InlineData(33,"80")] [InlineData(64,"FC")]
    public void ExistingSixBitStackEncodingIsPreserved(int count,string amount)
    {
        Assert.True(VendorBuyback.TryDescriptor(new(1,7332,count,123,1),out _,out var descriptor));
        Assert.Equal("0000"+amount+"10",Convert.ToHexString(descriptor));
    }
    [Fact] public void BuyBackSuccessHasWholeItemAndFinalOneWhileRefusalHasNoPositiveAck()
    {
        var response=VendorBuyback.Packets(new(true,new(1,11666,4,0,9),1000,680),true);
        Assert.Equal("5E0900922D000000000C1001",Convert.ToHexString(response[0]));
        Assert.Equal("810F922D000000000C10A802000001",Convert.ToHexString(response[^1]));
        Assert.Equal(new[]{"5E","80E7","80E4","810F"},response.Select(p=>Convert.ToHexString(p.AsSpan(0,p[0]==0x5e?1:2))));
        var denied=VendorBuyback.Packets(VendorBuyback.Result.Denied("Full."),true);
        Assert.Equal(2,denied.Length);Assert.Equal("81100000000000",Convert.ToHexString(denied[1]));
    }
    [Fact] public void WorldSessionTokensAreStableAcrossVendorCloseButChangeOnWorldGeneration()
    {
        var data=new ClientData();data.setCharID(110);string first=VendorBuyback.SessionFor(data);
        data.activeVendor=null;Assert.Equal(first,VendorBuyback.SessionFor(data));
        data.worldGeneration++;Assert.NotEqual(first,VendorBuyback.SessionFor(data));
        var another=new ClientData();another.setCharID(110);Assert.NotEqual(first,VendorBuyback.SessionFor(another));
    }
    private static WorldClient Client()
    {
        var c=(WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));c.playerData=new();
        c.playerData.setCharID(110);c.playerData.setDistrictId(1);c.playerData.setOnWorld(true);
        c.playerData.activeVendor=new Vendor(1,98);c.playerData.activeVendorGeneration=c.playerData.worldGeneration;
        c.Alive=true;c.messageQueue=new();
        typeof(WorldClient).GetField("lastUsedTime",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(c,TimeUtils.getUnixTimeUint32());
        typeof(WorldClient).GetField("lastFlushTick",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(c,long.MaxValue);
        typeof(WorldClient).GetField("<TransportFence>k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(c,new WorldSendFence());return c;
    }
    [Theory] [InlineData("character")] [InlineData("world")] [InlineData("life")] [InlineData("vendor")] [InlineData("epoch")] [InlineData("pending")] [InlineData("shutdown")] [InlineData("dead-session")]
    public void StalePublicationCannotOverwriteWalletOrPublishSuccess(string cause)
    {
        var c=Client();var d=c.playerData;d.setInfo(777);var stamp=new VendorBuyback.Publication(c);
        switch(cause)
        {
            case "character":d.setCharID(111);break;
            case "world":d.worldGeneration++;break;
            case "life":d.lifeGeneration++;break;
            case "vendor":d.activeVendor=new Vendor(1,98);break;
            case "epoch":c.TransportFence.PublishIncoming(true,()=>{});break;
            case "pending":d.worldChangePending=true;break;
            case "shutdown":d.waitForRPCShutDown=true;break;
            case "dead-session":c.Alive=false;break;
        }
        var result=new VendorBuyback.Result(true,new(1,11666,0,0,1),999,680);
        Assert.False(stamp.Publish(c,result,VendorBuyback.Packets(result,true)));
        Assert.Equal(777,d.getInfo());Assert.Empty(c.messageQueue.RPCMessagesQueue);
    }
    [Fact] public void ResetAfterQueuePrunesOnlyUnassignedTradeGroupAndRetainsOrdinaryRpc()
    {
        var c=Client();var stamp=new VendorBuyback.Publication(c);
        c.messageQueue.addRpcMessage(new byte[]{0x81,0x10,0,0,0,0,0});
        var result=new VendorBuyback.Result(true,new(1,11666,0,0,1),999,680);
        Assert.True(stamp.Publish(c,result,VendorBuyback.Packets(result,true)));
        Assert.Equal(5,c.messageQueue.RPCMessagesQueue.Count);
        c.playerData.worldGeneration++;
        c.messageQueue.PruneUnassignedWorldRpcs(c.playerData.worldGeneration,c.TransportFence.Capture());
        Assert.Single(c.messageQueue.RPCMessagesQueue);Assert.Equal(999,c.playerData.getInfo());
    }
}
