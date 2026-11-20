namespace hds
{
    internal sealed class SpyEscapeAdmissionHandler : ISpyAbilityHandler
    {
        public ushort[] AbilityIds => new[] { CombatSpecials.SideRollEscape, CombatSpecials.CartwheelEscape };

        public bool TryRefuse(WorldClient client, ushort abilityId, ushort viewId, ushort? spawnId, out uint refusalReason)
        {
            var combat = CombatManager.GetHandler(client);
            bool valid = combat.isCombatRunning && combat.PowerShotOpponent(viewId, spawnId).HasValue;
            refusalReason = valid ? 0u : AbilityRefusals.InvalidTarget;
            return !valid;
        }
    }
}
