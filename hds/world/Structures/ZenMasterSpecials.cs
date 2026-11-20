using System;
using System.Linq;

namespace hds
{
    /// <summary>
    /// Shipped GOB fixed physical damage bases and close-combat interlock opponent admission for
    /// Zen Master specials: Sweep The Floor (869: 312) and Spinning Flower (870: 325).
    /// </summary>
    public static class ZenMasterSpecials
    {
        public const ushort SweepTheFloor = 869, SpinningFlower = 870;
        public static readonly ushort[] Abilities = { SweepTheFloor, SpinningFlower };

        public static bool Handles(ushort abilityId)
        {
            ushort id = AbilityCatalog.Canonical(abilityId);
            return id == SweepTheFloor || id == SpinningFlower;
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

    internal sealed class ZenMasterSpecialsHandler : ISpyAbilityHandler
    {
        public ushort[] AbilityIds => ZenMasterSpecials.Abilities;
        public bool TryRefuse(WorldClient client, ushort abilityId, ushort targetViewId, ushort? targetSpawnId, out uint refusalReason) =>
            ZenMasterSpecials.TryRefuse(client, abilityId, targetViewId, targetSpawnId, out refusalReason);
        public bool TryResolveDamage(AbilityCatalog.Entry source, int characterLevel, Random random, out ushort damage) =>
            ZenMasterSpecials.TryResolve(source, out damage);
    }
}
