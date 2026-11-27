namespace hds
{
    internal static class HealInhalers
    {
        internal const ushort FirstAbility = 716, LastAbility = 720;
        internal const uint FirstItem = 39757, LastItem = 39761;
        private static readonly int[] LevelRequirements = { 0, 6, 12, 25, 40 };

        internal static bool Handles(ushort ability) => ability >= FirstAbility && ability <= LastAbility;
        internal static int RequiredLevel(uint item) => LevelRequirements[item - FirstItem];

        internal static ConsumableItems.Consumable Consumable(ushort ability)
        {
            var entry = AbilityCatalog.Find(ability);
            ushort family = AbilityCatalog.IdOf((uint)entry.Number("Ability/FamilyRecastTimer"));
            return new ConsumableItems.Consumable(ability, ConsumableItems.Effect.HealOverTime, family,
                (float)entry.Number("Ability/RecastTime"));
        }
    }
}
