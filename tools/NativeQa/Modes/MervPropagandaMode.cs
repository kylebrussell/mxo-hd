using hds;

namespace NativeQa.Modes;

public sealed class MervPropagandaMode : NativeQaMode
{
    public override string Name => "merv-propaganda-count";
    public override NativeQaModePlan Plan { get; } = new(new ushort[] { 2, 17, 1006, 77 }, 17000, 2300,
        Array.Empty<string>(), Array.Empty<string>(),
        new[] { HeadlessClient.MervPropagandaFixture.SeedPrerequisites(110),
            "UPDATE characters SET level=50,exp=" + PlayerProgression.ExperienceForLevel(50) +
            ",y=495,alignment=3,healthC=5000,healthM=5000,innerStrC=2000,innerStrM=2000 WHERE charId=110" },
        DevCommands: true, environmentOverrides: new() { ["MXO_MISSION_SITE_QA_SEED"] = "301101" },
        extraQaMetadata: new() { ["MervPropagandaCountQa"] = true, ["ExpectedPosterCounts"] = "3,5" },
        readyMessages: new[] {
            "CSR110: Call Flood > Critical > Propaganda. Use the existing Box proxies in groups of 3 and 5. The first retains an authored minimum (retail said at least three). In the final building neither three nor four may finish; the fifth must. Native Box placement/picking and real poster models remain unverified. No forced outcomes or GM. See docs/MISSION-MERV-PROPAGANDA.md."
        });
}
