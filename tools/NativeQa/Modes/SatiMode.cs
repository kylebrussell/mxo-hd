namespace NativeQa.Modes;
public sealed class SatiMode : NativeQaMode
{
    public override string Name=>"sati";
    public override NativeQaModePlan Plan{get;}=new(new ushort[]{2,17},111600,-6130,Array.Empty<string>(),
        new[]{"UPDATE characters SET alignment=1,district='slums',districtId=1,y=695,rotation=0 WHERE charId=110; DELETE FROM inventory WHERE charId=110 AND slot IN (3,106); INSERT INTO inventory(charId,goid,slot,count,purity) VALUES(110,47726,3,1,0)"},Array.Empty<string>(),
        environmentOverrides:new(){["MXO_MOBS"]=""},
        extraQaMetadata:new(){["SatiPartial"]=true,["FixtureLevel"]=50,["WorldMap"]=9,["RetailConstructId"]=18,["StaticKeys"]=7917,["ExcludedStaticKeys"]=70,["AvailableNavSectors"]=31,
            ["RetailArrivalUnrecovered"]=true,["ExistingArrivalPolicy"]="OURS nearest-entry fallback",["GenericSourceReturnersBound"]=false},
        readyMessages:new[]{"Sati PARTIAL: default actual50/XP50, Zion47726 bag3 at real Richland sender38282. Equip launcher106 and wait ordinary180s cycle. Existing OURS arrival14070/95/-4070 and generated Zion returner remain; exact retail arrival unknown. Four source47267 proxies are UNBOUND. Separate sati-door is direct diagnostic, NOT a walk route. See WORLD-SATI.md."});
}
