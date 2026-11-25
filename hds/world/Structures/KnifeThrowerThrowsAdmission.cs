using System;

namespace hds
{
    /// <summary>
    /// Guarded cast admission for Knife Thrower ranged throws:
    /// Deadly Throw (279: CastWhenUsed=2 live hostile 16 m + LOS) and
    /// Crippling Throw (280: CastWhenUsed=1 outside-combat live hostile 16 m + LOS).
    /// </summary>
    public static class KnifeThrowerThrowsAdmission
    {
        public static readonly ushort[] Abilities = { DeadlyThrowDamage.Id, SpyAbilityDamage.CripplingThrow };

        public static bool Handles(ushort abilityId)
        {
            ushort id = AbilityCatalog.Canonical(abilityId);
            return id == DeadlyThrowDamage.Id || id == SpyAbilityDamage.CripplingThrow;
        }

        public static bool TryRefuse(WorldClient client, ushort abilityId, ushort targetViewId, ushort? targetSpawnId, out uint refusalReason)
        {
            refusalReason = 0;
            ushort id = AbilityCatalog.Canonical(abilityId);
            if (!Handles(id)) return false;

            var combat = CombatManager.GetHandler(client);
            if (id == SpyAbilityDamage.CripplingThrow && (combat.isCombatRunning || combat.HasActiveInterlock))
            {
                refusalReason = AbilityRefusals.MustBeOutsideCombat;
                return true;
            }
            if (!AbilityTargets.HasLiveAttackSelection(client, targetViewId, targetSpawnId, requireOpponent: true))
            {
                refusalReason = AbilityRefusals.InvalidTarget;
                return true;
            }
            if (!AbilityTargets.WithinSelectionRange(client, id, targetViewId))
            {
                refusalReason = AbilityRefusals.TargetOutOfRange;
                return true;
            }
            if (!AbilityTargets.HasSelectionSight(client, AbilityCatalog.Find(id), targetViewId))
            {
                refusalReason = AbilityRefusals.TargetNotInSight;
                return true;
            }
            return false;
        }
    }

    internal sealed class KnifeThrowerThrowsAdmissionHandler : ISpyAbilityHandler
    {
        public ushort[] AbilityIds => KnifeThrowerThrowsAdmission.Abilities;

        public bool TryRefuse(WorldClient client, ushort abilityId, ushort targetViewId, ushort? targetSpawnId, out uint refusalReason) =>
            KnifeThrowerThrowsAdmission.TryRefuse(client, abilityId, targetViewId, targetSpawnId, out refusalReason);

        public bool TryResolveDamage(AbilityCatalog.Entry source, int characterLevel, Random random, out ushort damage)
        {
            if (DeadlyThrowDamage.TryResolve(source, characterLevel, out damage)) return true;
            return SpyAbilityDamage.TryResolve(source, characterLevel, random, out damage);
        }
    }
}
