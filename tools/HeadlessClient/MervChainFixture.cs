namespace HeadlessClient;
public static class MervChainFixture
{
    public static readonly int[] Missions=Enumerable.Range(2101,10).ToArray();
    public static readonly int[] Prerequisites={1201,1204,1203,1205,1206,1207,1208};
    public static string SeedPrerequisites(long character)=>"INSERT IGNORE INTO char_missions(charId,missionId) VALUES "+
        string.Join(",",Prerequisites.Select(id=>$"({character},{id})"));
}
