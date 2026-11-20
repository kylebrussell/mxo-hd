using System;
using System.Linq;

namespace hds
{
    /// <summary>
    /// Shipped GOB thrown damage bases and target/combat-state admission for Knife Thrower / Cutthroat / Assassin throws:
    /// Neuro-Dart (141: CastWhenUsed=1 outside-combat DOT/debuff), Blinding Throw (282: CastWhenUsed=2 live-enemy DOT/Blind),
    /// Throat Cutting Throw (340: CastWhenUsed=2 fixed thrown base 80), and Subduing Throw (347: CastWhenUsed=1 fixed thrown base 57).
    /// </summary>
    public static class MasterKnifeThrowerBatch
    {
        public const ushort NeuroDart = 141, BlindingThrow = 282, ThroatCuttingThrow = 340, SubduingThrow = 347;
        public static readonly ushort[] Abilities = { NeuroDart, BlindingThrow, ThroatCuttingThrow, SubduingThrow };

        public static bool Handles(ushort abilityId)
        {
            ushort id = AbilityCatalog.Canonical(abilityId);
            return id == NeuroDart || id == BlindingThrow || id == ThroatCuttingThrow || id == SubduingThrow;
        }

        public static bool TryRefuse(WorldClient client, ushort abilityId, ushort targetViewId, ushort? targetSpawnId, out uint refusalReason)
        {
            refusalReason = 0;
            ushort id = AbilityCatalog.Canonical(abilityId);
            if (!Handles(id)) return false;

            if ((id == NeuroDart || id == SubduingThrow) && CombatManager.GetHandler(client).isCombatRunning)
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
            if (source == null || (source.Id != ThroatCuttingThrow && source.Id != SubduingThrow)) return false;

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

    internal sealed class MasterKnifeThrowerBatchHandler : ISpyAbilityHandler
    {
        public ushort[] AbilityIds => MasterKnifeThrowerBatch.Abilities;
        public bool TryRefuse(WorldClient client, ushort abilityId, ushort targetViewId, ushort? targetSpawnId, out uint refusalReason) =>
            MasterKnifeThrowerBatch.TryRefuse(client, abilityId, targetViewId, targetSpawnId, out refusalReason);
        public bool TryResolveDamage(AbilityCatalog.Entry source, int characterLevel, Random random, out ushort damage) =>
            MasterKnifeThrowerBatch.TryResolve(source, out damage);
    }
}
