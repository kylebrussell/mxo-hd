using hds;

namespace NativeQa.Modes;

public sealed class PosterHuntMode : NativeQaMode
{
    public override string Name => "poster-hunt-count";
    public override NativeQaModePlan Plan { get; } = new(new ushort[] { 2, 17, 1006, 77 }, 17000, 2300,
        Array.Empty<string>(), Array.Empty<string>(),
        new[] { HeadlessClient.PosterHuntFixture.SeedPrerequisites(110),
            "UPDATE characters SET level=50,exp=" + PlayerProgression.ExperienceForLevel(50) +
            ",y=495,alignment=2,healthC=5000,healthM=5000,innerStrC=2000,innerStrM=2000 WHERE charId=110" },
        DevCommands: true, environmentOverrides: new() { ["MXO_MISSION_SITE_QA_SEED"] = "201202" },
        extraQaMetadata: new() { ["PosterHuntCountQa"] = true, ["ExpectedPosterCounts"] = "3,1,4" },
        readyMessages: new[] {
            "CSR110: Call Agent Gray > Critical > Poster Hunt. Use the existing Box proxies in groups of 3, 1 and 4. In the last building, the second triggers the alarm; three must not finish the area, and the fourth must. Native box placement/picking and actual poster models remain unverified. No forced combat roll or GM. See docs/MISSION-POSTER-HUNT.md."
        });
}
