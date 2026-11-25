using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class SneakMovementSpeedBonusMode : NativeQaMode
{
    private const ushort Parent = 151, SneakGrant = Stealth.SneakGrantAbility, Spy = CombatStyles.SpyStyle, Speed = 12;
    private const int X = 17000, Y = 495, Z = 6400, LaneEnd = 8300, Level = 50;
    private const string Home = "?tp 17000 495 6400 4e2 12 4 - 00";
    private static readonly ushort[] Loadout = DisablingShotMode.Closure(2, 17, Spy, Parent, Speed).Order().ToArray();

    public override string Name => "ability-parity-sneak-movement-speed";

    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), Array.Empty<string>(), new[]
    {
        $"UPDATE char_abilities SET level={Level} WHERE char_id=110",
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Z},rotation=0,alignment=1,innerStrC=1000,innerStrM=1000 WHERE charId=110",
        $"UPDATE characters SET x=17400,y={Y},z={Z},alignment=1 WHERE charId=6111"
    }, DevCommands: true, hotbarAbilities: new[] { Spy, SneakGrant, Speed },
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", [MovementSampleQa.Mode] = "1"
        }, extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "151", ["NativeCloseableRows"] = 1, ["MemoryUsed"] = Loadout.Sum(AbilityMemory.Cost)
        },
        readyMessages: new[]
        {
            $"Level 50, rank 50 Sneak parent (151), memory {Loadout.Sum(AbilityMemory.Cost)}/100 (no 120 Disguise or 175 Invisibility loaded). Start on verified outdoor HOME ({X},{Y},{Z}) facing +Z; {Home} to reset before each run. Friendly observer QAFriend 4 m east at (17400,{Y},{Z}). Page 2: 1=Spy Style (1112), 2=Sneak (1111), 3=Hyper-Speed (12).",
            $"Step 1 (Login A1, 151 loaded): Confirm permanent source=151 stats 998 +10, 1027 +5, 1143 +5, 1047 +5 and grant 1111. Self and QAFriend MovementScale = 0.10 right at login. Hold forward ~2 s along clear lane z={Z}–{LaneEnd} and save [MOVE-SAMPLE] dz/ms (A1, expected ~1.10x unloaded). Return HOME ({Home}).",
            "Step 2 (Unload B): Type '?qaunload 151'. Confirm source=151 stats and grant 1111 removed, and self + QAFriend MovementScale drops live 0.10 -> 0.00. Take matched ~2 s forward sample (B, 1.00x baseline). Return HOME.",
            "Step 3 (Reload A2): Type '?qaload 151'. Confirm source=151 stats and grant 1111 restored, and self + QAFriend MovementScale rises live 0.00 -> 0.10. Take matched ~2 s forward sample (A2, repeatable ~1.10x vs B). Return HOME.",
            "Step 4 (Hyper-Speed 12 stack + live reload): Press slot 3 (Hyper-Speed 12, 40 IS, 600 s). Expect self + QAFriend MovementScale = 0.55 (0.10 from 998 + 0.45 from 12). Type '?qaunload 151' -> scale drops live 0.55 -> 0.45 while Hyper-Speed stays active. Type '?qaload 151' -> scale rises live 0.45 -> 0.55. Type '?dismissbuff 12' -> scale returns 0.55 -> 0.10 (NOT 0.00).",
            "Step 5 (Sneak channel isolation): Press slot 1 (Spy Style), then slot 2 (Sneak 1111). Expect MovementScale drops 0.10 -> 0.00 (998 is linked to Run 115, not Sneak 877). Toggle slot 2 (Sneak 1111) off -> MovementScale restores 0.00 -> 0.10."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Sneak speed mode requires NAV.");
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        Check.Equal(Home, InvisibilityBeltWatcherMode.Warp(X, Y, Z, 0, inside: false), "Printed HOME");
        InvisibilityBeltWatcherMode.Warp(17400, Y, Z, 192, inside: false);
        for (int z = Z; z <= LaneEnd; z += 100)
        {
            InvisibilityBeltWatcherMode.Warp(X, Y, z, 0, inside: false);
            Check.That(NavMesh.HasSightPath(1, X, Y, Z, X, Y, z), "Clear Sneak speed sampling lane");
        }
        var observer = await HeadlessPlayer.LoginAsync(context.Address, "QAFriend", "QAFriend", stockCharacterLoad: true);
        context.Helpers.Add(observer.Handle, observer);
        await observer.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, observer);
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        DisablingShotMode.CheckProfile(actor, Loadout, Level);
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, actor);
        var observedView = await observer.WaitForViewNamedAsync(actor.Handle);

        // Step 1: Login baseline (151 loaded -> stats + grant 1111 + self & observer MovementScale 0.10).
        await ObservePassivesAsync(0, remove: false);
        await SelfScaleAsync(0, .10f);
        Check.That(observedView.Attributes.TryGetValue("MovementScale", out var initialObsScale) &&
            Math.Abs(BitConverter.ToSingle(initialObsScale) - .10f) < .001f,
            "Observer initial creation view receives MovementScale 0.10 from loaded 151");

        // Step 2: Unload 151 -> stats/grant removed and MovementScale 0.10 -> 0.00 on self & observer.
        int rpcAt = actor.World.RpcSnapshot().Count;
        int selfViewAt = actor.World.ViewSnapshot().Count;
        int obsViewAt = observer.World.ViewSnapshot().Count;
        actor.SendChat("?qaunload 151");
        await actor.WaitForTextAsync("unload 151");
        await ObservePassivesAsync(rpcAt, remove: true);
        await SelfScaleAsync(selfViewAt, 0f);
        await ObserverScaleAsync(obsViewAt, observedView.ViewId, 0f);

        // Step 3: Reload 151 -> stats/grant restored and MovementScale 0.00 -> 0.10 on self & observer.
        rpcAt = actor.World.RpcSnapshot().Count;
        selfViewAt = actor.World.ViewSnapshot().Count;
        obsViewAt = observer.World.ViewSnapshot().Count;
        actor.SendChat("?qaload 151");
        await actor.WaitForTextAsync("load 151");
        await ObservePassivesAsync(rpcAt, remove: false);
        await SelfScaleAsync(selfViewAt, .10f);
        await ObserverScaleAsync(obsViewAt, observedView.ViewId, .10f);

        // Step 4: Hyper-Speed (12) stacking (0.55) + live unload (0.45) + reload (0.55) + dismiss (0.10).
        selfViewAt = actor.World.ViewSnapshot().Count;
        obsViewAt = observer.World.ViewSnapshot().Count;
        actor.SendRpc(AwakenedStockStealthSpeedMode.SpeedUse);
        await EvidenceRetestWire.ViewSinceAsync(actor, selfViewAt, 2,
            $"02 88 05 8c 1e {WireFormat.ToHex(PlayerViewAttributes.F32(.55f))}");
        await ObserverScaleAsync(obsViewAt, observedView.ViewId, .55f);

        selfViewAt = actor.World.ViewSnapshot().Count;
        obsViewAt = observer.World.ViewSnapshot().Count;
        actor.SendChat("?qaunload 151");
        await actor.WaitForTextAsync("unload 151");
        await SelfScaleAsync(selfViewAt, .45f);
        await ObserverScaleAsync(obsViewAt, observedView.ViewId, .45f);

        selfViewAt = actor.World.ViewSnapshot().Count;
        obsViewAt = observer.World.ViewSnapshot().Count;
        actor.SendChat("?qaload 151");
        await actor.WaitForTextAsync("load 151");
        await SelfScaleAsync(selfViewAt, .55f);
        await ObserverScaleAsync(obsViewAt, observedView.ViewId, .55f);

        selfViewAt = actor.World.ViewSnapshot().Count;
        obsViewAt = observer.World.ViewSnapshot().Count;
        actor.SendChat("?dismissbuff 12");
        await EvidenceRetestWire.ViewSinceAsync(actor, selfViewAt, 2,
            WireFormat.ToHex(AbilityBuffs.BuildMovementUpdate(.10f, 0)[2..]));
        await ObserverScaleAsync(obsViewAt, observedView.ViewId, .10f);

        // Step 5: Sneak (1111) channel isolation (0.10 -> 0.00 -> 0.10).
        actor.SendRpc(AwakenedStockStealthSpeedMode.SpyUse);
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 58 04 00 00");
        selfViewAt = actor.World.ViewSnapshot().Count;
        obsViewAt = observer.World.ViewSnapshot().Count;
        actor.SendRpc(AwakenedStockStealthSpeedMode.SneakUse);
        await SelfScaleAsync(selfViewAt, 0f);
        await ObserverScaleAsync(obsViewAt, observedView.ViewId, 0f);

        selfViewAt = actor.World.ViewSnapshot().Count;
        obsViewAt = observer.World.ViewSnapshot().Count;
        actor.SendRpc(AwakenedStockStealthSpeedMode.SneakUse);
        await SelfScaleAsync(selfViewAt, .10f);
        await ObserverScaleAsync(obsViewAt, observedView.ViewId, .10f);

        Check.Equal(0, actor.World.Problems.Count + observer.World.Problems.Count, "Sneak movement speed bonus framing");
        Console.WriteLine("SNEAK MOVEMENT SPEED BONUS PASS: login 0.10, A/B/A 0.10->0.00->0.10, Hyper-Speed 0.55->0.45->0.55->0.10, Sneak isolation 0.00->0.10.");
        return Array.Empty<byte>();

        async Task ObservePassivesAsync(int cursor, bool remove)
        {
            await StockAbilityProfile.ObserveAsync(actor, cursor,
                frame => StockAbilityProfile.Bonus(frame, Parent, 998, 10, 0, remove), $"151 stat 998 +10 (remove={remove})");
            foreach (ushort stat in new ushort[] { 1027, 1143, 1047 })
                await StockAbilityProfile.ObserveAsync(actor, cursor,
                    frame => StockAbilityProfile.Bonus(frame, Parent, stat, 5, 0, remove), $"151 stat {stat} +5 (remove={remove})");
        }

        Task SelfScaleAsync(int cursor, float value) =>
            EvidenceRetestWire.ViewSinceAsync(actor, cursor, 2,
                WireFormat.ToHex(PlayerViewAttributes.Build((PlayerViewAttributes.SelfMovementScale, PlayerViewAttributes.F32(value)))));

        Task ObserverScaleAsync(int cursor, ushort viewId, float value) =>
            EvidenceRetestWire.ViewSinceAsync(observer, cursor, viewId,
                WireFormat.ToHex(PlayerViewAttributes.Build((PlayerViewAttributes.OtherMovementScale, PlayerViewAttributes.F32(value)))));
    }
}
