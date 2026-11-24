using hds;
using HeadlessClient;
using MxoHd.PacketResearch;
using static NativeQa.Modes.StarterHackerControlTargetMode;
using static NativeQa.StockAbilityProfile;

namespace NativeQa.Modes;

public sealed class AikidoGrandmasterControlsMode : NativeQaMode
{
    private const int X = 17000, Y = 495, Z = 6400, EnemyZ = 6600, Level = 50;
    private const ushort Aikido = 101, Probe = 198;
    private const string CheapShot = "80b9c600050003000000000000000000000000000000000000000000c6000000006b0000086b0000000000";
    private sealed record Sample(ushort Id, ushort State, float Seconds, string Frame);
    // PR473 native casts. Only the recipient-local handle is substituted.
    private static readonly Sample[] Rows =
    {
        new(407, TempMods.PowerlessAbility, 4.4f, "80b9970104000200000000000000000000000000000000000000000097010000006b0000086b0000000000"),
        new(411, TempMods.EnrageAbility, 15, "80b99b010400020000000000000000000000000000000000000000009b010000006b0000086b0000000000"),
        new(412, TempMods.EnrageAbility, 6, "80b99c010400020000000000000000000000000000000000000000009c010000006b0000086b0000000000")
    };
    private static readonly ushort[] Loadout = DisablingShotMode.Closure(2, 17, Aikido, 407, 411, 412);
    private static readonly ushort[] EnemyLoadout = DisablingShotMode.Closure(2, 17, Probe);

    public override string Name => "ability-parity-aikido-grandmaster-controls";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), Array.Empty<string>(), new[]
    {
        PvpOpponentsFixture.NativeSql,
        $"UPDATE characters SET x={X},y={Y},z={Z},rotation=0,level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},alignment=1,healthC=5000,healthM=5000,innerStrC=1200,innerStrM=1200 WHERE charId=110",
        $"UPDATE characters SET x={X},y={Y},z={EnemyZ},rotation=128,level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},alignment=2,healthC=5000,healthM=5000,innerStrC=1200,innerStrM=1200 WHERE charId=6114",
        $"UPDATE char_abilities SET level={Level} WHERE char_id=110",
        $"DELETE FROM inventory WHERE charId IN (110,6114) AND slot={CombatFixtureWeapons.HandSlot}",
        "DELETE FROM char_abilities WHERE char_id=6114",
        "INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES " +
            string.Join(",", EnemyLoadout.Select((id, i) => $"(6114,{400 + i},{AbilityMessages.AbilityGameObject(id)},{Level},1)"))
    }, DevCommands: true, testMobFixture: "martial-player-controls.csv", hotbarAbilities: new ushort[] { Aikido, 407, 411, 412 },
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            [CombatFixtureWeapons.AikidoControlsMode] = "1", [CombatFixtureWeapons.DisengageMode] = "1",
            ["MXO_CONDITIONAL_THROW_QA"] = "1"
        },
        extraQaMetadata: new() { ["ScopedStockRows"] = "407,411,412", ["NativeCloseableRows"] = 3 },
        readyMessages: new[]
        {
            "No tp. At verified HOME /target QAEnemy, right-click Close Combat, and reselect page 2 slot 1 Aikido. If QAEnemy hides behind CSR, strafe briefly and click before opening.",
            """For each row, prepare the state and win first. Start {"actor":"QAEnemy","action":"aikido-control","ability":407} in the background (substitute 411 or 412). It waits for your cast; press its slot immediately.""",
            "407: ?cd aw 407, then helper and slot 2. Expect 40 IS, state 96 4.4 s; Cheap Shot refused without debit, then admitted on the same recipient after paired release.",
            "411: ?cd offbalance, ?cd aw 411, then helper and slot 3. Expect 25 IS, source 411 stat93 for 15 s and caster source411 stat180 +40 for 15 s. Helper Withdraw is suppressed during Enrage and admitted after release. Wait for the withdrawal or use ?cd stop after the helper passes, then open Close Combat and reselect Aikido.",
            "412: ?cd offbalance, ?cd aw 412, then helper and slot 4. Expect 45 IS, source412 stat93 for 6 s and recipient stat180 -140 / 181 -100 for 12 s. Helper verifies Withdraw suppression, release and retry, plus matched stat removals.",
            "QA arms one win on the selected fight and ensures landed 411/412 modifier rolls on QAEnemy. Off Balance is fixture prep. Ordinary withdrawal outcomes are unmodified. Wait for each helper result before proceeding."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        Console.WriteLine($"READY HOME {InvisibilityBeltWatcherMode.Warp(X, Y, Z, 0, inside: false)}");
        Console.WriteLine($"READY QAEnemy {InvisibilityBeltWatcherMode.Warp(X, Y, EnemyZ, 128, inside: false)}");
        Check.That(NavMesh.HasSightPath(1, X, Y, Z, X, Y, EnemyZ), "Clear two-metre interlock site");
        var server = context.ActualServer!;
        var enemy = await HeadlessPlayer.LoginAsync(context.Address, "QAEnemy", "QAEnemy", stockCharacterLoad: true);
        context.Helpers.Add(enemy.Handle, enemy);
        await PvpOpponentsFixture.EnterAsync(enemy);
        DisablingShotMode.CheckProfile(enemy, EnemyLoadout, Level);
        NativeQaReadiness.RequireNoAmbientAggro(server, enemy);
        if (context.StockProbe is not { } caster) return Array.Empty<byte>();
        DisablingShotMode.CheckProfile(caster, Loadout, Level);
        await PvpOpponentsFixture.EnterAsync(caster);
        NativeQaReadiness.RequireNoAmbientAggro(server, caster);
        var opponent = await caster.WaitForViewNamedAsync(enemy.Handle);
        uint handle = DuelPackets.Handle(opponent.ViewId, opponent.SpawnId);
        string hex = WireFormat.ToHex(BitConverter.GetBytes(handle));
        await OpenAsync();
        foreach (var row in Rows)
        {
            if (row.Id != 407)
            {
                caster.SendChat("?cd offbalance");
                await caster.WaitForTextAsync("OffBalance fixture applied");
            }
            caster.SendChat($"?cd aw {row.Id}");
            await caster.WaitForTextAsync($"Next Aikido {row.Id} against QAEnemy is armed.");
            int from = caster.World.RpcSnapshot().Count;
            Task<object> witness = ObserveAsync(enemy, server, row.Id);
            caster.SendRpc(WithHandle(row.Frame, handle));
            await EvidenceRetestWire.TimerAsync(caster, from, row.Id, AbilityCooldowns.OwnTimer, 10);
            if (row.Id == 411)
            {
                var bonus = await StockAbilityProfile.ObserveAsync(caster, from, f => Bonus(f, 411, 180, 40, 15, false),
                    "411 adds the caster's +40 Melee Resistance for 15 s");
                await MatchRemovalAsync(caster, from, bonus);
            }
            await witness;
            if (row.Id == 411)
            {
                caster.SendChat("?cd stop");
                await caster.WaitForTextAsync("Fixture combat ended");
                await OpenAsync();
            }
        }
        Check.Equal(0, caster.World.Problems.Count + enemy.World.Problems.Count, "Clean retained stock framing");
        return Array.Empty<byte>();

        async Task OpenAsync()
        {
            int from = caster.World.RpcSnapshot().Count;
            caster.World.SendRpcs(WithHandle(PistolSlideMode.StockSelfDefense, handle), WireFormat.FromHex($"40 {hex} 01 6b"));
            await StockAbilityProfile.ObserveAsync(caster, from, rpc => rpc.Bytes is [0x46, ..] && rpc.Bytes.Length >= 6 && rpc.Bytes[5] == 1,
                "Stock Close Combat opened with QAEnemy");
            caster.SendRpc(WithHandle(MartialArtsStarterAdmissionMode.StockAikido, handle));
            await caster.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 65 00 00 00");
            caster.SendRpc($"81 51 {hex}");
        }
    }

    internal static async Task<object> ObserveAsync(HeadlessPlayer enemy, ServerInstance server, ushort ability)
    {
        Check.Equal(6114u, enemy.CharId, "Aikido recipient identity");
        var row = Rows.Single(sample => sample.Id == ability);
        int from = enemy.World.RpcSnapshot().Count;
        RpcRecord? add = null;
        await enemy.WaitUntilAsync(() => (add = enemy.World.RpcSnapshot().Skip(from).FirstOrDefault(f =>
            Bonus(f, ability, row.State, 0, row.Seconds, false))) != null, $"Recipient sees {ability}/{row.State} for {row.Seconds} s", TimeSpan.FromSeconds(30));
        var caster = await enemy.WaitForViewNamedAsync("CSR_Tester");
        byte[] attempt = row.State == TempMods.PowerlessAbility
            ? WithHandle(CheapShot, DuelPackets.Handle(caster.ViewId, caster.SpawnId))
            : WireFormat.FromHex("44 01");
        int logAt = server.DebugLog().Length;
        enemy.SkipReceived();
        enemy.SendRpc(attempt);
        if (row.State == TempMods.PowerlessAbility)
            await enemy.WaitForTextAsync("You cannot use abilities while powerless.");
        else
            await enemy.WaitUntilAsync(() => server.DebugLog()[logAt..].Contains("[ENRAGE] withdraw suppressed char=6114", StringComparison.Ordinal),
                "Ordinary Withdraw was received and suppressed for QAEnemy", HeadlessPlayer.DefaultTimeout);
        await MatchRemovalAsync(enemy, from, add!);
        byte[] closed = AbilityMessages.StateEnd(row.State, enemy.Handle);
        await StockAbilityProfile.ObserveAsync(enemy, from, f => f.Bytes.AsSpan().SequenceEqual(closed), "Localized state release");
        logAt = server.DebugLog().Length;
        int retryAt = enemy.World.RpcSnapshot().Count;
        enemy.SendRpc(attempt);
        if (row.State == TempMods.PowerlessAbility)
            await EvidenceRetestWire.TimerAsync(enemy, retryAt, Probe, AbilityCooldowns.OwnTimer, 20);
        else
            await enemy.WaitUntilAsync(() => server.DebugLog()[logAt..].Contains("[PVP] QAEnemy is withdrawing", StringComparison.Ordinal),
                "QAEnemy's ordinary retry is admitted", HeadlessPlayer.DefaultTimeout);
        if (ability == 412)
        {
            foreach (var (stat, value) in new (ushort, int)[] { (180, -140), (181, -100) })
            {
                var landed = await StockAbilityProfile.ObserveAsync(enemy, from, f => Bonus(f, 412, stat, value, 12, false), $"412 stat {stat} raw {value}");
                await MatchRemovalAsync(enemy, from, landed);
            }
        }
        Console.WriteLine($"AIKIDO CONTROL PASS {ability}");
        return new { Status = $"AIKIDO CONTROL PASS {ability}", row.Seconds, Blocked = true, Released = true, RetryAdmitted = true };
    }

    private static async Task MatchRemovalAsync(HeadlessPlayer witness, int from, RpcRecord add)
    {
        Check.That(ManageBonusRecord.TryDecode(add.Bytes, out var record) && record != null, "Decode the added record");
        await witness.WaitUntilAsync(() => witness.World.RpcSnapshot().Skip(from).Any(f =>
            ManageBonusRecord.TryDecode(f.Bytes, out var removed) && removed == record! with { AddRemoveType = 1 }),
            $"Matched expiry for {record!.SourceId}/{record.TargetId}", TimeSpan.FromSeconds(20));
    }
}
