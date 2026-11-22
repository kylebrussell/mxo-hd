using System.Buffers.Binary;
using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class DaemonBoostFriendlyPetsMode : NativeQaMode
{
    private const int Level = 29, HelperLevel = 22, X = 17000, Y = 495, Z = 6400, FriendX = 17400, EnemyX = 16600, FarZ = 2300;
    private const ushort Coder = 1008, Nuker = 22, Patch = 602, Boost = 609;
    private static readonly ushort[] Abilities = new ushort[] { 2, 17, Coder, Nuker, Patch, Boost }
        .SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys.Append(id)).Distinct().Order().ToArray();
    private static readonly int Memory = Abilities.Sum(AbilityMemory.Cost);
    // PR593/603 exact stock summon paths. The older utility fixture's full stock 594 envelope is the template for 609.
    private const string NukerWire = "80b916000000000000000000000000000000000000000000000000000090767241086b0000000000";
    private const string PatchWire = "80b95a02000000000000000000000000000000000000000000000000585ca80001000000086b0000000000";
    private const string StockSelf = "80b95202000000000000000000000000000000000000000000000000ffff7f7f00000000086b0000000000";
    private const string BoostFx = "04 80 80 80 c0 ?? ?? c0 44 08 00 28";

    public override string Name => "ability-parity-daemon-boost-friendly-pets";
    public override NativeQaModePlan Plan { get; } = new(Abilities, X, Z, Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110" },
        new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Z}," +
                $"rotation={MobAggro.YawTowards(X,Z,X+150,Z)},alignment=1,pvpflag=0,innerStrC=79,innerStrM=1000 WHERE charId=110",
            $"UPDATE characters SET level={HelperLevel},exp={PlayerProgression.ExperienceForLevel(HelperLevel)} WHERE charId IN (6111,6112,6114)",
            $"UPDATE char_abilities SET level=LEAST(level,{HelperLevel})," +
                $"is_loaded=IF(ability_id={AbilityMessages.AbilityGameObject(Boost)},0,is_loaded) WHERE char_id IN (6111,6112,6114)",
            $"UPDATE characters SET x={FriendX},y={Y},z={Z},healthC=100,healthM=5000 WHERE charId=6111",
            $"UPDATE characters SET x={X},y={Y},z={FarZ} WHERE charId=6112",
            $"UPDATE characters SET x={EnemyX},y={Y},z={Z},alignment=2,pvpflag=1 WHERE charId=6114"
        }, DevCommands: true, hotbarAbilities: new[] { Coder, Nuker, Boost }, environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1",
            ["MXO_DESPERATION_QA_HOLD_HEALTH"] = "1", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "609,699", ["NativeCloseableRows"] = 2, ["MemoryUsed"] = Memory },
        readyMessages: new[]
        {
            $"Level {Level}, full ranked profile {Memory}/{AbilityMemory.ForLevel(Level)} memory. Outdoor NAV CSR ({X},{Y},{Z}) faces own pet ({X+150},{Y},{Z}); QAFriend and Healer at ({FriendX},{Y},{Z})/({FriendX+150},{Y},{Z}); QAEnemy and Healer at ({EnemyX},{Y},{Z})/({EnemyX+150},{Y},{Z}); QAFar and Healer at ({X},{Y},{FarZ})/({X+150},{Y},{FarZ}).",
            "Clear selection, choose Coder Style and summon Logic Daemon 1.0 via page 2 slot 2. Expect L7 Nuker and IS 79->29. Press slot 3 once and retain the complete first stock 609 frame. Expect 2 s cast, 15 s own reuse, IS 29->19, and immediate 699 FX on the Nuker and QAFriend's Healer.",
            "Expect [DAEMON BOOST] 699 source=110 recipient=110 and recipient=6111, viral=14 healing=14. No 699 for QAEnemy 6114 or QAFar 6112. QAFriend's Healer heals him using current typed +14% scaling; [PET] typed healing shows base and final.",
            "Wait without switching it off. First and next 5 s upkeep logs paid=5 (19->14->9->4), each with the same two recipients. The fourth tick has only 4 IS, emits the shortfall message and [AURA] 609 off, no new pulse or debit. The last 5 s child expires. ?pet then reports aura viral/heal 0/0. A stock pass projects 609 and 699 to DONE."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Daemon Boost requires the repository NAV data.");
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        var stands = new[] { (X, Z), (X + 150, Z), (FriendX, Z), (FriendX + 150, Z), (EnemyX, Z), (EnemyX + 150, Z),
            (X, FarZ), (X + 150, FarZ), (X, Z + 800) };
        foreach (var (x, z) in stands)
            Check.That(NavMesh.Locate(1, x, Y, z, out var nav) && !nav.indoors && Math.Abs(nav.floorY + 95 - Y) < 1,
                $"Stand ({x},{Y},{z}) is on outdoor NAV");
        foreach (var (x, z) in stands.Where(stand => stand.Item2 != FarZ))
            Check.That(NavMesh.HasSightPath(1, X, Y, Z, x, Y, z), $"Stand ({x},{Y},{z}) is visible from CSR");

        var server = context.ActualServer!;
        var controls = new[] { ("QAFriend", FriendX, Z), ("QAEnemy", EnemyX, Z), ("QAFar", X, FarZ) };
        foreach (var (name, _, _) in controls)
        {
            var player = await HeadlessPlayer.LoginAsync(context.Address, name, name, stockCharacterLoad: true);
            context.Helpers.Add(name, player);
            await player.EnterWorldAsync();
            NativeQaReadiness.RequireNoAmbientAggro(server, player);
        }
        await Task.WhenAll(controls.Select(async control =>
        {
            var (name, x, z) = control;
            var player = context.Helpers[name];
            int creations = player.World.CreationSnapshot().Count;
            player.SendRpc(PatchWire);
            var registration = await player.WaitForRpcAsync("81 76", TimeSpan.FromSeconds(14));
            ViewCreation? pet = null;
            await player.WaitUntilAsync(() => (pet = player.World.CreationSnapshot().Skip(creations).FirstOrDefault(view =>
                view.Goid == CoderPets.SimulacraGoId && view.Attributes["SimulacraID"][0] == registration.Bytes[3] &&
                BitConverter.ToUInt32(view.Attributes["OwnerCharacterID"]) == player.CharId)) != null,
                $"{name}'s owned Healer entered world", HeadlessPlayer.DefaultTimeout);
            Check.That(pet!.Name == "Healer Simulacrum" && WorldNpcCornerFixture.Distance(EngageFoesFixture.Position(pet),
                new double[] { x + 150, Y, z }) < 1, $"{name}'s Healer is on the printed NAV stand");
        }));
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        Check.That(Memory <= AbilityMemory.ForLevel(Level) && archive.Keys.ToHashSet().SetEquals(Abilities) &&
            Abilities.All(id => AbilityPrerequisites.Current.Closure(id).All(need =>
                archive.TryGetValue(need.Key, out var row) && row.Level >= need.Value)), "Full legal rank-29 profile");
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        actor.SendRpc(StarterCoderPetBuffsAndDetonateMode.CoderStock);
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 f0 03 00 00");
        int summonViews = actor.World.ViewSnapshot().Count;
        actor.SendRpc(NukerWire);
        await actor.WaitForRpcAsync("81 76", TimeSpan.FromSeconds(14));
        var own = await actor.WaitForViewNamedAsync("Nuker Simulacrum");
        await WaitForIsAsync(summonViews, 29);

        actor.SendChat("?pet dummy 1");
        var dummy = await actor.WaitForViewNamedAsync("Simulacra Dummy");
        byte[] boost = Convert.FromHexString(StockSelf);
        BinaryPrimitives.WriteUInt16LittleEndian(boost.AsSpan(2), Boost);
        int rpc = actor.World.RpcSnapshot().Count, views = actor.World.ViewSnapshot().Count;
        int log = server.DebugLog().Length;
        actor.SendRpc(Convert.ToHexString(boost));
        await EvidenceRetestWire.CastBarAsync(actor, Boost, 2);
        await EvidenceRetestWire.TimerAsync(actor, rpc, Boost, AbilityCooldowns.OwnTimer, 15);
        await actor.WaitForViewUpdateAsync(own.ViewId, BoostFx);
        await WaitForIsAsync(views, 19);

        string handle = $"{StockAbilityProfile.LittleEndianHex(dummy.ViewId)} {StockAbilityProfile.LittleEndianHex(dummy.SpawnId)}";
        actor.SendRpc("81 51 " + handle);
        actor.SendRpc("81 7b fe 02 " + handle);
        await actor.WaitUntilAsync(() => server.DebugLog()[log..].Contains("[PET] typed influence Nuker base") &&
            server.DebugLog()[log..].Contains("[PET] typed healing base"), "Both typed 699 gameplay consumers apply",
            TimeSpan.FromSeconds(12));
        await actor.WaitUntilAsync(() => server.DebugLog()[log..].Contains("[AURA] 609 off"), "Fourth upkeep shortfall deactivates",
            TimeSpan.FromSeconds(25));
        var messages = server.DebugLog()[log..].Split('\n');
        var applications = messages.Where(line => line.Contains("[DAEMON BOOST] 699 source=110")).ToArray();
        foreach (uint id in new uint[] { actor.CharId, context.Helpers["QAFriend"].CharId })
            Check.Equal(4, applications.Count(line => line.Contains($"recipient={id} ") && line.Contains("viral=14 healing=14")),
                $"Four applications to friendly pet owner {id}");
        Check.That(applications.Length == 8,
            "Enemy and out-of-range pets are excluded. Applications: " + string.Join(" | ", applications));
        var balances = actor.World.ViewSnapshot().Skip(views).SelectMany(AbilityParityCoderSweepMode.InnerStrengthUpdates).Distinct().ToArray();
        Check.That(balances.SequenceEqual(new ushort[] { 19, 14, 9, 4 }), "10 IS activation, three exact 5 IS ticks and no debit on shortfall");
        int shutdown = server.DebugLog().Length;
        await Task.Delay(TimeSpan.FromSeconds(5.2));
        Check.That(!server.DebugLog()[shutdown..].Contains("[DAEMON BOOST] 699 source=110"), "No pulse after the parent switches off");
        actor.SendChat("?pet");
        await actor.WaitForTextAsync("aura viral/heal 0/0");
        Check.Equal(0, actor.World.Problems.Count + context.Helpers.Values.Sum(player => player.World.Problems.Count),
            "Daemon Boost packet framing");
        Console.WriteLine("DAEMON BOOST PASS: live friendly pet recipients, typed gameplay, exact cost/upkeep and shortfall.");
        return Array.Empty<byte>();

        Task WaitForIsAsync(int cursor, ushort expected) => actor.WaitUntilAsync(() => actor.World.ViewSnapshot().Skip(cursor)
            .SelectMany(AbilityParityCoderSweepMode.InnerStrengthUpdates).Contains(expected), $"CSR IS is {expected}", HeadlessPlayer.DefaultTimeout);
    }
}
