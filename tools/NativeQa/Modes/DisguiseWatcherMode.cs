using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class DisguiseWatcherMode : NativeQaMode
{
    private const ushort Level = 50, Mask = 1114, Parent = 120, Aura = 61;
    private const int X = 17000, Y = 495, Z = 6400, Edge = 7200, Outside = Edge + 1;
    private const int FriendX = 17300, GuardStandZ = 7800, GuardZ = 8100, WallX = 22350, WallY = 1095, WallZ = 16150, WatchX = 21750;
    private const string GuardName = "QA Disguise Watcher", Message = "You cannot disguise while an enemy is watching.";
    // Complete ordinary stock Mask frame from PR435c, same tool and slot as the original mask fixture.
    private const string MaskUse = "80b95a0400000000000000000000000000000000000000000000000064f11a00101a0c3d08280000000000";
    private const string MiasmaUse = "80b93d00000000000000000000000000000000000000000000000000ffff7f7f00000000086b0000000000";
    private static readonly ushort[] Loadout = DisablingShotMode.Closure(2, 17, Parent, 131, CombatStyles.SpyStyle, Aura, 67, CombatStyles.HackerStyle)
        .Order().ToArray();
    private static readonly int Memory = Loadout.Sum(AbilityMemory.Cost);

    public override string Name => "ability-parity-disguise-watcher";
    public override NativeQaModePlan Plan { get; } = new(
        Loadout, X, Z, Array.Empty<string>(),
        new[]
        {
            "DELETE FROM inventory WHERE charId=110 AND (slot IN (40,107) OR goid=1268)",
            "INSERT INTO inventory(charId,goid,slot,count,purity) VALUES(110,1268,40,1,0)"
        },
        new[]
        {
            PvpOpponentsFixture.NativeSql,
            $"UPDATE characters SET x={X},y={Y},z={Z},rotation=0,innerStrC=500,innerStrM=500 WHERE charId=110",
            $"UPDATE characters SET x={FriendX},y={Y},z={Z},rotation=192 WHERE charId=6111",
            $"UPDATE characters SET x={X},y={Y},z={Outside},rotation=128 WHERE charId=6114"
        }, DevCommands: true, testMobFixture: "disguise-watcher.csv",
        hotbarAbilities: new ushort[] { CombatStyles.SpyStyle, CombatStyles.HackerStyle, Aura },
        hotbarInventory: new AbilitySweepHotbar.InventoryShortcut[] { new(40, 1268, "Disguise Mask") },
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1",
            ["MXO_PVP_MAX_SAFE_LEVEL"] = "15", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1"
        }, extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "1114", ["NativeCloseableRows"] = 1,
            ["LoadedMemory"] = Memory, ["MemoryCapacity"] = AbilityMemory.ForLevel(Level)
        }, readyMessages: new[]
        {
            $"Rank {Level}, {Memory}/{AbilityMemory.ForLevel(Level)} memory. Page 2 slots 1ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â€šÂ¬Ã…â€œ4: Spy, Hacker, Miasma, Disguise Mask. No target selection or roll aid. Mask fallback: Actions > Tools > Disguise Mask. HOME is ({X},{Y},{Z}) facing north, QAFriend faces CSR at ({FriendX},{Y},{Z}), QAEnemy south-facing at ({X},{Y},{Outside}). The fixture prints HOME, GUARD and WALL teleports after checking NAV.",
            "With Spy active, cast Mask from HOME while QAEnemy faces CSR 8.01 m away and QAFriend faces CSR at 3 m. Expect ordinary 2 s cast, SWAT appearance, bits 8 and 1114/881 for 56 s, title hidden. Toggle the same mask off to restore appearance and title.",
            """Bring QAEnemy to 8.00 m via {"actor":"QAEnemy","action":"disguise-watcher","phase":"edge"}. Wait for the helper reply and one second between Mask attempts. Press Mask. Expect one watcher notice, [STEALTH] Disguise watcher QAEnemy refuses CSR_Tester, and no cast bar, charge, state or timer. Send the same action with phase "away" at the same range, then repeat. Expect a normal cast. Toggle it off.""",
            """Set helper phase "far", then run the printed GUARD teleport. The helper now faces south, away from GUARD. QA Disguise Watcher is 3 m north, stationary and facing CSR. Do not attack it. Press Mask and expect the same refusal with the NPC name. Run the printed WALL teleport, then send the same helper action with phase "wall". QAEnemy faces CSR from 6 m behind a mapped sight break. Press Mask and expect a normal cast. Toggle it off.""",
            "Return HOME and put QAEnemy back in phase far. Select Hacker, start Miasma, then reselect Spy and use Mask. Expect the real disguise model and 1114/881 add, then [AURA] 61 off. Let the mask end naturally after 56 s. Expect [STEALTH] ... 1114 off (expired), the matched state removal, and restoration of your original presentation. Prior stock Detection already covers successful forced reveal on the opponent only."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (var (x, y, z) in new[] { (X, Y, Z), (X, Y, Edge), (X, Y, Outside), (FriendX, Y, Z),
            (X, Y, GuardZ), (X, Y, GuardStandZ), (WallX, WallY, WallZ), (WatchX, WallY, WallZ) })
            Warp(x, y, z, 0);
        Check.That(NavMesh.HasSightPath(1, X, Y, Z, X, Y, Edge) && NavMesh.HasSightPath(1, X, Y, Z, FriendX, Y, Z) &&
            NavMesh.HasSightPath(1, X, Y, GuardStandZ, X, Y, GuardZ) &&
            !NavMesh.HasSightPath(1, WatchX, WallY, WallZ, WallX, WallY, WallZ), "Verified clear watchers and blocked six-metre wall");
        Console.WriteLine($"READY HOME {Warp(X, Y, Z, 0)}");
        Console.WriteLine($"READY GUARD {Warp(X, Y, GuardStandZ, 0)}");
        Console.WriteLine($"READY WALL {Warp(WallX, WallY, WallZ, 192)}");

        var enemy = await EnterHelperAsync("QAEnemy");
        var friend = await EnterHelperAsync("QAFriend");
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        DisablingShotMode.CheckProfile(actor, Loadout, Level);
        await PvpOpponentsFixture.EnterAsync(actor);
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, actor);
        await StockAbilityProfile.ObserveAsync(actor, 0, StockAbilityProfile.GrantRecord(Mask, remove: false, rank: Level),
            "Legal rank-50 Disguise grant from Archive");
        var friendView = await friend.WaitForViewNamedAsync(actor.Handle);
        await enemy.WaitForViewNamedAsync(actor.Handle);
        actor.SendRpc(Convert.FromHexString(AwakenedStockStealthSpeedMode.SpyUse));
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 58 04 00 00");

        await AcceptedAsync(checkAppearance: true);
        await SetWatcherAsync(enemy, "edge");
        await RefusedAsync("QAEnemy");
        await SetWatcherAsync(enemy, "away");
        await AcceptedAsync();
        await SetWatcherAsync(enemy, "far");
        await WarpAsync(actor, X, Y, GuardStandZ, 0);
        await actor.WaitForViewNamedAsync(GuardName);
        await RefusedAsync(GuardName);
        await WarpAsync(actor, WallX, WallY, WallZ, 192);
        await SetWatcherAsync(enemy, "wall");
        await AcceptedAsync();
        await WarpAsync(actor, X, Y, Z, 0);
        await SetWatcherAsync(enemy, "far");
        int auraAt = actor.World.RpcSnapshot().Count;
        actor.SendRpc(Convert.FromHexString(StarterHackerViralSightMode.StockStyle));
        actor.SendRpc(Convert.FromHexString(MiasmaUse));
        await ObserveAuraAsync(auraAt, on: true);
        actor.SendRpc(Convert.FromHexString(AwakenedStockStealthSpeedMode.SpyUse));
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 58 04 00 00");
        await AcceptedAsync();
        await ObserveAuraAsync(auraAt, on: false);
        Check.Equal(0, actor.World.Problems.Count + enemy.World.Problems.Count + friend.World.Problems.Count, "Stock packet framing");
        Console.WriteLine("DISGUISE WATCHER BOOTSTRAP PASS: exact retained stock Mask, 8 m edge, ally, NPC, facing, wall and presentation.");
        return Array.Empty<byte>();

        async Task<HeadlessPlayer> EnterHelperAsync(string handle)
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, handle, handle, stockCharacterLoad: true);
            context.Helpers.Add(handle, helper);
            await PvpOpponentsFixture.EnterAsync(helper);
            NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, helper);
            return helper;
        }

        async Task AcceptedAsync(bool checkAppearance = false)
        {
            int cursor = actor.World.RpcSnapshot().Count, friendCursor = friend.World.ViewSnapshot().Count;
            var mask = PlayerViewAttributes.Build((0, PlayerViewAttributes.U8(0)), (1, PlayerViewAttributes.U32(0)),
                (28, PlayerViewAttributes.U32(1308623188)));
            var restore = PlayerViewAttributes.Build((0, PlayerViewAttributes.U8(1)), (1, friendView.Attributes["TitleAbility"]),
                (28, friendView.Attributes.GetValueOrDefault("Description") ?? PlayerViewAttributes.U32(0)));
            actor.SendRpc(Convert.FromHexString(MaskUse));
            await StockAbilityProfile.ObserveAsync(actor, cursor, frame => StockAbilityProfile.Bonus(frame, Mask, 881, 0, 56, false),
                "Exact stock mask enters a real 56 s state");
            if (checkAppearance) await ObserveAppearanceAsync(friendCursor, mask);
            friendCursor = friend.World.ViewSnapshot().Count;
            actor.SendRpc(Convert.FromHexString(MaskUse));
            await StockAbilityProfile.ObserveAsync(actor, cursor, frame => StockAbilityProfile.Bonus(frame, Mask, 881, 0, 56, true),
                "Exact stock mask ends its own state");
            if (checkAppearance) await ObserveAppearanceAsync(friendCursor, restore);
            await Task.Delay(TimeSpan.FromSeconds(AbilityHandler.DuplicateAbilityWindowSeconds + .1));
        }

        Task<RpcRecord> ObserveAuraAsync(int cursor, bool on) => StockAbilityProfile.ObserveAsync(actor, cursor,
            frame => frame.Bytes.AsSpan().SequenceEqual(AbilityBuffs.BuildExclusive(Aura, on)), $"Miasma on={on}");

        Task ObserveAppearanceAsync(int cursor, byte[] fields)
        {
            var pattern = BytePattern.Parse(EvidenceRetestWire.Le(friendView.ViewId) + " " + WireFormat.ToHex(fields));
            return friend.WaitUntilAsync(() => friend.World.ViewSnapshot().Skip(cursor).Any(pattern.Occurs),
                "Ally view receives disguise appearance or restores its original fields", HeadlessPlayer.DefaultTimeout);
        }

        async Task RefusedAsync(string watcher)
        {
            int cursor = actor.World.RpcSnapshot().Count;
            int debugAt = context.ActualServer!.DebugLog().Length;
            await StarterHackerControlTargetMode.RefusedAsync(actor, Mask, Convert.FromHexString(MaskUse), AbilityRefusals.NoClientMessage);
            await actor.WaitForTextAsync(Message);
            Check.That(!actor.World.RpcSnapshot().Skip(cursor).Any(frame =>
                StockAbilityProfile.Bonus(frame, Mask, 881, 0, 56, false)), "Watcher rejection did not start the mask");
            Check.That(context.ActualServer!.DebugLog()[debugAt..].Contains($"Disguise watcher {watcher} refuses", StringComparison.Ordinal),
                $"The expected watcher caused the refusal: {watcher}");
            await Task.Delay(TimeSpan.FromSeconds(AbilityHandler.DuplicateAbilityWindowSeconds + .1));
        }
    }

    internal static Task SetWatcherAsync(HeadlessPlayer enemy, string phase)
    {
        if (enemy.Handle != "QAEnemy") throw new InvalidOperationException("Disguise watcher is QAEnemy.");
        return phase switch
        {
            "edge" => WarpAsync(enemy, X, Y, Edge, 128),
            "away" => WarpAsync(enemy, X, Y, Edge, 0),
            "far" => WarpAsync(enemy, X, Y, Outside, 128),
            "wall" => WarpAsync(enemy, WatchX, WallY, WallZ, 64),
            _ => throw new InvalidOperationException("Use phase edge, away, far or wall.")
        };
    }

    private static async Task WarpAsync(HeadlessPlayer actor, int x, int y, int z, byte yaw)
    {
        string command = Warp(x, y, z, yaw);
        actor.SkipReceived();
        actor.SendChat(command);
        string[] fields = command.Split(' ');
        await actor.WaitForTextAsync($"Teleported to {fields[4]}/{fields[5]}");
    }

    private static string Warp(int x, int y, int z, byte yaw)
    {
        Check.That(NavMesh.Locate(1, x, y, z, out var stand) && !stand.indoors && stand.floorY + 95 == y,
            $"Verified Disguise outdoor stand ({x},{y},{z})");
        return $"?tp {x} {y} {z} {stand.sector:x} {stand.region:x} {stand.polygon:x} - {yaw:x2}";
    }
}
