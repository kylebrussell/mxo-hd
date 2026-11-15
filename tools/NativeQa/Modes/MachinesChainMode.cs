namespace NativeQa.Modes;
/// <summary>Agent Gray's remote contact chain; only intro prerequisites are seeded.</summary>
public sealed class MachinesChainMode : NativeQaMode
{
    public override string Name=>"machines-chain";
    public override NativeQaModePlan Plan{get;}=new(new ushort[]{2,17,1006,77},17000,2300,
        Array.Empty<string>(),Array.Empty<string>(),new[]{HeadlessClient.MachinesChainFixture.SeedPrerequisites(110),
            "UPDATE characters SET level=50,y=495,alignment=2,healthC=5000,healthM=5000,innerStrC=2000,innerStrM=2000 WHERE charId=110"},
        DevCommands:true,environmentOverrides:new(){["MXO_MISSION_SITE_QA_SEED"]="220110"},
        extraQaMetadata:new(){["MachinesPrimaChainQa"]=true,["MachinesPrimaFirst"]=2201,["MachinesPrimaLast"]=2210,
            ["MachinesPrimaPhases"]=25,["MachinesPrimaGoals"]=52},
        readyMessages:new[]{"Agent Gray remote Missions contact > Critical missions; ten Prima missions2201–2210 are NOT pre-completed. Existing ?weaken 1 plus normal radial combat/objects only; see MACHINES-PRIMA-CHAIN.md."});
}
