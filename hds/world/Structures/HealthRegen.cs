using System;
using hds.shared;

namespace hds
{
    /// <summary>
    /// Health comes back out of combat. Retail sent the own-view Health about once a second while it rose; for
    /// 500-health characters the step was usually +5 (the most common rise in research/packet-dumps), with
    /// bigger steps for characters with more health, so 1% of the maximum per second. In a fight retail's own view kept
    /// rising, more slowly (see <see cref="CombatCalculator.FIGHT_REGEN_FACTOR"/>).
    /// </summary>
    public static class HealthRegen
    {
        public const double FractionPerSecond = 0.01;

        /// <summary>Health after <paramref name="seconds"/> of regeneration, or null when nothing changes.</summary>
        public static UInt16? Next(UInt16 current, UInt16 max, bool inCombat, bool dead, double seconds)
        {
            if (dead || max == 0 || current >= max || current == 0)
            {
                return null;
            }

            double fraction = inCombat ? FractionPerSecond * CombatCalculator.FIGHT_REGEN_FACTOR : FractionPerSecond;
            int step = Math.Max(1, (int)Math.Round(max * fraction * seconds));
            return (UInt16)Math.Min(max, current + step);
        }

        /// <summary>One timer tick for a player: raises their health and sends the own-view update.</summary>
        public static void Tick(WorldClient client, double seconds)
        {
            if (client == null || !client.playerData.getOnWorld())
            {
                return;
            }

            UInt16 current = NumericalUtils.ByteArrayToUint16(client.playerInstance.Health.getValue(), 1);
            UInt16 max = NumericalUtils.ByteArrayToUint16(client.playerInstance.MaxHealth.getValue(), 1);
            bool inCombat = CombatManager.GetHandler(client).isCombatRunning;
            UInt16? next = Next(current, max, inCombat, client.playerData.isDead, seconds);
            if (!next.HasValue)
            {
                return;
            }

            client.playerInstance.Health.setValue(next.Value);
            client.messageQueue.addObjectMessage(ServerPackets.BuildSelfHealthUpdate(next.Value), false);
            client.FlushQueue();
        }
    }
}
