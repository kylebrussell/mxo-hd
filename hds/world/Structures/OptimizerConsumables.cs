using System.Linq;

namespace hds
{
    internal static class OptimizerConsumables
    {
        internal const ushort Inhaler = 1275, Hypo = 1276, Overdrive = 1277;
        internal const uint InhalerItem = 47111, HypoItem = 47112, OverdriveItem = 47113;

        internal static bool Handles(ushort ability) => ability >= Inhaler && ability <= Overdrive;

        internal static bool MatchesItemAlias(uint item, ushort request) =>
            item == OverdriveItem && request == AbilityCatalog.IdOf((uint)AbilityCatalog.Find(Overdrive).Number("Ability/AbilityID"));

        internal static int RequiredLevel(uint item) =>
            (int)AbilityCatalog.Find((ushort)(Inhaler + (item - InhalerItem))).Number("CharStatsPrerequisite/ReqCharLevel");

        internal static ConsumableItems.Consumable Consumable(ushort ability) =>
            new ConsumableItems.Consumable(ability, ConsumableItems.Effect.TimedBuff, 0,
                (float)AbilityCatalog.Find(ability).Number("Ability/RecastTime"));

        internal static void Apply(WorldClient client, ushort ability)
        {
            var modifiers = AbilityCatalog.Find(ability).Modifiers.Where(mod => mod.Number("WhenEnabled") == 2).ToArray();
            // Overdrive is identified by the triggered template 1277 even though its internal AbilityID names 1276.
            var pulse = modifiers.SingleOrDefault(mod => mod.Text("StatModUpdateFunc") == "PlayUpdateFXIfInInterlock");
            var effect = new TempMods.Effect
            {
                Ability = ability,
                Mods = modifiers.Select(mod => new TempMods.Mod
                {
                    Kind = mod.State == 0 ? TempMods.KindStat : TempMods.KindState,
                    Proc = mod.State == 0 ? TempMods.Proc.Stat : TempMods.Proc.State,
                    Target = mod.State == 0 ? mod.Target : mod.State,
                    MinValue = (int)mod.Number("MinValue"),
                    MaxValue = (int)mod.Number("MaxValue"),
                    Seconds = (float)mod.Number("Duration"),
                    Interval = mod.Number("UpdateFuncInterval")
                }).ToArray()
            };
            var active = TimedEffects.Apply(TimedEffects.ForPlayer(client), effect, TimeUtils.GetSimTimeSeconds(),
                client, TimedEffects.PlayerName(client), configure: mod =>
                {
                    if (pulse != null && mod.Target == pulse.Target)
                        mod.InterlockFx = (uint)pulse.Number("UpdateFX");
                });
            if (pulse?.Text("StatModInitFunc") == "PlayUpdateFXIfInInterlock")
                foreach (TimedEffects.Active mod in active.Where(mod => mod.InterlockFx != 0))
                    TimedEffects.PlayInterlockFx(client, mod);
        }
    }
}
