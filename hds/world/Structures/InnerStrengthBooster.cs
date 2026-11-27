using System.Linq;

namespace hds
{
    internal static class InnerStrengthBooster
    {
        internal const uint Item = 47363;
        internal const ushort Ability = 1333;

        internal static AbilityBuffs.Buff Definition()
        {
            var modifier = AbilityCatalog.Find(Ability).Modifiers.Single();
            return new AbilityBuffs.Buff
            {
                Seconds = (float)modifier.Number("Duration"),
                Modifiers = new[] {
                    new AbilityBuffs.Modifier(AbilityBuffs.KindStat, modifier.Target, (int)modifier.Number("MinValue"))
                },
                PeriodicFx = 67108998, // tempmods.py: ISBoosterFunc plays FX only.
                PeriodicFxSeconds = modifier.Number("UpdateFuncInterval")
            };
        }
    }
}
