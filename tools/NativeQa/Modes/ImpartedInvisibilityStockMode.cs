using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class ImpartedInvisibilityStockMode : NativeQaMode
{
    private const ushort Imparted = 629, Belt = 1116, Parent = 175, Detection = 298;
    private const int Level = 50, BeltRank = 30, X = 17000, Y = 495, Z = 6400, FriendX = 17400, EnemyZ = 7201;
    private const string Home = "?tp 17000 495 6400 4e2 12 4 - 00";
    private static readonly ushort[] Loadout = DisablingShotMode.Closure(2, 17, 290, Parent, CombatStyles.SpyStyle,
        CombatStyles.HackerStyle, 61, 67, Imparted).Order().ToArray();
    private static readonly ushort[] EnemyLoadout = DisablingShotMode.Closure(2, 17, Detection);

    public override string Name => "ability-parity-imparted-invisibility-stock";
    public override NativeQaModePlan Plan { get; } = new(
        Loadout, X, Z, Array.Empty<string>(), new[]
        {
            $"UPDATE char_abilities SET level={BeltRank} WHERE char_id=110 AND ability_id={AbilityMessages.AbilityGameObject(Parent)}",
            "DELETE FROM inventory WHERE charId IN (110,6114) AND (slot IN (8,40,107) OR goid IN (460,2388))",
            "INSERT INTO inventory(charId,goid,slot,count,purity) VALUES(110,460,40,1,0),(6114,2388,8,1,0)"
        }, new[]
        {
            PvpOpponentsFixture.NativeSql,
            $"UPDATE characters SET x={X},y={Y},z={Z},rotation=0,innerStrC=500,innerStrM=500 WHERE charId=110",
            $"UPDATE characters SET x={FriendX},y={Y},z={Z},rotation=192 WHERE charId=6111",
            $"UPDATE characters SET x={X},y={Y},z={EnemyZ},rotation=0 WHERE charId=6114",
            "DELETE FROM char_abilities WHERE char_id=6114",
            "INSERT INTO char_abilities (char_id,slot,ability_id,level,is_loaded) VALUES " +
                string.Join(",", EnemyLoadout.Select((id, index) => $"(6114,{index + 400},{AbilityMessages.AbilityGameObject(id)},50,1)"))
        }, DevCommands: true, testMobFixture: "martial-player-controls.csv",
        hotbarAbilities: new ushort[] { CombatStyles.SpyStyle, Imparted, CombatStyles.HackerStyle, 61 },
        hotbarInventory: new AbilitySweepHotbar.InventoryShortcut[] { new(40, 460, "Invisibility Belt") },
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1",
            ["MXO_PVP_MAX_SAFE_LEVEL"] = "15", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "629", ["NativeCloseableRows"] = 1,
            ["LoadedMemory"] = Loadout.Sum(AbilityMemory.Cost), ["MemoryCapacity"] = AbilityMemory.ForLevel(Level) },
        readyMessages: new[]
        {
            $"No target selection, RNG aid or teleport needed. HOME {Home} if displaced. CSR ({X},{Y},{Z}) faces north; friendly QAFriend ({FriendX},{Y},{Z}) and hostile QAEnemy ({X},{Y},{EnemyZ}, facing away) are on clear NAV. Page 2 slots 1Ã¢â‚¬â€œ5 are Spy Style, Impart Invisibility (629), Hacker Style, Miasma, and Invisibility Belt (Actions > Tools fallback).",
            "Select Spy and activate Belt. Expect 1116 bits 4 for 130 s. While Belt is active press 629 with no target. Expect local block or one 'You are already invisible' line, no 629 cast/timer, and the original 1116 lease. After its 30 s reuse, toggle Belt off. Press slot 2 unloaded after ?qaunload 629; expect stock suppression or reason 7. Restore with ?qaload 629.",
            "Select Hacker and start Miasma, then select Spy and press 629. Preserve the first complete 80 b9 75 02 frame. Expect 3 s cast, no IS debit, 30 s own reuse, [STEALTH] CSR_Tester 629 on (bits 4, SL 0, 120 s), and [AURA] 61 off. No source 629 movement or 881 modifier. The opposing QAEnemy loses the view; the ally keeps it. Use /sit and wait without moving. At about 120 s expect exclusive-off, invis bit clear and Stand restored even if seated. Ally retains the view; the enemy gets CSR back.",
            """For an early-reveal repeat, cast 629 again at rest. Send {"actor":"QAEnemy","action":"imparted-detect"} after it lands. QAEnemy casts the retained ordinary 298 from 8.01 m away. Its +200 Detect always beats threshold 170, so expect [DETECTION] QAEnemy -> CSR_Tester ... rank=30 revealed=True, 629 off (revealed by Detection), the reveal FX and the reappearing view. Press slot 2 once more after reuse and toggle it off to verify explicit deactivation (wait out 30 s if stock holds the button); native bit clear, with no second charge."""
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (var (x, z) in new[] { (X, Z), (FriendX, Z), (X, EnemyZ) })
            Check.That(NavMesh.Locate(1, x, Y, z, out var stand) && stand.floorY + 95 == Y && !stand.indoors &&
                NavMesh.HasSightPath(1, X, Y, Z, x, Y, z), $"Clear Impart Invisibility stand ({x},{Y},{z})");
        Check.That(NavMesh.Locate(1, X, Y, Z, out var home) && (home.sector, home.region, home.polygon) == (0x4e2, 0x12, 4),
            "HOME command matches verified NAV");
        foreach (string handle in new[] { "QAFriend", "QAEnemy" })
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, handle, handle, stockCharacterLoad: true);
            context.Helpers.Add(handle, helper);
            await PvpOpponentsFixture.EnterAsync(helper);
            NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, helper);
        }
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        DisablingShotMode.CheckProfile(actor, Loadout, Level);
        var foe = context.Helpers["QAEnemy"];
        DisablingShotMode.CheckProfile(foe, EnemyLoadout, Level);
        await StockAbilityProfile.ObserveAsync(foe, 0, frame => StockAbilityProfile.Bonus(frame, Detection, 638, 200, 0, false),
            "Exact detection stat on the ordinary loaded ranked action");
        await PvpOpponentsFixture.EnterAsync(actor);
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, actor);
        await foe.WaitForViewNamedAsync(actor.Handle);
        await context.Helpers["QAFriend"].WaitForViewNamedAsync(actor.Handle);
        await StockAbilityProfile.ObserveAsync(actor, 0, StockAbilityProfile.GrantRecord(Belt, false), "Ordinary rank-zero Belt grant");
        actor.SendRpc(Convert.FromHexString(AwakenedStockStealthSpeedMode.SpyUse));
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 58 04 00 00");
        int at = actor.World.RpcSnapshot().Count;
        actor.SendRpc(Convert.FromHexString(AwakenedStockStealthSpeedMode.BeltUse));
        await StockAbilityProfile.ObserveAsync(actor, at, frame => StockAbilityProfile.Bonus(frame, Belt, 115, -300, 130, false),
            "Actual stock Belt puts 1116 on for 130 seconds");
        // Verify the real retained Detection action and observer route using the already captured ordinary Belt.
        await RevealAsync(foe);
        await StockAbilityProfile.ObserveAsync(actor, at, frame => StockAbilityProfile.Bonus(frame, Belt, 115, -300, 130, true),
            "Real opposing Detection restores the hidden player's view");
        Check.Equal(0, actor.World.Problems.Count + context.Helpers.Values.Sum(h => h.World.Problems.Count), "Stock framing");
        Console.WriteLine("IMPARTED INVISIBILITY BOOTSTRAP PASS: legal Archive, NAV, retained official Belt and Countermeasure. Direct 629 reserved for native.");
        return Array.Empty<byte>();
    }

    internal static async Task RevealAsync(HeadlessPlayer enemy)
    {
        if (enemy.Handle != "QAEnemy") throw new InvalidOperationException("The Detection witness is QAEnemy.");
        int at = enemy.World.RpcSnapshot().Count;
        enemy.SendRpc(AwakenedDetectionBoundaryMode.Countermeasure);
        await EvidenceRetestWire.TimerAsync(enemy, at, Detection, AbilityCooldowns.OwnTimer, 10);
    }
}
