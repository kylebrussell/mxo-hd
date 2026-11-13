using hds;
namespace NativeQa.Modes;
public sealed class PosterBoxFloorMode : NativeQaMode
{
    public override string Name => "poster-box-floor";
    public override NativeQaModePlan Plan { get; } = new(new ushort[]{2,17,1006,77},17000,2300,
        Array.Empty<string>(),Array.Empty<string>(),new[]{HeadlessClient.PosterHuntFixture.SeedPrerequisites(110),
            "UPDATE characters SET level=50,exp="+PlayerProgression.ExperienceForLevel(50)+",y=495,alignment=2,healthC=5000,healthM=5000,innerStrC=2000,innerStrM=2000 WHERE charId=110"},
        DevCommands:true,environmentOverrides:new(){["MXO_MISSION_SITE_QA_SEED"]="201202",[PosterBoxFloorProbe.EnvironmentKey]="1"},
        extraQaMetadata:new(){["PosterBoxFloorQa"]=true,["DiagnosticOnly"]=true,["NativePickingProven"]=false},
        readyMessages:new[]{"CSR110: Call Agent Gray > Critical > Poster Hunt; accept and use its normal elevator. Enter ?posterboxfloor on. Walk normally to 24960/2095/14100, face +X and LOOK DOWN at the floor. The separate free Box is at 25100/2040/14100. Inspect model, hover, target and radial Open; record actual C2S 80c7. The 140-unit approach is for looking; MaxDistance is 100, so move closer normally if native Open reports reach refusal. It has no quest authority or server Open executor. ?posterboxfloor off deletes only the private probe. Stop if the path or space is obstructed. Normal mission objects remain unchanged. See docs/POSTER-BOX-FLOOR-PROBE.md."});
}
