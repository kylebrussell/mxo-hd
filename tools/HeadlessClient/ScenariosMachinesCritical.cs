namespace HeadlessClient;
public static partial class Scenarios
{
    private static Task MachinesCriticalOpening(ScenarioContext context)=>RunPrimaChain(context,"MachinesCritical",2,
        MachinesCriticalFixture.Missions,MachinesCriticalFixture.Prerequisites,"machines_1_1_*.xml");
}
