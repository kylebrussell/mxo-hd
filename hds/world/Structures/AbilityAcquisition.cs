using System;
using System.Collections.Generic;
using System.Linq;
namespace hds
{
    /// <summary>Explicit server acquisition policy; no training, loading or prerequisite qualification.</summary>
    public static class AbilityAcquisition
    {
        public sealed class BagRow { public readonly uint Id; public readonly long Goid; public readonly int Slot,Count; public BagRow(uint id,int slot,long goid,int count){Id=id;Slot=slot;Goid=goid;Count=count;} }
        public sealed class ArchiveRow { public readonly uint Id; public readonly long Descriptor; public readonly int Slot; public ArchiveRow(uint id,int slot,long descriptor){Id=id;Slot=slot;Descriptor=descriptor;} }
        public sealed class Receipt { public readonly ushort BagSlot,ArchiveSlot; public readonly uint Goid,Cash,Price; public readonly long BeforeCash; public Receipt(ushort bag,ushort archive,uint goid,long before,uint cash,uint price){BagSlot=bag;ArchiveSlot=archive;Goid=goid;BeforeCash=before;Cash=cash;Price=price;} }
        public sealed class Refusal : Exception { public Refusal(string text):base(text){} }
        public static bool TryDescriptor(long raw,out uint value)
        {
            value=0;if(raw<int.MinValue||raw>uint.MaxValue)return false;
            value=unchecked((uint)raw);return true;
        }
        public static bool Genuine(uint value) => (value & 0xfc000000u)==0x80000000u &&
            AbilityCatalog.Find(Identity(value))?.Number("Ability/AbilityClass",-1)==0;
        // Bulk Upload considers occupied ability-code bag rows only. The transaction still
        // revalidates the exact row, descriptor and count and reports real races/duplicates.
        internal static bool UploadCandidate(Crafting.InventoryItem item) =>
            item.Slot>=1&&item.Slot<=96&&item.Count>0&&Genuine(item.Goid);
        // The legacy schema/writers store zero for one item; keep raw identity for CAS.
        internal static bool RawSingleCount(int count) => count==0||count==1;
        public static bool SameAbility(long raw,uint expected) => TryDescriptor(raw,out uint value)&&Genuine(value)&&Identity(value)==Identity(expected);
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
            if(real.Any(r=>SameAbility(r.Descriptor,goid)))
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
        internal sealed class Publication
        {
            internal readonly ClientData Data;internal readonly uint Character,District;internal readonly int World,Life;
            internal readonly long Transport;private readonly bool onWorld,la;
            internal Publication(WorldClient client)
            {
                Data=client.playerData;Character=Data.getCharID();World=Data.worldGeneration;Life=Data.lifeGeneration;
                District=Data.getDistrictId();onWorld=Data.getOnWorld();la=Data.DistrictIsLA();
                Transport=client.TransportFence.Capture();
            }
            private bool Current(WorldClient client)=>client.Alive&&!Data.waitForRPCShutDown&&!Data.worldChangePending&&ReferenceEquals(Data,client.playerData)&&Data.getCharID()==Character&&
                Data.worldGeneration==World&&Data.lifeGeneration==Life&&Data.getDistrictId()==District&&
                Data.getOnWorld()==onWorld&&Data.DistrictIsLA()==la;
            internal bool Publish(WorldClient client,params byte[][] packets)=>PublishPackets(client,null,packets);
            internal bool PublishPurchase(WorldClient client,uint cash,params byte[][] packets)=>PublishPackets(client,cash,packets);
            private bool PublishPackets(WorldClient client,uint? cash,byte[][] packets)
            {
                bool sent=false;
                client.TransportFence.TryRun(Transport,()=>
                {
                    if(!Current(client))return;
                    if(cash.HasValue)Data.setInfo(cash.Value);
                    foreach(var packet in packets)client.messageQueue.AddWorldBoundRpc(packet,World,Transport);
                    sent=true;
                });
                return sent;
            }
            internal void Notice(WorldClient client,string text)
            {
                byte[] packet=ServerPackets.BuildSystemChatMessage(text,"SYSTEM");
                if(Publish(client,packet))client.FlushQueue(); // Outside the transport fence.
            }
        }
    }
}
