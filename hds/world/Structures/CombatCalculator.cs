using System;

namespace hds
{
    /// <summary>
    /// Calculates combat damage and selects visual effects.
    ///
    /// See docs/COMBAT-SYSTEM-ROADMAP.md for combat system documentation.
    /// </summary>
    public static class CombatCalculator
    {
        private static Random random = new Random();

        /// <summary>
        /// Base damage for unarmed melee attacks.
        /// TODO: Should be derived from player stats/abilities.
        /// </summary>
        private const int BASE_MELEE_DAMAGE = 15;

        /// <summary>
        /// Base damage for ranged attacks.
        /// TODO: Should be derived from weapon stats.
        /// </summary>
        private const int BASE_RANGED_DAMAGE = 20;

        /// <summary>
        /// Minimum damage multiplier for random variance.
        /// </summary>
        private const float MIN_VARIANCE = 0.8f;

        /// <summary>
        /// Maximum damage multiplier for random variance.
        /// </summary>
        private const float MAX_VARIANCE = 1.2f;

        /// <summary>
        /// Hand combat hit FX IDs for visual variety.
        /// These correspond to different hit locations/animations.
        /// </summary>
        private static readonly uint[] MeleeHitFxIds = new uint[]
        {
            (uint)FXList.FX_HANDCOMBAT_HIT_MF,   // Mid front
            (uint)FXList.FX_HANDCOMBAT_HIT_HF,   // High front
            (uint)FXList.FX_HANDCOMBAT_HIT_LF,   // Low front
            (uint)FXList.FX_HANDCOMBAT_HIT_HR,   // High right
            (uint)FXList.FX_HANDCOMBAT_HIT_HL,   // High left
            (uint)FXList.FX_HANDCOMBAT_HIT_MR,   // Mid right
            (uint)FXList.FX_HANDCOMBAT_HIT_ML,   // Mid left
        };

        /// <summary>
        /// Bullet hit FX IDs for ranged combat.
        /// </summary>
        private static readonly uint[] RangedHitFxIds = new uint[]
        {
            (uint)FXList.FX_BULLET_BODY_HITS_BULLETHIT_MF,
            (uint)FXList.FX_BULLET_BODY_HITS_BULLETHIT_HF,
            (uint)FXList.FX_BULLET_BODY_HITS_BULLETHIT_LF,
            (uint)FXList.FX_BULLET_BODY_HITS_BULLETHIT_MB,
        };

        /// <summary>
        /// Calculates melee damage from attacker to defender.
        /// Formula: baseDamage * (attackerLevel / defenderLevel) * random(0.8, 1.2)
        /// </summary>
        /// <param name="attackerLevel">Level of the attacker</param>
        /// <param name="defenderLevel">Level of the defender</param>
        /// <returns>Calculated damage value</returns>
        public static UInt16 CalculateMeleeDamage(UInt16 attackerLevel, UInt16 defenderLevel)
        {
            // Prevent division by zero
            if (defenderLevel == 0) defenderLevel = 1;
            if (attackerLevel == 0) attackerLevel = 1;

            // Level ratio affects damage - attacking higher level mobs is harder
            float levelRatio = (float)attackerLevel / (float)defenderLevel;

            // Clamp level ratio to reasonable bounds (0.5 to 2.0)
            levelRatio = Math.Max(0.5f, Math.Min(2.0f, levelRatio));

            // Random variance for damage variety
            float variance = MIN_VARIANCE + (float)random.NextDouble() * (MAX_VARIANCE - MIN_VARIANCE);

            // Calculate final damage
            float damage = BASE_MELEE_DAMAGE * levelRatio * variance;

            // Ensure minimum 1 damage
            return (UInt16)Math.Max(1, (int)damage);
        }

        /// <summary>
        /// Calculates ranged damage from attacker to defender.
        /// </summary>
        /// <param name="attackerLevel">Level of the attacker</param>
        /// <param name="defenderLevel">Level of the defender</param>
        /// <returns>Calculated damage value</returns>
        public static UInt16 CalculateRangedDamage(UInt16 attackerLevel, UInt16 defenderLevel)
        {
            if (defenderLevel == 0) defenderLevel = 1;
            if (attackerLevel == 0) attackerLevel = 1;

            float levelRatio = (float)attackerLevel / (float)defenderLevel;
            levelRatio = Math.Max(0.5f, Math.Min(2.0f, levelRatio));

            float variance = MIN_VARIANCE + (float)random.NextDouble() * (MAX_VARIANCE - MIN_VARIANCE);
            float damage = BASE_RANGED_DAMAGE * levelRatio * variance;

            return (UInt16)Math.Max(1, (int)damage);
        }

        /// <summary>
        /// Gets a random melee hit FX ID for visual variety.
        /// </summary>
        public static uint GetRandomMeleeHitFx()
        {
            return MeleeHitFxIds[random.Next(MeleeHitFxIds.Length)];
        }

        /// <summary>
        /// Gets a random ranged hit FX ID for visual variety.
        /// </summary>
        public static uint GetRandomRangedHitFx()
        {
            return RangedHitFxIds[random.Next(RangedHitFxIds.Length)];
        }

        /// <summary>
        /// Calculates damage that a mob deals to a player.
        /// Mobs deal damage based on their level relative to the player.
        /// </summary>
        /// <param name="mobLevel">Level of the attacking mob</param>
        /// <param name="playerLevel">Level of the player being attacked</param>
        /// <returns>Calculated damage value</returns>
        public static UInt16 CalculateMobDamage(UInt16 mobLevel, UInt16 playerLevel)
        {
            if (playerLevel == 0) playerLevel = 1;
            if (mobLevel == 0) mobLevel = 1;

            // Mobs use a slightly lower base damage than players
            const int MOB_BASE_DAMAGE = 10;

            float levelRatio = (float)mobLevel / (float)playerLevel;
            levelRatio = Math.Max(0.3f, Math.Min(1.5f, levelRatio));

            float variance = MIN_VARIANCE + (float)random.NextDouble() * (MAX_VARIANCE - MIN_VARIANCE);
            float damage = MOB_BASE_DAMAGE * levelRatio * variance;

            return (UInt16)Math.Max(1, (int)damage);
        }
    }
}
