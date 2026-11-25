using System;
using hds.shared;

namespace hds
{
    /// <summary>
    /// Disposable fixture roll control for Disarm Traps (119) so native and headless QA can deterministically exercise
    /// Disarmed, Failed, and Critical Failure outcomes without changing DisarmTraps_Test contest math or detection gates.
    /// </summary>
    public static class DisarmTrapsQa
    {
        public const string Mode = "MXO_DISARM_TRAPS_QA";
        private static readonly object gate = new object();
        private static int? fixedRoll;

        internal static bool Allowed(string enabled, string dev, string database, uint caster, bool inWorld, bool dead) =>
            enabled == "1" && caster == 110 && QaLoadout.Allowed(dev, database, caster, inWorld, dead);

        private static bool Allowed(WorldClient caster) =>
            caster != null && Allowed(Environment.GetEnvironmentVariable(Mode),
                Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS"), Store.config?.dbParams?.DatabaseName,
                caster.playerData.getCharID(), caster.playerData.getOnWorld(), caster.playerData.isDead);

        public static string Dev(WorldClient caster, string[] args)
        {
            if (!Allowed(caster)) return "?traproll requires MXO_DISARM_TRAPS_QA=1 in a disposable QA fixture.";
            string arg = args != null && args.Length > 0 ? args[0].Trim().ToLowerInvariant() : "";
            if (arg == "off")
            {
                lock (gate) fixedRoll = null;
                return "Disarm Traps QA roll override cleared.";
            }
            if (!int.TryParse(arg, out int value) || value < 0 || value > 99)
                return "Usage: ?traproll <0..99|off>";
            lock (gate) fixedRoll = value;
            return "Disarm Traps QA roll set to " + value + ".";
        }

        internal static int? RollFor(WorldClient caster)
        {
            if (!Allowed(caster)) return null;
            lock (gate) return fixedRoll;
        }

        internal static void ResetForTests()
        {
            lock (gate) fixedRoll = null;
        }
    }
}
