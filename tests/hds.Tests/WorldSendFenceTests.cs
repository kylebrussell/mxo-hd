using hds;
namespace hds.Tests;

public class WorldSendFenceTests
{
    [Fact]
    public async Task PreparedOldWorldBatchCannotUseResetCountersOrDispatch()
    {
        var fence=new WorldSendFence();var queue=new MessageQueue();var data=new ClientData();
        queue.addObjectMessage(new byte[]{1,0,8,0xa3,0xb8},false);
        using var prepared=new ManualResetEventSlim();using var resume=new ManualResetEventSlim();
        int dispatched=0;WorldPacket? oldPacket=null;
        var flush=Task.Run(()=>
        {
            long epoch=fence.Capture();
            Assert.True(fence.TryRun(epoch,()=>
            {
                oldPacket=new WorldPacket(data);
                var message=(SequencedMessage)queue.ObjectMessagesQueue[0]!;
                message.MarkObjectSent(1);oldPacket.addObjectContent(message);
            }));
            prepared.Set();Assert.True(resume.Wait(TimeSpan.FromSeconds(5)));
            return fence.TryRun(epoch,()=>{oldPacket!.getFinalData(data);dispatched++;});
        });
        try
        {
            Assert.True(prepared.Wait(TimeSpan.FromSeconds(5)));
            fence.PublishIncoming(true,()=>data.setPss(0));
            long reset=fence.Capture();
            Assert.True(fence.ApplyReset(reset,()=>{data.setSseq(0);queue.ForgetWorldObjects();}));
            fence.Resume(reset);resume.Set();
            Assert.False(await flush);Assert.Equal(0,dispatched);Assert.Equal((ushort)0,data.getSseq());
        }
        finally { resume.Set(); }
    }

    [Fact]
    public async Task FlushCapturedBeforeResetCannotConsumeOrAckTheNewWorldCreation()
    {
        var fence=new WorldSendFence();var queue=new MessageQueue();
        using var captured=new ManualResetEventSlim();using var resume=new ManualResetEventSlim();
        var flush=Task.Run(()=>
        {
            long epoch=fence.Capture();captured.Set();Assert.True(resume.Wait(TimeSpan.FromSeconds(5)));
            return fence.TryRun(epoch,()=>((SequencedMessage)queue.ObjectMessagesQueue[0]!).MarkObjectSent(1));
        });
        try
        {
            Assert.True(captured.Wait(TimeSpan.FromSeconds(5)));
            fence.PublishIncoming(true,()=>{});long reset=fence.Capture();
            Assert.True(fence.ApplyReset(reset,queue.ForgetWorldObjects));
            queue.addObjectMessage(new byte[]{2,0,1,8,42},false);
            var next=(SequencedMessage)queue.ObjectMessagesQueue[0]!;
            fence.Resume(reset);resume.Set();Assert.False(await flush);
            Assert.Equal((ushort)0,next.sendCounter);Assert.False(next.IsAckedBy(0));Assert.True(next.IsObjectSendDue(10));
            Assert.True(fence.TryRun(reset,()=>next.MarkObjectSent(10)));
            Assert.Equal((ushort)1,next.sendCounter);
        }
        finally { resume.Set(); }
    }

    [Fact]
    public void HeaderPublicationSuspendsOutgoingPreparationUntilMatchingResetCompletes()
    {
        var fence=new WorldSendFence();var queue=new MessageQueue();bool published=false;
        long old=fence.Capture();fence.PublishIncoming(true,()=>published=true);long current=fence.Capture();
        Assert.True(published);Assert.NotEqual(old,current);
        queue.addObjectMessage(new byte[]{2,0,1,8,42},false);
        Assert.False(fence.TryRun(current,()=>throw new Exception("pending setup consumed")));
        fence.Resume(old);Assert.False(fence.TryRun(current,()=>{}));
        Assert.True(fence.ApplyReset(current,()=>{}));fence.Resume(current);
        Assert.True(fence.TryRun(current,()=>{}));
        fence.PublishIncoming(false,()=>published=false);
        Assert.False(published);Assert.Equal(current,fence.Capture());
        Assert.True(fence.TryRun(current,()=>{}));
    }
}
