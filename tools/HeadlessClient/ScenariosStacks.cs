using hds;

namespace HeadlessClient;

public static partial class Scenarios
{
    /// <summary>
    /// Ten Code Bits in slot 3: split four off into the first free slot (68 03 6b 04 00 00 00 00), merge them back
    /// (67 &lt;new&gt; 03 00), and a split of more than the stack holds is refused.
    /// </summary>
    private static async Task SplitAndMergeStacks(ScenarioContext context)
    {
        HeadlessPlayer player = (await context.AddPlayersInWorldAsync(
            new CharacterSeed("Stacker") { Items = new[] { (3, NpcLoot.CodeBit1, 10) } }))[0];
        long Count(int slot) => context.Database.LongAsync(
            $"SELECT GREATEST(count, 1) FROM inventory WHERE charId={player.CharId} AND slot={slot}").GetAwaiter().GetResult();

        player.SendRpc("68 03 6b 04 00 00 00 00");
        await player.WaitForRpcAsync($"69 01 {Le(3u)} {Le(5u * 4)}");
        RpcRecord added = await player.WaitForRpcAsync("5e");
        ushort slot = BitConverter.ToUInt16(added.Bytes, 1);
        Check.Equal(NpcLoot.CodeBit1, BitConverter.ToUInt32(added.Bytes, 3), "The new stack's item");
        Check.Equal((byte)(3 * 4), added.Bytes[9], "The new stack's amount byte (count - 1) * 4");
        Check.Equal(6L, Count(3), "Slot 3 in the database after the split");
        Check.Equal(4L, Count(slot), "The new stack in the database");

        player.SendRpc($"67 {Le(slot)} 03 00 00");
        await player.WaitForRpcAsync($"69 01 {Le(3u)} {Le(9u * 4)}");
        await player.WaitForRpcAsync($"5f {Le(slot)} 01");
        Check.Equal(10L, Count(3), "Slot 3 in the database after the merge");
        Check.Equal(0L, await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={player.CharId} AND slot={slot}"), "The merged slot is empty");

        player.SendRpc("68 03 6b 0b 00 00 00 00");
        await player.WaitForTextAsync("You can't do that");
        Check.Equal(10L, Count(3), "Slot 3 after a refused split");
    }
}
