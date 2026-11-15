namespace NativeQa.Modes;
public sealed class SatiMode : NativeQaMode
{
    public override string Name=>"sati";
    public override NativeQaModePlan Plan{get;}=new(new ushort[]{2,17},111600,-6130,Array.Empty<string>(),
        new[]{"UPDATE characters SET alignment=1,district='slums',districtId=1,y=695,rotation=0 WHERE charId=110; DELETE FROM inventory WHERE charId=110 AND slot IN (3,106); INSERT INTO inventory(charId,goid,slot,count,purity) VALUES(110,47726,3,1,0)"},Array.Empty<string>(),
        environmentOverrides:new(){["MXO_MOBS"]=""},
        extraQaMetadata:new(){["SatiPartial"]=true,["FixtureLevel"]=50,["WorldMap"]=9,["RetailConstructId"]=18,["StaticKeys"]=7917,["ExcludedStaticKeys"]=70,["AvailableNavSectors"]=31,
            ["RetailArrivalUnrecovered"]=true,["ExistingArrivalPolicy"]="OURS same-polygon separated fallback",["GenericSourceReturnersBound"]=false,
            ["SatiArrivalX"]=14225,["SatiArrivalY"]=95,["SatiArrivalZ"]=-5225,
            ["SatiReturnerX"]=14225,["SatiReturnerY"]=95,["SatiReturnerZ"]=-4925,["ExpectedArchiveOrganization"]=1},
        readyMessages:new[]{"Sati PARTIAL: actual50/XP50, Zion47726 bag3 at Richland sender38282. Equip launcher106; wait ordinary180s cycle. OURS arrival14225/95/-5225, generated Zion returner14225/95/-4925 is3m separate on same sourced floor. Inspect target/radial then walk and use normally; native picking remains pending. Exact retail arrival unknown; four47267 proxies UNBOUND. Separate sati-door unchanged. See WORLD-SATI.md."});
}
