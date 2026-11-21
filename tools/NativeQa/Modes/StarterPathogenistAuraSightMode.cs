using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class StarterPathogenistAuraSightMode : NativeQaMode
{
    // The mapped sight blocker and both stands were verified with stock area casts in PR568.
    private const int X = 21925, Y = 1095, Z = 16100;
    private const ushort Miasma = 61, DefensePulse = 701, StaticField = 89, DamagePulse = 694;
    private const ushort HackerStyle = 1006, CodeNuke = 60, ResistInfection = 75, TrojanHorse = 945;
    // Keep the complete loadout that the stock client used successfully for Static Field in PR574.
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(
        2, 17, HackerStyle, CodeNuke, ResistInfection, Miasma, StaticField, TrojanHorse).Order().ToArray();
    internal const string HackerUse = "80b9ee0300000000000000000000000000000000000000000000000000000084d75741086b0000000000";

    // PR588 stock selected Miasma CSEQ 87/97 and PR574 stock unselected Static Field CSEQ 58/66.
    internal const string MiasmaOn = "80b93d00050003000000000000000000000000000000000000000000785cbf0001000000086b0000000000";
    internal const string MiasmaOff = "80b93d00050003000000000000000000000000000000000000000000d557377c20000000086b0000000000";
    internal const string StaticUse = "80b959000000000000000000000000000000000000000000000000000000000084d75741086b0000000000";

    public override string Name => "ability-parity-starter-pathogenist-aura-sight";

    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), Array.Empty<string>(), new[]
    {
        PvpOpponentsFixture.NativeSql,
        $"UPDATE characters SET x={X},y={Y},z={Z},rotation={MobAggro.YawTowards(X, Z, X + 200, Z)} WHERE charId=110",
        $"UPDATE characters SET x={X + 200},y={Y},z={Z},alignment=2 WHERE charId=6114",
        $"UPDATE characters SET x={X - 250},y={Y},z={Z},alignment=2 WHERE charId=6112",
        "DELETE FROM char_abilities WHERE char_id IN (6112,6114)"
    }, DevCommands: true, hotbarAbilities: new[] { Miasma, StaticField, HackerStyle }, environmentOverrides: new()
    {
        ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
        [AreaTargetDiagnostics.Variable] = "1"
    }, extraQaMetadata: new() { ["ScopedStockRows"] = "61,89,694,701", ["NativeCloseableRows"] = 2, ["MemoryUsed"] = Loadout.Sum(AbilityMemory.Cost) },
        readyMessages: new[]
        {
            "Stay outside at the marked stand facing QAEnemy, 2 m east. QAEnemy is clear. QAFar is 2.5 m west on the same floor, across a mapped line-of-sight blocker. Both helpers are hostile and remain in world.",
            "Retest Static Field only. Leave world selection clear and remain outside interlock. Select Hacker Style in page 2 slot 3, then cast Static Field in slot 2 before Miasma. This matches the clear-selection cast from PR574. Code Nuke and the rank-50 Resist Infection chain are loaded.",
            "Only QAEnemy takes 1 viral damage from 694 on each pulse. Wait through the second pulse (~5 s), then toggle off. QAFar is inside 6 m but gets no hit. Capture the first 89 request, QAFar's AREA-QA reason=sight and CSR's 12 IS per pulse.",
            "If the stock use fails from the shortcut, try Actions > Abilities > Pathogenist > Static Field with selection still clear. Report any red prerequisite text. Miasma in slot 1 already passed natively with QAFar selected."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var server = context.ActualServer!;
        foreach (string name in new[] { "QAEnemy", "QAFar" })
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, name, name, stockCharacterLoad: true);
            context.Helpers.Add(name, helper);
            await PvpOpponentsFixture.EnterAsync(helper);
            NativeQaReadiness.RequireNoAmbientAggro(server, helper);
        }
        var clear = context.Helpers["QAEnemy"];
        var blocked = context.Helpers["QAFar"];
        await clear.WaitForViewNamedAsync(blocked.Handle);
        await blocked.WaitForViewNamedAsync(clear.Handle);
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[6])
            .Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor), row => row.Level);
        int memory = Loadout.Sum(AbilityMemory.Cost);
        Check.That(memory <= AbilityMemory.ForLevel(50), "Static Field's complete stock profile fits in memory");
        Check.That(Loadout.All(id => archive.ContainsKey(id)) &&
            AbilityPrerequisites.Current.Closure(StaticField).All(requirement =>
                archive.TryGetValue(requirement.Key, out var rank) && rank >= requirement.Value) &&
            archive[CodeNuke] == 50 && archive[ResistInfection] == 50,
            "Stock Margin loads Code Nuke, Resist Infection and the complete Static Field prerequisite chain");
        Console.WriteLine($"PATHOGENIST STOCK LOADOUT: {memory}/{AbilityMemory.ForLevel(50)} memory; Code Nuke 60 and Resist Infection 75 at rank 50.");

        await PvpOpponentsFixture.EnterAsync(actor);
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        await actor.WaitForViewNamedAsync(clear.Handle);
        var view = await actor.WaitForViewNamedAsync(blocked.Handle);
        uint handle = DuelPackets.Handle(view.ViewId, view.SpawnId);
        actor.SendRpc(HackerUse);
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 ee 03 00 00");

        int blockedCursor = blocked.World.RpcSnapshot().Count;
        int clearCursor = clear.World.RpcSnapshot().Count;
        int debugCursor = server.DebugLog().Length;
        await ToggleAsync(StaticField, Convert.FromHexString(StaticUse), on: true);
        await clear.WaitUntilAsync(() => clear.World.RpcSnapshot().Skip(clearCursor).Count(
            packet => IsStaticHit(packet, clear.Handle)) >= 2, "Static Field's initial and scheduled hits each deal 1",
            HeadlessPlayer.DefaultTimeout);
        await ToggleAsync(StaticField, Convert.FromHexString(StaticUse), on: false);
        VerifySight(debugCursor, DamagePulse);

        actor.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(handle))}");
        clearCursor = clear.World.RpcSnapshot().Count;
        debugCursor = server.DebugLog().Length;
        await ToggleAsync(Miasma, StarterHackerControlTargetMode.WithHandle(MiasmaOn, handle), on: true);
        var firstMelee = await DefenseRecordAsync(clearCursor, 1009);
        var firstBallistic = await DefenseRecordAsync(clearCursor, 1010);
        var refreshedMelee = await DefenseRecordAsync(clearCursor, 1009, firstMelee.RecordId);
        var refreshedBallistic = await DefenseRecordAsync(clearCursor, 1010, firstBallistic.RecordId);
        await ToggleAsync(Miasma, StarterHackerControlTargetMode.WithHandle(MiasmaOff, handle), on: false);
        foreach (var mod in new[] { refreshedMelee, refreshedBallistic })
            await StockAbilityProfile.ObserveAsync(clear, clearCursor,
                packet => ManageBonusRecord.TryDecode(packet.Bytes, out var record) &&
                    record!.SourceId == DefensePulse && record.RecordId == mod.RecordId && record.AddRemoveType == 1,
                $"The final Miasma modifier {mod.RecordId} expires after toggle-off");
        VerifySight(debugCursor, DefensePulse);

        int acknowledgement = blocked.World.RpcSnapshot().Count;
        blocked.SendRpc("81 48");
        await StockAbilityProfile.ObserveAsync(blocked, acknowledgement, frame => frame.Bytes is [0x81, 0x49, ..],
            "The blocked helper remained connected through both toggles");
        Check.That(!blocked.World.RpcSnapshot().Skip(blockedCursor).Any(packet =>
            IsStaticHit(packet, blocked.Handle) || ManageBonusRecord.TryDecode(packet.Bytes, out var mod) &&
                mod!.SourceId == DefensePulse && mod.AddRemoveType == 0),
            "The hostile behind the blocker receives neither child with or without selection");
        Check.Equal(0, actor.World.Problems.Count + clear.World.Problems.Count + blocked.World.Problems.Count,
            "Pathogenist aura sight fixture framing");
        return Array.Empty<byte>();

        async Task ToggleAsync(ushort ability, byte[] request, bool on)
        {
            int cursor = actor.World.RpcSnapshot().Count;
            actor.SendRpc(request);
            await StockAbilityProfile.ObserveAsync(actor, cursor,
                packet => packet.Bytes.AsSpan().SequenceEqual(AbilityBuffs.BuildExclusive(ability, on)),
                $"Stock toggle {ability} {(on ? "on" : "off")}");
        }

        async Task<ManageBonusRecord> DefenseRecordAsync(int cursor, ushort stat, ushort? previous = null)
        {
            var packet = await StockAbilityProfile.ObserveAsync(clear, cursor, frame =>
                StockAbilityProfile.Bonus(frame, DefensePulse, stat, -14, 5, false) &&
                ManageBonusRecord.TryDecode(frame.Bytes, out var mod) && mod!.RecordId != previous,
                $"Miasma defense {stat} at -14 for five seconds");
            ManageBonusRecord.TryDecode(packet.Bytes, out var record);
            return record!;
        }

        bool IsStaticHit(RpcRecord packet, string target) =>
            packet.Bytes.AsSpan().SequenceEqual(AbilityMessages.Damage(false, DamagePulse, actor.Handle, target, 1, 0));

        void VerifySight(int cursor, ushort child)
        {
            var lines = server.DebugLog()[cursor..].Split('\n');
            Check.That(lines.Any(line => line.Contains("candidate=QAFar ", StringComparison.Ordinal) &&
                line.Contains("distance=2.5000 floorDelta=0.00", StringComparison.Ordinal) &&
                line.TrimEnd().EndsWith("reason=sight", StringComparison.Ordinal)),
                $"Child {child} excludes the selected in-range hostile for sight");
        }
    }
}

