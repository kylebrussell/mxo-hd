using System;
using System.Linq;

namespace hds
{
    internal static class SmithRagePill
    {
        internal const uint Item = 46994;
        internal const ushort Ability = 1257;

        private static readonly AbilityCatalog.Modifier[] Modifiers =
            AbilityCatalog.Find(Ability).Modifiers.Where(mod => mod.Number("WhenEnabled") == 2).ToArray();
        private static readonly uint ActivationFx =
            (uint)Modifiers.Single(mod => mod.Has("ActivationFX")).Number("ActivationFX");

        internal static void Apply(WorldClient client)
        {
            int level = client.playerInstance.Level.getValue()[0];
            var effect = new TempMods.Effect
            {
                Ability = Ability,
                Mods = Modifiers.Select(mod => ResolveModifier(mod, level)).ToArray()
            };
            TimedEffects.Apply(TimedEffects.ForPlayer(client), effect, TimeUtils.GetSimTimeSeconds(),
                client, TimedEffects.PlayerName(client));
            ServerPackets.SendPlayerAbilityEffect(client, ActivationFx);
        }

        private static TempMods.Mod ResolveModifier(AbilityCatalog.Modifier modifier, int level)
        {
            float seconds = (float)modifier.Number("Duration");
            if (modifier.State != 0)
                return new TempMods.Mod
                {
                    Kind = TempMods.KindState, Proc = TempMods.Proc.State, Target = modifier.State, Seconds = seconds
                };

            if (!AbilityValues.TryValue(modifier, 0, level, out int value))
                throw new InvalidOperationException($"Unsupported Smith's Rage value rule for {modifier.Component}");
            return new TempMods.Mod
            {
                Kind = TempMods.KindStat, Proc = TempMods.Proc.Stat, Target = modifier.Target,
                MinValue = value, MaxValue = value, Seconds = seconds
            };
        }
    }
}
