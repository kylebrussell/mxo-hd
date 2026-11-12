namespace NativeQa.Modes;
public sealed class Archive01Mode : NativeQaMode
{
    public override string Name=>"archive01";
    public override NativeQaModePlan Plan{get;}=new(new ushort[]{2,17},111600,-6130,Array.Empty<string>(),
        new[]{"UPDATE characters SET level=50,alignment=1,district='slums',districtId=1,y=695 WHERE charId=110; DELETE FROM inventory WHERE charId=110 AND slot IN (3,106); INSERT INTO inventory(charId,goid,slot,count,purity) VALUES(110,38534,3,1,0)"},Array.Empty<string>(),
        environmentOverrides:new(){["MXO_MOBS"]=""},
        extraQaMetadata:new(){["Archive01Partial"]=true,["WorldMap"]=4,["RetailConstructId"]=15,["StaticKeys"]=156,["AvailableNavSectors"]=392,["HardlineCount"]=0},
        readyMessages:new[]{"Archive01 PARTIAL: level50 Zion at actual Richland sender38282; owned Zero One book38534 bag3. Equip book in program-launcher slot106, stand on sender, wait its ordinary180s cycle. Use Zion returner38271 at arrival. No hardline/LA route, no NPC visibility claim. See WORLD-ARCHIVE01.md."});
}
