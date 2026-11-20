using System.Buffers.Binary;
using hds;
using HeadlessClient;
using static NativeQa.StockAbilityProfile;

namespace NativeQa.Modes;

public sealed class StarterMovementObserverMode : NativeQaMode
{
    private const ushort Sprint = 184, Speed = 12, Accelerator = 62, HackerStyle = CombatStyles.HackerStyle;
    private static readonly ushort[] Loadout = Profile(2, 17, Sprint, Speed, Accelerator, HackerStyle).Order().ToArray();

    // Stock 11701b9dd84e (Hyper-Sprint from the shared fixture, Hyper-Speed lines 3289–3290).
    private const string SprintRequest = "80b9b800000000000000000000000000000000000000000000000000ffff7f7f00000000086b0000000000";
    private const string SpeedRequest = "80b90c00000000000000000000000000000000000000000000000000ffff7f7f00000000086b0000000000";
    // Retail PacketsZombiesPortingEtc, lines 81076–81119.
    private const string DismissSpeedRequest = "80 bb 0c 00";
    // Stock 8bbf81646957 lines 3073–3074 and fa273b663f5e lines 11334–11335.
    private const string HackerRequest = "80b9ee03000000000000000000000000000000000000000000000000d557377c20000000086b0000000000";
    private const string AcceleratorRequest = "80b93e000d000b000000000000000000000000000000000000000000785bb60001000000086b0000000000";

    public override string Name => "ability-parity-starter-movement-observers";

    public override NativeQaModePlan Plan { get; } = new(Loadout, 24800, -1498,
        Array.Empty<string>(), Array.Empty<string>(), new[]
        {
            $"UPDATE characters SET level=50,exp={PlayerProgression.ExperienceForLevel(50)},y=495,innerStrC=1500,innerStrM=1500 WHERE charId=110",
            "UPDATE characters SET x=25200,y=495,z=-1498 WHERE charId=6111",
            "UPDATE characters SET x=24300,y=495,z=13984 WHERE charId=6112"
        }, DevCommands: true, loadAbilityAncestors: true,
        hotbarAbilities: new ushort[] { Sprint, Speed, HackerStyle, Accelerator },
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        extraQaMetadata: new() { ["ScopedStockRows"] = "12,62,184", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            "With no target, page 2 slot 1 turns Hyper-Sprint on (+0.15 speed). Wait for the first 5 s drain, then press again to turn it off. QAFriend receives both observer updates.",
            "Still with no target, slot 2 applies Hyper-Speed (+0.45 speed). Type ?dismissbuff 12 and confirm the observer resets to zero again.",
            "Select Hacker Style with slot 3. Select QAFriend 4 m east and press slot 4. Capture +0.10 movement on QAFriend's own view and on QAFriend's view in CSR's PacketLog.",
            "While +0.10 is active, bring QAFar in with helper chat ?tp 24300 495 -1498 0 0 0. Its first view of QAFriend includes +0.10. Send ?dismissbuff 62 as QAFriend and confirm both observers reset to zero. Capture helpers through their broker PacketLogs."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        foreach (string handle in new[] { "QAFriend", "QAFar" })
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, handle, handle);
            context.Helpers.Add(handle, helper);
            await helper.EnterWorldAsync();
            NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, helper);
        }
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(50), "The fixture uses legal ability memory");
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, actor);
        var friend = context.Helpers["QAFriend"];
        var late = context.Helpers["QAFar"];
        var target = await actor.WaitForViewNamedAsync(friend.Handle);
        ushort observedActor = (await friend.WaitForViewNamedAsync(actor.Handle)).ViewId;

        int cursor = friend.World.ViewSnapshot().Count;
        actor.SendRpc(SprintRequest);
        await ObserverScale(friend, cursor, observedActor, .15f, 0x0400);
        await actor.WaitUntilAsync(() => context.ActualServer!.DebugLog().Contains("[BUFF-DRAIN] char110 ability184", StringComparison.Ordinal),
            "Hyper-Sprint's first five-second drain", TimeSpan.FromSeconds(8));
        cursor = friend.World.ViewSnapshot().Count;
        actor.SendRpc(SprintRequest);
        await ObserverScale(friend, cursor, observedActor, 0f, 0);

        cursor = friend.World.ViewSnapshot().Count;
        actor.SendRpc(SpeedRequest);
        await ObserverScale(friend, cursor, observedActor, .45f);
        cursor = friend.World.ViewSnapshot().Count;
        actor.SendRpc(DismissSpeedRequest);
        await ObserverScale(friend, cursor, observedActor, 0f);

        actor.SendRpc(HackerRequest);
        await actor.WaitForRpcAsync("80 bd 05 ee 03 00 00 00 00 00 01");
        byte[] request = Convert.FromHexString(AcceleratorRequest);
        BinaryPrimitives.WriteUInt32LittleEndian(request.AsSpan(4), DuelPackets.Handle(target.ViewId, target.SpawnId));
        int ownerAt = friend.World.ViewSnapshot().Count;
        cursor = actor.World.ViewSnapshot().Count;
        actor.SendRpc(request);
        await EvidenceRetestWire.ViewSinceAsync(friend, ownerAt, 2, "02 80 88 cd cc cc 3d 80 80 80 02 00 00");
        await ObserverScale(actor, cursor, target.ViewId, .10f);

        late.SendChat("?tp 24300 495 -1498 0 0 0");
        await late.WaitForTextAsync("Teleported to 0/0");
        var lateView = await late.WaitForViewNamedAsync(friend.Handle);
        Check.Equal(.10f, BitConverter.ToSingle(lateView.Attributes["MovementScale"]), "A late observer gets the active scale in the creation");
        int lateAt = late.World.ViewSnapshot().Count;
        cursor = actor.World.ViewSnapshot().Count;
        friend.SendChat("?dismissbuff 62");
        await ObserverScale(actor, cursor, target.ViewId, 0f);
        await ObserverScale(late, lateAt, lateView.ViewId, 0f);
        Console.WriteLine("  Stock observer requests: 184 +0.15/off, 12 +0.45/off, 62 +0.10 in owner, observer and late creation, both observers clear");
        return Array.Empty<byte>();
    }

    private static Task ObserverScale(HeadlessPlayer observer, int start, ushort view, float scale, ushort? bits = null)
    {
        var update = bits.HasValue
            ? PlayerViewAttributes.Build((5, PlayerViewAttributes.F32(scale)), (27, PlayerViewAttributes.U16(bits.Value)))
            : PlayerViewAttributes.Build((5, PlayerViewAttributes.F32(scale)));
        return EvidenceRetestWire.ViewSinceAsync(observer, start, view, WireFormat.ToHex(update));
    }
}
