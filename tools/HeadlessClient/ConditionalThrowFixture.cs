using hds;
namespace HeadlessClient;
public static class ConditionalThrowFixture
{
    public static ushort[] Abilities=>new ushort[]{2,17,1112,279,282};
    public static CharacterSeed Seed(string handle,double offset,int organization)=>new(handle)
    {
        X=24699+offset,Y=495,Z=-1498,Level=50,Experience=(int)PlayerProgression.ExperienceForLevel(50),Organization=organization,
        Health=5000,MaxHealth=5000,InnerStrength=2000,MaxInnerStrength=2000,
        OwnedLoadedAbilities=Abilities.Select(a=>(a,(ushort)50)).ToArray()
    };
    public static Dictionary<string,string> Environment=>new(){["MXO_DEV_COMMANDS"]="1",["MXO_MOBS"]="test",["MXO_MOB_WANDER"]="0",["MXO_AGGRO"]="0",
        ["MXO_CONDITIONAL_THROW_QA"]="1"};
}
