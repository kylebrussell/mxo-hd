using hds;
namespace HeadlessClient;
public static class WithdrawalFixture
{
    public const string Name="ordinary-withdrawal-input",SourcePin="MXO_WITHDRAWAL_SOURCE_SHA256";
    // Complete catalog normal reload closure for Disguise120; all parents fixed before/after.
    public static ushort[] Abilities=>new ushort[]{2,17,113,120,121,123,130,143,151,152,155,185,1112,1125};
    public static ushort DisguiseSlot=>(ushort)(400+Array.IndexOf(Abilities,(ushort)120));
    public static Dictionary<string,string> Environment=>new(PvpOpponentsFixture.Environment){["MXO_PACKET_LOG"]="1"};
    public static CharacterSeed Seed(string handle,bool enemy)=>new(handle){X=enemy?17120:17000,Y=495,Z=2300,Level=50,
        Experience=(int)PlayerProgression.ExperienceForLevel(50),Organization=enemy?2:1,Health=20000,MaxHealth=20000,InnerStrength=2000,MaxInnerStrength=2000,
        OwnedLoadedAbilities=Abilities.Select(a=>(a,(ushort)50)).ToArray()};
    public static bool WithdrawalRound(byte[] rpc)=>rpc.Length==89&&rpc[0]==0x45&&(BitConverter.ToUInt32(rpc,61)&0x200)!=0&&BitConverter.ToInt32(rpc,65)>0;
}
