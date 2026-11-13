using hds;
namespace NativeQa.Modes;
public sealed class YukiMode : NativeQaMode
{
    public override string Name=>"yuki";
    public override NativeQaModePlan Plan{get;}=new(new ushort[]{2,17},111600,-6130,Array.Empty<string>(),
        new[]{"UPDATE characters SET alignment=1,district='slums',districtId=1,y=695,rotation=0 WHERE charId=110; DELETE FROM inventory WHERE charId=110 AND slot IN (3,106); INSERT INTO inventory(charId,goid,slot,count,purity) VALUES(110,38535,3,1,0)"},
        new[]{$"UPDATE characters SET level=20,exp={PlayerProgression.ExperienceForLevel(20)} WHERE charId IN (110,6111,6112,6113,6114); UPDATE char_abilities SET level=LEAST(level,20) WHERE char_id IN (110,6111,6112,6113,6114)"},
        environmentOverrides:new(){["MXO_MOBS"]=""},
        extraQaMetadata:new(){["YukiPartial"]=true,["FixtureLevel"]=20,["WorldMap"]=11,["RetailConstructId"]=14,["StaticKeys"]=246,["AvailableNavSectors"]=49,["HardlineCount"]=0,["MissingInteriorSector"]=5,["MissingInteriorExtentKnown"]=false},
        readyMessages:new[]{"Yuki PARTIAL: level20 Zion at grounded sender38282; equip owned book38535 from bag3 into program-launcher slot106; wait ordinary180s cycle. Returner38271 returns to exact session origin. No hardline/LA or ambient NPC claim. See WORLD-YUKI.md."});
}
