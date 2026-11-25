using System;
using System.Linq;

namespace hds
{
    /// <summary>
    /// Shipped GOB ValueFunction 3 thrown base and outside-combat live-enemy admission for Suplex (287),
    /// whose legacy CSV row has an incomplete range (17/0).
    /// </summary>
    public static class SuplexDamage
    {
        public const ushort Suplex = 287;

        public static bool TryRefuse(WorldClient client, ushort abilityId, ushort targetViewId, ushort? targetSpawnId, out uint refusalReason)
        {
            refusalReason = 0;
            if (AbilityCatalog.Canonical(abilityId) != Suplex) return false;
            return SpyInstantSneakOpenerAdmission.TryRefuse(client, abilityId, targetViewId, targetSpawnId, out refusalReason);
        }

        public static bool TryResolve(AbilityCatalog.Entry source, int characterLevel, out ushort damage)
        {
            damage = 0;
            if (source == null || source.Id != Suplex) return false;

            var health = source.Modifiers.Where(m => m.Target == 108 && m.Number("WhenEnabled", -1) == 2).ToArray();
            if (health.Length != 1) return false;

            var shipped = health[0];
            if (shipped.Number("DamageType", -1) != 7 ||
                shipped.Number("DurationType", -1) != 1 ||
                shipped.Number("ValueFunction") != 3 ||
                !AbilityValues.TryValue(shipped, 0, characterLevel, out int amount) ||
                amount <= 0 || amount > ushort.MaxValue)
                return false;

            damage = (ushort)amount;
            return true;
        }
    }

    internal sealed class SuplexHandler : ISpyAbilityHandler
    {
        public ushort[] AbilityIds => new[] { SuplexDamage.Suplex };
        public bool TryRefuse(WorldClient client, ushort abilityId, ushort targetViewId, ushort? targetSpawnId, out uint refusalReason) =>
            SuplexDamage.TryRefuse(client, abilityId, targetViewId, targetSpawnId, out refusalReason);
        public bool TryResolveDamage(AbilityCatalog.Entry source, int characterLevel, Random random, out ushort damage) =>
            SuplexDamage.TryResolve(source, characterLevel, out damage);
    }
}
