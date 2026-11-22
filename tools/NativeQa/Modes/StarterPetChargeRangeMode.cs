using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class StarterPetChargeRangeMode : NativeQaMode
{
    private const int Level = 18, X = 17000, Y = 495, HomeZ = 2300, InnerZ = 3100, OuterZ = 3500;
    private const ushort Coder = CombatStyles.CoderStyle, Proxy = 27, Fortify = 20, Repair = 30, Charge = 603;
    private const string ChargeUse = "80b95b020000000000000000000000000000000000000000000000003860ad0001000000086b0000000000";
    private const string Summon = "80b91b00000000000000000000000000000000000000000000000000ffff7f7f00000000086b0000000000";
    private const string RepairUse = "80b91e000500040000000000000000000000000000000000000000003860ad0001000000086b0000000000";
    // PR619 summon and PR663 repair and targetless Charge. Only the live pet handle is substituted.
    private static readonly ushort[] Hotbar = { Coder, Proxy, Fortify, Repair, Charge };
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(Hotbar.Concat(new ushort[] { 2, 17 }).ToArray())
        .Order().ToArray();

    public override string Name => "ability-parity-starter-pet-charge-range";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, HomeZ, Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110" },
        new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={HomeZ}," +
                $"rotation={MobAggro.YawTowards(X,HomeZ,X+150,HomeZ)},innerStrC=700,innerStrM=1000 WHERE charId=110"
        }, DevCommands: true, hotbarAbilities: Hotbar, environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1",
            [CoderPets.ChargeRangeQa] = "1"
        }, extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "20,30,603", ["NativeCloseableRows"] = 0, ["MemoryUsed"] = Loadout.Sum(AbilityMemory.Cost)
        }, readyMessages: new[]
        {
            $"Level {Level}, {Loadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} memory with all prerequisites. Outdoor NAV CSR ({X},{Y},{HomeZ}) faces the Proxy spawn ({X+150},{Y},{HomeZ}). Page 2: Coder Style/27/20/30/603.",
            "Select Coder Style and summon. The L4 Proxy has 224/224 HP; IS is 650. Use ?pet passive and ?pet stay, then the OUTER stand printed by the fixture. Keep selection clear. Use ?pet fixture-range to stage stat 214 +80 for 120 s. It narrows Charge's callback range from 50 m to 10 m.",
            "At OUTER (12.09 m), use Charge. If stock sends a request, expect reason 34, no IS spend, and no reuse. Select the pet for Fortify, then Repair at this same stand. Expect 30 IS and [VITALS] 224/224 to 224/494, then 20 IS and pet HP 224 to 244 with no further max change. Their stored 16 m ranges still admit them.",
            "Use the INNER stand (8.14 m) and clear selection. Press Charge once. Expect 4 s cast, 35 IS (600 to 565), 30 s reuse and [PET] costReduction=20% regenIncrease=50% duration=600. The same pet reports cost 8, resistance 36. No interlock or player [VITALS] change."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (var stand in new[] { (X, HomeZ), (X + 150, HomeZ), (X, OuterZ), (X, InnerZ) })
        {
            Check.That(NavMesh.Locate(1, stand.Item1, Y, stand.Item2, out var floor) && !floor.indoors &&
                Math.Abs(floor.floorY + 95 - Y) < 1, $"Pet range stand {stand} is outdoors on floor 400");
            Check.That(NavMesh.HasSightPath(1, stand.Item1, Y, stand.Item2, X + 150, Y, HomeZ),
                $"Pet range stand {stand} has clear sight to the Proxy");
        }
        string outer = Teleport(OuterZ), inner = Teleport(InnerZ);
        Console.WriteLine($"PET RANGE OUTER: {outer}");
        Console.WriteLine($"PET RANGE INNER: {inner}");

        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor), row => row.Level);
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level) &&
            Loadout.All(id => archive.ContainsKey(id) && AbilityPrerequisites.Current.Closure(id).All(req =>
                archive.TryGetValue(req.Key, out ushort rank) && rank >= req.Value)), "Ranked Coder prerequisites fit memory");
        var server = context.ActualServer!;
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        actor.SendRpc(StarterCoderPetBuffsAndDetonateMode.CoderStock);
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 f0 03 00 00");
        await CastAsync(Proxy, WireFormat.FromHex(Summon), 8, 8, 650);
        var registration = await actor.WaitForRpcAsync("81 76");
        byte sim = registration.Bytes[3];
        ViewCreation? pet = null;
        await actor.WaitUntilAsync(() => (pet = actor.World.CreationSnapshot().LastOrDefault(view =>
            view.Goid == CoderPets.SimulacraGoId && view.Attributes["SimulacraID"][0] == sim)) != null,
            "The stock Simulacra window receives its live Proxy view", HeadlessPlayer.DefaultTimeout);
        uint handle = DuelPackets.Handle(pet!.ViewId, pet.SpawnId);
        await ChatAsync("?pet passive", "Passive.");
        await ChatAsync("?pet stay", "Staying.");
        await ChatAsync(outer, "Teleported to");

        // Admit the unmodified cast, then change its range before the original pet receives any effect.
        int rejectedAt = actor.World.RpcSnapshot().Count;
        int logAt = server.DebugLog().Length;
        actor.SendRpc(ChargeUse);
        await EvidenceRetestWire.CastBarAsync(actor, Charge, 4);
        await ChatAsync("?pet fixture-range", "Selection range reduced by 80 percent");
        await StockAbilityProfile.ObserveAsync(actor, rejectedAt, frame =>
            StockAbilityProfile.Bonus(frame, 214, 214, 80, 120, remove: false), "The stock client receives the selection range modifier");
        await actor.WaitUntilAsync(() => server.DebugLog()[logAt..].Contains("[PET] 603 fizzles:", StringComparison.Ordinal),
            "Changing the range during the cast prevents it from reaching the original pet", HeadlessPlayer.DefaultTimeout);
        await ChatAsync("?pet", "cost 10");

        await CastAsync(Fortify, StarterHackerControlTargetMode.WithHandle(StarterCoderPetBuffsAndDetonateMode.FortifyStock, handle), 2, 8, 585);
        await CastAsync(Repair, StarterHackerControlTargetMode.WithHandle(RepairUse, handle), 2, 13, 565);
        await StockAbilityProfile.ObserveAsync(actor, 0,
            frame => BytePattern.Parse($"81 78 {sim:x2} 00 f4 00 ee 01").IsMatch(frame.Bytes), "Pet receives Fortify and Repair under the range reduction");

        byte[] end = AbilityCooldowns.BuildTimer(Charge, AbilityCooldowns.OwnTimer, 30, start: false);
        await actor.WaitUntilAsync(() => actor.World.RpcSnapshot().Skip(rejectedAt).Any(frame => frame.Bytes.AsSpan().SequenceEqual(end)),
            "The interrupted Charge's ordinary reuse clears", TimeSpan.FromSeconds(34));
        await StarterHackerControlTargetMode.RefusedAsync(actor, Charge, WireFormat.FromHex(ChargeUse), AbilityRefusals.TargetOutOfRange);
        await ChatAsync(inner, "Teleported to");
        // The refusal also counts as a click for duplicate-request suppression.
        await Task.Delay(TimeSpan.FromSeconds(AbilityHandler.DuplicateAbilityWindowSeconds + 0.1));
        await CastAsync(Charge, WireFormat.FromHex(ChargeUse), 4, 30, 530);
        await ChatAsync("?pet", "cost 8, resistance 36");
        Check.Equal(0, actor.World.Problems.Count, "Pet range comparison packet framing");
        return Array.Empty<byte>();

        async Task ChatAsync(string request, string reply)
        {
            actor.SkipReceived();
            actor.SendChat(request);
            await actor.WaitForTextAsync(reply);
        }

        async Task CastAsync(ushort id, byte[] request, float cast, float reuse, int expectedIs)
        {
            int rpcAt = actor.World.RpcSnapshot().Count;
            int views = actor.World.ViewSnapshot().Count;
            actor.SendRpc(request);
            await EvidenceRetestWire.CastBarAsync(actor, id, cast);
            await EvidenceRetestWire.TimerAsync(actor, rpcAt, id, AbilityCooldowns.OwnTimer, reuse);
            await actor.WaitUntilAsync(() => actor.World.ViewSnapshot().Skip(views).SelectMany(AbilityParityCoderSweepMode.InnerStrengthUpdates)
                .Contains((ushort)expectedIs), $"{id} leaves {expectedIs} IS", HeadlessPlayer.DefaultTimeout);
        }
    }

    private static string Teleport(int z)
    {
        Check.That(NavMesh.Locate(1, X, Y, z, out var at), "Published teleport has a mapped NAV polygon");
        return $"?tp {X} {Y} {z} {at.sector:x} {at.region:x} {at.polygon:x}";
    }
}
