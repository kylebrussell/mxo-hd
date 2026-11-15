namespace HeadlessClient;
public static class ZionCriticalFixture
{
    public static readonly int[] Missions={101101,101102,101103,101201,101202};
    public static readonly int[] Prerequisites={1201,1204,1203,1205,1206,1207,1208,2301,2302,2303,2304,2305,2306,2307,2308,2309,2310};
    public static string SeedPrerequisites(long character)=>"INSERT IGNORE INTO char_missions(charId,missionId) VALUES "+string.Join(",",Prerequisites.Select(id=>$"({character},{id})"));
}
