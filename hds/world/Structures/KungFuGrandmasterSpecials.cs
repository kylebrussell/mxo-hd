using System;
using System.Linq;

namespace hds
{
    /// <summary>
    /// Shipped GOB fixed physical damage bases and close-combat interlock opponent admission for
    /// Kung Fu Grandmaster specials: Triple Front Kick (427: 91), Suicidal Butterfly (428: 25),
    /// Extreme Falling Kick (429: 46), and Wooden Dummy Drill (430: 107).
    /// </summary>
    public static class KungFuGrandmasterSpecials
    {
        public const ushort TripleFrontKick = 427, SuicidalButterfly = 428,
            ExtremeFallingKick = 429, WoodenDummyDrill = 430;
        public static readonly ushort[] Abilities = { TripleFrontKick, SuicidalButterfly, ExtremeFallingKick, WoodenDummyDrill };

        public static bool Handles(ushort abilityId)
        {
            ushort id = AbilityCatalog.Canonical(abilityId);
            return id == TripleFrontKick || id == SuicidalButterfly ||
                id == ExtremeFallingKick || id == WoodenDummyDrill;
        }

        public static bool TryRefuse(WorldClient client, ushort abilityId, ushort targetViewId, ushort? targetSpawnId, out uint refusalReason)
        {
            refusalReason = 0;
            if (!Handles(abilityId)) return false;

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

    internal sealed class KungFuGrandmasterSpecialsHandler : ISpyAbilityHandler
    {
        public ushort[] AbilityIds => KungFuGrandmasterSpecials.Abilities;
        public bool TryRefuse(WorldClient client, ushort abilityId, ushort targetViewId, ushort? targetSpawnId, out uint refusalReason) =>
            KungFuGrandmasterSpecials.TryRefuse(client, abilityId, targetViewId, targetSpawnId, out refusalReason);
        public bool TryResolveDamage(AbilityCatalog.Entry source, int characterLevel, Random random, out ushort damage) =>
            KungFuGrandmasterSpecials.TryResolve(source, out damage);
    }
}
