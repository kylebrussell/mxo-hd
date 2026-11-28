using System.Text;

namespace HeadlessClient;

public static partial class Scenarios
{
    public static readonly string SeededLongBackground = string.Concat(Enumerable.Repeat("Mara transmission verified. ", 22)) + "END";
    public static readonly string EditedLongBackground = SeededLongBackground + "!";
    private const int StockBackgroundChunkSize = 300;
    private const string CommitLongBackground = "81 96 00 00 01";

    public static string ChunkedBackgroundFixtureSql(uint owner) =>
        $"UPDATE characters SET background='{SeededLongBackground}' WHERE charId={owner};";

    private static async Task BackgroundChunkedSave(ScenarioContext context)
    {
        var owner = await context.AddPlayerAsync(new CharacterSeed("ChunkedBackgroundOwner"));
        await context.Database.ExecuteAsync(ChunkedBackgroundFixtureSql(owner.CharId));
        await owner.EnterWorldAsync();
        await ProveChunkedBackgroundAsync(owner, context.Database);
    }

    public static async Task ProveChunkedBackgroundAsync(HeadlessPlayer owner, ThrowawayDatabase db)
    {
        await CheckBackgroundReadAsync(owner, SeededLongBackground);
        byte[] edited = Encoding.Latin1.GetBytes(EditedLongBackground);

        async Task RefuseIncompleteAsync()
        {
            owner.SendRpc(CommitLongBackground);
            await owner.WaitForRpcAsync("81 97 01 00 00 00");
            Check.Equal(SeededLongBackground, await SavedBackgroundAsync(db, owner.CharId),
                "Refusal leaves the complete original background");
        }

        owner.SendRpc(LongBackgroundHeader(edited.Length));
        owner.SendRpc(LongBackgroundFragment(edited[..StockBackgroundChunkSize]));
        await RefuseIncompleteAsync();

        owner.SendRpc(LongBackgroundHeader(1025));
        SendLongBackgroundFragments(owner, edited);
        await RefuseIncompleteAsync();

        owner.SendRpc(LongBackgroundHeader(edited.Length));
        byte[] wrongOffset = LongBackgroundFragment(edited[..StockBackgroundChunkSize]);
        wrongOffset[2] = 5;
        owner.SendRpc(wrongOffset);
        SendLongBackgroundFragments(owner, edited);
        await RefuseIncompleteAsync();

        owner.SendRpc(LongBackgroundHeader(edited.Length));
        SendLongBackgroundFragments(owner, edited);
        await CheckBackgroundReadAsync(owner, SeededLongBackground); // another request cancels the pending transfer
        await RefuseIncompleteAsync();

        owner.SendRpc(LongBackgroundHeader(edited.Length));
        SendLongBackgroundFragments(owner, edited);
        owner.SendRpc(LongBackgroundFragment(new byte[] { (byte)'!' }));
        await RefuseIncompleteAsync();

        owner.SendRpc(LongBackgroundHeader(edited.Length));
        SendLongBackgroundFragments(owner, edited);
        owner.SendRpc(CommitLongBackground);
        await owner.WaitForRpcAsync("81 97 00 00 00 00");
        await CheckBackgroundReadAsync(owner, EditedLongBackground);
        Check.Equal(EditedLongBackground, await SavedBackgroundAsync(db, owner.CharId),
            "Every byte of the long stock edit is saved");

        owner.SendRpc(CommitLongBackground);
        await owner.WaitForRpcAsync("81 97 01 00 00 00");
        await CheckBackgroundReadAsync(owner, EditedLongBackground);
    }

    private static byte[] LongBackgroundHeader(int count) => new byte[] { 0x81, 0xab }
        .Concat(BitConverter.GetBytes((uint)count)).Concat(BitConverter.GetBytes((uint)StockBackgroundChunkSize))
        .Concat(new byte[] { 0, 0, 0 }).ToArray();

    private static byte[] LongBackgroundFragment(byte[] bytes) => new byte[] { 0x81, 0xac, 4, 0 }
        .Concat(BitConverter.GetBytes((ushort)bytes.Length)).Concat(bytes).ToArray();

    private static void SendLongBackgroundFragments(HeadlessPlayer owner, byte[] bytes)
    {
        foreach (byte[] fragment in bytes.Chunk(StockBackgroundChunkSize))
            owner.SendRpc(LongBackgroundFragment(fragment));
    }
}
