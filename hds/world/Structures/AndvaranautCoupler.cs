using System;
using System.Linq;

namespace hds
{
    internal static class AndvaranautCoupler
    {
        internal const uint Item = 47927;
        internal const ushort Ability = 1379;
        private static readonly AbilityCatalog.Entry Template = AbilityCatalog.Find(Ability);
        // The player template carries the same internal AbilityID as the NPC template.
        private static readonly ushort InternalAbility = AbilityCatalog.IdOf((uint)Template.Number("Ability/AbilityID"));
        private static readonly AbilityCatalog.Modifier[] Modifiers =
            Template.Modifiers.Where(mod => mod.Number("WhenEnabled") == 2).ToArray();
        private static readonly AbilityCatalog.Modifier Pulse =
            Modifiers.Single(mod => mod.Text("StatModUpdateFunc") == "AndvarFunc");

        internal static float Reuse => (float)Template.Number("Ability/RecastTime");
        internal static int RequiredAwakenedRank => (int)Template.Number("AbilityPrerequisite/ReqAbilityLevel");
        internal static uint ActivationFx => (uint)Pulse.Number("ActivationFX");

        internal static bool HandlesRequest(ushort request) => request == Ability || request == InternalAbility;
        internal static bool MatchesItemAlias(uint item, ushort request) => item == Item && request == InternalAbility;

        internal static AbilityBuffs.Buff Definition(int characterLevel) => new AbilityBuffs.Buff
        {
            Seconds = (float)Pulse.Number("Duration"),
            Modifiers = Modifiers.Select(mod =>
            {
                if (!AbilityValues.TryValue(mod, 0, characterLevel, out int value))
                    throw new InvalidOperationException($"Unsupported Andvaranaut value rule for {mod.Component}");
                return new AbilityBuffs.Modifier(AbilityBuffs.KindStat, mod.Target, value);
            }).ToArray(),
            PeriodicFx = 67109045, // tempmods.py: AndvarFunc
            PeriodicFxSeconds = Pulse.Number("UpdateFuncInterval")
        };
    }
}
