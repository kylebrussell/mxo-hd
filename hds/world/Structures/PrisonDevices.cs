using System;
using System.Linq;
using System.Runtime.CompilerServices;
using hds.shared;

namespace hds
{
    /// <summary>Exact equipped Pandora devices, not learned abilities or generic tools.</summary>
    public static class PrisonDevices
    {
        public const byte Slot = 106;
        public sealed class Owned
        {
            public readonly uint Identity,Goid;
            public readonly ushort Slot;
            public readonly int Count,CharacterLevel,AwakenedLevel;
            public Owned(uint identity,ushort slot,uint goid,int count,int characterLevel,int awakenedLevel)
            {Identity=identity;Slot=slot;Goid=goid;Count=count;CharacterLevel=characterLevel;AwakenedLevel=awakenedLevel;}
        }
        private sealed class Pending { public Owned Item; public int World,Life; public long Transport; public double Cast; }
        public static bool Device(uint goid)=>goid==42722 || goid==42723;
        public static bool SameItem(Owned before,Owned after)=>before!=null && after!=null && before.Identity==after.Identity &&
            before.Slot==after.Slot && before.Goid==after.Goid && before.Count==after.Count;
        public static bool Admitted(Owned row,ushort ability)=>row!=null && row.Slot==Slot && row.Goid==Item(ability) &&
            row.Count>=0 && row.Count<=1 && row.CharacterLevel>=RequiredLevel(ability) && row.AwakenedLevel>=RequiredLevel(ability);
        private static readonly ConditionalWeakTable<WorldClient, Pending> pending = new ConditionalWeakTable<WorldClient, Pending>();
        public static bool Handles(ushort ability) => ability == 949 || ability == 957;
        public static uint Item(ushort ability) => ability == 949 ? 42722u : ability == 957 ? 42723u : 0;
        public static int RequiredLevel(ushort ability) => ability == 949 ? 30 : ability == 957 ? 45 : int.MaxValue;
        public static int NextValue(ushort ability, int value, int damage) => damage > 0 &&
            value < (ability == 949 ? 25 : 26) ? value + (ability == 949 ? 2 : 3) : value;
        public static int Refund(int value) => Math.Min(100, value * 2);
        public static bool Request(byte[] body, ushort ability) => Handles(ability) && body?.Length == 41 &&
            BitConverter.ToUInt16(body, 0) == ability && body[35] == Slot && body[36] == 1;
        public static bool TryGet(ushort ability, out AbilityBuffs.Buff buff)
        {
            buff = null;
            if (!Handles(ability)) return false;
            buff = new AbilityBuffs.Buff { Seconds = 30,
                Modifiers = new[] { new AbilityBuffs.Modifier(AbilityBuffs.KindStat,144,ability == 949 ? 1 : 2) } };
            return true;
        }
        internal static bool EquipmentReady(WorldClient c) => c.Alive && c.playerData.getOnWorld() &&
            !c.playerData.waitForRPCShutDown && !c.playerData.worldChangePending && !c.playerData.DistrictIsLA() &&
            !c.playerData.isDead && !c.playerData.duelDeath;
        private static bool Ready(WorldClient c) => EquipmentReady(c) && ControlActions.CanUseAbilities(c) &&
            !ControlActions.Incapacitated(TimedEffects.ForPlayer(c).Effects);
        private static bool Prerequisite(WorldClient c, ushort ability) =>
            c.playerInstance.Level.getValue()[0] >= RequiredLevel(ability) &&
            AbilityPassives.Level(c, 2) >= RequiredLevel(ability);
        private static void Deny(WorldClient c, ushort ability)
        { c.messageQueue.addRpcMessage(AbilityCooldowns.BuildDenied(ability,AbilityCooldowns.DeniedBusy)); c.FlushQueue(); }

        public static void Activate(WorldClient c, ushort ability, byte[] body)
        {
            var data=c.playerData;
            lock(data.craftingSync)
            {
                long transport=c.TransportFence.Capture();
                int world=data.worldGeneration,life=data.lifeGeneration;
                Owned item;
                try { item=Store.dbManager.WorldDbHandler.ReadPrisonDevice(data.getCharID(),Slot); }
                catch(Exception e) { Output.WriteDebugLog("[PRISON] Item admission failed: "+e.Message);Deny(c,ability);return; }
                // The owned-row read is finished before taking the transport/buff locks.
                c.TransportFence.TryRun(transport,()=>
                {
                    double now=TimeUtils.GetSimTimeSeconds();TryGet(ability,out var buff);
                    if(!Request(body,ability) || !Ready(c) || !Prerequisite(c,ability) || !Admitted(item,ability) ||
                        world!=data.worldGeneration || life!=data.lifeGeneration || pending.TryGetValue(c,out _) ||
                        AbilityCooldowns.IsCasting(data,now) || AbilityCooldowns.IsCoolingDown(data,ability,now) ||
                        !AbilityBuffBuffer.CanApply(c,ability,buff)) {Deny(c,ability);return;}
                    CraftingSession.End(c);
                    AbilityCooldowns.StartCast(data,now,AbilityCatalog.Find(ability).CastSeconds);
                    var token=new Pending{Item=item,World=world,Life=life,Transport=transport,Cast=data.castEndsAt};pending.Add(c,token);
                    new ServerPackets().SendCastAbilityBar(ability,AbilityCatalog.Find(ability).CastSeconds);c.FlushQueue();
                    DelayedActions.AfterFor(c,AbilityCatalog.Find(ability).CastSeconds,()=>
                    {
                        lock(data.craftingSync)
                        {
                            if(!pending.TryGetValue(c,out var current) || current!=token)return;
                            pending.Remove(c);
                            Owned atLanding;
                            try {atLanding=Store.dbManager.WorldDbHandler.ReadPrisonDevice(data.getCharID(),Slot);}
                            catch(Exception e){Output.WriteDebugLog("[PRISON] Landing item validation failed: "+e.Message);return;}
                            CommitLanding(c,ability,token.Item,atLanding,token.World,token.Life,token.Cast,token.Transport);
                        }
                    });
                });
            }
        }
        // SQL/transaction must be complete before this actual presentation/application fence.
        internal static bool CommitLanding(WorldClient c,ushort ability,Owned original,Owned current,int world,int life,double cast,long transport)
        {
            bool applied=false;
            c.TransportFence.TryRun(transport,()=>
            {
                if(!Ready(c) || !Prerequisite(c,ability) || !SameItem(original,current) || !Admitted(current,ability) ||
                    world!=c.playerData.worldGeneration || life!=c.playerData.lifeGeneration || cast!=c.playerData.castEndsAt)return;
                applied=AbilityBuffs.Apply(c,ability,c);
                if(applied)AbilityCooldowns.Start(c,ability,TimeUtils.GetSimTimeSeconds());c.FlushQueue();
            });
            return applied;
        }

        /// <summary>Native category 0x6c505449 maps automatic equipment destination to106.</summary>
        public static bool Equipment(WorldClient c, ushort source, ushort destination)
        {
            if(source!=Slot && destination!=Slot && destination!=107)return false;
            lock(c.playerData.craftingSync)
            {
                var db=Store.dbManager.WorldDbHandler;Owned item,occupant;
                long transport=c.TransportFence.Capture();int world=c.playerData.worldGeneration,life=c.playerData.lifeGeneration;
                try
                {
                    item=db.ReadPrisonDevice(c.playerData.getCharID(),source);
                    occupant=destination==Slot?db.ReadPrisonDevice(c.playerData.getCharID(),Slot):null;
                }
                catch(Exception e){Output.WriteDebugLog("[PRISON] Equipment read failed: "+e.Message);return true;}
                if(source==Slot && item==null)return true; // empty/ambiguous equipment cannot fall through to a slot-only move
                bool device=item!=null && Device(item.Goid);
                if(device && destination==107)destination=Slot;
                if(!(device && (source==Slot || destination==Slot)) && !(occupant!=null && Device(occupant.Goid)))return false;
                if(!EquipmentReady(c))return true;
                bool moved;
                try {moved=db.MovePrisonDevice(c.playerData.getCharID(),item,destination);}
                catch(Exception e)
                {Output.WriteDebugLog("[PRISON] Equipment move rolled back: "+e.Message);ClientActionsHandler.Notice(c,"The device could not be moved; your inventory was kept.");return true;}
                if(!moved){ClientActionsHandler.Notice(c,"Empty the destination slot and meet the device prerequisite before moving it.");return true;}
                // Durable move is complete before any buff/queue/fence operation.
                if(source==Slot)Unequipped(c);
                c.TransportFence.TryRun(transport,()=>
                {
                    if(!EquipmentReady(c) || world!=c.playerData.worldGeneration || life!=c.playerData.lifeGeneration)return;
                    new ServerPackets().sendInventoryItemMove(source,destination,c);c.FlushQueue();
                });
                return true;
            }
        }
        public static void Unequipped(WorldClient c)
        {
            pending.Remove(c);
            // RemovePrisonMod explicitly deactivates BOTH abilities, not only the item's own ID.
            AbilityBuffs.Remove(c,949); AbilityBuffs.Remove(c,957);
        }
        public static void Damage(WorldClient c,int damage)
        {
            if(damage<=0) return;
            lock(c.playerData.activeBuffs)
            foreach(var active in c.playerData.activeBuffs.Values.Where(a=>Handles(a.AbilityId)))
            {
                var modifier=active.Definition.Modifiers[0]; int next=NextValue(active.AbilityId,modifier.Value,damage);
                if(next==modifier.Value) continue;
                c.messageQueue.addRpcMessage(AbilityBuffs.BuildModifier(modifier.Kind,active.RecordIds[0],active.AbilityId,144,modifier.Value,true,0));
                modifier.Value=next; active.Definition.Modifiers[0]=modifier;
                float remaining=(float)Math.Max(0,30-(TimeUtils.GetSimTimeSeconds()-active.Started));
                c.messageQueue.addRpcMessage(AbilityBuffs.BuildModifier(modifier.Kind,active.RecordIds[0],active.AbilityId,144,next,false,remaining));
            }
        }
        public static void Terminated(WorldClient c,AbilityBuffs.Active active)
        {
            if(!Handles(active.AbilityId)) return;
            int refund=Refund(active.Definition.Modifiers[0].Value);
            ushort inner=(ushort)Math.Min(AbilityHandler.MaxInnerStrength(c),AbilityHandler.CurrentInnerStrength(c)+refund);
            c.playerInstance.InnerStrengthAvailable.setValue(inner);
            new ServerPackets().sendISCurrent(c,inner);
        }
    }
}
