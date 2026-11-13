using System;
using System.Linq;
using hds.shared;
namespace hds
{
    /// <summary>Disposable diagnostic only. Changes a serialized pose, never the source/saved placement or use authority.</summary>
    internal static class MissionObjectPoseProbe
    {
        internal const string Variable="MXO_MISSION_OBJECT_POSE_PROBE";
        internal const string OriginalQuaternion="00000000f20435bf00000000f304353f";
        internal const string IdentityQuaternion="0000000000000000000000000000803f";
        internal readonly struct Pose
        {
            internal readonly double X,Y,Z;
            internal readonly string Quaternion;
            internal Pose(double x,double y,double z,string quaternion){X=x;Y=y;Z=z;Quaternion=quaternion;}
        }
        internal static bool Variant(string value)=>value=="control"||value=="native-y"||value=="full-native";
        internal static bool DatabaseAllowed(string value)=>value!=null&&value.StartsWith("mxotest_",StringComparison.Ordinal)&&value.Length>8&&value.All(c=>char.IsAsciiLetterOrDigit(c)||c=='_');
        internal static bool SourceMatches(StaticWorldObject s)=>s!=null&&s.metrId==1&&s.sectorID==347&&s.mxoStaticId==0x15b01176u&&s.staticId==0x1176u&&s.type!=null&&s.type.Length>=2&&
            MissionSites.Goid(s)==1415&&s.pos_x==92740&&s.pos_y==4445&&s.pos_z==13535&&string.Equals(s.quat,OriginalQuaternion,StringComparison.OrdinalIgnoreCase);
        internal static Pose SerializedPose(WorldClient client,MissionProgress progress,MissionObjectState state)
        {
            var s=state.spot;var original=new Pose(s.pos_x,s.pos_y,s.pos_z,s.quat);
            string mode=Environment.GetEnvironmentVariable(Variable);
            if(!Variant(mode))return original;
            var data=client?.playerData;
            if(Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS")!="1"||!DatabaseAllowed(Store.config?.dbParams?.DatabaseName)||
                client?.Alive!=true||data==null||!data.getUDPSEssionEstablished()||data.getCharID()!=110||
                SocialHandler.HandleOf(client)!="CSR_Tester"||!data.getOnWorld()||data.getDistrictId()!=1||data.DistrictIsLA()||data.worldChangePending||data.isDead||data.duelDeath||
                progress==null||!ReferenceEquals(data.missionProgress,progress)||!ReferenceEquals(data.activeMission,progress.mission)||progress.mission.id!=3072||progress.phaseIndex!=0||progress.failed||
                state.def?.id!=1||state.def.kind!="Computer"||state.goid!=3||!progress.objects.TryGetValue(1,out var owned)||!ReferenceEquals(owned,state)||!SourceMatches(s))return original;
            var pose=mode=="full-native"?new Pose(91865,4300,12660,IdentityQuaternion):mode=="native-y"?new Pose(92740,4300,13535,s.quat):original;
            Output.WriteDebugLog(FormattableString.Invariant($"[MISSION-POSE-PROBE] DIAGNOSTIC variant={mode} char=110 mission=3072 phase=0 object=1 source=7611b015 sourceGO=1415 dynamicGO=3 original=92740,4445,13535 serialized={pose.X},{pose.Y},{pose.Z} quaternion={pose.Quaternion} instance={data.missionInstanceCounter} world={data.worldGeneration} life={data.lifeGeneration} sourceUnchanged=True"));
            return pose;
        }
    }
}
