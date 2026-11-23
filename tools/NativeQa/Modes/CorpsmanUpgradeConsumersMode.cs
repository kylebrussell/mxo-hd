using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class CorpsmanUpgradeConsumersMode : NativeQaMode
{
    private const int Level = 50, X = 17000, Y = 495, Z = 6400;
    private const ushort Hacker = 1006, Corpsman = 369, Upgrade = 1092, Repairs = 371, Insidious = 360, PatcherUpgrade = 1089;
    private static readonly (string Handle, int X, int Z)[] Stands =
    {
        ("QAFriend", X + 200, Z), ("QAFar", X, Z + 900), ("QAOther", X, Z + 950), ("QAEnemy", X, Z + 500)
    };
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, Hacker, Corpsman, Repairs, Insidious).Order().ToArray();
    private static readonly (ushort Stat, int Amount)[] UpgradeStats = { (564, 7), (127, 10), (174, 6) };

    // Retained AfterWhoruNeo proxy_024 lines 12135–12136, 12607–12608 and 12727–12728. Only the selected view/spawn changes.
    private const string RepairsStock = "80b973010d005a0000000000000000000000000000000000000000002000000078f21200086b0000000000";
    private const string UpgradeStock = "80b944040d005a0000000000000000000000000000000000000000002000000078f21200086b0000000000";
    private const string PatcherStock = "80b941040d005a0000000000000000000000000000000000000000000000150000c00f2d086b0000000000";
    // Retained Vector stock 360, same approved frame as StarterConcentrationBatchMode.
    private const string InsidiousStock = "80b96801130027000000000000000000000000000000000000000000000000204762ad41076b0000000000";

    public override string Name => "ability-parity-corpsman-upgrade-consumers";

    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), Array.Empty<string>(),
        new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y={Y},rotation=0," +
                "healthC=1000,healthM=5000,innerStrC=1000,innerStrM=2000,alignment=1,pvpflag=0 WHERE charId=110",
            $"UPDATE characters SET y={Y},healthC=100,healthM=5000,alignment=1,pvpflag=0 WHERE charId BETWEEN 6111 AND 6114",
            $"UPDATE characters SET x={Stands[0].X},z={Stands[0].Z} WHERE charId=6111",
            $"UPDATE characters SET x={Stands[1].X},z={Stands[1].Z} WHERE charId=6112",
            $"UPDATE characters SET x={Stands[2].X},z={Stands[2].Z} WHERE charId=6113",
            $"UPDATE characters SET x={Stands[3].X},z={Stands[3].Z},alignment=2 WHERE charId=6114",
            "DELETE FROM char_abilities WHERE char_id BETWEEN 6111 AND 6114"
        }, DevCommands: true, hotbarAbilities: new[] { Hacker, Repairs, Insidious, Upgrade, PatcherUpgrade }, environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            ["MXO_CONCENTRATION_QA_DAMAGE"] = "1", ["MXO_CAST_INTERRUPT_QA_ROLL"] = "79",
            ["MXO_FREECAST_VIRAL_QA"] = "1", ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766", ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "1092", ["NativeCloseableRows"] = 1, ["MemoryUsed"] = Loadout.Sum(AbilityMemory.Cost) },
        readyMessages: new[]
        {
            "Level 50, 100/100 memory, full Howitzer and Corpsman profiles. CSR opens on outdoor NAV (17000,495,6400) facing QAEnemy 5 m away. Accept QAFriend's team invitation. QAFar at 9 m and QAOther at 9.5 m are also teammates.",
            "Page 2 slots 1 to 5 are Hacker Style, Restore Group 2.0 (371), Insidious Code (360), Corpsman Upgrade (1092), and Team Patcher Upgrade (1089). Choose Hacker Style and /target QAEnemy. Stay out of interlock.",
            "Baseline: slot 2 heals CSR and QAFriend for 27 each, with no heal on either far teammate. Enter ?cd hurtme 100 next and use slot 3 on QAEnemy. Actual 100 HP loss uses Concentration 5, through 77 and roll 79. The cast interrupts without a completed effect or reuse.",
            "Keep QAEnemy selected and press slot 4. Expect CSR-only [MODIFIER] source=1092 adds 564 +7, 127 +10 and 174 +6, 17 IS after Dedicated Code (20 base), and 30 s reuse. Press slot 2 again. Restore Group now heals CSR, QAFriend and QAFar for 28 each, and excludes QAOther and the selected opponent.",
            "Re-arm ?cd hurtme 100 next and use slot 3 on the same opponent. Expect Concentration 12, through 81, roll 79, the usual landed 360 and 58 s own reuse. After the cast press slot 5. Expect a single removal of each 1092 modifier and the ordinary 1089 adds. Type ?dismissbuff 1089 and use slot 2 once more to confirm the baseline."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var server = context.ActualServer ?? throw new InvalidOperationException("Missing Corpsman fixture server");
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Missing NAV source");
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (var (x, z) in Stands.Select(stand => (stand.X, stand.Z)).Append((X, Z)))
            Check.That(NavMesh.Locate(1, x, Y, z, out var at) && !at.indoors && Math.Abs(at.floorY + 95 - Y) < 1,
                $"Stand ({x},{Y},{z}) is on outdoor NAV");

        foreach (var (handle, _, _) in Stands)
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, handle, handle, stockCharacterLoad: true);
            context.Helpers.Add(handle, helper);
            if (handle == "QAEnemy") await PvpOpponentsFixture.EnterAsync(helper);
            else await helper.EnterWorldAsync();
            NativeQaReadiness.RequireNoAmbientAggro(server, helper);
        }
        var leader = context.Helpers["QAFriend"];
        var edge = context.Helpers["QAFar"];
        var outside = context.Helpers["QAOther"];
        var enemy = context.Helpers["QAEnemy"];
        await TeamBreadthMode.InviteAsync(leader, edge);
        await TeamBreadthMode.InviteAsync(leader, outside);
        if (context.StockProbe is not { } actor)
        {
            _ = StarterTeamPatcherUpgradeRadiusMode.InviteNativeAsync(leader, server);
            return Array.Empty<byte>();
        }

        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level) &&
            archive.Keys.ToHashSet().SetEquals(Loadout) && Loadout.All(id => AbilityPrerequisites.Current.Closure(id)
                .All(need => archive.TryGetValue(need.Key, out var row) && row.Level >= need.Value)),
            "The profile and all ranked prerequisites fit without seeding active grants");
        await PvpOpponentsFixture.EnterAsync(actor);
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        await TeamBreadthMode.InviteAsync(leader, actor);
        var view = await actor.WaitForViewNamedAsync(enemy.Handle);
        uint targetHandle = DuelPackets.Handle(view.ViewId, view.SpawnId);
        byte[] WithTarget(string frame) => StarterHackerControlTargetMode.WithHandle(frame, targetHandle);
        actor.SendRpc(StarterHackerViralSightMode.StockStyle);
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 ee 03 00 00");
        actor.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(targetHandle))}");
        await StockAbilityProfile.ObserveAsync(actor, 0, StockAbilityProfile.GrantRecord(Upgrade, remove: false), "Corpsman grants 1092");

        await HealAsync(27, expanded: false);
        int initial = actor.World.RpcSnapshot().Count;
        int enemyInitial = enemy.World.RpcSnapshot().Count;
        await ArmAndCastAsync();
        await StockAbilityProfile.ObserveAsync(actor, initial, record => BytePattern.Parse("80 ad 68 01 00 00 02").IsMatch(record.Bytes),
            "Baseline Insidious interrupted by real damage");
        Check.That(server.DebugLog().Contains("loss=100 concentration=5 through=77 roll=79"), "Baseline concentration controls the cast");

        int apply = actor.World.RpcSnapshot().Count;
        int beforeUpgrade = server.DebugLog().Length;
        var helpersAt = context.Helpers.Values.ToDictionary(helper => helper, helper => helper.World.RpcSnapshot().Count);
        actor.SendRpc(WithTarget(UpgradeStock));
        await RecordsAsync(apply, remove: false);
        await EvidenceRetestWire.TimerAsync(actor, apply, Upgrade, AbilityCooldowns.OwnTimer, 30);
        string[] upgradeCosts = server.DebugLog()[beforeUpgrade..].Split('\n')
            .Where(line => line.Contains("[ABILITY-COST] char=110 ability=1092 ", StringComparison.Ordinal)).ToArray();
        Check.That(upgradeCosts.Length == 1 && upgradeCosts[0].Contains("paid=17 ", StringComparison.Ordinal),
            $"The 20 base IS cost is discounted once by Dedicated Code: {string.Join(' ', upgradeCosts)}");
        await HealAsync(28, expanded: true);

        int landed = actor.World.RpcSnapshot().Count;
        var landedEffect = BytePattern.Parse("80 bc ?? ?? ?? ?? 00 68 01 00 00 9d 00");
        Check.That(!enemy.World.RpcSnapshot().Skip(enemyInitial).Any(record => landedEffect.IsMatch(record.Bytes)),
            "The interrupted cast applied no enemy effect");
        Check.That(!actor.World.RpcSnapshot().Skip(initial).Take(landed - initial)
            .Any(record => BytePattern.Parse("80 bd 01 68 01").IsMatch(record.Bytes)), "The interrupted cast started no reuse");
        await ArmAndCastAsync();
        await EvidenceRetestWire.TimerAsync(actor, landed, Insidious, AbilityCooldowns.OwnTimer, 58);
        await StockAbilityProfile.ObserveAsync(enemy, enemyInitial, record => landedEffect.IsMatch(record.Bytes),
            "Insidious reaches the selected opponent with the upgrade");
        Check.That(server.DebugLog().Contains("loss=100 concentration=12 through=81 roll=79"), "The upgrade passes the otherwise identical concentration roll");
        foreach (var (helper, cursor) in helpersAt)
            Check.That(!helper.World.RpcSnapshot().Skip(cursor).Any(record => UpgradeStats.Any(stat =>
                StockAbilityProfile.Bonus(record, Upgrade, stat.Stat, stat.Amount, 0, false))), "Corpsman records are caster-only");

        int replace = actor.World.RpcSnapshot().Count;
        actor.SendRpc(WithTarget(PatcherStock));
        await RecordsAsync(replace, remove: true);
        await StockAbilityProfile.ObserveAsync(actor, replace, record =>
            StockAbilityProfile.Bonus(record, PatcherUpgrade, 127, 7, 0, false), "The archived successor replaces Corpsman");
        int dismiss = actor.World.RpcSnapshot().Count;
        actor.SendChat("?dismissbuff 1089");
        await StockAbilityProfile.ObserveAsync(actor, dismiss, record =>
            StockAbilityProfile.Bonus(record, PatcherUpgrade, 127, 7, 0, true), "The control removes the successor");
        await HealAsync(27, expanded: false);
        Check.Equal(0, actor.World.Problems.Count + context.Helpers.Values.Sum(helper => helper.World.Problems.Count),
            "Retained upgrade, heal and interrupt traffic frames cleanly");
        Console.WriteLine("CORPSMAN UPGRADE PASS: self +7/+10/+6, 27/28/27 healed, 9m team reach, and 77/81 interruption.");
        return Array.Empty<byte>();

        async Task ArmAndCastAsync()
        {
            actor.SkipReceived();
            actor.SendChat("?cd hurtme 100 next");
            await actor.WaitForTextAsync("Concentration damage armed: 100");
            actor.SendRpc(WithTarget(InsidiousStock));
            await EvidenceRetestWire.CastBarAsync(actor, Insidious, 4);
        }

        async Task HealAsync(int amount, bool expanded)
        {
            int log = server.DebugLog().Length;
            int cast = actor.World.RpcSnapshot().Count;
            actor.SkipReceived();
            actor.SendRpc(WithTarget(RepairsStock));
            await EvidenceRetestWire.CastBarAsync(actor, Repairs, 4);
            await EvidenceRetestWire.TimerAsync(actor, cast, 685, AbilityCooldowns.GroupTimer, 4, ended: true);
            string heal = server.DebugLog()[log..];
            string[] outcomes = heal.Split('\n').Where(line => line.Contains("[ABILITY] Heal 371 ->", StringComparison.Ordinal))
                .Select(line => line.TrimEnd()).ToArray();
            Console.WriteLine(string.Join(Environment.NewLine, outcomes));
            foreach (var player in new[] { actor, leader, edge, outside, enemy })
            {
                bool expected = player == actor || player == leader || player == edge && expanded;
                Check.Equal(expected, heal.Contains($"Heal 371 -> {player.Handle} for {amount}", StringComparison.Ordinal),
                    $"Restore Group {amount} to {player.Handle}");
                if (!expected)
                    Check.That(!heal.Contains($"Heal 371 -> {player.Handle} for ", StringComparison.Ordinal), $"No heal reaches {player.Handle}");
            }
        }

        async Task RecordsAsync(int cursor, bool remove)
        {
            foreach (var (stat, amount) in UpgradeStats)
                await StockAbilityProfile.ObserveAsync(actor, cursor, record => StockAbilityProfile.Bonus(record, Upgrade, stat, amount, 0, remove),
                    $"Corpsman {stat} value {amount}, remove={remove}");
        }
    }
}
