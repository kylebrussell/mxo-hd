using System;
using hds.shared;

namespace hds
{
    public static class JumpRedirectQa
    {
        public const string Mode = "MXO_QA_JUMP_REDIRECT";
        // Retail Wan Chai proxy_026 CSEQ 2561. Reuse the captured ground destination and NAV unchanged.
        public const string RetailCancel = "80 c4 00 00 00 e0 c6 a7 de 40 00 00 00 00 00 90 7f c0 00 00 00 a0 7e e5 cf 40 5c 01 7c 00 00 00";

        internal static string Replay(WorldClient client)
        {
            var data = client.playerData;
            if (Environment.GetEnvironmentVariable(Mode) != "1" || data.getCharID() != 110 ||
                !QaLoadout.Allowed(Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS"), Store.config?.dbParams?.DatabaseName,
                    data.getCharID(), data.getOnWorld() && !data.worldChangePending, data.isDead || data.duelDeath))
                return "Jump redirect replay is available only in its disposable fixture.";

            uint now = (uint)Math.Round(TimeUtils.GetSimTimeSeconds() * 1000.0);
            var flight = data.PendingJump;
            if (data.getDistrictId() != 3 || !flight.HasValue || !flight.Value.FullPeakRoute.HasValue ||
                !flight.Value.IsCurrent(now, data) || unchecked(now - flight.Value.FullPeakRoute.Value.StartMilliseconds) <= 1000)
                return "Jump redirect replay needs a full-peak jump in Wan Chai more than one second after takeoff.";

            byte[] body = Convert.FromHexString(RetailCancel.Replace(" ", string.Empty))[2..];
            new AbilityHandler().processHyperJumpCancel(ref body);
            return "Fixture replayed the captured Wan Chai jump redirect.";
        }
    }
}
