using System;
using System.Linq;

namespace hds
{
    /// <summary>
    /// Shipped GOB thrown damage bases, outside-combat live-enemy admission, and 2 s cast stealth retention for
    /// Spy sneak openers: Drop Kick (153: 52 + 150 stealth), Spin Throw (286: 80 + 300 stealth),
    /// Topsy Turvy (288: 37 + 775 stealth), Punt (289: VF3 20 + 3*lvl + 600 stealth),
    /// Leg Throw (1131: VF3 24..154, 2 s cast), and Weakening Blow (1133: VF3 21..113, 2 s cast).
    /// </summary>
    public static class SpySneakOpeners
    {
        public const ushort DropKick = 153, SpinThrow = 286, TopsyTurvy = 288, Punt = 289,
            LegThrow = 1131, WeakeningBlow = 1133;
        public static readonly ushort[] Abilities = { DropKick, SpinThrow, TopsyTurvy, Punt, LegThrow, WeakeningBlow };

        public static bool Handles(ushort abilityId)
        {
            ushort id = AbilityCatalog.Canonical(abilityId);
            return id == DropKick || id == SpinThrow || id == TopsyTurvy ||
                id == Punt || id == LegThrow || id == WeakeningBlow;
        }

        public static bool RetainsStealthDuringCast(ushort abilityId)
        {
            ushort id = AbilityCatalog.Canonical(abilityId);
            return id == LegThrow || id == WeakeningBlow;
        }

        public static bool TryRefuse(WorldClient client, ushort abilityId, ushort targetViewId, ushort? targetSpawnId, out uint refusalReason)
        {
            refusalReason = 0;
            ushort id = AbilityCatalog.Canonical(abilityId);
            if (!Handles(id)) return false;
            // SneakFree throws: Spy Style, Stealthed, rear 50-degree approach, range and sight.
            if (id == LegThrow || id == WeakeningBlow)
                return SneakFreeThrowAdmission.TryRefuse(client, id, targetViewId, targetSpawnId, out refusalReason);
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

        public static bool TryResolve(AbilityCatalog.Entry source, int characterLevel, out ushort damage)
        {
            damage = 0;
            if (source == null || !Handles(source.Id)) return false;

            var health = source.Modifiers.Where(m => m.Target == 108 && m.Number("WhenEnabled", -1) == 2).ToArray();
            if (health.Length != 1) return false;

            var shipped = health[0];
            if (shipped.Number("DamageType", -1) != 7 || shipped.Number("DurationType", -1) != 1)
                return false;

            int vf = (int)shipped.Number("ValueFunction");
            if (vf != 0 && vf != 3) return false;
            if (vf == 0 && (!shipped.Has("MinValue") || !shipped.Has("MaxValue") || shipped.Number("MinValue") != shipped.Number("MaxValue")))
                return false;

            if (!AbilityValues.TryValue(shipped, 0, characterLevel, out int amount) || amount <= 0 || amount > ushort.MaxValue)
                return false;

            damage = (ushort)amount;
            return true;
        }
    }

    internal sealed class SpySneakOpenersHandler : ISpyAbilityHandler
    {
        public ushort[] AbilityIds => SpySneakOpeners.Abilities;
        public bool RetainsStealthDuringCast(ushort abilityId) => SpySneakOpeners.RetainsStealthDuringCast(abilityId);
        public bool TryRefuse(WorldClient client, ushort abilityId, ushort targetViewId, ushort? targetSpawnId, out uint refusalReason) =>
            SpySneakOpeners.TryRefuse(client, abilityId, targetViewId, targetSpawnId, out refusalReason);
        public bool TryResolveDamage(AbilityCatalog.Entry source, int characterLevel, Random random, out ushort damage) =>
            SpySneakOpeners.TryResolve(source, characterLevel, out damage);
        // The SneakFree*_Subject callbacks play the targeted knife throw on the selected opponent.
        public void OnLanded(WorldClient caster, ushort abilityId, ulong targetEntity)
        {
            ushort id = AbilityCatalog.Canonical(abilityId);
            if (id == SpySneakOpeners.LegThrow || id == SpySneakOpeners.WeakeningBlow)
                StealthShieldAbilities.PlayThrow(caster, targetEntity, abilityId);
        }
    }
}
