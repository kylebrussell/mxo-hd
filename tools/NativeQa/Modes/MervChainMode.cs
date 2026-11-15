namespace NativeQa.Modes;
/// <summary>Flood is the remote mission contact; placements and site pacing are authored OURS.</summary>
public sealed class MervChainMode : NativeQaMode
{
    public override string Name=>"merv-chain";
    public override NativeQaModePlan Plan {get;}=new(
        new ushort[]{2,17,1006,77},17000,2300,Array.Empty<string>(),Array.Empty<string>(),
        new[]{HeadlessClient.MervChainFixture.SeedPrerequisites(110),
            "UPDATE characters SET level=50,y=495,alignment=3,healthC=5000,healthM=5000,innerStrC=2000,innerStrM=2000 WHERE charId=110"},DevCommands:true,environmentOverrides:new(){["MXO_MISSION_SITE_QA_SEED"]="210110"});
}
