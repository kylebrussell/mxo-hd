using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class StarterDrainCodeDamageMode : NativeQaMode
{
    private const int Level = 8, X = 17000, Y = 495, Z = 6400;
    private const ushort Coder = 1008, Proxy = 27, Fortify = 20, Drain = 19;
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, Coder, Proxy, Fortify, Drain).Order().ToArray();
    // PR593 stock summon and PR391 CSEQ 61. Pet selection uses the live view and spawn IDs.
    private const string ProxyStock = "80b91b00000000000000000000000000000000000000000000000000585cb40001000000086b0000000000";
    private const string DrainStock = "80b913000e000f000000000000000000000000000000000000000000585cb40001000000086b0000000000";

    public override string Name => "ability-parity-starter-drain-code-damage";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110" },
        new[] { $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)}," +
            $"x={X},y={Y},z={Z},rotation=64,innerStrC=800,innerStrM=1000 WHERE charId=110" },
        DevCommands: true, hotbarAbilities: new[] { Coder, Proxy, Fortify, Drain }, testMobFixture: "martial-player-controls.csv",
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1",
            [CoderPets.DrainDamageQa] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "19,20,27", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            $"Level {Level}, {Loadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} memory. Outdoor NAV CSR ({X},{Y},{Z}) faces east toward its own pet stand ({X + 150},{Y},{Z}), clear of ice FX. No helpers and no bag.",
            "Leave selection clear. Press page 2 slot 1 Coder Style then slot 2 Remote Proxy. Expect one level-4 Proxy with a deliberately seeded 9000/9000 HP and IS 800 to 750 after the 8 s cast. Type ?pet stay.",
            "With selection still clear, press slot 3 Fortify. Expect 2 s, 8 s reuse and 750 to 720 IS. [VITALS] owner=110 maxHP 9000 -> 9120, current HP 9000, and [PET] Fortify resistance=16 duration=600. Type ?pet to confirm.",
            "Press slot 4 Drain Code on your Proxy. Selection can stay clear. Expect 2 s, 58 s own reuse and [PET] Drain Code type=5 damage=8000 HP 9000->1000 IS 720->770. The same pet/window stays at 1000/9120 HP. Type ?pet and check the values. No player maximum [VITALS] or [PVP] rounds. The 60 s DecoyDuration question stays open."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (int x in new[] { X, X + 150 })
            Check.That(NavMesh.Locate(1, x, Y, Z, out var stand) && !stand.indoors && Math.Abs(stand.floorY + 95 - Y) < 1,
                $"Outdoor NAV stand ({x},{Y},{Z})");
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        var server = context.ActualServer!;
        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor), row => row.Level);
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level) && Loadout.All(id =>
            archive.ContainsKey(id) && AbilityPrerequisites.Current.Closure(id)
                .All(need => archive.GetValueOrDefault(need.Key) >= need.Value)), "All starter pet prerequisites fit the archive");
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        actor.SendRpc(StarterCoderPetBuffsAndDetonateMode.CoderStock);
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 f0 03 00 00");

        int summonAt = await CastAsync(Convert.FromHexString(ProxyStock), Proxy, 8, 8, 750);
        var registration = await StockAbilityProfile.ObserveAsync(actor, summonAt,
            frame => frame.Bytes is [0x81, 0x76, ..], "The original Proxy registers");
        byte simId = registration.Bytes[3];
        Check.Equal((ushort)9000, BitConverter.ToUInt16(registration.Bytes, 37), "The test pet survives an 8000 hit");
        Check.Equal((ushort)9000, BitConverter.ToUInt16(registration.Bytes, 39), "The test pet starts at max HP");
        ViewCreation? pet = null;
        await actor.WaitUntilAsync(() => (pet = actor.World.CreationSnapshot().LastOrDefault(view =>
            view.Goid == CoderPets.SimulacraGoId && BitConverter.ToUInt32(view.Attributes["OwnerCharacterID"]) == actor.CharId)) != null,
            "The original Proxy is visible", HeadlessPlayer.DefaultTimeout);
        var seen = pet!;
        Check.Equal(simId, seen.Attributes["SimulacraID"][0], "The owner and world view identify the same Proxy");
        uint handle = DuelPackets.Handle(seen.ViewId, seen.SpawnId);
        int viewAt = actor.World.ViewSnapshot().Count;
        await CastAsync(StarterHackerControlTargetMode.WithHandle(StarterCoderPetBuffsAndDetonateMode.FortifyStock, handle),
            Fortify, 2, 8, 720);
        await EvidenceRetestWire.ViewSinceAsync(actor, viewAt, seen.ViewId,
            WireFormat.ToHex(CoderPets.BuildHealthUpdate(9000, 9120)));
        Check.That(server.DebugLog().Contains($"Fortify Simulacra (20) pet={simId} maxHP=9120 resistance=16 duration=600"),
            "Fortify adds the usual large health and resistance modifiers to the same pet");

        viewAt = actor.World.ViewSnapshot().Count;
        int drainAt = await CastAsync(StarterHackerControlTargetMode.WithHandle(DrainStock, handle), Drain, 2, 58, 770);
        await EvidenceRetestWire.ViewSinceAsync(actor, viewAt, seen.ViewId, "04 80 80 80 40 e8 03");
        await StockAbilityProfile.ObserveAsync(actor, drainAt, frame => frame.Bytes is [0x81, 0x78, var id, ..] &&
            id == simId && BitConverter.ToUInt16(frame.Bytes, 4) == 1000 && BitConverter.ToUInt16(frame.Bytes, 6) == 9120,
            "The same pet window reports 1000/9120 HP after Drain");
        Check.That(server.DebugLog().Contains($"Drain Code (19) pet={simId} type=5 damage=8000 HP 9000->1000 IS 720->770"),
            "Drain publishes the independently observable raw damage and IS return");
        Check.That(!actor.World.RpcSnapshot().Skip(drainAt).Any(frame => frame.Bytes is [0x81, 0x77, ..]),
            "Drain does not unregister a surviving pet");
        Check.Equal(0, actor.World.Problems.Count, "Drain damage framing");
        return Array.Empty<byte>();

        async Task<int> CastAsync(byte[] request, ushort ability, float cast, float reuse, ushort isAfter)
        {
            int rpcAt = actor.World.RpcSnapshot().Count;
            int views = actor.World.ViewSnapshot().Count;
            actor.SkipReceived();
            actor.SendRpc(request);
            await EvidenceRetestWire.CastBarAsync(actor, ability, cast);
            await EvidenceRetestWire.TimerAsync(actor, rpcAt, ability, AbilityCooldowns.OwnTimer, reuse);
            await actor.WaitUntilAsync(() => actor.World.ViewSnapshot().Skip(views)
                .SelectMany(AbilityParityCoderSweepMode.InnerStrengthUpdates).Contains(isAfter),
                $"Ability {ability} leaves {isAfter} IS", HeadlessPlayer.DefaultTimeout);
            return rpcAt;
        }
    }
}
