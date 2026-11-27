using System.Linq;

namespace hds
{
    internal static class QuicksilverPill
    {
        internal const uint Item = 47107;
        internal const ushort Ability = 1274;
        internal const ushort Armor = 0x100; // kArmor, PersonalFirewallInit/Term in the shipped callback

        internal static int RequiredLevel => (int)AbilityCatalog.Find(Ability).Number("CharStatsPrerequisite/ReqCharLevel");
        internal static float Reuse => (float)AbilityCatalog.Find(Ability).Number("Ability/RecastTime");

        internal static AbilityBuffs.Buff Definition()
        {
            var effects = AbilityCatalog.Find(Ability).Modifiers.Where(mod => mod.Number("WhenEnabled") == 2).ToArray();
            return new AbilityBuffs.Buff
            {
                Seconds = (float)effects[0].Number("Duration"),
                DissemblingType = Armor,
                Modifiers = effects.Select(mod => new AbilityBuffs.Modifier(AbilityBuffs.KindStat,
                    mod.Target, (int)mod.Number("MinValue"))).ToArray()
            };
        }
    }
}
