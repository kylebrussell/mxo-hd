using System;
using System.Linq;
using hds.shared;
namespace hds
{
    /// <summary>Read-only recipient diagnostics on an explicitly opted-in disposable database.</summary>
    public static class AreaTargetDiagnostics
    {
        public const string Variable="MXO_DEBUFF_RADIUS_QA";
        public static bool Allowed(string opt,string database)=>opt=="1" && database!=null &&
            database.StartsWith("mxotest_",StringComparison.Ordinal) && database.Length>8 &&
            database.All(c=>char.IsAsciiLetterOrDigit(c)||c=='_');
        public static bool Enabled=>Allowed(Environment.GetEnvironmentVariable(Variable),Store.config?.dbParams?.DatabaseName);
        public static string Describe(WorldClient caster,WorldClient target,string reason,PvpRules.Verdict? verdict,
            double x,double y,double z,double radius)
        {
            var a=PvpCombat.SubjectOf(caster);var b=PvpCombat.SubjectOf(target);
            double tx=0,ty=0,tz=0;NumericalUtils.LtVector3dToDoubles(target.playerInstance.Position.getValue(),ref tx,ref ty,ref tz);
            var view=caster.viewMan.FindViewForEntity(target.playerData.getEntityId());
            return FormattableString.Invariant($"[HOSTILE][AREA-QA] caster={SocialHandler.HandleOf(caster)} org/alignment={a.Organization} flag={a.PvpFlag} team={a.MissionTeamId} mode={PvpFlag.ModeFor(caster)} candidate={SocialHandler.HandleOf(target)} char={b.CharId} org/alignment={b.Organization} flag={b.PvpFlag} team={b.MissionTeamId} district={target.playerData.getDistrictId()} alive={!b.Dead} view={view?.ViewID} created={view?.viewCreated} point={tx},{ty},{tz} centre={x},{y},{z} radius={radius:F4} distance={Math.Sqrt((tx-x)*(tx-x)+(ty-y)*(ty-y)+(tz-z)*(tz-z))/100:F4} floorDelta={ty-y:F2} verdict={(verdict.HasValue?verdict.Value.ToString():"not-evaluated")} reason={reason}");
        }
        public static void Write(string line)=>Output.WriteDebugLog(line);
    }
}
