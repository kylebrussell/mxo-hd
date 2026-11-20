namespace hds
{
    /// <summary>
    /// Outside-combat and live-enemy admission for Spinning Backsweep (152, level 4 Spy stealth opener).
    /// </summary>
    public static class SpinningBacksweepAdmission
    {
        public const ushort SpinningBacksweep = 152;

        public static bool TryRefuse(WorldClient client, ushort abilityId, ushort targetViewId, ushort? targetSpawnId, out uint refusalReason)
        {
            refusalReason = 0;
            if (AbilityCatalog.Canonical(abilityId) != SpinningBacksweep) return false;
            if (CombatManager.GetHandler(client).isCombatRunning)
            {
                refusalReason = AbilityRefusals.MustBeOutsideCombat;
                return true;
            }
            if (!AbilityTargets.HasLiveAttackSelection(client, targetViewId, targetSpawnId))
            {
                refusalReason = AbilityRefusals.InvalidTarget;
                return true;
            }
            return false;
        }
    }

    internal sealed class SpinningBacksweepHandler : ISpyAbilityHandler
    {
        public ushort[] AbilityIds => new[] { SpinningBacksweepAdmission.SpinningBacksweep };
        public bool TryRefuse(WorldClient client, ushort abilityId, ushort targetViewId, ushort? targetSpawnId, out uint refusalReason) =>
            SpinningBacksweepAdmission.TryRefuse(client, abilityId, targetViewId, targetSpawnId, out refusalReason);
    }
}
