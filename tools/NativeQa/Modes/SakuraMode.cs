namespace NativeQa.Modes;
public sealed class SakuraMode : NativeQaMode
{
    public override string Name=>"sakura";
    public override NativeQaModePlan Plan{get;}=new(new ushort[]{2,17},111600,-6130,Array.Empty<string>(),
        new[]{"UPDATE characters SET alignment=1,district='slums',districtId=1,y=695 WHERE charId=110; DELETE FROM inventory WHERE charId=110 AND slot IN (3,106); INSERT INTO inventory(charId,goid,slot,count,purity) VALUES(110,38537,3,1,0)"},
        new[]{$"UPDATE characters SET level=35,exp={hds.PlayerProgression.ExperienceForLevel(35)} WHERE charId=110"},
        environmentOverrides:new(){["MXO_MOBS"]=""},
        extraQaMetadata:new(){["SakuraPartial"]=true,["WorldMap"]=8,["RetailConstructId"]=12,["StaticKeys"]=177,["AvailableNavSectors"]=49,["HardlineCount"]=0},
        readyMessages:new[]{"Sakura PARTIAL: level35/XP35 Zion at real Richland sender38282; owned Sakura book38537 bag3. Equip book in program-launcher slot106, stand on sender, wait its ordinary180s cycle. Use Zion returner38271 at arrival. No hardline/LA route or NPC visibility claim. See WORLD-SAKURA.md."});
}
