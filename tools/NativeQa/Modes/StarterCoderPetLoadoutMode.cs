using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class StarterCoderPetLoadoutMode : NativeQaMode
{
    private const int Level = 18, X = 17000, Y = 495, Z = 6400;
    private const ushort Style = CombatStyles.CoderStyle, Proxy = 27, Drain = CoderPets.DrainCode, Boost = CoderPets.PowerBoost, Derez = 26;
    private static readonly ushort[] Hotbar = { Style, Proxy, Boost, Drain, Derez };
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(Hotbar.Prepend((ushort)17).Prepend((ushort)2).ToArray()).Order().ToArray();
    // Full stock frames from PR593 (summon), PR619 (boost), PR391 (drain) and PR401 (derez).
    private const string ProxyRequest = "80b91b00000000000000000000000000000000000000000000000000585cb40001000000086b0000000000";
    private const string BoostRequest = "80b91900000000000000000000000000000000000000000000000000ffff7f7f00000000086b0000000000";
    private const string DrainRequest = "80b913000e000f000000000000000000000000000000000000000000585cb40001000000086b0000000000";
    private const string DerezRequest = "80b91a0011000f000000000000000000000000000000000000000000585cb30001000000086b0000000000";

    public override string Name => "ability-parity-starter-coder-pet-loadout";

    public override NativeQaModePlan Plan { get; } = new(
        Loadout, X, Z, Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110" },
        new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Z},rotation=64," +
                "innerStrC=300,innerStrM=800 WHERE charId=110"
        }, DevCommands: true, hotbarAbilities: Hotbar, testMobFixture: "martial-player-controls.csv",
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0",
            ["MXO_MOB_WANDER"] = "0",
            ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "19,25,26", ["NativeCloseableRows"] = 0, ["MemoryUsed"] = Loadout.Sum(AbilityMemory.Cost) },
        readyMessages: new[]
        {
            $"Level {Level}, {Loadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} memory. CSR ({X},{Y},{Z}) faces its Proxy spawn ({X + 150},{Y},{Z}); both are on clear outdoor NAV. No bag or helper. Page 2 slots 1â€“5 are Coder Style, Remote Proxy, Power Boost, Drain Code and Proxy Coder.",
            "Select Coder Style, summon the normal level-4 Proxy (300 to 250 IS), and select it from its pet window. Type ?qaunload 25, ?qaunload 19 and ?qaunload 26. Try their hotbar slots. Stock may gate the press or clear an unloaded slot. Any sent request is reason 7; no cast, reuse, debit or pet change.",
            "Run ?qaload 25, ?qaload 19 and ?qaload 26. If stock cleared a shortcut, use the matching Actions entry. With the own pet selected, slot 5 is gated or gives reason 36 for a friendly pet. Clear the selected view for Power Boost, then use slot 3: 50 IS (250 to 200), 4 s cast, 8 s reuse and [PET] melee=9 ballistic=9 duration=300.",
            "After the boost lands, type ?qaunload 25 and then ?pet. The same pet still has power melee/ballistic 9/9. Select the pet and use loaded Drain Code (slot 4): 2 s cast, 58 s reuse, [PET] damage=8000, same pet removed, and IS 200 to 250. No max [VITALS] or [PVP] rounds. The headless check additionally unloads 19 and 25 while their casts are in flight."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Pet loadout QA needs repository NAV data.");
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (int x in new[] { X, X + 150 })
            Check.That(NavMesh.Locate(1, x, Y, Z, out var floor) && !floor.indoors && Math.Abs(floor.floorY + 95 - Y) < 1,
                $"({x},{Y},{Z}) is on outdoor NAV");
        Check.That(NavMesh.HasSightPath(1, X, Y, Z, X + 150, Y, Z), "The own Proxy stands in clear sight");

        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        var server = context.ActualServer!;
        var profile = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level) && Loadout.All(id =>
            profile.ContainsKey(id) && AbilityPrerequisites.Current.Closure(id).All(need =>
                profile.TryGetValue(need.Key, out var row) && row.Level >= need.Value)), "The stocked pet loadout fits the level-18 profile");

        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        actor.SendRpc(StarterCoderPetBuffsAndDetonateMode.CoderStock);
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 f0 03 00 00");

        int summon = actor.World.RpcSnapshot().Count;
        int inner = actor.World.ViewSnapshot().Count;
        actor.SendRpc(ProxyRequest);
        var registration = await StockAbilityProfile.ObserveAsync(actor, summon, frame => frame.Bytes is [0x81, 0x76, ..],
            "The ordinary level-4 Proxy registers");
        await ExpectIsAsync(inner, 250);
        byte sim = registration.Bytes[3];
        ViewCreation? pet = null;
        await actor.WaitUntilAsync(() => (pet = actor.World.CreationSnapshot().LastOrDefault(view =>
            view.Goid == CoderPets.SimulacraGoId && view.Attributes["SimulacraID"][0] == sim)) != null,
            "CSR sees the registered Proxy", HeadlessPlayer.DefaultTimeout);
        uint handle = DuelPackets.Handle(pet!.ViewId, pet.SpawnId);
        byte[] drain = StarterHackerControlTargetMode.WithHandle(DrainRequest, handle);
        byte[] derez = StarterHackerControlTargetMode.WithHandle(DerezRequest, handle);
        byte[] boost = WireFormat.FromHex(BoostRequest);
        ushort[] batch = { Drain, Boost, Derez };
        await StockAbilityProfile.ChangeLoadoutAsync(actor, profile, batch, load: false);
        foreach (var (ability, request) in new[] { (Drain, drain), (Boost, boost), (Derez, derez) })
            await StarterHackerControlTargetMode.RefusedAsync(actor, ability, request, AbilityRefusals.NotLoaded);
        await StockAbilityProfile.ChangeLoadoutAsync(actor, profile, batch, load: true);
        await Task.Delay(TimeSpan.FromSeconds(AbilityHandler.DuplicateAbilityWindowSeconds + 0.15));
        await StarterHackerControlTargetMode.RefusedAsync(actor, Derez, derez, AbilityRefusals.NotEnemySimulacrum);

        int drainFizzle = await UnloadDuringCastAsync(Drain, drain, 2, 58);
        int boostFizzle = await UnloadDuringCastAsync(Boost, boost, 4, 8);
        await ExpectIsAsync(inner, 200);
        await ReportAsync("power melee/ballistic 0/0");
        Check.That(!actor.World.RpcSnapshot().Skip(drainFizzle).Any(frame => frame.Bytes is [0x81, 0x77, ..]),
            "Neither unloaded cast removes the original pet");

        await StockAbilityProfile.ChangeLoadoutAsync(actor, profile, new[] { Boost }, load: true);
        await WaitForTimerEndAsync(boostFizzle, Boost, 8);
        int boostAt = actor.World.RpcSnapshot().Count;
        inner = actor.World.ViewSnapshot().Count;
        actor.SendRpc(boost);
        await EvidenceRetestWire.CastBarAsync(actor, Boost, 4);
        await EvidenceRetestWire.TimerAsync(actor, boostAt, Boost, AbilityCooldowns.OwnTimer, 8);
        await ExpectIsAsync(inner, 150);
        await ReportAsync("power melee/ballistic 9/9");
        await StockAbilityProfile.ChangeLoadoutAsync(actor, profile, new[] { Boost }, load: false);
        await ReportAsync("power melee/ballistic 9/9");

        await StockAbilityProfile.ChangeLoadoutAsync(actor, profile, new[] { Drain }, load: true);
        await WaitForTimerEndAsync(drainFizzle, Drain, 58);
        int drainAt = actor.World.RpcSnapshot().Count;
        inner = actor.World.ViewSnapshot().Count;
        actor.SendRpc(drain);
        await EvidenceRetestWire.CastBarAsync(actor, Drain, 2);
        await EvidenceRetestWire.TimerAsync(actor, drainAt, Drain, AbilityCooldowns.OwnTimer, 58);
        await StockAbilityProfile.ObserveAsync(actor, drainAt, frame => frame.Bytes is [0x81, 0x77, var id] && id == sim,
            "Only the reloaded Drain removes the original pet");
        await ExpectIsAsync(inner, 200);
        Check.Equal(0, actor.World.Problems.Count, "Pet loadout changes and cast framing");
        return Array.Empty<byte>();

        async Task<int> UnloadDuringCastAsync(ushort id, byte[] request, float duration, float reuse)
        {
            int at = actor.World.RpcSnapshot().Count;
            int log = server.DebugLog().Length;
            actor.SendRpc(request);
            await EvidenceRetestWire.CastBarAsync(actor, id, duration);
            await StockAbilityProfile.ChangeLoadoutAsync(actor, profile, new[] { id }, load: false);
            await actor.WaitUntilAsync(() => server.DebugLog()[log..].Contains($"[PET] {id} fizzles: the ability was unloaded during the cast",
                StringComparison.Ordinal), $"Unloading {id} before landing prevents pet mutation", HeadlessPlayer.DefaultTimeout);
            await EvidenceRetestWire.TimerAsync(actor, at, id, AbilityCooldowns.OwnTimer, reuse);
            return at;
        }

        async Task ReportAsync(string info)
        {
            int from = actor.World.RpcSnapshot().Count;
            actor.SendChat("?pet");
            byte[] expected = System.Text.Encoding.ASCII.GetBytes(info);
            await StockAbilityProfile.ObserveAsync(actor, from, frame => frame.Bytes.AsSpan().IndexOf(expected) >= 0,
                $"The live Proxy still reports {info}");
        }

        Task ExpectIsAsync(int after, ushort value) => actor.WaitUntilAsync(() => actor.World.ViewSnapshot().Skip(after)
            .SelectMany(AbilityParityCoderSweepMode.InnerStrengthUpdates).Contains(value), $"CSR current IS is {value}", HeadlessPlayer.DefaultTimeout);

        Task WaitForTimerEndAsync(int at, ushort id, float seconds)
        {
            byte[] end = AbilityCooldowns.BuildTimer(id, AbilityCooldowns.OwnTimer, seconds, start: false);
            return actor.WaitUntilAsync(() => actor.World.RpcSnapshot().Skip(at).Any(frame => frame.Bytes.AsSpan().SequenceEqual(end)),
                $"The normal reuse of {id} ends", TimeSpan.FromSeconds(seconds + 5));
        }
    }
}
