using MxoHd.PacketResearch;
namespace HeadlessClient;

public static class ShadowReadyWireFixture
{
    public const string Name="shadow-ready-presentation",SourcePin="MXO_SHADOW_PRESENTATION_SOURCE_SHA256";
    public static ushort[] Parents=>new ushort[]{2,17,113,120,121,123,130,131,142,143,151,152,155,175,185,290,291,292,1112,1125,1128};
    public static Dictionary<string,string> Environment=>new(){["MXO_PACKET_LOG"]="1",["MXO_DEV_COMMANDS"]="0",["MXO_MOB_WANDER"]="0",["MXO_AGGRO"]="0",["MXO_MOBS"]="test"};
    public static void RequireSource(string? expected,string actual)
    {if(expected==null||expected.Length!=64||expected.Any(c=>!Uri.IsHexDigit(c))||!expected.Equals(actual,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Shadow proof requires exact "+SourcePin);}
    public static bool ChildBase(byte[] rpc)=>BaseAttributeRecord.TryDecode(rpc,out var b)&&b is{Id:1117};
    public static bool Base(byte[] rpc)=>rpc.SequenceEqual(Convert.FromHexString("80B25D0400000802"));
    public static bool Member(byte[] rpc,bool off=false)=>ManageBonusRecord.TryDecode(rpc,out var m)&&m is{Type:0x45,SourceId:290,TargetId:1117,DurationBits:0,SignedValue:0}&&m.Locator==((1117u<<8)|3)&&m.AddRemoveType==(off?1:0);
    public static bool AnyMember(byte[] rpc)=>ManageBonusRecord.TryDecode(rpc,out var m)&&m is{Type:0x45,SourceId:290,TargetId:1117};
}
