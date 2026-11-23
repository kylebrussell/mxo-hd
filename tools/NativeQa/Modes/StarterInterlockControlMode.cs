using System.Text.Json;
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
    internal readonly record struct Position(float X, float Y, float Z)
    {
        internal string Hex => WireFormat.ToHex(new[] { X, Y, Z }.SelectMany(BitConverter.GetBytes).ToArray());
    }
    private const ushort Smash = 145, Cover = 176, Body = 105;
    private const string NativeResults = "starter-interlock-control-recipient.jsonl";
    private sealed record RootReadback(Position Held, Position Released, float ScaleWhileRooted, float ScaleAfterRoot);
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
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "145,176,105", ["NativeCloseableRows"] = 1, ["MemoryUsed"] = Memory },
        readyMessages: new[]
        {
            $"The full profile uses {Memory}/100 memory at level 50 with rank-{Rank} abilities. CSR faces QAEnemy at {X},{Y},{EnemyZ}, 3 m ahead on the street. Both have 30,000 max HP. The fixture loads an empty ambient NPC roster. Page 2 comes in pairs: Karate/145, SMGs/176, Rifles/105.",
            "Equip the listed weapon, select QAEnemy, open ordinary Close Combat and then select the matching style. If a special misses, wait for its own reuse and retry it. The fixture forces the Enrage and Root procs only on landed specials. Enter submits chat. Keep the selection without Escape.",
            "Only the Covering Fire 176 retest is required. Overhand Smash 145 and Body Shot 105 are existing regression controls.",
            "Use ?cd w smg, select /target QAEnemy and use its right-click Close Combat. Reselect SMGs (page 2 slot 3), then Covering Fire (slot 4). Retain the stock 176 request and look for the caster's localized Root message. Expect 25 IS, 4 s / 8 s / 5 s (689), 21083, and [MODIFIER] char=6114 source=176 stat=99 value=0 for 8 s. Use ?cd stop on landing before follow-up hits break the root. The helper verifies its recipient Root message, scale -1 on both views, movement correction, a single existing Rooted state-end and scale/movement recovery. Wait for STARTER CONTROL PASS 176.",
            "Optional Body Shot control: use ?cd w rifle, re-open Close Combat and reselect Rifles. Expect [MODIFIER] char=6114 source=105 stats 180 and 181 each -5 for 10 s, paired removals, and no root or enrage. Wait for STARTER CONTROL PASS 105.",
            $"{NativeResults} in the fixture directory records the helper's waiting, verified, failed or stopped status and any movement readback."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (var (x, z) in new[] { (X, Z), (X, EnemyZ), (X + 60, EnemyZ - 20) })
            Check.That(NavMesh.Locate(1, x, Y, z, out var stand) && !stand.indoors && Math.Abs(stand.floorY + 95 - Y) < 1,
                $"Verified outdoor interlock stand ({x},{Y},{z})");
        Check.That(NavMesh.HasSightPath(1, X, Y, Z, X, Y, EnemyZ), "The initial helper and caster have clear sight");
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

            int casterAt = actor.World.RpcSnapshot().Count;
            int recipientAt = enemy.World.RpcSnapshot().Count;
            var scaleAt = (Owner: enemy.World.ViewSnapshot().Count, Observer: observer.World.ViewSnapshot().Count);
            await UntilLandedAsync(actor, enemy, row, DuelPackets.Handle(view.ViewId, view.SpawnId), recipientAt);
            if (row.Id == Cover)
            {
                actor.SendChat("?combatdepth fixture-disengage");
                await actor.WaitForTextAsync("Fixture combat ended.");
                byte[] message = AbilityMessages.AboutAbility(AbilityMessages.RootCaster, true, Cover, actor.Handle, enemy.Handle, 50, 0);
                await StockAbilityProfile.ObserveAsync(actor, casterAt, frame => frame.Bytes.AsSpan().SequenceEqual(message),
                    "Covering Fire's caster receives the authored Root line once");
                Check.Equal(1, actor.World.RpcSnapshot().Skip(casterAt).Count(frame => frame.Bytes.AsSpan().SequenceEqual(message)),
                    "One caster line for the landed Root proc");
            }
            await VerifyRecipientAsync(server, enemy, observer, observed.ViewId, row, recipientAt, scaleAt, expectedRootPosition);
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

    private static async Task<RootReadback?> VerifyRecipientAsync(ServerInstance server, HeadlessPlayer enemy, HeadlessPlayer observer,
        ushort view, Sample row, int from, (int Owner, int Observer) scaleAt, Position? expectedRootPosition = null)
    {
        RootReadback? readback = null;
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
            byte[] root = AbilityMessages.AboutAbility(AbilityMessages.RootTarget, false, Cover, "CSR_Tester", enemy.Handle, 50, 0);
            await StockAbilityProfile.ObserveAsync(enemy, from, frame => frame.Bytes.AsSpan().SequenceEqual(root),
                "Covering Fire's recipient receives the authored Root line and 50 percent break chance");
            int ownerRoot = await ObserveScaleAsync(enemy, 2, PlayerViewAttributes.SelfMovementScale, scaleAt.Owner, -1);
            int observerRoot = await ObserveScaleAsync(observer, view, PlayerViewAttributes.OtherMovementScale, scaleAt.Observer, -1);
            // A rooted movement correction carries the authoritative current position, including
            // any ordinary helper move or teleport made since login.
            Position origin = await CorrectedPositionAsync(enemy, EnemyStand with { X = EnemyStand.X - 20 });
            if (expectedRootPosition.HasValue)
                Check.Equal(expectedRootPosition.Value, origin, "The first rooted correction reports the relocated position");
            Position step = origin with { X = origin.X - 20 };
            Check.Equal(origin, await CorrectedPositionAsync(enemy, step),
                "Root refuses a step relative to the helper's current position");

            await AwaitBonusAsync(row.State, 0, true);
            byte[] stateEnd = AbilityMessages.StateEnd(row.State, enemy.Handle);
            await StockAbilityProfile.ObserveAsync(enemy, from, frame => frame.Bytes.AsSpan().SequenceEqual(stateEnd),
                "Covering Fire ends with the ordinary Rooted release message");
            await ObserveScaleAsync(enemy, 2, PlayerViewAttributes.SelfMovementScale, ownerRoot, 0);
            await ObserveScaleAsync(observer, view, PlayerViewAttributes.OtherMovementScale, observerRoot, 0);
            int visibleAt = observer.World.ViewSnapshot().Count;
            Move(enemy, step);
            await ObserveMoveAsync(observer, view, visibleAt, step, "An observer sees the same helper move after root expiry");
            Move(enemy, origin);
            Check.Equal(1, enemy.World.RpcSnapshot().Skip(from).Count(frame => frame.Bytes.AsSpan().SequenceEqual(stateEnd)),
                "One Rooted state-end for the completed Covering Fire proc");
            Console.WriteLine($"STARTER ROOT POSITION {origin.X},{origin.Y},{origin.Z} held, then released to {step.X},{step.Y},{step.Z}. Owner and observer MovementScale -1 then 0.");
            readback = new RootReadback(origin, step, -1, 0);
        }
        else
        {
            await AwaitBonusAsync(181, -5, false);
            await AwaitBonusAsync(180, -5, true);
            await AwaitBonusAsync(181, -5, true);
        }
        Console.WriteLine($"STARTER CONTROL PASS {row.Id}");
        return readback;

        Task AwaitBonusAsync(ushort stat, int value, bool remove) =>
            enemy.WaitUntilAsync(() => HasBonus(enemy, from, row, stat, value, remove),
                $"Recipient {row.Id}/{stat} value={value} remove={remove}", TimeSpan.FromSeconds(row.Seconds + 5));
    }

    internal static void Move(HeadlessPlayer actor, Position to) =>
        actor.World.SendViewList(WireFormat.FromHex($"02 00 01 08 {to.Hex} 00 00"));

    internal static Task ObserveMoveAsync(HeadlessPlayer observer, ushort view, int from, Position to, string description)
    {
        var packet = BytePattern.Parse($"{StockAbilityProfile.LittleEndianHex(view)} 01 08 {to.Hex}");
        return observer.WaitUntilAsync(() => observer.World.ViewSnapshot().Skip(from).Any(packet.Occurs),
            description, HeadlessPlayer.DefaultTimeout);
    }

    internal static async Task<int> ObserveScaleAsync(HeadlessPlayer player, ushort view, int attribute, int from, float scale)
    {
        var wire = PlayerViewAttributes.Build((attribute, PlayerViewAttributes.F32(scale)));
        var pattern = BytePattern.Parse($"{StockAbilityProfile.LittleEndianHex(view)} {WireFormat.ToHex(wire)}");
        int match = -1;
        await player.WaitUntilAsync(() => (match = player.World.ViewSnapshot().FindIndex(from, pattern.Occurs)) >= 0,
            $"view {view} shows MovementScale {scale}", HeadlessPlayer.DefaultTimeout);
        return match + 1;
    }

    internal static async Task<Position> CorrectedPositionAsync(HeadlessPlayer enemy, Position probe)
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
        string stop = Path.Combine(context.OutputDirectory, "stop");
        string results = Path.Combine(context.OutputDirectory, NativeResults);
        ushort? watching = null;
        try
        {
            int next = enemy.World.RpcSnapshot().Count;
            // The stock 145 pass carries over. Headless replay still verifies all three rows.
            foreach (var row in Samples.Where(row => row.Id != Smash))
            {
                watching = row.Id;
                int from = next;
                var scaleAt = (Owner: enemy.World.ViewSnapshot().Count, Observer: observer.World.ViewSnapshot().Count);
                await RecordAsync("Waiting");
                Console.WriteLine($"STARTER CONTROL WAITING {row.Id}");
                while (!server.HasExited && !File.Exists(stop) &&
                    !HasBonus(enemy, from, row, row.State, row.Id == Body ? -5 : 0, false))
                    await Task.Delay(100);
                if (server.HasExited || File.Exists(stop))
                {
                    await RecordAsync("Stopped");
                    return;
                }
                var movement = await VerifyRecipientAsync(server, enemy, observer, view, row, from, scaleAt);
                next = enemy.World.RpcSnapshot().Count;
                await RecordAsync("Verified", movement);
            }
        }
        catch (Exception error)
        {
            Console.WriteLine($"STARTER CONTROL CHECK FAILED {error.Message}");
            await RecordAsync("Failed", error: error.Message);
            await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "starter-interlock-control-recipient-error.txt"), error.ToString());
        }

        Task RecordAsync(string status, RootReadback? movement = null, string? error = null) =>
            File.AppendAllTextAsync(results, JsonSerializer.Serialize(new
            {
                At = DateTimeOffset.UtcNow, Ability = watching, Recipient = enemy.CharId, Status = status, Movement = movement, Error = error
            }) + Environment.NewLine);
    }
}
