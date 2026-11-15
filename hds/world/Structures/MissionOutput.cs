using System;
using System.Collections.Generic;

namespace hds
{
    /// <summary>Only the current mission operation's new output waits for its durable checkpoint.</summary>
    internal static class MissionOutput
    {
        internal sealed class Batch : IDisposable
        {
            internal readonly MessageQueue Queue;
            private readonly List<(byte[] Data,bool Object,bool Timed,ObjectMessageReceipt Receipt)> messages=new List<(byte[],bool,bool,ObjectMessageReceipt)>();
            private bool finished;
            private WorldSendFence fence;
            private long epoch;
            private int world;
            private bool bindingRejected;
            internal Batch(MessageQueue queue){Queue=queue;active=this;}
            internal void Add(byte[] data,bool objects,bool timed,ObjectMessageReceipt receipt)=>messages.Add((data,objects,timed,receipt));
            private void Publish((byte[] Data,bool Object,bool Timed,ObjectMessageReceipt Receipt) item)
            {
                if(item.Object){if(item.Receipt!=null)Queue.AddTrackedObject(item.Data,item.Receipt);else Queue.addObjectMessage(item.Data,item.Timed);}
                else Queue.addRpcMessage(item.Data);
            }
            private void CancelReceipts(){foreach(var item in messages)item.Receipt?.Cancel();}
            // Optional adoption-only publication lifetime. No owner callbacks or state/SQL work belong in this gate.
            internal bool Bind(MessageQueue queue,WorldSendFence transportFence,long capturedEpoch,int capturedWorld)
            {
                if(finished||bindingRejected)return false;
                if(!ReferenceEquals(queue,Queue)||transportFence==null||
                    (fence!=null&&(!ReferenceEquals(fence,transportFence)||epoch!=capturedEpoch||world!=capturedWorld)))
                {bindingRejected=true;return false;}
                fence=transportFence;epoch=capturedEpoch;world=capturedWorld;return true;
            }
            internal void Commit()
            {
                if(finished)return;finished=true;active=null;
                if(bindingRejected){CancelReceipts();return;}
                if(fence!=null)
                {
                    if(!fence.TryRun(epoch,()=>
                    {
                        foreach(var item in messages)
                            if(item.Object)Publish(item);
                            else Queue.AddWorldBoundRpc(item.Data,world,epoch);
                    }))CancelReceipts();
                    return;
                }
                foreach(var item in messages)
                    Publish(item);
            }
            public void Dispose(){if(!finished){finished=true;active=null;CancelReceipts();}}
        }
        [ThreadStatic] private static Batch active;
        internal static bool Buffered(MessageQueue queue)=>active?.Queue==queue;
        internal static bool BindCurrent(MessageQueue queue,WorldSendFence fence,long epoch,int world)=>active?.Bind(queue,fence,epoch,world)==true;
        internal static Batch Begin(MessageQueue queue)
        {
            if(active!=null)throw new InvalidOperationException("Nested mission output owner");
            return new Batch(queue);
        }
        internal static bool Stage(MessageQueue queue,byte[] bytes,bool objects,bool timed,ObjectMessageReceipt receipt=null)
        {
            if(!Buffered(queue))return false;
            active.Add(bytes,objects,timed,receipt);return true;
        }
    }
}
