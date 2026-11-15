namespace NativeQa.Modes;
public sealed class MissionPresentationMode : NativeQaMode
{
    public override string Name=>"mission-presentation";
    public override NativeQaModePlan Plan {get;}=new(
        new ushort[]{2,17,1006,77},59000,12200,Array.Empty<string>(),Array.Empty<string>(),
        new[]{HeadlessClient.MissionPresentationFixture.PriorMissionsSql(110),
            "UPDATE characters SET y=1095,alignment=1,healthC=5000,healthM=5000 WHERE charId=110"},DevCommands:true);
}
