using hds;

namespace HeadlessClient;

public static partial class Scenarios
{
    public static string ClothingEquipmentRsiSql(uint character) =>
        $"DELETE FROM rsivalues WHERE charid={character}; " +
        $"INSERT INTO rsivalues(charid,body,shirt,shirtcolor,pants,pantscolor,hair) VALUES({character},1,1,15,1,12,2);";

    public static string ClothingEquipmentFaultSql(uint character) =>
        "ALTER TABLE rsivalues ADD CONSTRAINT qa_outfit_rsi CHECK " +
        $"(charid <> {character} OR shirtcolor <> 40);";

    private static async Task AtomicClothingEquipment(ScenarioContext context)
    {
        var actor = await context.AddPlayerAsync(new CharacterSeed("ClothingSwapOwner")
        {
            X = 17000, Y = 495, Z = 6400,
            Items = new[] { (3, 48122u, 1), (4, 48123u, 1), (99, 5910u, 1), (102, 6389u, 1) }
        });
        await context.Database.ExecuteAsync(ClothingEquipmentRsiSql(actor.CharId) + ClothingEquipmentFaultSql(actor.CharId));
        await actor.EnterWorldAsync();
        var watcher = (await context.AddPlayersInWorldAsync(
            new CharacterSeed("ClothingWatcher") { X = 17120, Y = 495, Z = 6400 }))[0];
        await ProveAtomicClothingEquipmentAsync(actor, context.Database, watcher);

        string username = await context.Database.ScalarAsync(
            $"SELECT username FROM users JOIN characters USING (userId) WHERE charId={actor.CharId}");
        await QuitAsync(actor);
        // A fresh listener reads the saved outfit without sharing the old character reservation.
        using (var coldServer = await ServerInstance.StartAsync(context.RepositoryRoot, context.Database.Name, StillNpcs))
        using (var returned = await HeadlessPlayer.LoginAsync(coldServer.Address, username, actor.Handle))
        {
            await returned.EnterWorldAsync();
            var ownView = returned.World.CreationSnapshot().Last(view => view.ViewId == 2);
            Check.That(ownView.Attributes["RSIDescription"].SequenceEqual(WireFormat.FromHex(ClothingView(0, 0))),
                "Relog shows the saved unequipped outfit and preserves the rest of the look");
        }

        var full = await context.AddPlayerAsync(new CharacterSeed("FullClothingBag")
        {
            X = 17000, Y = 495, Z = 6400,
            Items = Enumerable.Range(1, 96).Select(slot => (slot, slot == 3 ? 48122u : 512u, 1))
                .Append((99, 5910u, 1)).ToArray()
        });
        await context.Database.ExecuteAsync(ClothingEquipmentRsiSql(full.CharId));
        await full.EnterWorldAsync();
        string state = $"SELECT invId,goid,slot,count,purity FROM inventory WHERE charId={full.CharId} ORDER BY invId";
        string before = await context.Database.ScalarAsync(state);
        full.SkipReceived();
        full.SendRpc("63 03 6b");
        await full.WaitForRpcAsync("65 03 00 03 00 00");
        await full.WaitForTextAsync("Your inventory is full.");
        full.SendRpc("64 63 00");
        await full.WaitForRpcAsync("65 63 00 63 00 00");
        await full.WaitForTextAsync("Your inventory is full.");
        Check.Equal(before, await context.Database.ScalarAsync(state), "Full bag refusals keep every item and its slot");
        Check.Equal("1 15 2", await Outfit(full, context.Database), "Full bag refusals keep the worn shirt and hair");
        Check.Equal(0, full.World.Problems.Count, "Both full-bag refusals decode");
    }

    public static async Task ProveAtomicClothingEquipmentAsync(
        HeadlessPlayer actor, ThrowawayDatabase db, HeadlessPlayer? watcher = null)
    {
        ushort observedId = watcher == null ? (ushort)0 : (await watcher.WaitForViewNamedAsync(actor.Handle)).ViewId;
        string worn = await Row(99), shirt = await Row(3), pants = await Row(102);
        Check.Equal("1 15 2", await Outfit(actor, db), "Fixture starts in the original shirt");
        int viewCursor = actor.World.ViewSnapshot().Count;
        int watcherCursor = watcher?.World.ViewSnapshot().Count ?? 0;

        actor.SkipReceived();
        actor.SendRpc("63 03 6b");
        await actor.WaitForRpcAsync("65 03 00 03 00 00");
        await actor.WaitForTextAsync("Your items were kept.");
        Check.Equal(worn, await Row(99), "The displaced shirt remains in its original worn slot");
        Check.Equal(shirt, await Row(3), "The replacement shirt remains in its original bag slot");
        Check.Equal("", await Row(1), "Rollback keeps the staging slot empty");
        Check.Equal("1 15 2", await Outfit(actor, db), "Rollback preserves the saved appearance");
        Check.Equal(0, actor.Received("65 63 00 01 00 00").Count + actor.Received("65 03 00 63 00 00").Count,
            "Rollback publishes neither transfer");
        Check.That(!actor.World.ViewSnapshot().Skip(viewCursor).Any(
            BytePattern.Parse("02 00 02 80 01 " + ClothingView(5, 40)).Occurs), "No rejected appearance reaches the wearer");
        if (watcher != null)
            Check.That(!watcher.World.ViewSnapshot().Skip(watcherCursor).Any(
                BytePattern.Parse($"{Le(observedId)} 02 08 {ClothingView(5, 40)}").Occurs),
                "No rejected appearance reaches the observer");

        await db.ExecuteAsync("ALTER TABLE rsivalues DROP CHECK qa_outfit_rsi");
        actor.SendRpc("63 03 6b");
        await actor.WaitForRpcAsync("65 63 00 01 00 00");
        await actor.WaitForRpcAsync("65 03 00 63 00 00");
        await SeeOutfit(5, 40);
        Check.Equal(worn, await Row(1), "The original shirt moves into the first free bag slot");
        Check.Equal(shirt, await Row(99), "The identical replacement row is now worn");
        Check.Equal("5 40 2", await Outfit(actor, db), "The model and color commit together without changing hair");

        actor.SendRpc("64 63 01");
        await actor.WaitForRpcAsync("65 63 00 63 00 00");
        await actor.WaitForTextAsync("Empty that bag slot first.");
        Check.Equal(worn, await Row(1), "The occupied unequip destination keeps its shirt");
        Check.Equal(shirt, await Row(99), "Occupied unequip leaves the worn row in place");
        Check.Equal("5 40 2", await Outfit(actor, db), "The refused unequip preserves presentation");

        actor.SendRpc("64 63 00");
        await actor.WaitForRpcAsync("65 63 00 02 00 00");
        await SeeOutfit(0, 0);
        Check.Equal(shirt, await Row(2), "Automatic unequip moves the same shirt into the first empty bag slot");
        Check.Equal("", await Row(99), "No shirt remains worn");
        Check.Equal(pants, await Row(102), "The unrelated worn pants keep their inventory row");
        Check.Equal("0 0 2", await Outfit(actor, db), "Unequip clears the shirt model and color, preserving hair");
        Check.Equal(0, actor.World.Problems.Count + (watcher?.World.Problems.Count ?? 0), "The wardrobe replies decode");

        Task<string> Row(int slot) => db.ScalarAsync(
            $"SELECT CONCAT(invId,':',goid,':',count,':',purity) FROM inventory WHERE charId={actor.CharId} AND slot={slot}");
        async Task SeeOutfit(int shirtModel, int color)
        {
            string packed = ClothingView(shirtModel, color);
            await actor.WaitForViewUpdateAsync(2, "02 80 01 " + packed);
            if (watcher != null) await watcher.WaitForViewUpdateAsync(observedId, "02 08 " + packed);
        }
    }

    private static Task<string> Outfit(HeadlessPlayer actor, ThrowawayDatabase db) =>
        db.ScalarAsync($"SELECT CONCAT(shirt,' ',shirtcolor,' ',hair) FROM rsivalues WHERE charid={actor.CharId}");

    private static string ClothingView(int shirt, int color)
    {
        var values = new int[22];
        values[1] = 1;
        values[4] = shirt;
        values[6] = 1;
        values[10] = 2;
        values[12] = color;
        values[13] = 12;
        return WireFormat.ToHex(PacketsUtils.getRSIBytes(values)) + " 00 00";
    }
}
