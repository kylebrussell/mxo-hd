namespace HeadlessClient;

public static partial class Scenarios
{
    public const string IgnoreSaveAdd = "QAFriend", IgnoreSaveRemove = "QAOther";
    public const string IgnoreSaveConstraint = "qa_ignore_add", IgnoreSavePin = "qa_ignore_delete_pin";

    public static string IgnoreSaveFixtureSql(uint owner) => $"""
        DELETE FROM ignorelist WHERE charId={owner};
        INSERT INTO ignorelist(charId,handle) VALUES ({owner},'{IgnoreSaveRemove}');
        ALTER TABLE ignorelist ADD CONSTRAINT {IgnoreSaveConstraint} CHECK (charId<>{owner} OR handle<>'{IgnoreSaveAdd}');
        CREATE TABLE {IgnoreSavePin} (
            charId INT UNSIGNED NOT NULL, handle VARCHAR(64) NOT NULL,
            PRIMARY KEY (charId,handle),
            FOREIGN KEY (charId,handle) REFERENCES ignorelist(charId,handle) ON DELETE RESTRICT
        ) ENGINE=InnoDB DEFAULT CHARSET=latin1;
        INSERT INTO {IgnoreSavePin}(charId,handle) VALUES ({owner},'{IgnoreSaveRemove}');
        """;

    public static async Task ReleaseIgnoreSaveFaultsAsync(ThrowawayDatabase db)
    {
        await db.ExecuteAsync($"ALTER TABLE ignorelist DROP CONSTRAINT {IgnoreSaveConstraint}; DROP TABLE {IgnoreSavePin};");
    }

    private static async Task IgnoreListWriteRefusal(ScenarioContext context)
    {
        var owner = await context.AddPlayerAsync(new CharacterSeed("IgnoreOwner"));
        await context.Database.ExecuteAsync(IgnoreSaveFixtureSql(owner.CharId));
        await owner.EnterWorldAsync();
        var helpers = await context.AddPlayersInWorldAsync(
            new CharacterSeed(IgnoreSaveAdd) { X = 17120 }, new CharacterSeed(IgnoreSaveRemove) { X = 17220 });
        await ProveIgnoreSaveAsync(owner, helpers[0], helpers[1], context.Database);
    }

    public static async Task CheckIgnoreSaveRowsAsync(ThrowawayDatabase db, uint owner, bool committed)
    {
        Task<long> Row(string handle) =>
            db.LongAsync($"SELECT COUNT(*) FROM ignorelist WHERE charId={owner} AND handle='{handle}'");
        Check.Equal(committed ? 1L : 0L, await Row(IgnoreSaveAdd), "Saved QAFriend ignore reflects the successful add");
        Check.Equal(committed ? 0L : 1L, await Row(IgnoreSaveRemove), "Saved QAOther ignore reflects the successful removal");
    }

    public static async Task ProveIgnoreSaveAsync(HeadlessPlayer owner, HeadlessPlayer friend, HeadlessPlayer other, ThrowawayDatabase db)
    {
        string world = await db.ScalarAsync("SELECT name FROM worlds ORDER BY worldId LIMIT 1");
        string prefix = $"SOE+MXO+{world}+";
        var loaded = await owner.WaitForRpcAsync("80 e1 08 00 3d 00 00 8e");
        Check.Equal(prefix + IgnoreSaveRemove, StringAtOffset(loaded.Bytes, 2), "QAOther is ignored at login");
        byte[] Edit(byte op, string handle) => new byte[] { 0x80, op, 4, 0 }.Concat(Sized(prefix + handle)).ToArray();

        owner.SendRpc(Edit(0xe0, IgnoreSaveAdd));
        var addRefused = await owner.WaitForRpcAsync("80 e1 08 00 68 00 00 0e");
        Check.Equal(prefix + IgnoreSaveAdd, StringAtOffset(addRefused.Bytes, 2), "Add failure names the requested player");
        owner.SendRpc(Edit(0xe2, IgnoreSaveRemove));
        var removeRefused = await owner.WaitForRpcAsync("80 e3 08 00 68 00 00 0e");
        Check.Equal(prefix + IgnoreSaveRemove, StringAtOffset(removeRefused.Bytes, 2), "Remove failure names the requested player");
        await CheckIgnoreSaveRowsAsync(db, owner.CharId, committed: false);
        await CheckIgnoreSaveTellsAsync(friend, other, owner.Handle, db, committed: false);
        await owner.WaitForTextAsync("Before retry from QAFriend");
        Check.That(!owner.World.RpcSnapshot().Any(r => System.Text.Encoding.UTF8.GetString(r.Bytes).Contains("Before retry from QAOther")),
            "The refused removal still blocks QAOther");

        await ReleaseIgnoreSaveFaultsAsync(db);
        owner.SendRpc(Edit(0xe0, IgnoreSaveAdd));
        await owner.WaitForRpcAsync("80 e1 08 00 84 00 00 8e");
        owner.SendRpc(Edit(0xe0, IgnoreSaveAdd));
        await owner.WaitForRpcAsync("80 e1 08 00 84 00 00 8e");
        owner.SendRpc(Edit(0xe2, IgnoreSaveRemove));
        await owner.WaitForRpcAsync("80 e3 08 00 84 00 00 8e");
        await CheckIgnoreSaveRowsAsync(db, owner.CharId, committed: true);
        await CheckIgnoreSaveTellsAsync(friend, other, owner.Handle, db, committed: true);
        await owner.WaitForTextAsync("After retry from QAOther");
        Check.That(!owner.World.RpcSnapshot().Any(r => System.Text.Encoding.UTF8.GetString(r.Bytes).Contains("After retry from QAFriend")),
            "The committed add blocks QAFriend");
    }

    public static async Task CheckIgnoreSaveTellsAsync(HeadlessPlayer friend, HeadlessPlayer other, string owner,
        ThrowawayDatabase db, bool committed)
    {
        string world = await db.ScalarAsync("SELECT name FROM worlds ORDER BY worldId LIMIT 1");
        byte[] address = Sized($"SOE+MXO+{world}+{owner}");
        string stage = committed ? "After" : "Before";
        byte counter = committed ? (byte)0x52 : (byte)0x51;

        async Task Tell(HeadlessPlayer sender, bool blocked)
        {
            sender.SkipReceived();
            byte[] body = Sized($"{stage} retry from {sender.Handle}");
            sender.SendRpc(new byte[] { 0x29 }.Concat(BitConverter.GetBytes((ushort)7))
                .Concat(BitConverter.GetBytes((ushort)(7 + address.Length))).Concat(new byte[] { 0, counter })
                .Concat(address).Concat(body).ToArray());
            await sender.WaitForRpcAsync($"2c {counter:x2} {(blocked ? "54 00 00 0e" : "84 00 00 8e")}");
        }

        await Tell(friend, blocked: committed);
        await Tell(other, blocked: !committed);
    }
}
