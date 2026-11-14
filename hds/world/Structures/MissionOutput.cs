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
            private readonly List<(byte[] Data,bool Object,bool Timed)> messages=new List<(byte[],bool,bool)>();
            private bool finished;
            private WorldSendFence fence;
            private long epoch;
            private int world;
            private bool bindingRejected;
            internal Batch(MessageQueue queue){Queue=queue;active=this;}
            internal void Add(byte[] data,bool objects,bool timed)=>messages.Add((data,objects,timed));
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
                if(bindingRejected)return;
                if(fence!=null)
                {
                    fence.TryRun(epoch,()=>
                    {
                        foreach(var item in messages)
                            if(item.Object)Queue.addObjectMessage(item.Data,item.Timed);
                            else Queue.AddWorldBoundRpc(item.Data,world,epoch);
                    });
                    return;
                }
                foreach(var item in messages)
                    if(item.Object)Queue.addObjectMessage(item.Data,item.Timed);else Queue.addRpcMessage(item.Data);
            }
            public void Dispose(){if(!finished){finished=true;active=null;}}
        }
        [ThreadStatic] private static Batch active;
        internal static bool Buffered(MessageQueue queue)=>active?.Queue==queue;
        internal static bool BindCurrent(MessageQueue queue,WorldSendFence fence,long epoch,int world)=>active?.Bind(queue,fence,epoch,world)==true;
        internal static Batch Begin(MessageQueue queue)
        {
            if(active!=null)throw new InvalidOperationException("Nested mission output owner");
            return new Batch(queue);
        }
        internal static bool Stage(MessageQueue queue,byte[] bytes,bool objects,bool timed)
        {
            if(!Buffered(queue))return false;
            active.Add(bytes,objects,timed);return true;
        }
    }
}
