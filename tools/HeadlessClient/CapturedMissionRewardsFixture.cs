using hds;
namespace HeadlessClient;
public static class CapturedMissionRewardsFixture
{
    public static (uint Xp,uint Info) Expected(int level)=>level switch {17=>(132000u,59500u),50=>(404250u,175000u),_=>throw new ArgumentOutOfRangeException(nameof(level))};
    public static string ActorSql(int level)
    {
        _=Expected(level);
        return "UPDATE characters SET level="+level+",exp="+PlayerProgression.ExperienceForLevel(level)+",y=495,alignment=2,healthC=5000,healthM=5000,innerStrC=2000,innerStrM=2000 WHERE charId=110";
    }
}
