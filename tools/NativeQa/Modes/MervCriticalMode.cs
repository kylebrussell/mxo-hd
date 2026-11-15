using hds;
namespace NativeQa.Modes;
public sealed class MervCriticalMode : NativeQaMode
{
    public override string Name=>"merovingian-critical-opening";
    public override NativeQaModePlan Plan {get;}=new(new ushort[]{2,17,1006,77},17000,2300,
        Array.Empty<string>(),Array.Empty<string>(),
        new[]{HeadlessClient.MervCriticalFixture.SeedPrerequisites(110),
            "UPDATE characters SET level=50,exp="+PlayerProgression.ExperienceForLevel(50)+",y=495,alignment=3,healthC=5000,healthM=5000,innerStrC=2000,innerStrM=2000 WHERE charId=110"},
        DevCommands:true,environmentOverrides:new(){["MXO_MISSION_SITE_QA_SEED"]="301101"},
        extraQaMetadata:new(){["MervCriticalOpeningQa"]=true,["MissionLayoutSeedOnly"]=true},
        readyMessages:new[]{"Flood remote Call > Critical Digital Device, Securing the Signal, Signature Manipulation. Three selected completions are NOT seeded. Existing weaken plus normal combat/loot/escort/Use only; see docs/MISSION-MEROVINGIAN-CRITICAL-OPENING.md. Placement/native reachability pending."});
}
