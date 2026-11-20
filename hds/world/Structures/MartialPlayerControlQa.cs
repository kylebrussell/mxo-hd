using System;
using System.Linq;
using hds.shared;

namespace hds
{
    /// <summary>Staged memory and modifier chance for the disposable player-recipient run.</summary>
    public static class MartialPlayerControlQa
    {
        public const string Mode = "MXO_MARTIAL_PLAYER_CONTROL_QA";
        public const int KungFuSlot = 200, KarateSlot = 300, EndSlot = 400;

        internal static bool Allowed(string enabled, string dev, string database, uint caster, bool inWorld, bool dead) =>
            enabled == "1" && caster == 110 && QaLoadout.Allowed(dev, database, caster, inWorld, dead);

        private static bool Allowed(WorldClient caster) =>
            caster != null && Allowed(Environment.GetEnvironmentVariable(Mode),
                Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS"), Store.config?.dbParams?.DatabaseName,
                caster.playerData.getCharID(), caster.playerData.getOnWorld(), caster.playerData.isDead);

        // Select the successful chance branch only. The combat contest, damage, values and expiry are unchanged.
        internal static bool ModifierProc(WorldClient caster, WorldClient recipient, ushort ability) =>
            recipient.playerData.getCharID() == 6114 &&
            (ability == 262 || ability == 265 || ability == 429 || ability == 534 || ability == 536) && Allowed(caster);

        internal static string LoadKarate(WorldClient caster)
        {
            if (!ReferenceEquals(caster, Store.currentClient) || !Allowed(caster))
                return "Martial control stage requires the disposable CSR fixture.";
            var db = Store.dbManager.MarginDbHandler;
            var owned = db.LoadAbilities(110);
            var remove = owned.Where(row => row.getSlot() >= KungFuSlot && row.getSlot() < KarateSlot && row.getLoaded()).ToArray();
            var add = owned.Where(row => row.getSlot() >= KarateSlot && row.getSlot() < EndSlot && !row.getLoaded()).ToArray();
            if (remove.Length == 0 || add.Length == 0)
                return "Martial control stage is already changed or its archive is incomplete.";

            var handler = new PlayerHandler();
            byte[] request = QaLoadout.Request(remove, unload: true);
            handler.ProcessLoadAbility(ref request);
            request = QaLoadout.Request(add, unload: false);
            handler.ProcessLoadAbility(ref request);
            var active = db.LoadAbilities(110).Where(row => row.getLoaded()).Select(row => row.getSlot()).ToHashSet();
            return remove.All(row => !active.Contains(row.getSlot())) && add.All(row => active.Contains(row.getSlot()))
                ? "Martial control Karate stage ready."
                : "Martial control Karate stage failed. Stop this fixture and retain the logs.";
        }
    }
}
