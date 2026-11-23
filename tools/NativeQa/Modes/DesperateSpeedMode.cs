using HeadlessClient;
using hds;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class DesperateSpeedMode : NativeQaMode
{
    private const ushort Speed = 12, DesperateSpeed = 291, Sneak = 1111, Invisibility = 1116;
    private const int Level = 50, X = 17000, Y = 495, Z = 6400, ObserverX = 17400, LaneEnd = 8300;
    private const string Home = "?tp 17000 495 6400 4e2 12 4 - 00";
    // Complete retained dismissal, PacketsZombiesPortingEtc lines 81076–81119.
    private const string SpeedOff = "80 bb 0c 00";
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, Speed, DesperateSpeed, 151, 175, CombatStyles.SpyStyle)
        .Order().ToArray();
    private static readonly int Memory = Loadout.Sum(AbilityMemory.Cost);

    public override string Name => "ability-parity-desperate-speed";

    public override NativeQaModePlan Plan { get; } = new(
        Loadout, X, Z, Array.Empty<string>(), new[]
        {
            "DELETE FROM inventory WHERE charId=110 AND (slot IN (40,107) OR goid=460)",
            "INSERT INTO inventory(charId,goid,slot,count,purity) VALUES(110,460,40,1,0)"
        }, new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Z},rotation=0," +
                "healthC=3000,healthM=3000,innerStrC=1000,innerStrM=1000,alignment=1,pvpflag=0 WHERE charId=110",
            $"UPDATE characters SET x={ObserverX},y={Y},z={Z},alignment=1,pvpflag=0 WHERE charId=6111"
        }, DevCommands: true,
        hotbarAbilities: new[] { CombatStyles.SpyStyle, DesperateSpeed, Sneak },
        hotbarInventory: new AbilitySweepHotbar.InventoryShortcut[] { new(40, 460, "Invisibility Belt") },
        testMobFixture: "martial-player-controls.csv",
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1"
        }, extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "291", ["NativeCloseableRows"] = 1,
            ["LoadedMemory"] = Memory, ["MemoryCapacity"] = AbilityMemory.ForLevel(Level)
        },
        readyMessages: new[]
        {
            $"Legal L{Level} profile {Memory}/{AbilityMemory.ForLevel(Level)} memory. Verified clear outdoor lane ({X},{Y},{Z}) to ({X},{Y},{LaneEnd}), heading +Z. QAFriend ({ObserverX},{Y},{Z}) is the stationary friendly observer. No teleport is needed to start. Type {Home} between the baseline runs and before the timed run. No NPC selection is needed. If an NPC appears behind CSR, strafe, turn the camera and single-click its name.",
            "Page 2 is Spy Style, Desperate Speed, Sneak and Invisibility Belt. Choose Spy Style. From HOME, hold forward about one second in each baseline stance, keeping the same run setting and heading. Start normal, then use slot 3 for Sneak, then turn Sneak off and use slot 4 for Belt. Wait for its real 3 s cast before moving. Record first/last positions and packet times for all three baselines. Wait out Belt's 30 s reuse and toggle it off. Return HOME.",
            "Timed run from HOME. Press slot 2 and save the complete stock 80 b9 23 01 request. Expect 50 IS, 298 s own reuse, [BUFF] 291 on for 15 s and [MODIFIER] source=291 stat 115 +40, 877 +20 and 878 +20 for 15 s. Hold forward about one second, release, press slot 3, briefly settle into Sneak, hold forward about one second and release. Press slot 3 again to leave Sneak, then slot 4 and wait for Belt to land. Briefly settle after Invisibility lands, then hold forward about one second. Stay in the same lane without teleporting during this 15 s sequence.",
            "Own and QAFriend MovementScale fields identify the phase transitions: +0.40 normal, +0.20 Sneak, +0.40 after leaving Sneak, +0.20 Belt and zero at 291 expiry. Actual stock movement is the forward z displacement divided by its packet time, compared with the matching unbuffed gait. Expect approximately 1.40/1.20/1.20 of those baselines. Keep the Belt active through expiry and confirm all three 291 removals.",
            "If a run starts before its stance update or after expiry, wait for ordinary 291 reuse. Return HOME with all stances off, use slot 2 and enter only the missed stance before repeating its movement sample. For a missed Belt run, let its 30 s reuse finish, turn any active Belt off, and wait for its new cast to land."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        var lane = Enumerable.Range(0, (LaneEnd - Z) / 100 + 1).Select(step => (X, Z + 100 * step));
        foreach (var (x, z) in lane.Append((ObserverX, Z)))
            Check.That(NavMesh.Locate(1, x, Y, z, out var stand) && !stand.indoors && stand.floorY + 95 == Y &&
                NavMesh.HasSightPath(1, X, Y, Z, x, Y, z), $"Verified outdoor stance sample ({x},{Y},{z})");
        Check.That(NavMesh.Locate(1, X, Y, Z, out var home) && home.sector == 0x4e2 && home.region == 0x12 && home.polygon == 4,
            "The printed return uses the verified HOME sector, region and polygon");

        var server = context.ActualServer!;
        var observer = await HeadlessPlayer.LoginAsync(context.Address, "QAFriend", "QAFriend", stockCharacterLoad: true);
        context.Helpers.Add(observer.Handle, observer);
        await observer.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, observer);
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        var ranks = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor), row => row.Level);
        Check.That(Memory <= AbilityMemory.ForLevel(Level) && ranks.Keys.ToHashSet().SetEquals(Loadout) &&
            Loadout.All(id => AbilityPrerequisites.Current.Closure(id).All(need => ranks.TryGetValue(need.Key, out ushort rank) && rank >= need.Value)),
            "Desperate Speed and the two stock stances have legal ranked Archive prerequisites");
        Check.Equal(1L, await context.OwnedDatabase!.LongAsync("SELECT COUNT(*) FROM inventory WHERE charId=110 AND goid=460 AND slot=40"),
            "The retained Belt action uses the seeded tool slot");

        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        var view = await observer.WaitForViewNamedAsync(actor.Handle);
        actor.SendRpc(AwakenedStockStealthSpeedMode.SpyUse);
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 58 04 00 00");

        // These retained stock controls exercise the same live view path. The first 291 request is reserved for native.
        await CheckScaleAsync(AwakenedStockStealthSpeedMode.SpeedUse, .45f, "02 88 05 8c 1e 66 66 e6 3e");
        int stance = actor.World.RpcSnapshot().Count;
        await CheckScaleAsync(AwakenedStockStealthSpeedMode.SneakUse, .12f);
        await StockAbilityProfile.ObserveAsync(actor, stance, frame => StockAbilityProfile.Bonus(frame, Sneak, 115, -200, 0, false),
            "Retained Sneak action grants the real stance");
        await CheckScaleAsync(AwakenedStockStealthSpeedMode.SneakUse, .45f);
        stance = actor.World.RpcSnapshot().Count;
        await CheckScaleAsync(AwakenedStockStealthSpeedMode.BeltUse, .12f);
        await StockAbilityProfile.ObserveAsync(actor, stance, frame => StockAbilityProfile.Bonus(frame, Invisibility, 115, -300, 130, false),
            "Retained Belt action grants the real invisible stance");
        await CheckScaleAsync(SpeedOff, 0f, WireFormat.ToHex(AbilityBuffs.BuildMovementUpdate(0f, Stealth.Invisible)[2..]));
        Check.Equal(0, actor.World.Problems.Count + observer.World.Problems.Count, "Stock movement and stance framing");
        Console.WriteLine("DESPERATE SPEED BOOTSTRAP PASS: verified stands and retained Spy, Hyper-Speed, Sneak, Belt and dismissal. 291 is reserved for native.");
        return Array.Empty<byte>();

        async Task CheckScaleAsync(string request, float expected, string? selfUpdate = null)
        {
            int ownAt = actor.World.ViewSnapshot().Count, observerAt = observer.World.ViewSnapshot().Count;
            actor.SendRpc(request);
            await EvidenceRetestWire.ViewSinceAsync(actor, ownAt, 2, selfUpdate ?? WireFormat.ToHex(PlayerViewAttributes.Build(
                (PlayerViewAttributes.SelfMovementScale, PlayerViewAttributes.F32(expected)))));
            await EvidenceRetestWire.ViewSinceAsync(observer, observerAt, view.ViewId, WireFormat.ToHex(PlayerViewAttributes.Build(
                (PlayerViewAttributes.OtherMovementScale, PlayerViewAttributes.F32(expected)))));
        }
    }
}
