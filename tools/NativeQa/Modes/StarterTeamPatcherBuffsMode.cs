using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class StarterTeamPatcherBuffsMode : NativeQaMode
{
    private const ushort CombatAura = 42, Firewall = 63, ResistCombat = 72, AccuracyPulse = 695;
    // Full requests from the stock .2 Team Patcher session, CSEQ 63/72/76/87.
    private const string StockAuraStart = "80b92a00000000000000000000000000000000000000000000000000d557377c20000000086b0000000000";
    private const string StockAuraStop = "80b92a000000000000000000000000000000000000000000000000000cf11a0001000000086b0000000000";
    private const string StockFirewall = "80b93f000000000000000000000000000000000000000000000000000cf11a0001000000086b0000000000";
    private const string StockResistCombat = "80b94800000000000000000000000000000000000000000000000000d557377c20000000086b0000000000";
    private const int X = 24800, Y = 495, Z = -1498;
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, CombatAura, Firewall, ResistCombat).Order().ToArray();
    private static readonly (string Name, int X, int Z)[] Helpers =
    {
        ("QAFriend", X + 250, Z), ("QAEnemy", X + 450, Z), ("QAFar", X + 1350, Z),
        ("QAOther", X - 200, Z)
    };

    public override string Name => "ability-parity-starter-team-patcher-buffs";

    public override NativeQaModePlan Plan { get; } = new(
        Loadout, X, Z, Array.Empty<string>(), Array.Empty<string>(),
        new[]
        {
            $"UPDATE characters SET x={X},y={Y},z={Z},innerStrC=1000,innerStrM=1000 WHERE charId=110",
            "DELETE FROM char_abilities WHERE char_id=6111",
            "INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES " +
                string.Join(",", StockAbilityProfile.Profile(2, 17, CombatAura).Select((id, slot) =>
                    $"(6111,{400 + slot},{AbilityMessages.AbilityGameObject(id)},50,1)")),
            "UPDATE characters SET innerStrC=1000,innerStrM=1000 WHERE charId=6111"
        }
            .Concat(Helpers.Select(stand =>
                $"UPDATE characters SET x={stand.X},y={Y},z={stand.Z},alignment=1,pvpflag=0 WHERE handle='{stand.Name}'"))
            .ToArray(), DevCommands: true, hotbarAbilities: new ushort[] { CombatAura, Firewall, ResistCombat },
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        extraQaMetadata: new() { ["ScopedStockRows"] = "42,63,72", ["NativeCloseableRows"] = 1 },
        readyMessages: new[]
        {
            "Stay on the starting stand and leave world selection clear. When QAFriend invites CSR to the mission team, click Accept. QAEnemy and QAFar are friendly team members already. QAOther stays outside the team.",
            "Page 2 slot 1: toggle Combat Aura 1.0 on. QAFriend (2.5 m) should receive +64 Melee and Ballistic Accuracy from child 695 for 5 s. QAEnemy (4.5 m), QAFar (13.5 m) and nonmember QAOther (2 m) should receive nothing. QAFriend automatically toggles a second copy after the first record and announces it. Toggle CSR off after that cue. QAEnemy is 2 m from QAFriend and should acquire the helper's pulse. CSR should show only +64/+64 while both auras overlap. The helper stops automatically after its second pulse. Wait for the final removals before slots 2–3.",
            "Page 2 slot 2: Network Firewall, 50 IS. QAFriend and QAEnemy should receive +35 Viral Defense and the three +5 defenses for 300 s. QAFar and QAOther should remain unaffected.",
            "Page 2 slot 3: Resist Combat, 40 IS. All three teammates should receive +20 Combat Evasion for 600 s. QAOther receives no record. Helper records are printed as TEAM-PATCHER BUFF."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var server = context.ActualServer ?? throw new InvalidOperationException("Team Patcher QA requires a disposable server.");
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(50), "The seeded Team Patcher profile fits in memory");
        foreach (var placement in Helpers)
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, placement.Name, placement.Name, stockCharacterLoad: true);
            context.Helpers.Add(placement.Name, helper);
            await helper.EnterWorldAsync();
            NativeQaReadiness.RequireNoAmbientAggro(server, helper);
            var position = helper.World.CreationSnapshot().Last(view => view.ViewId == 2).Attributes["Position"];
            Check.Equal((double)placement.X, BitConverter.ToDouble(position), $"{placement.Name} X");
            Check.Equal((double)Y, BitConverter.ToDouble(position, 8), $"{placement.Name} Y");
            Check.Equal((double)placement.Z, BitConverter.ToDouble(position, 16), $"{placement.Name} Z");
        }

        var leader = context.Helpers["QAFriend"];
        await TeamBreadthMode.InviteAsync(leader, context.Helpers["QAEnemy"]);
        await TeamBreadthMode.InviteAsync(leader, context.Helpers["QAFar"]);
        if (context.StockProbe is not { } actor)
        {
            _ = ObserveNativeAsync(context, leader);
            return Array.Empty<byte>();
        }

        await actor.EnterWorldAsync();
        await TeamBreadthMode.InviteAsync(leader, actor);
        var otherNear = context.Helpers["QAEnemy"];
        var far = context.Helpers["QAFar"];
        var outsider = context.Helpers["QAOther"];
        var participants = context.Helpers.Values.Append(actor).ToArray();

        var auraCursors = Snapshot();
        actor.SendRpc(StockAuraStart);
        await VerifyAddsAsync(auraCursors, AccuracyPulse, 5, new[] { actor, leader },
            new (ushort, int)[] { (522, 64), (524, 64) });
        var overlapCursors = Snapshot();
        await ToggleAsync(leader, StockAuraStart, on: true);
        await VerifyAddsAsync(overlapCursors, AccuracyPulse, 5, new[] { actor, leader, otherNear },
            new (ushort, int)[] { (522, 64), (524, 64) });
        await ToggleAsync(actor, StockAuraStop, on: false);
        await otherNear.WaitUntilAsync(() => AccuracyAdds(otherNear, overlapCursors[otherNear]) >= 2,
            "QAFriend keeps pulsing after CSR toggles off", HeadlessPlayer.DefaultTimeout);
        await ToggleAsync(leader, StockAuraStop, on: false);
        await actor.WaitUntilAsync(() => AccuracyAdds(actor, auraCursors[actor]) >= 3 &&
            ActiveAccuracyRecords(actor, auraCursors[actor]).Count == 0,
            "The final overlapping aura records expire on CSR", HeadlessPlayer.DefaultTimeout);
        foreach (var member in new[] { actor, leader, otherNear })
        {
            var active = new Dictionary<ushort, ushort>();
            foreach (var record in AccuracyRecords(member, auraCursors[member]))
            {
                if (record.AddRemoveType == 1) active.Remove(record.RecordId);
                else if (record.AddRemoveType == 0) active.Add(record.RecordId, record.TargetId);

                Check.That(active.Values.Distinct().Count() == active.Count,
                    $"{member.Handle} has only one active copy of each accuracy stat");
            }
        }

        var firewallCursors = Snapshot();
        actor.SendRpc(StockFirewall);
        await VerifyAddsAsync(firewallCursors, Firewall, 300, new[] { actor, leader, otherNear },
            new (ushort, int)[] { (183, 35), (1009, 5), (1010, 5), (1011, 5) });

        var resistCursors = Snapshot();
        actor.SendRpc(StockResistCombat);
        await VerifyAddsAsync(resistCursors, ResistCombat, 600, new[] { actor, leader, otherNear, far },
            new (ushort, int)[] { (520, 20) });

        Check.That(participants.All(player => player.World.Problems.Count == 0), "Team Patcher transport framing");
        Console.WriteLine($"TEAM-PATCHER BUFF PASS: 42 reaches CSR and {leader.Handle}; 63 also reaches {otherNear.Handle}; " +
            $"72 also reaches {far.Handle}; all exclude {outsider.Handle}.");
        return Array.Empty<byte>();

        Task<RpcRecord> ToggleAsync(HeadlessPlayer caster, string request, bool on)
        {
            int cursor = caster.World.RpcSnapshot().Count;
            caster.SendRpc(request);
            byte[] state = AbilityBuffs.BuildExclusive(CombatAura, on);
            return StockAbilityProfile.ObserveAsync(caster, cursor, packet => packet.Bytes.AsSpan().SequenceEqual(state),
                $"{caster.Handle} toggles Combat Aura {(on ? "on" : "off")}");
        }

        Dictionary<HeadlessPlayer, int> Snapshot() => participants.ToDictionary(player => player, player => player.World.RpcSnapshot().Count);

        async Task VerifyAddsAsync(Dictionary<HeadlessPlayer, int> cursors, ushort source, float seconds,
            HeadlessPlayer[] included, (ushort Stat, int Amount)[] stats)
        {
            foreach (var player in included)
                foreach (var stat in stats)
                    await StockAbilityProfile.ObserveAsync(player, cursors[player],
                        record => StockAbilityProfile.Bonus(record, source, stat.Stat, stat.Amount, seconds, remove: false),
                        $"{player.Handle}: {source} applies stat {stat.Stat}");

            foreach (var player in participants.Except(included))
            {
                int barrier = player.World.RpcSnapshot().Count;
                player.SendRpc("81 48");
                await StockAbilityProfile.ObserveAsync(player, barrier, record => record.Bytes is [0x81, 0x49, ..],
                    $"{player.Handle}: recipient barrier");
                Check.That(!player.World.RpcSnapshot().Skip(cursors[player]).Any(record =>
                    ManageBonusRecord.TryDecode(record.Bytes, out var bonus) && bonus!.SourceId == source && bonus.AddRemoveType == 0),
                    $"{player.Handle} is outside the {source} recipient set");
            }
        }
    }

    private static IEnumerable<ManageBonusRecord> AccuracyRecords(HeadlessPlayer player, int cursor) =>
        player.World.RpcSnapshot().Skip(cursor)
            .Select(packet => ManageBonusRecord.TryDecode(packet.Bytes, out var bonus) ? bonus : null)
            .OfType<ManageBonusRecord>().Where(bonus => bonus.SourceId == AccuracyPulse);

    private static int AccuracyAdds(HeadlessPlayer player, int cursor) =>
        AccuracyRecords(player, cursor).Count(bonus => bonus.AddRemoveType == 0 && bonus.TargetId == 522);

    private static HashSet<ushort> ActiveAccuracyRecords(HeadlessPlayer player, int cursor)
    {
        var active = new HashSet<ushort>();
        foreach (var bonus in AccuracyRecords(player, cursor))
        {
            if (bonus.AddRemoveType == 0) active.Add(bonus.RecordId);
            if (bonus.AddRemoveType == 1) active.Remove(bonus.RecordId);
        }
        return active;
    }

    private static async Task ObserveNativeAsync(NativeQaBootstrapContext context, HeadlessPlayer leader)
    {
        string stop = Path.Combine(context.OutputDirectory, "stop");
        var offsets = context.Helpers.Values.ToDictionary(helper => helper, helper => helper.World.RpcSnapshot().Count);
        bool invited = false;
        int leaderStart = offsets[leader];
        var overlap = new System.Diagnostics.Stopwatch();
        bool helperActive = false, helperStopped = false;
        while (!File.Exists(stop) && context.ActualServer is { HasExited: false })
        {
            if (!invited && leader.World.CreationSnapshot().Any(view => view.Name == "CSR_Tester"))
            {
                TeamBreadthMode.SendInvite(leader, "CSR_Tester");
                invited = true;
                Console.WriteLine("TEAM-PATCHER BUFF: QAFriend invited CSR_Tester.");
            }
            if (!overlap.IsRunning && AccuracyAdds(leader, leaderStart) > 0) overlap.Start();
            if (overlap.IsRunning && overlap.ElapsedMilliseconds >= 1000 && !helperActive && !helperStopped)
            {
                leader.SendRpc(StockAuraStart);
                leader.SendChat("QAFriend started Combat Aura. CSR can toggle off now.");
                Console.WriteLine("TEAM-PATCHER BUFF: QAFriend started the overlapping Combat Aura.");
                helperActive = true;
            }
            if (helperActive && overlap.ElapsedMilliseconds >= 7100)
            {
                leader.SendRpc(StockAuraStop);
                Console.WriteLine("TEAM-PATCHER BUFF: QAFriend stopped Combat Aura. Waiting for child expiry.");
                helperActive = false;
                helperStopped = true;
            }
            foreach (var helper in context.Helpers.Values)
            {
                var records = helper.World.RpcSnapshot();
                foreach (var record in records.Skip(offsets[helper]))
                {
                    if (ManageBonusRecord.TryDecode(record.Bytes, out var bonus) &&
                        bonus!.SourceId is AccuracyPulse or Firewall or ResistCombat)
                    {
                        Console.WriteLine($"TEAM-PATCHER BUFF: {helper.Handle} {bonus.SourceId}/{bonus.TargetId} " +
                            $"{(bonus.AddRemoveType == 0 ? "add" : "remove")} {bonus.SignedValue}, {bonus.Duration}s");
                    }
                }
                offsets[helper] = records.Count;
            }
            await Task.Delay(100);
        }
    }
}
