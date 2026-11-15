namespace HeadlessClient;

public static partial class Scenarios
{
    private static Task PosterHuntCount(ScenarioContext context) =>
        RunPrimaChain(context, "PosterHunt", 2, PosterHuntFixture.Missions,
            PosterHuntFixture.Prerequisites, "machines_1_2_*.xml", evidenceLabel: "poster-hunt");
}
