using System;
using hds.shared;

namespace hds
{
    /// <summary>Logs accepted CSR position updates for disposable movement fixtures.</summary>
    public static class MovementSampleQa
    {
        public const string Mode = "MXO_MOVE_SAMPLE_QA";

        internal static void Record(WorldClient client, float x, float y, float z)
        {
            if (Environment.GetEnvironmentVariable(Mode) != "1")
                return;

            var data = client.playerData;
            if (data.getCharID() != 110 || data.DistrictIsLA() || data.worldChangePending ||
                !QaLoadout.Allowed(Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS"),
                    Store.config?.dbParams?.DatabaseName, 110, data.getOnWorld(), data.isDead || data.duelDeath))
                return;

            Output.WriteDebugLog(FormattableString.Invariant(
                $"[MOVE-SAMPLE] char=110 ms={TimeUtils.GetSimTimeMilliseconds()} district={data.getDistrictId()} world={data.worldGeneration} x={x:R} y={y:R} z={z:R}"));
        }
    }
}
