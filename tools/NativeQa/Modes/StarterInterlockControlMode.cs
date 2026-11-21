using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class StarterInterlockControlMode : NativeQaMode
{
    private const int X = 24800, Y = 495, Z = -1498, EnemyZ = -1798, Rank = 19;
    private static readonly byte CasterYaw = MobAggro.YawTowards(X, Z, X, EnemyZ);
    private static readonly byte EnemyYaw = MobAggro.YawTowards(X, EnemyZ, X, Z);
    private static readonly Position EnemyStand = new(X, Y, EnemyZ);
    private static readonly byte[] PositionHandoff = Convert.FromHexString("0200010c");
    private readonly record struct Position(float X, float Y, float Z)
    {
        internal string Hex => WireFormat.ToHex(new[] { X, Y, Z }.SelectMany(BitConverter.GetBytes).ToArray());
    }
    private const ushort Smash = 145, Cover = 176, Body = 105;
    private const string StockOpen = "80b911000400020000000000000000000000000000000000000000000000000000000000086b0000000000";
    private const string StockWithdraw = "44 01";
    private sealed record Sample(ushort Id, ushort Style, uint Weapon, string WeaponName, string StockStyle, string StockUse,
        ushort State, int Seconds, int Reuse);
    private static readonly Sample[] Samples =
    {
        FromCapture(StarterSoldierResistanceMode.Samples.Single(sample => sample.Id == Smash), 93, 10),
        FromCapture(StarterGunmanResistanceMode.Samples.Single(sample => sample.Id == Cover), 99, 8),
        FromCapture(StarterGunmanResistanceMode.Samples.Single(sample => sample.Id == Body), 180, 10)
    };
    private static Sample FromCapture(StarterSoldierResistanceMode.Sample sample, ushort state, int seconds) =>
        new(sample.Id, sample.Style, sample.Goid, sample.Weapon, sample.StockStyle, sample.StockUse, state, seconds, sample.Reuse);

    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(Samples.SelectMany(row => new[] { row.Style, row.Id })
        .Concat(new ushort[] { 2, 17 }).ToArray()).Order().ToArray();
    private static readonly int Memory = Loadout.Sum(AbilityMemory.Cost);

    public override string Name => "ability-parity-starter-interlock-control-procs";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), Array.Empty<string>(),
        new[]
        {
            PvpOpponentsFixture.NativeSql,
            CombatFixtureLoadout.SeedWeapons,
            $"UPDATE characters SET x={X},y={Y},z={Z},rotation={CasterYaw},healthC=30000,healthM=30000 WHERE charId=110",
            $"UPDATE characters SET x={X},y={Y},z={EnemyZ},rotation={EnemyYaw},healthC=30000,healthM=30000 WHERE charId=6114",
            $"UPDATE characters SET x={X + 200},y={Y},z={Z + 100} WHERE charId=6111",
            $"DELETE FROM inventory WHERE charId=6114 AND slot={CombatFixtureWeapons.HandSlot}",
            "DELETE FROM char_abilities WHERE char_id=6114",
            "INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES (6114,400,2147485696,19,1),(6114,401,2147501056,19,1)",
            $"UPDATE char_abilities SET level={Rank} WHERE char_id=110"
        }, DevCommands: true, hotbarAbilities: Samples.SelectMany(sample => new[] { sample.Style, sample.Id }).ToArray(),
        testMobFixture: "martial-player-controls.csv", environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0",
            ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            [CombatFixtureWeapons.WeaponMode] = "1", [CombatFixtureWeapons.DisengageMode] = "1",
            [StarterInterlockControlQa.Mode] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "145,176,105", ["NativeCloseableRows"] = 0, ["MemoryUsed"] = Memory },
        readyMessages: new[]
        {
            $"The full profile uses {Memory}/100 memory at level 50 with rank-{Rank} abilities. CSR faces QAEnemy at {X},{Y},{EnemyZ}, 3 m ahead on the street. Both have 30,000 max HP. The fixture loads an empty ambient NPC roster. Page 2 comes in pairs: Karate/145, SMGs/176, Rifles/105.",
            "Equip the listed weapon, select QAEnemy, open ordinary Close Combat and then select the matching style. If a special misses, wait for its own reuse and retry it. The fixture forces the Enrage and Root procs only on landed specials. Enter submits chat. Keep the selection without Escape.",
            "Overhand Smash 145 already passed native QA. Start this retest with Covering Fire 176. The headless bootstrap still checks the full 145 Withdraw sequence.",
            "Use ?combatdepth fixture-weapon smg, open Close Combat and reselect SMGs. Use Covering Fire. On char=6114 expect [MODIFIER] source=176 stat=99 value=0 for 8 s. As soon as it lands, enter ?combatdepth fixture-disengage so follow-up interlock hits cannot break the root. The helper measures its current position from the server correction, then attempts the same step during root and again on expiry. Wait for STARTER CONTROL PASS 176.",
            "Use ?combatdepth fixture-weapon rifle, re-open Close Combat and reselect Rifles. Use Body Shot. Expect [MODIFIER] char=6114 source=105 stat=180 and stat=181 both -5 for 10 s, without a root or enrage from this attack. Both records remove on expiry. Wait for STARTER CONTROL PASS 105."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var server = context.ActualServer!;
        var enemy = await HeadlessPlayer.LoginAsync(context.Address, "QAEnemy", "QAEnemy", stockCharacterLoad: true);
        context.Helpers.Add(enemy.Handle, enemy);
        await PvpOpponentsFixture.EnterAsync(enemy);
        NativeQaReadiness.RequireNoAmbientAggro(server, enemy);
        var observer = await HeadlessPlayer.LoginAsync(context.Address, "QAFriend", "QAFriend", stockCharacterLoad: true);
        context.Helpers.Add(observer.Handle, observer);
        await observer.EnterWorldAsync();
        var observed = await observer.WaitForViewNamedAsync(enemy.Handle);
        NativeQaReadiness.RequireNoAmbientAggro(server, observer);
        if (context.StockProbe is not { } actor)
        {
            _ = WatchAsync(context, enemy, observer, observed.ViewId);
            return Array.Empty<byte>();
        }

        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor), row => row.Level);
        Check.That(Memory <= AbilityMemory.ForLevel(50) && archive.Keys.ToHashSet().SetEquals(Loadout) &&
            Loadout.All(id => AbilityPrerequisites.Current.Closure(id).All(need =>
                archive.TryGetValue(need.Key, out ushort rank) && rank >= need.Value)), "The stock profile fits and includes all ranked prerequisites");
        await PvpOpponentsFixture.EnterAsync(actor);
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        var view = await actor.WaitForViewNamedAsync(enemy.Handle);
        string handle = WireFormat.ToHex(BitConverter.GetBytes(DuelPackets.Handle(view.ViewId, view.SpawnId)));
        await enemy.WaitForViewNamedAsync(actor.Handle);

        foreach (var row in Samples)
        {
            Position? expectedRootPosition = null;
            if (row.Id == Cover)
            {
                // Reproduce a helper move made between rows. Root must use this position, not the spawn.
                var relocated = EnemyStand with { X = EnemyStand.X + 60, Z = EnemyStand.Z - 20 };
                int beforeMove = observer.World.ViewSnapshot().Count;
                Move(enemy, relocated);
                await ObserveMoveAsync(observer, observed.ViewId, beforeMove, relocated, "Helper is repositioned before Covering Fire");
                expectedRootPosition = relocated;
            }
            await CombatFixtureLoadout.VerifyWeaponSwapAsync(actor, context.OwnedDatabase!, row.WeaponName, row.Weapon);
            int openedAt = actor.World.RpcSnapshot().Count;
            actor.World.SendRpcs(StarterHackerControlTargetMode.WithHandle(StockOpen, DuelPackets.Handle(view.ViewId, view.SpawnId)),
                WireFormat.FromHex($"40 {handle} 01 6b"));
            await StockAbilityProfile.ObserveAsync(actor, openedAt,
                rpc => rpc.Bytes is [0x46, ..] && rpc.Bytes.Length >= 6 && rpc.Bytes[5] == 1, "Player interlock opened");
            actor.SkipReceived();
            actor.SendRpc(StarterHackerControlTargetMode.WithHandle(row.StockStyle, DuelPackets.Handle(view.ViewId, view.SpawnId)));
            await actor.WaitForViewUpdateAsync(2, $"02 80 80 80 80 80 10 {StockAbilityProfile.LittleEndianHex(row.Style)} 00 00");

            int recipientAt = enemy.World.RpcSnapshot().Count;
            await UntilLandedAsync(actor, enemy, row, DuelPackets.Handle(view.ViewId, view.SpawnId), recipientAt);
            if (row.Id == Cover)
            {
                actor.SendChat("?combatdepth fixture-disengage");
                await actor.WaitForTextAsync("Fixture combat ended.");
            }
            await VerifyRecipientAsync(server, enemy, observer, observed.ViewId, row, recipientAt, expectedRootPosition);
        }
        Check.Equal(0, actor.World.Problems.Count + enemy.World.Problems.Count, "Interlock control replay framing");
        return Array.Empty<byte>();
    }

    private static async Task UntilLandedAsync(HeadlessPlayer actor, HeadlessPlayer enemy, Sample row, uint handle, int viewAt)
    {
        for (int attempt = 0; attempt < 6; attempt++)
        {
            int start = actor.World.RpcSnapshot().Count;
            actor.SendRpc(StarterHackerControlTargetMode.WithHandle(row.StockUse, handle));
            await EvidenceRetestWire.TimerAsync(actor, start, row.Id, AbilityCooldowns.OwnTimer, row.Reuse);
            await actor.WaitUntilAsync(() => HasBonus(enemy, viewAt, row, row.State, row.Id == Body ? -5 : 0, false) ||
                actor.World.RpcSnapshot().Skip(start).Any(packet => packet.Bytes.AsSpan().SequenceEqual(
                    AbilityCooldowns.BuildTimer(row.Id, AbilityCooldowns.OwnTimer, row.Reuse, false))),
                $"Effect or new reuse window for {row.Id}", TimeSpan.FromSeconds(row.Reuse + 5));
            if (HasBonus(enemy, viewAt, row, row.State, row.Id == Body ? -5 : 0, false)) return;
        }
        throw new InvalidOperationException($"Ability {row.Id} missed six times.");
    }

    private static bool HasBonus(HeadlessPlayer enemy, int from, Sample row, ushort stat, int value, bool remove) =>
        enemy.World.RpcSnapshot().Skip(from).Any(frame => StockAbilityProfile.Bonus(frame, row.Id, stat, value, row.Seconds, remove));

    private static async Task VerifyRecipientAsync(ServerInstance server, HeadlessPlayer enemy, HeadlessPlayer observer,
        ushort view, Sample row, int from, Position? expectedRootPosition = null)
    {
        if (row.Id == Smash)
        {
            int at = server.DebugLog().Length;
            enemy.SkipReceived();
            enemy.SendRpc(StockWithdraw);
            enemy.SendRpc("81 48");
            await enemy.WaitForRpcAsync("81 49");
            Check.That(!server.DebugLog()[at..].Contains("[PVP] QAEnemy is withdrawing"), "Enrage refuses stock Withdraw");
            await AwaitBonusAsync(522, -10, false);
            await AwaitBonusAsync(1009, -10, false);
            await AwaitBonusAsync(row.State, 0, true);
            at = server.DebugLog().Length;
            enemy.SendRpc(StockWithdraw);
            await enemy.WaitUntilAsync(() => server.DebugLog()[at..].Contains("[PVP] QAEnemy is withdrawing"),
                "Stock Withdraw reaches the PvP fight after Enrage expires", HeadlessPlayer.DefaultTimeout);
        }
        else if (row.Id == Cover)
        {
            // A rooted movement correction carries the authoritative current position, including
            // any ordinary helper move or teleport made since login.
            Position origin = await CorrectedPositionAsync(enemy, EnemyStand with { X = EnemyStand.X - 20 });
            if (expectedRootPosition.HasValue)
                Check.Equal(expectedRootPosition.Value, origin, "The first rooted correction reports the relocated position");
            Position step = origin with { X = origin.X - 20 };
            Check.Equal(origin, await CorrectedPositionAsync(enemy, step),
                "Root refuses a step relative to the helper's current position");

            await AwaitBonusAsync(row.State, 0, true);
            int visibleAt = observer.World.ViewSnapshot().Count;
            Move(enemy, step);
            await ObserveMoveAsync(observer, view, visibleAt, step, "An observer sees the same helper move after root expiry");
            Move(enemy, origin);
            Console.WriteLine($"STARTER ROOT POSITION {origin.X},{origin.Y},{origin.Z} held, then released to {step.X},{step.Y},{step.Z}");
        }
        else
        {
            await AwaitBonusAsync(181, -5, false);
            await AwaitBonusAsync(180, -5, true);
            await AwaitBonusAsync(181, -5, true);
        }
        Console.WriteLine($"STARTER CONTROL PASS {row.Id}");

        Task AwaitBonusAsync(ushort stat, int value, bool remove) =>
            enemy.WaitUntilAsync(() => HasBonus(enemy, from, row, stat, value, remove),
                $"Recipient {row.Id}/{stat} value={value} remove={remove}", TimeSpan.FromSeconds(row.Seconds + 5));
    }

    private static void Move(HeadlessPlayer actor, Position to) =>
        actor.World.SendViewList(WireFormat.FromHex($"02 00 01 08 {to.Hex} 00 00"));

    private static Task ObserveMoveAsync(HeadlessPlayer observer, ushort view, int from, Position to, string description)
    {
        var packet = BytePattern.Parse($"{StockAbilityProfile.LittleEndianHex(view)} 01 08 {to.Hex}");
        return observer.WaitUntilAsync(() => observer.World.ViewSnapshot().Skip(from).Any(packet.Occurs),
            description, HeadlessPlayer.DefaultTimeout);
    }

    private static async Task<Position> CorrectedPositionAsync(HeadlessPlayer enemy, Position probe)
    {
        int from = enemy.World.ViewSnapshot().Count;
        Move(enemy, probe);
        Position? corrected = null;
        await enemy.WaitUntilAsync(() =>
        {
            foreach (byte[] frame in enemy.World.ViewSnapshot().Skip(from))
            {
                int start = BytePattern.IndexOf(frame, PositionHandoff, 0);
                if (start < 0 || frame.Length < start + 21) continue;
                // Four prefix bytes, one yaw byte, XYZ float32, then a four-byte clock.
                corrected = new Position(BitConverter.ToSingle(frame, start + 5), BitConverter.ToSingle(frame, start + 9),
                    BitConverter.ToSingle(frame, start + 13));
                return true;
            }
            return false;
        }, "The server corrects helper movement while rooted", TimeSpan.FromSeconds(3));
        return corrected!.Value;
    }

    private static async Task WatchAsync(NativeQaBootstrapContext context, HeadlessPlayer enemy, HeadlessPlayer observer, ushort view)
    {
        var server = context.ActualServer!;
        try
        {
            int next = enemy.World.RpcSnapshot().Count;
            // The stock 145 pass carries over. Headless replay still verifies all three rows.
            foreach (var row in Samples.Where(row => row.Id != Smash))
            {
                Console.WriteLine($"STARTER CONTROL WAITING {row.Id}");
                int from = next;
                while (!server.HasExited && !File.Exists(Path.Combine(context.OutputDirectory, "stop")) &&
                    !HasBonus(enemy, from, row, row.State, row.Id == Body ? -5 : 0, false))
                    await Task.Delay(100);
                if (server.HasExited || File.Exists(Path.Combine(context.OutputDirectory, "stop"))) return;
                await VerifyRecipientAsync(server, enemy, observer, view, row, from);
                next = enemy.World.RpcSnapshot().Count;
            }
        }
        catch (Exception error)
        {
            await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "starter-interlock-control-recipient-error.txt"), error.ToString());
            Console.WriteLine($"STARTER CONTROL CHECK FAILED {error.Message}");
        }
    }
}
