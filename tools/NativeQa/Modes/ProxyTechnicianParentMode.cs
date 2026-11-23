using System.Diagnostics;
using System.Text;
using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class ProxyTechnicianParentMode : NativeQaMode
{
    private const int Level = 30, X = 17000, Y = 495, Z = 6400, SpawnOffset = 150;
    private const ushort Coder = 1008, Proxy = 27, Aura = ProxyTechnician.Ability;
    private const string Stay = "81 7a fe 00";
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, Coder, Proxy, Aura).Order().ToArray();
    private static readonly int Memory = Loadout.Sum(AbilityMemory.Cost);
    private static readonly (string Name, int Id, int Org, int X, int Z)[] Helpers =
    {
        ("QAFriend", 6111, 1, X - SpawnOffset, Z + 2000),
        ("QAFar", 6112, 1, X + 2001 - SpawnOffset, Z),
        ("QAEnemy", 6114, 2, X + 250, Z)
    };

    public override string Name => "ability-parity-proxy-technician-parent";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110" },
        new[] { Character(110, 1, X, Z, 125) }.Concat(Helpers.Select(helper =>
            Character(helper.Id, helper.Org, helper.X, helper.Z, 200))).ToArray(),
        DevCommands: true, loadAbilityAncestors: true, hotbarAbilities: new[] { Coder, Proxy, Aura }, environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1",
            ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "589", ["NativeCloseableRows"] = 1, ["MemoryUsed"] = Memory },
        readyMessages: new[]
        {
            $"L{Level}, {Memory}/{AbilityMemory.ForLevel(Level)} full legal memory. CSR ({X},{Y},{Z}) faces own pet ({X+SpawnOffset},{Y},{Z}). Helper pets are parked on clear NAV: QAFriend ({X},{Y},{Z+2000}) at 20.00 m, QAFar ({X+2001},{Y},{Z}) at 20.01 m, QAEnemy ({X+400},{Y},{Z}) at 4 m and opposing PvP.",
            "Clear selection, use page 2 slots 1 Coder and 2 Remote Proxy 1.0. Expect 50 IS 125->75. Press slot 3 Proxy Technician. Expect 15 IS 75->60, 15 s own reuse, and immediate [PROXYTECH] +38/+38 on CSR's Proxy and QAFriend's exact-edge Proxy only. No pulse for QAFar or QAEnemy.",
            "After about 5 s expect the first [PROXYTECH] upkeep IS=60->55 and the same two recipients. Type ?qaunload 589 just after the tick. Expect one aura-off, no later drain or pulse, and the last child to expire within 5 s. After 6 s, ?pet reports technician melee/ballistic 0/0. The parent outcome is independent of any pet hit roll."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Proxy Technician requires repository NAV.");
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (var (x, z) in Helpers.SelectMany(helper => new[] { (helper.X, helper.Z), (helper.X + SpawnOffset, helper.Z) })
            .Concat(new[] { (X, Z), (X + SpawnOffset, Z) }))
            Check.That(NavMesh.Locate(1, x, Y, z, out var stand) && !stand.indoors && stand.floorY + 95 == Y &&
                NavMesh.HasSightPath(1, X, Y, Z, x, Y, z),
                $"Clear outdoor NAV at ({x},{Y},{z})");

        var server = context.ActualServer!;
        foreach (var helper in Helpers)
        {
            var player = await HeadlessPlayer.LoginAsync(context.Address, helper.Name, helper.Name, stockCharacterLoad: true);
            context.Helpers.Add(helper.Name, player);
            await PvpOpponentsFixture.EnterAsync(player);
            NativeQaReadiness.RequireNoAmbientAggro(server, player);
        }
        await Task.WhenAll(Helpers.Select(helper => ParkProxyAsync(context.Helpers[helper.Name], helper.X, helper.Z)));
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        var loaded = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[7]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        Check.That(Memory <= AbilityMemory.ForLevel(Level) && loaded.Keys.ToHashSet().SetEquals(Loadout) &&
            Loadout.All(id => AbilityPrerequisites.Current.Closure(id).All(need => loaded.TryGetValue(need.Key, out var row) &&
                row.Level >= need.Value)), "A full legal rank-30 Proxy Technician profile");

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
        await ParkProxyAsync(actor, X, Z);
        await WaitForIsAsync(views, 75);
        int startAt = server.DebugLog().Length;
        int rpcs = actor.World.RpcSnapshot().Count;
        var cadence = Stopwatch.StartNew();
        views = actor.World.ViewSnapshot().Count;
        actor.SendRpc(CoderOneCheckBatchMode.TechnicianRequest);
        await ExclusiveAsync(rpcs, active: true);
        await EvidenceRetestWire.TimerAsync(actor, rpcs, Aura, AbilityCooldowns.OwnTimer, 15);
        await WaitForIsAsync(views, 60);
        await actor.WaitUntilAsync(() => Pulses("CSR_Tester").Length == 1 && Pulses("QAFriend").Length == 1,
            "Exactly the own and 20.00 m friendly pets receive the immediate child", TimeSpan.FromSeconds(3));
        Check.That(!server.DebugLog()[startAt..].Contains("[PROXYTECH] drain", StringComparison.Ordinal),
            "The parent has no initial upkeep debit");
        await Task.WhenAll(PetTacticsAsync(actor, 38), PetTacticsAsync(context.Helpers["QAFriend"], 38),
            PetTacticsAsync(context.Helpers["QAFar"], 0), PetTacticsAsync(context.Helpers["QAEnemy"], 0));

        await actor.WaitUntilAsync(() => server.DebugLog()[startAt..].Contains(
            "[PROXYTECH] drain source=CSR_Tester amount=5 IS=60->55", StringComparison.Ordinal),
            "The first periodic debit is five IS", TimeSpan.FromSeconds(8));
        Check.That(cadence.Elapsed.TotalSeconds is >= 4.5 and <= 7.5, "The first upkeep waits approximately five seconds");
        await WaitForIsAsync(views, 55);
        await actor.WaitUntilAsync(() => Pulses("CSR_Tester").Length == 2 && Pulses("QAFriend").Length == 2,
            "Only the same two placements refresh after upkeep", TimeSpan.FromSeconds(3));
        Check.That(Pulses("CSR_Tester").Concat(Pulses("QAFriend")).All(line =>
                line.Contains("melee=38 ballistic=38", StringComparison.Ordinal)),
            "Both real pulses use the authored level-30 linked values");
        Check.That(Pulses("QAFar").Length == 0 && Pulses("QAEnemy").Length == 0,
            "The 20.01 m friendly pet and the in-range opposing pet were excluded by every pulse");

        int unloaded = await StockAbilityProfile.ChangeLoadoutAsync(actor, archive, new[] { Aura }, load: false);
        await ExclusiveAsync(unloaded, active: false);
        int stopped = server.DebugLog().Length;
        await Task.WhenAll(PetTacticsAsync(actor, 38), PetTacticsAsync(context.Helpers["QAFriend"], 38));
        await Task.Delay(TimeSpan.FromSeconds(5.5));
        await Task.WhenAll(PetTacticsAsync(actor, 0), PetTacticsAsync(context.Helpers["QAFriend"], 0));
        Check.That(!server.DebugLog()[stopped..].Contains("[PROXYTECH]", StringComparison.Ordinal),
            "Unloading stops both recurring callbacks while the last child expires");
        Check.Equal(0, actor.World.Problems.Count + context.Helpers.Values.Sum(helper => helper.World.Problems.Count),
            "Proxy Technician parent framing and helper views");
        Console.WriteLine("PROXY TECHNICIAN PARENT PASS: immediate own/20.00 m pulse, two excluded pets, 15 then 5 IS, unload and natural child expiry.");
        return Array.Empty<byte>();

        string[] Pulses(string recipient) => server.DebugLog()[startAt..].Split('\n').Where(line =>
            line.Contains($"[PROXYTECH] pulse source=CSR_Tester recipient={recipient} ", StringComparison.Ordinal)).ToArray();

        Task<RpcRecord> ExclusiveAsync(int cursor, bool active)
        {
            byte[] packet = AbilityBuffs.BuildExclusive(Aura, active);
            return StockAbilityProfile.ObserveAsync(actor, cursor, record => record.Bytes.AsSpan().SequenceEqual(packet),
                $"Proxy Technician active={active}");
        }

        Task WaitForIsAsync(int cursor, ushort value) => actor.WaitUntilAsync(() => actor.World.ViewSnapshot().Skip(cursor)
            .SelectMany(AbilityParityCoderSweepMode.InnerStrengthUpdates).Contains(value),
            $"CSR IS is {value}", HeadlessPlayer.DefaultTimeout);
    }

    private static string Character(int id, int organization, int x, int z, int innerStrength) =>
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={x},y={Y},z={z}," +
        $"rotation={MobAggro.YawTowards(x,z,x+SpawnOffset,z)},alignment={organization},pvpflag=0," +
        $"innerStrC={innerStrength},innerStrM=1000 WHERE charId={id}";

    private static async Task ParkProxyAsync(HeadlessPlayer player, int x, int z)
    {
        player.SendRpc(AdvancedSimulacraNoticeFixture.CoderStyle);
        await player.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 f0 03 00 00");
        player.SendRpc(AdvancedSimulacraNoticeFixture.Proxy1);
        var window = await player.WaitForRpcAsync("81 76", TimeSpan.FromSeconds(14));
        var pet = await AdvancedSimulacraNoticeFixture.PetViewAsync(player, player.CharId, window.Bytes[3]);
        Check.That(EngageFoesFixture.Position(pet).SequenceEqual(new double[] { x + SpawnOffset, Y, z }),
            $"{player.Handle} has a real Proxy on its exact measured stand");
        int cursor = player.World.RpcSnapshot().Count;
        player.SendRpc(Stay);
        await StockAbilityProfile.ObserveAsync(player, cursor, record => record.Opcode == 0x178,
            $"{player.Handle} parks the Proxy with stock Stay");
    }

    private static Task<RpcRecord> PetTacticsAsync(HeadlessPlayer owner, int amount)
    {
        int cursor = owner.World.RpcSnapshot().Count;
        owner.SendChat("?pet");
        return StockAbilityProfile.ObserveAsync(owner, cursor, record =>
            Encoding.ASCII.GetString(record.Bytes).Contains($"technician melee/ballistic {amount}/{amount}", StringComparison.Ordinal),
            $"{owner.Handle} reads back the current linked tactics");
    }
}
