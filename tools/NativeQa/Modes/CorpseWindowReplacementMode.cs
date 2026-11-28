using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class CorpseWindowReplacementMode : NativeQaMode
{
    private const int X = 17000, Y = 495, Z = 6400;
    private const string First = "Loot Test Dummy", Second = "Loot Spare Dummy";

    public override string Name => "corpse-window-replacement";
    public override NativeQaModePlan Plan { get; } = new(new ushort[] { 2, 17 }, X, Z, Array.Empty<string>(),
        new[] { """
            UPDATE characters SET y=495,rotation=64,cash=1000 WHERE charId=110;
            DELETE FROM inventory WHERE charId=110 AND slot BETWEEN 1 AND 96;
            """ }, Array.Empty<string>(), testMobFixture: "corpse-window-replacement.csv",
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0",
            ["MXO_CORPSE_TAKE_ALL_QA"] = "1", ["MXO_MOB_CORPSE_SECONDS"] = "120" },
        readyMessages: new[] {
            "CSR starts on Mara NAV (17000,495,6400), facing two 1 HP dummies. Loot Test Dummy stands at (17200,495,6400), Loot Spare Dummy at (17160,495,6530). No teleport or dev command.",
            "Defeat Loot Test Dummy with right-click > Close Combat. Leave its window open and unlooted at 25 Info plus one Code Bit 1 and one Antibiotic.",
            "With that window still open, defeat Loot Spare Dummy. Expect server 81 1a, client 81 1a, [LOOT] Replacement window opened closeEcho=True, then server 81 19 for 35 Info and two items.",
            "Use Take All on the replacement. Expect 81 20, 1035 Info and one of each item. Close it normally, then use Search on the original Loot Test Dummy. Its 25 Info and both items are still there. Take All for 1060 Info and two of each item."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string data = Path.Combine(context.RepositoryRoot!, "data");
        SectorProperties.Load(Path.Combine(data, "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(data, "navmesh"));
        foreach (var (x, z) in new[] { (X, Z), (17200, 6400), (17160, 6530) })
        {
            Check.That(NavMesh.Locate(1, x, Y, z, out var stand) && !stand.indoors && stand.floorY + 95 == Y,
                $"Open NAV at ({x},{Y},{z})");
            Check.That(NavMesh.HasSightPath(1, X, Y, Z, x, Y, z), $"Sight to ({x},{Y},{z})");
        }
        var db = context.OwnedDatabase!;
        Check.Equal(1000L, await Cash(), "The wallet starts at 1000");
        if (context.StockProbe is not { } player) return Array.Empty<byte>();
        await player.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, player);
        string first = await Handle(First), second = await Handle(Second);

        player.SendRpc($"40 {first} 01 6b");
        await player.WaitForRpcAsync(Window(25), TimeSpan.FromSeconds(30));
        player.SkipReceived();
        int offered = player.Received("81 19").Count;
        player.SendRpc($"40 {second} 01 6b");
        await player.WaitForRpcAsync("81 1a", TimeSpan.FromSeconds(30));
        Check.Equal(offered, player.Received("81 19").Count, "The replacement waits for the stock close echo");
        player.SendRpc("81 1a");
        await player.WaitForRpcAsync(Window(35));
        player.SendRpc("81 1f");
        await player.WaitForRpcAsync("80 e4 0b 04 00 00");
        await player.WaitForRpcAsync("81 20");
        Check.Equal(1035L, await Cash(), "The open window grants 35 Info from the second corpse");
        await Items(1);

        player.SendRpc("81 1a");
        player.SendRpc($"80 c7 {first} 05 00");
        await player.WaitForRpcAsync(Window(25));
        player.SendRpc("81 1f");
        await player.WaitForRpcAsync("80 e4 24 04 00 00");
        await player.WaitForRpcAsync("81 20");
        Check.Equal(1060L, await Cash(), "The first window still grants its original 25 Info");
        await Items(2);
        return Array.Empty<byte>();

        async Task<string> Handle(string name)
        {
            var view = await player.WaitForViewNamedAsync(name);
            Check.Equal((ushort)1, BitConverter.ToUInt16(view.Attributes["MaxHealth"]), $"{name} starts at 1 HP");
            return WireFormat.ToHex(BitConverter.GetBytes(DuelPackets.Handle(view.ViewId, view.SpawnId)));
        }
        Task<long> Cash() => db.LongAsync("SELECT cash FROM characters WHERE charId=110");
        async Task Items(long count)
        {
            foreach (int goid in new[] { 7332, 2399 })
                Check.Equal(count, await db.LongAsync($"SELECT SUM(GREATEST(count,1)) FROM inventory WHERE charId=110 AND goid={goid}"),
                    $"Only the selected loot was added, item {goid}");
        }
        static string Window(byte info) =>
            $"81 19 00 00 00 00 10 00 {info:x2} 00 00 00 00 00 00 00 02 00 00 00 00 00 a4 1c 00 00 00 00 00 00 5f 09 00 00";
    }
}
