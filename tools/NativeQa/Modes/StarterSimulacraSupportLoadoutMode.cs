using System.Diagnostics;
using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class StarterSimulacraSupportLoadoutMode : NativeQaMode
{
    private const int Level = 18, X = 17000, Y = 495, Z = 6400;
    private const ushort Coder = 1008, Proxy = 27, Fortify = 20, Repair = 30, Charge = 603;
    private static readonly ushort[] Hotbar = { Coder, Proxy, Fortify, Repair, Charge };
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(Hotbar.Prepend((ushort)17).Prepend((ushort)2).ToArray())
        .Order().ToArray();
    private static readonly int Memory = Loadout.Sum(AbilityMemory.Cost);
    // Complete retained requests from PR619 (summon) and PR663 (selected pet, lines 2947/3141/3565).
    private const string Summon = "80b91b00000000000000000000000000000000000000000000000000ffff7f7f00000000086b0000000000";
    private const string FortifyUse = "80b914000500040000000000000000000000000000000000000000003860ad0001000000086b0000000000";
    private const string RepairUse = "80b91e000500040000000000000000000000000000000000000000003860ad0001000000086b0000000000";
    private const string ChargeUse = "80b95b020500040000000000000000000000000000000000000000000090767241086b0000000000";

    public override string Name => "ability-parity-starter-simulacra-support-loadout";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110" },
        new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)}," +
                $"x={X},y={Y},z={Z},rotation=64,innerStrC=700,innerStrM=1000 WHERE charId=110"
        }, DevCommands: true, hotbarAbilities: Hotbar, environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "20,30,603", ["NativeCloseableRows"] = 0, ["MemoryUsed"] = Memory },
        readyMessages: new[]
        {
            $"Level {Level}, full prerequisites, {Memory}/{AbilityMemory.ForLevel(Level)} memory. CSR stays at verified outdoor NAV ({X},{Y},{Z}) facing the Proxy at ({X + 150},{Y},{Z}), clear of WinterFrost. Page 2 Coder Style/27/20/30/603. Select Coder Style, summon 27, select the pet and keep it selected. The L4 Proxy starts at 224/224 HP. IS 700 to 650.",
            "Unload 20, 30 and 603 with ?qaunload followed by the ID. Try their slots. Stock may disable them locally. Any sent request gives one reason 7 and preserves 650 IS without a cast, timer or pet change. Reload all three with ?qaload and the ID. If a slot disappears, use the matching Actions entry.",
            "Cast Fortify (20, 2 s) and unload it during the bar. Expect [PET] 20 fizzles: the ability was unloaded during the cast, IS 650 to 620 and 8 s reuse. The pet stays 224/224. Recharge the ability with ?qaload 20. Cast Charge (603, 4 s) and unload during its bar. Expect the same fizzle for 603, 35 IS to 585, 30 s reuse and ?pet cost 10. Reload after the bar. The headless replay also unloads and reloads both in the same bar.",
            "After the 20 timer ends, use a fresh Fortify. Expect [VITALS] 224→494 max, current still 224, resistance +36 for 600 s, IS 585→555. Unload 20 after it lands. The live 224/494 HP and resistance stay. Cast Repair (30, 2 s) and unload during the bar. Expect [PET] 30 fizzles, 20 IS to 535, 13 s reuse and no heal. Reload after the bar.",
            "Use fresh Repair after reuse, 224→244 current at 494 max, 20 IS to 515. After the 603 timer, use fresh Charge. Expect cost 8, +50% pet IS regen for 600 s, 35 IS to 480. Unload 603 after it lands and check cost remains 8. Same pet and window throughout. Projected DONE delta 0."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Pet support loadout needs repository NAV data.");
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (int x in new[] { X, X + 150 })
            Check.That(NavMesh.Locate(1, x, Y, Z, out var nav) && !nav.indoors && Math.Abs(nav.floorY + 95 - Y) < 1,
                $"Verified outdoor stand ({x},{Y},{Z})");
        Check.That(NavMesh.HasSightPath(1, X, Y, Z, X + 150, Y, Z), "Clear sight between CSR and Proxy");
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        var server = context.ActualServer!;
        var profile = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        Check.That(Memory <= AbilityMemory.ForLevel(Level) && profile.Keys.ToHashSet().SetEquals(Loadout) &&
            Loadout.All(id => AbilityPrerequisites.Current.Closure(id).All(need =>
                profile.TryGetValue(need.Key, out var row) && row.Level >= need.Value)), "Complete level 18 prerequisites fit");
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        actor.SendRpc(StarterCoderPetBuffsAndDetonateMode.CoderStock);
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 f0 03 00 00");
        await CastAsync(Proxy, WireFormat.FromHex(Summon), 8, 8, 650);
        var registration = await actor.WaitForRpcAsync("81 76");
        byte simId = registration.Bytes[3];
        ViewCreation? pet = null;
        await actor.WaitUntilAsync(() => (pet = actor.World.CreationSnapshot().LastOrDefault(view =>
            view.Goid == CoderPets.SimulacraGoId && view.Attributes["SimulacraID"][0] == simId)) != null, "Original pet view",
            HeadlessPlayer.DefaultTimeout);
        Check.That(BitConverter.ToUInt16(pet!.Attributes["MaxHealth"]) == 224, "The original L4 pet starts with 224 HP");
        uint handle = DuelPackets.Handle(pet.ViewId, pet.SpawnId);
        actor.SendRpc("81 51 " + WireFormat.ToHex(BitConverter.GetBytes(handle)));
        byte[] fortify = StarterHackerControlTargetMode.WithHandle(FortifyUse, handle);
        byte[] repair = StarterHackerControlTargetMode.WithHandle(RepairUse, handle);
        byte[] charge = StarterHackerControlTargetMode.WithHandle(ChargeUse, handle);

        var batch = new[] { Fortify, Repair, Charge };
        int supportAt = actor.World.RpcSnapshot().Count;
        await StockAbilityProfile.ChangeLoadoutAsync(actor, profile, batch, load: false);
        foreach (var (id, request) in new[] { (Fortify, fortify), (Repair, repair), (Charge, charge) })
            await StarterHackerControlTargetMode.RefusedAsync(actor, id, request, AbilityRefusals.NotLoaded);
        await StockAbilityProfile.ChangeLoadoutAsync(actor, profile, batch, load: true);
        await Task.Delay(TimeSpan.FromSeconds(AbilityHandler.DuplicateAbilityWindowSeconds + 0.15));

        var fortifyFizzle = await CycleDuringAsync(Fortify, fortify, 2, 8, 620);
        var chargeFizzle = await CycleDuringAsync(Charge, charge, 4, 30, 585);
        await PetStatusAsync("cost 10, resistance 0");
        await WaitReuseAsync(Fortify, fortifyFizzle, 8);
        int fortifyAt = actor.World.ViewSnapshot().Count;
        await CastAsync(Fortify, fortify, 2, 8, 555);
        await PetHealthAsync(fortifyAt, 224, 494);
        await StockAbilityProfile.ChangeLoadoutAsync(actor, profile, new[] { Fortify }, load: false);
        await PetStatusAsync("cost 10, resistance 36");

        int missRepairAt = actor.World.RpcSnapshot().Count;
        await CycleDuringAsync(Repair, repair, 2, 13, 535);
        Check.That(!actor.World.RpcSnapshot().Skip(missRepairAt).Any(frame =>
            BytePattern.Parse($"81 78 {simId:x2} 00 f4 00 ee 01").IsMatch(frame.Bytes)), "The stale Repair does not heal the wounded pet");
        await WaitReuseAsync(Repair, missRepairAt, 13);
        int repairAt = actor.World.RpcSnapshot().Count;
        int repairViews = actor.World.ViewSnapshot().Count;
        actor.SendRpc(repair);
        await EvidenceRetestWire.CastBarAsync(actor, Repair, 2);
        await StockAbilityProfile.ChangeLoadoutAsync(actor, profile, new[] { Fortify }, load: true);
        await EvidenceRetestWire.TimerAsync(actor, repairAt, Repair, AbilityCooldowns.OwnTimer, 13);
        await ExpectIsAsync(repairViews, 515);
        await StockAbilityProfile.ObserveAsync(actor, repairAt, frame => BytePattern.Parse($"81 78 {simId:x2} 00 f4 00 ee 01").IsMatch(frame.Bytes),
            "A fresh Repair survives the unrelated loadout change and heals 20");

        await WaitReuseAsync(Charge, chargeFizzle, 30);
        await CastAsync(Charge, charge, 4, 30, 480);
        await PetStatusAsync("cost 8, resistance 36");
        await StockAbilityProfile.ChangeLoadoutAsync(actor, profile, new[] { Charge }, load: false);
        await PetStatusAsync("cost 8, resistance 36");
        Check.That(!actor.World.RpcSnapshot().Skip(supportAt).Any(frame => frame.Opcode is 0x176 or 0x177),
            "The original pet window persists through unload, reload and each cast");
        Check.Equal(0, actor.World.Problems.Count, "Pet support loadout framing");
        Console.WriteLine("SIMULACRA SUPPORT LOADOUT PASS: unloaded refusals, no revived 20/30/603 casts, unrelated reload and landed state retained.");
        return Array.Empty<byte>();

        async Task<int> CycleDuringAsync(ushort id, byte[] request, float duration, float reuse, int expectedIs)
        {
            int from = actor.World.RpcSnapshot().Count, views = actor.World.ViewSnapshot().Count;
            int log = server.DebugLog().Length;
            actor.SkipReceived();
            var elapsed = Stopwatch.StartNew();
            actor.SendRpc(request);
            await EvidenceRetestWire.CastBarAsync(actor, id, duration);
            await StockAbilityProfile.ChangeLoadoutAsync(actor, profile, new[] { id }, load: false);
            await StockAbilityProfile.ChangeLoadoutAsync(actor, profile, new[] { id }, load: true);
            Check.That(elapsed.Elapsed.TotalSeconds < duration, $"Both stock 80 ae acknowledgements precede the {id} cast deadline");
            await EvidenceRetestWire.TimerAsync(actor, from, id, AbilityCooldowns.OwnTimer, reuse);
            await actor.WaitUntilAsync(() => server.DebugLog()[log..].Contains($"[PET] {id} fizzles: the ability was unloaded during the cast"),
                $"The reloaded {id} remains ineligible for its old pending cast", HeadlessPlayer.DefaultTimeout);
            await ExpectIsAsync(views, expectedIs);
            return from;
        }

        async Task CastAsync(ushort id, byte[] request, float castTime, float reuse, int expectedIs)
        {
            int from = actor.World.RpcSnapshot().Count, views = actor.World.ViewSnapshot().Count;
            actor.SkipReceived();
            actor.SendRpc(request);
            await EvidenceRetestWire.CastBarAsync(actor, id, castTime);
            await EvidenceRetestWire.TimerAsync(actor, from, id, AbilityCooldowns.OwnTimer, reuse);
            await ExpectIsAsync(views, expectedIs);
        }

        Task WaitReuseAsync(ushort id, int from, float seconds) => actor.WaitUntilAsync(() =>
            actor.World.RpcSnapshot().Skip(from).Any(frame =>
                frame.Bytes.AsSpan().SequenceEqual(AbilityCooldowns.BuildTimer(id, AbilityCooldowns.OwnTimer, seconds, start: false))),
            $"Ordinary reuse {id} ended", TimeSpan.FromSeconds(seconds + 5));

        Task ExpectIsAsync(int views, int expected) => actor.WaitUntilAsync(() =>
            actor.World.ViewSnapshot().Skip(views).SelectMany(AbilityParityCoderSweepMode.InnerStrengthUpdates).Any(value => value == expected),
            $"CSR has {expected} IS", HeadlessPlayer.DefaultTimeout);

        Task PetHealthAsync(int views, ushort hp, ushort max) =>
            EvidenceRetestWire.ViewSinceAsync(actor, views, pet.ViewId, WireFormat.ToHex(CoderPets.BuildHealthUpdate(hp, max)));

        async Task PetStatusAsync(string text)
        {
            actor.SkipReceived();
            actor.SendChat("?pet");
            await actor.WaitForTextAsync(text);
        }
    }
}
