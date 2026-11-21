using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class StarterCoderProxyPatchAndChargeMode : NativeQaMode
{
    private const int Level = 18, X = 24800, Y = 495, Z = -1498;
    private const ushort Coder = 1008, Proxy = 28, Patch = 602, Charge = 603;
    private static readonly ushort[] Abilities = StockAbilityProfile.Profile(2, 17, Coder, Proxy, Patch, Charge).Order().ToArray();
    private static readonly int Memory = Abilities.Sum(AbilityMemory.Cost);
    // Complete native Coder and Charge requests, from 4f792a63f1be:864 and b0d0eb7ff285:3369.
    private const string CoderStock = "80b9f0030000000000000000000000000000000000000000000000000000000090767241086b0000000000";
    private const string ChargeStock = "80b95b02000000000000000000000000000000000000000000000000ffff7f7f00000000086b0000000000";
    private const string Deactivate = "81 7a fe 01";

    public override string Name => "ability-parity-starter-coder-proxy-patch-and-charge";
    public override NativeQaModePlan Plan { get; } = new(Abilities, X, Z, Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110" },
        new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Z},rotation=64," +
                "healthC=500,healthM=500,innerStrC=700,innerStrM=1000 WHERE charId=110"
        }, DevCommands: true, hotbarAbilities: new[] { Coder, Proxy, Charge, Patch },
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1",
            ["MXO_DESPERATION_QA_HOLD_HEALTH"] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "28,602,603", ["NativeCloseableRows"] = 1, ["MemoryUsed"] = Memory },
        readyMessages: new[]
        {
            $"Level {Level}, full prerequisite profile {Memory}/{AbilityMemory.ForLevel(Level)} memory. CSR faces the pet stand 1.5 m east outdoors. Select Coder Style on page 2 slot 1 and leave world selection clear. Natural CSR HP/IS regen is held.",
            "Press slot 2 Remote Proxy 2.0 once and retain its first stock request. Expect 8 s cast, 8 s reuse, 50 IS and one live L7 Proxy with 337 Max HP under the current server behavior. Report separately if it disappears immediately. Its shipped Subject callback has an unresolved extra destroy call.",
            "Press slot 3 Charge Simulacra with selection clear. Expect 4 s cast, 30 s reuse, 35 IS, and +50% pet IS regen and 20% lower action cost for 600 s. ?pet should report cost 8 instead of 10. The stock callback range remains open.",
            "Without dismissing manually, press slot 4 Patch Daemon 1.0. Retain its first stock request. Expect 8 s cast, 10 s reuse, 100 IS, and one L18 Healer at 750 Max HP replacing the Proxy. IS 700 → 650 → 615 → 515. Enter ?health 100, confirm two positive CSR heal updates about 3 s apart (8–11 HP each in the current model), then /ordersimulacra All Deactivate. The Healer window/view should disappear and healing stop."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        var server = context.ActualServer!;
        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor), row => row.Level);
        Check.That(archive.Keys.ToHashSet().SetEquals(Abilities) && Abilities.All(id => AbilityPrerequisites.Current.Closure(id)
            .All(need => archive.TryGetValue(need.Key, out ushort rank) && rank >= need.Value)),
            "Margin publishes the complete ranked Coder loadout");
        Check.That(Memory <= AbilityMemory.ForLevel(Level), "The loadout fits stock memory at level 18");
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        actor.SendRpc(CoderStock);
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 f0 03 00 00");

        // The first native requests for 28 and 602 are part of this session. Until captured, use the
        // disposable cast command to exercise their real admission, cost, timer and landing without invented stock bytes.
        var proxy = await SummonAsync(Proxy, 7, 337, 8, 650);
        actor.SendChat("?pet");
        await actor.WaitForTextAsync("cost 10,");
        int chargeAt = actor.World.RpcSnapshot().Count;
        int chargeViews = actor.World.ViewSnapshot().Count;
        actor.SendRpc(ChargeStock);
        await EvidenceRetestWire.CastBarAsync(actor, Charge, 4);
        await EvidenceRetestWire.TimerAsync(actor, chargeAt, Charge, AbilityCooldowns.OwnTimer, 30);
        await WaitForIsAsync(chargeViews, 615);
        actor.SendChat("?pet");
        await actor.WaitForTextAsync("cost 8,");
        Check.That(server.DebugLog().Contains($"Charge Simulacra (603) pet={proxy.Id} costReduction=20% regenIncrease=50% duration=600"),
            "Charge applies both shipped resource changes to the original pet");

        int replaced = actor.World.RpcSnapshot().Count;
        var healer = await SummonAsync(Patch, Level, 750, 10, 515);
        await StockAbilityProfile.ObserveAsync(actor, replaced,
            frame => frame.Bytes is [0x81, 0x77, var id] && id == proxy.Id, "Patch replaces the Proxy's window");
        Check.That(healer.Id != proxy.Id && healer.View.Name == "Healer Simulacrum", "Patch creates a fresh Healer window");
        actor.SendChat("?pet");
        await actor.WaitForTextAsync("cost 10,");

        int woundsAt = actor.World.ViewSnapshot().Count;
        actor.SendChat("?health 100");
        await EvidenceRetestWire.ViewSinceAsync(actor, woundsAt, 2, "02 80 80 80 40 64 00");
        await actor.WaitUntilAsync(() => PvpOpponentsFixture.HealthHistory(actor.World.ViewSnapshot(), woundsAt, 2, self: true)
            .SkipWhile(hp => hp != 100).Distinct().Take(3).Count() == 3, "The Healer restores CSR twice", TimeSpan.FromSeconds(12));
        var healed = PvpOpponentsFixture.HealthHistory(actor.World.ViewSnapshot(), woundsAt, 2, self: true)
            .SkipWhile(hp => hp != 100).Distinct().Take(3).ToArray();
        Check.That(healed.Zip(healed.Skip(1), (before, after) => after - before).All(delta => delta is >= 8 and <= 11),
            "Both live Healer pulses use the modeled level-18 roll with natural HP regen held");
        int dismissed = actor.World.RpcSnapshot().Count;
        int deleteAt = actor.World.ViewSnapshot().Count;
        actor.SendRpc(Deactivate);
        await StockAbilityProfile.ObserveAsync(actor, dismissed,
            frame => frame.Bytes is [0x81, 0x77, var id] && id == healer.Id, "Stock Deactivate removes the Healer window");
        byte[] deletion = WireFormat.FromHex($"01 00 01 01 00 {StockAbilityProfile.LittleEndianHex(healer.View.ViewId)}");
        await actor.WaitUntilAsync(() => actor.World.ViewSnapshot().Skip(deleteAt).Any(packet => packet.AsSpan().IndexOf(deletion) >= 0),
            "Stock Deactivate removes the Healer world view", HeadlessPlayer.DefaultTimeout);
        int afterDismiss = actor.World.ViewSnapshot().Count;
        await Task.Delay(TimeSpan.FromSeconds(CoderPets.AttackPeriodSeconds + 0.5));
        Check.That(!PvpOpponentsFixture.HealthHistory(actor.World.ViewSnapshot(), afterDismiss, 2, self: true).Any(),
            "No further HP updates follow Healer dismissal");
        Check.Equal(0, actor.World.Problems.Count, "Starter Coder summon and charge framing");
        return Array.Empty<byte>();

        async Task<(byte Id, ViewCreation View)> SummonAsync(ushort ability, byte level, ushort health, int reuse, ushort isAfter)
        {
            int rpcAt = actor.World.RpcSnapshot().Count;
            int viewsAt = actor.World.ViewSnapshot().Count;
            int creationsAt = actor.World.CreationSnapshot().Count;
            actor.SendChat($"?pet cast {ability}");
            await EvidenceRetestWire.CastBarAsync(actor, ability, 8);
            await EvidenceRetestWire.TimerAsync(actor, rpcAt, ability, AbilityCooldowns.OwnTimer, reuse);
            await WaitForIsAsync(viewsAt, isAfter);
            var registration = await StockAbilityProfile.ObserveAsync(actor, rpcAt, frame => frame.Bytes is [0x81, 0x76, ..], $"Summon {ability} registration");
            byte id = registration.Bytes[3];
            ViewCreation? pet = null;
            await actor.WaitUntilAsync(() => (pet = actor.World.CreationSnapshot().Skip(creationsAt)
                .FirstOrDefault(view => view.Goid == CoderPets.SimulacraGoId && view.Attributes["SimulacraID"][0] == id)) != null,
                $"Summon {ability} world view", HeadlessPlayer.DefaultTimeout);
            Check.Equal(actor.CharId, BitConverter.ToUInt32(pet!.Attributes["OwnerCharacterID"]), "Summon belongs to CSR");
            Check.Equal(level, pet.Attributes["Level"][0], $"Summon {ability} level clamp");
            Check.Equal(health, BitConverter.ToUInt16(pet.Attributes["MaxHealth"]), $"Summon {ability} maximum HP");
            return (id, pet);
        }

        Task WaitForIsAsync(int views, ushort amount) => actor.WaitUntilAsync(() => actor.World.ViewSnapshot().Skip(views)
            .SelectMany(AbilityParityCoderSweepMode.InnerStrengthUpdates).Contains(amount), $"CSR has {amount} IS", HeadlessPlayer.DefaultTimeout);
    }
}
