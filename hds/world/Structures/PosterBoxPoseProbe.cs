using System;
using System.Linq;
using hds.shared;
namespace hds
{
    /// <summary>Disposable QA serialization diagnostic only; never changes source placement or use authority.</summary>
    internal static class PosterBoxPoseProbe
    {
        internal const string Variable="MXO_POSTER_BOX_POSE_PROBE";
        internal const string LegacyQuaternion="00000000f204353f00000000f304353f";
        internal const string NativeQuaternion="00000000f304353f00000000f20435bf";
        internal static bool Variant(string value)=>value=="control"||value=="source-native";
        internal static bool DatabaseAllowed(string value)=>value!=null&&value.StartsWith("mxotest_",StringComparison.Ordinal)&&value.Length>8&&value.All(c=>char.IsAsciiLetterOrDigit(c)||c=='_');
        internal static bool SourceMatches(StaticWorldObject s)=>s!=null&&s.metrId==1&&!s.exterior&&s.sectorID==209&&s.staticId==988&&s.mxoStaticId==0x0d1003dcu&&
            s.type!=null&&(s.type.Length==2||s.type.Length==4)&&s.type[0]==0x87&&s.type[1]==5&&(s.type.Length==2||s.type[2]==0&&s.type[3]==0)&&
            s.pos_x==23940&&s.pos_y==2245&&s.pos_z==15050&&string.Equals(s.quat,LegacyQuaternion,StringComparison.OrdinalIgnoreCase);
        internal static MissionComputerAnchors.Pose SerializedPose(WorldClient client,MissionProgress progress,MissionObjectState state,MissionComputerAnchors.Pose original)
        {
            string variant=Environment.GetEnvironmentVariable(Variable);
            if(!Variant(variant))return original;
            var data=client?.playerData;
            if(Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS")!="1"||!DatabaseAllowed(Store.config?.dbParams?.DatabaseName)||
                client?.Alive!=true||data==null||!data.getUDPSEssionEstablished()||data.getCharID()!=110||SocialHandler.HandleOf(client)!="CSR_Tester"||
                !data.getOnWorld()||data.getDistrictId()!=1||data.DistrictIsLA()||data.worldChangePending||data.isDead||data.duelDeath||
                progress==null||!ReferenceEquals(data.missionProgress,progress)||!ReferenceEquals(data.activeMission,progress.mission)||progress.mission.id!=201202||progress.phaseIndex!=0||progress.failed||
                state?.def?.id!=1||state.def.kind!="Box"||state.goid!=548||!progress.objects.TryGetValue(1,out var owned)||!ReferenceEquals(owned,state)||!SourceMatches(state.spot))return original;
            // Refuse if a future normal consumer already changes this base pose; never mask it with an old diagnostic.
            if(original.X!=23940||original.Y!=2245||original.Z!=15050||!original.Orientation.SequenceEqual(StringUtils.hexStringToBytes(LegacyQuaternion)))return original;
            var pose=variant=="source-native"?new MissionComputerAnchors.Pose(23940,2100,15050,StringUtils.hexStringToBytes(NativeQuaternion)):original;
            Output.WriteDebugLog(FormattableString.Invariant($"[POSTER-BOX-POSE] DIAGNOSTIC variant={variant} char=110 mission=201202 phase=0 object=1 source=dc03100d sourceGO=1415 concreteGO=548 original=23940,2245,15050 serialized={pose.X},{pose.Y},{pose.Z} quaternion={Convert.ToHexString(pose.Orientation)} instance={data.missionInstanceCounter} world={data.worldGeneration} life={data.lifeGeneration} sourceUnchanged=True"));
            return pose;
        }
    }
}
