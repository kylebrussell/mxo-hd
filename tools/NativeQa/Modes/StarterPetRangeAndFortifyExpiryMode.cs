using hds;
using HeadlessClient;
using static NativeQa.Modes.StarterCoderPetBuffsAndDetonateMode;

namespace NativeQa.Modes;

public sealed class StarterPetRangeAndFortifyExpiryMode : NativeQaMode
{
    private const int Level = 18, Y = 495;
    private sealed record Stand(int X, int Z, ushort Sector, ushort Region, ushort Polygon)
    {
        public string Teleport => $"?tp {X} {Y} {Z} {Sector:x} {Region:x} {Polygon:x}";
    }

    private static readonly Stand Home = new(24800, -1498, 0x414, 0xe, 0);
    private static readonly Stand FortifyStand = new(24800, -3048, 0x414, 0x10, 3);
    private static readonly Stand ShortRangeRefusal = new(24800, -3498, 0x414, 0x10, 3);
    private static readonly Stand ChargeRefusal = new(29500, -4000, 0x414, 6, 0);
    private const ushort Coder = 1008, Proxy = 27, Fortify = 20, Repair = 30, Charge = 603;
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, Coder, Proxy, Fortify, Repair, Charge).Order().ToArray();
    // Complete stock PR619 Proxy and PR663 (lines 2947, 3141, 3565 and 3814).
    private const string Summon = "80b91b00000000000000000000000000000000000000000000000000ffff7f7f00000000086b0000000000";
    private const string FortifyUse = "80b914000500040000000000000000000000000000000000000000003860ad0001000000086b0000000000";
    private const string RepairUse = "80b91e000500040000000000000000000000000000000000000000003860ad0001000000086b0000000000";
    private const string ChargeUse = "80b95b0205000400000000000000000000000000000000000000000000000090767241086b0000000000";
    private const string FarChargeUse = "80b95b020000000000000000000000000000000000000000000000003860ad0001000000086b0000000000";

    public override string Name => "ability-parity-starter-pet-range-and-fortify-expiry";
    public override NativeQaModePlan Plan { get; } = new(Loadout, Home.X, Home.Z, Array.Empty<string>(), Array.Empty<string>(),
        new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={Home.X},y={Y},z={Home.Z}," +
                $"rotation={MobAggro.YawTowards(Home.X,Home.Z,Home.X+150,Home.Z)},innerStrC=700,innerStrM=1000 WHERE charId=110",
            $"UPDATE characters SET x={Home.X + 380},y={Y},z={Home.Z - 400} WHERE charId=6111",
            $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110"
        }, DevCommands: true, hotbarAbilities: new[] { Coder, Proxy, Fortify, Repair, Charge },
        testMobFixture: "martial-player-controls.csv", environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1",
            [CoderPets.FortifyExpiryQa] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "20,30,603", ["NativeCloseableRows"] = 0, ["MemoryUsed"] = Loadout.Sum(AbilityMemory.Cost) },
        readyMessages: new[]
        {
            "Full level-18 profile. Page 2 Coder Style/27/20/30/603. Use style and summon, then enter ?pet passive and ?pet stay. The L4 Proxy is 1.5 m ahead on clear ground with 224/224 HP. QAFriend observes from the southeast.",
            $"Retest the in-range casts first. Type {FortifyStand.Teleport}. The pet is about 15.6 m away. Let the client settle at the street stand and type ?pet to confirm the pet is active. Use 20: [VITALS] 224 → 494, +36 resistances, and 2 s cast / 8 s reuse. Use 603 at the same stand: 4 s cast, 30 s reuse and 35 IS. The pet gets 600 s of -20% action cost and +50% regen. Type ?pet for cost 8.",
            $"Return with {Home.Teleport}. Slot 4 heals 224 → 244 while the maximum stays 494. IS is 700 → 650 → 620 → 585 → 565.",
            "Enter ?pet fixture-expire-fortify. This advances only the pet's Fortify expiry. The ordinary tick sends [VITALS] 494 → 224 and clamps current HP to 224 in the owner's and observer's views. Charge remains active (cost 8) and Fortify resistance reads 0 in ?pet.",
            $"Range feedback already passed natively. For an optional repeat after cooldowns end, use {ShortRangeRefusal.Teleport} for Fortify and Repair, then {ChargeRefusal.Teleport} for Charge. At the last stand, clear selection before using Charge. If stock sends a request, expect one range refusal (80 ba reason 34), no tactic line or IS debit."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var server = context.ActualServer!;
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Pet QA needs the repository NAV data.");
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (Stand stand in new[] { Home, FortifyStand, ShortRangeRefusal, ChargeRefusal })
        {
            Check.That(NavMesh.Locate(1, stand.X, Y, stand.Z, out var nav) && !nav.indoors &&
                Math.Abs(nav.floorY + 95 - Y) < 1 && nav.sector == stand.Sector && nav.region == stand.Region && nav.polygon == stand.Polygon,
                $"Prepared outdoor stand and teleport NAV match: {stand.Teleport}");
        }
        var observer = await HeadlessPlayer.LoginAsync(context.Address, "QAFriend", "QAFriend", stockCharacterLoad: true);
        context.Helpers.Add(observer.Handle, observer);
        await observer.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, observer);
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        var loaded = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor), row => row.Level);
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level) && loaded.Keys.ToHashSet().SetEquals(Loadout) &&
            Loadout.All(id => AbilityPrerequisites.Current.Closure(id).All(need =>
                loaded.TryGetValue(need.Key, out ushort rank) && rank >= need.Value)), "The full Coder profile fits memory");
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        actor.SendRpc(CoderStock);
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 f0 03 00 00");
        await CastAsync(Proxy, WireFormat.FromHex(Summon), 8, 8, 650);
        var registration = await actor.WaitForRpcAsync("81 76");
        byte sim = registration.Bytes[3];
        var local = await PetViewAsync(actor);
        var remote = await PetViewAsync(observer);
        actor.SendChat("?pet passive");
        await actor.WaitForTextAsync("Passive.");
        actor.SendChat("?pet stay");
        await actor.WaitForTextAsync("Staying.");

        await MoveAsync(FortifyStand);
        int raised = actor.World.ViewSnapshot().Count;
        int fortifyAt = await CastAsync(Fortify, Selected(FortifyUse, local), 2, 8, 620);
        await PetHealthAsync(actor, raised, local.ViewId, 224, 494);
        Check.That(server.DebugLog().Contains($"Fortify Simulacra (20) pet={sim} maxHP=494 resistance=36 duration=600"),
            "Fortify is admitted within its independent 16 m limit");

        int chargeAt = await CastAsync(Charge, Selected(ChargeUse, local), 4, 30, 585);
        await MoveAsync(Home);
        int repairAt = await CastAsync(Repair, Selected(RepairUse, local), 2, 13, 565);
        await StockAbilityProfile.ObserveAsync(actor, repairAt, frame => BytePattern.Parse($"81 78 {sim:x2} 00 f4 00 ee 01").IsMatch(frame.Bytes),
            "Repair leaves the pet at 244/494 HP");
        int ownerAt = actor.World.ViewSnapshot().Count;
        int observerAt = observer.World.ViewSnapshot().Count;
        actor.SendChat("?pet fixture-expire-fortify");
        await actor.WaitForTextAsync("Fortify expiry armed for the next pet tick.");
        await PetHealthAsync(actor, ownerAt, local.ViewId, 224, 224);
        await PetHealthAsync(observer, observerAt, remote.ViewId, 224, 224);
        actor.SendChat("?pet");
        await actor.WaitForTextAsync("cost 8, resistance 0");
        await Task.WhenAll(WaitForReuseAsync(Fortify, fortifyAt, 8), WaitForReuseAsync(Repair, repairAt, 13),
            WaitForReuseAsync(Charge, chargeAt, 30));
        await MoveAsync(ShortRangeRefusal);
        await RefusedAsync(Fortify, Selected(FortifyUse, local));
        await RefusedAsync(Repair, Selected(RepairUse, local));
        await MoveAsync(ChargeRefusal);
        await RefusedAsync(Charge, WireFormat.FromHex(FarChargeUse));
        Check.Equal(0, actor.World.Problems.Count + observer.World.Problems.Count, "Pet expiry framing");
        return Array.Empty<byte>();

        async Task<ViewCreation> PetViewAsync(HeadlessPlayer viewer)
        {
            ViewCreation? found = null;
            await viewer.WaitUntilAsync(() => (found = viewer.World.CreationSnapshot().LastOrDefault(view =>
                view.Goid == CoderPets.SimulacraGoId && view.Attributes["SimulacraID"][0] == sim)) != null,
                $"Live pet view for {viewer.Handle}", HeadlessPlayer.DefaultTimeout);
            return found!;
        }

        async Task MoveAsync(Stand stand)
        {
            actor.SkipReceived();
            actor.SendChat(stand.Teleport);
            await actor.WaitForTextAsync("Teleported to");
        }

        async Task<int> CastAsync(ushort id, byte[] request, int seconds, int reuse, int expectedIs)
        {
            int rpcAt = actor.World.RpcSnapshot().Count;
            int views = actor.World.ViewSnapshot().Count;
            actor.SendRpc(request);
            await EvidenceRetestWire.CastBarAsync(actor, id, seconds);
            await EvidenceRetestWire.TimerAsync(actor, rpcAt, id, AbilityCooldowns.OwnTimer, reuse);
            await actor.WaitUntilAsync(() => actor.World.ViewSnapshot().Skip(views).SelectMany(AbilityParityCoderSweepMode.InnerStrengthUpdates)
                .Any(isNow => isNow == expectedIs), $"{id} leaves {expectedIs} IS", HeadlessPlayer.DefaultTimeout);
            return rpcAt;
        }

        Task WaitForReuseAsync(ushort id, int from, float seconds)
        {
            byte[] end = AbilityCooldowns.BuildTimer(id, AbilityCooldowns.OwnTimer, seconds, start: false);
            return actor.WaitUntilAsync(() => actor.World.RpcSnapshot().Skip(from)
                .Any(frame => frame.Bytes.AsSpan().SequenceEqual(end)),
                $"Own reuse {id} ends before the range repeat", TimeSpan.FromSeconds(seconds + 4));
        }

        async Task RefusedAsync(ushort id, byte[] request)
        {
            int from = actor.World.RpcSnapshot().Count;
            int views = actor.World.ViewSnapshot().Count;
            actor.SendRpc(request);
            await StockAbilityProfile.ObserveAsync(actor, from,
                frame => AbilityRefusals.TryDecode(frame.Bytes, out var notice) && notice.AbilityID == id &&
                    notice.ErrorID == AbilityRefusals.TargetOutOfRange, $"Native range refusal for {id}");
            int barrier = actor.World.RpcSnapshot().Count;
            actor.SendRpc("81 48");
            await StockAbilityProfile.ObserveAsync(actor, barrier, frame => BytePattern.Parse("81 49").IsMatch(frame.Bytes), "Post-refusal barrier");
            string wireId = EvidenceRetestWire.Le(id);
            var frames = actor.World.RpcSnapshot().Skip(from).ToArray();
            Check.Equal(1, frames.Count(frame => AbilityRefusals.TryDecode(frame.Bytes, out var notice) && notice.AbilityID == id),
                "The client receives one refusal");
            Check.That(!frames.Any(frame => BytePattern.Parse($"80 ac {wireId}").IsMatch(frame.Bytes) ||
                BytePattern.Parse($"80 bd 01 {wireId}").IsMatch(frame.Bytes) ||
                System.Text.Encoding.ASCII.GetString(frame.Bytes).Contains("Your simulacrum is out of range.", StringComparison.Ordinal)) &&
                !actor.World.ViewSnapshot().Skip(views).SelectMany(AbilityParityCoderSweepMode.InnerStrengthUpdates).Any(isNow => isNow < 565),
                "Range refusal sends no extra chat, starts no cast or reuse, and spends no IS");
        }
    }

    private static byte[] Selected(string capture, ViewCreation pet) =>
        StarterHackerControlTargetMode.WithHandle(capture, DuelPackets.Handle(pet.ViewId, pet.SpawnId));

    private static Task PetHealthAsync(HeadlessPlayer player, int cursor, ushort view, ushort hp, ushort maximum) =>
        EvidenceRetestWire.ViewSinceAsync(player, cursor, view, WireFormat.ToHex(CoderPets.BuildHealthUpdate(hp, maximum)));
}
