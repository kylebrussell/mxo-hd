using System;
using System.Linq;

namespace hds
{
    /// <summary>
    /// Shipped GOB damage bases and live-target admission for reviewed Spy-family thrown abilities
    /// whose legacy CSV rows lack a usable endpoint range.
    /// </summary>
    public static class SpyAbilityDamage
    {
        public const ushort CripplingThrow = 280;

        public static bool RequiresLiveEnemy(ushort id) => AbilityCatalog.Canonical(id) == CripplingThrow;

        public static bool TryResolve(AbilityCatalog.Entry source, int characterLevel, Random random, out ushort damage)
        {
            damage = 0;
            if (source == null || source.Id != CripplingThrow) return false;

            var health = source.Modifiers.Where(m => m.Target == 108 && m.Number("WhenEnabled", -1) == 2).ToArray();
            if (health.Length != 1) return false;

            var shipped = health[0];
            if (shipped.Number("DamageType", -1) != 7 ||
                shipped.Number("DurationType", -1) != 1 ||
                shipped.Number("ValueFunction") != 0 ||
                !shipped.Has("MinValue") || !shipped.Has("MaxValue") ||
                shipped.Number("MinValue") != shipped.Number("MaxValue") ||
                !AbilityValues.TryValue(shipped, 0, characterLevel, out int amount) ||
                amount <= 0 || amount > ushort.MaxValue)
                return false;

            damage = (ushort)amount;
            return true;
        }
    }
}
