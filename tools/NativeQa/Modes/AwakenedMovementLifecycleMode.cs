using hds;
using HeadlessClient;
using MxoHd.PacketResearch;
using static NativeQa.StockAbilityProfile;

namespace NativeQa.Modes;

public sealed class AwakenedMovementLifecycleMode : NativeQaMode
{
    // Vector12 line 2445, native 11701b9dd84e and Vector_abilityloadoutchange lines 19011/20428.
    private const string Speed = "80b90c00000000000000000000000000000000000000000000000000000015000000071c086b0000000000";
    private const string Sprint = "80b9b800000000000000000000000000000000000000000000000000ffff7f7f00000000086b0000000000";
    private const string Jump = "80c20000008004baadc000000000005ea040000000008c94e2c0f2001900010000f0204601321c0a00";
    private const string Cancel = "80c400000000f8b9adc000000000005ea040000000006c94e2c0f20019000100";

    public override string Name => "ability-parity-awakened-movement-lifecycle";
    public override NativeQaModePlan Plan { get; } = new(
        new ushort[] { 2, 17, 11, 12, 184 }, 24800, -1498, Array.Empty<string>(), Array.Empty<string>(), new[]
        {
            "UPDATE characters SET y=495,innerStrC=1000,innerStrM=1000 WHERE charId=110",
            "UPDATE characters SET x=25200,y=495,z=-1498 WHERE charId=6111"
        }, DevCommands: true, loadAbilityAncestors: true, hotbarAbilities: new ushort[] { 11, 12, 184 },
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        extraQaMetadata: new() { ["ScopedStockRows"] = "11,12,184", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            "Mara outdoors. Hotbar page 2 slots 1–3 are Hyper-Jump, Hyper-Speed, Hyper-Sprint. Clear the target. Run slot 3 first and slot 2 next. Capture owner and QAFriend seeing +0.45 movement while both remain active. The owner should receive both active flags, 0x1400.",
            "With both movement effects active, use stock Hyper-Jump aim on the open street. Try one escape from aim and one completed jump. The committed jump should emit one start and a matching clock echo. Compare own IS on the jump packet with the latest resource update, allowing Sprint's normal timed debit.",
            "Use slot 3 again to stop Sprint. Hyper-Speed should remain active at +0.45. Capture the 5 s reuse; Sprint's resource drain should stop. If the client exposes a Hyper-Speed dismissal, capture +0.00. Stock aim cancel and an in-flight jump cancel are separate gestures."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var observer = await HeadlessPlayer.LoginAsync(context.Address, "QAFriend", "QAFriend", stockCharacterLoad: true);
        context.Helpers.Add(observer.Handle, observer);
        await observer.EnterWorldAsync();
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, actor);
        ushort view = (await observer.WaitForViewNamedAsync(actor.Handle)).ViewId;
        int visible = observer.World.ViewSnapshot().Count;
        actor.SendRpc(Convert.FromHexString(Sprint));
        await actor.WaitForRpcAsync("80 bd 05 b8 00 00 00 00 00 00 01");
        await MovementAsync(observer, visible, view, .15f, 0x0400);

        int ownCursor = actor.World.ViewSnapshot().Count;
        int rpcCursor = actor.World.RpcSnapshot().Count;
        visible = observer.World.ViewSnapshot().Count;
        actor.SendRpc(Convert.FromHexString(Speed));
        // Read the full activation record, including the final authoritative flags.
        var owner = BytePattern.Parse("02 00 02 88 05 8c 1e 66 66 e6 3e 80 90 ?? ?? b0 56 06 00 28 ?? 02 00 14");
        await actor.WaitUntilAsync(() => actor.World.ViewSnapshot().Skip(ownCursor).Any(owner.Occurs),
            "Hyper-Speed's own activation preserves active Hyper-Sprint", HeadlessPlayer.DefaultTimeout);
        await ObserveAsync(actor, rpcCursor, packet => Bonus(packet, 12, 115, 45, 600, remove: false), "Hyper-Speed linked movement +45");
        var scale = PlayerViewAttributes.Build((5, PlayerViewAttributes.F32(.45f)));
        var remote = BytePattern.Parse(Convert.ToHexString(BitConverter.GetBytes(view).Concat(scale).ToArray()));
        await observer.WaitUntilAsync(() => observer.World.ViewSnapshot().Skip(visible).Any(remote.Occurs),
            "Observer sees +0.45 movement", HeadlessPlayer.DefaultTimeout);

        // Reuse retail records with the destination and navigation tuple of the Mara fixture.
        byte[] start = AtFixture(Jump, 24885, -3247, 1044, 16, 3);
        byte[] cancel = AtFixture(Cancel, 24800, -1498, 847, 20, 5);
        rpcCursor = actor.World.RpcSnapshot().Count;
        ownCursor = actor.World.ViewSnapshot().Count;
        actor.SendRpc(start);
        await actor.WaitForRpcAsync("80 c3 32 1c 0a 00");
        var jump = BytePattern.Parse("02 00 03 01 08 00 80 80 bc");
        await actor.WaitUntilAsync(() => actor.World.ViewSnapshot().Skip(ownCursor).Any(jump.Occurs),
            "Stock Hyper-Jump reaches the normal movement consumer", HeadlessPlayer.DefaultTimeout);
        actor.SendRpc(cancel);
        int barrier = actor.World.RpcSnapshot().Count;
        actor.SendRpc("81 48");
        await ObserveAsync(actor, barrier, packet => BytePattern.Parse("81 49").IsMatch(packet.Bytes), "Post-cancel barrier");
        Check.Equal(1, actor.World.RpcSnapshot().Skip(rpcCursor)
            .Count(packet => BytePattern.Parse("80 c3").IsMatch(packet.Bytes)), "Jump cancellation creates no extra clock echo");

        visible = observer.World.ViewSnapshot().Count;
        rpcCursor = actor.World.RpcSnapshot().Count;
        actor.SendRpc(Convert.FromHexString(Sprint));
        await actor.WaitForRpcAsync("80 bd 05 b8 00 00 00 00 00 00 00");
        await EvidenceRetestWire.TimerAsync(actor, rpcCursor, 184, AbilityCooldowns.OwnTimer, 5);
        await MovementAsync(observer, visible, view, .45f, 0x1000);

        rpcCursor = actor.World.RpcSnapshot().Count;
        visible = observer.World.ViewSnapshot().Count;
        actor.SendRpc("80 bb 0c 00");
        await ObserveAsync(actor, rpcCursor, packet => Bonus(packet, 12, 115, 45, 600, remove: true), "Hyper-Speed removes the original movement modifier");
        scale = PlayerViewAttributes.Build((5, PlayerViewAttributes.F32(0)));
        remote = BytePattern.Parse(Convert.ToHexString(BitConverter.GetBytes(view).Concat(scale).ToArray()));
        await observer.WaitUntilAsync(() => observer.World.ViewSnapshot().Skip(visible).Any(remote.Occurs),
            "Observer returns to baseline after dismiss", HeadlessPlayer.DefaultTimeout);

        Check.Equal(0, actor.World.Problems.Count + observer.World.Problems.Count, "Awakened movement framing");
        return Array.Empty<byte>();
    }

    private static byte[] AtFixture(string capture, double x, double z, ushort sector, ushort prefab, ushort poly)
    {
        byte[] packet = Convert.FromHexString(capture);
        var destination = new JumpRecords.Destination(BitConverter.DoubleToUInt64Bits(x),
            BitConverter.DoubleToUInt64Bits(495), BitConverter.DoubleToUInt64Bits(z), sector, prefab, poly);
        byte[] location = JumpRecords.Encode(new JumpRecords.Cancel(destination));
        location.AsSpan(2).CopyTo(packet.AsSpan(2));
        return packet;
    }

    private static async Task MovementAsync(HeadlessPlayer watcher, int cursor, ushort view, float speed, ushort flags)
    {
        byte[] update = PlayerViewAttributes.Build((5, PlayerViewAttributes.F32(speed)), (27, PlayerViewAttributes.U16(flags)));
        var pattern = BytePattern.Parse(Convert.ToHexString(BitConverter.GetBytes(view).Concat(update).ToArray()));
        await watcher.WaitUntilAsync(() => watcher.World.ViewSnapshot().Skip(cursor).Any(pattern.Occurs),
            $"Observer movement {speed} and flags 0x{flags:x4}", HeadlessPlayer.DefaultTimeout);
    }
}
