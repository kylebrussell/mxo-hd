using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class MasterShadowStockLifecycleMode : NativeQaMode
{
    private const ushort Parent = 367, Shadow = 1118, Aura = 61;
    private const int Level = 50, X = 17000, Y = 495, Z = 6400, FriendX = 17400, EnemyZ = 7201;
    private const string Home = "?tp 17000 495 6400 4e2 12 4 - 00";
    private static readonly ushort[] Loadout = DisablingShotMode.Closure(2, 17, Parent, CombatStyles.SpyStyle,
        CombatStyles.HackerStyle, Aura, 67).Order().ToArray();

    public override string Name => "ability-parity-master-shadow-stock-lifecycle";
    public override NativeQaModePlan Plan { get; } = new(
        Loadout, X, Z, Array.Empty<string>(), new[]
        {
            "DELETE FROM inventory WHERE charId=110 AND slot IN (40,107)",
            "INSERT INTO inventory(charId,goid,slot,count,purity) VALUES(110,2399,40,1,0)"
        }, new[]
        {
            PvpOpponentsFixture.NativeSql,
            $"UPDATE characters SET x={X},y={Y},z={Z},rotation=0,healthC=3000,healthM=5000,innerStrC=100,innerStrM=100 WHERE charId=110",
            $"UPDATE characters SET x={FriendX},y={Y},z={Z},rotation=192 WHERE charId=6111",
            $"UPDATE characters SET x={X},y={Y},z={EnemyZ},rotation=128 WHERE charId=6114"
        }, DevCommands: true, testMobFixture: "martial-player-controls.csv",
        hotbarAbilities: new ushort[] { CombatStyles.HackerStyle, Aura, CombatStyles.SpyStyle, Shadow },
        hotbarInventory: new AbilitySweepHotbar.InventoryShortcut[] { new(40, 2399, "Health Pill 1.0") },
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1"
        }, extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "1118", ["NativeCloseableRows"] = 1,
            ["LoadedMemory"] = Loadout.Sum(AbilityMemory.Cost), ["MemoryCapacity"] = AbilityMemory.ForLevel(Level)
        }, readyMessages: new[]
        {
            $"HOME {Home} if displaced. CSR starts north on verified NAV, QAFriend 4 m east and QAEnemy 8.01 m north. No selection, rolls or combat. Page 2 Hacker / Miasma / Spy / Master Shadow / Health Pill 1.0. If the pill shortcut shows only a tooltip, use bag slot 40 right-click Activate Item.",
            "Clear selection. Unload 367 at rest with ?qaunload 367, try the active Shadow slot, then ?qaload 367. Expect a local unloaded gate or reason 7, and one restored action. Select Hacker, start Miasma, then Spy and Master Shadow slot 4. Preserve the first complete 80 b9 5e 04 frame. Expect 1.5 s cast, 12 IS, no activation reuse, source 1118/681 +25 for 180 s, bit 0x10, and Evading flag 0x00080000 without a shield meter. Both ally and opponent keep the view. [AURA] 61 off.",
            "While Shadow is active and before the third drain, attempt the Health Pill. Expect no cast bar or item removal. Accept stock suppression or one System refusal. Watch [SHADOW DRAIN] take exactly 25 every 5 s when enough IS remains. When below 25, the next tick ends Shadow without taking the balance. Expect one 30 s reuse at term, source 681 removal, bit and Evading flag clear, FX 28000a48 on the real owner and observer, and no later Shadow drain. Use the same Health Pill from rest after term and confirm its normal cast, heal and one bag removal. Miasma stays off."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (var (x, z) in new[] { (X, Z), (FriendX, Z), (X, EnemyZ) })
            Check.That(NavMesh.Locate(1, x, Y, z, out var stand) && stand.floorY + 95 == Y && !stand.indoors &&
                NavMesh.HasSightPath(1, X, Y, Z, x, Y, z), $"Clear Master Shadow stand ({x},{Y},{z})");
        Check.That(NavMesh.Locate(1, X, Y, Z, out var home) && (home.sector, home.region, home.polygon) == (0x4e2, 0x12, 4),
            "Printed return matches actual stand");
        foreach (string name in new[] { "QAEnemy", "QAFriend" })
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, name, name, stockCharacterLoad: true);
            context.Helpers.Add(name, helper);
            await PvpOpponentsFixture.EnterAsync(helper);
            NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, helper);
        }
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        DisablingShotMode.CheckProfile(actor, Loadout, Level);
        await PvpOpponentsFixture.EnterAsync(actor);
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, actor);
        await StockAbilityProfile.ObserveAsync(actor, 0, StockAbilityProfile.GrantRecord(Shadow, false), "Real zero-rank Master Shadow grant");
        foreach (var helper in context.Helpers.Values)
            await helper.WaitForViewNamedAsync(actor.Handle);
        int at = actor.World.RpcSnapshot().Count;
        actor.SendRpc(Convert.FromHexString(StarterHackerViralSightMode.StockStyle));
        actor.SendRpc("80b93d00000000000000000000000000000000000000000000000000ffff7f7f00000000086b0000000000");
        await StockAbilityProfile.ObserveAsync(actor, at,
            frame => frame.Bytes.AsSpan().SequenceEqual(AbilityBuffs.BuildExclusive(Aura, true)), "Retained actual Miasma request enables the control aura");
        actor.SendRpc(Convert.FromHexString(AwakenedStockStealthSpeedMode.SpyUse));
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 58 04 00 00");
        Check.Equal(1L, await context.OwnedDatabase!.LongAsync("SELECT COUNT(*) FROM inventory WHERE charId=110 AND slot=40 AND goid=2399"),
            "Real Health Pill is seeded in its printed bag slot");
        Check.Equal(0, actor.World.Problems.Count + context.Helpers.Values.Sum(helper => helper.World.Problems.Count), "Stock framing");
        Console.WriteLine("MASTER SHADOW BOOTSTRAP PASS: legal Archive, both observer roles, real grant and retained stock Hacker/Miasma/Spy. First 1118 reserved for native.");
        return Array.Empty<byte>();
    }
}
