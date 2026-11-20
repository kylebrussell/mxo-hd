using hds;
using HeadlessClient;
using static NativeQa.StockAbilityProfile;

namespace NativeQa.Modes;

public sealed class StarterCoderPetLifecycleMode : NativeQaMode
{
    private const ushort Style = 1008, Proxy = 27, Fortify = 20, Drain = 19;
    // Complete stock PR440 requests, PacketLog lines 1414, 2100 and 2124.
    private const string Summon = "80b91b00000000000000000000000000000000000000000000000000d557377c20000000086b0000000000";
    private const string FortifyRequest = "80b914000d000c000000000000000000000000000000000000000000585cbe0001000000086b0000000000";
    private const string DrainRequest = "80b913000d000c000000000000000000000000000000000000000000585cbe0001000000086b0000000000";
    private const string CoderStyle = "80b9f003000000000000000000000000000000000000000000000000585cc70001000000086b0000000000";

    public override string Name => "ability-parity-starter-coder-pet-lifecycle";
    public override NativeQaModePlan Plan { get; } = new(Profile(2, 17, Style, Proxy, Fortify, Drain).Order().ToArray(),
        24800, -1498, Array.Empty<string>(), Array.Empty<string>(), new[]
        {
            $"UPDATE characters SET level=15,exp={PlayerProgression.ExperienceForLevel(15)},y=495,innerStrC=500,innerStrM=1000 WHERE charId=110",
            "UPDATE char_abilities SET level=LEAST(level,15) WHERE char_id=110"
        }, DevCommands: true, hotbarAbilities: new ushort[] { Style, Proxy, Fortify, Drain }, environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "27,20,19", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            "Use page 2 slot 1 Coder Style and slot 2 Remote Proxy. Capture the new owned pet window and level-4 view. Select its body; Enter submits commands without using Escape.",
            "Send Fortify (slot 3), then dismiss the proxy during its 2 s cast if possible. Use the Simulacra Deactivate control or the dev command ?pet dismiss. Expect one removal and no old-pet Fortify. Resummon after slot 2 reuse clears and select the fresh proxy.",
            "Verify the replacement starts at 224 max HP, then Fortify it after reuse for 449. Select it again and use Drain Code (slot 4) while at least 50 IS below max. Capture one removal, the 58 s reuse and the separate +50 IS return. The headless replay also checks a stale prior pet request."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        var server = context.ActualServer!;
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        actor.SendRpc(CoderStyle);
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 f0 03 00 00");
        var old = await SummonAsync(actor);
        int castAt = actor.World.RpcSnapshot().Count;
        actor.SendRpc(StarterHackerControlTargetMode.WithHandle(FortifyRequest, Handle(old.Pet)));
        await EvidenceRetestWire.CastBarAsync(actor, Fortify, 2);
        int debugAt = server.DebugLog().Length;
        actor.SendRpc("81 7a fe 01"); // Stock Simulacra Deactivate command.
        await actor.WaitForRpcAsync($"81 77 {old.Id:x2}");
        await actor.WaitUntilAsync(() => server.DebugLog()[debugAt..].Contains("[PET] 20 fizzles: original pet"),
            "Pending Fortify does not survive removal of its original pet", HeadlessPlayer.DefaultTimeout);
        await EvidenceRetestWire.TimerAsync(actor, castAt, Fortify, AbilityCooldowns.OwnTimer, 8, ended: true);

        var fresh = await SummonAsync(actor);
        Check.That(Handle(old.Pet) != Handle(fresh.Pet) && old.Id != fresh.Id, "Replacement has a new target and registered ID");
        actor.SendRpc(StarterHackerControlTargetMode.WithHandle(DrainRequest, Handle(old.Pet)));
        await actor.WaitForTextAsync("That is not your simulacrum.");
        actor.SendRpc(StarterHackerControlTargetMode.WithHandle(FortifyRequest, Handle(fresh.Pet)));
        await actor.WaitForViewUpdateAsync(fresh.Pet.ViewId,
            WireFormat.ToHex(CoderPets.BuildHealthUpdate(224, 449)));

        int drainAt = actor.World.RpcSnapshot().Count;
        int viewsAt = actor.World.ViewSnapshot().Count;
        actor.SendRpc(StarterHackerControlTargetMode.WithHandle(DrainRequest, Handle(fresh.Pet)));
        await actor.WaitForRpcAsync($"81 77 {fresh.Id:x2}");
        await EvidenceRetestWire.TimerAsync(actor, drainAt, Drain, AbilityCooldowns.OwnTimer, 58);
        await actor.WaitUntilAsync(() =>
        {
            var snapshots = actor.World.ViewSnapshot();
            var before = snapshots.Take(viewsAt).SelectMany(AbilityParityCoderSweepMode.InnerStrengthUpdates).TakeLast(1);
            var after = snapshots.Skip(viewsAt).SelectMany(AbilityParityCoderSweepMode.InnerStrengthUpdates);
            var values = before.Concat(after).ToArray();
            return values.Zip(values.Skip(1), (prior, current) => current - prior).Contains(50);
        }, "Drain Code grants 50 IS after removing the current owned pet", HeadlessPlayer.DefaultTimeout);
        Check.Equal(0, actor.World.Problems.Count, "Simulacrum lifetime framing");
        return Array.Empty<byte>();
    }

    private static async Task<(ViewCreation Pet, byte Id)> SummonAsync(HeadlessPlayer actor)
    {
        int at = actor.World.CreationSnapshot().Count;
        int rpcAt = actor.World.RpcSnapshot().Count;
        actor.SkipReceived();
        actor.SendRpc(Summon);
        var register = await actor.WaitForRpcAsync("81 76", TimeSpan.FromSeconds(14));
        byte id = register.Bytes[3];
        ViewCreation? pet = null;
        await actor.WaitUntilAsync(() => (pet = actor.World.CreationSnapshot().Skip(at).FirstOrDefault(view =>
            view.Goid == 40376 && view.Attributes["SimulacraID"][0] == id)) != null,
            "New owned proxy publishes a live view", HeadlessPlayer.DefaultTimeout);
        Check.Equal(actor.CharId, BitConverter.ToUInt32(pet!.Attributes["OwnerCharacterID"]), "New proxy belongs to caster");
        Check.Equal((ushort)224, BitConverter.ToUInt16(pet.Attributes["MaxHealth"]), "New proxy starts at level-4 base health");
        await EvidenceRetestWire.TimerAsync(actor, rpcAt, Proxy, AbilityCooldowns.OwnTimer, 8);
        return (pet, id);
    }

    private static uint Handle(ViewCreation pet) => DuelPackets.Handle(pet.ViewId, pet.SpawnId);
}
