using hds;
using HeadlessClient;
using static NativeQa.StockAbilityProfile;
using static NativeQa.Modes.StarterHackerControlTargetMode;

namespace NativeQa.Modes;

public sealed class StarterGunLandingMode : NativeQaMode
{
    private const int Level = 19, PickX = 21550, BlockedX = 22350, TargetX = 21750, FarX = 19600, Y = 1095, Z = 16150;
    private const string TargetName = "QA Sight Target", Pick = "?cd sp", Block = "?cd sb", Far = "?cd gf";
    private const ushort Gunman = 128, Handguns = 129, Hindering = 130, Disarming = 126;
    private const int StartingIs = 500;
    // Stock PR389 (130) and PR394/404 (Handguns and 126).
    private const string HandgunsFrame = "80b981000400020000000000000000000000000000000000000000000000000000000000086b0000000000";
    private const string HinderingFrame = "80b982000400020000000000000000000000000000000000000000000000000090767241086b0000000000";
    private const string DisarmingFrame = "80b97e000400020000000000000000000000000000000000000000007e000000006b0000086b0000000000";
    private static readonly ushort[] Profile = new ushort[] { 2, 17, Gunman, Handguns, Hindering, Disarming }
        .SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys.Append(id)).Distinct().Order().ToArray();

    public override string Name => "ability-parity-starter-gun-delayed-landing";

    public override NativeQaModePlan Plan { get; } = new(Profile, PickX, Z, Array.Empty<string>(),
        new[] { StarterHackerPassiveFixture.CapRanks }, new[]
        {
            CombatFixtureLoadout.SeedWeapons,
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y={Y},rotation=64," +
                $"innerStrC={StartingIs},innerStrM={StartingIs} WHERE charId=110"
        }, DevCommands: true, hotbarAbilities: new[] { Handguns, Hindering, Disarming },
        environmentOverrides: new()
        {
            ["MXO_MOBS"] = "test", ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0",
            ["MXO_COMBAT_FOUNDATIONS_QA"] = "1", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "126,130", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            $"Level {Level}, {Profile.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} memory. Page 2 Handguns, Hindering, Disarming. {Pick} puts CSR at ({PickX},{Y},{Z}) facing {TargetName} at ({TargetX},{Y},{Z}). {Block} is ({BlockedX},{Y},{Z}). Pick the target, use slot 1 and stay out of interlock. Keep the selection without Escape.",
            $"In the clear, press slot 2. Use {Block} during the 2 s bar. Expect [ABILITY] 130 fizzles, with 20 IS, 8 s own / 5 s group reuse and no damage or [MODIFIER]. Return to the pick stand after the fizzle.",
            $"Repeat slot 2 from the pick stand but use {Far} during its 2 s bar. At ({FarX},{Y},{Z}), the target has a clear sight line at 21.5 m. Expect the same fizzle. Return to the pick stand.",
            "After reuse, keep the same target on the clear stand and press slot 2 again. Expect damage 9, source 130 [MODIFIER] stats 115/877/878 -50 and 519 -10 for 8 s, 20 IS and normal reuse.",
            $"For a control, press slot 3 from the pick stand after its group timer clears. Use {Block} during the 4 s bar. If the stock cast is admitted at rest, expect the same fizzle with 20 IS and 8 s own reuse. No max [VITALS] or [PVP] rounds."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (int x in new[] { PickX, TargetX, BlockedX, FarX })
            Check.That(NavMesh.Locate(1, x, Y, Z, out var floor) && !floor.indoors && Math.Abs(floor.floorY + 95 - Y) <= 100,
                $"Verified outdoor NAV ({x},{Y},{Z})");
        Check.That(NavMesh.HasSightPath(1, PickX, Y, Z, TargetX, Y, Z) &&
            !NavMesh.HasSightPath(1, BlockedX, Y, Z, TargetX, Y, Z), "Verified distinct sight results within 20 m");
        Check.That(Math.Abs(FarX - TargetX) > 2000 && NavMesh.HasSightPath(1, FarX, Y, Z, TargetX, Y, Z),
            "The distance control has clear sight beyond 20 m");
        Console.WriteLine($"GUN FAR: {Far} to ({FarX},{Y},{Z}), 21.5 m with clear sight.");

        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        StarterHackerPassiveFixture.VerifyProfile(actor, Profile);
        await CombatFixtureLoadout.VerifyWeaponAtLoginAsync(actor, CombatFixtureWeapons.Handgun);
        var server = context.ActualServer!;
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        await MoveAsync(Pick, "Select QA Sight Target 2 m ahead");
        var target = await actor.WaitForViewNamedAsync(TargetName);
        uint handle = DuelPackets.Handle(target.ViewId, target.SpawnId);
        actor.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(handle))}");
        actor.SendRpc(WithHandle(HandgunsFrame, handle));
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 81 00 00 00");
        await HinderingShotTargetMode.Refused(actor, WithHandle(HinderingFrame, handle ^ 0x10000), "stale target spawn");

        await FizzleAsync(Hindering, HinderingFrame, 2, Block, "QA Sight Target is 6 m west");
        await FizzleAsync(Hindering, HinderingFrame, 2, Far, "QA Sight Target is 21.5 m ahead with clear sight");
        int positiveAt = actor.World.RpcSnapshot().Count;
        int from = server.DebugLog().Length;
        actor.SendRpc(WithHandle(HinderingFrame, handle));
        await EvidenceRetestWire.CastBarAsync(actor, Hindering, 2);
        await EvidenceRetestWire.TimerAsync(actor, positiveAt, Hindering, AbilityCooldowns.OwnTimer, 8);
        await actor.WaitUntilAsync(() =>
        {
            string log = server.DebugLog()[from..];
            return log.Contains("[EFFECTS] 130 on QA Sight Target:", StringComparison.Ordinal) &&
                log.Contains($"[ABILITY] 130 lands on view {target.ViewId} for 9", StringComparison.Ordinal);
        }, "Hindering Shot lands for 9 with its effects after sight and range are restored", HeadlessPlayer.DefaultTimeout);
        string positive = server.DebugLog()[from..];
        foreach (string effect in new[] { "Stat 115 -50 8 s", "Stat 877 -50 8 s", "Stat 878 -50 8 s", "Stat 519 -10 8 s" })
            Check.That(positive.Contains(effect, StringComparison.Ordinal), $"The admitted hit applies {effect}");
        await EvidenceRetestWire.TimerAsync(actor, positiveAt, 690, AbilityCooldowns.GroupTimer, 5, ended: true);

        await FizzleAsync(Disarming, DisarmingFrame, 4, Block, "QA Sight Target is 6 m west");
        await actor.WaitUntilAsync(() => actor.World.ViewSnapshot().SelectMany(AbilityParityCoderSweepMode.InnerStrengthUpdates)
            .Contains((ushort)(StartingIs - 80)), "Four admitted starts cost 20 IS each", HeadlessPlayer.DefaultTimeout);
        Check.Equal(0, actor.World.Problems.Count, "Delayed handgun cast framing");
        return Array.Empty<byte>();

        async Task FizzleAsync(ushort ability, string capture, int cast, string move, string confirmation)
        {
            int attempt = server.DebugLog().Length;
            int reuseAt = actor.World.RpcSnapshot().Count;
            actor.SendRpc(WithHandle(capture, handle));
            await EvidenceRetestWire.CastBarAsync(actor, ability, cast);
            await MoveAsync(move, confirmation);
            await EvidenceRetestWire.TimerAsync(actor, reuseAt, ability, AbilityCooldowns.OwnTimer, 8);
            await actor.WaitUntilAsync(() => server.DebugLog()[attempt..].Contains(
                $"[ABILITY] {ability} fizzles: the selected gun target changed, left range or left sight", StringComparison.Ordinal),
                $"{ability} rejects the changed landing conditions", HeadlessPlayer.DefaultTimeout);
            string onLanding = server.DebugLog()[attempt..];
            Check.That(!onLanding.Contains($"[ABILITY] {ability} lands", StringComparison.Ordinal) &&
                !onLanding.Contains($"[EFFECTS] {ability} on", StringComparison.Ordinal), $"No collateral effects from cancelled {ability}");
            await MoveAsync(Pick, "Select QA Sight Target 2 m ahead");
            if (ability == Hindering)
            {
                await EvidenceRetestWire.TimerAsync(actor, reuseAt, 690, AbilityCooldowns.GroupTimer, 5);
                await EvidenceRetestWire.TimerAsync(actor, reuseAt, Hindering, AbilityCooldowns.OwnTimer, 8, ended: true);
            }
        }

        async Task MoveAsync(string command, string confirmation)
        {
            actor.SkipReceived();
            actor.SendChat(command);
            await actor.WaitForTextAsync(confirmation);
        }
    }
}
