using System;
using System.Linq;

namespace hds
{
    /// <summary>
    /// Guarded cast admission, shipped fixed thrown base (29), and targeted throw motion for Ghost Strike (365).
    /// </summary>
    public static class GhostStrikeAdmission
    {
        public const ushort Id = GhostStrikeCallbacks.Ability; // 365

        public static bool Handles(ushort abilityId) => AbilityCatalog.Canonical(abilityId) == Id;

        public static bool TryRefuse(WorldClient client, ushort abilityId, ushort targetViewId, ushort? targetSpawnId, out uint refusalReason)
        {
            refusalReason = 0;
            if (!Handles(abilityId)) return false;

            var combat = CombatManager.GetHandler(client);
            if (combat.isCombatRunning || combat.HasActiveInterlock)
            {
                refusalReason = AbilityRefusals.MustBeOutsideCombat;
                return true;
            }
            if (!AbilityTargets.HasLiveAttackSelection(client, targetViewId, targetSpawnId, requireOpponent: true))
            {
                refusalReason = AbilityRefusals.InvalidTarget;
                return true;
            }
            if (CombatStyles.Active(client) != CombatStyles.SpyStyle)
            {
                refusalReason = AbilityRefusals.WrongStyle;
                return true;
            }
            if (!AbilityTargets.WithinSelectionRange(client, Id, targetViewId))
            {
                refusalReason = AbilityRefusals.TargetOutOfRange;
                return true;
            }
            if (!AbilityTargets.HasSelectionSight(client, AbilityCatalog.Find(Id), targetViewId))
            {
                refusalReason = AbilityRefusals.TargetNotInSight;
                return true;
            }
            return false;
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
