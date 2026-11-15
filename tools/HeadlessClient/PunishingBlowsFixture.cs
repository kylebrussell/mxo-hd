using hds;
namespace HeadlessClient;

public static class PunishingBlowsFixture
{
    public static ushort[] Abilities=>new ushort[]{2,17,16};
    public static Dictionary<string,string> Environment=>new()
    {
        ["MXO_DEV_COMMANDS"]="1",["MXO_MOBS"]="test",["MXO_MOB_WANDER"]="0",["MXO_AGGRO"]="0",
        ["MXO_CONDITIONAL_THROW_QA"]="1",["MXO_PUNISHING_BLOWS_QA"]="1"
    };
    public static CharacterSeed Seed(string name,int offset,int organization)=>new(name)
    {
        X=24699+offset,Y=495,Z=-1498,Level=50,Experience=(int)PlayerProgression.ExperienceForLevel(50),
        Organization=organization,Health=20000,MaxHealth=20000,InnerStrength=2000,MaxInnerStrength=2000,
        Items=new[]{(105,692u,1)},OwnedLoadedAbilities=Abilities.Select(a=>(a,(ushort)50)).ToArray()
    };
}
