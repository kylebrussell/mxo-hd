using HeadlessClient;
using hds;
using MxoHd.PacketResearch;
using static NativeQa.StockAbilityProfile;
using static NativeQa.Modes.StarterHackerControlTargetMode;

namespace NativeQa.Modes;

public sealed class MasterThrowRecipientMode : NativeQaMode
{
    private const int X = 21550, TargetX = 21750, Y = 1095, Z = 16150, Level = 50;
    private const int FriendX = 21650, FriendZ = 16250;
    private const ushort Spy = 1112, Throat = 340, Subduing = 347;
    // Retained stock throws from the fixed-damage package.
    private const string StockThroat = "80b9540104000200000000000000000000000000000000000000000054010000006b0000086b0000000000";
    private const string StockSubduing = "80b95b01090008000000000000000000000000000000000000000000585cb60001000000086b0000000000";
    private static readonly ushort[] Loadout = DisablingShotMode.Closure(2, 17, Spy, Throat, Subduing);

    public override string Name => "ability-parity-master-throw-recipient";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), Array.Empty<string>(), new[]
    {
        PvpOpponentsFixture.NativeSql,
        $"UPDATE characters SET x={X},y={Y},z={Z},rotation=64,alignment=1," +
            "healthC=5000,healthM=5000,innerStrC=1000,innerStrM=1000 WHERE charId=110",
        $"UPDATE characters SET x={TargetX},y={Y},z={Z},rotation=192,alignment=2," +
            "healthC=5000,healthM=5000 WHERE charId=6114",
        $"UPDATE characters SET x={FriendX},y={Y},z={FriendZ},rotation=192,alignment=1 WHERE charId=6111",
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)} WHERE charId IN (110,6111,6114)",
        $"UPDATE char_abilities SET level={Level} WHERE char_id=110",
        $"DELETE FROM inventory WHERE charId IN (110,6114) AND slot={CombatFixtureWeapons.HandSlot}"
    }, DevCommands: true, testMobFixture: "martial-player-controls.csv", hotbarAbilities: new[] { Spy, Throat, Subduing },
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1",
            ["MXO_PVP_MAX_SAFE_LEVEL"] = "15", ["MXO_CONDITIONAL_POWERLESS_QA"] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "340,347", ["NativeCloseableRows"] = 2 },
        readyMessages: new[]
        {
            "Page 2 Spy / Throat Cutting Throw / Subduing Throw. Start at HOME facing QAEnemy. Use Spy slot 1. Stay out of close combat. Press slots 2/3 without selection, then /target QAFriend and repeat. Expect invalid target 24 or a local gate.",
            "Use printed BLOCK, /target QAEnemy, then slots 2/3 for sight 9. Use printed FAR, retarget QAEnemy, slots 2/3 for range 34. Enter each teleport in the printed chunks, one final Enter. No refusal should spend IS or start cast/reuse.",
            "Return to HOME, /target QAEnemy and cast slots 2/3. Expect 123 and 87 at 15 IS, 1.5 s cast and 8 s own reuse each. PR1105 already covers the prepared and unprepared control outcomes. No helper action or RNG aid for this geometry retest."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        var home = StarterConcealedShotTargetingMode.Stand(X, Z);
        var block = StarterConcealedShotTargetingMode.Stand(22350, Z);
        var far = StarterConcealedShotTargetingMode.FindFarStand();
        PrintStand("HOME", home);
        PrintStand("BLOCK", block);
        PrintStand("FAR", far);
        Console.WriteLine($"READY QAEnemy {InvisibilityBeltWatcherMode.Warp(TargetX, Y, Z, 192, inside: false)}");
        Check.That(NavMesh.HasSightPath(1, X, Y, Z, TargetX, Y, Z), "Clear 2 m path");
        Check.That(!NavMesh.HasSightPath(1, 22350, Y, Z, TargetX, Y, Z), "Verified solid wall");
        _ = InvisibilityBeltWatcherMode.Warp(FriendX, Y, FriendZ, 192, inside: false);
        var server = context.ActualServer!;
        var friend = await HeadlessPlayer.LoginAsync(context.Address, "QAFriend", "QAFriend", stockCharacterLoad: true);
        context.Helpers.Add(friend.Handle, friend);
        await PvpOpponentsFixture.EnterAsync(friend);
        NativeQaReadiness.RequireNoAmbientAggro(server, friend);
        var enemy = await HeadlessPlayer.LoginAsync(context.Address, "QAEnemy", "QAEnemy", stockCharacterLoad: true);
        context.Helpers.Add(enemy.Handle, enemy);
        await PvpOpponentsFixture.EnterAsync(enemy);
        NativeQaReadiness.RequireNoAmbientAggro(server, enemy);
        if (context.StockProbe is not { } caster) return Array.Empty<byte>();

        DisablingShotMode.CheckProfile(caster, Loadout, Level);
        await PvpOpponentsFixture.EnterAsync(caster);
        NativeQaReadiness.RequireNoAmbientAggro(server, caster);
        caster.SendRpc(AwakenedStockStealthSpeedMode.SpyUse);
        await caster.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 58 04 00 00");
        await RefuseBoth(0, AbilityRefusals.InvalidTarget);
        var ally = await caster.WaitForViewNamedAsync(friend.Handle);
        await RefuseBoth(DuelPackets.Handle(ally.ViewId, ally.SpawnId), AbilityRefusals.InvalidTarget);
        await Move(block);
        await RefuseBoth(await EnemyHandle(), AbilityRefusals.TargetNotInSight);
        await Move(far);
        await RefuseBoth(await EnemyHandle(), AbilityRefusals.TargetOutOfRange);
        await Move(home);

        var target = await caster.WaitForViewNamedAsync(enemy.Handle);
        uint handle = DuelPackets.Handle(target.ViewId, target.SpawnId);
        caster.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(handle))}");
        await CheckPair(Subduing, StockSubduing, 87, TempMods.StunAbility, 4, "?cd daze 8", "Daze");
        // The 347 landed siblings can outlast the reuse, which started at admission.
        await Task.Delay(TimeSpan.FromSeconds(2));
        await CheckPair(Throat, StockThroat, 123, TempMods.PowerlessAbility, 5.7f, "?cd stagger 8", "Stagger");
        int beforeMove = server.DebugLog().Length;
        caster.SendRpc(WithHandle(StockThroat, handle));
        await EvidenceRetestWire.CastBarAsync(caster, Throat, 1.5f);
        await Move(far);
        await caster.WaitUntilAsync(() => server.DebugLog()[beforeMove..].Contains(
            "[ABILITY] 340 fizzles: the selected opponent changed, left range or left sight", StringComparison.Ordinal),
            "A legal cast does not hit if the target leaves the valid range before completion", HeadlessPlayer.DefaultTimeout);
        Check.That(!server.DebugLog()[beforeMove..].Contains("ThroatCuttingThrowAbility lands on QAEnemy", StringComparison.Ordinal),
            "The completion-time geometry change prevents the hit");
        Check.Equal(0, caster.World.Problems.Count + enemy.World.Problems.Count, "Clean retained stock framing");
        Console.WriteLine("MASTER THROW RECIPIENT PASS: both conditional states absent without prep and present with matching removals after prep.");
        return Array.Empty<byte>();

        async Task<uint> EnemyHandle()
        {
            var view = await caster.WaitForViewNamedAsync(enemy.Handle);
            return DuelPackets.Handle(view.ViewId, view.SpawnId);
        }

        async Task RefuseBoth(uint selected, uint reason)
        {
            await RefusedAsync(caster, Throat, WithHandle(StockThroat, selected), reason);
            await RefusedAsync(caster, Subduing, WithHandle(StockSubduing, selected), reason);
        }

        async Task Move(StarterConcealedShotTargetingMode.Teleport stand)
        {
            caster.SkipReceived();
            caster.SendChat(stand.Command);
            await caster.WaitForTextAsync(stand.Confirmation);
        }

        async Task CheckPair(ushort id, string frame, int damage, ushort state, float seconds, string prep, string answer)
        {
            int plainFrom = enemy.World.RpcSnapshot().Count;
            int plainCast = await Cast(id, frame, damage);
            await EvidenceRetestWire.TimerAsync(caster, plainCast, id, AbilityCooldowns.OwnTimer, 8, ended: true);
            Check.That(!enemy.World.RpcSnapshot().Skip(plainFrom).Any(r =>
                ManageBonusRecord.TryDecode(r.Bytes, out var mod) && mod is { AddRemoveType: 0 } &&
                mod.SourceId == id && mod.TargetId == state),
                $"{id} does not install {state} without the predicate");
            caster.SendChat(prep);
            await caster.WaitForTextAsync($"{answer} fixture applied");
            int seededFrom = enemy.World.RpcSnapshot().Count;
            int seededCast = await Cast(id, frame, damage);
            var add = await ObserveAsync(enemy, seededFrom, r => Bonus(r, id, state, 0, seconds, false),
                $"Prepared {id} publishes state {state} for {seconds} s");
            ManageBonusRecord.TryDecode(add.Bytes, out var record);
            await ObserveAsync(enemy, seededFrom, r => ManageBonusRecord.TryDecode(r.Bytes, out var removal) &&
                removal == record with { AddRemoveType = 1 }, $"Prepared {id} retires the same state");
            if (id == Subduing)
                await ObserveAsync(enemy, seededFrom, r => Bonus(r, TempMods.ControlImmunityTimerAbility,
                    TempMods.ISDrainTargetAbility, 0, 4, false), "Prepared Subduing Throw sends separate four-second immunity");
            else
                Check.That(!enemy.World.RpcSnapshot().Skip(seededFrom).Any(r =>
                    ManageBonusRecord.TryDecode(r.Bytes, out var mod) && mod is { AddRemoveType: 0 } &&
                    mod.SourceId == TempMods.ControlImmunityTimerAbility && mod.TargetId == TempMods.ISDrainTargetAbility),
                    "Conditional Powerless does not install immunity");
            await EvidenceRetestWire.TimerAsync(caster, seededCast, id, AbilityCooldowns.OwnTimer, 8, ended: true);
        }

        async Task<int> Cast(ushort id, string frame, int damage)
        {
            int from = caster.World.RpcSnapshot().Count, recipientFrom = enemy.World.RpcSnapshot().Count;
            caster.SendRpc(WithHandle(frame, handle));
            await EvidenceRetestWire.CastBarAsync(caster, id, 1.5f);
            await EvidenceRetestWire.TimerAsync(caster, from, id, AbilityCooldowns.OwnTimer, 8);
            byte[] hit = AbilityMessages.Damage(false, id, caster.Handle, enemy.Handle, damage, 0);
            await ObserveAsync(enemy, recipientFrom, r => r.Bytes.AsSpan().SequenceEqual(hit), $"Live throw {id} hits for {damage}");
            return from;
        }
    }
    private static void PrintStand(string label, StarterConcealedShotTargetingMode.Teleport stand)
    {
        string[] parts = stand.Command.Split(' ');
        Console.WriteLine($"READY {label} type '{string.Join(' ', parts[..3])}', then ' {string.Join(' ', parts[3..7])}', then ' {parts[7]} {parts[8]}'; press Enter once.");
    }
}
