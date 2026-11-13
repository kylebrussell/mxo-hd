using HeadlessClient;
namespace NativeQa.Modes;
public abstract class MissionObjectPoseMode:NativeQaMode
{
    protected static NativeQaModePlan Fixture(string variant)=>new(NeighbourhoodMissionFixture.Abilities,NeighbourhoodMissionFixture.X,NeighbourhoodMissionFixture.Z,
        Array.Empty<string>(),Array.Empty<string>(),new[]{NeighbourhoodMissionFixture.NativeActorSql},DevCommands:true,GmCharacter110:false,FixedCallbackRolls:false,
        environmentOverrides:new(){["MXO_MISSION_SITE_QA_SEED"]="3072",["MXO_MISSION_OBJECT_POSE_PROBE"]=variant},
        extraQaMetadata:new(){["MissionObjectPoseDiagnostic"]=true,["MissionObjectPoseVariant"]=variant,["MissionObjectPoseMission"]=3072,["MissionObjectPoseSourceKey"]="7611b015",["MissionObjectPoseDefaultChanged"]=false},
        readyMessages:new[]{"DIAGNOSTIC ONLY: normal Chessman Get Mission > Retrieval > accept > elevator. Exact3072/Object1/source7611b015 only; variant "+variant+". Compare computer geometry/hover/click/radial and337 versus80c7; no completion/picking/coordinate-fix claim. Source/site/waypoint/interact/economy untouched. See docs/MISSION-OBJECT-POSE-PROBE.md."});
}
public sealed class MissionObjectPoseControlMode:MissionObjectPoseMode
{public override string Name=>"mission-object-pose-control";public override NativeQaModePlan Plan{get;}=Fixture("control");}
public sealed class MissionObjectPoseNativeYMode:MissionObjectPoseMode
{public override string Name=>"mission-object-pose-native-y";public override NativeQaModePlan Plan{get;}=Fixture("native-y");}
public sealed class MissionObjectPoseFullNativeMode:MissionObjectPoseMode
{public override string Name=>"mission-object-pose-full-native";public override NativeQaModePlan Plan{get;}=Fixture("full-native");}
