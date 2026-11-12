using System;
using System.Linq;

namespace hds
{
    /// <summary>Active buff/debuff totals used by the existing script-derived combat formulas.</summary>
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
            return Math.Min(value, cap);
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
            int type = modifier == null ? (TempMods.TryGet(ability, out var effect) && effect.Virus ? 2 : -1) : (int)modifier.Number("DamageType");
            ushort stat = type == 0 ? (ushort)159 : type == 1 ? (ushort)171 : type == 2 ? (ushort)172 : type == 7 ? (ushort)1034 : (ushort)0;
            float factor = stat == 0 ? 1 : (float)Math.Max(0, 1 + Influence(caster, stat) / 100.0);
            return (ushort)Math.Clamp((int)(amount * (double)factor), 0, ushort.MaxValue);
        }
        public static int Attack(WorldClient client, bool ranged) => Player(client, 163) + Player(client, ranged ? (UInt16)524 : (UInt16)522) +
            PercentDelta(client.playerInstance.Level.getValue()[0], Player(client, ranged ? (ushort)996 : (ushort)997)) +
            (TimedEffects.ForPlayer(client).Effects.HasAt(TempMods.BlindAbility, TimeUtils.GetSimTimeSeconds()) ? -30 : 0);
        public static int Attack(Mob mob, bool ranged) => Npc(mob, 163) + Npc(mob, ranged ? (UInt16)524 : (UInt16)522) +
            (TimedEffects.ForMob(mob).Effects.HasAt(TempMods.BlindAbility, TimeUtils.GetSimTimeSeconds()) ? -30 : 0);
        public static int Defense(WorldClient client, bool ranged) => Player(client, 959) + Player(client, ranged ? (UInt16)1010 : (UInt16)1009) +
            PercentDelta(client.playerInstance.Level.getValue()[0], Player(client, ranged ? (ushort)1027 : (ushort)1143));
        public static int Defense(Mob mob, bool ranged) => Npc(mob, 959) + Npc(mob, ranged ? (UInt16)1010 : (UInt16)1009);
        public static int Toughness(WorldClient client, bool ranged) => Player(client, 144) + Player(client, ranged ? (UInt16)181 : (UInt16)180) +
            PercentDelta(client.playerInstance.Level.getValue()[0] * 4 + 20, Player(client, ranged ? (ushort)1140 : (ushort)1144));
        public static int Toughness(Mob mob, bool ranged) => Npc(mob, 144) + Npc(mob, ranged ? (UInt16)181 : (UInt16)180);
        public static int PercentDelta(int baseline, int percent) => (int)(baseline * percent / 100f);
    }
}
