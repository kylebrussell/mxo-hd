namespace HeadlessClient;
public static class MervFollowupFixture
{
    public const int NextMission=301208;
    public static readonly int[] Missions={301203,301204,301205,301206,301207};
    public static readonly int[] Prerequisites={1201,1204,1203,1205,1206,1207,1208,2101,2102,2103,2104,2105,2106,2107,2108,2109,2110,301101,301102,301103,301201,301202};
    public static void ValidateReceipts(IReadOnlyList<int> requested,IReadOnlyList<int> prior,int next)
    {
        ArgumentNullException.ThrowIfNull(requested);ArgumentNullException.ThrowIfNull(prior);
        if(requested.Count==0||requested.Any(id=>id<=0)||prior.Any(id=>id<=0)||next<=0||
            requested.Distinct().Count()!=requested.Count||prior.Distinct().Count()!=prior.Count||
            requested.Intersect(prior).Any()||requested.Contains(next)||prior.Contains(next))
            throw new ArgumentException("Earlier receipts, requested chain and next offer must be positive, unique and disjoint");
    }
    public static CharacterSeed BeforeMargin(CharacterSeed seed,IReadOnlyList<int> requested,IReadOnlyList<int> prior,int next)
    {
        ValidateReceipts(requested,prior,next);
        if(seed.CompletedMissions?.Count>0)throw new ArgumentException("The profile already has prior receipts");
        return seed with {CompletedMissions=prior.ToArray()};
    }
    public static string SeedPrerequisites(long character)
    {
        ValidateReceipts(Missions,Prerequisites,NextMission);
        return "INSERT IGNORE INTO char_missions(charId,missionId) VALUES "+string.Join(",",Prerequisites.Select(id=>$"({character},{id})"));
    }
}
