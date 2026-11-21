using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class StarterPathogenistAuraSightMode : NativeQaMode
{
    // The mapped sight blocker and both stands were verified with stock area casts in PR568.
    private const int X = 21925, Y = 1095, Z = 16100;
    private const ushort Miasma = 61, DefensePulse = 701, StaticField = 89, DamagePulse = 694;
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, Miasma, StaticField).Order().ToArray();

    // Full stock self-toggle requests from PR564 and the stock selection shape from PR568.
    private const string MiasmaUse = "80b93d00000000000000000000000000000000000000000000000000ffff7f7f00000000086b0000000000";
    private const string StaticUse = "80b95900000000000000000000000000000000000000000000000000ffff7f7f00000000086b0000000000";

    public override string Name => "ability-parity-starter-pathogenist-aura-sight";

    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), Array.Empty<string>(), new[]
    {
        PvpOpponentsFixture.NativeSql,
        $"UPDATE characters SET x={X},y={Y},z={Z},rotation={MobAggro.YawTowards(X, Z, X + 200, Z)} WHERE charId=110",
        $"UPDATE characters SET x={X + 200},y={Y},z={Z},alignment=2 WHERE charId=6114",
        $"UPDATE characters SET x={X - 250},y={Y},z={Z},alignment=2 WHERE charId=6112",
        "DELETE FROM char_abilities WHERE char_id IN (6112,6114)"
    }, DevCommands: true, hotbarAbilities: new[] { Miasma, StaticField }, environmentOverrides: new()
    {
        ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
        [AreaTargetDiagnostics.Variable] = "1"
    }, extraQaMetadata: new() { ["ScopedStockRows"] = "61,89,694,701", ["NativeCloseableRows"] = 2 },
        readyMessages: new[]
        {
            "Stay outside at the marked stand facing QAEnemy, 2 m east. QAEnemy is clear. QAFar is 2.5 m west on the same floor, across a mapped line-of-sight blocker. Both helpers are hostile and remain in world.",
            "Turn to select QAFar without moving off the stand and keep it selected. Page 2 slot 1 toggles Miasma. Watch for 701: only QAEnemy gets melee and ballistic defense -14/-14 for 5 s. Keep it on through the next pulse (~5 s), then toggle off. Wait for the final two modifiers to expire.",
            "Keep QAFar selected. Slot 2 toggles Static Field. Only QAEnemy takes 1 viral damage from 694 on each pulse. Wait through the second pulse, then toggle off. Capture QAFar's AREA-QA reason=sight, both stock requests and CSR IS changes. Expected recurring IS debit is 3 for Miasma and 12 for Static Field."
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

        await PvpOpponentsFixture.EnterAsync(actor);
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        await actor.WaitForViewNamedAsync(clear.Handle);
        var view = await actor.WaitForViewNamedAsync(blocked.Handle);
        uint handle = DuelPackets.Handle(view.ViewId, view.SpawnId);
        actor.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(handle))}");

        int blockedCursor = blocked.World.RpcSnapshot().Count;
        int clearCursor = clear.World.RpcSnapshot().Count;
        int debugCursor = server.DebugLog().Length;
        await ToggleAsync(Miasma, MiasmaUse, on: true);
        ManageBonusRecord first = await DefenseRecordAsync(clearCursor, 1009);
        var firstBallistic = await DefenseRecordAsync(clearCursor, 1010);
        var refreshed = await DefenseRecordAsync(clearCursor, 1009, first.RecordId);
        var refreshedBallistic = await DefenseRecordAsync(clearCursor, 1010,
            firstBallistic.RecordId);
        await ToggleAsync(Miasma, MiasmaUse, on: false);
        foreach (var mod in new[] { refreshed, refreshedBallistic })
            await StockAbilityProfile.ObserveAsync(clear, clearCursor,
                packet => ManageBonusRecord.TryDecode(packet.Bytes, out var record) &&
                    record!.SourceId == DefensePulse && record.RecordId == mod.RecordId && record.AddRemoveType == 1,
                $"The final Miasma modifier {mod.RecordId} expires after toggle-off");
        VerifySight(debugCursor, DefensePulse);

        clearCursor = clear.World.RpcSnapshot().Count;
        debugCursor = server.DebugLog().Length;
        await ToggleAsync(StaticField, StaticUse, on: true);
        await clear.WaitUntilAsync(() => clear.World.RpcSnapshot().Skip(clearCursor).Count(
            packet => IsStaticHit(packet, clear.Handle)) >= 2, "Static Field's initial and scheduled hits each deal 1",
            HeadlessPlayer.DefaultTimeout);
        await ToggleAsync(StaticField, StaticUse, on: false);
        VerifySight(debugCursor, DamagePulse);

        int acknowledgement = blocked.World.RpcSnapshot().Count;
        blocked.SendRpc("81 48");
        await StockAbilityProfile.ObserveAsync(blocked, acknowledgement, frame => frame.Bytes is [0x81, 0x49, ..],
            "The blocked helper remained connected through both toggles");
        Check.That(!blocked.World.RpcSnapshot().Skip(blockedCursor).Any(packet =>
            IsStaticHit(packet, blocked.Handle) || ManageBonusRecord.TryDecode(packet.Bytes, out var mod) &&
                mod!.SourceId == DefensePulse && mod.AddRemoveType == 0),
            "The selected hostile behind the blocker receives neither child");
        Check.Equal(0, actor.World.Problems.Count + clear.World.Problems.Count + blocked.World.Problems.Count,
            "Pathogenist aura sight fixture framing");
        return Array.Empty<byte>();

        async Task ToggleAsync(ushort ability, string request, bool on)
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

