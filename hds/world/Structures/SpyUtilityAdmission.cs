namespace hds
{
    /// <summary>
    /// Outside-combat and live-enemy admission for Spy/Infiltrator utility &amp; stealth-free abilities:
    /// <list type="bullet">
    /// <item>Sixth Sense (149, Infiltrator): <c>CastWhenUsed=1</c> (outside close combat).</item>
    /// <item>Bull Dog (1110, Spy) and Disable Evasion (1125, Spy): <c>CastWhenUsed=1</c>, <c>CastValidTarget=3</c> (live enemy).</item>
    /// </list>
    /// </summary>
    public static class SpyUtilityAdmission
    {
        public static bool TryRefuse(WorldClient client, ushort abilityId, ushort targetViewId, ushort? targetSpawnId, out uint refusalReason)
        {
            refusalReason = 0;
            ushort canonical = AbilityCatalog.Canonical(abilityId);
            if (canonical != SixthSense.Ability &&
                canonical != StealthShieldAbilities.BullDog &&
                canonical != StealthShieldAbilities.DisableEvasion)
                return false;

            var combat = CombatManager.GetHandler(client);
            if (combat.isCombatRunning || combat.HasActiveInterlock)
            {
                refusalReason = AbilityRefusals.MustBeOutsideCombat;
                return true;
            }
            if ((canonical == StealthShieldAbilities.BullDog || canonical == StealthShieldAbilities.DisableEvasion) &&
                !AbilityTargets.HasLiveAttackSelection(client, targetViewId, targetSpawnId, requireOpponent: canonical == StealthShieldAbilities.BullDog))
            {
                refusalReason = AbilityRefusals.InvalidTarget;
                return true;
            }
            if (canonical == StealthShieldAbilities.BullDog)
            {
                if ((PvpFlag.FlagsOf(client) & Stealth.StealthedFlag) == 0)
                {
                    refusalReason = AbilityRefusals.CasterStateRequired;
                    return true;
                }
                if (!AbilityTargets.WithinSelectionRange(client, canonical, targetViewId))
                {
                    refusalReason = AbilityRefusals.TargetOutOfRange;
                    return true;
                }
                var template = AbilityCatalog.Find(canonical);
                if (!AbilityTargets.HasSelectionSight(client, template, targetViewId))
                {
                    refusalReason = AbilityRefusals.TargetNotInSight;
                    return true;
                }
                var (x, _, z) = CombatSight.Position(client);
                var mob = AbilityTargets.FindMob(client, targetViewId, out _);
                var player = mob == null ? PvpCombat.PlayerInView(client, targetViewId) : null;
                if (mob == null && player == null)
                {
                    refusalReason = AbilityRefusals.InvalidTarget;
                    return true;
                }
                var (tx, _, tz) = mob == null ? CombatSight.Position(player) : (mob.getXPos(), mob.getYPos(), mob.getZPos());
                byte yaw = mob == null ? player.playerInstance.YawInterval.getValue()[0] : (byte)mob.getRotation();
                double angle = template.Number("Ability/CastOutsideTargetFOVAngle");
                if (angle > 0 && Stealth.InFrustum(tx, tz, yaw, x, z, angle))
                {
                    refusalReason = AbilityRefusals.InvalidTarget;
                    return true;
                }
            }
            return false;
        }
    }

    internal sealed class SpyUtilityAdmissionHandler : ISpyAbilityHandler
    {
        public ushort[] AbilityIds => new[]
        {
            SixthSense.Ability,
            StealthShieldAbilities.BullDog,
            StealthShieldAbilities.DisableEvasion
        };

        public bool TryRefuse(WorldClient client, ushort abilityId, ushort targetViewId, ushort? targetSpawnId, out uint refusalReason) =>
            SpyUtilityAdmission.TryRefuse(client, abilityId, targetViewId, targetSpawnId, out refusalReason);
    }
}
