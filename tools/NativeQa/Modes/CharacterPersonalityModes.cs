using hds;
namespace NativeQa.Modes;
public static class CharacterPersonalityProfile
{
    internal static NativeQaModePlan For(uint trait)=>new(new ushort[]{2,17},17000,2300,
        new[]{"SET @trait_ddl=(SELECT IF(COUNT(*)=0,'ALTER TABLE characters ADD COLUMN trait_id INT UNSIGNED NULL DEFAULT NULL','DO 0') FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='characters' AND COLUMN_NAME='trait_id'); PREPARE trait_statement FROM @trait_ddl; EXECUTE trait_statement; DEALLOCATE PREPARE trait_statement"},
        Array.Empty<string>(),new[]{
            "UPDATE characters SET trait_id="+trait+",level=50,exp="+PlayerProgression.ExperienceForLevel(50)+",y=495,rotation=0 WHERE charId=110",
            "DELETE FROM char_abilities WHERE char_id=110 AND slot IN (598,599,601,602,604); "+string.Join("; ",CharacterAttributes.All.Select(a=>"INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES(110,"+CharacterAttributes.SlotOf(a)+","+CharacterAttributes.RowValueOf(a)+","+CharacterAttributes.BaseValue(a,trait)+",0)"))},
        extraQaMetadata:new(){["CharacterPersonalityQa"]=true,["AssignedTraitId"]=trait,["InitialAttributePoints"]=49},
        readyMessages:new[]{"CSR110 ONLY: trait"+trait+" ("+string.Join("/",CharacterAttributes.All.Select(a=>CharacterAttributes.BaseValue(a,trait)))+") at Level/EXP50,49 points. Open Character > Upgrade > Attributes. Confirm all five shown values; allocate ONE "+(trait==308?"Focus":"Belief")+" point, Apply All, verify shown+1/48 points and unchanged Info. Relog and confirm values/points. This seeded consumer probe does not perform native character creation. See docs/CHARACTER-CREATION-PERSONALITY.md. No dev/GM/RNG."});
}
public sealed class CharacterPersonality308Mode:NativeQaMode
{public override string Name=>"character-personality-308";public override NativeQaModePlan Plan {get;}=CharacterPersonalityProfile.For(308);}
public sealed class CharacterPersonality304Mode:NativeQaMode
{public override string Name=>"character-personality-304";public override NativeQaModePlan Plan {get;}=CharacterPersonalityProfile.For(304);}
