using hds;

namespace HeadlessClient;

public static partial class Scenarios
{
    /// <summary>Exit to the Loading Area as a hardline's "Exit to Loading Area" does: 80 fc 01, 80 fe 01, 81 08, 05.</summary>
    private static async Task GoToLoadingAreaAsync(HeadlessPlayer player)
    {
        player.SendRpc("80 fc 01");
        await player.WaitForRpcAsync("80 fd");
        player.SendRpc("80 fe 01");
        await player.WaitForRpcAsync("81 07");
        player.SendRpc("81 08");
        await player.WaitForRpcAsync("81 09");
        player.SendRpc("05");
        await Task.Delay(500);
    }

    /// <summary>
    /// Central storage (docs/CENTRAL-STORAGE.md): ten Code Bits are refused in the world, stored from the Loading Area
    /// (62 03 00 6b 00 01), split and merged inside storage (68/67 with IsCentralStorageItem), still there after a fresh
    /// server and login (margin known abilities), and taken back into the bag (62 &lt;slot&gt; 6b 00 00).
    /// </summary>
    private static async Task CentralStorageStoreRelogRetrieve(ScenarioContext context)
    {
        HeadlessPlayer player = (await context.AddPlayersInWorldAsync(
            new CharacterSeed("Archivist") { Items = new[] { (3, NpcLoot.CodeBit1, 10) } }))[0];
        string Stored(uint goid) => $"SELECT COALESCE(SUM(level + 1), 0) FROM char_abilities WHERE char_id={player.CharId} AND (ability_id & 0xFC000000) = 0x84000000 AND ((ability_id & 0x03FFFFFF) >> 7) = {goid}";

        // In the world the window doesn't exist: refused with LTPS_SPINVALIDINVENTORYTRANSACTION in the upload result
        player.SendRpc("62 03 00 6b 00 01");
        await player.WaitForRpcAsync("81 18 00 00 9c 02 00 0c");
        Check.Equal(0L, await context.Database.LongAsync(Stored(NpcLoot.CodeBit1)), "Nothing stored from the world");

        await GoToLoadingAreaAsync(player);
        player.SkipReceived();

        // Storage slot past the array: LTPS_SPINVALIDCENTRALSTORAGELOCATION
        player.SendRpc("62 03 00 08 02 01");
        await player.WaitForRpcAsync("81 18 00 00 59 02 00 0c");

        player.SendRpc("62 03 00 6b 00 01");
        await player.WaitForRpcAsync($"5f {Le((ushort)3)} 01");
        RpcRecord added = await player.WaitForRpcAsync(r => r.Bytes.Length == 12 && r.Bytes[0] == 0x5e && r.Bytes[11] == 0, "storage 5e");
        ushort slot = BitConverter.ToUInt16(added.Bytes, 1);
        Check.Equal(CodeStorage.Pack(NpcLoot.CodeBit1, 10), BitConverter.ToUInt32(added.Bytes, 7), "The new storage entry's value");
        await player.WaitForRpcAsync("81 18 00 00 00 00 00 00");
        Check.Equal(0L, await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={player.CharId} AND slot=3"), "Bag slot 3 emptied");
        Check.Equal(10L, await context.Database.LongAsync(Stored(NpcLoot.CodeBit1)), "Ten Code Bits stored");

        // 68 names storage slots as bytes (0x620a07ba), so this needs a slot below 0x100, as a new character's is
        Check.That(slot < 0x100, "The storage slot fits the split record's byte");
        {
            // Split four off into the first free storage slot, then merge them back
            player.SendRpc($"68 {slot:x2} 6b 04 00 00 00 01");
            await player.WaitForRpcAsync($"69 00 {Le(slot)} {Le(CodeStorage.Pack(NpcLoot.CodeBit1, 6))} 00 00");
            RpcRecord split = await player.WaitForRpcAsync(r => r.Bytes.Length == 12 && r.Bytes[0] == 0x5e && BitConverter.ToUInt16(r.Bytes, 1) != slot, "split 5e");
            ushort second = BitConverter.ToUInt16(split.Bytes, 1);
            Check.Equal(CodeStorage.Pack(NpcLoot.CodeBit1, 4), BitConverter.ToUInt32(split.Bytes, 7), "The split entry");
            player.SendRpc($"67 {Le(second)} {Le(slot)} 01");
            await player.WaitForRpcAsync($"69 00 {Le(slot)} {Le(CodeStorage.Pack(NpcLoot.CodeBit1, 10))} 00 00");
            await player.WaitForRpcAsync($"5f {Le(second)} 00");
            Check.Equal(10L, await context.Database.LongAsync(Stored(NpcLoot.CodeBit1)), "Still ten after split and merge");
        }

        // A fresh server on the same database: the entry comes with the known abilities (margin 0x07)
        string username = await context.Database.ScalarAsync($"SELECT username FROM users JOIN characters USING (userId) WHERE charId={player.CharId}");
        using ServerInstance reloaded = await ServerInstance.StartAsync(context.RepositoryRoot, context.Database.Name);
        using HeadlessPlayer relogged = await HeadlessPlayer.LoginAsync(reloaded.Address, username, player.Handle);
        byte[] known = relogged.CharacterData[0x07];
        byte[] entry = BitConverter.GetBytes(slot).Concat(BitConverter.GetBytes(CodeStorage.Pack(NpcLoot.CodeBit1, 10))).ToArray();
        Check.That(BytePattern.IndexOf(known, entry, 0) >= 0, "The stored Code Bits are in the known-abilities list at login");
        await relogged.EnterWorldAsync();
        await GoToLoadingAreaAsync(relogged);
        relogged.SkipReceived();

        // An empty storage slot: refused in chat (no client record carries a status for this direction)
        relogged.SendRpc($"62 {Le((ushort)(slot + 1))} 6b 00 00");
        await relogged.WaitForTextAsync("You can't do that");

        relogged.SendRpc($"62 {Le(slot)} 6b 00 00");
        await relogged.WaitForRpcAsync($"5f {Le(slot)} 00");
        RpcRecord back = await relogged.WaitForRpcAsync(r => r.Bytes.Length == 12 && r.Bytes[0] == 0x5e && r.Bytes[11] == 1, "bag 5e");
        Check.Equal(NpcLoot.CodeBit1, BitConverter.ToUInt32(back.Bytes, 3), "The item back in the bag");
        Check.Equal((byte)(9 * 4), back.Bytes[9], "Its amount byte (count - 1) * 4");
        ushort bagSlot = BitConverter.ToUInt16(back.Bytes, 1);
        Check.Equal(10L, await context.Database.LongAsync($"SELECT GREATEST(count, 1) FROM inventory WHERE charId={player.CharId} AND slot={bagSlot}"), "Ten Code Bits in the bag");
        Check.Equal(0L, await context.Database.LongAsync(Stored(NpcLoot.CodeBit1)), "Storage emptied");

        // Store them again and recycle the entry from the window (5d <slot u16> 00 01)
        relogged.SendRpc($"62 {Le(bagSlot)} 6b 00 01");
        RpcRecord again = await relogged.WaitForRpcAsync(r => r.Bytes.Length == 12 && r.Bytes[0] == 0x5e && r.Bytes[11] == 0, "storage 5e again");
        ushort againSlot = BitConverter.ToUInt16(again.Bytes, 1);
        long infoBefore = await context.Database.LongAsync($"SELECT cash FROM characters WHERE charId={player.CharId}");
        relogged.SendRpc($"5d {Le(againSlot)} 00 01");
        await relogged.WaitForRpcAsync("80 e4");
        await relogged.WaitForRpcAsync($"5f {Le(againSlot)} 00");
        Check.Equal(0L, await context.Database.LongAsync(Stored(NpcLoot.CodeBit1)), "Recycled out of storage");
        Check.That(await context.Database.LongAsync($"SELECT cash FROM characters WHERE charId={player.CharId}") >= infoBefore, "Recycling paid");
        Check.Equal(0, relogged.World.Problems.Count, "Readable, ordered packets");
    }
}
