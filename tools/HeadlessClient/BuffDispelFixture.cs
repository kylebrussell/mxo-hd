using System.Text.Json;
namespace HeadlessClient;

/// <summary>Real wire preparation shared by native bootstrap and disposable headless fixture coverage.</summary>
public static class BuffDispelFixture
{
    public static readonly ushort[] Abilities={2,17,133,261,49,327,42,420};
    private static string Record(ushort ability,ushort target)=>$"80 bc ?? ?? ?? ?? 00 {WireFormat.ToHex(BitConverter.GetBytes((uint)ability))} {WireFormat.ToHex(BitConverter.GetBytes(target))} ?? ?? ?? ?? 00";
    public static async Task<byte[][]> PrepareTargetAsync(HeadlessPlayer target)
    {
        target.SendChat("?org 2");
        target.SendChat("?pvpflag off"); target.SendChat("?pvpflag on");
        await target.WaitForViewUpdateAsync(2,"02 80 80 80 80 80 80 01 00 00 00 01");
        var records=new List<byte[]>();
        foreach(var effect in new[]{(Ability:(ushort)49,Target:(ushort)520),(Ability:(ushort)327,Target:(ushort)172)})
        {
            target.SendRpc($"80 b9 {WireFormat.ToHex(BitConverter.GetBytes(effect.Ability))} 02 00");
            records.Add((await target.WaitForRpcAsync(Record(effect.Ability,effect.Target),TimeSpan.FromSeconds(12))).Bytes);
            await Task.Delay(600);
        }
        target.SendRpc("80 b9 2a 00 02 00");
        records.Add((await target.WaitForRpcAsync("80 bd 05 2a 00 00 00 00 00 00 01",TimeSpan.FromSeconds(12))).Bytes);
        await target.WaitUntilAsync(()=>target.Received(Record(695,522)).Count>0 && target.Received(Record(695,524)).Count>0,
            "Both Combat Aura child records",TimeSpan.FromSeconds(12));
        records.Add(target.Received(Record(695,522)).Last().Bytes);
        records.Add(target.Received(Record(695,524)).Last().Bytes);
        return records.ToArray();
    }
}
