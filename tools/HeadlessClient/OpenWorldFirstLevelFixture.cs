using hds;
namespace HeadlessClient;
public static class OpenWorldFirstLevelFixture
{
    public const string SourcePin="MXO_FIRST_LEVEL_SOURCE_SHA256";
    public static double[] Start=>new[]{19403d,-705d,60401d};
    public static double[][] Spawns=>new[]{new[]{19403d,-705d,60701d},new[]{20097d,-705d,61491d}};
    public static Dictionary<string,string> Environment=>new(){["MXO_DEV_COMMANDS"]="0",["MXO_MOBS"]="world",["MXO_PACKET_LOG"]="1"};
    // Only the assisted wire case uses ?tp to the independently checked outdoor approaches.
    // Native QA retains Environment and has no developer commands or difficulty overrides.
    public static Dictionary<string,string> HeadlessEnvironment
    {
        get { var environment=Environment;environment["MXO_DEV_COMMANDS"]="1";return environment; }
    }
    public static CharacterSeed Seed=>new("FirstLevel"){X=19403,Y=-705,Z=60401,Level=1,Experience=0,Info=1000,Health=150,MaxHealth=150,InnerStrength=100,MaxInnerStrength=100,OwnedLoadedAbilities=new[]{((ushort)2,(ushort)1)}};
    public static void RequireSource(string? pin,string actual)
    {if(pin==null||pin.Length!=64||pin.Any(c=>!Uri.IsHexDigit(c))||!pin.Equals(actual,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("First-level proof requires exact "+SourcePin);}
    // Connected component only; the assisted point move does not prove a straight native walking route.
    public static NavPoint Approach(double[] target)
    {
        if(target.Length!=3||target.Any(v=>!double.IsFinite(v))||
            !NavMesh.Locate(1,target[0],target[1],target[2],out var at)||at.indoors||at.floorY!=-800||
            !NavMesh.Project(1,target[0],target[1],target[2]-100,out var approach,
                maxDistance:20,reachableFrom:Start,requireDirect:false)||approach.location.indoors||
            approach.location.floorY!=at.floorY||Math.Abs(approach.y-target[1])>.01||
            !CombatHandler.InMeleeReach((float)(Math.Sqrt(Math.Pow(approach.x-target[0],2)+Math.Pow(approach.y-target[1],2)+Math.Pow(approach.z-target[2],2))/100)))
            throw new InvalidDataException("Current outdoor target lacks bounded grounded melee approach");
        return approach;
    }
    // Independent literal retail80e6 gain schema, not a production Calculate oracle.
    public static bool FightingGain(byte[] bytes,uint gain)=>bytes.Length==10&&bytes[0]==0x80&&bytes[1]==0xe6&&BitConverter.ToUInt32(bytes,2)==gain&&bytes.AsSpan(6).SequenceEqual(new byte[]{1,0,0,0});
    public static bool OwnLevelTwo(byte[] section)
    {
        byte[] prefix=Convert.FromHexString("020002808083C80002D4");
        for(int i=0;i+22<=section.Length;i++)if(section.AsSpan(i,prefix.Length).SequenceEqual(prefix)&&BitConverter.ToUInt16(section,i+10)==103&&section[i+16]==0x30&&BitConverter.ToUInt32(section,i+17)==0x280005b9)return true;
        return false;
    }
    public static uint LootInfo(byte[] bytes)
    {if(bytes.Length<12||bytes[0]!=0x81||bytes[1]!=0x19)throw new InvalidDataException("Malformed ordinary loot window");return BitConverter.ToUInt32(bytes,8);}
}
