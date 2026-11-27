using System.Linq;

namespace hds
{
    internal static class TacticBoostItems
    {
        internal const ushort FirstAbility = 548, LastAbility = 551;
        internal const uint FirstItem = 8322, LastItem = 8325;
        internal const uint ActivationFx = 671089691, PulseFx = 67108932;

        internal static bool Handles(ushort ability) => ability >= FirstAbility && ability <= LastAbility;

        internal static int RequiredLevel(uint item) => (int)(item - FirstItem) * 15;

        internal static AbilityBuffs.Buff Definition(ushort ability)
        {
            var entry = AbilityCatalog.Find(ability);
            var mod = entry.Modifiers.Single();
            return new AbilityBuffs.Buff
            {
                Seconds = (float)mod.Number("Duration"),
                Family = 1000 + (int)entry.Number("Ability/StackingType"),
                Modifiers = new[] { new AbilityBuffs.Modifier(AbilityBuffs.KindStat, mod.Target, (int)mod.Number("MinValue")) },
                PeriodicFx = PulseFx, // tempmods.py: TacticBoostFunc
                PeriodicFxSeconds = mod.Number("UpdateFuncInterval")
            };
        }

        internal static bool Outranked(WorldClient client, ushort ability)
        {
            int priority = Priority(ability);
            lock (client.playerData.activeBuffs)
                return client.playerData.activeBuffs.Keys.Any(other => Handles(other) && Priority(other) > priority);
        }

        private static int Priority(ushort ability) => (int)AbilityCatalog.Find(ability).Number("Ability/StackingPriority");
    }
}
