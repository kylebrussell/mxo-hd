namespace HeadlessClient;

public static partial class Scenarios
{
    public static string WeaponSwapFaultSql(uint character) => "SET SESSION sql_mode=''; " +
        "ALTER TABLE inventory ADD CONSTRAINT qa_equip_second_move CHECK " +
        $"(charId <> {character} OR slot <> 105 OR goid <> 692);";

    private static async Task AtomicWeaponEquipment(ScenarioContext context)
    {
        var player = await context.AddPlayerAsync(new CharacterSeed("WeaponSwapOwner")
        {
            X = 17000, Y = 495, Z = 6400, Items = new[] { (3, 692u, 1), (4, 698u, 1), (105, 2565u, 1) }
        });
        await context.Database.ExecuteAsync(WeaponSwapFaultSql(player.CharId));
        await player.EnterWorldAsync();
        await ProveAtomicWeaponEquipmentAsync(player, context.Database);

        var full = await context.AddPlayerAsync(new CharacterSeed("FullWeaponBag")
        {
            X = 17000, Y = 495, Z = 6400,
            Items = Enumerable.Range(1, 96).Select(slot => (slot, slot == 3 ? 692u : 512u, 1))
                .Append((105, 2565u, 1)).ToArray()
        });
        await full.EnterWorldAsync();
        string rows = $"FROM inventory WHERE charId={full.CharId}";
        string slots = $"SELECT invId,goid,slot,count,purity {rows} ORDER BY invId";
        string before = await context.Database.ScalarAsync(slots);
        Check.Equal(97L, await context.Database.LongAsync($"SELECT COUNT(*) {rows}"), "The full bag fixture has 96 items and a held weapon");
        full.SkipReceived();
        full.SendRpc("63 03 69");
        await full.WaitForRpcAsync("65 03 00 03 00 00");
        await full.WaitForTextAsync("Your inventory is full.");
        full.SendRpc("64 69 00");
        await full.WaitForRpcAsync("65 69 00 69 00 00");
        await full.WaitForTextAsync("Your inventory is full.");
        Check.Equal(before, await context.Database.ScalarAsync(slots), "Neither full-bag refusal changes any inventory row");
        Check.Equal(0L, await context.Database.LongAsync($"SELECT COUNT(*) {rows} AND slot=0"), "Slot zero remains empty");
        Check.Equal(0, full.World.Problems.Count, "Both full-bag refusals decode");
    }

    public static async Task ProveAtomicWeaponEquipmentAsync(HeadlessPlayer player, ThrowawayDatabase db)
    {
        string revolver = await Row(105), rifle = await Row(3);
        Check.That(revolver.Contains(":2565:") && rifle.Contains(":692:"), "The revolver is held and the rifle is in bag 3");
        const string setRifle = "02 80 80 80 80 08 b4 02 00 00";
        const string clearHand = "02 80 80 80 80 08 00 00 00 00";
        int views = player.World.ViewSnapshot().Count;
        int discoveries = player.Received("5e").Count + player.Received("5f").Count;
        player.SendRpc("3f 6b 00 00 00 00 00");
        player.SkipReceived();
        player.SendRpc("63 03 69");
        await player.WaitForRpcAsync("65 03 00 03 00 00");
        await player.WaitForTextAsync("Your items were kept.");
        Check.Equal(revolver, await Row(105), "The first write rolls back to the original hand row");
        Check.Equal(rifle, await Row(3), "The failed second write keeps the original bag row");
        Check.Equal(0L, await Occupied(1), "The temporary destination is empty after rollback");
        Check.Equal(0, player.Received("65 69 00 01 00 00").Count + player.Received("65 03 00 69 00 00").Count,
            "Rollback publishes neither equipment move");
        Check.Equal(discoveries, player.Received("5e").Count + player.Received("5f").Count,
            "Rollback sends no Free Attack discovery refresh");
        Check.That(!HasUpdate(setRifle, views), "Rollback leaves the equipped-item view alone");

        await db.ExecuteAsync("ALTER TABLE inventory DROP CHECK qa_equip_second_move");
        player.SendRpc("63 03 69");
        await player.WaitForRpcAsync("65 69 00 01 00 00");
        await player.WaitForRpcAsync("65 03 00 69 00 00");
        await player.WaitForViewUpdateAsync(2, setRifle);
        Check.Equal(revolver, await Row(1), "The displaced revolver keeps its identity in bag 1");
        Check.Equal(rifle, await Row(105), "The same rifle row is now held");
        Check.Equal(0L, await Occupied(3), "The rifle source is empty");

        views = player.World.ViewSnapshot().Count;
        player.SendRpc("64 69 01");
        await player.WaitForRpcAsync("65 69 00 69 00 00");
        await player.WaitForTextAsync("Empty that bag slot first.");
        Check.Equal(revolver, await Row(1), "The occupied destination stays intact");
        Check.Equal(rifle, await Row(105), "The refusal keeps the rifle held");
        Check.That(!HasUpdate(clearHand, views), "The refusal sends no unequipped view");

        player.SendRpc("64 69 00");
        await player.WaitForRpcAsync("65 69 00 02 00 00");
        await player.WaitForViewUpdateAsync(2, clearHand);
        Check.Equal(rifle, await Row(2), "Automatic unequip keeps the rifle row in the first free slot");
        Check.Equal(0L, await Occupied(105), "The hand slot is empty");
        Check.Equal(0, player.World.Problems.Count, "The equipment replies decode");

        bool HasUpdate(string pattern, int after) =>
            player.World.ViewSnapshot().Skip(after).Any(BytePattern.Parse("02 00 " + pattern).Occurs);
        Task<string> Row(int slot) => db.ScalarAsync(
            $"SELECT CONCAT(invId,':',goid,':',count,':',purity) FROM inventory WHERE charId={player.CharId} AND slot={slot}");
        Task<long> Occupied(int slot) =>
            db.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={player.CharId} AND slot={slot}");
    }
}
