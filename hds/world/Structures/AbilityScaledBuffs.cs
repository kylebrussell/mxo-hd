using System;
using System.Linq;

namespace hds
{
    /// <summary>Numeric timed beneficial modifiers using the proven character-level value function3.
    /// Script callbacks, auras, states, random functions and indefinite combat styles are deliberately excluded.</summary>
    public static class AbilityScaledBuffs
    {
        public static bool TryGet(UInt16 id, int casterLevel, out AbilityBuffs.Buff buff)
        {
            buff = null;
            var entry = AbilityCatalog.Find(id);
            if (entry == null || (entry.Number("Ability/CastDirectObjects", -1) != 1 && !entry.FriendlyTarget) || entry.Healing) return false;
            var mods = entry.Modifiers.Where(m => m.Number("WhenEnabled", -1) == 2).ToList();
            if (!mods.Any(m => m.Number("ValueFunction") == 3) || mods.Any(m => m.Target == 0 || m.Target == 108 || m.State != 0 ||
                m.Number("DurationType", -1) != 0 || m.Number("Duration") <= 0 ||
                m.Number("ProcTriggerChance", 1) != 1 || m.Number("ProcTriggerFlags") != 0 ||
                m.Number("ValueFunction") == 2 || !AbilityValues.TryValue(m, 0, casterLevel, out _) ||
                !string.IsNullOrEmpty(m.Text("StatModInitFunc")) || !string.IsNullOrEmpty(m.Text("StatModUpdateFunc")) ||
                !string.IsNullOrEmpty(m.Text("StatModTermFunc")) || !string.IsNullOrEmpty(m.Text("StatModHealthChangedFunc")))) return false;
            var durations = mods.Select(m => m.Number("Duration")).Distinct().ToList();
            if (durations.Count != 1) return false;
            int stacking = (int)entry.Number("Ability/StackingType");
            buff = new AbilityBuffs.Buff { Seconds = (float)durations[0], Family = stacking == 0 ? 0 : 1000 + stacking,
                Modifiers = mods.Select(m =>
                {
                    AbilityValues.TryValue(m, 0, casterLevel, out int value);
                    return new AbilityBuffs.Modifier(AbilityBuffs.KindStat, m.Target, value);
                }).ToArray() };
            return true;
        }

        /// <summary>Captured overrides stay authoritative. Newly decoded templates resolve from the caster at application.</summary>
        public static bool Resolve(WorldClient caster, UInt16 id, out AbilityBuffs.Buff buff)
        {
            id = AbilityCatalog.Canonical(id);
            return AbilityBuffs.Retail.TryGetValue(id, out buff) ||
                TryGet(id, caster.playerInstance.Level.getValue()[0], out buff) || AbilityBuffs.TryGet(id, out buff);
        }
    }
}
