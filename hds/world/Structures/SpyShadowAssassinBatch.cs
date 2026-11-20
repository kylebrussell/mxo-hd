using System;
using System.Linq;

namespace hds
{
    /// <summary>
    /// Shipped GOB thrown damage bases and outside-combat live-enemy admission for:
    /// Sever Artery (341: CastWhenUsed=1 outside-combat ThrownDOT/state admission),
    /// Ghost Strike (365: CastWhenUsed=1 fixed thrown base 29), and
    /// Reverse Bull Dog (388: CastWhenUsed=1 fixed thrown base 56 + 1200 stealth opener).
    /// </summary>
    public static class SpyShadowAssassinBatch
    {
        public const ushort SeverArtery = 341, GhostStrike = 365, ReverseBullDog = 388;
        public static readonly ushort[] Abilities = { SeverArtery, GhostStrike, ReverseBullDog };

        public static bool Handles(ushort abilityId)
        {
            ushort id = AbilityCatalog.Canonical(abilityId);
            return id == SeverArtery || id == GhostStrike || id == ReverseBullDog;
        }

        public static bool TryRefuse(WorldClient client, ushort abilityId, ushort targetViewId, ushort? targetSpawnId, out uint refusalReason)
        {
            refusalReason = 0;
            if (!Handles(abilityId)) return false;

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

        public static bool TryResolve(AbilityCatalog.Entry source, out ushort damage)
        {
            damage = 0;
            if (source == null || (source.Id != GhostStrike && source.Id != ReverseBullDog)) return false;

            var health = source.Modifiers.Where(m => m.Target == 108 && m.Number("WhenEnabled", -1) == 2).ToArray();
            if (health.Length != 1) return false;

            var shipped = health[0];
            if (shipped.Number("DamageType", -1) != 7 ||
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

    internal sealed class SpyShadowAssassinBatchHandler : ISpyAbilityHandler
    {
        public ushort[] AbilityIds => SpyShadowAssassinBatch.Abilities;
        public bool TryRefuse(WorldClient client, ushort abilityId, ushort targetViewId, ushort? targetSpawnId, out uint refusalReason) =>
            SpyShadowAssassinBatch.TryRefuse(client, abilityId, targetViewId, targetSpawnId, out refusalReason);
        public bool TryResolveDamage(AbilityCatalog.Entry source, int characterLevel, Random random, out ushort damage) =>
            SpyShadowAssassinBatch.TryResolve(source, out damage);
    }
}
