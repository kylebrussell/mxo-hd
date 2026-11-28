using System.Text;

namespace HeadlessClient;

public static partial class Scenarios
{
    public const string OriginalBackground = "Saved profile", UpdatedBackground = "Mara trial";
    private const string BackgroundCheck = "qa_background_save";

    public static string BackgroundFixtureSql(uint owner, string ownerHandle) => $"""
        UPDATE characters SET background='{OriginalBackground}' WHERE charId={owner};
        ALTER TABLE characters ADD CONSTRAINT {BackgroundCheck}
            CHECK (handle<>'{ownerHandle}' OR background<>'{UpdatedBackground}');
        """;

    public static Task ReleaseBackgroundFaultAsync(ThrowawayDatabase db) =>
        db.ExecuteAsync($"ALTER TABLE characters DROP CONSTRAINT {BackgroundCheck};");

    public static async Task CheckBackgroundFaultAsync(ThrowawayDatabase db)
    {
        Check.Equal(1L, await db.LongAsync("SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS " +
            $"WHERE CONSTRAINT_SCHEMA=DATABASE() AND TABLE_NAME='characters' AND CONSTRAINT_NAME='{BackgroundCheck}'"),
            "The disposable background CHECK is armed");
    }

    public static Task<string> SavedBackgroundAsync(ThrowawayDatabase db, uint owner) =>
        db.ScalarAsync($"SELECT background FROM characters WHERE charId={owner}");

    private static async Task BackgroundSaveResult(ScenarioContext context)
    {
        var owner = await context.AddPlayerAsync(new CharacterSeed("BackgroundOwner"));
        await context.Database.ExecuteAsync(BackgroundFixtureSql(owner.CharId, owner.Handle));
        await owner.EnterWorldAsync();
        await ProveBackgroundSaveAsync(owner, context.Database);

        const string NarrowText = "Mara's caf\u00e9";
        await SaveBackgroundAsync(owner, NarrowText, saved: true);
        await SaveBackgroundAsync(owner, NarrowText, saved: true);
        await CheckBackgroundReadAsync(owner, NarrowText);

        var peer = (await context.AddPlayersInWorldAsync(new CharacterSeed("BackgroundReader")))[0];
        byte[] inspect = WireFormat.FromHex("81 92 00 00 00 00 08 00").Concat(Sized(owner.Handle)).ToArray();
        peer.SendRpc(inspect);
        var publicText = await peer.WaitForRpcAsync("81 98 05 00 01");
        Check.That(publicText.Bytes.SequenceEqual(BackgroundFrame(0x98, 1, NarrowText)),
            "Public profile has the same narrow string");

        await SaveBackgroundAsync(owner, "", saved: true);
        await CheckBackgroundReadAsync(owner, "");
        peer.SendRpc(inspect);
        var publicEmpty = await peer.WaitForRpcAsync("81 98 05 00 01");
        Check.That(publicEmpty.Bytes.SequenceEqual(BackgroundFrame(0x98, 1, "")),
            "Cleared public profile is a sized empty string");
        Check.Equal("", await SavedBackgroundAsync(context.Database, owner.CharId), "Clearing persists the empty profile");
    }

    public static async Task ProveBackgroundSaveAsync(HeadlessPlayer owner, ThrowawayDatabase db)
    {
        await CheckBackgroundReadAsync(owner, OriginalBackground);
        await SaveBackgroundAsync(owner, UpdatedBackground, saved: false);
        Check.Equal(OriginalBackground, await SavedBackgroundAsync(db, owner.CharId), "Refusal keeps the saved text");
        await CheckBackgroundReadAsync(owner, OriginalBackground);

        await ReleaseBackgroundFaultAsync(db);
        foreach (string request in new[] {
            "81 96",                            // missing body
            "81 96 00 00 01",                   // chunked envelope
            "81 96 06 00 00 01 00 00",          // bad pointer
            "81 96 05 00 00 02 00 51",          // missing terminator
            "81 96 05 00 00 03 00 00 51 00",    // embedded null
            "81 96 05 00 00 01 00 00 51"        // trailing data
        })
        {
            owner.SendRpc(request);
            await owner.WaitForRpcAsync("81 97 01 00 00 00");
        }
        await CheckBackgroundReadAsync(owner, OriginalBackground);
        await SaveBackgroundAsync(owner, UpdatedBackground, saved: true);
        await CheckBackgroundReadAsync(owner, UpdatedBackground);
        Check.Equal(UpdatedBackground, await SavedBackgroundAsync(db, owner.CharId), "Successful retry persists the new text");
    }

    private static byte[] BackgroundFrame(byte opcode, byte flag, string text)
    {
        byte[] bytes = Encoding.Latin1.GetBytes(text);
        return new byte[] { 0x81, opcode, 5, 0, flag }.Concat(BitConverter.GetBytes((ushort)(bytes.Length + 1)))
            .Concat(bytes).Append((byte)0).ToArray();
    }

    private static async Task CheckBackgroundReadAsync(HeadlessPlayer owner, string text)
    {
        owner.SendRpc("81 94");
        var reply = await owner.WaitForRpcAsync("81 95 05 00 00");
        Check.That(reply.Bytes.SequenceEqual(BackgroundFrame(0x95, 0, text)), "Editor receives the exact saved background");
    }

    private static async Task SaveBackgroundAsync(HeadlessPlayer owner, string text, bool saved)
    {
        owner.SendRpc(BackgroundFrame(0x96, 0, text));
        await owner.WaitForRpcAsync(saved ? "81 97 00 00 00 00" : "81 97 01 00 00 00");
    }
}
