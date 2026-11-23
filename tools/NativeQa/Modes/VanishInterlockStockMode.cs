using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class VanishInterlockStockMode : NativeQaMode
{
    private const ushort Vanish = Stealth.VanishAbility;
    private const int Level = 50, X = 17000, Y = 495, Z = 6400, EnemyZ = 6600, FriendX = 17400;
    private const string Home = "?tp 17000 495 6400 4e2 12 4 - 00";
    private const string StockSelfDefense = "80b911000400020000000000000000000000000000000000000000000000000000000000086b0000000000";
    private static readonly ushort[] Loadout = DisablingShotMode.Closure(2, 17, 175, 367, CombatStyles.SpyStyle, Vanish).Order().ToArray();

    public override string Name => "ability-parity-vanish-interlock-stock";
    public override NativeQaModePlan Plan { get; } = new(
        Loadout, X, Z, Array.Empty<string>(),
        new[]
        {
            "DELETE FROM inventory WHERE charId=110 AND (slot IN (40,107) OR goid=460)",
            "INSERT INTO inventory(charId,goid,slot,count,purity) VALUES(110,460,40,1,0)"
        }, new[]
        {
            PvpOpponentsFixture.NativeSql,
            $"UPDATE characters SET x={X},y={Y},z={Z},rotation=0,innerStrC=500,innerStrM=500 WHERE charId=110",
            $"UPDATE characters SET x={X},y={Y},z={EnemyZ},rotation=128 WHERE charId=6114",
            $"UPDATE characters SET x={FriendX},y={Y},z={Z},rotation=192 WHERE charId=6111"
        }, DevCommands: true, testMobFixture: "martial-player-controls.csv",
        hotbarAbilities: new ushort[] { CombatStyles.SpyStyle, Vanish },
        hotbarInventory: new AbilitySweepHotbar.InventoryShortcut[] { new(40, 460, "Invisibility Belt") },
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1", [CombatFixtureWeapons.VanishWinMode] = "1"
        }, extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "368", ["NativeCloseableRows"] = 1,
            ["LoadedMemory"] = Loadout.Sum(AbilityMemory.Cost), ["MemoryCapacity"] = AbilityMemory.ForLevel(Level)
        }, readyMessages: new[]
        {
            $"Verified HOME {Home}, facing QAEnemy two metres north and ally four metres east. No teleport needed. If QAEnemy is behind, use /target QAEnemy or strafe and turn, then single-click. Page 2 Spy / Vanish / Belt. No ordinary Withdraw command is used.",
            "At rest select Spy. Press Belt while QAEnemy faces CSR, expect one watcher refusal without IS/timer. Press Vanish at rest, expect client suppression or server interlock-required refusal with no IS/timer. Keep the same actor selected, use right-click Close Combat and wait for Round 0. Reselect Spy if stock switches styles.",
            "Type ?cd vw before the first Vanish press. This modifies only one withdrawal roll for CSR against the current QAEnemy on the disposable fixture. Save the first complete stock 80 b9 70 01 frame. Expect instant queued move, 30 IS, 1800 s own reuse, [VANISH QA] 368 win consumed, 0x100 with no ability damage, and [PVP] CSR_Tester withdraws. With QAEnemy on Speed/Self Defense the visible clip is NinjaVanish 4272 (4271 vs Grab/Block, 4273 vs Power). At about 2 s after victory expect [VANISH] callback and source 1116 -300/881 for 130 s with pose 1a, bit 4, the ally view left alone, and no second IS debit. The opponent who blocked the ordinary Belt is still nearby. Confirm normal movement after teardown and the opponent's view returns when Belt ends. When Belt is active slot 3 is the ordinary off control."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (var (x, z) in new[] { (X, Z), (X, EnemyZ), (FriendX, Z) })
            Check.That(NavMesh.Locate(1, x, Y, z, out var stand) && !stand.indoors && stand.floorY + 95 == Y &&
                NavMesh.HasSightPath(1, X, Y, Z, x, Y, z), $"Clear Vanish stand ({x},{Y},{z})");
        Check.That(NavMesh.Locate(1, X, Y, Z, out var home) && (home.sector, home.region, home.polygon) == (0x4e2, 0x12, 4),
            "Printed return matches outdoor NAV");
        foreach (string name in new[] { "QAEnemy", "QAFriend" })
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, name, name, stockCharacterLoad: true);
            context.Helpers.Add(name, helper);
            await PvpOpponentsFixture.EnterAsync(helper);
            NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, helper);
        }
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        var enemy = context.Helpers["QAEnemy"];
        DisablingShotMode.CheckProfile(actor, Loadout, Level);
        await PvpOpponentsFixture.EnterAsync(actor);
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, actor);
        actor.SendRpc(Convert.FromHexString(AwakenedStockStealthSpeedMode.SpyUse));
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 58 04 00 00");
        await StarterHackerControlTargetMode.RefusedAsync(actor, Stealth.InvisibilityGrantAbility,
            Convert.FromHexString(AwakenedStockStealthSpeedMode.BeltUse), AbilityRefusals.NoClientMessage);
        var view = await actor.WaitForViewNamedAsync(enemy.Handle);
        uint target = DuelPackets.Handle(view.ViewId, view.SpawnId);
        string handle = WireFormat.ToHex(BitConverter.GetBytes(target));
        int at = actor.World.RpcSnapshot().Count;
        actor.World.SendRpcs(StarterHackerControlTargetMode.WithHandle(StockSelfDefense, target), WireFormat.FromHex($"40 {handle} 01 6b"));
        await StockAbilityProfile.ObserveAsync(actor, at, rpc => rpc.Bytes is [0x46, ..] && rpc.Bytes.Length >= 6 && rpc.Bytes[5] == 1,
            "Retained stock Close Combat opens the watched opponent");
        actor.SendChat("?cd vw");
        await actor.WaitForTextAsync("Next Vanish against QAEnemy is armed for one winning withdrawal roll.");
        Check.Equal(1L, await context.OwnedDatabase!.LongAsync("SELECT COUNT(*) FROM inventory WHERE charId=110 AND slot=40 AND goid=460"),
            "Original term has real Invisibility Belt in its stated slot");
        Check.Equal(0, actor.World.Problems.Count + context.Helpers.Values.Sum(helper => helper.World.Problems.Count), "Stock framing");
        Console.WriteLine("VANISH BOOTSTRAP PASS: legal Archive, verified stands, exact Belt refusal and ordinary Close Combat. First 368 stays native.");
        return Array.Empty<byte>();
    }
}
