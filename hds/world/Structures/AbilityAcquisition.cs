using System;
using System.Collections.Generic;
using System.Linq;
namespace hds
{
    /// <summary>Explicit server acquisition policy; no training, loading or prerequisite qualification.</summary>
    public static class AbilityAcquisition
    {
        public sealed class BagRow { public readonly uint Id,Goid; public readonly int Slot,Count; public BagRow(uint id,int slot,uint goid,int count){Id=id;Slot=slot;Goid=goid;Count=count;} }
        public sealed class ArchiveRow { public readonly uint Id,Descriptor; public readonly int Slot; public ArchiveRow(uint id,int slot,uint descriptor){Id=id;Slot=slot;Descriptor=descriptor;} }
        public sealed class Receipt { public readonly ushort BagSlot,ArchiveSlot; public readonly uint Goid,Cash,Price; public readonly long BeforeCash; public Receipt(ushort bag,ushort archive,uint goid,long before,uint cash,uint price){BagSlot=bag;ArchiveSlot=archive;Goid=goid;BeforeCash=before;Cash=cash;Price=price;} }
        public sealed class Refusal : Exception { public Refusal(string text):base(text){} }
        public static bool Genuine(uint value) => (value & 0xfc000000u)==0x80000000u && AbilityCatalog.Find(Identity(value))!=null;
        public static ushort Identity(uint value) => AbilityCatalog.Canonical(AbilityCatalog.IdOf(value));
        public static ushort FreeBag(IEnumerable<BagRow> rows)
        {
            var used=rows.Where(r=>r.Slot>=1&&r.Slot<=96).Select(r=>r.Slot).ToArray();
            if(used.Distinct().Count()!=used.Length)throw new Refusal("Your bag has ambiguous slots; the purchase was refused.");
            for(ushort slot=1;slot<=96;slot++)if(!used.Contains(slot))return slot;
            throw new Refusal("Your inventory is full.");
        }
        public static uint BuyCash(long cash,uint price)
        {
            long next=cash-price;
            if(cash<0||next<0)throw new Refusal("You do not have enough information to buy this ability.");
            if(next>int.MaxValue)throw new Refusal("The resulting information balance cannot be represented.");
            return (uint)next;
        }
        public static ushort LearnSlot(IEnumerable<ArchiveRow> rows,uint goid,ushort? requested,IEnumerable<ushort> reserved=null)
        {
            if(!Genuine(goid))throw new Refusal("This item is not a recognized ability code.");
            var real=rows.Where(r=>r.Slot>=0&&r.Slot<520).ToArray();
            if(real.GroupBy(r=>r.Slot).Any(g=>g.Count()!=1))throw new Refusal("Your archive has ambiguous slots; the upload was refused.");
            if(real.Any(r=>(r.Descriptor&0xfc000000u)==0x80000000u&&Identity(r.Descriptor)==Identity(goid)))
                throw new Refusal("You already know this ability. Its code was kept in your bag.");
            var occupied=new HashSet<int>(real.Select(r=>r.Slot));
            if(reserved!=null)foreach(var slot in reserved)occupied.Add(slot);
            if(requested.HasValue)
            {
                if(requested.Value>=520||occupied.Contains(requested.Value))throw new Refusal("That archive slot is unavailable.");
                return requested.Value;
            }
            for(ushort slot=0;slot<520;slot++)if(!occupied.Contains(slot))return slot;
            throw new Refusal("Your archive is full. The ability code was kept in your bag.");
        }
    }
}
