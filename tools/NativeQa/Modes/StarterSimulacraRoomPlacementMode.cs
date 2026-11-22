using System.Text.Json;
using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class StarterSimulacraRoomPlacementMode : NativeQaMode
{
    private const int Level = 18, StreetX = 17000, StreetY = 495, StreetZ = 2300;
    private const int RoomX = 18533, RoomY = 1695, RoomZ = 3170;
    private const double RoomPetX = 18639.066, RoomPetZ = 3276.066;
    private const ushort Coder = 1008, Proxy = 27, SecondProxy = 28, Daemon = 22;
    private const string RoomCommand = "?tp 18533 1695 3170 393 19 36 - 20";
    private const string StreetCommand = "?tp 17000 495 2300 393 11 3 - 40";
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, Coder, Proxy, SecondProxy, Daemon).Order().ToArray();
    private static readonly int Memory = Loadout.Sum(AbilityMemory.Cost);
    // Full stock summons from PR593 (27 and 22) and PR603 (28).
    private const string ProxyWire = "80b91b00000000000000000000000000000000000000000000000000585cb40001000000086b0000000000";
    private const string SecondProxyWire = "80b91c00000000000000000000000000000000000000000000000000585ca80001000000086b0000000000";
    private const string DaemonWire = "80b916000000000000000000000000000000000000000000000000000090767241086b0000000000";

    public override string Name => "ability-parity-starter-simulacra-room-placement";
    public override NativeQaModePlan Plan { get; } = new(Loadout, StreetX, StreetZ, Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110" },
        new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)}," +
                $"x={StreetX},y={StreetY},z={StreetZ},rotation=64,innerStrC=350,innerStrM=1000 WHERE charId=110"
        }, DevCommands: true, hotbarAbilities: new[] { Coder, Proxy, SecondProxy, Daemon }, environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "22,27,28", ["NativeCloseableRows"] = 0, ["MemoryUsed"] = Memory },
        readyMessages: new[]
        {
            $"Level {Level} with all prerequisites, {Memory}/{AbilityMemory.ForLevel(Level)} memory. Page 2 slots 1–4 are Coder Style, 27, 28, 22. No bag or helper. Start clear of WinterFrost at ({StreetX},{StreetY},{StreetZ}), facing the expected open-ground pet at (17150,495,2300). Leave the target clear.",
            "Select Coder Style and summon slot 2. Expect a visible L4 Proxy, 224 max HP, one window, 8 s cast and reuse, IS 350 to 300. [PET] summoned ability 27 at (17150,495,2300).",
            $"Type {RoomCommand} and press Enter. This is the verified Mara room on floor 1600. The old (18683,1695,3170) is off NAV. Leave selection clear and use slot 3. The new L7 Proxy has 337 max HP and should be visible diagonally ahead on the same floor at (18639.07,1695,3276.07). The previous window closes. Cast and reuse 8 s, IS 300 to 250.",
            "Stay on the room stand and use slot 4. The L7/337 HP Nuker should replace the Proxy at the same clear stand, with one pet and one window. Cast 8 s, reuse 10 s, IS 250 to 200. Read the exact spawn in the [PET] summoned line for both room summons.",
            $"Type {StreetCommand}, press Enter and use slot 2 again. Expect L4/224 HP at (17150,495,2300), the old window closed, 8 s cast and reuse and IS 200 to 150. Owner max [VITALS] and [PVP] rounds should stay unchanged. Projected DONE delta 0."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Pet room placement needs repository NAV data.");
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        Check.That(NavMesh.Locate(1, StreetX, StreetY, StreetZ, out var street) && !street.indoors &&
            street.sector == 0x393 && street.region == 0x11 && street.polygon == 3 &&
            NavMesh.Locate(1, StreetX + 150, StreetY, StreetZ, out var streetPet) && !streetPet.indoors &&
            street.floorY == streetPet.floorY, "Street origin and legacy stand are on the same outdoor floor");
        Check.That(NavMesh.Locate(1, RoomX, RoomY, RoomZ, out var room) && room.indoors &&
            room.sector == 0x393 && room.region == 0x19 && room.polygon == 0x36 &&
            !NavMesh.Locate(1, RoomX + 150, RoomY, RoomZ, out _), "The room owner stand is valid and the old offset is off-floor");
        Check.That(NavMesh.Locate(1, RoomPetX, RoomY, RoomPetZ, out var roomPet) &&
            (roomPet.sector, roomPet.region, roomPet.polygon, roomPet.floorY) == (room.sector, room.region, (ushort)0x48, room.floorY) &&
            NavMesh.HasSightPath(1, RoomX, RoomY, RoomZ, RoomPetX, RoomY, RoomPetZ), "The alternate stand is visible on the same indoor floor");

        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        var server = context.ActualServer!;
        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor), row => row.Level);
        Check.That(Memory <= AbilityMemory.ForLevel(Level) && archive.Keys.ToHashSet().SetEquals(Loadout) &&
            Loadout.All(id => AbilityPrerequisites.Current.Closure(id).All(need =>
                archive.TryGetValue(need.Key, out ushort rank) && rank >= need.Value)), "The full level 18 profile fits");
        await actor.EnterWorldAsync();
        actor.SendRpc(StarterCoderPetBuffsAndDetonateMode.CoderStock);
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 f0 03 00 00");

        byte previousWindow = 0;
        var evidence = new List<object>();
        await SummonAsync(Proxy, ProxyWire, 4, 224, 8, 300, StreetX + 150, StreetY, StreetZ);
        await TeleportAsync(RoomCommand, "393/19");
        await SummonAsync(SecondProxy, SecondProxyWire, 7, 337, 8, 250, RoomPetX, RoomY, RoomPetZ);
        await SummonAsync(Daemon, DaemonWire, 7, 337, 10, 200, RoomPetX, RoomY, RoomPetZ);
        await TeleportAsync(StreetCommand, "393/11");
        await SummonAsync(Proxy, ProxyWire, 4, 224, 8, 150, StreetX + 150, StreetY, StreetZ);
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        Check.Equal(0, actor.World.Problems.Count, "Placement replay framing");
        await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "simulacra-room-placement.json"), JsonSerializer.Serialize(evidence));
        Console.WriteLine("SIMULACRA ROOM PLACEMENT PASS: both open-ground offsets, both same-room stands and all replacement windows.");
        return Array.Empty<byte>();

        async Task TeleportAsync(string command, string nav)
        {
            actor.SendChat(command);
            await actor.WaitForTextAsync("Teleported to " + nav);
        }

        async Task SummonAsync(ushort id, string frame, byte level, ushort hp, int reuse, int isAfter, double x, double y, double z)
        {
            int rpcFrom = actor.World.RpcSnapshot().Count;
            int viewsFrom = actor.World.ViewSnapshot().Count;
            actor.SkipReceived();
            actor.SendRpc(frame);
            await EvidenceRetestWire.CastBarAsync(actor, id, 8);
            await EvidenceRetestWire.TimerAsync(actor, rpcFrom, id, AbilityCooldowns.OwnTimer, reuse);
            var window = await StockAbilityProfile.ObserveAsync(actor, rpcFrom, packet => packet.Opcode == 0x176, "A new pet window");
            byte currentWindow = window.Bytes[3];
            if (previousWindow != 0)
            {
                Check.That(currentWindow != previousWindow, "Each summon gets a fresh ID");
                await StockAbilityProfile.ObserveAsync(actor, rpcFrom, packet => packet.Bytes is [0x81, 0x77, var removed] &&
                    removed == previousWindow, "The preceding window closes");
            }
            ViewCreation? pet = null;
            await actor.WaitUntilAsync(() => (pet = actor.World.CreationSnapshot().LastOrDefault(view =>
                view.Goid == CoderPets.SimulacraGoId && view.Attributes["SimulacraID"][0] == currentWindow)) != null,
                "The replacement pet is visible", HeadlessPlayer.DefaultTimeout);
            Check.That(pet!.Attributes["Level"][0] == level && BitConverter.ToUInt16(pet.Attributes["MaxHealth"]) == hp,
                $"Summon {id} retains level {level} and HP {hp}");
            double[] position = EngageFoesFixture.Position(pet);
            Check.That(position.Zip(new[] { x, y, z }).All(pair => Math.Abs(pair.First - pair.Second) < 0.1),
                $"Summon {id} at ({string.Join(",", position)}) matches its verified stand");
            await actor.WaitUntilAsync(() => actor.World.ViewSnapshot().Skip(viewsFrom).SelectMany(AbilityParityCoderSweepMode.InnerStrengthUpdates)
                .Any(value => value == isAfter), $"Summon {id} leaves {isAfter} IS", HeadlessPlayer.DefaultTimeout);
            Check.That(actor.World.RpcSnapshot().Skip(rpcFrom).Where(packet => packet.Opcode == 0x177)
                .All(packet => packet.Bytes[2] == previousWindow), "Only the preceding pet window may close");
            evidence.Add(new { Ability = id, Position = position, Window = currentWindow, PriorWindow = previousWindow, IsAfter = isAfter });
            previousWindow = currentWindow;
        }
    }
}
