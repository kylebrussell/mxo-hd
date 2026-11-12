using hds;
namespace HeadlessClient;

/// <summary>Shared disposable122 fixture contract; no client, lock or main database operations.</summary>
public static class StealthShieldFixture
{
    public const int CasterOrg=1,TargetOrg=2,InnerStrength=2000;
    public const uint Tool=531;
    public static readonly ushort[] Abilities={2,17,1110,1125,217,1111,1116,1112,49};
    public static CharacterSeed Seed(string name,bool enemy=false)=>new(name){X=24800+(enemy?-226:0),Y=495,Z=-1498+(enemy?-107:0),
        Level=50,Experience=checked((int)PlayerProgression.ExperienceForLevel(50)),Organization=enemy?TargetOrg:CasterOrg,
        Health=enemy?5000:100,MaxHealth=enemy?5000:500,InnerStrength=InnerStrength,MaxInnerStrength=InnerStrength,Items=new[]{(1,Tool,1)}};
    public static async Task PrepareCasterAsync(HeadlessPlayer player)
    {
        player.SendChat("?org 1");
        await player.WaitForViewUpdateAsync(2,WireFormat.ToHex(PlayerViewAttributes.Build((8,new byte[]{CasterOrg}))));
        player.SendChat("?pvpflag off"); // force a subsequent observable on acknowledgement even if login already had the flag
        player.SendChat("?pvpflag on");
        await player.WaitForViewUpdateAsync(2,"02 80 80 80 80 80 80 01 00 00 00 01");
    }
    public static async Task<byte[]> ActivateTargetAsync(HeadlessPlayer player)
    {
        player.SendChat("?org 2");
        await player.WaitForViewUpdateAsync(2,WireFormat.ToHex(PlayerViewAttributes.Build((8,new byte[]{TargetOrg}))));
        player.SendChat("?pvpflag off"); // force a subsequent observable on acknowledgement even if login already had the flag
        player.SendChat("?pvpflag on");
        await player.WaitForViewUpdateAsync(2,"02 80 80 80 80 80 80 01 00 00 00 01");
        player.SendRpc("80 b9 d9 00 02 00");
        var ack=await player.WaitForRpcAsync("80 bc ?? ?? ?? ?? 00 d9 00 00 00 70 02 ?? ?? ?? ?? 00");
        return ack.Bytes;
    }
}
