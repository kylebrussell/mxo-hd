using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class InvisibilityBeltWatcherMode : NativeQaMode
{
    private const ushort Belt = 1116, Parent = 175;
    private const int Level = 50, X = 17000, Y = 495, Z = 6400, Edge = 7200, Far = Edge + 1, Guard = 7800;
    private const int WallX = 28650, WallY = 895, WallZ = 12700, WatchX = 29150, FriendX = 17400;
    private const string GuardName = "QA Disguise Watcher";
    private static readonly ushort[] Loadout = DisablingShotMode.Closure(2, 17, Parent, 290, CombatStyles.SpyStyle).Order().ToArray();

    public override string Name => "ability-parity-invisibility-belt-watcher";
    public override NativeQaModePlan Plan { get; } = new(
        Loadout, X, Z, Array.Empty<string>(),
        new[]
        {
            "DELETE FROM inventory WHERE charId=110 AND (slot IN (40,107) OR goid=460)",
            "INSERT INTO inventory(charId,goid,slot,count,purity) VALUES(110,460,40,1,0)"
        },
        new[]
        {
            PvpOpponentsFixture.NativeSql,
            $"UPDATE characters SET x={X},y={Y},z={Z},rotation=0,innerStrC=500,innerStrM=500 WHERE charId=110",
            $"UPDATE characters SET x={FriendX},y={Y},z={Z},rotation=192 WHERE charId=6111",
            $"UPDATE characters SET x={X},y={Y},z={Far},rotation=128 WHERE charId=6114"
        }, DevCommands: true, testMobFixture: "disguise-watcher.csv",
        hotbarAbilities: new ushort[] { CombatStyles.SpyStyle },
        hotbarInventory: new AbilitySweepHotbar.InventoryShortcut[] { new(40, 460, "Invisibility Belt") },
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1",
            ["MXO_PVP_MAX_SAFE_LEVEL"] = "15", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1", [InvisibilityBeltQa.Mode] = "1"
        }, extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "1116", ["NativeCloseableRows"] = 1,
            ["LoadedMemory"] = Loadout.Sum(AbilityMemory.Cost), ["MemoryCapacity"] = AbilityMemory.ForLevel(Level)
        }, readyMessages: new[]
        {
            "Page 2 Spy and Invisibility Belt; use Actions > Tools > Invisibility Belt if needed. No selection or combat. Start at HOME (17000,495,6400) with a same-side observer 4 m east and QAEnemy initially 8.01 m north. The fixture prints verified HOME, GUARD and WALL ?tp returns. Leave one second between Belt attempts.",
            """At HOME select Spy. Send {"actor":"QAEnemy","action":"belt-watcher","phase":"edge"}. Press Belt while the standing enemy faces CSR at 8.00 m. Expect one 'You cannot become invisible near enemies' notice, [STEALTH] Belt watcher QAEnemy refuses CSR_Tester, and no cast, charge, 115/881 records or reuse. Send phase "far", then run the GUARD teleport. Press Belt. The stationary hostile NPC 3 m north produces the same refusal with its name.""",
            """Return HOME. Send phase "blind" (puts QAEnemy back at 8.00 m facing CSR and applies 20-second Blind with the disposable helper aid). Press Belt promptly. Expect actual 80 b9 5c 04, 3 s cast, 50 IS and 30 s reuse. [MODIFIER] source=1116 stat 115 -300 and 881 both 130 s; own pose 1a and invisible bit 4, opposing view gone, ally view remains. After own reuse, press Belt again and expect both removals, no new debit and normal pose/view restored.""",
            """Run the WALL return and send helper phase "wall". QAEnemy stands opposite CSR at 5 m across a mapped office barrier. Both stand on the same floor 800. Confirm the barrier visually. Press Belt and expect normal 3 s activation and 50 IS, with no watcher refusal. The wall check uses a three-metre-wide blocked sight band. Previously verified stock Detection, aura cancellation, duration bonus and natural cleanup carry over.""",
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (var (x, z) in new[] { (X, Z), (FriendX, Z), (X, Edge), (X, Far), (X, Guard), (X, 8100) })
            Warp(x, Y, z, 0, inside: false);
        Check.That(NavMesh.HasSightPath(1, X, Y, Z, X, Y, Edge) && NavMesh.HasSightPath(1, X, Y, Guard, X, Y, 8100),
            "The facing player and NPC have clear street sight");
        foreach (int z in Enumerable.Range(-3, 7).Select(offset => WallZ + 50 * offset))
        {
            Warp(WallX, WallY, z, 64, inside: true);
            Warp(WatchX, WallY, z, 192, inside: true);
            Check.That(!NavMesh.HasSightPath(1, WallX, WallY, z, WatchX, WallY, z) &&
                !NavMesh.Locate(1, (WallX + WatchX) / 2, WallY, z, out _), "Continuous office barrier across at least three metres");
        }
        Console.WriteLine($"READY HOME {Warp(X, Y, Z, 0, inside: false)}");
        Console.WriteLine($"READY GUARD {Warp(X, Y, Guard, 0, inside: false)}");
        Console.WriteLine($"READY WALL {Warp(WallX, WallY, WallZ, 64, inside: true)}");

        foreach (string name in new[] { "QAEnemy", "QAFriend" })
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, name, name, stockCharacterLoad: true);
            context.Helpers.Add(name, helper);
            await PvpOpponentsFixture.EnterAsync(helper);
            NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, helper);
        }
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        var enemy = context.Helpers["QAEnemy"];
        var friend = context.Helpers["QAFriend"];
        DisablingShotMode.CheckProfile(actor, Loadout, Level);
        await PvpOpponentsFixture.EnterAsync(actor);
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, actor);
        await StockAbilityProfile.ObserveAsync(actor, 0, StockAbilityProfile.GrantRecord(Belt, false), "Ordinary hidden Belt grant");
        await enemy.WaitForViewNamedAsync(actor.Handle);
        await friend.WaitForViewNamedAsync(actor.Handle);
        actor.SendRpc(Convert.FromHexString(AwakenedStockStealthSpeedMode.SpyUse));
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 58 04 00 00");

        await SetWatcherAsync(enemy, "edge");
        await RefusedAsync("QAEnemy");
        await SetWatcherAsync(enemy, "far");
        await WarpAsync(actor, X, Y, Guard, 0, inside: false);
        await actor.WaitForViewNamedAsync(GuardName);
        await RefusedAsync(GuardName);
        await WarpAsync(actor, X, Y, Z, 0, inside: false);
        await SetWatcherAsync(enemy, "blind");
        int at = actor.World.RpcSnapshot().Count;
        actor.SendRpc(Convert.FromHexString(AwakenedStockStealthSpeedMode.BeltUse));
        await StockAbilityProfile.ObserveAsync(actor, at, frame => StockAbilityProfile.Bonus(frame, Belt, 115, -300, 130, false),
            "The real Belt action admits a blinded watcher");
        await EvidenceRetestWire.TimerAsync(actor, at, Belt, AbilityCooldowns.OwnTimer, 30);
        actor.SendRpc(Convert.FromHexString(AwakenedStockStealthSpeedMode.BeltUse));
        await StockAbilityProfile.ObserveAsync(actor, at, frame => StockAbilityProfile.Bonus(frame, Belt, 115, -300, 130, true),
            "The real Belt action deactivates");
        await WarpAsync(actor, WallX, WallY, WallZ, 64, inside: true);
        await SetWatcherAsync(enemy, "wall");
        await Task.Delay(TimeSpan.FromSeconds(31));
        int wallAt = actor.World.RpcSnapshot().Count;
        actor.SendRpc(Convert.FromHexString(AwakenedStockStealthSpeedMode.BeltUse));
        await StockAbilityProfile.ObserveAsync(actor, wallAt, frame => StockAbilityProfile.Bonus(frame, Belt, 115, -300, 130, false),
            "The ordinary Belt action passes with a facing watcher across the verified office barrier");
        Check.Equal(0, actor.World.Problems.Count + context.Helpers.Values.Sum(helper => helper.World.Problems.Count), "Stock framing");
        Console.WriteLine("BELT WATCHER BOOTSTRAP PASS: retained stock Belt, aware and blinded player, live NPC, ordinary teardown, verified office barrier.");
        return Array.Empty<byte>();

        async Task RefusedAsync(string name)
        {
            int at = context.ActualServer!.DebugLog().Length;
            await StarterHackerControlTargetMode.RefusedAsync(actor, Belt,
                Convert.FromHexString(AwakenedStockStealthSpeedMode.BeltUse), AbilityRefusals.NoClientMessage);
            Check.That(context.ActualServer!.DebugLog()[at..].Contains($"Belt watcher {name} refuses", StringComparison.Ordinal),
                $"The expected Belt watcher caused the refusal: {name}");
            await Task.Delay(TimeSpan.FromSeconds(AbilityHandler.DuplicateAbilityWindowSeconds + .1));
        }
    }

    internal static async Task SetWatcherAsync(HeadlessPlayer enemy, string phase)
    {
        if (enemy.Handle != "QAEnemy") throw new InvalidOperationException("The Belt watcher is QAEnemy.");
        if (phase == "wall")
        {
            await WarpAsync(enemy, WatchX, WallY, WallZ, 192, inside: true);
            return;
        }
        if (phase is not ("edge" or "far" or "blind")) throw new InvalidOperationException("Use phase edge, far, blind or wall.");
        await WarpAsync(enemy, X, Y, phase == "far" ? Far : Edge, 128, inside: false);
        if (phase == "blind")
        {
            enemy.SkipReceived();
            enemy.SendChat("?cd beltblind");
            await enemy.WaitForTextAsync("QAEnemy Blinded for the Belt watcher check.");
        }
    }

    private static async Task WarpAsync(HeadlessPlayer actor, int x, int y, int z, byte yaw, bool inside)
    {
        string command = Warp(x, y, z, yaw, inside);
        actor.SkipReceived();
        actor.SendChat(command);
        string[] fields = command.Split(' ');
        await actor.WaitForTextAsync($"Teleported to {fields[4]}/{fields[5]}");
    }

    private static string Warp(int x, int y, int z, byte yaw, bool inside)
    {
        Check.That(NavMesh.Locate(1, x, y, z, out var stand) && stand.indoors == inside && stand.floorY + 95 == y,
            $"Verified Belt stand ({x},{y},{z})");
        return $"?tp {x} {y} {z} {stand.sector:x} {stand.region:x} {stand.polygon:x} - {yaw:x2}";
    }
}
