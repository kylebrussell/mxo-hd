using System;
using System.Linq;

namespace hds
{
    /// <summary>
    /// Shipped GOB fixed physical damage bases for the remaining Martial Arts Master/Grandmaster specials:
    /// Swirling Ki Summon (268: 2, 3 m PBAE), Aikido Spinning Clay Pigeon (296: 375, close-combat interlock),
    /// and Ki Burst (406: 23, 4 m PBAE).
    /// </summary>
    public static class MartialArtsRemainingSpecials
    {
        public const ushort SwirlingKiSummon = 268, AikidoSpinningClayPigeon = 296, KiBurst = 406;
        public static readonly ushort[] Abilities = { SwirlingKiSummon, AikidoSpinningClayPigeon, KiBurst };

        public static bool Handles(ushort abilityId)
        {
            ushort id = AbilityCatalog.Canonical(abilityId);
            return id == SwirlingKiSummon || id == AikidoSpinningClayPigeon || id == KiBurst;
        }

        public static bool TryRefuse(WorldClient client, ushort abilityId, ushort targetViewId, ushort? targetSpawnId, out uint refusalReason)
        {
            refusalReason = 0;
            ushort id = AbilityCatalog.Canonical(abilityId);
            // 268 and 406 are self-centered PBAEs (CastWhenUsed=2, CastValidTarget=0) handled by CasterHostileArea.
            if (id != AikidoSpinningClayPigeon) return false;

            var handler = CombatManager.GetHandler(client);
            if (!handler.isCombatRunning || !handler.PowerShotOpponent(targetViewId, targetSpawnId).HasValue)
            {
                refusalReason = AbilityRefusals.InvalidTarget;
                return true;
            }
            return false;
        }

        public static bool TryResolve(AbilityCatalog.Entry source, out ushort damage)
        {
            damage = 0;
            if (source == null || !Handles(source.Id)) return false;

            var health = source.Modifiers.Where(m => m.Target == 108 && m.Number("WhenEnabled", -1) == 2).ToArray();
            if (health.Length != 1) return false;

            var shipped = health[0];
            if (shipped.Number("DamageType", -1) != 0 ||
                shipped.Number("DurationType", -1) != 1 ||
                shipped.Number("ValueFunction") != 0 ||
                shipped.Number("MinValue") != shipped.Number("MaxValue") ||
                !AbilityValues.TryValue(shipped, 0, 0, out int amount) ||
                amount <= 0 || amount > ushort.MaxValue)
                return false;

            damage = (ushort)amount;
            return true;
        }
    }

    internal sealed class MartialArtsRemainingSpecialsHandler : ISpyAbilityHandler
    {
        public ushort[] AbilityIds => MartialArtsRemainingSpecials.Abilities;
        public bool TryRefuse(WorldClient client, ushort abilityId, ushort targetViewId, ushort? targetSpawnId, out uint refusalReason) =>
            MartialArtsRemainingSpecials.TryRefuse(client, abilityId, targetViewId, targetSpawnId, out refusalReason);
        public bool TryResolveDamage(AbilityCatalog.Entry source, int characterLevel, Random random, out ushort damage) =>
            MartialArtsRemainingSpecials.TryResolve(source, out damage);
    }
}
