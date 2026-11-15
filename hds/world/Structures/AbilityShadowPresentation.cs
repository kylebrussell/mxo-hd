using System;
using System.Collections.Generic;
using System.Linq;
namespace hds
{
    // Native-presentation candidate only. These snapshots never grant archive ownership or affect memory/levels.
    internal static class AbilityShadowPresentation
    {
        internal const ushort Parent=290,Child=1117;
        internal static bool Qualifies(AbilityCatalog.Entry parent)=>parent?.Id==Parent&&AbilityGrantRegistration.ZeroGrant(parent,Child);
        internal readonly struct Base : IEquatable<Base>
        {
            internal readonly ushort Level,Slot;
            internal Base(ushort level,ushort slot){Level=level;Slot=slot;}
            public bool Equals(Base other)=>Level==other.Level&&Slot==other.Slot;
            public override bool Equals(object value)=>value is Base other&&Equals(other);
            public override int GetHashCode()=>HashCode.Combine(Level,Slot);
        }
        internal readonly struct Owned
        {
            internal readonly Base? Value;internal readonly bool Ambiguous;
            private Owned(Base? value,bool ambiguous){Value=value;Ambiguous=ambiguous;}
            internal static Owned From(IEnumerable<Base> rows)
            {
                var values=rows.ToArray();return values.Length==0?default:values.Length==1?new Owned(values[0],false):new Owned(null,true);
            }
            internal Owned Retain(IEnumerable<(ushort Ability,ushort Level)> rows)
            {
                var child=rows.Where(r=>AbilityCatalog.Canonical(r.Ability)==Child).ToArray();
                if(child.Length==0)return default;
                // Training's committed tuples can update a known actual slot; they cannot invent a missing slot.
                return child.Length==1&&Value.HasValue&&!Ambiguous?new Owned(new Base(child[0].Level,Value.Value.Slot),false):new Owned(null,true);
            }
        }
        internal static Owned FromArchive(IEnumerable<MarginAbilityItem> rows)=>Owned.From(rows.Where(r=>r.getLoaded()&&
            AbilityMemory.IsAbility(unchecked((uint)r.getAbilityID()))&&AbilityCatalog.Canonical(AbilityCatalog.IdOf(unchecked((uint)r.getAbilityID())))==Child)
            .Select(r=>new Base(r.getLevel(),r.getSlot())));
        internal static Owned FromRows(IEnumerable<AbilityMemory.Row> rows)=>Owned.From(rows.Where(r=>r.Loaded&&AbilityMemory.IsAbility(r.Descriptor)&&r.Ability==Child)
            .Select(r=>new Base(r.Level,r.Slot)));
        internal readonly struct Emitted
        {
            internal readonly bool Present;internal readonly Base? Value;
            private Emitted(Base? value){Present=true;Value=value;}
            internal static Emitted FromPacket(byte[] packet)
            {
                if(packet?.Length==8&&packet[0]==0x80&&packet[1]==0xb2&&BitConverter.ToUInt16(packet,2)==Child)
                    return new Emitted(new Base(BitConverter.ToUInt16(packet,4),BitConverter.ToUInt16(packet,6)));
                if(packet?.Length==4&&packet[0]==0x80&&packet[1]==0xb3&&BitConverter.ToUInt16(packet,2)==Child)return new Emitted(null);
                return default;
            }
        }
        internal readonly struct Snapshot
        {
            internal readonly ClientData Owner;internal readonly AbilityPassives.State State;internal readonly int Generation;
            internal readonly bool ParentLoaded;internal readonly Owned Owned;internal readonly Emitted Emitted;
            internal Snapshot(ClientData owner,bool parent,Owned owned,Emitted emitted)
            {Owner=owner;State=owner.passives;Generation=State.Generation;ParentLoaded=parent;Owned=owned;Emitted=emitted;}
            internal bool Current(ClientData current)=>current!=null&&ReferenceEquals(Owner,current)&&ReferenceEquals(current.passives,State)&&State.Generation==Generation;
        }
        internal sealed class Presentation
        {
            private ClientData owner;private AbilityGrantRegistration.Stamp? stamp;private bool ready,member;private Base? shown;
            internal List<byte[]> Reconcile(ClientData current,AbilityGrantRegistration.Stamp now,bool complete,Snapshot snapshot)
            {
                var output=new List<byte[]>();if(!snapshot.Current(current))return output;
                bool wasReady=ReferenceEquals(owner,current)&&stamp==now&&ready;
                if(!ReferenceEquals(owner,current)||stamp!=now){owner=current;stamp=now;ready=false;member=false;shown=null;}
                if(complete)ready=true;if(!ready)return output;
                // Only a normal packet queued in this already-ready world can satisfy a base transition.
                // Early/old-world packets never suppress the required late ready replay.
                if(wasReady&&snapshot.Emitted.Present)shown=snapshot.Emitted.Value;
                if(member&&!snapshot.ParentLoaded){output.Add(AbilityGrantRegistration.Record(new(Parent,Child,0),true));member=false;}
                if(snapshot.Owned.Ambiguous)return output; // preserve ordinary rows; never guess a slot or overwrite them with derived0
                Base? next=snapshot.Owned.Value??(snapshot.ParentLoaded?new Base(0,AbilityProgression.HiddenSlot):(Base?)null);
                if(!Nullable.Equals(shown,next))
                {
                    if(next.HasValue)output.Add(ServerPackets.BuildAbilityLoad(Child,next.Value.Level,next.Value.Slot));
                    else if(shown.HasValue)output.Add(ServerPackets.BuildAbilityUnload(Child));
                    shown=next;
                }
                if(snapshot.ParentLoaded&&!member){output.Add(AbilityGrantRegistration.Record(new(Parent,Child,0),false));member=true;}
                return output;
            }
        }
    }
}
