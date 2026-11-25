using System;
using System.Linq;

namespace hds
{
    /// <summary>
    /// Bull Dog (387, SneakAttackBullDogAbility) stores a single instant thrown health component of 75
    /// (<c>DamageType=7</c>, <c>DurationType=1</c>, <c>ValueFunction=0</c>, <c>MinValue=MaxValue=75</c>).
    /// Its legacy CSV endpoints (75/0) are incomplete and previously fell through to the generic 50..80 roll.
    /// </summary>
    public static class BullDogDamage
    {
        public const ushort Id = 387;

        public static bool TryRefuse(WorldClient caster, ushort abilityId, ushort targetViewId, ushort? spawn, out uint reason)
        {
            reason = 0;
            if (AbilityCatalog.Canonical(abilityId) != Id) return false;
            return SpyInstantSneakOpenerAdmission.TryRefuse(caster, abilityId, targetViewId, spawn, out reason);
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

    internal sealed class BullDogHandler : ISpyAbilityHandler
    {
        public ushort[] AbilityIds => new[] { BullDogDamage.Id };
        public bool TryRefuse(WorldClient client, ushort abilityId, ushort targetViewId, ushort? targetSpawnId, out uint refusalReason) =>
            BullDogDamage.TryRefuse(client, abilityId, targetViewId, targetSpawnId, out refusalReason);
        public bool TryResolveDamage(AbilityCatalog.Entry source, int characterLevel, Random random, out ushort damage) =>
            BullDogDamage.TryResolve(source, out damage);
    }
}
