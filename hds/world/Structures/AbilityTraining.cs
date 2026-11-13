using System;
using System.Collections.Generic;
using System.Linq;
using hds.shared;

namespace hds
{
    /// <summary>Ordinary80b7 only: native quotes/admission, durable mutation before current-session presentation.</summary>
    public static class AbilityTraining
    {
        public readonly struct ArchiveRow
        {
            public readonly uint Identity;
            public readonly int Slot;
            public readonly long Descriptor, Level;
            public readonly bool Loaded;
            public ArchiveRow(uint identity,int slot,long descriptor,long level,bool loaded)
            {Identity=identity;Slot=slot;Descriptor=descriptor;Level=level;Loaded=loaded;}
        }
        public sealed class Result
        {
            public bool Committed {get;}
            public ushort Ability {get;}
            public ushort Level {get;}
            public uint Cost {get;}
            public long Cash {get;}
            public string Reason {get;}
            public IReadOnlyList<(ushort Ability,ushort Level)> Loaded {get;}
            public Result(bool committed,ushort ability,ushort level,uint cost,long cash,string reason,
                IEnumerable<(ushort Ability,ushort Level)> loaded=null)
            {Committed=committed;Ability=ability;Level=level;Cost=cost;Cash=cash;Reason=reason;
                Loaded=Array.AsReadOnly((loaded??Array.Empty<(ushort,ushort)>()).ToArray());}
        }
        public static bool Ordinary(ushort ability) => ability>0 && ability<4096 &&
            AbilityCatalog.Find(ability)?.Id==ability && AbilityCatalog.Find(ability).Number("Ability/AbilityClass",-1)==0;
        public static bool DescriptorMatches(long raw,ushort ability) => Ordinary(ability) &&
            raw>=int.MinValue && raw<=uint.MaxValue && (unchecked((uint)raw)&0xfc000000u)==0x80000000u &&
            AbilityCatalog.Canonical(AbilityCatalog.IdOf(unchecked((uint)raw)))==ability;
        //62518f00: old level -> one next-level price. Mastery prices the base at its cap, without training that base.
        public static bool TryCost(ushort ability,int from,int levels,out uint cost)
        {
            cost=0;if(!Ordinary(ability)||from<0||from>50||levels<1||levels>50||from+levels>50)return false;
            try
            {
                uint total=0;
                for(int old=from;old<from+levels;old++)total=checked(total+Step(ability,old,new HashSet<ushort>()));
                cost=total;return true;
            }
            catch(Exception e) when(e is ArgumentException || e is OverflowException){return false;}
        }
        private static uint Step(ushort ability,int old,HashSet<ushort> visiting)
        {
            if(!Ordinary(ability)||old<0||old>50||!visiting.Add(ability))throw new ArgumentException("Invalid training quote graph");
            var e=AbilityCatalog.Find(ability);uint extra=0;
            if(e.Number("Ability/MasteryAbility")!=0)
            {
                uint raw=(uint)e.Number("Ability/MasteryBaseAbility");ushort parent=AbilityCatalog.IdOf(raw);
                if(!DescriptorMatches(raw,parent))throw new ArgumentException("Unknown mastery base");
                int cap=(int)AbilityCatalog.Find(parent).Number("Ability/AbilityLevelCap");
                extra=Step(parent,cap,visiting);
            }
            int type=(int)e.Number("Ability/LevelingCost");int factor=type==1?2:type==2?4:1;
            uint subtotal=checked(extra+(uint)(200*(old+1)*(old+1)*factor));
            float multiplier=(float)e.Number("Ability/UpgradeCostMultiplier",1);
            double value=(float)subtotal*(double)multiplier; //native f32 subtotal, f32 multiplier, x87 truncation toward zero
            if(!double.IsFinite(value)||value<0||value>uint.MaxValue)throw new OverflowException("Invalid training quote");
            visiting.Remove(ability);return (uint)value;
        }
        public static bool Admit(AbilityProgression.UpgradeRequest request,ArchiveRow row,long experience,out ushort level,out uint cost)
        {
            level=0;cost=0;
            if(request.Slot>=CharacterAttributes.FirstSlot || request.Levels==0 || row.Slot!=request.Slot ||
                !DescriptorMatches(row.Descriptor,request.Ability)||row.Level<0||row.Level>50||experience<0||experience>uint.MaxValue)return false;
            int next=checked((int)row.Level+request.Levels);
            int character=PlayerProgression.LevelForExperience(1,experience);
            int cap=(int)AbilityCatalog.Find(request.Ability).Number("Ability/AbilityLevelCap");
            if(next>character||next>50||(cap>0&&next>cap)||!TryCost(request.Ability,(int)row.Level,request.Levels,out cost))return false;
            level=(ushort)next;return true;
        }
        internal static bool TryRemainingInfo(long cash,uint cost,out long remaining)
        {
            remaining=0;if(cash<0 || cash<cost)return false;
            remaining=cash-cost;return remaining<=uint.MaxValue;
        }
        internal static bool UniqueOwnedAbility(IEnumerable<ArchiveRow> rows,ushort ability) =>
            rows.Count(r=>r.Slot>=0&&r.Slot<CharacterAttributes.FirstSlot&&DescriptorMatches(r.Descriptor,ability))==1;
        internal static IEnumerable<(ushort Ability,ushort Level)> LoadedSnapshot(IEnumerable<ArchiveRow> rows) =>
            rows.Where(r=>r.Loaded&&r.Slot>=0&&r.Slot<CharacterAttributes.FirstSlot&&r.Descriptor>=int.MinValue&&r.Descriptor<=uint.MaxValue &&
                (unchecked((uint)r.Descriptor)&0xfc000000u)==0x80000000u)
                .Select(r=>(AbilityCatalog.Canonical(AbilityCatalog.IdOf(unchecked((uint)r.Descriptor))),unchecked((ushort)r.Level)));
        internal static IEnumerable<(ushort Ability,ushort Level)> GrantRefreshes(Result result)
        {
            if(!result.Committed)return Array.Empty<(ushort,ushort)>();
            var loaded=result.Loaded.Where(r=>r.Ability==result.Ability).ToArray();
            if(loaded.Length==0)return Array.Empty<(ushort,ushort)>();
            ushort effective=loaded.Max(r=>r.Level);
            return AbilityProgression.GrantsOf(result.Ability).Where(g=>!AbilityGrantRegistration.Handles(result.Ability,g))
                .Select(g=>(g,AbilityProgression.GrantLevel(result.Ability,effective))).ToArray();
        }
        internal static bool Current(WorldClient c,ClientData data,int generation,int world,int life,long transport,uint character) =>
            ReferenceEquals(c.playerData,data)&&data.getCharID()==character&&data.trainingGeneration==generation&&data.worldGeneration==world&&data.lifeGeneration==life &&
            c.TransportFence.Capture()==transport&&c.Alive&&!data.worldChangePending&&!data.waitForRPCShutDown;
        //621e8860 signed-positive status takes the failure branch; 1800000c selects STA24000106.
        internal const uint FailureStatus=0x1800000c;
        internal static void CompleteCurrent(WorldClient client,long transport,Func<bool> current,Action reconcile,Action publish,Action afterPublish=null)
        {
            bool admitted=false;
            client.TransportFence.TryRun(transport,()=>admitted=current());
            if(!admitted)return;
            reconcile(); //callbacks may take holder->fence; no outer fence/queue/life lock here
            bool published=false;
            client.TransportFence.TryRun(transport,()=>{if(current()){publish();published=true;}});
            if(published && current())afterPublish?.Invoke();
            //Flush can traverse other clients and must also remain outside our transport fence.
            if(current())client.FlushQueue();
        }
        public static void Handle(WorldClient client,byte[] body)
        {
            var request=AbilityProgression.ParseUpgrade(body);if(!request.HasValue)return;
            var data=client.playerData;
            lock(data.trainingSync)
            {
                uint character=data.getCharID();
                int generation=++data.trainingGeneration,world=data.worldGeneration,life=data.lifeGeneration;
                long transport=client.TransportFence.Capture();Result result;
                try{result=Store.dbManager.WorldDbHandler.TrainOwnedAbility(character,request.Value);}
                catch(Exception e)
                {
                    Output.WriteDebugLog("[TRAINING] char="+data.getCharID()+" ability="+request.Value.Ability+" failed: "+e);
                    result=new Result(false,request.Value.Ability,0,0,0,"Training could not be saved; nothing was charged.");
                }
                //SQL is disposed. Never hold TransportFence while SetLoaded/registration/tutorial acquire their own locks.
                Func<bool> current=()=>Current(client,data,generation,world,life,transport,character);
                bool live=false;
                CompleteCurrent(client,transport,current,()=>
                {
                    if(!result.Committed || !current())return;
                    data.setInfo(result.Cash);
                    live=client.playerInstance!=null && data.getOnWorld();
                    //Committed snapshot includes unrelated loaded rows. Normal RPC serialization owns this callback phase.
                    if(live && current())AbilityPassives.SetLoaded(client,result.Loaded);
                },()=>
                {
                    var packets=new ServerPackets();
                    if(!result.Committed)
                    {
                        packets.sendSystemChatMessage(client,result.Reason,"MODAL");
                        client.messageQueue.addRpcMessage(ServerPackets.BuildAbilityUpgraded(result.Ability,result.Level,FailureStatus));
                        return;
                    }
                    packets.SendInfoDelta(client,-(long)result.Cost,ServerPackets.InfoReasonAbilityUpgrade);
                    packets.SendInfoCurrent(client,(uint)result.Cash,ServerPackets.InfoReasonAbilityUpgrade);
                    foreach(var grant in live?GrantRefreshes(result):Array.Empty<(ushort Ability,ushort Level)>())
                    {
                        client.messageQueue.addRpcMessage(ServerPackets.BuildAbilityUnload(grant.Ability));
                        client.messageQueue.addRpcMessage(ServerPackets.BuildAbilityLoad(grant.Ability,grant.Level,AbilityProgression.HiddenSlot));
                    }
                    client.messageQueue.addRpcMessage(ServerPackets.BuildAbilityUpgraded(result.Ability,result.Level));
                },()=>{if(result.Committed && live && current())TutorialMission.Of(client)?.OnAbilityUpgraded();});
                if(result.Committed)Output.WriteDebugLog("[TRAINING] Committed char="+character+" ability="+result.Ability+" level="+result.Level+" cost="+result.Cost);

            }
        }
    }
}
