using hds;
namespace HeadlessClient;
public static class DeflectCodeFixture
{
    public static ushort[] DefenderAbilities=>AbilityPrerequisites.Current.Closure(23).Keys.Concat(new ushort[]{2,17,23}).Distinct().Order().ToArray();
    public static readonly ushort[] CasterAbilities={2,17,1006,315};
    public static ushort Slot=>(ushort)(400+Array.IndexOf(DefenderAbilities,(ushort)23));
    public static Dictionary<string,string> Environment=>new(PvpOpponentsFixture.Environment)
    { ["MXO_DEV_COMMANDS"]="1",["MXO_PACKET_LOG"]="1",["MXO_FREECAST_VIRAL_QA"]="1",["MXO_FREECAST_VIRAL_QA_ATTACK15"]="32766",["MXO_FREECAST_VIRAL_QA_DEFENSE15"]="0" };
    public static CharacterSeed Seed(string name,bool caster)=>new(name)
    {
        X=caster?17120:17000,Y=495,Z=2300,Level=50,Experience=(int)PlayerProgression.ExperienceForLevel(50),Organization=caster?2:1,
        Health=5000,MaxHealth=5000,InnerStrength=2000,MaxInnerStrength=2000,
        OwnedLoadedAbilities=(caster?CasterAbilities:DefenderAbilities).Select(id=>(id,(ushort)50)).ToArray()
    };
    public static string CasterSql(uint character)=>$"DELETE FROM char_abilities WHERE char_id={character} AND slot<520;"+
        string.Join("",CasterAbilities.Select((id,i)=>$"INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES({character},{400+i},{0x80000000L|((long)id<<10)},50,1);"));
    public static bool State(byte[] b,ushort parent,ushort target,bool off,out float seconds)
    {
        seconds=0;
        if(b.Length!=22||b[0]!=0x80||b[1]!=0xbc||b[2]!=(parent==734?1:0x11)||b[3]!=0||b[6]!=0||
            BitConverter.ToUInt32(b,7)!=parent||BitConverter.ToUInt16(b,11)!=target||BitConverter.ToInt32(b,13)!=0||b[17]!=(off?1:0))return false;
        seconds=BitConverter.ToSingle(b,18);return float.IsFinite(seconds)&&seconds>0;
    }
    //An absolute current-cast window, independent of the consuming convenience wait cursor.
    public static byte[]? FreshState(IEnumerable<byte[]> records,int cursor,ushort parent,ushort target)
    {
        if(cursor<0)throw new ArgumentOutOfRangeException(nameof(cursor));
        return records.Skip(cursor).FirstOrDefault(b=>State(b,parent,target,false,out _));
    }
}
