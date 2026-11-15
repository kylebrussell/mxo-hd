namespace NativeQa.Modes;
public sealed class DataMineMode : NativeQaMode
{
    public override string Name=>"datamine";
    public override NativeQaModePlan Plan{get;}=new(new ushort[]{2,17},111600,-6130,Array.Empty<string>(),
        new[]{"UPDATE characters SET y=745,alignment=1 WHERE charId=110; DELETE FROM inventory WHERE charId=110 AND slot IN(1,106); INSERT INTO inventory(charId,goid,slot,count,purity) VALUES(110,47270,1,1,0)"},Array.Empty<string>(),
        environmentOverrides:new(){["MXO_MOBS"]=""},extraQaMetadata:new(){["DataMinePartial"]=true,["WorldMap"]=7,["RetailConstruct"]=17,["GeneratedArrivalFallbackRetained"]=true},
        readyMessages:new[]{"DataMine PARTIAL: equip Zion book47270 frombag1 into launcher, stand at sourced Zion sender; wait normal180s cycle. Map7/retail17. Existing arrival/generated returner remains OURS. Eight actual source ArchivistProxy47267 views/Use cityexit need native check; no nodes/HL/subway invented. See WORLD-DATAMINE.md."});
}
