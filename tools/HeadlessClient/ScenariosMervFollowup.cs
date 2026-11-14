namespace HeadlessClient;
public static partial class Scenarios
{
    private static Task MervCriticalFollowup(ScenarioContext context)=>RunPrimaChain(context,"MervFollowup",3,
        MervFollowupFixture.Missions,MervFollowupFixture.Prerequisites,"merovingian_1_2_*.xml",evidenceLabel:"merv-critical-followup",relogAfterChain:true,checkpointRecreatedCarriedNpcs:true);
}
