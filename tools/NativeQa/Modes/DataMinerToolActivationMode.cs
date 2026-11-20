using System.Text.Json;
using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class DataMinerToolActivationMode : NativeQaMode
{
    private const ushort Locate = 215, Mastery = 621, Siphon = 622;
    private const int X = 24800, Y = 495, Z = -1498, StandX = X + 1200;

    // Stock 215 tool shortcuts from PR448, bag slots 8 and 1. The replay uses the same
    // slot 8 with the advanced goggles, whose shipped ActivatedAbility is also 215.
    private const string Goggles = "80 b9 d7 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 " +
        "00 00 00 00 64 f1 1a 00 68 49 3b 3b 08 08 00 00 00 00 00";
    private const string Handheld = "80 b9 d7 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 " +
        "00 00 00 00 64 f1 1a 00 68 24 3b 3b 08 01 00 00 00 00 00";

    public override string Name => "ability-parity-data-miner-activation";
    public override NativeQaModePlan Plan { get; } = new(
        new ushort[] { 2, 17, Locate, Mastery, Siphon }.Concat(
            StockAbilityProfile.Profile(2, 17, Locate, Mastery, Siphon).Order()).Distinct().ToArray(),
        X, Z, Array.Empty<string>(),
        new[]
        {
            "DELETE FROM inventory WHERE charId=110 AND slot IN (1,8)",
            "INSERT INTO inventory(charId,goid,slot,count,purity) VALUES(110,422,1,1,0),(110,38208,8,1,0)"
        }, new[] { $"UPDATE characters SET y={Y} WHERE charId=110" },
        DevCommands: true, loadAbilityAncestors: true, hotbarAbilities: new[] { Locate, Mastery, Siphon },
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        extraQaMetadata: new() { ["ScopedStockRows"] = "215,621,622", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            $"Step A. Use ?utilityobject 606 at spawn. Move to ?tp {StandX} {Y} {Z} 0 0 0, 12 metres east. From Actions > Tools add the Advanced Data Node Locator Goggles (bag 8) and the Data Node Locator handheld (bag 1) to two free hotbar slots.",
            "Step B. Use the Advanced Goggles item shortcut. Capture a 215 request with slot 8 and node FX with the marker 12 metres west. While active, ?qaunload 622. Check FX and 215 activation clear. Retry the same item while 622 is unloaded and check refusal. Then ?qaload 622 and confirm the item works again. Toggle it off before the next step.",
            "Step C. Use the handheld item shortcut as the loaded 215 control. After expiry, try the direct 621 and 622 ability shortcuts on page 2. Record any local refusal and whether either sends a request. Their stock tool paths need separate evidence."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var db = context.OwnedDatabase ?? throw new InvalidOperationException("Expected the disposable Data Miner database.");
        Check.Equal(2L, await db.LongAsync(
            "SELECT COUNT(*) FROM inventory WHERE charId=110 AND count=1 AND ((slot=1 AND goid=422) OR (slot=8 AND goid=38208))"),
            "Both locator tools occupy their documented bag slots");
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        var server = context.ActualServer ?? throw new InvalidOperationException("Expected the disposable Data Miner server.");
        var loaded = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Select(row => AbilityCatalog.IdOf(row.Descriptor)).ToHashSet();
        Check.That(Plan.Abilities.All(loaded.Contains), "Both locator abilities, the Siphon gate and their ancestors are loaded");

        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        actor.SendChat("?utilityobject 606");
        await actor.WaitForTextAsync("Utility fixture 606 placed.");
        actor.SendChat($"?tp {StandX} {Y} {Z} 0 0 0");
        await actor.WaitForTextAsync("Teleported to 0/0");

        var proof = new List<string>();
        byte[] on = AbilityBuffs.BuildExclusive(Locate, true);
        byte[] off = AbilityBuffs.BuildExclusive(Locate, false);
        byte[] marker = DataMiningPackets.Marker(0, DataMiningPackets.NodeFx, X, Y, Z);
        actor.SkipReceived();
        actor.SendRpc(Goggles);
        proof.Add((await actor.WaitForRpcAsync(Exact(on), "Advanced goggles turn on under 215")).Hex);
        proof.Add((await actor.WaitForRpcAsync(Exact(marker), "Advanced goggles mark the node 12 metres away")).Hex);
        await actor.WaitUntilAsync(() => server.DebugLog().Contains("ability=215 tool=38208 outcome=on", StringComparison.Ordinal),
            "The 215 cast selects the advanced goggles", HeadlessPlayer.DefaultTimeout);

        int cursor = actor.World.RpcSnapshot().Count;
        actor.SendChat("?qaunload 622");
        proof.Add((await StockAbilityProfile.ObserveAsync(actor, cursor, Exact(off), "Unloading 622 deactivates the 215 tool search")).Hex);
        proof.Add((await StockAbilityProfile.ObserveAsync(actor, cursor, Exact(DataMiningPackets.Remove(0)), "Unloading 622 removes the node marker")).Hex);
        actor.SkipReceived();
        actor.SendRpc(Goggles);
        proof.Add((await actor.WaitForRpcAsync("2e 67 00 00 00 00 00 00 00 00 00 00 00 2e 00 00 60")).Hex);

        actor.SendChat("?qaload 622");
        await actor.WaitForRpcAsync("80 b2 6e 02");
        actor.SendRpc(Goggles);
        proof.Add((await actor.WaitForRpcAsync(Exact(on), "Reloading 622 restores the advanced tool search")).Hex);
        await actor.WaitForRpcAsync(Exact(marker), "The restored search finds the same node");
        actor.SendRpc(Goggles);
        await actor.WaitForRpcAsync(Exact(off), "The second item press ends the restored search");

        actor.SendRpc(Handheld);
        proof.Add((await actor.WaitForRpcAsync(Exact(on), "The basic handheld still uses 215")).Hex);
        await actor.WaitUntilAsync(() => server.DebugLog().Contains("ability=215 tool=422 outcome=on", StringComparison.Ordinal),
            "The basic handheld is selected independently", HeadlessPlayer.DefaultTimeout);
        proof.Add((await actor.WaitForRpcAsync(Exact(off), "The basic handheld automatically expires", TimeSpan.FromSeconds(15))).Hex);
        Check.Equal(0, actor.World.Problems.Count, "Data Miner tool replay framing");
        await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "data-miner-activation.json"), JsonSerializer.Serialize(proof));
        return Array.Empty<byte>();

        static Func<RpcRecord, bool> Exact(byte[] bytes) => packet => packet.Bytes.AsSpan().SequenceEqual(bytes);
    }
}
