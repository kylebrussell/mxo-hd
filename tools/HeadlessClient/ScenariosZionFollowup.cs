namespace HeadlessClient;
public static partial class Scenarios
{
    private static Task ZionCriticalFollowup(ScenarioContext context)=>RunPrimaChain(context,"ZionFollowup",1,
        ZionFollowupFixture.Missions,ZionFollowupFixture.Prerequisites,"zion_1_2_*.xml",requireStandardCourier:true,evidenceLabel:"zion-followup");
}
