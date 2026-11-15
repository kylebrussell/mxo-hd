using hds;
namespace HeadlessClient;
public static class InvisibilityDurationFixture
{
    public const string SourcePin="MXO_INVISIBILITY_DURATION_SOURCE_SHA256";
    // Full stored type39 closure of175/290, plus ordinary Self Defense. Hidden1116/1117 are grants.
    public static ushort[] Abilities=>new ushort[]{2,17,113,120,121,123,130,131,142,143,151,152,155,175,185,290,291,292,1112,1125,1128};
    public static IReadOnlyDictionary<string,string> Environment=>new Dictionary<string,string>{["MXO_DEV_COMMANDS"]="0",["MXO_MOBS"]="test",["MXO_MOB_WANDER"]="0",["MXO_AGGRO"]="0",["MXO_PACKET_LOG"]="1"};
    public static CharacterSeed Seed(string name)=>new(name){X=17000,Y=495,Z=2300,Level=50,Experience=(int)PlayerProgression.ExperienceForLevel(50),Organization=1,
        Health=5000,MaxHealth=5000,InnerStrength=2000,MaxInnerStrength=2000,Items=new[]{(40,460u,1)},OwnedLoadedAbilities=Abilities.Select(id=>(id,(ushort)50)).ToArray()};
    public sealed record Modifier(ushort Kind,ushort Record,uint Parent,ushort Target,int Value,bool Off,float Seconds);
    public static Modifier? Read(byte[] b)=>b.Length==22&&b[0]==0x80&&b[1]==0xbc?new(BitConverter.ToUInt16(b,2),BitConverter.ToUInt16(b,4),BitConverter.ToUInt32(b,7),BitConverter.ToUInt16(b,11),BitConverter.ToInt32(b,13),b[17]!=0,BitConverter.ToSingle(b,18)):null;
    public static IEnumerable<Modifier> Mods(HeadlessPlayer p,int cursor=0)=>p.World.RpcSnapshot().Skip(cursor).Select(r=>Read(r.Bytes)).OfType<Modifier>();
    public static bool Off(HeadlessPlayer p,Modifier on,int cursor)=>Mods(p,cursor).Any(m=>m.Off&&m.Record==on.Record&&m.Parent==on.Parent&&m.Target==on.Target);
    public sealed record Cooldown(int RpcIndex,float Seconds,bool Running,string Raw);
    public sealed record Readiness(bool SeenStart,Cooldown? Latest)
    {public bool Ready=>SeenStart&&Latest is {Running:false};}
    public static Readiness CooldownReadiness(IEnumerable<byte[]> rpc)
    {
        bool start=false;Cooldown? latest=null;int index=0;
        foreach(var b in rpc)
        {
            if(b.Length==11&&b[0]==0x80&&b[1]==0xbd&&b[2]==1&&BitConverter.ToUInt16(b,3)==1116&&b[5]==4)
            {
                float seconds=BitConverter.ToSingle(b,6);
                if(!float.IsFinite(seconds)||seconds<0||b[10]>1)throw new InvalidDataException("Malformed current1116 own cooldown record");
                bool running=b[10]!=0;start|=running;latest=new(index,seconds,running,Convert.ToHexString(b));
            }
            index++;
        }
        return new(start,latest);
    }
    public static void RequirePin(string? expected,string actual)
    {if(expected==null||expected.Length!=64||expected.Any(c=>!Uri.IsHexDigit(c))||!expected.Equals(actual,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Invisibility duration requires exact source pin via "+SourcePin);}
}
