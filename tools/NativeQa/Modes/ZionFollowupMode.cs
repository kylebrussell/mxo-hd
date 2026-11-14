using hds;
namespace NativeQa.Modes;
public sealed class ZionFollowupMode : NativeQaMode
{
    public override string Name=>"zion-critical-followup";
    public override NativeQaModePlan Plan {get;}=new(new ushort[]{2,17,1006,77},17000,2300,
        Array.Empty<string>(),Array.Empty<string>(),new[]{HeadlessClient.ZionFollowupFixture.SeedPrerequisites(110),
            "UPDATE characters SET level=50,exp="+PlayerProgression.ExperienceForLevel(50)+",y=495,alignment=1,healthC=5000,healthM=5000,innerStrC=2000,innerStrM=2000 WHERE charId=110"},
        DevCommands:true,environmentOverrides:new(){["MXO_MISSION_SITE_QA_SEED"]="101203"},
        extraQaMetadata:new(){["ZionCriticalFollowupQa"]=true,["MissionLayoutSeedOnly"]=true},
        readyMessages:new[]{"Tyndall Call > Critical Confiscation beside Standard Courier. Earn missions 101203-101207 normally; selected completions are not seeded. Two source computers require PR196. See docs/MISSION-ZION-CRITICAL-FOLLOWUP.md; native accessibility pending."});
}
