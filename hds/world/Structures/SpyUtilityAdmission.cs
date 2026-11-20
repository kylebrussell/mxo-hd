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

            if (CombatManager.GetHandler(client).isCombatRunning)
            {
                refusalReason = AbilityRefusals.MustBeOutsideCombat;
                return true;
            }
            if ((canonical == StealthShieldAbilities.BullDog || canonical == StealthShieldAbilities.DisableEvasion) &&
                !AbilityTargets.HasLiveAttackSelection(client, targetViewId, targetSpawnId))
            {
                refusalReason = AbilityRefusals.InvalidTarget;
                return true;
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
