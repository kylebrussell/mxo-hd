using System.Buffers.Binary;
using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class DataMinerOneCheckBatchMode : NativeQaMode
{
    private const ushort Locate = 215, SeeLuggables = 540, DetectLuggables = 541, Mastery = 621;
    private const int X = 23828, Y = 495, Z = 13984, StandX = X + 200;

    public override string Name => "ability-parity-data-miner-tools";
    public override NativeQaModePlan Plan { get; } = new(
        new ushort[] { 2, 17, Locate, SeeLuggables, DetectLuggables, Mastery }, X, Z, Array.Empty<string>(),
        new[]
        {
            "DELETE FROM inventory WHERE charId=110 AND slot IN (1,8)",
            "INSERT INTO inventory(charId,goid,slot,count,purity) VALUES(110,422,1,1,0),(110,423,8,1,0)"
        }, new[] { $"UPDATE characters SET y={Y} WHERE charId=110" }, DevCommands: true, loadAbilityAncestors: true,
        hotbarAbilities: new[] { Mastery }, environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        extraQaMetadata: new() { ["ScopedStockRows"] = "540,541,621", ["NativeCloseableRows"] = 2 },
        readyMessages: new[]
        {
            $"Step A. Place the light luggable with ?utilityobject 596 at spawn, then move to ?tp {StandX} {Y} {Z} 0 0 0, two metres east. Add Data Node Goggles and Data Node Locator from Actions > Tools to free hotbar slots.",
            "Step B. Use the seeded bag-slot-8 Goggles and check the light luggable aura (540). Press the goggles again to clear it. Open the minimap and use the bag-slot-1 handheld. Check the separate yellow luggable ping (541), its ten-second display, and the outgoing tool press.",
            $"Step C. After handheld expiry, return to ?tp {X} {Y} {Z} 0 0 0 and use ?utilityobject 606 to place a node. Try page 2 slot 1 with both tools in the bag. Record whether Find Data Node emits a 621 request or the client suppresses the slot. The stock Tools entries are the known 215 route."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        var owned = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Select(row => AbilityCatalog.IdOf(row.Descriptor)).ToHashSet();
        Check.That(new[] { Locate, SeeLuggables, DetectLuggables, Mastery }.All(owned.Contains), "Both searches and both luggable abilities are loaded");
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, actor);
        actor.SendChat("?utilityobject 596");
        await actor.WaitForTextAsync("Utility fixture 596 placed.");
        actor.SendChat($"?tp {StandX} {Y} {Z} 0 0 0");
        await actor.WaitForTextAsync("Teleported to 0/0");
        actor.SendRpc(AwakenedDataNodeToolsMode.Goggles);
        await actor.WaitForRpcAsync("80 cd 80 00 f9 04 00 28");
        actor.SendRpc(AwakenedDataNodeToolsMode.Goggles);
        await actor.WaitForRpcAsync("80 ce 80 00");
        await actor.WaitForRpcAsync(packet => packet.Bytes.AsSpan().SequenceEqual(AbilityBuffs.BuildExclusive(Locate, false)), "Goggles switch off");
        actor.SendRpc(AwakenedDataNodeToolsMode.Handheld);
        byte[] yellow = DataMiningPackets.Map(new[] { new DataMiningPackets.Ping(-2, 0, 0) }, 10);
        await actor.WaitForRpcAsync(packet => packet.Bytes.AsSpan().SequenceEqual(yellow), "Handheld sends the westward yellow luggable ping");
        await actor.WaitForRpcAsync(packet => packet.Bytes.AsSpan().SequenceEqual(AbilityBuffs.BuildExclusive(Locate, false)),
            "The handheld search expires", TimeSpan.FromSeconds(15));
        actor.SkipReceived();
        actor.SendChat($"?tp {X} {Y} {Z} 0 0 0");
        await actor.WaitForTextAsync("Teleported to 0/0");
        actor.SendChat("?utilityobject 606");
        await actor.WaitForTextAsync("Utility fixture 606 placed.");
        // 621 still needs a native sender. Probe the server route using the known full handheld frame.
        byte[] mastery = WireFormat.FromHex(AwakenedDataNodeToolsMode.Handheld);
        BinaryPrimitives.WriteUInt16LittleEndian(mastery.AsSpan(2), Mastery);
        actor.SendRpc(mastery);
        await actor.WaitForRpcAsync(packet => packet.Bytes.AsSpan().SequenceEqual(AbilityBuffs.BuildExclusive(Mastery, true)),
            "The explicit mastery uses its own activation ID");
        await actor.WaitForRpcAsync("80 cd 00 00 62 04 00 28");
        Check.Equal(0, actor.World.Problems.Count, "Data Miner batch request framing");
        return Array.Empty<byte>();
    }
}
