namespace hds
{
    /// <summary>
    /// Target and combat-state admission for Spy thrown-DOT abilities:
    /// <list type="bullet">
    /// <item>Poison Knife (146, level 4 Spy): <c>CastWhenUsed=1</c> (outside combat), live enemy required.</item>
    /// <item>Wounding Throw (281, Shadow): <c>CastWhenUsed=2</c>, live enemy required.</item>
    /// </list>
    /// </summary>
    public static class ThrownDotAdmission
    {
        public static bool TryRefuse(WorldClient caster, ushort abilityId, ushort targetViewId, ushort? spawn, out uint reason)
        {
            reason = 0;
            ushort canonical = AbilityCatalog.Canonical(abilityId);
            if (canonical != CombatSpecials.PoisonKnife && canonical != CombatSpecials.WoundingThrow)
                return false;

            if (canonical == CombatSpecials.PoisonKnife)
            {
                reason = PoisonKnifeRefusal(caster, targetViewId, spawn);
                return reason != 0;
            }
            reason = WoundingThrowRefusal(caster, targetViewId, spawn);
            return reason != 0;
        }

        private static uint WoundingThrowRefusal(WorldClient caster, ushort view, ushort? spawn)
        {
            if (!AbilityTargets.HasLiveAttackSelection(caster, view, spawn, requireOpponent: true))
                return AbilityRefusals.InvalidTarget;
            if (!AbilityTargets.WithinSelectionRange(caster, CombatSpecials.WoundingThrow, view))
                return AbilityRefusals.TargetOutOfRange;
            if (!AbilityTargets.HasSelectionSight(caster, AbilityCatalog.Find(CombatSpecials.WoundingThrow), view))
                return AbilityRefusals.TargetNotInSight;
            return 0;
        }

        private static uint PoisonKnifeRefusal(WorldClient caster, ushort view, ushort? spawn)
        {
            var fight = CombatManager.GetHandler(caster);
            if (fight.isCombatRunning || fight.HasActiveInterlock)
                return AbilityRefusals.MustBeOutsideCombat;
            if (!AbilityTargets.HasLiveAttackSelection(caster, view, spawn, requireOpponent: true))
                return AbilityRefusals.InvalidTarget;
            if (!AbilityPassives.IsLoaded(caster, CombatSpecials.PoisonKnife))
                return AbilityRefusals.NotLoaded;
            if (CombatStyles.Active(caster) != CombatStyles.SpyStyle)
                return AbilityRefusals.WrongStyle;
            if (!AbilityTargets.WithinSelectionRange(caster, CombatSpecials.PoisonKnife, view))
                return AbilityRefusals.TargetOutOfRange;
            if (!AbilityTargets.HasSelectionSight(caster, AbilityCatalog.Find(CombatSpecials.PoisonKnife), view))
                return AbilityRefusals.TargetNotInSight;
            return 0;
        }
    }

    internal sealed class ThrownDotAdmissionHandler : ISpyAbilityHandler
    {
        public ushort[] AbilityIds => new[] { CombatSpecials.PoisonKnife, CombatSpecials.WoundingThrow };
        public bool TryRefuse(WorldClient client, ushort abilityId, ushort targetViewId, ushort? targetSpawnId, out uint refusalReason) =>
            ThrownDotAdmission.TryRefuse(client, abilityId, targetViewId, targetSpawnId, out refusalReason);
    }
}
