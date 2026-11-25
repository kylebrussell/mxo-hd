using System;

namespace hds
{
    /// <summary>
    /// Selected-opponent admission and interlock presentation for the two Assassin/Master Assassin timed Spy throws:
    /// Blinding Throw (282) and Paralyzing Throw (1135). Both GOBs store CastWhenUsed2, CastingTime2s, a16 m selector,
    /// a required Spy Style stance (0x80116000 = 1112), CombatSpecialMove1 and HasSuccessInterlockMove1, and the ILDB
    /// names one success move for each ("Blinding Throw SR" 291, "Paralyzing Throw SR" 292).
    /// At rest the completed cast lands as an ordinary selected throw. When the caster is interlocked with that same
    /// opponent at completion, the throw becomes the next special move, like the reviewed timed gun specials:
    /// a won exchange plays the named move and lands the ordinary effects; a lost exchange plays the ordinary round.
    /// </summary>
    public static class SpyTimedThrows
    {
        public const ushort BlindingThrow = 282, ParalyzingThrow = 1135;
        public static readonly ushort[] Abilities = { BlindingThrow, ParalyzingThrow };

        public static bool Handles(ushort abilityId)
        {
            ushort id = AbilityCatalog.Canonical(abilityId);
            return id == BlindingThrow || id == ParalyzingThrow;
        }

        public static bool TryRefuse(WorldClient client, ushort abilityId, ushort targetViewId, ushort? targetSpawnId, out uint refusalReason)
        {
            refusalReason = 0;
            ushort id = AbilityCatalog.Canonical(abilityId);
            if (!Handles(id)) return false;
            refusalReason = Refusal(client, id, targetViewId, targetSpawnId);
            return refusalReason != 0;
        }

        internal static uint Refusal(WorldClient client, ushort id, ushort targetViewId, ushort? targetSpawnId)
        {
            if (!AbilityTargets.HasLiveAttackSelection(client, targetViewId, targetSpawnId, requireOpponent: true))
                return AbilityRefusals.InvalidTarget;
            if (!AbilityTargets.WithinSelectionRange(client, id, targetViewId))
                return AbilityRefusals.TargetOutOfRange;
            if (!AbilityTargets.HasSelectionSight(client, AbilityCatalog.Find(id), targetViewId))
                return AbilityRefusals.TargetNotInSight;
            return 0;
        }

        /// <summary>The completed cast may become the next interlock move (requires the stored Spy Style stance).</summary>
        public static bool CanQueue(WorldClient caster, ushort abilityId)
        {
            ushort id = AbilityCatalog.Canonical(abilityId);
            return (Handles(id) || id == CombatSpecials.WoundingThrow) && CombatStyles.CanUse(caster, id);
        }
    }

    internal sealed class SpyTimedThrowsHandler : ISpyAbilityHandler
    {
        public ushort[] AbilityIds => SpyTimedThrows.Abilities;
        public bool TryRefuse(WorldClient client, ushort abilityId, ushort targetViewId, ushort? targetSpawnId, out uint refusalReason) =>
            SpyTimedThrows.TryRefuse(client, abilityId, targetViewId, targetSpawnId, out refusalReason);
        public bool TryResolveDamage(AbilityCatalog.Entry source, int characterLevel, Random random, out ushort damage) =>
            ParalyzingThrowDamage.TryResolve(source, out damage);
    }
}
