using System;
using System.Runtime.CompilerServices;
using hds.shared;

namespace hds
{
    /// <summary>Owned health boost items; numeric effects use the existing stored buff executor.</summary>
    public static class HealthBoostPills
    {
        private sealed class Pending { }
        private static readonly ConditionalWeakTable<WorldClient, Pending> pending = new ConditionalWeakTable<WorldClient, Pending>();
        public static bool Handles(ushort ability) => ability == 187 || ability == 188 || ability == 189 || ability == 672 || ability == 673;
        public static uint Item(ushort ability) => ability == 187 ? 2407u : ability == 672 ? 39411u :
            ability == 188 ? 39412u : ability == 189 ? 39413u : ability == 673 ? 39414u : 0;
        // CharStatsPrerequisite/ReqCharLevel; legacy2407 has no stored character-level prerequisite.
        public static int RequiredLevel(ushort ability) => ability == 672 ? 10 : ability == 188 ? 20 : ability == 189 ? 30 : ability == 673 ? 40 : 0;
        private static bool Ready(WorldClient client, ushort ability) => client.Alive && client.playerData.getOnWorld() &&
            !client.playerData.worldChangePending && !client.playerData.DistrictIsLA() && ControlActions.CanUseAbilities(client) &&
            client.playerInstance.Level.getValue()[0] >= RequiredLevel(ability);

        public static void Deny(WorldClient client, ushort ability)
        {
            client.messageQueue.addRpcMessage(AbilityCooldowns.BuildDenied(ability, AbilityCooldowns.DeniedBusy));
            client.FlushQueue();
        }

        public static bool Use(WorldClient client, ushort slot, ushort ability, uint goid)
        {
            if (!Handles(ability) || Item(ability) != goid || slot < 1 || slot > 96) return false;
            var data = client.playerData;
            lock (data.craftingSync)
            {
                double now = TimeUtils.GetSimTimeSeconds();
                if (!Ready(client, ability) || Store.dbManager.WorldDbHandler.GetItemGOIDAtInventorySlot(slot) != goid ||
                    pending.TryGetValue(client, out _) || AbilityCooldowns.IsCasting(data, now) ||
                    AbilityCooldowns.IsCoolingDown(data, ability, now) || !AbilityBuffs.TryGet(ability, out var buff) ||
                    !AbilityBuffBuffer.CanApply(client, ability, buff))
                { Deny(client, ability); return true; }
                CompileSession.End(client);
                var token = new Pending();
                pending.Add(client, token);
                int world = data.worldGeneration, life = data.lifeGeneration;
                float cast = AbilityCatalog.Find(ability).CastSeconds;
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
                        if (!Ready(client, ability) || data.worldGeneration != world || data.lifeGeneration != life ||
                            data.castEndsAt != castEnd || Store.dbManager.WorldDbHandler.GetItemGOIDAtInventorySlot(slot) != goid)
                        { Output.WriteDebugLog("[HEALTHBOOST] Cast cancelled: item, life, world, control or cast changed"); return; }
                        if (!AbilityBuffs.Apply(client, ability, client)) return;
                        InventoryHandler.RemoveOne(client, slot);
                        AbilityCooldowns.Start(client, ability, TimeUtils.GetSimTimeSeconds());
                        client.FlushQueue();
                        Output.WriteDebugLog("[HEALTHBOOST] Used " + ability + " from owned item " + goid);
                    }
                });
                return true;
            }
        }
    }
}
