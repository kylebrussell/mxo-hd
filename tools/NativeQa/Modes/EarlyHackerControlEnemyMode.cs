using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class EarlyHackerControlEnemyMode : NativeQaMode
{
    private const ushort HackerStyle = CombatStyles.HackerStyle;
    // PR487 native PacketLog, lines 566–567.
    private const string StockStyle = "80b9ee030000000000000000000000000000000000000000000000000000000090c97941086b0000000000";
    private static readonly (ushort Ability, string Capture, string Enemy, ushort State, float Duration, int Cast, int Reuse)[] Samples =
    {
        (43, EarlyHackerControlRecoveryMode.BlindRequest, "QAEnemy", TempMods.BlindAbility, 16, 2, 43),
        (48, EarlyHackerControlRecoveryMode.PacifyRequest, "QAFriend", TempMods.MezAbility, 6, 2, 43),
        (315, EarlyHackerControlRecoveryMode.StunRequest, "QAFar", TempMods.StunAbility, 7.35f, 4, 8)
    };
    private static readonly (uint Id, string Name, int X, int Z, int Alignment)[] Stands =
    {
        (6113, "QAOther", 24800, -1898, 1),
        (6114, "QAEnemy", 25200, -1498, 2),
        (6111, "QAFriend", 25100, -1360, 2),
        (6112, "QAFar", 25200, -1620, 2)
    };
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, HackerStyle, 43, 48, 315).Order().ToArray();

    public override string Name => "ability-parity-early-hacker-control-enemy";
    public override NativeQaModePlan Plan { get; } = new(
        Loadout, 24800, -1498, Array.Empty<string>(), Array.Empty<string>(), new[]
        {
            "UPDATE characters SET level=50,y=495,alignment=1,pvpflag=0,healthC=1000,healthM=1000,innerStrC=1000,innerStrM=1000 WHERE charId=110",
            "UPDATE char_abilities SET level=50 WHERE char_id=110",
            "DELETE FROM char_abilities WHERE char_id IN (6111,6112,6113,6114)"
        }.Concat(Stands.Select(stand =>
            $"UPDATE characters SET x={stand.X},y=495,z={stand.Z},alignment={stand.Alignment},level=50,pvpflag=0 WHERE charId={stand.Id}"))
            .ToArray(),
        DevCommands: true, loadAbilityAncestors: true, hotbarAbilities: new ushort[] { HackerStyle, 43, 48, 315 },
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "10",
            ["MXO_FREECAST_VIRAL_QA"] = "1", ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766", ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0"
        },
        extraQaMetadata: new() { ["ScopedStockRows"] = "43,48,315", ["NativeCloseableRows"] = 1 },
        readyMessages: new[]
        {
            "Select Hacker Style in page 2 slot 1. Select friendly QAOther four meters south. Try slots 2–4 (Disrupt Inputs, Freeze System and Stun). The client may refuse locally. A sent request should return one Target is not valid line with no cast bar or reuse.",
            "On three separate fresh opposing targets, try slot 2 on QAEnemy, slot 3 on QAFriend, and slot 4 on QAFar. Expect Blind 16 s, Pacify 6 s and Stun 7.35 s respectively. QAFriend is an enemy in this fixture. The disposable fixture fixes only eligible viral hit draws. Enter submits helper chat. Keep the selected target without Escape. Record each state add and its natural removal. For Pacify and Stun, the separate immunity must still be active when the control ends (12 s and 14 s total). Do not move or damage the helpers during the timers."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var server = context.ActualServer!;
        foreach (var stand in Stands)
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, stand.Name, stand.Name, stockCharacterLoad: true);
            context.Helpers.Add(stand.Name, helper);
            await helper.EnterWorldAsync();
            NativeQaReadiness.RequireNoAmbientAggro(server, helper);
        }
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(50), "This control profile fits native memory");
        await actor.EnterWorldAsync();
        foreach (var stand in Stands)
        {
            var view = await actor.WaitForViewNamedAsync(stand.Name);
            Check.That(EngageFoesFixture.Position(view).SequenceEqual(new double[] { stand.X, 495, stand.Z }),
                $"{stand.Name} is visible at its printed stand");
        }
        actor.SendRpc(StockStyle);
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 ee 03 00 00");

        var friendly = await actor.WaitForViewNamedAsync("QAOther");
        uint friendlyHandle = DuelPackets.Handle(friendly.ViewId, friendly.SpawnId);
        actor.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(friendlyHandle))}");
        foreach (var sample in Samples)
            await StarterHackerControlTargetMode.RefusedAsync(actor, sample.Ability,
                StarterHackerControlTargetMode.WithHandle(sample.Capture, friendlyHandle));

        var removals = new List<Task>();
        foreach (var (ability, capture, name, state, duration, cast, reuse) in Samples)
        {
            var target = await actor.WaitForViewNamedAsync(name);
            var recipient = context.Helpers[name];
            uint handle = DuelPackets.Handle(target.ViewId, target.SpawnId);
            actor.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(handle))}");
            int sourceAt = actor.World.RpcSnapshot().Count, recipientAt = recipient.World.RpcSnapshot().Count;
            actor.SendRpc(StarterHackerControlTargetMode.WithHandle(capture, handle));
            await EvidenceRetestWire.CastBarAsync(actor, ability, cast);
            await EvidenceRetestWire.TimerAsync(actor, sourceAt, ability, AbilityCooldowns.OwnTimer, reuse);
            var applied = await StockAbilityProfile.ObserveAsync(recipient, recipientAt,
                frame => IsControl(frame, ability, state, duration, 0),
                $"{name} receives control {state} from {ability} for {duration} seconds");
            Check.That(ManageBonusRecord.TryDecode(applied.Bytes, out var added), "Control record decodes");
            int immunity = ability == 48 ? 12 : ability == 315 ? 14 : 0;
            removals.Add(CheckExpiryAsync(recipient, recipientAt, ability, state, added!.RecordId, immunity));
        }
        await Task.WhenAll(removals);
        Check.Equal(0, actor.World.Problems.Count + context.Helpers.Values.Sum(helper => helper.World.Problems.Count),
            "Hacker control targeting framing");
        return Array.Empty<byte>();
    }

    private static async Task CheckExpiryAsync(HeadlessPlayer recipient, int cursor, ushort ability,
        ushort state, ushort recordId, int immunity)
    {
        bool Removed(RpcRecord frame, ushort source, ushort target, ushort id) =>
            ManageBonusRecord.TryDecode(frame.Bytes, out var modifier) && modifier!.SourceId == source &&
            modifier.TargetId == target && modifier.RecordId == id && modifier.AddRemoveType == 1;

        ushort? immunityId = null;
        if (immunity != 0)
        {
            var added = await StockAbilityProfile.ObserveAsync(recipient, cursor, frame =>
                ManageBonusRecord.TryDecode(frame.Bytes, out var record) && record!.SourceId == TempMods.ControlImmunityTimerAbility &&
                record.TargetId == TempMods.ISDrainTargetAbility && record.Duration == immunity && record.AddRemoveType == 0,
                $"{ability} installs independent {immunity}-second immunity");
            Check.That(ManageBonusRecord.TryDecode(added.Bytes, out var bonus), "Immunity record decodes");
            immunityId = bonus!.RecordId;
        }

        await recipient.WaitUntilAsync(() => recipient.World.RpcSnapshot().Skip(cursor)
            .Any(frame => Removed(frame, ability, state, recordId)),
            $"{ability} removes its original state record", TimeSpan.FromSeconds(20));
        if (immunityId is ushort id)
        {
            await recipient.WaitUntilAsync(() => recipient.World.RpcSnapshot().Skip(cursor)
                .Any(frame => Removed(frame, TempMods.ControlImmunityTimerAbility, TempMods.ISDrainTargetAbility, id)),
                $"{ability} removes its independent immunity record", TimeSpan.FromSeconds(20));
            var frames = recipient.World.RpcSnapshot().Skip(cursor).ToList();
            Check.That(frames.FindIndex(frame => Removed(frame, ability, state, recordId)) <
                frames.FindIndex(frame => Removed(frame, TempMods.ControlImmunityTimerAbility, TempMods.ISDrainTargetAbility, id)),
                $"{ability} state removal precedes immunity removal");
        }
    }

    private static bool IsControl(RpcRecord frame, ushort ability, ushort state, float duration, byte action) =>
        ManageBonusRecord.TryDecode(frame.Bytes, out var modifier) && modifier!.SourceId == ability &&
        modifier.Type == TempMods.KindState && modifier.TargetId == state && modifier.AddRemoveType == action &&
        Math.Abs(modifier.Duration - duration) < 0.005f;
}
