using System.Buffers.Binary;
using System.Diagnostics;
using System.Text.Json;
using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class MartialPlayerControlMode : NativeQaMode
{
    private const ushort Probe = 198;
    private const int CasterX = CombatFixtureLoadout.CasterX, StandY = CombatFixtureLoadout.GroundY,
        StandZ = CombatFixtureLoadout.CasterZ, RecipientX = 24499;
    private static readonly byte CasterYaw = MobAggro.YawTowards(CasterX, StandZ, RecipientX, StandZ);
    private static readonly byte RecipientYaw = MobAggro.YawTowards(RecipientX, StandZ, CasterX, StandZ);
    // Style and close-combat inputs from the native PR555/561 sessions.
    private const string StockSelfDefense = "80b911000400020000000000000000000000000000000000000000000000000000000000086b0000000000";
    private const string StockKungFu = "80b985000400020000000000000000000000000000000000000000000000000000000000086b0000000000";
    private const string StockKarate = "80b984000400020000000000000000000000000000000000000000000000000000000000086b0000000000";
    private const string StockCheapShot = "80b9c600050003000000000000000000000000000000000000000000c6000000006b0000086b0000000000";
    private static readonly TimeSpan Hold = TimeSpan.FromSeconds(10);
    private sealed record Sample(ushort Id, ushort State, float Seconds, string? Setup = null)
    {
        public float ImmunitySeconds => MathF.Floor(Seconds);
    }
    private static readonly Sample[] KungFu =
    {
        new(259, 96, 6.12f, "Daze"), new(427, 96, 6.12f, "OffBalance"), new(430, 96, 6.12f, "Daze"),
        new(262, 102, 4.4f, "Stagger"), new(429, 102, 4)
    };
    private static readonly Sample[] Karate =
    {
        new(536, 96, 6, "Stagger"), new(265, 102, 4, "Stagger"),
        new(532, 102, 4, "Daze"), new(534, 102, 4)
    };
    private static readonly Sample[] Samples = KungFu.Concat(Karate).ToArray();
    private static readonly ushort[] KungFuProfile = Profile(CombatStyles.KungFu, KungFu);
    private static readonly ushort[] KarateProfile = Profile(CombatStyles.Karate, Karate);
    private static readonly ushort[] RecipientProfile = StockAbilityProfile.Profile(2, 17, Probe).Order().ToArray();
    private static int Memory(IEnumerable<ushort> ids) => ids.Sum(AbilityMemory.Cost);

    public override string Name => "ability-parity-ma-player-controls";
    public override NativeQaModePlan Plan { get; } = MakePlan();

    private static NativeQaModePlan MakePlan()
    {
        foreach (ushort[] profile in new[] { KungFuProfile, KarateProfile, RecipientProfile })
            Check.That(Memory(profile) <= AbilityMemory.ForLevel(50), "Each staged profile fits the stock level-50 capacity");
        string[] archive = KungFuProfile.Except(KarateProfile).Select((id, i) =>
            $"UPDATE char_abilities SET slot={MartialPlayerControlQa.KungFuSlot + i} WHERE char_id=110 AND ability_id={Descriptor(id)}")
            .Concat(KarateProfile.Except(KungFuProfile).Select((id, i) => Insert(110, MartialPlayerControlQa.KarateSlot + i, id, false)))
            .ToArray();
        string[] helpers = new[]
        {
            PvpOpponentsFixture.NativeSql,
            $"UPDATE characters SET x={CasterX},y={StandY},z={StandZ},rotation={CasterYaw} WHERE charId=110",
            $"UPDATE characters SET x={RecipientX},y={StandY},z={StandZ},rotation={RecipientYaw} WHERE charId=6114",
            "UPDATE characters SET healthC=30000,healthM=30000 WHERE charId IN (110,6114)",
            $"DELETE FROM inventory WHERE charId IN (110,6114) AND slot={CombatFixtureWeapons.HandSlot}",
            "DELETE FROM char_abilities WHERE char_id=6114"
        }.Concat(RecipientProfile.Select((id, i) => Insert(6114, i, id, true))).ToArray();
        return new(KungFuProfile, CasterX, StandZ, Array.Empty<string>(), archive, helpers,
            DevCommands: true, hotbarAbilities: Hotbar(CombatStyles.KungFu, KungFu),
            hotbarPageThree: Hotbar(CombatStyles.Karate, Karate), testMobFixture: "martial-player-controls.csv",
            environmentOverrides: new()
            {
                ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
                ["MXO_CONDITIONAL_THROW_QA"] = "1", ["MXO_CONDITIONAL_POWERLESS_QA"] = "1",
                [MartialPlayerControlQa.Mode] = "1"
            }, extraQaMetadata: new()
            {
                ["PlayerRecipient"] = "QAEnemy (6114)", ["NativeControlChecks"] = Samples.Length,
                ["KungFuMemory"] = Memory(KungFuProfile), ["KarateMemory"] = Memory(KarateProfile),
                ["FixedModifierChance"] = string.Join(",", MartialPlayerControlQa.FixedAbilityIds)
            }, readyMessages: new[]
            {
                $"CSR spawns at {CasterX},{StandY},{StandZ} facing QAEnemy, 3 m directly ahead at {RecipientX},{StandY},{StandZ}. Select QAEnemy, open ordinary Close Combat, then select the style on page 2 slot 1. Keep QAEnemy selected and remain in this interlock.",
                "For each row, submit the listed prep with Enter and no Escape, then press the listed stock hotbar slot. The helper attempts stock Cheap Shot during control and again after both removals.",
                "Wait for the NativeQa console PASS and NEXT lines after each row. NEXT follows Cheap Shot's 20 s reuse. If your special misses, wait for its own reuse to end, reapply the prep and retry the same slot.",
                "The helper waits for each expected state while the fixture server is running. This disposable fixture selects the positive modifier roll for all nine moves. Hit contests, durations, immunity, refusal and expiry use the ordinary server paths."
            }.Concat(Instructions(2, KungFu)).Append(
                "After the fifth NEXT line, enter ?combatdepth control-karate. Wait for Martial control Karate stage ready. Switch to page 3, press slot 1 (Karate), then run these slots in the same interlock.")
                .Concat(Instructions(3, Karate)).Append(
                "Keep martial-player-controls.jsonl, QAEnemy's PacketLog and the stock CSR PacketLog for the nine recipient rows.").ToArray());
    }

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var server = context.ActualServer ?? throw new InvalidOperationException("Player control fixture requires the owned server.");
        var recipient = await HeadlessPlayer.LoginAsync(context.Address, "QAEnemy", "QAEnemy", stockCharacterLoad: true);
        context.Helpers.Add(recipient.Handle, recipient);
        Check.Equal(6114u, recipient.CharId, "Player control helper identity");
        var profile = AbilityMemoryUiFixture.Decode(recipient.CharacterData[6])
            .Select(row => AbilityCatalog.IdOf(row.Descriptor)).ToHashSet();
        Check.That(RecipientProfile.All(profile.Contains), "Helper owns Cheap Shot and all of its prerequisites");
        await PvpOpponentsFixture.EnterAsync(recipient);
        VerifyStand(recipient.World.CreationSnapshot().Last(view => view.ViewId == 2), RecipientX, RecipientYaw);
        NativeQaReadiness.RequireNoAmbientAggro(server, recipient);
        if (context.StockProbe is not { } caster)
        {
            _ = WatchNativeAsync(context, recipient, server);
            return Array.Empty<byte>();
        }

        await PvpOpponentsFixture.EnterAsync(caster);
        var enemy = await caster.WaitForViewNamedAsync(recipient.Handle);
        PvpOpponentsFixture.Observe(enemy, 2, recipient.CharId);
        var csr = await recipient.WaitForViewNamedAsync(caster.Handle);
        VerifyStand(caster.World.CreationSnapshot().Last(view => view.ViewId == 2), CasterX, CasterYaw);
        VerifyStand(csr, CasterX, CasterYaw);
        VerifyStand(enemy, RecipientX, RecipientYaw);
        var target = Handle(enemy);
        string targetHex = WireFormat.ToHex(BitConverter.GetBytes(target));
        caster.SendRpc($"81 51 {targetHex}");
        await CheckTacticStateVisibilityAsync();
        int start = caster.World.RpcSnapshot().Count;
        caster.World.SendRpcs(StarterHackerControlTargetMode.WithHandle(StockSelfDefense, target),
            WireFormat.FromHex($"40 {targetHex} 01 6b"));
        await StockAbilityProfile.ObserveAsync(caster, start, rpc => rpc.Bytes is [0x46, ..] &&
            rpc.Bytes.Length >= 6 && rpc.Bytes[5] == 1, "Real PvP interlock opens");
        try
        {
            await RunStage(KungFu, CombatStyles.KungFu);
            caster.SendChat("?combatdepth control-karate");
            await caster.WaitForTextAsync("Martial control Karate stage ready.");
            await RunStage(Karate, CombatStyles.Karate);
        }
        catch
        {
            await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "martial-player-control-failure.log"), server.DebugLog());
            throw;
        }
        Check.Equal(0, caster.World.Problems.Count + recipient.World.Problems.Count, "Player control replay framing");
        return Array.Empty<byte>();

        async Task CheckTacticStateVisibilityAsync()
        {
            // Outside combat, no natural proc can supply the flag expected from the timed command.
            var records = new List<object>();
            foreach (var (prep, tactic) in new[] { ("Daze", CombatTactic.Speed), ("OffBalance", CombatTactic.Grab),
                ("Stagger", CombatTactic.Power) })
            {
                int casterStart = caster.World.ViewSnapshot().Count;
                int recipientStart = recipient.World.ViewSnapshot().Count;
                const uint baseline = PvpFlag.PvpBit;
                uint active = baseline | CombatInterlock.ConditionBit(tactic);
                caster.SkipReceived();
                caster.SendChat($"?combatdepth {prep.ToLowerInvariant()} 2");
                await caster.WaitForTextAsync($"{prep} fixture applied");
                byte[] observerOn = WorldServer.BuildViewUpdate(enemy.ViewId, PvpFlag.BuildOtherFlags(active));
                byte[] observerOff = WorldServer.BuildViewUpdate(enemy.ViewId, PvpFlag.BuildOtherFlags(baseline));
                byte[] ownOn = PvpFlag.BuildSelfFlags(active);
                byte[] ownOff = PvpFlag.BuildSelfFlags(baseline);
                var on = await Expect(caster, casterStart, observerOn, $"{prep} visible to the attacker");
                var own = await Expect(recipient, recipientStart, ownOn, $"{prep} visible to the recipient");
                var off = await Expect(caster, on + 1, observerOff, $"{prep} expiry visible to the attacker");
                var ownEnd = await Expect(recipient, own + 1, ownOff, $"{prep} expiry visible to the recipient");
                records.Add(new
                {
                    State = prep, TargetView = enemy.ViewId,
                    ObserverAdd = Convert.ToHexString(caster.World.ViewSnapshot()[on]),
                    ObserverRemove = Convert.ToHexString(caster.World.ViewSnapshot()[off]),
                    RecipientAdd = Convert.ToHexString(recipient.World.ViewSnapshot()[own]),
                    RecipientRemove = Convert.ToHexString(recipient.World.ViewSnapshot()[ownEnd])
                });
                Console.WriteLine($"MARTIAL TACTIC VIEW PASS {prep} applied and expired on both clients");
            }
            await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "martial-player-tactic-views.json"),
                JsonSerializer.Serialize(records));

            async Task<int> Expect(HeadlessPlayer actor, int cursor, byte[] update, string message)
            {
                var pattern = BytePattern.Parse(WireFormat.ToHex(update));
                int index = -1;
                await actor.WaitUntilAsync(() => (index = actor.World.ViewSnapshot().FindIndex(cursor, pattern.Occurs)) >= 0,
                    message, Hold);
                return index;
            }
        }

        async Task RunStage(Sample[] samples, ushort style)
        {
            int viewCursor = caster.World.ViewSnapshot().Count;
            string styleHex = StockAbilityProfile.LittleEndianHex(style);
            var activeStyle = BytePattern.Parse($"02 00 02 80 80 80 80 80 10 {styleHex} 00 00");
            caster.SendRpc(StarterHackerControlTargetMode.WithHandle(style == CombatStyles.KungFu ? StockKungFu : StockKarate, target));
            await caster.WaitUntilAsync(() => caster.World.ViewSnapshot().Skip(viewCursor).Any(activeStyle.Occurs),
                $"the ordinary active style update for {style}", Hold);
            caster.SendRpc($"81 51 {targetHex}");
            foreach (Sample sample in samples)
            {
                int cursor = recipient.World.RpcSnapshot().Count;
                var proof = ReplayRecipientAsync();
                for (int attempt = 0; attempt < 5 && !proof.IsCompleted; attempt++)
                {
                    if (sample.Setup != null)
                    {
                        caster.SkipReceived();
                        caster.SendChat($"?combatdepth {sample.Setup.ToLowerInvariant()} 60");
                        await caster.WaitForTextAsync($"{sample.Setup} fixture applied");
                    }
                    caster.SendRpc(MartialInterlockAdmissionMode.ControlRequest(sample.Id, target));
                    await Task.WhenAny(proof, Task.Delay(TimeSpan.FromSeconds(11)));
                    if (proof.IsCompleted || recipient.World.RpcSnapshot().Skip(cursor).Any(frame => State(frame, sample, false)))
                        break;
                }
                await proof;

                async Task ReplayRecipientAsync()
                {
                    var added = await recipient.WaitForRpcAsync(frame => State(frame, sample, false),
                        $"recipient state add {sample.Id}/{sample.State}", TimeSpan.FromMinutes(2));
                    await ObserveRecipientAsync(context, recipient, Handle(csr), sample, cursor, added);
                }
            }
        }
    }

    private static async Task WatchNativeAsync(NativeQaBootstrapContext context, HeadlessPlayer recipient, ServerInstance server)
    {
        string stop = Path.Combine(context.OutputDirectory, "stop");
        try
        {
            var csr = await WaitForNativeEventAsync(() => recipient.World.CreationSnapshot().LastOrDefault(view => view.Name == "CSR_Tester"));
            if (csr == null) return;
            int cursor = 0;
            foreach (Sample sample in Samples)
            {
                Console.WriteLine($"MARTIAL RECIPIENT WAITING {sample.Id}");
                var added = await WaitForNativeEventAsync(() => recipient.World.RpcSnapshot().Skip(cursor)
                    .FirstOrDefault(frame => State(frame, sample, false)));
                if (added == null) return;
                cursor = await ObserveRecipientAsync(context, recipient, Handle(csr), sample, cursor, added);
            }
        }
        catch (Exception error)
        {
            if (!Running()) return;
            Console.WriteLine($"MARTIAL RECIPIENT FAILED {error.Message}");
            await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "martial-player-control-failure.log"), error.ToString());
        }

        bool Running()
        {
            if (File.Exists(stop)) return false;
            try { return !server.HasExited; }
            catch (InvalidOperationException) { return false; }
        }

        async Task<T?> WaitForNativeEventAsync<T>(Func<T?> read) where T : class
        {
            long seen = -1;
            while (Running())
            {
                long version = recipient.World.Version;
                if (seen != version)
                {
                    seen = version;
                    if (read() is { } result) return result;
                }
                await Task.Delay(25);
            }
            return null;
        }
    }

    private static async Task<int> ObserveRecipientAsync(NativeQaBootstrapContext context, HeadlessPlayer recipient,
        uint caster, Sample sample, int cursor, RpcRecord added)
    {
        long stateAt = Stopwatch.GetTimestamp();
        var immune = await StockAbilityProfile.ObserveAsync(recipient, cursor, frame => Immunity(frame, sample, false),
            $"separate immunity add for {sample.Id}");
        var stateExpiry = ExpiryAsync(frame => State(frame, sample, true) && SameLocator(added, frame),
            sample.Seconds, "control");
        var immuneExpiry = ExpiryAsync(frame => Immunity(frame, sample, true) && SameLocator(immune, frame),
            sample.ImmunitySeconds, "immunity");
        byte[] action = StarterHackerControlTargetMode.WithHandle(StockCheapShot, caster);
        int attempt = recipient.World.RpcSnapshot().Count;
        int views = recipient.World.ViewSnapshot().Count;
        recipient.SkipReceived();
        recipient.SendRpc(action);
        var refused = await recipient.WaitForRpcAsync(frame => AbilityRefusals.TryDecode(frame.Bytes, out var reason) &&
            reason.AbilityID == Probe, $"Cheap Shot refused by {sample.State}", Hold);
        string message = sample.State == TempMods.PowerlessAbility ? "You cannot use abilities while powerless." :
            "You cannot act while stunned or pacified.";
        await recipient.WaitForTextAsync(message, Hold);
        recipient.SendRpc("81 48");
        await recipient.WaitForRpcAsync("81 49", Hold);
        Check.That(!recipient.World.RpcSnapshot().Skip(attempt).Any(frame => ProbeTimer(frame, false) ||
            BytePattern.Parse("80 ac c6 00").IsMatch(frame.Bytes)), "Refused helper action is free of cast and reuse");
        var before = recipient.World.ViewSnapshot().Take(views).Select(ReadInnerStrength).LastOrDefault(v => v.HasValue);
        if (before.HasValue)
            Check.That(!recipient.World.ViewSnapshot().Skip(views).Select(ReadInnerStrength).Any(v => v < before),
                "Refused helper action does not debit IS");
        var (removed, stateElapsed) = await stateExpiry;
        var (immuneRemoved, immuneElapsed) = await immuneExpiry;

        int recoveredAt = recipient.World.RpcSnapshot().Count;
        recipient.SendRpc(action);
        var recovery = await StockAbilityProfile.ObserveAsync(recipient, recoveredAt, frame => ProbeTimer(frame, false),
            $"same Cheap Shot admitted after control {sample.Id} expired");
        var round = await StockAbilityProfile.ObserveAsync(recipient, recoveredAt,
            frame => frame.Bytes.Length == 89 && frame.Bytes[0] == 0x45 &&
                BinaryPrimitives.ReadUInt32LittleEndian(frame.Bytes.AsSpan(13)) == (0x80000000u | Probe),
            $"recovered Cheap Shot submitted an ordinary round after {sample.Id}");
        string path = Path.Combine(context.OutputDirectory, "martial-player-controls.jsonl");
        await File.AppendAllTextAsync(path, JsonSerializer.Serialize(new
        {
            sample.Id, sample.State, sample.Seconds, sample.ImmunitySeconds, stateElapsed, immuneElapsed,
            Recipient = recipient.CharId, Request = Convert.ToHexString(action),
            Add = added.Hex, Remove = removed.Hex, ImmunityAdd = immune.Hex, ImmunityRemove = immuneRemoved.Hex,
            Refusal = refused.Hex, Message = message, RecoveryTimer = recovery.Hex, RecoveryRound = round.Hex
        }) + Environment.NewLine);
        Console.WriteLine($"MARTIAL RECIPIENT PASS {sample.Id} ({sample.State}/{sample.Seconds}s); state and immunity removed; Cheap Shot refused then recovered.");
        await recipient.WaitForRpcAsync(frame => ProbeTimer(frame, true), $"Cheap Shot ready for next control after {sample.Id}", TimeSpan.FromSeconds(22));
        Console.WriteLine($"MARTIAL RECIPIENT NEXT after {sample.Id}");
        return recipient.World.RpcSnapshot().Count;

        async Task<(RpcRecord Record, double Seconds)> ExpiryAsync(Func<RpcRecord, bool> matches, float seconds, string kind)
        {
            int from = recipient.World.RpcSnapshot().Count;
            var record = await StockAbilityProfile.ObserveAsync(recipient, from, matches, $"matched {kind} removal for {sample.Id}");
            double elapsed = Stopwatch.GetElapsedTime(stateAt).TotalSeconds;
            Check.That(Math.Abs(elapsed - seconds) < 1.25, $"{sample.Id} {kind} expiry after {elapsed:F2}s, expected {seconds}s");
            return (record, elapsed);
        }
    }

    private static bool State(RpcRecord frame, Sample sample, bool remove) =>
        ControlRecord(frame, TempMods.KindState, sample.Id, sample.State, sample.Seconds, remove);
    private static bool Immunity(RpcRecord frame, Sample sample, bool remove) =>
        ControlRecord(frame, TempMods.KindPlain, TempMods.ControlImmunityTimerAbility, TempMods.ISDrainTargetAbility,
            sample.ImmunitySeconds, remove);
    private static bool ControlRecord(RpcRecord frame, ushort kind, ushort source, ushort stat, float seconds, bool remove) =>
        ManageBonusRecord.TryDecode(frame.Bytes, out var record) && record!.Type == kind && record.SourceId == source &&
        record.TargetId == stat && record.SignedValue == 0 && Math.Abs(record.Duration - seconds) < 0.0001 &&
        record.AddRemoveType == (remove ? 1 : 0);
    private static bool SameLocator(RpcRecord add, RpcRecord remove) =>
        ManageBonusRecord.TryDecode(add.Bytes, out var first) && ManageBonusRecord.TryDecode(remove.Bytes, out var second) &&
        first!.Locator == second!.Locator;
    private static bool ProbeTimer(RpcRecord frame, bool end) =>
        BytePattern.Parse($"80 bd 01 c6 00 {AbilityCooldowns.OwnTimer:x2} 00 00 a0 41 {(end ? "00" : "01")}").IsMatch(frame.Bytes);
    private static ushort? ReadInnerStrength(byte[] view) =>
        BuffDrainLifetimeFixture.TryInnerStrength(view, out ushort value) ? value : null;
    private static void VerifyStand(ViewCreation view, int x, byte yaw)
    {
        byte[] position = view.Attributes["Position"];
        Check.Equal((double)x, BitConverter.ToDouble(position), $"{view.Name} exact spawn x");
        Check.Equal((double)StandY, BitConverter.ToDouble(position, 8), $"{view.Name} exact spawn y");
        Check.Equal((double)StandZ, BitConverter.ToDouble(position, 16), $"{view.Name} exact spawn z");
        Check.Equal(yaw, view.Attributes["Yaw Interval"][0], $"{view.Name} faces the other participant");
        Check.That(NavMesh.Locate(1, x, StandY, StandZ, out var spot) && !spot.indoors && Math.Abs(spot.floorY + 95 - StandY) < 10,
            $"{view.Name} spawn is on the outdoor walkable floor");
    }
    private static uint Handle(ViewCreation view) => DuelPackets.Handle(view.ViewId, view.SpawnId);
    private static long Descriptor(ushort id) => 0x80000000L | ((long)id << 10);
    private static ushort[] Profile(ushort style, Sample[] samples) => StockAbilityProfile.Profile(Hotbar(style, samples)
        .Concat(new ushort[] { 2, 17 }).ToArray()).Order().ToArray();
    private static ushort[] Hotbar(ushort style, Sample[] samples) => new[] { style }.Concat(samples.Select(sample => sample.Id)).ToArray();
    private static string Insert(int actor, int slot, ushort id, bool loaded)
    {
        var ability = AbilityCatalog.Find(id);
        string name = ability.Name.Replace("'", "''");
        int rank = (int)Math.Min(50, ability.Number("Ability/AbilityLevelCap", 50));
        return $"INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded,ability_name) VALUES({actor},{slot},{Descriptor(id)},{rank},{(loaded ? 1 : 0)},'{name}')";
    }
    private static IEnumerable<string> Instructions(int page, Sample[] samples) => samples.Select((sample, index) =>
        $"Page {page} slot {index + 2} {AbilityCatalog.Find(sample.Id).Name} ({sample.Id}). " +
        (sample.Setup == null ? string.Empty : $"Enter ?combatdepth {sample.Setup.ToLowerInvariant()} 60 first. ") +
        $"Expect {(sample.State == 96 ? "Powerless" : "Stun")} {sample.Seconds}s and separate immunity {sample.ImmunitySeconds}s.");
}
