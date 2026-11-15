using MxoHd.PacketResearch;
namespace HeadlessClient;

public static class SneakScaledGrantFixture
{
    public const string Name="sneak-scaled-grant",SourcePin="MXO_SNEAK_GRANT_SOURCE_SHA256";
    public const ushort Parent=151,Child=1111,NativeLevel=17;
    public static ushort[] Parents=>new ushort[]{2,113,130,143,151,185};
    public static (ushort Ability,ushort Level)[] Loadout(ushort learned)=>Parents.Select(p=>(p,p==Parent?learned:(ushort)50)).ToArray();
    public static ushort ParentSlot=>(ushort)(400+Array.IndexOf(Parents,Parent));
    public static Dictionary<string,string> Environment=>new(){["MXO_DEV_COMMANDS"]="1",["MXO_PACKET_LOG"]="1",["MXO_MOBS"]="test",["MXO_MOB_WANDER"]="0",["MXO_AGGRO"]="0"};
    public static void RequireSource(string? expected,string actual)
    {if(expected==null||expected.Length!=64||expected.Any(c=>!Uri.IsHexDigit(c))||!expected.Equals(actual,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Sneak proof requires exact "+SourcePin);}
    public static bool Base(byte[] rpc,ushort level)=>BaseAttributeRecord.TryDecode(rpc,out var b)&&b is{Id:Child,CsSlot:520}&&b.Value==level;
    public static bool ChildBase(byte[] rpc)=>BaseAttributeRecord.TryDecode(rpc,out var b)&&b is{Id:Child,CsSlot:520};
    public static bool ChildMembership(byte[] rpc)=>ManageBonusRecord.TryDecode(rpc,out var m)&&m is{Type:0x45,SourceId:Parent,TargetId:Child}&&m.Locator==((uint)Child<<8|3);
    public static bool Membership(byte[] rpc,ushort level,bool off=false)=>ManageBonusRecord.TryDecode(rpc,out var m)&&m is{Type:0x45,SourceId:Parent,TargetId:Child,DurationBits:0}&&
        m.Locator==((uint)Child<<8|3)&&m.SignedValue==level&&m.AddRemoveType==(off?1:0);
    public static bool Stealth209(byte[] rpc,ushort level,bool off=false)=>ManageBonusRecord.TryDecode(rpc,out var m)&&m is{Type:0x45,SourceId:Child,TargetId:209,DurationBits:0}&&
        (m.Locator&255)==0&&m.SignedValue==level&&m.AddRemoveType==(off?1:0);
}
