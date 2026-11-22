using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class AbsorbSimulacraMode : NativeQaMode
{
    private const int Level = 27, X = 17000, Y = 495, Z = 6400, PetX = X + 150;
    private const ushort Coder = 1008, StrongProxy = 586, WeakProxy = 27, Absorb = CoderPets.AbsorbSimulacra;
    private static readonly ushort[] Hotbar = { Coder, StrongProxy, WeakProxy, Absorb };
    private static readonly ushort[] Loadout = Hotbar.Concat(new ushort[] { 2, 17 })
        .SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys.Append(id)).Distinct().Order().ToArray();
    private static readonly int Memory = Loadout.Sum(AbilityMemory.Cost);
    // PR596 PacketLog 4f792a63f1be:923 (586), PR593 (27 and the Deactivate command).
    private const string StrongWire = "80b94a02000000000000000000000000000000000000000000000000585cb70001000000086b0000000000";
    private const string WeakWire = "80b91b00000000000000000000000000000000000000000000000000585cb40001000000086b0000000000";

    public override string Name => "ability-parity-absorb-simulacra";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110" },
        new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Z}," +
                $"rotation={MobAggro.YawTowards(X,Z,PetX,Z)},healthC=100,healthM=500,innerStrC=700,innerStrM=1000 WHERE charId=110"
        }, DevCommands: true, hotbarAbilities: Hotbar, environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1",
            ["MXO_DESPERATION_QA_HOLD_HEALTH"] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "607", ["NativeCloseableRows"] = 1, ["MemoryUsed"] = Memory },
        readyMessages: new[]
        {
            $"Level {Level}, {Memory}/{AbilityMemory.ForLevel(Level)} memory with the full ranked prerequisite profile. Clear outdoor NAV CSR ({X},{Y},{Z}) faces the isolated pet ({PetX},{Y},{Z}), 1.5 m east and away from the ice. No bag or helper selection. Health and IS regeneration are held.",
            "Choose Coder Style on page 2 slot 1 and press slot 2 for Remote Proxy 4.0. Expect L27/1087 HP, 8 s cast and IS 700->625. With no selected target, an optional slot 4 try is unavailable locally or gives reason 24, with no spend or timer. Click the visible Proxy body, confirm its name, then press slot 4. Save the first complete stock 607 request.",
            "607 takes 2 s, spends 25 IS (625->600) and starts 30 s own reuse. [PET] type=5 raw=725 HP 1087->362. [VITALS] char=110 ability=607 type=6 raw=234 HP 100->334. The same Proxy and Simulacra window remain at 362/1087 HP. Type ?pet to confirm.",
            "Press slot 3 for Remote Proxy 1.0, expect L4/224 HP, 50 IS (600->550), and the old window replaced. Type ?health 400, select the new Proxy and wait for 607 reuse. Press slot 4. Expect IS 550->525, [PET] type=5 raw=725 HP 224->0 and one pet window removal. [VITALS] char=110 ability=607 type=6 raw=234 HP 400->500, capped. Neither cast changes maximum HP or opens interlock. Projected DONE +1 for 607."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Absorb Simulacra needs repository NAV data.");
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (int x in new[] { X, PetX })
            Check.That(NavMesh.Locate(1, x, Y, Z, out var nav) && !nav.indoors && Math.Abs(nav.floorY + 95 - Y) < 1 &&
                NavMesh.HasSightPath(1, X, Y, Z, x, Y, Z), $"Stand ({x},{Y},{Z}) is on clear outdoor NAV");
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        var server = context.ActualServer!;
        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        Check.That(Memory <= AbilityMemory.ForLevel(Level) && archive.Keys.ToHashSet().SetEquals(Loadout) &&
            Loadout.All(id => AbilityPrerequisites.Current.Closure(id).All(need =>
                archive.TryGetValue(need.Key, out var row) && row.Level >= need.Value)), "Complete legal rank-27 pet loadout");
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        actor.SendRpc(StarterCoderPetBuffsAndDetonateMode.CoderStock);
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 f0 03 00 00");
        var large = await SummonAsync(StrongProxy, StrongWire, 27, 1087, 625);
        var small = await SummonAsync(WeakProxy, WeakWire, 4, 224, 575);
        Check.That(large != small && actor.World.RpcSnapshot().Any(frame => frame.Bytes is [0x81, 0x77, var oldId] && oldId == large),
            "The captured second summon replaces only the original Proxy");
        int removeAt = actor.World.RpcSnapshot().Count;
        actor.SendRpc("81 7a fe 01");
        await StockAbilityProfile.ObserveAsync(actor, removeAt, frame => frame.Bytes is [0x81, 0x77, var newId] && newId == small,
            "Captured Deactivate removes the weak Proxy");
        Check.Equal(0, actor.World.Problems.Count, "Captured summon and removal framing");
        Console.WriteLine("ABSORB SIMULACRA NATIVE BOOTSTRAP PASS: captured Coder, 586, 27 and Deactivate. The first 607 request is reserved for native.");
        return Array.Empty<byte>();

        async Task<byte> SummonAsync(ushort ability, string request, byte level, ushort maxHp, ushort innerStrength)
        {
            int rpc = actor.World.RpcSnapshot().Count, views = actor.World.ViewSnapshot().Count, creations = actor.World.CreationSnapshot().Count;
            actor.SendRpc(request);
            await EvidenceRetestWire.CastBarAsync(actor, ability, 8);
            await EvidenceRetestWire.TimerAsync(actor, rpc, ability, AbilityCooldowns.OwnTimer, 8);
            var registration = await StockAbilityProfile.ObserveAsync(actor, rpc, frame => frame.Opcode == 0x176, "Stock Proxy registration");
            byte id = registration.Bytes[3];
            ViewCreation? pet = null;
            await actor.WaitUntilAsync(() => (pet = actor.World.CreationSnapshot().Skip(creations).FirstOrDefault(view =>
                view.Goid == CoderPets.SimulacraGoId && view.Attributes["SimulacraID"][0] == id &&
                BitConverter.ToUInt32(view.Attributes["OwnerCharacterID"]) == actor.CharId)) != null,
                "The new owned Proxy is visible", HeadlessPlayer.DefaultTimeout);
            Check.Equal(level, pet!.Attributes["Level"][0], "Proxy level");
            Check.Equal(maxHp, BitConverter.ToUInt16(pet.Attributes["MaxHealth"]), "Proxy maximum HP");
            Check.That(EngageFoesFixture.Position(pet).SequenceEqual(new double[] { PetX, Y, Z }), "Proxy is on its printed stand");
            await actor.WaitUntilAsync(() => actor.World.ViewSnapshot().Skip(views)
                .SelectMany(AbilityParityCoderSweepMode.InnerStrengthUpdates).Contains(innerStrength),
                $"Summon leaves {innerStrength} IS", HeadlessPlayer.DefaultTimeout);
            return id;
        }
    }
}
