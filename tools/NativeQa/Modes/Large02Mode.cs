namespace NativeQa.Modes;
public sealed class Large02Mode : NativeQaMode
{
    public override string Name=>"large02";
    public override NativeQaModePlan Plan{get;}=new(new ushort[]{2,17},-7500,-2000,Array.Empty<string>(),
        new[]{"UPDATE characters SET district='large02',districtId=13,y=95,rotation=90 WHERE charId=110"},Array.Empty<string>(),
        extraQaMetadata:new(){["Large02Partial"]=true,["WorldMap"]=13,["Hardline"]=1,["ExcludedStaticKeyCount"]=477,
            ["UnavailableNavigationSector"]=47,["LoadingAreaUiReturnUnresolved"]=true},
        readyMessages:new[]{"Large02 PARTIAL: shipped13/1;1619safe statics/225available nav sectors.477native keys omitted;sector47 nav unavailable despite enable flag. No ambient population synthesized. LA native UI return unresolved. Walk to native door27/11 near12200,-4100; see WORLD-LARGE02.md."});
}
