namespace hds
{
    /// <summary>
    /// Close-combat interlock opponent admission for Martial Arts Initiate starter specials:
    /// Guard Breaker (138, CastWhenUsed=3) and Iron Guard (139, CastWhenUsed=3).
    /// </summary>
    public static class MartialArtsStarterAdmission
    {
        public const ushort GuardBreaker = 138, IronGuard = 139;

        public static bool TryRefuse(WorldClient client, ushort abilityId, ushort targetViewId, ushort? targetSpawnId, out uint refusalReason)
        {
            refusalReason = 0;
            ushort canonical = AbilityCatalog.Canonical(abilityId);
            if (canonical != GuardBreaker && canonical != IronGuard) return false;

            var handler = CombatManager.GetHandler(client);
            if (!handler.isCombatRunning || !handler.PowerShotOpponent(targetViewId, targetSpawnId).HasValue)
            {
                refusalReason = AbilityRefusals.InvalidTarget;
                return true;
            }
            return false;
        }
    }

    internal sealed class MartialArtsStarterAdmissionHandler : ISpyAbilityHandler
    {
        public ushort[] AbilityIds => new[] { MartialArtsStarterAdmission.GuardBreaker, MartialArtsStarterAdmission.IronGuard };
        public bool TryRefuse(WorldClient client, ushort abilityId, ushort targetViewId, ushort? targetSpawnId, out uint refusalReason) =>
            MartialArtsStarterAdmission.TryRefuse(client, abilityId, targetViewId, targetSpawnId, out refusalReason);
    }
}
