namespace HeadlessClient;
public static class MervAfter301207Fixture
{
    public const int NextMission=301214;
    public const string SourcePin="MXO_MERV_AFTER301207_SOURCE_SHA256";
    public static readonly int[] Missions={301208,301209,301210,301211,301212};
    public static readonly int[] Prerequisites=MervPropagandaFixture.Prerequisites.Concat(Enumerable.Range(301202,6)).ToArray();
    // Literal existing authored level50 payouts; never call production Calculate as the test oracle.
    public static readonly IReadOnlyDictionary<int,(uint Xp,uint Info)> Payouts=new Dictionary<int,(uint,uint)>{
        [301208]=(68750,91000),[301209]=(68750,91000),[301210]=(68750,91000),[301211]=(68750,91000),[301212]=(68750,91000)};
    public static CharacterSeed BeforeMargin(CharacterSeed seed)=>BeforeMargin(seed,Missions,Prerequisites,NextMission);
    public static CharacterSeed BeforeMargin(CharacterSeed seed,IReadOnlyList<int> requested,IReadOnlyList<int> prior,int next)=>
        MervFollowupFixture.BeforeMargin(seed,requested,prior,next);
    public static string SeedPrerequisites(long character)
    {
        MervFollowupFixture.ValidateReceipts(Missions,Prerequisites,NextMission);
        return "INSERT IGNORE INTO char_missions(charId,missionId) VALUES "+string.Join(",",Prerequisites.Select(id=>$"({character},{id})"));
    }
    public static void RequireSource(string actual)
    {
        string? required=Environment.GetEnvironmentVariable(SourcePin);
        if(string.IsNullOrWhiteSpace(required)||!string.Equals(required,actual,StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Next Merv case requires an exact granted actual source hash before player login");
    }
}
