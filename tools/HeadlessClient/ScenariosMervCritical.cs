namespace HeadlessClient;
public static partial class Scenarios
{
    private static Task MervCriticalOpening(ScenarioContext context)=>RunPrimaChain(context,"MervCritical",3,
        MervCriticalFixture.Missions,MervCriticalFixture.Prerequisites,"merovingian_1_1_*.xml");
}
