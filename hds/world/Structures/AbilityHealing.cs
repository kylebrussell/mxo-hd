using System;
using System.Linq;

namespace hds
{
    public static class AbilityHealing
    {
        public static bool PlayerTarget(AbilityCatalog.Entry entry) => entry != null && entry.Healing &&
            (entry.FriendlyTarget || entry.Number("Ability/CastDirectObjects") == 1 || entry.Number("Ability/CastDirectObjects") == 9);

        /// <summary>Native 625ec512..58a uses a binary32 multiplier, then truncates toward zero.</summary>
        internal static int ApplyInfluence(int amount, int influence) =>
            (int)(amount * (double)(float)Math.Max(0, 1 + influence / 100.0));

        /// <summary>Damage type 3 uses caster HealDamage (174). Untyped HOT totals are evaluated without that influence.
        /// Mobius Code uses 10% of its target's maximum health.</summary>
        public static ushort Amount(WorldClient caster, WorldClient target, ushort ability)
        {
            ability = AbilityCatalog.Canonical(ability);
            if (ability == 967) return (ushort)(NumericalUtils.ByteArrayToUint16(target.playerInstance.MaxHealth.getValue(), 1) / 10);
            var entry = AbilityCatalog.Find(ability);
            if (!PlayerTarget(entry)) return 0;
            int amount = 0;
            foreach (var mod in entry.Modifiers.Where(m => m.Number("WhenEnabled", -1) == 2 &&
                (m.Target == 108 && m.Number("DamageType", -1) == 3 || m.Text("StatModUpdateFunc") == "HOT")))
                if (AbilityValues.TryValue(mod, DeathPenalty.EffectiveLevel(caster, AbilityPassives.Level(caster, ability)),
                    caster.playerInstance.Level.getValue()[0], out int value))
                    amount += ApplyInfluence(value, mod.Number("DamageType", -1) == 3 ? AbilityStats.Influence(caster, 174) : 0);
            return (ushort)Math.Clamp(amount, 0, ushort.MaxValue);
        }
    }
}
