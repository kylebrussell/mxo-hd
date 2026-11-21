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
    private static readonly ushort[] HelperLoadout = StockAbilityProfile.Profile(2, 17, Coder, Proxy).Order().ToArray();
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
                "innerStrC=700,innerStrM=1000 WHERE charId=110",
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X - 400},y={Y},z={Z}," +
                "rotation=192,innerStrC=500,innerStrM=1000 WHERE charId=6111",
            "DELETE FROM char_abilities WHERE char_id=6111",
            "INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES " +
                string.Join(",", HelperLoadout.Select((id, i) => $"(6111,{i},{0x80000000u | ((uint)id << 10)},{Level},1)"))
        }, DevCommands: true, hotbarAbilities: new[] { Coder, Proxy, Fortify, Power, Detonate },
        testMobFixture: "coder-detonate-proxy.csv", environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_COMBAT_FOUNDATIONS_QA"] = "1",
            ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "20,25,34", ["NativeCloseableRows"] = 0, ["MemoryUsed"] = Memory },
        readyMessages: new[]
        {
            $"Level {Level}, full profile {Memory}/{AbilityMemory.ForLevel(Level)} memory. Start outdoors facing east. Enter ?combatdepth foundations-detonate, leave world selection clear, select Coder Style (page 2 slot 1) and use slot 2 Remote Proxy 1.0. Expect level 4, HP 224/224. Enter ?pet passive to park its attack stance.",
            "QAFriend's proxy is parked west of the starting stand. Clear selection if needed, turn west and select that proxy. Try slots 3/4/5. If the client sends a request on its handle, each should give one 'That is not your simulacrum.' refusal, with no cast bar, debit, timer or change to either proxy. Select empty ground or your own proxy east of the stand to continue.",
            "With no target, use slot 4 Power Boost. Expect 4 s cast, 8 s reuse, 50 IS and +9 melee/+9 ballistic for 300 s. Enter ?pet to confirm both values. Only the own Proxy gets them.",
            "Select the Proxy 1.5 m ahead and slot 3 Fortify. Expect 2 s cast, 8 s reuse, 30 IS, HP 224/494 and +36 to each of four resistances for 600 s. Check [VITALS] owner=110 maxHP 224 -> 494 and the matching [PET] resistance/duration record. ?pet confirms the resistance. Clear target by selecting empty ground before Detonate, or keep the live Proxy selected.",
            $"Use slot 5 Detonate Proxy. Expect 2 s cast, 30 IS and 60 s reuse. The live 224/494 pet gives {RawBlast} damage to Neutral and {ReducedBlast} to +75 Viral Resistance. Outside stays at 5000. Credit dies and awards normal owner XP. The own pet window and view disappear about 0.5 s after the blast. QAFriend's pet stays alive and registered. With regen held, IS goes 700 → 650 → 600 → 570 → 540."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var server = context.ActualServer!;
        var friend = await HeadlessPlayer.LoginAsync(context.Address, "QAFriend", "QAFriend", stockCharacterLoad: true);
        context.Helpers.Add(friend.Handle, friend);
        await friend.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, friend);
        friend.SendRpc(CoderStock);
        await friend.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 f0 03 00 00");
        friend.SendRpc(ProxyStock);
        await friend.WaitForRpcAsync("81 76");
        await PetViewAsync(friend, friend.CharId);
        friend.SendChat("?pet passive");
        await friend.WaitForTextAsync("Passive.");
        friend.SendChat("?pet stay");
        await friend.WaitForTextAsync("Staying.");
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
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
        var pet = await PetViewAsync(actor, actor.CharId);
        Check.Equal(simId, pet.Attributes["SimulacraID"][0], "Own creation matches the registration");
        var foreign = await PetViewAsync(actor, friend.CharId);
        int friendAt = friend.World.RpcSnapshot().Count;
        Check.Equal(actor.CharId, BitConverter.ToUInt32(pet.Attributes["OwnerCharacterID"]), "Proxy belongs to CSR");
        Check.Equal((ushort)224, BitConverter.ToUInt16(pet.Attributes["MaxHealth"]), "Level-four Proxy base HP");
        actor.SendChat("?pet passive");
        await actor.WaitForTextAsync("Passive.");

        uint foreignHandle = DuelPackets.Handle(foreign.ViewId, foreign.SpawnId);
        uint staleHandle = DuelPackets.Handle(pet.ViewId, pet.SpawnId) ^ 0x10000;
        // Alternate ability IDs so the double-click filter does not swallow a distinct negative probe.
        foreach (uint target in new[] { foreignHandle, staleHandle })
            foreach (var (id, capture) in new[] { (Fortify, FortifyStock), (Power, PowerStock), (Detonate, DetonateStock) })
                await RefuseAsync(id, capture, target);

        await CastAsync(Power, Convert.FromHexString(PowerStock), 4, 8, 600);
        Check.That(server.DebugLog().Contains($"Power Boost (25) pet={simId} melee=9 ballistic=9 duration=300"),
            "The unselected Power Boost route snapshots both nine-point bonuses on the current own pet");
        uint handle = DuelPackets.Handle(pet.ViewId, pet.SpawnId);
        int fortifyViews = actor.World.ViewSnapshot().Count;
        await CastAsync(Fortify, StarterHackerControlTargetMode.WithHandle(FortifyStock, handle), 2, 8, 570);
        await EvidenceRetestWire.ViewSinceAsync(actor, fortifyViews, pet.ViewId, WireFormat.ToHex(CoderPets.BuildHealthUpdate(224, FortifiedMax)));
        Check.That(server.DebugLog().Contains($"[VITALS] owner=110 pet={simId} maxHP 224 -> 494") &&
            server.DebugLog().Contains($"Fortify Simulacra (20) pet={simId} maxHP=494 resistance=36 duration=600"),
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
        Check.That(!friend.World.RpcSnapshot().Skip(friendAt).Any(frame => frame.Bytes is [0x81, 0x77, ..]),
            "QAFriend retains their pet after refusals and the caster's own Detonate");
        Check.Equal(0, actor.World.Problems.Count + friend.World.Problems.Count, "Coder pet and Detonate records frame cleanly");
        return Array.Empty<byte>();

        async Task RefuseAsync(ushort id, string capture, uint target)
        {
            int rpcAt = actor.World.RpcSnapshot().Count;
            int viewAt = actor.World.ViewSnapshot().Count;
            actor.SkipReceived();
            actor.SendRpc(StarterHackerControlTargetMode.WithHandle(capture, target));
            await actor.WaitForTextAsync("That is not your simulacrum.");
            await StockAbilityProfile.ObserveAsync(actor, rpcAt,
                frame => AbilityRefusals.TryDecode(frame.Bytes, out var refusal) && refusal.AbilityID == id,
                $"Pet selection refusal {id}");
            var cast = BytePattern.Parse($"80 ac {StockAbilityProfile.LittleEndianHex(id)}");
            var reuse = BytePattern.Parse($"80 bd 01 {StockAbilityProfile.LittleEndianHex(id)}");
            Check.That(!actor.World.RpcSnapshot().Skip(rpcAt).Any(frame => cast.IsMatch(frame.Bytes) || reuse.IsMatch(frame.Bytes)),
                "Foreign and stale pet requests start no cast or reuse");
            Check.That(!actor.World.ViewSnapshot().Skip(viewAt).SelectMany(AbilityParityCoderSweepMode.InnerStrengthUpdates)
                .Any(value => value < 650), "Foreign and stale pet requests spend no IS");
        }

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

    private static async Task<ViewCreation> PetViewAsync(HeadlessPlayer viewer, uint owner)
    {
        ViewCreation? pet = null;
        await viewer.WaitUntilAsync(() => (pet = viewer.World.CreationSnapshot().LastOrDefault(view =>
            view.Goid == CoderPets.SimulacraGoId && view.Attributes.TryGetValue("OwnerCharacterID", out var bytes) &&
            BitConverter.ToUInt32(bytes) == owner)) != null, $"Proxy of owner {owner} is visible", HeadlessPlayer.DefaultTimeout);
        return pet!;
    }
}
