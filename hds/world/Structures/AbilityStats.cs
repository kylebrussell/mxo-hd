using System;
using System.Linq;

namespace hds
{
    /// <summary>Character attribute bases and capped modifiers used by the combat formulas.</summary>
    public static class AbilityStats
    {
        public static int Player(WorldClient client, UInt16 stat)
        {
            int value = AbilityBuffs.Total(client, stat) + AbilityPassives.Total(client, stat);
            var entry = AbilityCatalog.Find(stat);
            int cap = (int)(entry?.Number("Ability/TotalMaxBuffValue", int.MaxValue) ?? int.MaxValue);
            int level = client.playerInstance.Level.getValue()[0];
            if (level > 0 && entry?.Has("Ability/BuffBonusCapScale") == true)
                cap = Math.Min(cap, (int)(level * entry.Number("Ability/BuffBonusCapScale")));
            int bonus = Math.Min(value, cap);
            return AttributeAllocation.Known(stat) ? CharacterBase(client, (byte)stat) + bonus : bonus;
        }

        internal static ushort CharacterBase(WorldClient client, byte attribute)
        {
            var snapshot = System.Threading.Volatile.Read(ref client.playerData.attributeBases);
            return snapshot != null && snapshot.TryGetValue(attribute, out ushort value)
                ? value
                : CharacterAttributes.LegacyValue(attribute);
        }

        public static int Npc(Mob mob, UInt16 stat) => TimedEffects.ForMob(mob).Effects.Total(stat);
        public static int Influence(WorldClient client, UInt16 stat)
        {
            UInt16 bonus = AbilityCatalog.IdOf((uint)(AbilityCatalog.Find(stat)?.Number("AbilityAttributeLink/AttributeLink") ?? 0));
            return Player(client, stat) + (bonus != 0 && AbilityCatalog.Find(bonus)?.Number("Ability/ValueType") == 1 ? Player(client, bonus) : 0);
        }
        /// <summary>The native health-modifier influence multiplier (625ec512..58a), applied once when a hit/DOT lands.
        /// Existing captured/generic base damage remains approximate; bonuses must still affect NPC and PvP casts alike.</summary>
        public static ushort ScaleAbilityDamage(WorldClient caster, ushort ability, int amount)
        {
            var entry = AbilityCatalog.Find(ability);
            var modifier = entry?.Modifiers.FirstOrDefault(m => m.Has("DamageType") && m.Number("DamageType") != 3);
            int type = modifier != null ? (int)modifier.Number("DamageType") : CombatSpecials.ExileDamageType(ability);
            if (modifier == null && type < 0 && TempMods.TryGet(ability, out var effect) && effect.Virus)
                type = 2;
            ushort influenced = ScaleTypedAbilityDamage(caster, type, amount);
            // Retained Code Nuke 2.0 hits separate at Speed 300 and Power 360 before target mitigation.
            if (ability == 337 && caster.playerData.combatTactic == CombatTactic.Power)
                return (ushort)Math.Min(ushort.MaxValue, (int)(influenced * (double)CombatCalculator.TacticDamageMultiplier(CombatTactic.Power)));
            return influenced;
        }

        internal static ushort ScaleTypedAbilityDamage(WorldClient caster, int type, int amount)
        {
            ushort stat = type == 0 ? (ushort)159 : type == 1 ? (ushort)171 : type == 2 ? (ushort)172 : type == 7 ? (ushort)1034 : (ushort)0;
            float factor = stat == 0 ? 1 : (float)Math.Max(0, 1 + Influence(caster, stat) / 100.0);
            return (ushort)Math.Clamp((int)(amount * (double)factor), 0, ushort.MaxValue);
        }
        public static int Attack(WorldClient client, bool ranged) => Player(client, 163) + Player(client, ranged ? (UInt16)524 : (UInt16)522) +
            PercentDelta(client.playerInstance.Level.getValue()[0], Player(client, ranged ? (ushort)996 : (ushort)997)) +
            (TimedEffects.ForPlayer(client).Effects.HasAt(TempMods.BlindAbility, TimeUtils.GetSimTimeSeconds()) ? -30 : 0);
        public static int Attack(Mob mob, bool ranged) => Npc(mob, 163) + Npc(mob, ranged ? (UInt16)524 : (UInt16)522) +
            (TimedEffects.ForMob(mob).Effects.HasAt(TempMods.BlindAbility, TimeUtils.GetSimTimeSeconds()) ? -30 : 0);
        /// <summary>Native UI getCombatTacticBonusType (6251fe10), inferred for this server's queued-special contests.
        /// Ordinary attacks, unknown functions and escape retain the existing server calculation.</summary>
        public static ushort SpecialAttackStat(int testFunction) => testFunction switch
        {
            1 => 524, 2 => 342, 3 => 522, 4 => 1032, _ => 0
        };
        public static int SpecialAttack(WorldClient attacker, AbilityItem ability)
        {
            var definition = ability == null ? null : AbilityCatalog.Find(ability.getAbilityID());
            int test = (int)(definition?.Number("Ability/TestFunction", -1) ?? -1);
            ushort stat = SpecialAttackStat(test);
            // Linked accuracy bonus policy is OURS: the same level-scaled percentage convention as melee/ranged.
            // Exact retail linked-stat coefficients remain unrecovered.
            int total = test == 1 ? Attack(attacker, true) : stat == 342 || stat == 1032 ?
                Player(attacker, 163) + (stat == 342 ? ViralAccuracy(attacker) : ThrownAccuracy(attacker)) +
                (TimedEffects.ForPlayer(attacker).Effects.HasAt(TempMods.BlindAbility, TimeUtils.GetSimTimeSeconds()) ? -30 : 0) : Attack(attacker, false);
            // Use the authored flat tactic adjustment for style-bound queued gun moves. Their original
            // tacticalValue producer is unknown; keep the free-cast, escape and item contest rules separate.
            if (test == 1 && definition.Number("Ability/CastRequiredCombatStance") != 0 &&
                (CombatSpecials.IsFullAuto(definition.Id) ||
                 definition.Number("Ability/HasSuccessInterlockMove") == 1 &&
                 (definition.Number("Ability/CastWhenUsed") == 3 || definition.Id == CombatSpecials.ThreeRoundBurst ||
                  definition.Id == CombatSpecials.PistolEvasive || definition.Id == CombatSpecials.PistolBarrage ||
                  definition.Id == CombatSpecials.DualPistolPointBlank)))
            {
                int bonus = (int)definition.Number("Ability/TacticBonus");
                if (bonus != 0)
                {
                    Output.WriteDebugLog($"[SPECIAL-TACTIC OURS] ability={definition.Id} bonus={bonus} base={total} total={total + bonus}");
                    total += bonus;
                }
            }
            if (ability != null) Output.WriteDebugLog("[COMBAT-ATTACK OURS] special " + ability.getAbilityID() + " stat " + stat + " total " + total);
            return total;
        }
        /// <summary>OURS approximation: loaded/active Viral Accuracy Bonus1147 adds level*percent/100,
        /// using the existing melee/ranged truncation convention. Direct342 keeps its separate cap.</summary>
        public static int ViralAccuracy(WorldClient client) => Player(client,342) +
            PercentDelta(client.playerInstance.Level.getValue()[0],Player(client,1147));
        /// <summary>OURS coefficient for the primary 1032-to-1145 link: capped direct thrown accuracy plus
        /// the existing level-scaled percentage convention, once. Ordinary/escape and damage remain separate.</summary>
        public static int ThrownAccuracy(WorldClient client) => Player(client,1032) +
            PercentDelta(client.playerInstance.Level.getValue()[0],Player(client,1145));
        public static int Defense(WorldClient client, bool ranged) => Player(client, 959) + Player(client, ranged ? (UInt16)1010 : (UInt16)1009) +
            PercentDelta(client.playerInstance.Level.getValue()[0], Player(client, ranged ? (ushort)1027 : (ushort)1143));
        public static int Defense(Mob mob, bool ranged) => Npc(mob, 959) + Npc(mob, ranged ? (UInt16)1010 : (UInt16)1009);
        /// <summary>Native getDefenseTacticBonusType (6251ffd0): TestFunction chooses the opposing special's defence.
        /// Unknown/non-contested test functions return no defence; an ordinary round retains its existing melee base.</summary>
        public static ushort SpecialDefenseStat(int testFunction) => testFunction switch
        {
            1 => 1010, 2 => 183, 3 => 1009, 4 => 1011, 6 => 1011, _ => 0
        };
        public static int SpecialDefense(WorldClient defender, AbilityItem opposingAbility)
        {
            if (opposingAbility == null) return Defense(defender, false);
            int test = (int)(AbilityCatalog.Find(opposingAbility.getAbilityID())?.Number("Ability/TestFunction", -1) ?? -1);
            // combat_utility.isSpecialMove excludes isEscapeAbility: native 62516a66 compares TestFunction with6.
            if (test == 6) return Defense(defender, false);
            ushort stat = SpecialDefenseStat(test);
            Output.WriteDebugLog("[COMBAT-DEFENCE] PvP special " + opposingAbility.getAbilityID() + " stat " + stat);
            if (stat == 0) return 0;
            ushort bonus = stat == 1010 ? (ushort)1027 : stat == 1009 ? (ushort)1143 : stat == 1011 ? (ushort)1029 : (ushort)1030;
            // Preserve this server's ordinary defence/level baseline and percent-delta policy, now selecting its proper type.
            return Player(defender, 959) + Player(defender, stat) + PercentDelta(defender.playerInstance.Level.getValue()[0], Player(defender, bonus));
        }
        public static int SpecialDefense(Mob defender, AbilityItem opposingAbility)
        {
            if (opposingAbility == null) return Defense(defender, false);
            int test = (int)(AbilityCatalog.Find(opposingAbility.getAbilityID())?.Number("Ability/TestFunction", -1) ?? -1);
            // combat_utility.isSpecialMove excludes isEscapeAbility: native 62516a66 compares TestFunction with6.
            if (test == 6) return Defense(defender, false);
            ushort stat = SpecialDefenseStat(test);
            Output.WriteDebugLog("[COMBAT-DEFENCE] NPC special " + opposingAbility.getAbilityID() + " stat " + stat);
            return stat == 0 ? 0 : Npc(defender, 959) + Npc(defender, stat);
        }
        public static int Toughness(WorldClient client, bool ranged) => Player(client, 144) + Player(client, ranged ? (UInt16)181 : (UInt16)180) +
            PercentDelta(client.playerInstance.Level.getValue()[0] * 4 + 20, Player(client, ranged ? (ushort)1140 : (ushort)1144));
        public static int Toughness(Mob mob, bool ranged) => Npc(mob, 144) + Npc(mob, ranged ? (UInt16)181 : (UInt16)180);
        public static int PercentDelta(int baseline, int percent) => (int)(baseline * percent / 100f);
    }
}
