namespace HeadlessClient;

public static partial class Scenarios
{
    /// <summary>Any server prompt (81 8f) or FX token (81 42) can carry a callback: the answer (81 90) and the FX's end
    /// (81 91) come back to it, and only once.</summary>
    private static async Task PromptAndFxCallbacks(ScenarioContext context)
    {
        HeadlessPlayer player = (await context.AddPlayersInWorldAsync(new CharacterSeed("Alice")))[0];

        player.SendChat("?ask");
        RpcRecord prompt = await player.WaitForRpcAsync("81 8f");
        uint message = BitConverter.ToUInt32(prompt.Bytes, 12);
        Check.That(message >= 0x10000, "The prompt's Message comes from the callback range: " + message);
        player.SendRpc(new byte[] { 0x81, 0x90 }.Concat(BitConverter.GetBytes(message)).Append((byte)0).ToArray());
        await player.WaitForTextAsync("You answered No.");

        // The same answer again runs nothing
        player.SendRpc(new byte[] { 0x81, 0x90 }.Concat(BitConverter.GetBytes(message)).Append((byte)1).ToArray());
        player.SendChat("?ask");
        RpcRecord again = await player.WaitForRpcAsync("81 8f");
        player.SendRpc(new byte[] { 0x81, 0x90 }.Concat(BitConverter.GetBytes(BitConverter.ToUInt32(again.Bytes, 12))).Append((byte)1).ToArray());
        await player.WaitForTextAsync("You answered Yes.");
        Check.Equal(0, player.Received("").Count(r => Contains(r.Bytes, "You answered Yes.")) - 1, "One answer per prompt");

        player.SendChat("?fxdone 1");
        RpcRecord fx = await player.WaitForRpcAsync("81 42 01 00 00 00");
        uint token = BitConverter.ToUInt32(fx.Bytes, 6);
        player.SendRpc(new byte[] { 0x81, 0x91 }.Concat(BitConverter.GetBytes(token)).ToArray());
        await player.WaitForTextAsync("The FX ended.");
    }
}
