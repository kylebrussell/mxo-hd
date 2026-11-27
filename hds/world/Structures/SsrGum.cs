using System.Linq;

namespace hds
{
    internal static class SsrGum
    {
        internal const uint Item = 47360;
        internal const ushort Ability = 1325;

        internal static AbilityBuffs.Buff Definition()
        {
            var mods = AbilityCatalog.Find(Ability).Modifiers.Where(mod => mod.Number("WhenEnabled") == 2).ToArray();
            var pulse = mods.Single(mod => mod.Text("StatModUpdateFunc") == "SSRGumFunc");
            return new AbilityBuffs.Buff
            {
                Seconds = (float)pulse.Number("Duration"),
                Modifiers = mods.Select(mod => new AbilityBuffs.Modifier(AbilityBuffs.KindStat,
                    mod.Target, (int)mod.Number("MinValue"))).ToArray(),
                PeriodicFx = 67108996, // tempmods.py: SSRGumFunc
                PeriodicFxSeconds = pulse.Number("UpdateFuncInterval")
            };
        }
    }
}
