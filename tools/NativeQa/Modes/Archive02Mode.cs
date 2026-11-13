using hds;
namespace NativeQa.Modes;
public sealed class Archive02Mode : NativeQaMode
{
    public override string Name=>"archive02";
    public override NativeQaModePlan Plan{get;}=new(new ushort[]{2,17},111600,-6130,Array.Empty<string>(),
        new[]{"UPDATE characters SET alignment=1,district='slums',districtId=1,y=695 WHERE charId=110; DELETE FROM inventory WHERE charId=110 AND slot IN (3,106); INSERT INTO inventory(charId,goid,slot,count,purity) VALUES(110,38533,3,1,0)"},
        new[]{$"UPDATE characters SET level=30,exp={PlayerProgression.ExperienceForLevel(30)} WHERE charId IN (110,6111,6112,6113,6114); UPDATE char_abilities SET level=LEAST(level,30) WHERE char_id IN (110,6111,6112,6113,6114)"},
        environmentOverrides:new(){["MXO_MOBS"]=""},
        extraQaMetadata:new(){["Archive02Partial"]=true,["FixtureLevel"]=30,["WorldMap"]=5,["RetailConstructId"]=16,["StaticKeys"]=77,["AvailableNavSectors"]=109,["HardlineCount"]=0,["MissingExteriorSector"]=108,["MissingExteriorExtentKnown"]=false},
        readyMessages:new[]{"Archive02 PARTIAL: level30 Zion at grounded sender38282; equip owned book38533 from bag3 into program-launcher slot106; wait ordinary180s cycle. Returner38271 returns to exact session origin. Arrival sector108 has missing exterior SID: native geometry UNPROVEN. No hardline/LA or ambient NPC claim. See WORLD-ARCHIVE02.md."});
}
