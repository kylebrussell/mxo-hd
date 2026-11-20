using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class StarterHackerAreaSightMode : NativeQaMode
{
    private const int X = 21925, Y = 1095, Z = 16100;
    private const ushort HackerStyle = 1006, UiLag = 97, Disruption = 38, Freeze = 47;
    // Native PR556b Freeze and Disruption. PR556 Hacker Style and PR512 UI Lag.
    private const string HackerCast = "80b9ee0300000000000000000000000000000000000000000000000000000084d75741086b0000000000";
    private static readonly (ushort Id, string Request, float Reuse, ushort Stat, int Value, float Seconds)[] Samples =
    {
        (UiLag, "80b96100060004000000000000000000000000000000000000000000585cb70001000000086b0000000000", 58, 342, -14, 27),
        (Disruption, "80b92600060004000000000000000000000000000000000000000000585cc60001000000086b0000000000", 43, 102, 0, 3 * 1.05f),
        (Freeze, "80b92f00060004000000000000000000000000000000000000000000585cc60001000000086b0000000000", 58, 85, 0, 6)
    };
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, HackerStyle, UiLag, Disruption, Freeze)
        .Order().ToArray();

    public override string Name => "ability-parity-starter-hacker-area-sight";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), Array.Empty<string>(), new[]
    {
        PvpOpponentsFixture.NativeSql,
        $"UPDATE characters SET x={X},y={Y},z={Z} WHERE charId=110",
        $"UPDATE characters SET x={X + 200},y={Y},z={Z},alignment=2 WHERE charId=6114",
        $"UPDATE characters SET x={X - 250},y={Y},z={Z},alignment=2 WHERE charId=6112",
        "DELETE FROM char_abilities WHERE char_id IN (6112,6114)"
    }, DevCommands: true, hotbarAbilities: new[] { HackerStyle, UiLag, Disruption, Freeze }, environmentOverrides: new()
    {
        ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
        ["MXO_FREECAST_VIRAL_QA"] = "1", ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766", ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0",
        [AreaTargetDiagnostics.Variable] = "1"
    }, extraQaMetadata: new() { ["ScopedStockRows"] = "38,47,97", ["NativeCloseableRows"] = 1 },
        readyMessages: new[]
        {
            "Stay on the marked outdoor stand. QAEnemy is clear 2 m east. QAFar is a visible hostile on the same floor 2.5 m west across a mapped gap. Both are inside every cast's radius.",
            "On page 2 select Hacker Style in slot 1. Select QAFar and keep it selected throughout. Press UI Lag (slot 2), then Area Disruption (slot 3). The clear QAEnemy receives both effects. QAFar is excluded by sight without a viral contest.",
            "Wait at least eight seconds from the Area Disruption landing, then press Freeze Area (slot 4). QAEnemy receives six-second Pacify and 12-second immunity. QAFar again stays unaffected. Keep the AREA-QA reason=sight lines and the recipient PacketLog."
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
        actor.SendRpc(HackerCast);
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 ee 03 00 00");
        actor.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(handle))}");
        foreach (var sample in Samples)
        {
            int fromDebug = server.DebugLog().Length;
            int fromClear = clear.World.RpcSnapshot().Count;
            int fromBlocked = blocked.World.RpcSnapshot().Count;
            int fromCaster = actor.World.RpcSnapshot().Count;
            actor.SendRpc(StarterHackerControlTargetMode.WithHandle(sample.Request, handle));
            await EvidenceRetestWire.CastBarAsync(actor, sample.Id, 2);
            await EvidenceRetestWire.TimerAsync(actor, fromCaster, sample.Id, AbilityCooldowns.OwnTimer, sample.Reuse);
            await StockAbilityProfile.ObserveAsync(clear, fromClear,
                frame => StockAbilityProfile.Bonus(frame, sample.Id, sample.Stat, sample.Value, sample.Seconds, false),
                $"{sample.Id} lands on the clear witness");
            string areaLog = server.DebugLog()[fromDebug..];
            Check.That(areaLog.Split('\n').Any(line => line.Contains("candidate=QAFar ", StringComparison.Ordinal) &&
                    line.Contains("distance=2.5000 floorDelta=0.00", StringComparison.Ordinal) &&
                    line.TrimEnd().EndsWith("reason=sight", StringComparison.Ordinal)),
                $"{sample.Id} excludes the in-range hostile specifically for sight");
            Check.That(!areaLog.Split('\n').Any(line => line.Contains($"[FREECAST-VIRAL] ability={sample.Id} ", StringComparison.Ordinal) &&
                    line.Contains("target=QAFar ", StringComparison.Ordinal)),
                $"{sample.Id} never rolls a contest for the blocked recipient");
            int acknowledgement = blocked.World.RpcSnapshot().Count;
            blocked.SendRpc("81 48");
            await StockAbilityProfile.ObserveAsync(blocked, acknowledgement, frame => BytePattern.Parse("81 49").IsMatch(frame.Bytes),
                "Blocked witness stayed connected through landing");
            Check.That(!blocked.World.RpcSnapshot().Skip(fromBlocked).Any(frame => ManageBonusRecord.TryDecode(frame.Bytes, out var bonus) &&
                    bonus!.AddRemoveType == 0 && (bonus.SourceId == sample.Id || bonus.SourceId == TempMods.ControlImmunityTimerAbility)),
                $"{sample.Id} sends no effect or immunity to the obstructed witness");
            if (sample.Id == Disruption)
                await StockAbilityProfile.ObserveAsync(clear, fromClear, frame => StockAbilityProfile.Bonus(frame,
                    TempMods.ControlImmunityTimerAbility, 681, 0, 8, true), "Area Disruption immunity ends before the Freeze cast");
        }
        Check.Equal(0, actor.World.Problems.Count + clear.World.Problems.Count + blocked.World.Problems.Count, "Area sight fixture framing");
        return Array.Empty<byte>();
    }
}
