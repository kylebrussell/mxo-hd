namespace hds
{
    /// <summary>
    /// Active close-combat interlock opponent admission for Leg Sweep (134, Infiltrator Spy Style special).
    /// </summary>
    public static class LegSweepAdmission
    {
        public const ushort LegSweep = 134;

        public static bool TryRefuse(WorldClient client, ushort abilityId, ushort targetViewId, ushort? targetSpawnId, out uint refusalReason)
        {
            refusalReason = 0;
            if (AbilityCatalog.Canonical(abilityId) != LegSweep) return false;
            var handler = CombatManager.GetHandler(client);
            if (!handler.HasActiveInterlock || !handler.PowerShotOpponent(targetViewId, targetSpawnId).HasValue)
            {
                refusalReason = AbilityRefusals.InvalidTarget;
                return true;
            }
            return false;
        }
    }

    internal sealed class LegSweepAdmissionHandler : ISpyAbilityHandler
    {
        public ushort[] AbilityIds => new[] { LegSweepAdmission.LegSweep };

        public bool TryRefuse(WorldClient client, ushort abilityId, ushort targetViewId, ushort? targetSpawnId, out uint refusalReason) =>
            LegSweepAdmission.TryRefuse(client, abilityId, targetViewId, targetSpawnId, out refusalReason);
    }
}
