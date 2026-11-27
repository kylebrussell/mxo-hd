using HeadlessClient;
using hds;

namespace NativeQa.Modes;

public sealed class DecompileFragmentRecoveryMode : NativeQaMode
{
    private const ushort Decompile = 276;
    private const uint Shotgun = 696;
    private const int Rank = 18, X = 17000, Y = 495, Z = 2300;

    public override string Name => "decompile-fragment-recovery";
    public override NativeQaModePlan Plan { get; } = new(
        StockAbilityProfile.Profile(2, 17, 37, Decompile, 277).Order().ToArray(), X, Z,
        Array.Empty<string>(), new[]
        {
            $"UPDATE characters SET y={Y},rotation=128,cash=1000 WHERE charId=110",
            "DELETE FROM inventory WHERE charId=110 AND slot BETWEEN 1 AND 96",
            $"INSERT INTO inventory(charId,goid,slot,count,purity) VALUES (110,{Crafting.DecompilerTool},3,1,0)," +
                $"(110,{Shotgun},5,1,0),(110,{Shotgun},6,1,0),(110,2435,7,2,0)",
            "CREATE TABLE IF NOT EXISTS char_recipes (charId INT UNSIGNED NOT NULL, goid BIGINT UNSIGNED NOT NULL, PRIMARY KEY(charId,goid))",
            $"DELETE FROM char_recipes WHERE charId=110 AND goid={Shotgun}"
        }, new[]
        {
            $"UPDATE char_abilities SET level={Rank} WHERE char_id=110 AND ability_id={AbilityMessages.AbilityGameObject(Decompile)}"
        },
        hotbarAbilities: new[] { Decompile },
        hotbarInventory: new AbilitySweepHotbar.InventoryShortcut[] { new(3, Crafting.DecompilerTool, "Item Decompiler") },
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        readyMessages: new[]
        {
            "Mara Central (17000,495,2300), no teleport. Open Inventory (I) and hotbar page 2. Use slot 1 Decompile Items, then click the Shotgun in bag slot 5. If the ability does not arm the cursor, use the slot 2 Item Decompiler shortcut. Without a seeded hotbar, drag Decompile Items from Actions or right-click bag 3 > Add Item to Hotbar.",
            "First Shotgun: confirm decompile if prompted. Bag 5 empties and its recipe is learned (81 30 then 81 2f 01). The Class Routine stack in bag 7 stays at 2.",
            "Wait for the 10 s reuse. Repeat on the Shotgun in bag 6. Expect its removal, the Class Routine stack 2 -> 3 in bag 7, and one fragment each in bag 1/2/4 (2444/2449/2451). Expect 'Recovered 4 code fragments' in chat, 81 2f 01, and no second recipe notification. Info remains 1,000.",
            "The fixture's +18 Decompile skill makes these four rolls succeed even at 0. There are no forced rolls or live QA commands."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var db = context.OwnedDatabase ?? throw new InvalidOperationException("Decompile QA requires a disposable database.");
        string data = Path.Combine(context.RepositoryRoot ?? throw new InvalidOperationException("Missing world data path."), "data");
        NavMesh.UseDirectory(Path.Combine(data, "navmesh"));
        Check.That(NavMesh.Locate(1, X, Y, Z, out _), "CSR's Mara Central stand is on NAV");
        Check.That(!CraftingHandler.KnownRecipes.Contains(Shotgun) && await Learned() == 0,
            "The Shotgun recipe starts unknown");
        Check.Equal(2L, await Sources(), "Both Shotgun inputs are in the bag");
        Check.Equal(2L, await Fragments(), "The Class Routine stack starts at two");
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, actor);
        actor.SendRpc("81 31 14 01 00 00 05 03 00");
        await actor.WaitForRpcAsync("5f 05 00 01");
        await actor.WaitForRpcAsync("81 30 b8 02 00 00 02");
        await actor.WaitForRpcAsync("81 2f 01 05 b8 02 00 00");
        Check.Equal(1L, await Learned(), "Only the newly learned recipe is persisted");
        Check.Equal(2L, await Fragments(), "Learning the recipe grants no fragments");
        await actor.WaitForRpcAsync("80 bd 01 14 01 04 00 00 20 41 00");

        actor.SendRpc("81 31 14 01 00 00 06 03 01");
        await actor.WaitForRpcAsync("5f 06 00 01");
        await actor.WaitForRpcAsync("5e 01 00 8c 09 00 00 00 00 00 00 01");
        await actor.WaitForRpcAsync("5e 02 00 91 09 00 00 00 00 00 00 01");
        await actor.WaitForRpcAsync("5e 04 00 93 09 00 00 00 00 00 00 01");
        await actor.WaitForRpcAsync("69 01 07 00 00 00 08 00 00 00");
        await actor.WaitForRpcAsync("81 2f 01 06 b8 02 00 00");
        await actor.WaitForTextAsync("Recovered 4 code fragments");
        Check.Equal(0L, await Sources(), "Both source items were consumed once");
        Check.Equal(6L, await Fragments(), "The four recovered fragments are durable");
        Check.Equal(1, actor.Received("81 30 b8 02 00 00").Count, "Only the first decompile learns the recipe");
        Check.Equal(1000L, await db.LongAsync("SELECT cash FROM characters WHERE charId=110"), "Decompile costs no Info");
        Console.WriteLine("DECOMPILE PASS: unknown recipe learned, then 2435/2444/2449/2451 recovered into the bag.");
        return Array.Empty<byte>();

        Task<long> Sources() => db.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId=110 AND goid={Shotgun} AND slot IN (5,6)");
        Task<long> Learned() => db.LongAsync($"SELECT COUNT(*) FROM char_recipes WHERE charId=110 AND goid={Shotgun}");
        Task<long> Fragments() => db.LongAsync("SELECT COALESCE(SUM(count),0) FROM inventory WHERE charId=110 " +
            "AND goid IN (2435,2444,2449,2451)");
    }
}
