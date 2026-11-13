using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using hds.shared;

namespace hds
{
    /// <summary>OURS last-sale receipt policy. The native request supplies no item identity.</summary>
    public static class VendorBuyback
    {
        public sealed class Item
        {
            public uint OriginalId { get; }
            public long RawGoid { get; }
            public int RawCount { get; }
            public int Purity { get; }
            public ushort Slot { get; }
            public string Created { get; }
            public string Updated { get; }
            public Item(uint id,long goid,int count,int purity,ushort slot,string created="",string updated="")
            {OriginalId=id;RawGoid=goid;RawCount=count;Purity=purity;Slot=slot;Created=created;Updated=updated;}
        }
        public sealed class Result
        {
            public bool Committed { get; }
            public Item Entry { get; }
            public uint Cash { get; }
            public uint Paid { get; }
            public string Refusal { get; }
            public Result(bool committed,Item entry,uint cash,uint paid,string refusal=null)
            {Committed=committed;Entry=entry;Cash=cash;Paid=paid;Refusal=refusal;}
            public static Result Denied(string reason)=>new Result(false,null,0,0,reason);
        }
        private sealed class Session { public readonly string Token=Guid.NewGuid().ToString("N"); public readonly int World; public readonly uint Character; public Session(int world,uint character){World=world;Character=character;} }
        private static readonly ConditionalWeakTable<ClientData,Session> sessions=new ConditionalWeakTable<ClientData,Session>();
        private static readonly object sessionSync=new object();
        internal static string SessionFor(ClientData data)
        {
            lock(sessionSync)
            {
                if(sessions.TryGetValue(data,out var s)&&s.World==data.worldGeneration&&s.Character==data.getCharID())return s.Token;
                sessions.Remove(data);s=new Session(data.worldGeneration,data.getCharID());sessions.Add(data,s);return s.Token;
            }
        }
        public static bool TryBuyback(byte[] body)=>body!=null&&body.Length==5&&body[4]==1&&
            body[0]==0&&body[1]==0&&body[2]==0&&body[3]==0;
        public static bool TryDescriptor(Item item,out uint goid,out byte[] descriptor)
        {
            goid=0;descriptor=null;
            if(item==null||item.RawGoid<int.MinValue||item.RawGoid>uint.MaxValue||item.RawCount<0||item.RawCount>ItemStacks.MaxStack)return false;
            goid=unchecked((uint)item.RawGoid);if(goid==0)return false;
            if(AbilityProgression.IsAbilityCode(goid))
                descriptor=NumericalUtils.uint32ToByteArray(AbilityProgression.AbilityCodeValue(goid),1);
            else descriptor=new byte[]{0,0,ItemStacks.AmountByte(item.RawCount),0x10};
            return true;
        }
        internal static uint WireClass(uint goid)=>AbilityProgression.IsAbilityCode(goid)?AbilityProgression.AbilityCodeItem:goid;
        internal static byte[] BuyAck(uint goid,byte[] descriptor,uint paid)
        {
            var packet=ServerPackets.BuildVendorBuyAck(WireClass(goid),descriptor,paid);packet[^1]=1;return packet;
        }
        internal static byte[] RefusalAck()=>new byte[]{0x81,0x10,0,0,0,0,0};
        internal static byte[][] Packets(Result result,bool buy)
        {
            if(!result.Committed)return new[]{ServerPackets.BuildSystemChatMessage(result.Refusal,"MODAL"),RefusalAck()};
            if(!TryDescriptor(result.Entry,out uint goid,out var descriptor))throw new InvalidOperationException("Committed receipt has no lossless wire descriptor");
            uint reason=buy?ServerPackets.InfoReasonVendorBuy:ServerPackets.InfoReasonVendorSell;
            var packets=new List<byte[]>();
            if(buy)
            {
                var add=new PacketContent();add.AddByte(0x5e);add.AddUint16(result.Entry.Slot,1);
                add.AddUint32(WireClass(goid),1);add.AddByteArray(descriptor);add.AddByte(1);packets.Add(add.ReturnFinalPacket());
            }
            packets.Add(ServerPackets.BuildInfoDelta(buy?-(long)result.Paid:result.Paid,reason));
            packets.Add(ServerPackets.BuildInfoCurrent(result.Cash,reason));
            if(buy)packets.Add(BuyAck(goid,descriptor,result.Paid));
            else
            {
                packets.Add(ServerPackets.BuildInventoryItemDelete(result.Entry.Slot));
                packets.Add(ServerPackets.BuildVendorSellAck(WireClass(goid),descriptor,result.Paid));
            }
            return packets.ToArray();
        }
        internal sealed class Publication
        {
            internal readonly ClientData Data;internal readonly uint Character;internal readonly int World,Life;
            internal readonly long Epoch;internal readonly Vendor Vendor;
            internal Publication(WorldClient client)
            {Data=client.playerData;Character=Data.getCharID();World=Data.worldGeneration;Life=Data.lifeGeneration;Epoch=client.TransportFence.Capture();Vendor=Data.activeVendor;}
            internal bool Current(WorldClient client)=>ReferenceEquals(client.playerData,Data)&&Data.getCharID()==Character&&
                Data.worldGeneration==World&&Data.lifeGeneration==Life&&client.TransportFence.Capture()==Epoch&&
                client.Alive&&!Data.waitForRPCShutDown&&!Data.worldChangePending&&Data.getOnWorld()&&
                ReferenceEquals(Data.activeVendor,Vendor)&&Vendor!=null&&Vendor.metrId==Data.getDistrictId()&&Data.activeVendorGeneration==World;
            internal bool Publish(WorldClient client,Result result,byte[][] packets)
            {
                bool published=false;
                client.TransportFence.TryRun(Epoch,()=>
                {
                    if(!Current(client))return;
                    if(result.Committed)Data.setInfo(result.Cash);
                    foreach(var packet in packets)client.messageQueue.AddWorldBoundRpc(packet,World,Epoch);
                    published=true;
                });
                if(published)client.FlushQueue();return published;
            }
        }
        internal static void Handle(WorldClient client,bool buy,ushort slot=0)
        {
            var data=client.playerData;
            lock(data.craftingSync)
            {
                var stamp=new Publication(client);if(!stamp.Current(client))return;
                string session=SessionFor(data);Result result;
                try
                {
                    result=buy?Store.dbManager.WorldDbHandler.BuyBackVendorItem(stamp.Character,session,stamp.Vendor.vendorStaticID):
                        Store.dbManager.WorldDbHandler.SellVendorItem(stamp.Character,slot,session,stamp.Vendor.vendorStaticID);
                }
                catch(Exception e)
                {
                    Output.WriteDebugLog("[BUYBACK] Transaction rolled back: "+e);
                    result=Result.Denied(buy?"The buyback could not be completed. Your item, information and last-sale receipt were kept.":"The sale could not be completed. Your item, information and last-sale receipt were kept.");
                }
                if(result.Committed)Output.WriteDebugLog("[BUYBACK] "+(buy?"restored":"sold")+" char="+stamp.Character+
                    " vendor="+stamp.Vendor.vendorStaticID+" originalId="+result.Entry.OriginalId+" rawGoid="+result.Entry.RawGoid+
                    " rawCount="+result.Entry.RawCount+" purity="+result.Entry.Purity+" originalCreated="+result.Entry.Created+
                    " originalUpdated="+result.Entry.Updated+" paid="+result.Paid+" slot="+result.Entry.Slot);
                var packets=Packets(result,buy);stamp.Publish(client,result,packets);
            }
        }
    }
}
