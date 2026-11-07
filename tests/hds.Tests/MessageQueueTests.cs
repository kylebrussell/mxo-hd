using System.Collections;
using hds;

namespace hds.Tests;

public class MessageQueueTests
{
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
