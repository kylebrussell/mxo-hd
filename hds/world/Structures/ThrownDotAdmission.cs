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

            if (canonical == CombatSpecials.PoisonKnife && CombatManager.GetHandler(caster).isCombatRunning)
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
    }

    internal sealed class ThrownDotAdmissionHandler : ISpyAbilityHandler
    {
        public ushort[] AbilityIds => new[] { CombatSpecials.PoisonKnife, CombatSpecials.WoundingThrow };
        public bool TryRefuse(WorldClient client, ushort abilityId, ushort targetViewId, ushort? targetSpawnId, out uint refusalReason) =>
            ThrownDotAdmission.TryRefuse(client, abilityId, targetViewId, targetSpawnId, out refusalReason);
    }
}
