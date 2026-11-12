namespace NativeQa.Modes;
public sealed class Medium01Mode : NativeQaMode
{
    public override string Name=>"medium01";
    public override NativeQaModePlan Plan{get;}=new(new ushort[]{2,17},39250,27000,Array.Empty<string>(),
        new[]{"UPDATE characters SET district='medium01',districtId=14,y=-953,rotation=-1.5708 WHERE charId=110"},Array.Empty<string>(),
        environmentOverrides:new(){["MXO_MOBS"]=""},
        extraQaMetadata:new(){["Medium01Partial"]=true,["WorldMap"]=14,["Hardline"]=1,["ExcludedStaticKeyCount"]=0,
            ["LoadingAreaUiReturnUnresolved"]=true},
        readyMessages:new[]{"Medium01 PARTIAL:39validated statics/144nav sectors; native14/1 booth.38statics are empty PropagandaFloorSpawner markers, not spawned gameplay.12captured NPCs remain at unverified/non-nav positions. LA UI return UNRESOLVED; direct80ff is not UI proof. See WORLD-MEDIUM01.md."});
}
