using System.Globalization;
using System.Text.RegularExpressions;
using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class StarterFortifyRetaliationMode : NativeQaMode
{
    private const int Level = 18, X = 17000, Y = 495, Z = 2300, PetX = 17150, EnemyX = 17400;
    private const ushort Coder = 1008, Proxy = 27, Fortify = 20;
    private const string TargetName = "QA Fortify Target";
    // Full stock PR593 Remote Proxy request; use the retained PR619 selected Fortify and PR772 pet orders below.
    private const string Summon = "80b91b00000000000000000000000000000000000000000000000000585cb40001000000086b0000000000";
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, Coder, Proxy, Fortify).Order().ToArray();
    private static readonly int Memory = Loadout.Sum(AbilityMemory.Cost);
    private static readonly Regex Retaliation = new(@"\[PET RETALIATION\] attacker=QA Fortify Target attackerLevel=4 pet=(\d+) petLevel=4 meleeRating=(\d+) raw=(\d+) final=(\d+) HP=(\d+)->(\d+)");

    public override string Name => "ability-parity-starter-fortify-retaliation";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110" }, new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Z}," +
                "rotation=64,innerStrC=700,innerStrM=1000 WHERE charId=110"
        }, DevCommands: true, hotbarAbilities: new[] { Coder, Proxy, Fortify }, testMobFixture: "starter-fortify-retaliation.csv",
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1",
            [CoderPets.FortifyExpiryQa] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "20", ["NativeCloseableRows"] = 1, ["MemoryUsed"] = Memory },
        readyMessages: new[]
        {
            $"Level {Level}, {Memory}/{AbilityMemory.ForLevel(Level)} memory. Outdoor NAV CSR ({X},{Y},{Z}) faces east past the Proxy stand ({PetX},{Y},{Z}) to the level-4 QA Fortify Target ({EnemyX},{Y},{Z}), clear of ice. Page 2 Coder/Remote Proxy 1.0/Fortify. Clear selection and summon a level-4 Proxy, 224/224 HP. No other players.",
            "Select QA Fortify Target and click Attack in the Simulacra window (or type /ordersimulacra All Attack). Stay out of owner interlock. Expect [PET RETALIATION] attackerLevel=4 petLevel=4 meleeRating=0 raw=N final=N and pet HP falling by N. Click Stay in the pet window before the next stage.",
            "Select the Proxy or clear the selection, then use slot 3. Fortify spends 30 IS, casts for 2 s, starts 8 s reuse, supplies +36 for 600 s and gives [VITALS] maxHP 224 -> 494 without refilling current HP. Select QA Fortify Target again and click Attack. Expect meleeRating=36 and final=max(1,floor(raw*201/289)); pet HP falls by final. For raw 10 the result is 6. Natural NPC damage rolls vary.",
            "While the Proxy keeps attacking, type ?pet fixture-expire-fortify. The normal pet tick then publishes [VITALS] maxHP 494 -> 224. The next retaliation against the same Proxy shows meleeRating=0 and final=raw. Confirm the matching pet-window HP records. No [PVP] round or owner-health change."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (int x in new[] { X, PetX, EnemyX })
            Check.That(NavMesh.Locate(1, x, Y, Z, out var stand) && !stand.indoors && Math.Abs(stand.floorY + 95 - Y) < 1,
                $"Outdoor NAV at ({x},{Y},{Z})");
        Check.That(NavMesh.HasSightPath(1, X, Y, Z, EnemyX, Y, Z) && NavMesh.HasSightPath(1, PetX, Y, Z, EnemyX, Y, Z),
            "CSR and the Proxy have mapped sight to the NPC");

        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        Check.That(Memory <= AbilityMemory.ForLevel(Level) && Loadout.All(id => archive.ContainsKey(id) &&
            AbilityPrerequisites.Current.Closure(id).All(need => archive.TryGetValue(need.Key, out var row) && row.Level >= need.Value)),
            "The legal ranked pet profile fits memory");
        var server = context.ActualServer!;
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        var target = await actor.WaitForViewNamedAsync(TargetName);
        Check.That(EngageFoesFixture.Position(target).SequenceEqual(new double[] { EnemyX, Y, Z }), "The ordinary NPC is on its verified stand");
        string targetHandle = WireFormat.ToHex(BitConverter.GetBytes(DuelPackets.Handle(target.ViewId, target.SpawnId)));
        actor.SendRpc(StarterCoderPetBuffsAndDetonateMode.CoderStock);
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 f0 03 00 00");
        actor.SendRpc(Summon);
        byte sim = (await actor.WaitForRpcAsync("81 76", TimeSpan.FromSeconds(12))).Bytes[3];
        ViewCreation? pet = null;
        await actor.WaitUntilAsync(() => (pet = actor.World.CreationSnapshot().LastOrDefault(view => view.Goid == CoderPets.SimulacraGoId &&
            view.Attributes["SimulacraID"][0] == sim)) != null, "The summoned level-4 Proxy is visible", HeadlessPlayer.DefaultTimeout);

        await AttackSampleAsync(0, 224);
        actor.SendRpc("81 7a fe 00");
        await actor.WaitUntilAsync(() => server.DebugLog().Contains("id 254 command Stay", StringComparison.Ordinal),
            "The stock Stay stops the Proxy before Fortify", HeadlessPlayer.DefaultTimeout);
        int castCursor = actor.World.RpcSnapshot().Count;
        int logCursor = server.DebugLog().Length;
        actor.SendRpc(StarterHackerControlTargetMode.WithHandle(StarterCoderPetBuffsAndDetonateMode.FortifyStock,
            DuelPackets.Handle(pet!.ViewId, pet.SpawnId)));
        await EvidenceRetestWire.CastBarAsync(actor, Fortify, 2);
        await EvidenceRetestWire.TimerAsync(actor, castCursor, Fortify, AbilityCooldowns.OwnTimer, 8);
        await actor.WaitUntilAsync(() => server.DebugLog()[logCursor..].Contains("maxHP 224 -> 494", StringComparison.Ordinal),
            "Fortify raises the original pet maximum", HeadlessPlayer.DefaultTimeout);

        await AttackSampleAsync(36, 494);
        int beforeExpiryLog = server.DebugLog().Length;
        int beforeExpiryRpc = actor.World.RpcSnapshot().Count;
        actor.SendChat("?pet fixture-expire-fortify");
        await actor.WaitForTextAsync("Fortify expiry armed for the next pet tick.");
        await actor.WaitUntilAsync(() => server.DebugLog()[beforeExpiryLog..].Contains("maxHP 494 -> 224", StringComparison.Ordinal),
            "The ordinary tick restores the original maximum at expiry", HeadlessPlayer.DefaultTimeout);
        await ObserveRetaliationAsync(beforeExpiryLog, beforeExpiryRpc, 0, 224);
        actor.SendRpc("81 7a fe 00");
        Check.Equal(0, actor.World.Problems.Count, "The pet damage stream frames cleanly");
        return Array.Empty<byte>();

        Task AttackSampleAsync(int rating, ushort maxHealth)
        {
            int log = server.DebugLog().Length;
            int rpc = actor.World.RpcSnapshot().Count;
            actor.SendRpc($"81 51 {targetHandle}");
            actor.SendRpc($"81 7b fe 02 {targetHandle}"); // PR772 stock Attack, changing only the selected live handle.
            return ObserveRetaliationAsync(log, rpc, rating, maxHealth);
        }

        async Task ObserveRetaliationAsync(int fromLog, int fromRpc, int rating, ushort maxHealth)
        {
            Match? hit = null;
            await actor.WaitUntilAsync(() => (hit = Retaliation.Matches(server.DebugLog()[fromLog..]).FirstOrDefault(match =>
                Number(match, 1) == sim && Number(match, 2) == rating)) != null,
                $"Ordinary NPC retaliates against the same pet with melee rating {rating}", HeadlessPlayer.DefaultTimeout);
            int raw = Number(hit!, 3), damage = Number(hit!, 4), before = Number(hit!, 5), after = Number(hit!, 6);
            // At equal level 4 the shipped neutral and capped-armored remaining fractions are 289/300 and 201/300.
            int expected = rating == 0 ? raw : Math.Max(1, raw * 201 / 289);
            Check.Equal(expected, damage, $"Retaliation applies the armor change to raw {raw}");
            Check.Equal(before - damage, after, "The retaliation writes the calculated damage to pet HP");
            var record = BytePattern.Parse($"81 78 {sim:x2} 00 {EvidenceRetestWire.Le((ushort)after)} {EvidenceRetestWire.Le(maxHealth)}");
            await StockAbilityProfile.ObserveAsync(actor, fromRpc, frame => record.IsMatch(frame.Bytes), "The client gets the matching pet-window HP");
        }

        static int Number(Match match, int group) => int.Parse(match.Groups[group].Value, CultureInfo.InvariantCulture);
    }
}
