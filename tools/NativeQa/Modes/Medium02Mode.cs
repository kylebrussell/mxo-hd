namespace NativeQa.Modes;
public sealed class Medium02Mode : NativeQaMode
{
    public override string Name=>"medium02";
    public override NativeQaModePlan Plan{get;}=new(new ushort[]{2,17},-1000,2500,Array.Empty<string>(),
        new[]{"UPDATE characters SET district='medium02',districtId=15,y=95,rotation=0 WHERE charId=110"},Array.Empty<string>(),
        extraQaMetadata:new(){["Medium02Partial"]=true,["WorldMap"]=15,["Hardline"]=1,["ExcludedStaticKeyCount"]=597,
            ["CapturedPopulationValidated"]=false,["LoadingAreaUiReturnUnresolved"]=true},
        readyMessages:new[]{"Medium02 PARTIAL: shipped15/1;426safe statics/100nav sectors;597native keys omitted. CapturedNPCs not validated or relocated. Walk to native414door76/210 near-5700,15000; normalOpen only. LA native UI return unresolved;80fa15 is not a fix. See WORLD-MEDIUM02.md."});
}
