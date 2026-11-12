using System;
using System.Linq;

namespace hds
{
    /// <summary>Numeric timed beneficial modifiers using the proven character-level value function3.
    /// Script callbacks, auras, states, random functions and indefinite combat styles are deliberately excluded.</summary>
    public static class AbilityScaledBuffs
    {
        // UBF0 group effects use CastValidTarget0, not the direct friendly-target value2.
        // Require the beneficial category as well as group targeting; never admit hostile area templates.
        public static bool BeneficialTarget(AbilityCatalog.Entry entry) => entry != null &&
            (entry.Number("Ability/CastDirectObjects", -1) == 1 || entry.FriendlyTarget ||
             entry.Number("Ability/CastDirectObjects", -1) == 9 && entry.Number("Ability/StatusCategory") == AbilityCancellation.BuffCategory);

        public static bool TryGet(UInt16 id, int casterLevel, out AbilityBuffs.Buff buff)
        {
            buff = null;
            var entry = AbilityCatalog.Find(id);
            if (entry == null || !BeneficialTarget(entry) || entry.Healing) return false;
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

        // Indefinite learned-level self modifiers; the stored toggle flag remains authoritative.
        public static bool TryToggle(ushort id, int learnedLevel, out AbilityBuffs.Buff buff)
        {
            buff = null;
            if (id != 1101 && id != 1102 && id != 13 && id != 16 && id != 442) return false;
            var entry = AbilityCatalog.Find(id);
            var mod = entry.Modifiers.Single(m => m.Number("WhenEnabled", -1) == 2 && m.Target != 0 &&
                m.Text("StatModUpdateFunc") != "ISDrainWithAbilityDeactivation");
            var drain = entry.Modifiers.FirstOrDefault(m => m.Text("StatModUpdateFunc") == "ISDrainWithAbilityDeactivation");
            if (!AbilityValues.TryValue(mod, Math.Min(50, Math.Max(0, learnedLevel)), 0, out int value)) return false;
            buff = new AbilityBuffs.Buff { Exclusive = true, Toggle = entry.Number("Ability/IsAToggleAbility") == 1, Family = id == 1101 ? 1040 : 0,
                DrainAmount = (int)(drain?.Number("MinValue") ?? 0), DrainSeconds = drain?.Number("UpdateFuncInterval") ?? 0,
                Modifiers = new[] { new AbilityBuffs.Modifier(AbilityBuffs.KindStat, mod.Target, value) } };
            return true;
        }

        public static bool TryScriptedTimed(ushort id, int casterLevel, out AbilityBuffs.Buff buff)
        {
            buff = null;
            if (id != 63 && id != 436 && id != 68 && id != 319) return false;
            var entry = AbilityCatalog.Find(id);
            var modifiers = entry.Modifiers.Where(m => m.Number("WhenEnabled",-1)==2).ToList();
            var stats = modifiers.Where(m=>m.Target!=0 && m.Target!=710).ToList();
            if(stats.Any(m=>!AbilityValues.TryValue(m,0,casterLevel,out _))) return false;
            buff = new AbilityBuffs.Buff { Seconds = 300, Family = 1000+(int)entry.Number("Ability/StackingType"),
                RefundIS = (int)(modifiers.FirstOrDefault(m=>m.Text("StatModTermFunc")=="RefundIS")?.Number("MinValue") ?? 0),
                TermHeal = (int)(modifiers.FirstOrDefault(m=>m.Text("StatModTermFunc")=="GenericConstHealthRegenEvent")?.Number("MinValue") ?? 0),
                DissemblingType = id==68 || id==319 ? (ushort)0x100 : (ushort)0,
                Modifiers = stats.Select(m=> { AbilityValues.TryValue(m,0,casterLevel,out int value);
                    return new AbilityBuffs.Modifier(AbilityBuffs.KindStat,m.Target,value); }).ToArray() };
            return true;
        }

        /// <summary>Captured overrides stay authoritative. Newly decoded templates resolve from the caster at application.</summary>
        public static bool Resolve(WorldClient caster, UInt16 id, out AbilityBuffs.Buff buff)
        {
            id = AbilityCatalog.Canonical(id);
            return AbilityBuffs.Retail.TryGetValue(id, out buff) ||
                TryScriptedTimed(id, caster.playerInstance.Level.getValue()[0],out buff) ||
                TryToggle(id, DeathPenalty.EffectiveLevel(caster, AbilityPassives.Level(caster, id)), out buff) ||
                TryGet(id, caster.playerInstance.Level.getValue()[0], out buff) || AbilityBuffs.TryGet(id, out buff);
        }
    }
}
