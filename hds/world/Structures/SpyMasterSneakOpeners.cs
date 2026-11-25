using System;
using System.Linq;

namespace hds
{
    /// <summary>
    /// Shipped GOB thrown damage bases, outside-combat live-enemy admission, and 2 s cast stealth retention for
    /// Master Spy sneak openers and free throws: Staggering Throw (348: VF3 35..191, 2 s cast),
    /// Diving Frankensteiner (385: 164 + 725 stealth), Freedom to Joints (386: 92 + 900 stealth),
    /// Infuriating Assault (1130: VF3 17..132, 2 s cast), and Warding Blow (1134: VF3 25..142, 2 s cast).
    /// </summary>
    public static class SpyMasterSneakOpeners
    {
        public const ushort StaggeringThrow = 348, DivingFrankensteiner = 385, FreedomToJoints = 386,
            InfuriatingAssault = 1130, WardingBlow = 1134;
        public static readonly ushort[] Abilities =
            { StaggeringThrow, DivingFrankensteiner, FreedomToJoints, InfuriatingAssault, WardingBlow };

        public static bool Handles(ushort abilityId)
        {
            ushort id = AbilityCatalog.Canonical(abilityId);
            return id == StaggeringThrow || id == DivingFrankensteiner || id == FreedomToJoints ||
                id == InfuriatingAssault || id == WardingBlow;
        }

        public static bool RetainsStealthDuringCast(ushort abilityId)
        {
            ushort id = AbilityCatalog.Canonical(abilityId);
            return id == StaggeringThrow || id == InfuriatingAssault || id == WardingBlow;
        }

        public static bool TryRefuse(WorldClient client, ushort abilityId, ushort targetViewId, ushort? targetSpawnId, out uint refusalReason)
        {
            refusalReason = 0;
            ushort id = AbilityCatalog.Canonical(abilityId);
            if (!Handles(id)) return false;
            if (id == StaggeringThrow || id == InfuriatingAssault)
                return SneakFreeThrowAdmission.TryRefuse(client, id, targetViewId, targetSpawnId, out refusalReason);
            if (CombatManager.GetHandler(client).isCombatRunning)
            {
                refusalReason = AbilityRefusals.MustBeOutsideCombat;
                return true;
            }
            bool freedom = AbilityCatalog.Canonical(abilityId) == FreedomToJoints;
            if (freedom && CombatStyles.Active(client) != CombatStyles.SpyStyle)
            {
                refusalReason = AbilityRefusals.WrongStyle;
                return true;
            }
            if (freedom && (PvpFlag.FlagsOf(client) & Stealth.StealthedFlag) == 0)
            {
                refusalReason = AbilityRefusals.CasterStateRequired;
                return true;
            }
            if (!AbilityTargets.HasLiveAttackSelection(client, targetViewId, targetSpawnId, requireOpponent: freedom))
            {
                refusalReason = AbilityRefusals.InvalidTarget;
                return true;
            }
            // Answer before spending IS or starting either timer, using the same range as landing.
            double range = AbilityCatalog.Find(abilityId).Number("Ability/SelectionRange");
            var mob = AbilityTargets.FindMob(client, targetViewId, out _);
            var player = mob == null ? PvpCombat.PlayerInView(client, targetViewId) : null;
            if (mob == null && player == null)
            {
                refusalReason = AbilityRefusals.InvalidTarget;
                return true;
            }
            var (x, y, z) = mob != null ? (mob.getXPos(), mob.getYPos(), mob.getZPos()) : CombatSight.Position(player);
            if (range > 0 && CombatCalculator.CalculateDistanceMetres(client.playerInstance.Position.getValue(), x, y, z) > range)
            {
                refusalReason = AbilityRefusals.TargetOutOfRange;
                return true;
            }
            if (freedom && !AbilityTargets.HasSelectionSight(client, AbilityCatalog.Find(abilityId), targetViewId))
            {
                refusalReason = AbilityRefusals.TargetNotInSight;
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

    internal sealed class SpyMasterSneakOpenersHandler : ISpyAbilityHandler
    {
        public ushort[] AbilityIds => SpyMasterSneakOpeners.Abilities;
        public bool RetainsStealthDuringCast(ushort abilityId) => SpyMasterSneakOpeners.RetainsStealthDuringCast(abilityId);
        public bool TryRefuse(WorldClient client, ushort abilityId, ushort targetViewId, ushort? targetSpawnId, out uint refusalReason) =>
            SpyMasterSneakOpeners.TryRefuse(client, abilityId, targetViewId, targetSpawnId, out refusalReason);
        public bool TryResolveDamage(AbilityCatalog.Entry source, int characterLevel, Random random, out ushort damage) =>
            SpyMasterSneakOpeners.TryResolve(source, characterLevel, out damage);
        public void OnLanded(WorldClient caster, ushort abilityId, ulong targetEntity)
        {
            ushort id = AbilityCatalog.Canonical(abilityId);
            if (id == SpyMasterSneakOpeners.StaggeringThrow || id == SpyMasterSneakOpeners.InfuriatingAssault)
                StealthShieldAbilities.PlayThrow(caster, targetEntity, abilityId);
        }
    }
}
