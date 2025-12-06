using System;

namespace hds
{
    /// <summary>
    /// Combat tactics follow a rock-paper-scissors system:
    /// - Speed beats Power (faster attacks land first)
    /// - Power beats Grab (raw damage breaks through)
    /// - Grab beats Speed (catches quick attacks)
    /// </summary>
    public enum CombatTactic
    {
        /// <summary>No tactic selected - balanced combat</summary>
        None = 0,
        /// <summary>Speed tactic - faster attacks, higher evasion, lower damage</summary>
        Speed = 1,
        /// <summary>Power tactic - higher damage, slower attacks</summary>
        Power = 2,
        /// <summary>Grab/Block tactic - defensive, reduces incoming damage</summary>
        Grab = 3
    }

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
        /// Includes body hits from multiple angles for visual variety.
        /// </summary>
        private static readonly uint[] RangedHitFxIds = new uint[]
        {
            (uint)FXList.FX_BULLET_BODY_HITS_BULLETHIT_MF,   // Mid front
            (uint)FXList.FX_BULLET_BODY_HITS_BULLETHIT_HF,   // High front
            (uint)FXList.FX_BULLET_BODY_HITS_BULLETHIT_LF,   // Low front
            (uint)FXList.FX_BULLET_BODY_HITS_BULLETHIT_MB,   // Mid back
            (uint)FXList.FX_BULLET_BODY_HITS_BULLETHIT_HB,   // High back
            (uint)FXList.FX_BULLET_BODY_HITS_BULLETHIT_HR,   // High right
            (uint)FXList.FX_BULLET_BODY_HITS_BULLETHIT_HL,   // High left
            (uint)FXList.FX_BULLET_BODY_HITS_BULLETHIT_MR,   // Mid right
            (uint)FXList.FX_BULLET_BODY_HITS_BULLETHIT_ML,   // Mid left
            (uint)FXList.FX_BULLET_BODY_HITS_BULLETHIT_LR,   // Low right
            (uint)FXList.FX_BULLET_BODY_HITS_BULLETHIT_LL,   // Low left
        };

        /// <summary>
        /// Muzzle flash FX IDs for ranged combat.
        /// TODO: Send muzzle flash to attacker on ranged attacks.
        /// </summary>
        private static readonly uint[] MuzzleFlashFxIds = new uint[]
        {
            (uint)FXList.FX_WEAPON_SEMIAUTO_PISTOL1_MUZZLE_FLASH,
            (uint)FXList.FX_WEAPON_SEMIAUTO_PISTOL3_MUZZLE_FLASH,
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
        /// <param name="distance">Optional distance to target (affects damage falloff)</param>
        /// <returns>Calculated damage value</returns>
        public static UInt16 CalculateRangedDamage(UInt16 attackerLevel, UInt16 defenderLevel, float distance = 0f)
        {
            if (defenderLevel == 0) defenderLevel = 1;
            if (attackerLevel == 0) attackerLevel = 1;

            float levelRatio = (float)attackerLevel / (float)defenderLevel;
            levelRatio = Math.Max(0.5f, Math.Min(2.0f, levelRatio));

            float variance = MIN_VARIANCE + (float)random.NextDouble() * (MAX_VARIANCE - MIN_VARIANCE);
            float damage = BASE_RANGED_DAMAGE * levelRatio * variance;

            // Apply distance falloff for ranged combat
            // Optimal range is 10-30 meters, damage falls off beyond that
            if (distance > 0f)
            {
                float distanceModifier = CalculateDistanceModifier(distance);
                damage *= distanceModifier;
            }

            return (UInt16)Math.Max(1, (int)damage);
        }

        /// <summary>
        /// Calculates a damage modifier based on distance.
        /// - Close range (under 5m): 90% damage (too close for good aim)
        /// - Optimal range (5-30m): 100% damage
        /// - Medium range (30-50m): 80% damage
        /// - Long range (50m+): 60% damage minimum
        /// </summary>
        private static float CalculateDistanceModifier(float distance)
        {
            if (distance < 5f)
            {
                return 0.9f; // Too close
            }
            else if (distance <= 30f)
            {
                return 1.0f; // Optimal range
            }
            else if (distance <= 50f)
            {
                // Linear falloff from 100% to 80%
                return 1.0f - ((distance - 30f) / 20f) * 0.2f;
            }
            else
            {
                // Beyond 50m, minimum 60% damage
                return Math.Max(0.6f, 0.8f - ((distance - 50f) / 100f) * 0.2f);
            }
        }

        /// <summary>
        /// Calculates distance between player and mob positions.
        /// </summary>
        /// <param name="playerPositionBytes">Player position as LtVector3d byte array</param>
        /// <param name="mobX">Mob X coordinate</param>
        /// <param name="mobY">Mob Y coordinate</param>
        /// <param name="mobZ">Mob Z coordinate</param>
        /// <returns>Distance in game units (meters)</returns>
        public static float CalculateDistance(byte[] playerPositionBytes, double mobX, double mobY, double mobZ)
        {
            if (playerPositionBytes == null || playerPositionBytes.Length < 24)
            {
                return 0f; // Invalid position data
            }

            double playerX = 0, playerY = 0, playerZ = 0;
            NumericalUtils.LtVector3dToDoubles(playerPositionBytes, ref playerX, ref playerY, ref playerZ);

            double dx = playerX - mobX;
            double dy = playerY - mobY;
            double dz = playerZ - mobZ;

            return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
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
        /// Gets a random muzzle flash FX ID.
        /// TODO: Wire this up to play on the attacker during ranged attacks.
        /// </summary>
        public static uint GetRandomMuzzleFlashFx()
        {
            return MuzzleFlashFxIds[random.Next(MuzzleFlashFxIds.Length)];
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

        #region Tactic System

        /// <summary>
        /// IS (Inner Strength) cost for changing combat tactics.
        /// </summary>
        public const int TACTIC_CHANGE_IS_COST = 10;

        /// <summary>
        /// IS cost per combat round while in combat.
        /// </summary>
        public const int COMBAT_ROUND_IS_COST = 2;

        /// <summary>
        /// Gets the damage modifier for the attacker's tactic.
        /// </summary>
        /// <param name="attackerTactic">Attacker's current tactic</param>
        /// <param name="defenderTactic">Defender's current tactic</param>
        /// <returns>Damage multiplier (1.0 = normal, >1 = bonus, <1 = penalty)</returns>
        public static float GetTacticDamageModifier(CombatTactic attackerTactic, CombatTactic defenderTactic)
        {
            // No tactic = no modifier
            if (attackerTactic == CombatTactic.None)
                return 1.0f;

            // Same tactic = neutral
            if (attackerTactic == defenderTactic)
                return 1.0f;

            // Rock-paper-scissors: Speed > Power > Grab > Speed
            switch (attackerTactic)
            {
                case CombatTactic.Speed:
                    if (defenderTactic == CombatTactic.Power)
                        return 1.25f; // Speed beats Power - land hits faster
                    if (defenderTactic == CombatTactic.Grab)
                        return 0.75f; // Grab catches Speed
                    break;

                case CombatTactic.Power:
                    if (defenderTactic == CombatTactic.Grab)
                        return 1.25f; // Power breaks through Grab
                    if (defenderTactic == CombatTactic.Speed)
                        return 0.75f; // Speed evades Power
                    break;

                case CombatTactic.Grab:
                    if (defenderTactic == CombatTactic.Speed)
                        return 1.25f; // Grab catches Speed
                    if (defenderTactic == CombatTactic.Power)
                        return 0.75f; // Power breaks Grab
                    break;
            }

            return 1.0f;
        }

        /// <summary>
        /// Gets the base damage modifier for a tactic (independent of opponent).
        /// Speed: Less damage but more attacks
        /// Power: More damage per hit
        /// Grab: Balanced damage
        /// </summary>
        public static float GetTacticBaseDamageModifier(CombatTactic tactic)
        {
            switch (tactic)
            {
                case CombatTactic.Speed:
                    return 0.85f; // Less damage per hit, but faster
                case CombatTactic.Power:
                    return 1.20f; // More damage per hit
                case CombatTactic.Grab:
                    return 0.95f; // Slightly less damage, defensive focus
                default:
                    return 1.0f;
            }
        }

        /// <summary>
        /// Gets the defense modifier for a tactic (reduces incoming damage).
        /// </summary>
        public static float GetTacticDefenseModifier(CombatTactic tactic)
        {
            switch (tactic)
            {
                case CombatTactic.Speed:
                    return 1.10f; // Take 10% more damage (offense focus)
                case CombatTactic.Power:
                    return 1.05f; // Take 5% more damage
                case CombatTactic.Grab:
                    return 0.80f; // Take 20% less damage (defensive)
                default:
                    return 1.0f;
            }
        }

        /// <summary>
        /// Calculates melee damage with tactic modifiers applied.
        /// </summary>
        public static UInt16 CalculateMeleeDamageWithTactics(
            UInt16 attackerLevel,
            UInt16 defenderLevel,
            CombatTactic attackerTactic,
            CombatTactic defenderTactic)
        {
            // Get base damage
            UInt16 baseDamage = CalculateMeleeDamage(attackerLevel, defenderLevel);

            // Apply tactic modifiers
            float tacticMod = GetTacticDamageModifier(attackerTactic, defenderTactic);
            float baseTacticMod = GetTacticBaseDamageModifier(attackerTactic);
            float defenseMod = GetTacticDefenseModifier(defenderTactic);

            float finalDamage = baseDamage * tacticMod * baseTacticMod * defenseMod;

            return (UInt16)Math.Max(1, (int)finalDamage);
        }

        /// <summary>
        /// Calculates ranged damage with tactic modifiers applied.
        /// </summary>
        public static UInt16 CalculateRangedDamageWithTactics(
            UInt16 attackerLevel,
            UInt16 defenderLevel,
            float distance,
            CombatTactic attackerTactic,
            CombatTactic defenderTactic)
        {
            // Get base damage
            UInt16 baseDamage = CalculateRangedDamage(attackerLevel, defenderLevel, distance);

            // Apply tactic modifiers
            float tacticMod = GetTacticDamageModifier(attackerTactic, defenderTactic);
            float baseTacticMod = GetTacticBaseDamageModifier(attackerTactic);
            float defenseMod = GetTacticDefenseModifier(defenderTactic);

            float finalDamage = baseDamage * tacticMod * baseTacticMod * defenseMod;

            return (UInt16)Math.Max(1, (int)finalDamage);
        }

        /// <summary>
        /// Calculates mob damage to player with player's defensive tactic applied.
        /// </summary>
        public static UInt16 CalculateMobDamageWithTactics(
            UInt16 mobLevel,
            UInt16 playerLevel,
            CombatTactic playerTactic)
        {
            UInt16 baseDamage = CalculateMobDamage(mobLevel, playerLevel);

            // Apply player's defensive tactic
            float defenseMod = GetTacticDefenseModifier(playerTactic);

            return (UInt16)Math.Max(1, (int)(baseDamage * defenseMod));
        }

        #endregion

        #region Inner Strength

        /// <summary>
        /// Calculates IS regeneration per tick (out of combat).
        /// </summary>
        /// <param name="maxIS">Player's maximum IS</param>
        /// <returns>Amount of IS to regenerate</returns>
        public static UInt16 CalculateISRegen(UInt16 maxIS)
        {
            // Regenerate 5% of max IS per tick, minimum 1
            return (UInt16)Math.Max(1, maxIS / 20);
        }

        /// <summary>
        /// Calculates IS cost for using an ability in combat.
        /// </summary>
        /// <param name="baseCost">Base IS cost of the ability</param>
        /// <param name="tactic">Current combat tactic</param>
        /// <returns>Modified IS cost</returns>
        public static UInt16 CalculateAbilityISCost(UInt16 baseCost, CombatTactic tactic)
        {
            // Speed tactic reduces IS costs (quicker abilities)
            // Power tactic increases IS costs (more powerful)
            float modifier = 1.0f;
            switch (tactic)
            {
                case CombatTactic.Speed:
                    modifier = 0.9f;
                    break;
                case CombatTactic.Power:
                    modifier = 1.1f;
                    break;
            }

            return (UInt16)Math.Max(1, (int)(baseCost * modifier));
        }

        #endregion
    }
}
