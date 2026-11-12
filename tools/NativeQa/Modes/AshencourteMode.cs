namespace NativeQa.Modes;
public sealed class AshencourteMode : NativeQaMode
{
    public override string Name=>"ashencourte";
    public override NativeQaModePlan Plan{get;}=new(new ushort[]{2,17},111600,-6130,Array.Empty<string>(),
        new[]{"UPDATE characters SET level=25,alignment=1,district='slums',districtId=1,y=695 WHERE charId=110; DELETE FROM inventory WHERE charId=110 AND slot IN (3,106); INSERT INTO inventory(charId,goid,slot,count,purity) VALUES(110,38538,3,1,0)"},Array.Empty<string>(),
        environmentOverrides:new(){["MXO_MOBS"]=""},
        extraQaMetadata:new(){["AshencourtePartial"]=true,["WorldMap"]=6,["RetailConstructId"]=11,["StaticKeys"]=223,["AvailableNavSectors"]=86,["HardlineCount"]=0},
        readyMessages:new[]{"Ashencourte PARTIAL: level25 Zion at actual Richland sender38282; owned Ashencourte book38538 bag3. Equip book in program-launcher slot106, stand on sender, wait its ordinary180s cycle. Use Zion returner38271 at arrival. No hardline/LA route. Optional real walk to grounded captured Goof11070/95/3974; unsupported Mushfake remain unverified. See WORLD-ASHENCOURTE.md."});
}
