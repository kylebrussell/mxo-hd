namespace NativeQa.Modes;
public sealed class Large01Mode : NativeQaMode
{
    public override string Name=>"large01";
    public override NativeQaModePlan Plan{get;}=new(new ushort[]{2,17},13000,-7400,Array.Empty<string>(),
        new[]{"UPDATE characters SET district='large01',districtId=12,y=-1302,rotation=0 WHERE charId=110"},Array.Empty<string>(),
        environmentOverrides:new(){["MXO_MOBS"]=""},
        extraQaMetadata:new(){["Large01Partial"]=true,["WorldMap"]=12,["Hardline"]=1,["ExcludedStaticKeyCount"]=48,
            ["LoadingAreaUiReturnUnresolved"]=true},
        readyMessages:new[]{"Large01 PARTIAL: shipped hardline12/1;953safe statics/153nav sectors;48native keys omitted. No fabricated subway/NPC. Loading Area UI list/return is UNRESOLVED; direct headless80ff is not UI proof. See WORLD-LARGE01.md."});
}
