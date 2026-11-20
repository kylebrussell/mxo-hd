using System;
using System.Linq;

namespace hds
{
    /// <summary>
    /// Shipped GOB ValueFunction 4 thrown damage (8..10, levels 22..50) and close-combat opponent admission
    /// for Canny Strike (295), whose legacy CSV row has 0/0.
    /// </summary>
    public static class CannyStrikeDamage
    {
        public const ushort CannyStrike = 295;

        public static bool TryRefuse(WorldClient client, ushort abilityId, ushort targetViewId, ushort? targetSpawnId, out uint refusalReason)
        {
            refusalReason = 0;
            if (AbilityCatalog.Canonical(abilityId) != CannyStrike) return false;
            var handler = CombatManager.GetHandler(client);
            if (!handler.isCombatRunning || !handler.PowerShotOpponent(targetViewId, targetSpawnId).HasValue)
            {
                refusalReason = AbilityRefusals.InvalidTarget;
                return true;
            }
            return false;
        }

        public static bool TryResolve(AbilityCatalog.Entry source, int characterLevel, Random random, out ushort damage)
        {
            damage = 0;
            if (source == null || source.Id != CannyStrike) return false;

            var health = source.Modifiers.Where(m => m.Target == 108 && m.Number("WhenEnabled", -1) == 2).ToArray();
            if (health.Length != 1) return false;

            var shipped = health[0];
            if (shipped.Number("DamageType", -1) != 7 ||
                shipped.Number("DurationType", -1) != 1 ||
                shipped.Number("ValueFunction", -1) != 4)
                return false;

            int amount = Sample(source, shipped, characterLevel, random);
            if (amount <= 0 || amount > ushort.MaxValue) return false;

            damage = (ushort)amount;
            return true;
        }

        internal static int Sample(AbilityCatalog.Entry entry, AbilityCatalog.Modifier modifier, int level, Random random)
        {
            int min = (short)modifier.Number("MinValue"), max = (short)modifier.Number("MaxValue");
            int minLevel = (int)entry.Number("Ability/MinValueScaleLevel");
            int maxLevel = (int)entry.Number("Ability/MaxValueScaleLevel");
            if (minLevel == 0 || maxLevel == 0 || minLevel == maxLevel) return min;

            // 625180c5: same native ValueFunction 4 Gaussian clamp as AwakenedPowerShot.Sample.
            float midpoint = (min + max) / 2f;
            float topLevelCenter = (float)((max - (double)midpoint) * 0.8f + midpoint);
            float progress = Math.Clamp((float)(1.0 - (double)(maxLevel - level) / (maxLevel - minLevel)), 0, 1);
            int center = (int)((topLevelCenter - (double)midpoint) * progress + midpoint);
            int roll = CombatCalculator.GaussianClamped(random, center - min, (max - min) / 6, 0, max - min);
            return (int)((min + roll) * (double)(float)modifier.Number("ValueScale", 1));
        }
    }

    internal sealed class CannyStrikeHandler : ISpyAbilityHandler
    {
        public ushort[] AbilityIds => new[] { CannyStrikeDamage.CannyStrike };
        public bool TryRefuse(WorldClient client, ushort abilityId, ushort targetViewId, ushort? targetSpawnId, out uint refusalReason) =>
            CannyStrikeDamage.TryRefuse(client, abilityId, targetViewId, targetSpawnId, out refusalReason);
        public bool TryResolveDamage(AbilityCatalog.Entry source, int characterLevel, Random random, out ushort damage) =>
            CannyStrikeDamage.TryResolve(source, characterLevel, random, out damage);
    }
}
