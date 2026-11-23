using System;
using hds.shared;

namespace hds
{
    /// <summary>Repeatable boundary roll for the two actors in the disposable Assassin fixture.</summary>
    internal static class AssassinDetectionQa
    {
        internal const string Mode = "MXO_ASSASSIN_DETECTION_QA";
        internal const int BoundaryRoll = 35;

        internal static bool Allows(string enabled, string dev, string database, uint sneaker, uint observer,
            ushort ability, bool bothInWorld, bool eitherDead) =>
            enabled == "1" && sneaker == 110 && observer == 6114 && ability == Stealth.SneakGrantAbility &&
            QaLoadout.Allowed(dev, database, sneaker, bothInWorld, eitherDead);

        internal static bool Allows(WorldClient sneaker, WorldClient observer, ushort ability) =>
            Allows(Environment.GetEnvironmentVariable(Mode), Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS"),
                Store.config?.dbParams?.DatabaseName, sneaker.playerData.getCharID(), observer.playerData.getCharID(),
                ability, sneaker.playerData.getOnWorld() && observer.playerData.getOnWorld(),
                sneaker.playerData.isDead || observer.playerData.isDead);
    }
}
