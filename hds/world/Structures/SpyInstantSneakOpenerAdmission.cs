using System;

namespace hds
{
    /// <summary>
    /// Shared rear-opener admission for instant Spy sneak attacks:
    /// Spinning Backsweep (152), Suplex (287), and Bull Dog (387).
    /// All three store CastWhenUsed=1, Spy Style stance, Stealthed caster state (0x02000000),
    /// SelectionRange=6 m, and CastOutsideTargetFOVAngle=50 deg.
    /// </summary>
    public static class SpyInstantSneakOpenerAdmission
    {
        public static bool Handles(ushort abilityId)
        {
            ushort id = AbilityCatalog.Canonical(abilityId);
            return id == SpinningBacksweepAdmission.SpinningBacksweep ||
                   id == SuplexDamage.Suplex ||
                   id == BullDogDamage.Id;
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
