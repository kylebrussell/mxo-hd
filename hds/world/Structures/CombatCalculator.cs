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
        private static readonly Random random = Random.Shared;

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
        /// Not sent to the client: these FX.cs values aren't valid effect ids (retail effect ids are
        /// 0x28xxxxxx; most FX.cs entries are stored byte-reversed), and retail melee hits update the
        /// NPC's health alone. See CombatHandler.HandleCombatTick.
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

        /// <summary>World units per metre (?gotopos and retail positions use 100 units per metre).</summary>
        public const float WorldUnitsPerMetre = 100f;

        /// <summary>
        /// Distance in metres, which is what the ranged damage falloff expects. <see cref="CalculateDistance"/> returns
        /// world units: a typical 6 m gunfight (retail captures: median 626 units) fell into the "beyond 50 m" band and
        /// did 60% damage.
        /// </summary>
        public static float CalculateDistanceMetres(byte[] playerPositionBytes, double mobX, double mobY, double mobZ)
        {
            return CalculateDistance(playerPositionBytes, mobX, mobY, mobZ) / WorldUnitsPerMetre;
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
        /// Rolls one gunshot: hit when the attack roll beats the defense roll, as in retail's 81 68 combat log
        /// (tutorial fight: attack rolls 19-95, defense rolls 7-65, 7 of 10 shots hit). The ranges are a guess
        /// shaped like those numbers; retail's formula (skills, tactics, range) is unknown.
        /// </summary>
        public static bool RollRangedHit(Random rng, out UInt16 attackRoll, out UInt16 defenseRoll)
        {
            attackRoll = (UInt16)rng.Next(15, 100);
            defenseRoll = (UInt16)rng.Next(5, 70);
            return attackRoll > defenseRoll;
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
        /// Inner Strength a tactic change costs in a fight: none in retail. combat.GetInnerStrengthUsed in the client's
        /// Monolith.ltp has no such cost and captures show none (see docs/COMBAT-SYSTEM-ROADMAP.md).
        /// </summary>
        public const int TACTIC_CHANGE_IS_COST = 0;

        /// <summary>
        /// Inner Strength a combat round costs: none in retail, whose only effects on it in a fight were slower
        /// regeneration (CombatEffectAbility) and the Block gain.
        /// </summary>
        public const int COMBAT_ROUND_IS_COST = 0;

        /// <summary>combat.GetInnerStrengthUsed: a round on Block regains 10 (with the GainISFromBlocks console variable).</summary>
        public const int BLOCK_ROUND_IS_GAIN = 10;

        /// <summary>Inner Strength change of a round on this tactic.</summary>
        public static int RoundInnerStrengthChange(CombatTactic tactic)
        {
            return tactic == CombatTactic.Block ? BLOCK_ROUND_IS_GAIN : -COMBAT_ROUND_IS_COST;
        }

        /// <summary>
        /// Regeneration in a fight as a share of the normal rate. Retail's CombatEffectAbility records put -80 on the Inner
        /// Strength and health regen rate stats. Reading that as -80% is inferred: the scripts don't state it, and in 20
        /// captures in-fight Inner Strength regen ran at a median 0.42 of the out-of-fight rate, 0.25-0.33 in the lowest logs.
        /// </summary>
        public const double FIGHT_REGEN_FACTOR = 0.2;

        /// <summary>Damage dealt with this tactic (DamagePercentAbility): Speed 100%, Power 120%, Grab 85%, Block none.</summary>
        public static float TacticDamageMultiplier(CombatTactic tactic)
        {
            return Math.Max(0f, 1f + CombatTactics.Get(tactic).DamagePercent / 100f);
        }

        private static UInt16 ApplyTactic(UInt16 baseDamage, CombatTactic attacker)
        {
            float multiplier = TacticDamageMultiplier(attacker);
            return multiplier <= 0f ? (UInt16)0 : (UInt16)Math.Max(1, (int)(baseDamage * multiplier));
        }

        /// <summary>Ranged damage with the attacker's tactic; 0 on Block.</summary>
        public static UInt16 CalculateRangedDamageWithTactics(UInt16 attackerLevel, UInt16 defenderLevel, float distance,
            CombatTactic attackerTactic)
        {
            return ApplyTactic(CalculateRangedDamage(attackerLevel, defenderLevel, distance), attackerTactic);
        }

        /// <summary>A mob's damage to a player with the mob's tactic.</summary>
        public static UInt16 CalculateMobDamageWithTactics(UInt16 mobLevel, UInt16 playerLevel, CombatTactic mobTactic = CombatTactic.Speed)
        {
            return ApplyTactic(CalculateMobDamage(mobLevel, playerLevel), mobTactic);
        }

        /// <summary>
        /// Retail's combat_calculations.GetDamageReductionValue (client Monolith.ltp), the share of the damage the defender
        /// absorbs: 0.2 per level above the attacker plus armor between -0.33 and 0.33, capped at +-0.9. It can be negative,
        /// which is why retail's records show negative absorbed damage (tutorial: 21 done, -5 absorbed).
        /// </summary>
        public static double DamageReduction(int attackerLevel, int defenderLevel, int toughness = 0)
        {
            double levelPart = (defenderLevel - attackerLevel) * 0.2;
            double totalRange = defenderLevel * 4.0 + 20.0;
            double armor = Math.Min(0.33, 0.66 / totalRange * (toughness + 20.0) - 0.33);
            return Math.Clamp(levelPart + armor, -0.9, 0.9);
        }

        /// <summary>
        /// Retail's discovery.getGaussianRandomClamped, as combat_calculations calls it: normal around the average, within
        /// the given limits.
        /// </summary>
        public static int GaussianClamped(Random rng, double average, double deviation, double min, double max)
        {
            double u1 = 1.0 - rng.NextDouble(), u2 = rng.NextDouble();
            double normal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
            return (int)Math.Clamp(average + normal * deviation, min, max);
        }

        /// <summary>
        /// The damage part of retail's GetCloseCombatDamage. The characters' DamageBonus stat isn't modelled by this server,
        /// so it is a guess by level; retail's tutorial fight (level 1, 16 before absorption) fits the level 1 value.
        /// </summary>
        public static int CloseCombatDamageBonus(int level)
        {
            return 4 + level / 10;
        }

        /// <summary>
        /// Retail's close-combat damage (combat_resolution.calcDamageForLoser): (bonus, Gaussian 1/3 to 4/3) x tactic x 4, then the
        /// absorbed share comes off, at least 1 damage left. <paramref name="absorbed"/> goes in the round record.
        /// </summary>
        public static UInt16 CloseCombatDamage(Random rng, int attackerLevel, int defenderLevel, CombatTactic tactic, out Int16 absorbed,
            CombatTacticType type = CombatTacticType.Normal)
        {
            int damage = (int)(CloseCombatDamageBonus(attackerLevel) * CombatTactics.TypeDamageMultiplier(type));
            damage = GaussianClamped(rng, damage, damage / 10.0, damage / 3.0, 4.0 * damage / 3.0);
            damage = (int)(damage * TacticDamageMultiplier(tactic));
            damage = (int)(damage * 4.0);
            absorbed = (Int16)(damage * DamageReduction(attackerLevel, defenderLevel));
            damage -= absorbed;
            return (UInt16)Math.Max(1, damage);
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

        #endregion
    }
}
