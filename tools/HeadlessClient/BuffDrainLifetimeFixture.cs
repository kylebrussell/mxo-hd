using System.Text.RegularExpressions;
namespace HeadlessClient;
public static class BuffDrainLifetimeFixture
{
    public static ushort[] Abilities=>new ushort[]{2,17,16};
    public static Dictionary<string,string> Environment=>new(){["MXO_DEV_COMMANDS"]="0",["MXO_MOBS"]="test",["MXO_MOB_WANDER"]="0",["MXO_AGGRO"]="0"};
    public static bool TryInnerStrength(byte[] bytes,out ushort value)
    {
        value=0;
        if(bytes.Length!=9 || !bytes.AsSpan(0,7).SequenceEqual(Convert.FromHexString("02000280808010")))return false;
        value=BitConverter.ToUInt16(bytes,7);return true;
    }
    public static (int Generation,int Amount,int Remaining)[] Drains(string log,uint character)=>
        Regex.Matches(log,@"\[BUFF-DRAIN\] char"+character+@" ability16 generation(\d+) amount(\d+) remaining(\d+)")
            .Select(m=>(int.Parse(m.Groups[1].Value),int.Parse(m.Groups[2].Value),int.Parse(m.Groups[3].Value))).ToArray();
}
