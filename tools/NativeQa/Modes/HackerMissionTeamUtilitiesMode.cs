using System.Text.Json;
using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class HackerMissionTeamUtilitiesMode : NativeQaMode
{
    private const ushort Map = UtilityAbilities.DownloadMissionMap;
    private const ushort RecallMember = UtilityAbilities.RecallTeamMember, RecallTeam = UtilityAbilities.RecallMissionTeam;
    private static readonly (string Handle, int X, int Z)[] Stands =
    {
        ("QAFriend", 25183, -1177), ("QAFar", 25719, -2269), ("QAOther", 25100, -1498)
    };

    public override string Name => "ability-parity-hacker-mission-utilities";
    public override NativeQaModePlan Plan { get; } = new(
        new ushort[] { 2, 17, Map, RecallMember, RecallTeam }, 24800, -1498,
        Array.Empty<string>(), Array.Empty<string>(),
        new[] { "UPDATE characters SET alignment=3, repMero=100, y=495 WHERE charId=110" }
            .Concat(Stands.Select(stand =>
                $"UPDATE characters SET x={stand.X}, y=495, z={stand.Z}, alignment=3, pvpflag=0 WHERE handle='{stand.Handle}'"))
            .ToArray(),
        DevCommands: true, loadAbilityAncestors: true, hotbarAbilities: new[] { Map, RecallMember, RecallTeam },
        missionFixture: "locked-safe.xml", environmentOverrides: new() { ["MXO_UTILITY_MAP_QA_REVEAL"] = "1" },
        extraQaMetadata: new() { ["ScopedStockRows"] = "44,382,486", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            "Step A. Form a mission team with QAFriend and QAFar using the normal radial and helper accept. Keep QAOther outside. Their initial stands are separate and within view.",
            "Step B. Select QAFriend and press page 2 slot 2 (382). Observe that only QAFriend relocates beside CSR. Record the full native cast. Use helper chat '?tp 25183 495 -1177 0 0 0' to restore the distance before the next check.",
            "Step C. Clear selection and press page 2 slot 3 (486). Confirm both teammates arrive and QAOther stays put. Record the complete stock request and the before/after views.",
            "Step D. With 44 loaded, take the Locked Safe Fixture from Flood and approach the safe using the printed stand. Confirm the objective map ping. Use ?qaunload 44 and confirm refresh stops. The page 2 slot for 44 identifies the passive; its effect is triggered by mission acceptance."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var server = context.ActualServer ?? throw new InvalidOperationException("Expected a disposable mission-utility server.");
        foreach (var stand in Stands)
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, stand.Handle, stand.Handle, stockCharacterLoad: true);
            context.Helpers.Add(stand.Handle, helper);
            await helper.EnterWorldAsync();
            NativeQaReadiness.RequireNoAmbientAggro(server, helper);
        }

        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        var db = context.OwnedDatabase ?? throw new InvalidOperationException("Expected a disposable mission-utility database.");
        await actor.EnterWorldAsync();
        foreach (var stand in Stands)
        {
            var view = await actor.WaitForViewNamedAsync(stand.Handle);
            Check.That(EngageFoesFixture.Position(view).SequenceEqual(new double[] { stand.X, 495, stand.Z }),
                $"{stand.Handle} is alive and visible on the documented stand");
        }
        await HackerParitySweepMode.InviteTeamMembersAsync(
            actor, new[] { context.Helpers["QAFriend"], context.Helpers["QAFar"] });

        ushort transaction = 5;
        var map = await DownloadMissionMapMode.AcceptAndRevealAsync(actor, db, server, () => transaction++);
        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Single(row => AbilityCatalog.IdOf(row.Descriptor) == Map);
        int beforeUnload = server.DebugLog().Length;
        await StockAbilityProfile.ChangeLoadoutAsync(actor,
            new Dictionary<ushort, AbilityMemoryUiFixture.Tuple> { [Map] = archive }, new[] { Map }, load: false);
        await actor.WaitUntilAsync(() => server.DebugLog()[beforeUnload..].Contains("Download Mission Map: ping cleared Safe 1", StringComparison.Ordinal),
            "Unloading the map clears its refresh while the team remains formed", HeadlessPlayer.DefaultTimeout);
        Check.Equal(0, actor.World.Problems.Count, "Mission-utility team and map startup framing");
        await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "hacker-mission-utilities.json"),
            JsonSerializer.Serialize(new { Team = new[] { "QAFriend", "QAFar" }, Outsider = "QAOther", map.Flags, Map = map.Reveal.Hex }));
        return Array.Empty<byte>();
    }
}
