namespace HeadlessClient;
public static partial class Scenarios
{
    private static Task ZionCriticalOpening(ScenarioContext context)=>RunPrimaChain(context,"ZionCritical",1,
        ZionCriticalFixture.Missions,ZionCriticalFixture.Prerequisites,"zion_1_*.xml",requireStandardCourier:true);
}
