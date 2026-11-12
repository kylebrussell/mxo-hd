namespace HeadlessClient;
public static class MachinesChainFixture
{
    public static readonly int[] Missions={2201,2202,2203,2204,2205,2206,2207,2208,2209,2210};
    public static readonly int[] Prerequisites={1201,1204,1203,1205,1206,1207,1208};
    public static string SeedPrerequisites(long character)=>"INSERT IGNORE INTO char_missions(charId,missionId) VALUES "+
        string.Join(",",Prerequisites.Select(id=>$"({character},{id})"));
}
