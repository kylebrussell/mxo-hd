using System;
using hds.shared;

namespace hds
{
    /// <summary>
    /// Disposable fixture proc-draw controller for SneakFree* control throws (1127 Harrying Attack, 1132 Dazzling Throw).
    /// Controls only the probabilistic state proc draws; damage, cast, stealth, reuse, and expiry paths remain ordinary.
    /// </summary>
    public static class SneakFreeControlsQa
    {
        public const string Mode = "MXO_SNEAK_FREE_CONTROLS_QA";

        private sealed class FixedProcRandom : Random
        {
            private readonly double draw;
            public FixedProcRandom(double draw) => this.draw = draw;
            public override double NextDouble() => draw;
        }

        private static readonly Random alwaysProc = new FixedProcRandom(0.0);
        private static readonly Random neverProc = new FixedProcRandom(0.99);
        private static readonly object sync = new object();
        private static string preset = "all";

        internal static bool Allowed(string enabled, string dev, string database, uint caster, bool inWorld, bool dead) =>
            enabled == "1" && caster == 110 && QaLoadout.Allowed(dev, database, caster, inWorld, dead);

        private static bool Allowed(WorldClient caster) =>
            caster != null && Allowed(Environment.GetEnvironmentVariable(Mode),
                Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS"), Store.config?.dbParams?.DatabaseName,
                caster.playerData.getCharID(), caster.playerData.getOnWorld(), caster.playerData.isDead);

        internal static string Dev(WorldClient caster, string[] args)
        {
            if (!Allowed(caster)) return "Sneak-free controls QA proc override is unavailable here.";
            string mode = args.Length > 0 ? args[0].Trim().ToLowerInvariant() : "";
            switch (mode)
            {
                case "all":
                case "none":
                case "off":
                    lock (sync) preset = mode;
                    return $"Sneak-free controls proc preset: {mode}";
                default:
                    return "Usage: ?sneakproc <all|none|off>";
            }
        }

        internal static Random ProcRandom(WorldClient caster, WorldClient recipient, ushort ability)
        {
            if (!SneakFreeThrowAdmission.Handles(ability) || recipient == null ||
                recipient.playerData.getCharID() != 6114 || !Allowed(caster))
                return null;
            string current;
            lock (sync) current = preset;
            return current switch
            {
                "all" => alwaysProc,
                "none" => neverProc,
                _ => null
            };
        }
    }
}
