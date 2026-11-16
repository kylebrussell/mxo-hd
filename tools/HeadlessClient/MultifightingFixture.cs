using hds;
namespace HeadlessClient;
public static class MultifightingFixture
{
    public static ushort[] Abilities=>new ushort[]{2,17,140};
    public const ushort Slot=402;
    public static Dictionary<string,string> Environment=>new(PvpOpponentsFixture.Environment){["MXO_PACKET_LOG"]="1"};
    public static CharacterSeed Seed(string name,int index)=>new(name){X=17000+index*30,Y=495,Z=2300,Level=50,
        Experience=(int)PlayerProgression.ExperienceForLevel(50),Organization=index==0?2:1,Health=20000,MaxHealth=20000,
        InnerStrength=2000,MaxInnerStrength=2000,OwnedLoadedAbilities=Abilities.Select(id=>(id,(ushort)50)).ToArray()};
}
