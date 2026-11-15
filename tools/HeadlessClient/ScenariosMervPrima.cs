namespace HeadlessClient;
public static partial class Scenarios
{
    private static Task MervPrimaChain(ScenarioContext context)=>RunPrimaChain(context,"MervPrima",3,
        MervChainFixture.Missions,MervChainFixture.Prerequisites,"exile_*.xml");
}
