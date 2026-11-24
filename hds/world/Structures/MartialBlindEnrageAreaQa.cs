using System;
using System.Collections.Generic;
using System.Linq;
using hds.shared;

namespace hds
{
    /// <summary>Disposable fixture proc-chance gate for the Martial Arts Blind (260), Enraged (266), area FX (268), and Ki Burst (406) native QA modes.</summary>
    public static class MartialBlindEnrageAreaQa
    {
        public const string Mode = "MXO_MARTIAL_BLIND_ENRAGE_AREA_QA";
        public const string KiBurstMode = "MXO_KI_BURST_RECIPIENT_QA";
        public static IReadOnlyList<ushort> FixedAbilityIds { get; } =
            Array.AsReadOnly(new ushort[] { KarateAikidoMasterSpecials.SidekickCombo });

        private sealed class AlwaysProcRandom : Random
        {
            public override double NextDouble() => 0.0;
        }

        private static readonly Random alwaysProc = new AlwaysProcRandom();

        internal static bool Allowed(string enabled, string dev, string database, uint caster, bool inWorld, bool dead) =>
            enabled == "1" && caster == 110 && QaLoadout.Allowed(dev, database, caster, inWorld, dead);

        private static bool Allowed(WorldClient caster, string mode) =>
            caster != null && Allowed(Environment.GetEnvironmentVariable(mode),
                Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS"), Store.config?.dbParams?.DatabaseName,
                caster.playerData.getCharID(), caster.playerData.getOnWorld(), caster.playerData.isDead);

        /// <summary>
        /// In the disposable CSR -> QAEnemy (6114) fixture, fixes the active mode's 20% Enraged modifier chance
        /// so every landed hit exercises the live recipient state, withdraw suppression, expiry, and recovery.
        /// Combat contest, damage, duration, and expiry paths remain unchanged.
        /// </summary>
        internal static Random ProcRandom(WorldClient caster, WorldClient recipient, ushort ability) =>
            recipient != null && recipient.playerData.getCharID() == 6114 && ((FixedAbilityIds.Contains(ability) && Allowed(caster, Mode)) ||
                (ability == MartialArtsRemainingSpecials.KiBurst && Allowed(caster, KiBurstMode)) ||
                ((ability == 411 || ability == 412) && CombatFixtureWeapons.AllowsAikidoControlWin(caster, ability)))
                ? alwaysProc
                : null;
    }
}
