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
        private sealed class Pending { }
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
        private static bool Ready(WorldClient c) => c.Alive && c.playerData.getOnWorld() &&
            !c.playerData.worldChangePending && !c.playerData.DistrictIsLA() && !c.playerData.isDead &&
            !c.playerData.duelDeath && ControlActions.CanUseAbilities(c) &&
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
                double now=TimeUtils.GetSimTimeSeconds();
                TryGet(ability,out var buff);
                if (!Request(body,ability) || !Ready(c) || !Prerequisite(c,ability) ||
                    Store.dbManager.WorldDbHandler.GetItemGOIDAtInventorySlot(Slot)!=Item(ability) ||
                    pending.TryGetValue(c,out _) || AbilityCooldowns.IsCasting(data,now) ||
                    AbilityCooldowns.IsCoolingDown(data,ability,now) || !AbilityBuffBuffer.CanApply(c,ability,buff))
                { Deny(c,ability); return; }
                CraftingSession.End(c);
                var token=new Pending(); pending.Add(c,token);
                int world=data.worldGeneration,life=data.lifeGeneration;
                float cast=AbilityCatalog.Find(ability).CastSeconds;
                AbilityCooldowns.StartCast(data,now,cast); double stamp=data.castEndsAt;
                new ServerPackets().SendCastAbilityBar(ability,cast); c.FlushQueue();
                DelayedActions.AfterFor(c,cast,()=>
                {
                    lock(data.craftingSync)
                    {
                        if(!pending.TryGetValue(c,out var current) || current!=token) return;
                        pending.Remove(c);
                        if(!Ready(c) || !Prerequisite(c,ability) || world!=data.worldGeneration || life!=data.lifeGeneration ||
                            stamp!=data.castEndsAt || Store.dbManager.WorldDbHandler.GetItemGOIDAtInventorySlot(Slot)!=Item(ability)) return;
                        if(AbilityBuffs.Apply(c,ability,c)) AbilityCooldowns.Start(c,ability,TimeUtils.GetSimTimeSeconds());
                        c.FlushQueue();
                    }
                });
            }
        }

        /// <summary>Native category 0x6c505449 maps automatic equipment destination to106.</summary>
        public static bool Equipment(WorldClient c, ushort source, ushort destination)
        {
            if(source!=Slot && destination!=Slot && destination!=107) return false;
            lock(c.playerData.craftingSync)
            {
                var db=Store.dbManager.WorldDbHandler;
                var inventory=db.GetInventoryItems(c.playerData.getCharID());
                uint goid=inventory.FirstOrDefault(i=>i.Slot==source).Goid;
                bool device=goid==42722 || goid==42723;
                bool occupiedDevice=inventory.Any(i=>i.Slot==Slot && (i.Goid==42722 || i.Goid==42723));
                if(device && destination==107) destination=Slot;
                if(!(device && (source==Slot || destination==Slot)) && !(occupiedDevice && destination==Slot)) return false;
                ushort ability=goid==42722?(ushort)949:(ushort)957;
                bool mounting=destination==Slot;
                if(!Ready(c) || !device || source==destination ||
                    (mounting ? source<1 || source>96 || !Prerequisite(c,ability) : source!=Slot || destination<1 || destination>96) ||
                    inventory.Any(i=>i.Slot==destination))
                { ClientActionsHandler.Notice(c,"Empty the destination slot and meet the device prerequisite before moving it."); return true; }
                db.UpdateInventorySlot(source,destination,c.playerData.getCharID());
                if(source==Slot) Unequipped(c);
                new ServerPackets().sendInventoryItemMove(source,destination,c); c.FlushQueue(); return true;
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
