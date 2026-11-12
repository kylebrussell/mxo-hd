using System;
using System.Collections.Generic;
using System.Linq;

namespace hds
{
    /// <summary>Upgrade-buffer accounting. Script mechanics are sourced; the baseline100+4*character level is OURS,
    /// pending native getBuffBufferMax recovery (the catalog supplies only stat162's bonus cap, not its baseline).</summary>
    public static class AbilityBuffBuffer
    {
        public static int Maximum(WorldClient client) => Math.Max(0,100 + 4 * client.playerInstance.Level.getValue()[0] + AbilityStats.Player(client,162));
        public static int Size(ushort id) => AbilityHostile.IsBuff(id) ? Math.Max(0,(int)(AbilityCatalog.Find(id)?.Number("Ability/BufferSize") ?? 0)) : 0;
        private static HashSet<ushort> Parents(WorldClient client)
        {
            var ids=new HashSet<ushort>();
            lock(client.playerData.activeBuffs) ids.UnionWith(client.playerData.activeBuffs.Keys);
            ids.UnionWith(AbilityAuras.Active(client));
            ids.UnionWith(TimedEffects.ForPlayer(client).Effects.Snapshot().Select(m=>m.Parent).Where(AbilityHostile.IsBuff));
            var stealth=Stealth.Current(client); if(stealth!=null) ids.Add(stealth.Def.Ability);
            return ids;
        }
        public static int Used(WorldClient client) => Parents(client).Sum(Size);
        public static int Reserved(WorldClient client)
        {
            int amount=TimedEffects.ForPlayer(client).Effects.Snapshot()
                .Where(m=>m.Expires>TimeUtils.GetSimTimeSeconds() && AbilityCatalog.Find(m.Parent)?.Modifiers.Any(
                    d=>d.Text("StatModInitFunc")=="BufferOverflowInit") == true).Sum(m=>Math.Max(0,m.Value));
            // BufferOverflowUpdate removes its own reservation, then adds min(Value, max-current).
            // Recompute on every query, which also implements BufferOverflowEvent after buff changes.
            return Math.Min(amount,Math.Max(0,Maximum(client)-Used(client)));
        }
        public static bool CanApply(WorldClient client, ushort id, AbilityBuffs.Buff definition=null)
        {
            int cost=Size(id); if(cost==0) return true;
            var ids=Parents(client);
            var replacing=new HashSet<ushort>{id};
            if(definition?.Family>0)
                lock(client.playerData.activeBuffs)
                    foreach(var active in client.playerData.activeBuffs.Values)
                        if((active.Definition?.Family ?? 0)==definition.Family) replacing.Add(active.AbilityId);
            if(AbilityCatalog.Find(id)?.Number("Ability/AbilityIsExclusive")==1 && Stealth.Current(client) is Stealth.State state)
                replacing.Add(state.Def.Ability);
            int credit=ids.Where(replacing.Contains).Sum(Size);
            return cost <= Math.Max(0,Maximum(client)-Used(client)-Reserved(client))+credit;
        }
    }
}
