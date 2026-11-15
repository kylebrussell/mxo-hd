using System.Collections.Generic;
namespace hds
{
    // Optional producer receipt. Exact transport delivery only, never renderer readiness.
    internal sealed class ObjectMessageReceipt
    {
        private readonly object sync=new object();
        private readonly Dictionary<ushort,long> dispatched=new Dictionary<ushort,long>();
        private bool received,cancelled;
        internal bool Received { get {lock(sync)return received&&!cancelled;} }
        internal bool Finished { get {lock(sync)return received||cancelled;} }
        internal void Dispatched(ushort sequence,long logical){lock(sync)if(!cancelled&&!received)dispatched[(ushort)(sequence&0xfff)]=logical;}
        internal void Advance(long logical)
        {
            lock(sync)
            {
                if(cancelled||received||dispatched.Count==0)return;
                var expired=new List<ushort>();
                foreach(var item in dispatched)if(logical-item.Value>=0x800)expired.Add(item.Key);
                foreach(var key in expired)dispatched.Remove(key);
                if(dispatched.Count==0)cancelled=true;
            }
        }
        internal void Observe(ISet<ushort> acknowledged){lock(sync)if(!cancelled&&!received&&System.Linq.Enumerable.Any(dispatched.Keys,acknowledged.Contains))received=true;}
        internal void Cancel(){lock(sync){cancelled=true;dispatched.Clear();}}
    }
}
