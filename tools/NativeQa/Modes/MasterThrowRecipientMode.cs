using HeadlessClient;
using hds;
using MxoHd.PacketResearch;
using static NativeQa.StockAbilityProfile;
using static NativeQa.Modes.StarterHackerControlTargetMode;

namespace NativeQa.Modes;

public sealed class MasterThrowRecipientMode : NativeQaMode
{
    private const int X = 21550, TargetX = 21750, Y = 1095, Z = 16150, Level = 50;
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
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)} WHERE charId IN (110,6114)",
        $"UPDATE char_abilities SET level={Level} WHERE char_id=110",
        $"DELETE FROM inventory WHERE charId IN (110,6114) AND slot={CombatFixtureWeapons.HandSlot}"
    }, DevCommands: true, testMobFixture: "martial-player-controls.csv", hotbarAbilities: new[] { Spy, Throat, Subduing },
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1",
            ["MXO_PVP_MAX_SAFE_LEVEL"] = "15", ["MXO_CONDITIONAL_POWERLESS_QA"] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "340,347", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            "Stay at printed HOME. CSR faces QAEnemy at 2 m on verified outdoor NAV. No teleport. Page 2 Spy / Throat Cutting Throw / Subduing Throw. Use Spy slot 1 and /target QAEnemy. Stay at rest. No helper action.",
            "Press slot 3 unprepared first. Expect 15 IS, a 1.5 s bar, 8 s own reuse and an 87 damage hit, with no 347 Stun. After own reuse, type ?cd daze 8 then immediately press slot 3. Expect the same debit and hit, plus linked Stun 102 and separate immunity 734/681 for 4 s. Wait for their matched removals and for the preparation Daze and any sibling Stagger to end.",
            "Press slot 2 unprepared. Expect 15 IS, 1.5 s bar and 8 s own reuse, 123 damage and no source 340 Powerless. Wait until any sibling effects expire and the own timer clears. Type ?cd stagger 8 and press slot 2 immediately. Expect a live hit, source 340 state 96 for 5.7 s (six-second base) with the matching removal, and no new immunity. Keep both target and caster logs.",
            "QA supplies only the selected player's setup state. No proc override and no exchange. Headless also checks targetless refusal before all four shots. Scope is the conditional player-target branch. The broader targeting check stays open."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        Console.WriteLine($"READY HOME {InvisibilityBeltWatcherMode.Warp(X, Y, Z, 64, inside: false)}");
        Console.WriteLine($"READY QAEnemy {InvisibilityBeltWatcherMode.Warp(TargetX, Y, Z, 192, inside: false)}");
        Check.That(NavMesh.HasSightPath(1, X, Y, Z, TargetX, Y, Z), "Clear 2 m path");
        var server = context.ActualServer!;
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
        await RefusedAsync(caster, Throat, WithHandle(StockThroat, 0));
        await RefusedAsync(caster, Subduing, WithHandle(StockSubduing, 0));

        var target = await caster.WaitForViewNamedAsync(enemy.Handle);
        uint handle = DuelPackets.Handle(target.ViewId, target.SpawnId);
        caster.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(handle))}");
        await CheckPair(Subduing, StockSubduing, 87, TempMods.StunAbility, 4, "?cd daze 8", "Daze");
        // The 347 landed siblings can outlast the reuse, which started at admission.
        await Task.Delay(TimeSpan.FromSeconds(2));
        await CheckPair(Throat, StockThroat, 123, TempMods.PowerlessAbility, 5.7f, "?cd stagger 8", "Stagger");
        Check.Equal(0, caster.World.Problems.Count + enemy.World.Problems.Count, "Clean retained stock framing");
        Console.WriteLine("MASTER THROW RECIPIENT PASS: both conditional states absent without prep and present with matching removals after prep.");
        return Array.Empty<byte>();

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
}
