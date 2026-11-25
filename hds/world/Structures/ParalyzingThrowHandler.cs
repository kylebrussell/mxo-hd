using System;
using System.Linq;

namespace hds
{
    /// <summary>
    /// Shipped GOB fixed thrown damage (30) for Paralyzing Throw (1135, Master Assassin),
    /// whose legacy CSV endpoints are incomplete and previously fell through to the generic 50..80 roll.
    /// Selection admission and the interlock route live in <see cref="SpyTimedThrows"/>.
    /// TypedResistanceQa fixtures still override 1135 before this resolver runs.
    /// </summary>
    public static class ParalyzingThrowDamage
    {
        public const ushort Id = 1135;

        public static bool TryRefuse(WorldClient client, ushort abilityId, ushort targetViewId, ushort? targetSpawnId, out uint refusalReason)
        {
            refusalReason = 0;
            return AbilityCatalog.Canonical(abilityId) == Id &&
                SpyTimedThrows.TryRefuse(client, abilityId, targetViewId, targetSpawnId, out refusalReason);
        }

        public static bool TryResolve(AbilityCatalog.Entry source, out ushort damage)
        {
            damage = 0;
            if (source == null || source.Id != Id) return false;

            var health = source.Modifiers.Where(m => m.Target == 108 && m.Number("WhenEnabled", -1) == 2).ToArray();
            if (health.Length != 1) return false;

            var shipped = health[0];
            if (shipped.Number("DamageType", -1) != 7 ||
                shipped.Number("DurationType", -1) != 1 ||
                shipped.Number("ValueFunction") != 0 ||
                !shipped.Has("MinValue") || !shipped.Has("MaxValue") ||
                shipped.Number("MinValue") != shipped.Number("MaxValue") ||
                !AbilityValues.TryValue(shipped, 0, 0, out int amount) ||
                amount <= 0 || amount > ushort.MaxValue)
                return false;

            damage = (ushort)amount;
            return true;
        }
    }
}
