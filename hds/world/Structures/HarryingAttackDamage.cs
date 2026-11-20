using System;
using System.Linq;

namespace hds
{
    /// <summary>
    /// Harrying Attack (1127, SneakFreeHarryingAttackAbility) stores a single instant thrown health component
    /// using native ValueFunction 3: <c>min(16 + trunc(characterLevel * 3.0f), 76)</c> (49 at level 11, 73 at 19, 76 at 20+).
    /// Its legacy CSV row has incomplete endpoints (0/0) and previously fell through to generic 50..80.
    /// </summary>
    public static class HarryingAttackDamage
    {
        public const ushort Id = 1127;

        public static bool RetainsStealthDuringCast(ushort id) => AbilityCatalog.Canonical(id) == Id;

        public static bool TryRefuse(WorldClient caster, ushort abilityId, ushort targetViewId, ushort? spawn, out uint reason)
        {
            reason = 0;
            if (AbilityCatalog.Canonical(abilityId) != Id) return false;
            if (CombatManager.GetHandler(caster).isCombatRunning)
            {
                reason = AbilityRefusals.MustBeOutsideCombat;
                return true;
            }
            if (!AbilityTargets.HasLiveAttackSelection(caster, targetViewId, spawn))
            {
                reason = AbilityRefusals.InvalidTarget;
                return true;
            }
            return false;
        }

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

    internal sealed class HarryingAttackHandler : ISpyAbilityHandler
    {
        public ushort[] AbilityIds => new[] { HarryingAttackDamage.Id };
        public bool RetainsStealthDuringCast(ushort abilityId) => HarryingAttackDamage.RetainsStealthDuringCast(abilityId);
        public bool TryRefuse(WorldClient client, ushort abilityId, ushort targetViewId, ushort? targetSpawnId, out uint refusalReason) =>
            HarryingAttackDamage.TryRefuse(client, abilityId, targetViewId, targetSpawnId, out refusalReason);
        public bool TryResolveDamage(AbilityCatalog.Entry source, int characterLevel, Random random, out ushort damage) =>
            HarryingAttackDamage.TryResolve(source, characterLevel, out damage);
    }
}
