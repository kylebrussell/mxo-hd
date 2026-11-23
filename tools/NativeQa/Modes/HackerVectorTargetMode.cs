using hds;
using HeadlessClient;
using static NativeQa.Modes.StarterHackerControlTargetMode;

namespace NativeQa.Modes;

public sealed class HackerVectorTargetMode : NativeQaMode
{
    private const ushort Vector = 355, Blind = 43, HackerStyle = CombatStyles.HackerStyle;
    private const int Level = 31, Rank = 30, StartingIs = 700;
    private const int PickX = 21550, TargetX = 21750, BlockedX = 22350, FarX = 19600, Y = 1095, Z = 16150;
    private const string Pick = "?cd sp", Blocked = "?cd sb", Far = "?cd gf";
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, HackerStyle, Vector, Blind).Order().ToArray();

    public override string Name => "ability-parity-hacker-vector-target";

    public override NativeQaModePlan Plan { get; } = new(Loadout, PickX, Z, Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET level=LEAST(level,{Rank}) WHERE char_id=110" }, new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y={Y},rotation=64," +
                $"innerStrC={StartingIs},innerStrM={StartingIs} WHERE charId=110"
        }, DevCommands: true, hotbarAbilities: new[] { HackerStyle, Vector }, environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_COMBAT_FOUNDATIONS_QA"] = "1",
            ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1", ["MXO_FREECAST_VIRAL_QA"] = "1",
            ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766", ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0"
        }, extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "355", ["NativeCloseableRows"] = 0,
            ["LoadedMemory"] = Loadout.Sum(AbilityMemory.Cost), ["MemoryCapacity"] = AbilityMemory.ForLevel(Level)
        }, readyMessages: new[]
        {
            $"Level {Level}, rank {Rank}, {Loadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} memory. Outdoor NAV CSR ({PickX},{Y},{Z}) faces QA Sight Target ({TargetX},{Y},{Z}). Page 2 slot 1 Hacker Style, slot 2 Vector. Run {Pick} to spawn the passive target, then select it without starting combat.",
            $"Try slot 2 before selecting Hacker Style. Select Hacker Style, keep the target selected with no Escape, and try slot 2 at {Far} ({FarX},{Y},{Z}, 21.5 m), then {Blocked} ({BlockedX},{Y},{Z}, blocked at 6 m). If sent, expect refusal 13, 34 or 9 respectively, with no IS, cast or reuse.",
            $"Return with {Pick}, reselect if needed and cast Vector. Save the first complete stock 80 b9 Vector request. Expect a two-second cast, [ABILITY-COST] 35 IS ({StartingIs} to {StartingIs - 35}), 60 s own reuse and a [FREECAST-VIRAL] hit only on QA Sight Target, with no health damage or interlock.",
            "Expect the current comparison records: [MODIFIER] source=355 stat=96 Powerless for 8.94 s plus stat=183 -60 for 30 s. Confirm removal at each lifetime and the existing 8 s immunity notice. These are the server's retained comparison values; the shipped template authors 6 s and -35 and their original scaling remains unresolved."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (int x in new[] { PickX, TargetX, BlockedX, FarX })
            Check.That(NavMesh.Locate(1, x, Y, Z, out var stand) && !stand.indoors && Math.Abs(stand.floorY + 95 - Y) < 1,
                $"Verify outdoor Vector stand ({x},{Y},{Z})");
        Check.That(NavMesh.HasSightPath(1, PickX, Y, Z, TargetX, Y, Z) &&
            NavMesh.HasSightPath(1, FarX, Y, Z, TargetX, Y, Z) && !NavMesh.HasSightPath(1, BlockedX, Y, Z, TargetX, Y, Z),
            "Verify the clear normal and far stands and the blocked near stand");

        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        var profile = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        Check.That(profile.Keys.ToHashSet().SetEquals(Loadout) && Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level) &&
            Loadout.All(id => AbilityPrerequisites.Current.Closure(id).All(required =>
                profile.TryGetValue(required.Key, out var row) && row.Level >= required.Value)),
            "Verify ordinary level-31 Archive memory and every ranked prerequisite");

        var target = await SightStandCastProof.PickAsync(actor, HackerStyle, StarterHackerViralSightMode.StockStyle);
        var server = context.ActualServer!;
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        uint handle = DuelPackets.Handle(target.ViewId, target.SpawnId);
        // Retained Disrupt Inputs has the same direct-target selection range and a complete capture.
        // Vector is covered at the production dispatcher in tests and will be captured in native QA.
        byte[] blindRequest = WithHandle(EarlyHackerControlRecoveryMode.BlindRequest, handle);

        actor.SendChat(Far);
        await actor.WaitForTextAsync("QA Sight Target is 21.5 m ahead with clear sight");
        await RefusedAsync(actor, Blind, blindRequest, AbilityRefusals.TargetOutOfRange);
        actor.SendChat(Pick);
        await actor.WaitForTextAsync("Select QA Sight Target 2 m ahead");

        int logAt = server.DebugLog().Length;
        int rpcAt = actor.World.RpcSnapshot().Count;
        actor.SendRpc(blindRequest);
        await EvidenceRetestWire.CastBarAsync(actor, Blind, 2);
        await EvidenceRetestWire.TimerAsync(actor, rpcAt, Blind, AbilityCooldowns.OwnTimer, 43);
        await actor.WaitUntilAsync(() => server.DebugLog()[logAt..].Split('\n').Any(line =>
            line.Contains("[EFFECTS] 43 on QA Sight Target:", StringComparison.Ordinal) &&
            line.Contains("State 90 0 16 s", StringComparison.Ordinal)), "Stock Disrupt Inputs lands at the clear stand", HeadlessPlayer.DefaultTimeout);
        Check.Equal(0, actor.World.Problems.Count, "Vector supporting control framing");
        return Array.Empty<byte>();
    }
}
