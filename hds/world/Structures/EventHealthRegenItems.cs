using System.Linq;

namespace hds
{
    /// <summary>Owned event consumables whose shipped stat 112 modifier raises health regeneration.</summary>
    internal static class EventHealthRegenItems
    {
        internal const uint CookieItem = 46995, PatcherItem = 47013;
        internal const ushort Cookie = 1258, Patcher = 1263;

        internal static bool Handles(ushort id) => id == Cookie || id == Patcher;

        private static AbilityCatalog.Modifier Modifier(ushort id) => AbilityCatalog.Find(id).Modifiers.Single();

        internal static AbilityBuffs.Buff Definition(ushort id)
        {
            var modifier = Modifier(id);
            return new AbilityBuffs.Buff
            {
                Seconds = (float)modifier.Number("Duration"),
                Modifiers = new[] {
                    new AbilityBuffs.Modifier(AbilityBuffs.KindStat, modifier.Target, (int)modifier.Number("MinValue"))
                }
            };
        }

        internal static uint ActivationFx(ushort id) => (uint)Modifier(id).Number("ActivationFX");
    }
}
