using System;
using System.Linq;

namespace hds
{
    public static class AbilityHealing
    {
        public static bool PlayerTarget(AbilityCatalog.Entry entry) => entry != null && entry.Healing &&
            (entry.FriendlyTarget || entry.Number("Ability/CastDirectObjects") == 1 || entry.Number("Ability/CastDirectObjects") == 9);

        /// <summary>Stored fixed healing values, evaluated at the caster. Health's native damage-type-3 link is
        /// HealDamage (174); 625ec512..58a multiplies by 1 + that influence / 100, truncating toward zero.
        /// HOT's value is a total, divided into ticks by tempmods.HOT. Mobius is separately scripted as 10% target max HP.</summary>
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
                    caster.playerInstance.Level.getValue()[0], out int value)) amount += value;
            float influence = (float)Math.Max(0, 1 + AbilityStats.Influence(caster, 174) / 100.0);
            return (ushort)Math.Clamp((int)(amount * (double)influence), 0, ushort.MaxValue);
        }
    }
}
