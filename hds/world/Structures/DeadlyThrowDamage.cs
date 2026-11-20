using System;
using System.Linq;

namespace hds
{
    /// <summary>
    /// Deadly Throw (279) stores a single instant thrown health modifier using native ValueFunction 3:
    /// <c>21 + trunc(characterLevel * binary32(0.466667))</c> (30 at level 20, 44 at level 50).
    /// Its legacy CSV row has incomplete endpoints (0/0) and previously fell through to generic 50..80.
    /// </summary>
    public static class DeadlyThrowDamage
    {
        public const ushort Id = 279;

        public static bool RequiresLiveEnemy(ushort id) => AbilityCatalog.Canonical(id) == Id;

        public static bool TryResolve(AbilityCatalog.Entry source, int characterLevel, out ushort damage)
        {
            damage = 0;
            if (source == null || source.Id != Id) return false;

            var health = source.Modifiers.Where(m => m.Target == 108 && m.Number("WhenEnabled", -1) == 2).ToArray();
            if (health.Length != 1) return false;

            var shipped = health[0];
            if (shipped.Number("DamageType", -1) != 7 ||
                shipped.Number("DurationType", -1) != 1 ||
                shipped.Number("ValueFunction") != 3 ||
                !shipped.Has("MinValue") ||
                !AbilityValues.TryValue(shipped, 0, Math.Max(0, characterLevel), out int amount) ||
                amount <= 0 || amount > ushort.MaxValue)
                return false;

            damage = (ushort)amount;
            return true;
        }
    }
}
