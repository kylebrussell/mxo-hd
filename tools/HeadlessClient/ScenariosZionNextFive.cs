using hds;
namespace HeadlessClient;
public static partial class Scenarios
{
    private static readonly IReadOnlyDictionary<string,string> ZionNextFiveEnvironment=new Dictionary<string,string>(StillNpcs){["MXO_DEV_COMMANDS"]="1",["MXO_MISSION_SITE_QA_SEED"]="101208"};
    private static Task ZionCriticalNextFive(ScenarioContext context)=>RunPrimaChain(context,"ZionNextFive",1,ZionNextFiveFixture.Missions,
        ZionNextFiveFixture.Prerequisites,"zion_1_2_*.xml",requireStandardCourier:true,evidenceLabel:"zion-critical-next-five",
        evidenceOptions:new(checked((int)PlayerProgression.ExperienceForLevel(50)),ColdRestart:true,RestartEnvironment:ZionNextFiveEnvironment,ExpectedExperienceDelta:434250,ExpectedInformationDelta:455000,FreshUploadBooth:true,MissionExperience:new uint[]{68625,90750,137500,68625,68750},MissionInformation:new uint[]{91000,91000,91000,91000,91000}));
}
