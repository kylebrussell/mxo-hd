namespace HeadlessClient;

public static class MervPropagandaFixture
{
    public static readonly int[] Missions = { 301202 };
    public static readonly int[] Prerequisites = {
        1201, 1204, 1203, 1205, 1206, 1207, 1208,
        2101, 2102, 2103, 2104, 2105, 2106, 2107, 2108, 2109, 2110,
        301101, 301102, 301103, 301201
    };
    public static string SeedPrerequisites(long character) =>
        "INSERT IGNORE INTO char_missions(charId,missionId) VALUES " +
        string.Join(",", Prerequisites.Select(id => $"({character},{id})"));
}
