using hds;
namespace hds.Tests;

public class MissionOutputBoundTests
{
    [Fact]
    public void BindCurrentRequiresAnActiveOwnerAndDoesNotExposeTheBatchOrRunCallbacks()
    {
        var queue=new MessageQueue();var fence=new WorldSendFence();
        Assert.False(MissionOutput.BindCurrent(queue,fence,0,3));
        using(var batch=MissionOutput.Begin(queue))
        {
            Assert.True(MissionOutput.BindCurrent(queue,fence,0,3));
            queue.addRpcMessage(new byte[]{0x80,0xa0});batch.Commit();
        }
        Assert.False(MissionOutput.BindCurrent(queue,fence,0,3));
        var rpc=Assert.IsType<SequencedMessage>(Assert.Single(queue.RPCMessagesQueue));
        Assert.Equal(3,rpc.RpcWorldGeneration);Assert.Equal(-1,rpc.rpcNumber);
    }
    [Fact]
    public async Task ResetBetweenStagingAndCommitDropsTheWholeOldBatchBeforeAnyCounterAssignment()
    {
        var queue=new MessageQueue();var fence=new WorldSendFence();
        using var staged=new ManualResetEventSlim();using var release=new ManualResetEventSlim();
        var producer=Task.Run(()=>
        {
            using var batch=MissionOutput.Begin(queue);
            Assert.True(batch.Bind(queue,fence,fence.Capture(),7));
            queue.addRpcMessage(new byte[]{0x80,0xa0});queue.addObjectMessage(new byte[]{1,2,3},true);
            staged.Set();Assert.True(release.Wait(TimeSpan.FromSeconds(5)));
            batch.Commit();Assert.False(MissionOutput.Buffered(queue));
        });
        try
        {
            Assert.True(staged.Wait(TimeSpan.FromSeconds(5)));Assert.Empty(queue.RPCMessagesQueue);Assert.Empty(queue.ObjectMessagesQueue);
            fence.PublishIncoming(true,()=>{});long current=fence.Capture();
            Assert.True(fence.ApplyReset(current,queue.ForgetWorldObjects));fence.Resume(current);
            release.Set();await producer;
            Assert.Empty(queue.RPCMessagesQueue);Assert.Empty(queue.ObjectMessagesQueue);
        }
        finally{release.Set();}
    }
    [Fact]
    public void PendingResetRejectsEvenItsCurrentEpochAndClearsTheBatchOwner()
    {
        var queue=new MessageQueue();var fence=new WorldSendFence();fence.PublishIncoming(true,()=>{});
        using(var batch=MissionOutput.Begin(queue))
        {
            Assert.True(batch.Bind(queue,fence,fence.Capture(),9));
            queue.addRpcMessage(new byte[]{1});queue.addObjectMessage(new byte[]{2},false);batch.Commit();
        }
        Assert.Empty(queue.RPCMessagesQueue);Assert.Empty(queue.ObjectMessagesQueue);Assert.False(MissionOutput.Buffered(queue));
        using var next=MissionOutput.Begin(queue); // rejected Commit must not leave a nested ThreadStatic owner
    }
    [Fact]
    public void ResumedNewWorldTagsRpcsPreservesObjectsAndDoesNotAssignReliableCounters()
    {
        var queue=new MessageQueue();var fence=new WorldSendFence();fence.PublishIncoming(true,()=>{});long epoch=fence.Capture();
        Assert.True(fence.ApplyReset(epoch,queue.ForgetWorldObjects));fence.Resume(epoch);
        using(var batch=MissionOutput.Begin(queue))
        {
            Assert.True(batch.Bind(queue,fence,epoch,12));Assert.True(batch.Bind(queue,fence,epoch,12));
            queue.addRpcMessage(new byte[]{0x80,0xa0});queue.addObjectMessage(new byte[]{9,8},true);batch.Commit();batch.Commit();
            Assert.False(batch.Bind(queue,fence,epoch,12));
        }
        var rpc=Assert.IsType<SequencedMessage>(Assert.Single(queue.RPCMessagesQueue));
        Assert.Equal(12,rpc.RpcWorldGeneration);Assert.Equal(epoch,rpc.RpcTransportEpoch);Assert.Equal(-1,rpc.rpcNumber);
        var obj=Assert.IsType<SequencedMessage>(Assert.Single(queue.ObjectMessagesQueue));
        Assert.Equal(new byte[]{9,8},obj.content);Assert.True(obj.isTimed);Assert.Equal((ushort)0,obj.sendCounter);Assert.False(obj.isSent);
        queue.PruneUnassignedWorldRpcs(13,epoch+1);Assert.Empty(queue.RPCMessagesQueue); // stale RPC consumes no number
    }
    [Theory][InlineData(0)][InlineData(1)][InlineData(2)][InlineData(3)][InlineData(4)]
    public void ConflictingBindingPoisonsTheBatchInsteadOfFallingBackToLegacyPublication(int conflict)
    {
        var queue=new MessageQueue();var fence=new WorldSendFence();long epoch=fence.Capture();
        using(var batch=MissionOutput.Begin(queue))
        {
            Assert.True(batch.Bind(queue,fence,epoch,4));
            bool accepted=conflict switch
            {
                0=>batch.Bind(new MessageQueue(),fence,epoch,4),
                1=>batch.Bind(queue,new WorldSendFence(),epoch,4),
                2=>batch.Bind(queue,fence,epoch+1,4),
                3=>batch.Bind(queue,fence,epoch,5),
                _=>batch.Bind(queue,null!,epoch,4)
            };
            Assert.False(accepted);Assert.False(batch.Bind(queue,fence,epoch,4));
            queue.addRpcMessage(new byte[]{1});queue.addObjectMessage(new byte[]{2},false);batch.Commit();
        }
        Assert.Empty(queue.RPCMessagesQueue);Assert.Empty(queue.ObjectMessagesQueue);Assert.False(MissionOutput.Buffered(queue));
    }
    [Fact]
    public void InvalidInitialBindingCannotAccidentallyPublishAsAnUnboundBatch()
    {
        var queue=new MessageQueue();using(var batch=MissionOutput.Begin(queue))
        {Assert.False(batch.Bind(queue,null!,0,1));queue.addRpcMessage(new byte[]{1});batch.Commit();}
        Assert.Empty(queue.RPCMessagesQueue);
    }
    [Fact]
    public void LegacyUnboundBatchKeepsExactBytesUntypedLifetimeAndIndependentImmediateQueues()
    {
        var queue=new MessageQueue();var other=new MessageQueue();var unrelatedFence=new WorldSendFence();
        using(var batch=MissionOutput.Begin(queue))
        {
            queue.addRpcMessage(new byte[]{0x80,0xa0});queue.addObjectMessage(new byte[]{1,2,3},false);other.addRpcMessage(new byte[]{4});
            unrelatedFence.PublishIncoming(true,()=>{});batch.Commit();
        }
        var rpc=Assert.IsType<SequencedMessage>(Assert.Single(queue.RPCMessagesQueue));Assert.Null(rpc.RpcWorldGeneration);
        Assert.Equal(new byte[]{2,0x80,0xa0},rpc.content);Assert.Equal(-1,rpc.rpcNumber);
        var obj=Assert.IsType<SequencedMessage>(Assert.Single(queue.ObjectMessagesQueue));Assert.Equal(new byte[]{1,2,3},obj.content);Assert.False(obj.isTimed);
        Assert.Single(other.RPCMessagesQueue);
    }
    [Fact]
    public void DisposingBoundBatchBeforeCommitStillDiscardsItsOutput()
    {
        var queue=new MessageQueue();var fence=new WorldSendFence();
        using(var batch=MissionOutput.Begin(queue)){Assert.True(batch.Bind(queue,fence,0,1));queue.addRpcMessage(new byte[]{1});}
        Assert.Empty(queue.RPCMessagesQueue);Assert.False(MissionOutput.Buffered(queue));
    }
}
