using System;

namespace hds
{
    /// <summary>
    /// Shared admission and rear-hemisphere targeting checks for SneakFree* thrown abilities
    /// (CastWhenUsed=1, Spy Style, Stealthed caster, live hostile opponent, SelectionRange, LOS, and
    /// CastOutsideTargetFOVAngle=50 deg).
    /// </summary>
    public static class SneakFreeThrowAdmission
    {
        public static bool Handles(ushort abilityId)
        {
            ushort id = AbilityCatalog.Canonical(abilityId);
            return id == HarryingAttackDamage.Id || id == DazzlingThrowDamage.Id ||
                id == SpySneakOpeners.LegThrow || id == SpyMasterSneakOpeners.InfuriatingAssault ||
                id == SpyMasterSneakOpeners.StaggeringThrow;
        }

        public static bool TryRefuse(WorldClient caster, ushort abilityId, ushort targetViewId, ushort? spawn, out uint reason)
        {
            reason = 0;
            ushort id = AbilityCatalog.Canonical(abilityId);
            if (!Handles(id)) return false;

            var combat = CombatManager.GetHandler(caster);
            if (combat.isCombatRunning || combat.HasActiveInterlock)
            {
                reason = AbilityRefusals.MustBeOutsideCombat;
                return true;
            }
            if (CombatStyles.Active(caster) != CombatStyles.SpyStyle)
            {
                reason = AbilityRefusals.WrongStyle;
                return true;
            }
            if ((PvpFlag.FlagsOf(caster) & Stealth.StealthedFlag) == 0)
            {
                reason = AbilityRefusals.CasterStateRequired;
                return true;
            }
            if (!AbilityTargets.HasLiveAttackSelection(caster, targetViewId, spawn, requireOpponent: true))
            {
                reason = AbilityRefusals.InvalidTarget;
                return true;
            }
            if (!AbilityTargets.WithinSelectionRange(caster, id, targetViewId))
            {
                reason = AbilityRefusals.TargetOutOfRange;
                return true;
            }
            var template = AbilityCatalog.Find(id);
            if (!AbilityTargets.HasSelectionSight(caster, template, targetViewId))
            {
                reason = AbilityRefusals.TargetNotInSight;
                return true;
            }
            var (x, _, z) = CombatSight.Position(caster);
            var mob = AbilityTargets.FindMob(caster, targetViewId, out _);
            var player = mob == null ? PvpCombat.PlayerInView(caster, targetViewId) : null;
            if (mob == null && player == null)
            {
                reason = AbilityRefusals.InvalidTarget;
                return true;
            }
            var (tx, _, tz) = mob == null ? CombatSight.Position(player) : (mob.getXPos(), mob.getYPos(), mob.getZPos());
            byte yaw = mob == null ? player.playerInstance.YawInterval.getValue()[0] : (byte)mob.getRotation();
            double angle = template.Number("Ability/CastOutsideTargetFOVAngle");
            if (angle > 0 && Stealth.InFrustum(tx, tz, yaw, x, z, angle))
            {
                reason = AbilityRefusals.InvalidTarget;
                return true;
            }
            return false;
        }
    }
}
