using System.Reflection;
using System.Runtime.CompilerServices;
using hds;
using hds.shared;
namespace hds.Tests;
[Collection("DelayedActions")]
public class MissionContainerRetryTests
{
    private static WorldClient Client()
    {
        var c=(WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        c.playerData=new ClientData(); c.playerData.setCharID(110); c.playerData.setDistrictId(1); c.playerData.setOnWorld(true);
        c.messageQueue=new MessageQueue(); c.Alive=true;
        typeof(WorldClient).GetField("lastUsedTime",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(c,TimeUtils.getUnixTimeUint32());
        typeof(WorldClient).GetField("<TransportFence>k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(c,new WorldSendFence());
        return c;
    }
    [Fact]
    public void CurrentRollbackOwnerGetsOnlyWorldBoundZeroClaimReply()
    {
        var previous=Store.currentClient; var c=Client(); Store.currentClient=c;
        try
        {
            var owner=new MissionContainerRetry.Owner(c);
            Assert.True(MissionContainerRetry.Acknowledge(c,owner));
            var ack=(SequencedMessage)Assert.Single(c.messageQueue.RPCMessagesQueue)!;
            Assert.Equal("0E81200C0000000000000000000000",Convert.ToHexString(ack.content));
            Assert.Equal(owner.World,ack.RpcWorldGeneration); Assert.Equal(owner.Transport,ack.RpcTransportEpoch);
            Assert.Empty(c.messageQueue.ObjectMessagesQueue);
            // An incoming transport switch before assignment prunes this truthful reply as well.
            c.TransportFence.PublishIncoming(true,()=>{}); long next=c.TransportFence.Capture();
            c.messageQueue.PruneUnassignedWorldRpcs(owner.World,next);
            Assert.Empty(c.messageQueue.RPCMessagesQueue);
        }
        finally { Store.currentClient=previous; }
    }
    [Theory]
    [InlineData("client")][InlineData("data")][InlineData("queue")][InlineData("character")]
    [InlineData("world")][InlineData("life")][InlineData("district")][InlineData("offline")]
    [InlineData("dead")][InlineData("duel-dead")][InlineData("switching")][InlineData("shutdown")]
    [InlineData("disconnected")][InlineData("transport")]
    public void SwitchedOrUnavailableOwnerGetsNoRetryReply(string change)
    {
        var previous=Store.currentClient; var c=Client(); Store.currentClient=c;
        try
        {
            var owner=new MissionContainerRetry.Owner(c);
            switch(change)
            {
                case "client": Store.currentClient=Client(); break;
                case "data": c.playerData=new ClientData(); break;
                case "queue": c.messageQueue=new MessageQueue(); break;
                case "character": c.playerData.setCharID(111); break;
                case "world": c.playerData.worldGeneration++; break;
                case "life": c.playerData.lifeGeneration++; break;
                case "district": c.playerData.setDistrictId(2); break;
                case "offline": c.playerData.setOnWorld(false); break;
                case "dead": c.playerData.isDead=true; break;
                case "duel-dead": c.playerData.duelDeath=true; break;
                case "switching": c.playerData.worldChangePending=true; break;
                case "shutdown": c.playerData.waitForRPCShutDown=true; break;
                case "disconnected": c.Alive=false; break;
                case "transport": c.TransportFence.PublishIncoming(true,()=>{}); break;
            }
            Assert.False(MissionContainerRetry.Acknowledge(c,owner));
            Assert.Empty(c.messageQueue.RPCMessagesQueue); Assert.Empty(owner.Queue.RPCMessagesQueue);
        }
        finally { Store.currentClient=previous; }
    }
}
