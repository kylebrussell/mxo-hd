using System.Globalization;
using System.Text.RegularExpressions;
using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class StarterCoderPetBuffsAndDetonateMode : NativeQaMode
{
    private const int Level = 18, X = 24800, Y = 495, Z = -1498;
    private const ushort Coder = 1008, Proxy = 27, Fortify = 20, Power = 25, Detonate = 34;
    private const int FortifiedMax = 494, RawBlast = 123;
    private static readonly ushort[] Abilities = StockAbilityProfile.Profile(2, 17, Coder, Proxy, Fortify, Power, Detonate).Order().ToArray();
    private static readonly int Memory = Abilities.Sum(AbilityMemory.Cost);
    private static readonly int ReducedBlast = TypedAbilityResistance.Amount(RawBlast, 4, 7, 75);

    // Stock PR596 4f792a63f1be:864 and native b0d0eb7ff285:2731,5530,3262,10220.
    private const string CoderStock = "80b9f0030000000000000000000000000000000000000000000000000000000090767241086b0000000000";
    private const string ProxyStock = "80b91b00000000000000000000000000000000000000000000000000ffff7f7f00000000086b0000000000";
    private const string FortifyStock = "80b914000e000d000000000000000000000000000000000000000000ffff7f7f00000000086b0000000000";
    private const string PowerStock = "80b91900000000000000000000000000000000000000000000000000ffff7f7f00000000086b0000000000";
    private const string DetonateStock = "80b92200000000000000000000000000000000000000000000000000ffff7f7f00000000086b0000000000";

    public override string Name => "ability-parity-starter-coder-pet-buffs-and-detonate";
    public override NativeQaModePlan Plan { get; } = new(Abilities, X, Z, Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110" },
        new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Z},rotation=64," +
                "innerStrC=700,innerStrM=1000 WHERE charId=110"
        }, DevCommands: true, hotbarAbilities: new[] { Coder, Proxy, Fortify, Power, Detonate },
        testMobFixture: "coder-detonate-proxy.csv", environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_COMBAT_FOUNDATIONS_QA"] = "1",
            ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "20,25,34", ["NativeCloseableRows"] = 0, ["MemoryUsed"] = Memory },
        readyMessages: new[]
        {
            $"Level {Level}, full profile {Memory}/{AbilityMemory.ForLevel(Level)} memory. Start outdoors facing east. Enter ?combatdepth foundations-detonate, leave world selection clear, select Coder Style (page 2 slot 1) and use slot 2 Remote Proxy 1.0. Expect level 4, HP 224/224. Enter ?pet passive to park its attack stance.",
            "Before selecting the pet, use slot 4 Power Boost. Expect 4 s cast, 8 s reuse, 50 IS and +9 melee/+9 ballistic for 300 s. Enter ?pet to confirm both values. Only the own Proxy gets them.",
            "Select the Proxy 1.5 m ahead and slot 3 Fortify. Expect 2 s cast, 8 s reuse, 30 IS, HP 224/494 and +36 to each of four resistances for 600 s. ?pet confirms the resistance. Clear target by selecting empty ground before Detonate, or keep the live Proxy selected.",
            $"Use slot 5 Detonate Proxy. Expect 2 s cast, 30 IS and 60 s reuse. The live 224/494 pet gives {RawBlast} damage to Neutral and {ReducedBlast} to +75 Viral Resistance. Outside stays at 5000. Credit dies and awards normal owner XP. The own pet window and view disappear about 0.5 s after the blast. With regen held, IS goes 700 → 650 → 600 → 570 → 540."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        var server = context.ActualServer!;
        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor), row => row.Level);
        Check.That(archive.Keys.ToHashSet().SetEquals(Abilities) && Abilities.All(id => AbilityPrerequisites.Current.Closure(id)
            .All(need => archive.TryGetValue(need.Key, out ushort rank) && rank >= need.Value)),
            "The full ranked Coder profile arrives in Margin");
        Check.That(Memory <= AbilityMemory.ForLevel(Level), "The profile fits at level 18");
        await actor.EnterWorldAsync();
        actor.SendChat("?combatdepth foundations-detonate");
        await actor.WaitForTextAsync("Detonate fixture ready");
        var neutral = await actor.WaitForViewNamedAsync("Detonate Neutral");
        var resisted = await actor.WaitForViewNamedAsync("Detonate Resisted");
        var outside = await actor.WaitForViewNamedAsync("Detonate Outside");
        var credit = await actor.WaitForViewNamedAsync("Detonate Credit");
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        actor.SendRpc(CoderStock);
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 f0 03 00 00");

        int summonAt = await CastAsync(Proxy, Convert.FromHexString(ProxyStock), 8, 8, 650);
        var registration = await StockAbilityProfile.ObserveAsync(actor, summonAt, frame => frame.Bytes is [0x81, 0x76, ..], "Proxy registers once");
        byte simId = registration.Bytes[3];
        ViewCreation? proxy = null;
        await actor.WaitUntilAsync(() => (proxy = actor.World.CreationSnapshot().FirstOrDefault(view => view.Goid == CoderPets.SimulacraGoId &&
            view.Attributes["SimulacraID"][0] == simId)) != null, "Own Proxy view", HeadlessPlayer.DefaultTimeout);
        var pet = proxy!;
        Check.Equal(actor.CharId, BitConverter.ToUInt32(pet.Attributes["OwnerCharacterID"]), "Proxy belongs to CSR");
        Check.Equal((ushort)224, BitConverter.ToUInt16(pet.Attributes["MaxHealth"]), "Level-four Proxy base HP");
        actor.SendChat("?pet passive");
        await actor.WaitForTextAsync("Passive.");

        await CastAsync(Power, Convert.FromHexString(PowerStock), 4, 8, 600);
        Check.That(server.DebugLog().Contains($"Power Boost (25) pet={simId} melee=9 ballistic=9 duration=300"),
            "The unselected Power Boost route snapshots both nine-point bonuses on the current own pet");
        uint handle = DuelPackets.Handle(pet.ViewId, pet.SpawnId);
        int fortifyViews = actor.World.ViewSnapshot().Count;
        await CastAsync(Fortify, StarterHackerControlTargetMode.WithHandle(FortifyStock, handle), 2, 8, 570);
        await EvidenceRetestWire.ViewSinceAsync(actor, fortifyViews, pet.ViewId, WireFormat.ToHex(CoderPets.BuildHealthUpdate(224, FortifiedMax)));
        Check.That(server.DebugLog().Contains($"Fortify Simulacra (20) pet={simId} maxHP=494 resistance=36 duration=600"),
            "Selected Fortify changes maximum health without refilling it and adds resistance");

        int blastViews = actor.World.ViewSnapshot().Count;
        int blastLog = server.DebugLog().Length;
        int blastAt = await CastAsync(Detonate, Convert.FromHexString(DetonateStock), 2, 60, 540);
        foreach (var (view, hp) in new[] { (neutral, 5000 - RawBlast), (resisted, 5000 - ReducedBlast), (credit, 0) })
            await EvidenceRetestWire.ViewSinceAsync(actor, blastViews, view.ViewId, $"04 80 80 80 c0 {EvidenceRetestWire.Le((ushort)hp)} c0 43 0a 00 28");
        await StockAbilityProfile.ObserveAsync(actor, blastAt,
            frame => frame.Bytes is [0x81, 0x77, var id] && id == simId, "Detonate removes the original pet window");
        byte[] deletion = WireFormat.FromHex($"01 00 01 01 00 {StockAbilityProfile.LittleEndianHex(pet.ViewId)}");
        await actor.WaitUntilAsync(() => actor.World.ViewSnapshot().Skip(blastViews).Any(packet => packet.AsSpan().IndexOf(deletion) >= 0),
            "Detonate removes the original pet view", HeadlessPlayer.DefaultTimeout);
        var excluded = BytePattern.Parse($"{EvidenceRetestWire.Le(outside.ViewId)} 04 80 80 80 c0 ?? ?? c0 43 0a 00 28");
        Check.That(!actor.World.ViewSnapshot().Skip(blastViews).Any(excluded.Occurs), "The outside target takes no blast");
        string log = server.DebugLog()[blastLog..];
        var start = Regex.Match(log, $@"Detonate Proxy: {RawBlast} to 3 NPC\(s\).*start ([0-9.]+)");
        var term = Regex.Match(log, $@"Detonate Proxy term: pet {simId}, at ([0-9.]+)");
        Check.That(start.Success && term.Success, "The fortified pet supplies the blast and its own delayed termination");
        double delay = double.Parse(term.Groups[1].Value, CultureInfo.InvariantCulture) - double.Parse(start.Groups[1].Value, CultureInfo.InvariantCulture);
        Check.That(delay is >= .49 and < 1.5 && log.Contains($"Awarded {KillExperience.ForKill(50, Level)} experience"),
            "The half-second term and ordinary owner XP both occur");
        Check.Equal(0, actor.World.Problems.Count, "Coder pet and Detonate records frame cleanly");
        return Array.Empty<byte>();

        async Task<int> CastAsync(ushort id, byte[] request, float cast, float reuse, ushort isAfter)
        {
            int rpcAt = actor.World.RpcSnapshot().Count;
            int viewAt = actor.World.ViewSnapshot().Count;
            actor.SendRpc(request);
            await EvidenceRetestWire.CastBarAsync(actor, id, cast);
            await EvidenceRetestWire.TimerAsync(actor, rpcAt, id, AbilityCooldowns.OwnTimer, reuse);
            await actor.WaitUntilAsync(() => actor.World.ViewSnapshot().Skip(viewAt)
                .SelectMany(AbilityParityCoderSweepMode.InnerStrengthUpdates).Contains(isAfter),
                $"{id} leaves {isAfter} IS", HeadlessPlayer.DefaultTimeout);
            return rpcAt;
        }
    }
}
