namespace HeadlessClient;
public static class MissionPresentationFixture
{
    public static string PriorMissionsSql(long character) =>
        $"INSERT IGNORE INTO char_missions(charId,missionId) VALUES ({character},1201),({character},1204)";
}
