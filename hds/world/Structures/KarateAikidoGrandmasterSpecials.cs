using System;
using System.Linq;

namespace hds
{
    /// <summary>
    /// Shipped GOB fixed physical damage bases and close-combat interlock opponent admission for
    /// Karate Grandmaster and Aikido Grandmaster specials: Punch Reversal (407: 21),
    /// Aerial Takedown (411: 96), Tomo Nage (412: 113), Machinegun Kick (532: 73),
    /// Wrist Throw (534: 31), and Ki-Charged Foot Sweep (536: 134).
    /// </summary>
    public static class KarateAikidoGrandmasterSpecials
    {
        public const ushort PunchReversal = 407, AerialTakedown = 411, TomoNage = 412,
            MachinegunKick = 532, WristThrow = 534, KiChargedFootSweep = 536;
        public static readonly ushort[] Abilities =
            { PunchReversal, AerialTakedown, TomoNage, MachinegunKick, WristThrow, KiChargedFootSweep };

        public static bool Handles(ushort abilityId)
        {
            ushort id = AbilityCatalog.Canonical(abilityId);
            return id == PunchReversal || id == AerialTakedown || id == TomoNage ||
                id == MachinegunKick || id == WristThrow || id == KiChargedFootSweep;
        }

        public static bool TryRefuse(WorldClient client, ushort abilityId, ushort targetViewId, ushort? targetSpawnId, out uint refusalReason)
        {
            refusalReason = 0;
            if (!Handles(abilityId)) return false;

            var handler = CombatManager.GetHandler(client);
            if (!handler.isCombatRunning || !handler.PowerShotOpponent(targetViewId, targetSpawnId).HasValue)
            {
                refusalReason = AbilityRefusals.InvalidTarget;
                return true;
            }
            return false;
        }

        public static bool TryResolve(AbilityCatalog.Entry source, out ushort damage)
        {
            damage = 0;
            if (source == null || !Handles(source.Id)) return false;

            var health = source.Modifiers.Where(m => m.Target == 108 && m.Number("WhenEnabled", -1) == 2).ToArray();
            if (health.Length != 1) return false;

            var shipped = health[0];
            if (shipped.Number("DamageType", -1) != 0 ||
                shipped.Number("DurationType", -1) != 1 ||
                shipped.Number("ValueFunction") != 0 ||
                shipped.Number("MinValue") != shipped.Number("MaxValue") ||
                !AbilityValues.TryValue(shipped, 0, 0, out int amount) ||
                amount <= 0 || amount > ushort.MaxValue)
                return false;

            damage = (ushort)amount;
            return true;
        }
    }

    internal sealed class KarateAikidoGrandmasterSpecialsHandler : ISpyAbilityHandler
    {
        public ushort[] AbilityIds => KarateAikidoGrandmasterSpecials.Abilities;
        public bool TryRefuse(WorldClient client, ushort abilityId, ushort targetViewId, ushort? targetSpawnId, out uint refusalReason) =>
            KarateAikidoGrandmasterSpecials.TryRefuse(client, abilityId, targetViewId, targetSpawnId, out refusalReason);
        public bool TryResolveDamage(AbilityCatalog.Entry source, int characterLevel, Random random, out ushort damage) =>
            KarateAikidoGrandmasterSpecials.TryResolve(source, out damage);
    }
}
