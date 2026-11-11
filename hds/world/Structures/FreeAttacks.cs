using System;

namespace hds
{
    /// <summary>
    /// Gunshots ("free attacks") by retail's scripts (research/client-scripts): interlock/combat_free_attacks.
    /// PerformWeaponFreeAttack, combat_calculations.GetFreeAttackDamage and GetDamageReductionValue, and the rolls of
    /// generic_free_attacks. What the scripts leave to the engine or to character stats this server doesn't model is
    /// filled in from the 423 retail hits in the captures' 81 68 combat-log lines, and marked as a guess.
    /// </summary>
    public static class FreeAttacks
    {
        /// <summary>
        /// GetFreeAttackDamage's last factor, fWeaponSpeed. Inferred: every one of the 423 retail hits has damage plus
        /// absorbed (the damage before absorption) a whole multiple of the gun's Tool/ReuseTimer rounded down: 4 for the
        /// 4.5 s Redpill Special (4, 8, 12), 3 for 3.5 s rifles, 2 for 2.5 s dual SMGs, 4 for 4.0 s shotguns.
        /// </summary>
        public static int WeaponSpeed(float reuseSeconds)
        {
            return Math.Max(1, (int)Math.Floor(reuseSeconds));
        }

        /// <summary>
        /// The attacker's damage bonus (GetFreeAttackDamage's iBonuses; RangePhysicalDamage and gear, not modelled
        /// here). Guess fitted to the captures: a new character's Redpill Special (Tool/BaseDamage 1) hit for 3 x 4 = 12
        /// before absorption, so 2 at level 1; level 50 guns (BaseDamage 15) hit for about 31-37 a second, so about 20.
        /// </summary>
        public static int DamageBonus(int level)
        {
            return 2 + (Math.Max(1, level) - 1) * 2 / 5;
        }

        /// <summary>combat_calculations.calcDamageWithGun: the gun's damage, give or take its range.</summary>
        public static int GunDamage(Random rng, int baseDamage, int damageRange)
        {
            return damageRange > 0 ? baseDamage + rng.Next(0, damageRange * 2) - damageRange : baseDamage;
        }

        /// <summary>
        /// combat_calculations.GetFreeAttackDamage, step by step (Python 2 integer division where the script divides
        /// ints): the gun's damage, plus its share of damage-type influence; Gaussian around that (1/10 deviation,
        /// clamped to 1/3..4/3); plus the bonus; times FreeFireScalar (a console variable, unknown: 1 fits the
        /// captures); the tactic's damage (Speed 1.0, Power 1.2, Grab 0.85, Block 0); Energized x1.15 or Precise x0.85;
        /// times the weapon speed. Truncated to an int after each multiplication, as the script does.
        /// </summary>
        public static int Damage(Random rng, int bonus, double damageTypeInfluence, int weaponBaseDamage, int weaponDamageRange,
            int weaponSpeed, CombatTactic tactic, CombatTacticType type, double freeFireScalar = 1.0)
        {
            int baseDamage = GunDamage(rng, weaponBaseDamage, weaponDamageRange);
            if (weaponBaseDamage != 0)
            {
                baseDamage += (int)(baseDamage * damageTypeInfluence);
            }
            baseDamage = CombatCalculator.GaussianClamped(rng, baseDamage, baseDamage / 10, baseDamage / 3, 4 * baseDamage / 3);
            double total = (bonus + baseDamage) * freeFireScalar;
            int damage = (int)(total * CombatCalculator.TacticDamageMultiplier(tactic));
            if (type == CombatTacticType.Energized)
            {
                damage = (int)(damage * 1.15);
            }
            if (type == CombatTacticType.Precise)
            {
                damage = (int)(damage * 0.85);
            }
            return (int)((double)damage * weaponSpeed);
        }

        /// <summary>
        /// PerformWeaponFreeAttack's absorption: the share from GetDamageReductionValue (defender's toughness plus ranged
        /// toughness; 0 here, as for close combat) comes off. It can be negative, adding damage, as in retail's tutorial
        /// (12 before absorption, 19 done, -7 absorbed). Only a result below 0 becomes 1, so Block's 0 stays 0. The packet
        /// carries both numbers truncated.
        /// </summary>
        public static UInt16 Absorb(int damage, int attackerLevel, int defenderLevel, int toughness, out Int16 absorbed)
        {
            double absorbedExact = damage * CombatCalculator.DamageReduction(attackerLevel, defenderLevel, toughness);
            double done = damage - absorbedExact;
            if (done < 0)
            {
                done = 1;
            }
            absorbed = (Int16)Math.Clamp((int)absorbedExact, Int16.MinValue, Int16.MaxValue);
            return (UInt16)Math.Min(UInt16.MaxValue, (int)done);
        }

        /// <summary>One shot's outcome, for the combat log and the target's health.</summary>
        public struct Shot
        {
            public bool Hit;
            public UInt16 AttackRoll, DefenseRoll;
            public UInt16 Damage;
            public Int16 Absorbed;
        }

        /// <summary>
        /// A gunshot: attack roll against defense roll (generic_free_attacks: getTacticRoll with range 100 against 69,
        /// each fighter's ranged tactics stat times its tactic's modifier; the same rolls as close combat, so
        /// <see cref="CombatInterlock.RollHit"/>, with the level standing in for the stats), then the damage. With
        /// levels as the stats the hit rate is about 78% at level 1 and 75% at level 50; retail's captures hit 78%
        /// (423 of 542).
        /// </summary>
        public static Shot Fire(Random rng, int attackerLevel, CombatTactic attackerTactic, CombatTacticType attackerType,
            int defenderLevel, CombatTactic defenderTactic, UInt32 weaponGoid, int attackBonus = 0, int defenseBonus = 0,
            int damageBonus = 0, double damageInfluence = 0, int toughness = 0)
        {
            Shot shot = new Shot();
            shot.Hit = CombatInterlock.RollHit(rng, attackerTactic, attackerLevel, defenderTactic, defenderLevel,
                out shot.AttackRoll, out shot.DefenseRoll, attackerType, attackBonus, defenseBonus);
            if (!shot.Hit)
            {
                return shot;
            }
            RangedWeapons.Stats stats = RangedWeapons.GetStats(weaponGoid);
            int baseDamage = stats != null ? Math.Max(1, stats.BaseDamage) : 1;
            float reuse = stats != null ? stats.ReuseSeconds : RangedWeapons.DefaultReuseSeconds(RangedWeapons.AttackClass(weaponGoid));
            int damage = Damage(rng, Math.Max(0, DamageBonus(attackerLevel) + damageBonus), damageInfluence, baseDamage, 0, WeaponSpeed(reuse), attackerTactic, attackerType);
            shot.Damage = Absorb(damage, attackerLevel, defenderLevel, toughness, out shot.Absorbed);
            return shot;
        }
    }
}
