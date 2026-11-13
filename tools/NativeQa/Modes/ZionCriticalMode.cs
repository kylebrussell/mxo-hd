using hds;
namespace NativeQa.Modes;
public sealed class ZionCriticalMode : NativeQaMode
{
    public override string Name=>"zion-critical-opening";
    public override NativeQaModePlan Plan {get;}=new(new ushort[]{2,17,1006,77},17000,2300,
        Array.Empty<string>(),Array.Empty<string>(),
        new[]{HeadlessClient.ZionCriticalFixture.SeedPrerequisites(110),
            "UPDATE characters SET level=50,exp="+PlayerProgression.ExperienceForLevel(50)+",y=495,alignment=1,healthC=5000,healthM=5000,innerStrC=2000,innerStrM=2000 WHERE charId=110"},
        DevCommands:true,environmentOverrides:new(){["MXO_MISSION_SITE_QA_SEED"]="101101"},
        extraQaMetadata:new(){["ZionCriticalOpeningQa"]=true,["MissionLayoutSeedOnly"]=true},
        readyMessages:new[]{"Tyndall Call: select Critical Redpill Rescue beside Standard Courier. Earn101101–101103 then101201–101202 in order. No selected completion is seeded. See docs/MISSION-ZION-CRITICAL-OPENING.md for authored/retail limits."});
}
