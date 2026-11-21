using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class StarterSimulacrumDismissMode : NativeQaMode
{
    private const int X = 24800, Y = 495, Z = -1498;
    private const byte Rank = 15;
    private const ushort StartingIs = 500, MaxIs = 1000;
    private const ushort Coder = 1008, Proxy = 27, Daemon = 22, Drain = 19;
    private static readonly ushort[] Abilities = StockAbilityProfile.Profile(2, 17, Coder, Proxy, Daemon, Drain).Order().ToArray();
    private static readonly int Memory = Abilities.Sum(AbilityMemory.Cost);

    // Stock captures PR510 CSEQ 54/58/135 and PR391 CSEQ 61. Only the Drain target handle is dynamic.
    private const string StockCoder = "80b9f003000000000000000000000000000000000000000000000000585cb40001000000086b0000000000";
    private const string StockProxy = "80b91b00000000000000000000000000000000000000000000000000585cb40001000000086b0000000000";
    private const string StockDaemon = "80b916000000000000000000000000000000000000000000000000000000000090767241086b0000000000";
    private const string StockDrain = "80b913000e000f000000000000000000000000000000000000000000585cb40001000000086b0000000000";
    // client.dll 62257b5a slash parser: All resolves to fe, then the Deactivate token selects command 1.
    // PR440 d148595f0aa3 lines 1652/1940 also capture these same bytes from the pet window.
    private const string StockDeactivate = "81 7a fe 01";

    public override string Name => "ability-parity-starter-simulacrum-dismiss";

    public override NativeQaModePlan Plan { get; } = new(Abilities, X, Z, Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET level=LEAST(level,{Rank}) WHERE char_id=110" },
        new[]
        {
            $"UPDATE characters SET level={Rank},exp={PlayerProgression.ExperienceForLevel(Rank)},x={X},y={Y},z={Z}," +
                $"rotation={MobAggro.YawTowards(X,Z,X+150,Z)},innerStrC={StartingIs},innerStrM={MaxIs} WHERE charId=110"
        }, DevCommands: true, hotbarAbilities: new[] { Coder, Proxy, Daemon, Drain },
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1"
        },
        extraQaMetadata: new() { ["ScopedStockRows"] = "22,27,19", ["NativeCloseableRows"] = 0, ["MemoryUsed"] = Memory },
        readyMessages: new[]
        {
            $"Rank {Rank}, {Memory}/{AbilityMemory.ForLevel(Rank)} memory. Retest Deactivate and Drain only. Stay on the outdoor stand. Start at {StartingIs}/{MaxIs} IS with natural IS regeneration held in this fixture.",
            "Select Coder Style (page 2 slot 1), clear world selection and use Logic Daemon (slot 3). Once the level-7 Nuker window appears, enter /ordersimulacra All Deactivate in stock chat. Expect 81 7a fe 01, one removal, and the window and world view to disappear.",
            "If All refuses locally, try /ordersimulacra 1 Deactivate using the actual current simulacrum number (1 if this is the first summon). If neither sends an order, record the refusal. You may use ?pet dismiss to continue the Drain check without crediting Deactivate.",
            "With no pet and clear selection, summon Remote Proxy (slot 2) and select its fresh body 1.5 m east. Use Drain Code (slot 4). Expect one removal, 58 s reuse, and CSR IS 400 to 450 after the two 50 IS summons. Enter submits commands without Escape."
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
        Check.Equal(StartingIs, ReadIs(), "The disposable CSR starts at 500 IS");
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
        Check.Equal((ushort)(StartingIs - 150), ReadIs(),
            "Three test summons cost 150 IS with no regeneration during their cast bars");
        actor.SendRpc(StarterHackerControlTargetMode.WithHandle(StockDrain,
            DuelPackets.Handle(fresh.Pet.ViewId, fresh.Pet.SpawnId)));
        await StockAbilityProfile.ObserveAsync(actor, drainAt, packet => IsWindowRemoval(packet, fresh.Id),
            "Drain Code removes the selected live proxy window");
        await EvidenceRetestWire.TimerAsync(actor, drainAt, Drain, AbilityCooldowns.OwnTimer, 58);
        await WaitForIsDeltaAsync(drainViews, 50);
        Check.Equal((ushort)(StartingIs - 100), ReadIs(), "Drain restores the entire 50 IS to CSR");
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

        ushort ReadIs() => actor.World.ViewSnapshot().SelectMany(AbilityParityCoderSweepMode.InnerStrengthUpdates)
            .LastOrDefault(BitConverter.ToUInt16(actor.World.CreationSnapshot().First(view => view.ViewId == 2)
                .Attributes["InnerStrengthAvailable"]));

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

