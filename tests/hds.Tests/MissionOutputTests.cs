using hds;

namespace hds.Tests;

public class MissionOutputTests
{
    [Fact]
    public void CommitPublishesOnlyAfterCheckpointAndLeavesOtherQueuesImmediate()
    {
        var q=new MessageQueue();var other=new MessageQueue();
        using(var batch=MissionOutput.Begin(q))
        {
            q.addRpcMessage(new byte[]{0x80,0xa0});q.addObjectMessage(new byte[]{1,2,3},false);
            other.addRpcMessage(new byte[]{4});
            Assert.Empty(q.RPCMessagesQueue);Assert.Empty(q.ObjectMessagesQueue);Assert.Single(other.RPCMessagesQueue);
            Assert.True(MissionOutput.Buffered(q));Assert.False(MissionOutput.Buffered(other));
            batch.Commit();
        }
        Assert.Single(q.RPCMessagesQueue);Assert.Single(q.ObjectMessagesQueue);Assert.False(MissionOutput.Buffered(q));
    }
    [Fact]
    public void FailedOperationDiscardsItsUnpublishedGoalAndItemMessages()
    {
        var q=new MessageQueue();q.addRpcMessage(new byte[]{1});
        using(MissionOutput.Begin(q)){q.addRpcMessage(new byte[]{0x80,0xa0});q.addRpcMessage(new byte[]{0x5e});q.addObjectMessage(new byte[]{2},false);}
        Assert.Single(q.RPCMessagesQueue);Assert.Empty(q.ObjectMessagesQueue);
        q.addRpcMessage(new byte[]{3});Assert.Equal(2,q.RPCMessagesQueue.Count);
    }
}
