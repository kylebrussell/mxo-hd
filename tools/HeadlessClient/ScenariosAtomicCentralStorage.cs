using hds;

namespace HeadlessClient;

public static partial class Scenarios
{
    private const uint StorageTestBit = 7332;
    public static string CentralStorageUploadFaultSql(uint character) => RejectArchiveCount("qa_archive_upload", character, 10);

    private static string RejectArchiveCount(string constraint, uint character, int count) =>
        $"SET SESSION sql_mode=''; ALTER TABLE char_abilities ADD CONSTRAINT {constraint} " +
        $"CHECK (char_id <> {character} OR (ability_id & 4294967295) <> {unchecked((uint)CodeStorage.BaseValue(StorageTestBit))} OR level <> {count - 1})";

    private static async Task AtomicCentralStorage(ScenarioContext context)
    {
        var player = await context.AddPlayerAsync(new CharacterSeed("ArchiveOwner")
        {
            Items = new[] { (3, StorageTestBit, 10), (4, 7333u, 3) },
            OwnedLoadedAbilities = new[] { ((ushort)2, (ushort)1) }
        });
        // Imported saves can share an unrelated archive slot. Neither row is safe to move.
        await context.Database.ExecuteAsync("INSERT INTO char_abilities (char_id,slot,ability_id,level,is_loaded) VALUES " +
            $"({player.CharId},2,-2147466240,1,0),({player.CharId},2,-2147466240,2,0)");
        await context.Database.ExecuteAsync(CentralStorageUploadFaultSql(player.CharId));
        await player.EnterWorldAsync();
        await ProveAtomicCentralStorageAsync(player, context.Database);
        Check.Equal(2L, await context.Database.LongAsync(
            $"SELECT COUNT(*) FROM char_abilities WHERE char_id={player.CharId} AND slot=2 AND ability_id=-2147466240"),
            "Unrelated ambiguous archive rows are preserved");
    }

    public static async Task ProveAtomicCentralStorageAsync(HeadlessPlayer player, ThrowawayDatabase db)
    {
        const string upload = "62 03 00 6b 00 01";
        const string kept = "Your items were kept.";
        await GoToLoadingAreaAsync(player);

        var before = Cursor();
        player.SendRpc(upload);
        await player.WaitForTextAsync(kept);
        await player.WaitForRpcAsync("81 18 00 00 9c 02 00 0c");
        NoBagOrArchiveMutation(before);
        Check.Equal(10L, await BagCount(), "Failed archive insert restores the entire bag stack");
        Check.Equal(0L, await StoredCount(), "Failed upload stores nothing");

        await db.ExecuteAsync("ALTER TABLE char_abilities DROP CHECK qa_archive_upload");
        player.SendRpc(upload);
        await player.WaitForRpcAsync("5f 03 00 01");
        ushort first = await ArchiveAdd(10);
        await player.WaitForRpcAsync("81 18 00 00 00 00 00 00");
        Check.Equal(0L, await BagCount(), "Committed upload removes the bag stack");
        Check.Equal(10L, await StoredCount(first), "Committed upload stores all ten Code Bits");

        Check.That(first < 256, "Test archive slot fits the stock split byte");
        string split = $"68 {first:x2} 6b 04 00 00 00 01";
        await db.ExecuteAsync(RejectArchiveCount("qa_archive_split", player.CharId, 4));
        before = Cursor();
        player.SendRpc(split);
        await player.WaitForTextAsync(kept);
        NoBagOrArchiveMutation(before);
        Check.Equal(10L, await StoredCount(first), "Failed archive split keeps the original ten");
        Check.Equal(10L, await StoredCount(), "Failed archive split creates no new charges");

        await db.ExecuteAsync("ALTER TABLE char_abilities DROP CHECK qa_archive_split");
        player.SendRpc(split);
        await player.WaitForRpcAsync(ArchiveUpdate(first, 6));
        ushort second = await ArchiveAdd(4);

        string merge = $"67 {Le(second)} {Le(first)} 01";
        await db.ExecuteAsync(RejectArchiveCount("qa_archive_merge", player.CharId, 10));
        before = Cursor();
        player.SendRpc(merge);
        await player.WaitForTextAsync(kept);
        NoBagOrArchiveMutation(before);
        Check.Equal(6L, await StoredCount(first), "Failed archive merge keeps the destination");
        Check.Equal(4L, await StoredCount(second), "Failed archive merge restores the source");

        await db.ExecuteAsync("ALTER TABLE char_abilities DROP CHECK qa_archive_merge");
        player.SendRpc(merge);
        await player.WaitForRpcAsync(ArchiveUpdate(first, 10));
        await player.WaitForRpcAsync($"5f {Le(second)} 00");

        string downloadFault = "SET SESSION sql_mode=''; ALTER TABLE inventory ADD CONSTRAINT qa_archive_download " +
            $"CHECK (charId <> {player.CharId} OR goid <> 7332 OR count <> 10)";
        await db.ExecuteAsync(downloadFault);
        string download = $"62 {Le(first)} 03 00 00";
        before = Cursor();
        player.SendRpc(download);
        await player.WaitForTextAsync(kept);
        NoBagOrArchiveMutation(before);
        Check.Equal(10L, await StoredCount(first), "Failed bag insert restores the archive stack");
        Check.Equal(0L, await BagCount(), "Failed download gives nothing");

        await db.ExecuteAsync("ALTER TABLE inventory DROP CHECK qa_archive_download");
        player.SendRpc(download);
        await player.WaitForRpcAsync($"5f {Le(first)} 00");
        await player.WaitForRpcAsync("5e 03 00 a4 1c 00 00 00 00 24 0c 01");
        Check.Equal(10L, await BagCount(), "Committed download restores all ten Code Bits");
        Check.Equal(0L, await StoredCount(), "Committed download leaves no archive copy");
        Check.Equal(0, player.World.Problems.Count, "Storage records decode");

        int Cursor() => player.World.RpcSnapshot().Count;
        void NoBagOrArchiveMutation(int start) =>
            Check.That(player.World.RpcSnapshot().Skip(start)
                .All(record => record.Bytes[0] != 0x5e && record.Bytes[0] != 0x5f && record.Bytes[0] != 0x69),
                "A failed operation publishes no tentative bag or archive mutation");

        async Task<ushort> ArchiveAdd(int count)
        {
            byte[] value = BitConverter.GetBytes(CodeStorage.Pack(StorageTestBit, count));
            var row = await player.WaitForRpcAsync(record => record.Bytes.Length == 12 && record.Bytes[0] == 0x5e &&
                record.Bytes[11] == 0 && record.Bytes.AsSpan(7, 4).SequenceEqual(value), $"archive add x{count}");
            return BitConverter.ToUInt16(row.Bytes, 1);
        }

        static string ArchiveUpdate(ushort slot, int count) =>
            $"69 00 {Le(slot)} {Le(CodeStorage.Pack(StorageTestBit, count))} 00 00";

        Task<long> BagCount() => db.LongAsync(
            $"SELECT COALESCE(SUM(GREATEST(count,1)),0) FROM inventory WHERE charId={player.CharId} AND goid={StorageTestBit}");
        Task<long> StoredCount(ushort? slot = null) => db.LongAsync(
            $"SELECT COALESCE(SUM(level+1),0) FROM char_abilities WHERE char_id={player.CharId} AND " +
            $"(ability_id & 4294967295)={unchecked((uint)CodeStorage.BaseValue(StorageTestBit))}" +
            (slot.HasValue ? $" AND slot={slot.Value}" : ""));
    }
}
