using hds;
namespace NativeQa.Modes;
public sealed class MachinesCriticalMode : NativeQaMode
{
    public override string Name=>"machines-critical-opening";
    public override NativeQaModePlan Plan {get;}=new(new ushort[]{2,17,1006,77},17000,2300,
        Array.Empty<string>(),Array.Empty<string>(),
        new[]{HeadlessClient.MachinesCriticalFixture.SeedPrerequisites(110),
            "UPDATE characters SET level=50,exp="+PlayerProgression.ExperienceForLevel(50)+",y=495,alignment=2,healthC=5000,healthM=5000,innerStrC=2000,innerStrM=2000 WHERE charId=110"},
        DevCommands:true,environmentOverrides:new(){["MXO_MISSION_SITE_QA_SEED"]="201101"},
        extraQaMetadata:new(){["MachinesCriticalOpeningQa"]=true,["MissionLayoutSeedOnly"]=true},
        readyMessages:new[]{"Agent Gray remote Call > Critical Interference, Redpill Redux, Scheduled for Deletion. Three selected completions are NOT seeded. Existing weaken plus normal combat/loot/escort/Use only; see docs/MISSION-MACHINES-CRITICAL-OPENING.md. Placement/native reachability pending."});
}
