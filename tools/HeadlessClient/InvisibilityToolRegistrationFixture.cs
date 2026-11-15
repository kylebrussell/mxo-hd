namespace HeadlessClient;

public static class InvisibilityToolRegistrationFixture
{
    public const byte Slot=40;
    public const string ExpectedSource="MXO_INVISIBILITY_TOOL_EXPECTED_SOURCE";
    public static IReadOnlyDictionary<string,string> Environment => new Dictionary<string,string>{["MXO_MOB_WANDER"]="0",["MXO_MOBS"]="test",["MXO_AGGRO"]="0",["MXO_PACKET_LOG"]="1"};
    public static void RequireSource(string? expected,string actual)
    {
        if(expected is null||expected.Length!=64||!expected.All(Uri.IsHexDigit)||!expected.Equals(actual,StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Exact source SHA256 pin required for invisibility registration proof");
    }
    public static ushort[] Abilities => new ushort[]{2,17,113,130,185,143,1112,151,121,152,1125,123,155,120,292,142,131,175};
    public static CharacterSeed Seed(string name)=>new(name){X=24800,Y=495,Z=-1498,Level=50,
        Experience=(int)hds.PlayerProgression.ExperienceForLevel(50),InnerStrength=2000,MaxInnerStrength=2000,
        Items=new[]{((int)Slot,460u,1)},OwnedLoadedAbilities=Abilities.Select(a=>(a,(ushort)50)).ToArray()};
    public static byte[] ToolRequest()
    {
        // Shared normal tool producer621ecf10: 43 bytes, bag slot at37, ordinary flag0 at38.
        var b=new byte[43];b[0]=0x80;b[1]=0xb9;BitConverter.GetBytes((ushort)1116).CopyTo(b,2);
        BitConverter.GetBytes((ushort)2).CopyTo(b,4);b[36]=1;b[37]=Slot;return b;
    }
}
