using hds;
using HeadlessClient;
using static NativeQa.Modes.StarterCoderPetBuffsAndDetonateMode;

namespace NativeQa.Modes;

public sealed class StarterPetRangeAndFortifyExpiryMode : NativeQaMode
{
    private const int Level = 18, X = 24800, Y = 495, Z = -1498, BoundaryZ = Z - 1550, OutsideChargeZ = Z - 5100;
    private const int OutsideShortRangeZ = Z - 2000;
    private const ushort Coder = 1008, Proxy = 27, Fortify = 20, Repair = 30, Charge = 603;
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, Coder, Proxy, Fortify, Repair, Charge).Order().ToArray();
    // PR619 Proxy, PR333 e963ca7e868a:4424–4425 Repair, PR603:836 Charge.
    private const string Summon = "80b91b00000000000000000000000000000000000000000000000000ffff7f7f00000000086b0000000000";
    private const string RepairUse = "80b91e000e000d000000000000000000000000000000000000000000d557377c20000000086b0000000000";
    private const string ChargeUse = "80b95b0200000000000000000000000000000000000000000000000000000090767241086b0000000000";

    public override string Name => "ability-parity-starter-pet-range-and-fortify-expiry";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), Array.Empty<string>(),
        new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Z}," +
                $"rotation={MobAggro.YawTowards(X,Z,X+150,Z)},innerStrC=700,innerStrM=1000 WHERE charId=110",
            $"UPDATE characters SET x={X + 380},y={Y},z={Z - 400} WHERE charId=6111",
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
            $"Leave selection clear and move to ?tp {X} {Y} {OutsideChargeZ} 0 0 0. At about 51 m, press slot 5 for Charge. Then move to ?tp {X} {Y} {OutsideShortRangeZ} 0 0 0 and try slots 3/4 for Fortify and Repair. If stock sends a request, expect only the native range refusal (80 ba reason 34), with no tactic line, cast bar, timer or IS spending. Record stock-local suppression separately.",
            $"Return to ?tp {X} {Y} {BoundaryZ} 0 0 0. The pet is about 15.6 m away. Use 20 first: [VITALS] 224 → 494, +36 resistances, and 2 s cast / 8 s reuse. Use 603 from the same stand. Expect a 4 s cast, 30 s reuse and 35 IS spent. The pet gets 600 s of -20% action cost and +50% regen. Type ?pet for cost 8.",
            $"Return to ?tp {X} {Y} {Z} 0 0 0. Slot 4 heals 224 → 244 while the maximum stays 494. IS is 700 → 650 → 620 → 585 → 565.",
            "Enter ?pet fixture-expire-fortify. This advances only the pet's Fortify expiry. The ordinary tick sends [VITALS] 494 → 224 and clamps current HP to 224 in the owner's and observer's views. Charge remains active (cost 8) and Fortify resistance reads 0 in ?pet."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var server = context.ActualServer!;
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
        var remote = await PetViewAsync(observer);
        actor.SendChat("?pet passive");
        await actor.WaitForTextAsync("Passive.");
        actor.SendChat("?pet stay");
        await actor.WaitForTextAsync("Staying.");

        await MoveAsync(OutsideChargeZ);
        await RefusedAsync(Charge, ChargeUse);
        await MoveAsync(OutsideShortRangeZ);
        await RefusedAsync(Fortify, FortifyStock);
        await RefusedAsync(Repair, RepairUse);
        await MoveAsync(BoundaryZ);
        int raised = actor.World.ViewSnapshot().Count;
        await CastAsync(Fortify, StarterHackerControlTargetMode.WithHandle(FortifyStock, 0), 2, 8, 620);
        var local = await PetViewAsync(actor);
        await PetHealthAsync(actor, raised, local.ViewId, 224, 494);
        Check.That(server.DebugLog().Contains($"Fortify Simulacra (20) pet={sim} maxHP=494 resistance=36 duration=600"),
            "Fortify is admitted within its independent 16 m limit");

        await CastAsync(Charge, StarterHackerControlTargetMode.WithHandle(ChargeUse, 0), 4, 30, 585);
        await MoveAsync(Z);
        int repairAt = actor.World.RpcSnapshot().Count;
        await CastAsync(Repair, StarterHackerControlTargetMode.WithHandle(RepairUse, 0), 2, 13, 565);
        local = await PetViewAsync(actor);
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

        async Task MoveAsync(int z)
        {
            actor.SkipReceived();
            actor.SendChat($"?tp {X} {Y} {z} 0 0 0");
            await actor.WaitForTextAsync("Teleported to 0/0");
        }

        async Task CastAsync(ushort id, byte[] request, int seconds, int reuse, int expectedIs)
        {
            int rpcAt = actor.World.RpcSnapshot().Count;
            int views = actor.World.ViewSnapshot().Count;
            actor.SendRpc(request);
            await EvidenceRetestWire.CastBarAsync(actor, id, seconds);
            await EvidenceRetestWire.TimerAsync(actor, rpcAt, id, AbilityCooldowns.OwnTimer, reuse);
            await actor.WaitUntilAsync(() => actor.World.ViewSnapshot().Skip(views).SelectMany(AbilityParityCoderSweepMode.InnerStrengthUpdates)
                .Any(isNow => isNow == expectedIs), $"{id} leaves {expectedIs} IS", HeadlessPlayer.DefaultTimeout);
        }

        async Task RefusedAsync(ushort id, string request)
        {
            int from = actor.World.RpcSnapshot().Count;
            int views = actor.World.ViewSnapshot().Count;
            // The ordinary unselected form still resolves the caster's own pet after teleport.
            actor.SendRpc(StarterHackerControlTargetMode.WithHandle(request, 0));
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
                !actor.World.ViewSnapshot().Skip(views).SelectMany(AbilityParityCoderSweepMode.InnerStrengthUpdates).Any(isNow => isNow < 650),
                "Range refusal sends no extra chat, starts no cast or reuse, and spends no IS");
        }
    }

    private static Task PetHealthAsync(HeadlessPlayer player, int cursor, ushort view, ushort hp, ushort maximum) =>
        EvidenceRetestWire.ViewSinceAsync(player, cursor, view, WireFormat.ToHex(CoderPets.BuildHealthUpdate(hp, maximum)));
}
