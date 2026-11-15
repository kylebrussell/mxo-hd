using hds;
using MxoHd.PacketResearch;
namespace HeadlessClient;
public static class ReverseBulldogFixture
{
    public const string SourcePin="MXO_REVERSE_BULLDOG_SOURCE_SHA256";
    public static ushort[] Abilities=>new ushort[]{2,1112,151,1111,388};
    public static IReadOnlyDictionary<string,string> Environment=>new Dictionary<string,string>{["MXO_DEV_COMMANDS"]="0",["MXO_MOBS"]="test",["MXO_MOB_WANDER"]="0",["MXO_AGGRO"]="0",["MXO_PVP_SERVER"]="1",["MXO_PVP_MAX_SAFE_LEVEL"]="0"};
    public static CharacterSeed Seed(string name,bool target=false)=>new(name){X=target?17220:17020,Y=495,Z=2693,Rotation=64,Organization=target?2:1,
        Level=50,Experience=(int)PlayerProgression.ExperienceForLevel(50),Health=10000,MaxHealth=10000,InnerStrength=2000,MaxInnerStrength=2000,
        OwnedLoadedAbilities=(target?new ushort[]{2,17}:Abilities).Select(id=>(id,(ushort)50)).ToArray()};
    public sealed record Modifier(ushort Kind,ushort Record,uint Parent,ushort Target,int Value,bool Off,float Seconds);
    public static IEnumerable<Modifier> Mods(HeadlessPlayer player,int cursor=0)=>player.World.RpcSnapshot().Skip(cursor).Where(r=>r.Bytes.Length==22&&r.Bytes[0]==0x80&&r.Bytes[1]==0xbc).Select(r=>{
        var b=r.Bytes;return new Modifier(BitConverter.ToUInt16(b,2),BitConverter.ToUInt16(b,4),BitConverter.ToUInt32(b,7),BitConverter.ToUInt16(b,11),BitConverter.ToInt32(b,13),b[17]!=0,BitConverter.ToSingle(b,18));});
    public static bool Off(HeadlessPlayer player,Modifier on)=>Mods(player).Any(m=>m.Off&&m.Record==on.Record&&m.Parent==on.Parent&&m.Target==on.Target);
    public static ViewCreation? Current(IEnumerable<ViewCreation> views,string name)=>views.GroupBy(v=>v.ViewId).Select(g=>g.Last()).LastOrDefault(v=>v.Name==name&&v.Goid==12);
    public static double[] Position(ViewCreation view)
    {var bytes=view.Attributes["Position"];Check.Equal(24,bytes.Length,"Actual player XYZ position bytes");return new[]{BitConverter.ToDouble(bytes,0),BitConverter.ToDouble(bytes,8),BitConverter.ToDouble(bytes,16)};}
    public static byte[] Cast(HeadlessPlayer actor,ushort ability,ViewCreation target)
    {var bytes=new AbilityUseRecord(ability,DuelPackets.Handle(target.ViewId,target.SpawnId),0,0,0,0,0x6b,0x6b,0,0).Encode();Check.Equal(43,bytes.Length,"Full native-layout80b9");actor.SendRpc(bytes);return bytes;}
    public static async Task SneakAsync(HeadlessPlayer actor)
    {
        var self=actor.World.CreationSnapshot().Last(v=>v.ViewId==2);int cursor=actor.World.RpcSnapshot().Count;Cast(actor,1111,self);
        await actor.WaitUntilAsync(()=>actor.World.RpcSnapshot().Skip(cursor).Any(r=>Convert.ToHexString(r.Bytes).StartsWith("80BD055704000000000001",StringComparison.Ordinal)),"ordinary Sneak activation",TimeSpan.FromSeconds(10));
    }
    public static void RequirePin(string? expected,string actual)
    {if(expected==null||expected.Length!=64||expected.Any(c=>!Uri.IsHexDigit(c))||!expected.Equals(actual,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Reverse Bull Dog requires exact source pin via "+SourcePin);}
}
