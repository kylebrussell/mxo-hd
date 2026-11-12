namespace NativeQa.Modes;
/// <summary>Tyndall is a remote mission contact; start at the known Mara hardline, not an invented NPC contact.</summary>
public sealed class ZionChainMode : NativeQaMode
{
    public override string Name=>"zion-chain";
    public override NativeQaModePlan Plan {get;}=new(
        new ushort[]{2,17,1006,77},17000,2300,Array.Empty<string>(),Array.Empty<string>(),
        new[]{HeadlessClient.ZionChainFixture.SeedPrerequisites(110),
            "UPDATE characters SET level=50,y=495,alignment=1,healthC=5000,healthM=5000,innerStrC=2000,innerStrM=2000 WHERE charId=110"},DevCommands:true,environmentOverrides:new(){["MXO_MISSION_SITE_QA_SEED"]="230410"});
}
