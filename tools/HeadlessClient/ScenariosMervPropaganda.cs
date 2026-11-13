namespace HeadlessClient;

public static partial class Scenarios
{
    private static Task MervPropagandaCount(ScenarioContext context) =>
        RunPrimaChain(context, "MervPropaganda", 3, MervPropagandaFixture.Missions,
            MervPropagandaFixture.Prerequisites, "merovingian_1_2_*.xml", evidenceLabel: "merv-propaganda");
}
