using System.Text;
using hds;
namespace HeadlessClient;

public static class TransmitCodeFixture
{
    public static ushort[] DefenderAbilities=>AbilityPrerequisites.Current.Closure(593).Keys.Concat(new ushort[]{17,593}).Distinct().Order().ToArray();
    public static ushort Slot=>(ushort)(400+Array.IndexOf(DefenderAbilities,(ushort)593));
    public static Dictionary<string,string> Environment=>new(PvpOpponentsFixture.Environment)
    {
        ["MXO_DEV_COMMANDS"]="1",["MXO_PACKET_LOG"]="1",["MXO_TYPED_RESISTANCE_QA"]="1",["MXO_TYPED_RESISTANCE_QA_DAMAGE_DRAW"]="65",
        ["MXO_FREECAST_VIRAL_QA"]="1",["MXO_FREECAST_VIRAL_QA_ATTACK15"]="32766",["MXO_FREECAST_VIRAL_QA_DEFENSE15"]="0"
    };
    public static CharacterSeed Seed(string name,bool caster)=>new(name)
    {
        X=caster?17120:17000,Y=495,Z=2300,Level=50,Experience=(int)PlayerProgression.ExperienceForLevel(50),Organization=caster?2:1,
        Health=20000,MaxHealth=20000,InnerStrength=2000,MaxInnerStrength=2000,
        OwnedLoadedAbilities=(caster?TypedResistanceFixture.Abilities:DefenderAbilities).Select(id=>(id,(ushort)50)).ToArray()
    };
    public static string CasterSql(uint character)=>$"DELETE FROM char_abilities WHERE char_id={character} AND slot<520;"+
        string.Join("",TypedResistanceFixture.Abilities.Select((id,i)=>$"INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES({character},{400+i},{0x80000000L|((long)id<<10)},50,1);"));

    //Complete8167 DamageTarget record, using the documented offsets/counts rather than substring matching.
    public static bool Damage(byte[] b,ushort ability,string caster,string target,out int done,out int resisted)
    {
        done=resisted=0;
        if(b.Length<23||b[0]!=0x81||b[1]!=0x67||BitConverter.ToUInt32(b,6)!=0x7c0000f9||b[10]!=0||
            BitConverter.ToUInt32(b,11)!=0x1000||BitConverter.ToUInt32(b,15)!=(0x80000000u|((uint)ability<<10))||b[21]!=0||b[22]!=7)return false;
        int n1=BitConverter.ToUInt16(b,2),n2=BitConverter.ToUInt16(b,4),values=BitConverter.ToUInt16(b,19);
        bool Name(int at,int end,string expected)=>at>=23&&at+2<=b.Length&&end<=b.Length&&
            BitConverter.ToUInt16(b,at)>0&&at+2+BitConverter.ToUInt16(b,at)==end&&b[end-1]==0&&
            Encoding.ASCII.GetString(b,at+2,BitConverter.ToUInt16(b,at)-1)==expected;
        if(n1!=23||!Name(n1,n2,caster)||!Name(n2,values,target)||values+10!=b.Length||BitConverter.ToUInt16(b,values)!=2)return false;
        done=BitConverter.ToInt32(b,values+2);resisted=BitConverter.ToInt32(b,values+6);return true;
    }
}
