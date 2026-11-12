using System;

namespace hds
{
    /// <summary>Class-4 state duration influence: client.dll 6251dd00 resolves AttributeLink, and
    /// 625ec545..574 multiplies the float duration instead of truncating an integer modifier value.</summary>
    public static class AbilityControlDurations
    {
        public static ushort BonusStat(ushort state)
        {
            var entry = AbilityCatalog.Find(state);
            if (entry?.Number("Ability/AbilityClass", -1) != 4) return 0;
            ushort stat = AbilityCatalog.IdOf((uint)entry.Number("AbilityAttributeLink/AttributeLink"));
            return stat != 0 && AbilityCatalog.Find(stat)?.Number("Ability/AbilityRank", -1) == 2 ? stat : (ushort)0;
        }

        public static float Scale(WorldClient caster, ushort state, float seconds)
        {
            ushort bonus = BonusStat(state);
            if (caster == null || bonus == 0 || seconds <= 0) return seconds;
            float factor = (float)Math.Max(0, 1 + AbilityStats.Player(caster, bonus) / 100.0);
            return (float)(seconds * (double)factor);
        }
    }
}
