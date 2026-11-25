using hds;
using HeadlessClient;
using MxoHd.PacketResearch;
using static NativeQa.StockAbilityProfile;

namespace NativeQa.Modes;

public sealed class HolyWaterCureMode : NativeQaMode
{
    private const int Level = 19, X = 17000, Y = 495, Z = 6400, EnemyX = 17200, DummyZ = 6700, FriendZ = 6200;
    private const string Home = "?tp 17000 495 6400 4e2 12 4 - 40";
    private const ushort HolyWater = CombatSpecials.HolyWater;         // 547
    private const ushort HolyWaterPlus = CombatSpecials.HolyWaterPlus; // 1023
    private static readonly ushort[] Loadout = new ushort[] { 2, 17, HolyWater, HolyWaterPlus }
        .SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys.Append(id)).Distinct().Order().ToArray();

    public override string Name => "ability-parity-holy-water-cure";

    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), Array.Empty<string>(), new[]
    {
        PvpOpponentsFixture.NativeSql,
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Z},rotation=64,alignment=1,healthC=5000,healthM=5000,innerStrC=1000,innerStrM=1000 WHERE charId=110",
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={EnemyX},y={Y},z={Z},rotation=192,alignment=2,healthC=5000,healthM=5000,innerStrC=1000,innerStrM=1000 WHERE charId=6114",
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={FriendZ},rotation=0,alignment=1 WHERE charId=6111",
        "DELETE FROM char_abilities WHERE char_id IN (6111,6114)",
        $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110",
        "DELETE FROM inventory WHERE charId IN (110,6111,6114) AND (slot<97 OR slot IN (105,107))",
        "INSERT INTO inventory(charId,goid,slot,count,purity) VALUES (110,8253,5,8,0),(110,43034,6,8,0)"
    }, DevCommands: true, hotbarAbilities: new[] { HolyWater, HolyWaterPlus }, hotbarWithdraw: true,
        testMobFixture: "wounding-throw.csv", environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0",
            ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            [CombatFixtureWeapons.DisengageMode] = "1",
            [HolyWaterCure.QaMode] = "1"
        }, extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "547,1023", ["NativeCloseableRows"] = 2, ["NativeCloseableIds"] = "547,1023"
        }, readyMessages: new[]
        {
            $"Level {Level}, Holy Water (GOID 8253, bag slot 5) & Enhanced Holy Water (GOID 43034, bag slot 6). Verified outdoor HOME ({X},{Y},{Z}) faces hostile QAEnemy 2 m east ({EnemyX},{Y},{Z}); Melee Dummy 3 m north ({X},{Y},{DummyZ}); friendly observer QAFriend 2 m south ({X},{Y},{FriendZ}). Page 2: 1=Holy Water Attack (547), 2=Enhanced Holy Water Attack (1023), 3=Withdraw. Disposable QA guarantees 547/1023 round wins.",
            "Step 1 (At-rest refusal): Before Close Combat, /target QAEnemy and try slots 1 & 2 -> local combat-only gate or refusal (0 IS).",
            "Step 2 (547 PvP ZombieVirus2 cure + RSICap deactivation + RSI revert): Type '?holywater infect 2 rsicap' -> QAEnemy receives ZombieVirus2Base(1235), ZombieVirus2Bite(1236), NPCKilledOnlyByHolyWater(432), RSICapture(1388), and model override (UseRSIDescription=0). Open Close Combat on QAEnemy, press slot 1 (547) -> 20 IS, 111 damage (raw 100 +11% L19 Melee), '[HOLY-WATER-CURE] ... ability=547 removed=1235,1236,432 rsiCaps=1388 revertedRsi=true', QAEnemy reverts to normal RSI. Press slot 1 (547) again while still fighting -> 11 damage (raw 10, no cure line).",
            "Step 3 (1023 PvP ZombieVirus3 cure + RSI revert, RSICap retained): Type '?holywater infect 3 rsicap' -> QAEnemy receives ZombieVirus3Base(1238), ZombieVirus3Bite(1237), 432, and RSICapture(1388). Press slot 2 (1023) -> 20 IS, 832 damage (raw 750 +11% L19 Melee), '[HOLY-WATER-CURE] ... ability=1023 removed=1238,1237,432 rsiCaps=none revertedRsi=true', QAEnemy reverts to normal RSI. Press slot 2 (1023) again -> 11 damage (raw 10). Type '?cd stop' to disengage.",
            "Step 4 (Optional NPC check on Melee Dummy 3 m N): Open Close Combat on Melee Dummy, '?exileweakness none' -> slot 1 deals 11 (raw 10); '?exileweakness holywater' -> slot 1 deals 111 (raw 100), slot 2 deals 832 (raw 750); '?cd stop'."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Holy Water cure mode requires NAV.");
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        Check.Equal(Home, InvisibilityBeltWatcherMode.Warp(X, Y, Z, 64, inside: false), "Printed HOME");
        InvisibilityBeltWatcherMode.Warp(EnemyX, Y, Z, 192, inside: false);
        InvisibilityBeltWatcherMode.Warp(X, Y, DummyZ, 0, inside: false);
        InvisibilityBeltWatcherMode.Warp(X, Y, FriendZ, 0, inside: false);
        Check.That(NavMesh.HasSightPath(1, X, Y, Z, EnemyX, Y, Z), "Clear path to QAEnemy");
        Check.That(NavMesh.HasSightPath(1, X, Y, Z, X, Y, DummyZ), "Clear path to Melee Dummy");

        var server = context.ActualServer!;
        var friend = await LoginHelperAsync("QAFriend");
        var enemy = await LoginHelperAsync("QAEnemy");
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        DisablingShotMode.CheckProfile(actor, Loadout, Level);
        await PvpOpponentsFixture.EnterAsync(actor);
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        var enemyView = await actor.WaitForViewNamedAsync("QAEnemy");
        var enemyForFriend = await friend.WaitForViewNamedAsync("QAEnemy");
        uint enemyHandle = DuelPackets.Handle(enemyView.ViewId, enemyView.SpawnId);

        // Step 1: At-rest refusal for 547 & 1023 (0 IS).
        await StarterHackerControlTargetMode.RefusedAsync(actor, HolyWater, Use(HolyWater, enemyHandle), AbilityRefusals.InvalidTarget);
        await StarterHackerControlTargetMode.RefusedAsync(actor, HolyWaterPlus, Use(HolyWaterPlus, enemyHandle), AbilityRefusals.InvalidTarget);

        // Step 2: Infect QAEnemy with ZombieVirus2 + RSICapture(1388), enter PvP Close Combat, cast 547 -> 100 dmg + cure + RSI revert + 1388 off.
        actor.SendChat("?holywater infect 2 rsicap");
        await actor.WaitForTextAsync("Infected QAEnemy with ZombieVirus2");

        string hex = WireFormat.ToHex(BitConverter.GetBytes(enemyHandle));
        actor.SendRpc($"40 {hex} 01 6b");
        await actor.WaitForRpcAsync("47");

        int enemyRpc = enemy.World.RpcSnapshot().Count;
        int enemyViewAt = enemy.World.ViewSnapshot().Count;
        int friendViewAt = friend.World.ViewSnapshot().Count;
        int logAt = server.DebugLog().Length;
        int actorRpc = actor.World.RpcSnapshot().Count;
        actor.SendRpc(Use(HolyWater, enemyHandle));
        byte[] hit111 = AbilityMessages.Damage(false, HolyWater, actor.Handle, enemy.Handle, 111, 0);
        await ObserveAsync(enemy, enemyRpc, r => r.Bytes.AsSpan().SequenceEqual(hit111), "547 hits infected QAEnemy for 111 (raw 100 +11%)");
        foreach (ushort grant in new[] { HolyWaterCure.ZombieVirus2Base, HolyWaterCure.ZombieVirus2Bite, HolyWaterCure.KilledOnlyByHolyWater })
        {
            byte[] unload = ServerPackets.BuildAbilityUnload(grant);
            await ObserveAsync(enemy, enemyRpc, r => r.Bytes.AsSpan().SequenceEqual(unload), $"547 unloads grant {grant}");
        }
        await EvidenceRetestWire.ViewSinceAsync(enemy, enemyViewAt, 2,
            WireFormat.ToHex(PlayerViewAttributes.Build((0, new byte[] { 1 }), (37, PlayerViewAttributes.U32(0)))));
        await EvidenceRetestWire.ViewSinceAsync(friend, friendViewAt, enemyForFriend.ViewId,
            WireFormat.ToHex(PlayerViewAttributes.Build((0, new byte[] { 1 }), (28, PlayerViewAttributes.U32(0)))));
        await actor.WaitUntilAsync(() => server.DebugLog()[logAt..].Contains(
            "[HOLY-WATER-CURE] caster=CSR_Tester target=QAEnemy ability=547 removed=1235,1236,432 rsiCaps=1388 revertedRsi=true",
            StringComparison.Ordinal), "547 cure log with RSICap 1388 deactivation", HeadlessPlayer.DefaultTimeout);

        // Repeat 547 on now-cured QAEnemy -> 11 damage (raw 10 +11%), no cure line.
        enemyRpc = enemy.World.RpcSnapshot().Count;
        logAt = server.DebugLog().Length;
        actor.SendRpc(Use(HolyWater, enemyHandle));
        byte[] hit11 = AbilityMessages.Damage(false, HolyWater, actor.Handle, enemy.Handle, 11, 0);
        await ObserveAsync(enemy, enemyRpc, r => r.Bytes.AsSpan().SequenceEqual(hit11), "547 hits cured QAEnemy for 11 (raw 10 +11%)");
        Check.That(!server.DebugLog()[logAt..].Contains("[HOLY-WATER-CURE]", StringComparison.Ordinal), "No cure line on uninfected target");

        // Step 3: Infect QAEnemy with ZombieVirus3 + RSICapture(1388), cast 1023 -> 832 dmg + cure + RSI revert, rsiCaps=none.
        actor.SendChat("?holywater infect 3 rsicap");
        await actor.WaitForTextAsync("Infected QAEnemy with ZombieVirus3");

        enemyRpc = enemy.World.RpcSnapshot().Count;
        enemyViewAt = enemy.World.ViewSnapshot().Count;
        friendViewAt = friend.World.ViewSnapshot().Count;
        logAt = server.DebugLog().Length;
        actor.SendRpc(Use(HolyWaterPlus, enemyHandle));
        byte[] hit832 = AbilityMessages.Damage(false, HolyWaterPlus, actor.Handle, enemy.Handle, 832, 0);
        await ObserveAsync(enemy, enemyRpc, r => r.Bytes.AsSpan().SequenceEqual(hit832), "1023 hits infected QAEnemy for 832 (raw 750 +11%)");
        foreach (ushort grant in new[] { HolyWaterCure.ZombieVirus3Base, HolyWaterCure.ZombieVirus3Bite, HolyWaterCure.KilledOnlyByHolyWater })
        {
            byte[] unload = ServerPackets.BuildAbilityUnload(grant);
            await ObserveAsync(enemy, enemyRpc, r => r.Bytes.AsSpan().SequenceEqual(unload), $"1023 unloads grant {grant}");
        }
        await EvidenceRetestWire.ViewSinceAsync(enemy, enemyViewAt, 2,
            WireFormat.ToHex(PlayerViewAttributes.Build((0, new byte[] { 1 }), (37, PlayerViewAttributes.U32(0)))));
        await EvidenceRetestWire.ViewSinceAsync(friend, friendViewAt, enemyForFriend.ViewId,
            WireFormat.ToHex(PlayerViewAttributes.Build((0, new byte[] { 1 }), (28, PlayerViewAttributes.U32(0)))));
        await actor.WaitUntilAsync(() => server.DebugLog()[logAt..].Contains(
            "[HOLY-WATER-CURE] caster=CSR_Tester target=QAEnemy ability=1023 removed=1238,1237,432 rsiCaps=none revertedRsi=true",
            StringComparison.Ordinal), "1023 cure log without RSICap deactivation", HeadlessPlayer.DefaultTimeout);

        // Repeat 1023 on cured QAEnemy -> 11 damage (raw 10 +11%).
        enemyRpc = enemy.World.RpcSnapshot().Count;
        actor.SendRpc(Use(HolyWaterPlus, enemyHandle));
        byte[] hitPlus11 = AbilityMessages.Damage(false, HolyWaterPlus, actor.Handle, enemy.Handle, 11, 0);
        await ObserveAsync(enemy, enemyRpc, r => r.Bytes.AsSpan().SequenceEqual(hitPlus11), "1023 hits cured QAEnemy for 11 (raw 10 +11%)");

        actor.SendChat("?cd stop");
        await actor.WaitForTextAsync("Fixture combat ended.");

        Check.Equal(0, actor.World.Problems.Count + enemy.World.Problems.Count + friend.World.Problems.Count,
            "Holy Water cure framing");
        Console.WriteLine("HOLY WATER CURE PASS: 547 (100->cure+1388+RSI->10) and 1023 (750->cure+RSI->10) verified on self & observer.");
        return Array.Empty<byte>();

        async Task<HeadlessPlayer> LoginHelperAsync(string name)
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, name, name, stockCharacterLoad: true);
            context.Helpers.Add(name, helper);
            await PvpOpponentsFixture.EnterAsync(helper);
            NativeQaReadiness.RequireNoAmbientAggro(server, helper);
            return helper;
        }
    }

    private static byte[] Use(ushort ability, uint target) =>
        new AbilityUseRecord(ability, target, 0, 0, 0, ability | (0x6bUL << 40), 8, 0x6b, 0, 0).Encode();
}
