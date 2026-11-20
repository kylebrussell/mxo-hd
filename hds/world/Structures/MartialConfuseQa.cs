using System;
using System.Collections.Generic;
using System.Linq;
using hds.shared;

namespace hds
{
    /// <summary>
    /// Disposable fixture gates for the Martial Arts Confuse native QA mode:
    /// Suicidal Butterfly (428), Counter Throw (273), and Aikido Spinning Clay Pigeon (296).
    /// </summary>
    public static class MartialConfuseQa
    {
        public const string Mode = "MXO_MARTIAL_CONFUSE_QA";
        public static IReadOnlyList<ushort> FixedAbilityIds { get; } =
            Array.AsReadOnly(new ushort[] { KungFuGrandmasterSpecials.SuicidalButterfly });

        private sealed class AlwaysProcRandom : Random
        {
            public override double NextDouble() => 0.0;
        }

        private static readonly Random alwaysProc = new AlwaysProcRandom();

        internal static bool Allowed(string enabled, string dev, string database, uint caster, bool inWorld, bool dead) =>
            enabled == "1" && caster == 110 && QaLoadout.Allowed(dev, database, caster, inWorld, dead);

        private static bool Allowed(WorldClient caster) =>
            caster != null && Allowed(Environment.GetEnvironmentVariable(Mode),
                Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS"), Store.config?.dbParams?.DatabaseName,
                caster.playerData.getCharID(), caster.playerData.getOnWorld(), caster.playerData.isDead);

        /// <summary>
        /// In the disposable CSR -> QAEnemy (6114) fixture, fixes Suicidal Butterfly (428)'s 30% Confuse modifier chance
        /// so every landed hit exercises live Confuse, special-move refusal, expiry, and recovery.
        /// Combat contest, damage, duration, and expiry paths remain unchanged.
        /// </summary>
        internal static Random ProcRandom(WorldClient caster, WorldClient recipient, ushort ability) =>
            recipient != null && recipient.playerData.getCharID() == 6114 && FixedAbilityIds.Contains(ability) && Allowed(caster)
                ? alwaysProc
                : null;

        /// <summary>
        /// In the disposable CSR -> QAEnemy (6114) fixture, guarantees the real retail Grab tactic Off-Balance condition
        /// proc (normally 25%) on damaging Grab rounds so CSR's stock client receives QAEnemy's Off-Balance (0x04) flag
        /// through the real PvpCombat.ApplyCondition -> PvpFlag.SendFlags path without dev state injection.
        /// </summary>
        internal static bool GuaranteeGrabCondition(WorldClient attacker, WorldClient defender, CombatTactic tactic) =>
            tactic == CombatTactic.Grab && defender != null && defender.playerData.getCharID() == 6114 && Allowed(attacker);
    }
}
