using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class DaemonBoostAccuracyPetsMode : NativeQaMode
{
    private const int Level = 50, X = 17000, Y = 495, Z = 6400, SpawnOffset = 150;
    private const ushort Coder = 1008, RifleProxy = 28, Boost = 619, DamageBoost = 609;
    private const string Stay = "81 7a fe 00";
    // The full 609 RPC comes from the PR832 stock capture, not from the 619 server-only probe.
    private const string StockDamageBoost = "80b961020000000000000000000000000000000000000000000000001060b60001000000086b0000000000";
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, Coder, Boost, RifleProxy).Order().ToArray();
    private static readonly int Memory = Loadout.Sum(AbilityMemory.Cost);
    private static readonly (string Name, int Id, int Org, int X, int Z, string Summon, string PetName)[] Helpers =
    {
        ("QAFriend", 6111, 1, X - SpawnOffset, Z + 1000, AdvancedSimulacraNoticeFixture.Proxy1, "Karate Simulacrum"),
        ("QAFar", 6112, 1, X + 1001 - SpawnOffset, Z, AdvancedSimulacraNoticeFixture.Proxy1, "Karate Simulacrum"),
        ("QAOther", 6113, 1, X - 400, Z, AdvancedSimulacraNoticeFixture.Nuker1, "Nuker Simulacrum"),
        ("QAEnemy", 6114, 2, X + 400, Z, AdvancedSimulacraNoticeFixture.Proxy1, "Karate Simulacrum")
    };

    public override string Name => "ability-parity-daemon-boost-accuracy-pets";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110" },
        new[] { Character(110, 1, X, Z, 79) }.Concat(Helpers.Select(helper =>
            Character(helper.Id, helper.Org, helper.X, helper.Z, 200))).ToArray(),
        DevCommands: true, loadAbilityAncestors: true, hotbarAbilities: new[] { Coder, RifleProxy, Boost }, environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1",
            ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            [WorldConfig.RemoteProxySheetVariable] = "Simulacra.Karate", [CoderPets.RifleProxyQa] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "619,700", ["NativeCloseableRows"] = 2, ["MemoryUsed"] = Memory },
        readyMessages: new[]
        {
            $"L{Level}, {Memory}/{AbilityMemory.ForLevel(Level)} legal memory. Verified outdoor NAV CSR ({X},{Y},{Z}) faces own Rifle ({X+SpawnOffset},{Y},{Z}). Parked pets: QAFriend Karate ({X},{Y},{Z+1000}) at 10.00 m; QAFar Karate ({X+1001},{Y},{Z}) at 10.01 m; QAOther Nuker ({X-250},{Y},{Z}) 2.5 m; QAEnemy Karate ({X+550},{Y},{Z}) 5.5 m, opposing flagged.",
            "Clear selection. Page 2 Coder then Remote Proxy 2.0 (79->29 IS). Expect the fixture's L7 Rifle. Press slot 3 Daemon Boost 2.0 and save the full first 619 request. Expect 2 s, 20 IS (29->9), 15 s reuse, and immediate [DAEMON BOOST] 700 melee=64 ballistic=64 viral=64 on CSR, QAFriend and QAOther only. No 700 on QAFar or QAEnemy.",
            "Leave it running. At approximately +5 s the first upkeep pays 5 IS (9->4) and refreshes exactly those three pets. At +10 s the shortfall turns 619 off with no new debit or pulse. ?pet then reports accuracy 0 after the five-second child expires. No world targeting or attack orders in this stock run; the separate pet attack tests cover typed consumers."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Daemon Boost requires repository NAV.");
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (var (x, z) in Helpers.SelectMany(helper => new[] { (helper.X, helper.Z), (helper.X + SpawnOffset, helper.Z) })
            .Concat(new[] { (X, Z), (X + SpawnOffset, Z) }))
            Check.That(NavMesh.Locate(1, x, Y, z, out var stand) && !stand.indoors && stand.floorY + 95 == Y &&
                NavMesh.HasSightPath(1, X, Y, Z, x, Y, z), $"Clear outdoor NAV at ({x},{Y},{z})");

        var server = context.ActualServer!;
        foreach (var helper in Helpers)
        {
            var player = await HeadlessPlayer.LoginAsync(context.Address, helper.Name, helper.Name, stockCharacterLoad: true);
            context.Helpers.Add(helper.Name, player);
            await PvpOpponentsFixture.EnterAsync(player);
            NativeQaReadiness.RequireNoAmbientAggro(server, player);
        }
        await Task.WhenAll(Helpers.Select(helper => ParkPetAsync(context.Helpers[helper.Name], helper.X, helper.Z,
            helper.Summon, helper.PetName)));
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        var loaded = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        Check.That(Memory <= AbilityMemory.ForLevel(Level) && loaded.Keys.ToHashSet().SetEquals(Loadout) &&
            Loadout.All(id => AbilityPrerequisites.Current.Closure(id).All(need => loaded.TryGetValue(need.Key, out var row) &&
                row.Level >= need.Value)), "The full ranked 619 profile and the 609 control fit in memory");
        await PvpOpponentsFixture.EnterAsync(actor);
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        foreach (var helper in Helpers)
        {
            var observed = await actor.WaitForViewNamedAsync(helper.Name);
            Check.That(observed.Attributes["OrganizationID"][0] == helper.Org &&
                (BitConverter.ToUInt32(observed.Attributes["ConditionStateFlags"]) & PvpFlag.PvpBit) != 0,
                $"{helper.Name} has the fixture's actual flagged organization");
        }

        int views = actor.World.ViewSnapshot().Count;
        await ParkPetAsync(actor, X, Z, StarterPetConsumersMode.ProxyStock, "Rifle Simulacrum");
        await WaitForIsAsync(views, 29);
        int start = server.DebugLog().Length;
        int rpcs = actor.World.RpcSnapshot().Count;
        views = actor.World.ViewSnapshot().Count;
        actor.SendRpc(StockDamageBoost);
        await EvidenceRetestWire.CastBarAsync(actor, DamageBoost, 2);
        await EvidenceRetestWire.TimerAsync(actor, rpcs, DamageBoost, AbilityCooldowns.OwnTimer, 15);
        await WaitForIsAsync(views, 19);
        await actor.WaitUntilAsync(() => new[] { 110, 6111, 6112, 6113 }.All(id =>
                server.DebugLog()[start..].Contains($"[DAEMON BOOST] 699 source=110 recipient={id} ", StringComparison.Ordinal)),
            "Retained stock 609 reaches every friendly fixture pet within its separate 20 m radius", HeadlessPlayer.DefaultTimeout);
        Check.That(!server.DebugLog()[start..].Contains("[DAEMON BOOST] 699 source=110 recipient=6114 ", StringComparison.Ordinal),
            "The stock 609 control also excludes the flagged opposing pet");
        await WaitForIsAsync(views, 14);

        int off = actor.World.RpcSnapshot().Count;
        actor.SendRpc(StockDamageBoost);
        byte[] stopped = AbilityBuffs.BuildExclusive(DamageBoost, false);
        await StockAbilityProfile.ObserveAsync(actor, off, frame => frame.Bytes.AsSpan().SequenceEqual(stopped),
            "Stock 609 toggle ends the control aura");
        await Task.Delay(TimeSpan.FromSeconds(5.5));
        actor.SkipReceived();
        actor.SendChat("?pet");
        await actor.WaitForTextAsync("aura viral/heal 0/0");
        Check.Equal(0, actor.World.Problems.Count + context.Helpers.Values.Sum(helper => helper.World.Problems.Count),
            "Stock fixture and 609 control decode cleanly");
        Console.WriteLine("DAEMON BOOST 2.0 FIXTURE PASS: all live pet stands, stock summons, and exact 609 control. First 619 wire reserved for native.");
        return Array.Empty<byte>();

        Task WaitForIsAsync(int cursor, ushort value) => actor.WaitUntilAsync(() => actor.World.ViewSnapshot().Skip(cursor)
            .SelectMany(AbilityParityCoderSweepMode.InnerStrengthUpdates).Contains(value),
            $"CSR IS is {value}", HeadlessPlayer.DefaultTimeout);
    }

    private static string Character(int id, int organization, int x, int z, int innerStrength) =>
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={x},y={Y},z={z}," +
        $"rotation={MobAggro.YawTowards(x,z,x+SpawnOffset,z)},alignment={organization},pvpflag=0," +
        $"innerStrC={innerStrength},innerStrM=1000 WHERE charId={id}";

    private static async Task ParkPetAsync(HeadlessPlayer player, int x, int z, string summon, string petName)
    {
        player.SendRpc(AdvancedSimulacraNoticeFixture.CoderStyle);
        await player.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 f0 03 00 00");
        player.SendRpc(summon);
        var window = await player.WaitForRpcAsync("81 76", TimeSpan.FromSeconds(14));
        var pet = await AdvancedSimulacraNoticeFixture.PetViewAsync(player, player.CharId, window.Bytes[3]);
        Check.That(pet.Name == petName && EngageFoesFixture.Position(pet).SequenceEqual(new double[] { x + SpawnOffset, Y, z }),
            $"{player.Handle} has an actual {petName} on the measured stand");
        int cursor = player.World.RpcSnapshot().Count;
        player.SendRpc(Stay);
        await StockAbilityProfile.ObserveAsync(player, cursor, record => record.Opcode == 0x178,
            $"{player.Handle} parks the pet with stock Stay");
    }
}
