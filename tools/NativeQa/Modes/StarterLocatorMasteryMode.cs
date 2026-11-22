using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class StarterLocatorMasteryMode : NativeQaMode
{
    private const int Level = 19, X = 17000, Y = 495, Z = 2300;
    private const ushort Locate = UtilityAbilities.LocateDataNode, Mastery = UtilityAbilities.FindDataNode;
    private static readonly ushort[] Loadout = new ushort[] { 2, 17, Locate, Mastery }
        .SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys.Append(id)).Distinct().Order().ToArray();

    public override string Name => "ability-parity-starter-locator-mastery";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), new[]
    {
        "DELETE FROM inventory WHERE charId=110 AND slot=8",
        "INSERT INTO inventory(charId,goid,slot,count,purity) VALUES(110,423,8,1,0)",
        StarterHackerPassiveFixture.CapRanks
    }, new[]
    {
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Z}," +
            "innerStrC=500,innerStrM=500 WHERE charId=110"
    }, DevCommands: true, hotbarInventory: new AbilitySweepHotbar.InventoryShortcut[] { new(8, 423, "Data Node Goggles") },
        testMobFixture: "martial-player-controls.csv", environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "621", ["NativeCloseableRows"] = 1 },
        readyMessages: new[]
        {
            $"Stay on verified outdoor NAV ({X},{Y},{Z}). Type ?utilityobject 606 to place a node. Use Data Node Goggles from page 2 slot 1. The item is in bag slot 8.",
            "With 621 loaded, the item sends ordinary 215. Expect [ABILITY-COST] ability=215 paid=0 IS=500->500, node FX and a 215 active shortcut. Press again after the marker appears to stop and verify the marker and shortcut clear.",
            "Type ?qaunload 621, leaving 215 loaded, and use the same goggles. Expect the same 215 request and marker with [ABILITY-COST] paid=5 IS=500->495. Toggle it off. Type ?qaload 621 and repeat to confirm paid=0 IS=495->495. No max [VITALS] or [PVP] changes."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        Check.That(NavMesh.Locate(1, X, Y, Z, out var stand) && !stand.indoors && Math.Abs(stand.floorY + 95 - Y) < 1,
            $"Verified outdoors on NAV at ({X},{Y},{Z})");
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        var archive = StarterHackerPassiveFixture.VerifyProfile(actor, Loadout);
        var server = context.ActualServer!;
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        actor.SendChat("?utilityobject 606");
        await actor.WaitForTextAsync("Utility fixture 606 placed.");
        byte[]? node = null;

        await SearchAsync(0, 500, 500);
        await StockAbilityProfile.ChangeLoadoutAsync(actor, archive, new[] { Mastery }, load: false);
        await SearchAsync(5, 500, 495);
        await StockAbilityProfile.ChangeLoadoutAsync(actor, archive, new[] { Mastery }, load: true);
        await SearchAsync(0, 495, 495);
        Check.Equal(0, actor.World.Problems.Count, "Mastery leaves the stock tool request and response framing unchanged");
        return Array.Empty<byte>();

        async Task SearchAsync(int cost, int before, int after)
        {
            int logAt = server.DebugLog().Length;
            int onAt = actor.World.RpcSnapshot().Count;
            actor.SendRpc(AwakenedDataNodeToolsMode.Goggles);
            await StockAbilityProfile.ObserveAsync(actor, onAt,
                frame => frame.Bytes.AsSpan().SequenceEqual(AbilityBuffs.BuildExclusive(Locate, true)), "Stock goggles activate under 215");
            var marker = await StockAbilityProfile.ObserveAsync(actor, onAt,
                frame => BytePattern.Parse("80 cd 00 00 62 04 00 28").IsMatch(frame.Bytes), "The stock tool displays the nearby node");
            node ??= marker.Bytes;
            Check.That(marker.Bytes.AsSpan().SequenceEqual(node), "Changing mastery keeps the same node marker and coordinates");
            Check.That(server.DebugLog()[logAt..].Contains($"ability=215 paid={cost} IS={before}->{after}"), "Loaded mastery supplies the stored cost");
            Check.That(!actor.World.RpcSnapshot().Skip(onAt).Any(frame =>
                frame.Bytes.AsSpan().SequenceEqual(AbilityBuffs.BuildExclusive(Mastery, true))), "The stock tool keeps its native activation ID");

            int offAt = actor.World.RpcSnapshot().Count;
            actor.SendRpc(AwakenedDataNodeToolsMode.Goggles);
            await StockAbilityProfile.ObserveAsync(actor, offAt,
                frame => frame.Bytes.AsSpan().SequenceEqual(AbilityBuffs.BuildExclusive(Locate, false)), "The same tool toggles 215 off");
            await StockAbilityProfile.ObserveAsync(actor, offAt,
                frame => frame.Bytes.AsSpan().SequenceEqual(DataMiningPackets.Remove(0)), "Toggling off clears the node marker");
            await Task.Delay(TimeSpan.FromSeconds(AbilityHandler.DuplicateAbilityWindowSeconds + .1));
        }
    }
}
