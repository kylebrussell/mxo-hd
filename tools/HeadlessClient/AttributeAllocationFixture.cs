using hds;
namespace HeadlessClient;
public static class AttributeAllocationFixture
{
    public static string Seed(uint character)=>$"DELETE FROM char_abilities WHERE char_id={character} AND slot IN (598,599,601,602,604); INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES "+
        string.Join(",",CharacterAttributes.All.Select(a=>$"({character},{CharacterAttributes.SlotOf(a)},{CharacterAttributes.RowValueOf(a)},{CharacterAttributes.BaseValue(a)},0)"));
}
