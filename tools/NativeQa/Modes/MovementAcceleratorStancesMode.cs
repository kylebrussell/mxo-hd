using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class MovementAcceleratorStancesMode : NativeQaMode
{
    private const int Level = 50, X = 17000, Y = 495, Z = 6400, FriendX = 17400;
    private const ushort Speed = 12, Move1 = 62, Move2 = 323, Move3 = 423, Sneak = 1111, Invisibility = 1116;
    // Stock PR658, including the unselected target fields.
    private const string SpeedRequest = "80b90c00000000000000000000000000000000000000000000000000000015000000071c086b0000000000";
    private static readonly ushort[] Loadout = new ushort[] { 2, 17, Speed, Move2, Move3, 151, 175, CombatStyles.SpyStyle, CombatStyles.HackerStyle }
        .SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys.Append(id)).Distinct().Order().ToArray();

    public override string Name => "ability-parity-movement-accelerator-stances";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), new[]
    {
        "DELETE FROM inventory WHERE charId=110 AND (slot=40 OR goid=460)",
        "INSERT INTO inventory(charId,goid,slot,count,purity) VALUES(110,460,40,1,0)"
    }, new[]
    {
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Z},rotation=0,innerStrC=1000,innerStrM=1000,alignment=1,pvpflag=0 WHERE charId=110",
        $"UPDATE characters SET x={FriendX},y={Y},z={Z},alignment=1,pvpflag=0 WHERE charId=6111",
        $"UPDATE char_abilities SET level={Level} WHERE char_id=110"
    }, DevCommands: true,
        hotbarAbilities: new ushort[] { CombatStyles.HackerStyle, Move1, Move2, Move3, CombatStyles.SpyStyle, Sneak, Speed },
        hotbarInventory: new AbilitySweepHotbar.InventoryShortcut[] { new(40, 460, "Invisibility Belt") },
        testMobFixture: "martial-player-controls.csv", environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "12,323,423", ["NativeCloseableRows"] = 3 },
        readyMessages: new[]
        {
            $"Full ranked profile {Loadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} memory at L{Level}; both stance grants and all accelerator ranks. CSR ({X},{Y},{Z}) faces the +Z outdoor lane through z=8000. QAFriend ({FriendX},{Y},{Z}) stands 4 m east. HOME is ?tp 17000 495 6400 4e2 12 4 - 00.",
            "Page 2 is Hacker, Accelerator 1/2/3, Spy, Sneak, Hyper-Speed and Belt. Take a two-second forward run in normal, Sneak and Belt with no movement buffs. Use the same HOME, heading and run setting each time. After the Belt's 30 s reuse, press Belt again to toggle it off. Confirm [STEALTH] 1116 off before the next cast.",
            "Hacker then /target QAFriend and slot 3: Movement Accelerator 2.0 costs 40 IS with 2 s cast and 8 s reuse. Only QAFriend receives [MODIFIER] source=323 stats 115 +25, 877/878 +5 for 720 s and own/observer scale +0.25. Slot 4 on QAFriend costs 50 IS with 10 s reuse, removes all three 323 records and adds 423: 115 +45, 877/878 +10 for 960 s and scale +0.45. Ask helper to ?dismissbuff 423; all three remove and scale returns to zero. Capture each full first 323/423 RPC.",
            "Target CSR_Tester and cast 323 on self after its reuse. Run normally at +0.25; then Spy and Sneak, run at +0.05 versus the no-buff Sneak baseline. Sneak off restores +0.25. Belt shows +0.05 versus its own baseline and its end restores +0.25. Dismiss 323 to clear the field. Repeat with 423 (normal +0.45, each stance +0.10).",
            "Clear selection. Hyper-Speed (slot 7) costs 40 IS and sends [MODIFIER] source=12 stat 115 +45, 877/878 +12 for 600 s; normal +0.45. Repeat Sneak and Belt at scale +0.12, then dismiss 12 to see all records removed and zero. Expected scaled distance uses each stance's own no-buff baseline. No max [VITALS] or [PVP] rounds."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Accelerator stances need workspace NAV.");
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        var lane = Enumerable.Range(0, 17).Select(offset => (X, Z + offset * 100));
        foreach (var (x, z) in lane.Append((FriendX, Z)))
            Check.That(NavMesh.Locate(1, x, Y, z, out var stand) && !stand.indoors &&
                Math.Abs(stand.floorY + 95 - Y) < 1 && NavMesh.HasSightPath(1, X, Y, Z, x, Y, z),
                $"Verified accelerator outdoor stand ({x},{Y},{z})");
        Check.That(NavMesh.Locate(1, X, Y, Z, out var home) && home.sector == 0x4e2 && home.region == 0x12 && home.polygon == 4,
            "The movement reset uses the actual home polygon");

        var observer = await HeadlessPlayer.LoginAsync(context.Address, "QAFriend", "QAFriend", stockCharacterLoad: true);
        context.Helpers.Add(observer.Handle, observer);
        await observer.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, observer);
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        var loaded = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor), row => row.Level);
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level) && Loadout.All(id => loaded.ContainsKey(id) &&
            AbilityPrerequisites.Current.Closure(id).All(need => loaded.TryGetValue(need.Key, out ushort rank) && rank >= need.Value)),
            "The full accelerator loadout fits in memory and includes the ranked prerequisite closure");
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, actor);
        var view = await observer.WaitForViewNamedAsync(actor.Handle);
        actor.SendRpc(AwakenedStockStealthSpeedMode.SpyUse);
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 58 04 00 00");
        await CheckScaleAsync(SpeedRequest, .45f, "02 88 05 8c 1e 66 66 e6 3e");
        await CheckScaleAsync(AwakenedStockStealthSpeedMode.SneakUse, .12f);
        await StockAbilityProfile.ObserveAsync(actor, 0, packet => StockAbilityProfile.Bonus(packet, Sneak, 115, -200, 0, false), "Stock Sneak starts");
        await CheckScaleAsync(AwakenedStockStealthSpeedMode.SneakUse, .45f);
        await CheckScaleAsync(AwakenedStockStealthSpeedMode.BeltUse, .12f);
        await StockAbilityProfile.ObserveAsync(actor, 0, packet => StockAbilityProfile.Bonus(packet, Invisibility, 115, -300, 130, false), "Stock Invisibility starts");
        // Retail PacketsZombiesPortingEtc, lines 81076–81119, captured dismiss.
        await CheckScaleAsync("80 bb 0c 00", 0f, WireFormat.ToHex(AbilityBuffs.BuildMovementUpdate(0f, Stealth.Invisible)[2..]));
        Check.Equal(0, actor.World.Problems.Count + observer.World.Problems.Count, "Stock speed and stance framing");
        return Array.Empty<byte>();

        async Task CheckScaleAsync(string request, float scale, string? selfUpdate = null)
        {
            int ownAt = actor.World.ViewSnapshot().Count, observerAt = observer.World.ViewSnapshot().Count;
            actor.SendRpc(request);
            await EvidenceRetestWire.ViewSinceAsync(actor, ownAt, 2, selfUpdate ?? WireFormat.ToHex(PlayerViewAttributes.Build(
                (PlayerViewAttributes.SelfMovementScale, PlayerViewAttributes.F32(scale)))));
            await EvidenceRetestWire.ViewSinceAsync(observer, observerAt, view.ViewId, WireFormat.ToHex(PlayerViewAttributes.Build(
                (PlayerViewAttributes.OtherMovementScale, PlayerViewAttributes.F32(scale)))));
        }
    }
}
