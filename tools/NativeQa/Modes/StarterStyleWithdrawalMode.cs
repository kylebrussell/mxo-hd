using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class StarterStyleWithdrawalMode : NativeQaMode
{
    private const ushort SelfDefense = CombatStyles.SelfDefense, Hacker = CombatStyles.HackerStyle, Coder = CombatStyles.CoderStyle;
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, Hacker, Coder).Order().ToArray();

    // The complete stock alternative stance selections also used by StarterStyleFallbackMode.
    private static readonly (ushort Style, int Withdraw, string? Request)[] Trials =
    {
        (SelfDefense, 65, null),
        (Hacker, 19, "80b9ee0300000000000000000000000000000000000000000000000000000084d75741086b0000000000"),
        (Coder, 21, "80b9f0030400020000000000000000000000000000000000000000000000000084d75741086b0000000000")
    };

    public override string Name => "ability-parity-starter-style-withdrawal";
    public override NativeQaModePlan Plan { get; } = new(Loadout, CombatFixtureLoadout.CasterX, CombatFixtureLoadout.CasterZ,
        Array.Empty<string>(), new[]
        {
            $"UPDATE char_abilities SET level=15 WHERE char_id=110 AND ability_id IN ({AbilityMessages.AbilityGameObject(Hacker)},{AbilityMessages.AbilityGameObject(Coder)})"
        }, new[]
        {
            $"UPDATE characters SET level=50,exp={PlayerProgression.ExperienceForLevel(50)},y={CombatFixtureLoadout.GroundY},healthC=4000,healthM=4000 WHERE charId=110",
            "DELETE FROM inventory WHERE charId=110 AND slot=107"
        }, DevCommands: true, hotbarAbilities: new[] { SelfDefense, Hacker, Coder },
        testMobFixture: "awakened-interlock-resistance.csv", environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0",
            [CombatFixtureWeapons.ResetMode] = "1", [CombatFixtureWeapons.LiveResetMode] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "17,1006,1008", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            "Page 2 slots 1–3 are Self Defense rank 50, Hacker Style rank 15 and Coder Style rank 15. Each trial uses the same passive Melee Dummy.",
            "Before each trial use ?combatdepth fixture-reset. Wait for the ready line and for the combat HUD to clear. Strafe and reselect the same dummy, then open ordinary Close Combat.",
            "Close Combat selects Self Defense. Leave it active for the first trial. For the next two trials choose Hacker or Coder after entering interlock. Use the normal Withdraw button once. The server log should print [WITHDRAW INPUT OURS] with direct/input 65, 19 or 21. The roll can still fail. Reset and move to the next style once the input is logged."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        var server = context.ActualServer!;
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        var dummy = await actor.WaitForViewNamedAsync(CombatFixtureLoadout.DummyName);
        CombatFixtureLoadout.AssertDummy(actor, dummy);
        uint handle = DuelPackets.Handle(dummy.ViewId, dummy.SpawnId);
        string hex = WireFormat.ToHex(BitConverter.GetBytes(handle));
        foreach (var trial in Trials)
        {
            actor.SkipReceived();
            actor.SendChat("?combatdepth fixture-reset");
            await actor.WaitForTextAsync($"Fixture ready: Melee Dummy view {dummy.ViewId}, spawn {dummy.SpawnId}");
            actor.SendRpc($"81 51 {hex}");
            actor.World.SendRpcs(StarterHackerControlTargetMode.WithHandle(AwakenedInterlockAdmissionMode.StockOpen, handle),
                WireFormat.FromHex($"40 {hex} 01 6b"));
            await actor.WaitForRpcAsync($"40 {hex} 00 6b");
            await actor.WaitForRpcAsync("47");

            if (trial.Request is { } request)
            {
                int viewStart = actor.World.ViewSnapshot().Count;
                actor.SendRpc(StarterHackerControlTargetMode.WithHandle(request, handle));
                byte[] selection = PlayerViewAttributes.BuildSelf((39, PlayerViewAttributes.U32(trial.Style)));
                await actor.WaitUntilAsync(() => actor.World.ViewSnapshot().Skip(viewStart)
                        .Any(view => BytePattern.IndexOf(view, selection, 0) >= 0),
                    $"The active stance is {trial.Style}", HeadlessPlayer.DefaultTimeout);
            }
            await StockAbilityProfile.ObserveAsync(actor, 0,
                frame => StockAbilityProfile.Bonus(frame, trial.Style, 1005, trial.Withdraw, 0, false),
                "The selected style sends the expected Contested Withdraw modifier");

            int from = server.DebugLog().Length;
            actor.SendRpc("44 01");
            await actor.WaitUntilAsync(() => server.DebugLog()[from..].Contains(
                    $"[WITHDRAW INPUT OURS] CSR_Tester direct={trial.Withdraw} input={trial.Withdraw}", StringComparison.Ordinal),
                $"Ordinary Withdraw uses the current style's {trial.Withdraw} input", HeadlessPlayer.DefaultTimeout);
        }
        actor.SendChat("?combatdepth fixture-reset");
        await actor.WaitForTextAsync($"Fixture ready: Melee Dummy view {dummy.ViewId}, spawn {dummy.SpawnId}");
        Check.Equal(0, actor.World.Problems.Count, "Style withdrawal fixture framing");
        return Array.Empty<byte>();
    }
}
