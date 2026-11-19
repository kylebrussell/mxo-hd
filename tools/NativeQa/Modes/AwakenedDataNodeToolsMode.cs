using System.Text.Json;
using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class AwakenedDataNodeToolsMode : NativeQaMode
{
    private const int CharacterLevel = 10;
    // Complete stock hotbar requests from PR385, PacketLog 226d05636f73 lines 1195–1196 and 1380–1381.
    private const string Goggles = "80 b9 d7 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 " +
        "00 00 00 00 64 f1 1a 00 e8 26 28 3b 08 08 00 00 00 00 00";
    private const string Handheld = "80 b9 d7 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 " +
        "00 00 00 00 64 f1 1a 00 e8 18 28 3b 08 01 00 00 00 00 00";
    private const string Enabled = "80 bd 05 d7 00 00 00 00 00 00 01";
    private const string Disabled = "80 bd 05 d7 00 00 00 00 00 00 00";

    public override string Name => "awakened-data-node-tools";
    public override NativeQaModePlan Plan { get; } = new(
        new ushort[] { 2, 17, 215, 165 }, 23828, 13984,
        Array.Empty<string>(),
        new[]
        {
            "DELETE FROM inventory WHERE charId=110 AND slot IN (1,8,9)",
            "INSERT INTO inventory(charId,goid,slot,count,purity) VALUES(110,422,1,1,0),(110,423,8,1,0),(110,38210,9,1,0)",
            $"UPDATE char_abilities SET level=LEAST(level,{CharacterLevel}) WHERE char_id=110"
        },
        new[] { $"UPDATE characters SET level={CharacterLevel},exp={PlayerProgression.ExperienceForLevel(CharacterLevel)},y=495 WHERE charId=110" },
        DevCommands: true, loadAbilityAncestors: true,
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        readyMessages: new[]
        {
            "Use ?utilityobject 606 on the Mara NW stand to place one node at your feet. Keep all three seeded tools in the bag.",
            "In Actions > Tools, right-click Data Node Goggles and Data Node Locator and add both items to the hotbar. Activate Goggles from the hotbar. Capture 215 with bag slot 8 and the server's 423 goggles search. The node FX stays active beyond 10 seconds.",
            "Use the goggles hotbar item again to stop, then activate the handheld hotbar item. Capture 215 with slot 1, the 422 handheld search, its attenuation record and automatic 10-second expiry.",
            "The earlier stock run captured the Data Tap request from slot 9. This retest covers the two locator items."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var db = context.OwnedDatabase ?? throw new InvalidOperationException("Expected the disposable data-node database.");
        Check.Equal(3L, await db.LongAsync(
            "SELECT COUNT(*) FROM inventory WHERE charId=110 AND count=1 AND ((slot=1 AND goid=422) OR (slot=8 AND goid=423) OR (slot=9 AND goid=38210))"),
            "The two locator tools and the basic tap each occupy their documented personal bag slot");
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        var server = context.ActualServer ?? throw new InvalidOperationException("Expected the disposable data-node server.");
        var loaded = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]);
        foreach (ushort id in new ushort[] { 165, 215 })
            Check.That(loaded.Any(row => AbilityCatalog.IdOf(row.Descriptor) == id && row.Level == 5), $"Stock Margin loads ability {id} at its cap of five");

        await actor.EnterWorldAsync();
        actor.SendChat("?utilityobject 606");
        await actor.WaitForTextAsync("Utility fixture 606 placed.");
        var proof = new List<string>();
        SendLocate(Goggles);
        proof.Add((await actor.WaitForRpcAsync(Enabled)).Hex);
        proof.Add((await actor.WaitForRpcAsync("80 cd 00 00 62 04 00 28")).Hex);
        await actor.WaitUntilAsync(() => server.DebugLog().Contains("ability=215 tool=423 outcome=on", StringComparison.Ordinal),
            "The selected goggles win over the earlier handheld bag slot", HeadlessPlayer.DefaultTimeout);
        await Task.Delay(TimeSpan.FromSeconds(11));
        Check.That(actor.Received(Disabled).Count == 0, "The goggles remain on after the handheld's ten-second lifetime");

        SendLocate(Goggles);
        proof.Add((await actor.WaitForRpcAsync(Disabled)).Hex);
        byte[] wrongTool = WireFormat.FromHex(Goggles);
        wrongTool[37] = 9;
        SendLocate(WireFormat.ToHex(wrongTool));
        proof.Add((await actor.WaitForRpcAsync("2e 67 00 00 00 00 00 00 00 00 00 00 00 2e 00 00 60")).Hex);

        SendLocate(Goggles);
        await actor.WaitForRpcAsync("80 ac d7 00");
        await db.ExecuteAsync("UPDATE inventory SET goid=422 WHERE charId=110 AND slot=8");
        await actor.WaitForRpcAsync("2e 67 00 00 00 00 00 00 00 00 00 00 00 2e 00 00 60");
        await db.ExecuteAsync("UPDATE inventory SET goid=423 WHERE charId=110 AND slot=8");

        SendLocate(Handheld);
        proof.Add((await actor.WaitForRpcAsync(Enabled)).Hex);
        proof.Add((await actor.WaitForRpcAsync("80 cb")).Hex);
        await actor.WaitUntilAsync(() => server.DebugLog().Contains("ability=215 tool=422 outcome=on", StringComparison.Ordinal),
            "Selecting the handheld starts the 422 search", HeadlessPlayer.DefaultTimeout);
        proof.Add((await actor.WaitForRpcAsync(Disabled, TimeSpan.FromSeconds(15))).Hex);
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "data-node-tools.json"), JsonSerializer.Serialize(proof));
        return Array.Empty<byte>();

        void SendLocate(string request)
        {
            actor.SkipReceived();
            actor.SendRpc(request);
        }
    }
}
