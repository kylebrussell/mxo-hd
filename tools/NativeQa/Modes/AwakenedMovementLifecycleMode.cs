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

    private const ushort Lockdown = 21;
    // Complete PR409 stock request. The selected view/spawn is replaced in each session.
    private const string LockdownCast = "80b915000d000b000000000000000000000000000000000000000000585cc70001000000086b0000000000";
    private const string CoderStyle = "80b9f003000000000000000000000000000000000000000000000000585cc70001000000086b0000000000";

    public override string Name => "ability-parity-awakened-movement-lifecycle";
    public override NativeQaModePlan Plan { get; } = new(
        new ushort[] { 2, 17, 11, 12, 184 }, 24800, -1498, Array.Empty<string>(), Array.Empty<string>(), new[]
        {
            $"UPDATE characters SET level=50,exp={PlayerProgression.ExperienceForLevel(50)},y=495,pvpflag=0,alignment=CASE WHEN charId=6114 THEN 2 ELSE 1 END,innerStrC=1000,innerStrM=1000 WHERE charId IN (110,6111,6114)",
            "UPDATE characters SET x=25200,z=-1498 WHERE charId=6111",
            "UPDATE characters SET x=25100,z=-1320 WHERE charId=6114",
            "DELETE FROM char_abilities WHERE char_id=6114",
            "INSERT INTO char_abilities (char_id,slot,ability_id,level,is_loaded) VALUES " +
                string.Join(",", Profile(2, 17, 1008, Lockdown).Order().Select((id, i) =>
                    $"(6114,{400 + i},{0x80000000L | ((long)id << 10)},50,1)"))
        }, DevCommands: true, loadAbilityAncestors: true, hotbarAbilities: new ushort[] { 11, 12, 184 },
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1",
            ["MXO_PVP_MAX_SAFE_LEVEL"] = "15", ["MXO_FREECAST_VIRAL_QA"] = "1",
            ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766", ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0" },
        extraQaMetadata: new() { ["ScopedStockRows"] = "11,12,21,184", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            """At the starting stand, ask QAEnemy to root CSR using {"actor":"QAEnemy","action":"cast","target":"CSR_Tester","ability":21}. During the 12 s root, try walking and commit a Hyper-Jump while facing down the open street. Capture client suppression or a server correction to the stand. Wait for the root removal before repeating. Enter submits any chat. Avoid Escape while a combat target is selected.""",
            "After the root clears, test Hyper-Jump before starting either speed buff. At 24800,495,-1498, clear the target. Use page 2 slot 1 (or drag Hyper-Jump from Actions to a free hotbar slot as in the prior native pass). A single left click on visible outdoor ground commits the aim. Use the open street toward 24885,495,-3247, which passed at this stand in PR384. If aiming gives no start, use the stock default Ctrl+Space while facing down the open street. Confirm 80 c2 and one matching 80 c3. Escape cancels aim locally.",
            "Return to the original stand with ?tp 24800 495 -1498 0 0 0. Clear the target. Run slot 3 first and slot 2 next. Capture owner and QAFriend seeing +0.45 movement while both remain active. The owner should receive both active flags, 0x1400.",
            "Use slot 3 again to stop Sprint. Hyper-Speed remains active at +0.45. Capture the 5 s reuse and confirm Sprint's resource drain stops. Use the dev-only ?dismissbuff 12 if a zero-speed reset is needed. Run any optional in-flight cancel separately from Escape in aim."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var observer = await HeadlessPlayer.LoginAsync(context.Address, "QAFriend", "QAFriend", stockCharacterLoad: true);
        context.Helpers.Add(observer.Handle, observer);
        await observer.EnterWorldAsync();
        var enemy = await HeadlessPlayer.LoginAsync(context.Address, "QAEnemy", "QAEnemy", stockCharacterLoad: true);
        context.Helpers.Add(enemy.Handle, enemy);
        await enemy.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, enemy);
        enemy.SendRpc(CoderStyle);
        await enemy.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 f0 03 00 00");
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, actor);
        // Reuse retail records with the destination and navigation tuple of the Mara fixture.
        byte[] start = AtFixture(Jump, 24885, -3247, 1044, 16, 3);
        byte[] cancel = AtFixture(Cancel, 24800, -1498, 847, 20, 5);
        int rootedAt = actor.World.RpcSnapshot().Count;
        await SendLockdownAsync(enemy, actor.Handle, Lockdown);
        var root = await ObserveAsync(actor, rootedAt, frame =>
            ManageBonusRecord.TryDecode(frame.Bytes, out var effect) && effect!.SourceId == Lockdown &&
            effect.TargetId == TempMods.RootedAbility && effect.AddRemoveType == 0 && effect.Duration == 12,
            "QAEnemy's real Lockdown roots the jumper");
        int blockedAt = actor.World.RpcSnapshot().Count;
        actor.SendRpc(start);
        string stand = Convert.ToHexString(new[] { 24800f, 495f, -1498f }.SelectMany(BitConverter.GetBytes).ToArray());
        await actor.WaitForViewUpdateAsync(2, $"01 0c ?? {stand}");
        actor.SendRpc("81 48");
        await ObserveAsync(actor, blockedAt, frame => BytePattern.Parse("81 49").IsMatch(frame.Bytes), "Rooted jump barrier");
        Check.That(!actor.World.RpcSnapshot().Skip(blockedAt).Any(frame => BytePattern.Parse("80 c3").IsMatch(frame.Bytes)),
            "Root rejects the start before the jump clock echo");
        var removed = root.Bytes.ToArray();
        removed[17] = 1;
        await actor.WaitUntilAsync(() => actor.World.RpcSnapshot().Skip(rootedAt).Any(frame => frame.Bytes.AsSpan().SequenceEqual(removed)),
            "Root naturally removes before the same jump is sent", TimeSpan.FromSeconds(16));

        int rpcCursor = actor.World.RpcSnapshot().Count;
        int ownCursor = actor.World.ViewSnapshot().Count;
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

        ushort view = (await observer.WaitForViewNamedAsync(actor.Handle)).ViewId;
        int visible = observer.World.ViewSnapshot().Count;
        actor.SendRpc(Convert.FromHexString(Sprint));
        await actor.WaitForRpcAsync("80 bd 05 b8 00 00 00 00 00 00 01");
        await MovementAsync(observer, visible, view, .15f, 0x0400);

        ownCursor = actor.World.ViewSnapshot().Count;
        rpcCursor = actor.World.RpcSnapshot().Count;
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

    public static async Task<object> SendLockdownAsync(HeadlessPlayer caster, string target, ushort ability)
    {
        if (caster.Handle != "QAEnemy" || target != "CSR_Tester" || ability != Lockdown)
            throw new InvalidOperationException("Use QAEnemy to cast Lockdown on CSR_Tester in this fixture.");
        var view = await caster.WaitForViewNamedAsync(target);
        byte[] request = StarterHackerControlTargetMode.WithHandle(LockdownCast, DuelPackets.Handle(view.ViewId, view.SpawnId));
        caster.SendRpc(request);
        return new { Ability = ability, Target = target, Request = Convert.ToHexString(request) };
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
