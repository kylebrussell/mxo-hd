namespace HeadlessClient;
/// <summary>Known owned rows only; literal prerequisite/folder closure checked against current stored GOB fields.</summary>
public static class AbilityTrainingFixture
{
    public static readonly IReadOnlyList<(ushort Ability,ushort Level,bool Loaded)> Rows=Array.AsReadOnly(new (ushort,ushort,bool)[]{
        (2, 50, true),
        (17, 50, true),
        (18, 1, false),
        (27, 1, false),
        (36, 1, false),
        (37, 14, false),
        (44, 10, false),
        (45, 50, true),
        (52, 49, true),
        (57, 1, true),
        (77, 1, true),
        (113, 2, false),
        (121, 1, false),
        (123, 1, false),
        (130, 1, false),
        (143, 1, false),
        (151, 25, false),
        (152, 1, false),
        (155, 1, false),
        (165, 5, false),
        (185, 1, false),
        (186, 1, false),
        (215, 5, false),
        (298, 1, false),
        (537, 1, false),
        (538, 1, false),
        (540, 1, false),
        (621, 30, false),
        (1006, 2, true),
        (1008, 2, false),
        (1112, 5, false),
        (1125, 1, false)
    });
    public static ushort Slot(ushort ability)=>(ushort)(300+Rows.Select((r,i)=>(r,i)).Single(p=>p.r.Ability==ability).i);
    public static string Seed(long character)=>"DELETE FROM char_abilities WHERE char_id="+character+" AND slot<520; INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded,ability_name) VALUES "+
        string.Join(",",Rows.Select((r,i)=>$"({character},{300+i},{0x80000000L+((long)r.Ability<<10)},{r.Level},{(r.Loaded?1:0)},'Ability {r.Ability}')"));
}
