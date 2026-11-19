using System.Text.Json;
using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class AwakenedDataNodeToolsMode : NativeQaMode
{
    private const int CharacterLevel = 10;
    // PR113's complete stock Tools activation. Only the selected personal-bag slot changes.
    private const string Locate = "80 b9 d7 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 " +
        "00 00 00 00 0c f5 1a 00 c8 73 91 3d 08";
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
            "Open Actions > Tools and double-click Data Node Locator Goggles in slot 8. Capture Locate Data Node 215 with bag slot 8 and the server's 423 goggles search. The node FX stays active beyond 10 seconds.",
            "Use the goggles again to stop, then use Data Node Locator Handheld in slot 1. Capture 215 with slot 1, the 422 handheld search, its attenuation record and automatic 10-second expiry.",
            "After expiry, start the goggles again. Beside the visible node FX, use Basic Data Tap in slot 9 from Actions > Tools. Save the complete outgoing request, any target or tool refusal, and whether the locator FX persists. Tapping yield and completion are still open."
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
        SendLocate(8);
        proof.Add((await actor.WaitForRpcAsync(Enabled)).Hex);
        proof.Add((await actor.WaitForRpcAsync("80 cd 00 00 62 04 00 28")).Hex);
        await actor.WaitUntilAsync(() => server.DebugLog().Contains("ability=215 tool=423 outcome=on", StringComparison.Ordinal),
            "The selected goggles win over the earlier handheld bag slot", HeadlessPlayer.DefaultTimeout);
        await Task.Delay(TimeSpan.FromSeconds(11));
        Check.That(actor.Received(Disabled).Count == 0, "The goggles remain on after the handheld's ten-second lifetime");

        SendLocate(8);
        proof.Add((await actor.WaitForRpcAsync(Disabled)).Hex);
        SendLocate(9);
        proof.Add((await actor.WaitForRpcAsync("2e 67 00 00 00 00 00 00 00 00 00 00 00 2e 00 00 60")).Hex);

        SendLocate(8);
        await actor.WaitForRpcAsync("80 ac d7 00");
        await db.ExecuteAsync("UPDATE inventory SET goid=422 WHERE charId=110 AND slot=8");
        await actor.WaitForRpcAsync("2e 67 00 00 00 00 00 00 00 00 00 00 00 2e 00 00 60");
        await db.ExecuteAsync("UPDATE inventory SET goid=423 WHERE charId=110 AND slot=8");

        SendLocate(1);
        proof.Add((await actor.WaitForRpcAsync(Enabled)).Hex);
        proof.Add((await actor.WaitForRpcAsync("80 cb")).Hex);
        await actor.WaitUntilAsync(() => server.DebugLog().Contains("ability=215 tool=422 outcome=on", StringComparison.Ordinal),
            "Selecting the handheld starts the 422 search", HeadlessPlayer.DefaultTimeout);
        proof.Add((await actor.WaitForRpcAsync(Disabled, TimeSpan.FromSeconds(15))).Hex);
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "data-node-tools.json"), JsonSerializer.Serialize(proof));
        return Array.Empty<byte>();

        void SendLocate(byte slot)
        {
            actor.SkipReceived();
            actor.SendRpc($"{Locate} {slot:x2} 01 00 00 00 00");
        }
    }
}
