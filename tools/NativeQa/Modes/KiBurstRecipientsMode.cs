using HeadlessClient;
using hds;
using MxoHd.PacketResearch;
using static NativeQa.StockAbilityProfile;

namespace NativeQa.Modes;

public sealed class KiBurstRecipientsMode : NativeQaMode
{
    private const int X = 17000, Y = 495, Z = 2300, Level = 50;
    private const ushort Style = 101, Burst = MartialArtsRemainingSpecials.KiBurst;
    // PR476 stock Ki Burst, with no selected object in the cast.
    private const string StockBurst = "80b99601000000000000000000000000000000000000000000000000585cc70001000000086b0000000000";
    private static readonly ushort[] Loadout = DisablingShotMode.Closure(2, 17, Style, Burst);
    private static readonly (string Name, int Id, int X, int Z, int Org)[] Stands =
    {
        ("QAEnemy", 6114, 17120, 2300, 2),
        ("QAFriend", 6111, 17120, 2500, 1),
        ("QAFar", 6112, 17500, 2300, 2)
    };

    public override string Name => "ability-parity-ki-burst-recipients";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), Array.Empty<string>(),
        new[] { PvpOpponentsFixture.NativeSql,
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y={Y},rotation=64," +
                "alignment=1,healthC=3000,healthM=3000,innerStrC=1000,innerStrM=1000 WHERE charId=110",
            $"UPDATE char_abilities SET level={Level} WHERE char_id=110",
            $"DELETE FROM inventory WHERE charId=110 AND slot={CombatFixtureWeapons.HandSlot}"
        }.Concat(Stands.Select(s => $"UPDATE characters SET x={s.X},y={Y},z={s.Z},alignment={s.Org}," +
            $"level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},healthC=3000,healthM=3000 WHERE charId={s.Id}")).ToArray(),
        DevCommands: true, testMobFixture: "martial-player-controls.csv", hotbarAbilities: new[] { Style, Burst },
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1",
            ["MXO_PVP_MAX_SAFE_LEVEL"] = "15", [AreaTargetDiagnostics.Variable] = "1",
            [MartialBlindEnrageAreaQa.KiBurstMode] = "1"
        },
        extraQaMetadata: new() { ["ScopedStockRows"] = "406", ["NativeCloseableRows"] = 1 },
        readyMessages: new[]
        {
            "Stay at HOME facing QAEnemy. /target QAEnemy, right-click Close Combat, then reselect Aikido on page 2 slot 1. No teleport.",
            """Start {"actor":"QAEnemy","action":"ki-withdraw"} in the background while the fight is active. It waits for your cast, so press Ki Burst slot 2 immediately.""",
            "Expect 70 IS, a 1 s cast, damage 31 and [MODIFIER] char=6114 source=406 stat=93 add 0 for 15 s. The helper submits ordinary Withdraw during Enrage and verifies the server [ENRAGE] withdraw suppressed char=6114 line. It waits for the exact paired state removal and localized release, then submits Withdraw again. Use the final response with Result.Status = KI BURST WITHDRAW PASS. Expect the first request suppressed and the second accepted for an ordinary round attempt. The win or loss of that attempt is outside the pass criteria.",
            "QA guarantees the Enrage proc on the disposable player witness only. The helper uses the ordinary Withdraw frame twice and never forces an exchange result."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        Console.WriteLine($"READY HOME {InvisibilityBeltWatcherMode.Warp(X, Y, Z, 64, inside: false)}");
        var server = context.ActualServer!;
        foreach (var s in Stands)
        {
            Console.WriteLine($"READY {s.Name} {InvisibilityBeltWatcherMode.Warp(s.X, Y, s.Z, 192, inside: false)}");
            Check.That(NavMesh.HasSightPath(1, X, Y, Z, s.X, Y, s.Z), $"Clear floor and sight to {s.Name}");
            var helper = await HeadlessPlayer.LoginAsync(context.Address, s.Name, s.Name, stockCharacterLoad: true);
            context.Helpers.Add(s.Name, helper);
            Check.Equal((uint)s.Id, helper.CharId, $"Correct helper {s.Name}");
            await PvpOpponentsFixture.EnterAsync(helper);
            NativeQaReadiness.RequireNoAmbientAggro(server, helper);
        }
        if (context.StockProbe is not { } caster) return Array.Empty<byte>();

        DisablingShotMode.CheckProfile(caster, Loadout, Level);
        await PvpOpponentsFixture.EnterAsync(caster);
        NativeQaReadiness.RequireNoAmbientAggro(server, caster);
        foreach (var s in Stands) await caster.WaitForViewNamedAsync(s.Name);

        var target = await caster.WaitForViewNamedAsync("QAEnemy");
        var enemy = context.Helpers["QAEnemy"];
        uint targetHandle = DuelPackets.Handle(target.ViewId, target.SpawnId);
        string hex = WireFormat.ToHex(BitConverter.GetBytes(targetHandle));
        caster.World.SendRpcs(StarterHackerControlTargetMode.WithHandle(PistolSlideMode.StockSelfDefense, targetHandle),
            WireFormat.FromHex($"40 {hex} 01 6b"));
        await caster.WaitForRpcAsync($"46 {hex} 01");
        caster.SendRpc(MartialArtsStarterAdmissionMode.StockAikido);
        await caster.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 65 00 00 00");
        int from = caster.World.RpcSnapshot().Count;
        int enemyFrom = enemy.World.RpcSnapshot().Count;
        Task proof = CheckWithdrawAsync(enemy, server);
        caster.SendRpc(StockBurst);
        await EvidenceRetestWire.CastBarAsync(caster, Burst, 1);
        await EvidenceRetestWire.TimerAsync(caster, from, Burst, AbilityCooldowns.OwnTimer, 15);
        await EvidenceRetestWire.TimerAsync(caster, from, 688, AbilityCooldowns.GroupTimer, 5);
        byte[] damage = AbilityMessages.Damage(false, Burst, caster.Handle, enemy.Handle, 31, 0);
        await ObserveAsync(enemy, enemyFrom, r => r.Bytes.AsSpan().SequenceEqual(damage), "Ki Burst hits the interlock opponent");
        await proof;
        Check.Equal(0, caster.World.Problems.Count + enemy.World.Problems.Count, "Clean retained stock framing");
        return Array.Empty<byte>();
    }

    internal static async Task<object> CheckWithdrawAsync(HeadlessPlayer enemy, ServerInstance server)
    {
        Check.Equal(6114u, enemy.CharId, "Ki Burst recipient helper");
        int from = enemy.World.RpcSnapshot().Count;
        RpcRecord? add = null;
        await enemy.WaitUntilAsync(() => (add = enemy.World.RpcSnapshot().Skip(from)
                .FirstOrDefault(r => Bonus(r, Burst, TempMods.EnrageAbility, 0, 15, false))) != null,
            "Ki Burst applies 15 s Enrage to QAEnemy", TimeSpan.FromSeconds(25));
        Check.That(ManageBonusRecord.TryDecode(add!.Bytes, out var record) && record != null, "Decode the original Enrage record");
        int blockedAt = server.DebugLog().Length;
        enemy.SkipReceived();
        enemy.SendRpc("44 01");
        enemy.SendRpc("81 48");
        await enemy.WaitForRpcAsync("81 49", HeadlessPlayer.DefaultTimeout);
        const string suppressed = "[ENRAGE] withdraw suppressed char=6114 actor=QAEnemy state=93";
        await enemy.WaitUntilAsync(() => server.DebugLog()[blockedAt..].Contains(suppressed, StringComparison.Ordinal),
            "Server confirms the first ordinary Withdraw was received and suppressed", HeadlessPlayer.DefaultTimeout);
        byte[] release = AbilityMessages.StateEnd(TempMods.EnrageAbility, enemy.Handle);
        await enemy.WaitUntilAsync(() =>
        {
            var received = enemy.World.RpcSnapshot().Skip(from).ToArray();
            return received.Any(r => ManageBonusRecord.TryDecode(r.Bytes, out var remove) &&
                    remove == record! with { AddRemoveType = 1 }) &&
                received.Any(r => r.Bytes.AsSpan().SequenceEqual(release));
        }, "Ki Burst sends the matched state removal and localized release", TimeSpan.FromSeconds(20));
        int acceptedAt = server.DebugLog().Length;
        enemy.SendRpc("44 01");
        await enemy.WaitUntilAsync(() => server.DebugLog()[acceptedAt..].Contains("[COMBAT] Leave combat requested", StringComparison.Ordinal),
            "Ordinary Withdraw accepted after Ki Burst Enrage ends", HeadlessPlayer.DefaultTimeout);
        const string pass = "KI BURST WITHDRAW PASS";
        Console.WriteLine($"{pass}: 15 s Enrage blocks ordinary Withdraw, matched release restores admission.");
        return new { Status = pass, Suppressed = suppressed, ReleaseObserved = true, RetryAdmitted = true };
    }
}
