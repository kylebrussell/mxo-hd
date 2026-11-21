using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class StarterSimulacrumDismissMode : NativeQaMode
{
    private const int X = 24800, Y = 495, Z = -1498;
    private const byte Rank = 15;
    private const ushort Coder = 1008, Proxy = 27, Daemon = 22, Drain = 19;
    private static readonly ushort[] Abilities = StockAbilityProfile.Profile(2, 17, Coder, Proxy, Daemon, Drain).Order().ToArray();
    private static readonly int Memory = Abilities.Sum(AbilityMemory.Cost);

    // Stock captures PR510 CSEQ 54/58/135 and PR391 CSEQ 61. Only the Drain target handle is dynamic.
    private const string StockCoder = "80b9f003000000000000000000000000000000000000000000000000585cb40001000000086b0000000000";
    private const string StockProxy = "80b91b00000000000000000000000000000000000000000000000000585cb40001000000086b0000000000";
    private const string StockDaemon = "80b916000000000000000000000000000000000000000000000000000000000090767241086b0000000000";
    private const string StockDrain = "80b913000e000f000000000000000000000000000000000000000000585cb40001000000086b0000000000";
    private const string StockDeactivate = "81 7a fe 01";

    public override string Name => "ability-parity-starter-simulacrum-dismiss";

    public override NativeQaModePlan Plan { get; } = new(Abilities, X, Z, Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET level=LEAST(level,{Rank}) WHERE char_id=110" },
        new[]
        {
            $"UPDATE characters SET level={Rank},exp={PlayerProgression.ExperienceForLevel(Rank)},x={X},y={Y},z={Z}," +
                $"rotation={MobAggro.YawTowards(X,Z,X+150,Z)},innerStrC=500,innerStrM=1000 WHERE charId=110"
        }, DevCommands: true, hotbarAbilities: new[] { Coder, Proxy, Daemon, Drain },
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        extraQaMetadata: new() { ["ScopedStockRows"] = "22,27,19", ["NativeCloseableRows"] = 2, ["MemoryUsed"] = Memory },
        readyMessages: new[]
        {
            $"Rank {Rank}, {Memory}/{AbilityMemory.ForLevel(Rank)} memory. Stay on the outdoor stand facing the pet spawn 1.5 m east. Leave world selection clear for summons. Page 2 slot 1 selects Coder Style.",
            "Slot 2: Remote Proxy 1.0 (27). Expect 8 s cast, 50 IS, and 8 s own reuse. Its level-4 pet has 224 max HP and a new Simulacra window. Its appearance varies by combat sheet.",
            "While the proxy is still alive, use Logic Daemon 1.0 (22) in slot 3. Expect 8 s cast, 50 IS, and 10 s own reuse. Confirm the old proxy window and view clear before the level-7 Nuker with 337 max HP becomes the only registered pet.",
            "Use Deactivate in the Daemon's Simulacra window. Expect an 81 7a order and one removal. Summon another proxy with slot 2. Select the fresh pet, then use Drain Code (19) in slot 4 with at least 50 IS below cap. Expect zero activation IS, one pet removed, +50 IS and 58 s reuse. Enter submits commands without Escape."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        var loaded = AbilityMemoryUiFixture.Decode(actor.CharacterData[6])
            .Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor), row => row.Level);
        Check.That(loaded.Keys.ToHashSet().SetEquals(Abilities) &&
            Abilities.All(id => AbilityPrerequisites.Current.Closure(id).All(required =>
                loaded.TryGetValue(required.Key, out ushort rank) && rank >= required.Value)),
            "Margin sends the complete prerequisite profile for the pet batch");
        Check.That(Memory <= AbilityMemory.ForLevel(Rank), "The loaded pet profile fits at rank 15");

        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, actor);
        actor.SendRpc(StockCoder);
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 f0 03 00 00");

        var first = await SummonAsync(Proxy, StockProxy, 4, 224, 8);
        int swapAt = actor.World.RpcSnapshot().Count;
        int swapViews = actor.World.ViewSnapshot().Count;
        var daemon = await SummonAsync(Daemon, StockDaemon, 7, 337, 10);
        Check.Equal("Nuker Simulacrum", daemon.Pet.Name, "Logic Daemon 1.0 creates a Nuker");
        var swap = actor.World.RpcSnapshot().Skip(swapAt).ToList();
        int oldRemoved = swap.FindIndex(packet => IsWindowRemoval(packet, first.Id));
        int newRegistered = swap.FindIndex(packet => IsWindowRegistration(packet, daemon.Id));
        Check.That(oldRemoved >= 0 && newRegistered > oldRemoved, "The older window closes before the new daemon registers");
        await ViewDeletedAsync(first.Pet, swapViews);

        int dismissal = actor.World.RpcSnapshot().Count;
        int dismissalViews = actor.World.ViewSnapshot().Count;
        actor.SendRpc(StockDeactivate);
        await StockAbilityProfile.ObserveAsync(actor, dismissal, packet => IsWindowRemoval(packet, daemon.Id),
            "The stock Simulacra action deactivates the current daemon");
        await ViewDeletedAsync(daemon.Pet, dismissalViews);

        var fresh = await SummonAsync(Proxy, StockProxy, 4, 224, 8);
        Check.That(first.Id != fresh.Id && DuelPackets.Handle(first.Pet.ViewId, first.Pet.SpawnId) !=
            DuelPackets.Handle(fresh.Pet.ViewId, fresh.Pet.SpawnId), "The new proxy has a fresh view and window identity");

        int drainAt = actor.World.RpcSnapshot().Count;
        actor.SendRpc(StarterHackerControlTargetMode.WithHandle(StockDrain,
            DuelPackets.Handle(first.Pet.ViewId, first.Pet.SpawnId)));
        await actor.WaitForTextAsync("That is not your simulacrum.");
        Check.That(!actor.World.RpcSnapshot().Skip(drainAt).Any(packet => IsWindowRemoval(packet, fresh.Id)),
            "A request on the old proxy leaves the new pet alone");

        await Task.Delay(800); // Two requests for the same ability must clear the normal repeat guard.
        int drainViews = actor.World.ViewSnapshot().Count;
        actor.SendRpc(StarterHackerControlTargetMode.WithHandle(StockDrain,
            DuelPackets.Handle(fresh.Pet.ViewId, fresh.Pet.SpawnId)));
        await StockAbilityProfile.ObserveAsync(actor, drainAt, packet => IsWindowRemoval(packet, fresh.Id),
            "Drain Code removes the selected live proxy window");
        await EvidenceRetestWire.TimerAsync(actor, drainAt, Drain, AbilityCooldowns.OwnTimer, 58);
        await WaitForIsDeltaAsync(drainViews, 50);
        await ViewDeletedAsync(fresh.Pet, drainViews);
        Check.Equal(0, actor.World.Problems.Count, "Simulacrum lifecycle packet framing");
        return Array.Empty<byte>();

        async Task<(ViewCreation Pet, byte Id)> SummonAsync(ushort id, string request, byte level, ushort hp, float reuse)
        {
            int rpcAt = actor.World.RpcSnapshot().Count;
            int viewAt = actor.World.ViewSnapshot().Count;
            int creationAt = actor.World.CreationSnapshot().Count;
            actor.SendRpc(request);
            await EvidenceRetestWire.CastBarAsync(actor, id, 8);
            var registration = await StockAbilityProfile.ObserveAsync(actor, rpcAt, packet => packet.Bytes is [0x81, 0x76, ..],
                $"Summon {id} registers a new Simulacra window");
            byte simId = registration.Bytes[3];
            Check.Equal(46, registration.Bytes.Length, "Stock Simulacra window record length");
            ViewCreation? pet = null;
            await actor.WaitUntilAsync(() => (pet = actor.World.CreationSnapshot().Skip(creationAt).FirstOrDefault(view =>
                view.Goid == 40376 && view.Attributes["SimulacraID"][0] == simId)) != null,
                $"Summon {id} creates one new owned view", HeadlessPlayer.DefaultTimeout);
            Check.Equal(actor.CharId, BitConverter.ToUInt32(pet!.Attributes["OwnerCharacterID"]), "The pet belongs to CSR");
            Check.Equal(actor.CharId, BitConverter.ToUInt32(pet.Attributes["MasterCharacterID"]), "The pet master is CSR");
            Check.Equal(level, pet.Attributes["Level"][0], "Pet uses the shipped subject-level clamp");
            Check.Equal(hp, BitConverter.ToUInt16(pet.Attributes["MaxHealth"]), "Pet uses the expected starter health");
            await EvidenceRetestWire.TimerAsync(actor, rpcAt, id, AbilityCooldowns.OwnTimer, reuse);
            await WaitForIsDeltaAsync(viewAt, -50);
            return (pet, simId);
        }

        async Task ViewDeletedAsync(ViewCreation view, int cursor)
        {
            byte[] deletion = WireFormat.FromHex($"01 00 01 01 00 {StockAbilityProfile.LittleEndianHex(view.ViewId)}");
            await actor.WaitUntilAsync(() => actor.World.ViewSnapshot().Skip(cursor).Any(list => list.AsSpan().IndexOf(deletion) >= 0),
                $"Retired pet view {view.ViewId} is deleted", HeadlessPlayer.DefaultTimeout);
        }

        async Task WaitForIsDeltaAsync(int cursor, int delta)
        {
            ushort start = BitConverter.ToUInt16(actor.World.CreationSnapshot().First(view => view.ViewId == 2)
                .Attributes["InnerStrengthAvailable"]);
            await actor.WaitUntilAsync(() =>
            {
                var snapshots = actor.World.ViewSnapshot();
                var previous = snapshots.Take(cursor).SelectMany(AbilityParityCoderSweepMode.InnerStrengthUpdates).LastOrDefault(start);
                var updates = snapshots.Skip(cursor).SelectMany(AbilityParityCoderSweepMode.InnerStrengthUpdates).Prepend(previous).ToArray();
                return updates.Zip(updates.Skip(1), (before, after) => after - before).Contains(delta);
            }, $"Owner receives an independent {delta} IS delta", HeadlessPlayer.DefaultTimeout);
        }
    }

    private static bool IsWindowRemoval(RpcRecord packet, byte id) => packet.Bytes is [0x81, 0x77, var current, ..] && current == id;

    private static bool IsWindowRegistration(RpcRecord packet, byte id) =>
        packet.Bytes is [0x81, 0x76, _, var current, ..] && current == id;
}

