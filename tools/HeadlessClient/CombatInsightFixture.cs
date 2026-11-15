using hds;
namespace HeadlessClient;

public static class CombatInsightFixture
{
    //49's stored prerequisite77 and the common Awakened path are owned before Margin, not injected buffs.
    public static ushort[] Abilities=>new ushort[]{2,17,49,77,113,217};
    public static ushort InsightSlot=>(ushort)(400+Array.IndexOf(Abilities,(ushort)113));
    public static Dictionary<string,string> Environment=>new(PvpOpponentsFixture.Environment){["MXO_PACKET_LOG"]="1"};
    public static CharacterSeed Seed(string name,bool target)=>new(name)
    {
        X=target?17120:17000,Y=495,Z=2300,Level=50,Experience=(int)PlayerProgression.ExperienceForLevel(50),
        Organization=target?2:1,Health=20000,MaxHealth=20000,InnerStrength=2000,MaxInnerStrength=2000,
        OwnedLoadedAbilities=Abilities.Select(id=>(id,(ushort)50)).ToArray()
    };
}
