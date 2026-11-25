using System;
using System.Collections.Generic;
using hds.shared;

namespace hds
{
    /// <summary>
    /// Disposable fixture proc-draw controller for Dual Pistol Execution (502) native QA.
    /// Controls only the two independent 40% modifier rolls (1010 Ballistic Defense -31, 522 Melee Accuracy -27).
    /// </summary>
    public static class DualPistolExecutionQa
    {
        public const string Mode = "MXO_DUAL_PISTOL_EXECUTION_QA";
        public const ushort AbilityId = 502;

        private sealed class SequenceRandom : Random
        {
            private readonly double[] draws;
            private int index;

            public SequenceRandom(params double[] draws) => this.draws = draws;
            public override double NextDouble() => index < draws.Length ? draws[index++] : 0.99;
        }

        private static readonly object sync = new object();
        private static string activePreset;

        internal static bool Allowed(string enabled, string dev, string database, uint caster, bool inWorld, bool dead) =>
            enabled == "1" && caster == 110 && QaLoadout.Allowed(dev, database, caster, inWorld, dead);

        private static bool Allowed(WorldClient caster) =>
            caster != null && Allowed(Environment.GetEnvironmentVariable(Mode),
                Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS"), Store.config?.dbParams?.DatabaseName,
                caster.playerData.getCharID(), caster.playerData.getOnWorld(), caster.playerData.isDead);

        internal static string Dev(WorldClient caster, string[] args)
        {
            if (!Allowed(caster)) return "Dual Pistol Execution QA proc override is unavailable here.";
            string mode = args.Length > 0 ? args[0].Trim().ToLowerInvariant() : "";
            switch (mode)
            {
                case "both":
                case "1010":
                case "522":
                case "none":
                    lock (sync) activePreset = mode;
                    return $"Dual Pistol Execution 502 proc preset: {mode}";
                case "off":
                    lock (sync) activePreset = null;
                    return "Dual Pistol Execution 502 proc preset: off";
                default:
                    return "Usage: ?pistolproc <both|1010|522|none|off>";
            }
        }

        internal static Random ProcRandom(WorldClient caster, WorldClient recipient, ushort ability)
        {
            if (ability != AbilityId || recipient == null || recipient.playerData.getCharID() != 6114 || !Allowed(caster))
                return null;
            string preset;
            lock (sync) preset = activePreset;
            if (preset == null) return null;

            // AbilityCatalog.Find(502).Modifiers emits 1010 first, then 522 (each with 0.4 ProcTriggerChance).
            // Match each modifier's exact index dynamically so preset selection never depends on catalog ordering.
            var effect = AbilityHostile.Catalog(AbilityId);
            if (effect == null) return null;
            var draws = new List<double>(effect.Mods.Length);
            foreach (var mod in effect.Mods)
            {
                bool hit = preset == "both" ||
                           (preset == "1010" && mod.Target == 1010) ||
                           (preset == "522" && mod.Target == 522);
                draws.Add(hit ? 0.1 : 0.9);
            }
            return new SequenceRandom(draws.ToArray());
        }
    }
}
