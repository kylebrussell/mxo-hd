using MxoHd.PacketResearch;
using hds;
using HeadlessClient;
using static NativeQa.StockAbilityProfile;
using static NativeQa.Modes.StarterHackerControlTargetMode;

namespace NativeQa.Modes;

public sealed class ZenMasterRecipientMode : NativeQaMode
{
    private const int X = 17000, Y = 495, Z = 6400, EnemyZ = 6600, Level = 99, Rank = 50;
    private const ushort Sweep = 869, Flower = 870, Probe = 198;
    // Retained PR477 stock special requests and the ordinary Cheap Shot probe.
    private const string StockSweep = "80b9650304000200000000000000000000000000000000000000000065030000006b0000086b0000000000";
    private const string StockFlower = "80b9660304000200000000000000000000000000000000000000000066030000006b0000086b0000000000";
    private const string CheapShot = "80b9c600050003000000000000000000000000000000000000000000c6000000006b0000086b0000000000";
    private static readonly ushort[] Loadout = DisablingShotMode.Closure(2, 17, Sweep, Flower);
    private static readonly ushort[] EnemyLoadout = DisablingShotMode.Closure(2, 17, Probe);

    public override string Name => "ability-parity-zen-master-recipient";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), Array.Empty<string>(), new[]
    {
        PvpOpponentsFixture.NativeSql,
        $"UPDATE characters SET x={X},y={Y},z={Z},rotation=0,alignment=1,level={Level},exp={PlayerProgression.ExperienceForLevel(50)},healthC=9000,healthM=9000,innerStrC=1800,innerStrM=1800 WHERE charId=110",
        $"UPDATE characters SET x={X},y={Y},z={EnemyZ},rotation=128,alignment=2,level={Level},exp={PlayerProgression.ExperienceForLevel(50)},healthC=9000,healthM=9000,innerStrC=1800,innerStrM=1800 WHERE charId=6114",
        $"UPDATE char_abilities SET level={Rank} WHERE char_id=110",
        $"DELETE FROM inventory WHERE charId IN (110,6114) AND slot={CombatFixtureWeapons.HandSlot}",
        $"INSERT INTO inventory(charId,goid,slot,count,purity) VALUES (6114,{CombatFixtureWeapons.Rifle},{CombatFixtureWeapons.HandSlot},1,0)",
        "DELETE FROM char_abilities WHERE char_id=6114",
        "INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES " +
            string.Join(",", EnemyLoadout.Select((id, i) => $"(6114,{400 + i},{AbilityMessages.AbilityGameObject(id)},{Rank},1)"))
    }, DevCommands: true, testMobFixture: "martial-player-controls.csv", hotbarAbilities: new ushort[] { Flower, Sweep },
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            ["MXO_CONDITIONAL_THROW_QA"] = "1",
            [CombatFixtureWeapons.ZenControlsMode] = "1", [CombatFixtureWeapons.DisengageMode] = "1"
        },
        extraQaMetadata: new() { ["ScopedStockRows"] = "869,870", ["NativeCloseableRows"] = 2 },
        readyMessages: new[]
        {
            "Diagnostic L99 qualifies for these stock abilities. Keep Self Defense. HOME faces QAEnemy 2 m north. No tp. /target QAEnemy, try both slots at rest for the local combat gate, then right-click Close Combat. Strafe before opening if the avatar is hidden.",
            """870: ?cd zw 870, start {"actor":"QAEnemy","action":"zen-recipient","ability":870} in the background and press slot 1 immediately. The helper waits for the cast. Expect 40 IS, 419, Powerless state96, linked release, stat163 -10 and Cheap Shot blocked then admitted. Wait for ZEN RECIPIENT PASS 870 and the next round before proceeding.""",
            """869: Without Off Balance, slot 2 should show the state gate. Then ?cd offbalance and ?cd zw 869. Start {"actor":"QAEnemy","action":"zen-recipient","ability":869} in the background and press slot 2 immediately. Expect 40 IS, 402 and source869 state231 for 20 s. As soon as the Disarm add appears, use ?cd stop. The helper waits for teardown, attempts ordinary 41 while Disarmed, then repeats after release.""",
            "Expect [DISARM] Free Fire suppressed char=6114 before the matching removal. After release, QAEnemy fires with the already-equipped rifle. ZEN RECIPIENT PASS 869 carries the server block and recovery. QA supplies the required state, one winner per special and the fixture modifier chance. No modifier lasts longer because of that aid."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        Console.WriteLine($"READY HOME {InvisibilityBeltWatcherMode.Warp(X, Y, Z, 0, inside: false)}");
        Console.WriteLine($"READY QAEnemy {InvisibilityBeltWatcherMode.Warp(X, Y, EnemyZ, 128, inside: false)}");
        Check.That(NavMesh.HasSightPath(1, X, Y, Z, X, Y, EnemyZ), "Clear site for interlock and the post-teardown shot");
        var server = context.ActualServer!;
        var enemy = await HeadlessPlayer.LoginAsync(context.Address, "QAEnemy", "QAEnemy", stockCharacterLoad: true);
        context.Helpers.Add(enemy.Handle, enemy);
        await PvpOpponentsFixture.EnterAsync(enemy);
        await CombatFixtureLoadout.VerifyWeaponAtLoginAsync(enemy, CombatFixtureWeapons.Rifle);
        NativeQaReadiness.RequireNoAmbientAggro(server, enemy);
        if (context.StockProbe is not { } caster) return Array.Empty<byte>();

        DisablingShotMode.CheckProfile(caster, Loadout, Rank);
        DisablingShotMode.CheckProfile(enemy, EnemyLoadout, Rank);
        await PvpOpponentsFixture.EnterAsync(caster);
        Check.Equal((byte)Level, caster.World.CreationSnapshot().Last(v => v.ViewId == 2).Attributes["Level"][0], "Stock sees diagnostic level");
        NativeQaReadiness.RequireNoAmbientAggro(server, caster);
        var target = await caster.WaitForViewNamedAsync(enemy.Handle);
        uint handle = DuelPackets.Handle(target.ViewId, target.SpawnId);
        string hex = WireFormat.ToHex(BitConverter.GetBytes(handle));
        await RefusedAsync(caster, Flower, WithHandle(StockFlower, handle));
        await RefusedAsync(caster, Sweep, WithHandle(StockSweep, handle));
        int before = caster.World.RpcSnapshot().Count;
        caster.World.SendRpcs(WithHandle(PistolSlideMode.StockSelfDefense, handle), WireFormat.FromHex($"40 {hex} 01 6b"));
        await ObserveAsync(caster, before, r => r.Bytes is [0x46, ..] && r.Bytes.Length >= 6 && r.Bytes[5] == 1, "Stock close combat");
        caster.SendRpc($"81 51 {hex}");
        await RefusedAsync(caster, Flower, WithHandle(StockFlower, handle ^ 0x10000));
        await RefusedAsync(caster, Sweep, WithHandle(StockSweep, handle ^ 0x10000));
        await RefusedAsync(caster, Sweep, WithHandle(StockSweep, handle), AbilityRefusals.TargetStateRequired);

        await Arm(Flower);
        Task<object> flower = CheckRecipientAsync(enemy, server, Flower);
        caster.SendRpc(WithHandle(StockFlower, handle));
        await flower;
        int recovered = server.DebugLog().Length;
        await caster.WaitUntilAsync(() => server.DebugLog()[recovered..].Contains("[PVP] Round ", StringComparison.Ordinal),
            "The recovered probe retires before Sweep is armed", HeadlessPlayer.DefaultTimeout);

        caster.SendChat("?cd offbalance");
        await caster.WaitForTextAsync("OffBalance fixture applied");
        await Arm(Sweep);
        int sweepAt = enemy.World.RpcSnapshot().Count;
        Task<object> sweep = CheckRecipientAsync(enemy, server, Sweep);
        caster.SendRpc(WithHandle(StockSweep, handle));
        await ObserveAsync(enemy, sweepAt, r => Bonus(r, Sweep, TempMods.DisarmedAbility, 0, 20, false), "Sweep lands before fixture teardown");
        caster.SendChat("?cd stop");
        await caster.WaitForTextAsync("Fixture combat ended");
        await sweep;
        Check.Equal(0, caster.World.Problems.Count + enemy.World.Problems.Count, "Clean stock frames");
        return Array.Empty<byte>();

        async Task Arm(ushort id)
        {
            caster.SendChat($"?cd zw {id}");
            await caster.WaitForTextAsync($"Next Zen {id} against QAEnemy is armed.");
        }
    }

    internal static async Task<object> CheckRecipientAsync(HeadlessPlayer enemy, ServerInstance server, ushort id)
    {
        Check.Equal(6114u, enemy.CharId, "Zen recipient helper");
        Check.That(id is Sweep or Flower, "Only the two fixture rows are supported");
        int from = enemy.World.RpcSnapshot().Count;
        bool isSweep = id == Sweep;
        ushort state = isSweep ? TempMods.DisarmedAbility : TempMods.PowerlessAbility;
        float seconds = isSweep ? 20 : 24.6f;
        var caster = await enemy.WaitForViewNamedAsync("CSR_Tester");
        uint handle = DuelPackets.Handle(caster.ViewId, caster.SpawnId);
        string target = WireFormat.ToHex(BitConverter.GetBytes(handle));
        RpcRecord? statePacket = null;
        await enemy.WaitUntilAsync(() => (statePacket = enemy.World.RpcSnapshot().Skip(from)
            .FirstOrDefault(r => Bonus(r, id, state, 0, seconds, false))) != null,
            $"Recipient state {id}/{state} for {seconds} s", TimeSpan.FromSeconds(27));

        int logStart = server.DebugLog().Length;
        if (isSweep)
        {
            await enemy.WaitUntilAsync(() => enemy.World.RpcSnapshot().Skip(from).Any(r =>
                r.Bytes.AsSpan().SequenceEqual(WireFormat.FromHex($"46 {target} 00"))),
                "Operator ends the interlock while Disarm is active", TimeSpan.FromSeconds(10));
            enemy.SendRpc($"41 {target}");
            await enemy.WaitUntilAsync(() => server.DebugLog()[logStart..].Contains("[DISARM] Free Fire suppressed char=6114", StringComparison.Ordinal),
                "Disarmed recipient's free-fire request reaches the gate", HeadlessPlayer.DefaultTimeout);
        }
        else
        {
            enemy.SendRpc(WithHandle(CheapShot, handle));
            await enemy.WaitForTextAsync("You cannot use abilities while powerless.");
            var penalty = await ObserveAsync(enemy, from, r => Bonus(r, Flower, 163, -10, 15, false),
                "Spinning Flower adds the independent 15 s Tactics penalty");
            await WaitRemovalAsync(penalty);
        }

        await WaitRemovalAsync(statePacket!);

        int after = enemy.World.RpcSnapshot().Count;
        if (isSweep)
        {
            enemy.SendRpc($"41 {target}");
            await ObserveAsync(enemy, after, r => r.Bytes.AsSpan().SequenceEqual(WireFormat.FromHex("4e 00")),
                "The equipped rifle can fire after Disarm is removed");
        }
        else
        {
            await ObserveAsync(enemy, from, r => r.Bytes.AsSpan().SequenceEqual(AbilityMessages.StateEnd(state, enemy.Handle)), "Powerless release notice");
            enemy.SendRpc(WithHandle(CheapShot, handle));
            await EvidenceRetestWire.TimerAsync(enemy, after, Probe, AbilityCooldowns.OwnTimer, 20);
        }
        string pass = $"ZEN RECIPIENT PASS {id}";
        Console.WriteLine(pass);
        return new { Status = pass, Seconds = seconds, Blocked = true, Released = true, RetryAdmitted = true };

        async Task WaitRemovalAsync(RpcRecord added)
        {
            Check.That(ManageBonusRecord.TryDecode(added.Bytes, out var add) && add != null, "Decode original modifier");
            await enemy.WaitUntilAsync(() => enemy.World.RpcSnapshot().Skip(from).Any(r =>
                ManageBonusRecord.TryDecode(r.Bytes, out var remove) && remove == add! with { AddRemoveType = 1 }),
                $"Matched removal for {id}/{add!.TargetId}", TimeSpan.FromSeconds(27));
        }
    }
}
