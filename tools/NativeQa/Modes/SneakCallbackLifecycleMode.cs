using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class SneakCallbackLifecycleMode : NativeQaMode
{
    private const ushort Parent = 151, Sneak = Stealth.SneakGrantAbility;
    private const int Level = 50, Rank = 15, X = 17000, Y = 495, HomeZ = 6400, Mid = 7200, Away = 8300, FriendX = 17400;
    private const string Home = "?tp 17000 495 6400 4e2 12 4 - 00";
    private static readonly ushort[] Loadout = DisablingShotMode.Closure(2, 17, Parent, CombatStyles.SpyStyle).Order().ToArray();
    private static readonly (ushort Stat, int Value)[] Records = { (115, -200), (209, Rank), (509, 10), (881, 0) };

    public override string Name => "ability-parity-sneak-callback-lifecycle";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, HomeZ, Array.Empty<string>(),
        Array.Empty<string>(), new[]
        {
            PvpOpponentsFixture.NativeSql,
            $"UPDATE characters SET x={X},y={Y},z={HomeZ},rotation=0 WHERE charId=110",
            $"UPDATE char_abilities SET level={Rank} WHERE char_id=110 AND ability_id={AbilityMessages.AbilityGameObject(Parent)}",
            $"UPDATE characters SET x={X},y={Y},z={Away},rotation=128 WHERE charId=6114",
            $"UPDATE characters SET x={FriendX},y={Y},z={HomeZ},rotation=192 WHERE charId=6111",
            "DELETE FROM char_abilities WHERE char_id=6114",
            "INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES " +
                string.Join(",", DisablingShotMode.Closure(2, 17, 293).Select((id, index) =>
                    $"(6114,{400 + index},{AbilityMessages.AbilityGameObject(id)},50,1)"))
        }, DevCommands: true, hotbarAbilities: new ushort[] { CombatStyles.SpyStyle, Sneak },
        testMobFixture: "martial-player-controls.csv", environmentOverrides: new()
        {
            ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_ASSASSIN_DETECTION_QA"] = "1"
        }, extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "1111", ["NativeCloseableRows"] = 1,
            ["LoadedMemory"] = Loadout.Sum(AbilityMemory.Cost), ["MemoryCapacity"] = AbilityMemory.ForLevel(Level)
        }, readyMessages: new[]
        {
            $"Verified HOME {Home} if displaced, +Z. QAFriend four metres east; QAEnemy faces south at 19 m initially. Page 2 Spy and rank-15 Sneak. Self-casts only; no bag or target. Helper JSON is on the PR. Only this pair's ordinary Sneak check has a fixed roll 35.",
            "Select Spy. Type ?health 2000 then press Sneak. Expect 20 IS on activation, [STEALTH] 1111 on, pose 09, and [MODIFIER] source=1111 115 -200, 209 +15, 509 +10 and state 881. QAFriend keeps CSR; QAEnemy loses that view. Type ?health 4000 and confirm Sneak remains. Then ?health 1000 and expect 1111 off (damaged), four matching removals, stance 00 and QAEnemy view recreation. This is the existing self-health fixture command through the normal callback.",
            """Press Sneak again from the same HOME. Once active, send {"actor":"QAEnemy","action":"assassin-watch","phase":"mid"}. At the next five-second check SA 13, SL 15 and roll 35 on clear NAV at eight metres must reveal CSR. Verify the same removals and standing. With QAEnemy still at MID, press Sneak again. Expect one enemy-watching refusal or stock suppression and no active state.""",
            """Send {"actor":"QAEnemy","action":"assassin-watch","phase":"away"}. Press Sneak, wait a second, then press it again for an explicit off. Confirm the same pose and record removals. Save the full stock 1111 request if available. The 509 record is verified separately from the contest's live movement NoiseLevel input."""
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (var (x, z) in new[] { (X, HomeZ), (FriendX, HomeZ), (X, Mid), (X, Away) })
            Check.That(NavMesh.Locate(1, x, Y, z, out var stand) && !stand.indoors && stand.floorY + 95 == Y &&
                NavMesh.HasSightPath(1, X, Y, HomeZ, x, Y, z), $"Verified open Sneak NAV ({x},{Y},{z})");
        Check.That(NavMesh.Locate(1, X, Y, HomeZ, out var home) &&
            (home.sector, home.region, home.polygon) == (0x4e2, 0x12, 4), "HOME matches NAV");
        var server = context.ActualServer!;
        foreach (string handle in new[] { "QAEnemy", "QAFriend" })
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, handle, handle, stockCharacterLoad: true);
            context.Helpers.Add(handle, helper);
            await PvpOpponentsFixture.EnterAsync(helper);
            NativeQaReadiness.RequireNoAmbientAggro(server, helper);
        }
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        DisablingShotMode.CheckProfile(actor, Loadout, Level);
        await PvpOpponentsFixture.EnterAsync(actor);
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        var enemy = context.Helpers["QAEnemy"];
        await enemy.WaitForViewNamedAsync(actor.Handle);
        await context.Helpers["QAFriend"].WaitForViewNamedAsync(actor.Handle);
        await StockAbilityProfile.ObserveAsync(actor, 0, StockAbilityProfile.GrantRecord(Sneak, false, Rank), "Normal Sneak rank-15 grant");
        actor.SendRpc(AwakenedStockStealthSpeedMode.SpyUse);
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 58 04 00 00");
        await HealthAsync(2000);
        int from = await BeginAsync();
        await HealthAsync(4000);
        Check.That(!actor.World.RpcSnapshot().Skip(from).Any(packet =>
            packet.Bytes.AsSpan().SequenceEqual(AbilityBuffs.BuildExclusive(Sneak, false))), "Healing does not terminate Sneak");
        int damage = server.DebugLog().Length;
        await HealthAsync(1000);
        await RecordsAsync(from, true);
        Check.That(server.DebugLog()[damage..].Contains("1111 off (damaged)", StringComparison.Ordinal),
            "The ordinary health callback cancels Sneak");
        from = await BeginAsync();
        string trace = await AssassinUpgradeDetectionMode.WatchAsync(enemy, server, "mid");
        Check.That(trace.Contains("SA 13 SL 15", StringComparison.Ordinal) &&
            trace.Contains("QAEnemy sees CSR_Tester", StringComparison.Ordinal), "Periodic check reveals the active Sneak");
        await RecordsAsync(from, true);
        await StarterHackerControlTargetMode.RefusedAsync(actor, Sneak,
            Convert.FromHexString(AwakenedStockStealthSpeedMode.SneakUse), AbilityRefusals.LegacyAmbiguous);
        await AssassinUpgradeDetectionMode.WatchAsync(enemy, server, "away");
        from = await BeginAsync();
        await WaitForNextPressAsync();
        actor.SendRpc(AwakenedStockStealthSpeedMode.SneakUse);
        await RecordsAsync(from, true);
        Check.Equal(0, actor.World.Problems.Count + context.Helpers.Values.Sum(helper => helper.World.Problems.Count), "Stock Sneak framing");
        return Array.Empty<byte>();

        static Task WaitForNextPressAsync() => Task.Delay(TimeSpan.FromSeconds(AbilityHandler.DuplicateAbilityWindowSeconds + .15));

        async Task<int> BeginAsync()
        {
            await WaitForNextPressAsync();
            int at = actor.World.RpcSnapshot().Count;
            int pose = actor.World.ViewSnapshot().Count;
            actor.SendRpc(AwakenedStockStealthSpeedMode.SneakUse);
            await RecordsAsync(at, false);
            await EvidenceRetestWire.ViewSinceAsync(actor, pose, 2, "01 28 ?? 40 09 00 00 10");
            return at;
        }

        async Task RecordsAsync(int at, bool remove)
        {
            foreach (var (stat, value) in Records)
                await StockAbilityProfile.ObserveAsync(actor, at, rpc => StockAbilityProfile.Bonus(rpc, Sneak, stat, value, 0, remove),
                    $"Sneak 1111/{stat} {value}, remove={remove}");
        }

        async Task HealthAsync(int health)
        {
            int at = actor.World.ViewSnapshot().Count;
            actor.SendChat($"?health {health}");
            await actor.WaitUntilAsync(() =>
                PvpOpponentsFixture.HealthHistory(actor.World.ViewSnapshot(), at, 2, self: true).Contains((ushort)health),
                $"Ordinary own-health update {health}", HeadlessPlayer.DefaultTimeout);
        }
    }
}
