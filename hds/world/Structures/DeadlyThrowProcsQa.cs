using System;
using hds.shared;

namespace hds
{
    /// <summary>
    /// Disposable fixture proc-draw aid for Deadly Throw (279)'s two independent 20% control draws
    /// (Dazed 199 and Staggered 201 for 8 s). Controls only the two proc draws on CSR (110) -> QAEnemy (6114).
    /// </summary>
    public static class DeadlyThrowProcsQa
    {
        public const string Mode = "MXO_DEADLY_THROW_PROCS_QA";
        private static readonly object gate = new object();
        private static string selection = "both";

        private sealed class ScriptedRandom : Random
        {
            private readonly double[] draws;
            private int index;
            public ScriptedRandom(params double[] draws) => this.draws = draws;
            public override double NextDouble() => index < draws.Length ? draws[index++] : 0.99;
        }

        internal static bool Allowed(string enabled, string dev, string database, uint caster, bool inWorld, bool dead) =>
            enabled == "1" && caster == 110 && QaLoadout.Allowed(dev, database, caster, inWorld, dead);

        private static bool Allowed(WorldClient caster) =>
            caster != null && Allowed(Environment.GetEnvironmentVariable(Mode),
                Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS"), Store.config?.dbParams?.DatabaseName,
                caster.playerData.getCharID(), caster.playerData.getOnWorld(), caster.playerData.isDead);

        public static string Dev(WorldClient caster, string[] args)
        {
            if (!Allowed(caster)) return "?deadlyproc requires MXO_DEADLY_THROW_PROCS_QA=1 in a disposable QA fixture.";
            string value = args != null && args.Length > 0 ? args[0].Trim().ToLowerInvariant() : "";
            if (value != "both" && value != "daze" && value != "stagger" && value != "none" && value != "off")
                return "Usage: ?deadlyproc <both|daze|stagger|none|off>";
            lock (gate) selection = value;
            return "Deadly Throw QA proc draws: " + value + ".";
        }

        internal static void ResetForTests(string mode = "both")
        {
            lock (gate) selection = mode;
        }

        internal static Random ProcRandom(WorldClient caster, WorldClient recipient, ushort ability)
        {
            if (AbilityCatalog.Canonical(ability) != DeadlyThrowDamage.Id ||
                recipient == null || recipient.playerData.getCharID() != 6114 || !Allowed(caster))
                return null;
            string current;
            lock (gate) current = selection;
            return current switch
            {
                "both" => new ScriptedRandom(0.0, 0.0),
                "daze" => new ScriptedRandom(0.0, 0.99),
                "stagger" => new ScriptedRandom(0.99, 0.0),
                "none" => new ScriptedRandom(0.99, 0.99),
                _ => null
            };
        }
    }
}
