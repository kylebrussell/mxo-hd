using System.Reflection;
using System.Runtime.CompilerServices;
using hds;
using hds.shared;
namespace hds.Tests;
[Collection("DelayedActions")]
public class AbilityTrainingPublicationTests
{
    private static WorldClient Client()
    {
        var c=(WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));c.playerData=new();c.playerData.setCharID(110);c.Alive=true;c.messageQueue=new();
        typeof(WorldClient).GetField("lastUsedTime",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(c,TimeUtils.getUnixTimeUint32());
        typeof(WorldClient).GetField("lastFlushTick",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(c,long.MaxValue);
        typeof(WorldClient).GetField("<TransportFence>k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(c,new WorldSendFence());return c;
    }
    [Fact] public void PureModalRetainsLiteralLegacyBytesAndResponseOrder()
    {
        var expected=Convert.FromHexString("2E17"+"00000000000000000024000000000000000000000000000000000000000000000000"+"04004E6F2E00");
        Assert.Equal(expected,ServerPackets.BuildSystemChatMessage("No.","MODAL"));
        var response=AbilityTraining.ResponsePackets(new(false,37,14,0,0,"No."),false);
        Assert.Equal(2,response.Length);Assert.Equal(expected,response[0]);Assert.Equal("80B825000E000C000018",Convert.ToHexString(response[1]));
        var c=Client();new ServerPackets().sendSystemChatMessage(c,"No.","MODAL");
        Assert.Equal(expected,((SequencedMessage)c.messageQueue.RPCMessagesQueue[0]!).content.Skip(1));
    }
    [Fact] public void SuccessKeepsCashGrantAndAckOrderWithNoAvatarSuppressingOnlyGrantRefresh()
    {
        var result=new AbilityTraining.Result(true,37,15,22500,1000,"",new[]{((ushort)37,(ushort)15)});
        var response=AbilityTraining.ResponsePackets(result,true);
        Assert.Equal(new[]{"80E7","80E4","80B3","80B2","80B8"},response.Select(p=>Convert.ToHexString(p.AsSpan(0,2))));
        Assert.Equal(ServerPackets.BuildInfoDelta(-22500,0x11),response[0]);Assert.Equal(ServerPackets.BuildInfoCurrent(1000,0x11),response[1]);
        Assert.Equal(ServerPackets.BuildAbilityUnload(970),response[2]);Assert.Equal(ServerPackets.BuildAbilityLoad(970,0,520),response[3]);
        Assert.Equal(ServerPackets.BuildAbilityUpgraded(37,15),response[4]);
        var noAvatar=AbilityTraining.ResponsePackets(result,false);Assert.Equal(new[]{response[0],response[1],response[4]},noAvatar);
    }
    [Fact] public void PacketPreparationIsOutsideFenceAndResetDuringPreparationCannotPublish()
    {
        var c=Client();long epoch=c.TransportFence.Capture();bool published=false,prepared=false;
        AbilityTraining.CompleteCurrent(c,epoch,()=>c.TransportFence.Capture()==epoch,()=>{},()=>published=true,prepare:()=>
        {
            var other=Task.Run(()=>c.TransportFence.TryRun(epoch,()=>{}));Assert.True(other.Wait(TimeSpan.FromSeconds(2)));Assert.True(other.Result);
            prepared=true;c.TransportFence.PublishIncoming(true,()=>{});
        });Assert.True(prepared);Assert.False(published);
    }
    [Fact] public void NoAvatarResponseQueuesWithoutFlushUnderFenceAndResetPrunesOnlyUnassignedTraining()
    {
        var c=Client();var data=c.playerData;long epoch=c.TransportFence.Capture();byte[][] responses=Array.Empty<byte[]>();
        AbilityTraining.CompleteCurrent(c,epoch,()=>AbilityTraining.Current(c,data,0,0,0,epoch,110),()=>{},()=>
        {
            foreach(var p in responses)c.messageQueue.AddWorldBoundRpc(p,0,epoch);
        },prepare:()=>responses=AbilityTraining.ResponsePackets(new(false,37,14,0,0,"No."),false));
        Assert.Equal(2,c.messageQueue.RPCMessagesQueue.Count);var old=c.messageQueue.RPCMessagesQueue.Cast<SequencedMessage>().ToArray();
        var prepared=new WorldPacket(data);foreach(var message in old)prepared.addRPCContent(message);
        c.messageQueue.addRpcMessage(new byte[]{0x81,0x49});var unrelated=(SequencedMessage)c.messageQueue.RPCMessagesQueue[2]!;
        c.TransportFence.PublishIncoming(true,()=>{});long current=c.TransportFence.Capture();
        Assert.True(c.TransportFence.ApplyReset(current,c.messageQueue.ForgetSentRpcs));c.TransportFence.Resume(current);
        Assert.False(c.TransportFence.TryRun(epoch,()=>prepared.getFinalData(data)));
        c.TransportFence.TryRun(current,()=>
        {
            c.messageQueue.PruneUnassignedWorldRpcs(data.worldGeneration,current);
            var next=new WorldPacket(data);foreach(SequencedMessage m in c.messageQueue.RPCMessagesQueue)next.addRPCContent(m);
            var rpc=Assert.Single(HeadlessClient.WireFormat.ParsePayload(next.getFinalData(data)).Rpcs);
            Assert.Equal("8149",Convert.ToHexString(rpc.Bytes));Assert.Equal((ushort)0,rpc.ListCounter);
        });
        Assert.Single(c.messageQueue.RPCMessagesQueue);Assert.All(old,m=>Assert.Equal(-1,m.rpcNumber));Assert.Equal(0,unrelated.rpcNumber);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public void CashCacheAndPacketsChangeOnlyTogetherAtCurrentPublication(bool resetDuringPreparation)
    {
        var c=Client();var data=c.playerData;data.setInfo(1000);long epoch=c.TransportFence.Capture();
        var result=new AbilityTraining.Result(true,37,15,200,800,"");byte[][] packets=Array.Empty<byte[]>();
        AbilityTraining.CompleteCurrent(c,epoch,()=>AbilityTraining.Current(c,data,0,0,0,epoch,110),()=>{},
            ()=>AbilityTraining.PublishResponses(c,data,result,packets,0,epoch),prepare:()=>
            {
                packets=AbilityTraining.ResponsePackets(result,false);
                if(resetDuringPreparation)c.TransportFence.PublishIncoming(true,()=>{});
            });
        Assert.Equal(resetDuringPreparation?1000L:800L,data.getInfo());
        Assert.Equal(resetDuringPreparation?0:3,c.messageQueue.RPCMessagesQueue.Count);
    }

}
