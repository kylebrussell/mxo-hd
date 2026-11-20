namespace hds
{
    /// <summary>
    /// Close-combat interlock opponent admission for Berserker Attack (535, CastValidTarget=3, CombatSpecialMove=1).
    /// </summary>
    public static class BerserkerAttackAdmission
    {
        public const ushort BerserkerAttack = CombatSpecials.BerserkerAttack;

        public static bool TryRefuse(WorldClient client, ushort abilityId, ushort targetViewId, ushort? targetSpawnId, out uint refusalReason)
        {
            refusalReason = 0;
            if (AbilityCatalog.Canonical(abilityId) != BerserkerAttack) return false;

            var handler = CombatManager.GetHandler(client);
            if (!handler.isCombatRunning || !handler.PowerShotOpponent(targetViewId, targetSpawnId).HasValue)
            {
                refusalReason = AbilityRefusals.InvalidTarget;
                return true;
            }
            return false;
        }
    }

    internal sealed class BerserkerAttackAdmissionHandler : ISpyAbilityHandler
    {
        public ushort[] AbilityIds => new[] { BerserkerAttackAdmission.BerserkerAttack };
        public bool TryRefuse(WorldClient client, ushort abilityId, ushort targetViewId, ushort? targetSpawnId, out uint refusalReason) =>
            BerserkerAttackAdmission.TryRefuse(client, abilityId, targetViewId, targetSpawnId, out refusalReason);
    }
}
