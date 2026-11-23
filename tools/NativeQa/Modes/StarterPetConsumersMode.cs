using System.Text;
using System.Text.RegularExpressions;
using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class StarterPetConsumersMode : NativeQaMode
{
    private const int Level = 18, X = 17000, Y = 495, Z = 2300, TargetX = 17900, TargetZ = 1900;
    private const ushort Coder = 1008, Proxy = 28, Power = 25, Charge = 603, Replacement = 27;
    // Complete PR603 Proxy/Charge and PR619 Power/lower Proxy stock requests.
    internal const string ProxyStock = "80b91c00000000000000000000000000000000000000000000000000585ca80001000000086b0000000000";
    private const string PowerStock = "80b91900000000000000000000000000000000000000000000000000ffff7f7f00000000086b0000000000";
    private const string ChargeStock = "80b95b0200000000000000000000000000000000000000000000000000000090767241086b0000000000";
    private const string ReplacementStock = "80b91b00000000000000000000000000000000000000000000000000ffff7f7f00000000086b0000000000";
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, Coder, Proxy, Power, Charge, Replacement).Order().ToArray();
    private static readonly int Memory = Loadout.Sum(AbilityMemory.Cost);

    public override string Name => "ability-parity-starter-pet-consumers";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110" },
        new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Z},rotation={MobAggro.YawTowards(X,Z,TargetX,TargetZ)}," +
                "innerStrC=700,innerStrM=1000 WHERE charId=110"
        }, DevCommands: true, hotbarAbilities: new[] { Coder, Proxy, Power, Charge, Replacement },
        testMobFixture: "starter-pet-consumers.csv", environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1",
            [CoderPets.RifleProxyQa] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "25,27,28,603", ["NativeCloseableRows"] = 0, ["MemoryUsed"] = Memory },
        readyMessages: new[]
        {
            $"Level {Level}, {Memory}/{AbilityMemory.ForLevel(Level)} memory with all prerequisites. CSR ({X},{Y},{Z}) faces QA Pet Target ({TargetX},{Y},{TargetZ}), 9.85 m away. Both stands and the Rifle spawn (17150,{Y},{Z}) are on outdoor NAV floor 400, clear of WinterFrost. Leave selection clear. Page 2 slots are Coder Style/28/25/603/27. All chat commands can be typed, then press Enter.",
            "Use slots 1 and 2. Expect one live L7 Rifle Proxy, 337 max HP, 8 s cast/reuse, and IS 700 → 650. Type ?pet attack and wait for an ordinary Rifle hit from 8.5 m without a typed influence line, then ?pet stay. Use slot 3 Power Boost with selection clear: 4 s cast, 8 s reuse, IS 650 → 600. [PET] Power Boost adds +9 melee and +9 ballistic for 300 s.",
            "Use slot 4 Charge with selection clear: 4 s cast, 30 s reuse, IS 600 → 565. [PET] Charge reports 20% cost reduction and 50% regen increase for 600 s. Type ?pet to verify cost 8 and power 9/9.",
            "Type ?pet attack. Wait for a [PET] typed influence Proxy base N final M line against QA Pet Target; M is floor(N * 1.09). The actual [PET] hit must report M. The fixture fixes the 28 sheet to Rifle without fixing the hit roll. Type ?pet stay after the hit.",
            "With selection still clear, use slot 5. Expect the old window to close, a fresh L4 Proxy with 224 max HP, 8 s cast/reuse, and IS 565 → 515. Type ?pet: cost 10, power 0/0. Type ?pet attack again and confirm an ordinary hit without a typed bonus. ?pet stay stops it. No interlock [PVP] rounds or player maximum [VITALS] changes."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Pet consumers need repository NAV data.");
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (var stand in new[] { (X, Z), (X + 150, Z), (TargetX, TargetZ) })
            Check.That(NavMesh.Locate(1, stand.Item1, Y, stand.Item2, out var at) && !at.indoors && Math.Abs(at.floorY + 95 - Y) < 1,
                $"Pet consumer stand ({stand.Item1},{Y},{stand.Item2}) is on outdoor NAV");
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        var server = context.ActualServer!;
        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor), row => row.Level);
        Check.That(Memory <= AbilityMemory.ForLevel(Level) && archive.Keys.ToHashSet().SetEquals(Loadout) &&
            Loadout.All(id => AbilityPrerequisites.Current.Closure(id).All(need => archive.TryGetValue(need.Key, out ushort rank) && rank >= need.Value)),
            "Every ranked prerequisite fits the stock loadout");
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        await actor.WaitForViewNamedAsync("QA Pet Target");
        actor.SendRpc(StarterCoderPetBuffsAndDetonateMode.CoderStock);
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 f0 03 00 00");

        int originalAt = actor.World.RpcSnapshot().Count;
        await CastAsync(Proxy, ProxyStock, 8, 8, 650);
        var original = await WindowAsync(originalAt);
        await CheckViewAsync(original.Bytes[3], 7, 337);
        await actor.WaitForViewNamedAsync("Rifle Simulacrum");
        int rifleAt = server.DebugLog().Length;
        actor.SendChat("?pet attack");
        await actor.WaitForTextAsync("Attacking.");
        await actor.WaitUntilAsync(() => server.DebugLog()[rifleAt..].Contains("[PET] Rifle Simulacrum hits QA Pet Target for "),
            "The Rifle Proxy hits from ballistic range", TimeSpan.FromSeconds(22));
        Check.That(!server.DebugLog()[rifleAt..].Contains("[PET] typed influence"), "The unboosted Rifle has no typed influence");
        await StopAsync();
        await CastAsync(Power, PowerStock, 4, 8, 600);
        await CastAsync(Charge, ChargeStock, 4, 30, 565);
        await PetReportAsync("cost 8", "power melee/ballistic 9/9");

        int boostedAt = server.DebugLog().Length;
        actor.SendChat("?pet attack");
        await actor.WaitForTextAsync("Attacking.");
        var boosted = new Regex(@"\[PET\] typed influence Proxy base (\d+) final (\d+)[\s\S]*?\[PET\] Rifle Simulacrum hits QA Pet Target for (\d+)");
        await actor.WaitUntilAsync(() => boosted.IsMatch(server.DebugLog()[boostedAt..]), "Boosted hit on the isolated NPC", TimeSpan.FromSeconds(22));
        var hit = boosted.Match(server.DebugLog()[boostedAt..]);
        int raw = int.Parse(hit.Groups[1].Value);
        int final = int.Parse(hit.Groups[2].Value);
        Check.That(final > raw && final == raw * 109 / 100 && final == int.Parse(hit.Groups[3].Value), "Power Boost changes the real damage");
        await StopAsync();

        int replacementAt = actor.World.RpcSnapshot().Count;
        await CastAsync(Replacement, ReplacementStock, 8, 8, 515);
        var replacement = await WindowAsync(replacementAt);
        Check.That(replacement.Bytes[3] != original.Bytes[3], "Replacement has a fresh live window ID");
        await StockAbilityProfile.ObserveAsync(actor, replacementAt, frame => frame.Bytes is [0x81, 0x77, var id] && id == original.Bytes[3],
            "The original window was removed");
        await CheckViewAsync(replacement.Bytes[3], 4, 224);
        await PetReportAsync("cost 10", "power melee/ballistic 0/0");
        int baselineAt = server.DebugLog().Length;
        actor.SendChat("?pet attack");
        await actor.WaitForTextAsync("Attacking.");
        await actor.WaitUntilAsync(() => server.DebugLog()[baselineAt..].Contains(" hits QA Pet Target for "), "Replacement hits the NPC", TimeSpan.FromSeconds(22));
        Check.That(!server.DebugLog()[baselineAt..].Contains("[PET] typed influence"), "The old pet's boost does not transfer");
        await StopAsync();
        Check.Equal(0, actor.World.Problems.Count, "Pet consumer batch framing");
        Console.WriteLine($"PET CONSUMERS PASS: boosted Rifle hit {raw} → {final}; replacement clears both bonuses.");
        return Array.Empty<byte>();

        async Task StopAsync()
        {
            actor.SendChat("?pet stay");
            await actor.WaitForTextAsync("Staying.");
        }

        async Task PetReportAsync(string cost, string power)
        {
            actor.SendChat("?pet");
            var response = await actor.WaitForTextAsync(cost);
            Check.That(Encoding.ASCII.GetString(response.Bytes).Contains(power), $"Pet reports {power}");
        }

        Task<RpcRecord> WindowAsync(int start) => StockAbilityProfile.ObserveAsync(actor, start,
            frame => frame.Bytes is [0x81, 0x76, ..], "A stock Simulacra registration");

        async Task CheckViewAsync(byte id, byte level, ushort hp)
        {
            await actor.WaitUntilAsync(() => actor.World.CreationSnapshot().Any(view =>
                view.Goid == CoderPets.SimulacraGoId && view.Attributes["SimulacraID"][0] == id &&
                view.Attributes["Level"][0] == level && BitConverter.ToUInt16(view.Attributes["MaxHealth"]) == hp),
                $"Live level-{level} Proxy with {hp} HP", HeadlessPlayer.DefaultTimeout);
        }

        async Task CastAsync(ushort id, string frame, int cast, int reuse, int isAfter)
        {
            int from = actor.World.RpcSnapshot().Count;
            int views = actor.World.ViewSnapshot().Count;
            actor.SkipReceived();
            actor.SendRpc(frame);
            await EvidenceRetestWire.CastBarAsync(actor, id, cast);
            await EvidenceRetestWire.TimerAsync(actor, from, id, AbilityCooldowns.OwnTimer, reuse);
            await actor.WaitUntilAsync(() => actor.World.ViewSnapshot().Skip(views).SelectMany(AbilityParityCoderSweepMode.InnerStrengthUpdates)
                .Any(value => value == isAfter), $"{id} leaves {isAfter} IS", HeadlessPlayer.DefaultTimeout);
        }
    }
}
