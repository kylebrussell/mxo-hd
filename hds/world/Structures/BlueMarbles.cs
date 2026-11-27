using System.Linq;

namespace hds
{
    internal static class BlueMarbles
    {
        internal const ushort Stage1 = 1374, Stage2 = 1375, Stage3 = 1376;
        internal const uint Item1 = 47907, Item2 = 47908, Item3 = 47909;

        internal static bool Handles(ushort ability) => ability >= Stage1 && ability <= Stage3;

        internal static ushort RequiredAwakenedRank(ushort ability) =>
            (ushort)AbilityCatalog.Find(ability).Number("AbilityPrerequisite/ReqAbilityLevel");

        internal static ConsumableItems.Consumable Consumable(ushort ability)
        {
            var entry = AbilityCatalog.Find(ability);
            ushort family = AbilityCatalog.IdOf((uint)entry.Number("Ability/FamilyRecastTimer"));
            return new ConsumableItems.Consumable(ability, ConsumableItems.Effect.TimedBuff, family,
                (float)entry.Number("Ability/RecastTime"));
        }

        internal static uint ActivationFx(ushort ability) =>
            (uint)AbilityCatalog.Find(ability).Modifiers.Single(mod => mod.Target == 1010).Number("ActivationFX");

        internal static AbilityBuffs.Buff Definition(ushort ability)
        {
            var mods = AbilityCatalog.Find(ability).Modifiers.Where(mod => mod.Number("WhenEnabled") == 2).ToArray();
            var pulse = mods.Single(mod => mod.Text("StatModUpdateFunc") == "BlueShiftFunc");
            return new AbilityBuffs.Buff
            {
                Seconds = (float)pulse.Number("Duration"),
                Modifiers = mods.Select(mod => new AbilityBuffs.Modifier(AbilityBuffs.KindStat,
                    mod.Target, (int)mod.Number("MinValue"))).ToArray(),
                PeriodicFx = 67109032, // tempmods.py: BlueShiftFunc
                PeriodicFxSeconds = pulse.Number("UpdateFuncInterval")
            };
        }
    }
}
