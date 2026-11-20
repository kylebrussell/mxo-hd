using hds;
using HeadlessClient;
using MxoHd.PacketResearch;
using static NativeQa.StockAbilityProfile;

namespace NativeQa.Modes;

public sealed class StarterHackerSlowRadiusMode : NativeQaMode
{
    private const ushort Style = 1006, UiLag = 97, Slow = 86, Freeze = 40;
    private const string StockStyle = "80b9ee030000000000000000000000000000000000000000000000000000000090c97941086b0000000000";
    // Retail special_agent_spawn line 1214, PR439 Slow, PR410 control capture for Code Freeze.
    private const string LagCast = "80b961001200760000000000000000000000000000000000000000000000000000000000086b0000000000";
    private const string SlowCast = "80b95600040002000000000000000000000000000000000000000000585cbb0001000000086b0000000000";
    private const string FreezeCast = "80b92800040002000000000000000000000000000000000000000000ffff7f7f00000000086b0000000000";
    private static readonly (string Name, uint Id, int X, int Z)[] Stands =
    {
        ("QAEnemy", 6114, 25000, -1498), ("QAFar", 6112, 25250, -1498), ("QAOther", 6113, 24900, -1398)
    };

    public override string Name => "ability-parity-starter-hacker-slow-radius";
    public override NativeQaModePlan Plan { get; } = new(Profile(2, 17, Style, UiLag, Slow, Freeze).Order().ToArray(),
        24800, -1498, Array.Empty<string>(), Array.Empty<string>(), new[]
        {
            $"UPDATE characters SET level=50,exp={PlayerProgression.ExperienceForLevel(50)},y=495,healthC=3000,healthM=3000,innerStrC=500,innerStrM=500,alignment=CASE WHEN charId IN (110,6113) THEN 1 ELSE 2 END,pvpflag=0 WHERE charId IN (110,6112,6113,6114)",
            "DELETE FROM char_abilities WHERE char_id IN (6112,6113,6114)"
        }.Concat(Stands.Select(stand => $"UPDATE characters SET x={stand.X},z={stand.Z} WHERE charId={stand.Id}")).ToArray(),
        DevCommands: true, hotbarAbilities: new ushort[] { Style, UiLag, Slow, Freeze }, environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "10",
            ["MXO_FREECAST_VIRAL_QA"] = "1", ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766", ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "97,86,40", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            "Use Hacker Style on page 2 slot 1. Right of CSR are QAEnemy at 2 m and QAFar at 4.5 m; friendly QAOther stands inside 3 m. Select QAFar and keep that selection without Escape.",
            "Slot 2 UI Lag centres on CSR. Only QAEnemy gets the four 27 s debuffs, even with QAFar selected. Slots 3 and 4 (Slow and Code Freeze) must instead land on QAFar only. Capture the source records or logs on each player. Stay out of interlock and wait for each cast to land."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        foreach (var stand in Stands)
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, stand.Name, stand.Name, stockCharacterLoad: true);
            context.Helpers.Add(stand.Name, helper);
            await helper.EnterWorldAsync();
        }
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, actor);
        actor.SendRpc(StockStyle);
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 ee 03 00 00");
        foreach (var helper in context.Helpers.Values) await actor.WaitForViewNamedAsync(helper.Handle);
        var far = context.Helpers["QAFar"];
        var view = await actor.WaitForViewNamedAsync(far.Handle);
        uint handle = DuelPackets.Handle(view.ViewId, view.SpawnId);
        actor.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(handle))}");

        foreach (var (id, request, recipient) in new[] { (UiLag, LagCast, "QAEnemy"), (Slow, SlowCast, "QAFar"), (Freeze, FreezeCast, "QAFar") })
        {
            var cursors = context.Helpers.Values.ToDictionary(helper => helper.Handle, helper => helper.World.RpcSnapshot().Count);
            int castAt = actor.World.RpcSnapshot().Count;
            actor.SendRpc(StarterHackerControlTargetMode.WithHandle(request, handle));
            var target = context.Helpers[recipient];
            bool Applied(RpcRecord frame) => ManageBonusRecord.TryDecode(frame.Bytes, out var mod) &&
                mod!.SourceId == id && mod.AddRemoveType == 0;
            await ObserveAsync(target, cursors[recipient], Applied, $"{id} lands on {recipient}");
            if (id == UiLag)
            {
                foreach (var (stat, value) in new (ushort, int)[] { (1032, -10), (183, -17), (1011, -12), (342, -14) })
                    await ObserveAsync(target, cursors[recipient], frame => Bonus(frame, id, stat, value, 27, false),
                        $"UI Lag adds {stat} on the near opponent only");
            }
            else if (id == Slow)
                await ObserveAsync(target, cursors[recipient], frame => Bonus(frame, Slow, 115, -50, 28, false), "Slow stays selected-target");
            else
                await ObserveAsync(target, cursors[recipient], frame => ManageBonusRecord.TryDecode(frame.Bytes, out var root) &&
                    root!.SourceId == Freeze && root.TargetId == TempMods.RootedAbility && root.AddRemoveType == 0, "Code Freeze roots the selected opponent");
            foreach (var other in context.Helpers.Values.Where(helper => helper.Handle != recipient))
            {
                int barrier = other.World.RpcSnapshot().Count;
                other.SendRpc("81 48");
                await ObserveAsync(other, barrier, frame => BytePattern.Parse("81 49").IsMatch(frame.Bytes), "Recipient exclusion barrier");
                Check.That(!other.World.RpcSnapshot().Skip(cursors[other.Handle]).Any(Applied), $"{id} excludes {other.Handle}");
            }
            if (AbilityCooldowns.TryGet(id, out var cooldown))
            {
                await EvidenceRetestWire.TimerAsync(actor, castAt, id, AbilityCooldowns.OwnTimer, cooldown.Seconds);
                if (cooldown.Group != 0 && id != Freeze)
                    await EvidenceRetestWire.TimerAsync(actor, castAt, cooldown.Group, AbilityCooldowns.GroupTimer, cooldown.GroupSeconds, ended: true);
            }
        }
        Check.Equal(0, actor.World.Problems.Count + context.Helpers.Values.Sum(helper => helper.World.Problems.Count),
            "Hacker radius and selected-target framing");
        return Array.Empty<byte>();
    }
}
