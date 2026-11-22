using System.Text.Json;
using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class StarterSimulacraCornerRoutingMode : NativeQaMode
{
    private const int Level = 18;
    private const ushort Coder = 1008, Proxy = 27, RifleProxy = 28, Nuker = 22;
    private const string NearCommand = "?tp 18924 95 -8852 460 0 1 - 40";
    private const string FarCommand = "?tp 20218 95 -9510 18 8 b - 8f";
    private const string OpenCommand = "?tp 19930 95 -9530 18 8 5 - 64";
    private const string FloorCommand = "?tp 18533 1695 3170 393 19 36 - 20";
    private const string TargetName = "QA Pet Route Target";
    private static readonly double[] Near = { 18924, 95, -8852 }, NearPet = { 19074, 95, -8852 };
    private static readonly double[] Far = { 20218, 95, -9510 }, Open = { 19930, 95, -9530 }, OpenPet = { 20080, 95, -9530 };
    private static readonly double[] Room = { 18533, 1695, 3170 }, AttackTarget = { 20120, 95, -9760 };
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, Coder, Proxy, RifleProxy, Nuker).Order().ToArray();
    private static readonly int Memory = Loadout.Sum(AbilityMemory.Cost);
    // Complete stock summons from PR593 (27 and 22) and PR603 (28).
    private const string ProxyWire = "80b91b00000000000000000000000000000000000000000000000000585cb40001000000086b0000000000";
    private const string RifleWire = "80b91c00000000000000000000000000000000000000000000000000585ca80001000000086b0000000000";
    private const string NukerWire = "80b916000000000000000000000000000000000000000000000000000090767241086b0000000000";

    public override string Name => "ability-parity-starter-simulacra-corner-routing";
    public override NativeQaModePlan Plan { get; } = new(Loadout, (int)Near[0], (int)Near[2], Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110" },
        new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)}," +
                $"x={Near[0]},y={Near[1]},z={Near[2]},rotation=64,innerStrC=300,innerStrM=1000 WHERE charId=110"
        }, DevCommands: true, hotbarAbilities: new[] { Coder, Proxy, RifleProxy, Nuker }, testMobFixture: "starter-simulacra-corner-routing.csv",
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1",
            [CoderPets.RifleProxyQa] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "22,27,28", ["NativeCloseableRows"] = 0, ["MemoryUsed"] = Memory },
        readyMessages: new[]
        {
            $"Level {Level}, full {Memory}/{AbilityMemory.ForLevel(Level)} memory. Page 2 is Coder Style/27/28/22. CSR starts on verified outdoor NAV at (18924,95,-8852), facing the pet spawn at (19074,95,-8852). The fixture is clear of WinterFrost. The Mara corner blocks a direct path to (20218,95,-9510).",
            $"Use slots 1 and 2 to summon 27; HP 224, IS 300 to 250. Type ?pet stay, then {FarCommand}. Wait three seconds. The pet stays at its original mark. Type ?pet follow and watch the same pet reach within 2.5 m from around the corner. If it arrives outside the camera, turn toward the west corridor or strafe. Save the 02 0e position updates; each step is on floor 0 and follows the open path.",
            $"With Follow still active, type {FloorCommand}. Wait three seconds, then type {FarCommand} again. The pet holds at its last grounded spot on floor 0 while CSR is on the mapped upper floor 1600, and the same pet window persists.",
            $"Type {OpenCommand}. Clear selection and use slot 3 for the L7/337 HP Rifle Proxy at (20080,95,-9530), IS 250 to 200. Single-click QA Pet Route Target (20120,95,-9760) and confirm its name. Press Attack in the Simulacra window. Expect a normal [PET] Rifle Simulacrum hit from the clear spawn without moving or starting owner combat. Type ?pet stay.",
            $"Type {NearCommand}; clear selection and use slot 4 for L7/337 HP Nuker, IS 200 to 150. Type ?pet stay before {FarCommand}. Select QA Pet Route Target from this visible stand, then press stock Simulacra Attack. Capture the full 81 7b and selected view. The Nuker starts within 15 m but waits to hit until it takes a walkable step around the corner and gains clear sight. Use ?pet stay after a hit. All three casts take 8 s. Own reuse 8/8/10 s. Projected DONE delta 0."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Pet routing needs repository NAV data.");
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (double[] point in new[] { Near, NearPet, Far, Open, OpenPet, AttackTarget, Room })
            Check.That(NavMesh.Locate(1, point[0], point[1], point[2], out var at) && at.indoors == ReferenceEquals(point, Room) &&
                Math.Abs(at.floorY + 95 - point[1]) < 1, $"Verified stand ({string.Join(",", point)})");
        Check.That(!Clear(NearPet, Far) && !Clear(NearPet, AttackTarget) && !Clear(Far, Room) &&
            Clear(Open, AttackTarget) && Clear(OpenPet, AttackTarget) && WorldNpcCornerFixture.Distance(NearPet, AttackTarget) < 1500,
            "Blocked corner and floor, clear rifle control, and the Nuker starts inside its attack radius");

        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        var server = context.ActualServer!;
        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor), row => row.Level);
        Check.That(Memory <= AbilityMemory.ForLevel(Level) && archive.Keys.ToHashSet().SetEquals(Loadout) &&
            Loadout.All(id => AbilityPrerequisites.Current.Closure(id).All(need =>
                archive.TryGetValue(need.Key, out ushort rank) && rank >= need.Value)), "The full ranked profile fits");
        await actor.EnterWorldAsync();
        actor.SendRpc(StarterCoderPetBuffsAndDetonateMode.CoderStock);
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 f0 03 00 00");

        byte priorWindow = 0;
        var proxy = await SummonAsync(Proxy, ProxyWire, 4, 224, 8, 250, NearPet);
        await StayAsync();
        int stationaryAt = actor.World.ViewSnapshot().Count;
        await TeleportAsync(FarCommand, "18/8");
        await Task.Delay(TimeSpan.FromSeconds(3));
        Check.That(Trail(proxy, stationaryAt).All(p => SamePosition(p, NearPet)), "Stay leaves the original pet in place");
        int followAt = actor.World.ViewSnapshot().Count;
        actor.SendRpc("81 7a fe 03");
        await actor.WaitUntilAsync(() => Trail(proxy, followAt).Any(p => WorldNpcCornerFixture.Distance(p, Far) <= 251),
            "The same Proxy follows around the corner within 2.5 m of the owner", TimeSpan.FromSeconds(15));
        var following = Trail(proxy, followAt).ToArray();
        Check.That(following.Length >= 2, "Follow published intermediate navigation positions");
        CheckTrail(NearPet, following, "Follow");

        var anchor = following[^1];
        int blockedAt = actor.World.ViewSnapshot().Count;
        await TeleportAsync(FloorCommand, "393/19");
        await Task.Delay(TimeSpan.FromSeconds(3));
        await TeleportAsync(FarCommand, "18/8");
        Check.That(Trail(proxy, blockedAt).All(p => SamePosition(p, anchor)), "The lower-floor Proxy holds its last stand");
        await StayAsync();

        await TeleportAsync(OpenCommand, "18/8");
        var rifle = await SummonAsync(RifleProxy, RifleWire, 7, 337, 8, 200, OpenPet);
        var npc = await actor.WaitForViewNamedAsync(TargetName);
        int rifleAt = actor.World.ViewSnapshot().Count;
        await AttackAsync(npc, "Rifle Simulacrum");
        await StayAsync();
        Check.That(Trail(rifle, rifleAt).All(p => SamePosition(p, OpenPet)), "The clear Rifle control attacks without moving");

        await TeleportAsync(NearCommand, "460/0");
        var nuker = await SummonAsync(Nuker, NukerWire, 7, 337, 10, 150, NearPet);
        await StayAsync();
        await TeleportAsync(FarCommand, "18/8");
        npc = actor.World.CreationSnapshot().Last(view => view.Name == TargetName);
        int attackAt = actor.World.ViewSnapshot().Count;
        await AttackAsync(npc, "Nuker Simulacrum");
        // Mob.BuildHitUpdate selects health and impact FX with 04 80 80 80 c0.
        byte[] damagePrefix = { (byte)npc.ViewId, (byte)(npc.ViewId >> 8), 0x04, 0x80, 0x80, 0x80, 0xc0 };
        await actor.WaitUntilAsync(() => actor.World.ViewSnapshot().Skip(attackAt)
            .Any(section => section.AsSpan().IndexOf(damagePrefix) >= 0),
            "The selected NPC received the actual first damage update", HeadlessPlayer.DefaultTimeout);
        var firstHit = actor.World.ViewSnapshot().Skip(attackAt)
            .Select((section, index) => (Section: index, Offset: section.AsSpan().IndexOf(damagePrefix)))
            .First(hit => hit.Offset >= 0);
        var attacking = Motion(nuker, attackAt).Where(move => (move.Section, move.Offset).CompareTo(firstHit) < 0)
            .Select(move => move.Position).ToArray();
        Check.That(attacking.Length > 0 && Clear(attacking[^1], AttackTarget), "A clear movement update precedes the first hit");
        CheckTrail(NearPet, attacking, "Attack");
        await StayAsync();

        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        Check.That(!server.DebugLog().Contains("[PVP] Round"), "Pet movement does not open owner combat");
        Check.Equal(0, actor.World.Problems.Count, "Pet routing packet framing");
        await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "simulacra-corner-routing.json"),
            JsonSerializer.Serialize(new { Follow = following, Attack = attacking }));
        Console.WriteLine("SIMULACRA CORNER ROUTING PASS: original Stay, grounded Follow and Attack, upper floor hold, stationary Rifle shot.");
        return Array.Empty<byte>();

        async Task TeleportAsync(string command, string nav)
        {
            actor.SkipReceived();
            actor.SendChat(command);
            await actor.WaitForTextAsync("Teleported to " + nav);
        }

        async Task StayAsync()
        {
            actor.SkipReceived();
            actor.SendChat("?pet stay");
            await actor.WaitForTextAsync("Staying.");
        }

        async Task AttackAsync(ViewCreation target, string petName)
        {
            int cursor = server.DebugLog().Length;
            string field = WireFormat.ToHex(BitConverter.GetBytes(DuelPackets.Handle(target.ViewId, target.SpawnId)));
            // PR772 native: 81 51 04 00 02 00, then 81 7b fe 02 04 00 02 00.
            // Substitute the current selected NPC's view and spawn.
            actor.SendRpc("81 51 " + field);
            actor.SendRpc("81 7b fe 02 " + field);
            await actor.WaitUntilAsync(() => server.DebugLog()[cursor..].Contains($"[PET] {petName} hits {TargetName} for "),
                $"{petName} hits the selected NPC", TimeSpan.FromSeconds(22));
        }

        async Task<ViewCreation> SummonAsync(ushort id, string request, byte level, ushort hp, int reuse, int after, double[] expected)
        {
            actor.SkipReceived();
            actor.SendRpc("81 51 00 00 00 00");
            int rpcAt = actor.World.RpcSnapshot().Count;
            int viewAt = actor.World.ViewSnapshot().Count;
            actor.SendRpc(request);
            await EvidenceRetestWire.CastBarAsync(actor, id, 8);
            await EvidenceRetestWire.TimerAsync(actor, rpcAt, id, AbilityCooldowns.OwnTimer, reuse);
            var window = await StockAbilityProfile.ObserveAsync(actor, rpcAt, packet => packet.Opcode == 0x176, "New pet window");
            byte simId = window.Bytes[3];
            if (priorWindow != 0)
                await StockAbilityProfile.ObserveAsync(actor, rpcAt, packet => packet.Bytes is [0x81, 0x77, var removed] &&
                    removed == priorWindow, "Previous window removed");
            priorWindow = simId;
            ViewCreation? pet = null;
            await actor.WaitUntilAsync(() => (pet = actor.World.CreationSnapshot().LastOrDefault(view =>
                view.Goid == CoderPets.SimulacraGoId && view.Attributes["SimulacraID"][0] == simId)) != null,
                "New pet view", HeadlessPlayer.DefaultTimeout);
            Check.That(pet!.Attributes["Level"][0] == level && BitConverter.ToUInt16(pet.Attributes["MaxHealth"]) == hp &&
                SamePosition(EngageFoesFixture.Position(pet), expected), $"Summon {id} is at the verified stand with expected maximum HP");
            await actor.WaitUntilAsync(() => actor.World.ViewSnapshot().Skip(viewAt).SelectMany(AbilityParityCoderSweepMode.InnerStrengthUpdates)
                .Any(value => value == after), $"Summon {id} leaves {after} IS", HeadlessPlayer.DefaultTimeout);
            return pet;
        }

        IEnumerable<double[]> Trail(ViewCreation pet, int cursor) => Motion(pet, cursor).Select(move => move.Position);

        IEnumerable<(int Section, int Offset, double[] Position)> Motion(ViewCreation pet, int cursor)
        {
            foreach (var (section, index) in actor.World.ViewSnapshot().Skip(cursor).Select((section, index) => (section, index)))
                for (int offset = 0; offset + 18 <= section.Length; offset++)
                {
                    if (BitConverter.ToUInt16(section, offset) != pet.ViewId || section[offset + 2] != 0x02 ||
                        section[offset + 3] != 0x0e || section[offset + 4] is not ((byte)Mob.Animations.WALK or (byte)Mob.Animations.STAND))
                        continue;
                    yield return (index, offset, new double[] { BitConverter.ToSingle(section, offset + 6),
                        BitConverter.ToSingle(section, offset + 10), BitConverter.ToSingle(section, offset + 14) });
                    offset += 17;
                }
        }
    }

    private static bool Clear(double[] a, double[] b) => WorldNpcCornerFixture.Straight(a, b);
    private static bool SamePosition(double[] a, double[] b) => a.Zip(b).All(pair => Math.Abs(pair.First - pair.Second) < 1);

    private static void CheckTrail(double[] from, double[][] trail, string action)
    {
        foreach (var point in trail)
        {
            Check.That(NavMesh.Locate(1, point[0], point[1], point[2], out var stand) && !stand.indoors &&
                Math.Abs(stand.floorY + 95 - point[1]) < 1, $"{action} step is grounded");
            Check.That(Clear(from, point) && WorldNpcCornerFixture.Distance(from, point) <= 501,
                $"{action} step stays on the open path under 5 m");
            from = point;
        }
    }
}
