using System.Collections.Generic;
namespace hds
{
    // Optional producer receipt. Exact transport delivery only, never renderer readiness.
    internal sealed class ObjectMessageReceipt
    {
        private readonly object sync=new object();
        private readonly HashSet<ushort> dispatched=new HashSet<ushort>();
        private bool received,cancelled;
        internal bool Received { get {lock(sync)return received&&!cancelled;} }
        internal bool Finished { get {lock(sync)return received||cancelled;} }
        internal void Dispatched(ushort sequence){lock(sync)if(!cancelled&&!received)dispatched.Add((ushort)(sequence&0xfff));}
        internal void Observe(ISet<ushort> acknowledged){lock(sync)if(!cancelled&&!received&&dispatched.Overlaps(acknowledged))received=true;}
        internal void Cancel(){lock(sync){cancelled=true;dispatched.Clear();}}
    }
}
