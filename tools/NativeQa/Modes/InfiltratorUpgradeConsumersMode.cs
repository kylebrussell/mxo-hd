using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class InfiltratorUpgradeConsumersMode : NativeQaMode
{
    private const ushort Parent = 131, Upgrade = 1115, Mask = 1114, Sneak = 1111;
    private const int Level = 50, X = 17000, Y = 495, Z = 6400, LaneEnd = 8300, ObserverX = 17400;
    private const string Home = "?tp 17000 495 6400 4e2 12 4 - 00";
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, Parent, 120, 151, CombatStyles.SpyStyle)
        .Order().ToArray();
    private static readonly int Memory = Loadout.Sum(AbilityMemory.Cost);

    public override string Name => "ability-parity-infiltrator-upgrade-consumers";
    public override NativeQaModePlan Plan { get; } = new(
        Loadout, X, Z, Array.Empty<string>(),
        new[]
        {
            "DELETE FROM inventory WHERE charId=110 AND (slot IN (40,107) OR goid=1268)",
            "INSERT INTO inventory(charId,goid,slot,count,purity) VALUES(110,1268,40,1,0)"
        },
        new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Z}," +
                "rotation=0,healthC=4000,healthM=4000,innerStrC=1000,innerStrM=1000,alignment=1,pvpflag=0 WHERE charId=110",
            $"UPDATE characters SET x={ObserverX},y={Y},z={Z},alignment=1,pvpflag=0 WHERE charId=6111"
        }, DevCommands: true,
        hotbarAbilities: new ushort[] { CombatStyles.SpyStyle, Upgrade, Sneak },
        hotbarInventory: new AbilitySweepHotbar.InventoryShortcut[] { new(40, 1268, "Disguise Mask") },
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", [MovementSampleQa.Mode] = "1",
            ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1"
        }, extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "1115", ["NativeCloseableRows"] = 1,
            ["LoadedMemory"] = Memory, ["MemoryCapacity"] = AbilityMemory.ForLevel(Level)
        }, readyMessages: new[]
        {
            $"Level {Level}, rank 50, {Memory}/{AbilityMemory.ForLevel(Level)} memory. Page 2 slots 1–4: Spy Style, Infiltrator Upgrade, Sneak, Disguise Mask. If the mask shortcut is unresponsive, use Actions > Tools > Disguise Mask. CSR starts facing +Z at clear HOME ({X},{Y},{Z}); same-org QAFriend is at ({ObserverX},{Y},{Z}). No selected target, combat, or RNG aid. If displaced, return with {Home}.",
            "Baseline: select Spy Style, run ?cd totals and save the thrown total. Use the mask and confirm the 2 s cast, SWAT model 1308623188, and [STEALTH] 1114 on for 56 s. Press the mask again to end it. Confirm run gait with the stock run toggle before movement sampling, then use Sneak. Hold forward about 3 s in the established Sneak gait and stop inside the verified lane at z=8300. Compare steady [MOVE-SAMPLE] dz/ms only; skip startup and release. Turn Sneak off and return HOME.",
            "Use Infiltrator Upgrade at rest. Capture 20 IS, 30 s own reuse and [MODIFIER] source=1115 stat 1009 +3, 1032 +3, 639 +3, 877 +10. Repeat ?cd totals for thrown +3 versus the baseline, with no melee attack or ballistic attack change. Use the mask for 59 s with the same model. Toggle the mask off, return HOME, confirm run gait again, then activate Sneak and repeat the matched hold. Expect own and observer scale 0.35 (0.25 from loaded Disguise and 0.10 from the upgrade), about 1.08x the baseline Sneak gait.",
            "With Sneak running, toggle the upgrade off with page 2 slot 2. Confirm all four 1115 removals, no new reuse, same Sneak still active, and own and observer movement scales back to 0.25. Return HOME for the last matched sample; speed returns near the original Sneak gait. Finish by toggling Sneak off. The stock client previously confirmed toggle and grant lifecycle. No live melee result is required; focused tests run the real melee defense and Thrown Accuracy consumers."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        var lane = Enumerable.Range(0, (LaneEnd - Z) / 100 + 1).Select(step => (X, Z + 100 * step));
        foreach (var (x, z) in lane.Append((ObserverX, Z)))
            Check.That(NavMesh.Locate(1, x, Y, z, out var stand) && !stand.indoors && stand.floorY + 95 == Y &&
                NavMesh.HasSightPath(1, X, Y, Z, x, Y, z), $"Verified clear Infiltrator stand ({x},{Y},{z})");
        Check.That(NavMesh.Locate(1, X, Y, Z, out var home) && (home.sector, home.region, home.polygon) == (0x4e2, 0x12, 4),
            "Printed return agrees with outdoor NAV");

        var observer = await HeadlessPlayer.LoginAsync(context.Address, "QAFriend", "QAFriend", stockCharacterLoad: true);
        context.Helpers.Add(observer.Handle, observer);
        await observer.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, observer);
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        var ranks = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor), row => row.Level);
        Check.That(Memory <= AbilityMemory.ForLevel(Level) && ranks.Keys.ToHashSet().SetEquals(Loadout) &&
            Loadout.All(id => AbilityPrerequisites.Current.Closure(id).All(need => ranks.TryGetValue(need.Key, out ushort rank) && rank >= need.Value)),
            "Ranked Infiltrator, Sneak and Disguise fit the legal Archive");
        Check.Equal(1L, await context.OwnedDatabase!.LongAsync("SELECT COUNT(*) FROM inventory WHERE charId=110 AND goid=1268 AND slot=40"),
            "Stock mask request uses the seeded tool");
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, actor);
        await StockAbilityProfile.ObserveAsync(actor, 0, StockAbilityProfile.GrantRecord(Upgrade, remove: false),
            "Infiltrator Upgrade appears through ordinary selected grant readiness");
        await observer.WaitForViewNamedAsync(actor.Handle);
        actor.SendRpc(Convert.FromHexString(AwakenedStockStealthSpeedMode.SpyUse));
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 58 04 00 00");
        int start = actor.World.RpcSnapshot().Count;
        actor.SendRpc(Convert.FromHexString(AwakenedDetectionDisguiseMode.MaskUse));
        await StockAbilityProfile.ObserveAsync(actor, start, frame => StockAbilityProfile.Bonus(frame, Mask, 881, 0, 56, false),
            "Retained stock mask produces actual 56 s baseline");
        actor.SendRpc(Convert.FromHexString(AwakenedDetectionDisguiseMode.MaskUse));
        await StockAbilityProfile.ObserveAsync(actor, start, frame => StockAbilityProfile.Bonus(frame, Mask, 881, 0, 56, true),
            "Retained stock mask toggles off");
        actor.SendRpc(Convert.FromHexString(AwakenedStockStealthSpeedMode.SneakUse));
        await StockAbilityProfile.ObserveAsync(actor, start, frame => StockAbilityProfile.Bonus(frame, Sneak, 115, -200, 0, false),
            "Retained stock Sneak reaches its ordinary baseline");
        Check.Equal(0, actor.World.Problems.Count + observer.World.Problems.Count, "Stock framing");
        Console.WriteLine($"INFILTRATOR UPGRADE BOOTSTRAP PASS: legal {Memory}/100, verified NAV and retained Spy, Mask and Sneak. 1115 reserved for native.");
        return Array.Empty<byte>();
    }
}
