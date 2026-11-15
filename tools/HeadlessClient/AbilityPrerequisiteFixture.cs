namespace HeadlessClient;
/// <summary>Level1/EXP0 native load profile; ordinary client emits missing owned parents itself.</summary>
public static class AbilityPrerequisiteFixture
{
    public static readonly IReadOnlyList<ushort> Known=Array.AsReadOnly(new ushort[]{2,17,113,130,217,628});
    public static string ArchiveSql(uint character)=>$"UPDATE char_abilities SET level=1,is_loaded=CASE WHEN ((ability_id>>10)&4095) IN (2,17) THEN 1 ELSE 0 END WHERE char_id={character} AND slot<520";
    public static string CharacterSql(uint character)=>$"UPDATE characters SET level=1,exp=0,y=495,rotation=0 WHERE charId={character}";
}
