using System;
using System.Linq;
using hds.shared;

namespace hds
{
    /// <summary>Gives the stock client time to try a same-fight retarget in the disposable fixture.</summary>
    public static class EnrageRetargetQa
    {
        public const string Mode = "MXO_ENRAGE_RETARGET_QA";
        public const string Command = "rh";
        public const ushort Ability = 597;
        public const int HoldSeconds = 45;

        internal static string Hold(WorldClient recipient)
        {
            var data = recipient.playerData;
            if (Environment.GetEnvironmentVariable(Mode) != "1" || data.getCharID() != 110 ||
                !QaLoadout.Allowed(Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS"), Store.config?.dbParams?.DatabaseName,
                    data.getCharID(), data.getOnWorld() && !data.worldChangePending && !data.DistrictIsLA(), data.isDead || data.duelDeath))
                return "Retarget hold requires the disposable Proxy Master fixture.";

            var fight = CombatManager.GetHandler(recipient).pvpFight;
            if (fight?.IsActive != true || fight.SideOf(recipient)?.Other?.Client.playerData.getCharID() != 6114 ||
                !fight.CurrentOpponents(recipient).Any(client => client.playerData.getCharID() == 6113))
                return "Return to QAEnemy with QAOther still in the same interlock before starting the hold.";

            var subject = TimedEffects.ForPlayer(recipient);
            double now = TimeUtils.GetSimTimeSeconds();
            if (subject.Effects.HasAt(TempMods.EnrageAbility, now))
                return "An Enrage state is already active. Wait for its removal.";

            if (!TempMods.TryGet(Ability, out var effect))
                return "Retarget hold could not load the Enrage state.";
            // The ordinary cast and its natural lifetime are unchanged. This fixture applies
            // the same timed state through the normal state lifecycle, without charging or casting.
            var active = TimedEffects.Apply(subject, effect, now, configure: mod => mod.Seconds = HoldSeconds);
            if (!active.Any(mod => mod.Target == TempMods.EnrageAbility))
                return "Retarget hold could not apply the Enrage state.";
            return $"RETARGET HOLD ACTIVE for {HoldSeconds} seconds.";
        }
    }
}
