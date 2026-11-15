using System;
using System.Collections.Generic;
using System.Linq;
using hds.shared;
namespace hds
{
    //Only80b5 allocation: existing base/archetype policy, sourced point budget/cap and committed replies.
    public static class AttributeAllocation
    {
        public readonly struct Row
        {
            public readonly uint Identity; public readonly int Slot; public readonly long Descriptor,Value;
            public Row(uint identity,int slot,long descriptor,long value){Identity=identity;Slot=slot;Descriptor=descriptor;Value=value;}
        }
        public sealed class Result
        {
            public bool Committed {get;}
            public string Reason {get;}
            public uint? Trait {get;}
            public IReadOnlyDictionary<byte,ushort> Values {get;}
            public Result(bool committed,string reason,IDictionary<byte,ushort> values=null,uint? trait=null)
            {Committed=committed;Reason=reason;Trait=trait;Values=new System.Collections.ObjectModel.ReadOnlyDictionary<byte,ushort>(new Dictionary<byte,ushort>(values??new Dictionary<byte,ushort>()));}
        }
        public static bool Known(uint attribute)=>attribute<=byte.MaxValue && Array.IndexOf(CharacterAttributes.All,(byte)attribute)>=0;
        public static bool Matches(Row row,byte attribute)=>Known(attribute) && row.Slot==CharacterAttributes.SlotOf(attribute) &&
            row.Descriptor>=int.MinValue && row.Descriptor<=uint.MaxValue && (unchecked((uint)row.Descriptor)&0xfc000000u)==0x80000000u &&
            AbilityCatalog.Canonical(AbilityCatalog.IdOf(unchecked((uint)row.Descriptor)))==attribute;
        //Display readers retain existing last-row policy for corrupt duplicates; allocation refuses ambiguity before mutation.
        public static Dictionary<byte,ushort> ReadValues(IEnumerable<Row> rows)
        {
            var values=new Dictionary<byte,ushort>();
            foreach(var row in rows)
                foreach(byte attribute in CharacterAttributes.All)
                    if(Matches(row,attribute))values[attribute]=checked((ushort)row.Value);
            return values;
        }
        public static bool TryPlan(IEnumerable<Row> source,IReadOnlyList<CharacterAttributes.Raise> raises,long experience,out Dictionary<byte,ushort> values,uint? trait=null)
        {
            values=new Dictionary<byte,ushort>();var rows=source.ToArray();
            if(trait.HasValue&&!CharacterPersonality.Valid(trait.Value))return false;
            if(raises==null||raises.Count<1||raises.Count>5||experience<0||experience>uint.MaxValue)return false;
            foreach(byte attribute in CharacterAttributes.All)
            {
                var owned=rows.Where(r=>r.Slot==CharacterAttributes.SlotOf(attribute)).ToArray();
                if((trait.HasValue&&owned.Length!=1)||owned.Length>1 || owned.Any(r=>!Matches(r,attribute)||r.Value<0||r.Value>CharacterAttributes.Cap))return false;
                values[attribute]=owned.Length==1?(ushort)owned[0].Value:CharacterAttributes.LegacyValue(attribute);
            }
            int spent=values.Sum(v=>CharacterAttributes.SpentPoints(v.Key,v.Value,trait));
            var seen=new HashSet<byte>();int requested=0;
            foreach(var raise in raises)
            {
                if(!Known(raise.Attribute)||raise.Points==0||!seen.Add(raise.Attribute))return false;
                int next=values[raise.Attribute]+raise.Points;if(next>CharacterAttributes.Cap)return false;
                values[raise.Attribute]=(ushort)next;requested+=raise.Points;
            }
            int level=PlayerProgression.LevelForExperience(1,experience);
            return spent+requested<=Math.Max(level-1,0);
        }
        //Independently proved attribute consumer621e8c30/621e8fbc; no global status change.
        public const uint FailureStatus=0x1800000c;
        public static byte[] Reply(Result result,IReadOnlyList<CharacterAttributes.Raise> raises)
        {
            var packet=new PacketContent();packet.AddHexBytes("80b6");packet.AddUint16(8,1);
            packet.AddUint32(result.Committed?0:FailureStatus,1);packet.AddUint16(result.Committed?(ushort)raises.Count:(ushort)0,1);
            if(result.Committed)foreach(var raise in raises){packet.AddByte(0);packet.AddUint32(raise.Attribute,1);packet.AddByte(CharacterAttributes.NewLevelOf(raise.Attribute,result.Values[raise.Attribute],result.Trait));}
            return packet.ReturnFinalPacket();
        }
        internal static bool Current(WorldClient c,ClientData d,int generation,int world,int life,long transport,uint character)=>
            ReferenceEquals(c.playerData,d)&&d.getCharID()==character&&d.attributeGeneration==generation&&d.worldGeneration==world&&d.lifeGeneration==life&&
            c.Alive&&c.TransportFence.Capture()==transport&&!d.worldChangePending&&!d.waitForRPCShutDown;
        internal static void Finish(WorldClient client,long transport,int world,Func<bool> current,Result result,IReadOnlyList<CharacterAttributes.Raise> raises,Action onSuccess,Action flush)
        {
            if(!current())return;
            byte[] modal=result.Committed?null:ServerPackets.BuildSystemChatMessage(result.Reason,"MODAL");
            byte[] reply=Reply(result,raises);bool published=false;
            //The database transaction is disposed; no callback runs inside this short packet-publication fence.
            client.TransportFence.TryRun(transport,()=>
            {
                if(!current())return;
                if(modal!=null)client.messageQueue.AddWorldBoundRpc(modal,world,transport);
                client.messageQueue.AddWorldBoundRpc(reply,world,transport);published=true;
            });
            //Tutorial may flush/reward. Its callback follows the committed reply, outside the transport fence.
            if(published&&result.Committed&&current())onSuccess();
            if(published&&current())flush();
        }
        public static void Handle(WorldClient client,byte[] body)
        {
            var raises=CharacterAttributes.ParseRaises(body);if(raises.Count==0)return;
            var data=client.playerData;
            lock(data.attributeSync)
            {
                uint character=data.getCharID();int generation=++data.attributeGeneration,world=data.worldGeneration,life=data.lifeGeneration;
                long transport=client.TransportFence.Capture();Result result;
                try{result=Store.dbManager.WorldDbHandler.AllocateAttributes(character,raises);}
                catch(Exception e){Output.WriteDebugLog("[ATTRIBUTE] char="+character+" allocation failed: "+e);result=new Result(false,"Attribute allocation could not be saved; nothing was changed.");}
                Finish(client,transport,world,()=>Current(client,data,generation,world,life,transport,character),result,raises,
                    ()=>TutorialMission.Of(client)?.OnAttributeRaised(),()=>client.FlushQueue());
            }
        }
    }
}
