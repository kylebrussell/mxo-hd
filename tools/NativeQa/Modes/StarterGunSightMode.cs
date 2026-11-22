using hds;
using HeadlessClient;
using static NativeQa.Modes.StarterHackerControlTargetMode;

namespace NativeQa.Modes;

public sealed class StarterGunSightMode : NativeQaMode
{
    private const int Level = 19, PickX = 21550, BlockedX = 22350, TargetX = 21750, Y = 1095, Z = 16150;
    private const ushort Handguns = 129, Smgs = 158, Disarming = 126, Hindering = 130, Covering = 176;

    // PR389 stock Hindering Shot and PR394/404 stock style and timed-special frames, also used by the admission fixture.
    private const string HandgunsFrame = "80b981000400020000000000000000000000000000000000000000000000000000000000086b0000000000";
    private const string SmgsFrame = "80b99e000400020000000000000000000000000000000000000000000000000000000000086b0000000000";
    private static readonly (ushort Id, string Frame, int CastSeconds)[] HandgunShots =
    {
        (Hindering, "80b982000400020000000000000000000000000000000000000000000000000090767241086b0000000000", 2),
        (Disarming, "80b97e000400020000000000000000000000000000000000000000007e000000006b0000086b0000000000", 4)
    };
    private static readonly (ushort Id, string Frame, int CastSeconds) SmgShot =
        (Covering, "80b9b000050003000000000000000000000000000000000000000000b0000000006b0000086b0000000000", 4);
    private static readonly ushort[] Loadout = new ushort[] { 2, 17, 128, Handguns, Smgs, Hindering, Disarming, Covering }
        .SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys.Append(id)).Distinct().Order().ToArray();

    public override string Name => "ability-parity-starter-gun-selected-sight";

    public override NativeQaModePlan Plan { get; } = new(Loadout, PickX, Z, Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110" },
        new[]
        {
            CombatFixtureLoadout.SeedWeapons,
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y={Y},rotation=64," +
                "healthC=1200,healthM=1200,innerStrC=500,innerStrM=500 WHERE charId=110"
        }, DevCommands: true, hotbarAbilities: new[] { Handguns, Hindering, Disarming, Smgs, Covering },
        environmentOverrides: new()
        {
            ["MXO_MOBS"] = "test", ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0",
            ["MXO_COMBAT_FOUNDATIONS_QA"] = "1", [CombatFixtureWeapons.WeaponMode] = "1"
        },
        extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "126,130,176", ["NativeCloseableRows"] = 0, ["MemoryUsed"] = Loadout.Sum(AbilityMemory.Cost)
        },
        readyMessages: new[]
        {
            $"Level {Level}, {Loadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} memory. Verified outdoor NAV: pick ({PickX},{Y},{Z}), QA Sight Target ({TargetX},{Y},{Z}), blocked cast ({BlockedX},{Y},{Z}). Handgun equipped. Run ?cd foundations-sight-pick, select the target 2 m ahead and choose page 2 slot 1 Handguns.",
            "Keep the target selected and use ?cd foundations-sight-blocked. The same target is 6 m west across the mapped gap. Try slots 2 (Hindering Shot) and 3 (Disarming Shot) from rest. Expect a local sight gate or server reason 9, with no [ABILITY-COST], cast, reuse, damage or modifier.",
            "Return with ?cd foundations-sight-pick and keep or reselect the same target. Cast slot 2 for the clear control, look for 20 IS, a 2 s cast, 8 s own reuse, damage 9 and [MODIFIER] source 130, stats 115/877/878 -50 and 519 -10 for 8 s. Once the shared reuse clears, try slot 3. If stock admits it at rest, look for a 4 s cast, 20 IS, 8 s own reuse and damage 11.",
            "Use ?cd w smg and choose slot 4 (Submachine Guns). Keep the target selected and move to the blocked stand again, then try slot 5 (Covering Fire). Expect the same sight gate/no debit. Back on the clear stand, if stock sends it at rest, expect a 4 s cast, 25 IS, 8 s own reuse and damage 18. Do not open Close Combat on the sight target."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (int x in new[] { PickX, TargetX, BlockedX })
            Check.That(NavMesh.Locate(1, x, Y, Z, out var stand) && !stand.indoors && Math.Abs(stand.floorY + 95 - Y) <= 100,
                $"Outdoor gun sight stand ({x},{Y},{Z}) is on mapped NAV");
        Check.That(NavMesh.HasSightPath(1, PickX, Y, Z, TargetX, Y, Z) &&
            !NavMesh.HasSightPath(1, BlockedX, Y, Z, TargetX, Y, Z), "The fixture distinguishes clear and blocked sight within range");
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        StarterHackerPassiveFixture.VerifyProfile(actor, Loadout);
        await CombatFixtureLoadout.VerifyWeaponAtLoginAsync(actor, CombatFixtureWeapons.Handgun);
        var server = context.ActualServer!;
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        await MoveAsync(blocked: false);
        var target = await actor.WaitForViewNamedAsync("QA Sight Target");
        uint handle = DuelPackets.Handle(target.ViewId, target.SpawnId);
        actor.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(handle))}");
        await SelectAsync(Handguns, HandgunsFrame);
        await MoveAsync(blocked: true);
        foreach (var shot in HandgunShots)
            await RefusedAsync(actor, shot.Id, WithHandle(shot.Frame, handle), AbilityRefusals.TargetNotInSight);
        await MoveAsync(blocked: false);
        foreach (var shot in HandgunShots)
            await LandAsync(shot);

        await CombatFixtureLoadout.VerifyWeaponSwapAsync(actor, context.OwnedDatabase!, "smg", CombatFixtureWeapons.Smg);
        await SelectAsync(Smgs, SmgsFrame);
        await MoveAsync(blocked: true);
        await RefusedAsync(actor, SmgShot.Id, WithHandle(SmgShot.Frame, handle), AbilityRefusals.TargetNotInSight);
        await MoveAsync(blocked: false);
        int landingAt = actor.World.RpcSnapshot().Count;
        await LandAsync(SmgShot);
        await EvidenceRetestWire.TimerAsync(actor, landingAt, Covering, AbilityCooldowns.OwnTimer, 8, ended: true);

        // The first request was refused without a cast. This second request is admitted before sight changes.
        int startedAt = actor.World.RpcSnapshot().Count;
        int fizzleAt = server.DebugLog().Length;
        actor.SendRpc(WithHandle(SmgShot.Frame, handle));
        await EvidenceRetestWire.CastBarAsync(actor, Covering, SmgShot.CastSeconds);
        await MoveAsync(blocked: true);
        await EvidenceRetestWire.TimerAsync(actor, startedAt, Covering, AbilityCooldowns.OwnTimer, 8);
        await actor.WaitUntilAsync(() => server.DebugLog()[fizzleAt..].Contains("[ABILITY] 176 fizzles:", StringComparison.Ordinal),
            "An already admitted Covering Fire fizzles when sight is lost", HeadlessPlayer.DefaultTimeout);
        string attempt = server.DebugLog()[fizzleAt..];
        Check.That(!attempt.Contains("[ABILITY] 176 lands", StringComparison.Ordinal) &&
            !attempt.Contains("[EFFECTS] 176", StringComparison.Ordinal),
            "Covering Fire does not apply damage or a root after sight is lost");

        Check.Equal(0, actor.World.Problems.Count, "Gun sight protocol framing");
        return Array.Empty<byte>();

        async Task MoveAsync(bool blocked)
        {
            actor.SkipReceived();
            actor.SendChat(blocked ? "?cd foundations-sight-blocked" : "?cd foundations-sight-pick");
            await actor.WaitForTextAsync(blocked ? "QA Sight Target is 6 m west" : "Select QA Sight Target 2 m ahead");
        }

        async Task SelectAsync(ushort style, string capture)
        {
            actor.SkipReceived();
            actor.SendRpc(WithHandle(capture, handle));
            await actor.WaitForViewUpdateAsync(2, $"02 80 80 80 80 80 10 {StockAbilityProfile.LittleEndianHex(style)} 00 00");
        }

        async Task LandAsync((ushort Id, string Frame, int CastSeconds) shot)
        {
            int rpcAt = actor.World.RpcSnapshot().Count;
            int logAt = server.DebugLog().Length;
            actor.SendRpc(WithHandle(shot.Frame, handle));
            await EvidenceRetestWire.CastBarAsync(actor, shot.Id, shot.CastSeconds);
            await EvidenceRetestWire.TimerAsync(actor, rpcAt, shot.Id, AbilityCooldowns.OwnTimer, 8);
            await actor.WaitUntilAsync(() => server.DebugLog()[logAt..].Contains($"[ABILITY] {shot.Id} lands on view {target.ViewId}"),
                $"Shot {shot.Id} lands on the same target with sight restored", HeadlessPlayer.DefaultTimeout);
            if (AbilityCooldowns.TryGet(shot.Id, out var cooldown) && cooldown.Group != 0)
                await EvidenceRetestWire.TimerAsync(actor, rpcAt, cooldown.Group, AbilityCooldowns.GroupTimer, cooldown.GroupSeconds, ended: true);
        }
    }
}
