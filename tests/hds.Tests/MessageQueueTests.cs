using System.Collections;
using hds;

namespace hds.Tests;

public class MessageQueueTests
{
    [Fact]
    public void WorldResetDropsSentAndUnsentOldViewsButKeepsTheNewWorldAndRpcPolicy()
    {
        var queue = new MessageQueue();
        queue.addObjectMessage(new byte[] { 1, 0, 8, 0xa3, 0xb8 }, false);
        queue.addObjectMessage(new byte[] { 2, 0, 2, 1 }, true);
        var sent = (SequencedMessage)queue.ObjectMessagesQueue[0]!;
        sent.MarkObjectSent(1);
        queue.addRpcMessage(new byte[] { 0x81, 8 });
        queue.addRpcMessage(new byte[] { 6, 0 });
        ((SequencedMessage)queue.RPCMessagesQueue[0]!).rpcNumber = 12;

        queue.ForgetSentRpcs();
        queue.ForgetWorldObjects();
        Assert.Empty(queue.ObjectMessagesQueue);
        Assert.Single(queue.RPCMessagesQueue);
        Assert.Equal(new byte[] { 2, 6, 0 }, ((SequencedMessage)queue.RPCMessagesQueue[0]!).content);

        byte[] nextWorld = { 2, 0, 1, 8, 42 };
        queue.addObjectMessage(nextWorld, false);
        Assert.Same(nextWorld, ((SequencedMessage)Assert.Single(queue.ObjectMessagesQueue)!).content);
    }

    [Fact]
    public void AddingRpcsWhileTheFlushEnumeratesDoesNotThrow()
    {
        // The flush thread enumerates RPCMessagesQueue under its SyncRoot while packet handlers add RPCs.
        // An unlocked Add crashed the server with "Collection was modified" during a login.
        MessageQueue queue = new MessageQueue();
        Exception failure = null;
        using CancellationTokenSource stop = new CancellationTokenSource();

        Thread reader = new Thread(() =>
        {
            try
            {
                while (!stop.IsCancellationRequested)
                {
                    lock (queue.RPCMessagesQueue.SyncRoot)
                    {
                        foreach (object _ in queue.RPCMessagesQueue)
                        {
                        }
                    }
                }
            }
            catch (Exception e)
            {
                failure = e;
            }
        });
        reader.Start();

        for (int i = 0; i < 3000; i++)
        {
            queue.addRpcMessage(new byte[] { 0x80, 0xc9, (byte)i });
        }

        stop.Cancel();
        reader.Join();

        Assert.Null(failure);
        Assert.Equal(3000, queue.RPCMessagesQueue.Count);
    }
}
