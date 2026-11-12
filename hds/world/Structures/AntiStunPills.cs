using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using hds.shared;

namespace hds
{
    /// <summary>Stored AntiStunPill items and tempmods.py AntiStunPillEffect. No free ability activation.</summary>
    public static class AntiStunPills
    {
        public const ushort First = 971, Second = 972, Family = 973;
        public static bool Handles(ushort ability) => ability == First || ability == Second;
        public static uint Item(ushort ability) => ability == First ? 42763u : ability == Second ? 42764u : 0;
        private sealed class Pending { }
        private static readonly ConditionalWeakTable<WorldClient, Pending> pending = new ConditionalWeakTable<WorldClient, Pending>();

        /// <summary>Remove Stun/Mez tuples only; refresh each immunity using half its remaining time when at least1s.</summary>
        public static void Apply(TimedEffects.Subject subject, double now)
        {
            var snapshot = subject.Effects.Snapshot();
            var removed = new List<TimedEffects.Active>();
            foreach (var mod in snapshot)
                if ((mod.Target == TempMods.StunAbility || mod.Target == TempMods.MezAbility) && subject.Effects.Remove(mod))
                    removed.Add(mod);
            if (removed.Count > 0) subject.Ended(removed);
            foreach (var mod in snapshot.Where(m => m.Parent == TempMods.ControlImmunityTimerAbility))
            {
                float seconds = (float)((mod.Expires - now) / 2);
                if (seconds < 1) continue;
                TimedEffects.Apply(subject, new TempMods.Effect { Ability = TempMods.ControlImmunityTimerAbility,
                    Mods = new[] { new TempMods.Mod { Kind = TempMods.KindPlain, Target = TempMods.ISDrainTargetAbility,
                        Seconds = seconds, Proc = TempMods.Proc.State } } }, now);
            }
        }

        private static bool Ready(WorldClient client) => client.Alive && client.playerData.getOnWorld() &&
            !client.playerData.worldChangePending && !client.playerData.DistrictIsLA() &&
            !client.playerData.isDead && !client.playerData.duelDeath;

        /// <summary>Requires the real owned item; CanUseWhileStunned1 belongs only to these two stored items.</summary>
        public static bool Use(WorldClient client, ushort slot, ushort ability, uint goid)
        {
            if (!Handles(ability) || Item(ability) != goid || slot < 1 || slot > 96) return false;
            var data = client.playerData;
            lock (data.craftingSync)
            {
                double now = TimeUtils.GetSimTimeSeconds();
                if (!Ready(client) || Store.dbManager.WorldDbHandler.GetItemGOIDAtInventorySlot(slot) != goid ||
                    pending.TryGetValue(client, out _) || AbilityCooldowns.IsCasting(data, now) ||
                    AbilityCooldowns.IsCoolingDown(data, ability, now))
                { Deny(client, ability); return true; }
                CraftingSession.End(client);
                var token = new Pending();
                pending.Add(client, token);
                int world = data.worldGeneration, life = data.lifeGeneration;
                float cast = (float)AbilityCatalog.Find(ability).Number("Ability/CastingTime");
                AbilityCooldowns.StartCast(data, now, cast);
                double castEnd = data.castEndsAt;
                new ServerPackets().SendCastAbilityBar(ability, cast);
                client.FlushQueue();
                DelayedActions.AfterFor(client, cast, () =>
                {
                    lock (data.craftingSync)
                    {
                        if (!pending.TryGetValue(client, out var current) || current != token) return;
                        pending.Remove(client);
                        if (!Ready(client) || data.worldGeneration != world || data.lifeGeneration != life ||
                            data.castEndsAt != castEnd || Store.dbManager.WorldDbHandler.GetItemGOIDAtInventorySlot(slot) != goid)
                        { Output.WriteDebugLog("[ANTISTUN] Cast cancelled: item, life, world or cast changed"); return; }
                        InventoryHandler.RemoveOne(client, slot);
                        AbilityCooldowns.Start(client, ability, TimeUtils.GetSimTimeSeconds());
                        Apply(TimedEffects.ForPlayer(client), TimeUtils.GetSimTimeSeconds());
                        client.FlushQueue();
                        Output.WriteDebugLog("[ANTISTUN] Used " + ability + " from owned item " + goid);
                    }
                });
                return true;
            }
        }

        public static void Deny(WorldClient client, ushort ability)
        {
            client.messageQueue.addRpcMessage(AbilityCooldowns.BuildDenied(ability, AbilityCooldowns.DeniedBusy));
            client.FlushQueue();
        }
    }
}
