using hds;
using HeadlessClient;
namespace NativeQa.Modes;
internal static class PosterBoxPosePlans
{
    public static NativeQaModePlan For(string variant)=>new(new ushort[]{2,17,1006,77},17000,2300,
        Array.Empty<string>(),Array.Empty<string>(),new[]{PosterHuntFixture.SeedPrerequisites(110),
            "UPDATE characters SET level=50,exp="+PlayerProgression.ExperienceForLevel(50)+",y=495,alignment=2,healthC=5000,healthM=5000,innerStrC=2000,innerStrM=2000 WHERE charId=110"},
        DevCommands:true,GmCharacter110:false,FixedCallbackRolls:false,
        environmentOverrides:new(){["MXO_MISSION_SITE_QA_SEED"]="201202",["MXO_POSTER_BOX_POSE_PROBE"]=variant},
        extraQaMetadata:new(){["PosterBoxPoseDiagnostic"]=true,["PosterBoxPoseVariant"]=variant,["PosterBoxSourceKey"]="0d1003dc"},
        readyMessages:new[]{"DIAGNOSTIC "+variant+": CSR 110, normal Agent Gray Call > Poster Hunt > accept > ordinary elevator. Only first Box548 is overlaid; no model/Hidden/range/source-state change. Grounded approach24080/2095/15080. Compare visible model, hover/target, radial Open and C2S337/80c7. Control23940/2245/15050; source-native23940/2100/15050 with exact native quaternion. No completion required; no retail placement/picking claim. See docs/POSTER-BOX-POSE-PROBE.md."});
}
public sealed class PosterBoxPoseControlMode : NativeQaMode
{
    public override string Name=>"poster-box-pose-control";
    public override NativeQaModePlan Plan {get;}=PosterBoxPosePlans.For("control");
}
public sealed class PosterBoxPoseNativeMode : NativeQaMode
{
    public override string Name=>"poster-box-pose-native";
    public override NativeQaModePlan Plan {get;}=PosterBoxPosePlans.For("source-native");
}
