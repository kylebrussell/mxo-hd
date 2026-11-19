using System;
using System.Linq;

namespace hds
{
    /// <summary>Shipped PlayEnergizedFX and PlayPreciseFX toggle callbacks.</summary>
    internal static class AwakenedTacticalToggles
    {
        private const ushort Energized = 9, Precise = 15;
        private const uint EnergizedEffect = 671090444, PreciseEffect = 671090431;
        private const uint EnergizedConflict = 0xc6001a9f, PreciseConflict = 0xc6001a9b;

        internal static void Refuse(WorldClient caster, ushort ability)
        {
            string name = TimedEffects.PlayerName(caster);
            // Keep the existing action refusal acknowledgement before the source-specific message.
            caster.messageQueue.addRpcMessage(AbilityRefusals.Encode(new AbilityRefusals.Record(ability, AbilityRefusals.LegacyAmbiguous, 0)));
            caster.messageQueue.addRpcMessage(AbilityMessages.AboutAbility(
                ability == Energized ? EnergizedConflict : PreciseConflict, true, Energized, name, name));
            caster.FlushQueue();
        }

        internal static void Schedule(AbilityBuffs.Active active, AbilityBuffs.DrainLifetime lifetime)
        {
            if (active.AbilityId != Energized && active.AbilityId != Precise) return;
            var entry = AbilityCatalog.Find(active.AbilityId);
            var modifier = entry?.Modifiers.FirstOrDefault(item => item.Text("StatModUpdateFunc") ==
                (active.AbilityId == Energized ? "PlayEnergizedFX" : "PlayPreciseFX"));
            double interval = modifier?.Number("UpdateFuncInterval") ?? 0;
            if (interval <= 0 || !lifetime.Current) return;

            uint effect = active.AbilityId == Energized ? EnergizedEffect : PreciseEffect;
            QueueNext();
            void QueueNext() => DelayedActions.AfterFor(lifetime.Client, interval, Tick);

            void Tick()
            {
                var client = lifetime.Client;
                if (!lifetime.Current || !client.IsCurrentEndpointOwner) return;
                bool keep = false;
                byte? counter = null;
                var combat = CombatManager.GetHandler(client);
                client.TransportFence.TryRun(lifetime.Transport, () =>
                {
                    lock (lifetime.Data.activeBuffs)
                    {
                        if (!lifetime.Current || lifetime.Data.isDead || lifetime.Data.duelDeath ||
                            !lifetime.Data.activeBuffs.TryGetValue(active.AbilityId, out var current) ||
                            !ReferenceEquals(current, active)) return;
                        keep = true;
                        if (!combat.isCombatRunning && combat.pvpFight?.IsActive != true && combat.pvpGunfire?.IsActive != true)
                            return;
                        counter = ServerPackets.QueuePlayerAbilityEffect(client, effect);
                    }
                });
                if (counter.HasValue && lifetime.Current)
                {
                    client.FlushQueue();
                    PvpFlag.ToWatchers(client, ServerPackets.BuildPlayerAbilityEffect(effect, counter.Value, false));
                }
                if (keep && lifetime.Current) QueueNext();
            }
        }
    }
}
