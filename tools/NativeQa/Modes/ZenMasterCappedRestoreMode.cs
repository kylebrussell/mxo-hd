using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class ZenMasterCappedRestoreMode : NativeQaMode
{
    private const int X = 17000, Y = 495, Z = 6400;
    private const ushort Parent = 871, Effect = 872;

    public override string Name => "zen-master-capped-restore";

    public override NativeQaModePlan Plan { get; } = new(new ushort[] { 2, 17, Parent }, X, Z,
        Array.Empty<string>(), Array.Empty<string>(), new[]
        {
            $"UPDATE characters SET level=99,exp={PlayerProgression.ExperienceForLevel(50)},x={X},y={Y},z={Z},rotation=0,healthC=500,healthM=500,innerStrC=193,innerStrM=200 WHERE charId=110"
        }, DevCommands: true, FixedCallbackRolls: true, HealthCallbackQa: true,
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_DESPERATION_QA_HOLD_HEALTH"] = "1",
            ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1"
        },
        extraQaMetadata: new() { ["ScopedStockRows"] = "872", ["NativeCloseableRows"] = 1, ["ZenMasterForcedLevel"] = 99 },
        readyMessages: new[]
        {
            "Stay at verified HOME, no target. If displaced, type the READY warp in chunks of up to 16 characters into one line before Enter. Disposable level 99 admits the shipped parent; HP/IS regen held and passive roll fixed only in this fixture.",
            "Start 193/200 IS. Type ?health 499 and verify +5 (193 to 198), restore text/FX. Type ?health 498 for +2 (198 to 200), restore text/FX.",
            "At cap type ?health 497. Verify real HP change and [HEALTH-EFFECT] 871->872 skips restore at cap 200, IS still 200, no new restore line or FX. ?qaunload 871, then ?health 496: no 872 branch at all. No activation request exists for the internal child."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Repository NAV required.");
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        Console.WriteLine($"READY HOME {InvisibilityBeltWatcherMode.Warp(X, Y, Z, 0, inside: false)}");
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        await actor.EnterWorldAsync();
        var server = context.ActualServer ?? throw new InvalidOperationException("Disposable server required.");
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        var view = actor.World.CreationSnapshot().Last(frame => frame.ViewId == 2);
        Check.Equal((byte)99, view.Attributes["Level"][0], "Diagnostic level meets the actual parent prerequisite");
        uint restoreText = (uint)AbilityCatalog.Find(Effect).Modifiers.Single().Number("ActivationStringOnSelf");
        byte[] Message(int amount) => AbilityMessages.AboutAbility(restoreText, true, Effect, actor.Handle, actor.Handle, amount);
        int entries = actor.World.RpcSnapshot().Count;
        int log = server.DebugLog().Length;
        await ChangeHealthAsync(499);
        await StockAbilityProfile.ObserveAsync(actor, entries, frame => frame.Bytes.AsSpan().SequenceEqual(Message(5)), "Actual +5 restore");
        await ChangeHealthAsync(498);
        await StockAbilityProfile.ObserveAsync(actor, entries, frame => frame.Bytes.AsSpan().SequenceEqual(Message(2)), "Actual clipped +2 restore");
        await ChangeHealthAsync(497);
        string effects = server.DebugLog()[log..];
        Check.That(effects.Contains("871->872 restores IS to 198") && effects.Contains("871->872 restores IS to 200") &&
            effects.Contains("871->872 skips restore at cap 200"), "Two paid restores and one genuine capped trigger");
        Check.That(!actor.World.RpcSnapshot().Skip(entries).Any(frame => frame.Bytes.AsSpan().SequenceEqual(Message(0))),
            "No false zero-amount restore RPC after capped trigger");
        var rows = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        await StockAbilityProfile.ChangeLoadoutAsync(actor, rows, new[] { Parent }, load: false);
        log = server.DebugLog().Length;
        await ChangeHealthAsync(496);
        Check.That(!server.DebugLog()[log..].Contains("871->872"), "Unloaded listener does not process the health change");
        Check.Equal(0, actor.World.Problems.Count, "Valid stock health and Archive traffic");
        return Array.Empty<byte>();

        async Task ChangeHealthAsync(int value)
        {
            actor.SkipReceived();
            actor.SendChat($"?health {value}");
            actor.SendChat("?health");
            await actor.WaitForTextAsync($"Current health: {value}");
        }
    }
}
