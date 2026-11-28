namespace HeadlessClient;

public static partial class Scenarios
{
    public const string BuddySaveAdd = "QAFriend", BuddySaveRemove = "QAOther";
    private const string BuddyAddCheck = "qa_buddy_add", BuddyDeletePin = "qa_buddy_delete_pin";

    public static string BuddySaveFixtureSql(uint owner, uint friend, uint other) => $"""
        DELETE FROM buddylist WHERE charId={owner};
        INSERT INTO buddylist(charId,friendId) VALUES ({owner},{other});
        ALTER TABLE buddylist ADD CONSTRAINT {BuddyAddCheck} CHECK (charId<>{owner} OR friendId<>{friend});
        CREATE TABLE {BuddyDeletePin} (
            id INT NOT NULL PRIMARY KEY,
            FOREIGN KEY (id) REFERENCES buddylist(id) ON DELETE RESTRICT
        ) ENGINE=InnoDB;
        INSERT INTO {BuddyDeletePin}(id) SELECT id FROM buddylist WHERE charId={owner};
        """;

    public static Task ReleaseBuddySaveFaultsAsync(ThrowawayDatabase db) =>
        db.ExecuteAsync($"ALTER TABLE buddylist DROP CONSTRAINT {BuddyAddCheck}; DROP TABLE {BuddyDeletePin};");

    public static async Task CheckBuddySaveFixtureAsync(ThrowawayDatabase db, uint owner)
    {
        await CheckBuddySaveRowsAsync(db, owner, committed: false);
        Check.Equal(1L, await db.LongAsync("SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS " +
            $"WHERE CONSTRAINT_SCHEMA=DATABASE() AND TABLE_NAME='buddylist' AND CONSTRAINT_NAME='{BuddyAddCheck}'"),
            "The fixture Buddy List insert CHECK is armed");
        Check.Equal(1L, await db.LongAsync($"SELECT COUNT(*) FROM {BuddyDeletePin} JOIN buddylist USING (id) WHERE charId={owner}"),
            "The fixture foreign key pins the original Buddy List row");
    }

    public static async Task CheckBuddySaveRowsAsync(ThrowawayDatabase db, uint owner, bool committed)
    {
        string expected = committed ? BuddySaveAdd : BuddySaveRemove;
        Check.Equal(1L, await db.LongAsync($"SELECT COUNT(*) FROM buddylist WHERE charId={owner}"),
            "One saved buddy row after each pair of edits");
        Check.Equal(expected, await db.ScalarAsync("SELECT c.handle FROM buddylist b JOIN characters c ON c.charId=b.friendId " +
            $"WHERE b.charId={owner}"), "The saved buddy is the expected player");
    }

    private static async Task BuddyListWriteRefusal(ScenarioContext context)
    {
        var owner = await context.AddPlayerAsync(new CharacterSeed("BuddyOwner"));
        var helpers = await context.AddPlayersInWorldAsync(new CharacterSeed(BuddySaveAdd) { X = 17120 },
            new CharacterSeed(BuddySaveRemove) { X = 17220 });
        await context.Database.ExecuteAsync(BuddySaveFixtureSql(owner.CharId, helpers[0].CharId, helpers[1].CharId));
        await CheckBuddySaveFixtureAsync(context.Database, owner.CharId);
        await owner.EnterWorldAsync();
        await ProveBuddySaveAsync(owner, context.Database);

        string username = await context.Database.ScalarAsync(
            $"SELECT username FROM users JOIN characters USING (userId) WHERE charId={owner.CharId}");
        await QuitAsync(owner);
        using var server = await ServerInstance.StartAsync(context.RepositoryRoot, context.Database.Name, StillNpcs);
        using var returned = await HeadlessPlayer.LoginAsync(server.Address, username, owner.Handle);
        await returned.EnterWorldAsync();
        var saved = await returned.WaitForRpcAsync("80 d7 08 00 3c 00 00 8e");
        Check.Equal(await BuddyAddressPrefixAsync(context.Database) + BuddySaveAdd, StringAtOffset(saved.Bytes, 2),
            "Relog restores the successfully added buddy");
        Check.Equal(1, returned.Received("80 d7").Count, "Relog does not show the removed buddy");
    }

    private static async Task<string> BuddyAddressPrefixAsync(ThrowawayDatabase db) =>
        $"SOE+MXO+{await db.ScalarAsync("SELECT name FROM worlds ORDER BY worldId LIMIT 1")}+";

    public static async Task ProveBuddySaveAsync(HeadlessPlayer owner, ThrowawayDatabase db)
    {
        string prefix = await BuddyAddressPrefixAsync(db);
        string friend = prefix + BuddySaveAdd, other = prefix + BuddySaveRemove;
        var loaded = await owner.WaitForRpcAsync("80 d7 08 00 3b 00 00 8e");
        Check.Equal(other, StringAtOffset(loaded.Bytes, 2), "QAOther is the active buddy at login");
        await owner.WaitForRpcAsync("80 de");
        byte[] Edit(byte op, string address) => new byte[] { 0x80, op, 4, 0 }.Concat(Sized(address)).ToArray();

        owner.SendRpc(Edit(0xd6, friend));
        var addRefused = await owner.WaitForRpcAsync("80 d7 08 00 68 00 00 0e");
        Check.Equal(friend, StringAtOffset(addRefused.Bytes, 2), "Add refusal names the unsaved buddy");
        owner.SendRpc(Edit(0xda, other));
        var removeRefused = await owner.WaitForRpcAsync("80 db 08 00 68 00 00 0e");
        Check.Equal(other, StringAtOffset(removeRefused.Bytes, 2), "Remove refusal names the retained buddy");
        Check.Equal(1, owner.Received("80 de").Count, "No online notification for the refused add");
        await CheckBuddySaveRowsAsync(db, owner.CharId, committed: false);

        await ReleaseBuddySaveFaultsAsync(db);
        owner.SendRpc(Edit(0xd6, friend));
        var added = await owner.WaitForRpcAsync("80 d7 08 00 84 00 00 8e");
        Check.Equal(friend, StringAtOffset(added.Bytes, 2), "Retry adds QAFriend");
        var online = await owner.WaitForRpcAsync("80 de");
        Check.Equal(friend, StringAtOffset(online.Bytes, 2), "The successful add publishes online presence");
        owner.SendRpc(Edit(0xd6, friend));
        await owner.WaitForRpcAsync("80 d7 08 00 84 00 00 8e");
        owner.SendRpc(Edit(0xda, other));
        var removed = await owner.WaitForRpcAsync("80 db 08 00 84 00 00 8e");
        Check.Equal(other, StringAtOffset(removed.Bytes, 2), "Retry removes QAOther");
        await CheckBuddySaveRowsAsync(db, owner.CharId, committed: true);
    }
}
