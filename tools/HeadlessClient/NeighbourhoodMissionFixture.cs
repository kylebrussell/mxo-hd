namespace HeadlessClient;
public static class NeighbourhoodMissionFixture
{
    public const int Contact=7,Retrieval=3072,Level=10,SiteSeed=3072;
    public const int X=76015,Y=485,Z=15845;
    public const int Experience=1282500,RewardExperience=4040,RewardInfo=6300;
    public static ushort[] Abilities=>new ushort[]{2,17};
    public static string NativeActorSql=>"UPDATE characters SET level=10,exp=1282500,alignment=1,healthC=2000,healthM=2000,innerStrC=1000,innerStrM=1000,y=485,rotation=128 WHERE charId=110";
    public static IReadOnlyDictionary<string,string> Environment=>new Dictionary<string,string>
    {
        ["MXO_DEV_COMMANDS"]="1",["MXO_MISSION_SITE_QA_SEED"]="3072",["MXO_MOB_WANDER"]="0",["MXO_MOBS"]="test",["MXO_AGGRO"]="0"
    };
}
