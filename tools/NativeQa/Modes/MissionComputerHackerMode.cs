using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class MissionComputerHackerMode : NativeQaMode
{
    public override string Name => "mission-computer-hacker";

    public override NativeQaModePlan Plan { get; } = new(
        MachinesCriticalFixture.Abilities.Select(row => row.Ability).ToArray(), 17000, 2300,
        Array.Empty<string>(),
        new[] { "UPDATE char_abilities SET level=LEAST(level,2) WHERE char_id=110 AND slot<520" },
        new[]
        {
            $"UPDATE characters SET level=19,exp={PlayerProgression.ExperienceForLevel(19)},y=495,alignment=2," +
            "healthC=5000,healthM=5000,innerStrC=2000,innerStrM=2000,pvpflag=0 WHERE charId=110"
        },
        DevCommands: true,
        environmentOverrides: new()
        {
            ["MXO_MISSION_SITE_QA_SEED"] = "201101",
            ["MXO_QA_MISSION_PHASE_COMMAND"] = "1",
            ["MXO_AGGRO"] = "0",
            ["MXO_MOB_WANDER"] = "0"
        },
        readyMessages: new[]
        {
            "Use ?missionphase 201103 1. This starts the ordinary Camillo room with goal 1 open and bypasses the contact chain.",
            "Use the printed street door and lobby car, then walk to the Computer object 1 on the printed NAV stand and face it.",
            "Use ?qaunload 52, then right-click Hack. Expect 'Load Hacker before attempting to hack this computer.', goal 1 still open, and no XP.",
            "Use ?qaload 52. Check [MODIFIER] source=52 stat=643 +8, then use stock Hack again. Expect one goal 1 completion, idle terminal, and one 6030 XP gain.",
            "Try Hack again if the menu still offers it. No second gain. Note any progress bar and how the stock feedback renders."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Missing mission fixture path.");
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        await MissionCatalogSampleMode.CheckDirectSamplesAsync(context, new[] { (MachinesCriticalFixture.CamilloMission, 1) },
            requiredStandObject: 1);
        return Array.Empty<byte>();
    }
}
