using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class StarterRemoteProxy2ReplacementMode : NativeQaMode
{
    private const int Level = 5, X = 17000, Y = 495, Z = 6400, PetX = X + 150, ObserverX = X + 400;
    private const ushort Coder = 1008, Proxy1 = 27, Proxy2 = 28;
    private static readonly ushort[] Hotbar = { Coder, Proxy2, Proxy1 };
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, Coder, Proxy1, Proxy2).Order().ToArray();

    // PR593 and PR603 full stock casts. PR593's /ordersimulacra All Deactivate sends 81 7a fe 01.
    private const string Proxy1Wire = "80b91b00000000000000000000000000000000000000000000000000585cb40001000000086b0000000000";
    private const string Proxy2Wire = "80b91c00000000000000000000000000000000000000000000000000585ca80001000000086b0000000000";
    private const string Deactivate = "81 7a fe 01";

    public override string Name => "ability-parity-starter-remote-proxy2-replacement";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110" },
        new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Z}," +
                $"rotation={MobAggro.YawTowards(X,Z,PetX,Z)},innerStrC=350,innerStrM=1000 WHERE charId=110",
            $"UPDATE characters SET x={ObserverX},y={Y},z={Z} WHERE charId=6111"
        }, DevCommands: true, hotbarAbilities: Hotbar, environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "28", ["NativeCloseableRows"] = 1, ["MemoryUsed"] = Loadout.Sum(AbilityMemory.Cost) },
        readyMessages: new[]
        {
            $"Level {Level}, full {Loadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} ranked profile. Verified outdoor NAV: CSR ({X},{Y},{Z}) faces the summon ({PetX},{Y},{Z}); QAFriend observes from ({ObserverX},{Y},{Z}). Page 2 Coder Style/Remote Proxy 2.0/Remote Proxy 1.0.",
            "With selection clear, choose Coder Style and press slot 2. Expect [PET] Remote Proxy 2.0 prepared simulacrum 1; previous 0, one L5/262 HP proxy and its window, 350 to 300 IS, 8 s cast/reuse. Type /ordersimulacra All Deactivate. Capture 81 7a fe 01 and one 81 77 01. The proxy and its window disappear.",
            "Press slot 3 for the L4/224 HP proxy, 300 to 250 IS. When it is visible and slot 2 is ready, press slot 2 again. The old proxy remains through the 8 s bar. At landing, [PET] Remote Proxy 2.0 prepared simulacrum 3; previous 2 precedes removal of the old proxy. Expect 81 77 02 then one 81 76 for 3, L5/262 HP, and 250 to 200 IS. Only the new proxy remains.",
            "Use /ordersimulacra All Deactivate again. Expect one 81 77 03 and the replacement body and window disappear. A passing stock run closes row 28 for the shipped callback interpretation. Projected DONE +1."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Proxy replacement needs repository NAV data.");
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (int x in new[] { X, PetX, ObserverX })
            Check.That(NavMesh.Locate(1, x, Y, Z, out var at) && !at.indoors && Math.Abs(at.floorY + 95 - Y) < 1 &&
                NavMesh.HasSightPath(1, x, Y, Z, PetX, Y, Z), $"Verified clear outdoor stand ({x},{Y},{Z})");
        var server = context.ActualServer!;
        var watcher = await HeadlessPlayer.LoginAsync(context.Address, "QAFriend", "QAFriend", stockCharacterLoad: true);
        context.Helpers.Add(watcher.Handle, watcher);
        await watcher.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, watcher);
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        var rows = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level) && rows.Keys.ToHashSet().SetEquals(Loadout) &&
            Loadout.All(id => AbilityPrerequisites.Current.Closure(id).All(need =>
                rows.TryGetValue(need.Key, out var row) && row.Level >= need.Value)), "Full ranked starter profile");
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        actor.SendRpc(StarterCoderPetBuffsAndDetonateMode.CoderStock);
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 f0 03 00 00");

        var empty = await SummonAsync(Proxy2, Proxy2Wire, 5, 262, 300, null);
        await DismissAsync(empty);
        var old = await SummonAsync(Proxy1, Proxy1Wire, 4, 224, 250, null);
        var replacement = await SummonAsync(Proxy2, Proxy2Wire, 5, 262, 200, old);
        await DismissAsync(replacement);
        Check.Equal(0, actor.World.Problems.Count + watcher.World.Problems.Count, "Summon, replacement and deactivation wire framing");
        Console.WriteLine("REMOTE PROXY 2 CALLBACK PASS: empty-slot cast, old-only replacement, and both stock Deactivate orders.");
        return Array.Empty<byte>();

        async Task<(byte Id, ViewCreation Own, ViewCreation Remote)> SummonAsync(ushort ability, string request,
            byte rank, ushort maxHp, int after, (byte Id, ViewCreation Own, ViewCreation Remote)? oldPet)
        {
            int rpc = actor.World.RpcSnapshot().Count;
            int ownView = actor.World.ViewSnapshot().Count, remoteView = watcher.World.ViewSnapshot().Count;
            int ownCreation = actor.World.CreationSnapshot().Count, remoteCreation = watcher.World.CreationSnapshot().Count;
            int log = server.DebugLog().Length;

            actor.SkipReceived();
            actor.SendRpc(request);
            await EvidenceRetestWire.CastBarAsync(actor, ability, 8);
            Check.That(!actor.World.RpcSnapshot().Skip(rpc).Any(Removed), "Starting the cast keeps the old window");
            await EvidenceRetestWire.TimerAsync(actor, rpc, ability, AbilityCooldowns.OwnTimer, 8);
            var registration = await StockAbilityProfile.ObserveAsync(actor, rpc, frame => frame.Opcode == 0x176, "New pet window");
            byte id = registration.Bytes[3];
            var own = await ViewAsync(actor, ownCreation, id);
            var remote = await ViewAsync(watcher, remoteCreation, id);
            Check.Equal(rank, own.Attributes["Level"][0], "Expected rank");
            Check.Equal(maxHp, BitConverter.ToUInt16(own.Attributes["MaxHealth"]), "Expected maximum HP");
            Check.That(WorldNpcCornerFixture.Distance(EngageFoesFixture.Position(own), new double[] { PetX, Y, Z }) < 1,
                "Summon is at the marked NAV stand");
            await actor.WaitUntilAsync(() => actor.World.ViewSnapshot().Skip(ownView)
                .SelectMany(AbilityParityCoderSweepMode.InnerStrengthUpdates).Any(isValue => isValue == after),
                $"CSR has {after} IS", HeadlessPlayer.DefaultTimeout);

            if (oldPet is { } prior)
            {
                await DeletedAsync(actor, ownView, prior.Own.ViewId);
                await DeletedAsync(watcher, remoteView, prior.Remote.ViewId);
            }
            var windows = actor.World.RpcSnapshot().Skip(rpc).Where(frame => frame.Opcode is 0x176 or 0x177)
                .Select(frame => (frame.Opcode, Id: frame.Bytes[frame.Opcode == 0x176 ? 3 : 2])).ToArray();
            Check.That(oldPet is { } previous
                ? windows.SequenceEqual(new[] { (0x177, previous.Id), (0x176, id) }) && previous.Id != id
                : windows.SequenceEqual(new[] { (0x176, id) }), "Only the previous ID is retired, before the new window opens");

            if (ability == Proxy2)
            {
                string publication = $"ability {ability}, simulacrum {id}";
                await actor.WaitUntilAsync(() => server.DebugLog()[log..].Contains(publication),
                    "The new proxy is published", HeadlessPlayer.DefaultTimeout);
                string changes = server.DebugLog()[log..];
                int prepared = changes.IndexOf($"[PET] Remote Proxy 2.0 prepared simulacrum {id}; previous {oldPet?.Id ?? 0}", StringComparison.Ordinal);
                int installed = changes.IndexOf(publication, StringComparison.Ordinal);
                int retired = oldPet is { } p ? changes.IndexOf($"[PET] {p.Own.Name} of {actor.Handle} removed", StringComparison.Ordinal) : prepared;
                Check.That(prepared >= 0 && prepared <= retired && retired < installed, "Prepared candidate, previous retirement, then publication");
            }
            return (id, own, remote);
        }

        async Task DismissAsync((byte Id, ViewCreation Own, ViewCreation Remote) pet)
        {
            int rpc = actor.World.RpcSnapshot().Count;
            int ownView = actor.World.ViewSnapshot().Count, remoteView = watcher.World.ViewSnapshot().Count;
            actor.SendRpc(Deactivate);
            await StockAbilityProfile.ObserveAsync(actor, rpc, frame => Removed(frame) && frame.Bytes[2] == pet.Id,
                "Stock Deactivate removes the current proxy");
            await DeletedAsync(actor, ownView, pet.Own.ViewId);
            await DeletedAsync(watcher, remoteView, pet.Remote.ViewId);
            Check.That(actor.World.RpcSnapshot().Skip(rpc).Count(Removed) == 1 &&
                !actor.World.RpcSnapshot().Skip(rpc).Any(frame => frame.Opcode == 0x176), "Deactivate retires exactly one pet window");
        }

        async Task<ViewCreation> ViewAsync(HeadlessPlayer player, int cursor, byte id)
        {
            ViewCreation? view = null;
            await player.WaitUntilAsync(() => (view = player.World.CreationSnapshot().Skip(cursor).FirstOrDefault(row =>
                row.Goid == CoderPets.SimulacraGoId && row.Attributes["SimulacraID"][0] == id &&
                BitConverter.ToUInt32(row.Attributes["OwnerCharacterID"]) == actor.CharId)) != null,
                $"Owned simulacrum {id} visible to {player.Handle}", HeadlessPlayer.DefaultTimeout);
            return view!;
        }
    }

    private static bool Removed(RpcRecord frame) => frame.Bytes is [0x81, 0x77, _];

    private static Task DeletedAsync(HeadlessPlayer player, int cursor, ushort view)
    {
        byte[] deletion = WireFormat.FromHex($"01 00 01 01 00 {StockAbilityProfile.LittleEndianHex(view)}");
        return player.WaitUntilAsync(() => player.World.ViewSnapshot().Skip(cursor).Any(section => section.AsSpan().IndexOf(deletion) >= 0),
            $"Pet view {view} removed for {player.Handle}", HeadlessPlayer.DefaultTimeout);
    }
}
