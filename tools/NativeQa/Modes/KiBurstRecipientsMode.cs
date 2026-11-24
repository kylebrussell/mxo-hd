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
        extraQaMetadata: new() { ["ScopedStockRows"] = "406", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            "Stay at printed HOME. CSR faces QAEnemy at 1.2 m, QAFriend is inside 4 m and QAFar is at 5 m. All are live helpers. No teleport or interlock.",
            "Page 2 slot 1 Aikido. Select /target QAFar, then slot 2 Ki Burst. Expect a 1 s cast, 70 IS, and 15 s own / 5 s family 688 reuse. Burst uses CSR as its centre despite the selected distant player.",
            "Expect AREA-QA QAEnemy included, QAFriend pvp-NotOpponent, QAFar radius. Only QAEnemy takes Ki Burst damage. Expect [MODIFIER] char=6114 source=406 stat=93 add 0 for 15 s, then the matching remove. No helper command.",
            "QA guarantees only the disposable 406 Enrage roll on QAEnemy. The pass covers area targeting and recipient-state delivery and expiry, with other Enrage behavior tracked separately."
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

        var far = await caster.WaitForViewNamedAsync("QAFar");
        var enemy = context.Helpers["QAEnemy"];
        var friend = context.Helpers["QAFriend"];
        var farHelper = context.Helpers["QAFar"];
        caster.SendRpc(MartialArtsStarterAdmissionMode.StockAikido);
        await caster.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 65 00 00 00");
        caster.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(DuelPackets.Handle(far.ViewId, far.SpawnId)))}");
        int logStart = server.DebugLog().Length, from = caster.World.RpcSnapshot().Count;
        int enemyFrom = enemy.World.RpcSnapshot().Count, friendFrom = friend.World.RpcSnapshot().Count, farFrom = farHelper.World.RpcSnapshot().Count;
        caster.SendRpc(StockBurst);
        await EvidenceRetestWire.CastBarAsync(caster, Burst, 1);
        await EvidenceRetestWire.TimerAsync(caster, from, Burst, AbilityCooldowns.OwnTimer, 15);
        await EvidenceRetestWire.TimerAsync(caster, from, 688, AbilityCooldowns.GroupTimer, 5);
        byte[] damage = AbilityMessages.Damage(false, Burst, caster.Handle, enemy.Handle, 31, 0);
        await ObserveAsync(enemy, enemyFrom, r => r.Bytes.AsSpan().SequenceEqual(damage), "Only the inside hostile receives the 31 damage hit");
        var add = await ObserveAsync(enemy, enemyFrom, r => Bonus(r, Burst, TempMods.EnrageAbility, 0, 15, false), "QAEnemy receives 15 s Enrage");
        ManageBonusRecord.TryDecode(add.Bytes, out var record);
        Check.That(record != null, "Decode the original Enrage record");
        await enemy.WaitUntilAsync(() => enemy.World.RpcSnapshot().Skip(enemyFrom).Any(r =>
            ManageBonusRecord.TryDecode(r.Bytes, out var removal) && removal == record! with { AddRemoveType = 1 }),
            "The matching Enrage record expires", TimeSpan.FromSeconds(20));

        string[] ledger = server.DebugLog()[logStart..].Split('\n');
        Check.That(Has("QAEnemy", "included") && Has("QAFriend", "pvp-NotOpponent") && Has("QAFar", "radius"),
            "Ki Burst includes only the in-range hostile, independently of selection");
        foreach (var (witness, cursor) in new[] { (friend, friendFrom), (farHelper, farFrom) })
        {
            Check.That(!witness.World.RpcSnapshot().Skip(cursor).Any(r => Bonus(r, Burst, TempMods.EnrageAbility, 0, 15, false)),
                $"{witness.Handle} receives no Enrage");
        }
        Console.WriteLine("KI BURST RECIPIENTS PASS: exact stock cast, radius and PvP split, timed Enrage add/remove.");
        return Array.Empty<byte>();

        bool Has(string name, string reason) => ledger.Any(line => line.Contains("[AREA-QA]", StringComparison.Ordinal) &&
            line.Contains($"candidate={name} ", StringComparison.Ordinal) && line.Contains($"reason={reason}", StringComparison.Ordinal));
    }
}
