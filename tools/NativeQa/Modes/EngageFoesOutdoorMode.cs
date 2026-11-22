using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class EngageFoesOutdoorMode : NativeQaMode
{
    private const int Level = 20, X = 17000, Y = 495, Z = 6400;
    private const ushort Coder = 1008, Engage = 610;
    private static readonly (string Name, int X, int Z, bool Hit)[] Targets =
    {
        ("QA Engage Primary", 17400, 6400, true),
        ("QA Engage Edge", 17400, 5800, true),
        ("QA Engage Outside", 17400, 7001, false),
        ("QA Engage Near CSR", 16800, 6300, false)
    };
    private static readonly ushort[] Abilities = new ushort[] { 2, 17, Coder, Engage }
        .SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys.Append(id)).Distinct().Order().ToArray();
    private static readonly int Memory = Abilities.Sum(AbilityMemory.Cost);

    public override string Name => "ability-parity-engage-foes-outdoor";
    public override NativeQaModePlan Plan { get; } = new(Abilities, X, Z, Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110" },
        new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Z}," +
                $"rotation={MobAggro.YawTowards(X,Z,Targets[0].X,Targets[0].Z)},healthC=5000,healthM=5000,innerStrC=250,innerStrM=1000 WHERE charId=110",
            $"UPDATE characters SET x={X},y={Y},z={Z-150} WHERE charId=6111"
        }, DevCommands: true, hotbarAbilities: new[] { Coder, Engage }, testMobFixture: "engage-foes-outdoor.csv",
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "610", ["NativeCloseableRows"] = 1, ["MemoryUsed"] = Memory },
        readyMessages: new[]
        {
            $"Level {Level}, full ranked prerequisite profile {Memory}/{AbilityMemory.ForLevel(Level)} memory. Verified outdoor NAV CSR ({X},{Y},{Z}) facing QA Engage Primary ({Targets[0].X},{Y},{Targets[0].Z}). Silent observer QAFriend ({X},{Y},{Z-150}). No helper starts a fight.",
            "The primary and all three controls are healthy, attackable and stationary. The edge NPC is 6.00 m from Primary, Outside is 6.01 m away on the opposite side, and Near CSR is close to you but outside the primary circle. See the selected primary's name before casting.",
            "Choose Coder Style on page 2 slot 1. Single-click QA Engage Primary and press slot 2 while outside interlock. Save the complete first 610 stock request. Expect 2 s cast, 50 IS (250 to 200), 30 s own reuse. No direct HP damage or PvP round.",
            "Only QA Engage Primary and QA Engage Edge should give one [ENGAGE-FOES] most-hated line each and [MODIFIER] source=610 stat=93 add value=0 duration=6s followed by removal at about 6 s. The two affected NPCs may pursue and start ordinary NPC interlock after 610 lands. Record the six-second removal without retargeting or opening a fight yourself. The two excluded NPCs have no 610 records. The existing headless scenario separately proves diversion when the taunter is already in a fight. Projected DONE +1 for 610 after the stock witness."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var server = context.ActualServer!;
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Engage Foes requires repository NAV data.");
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        var stands = Targets.Select(item => (item.X, item.Z)).Append((X, Z)).Append((X, Z - 150));
        foreach (var (x, z) in stands)
            Check.That(NavMesh.Locate(1, x, Y, z, out var nav) && !nav.indoors && Math.Abs(nav.floorY + 95 - Y) < 1 &&
                NavMesh.HasSightPath(1, X, Y, Z, x, Y, z), $"Stand ({x},{Y},{z}) is on clear outdoor NAV");

        var observer = await HeadlessPlayer.LoginAsync(context.Address, "QAFriend", "QAFriend", stockCharacterLoad: true);
        context.Helpers.Add(observer.Handle, observer);
        await observer.EnterWorldAsync();
        var observerTargets = await VerifyTargetsAsync(observer);
        NativeQaReadiness.RequireNoAmbientAggro(server, observer);
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        Check.That(Memory <= AbilityMemory.ForLevel(Level) && archive.Keys.ToHashSet().SetEquals(Abilities) &&
            Abilities.All(id => AbilityPrerequisites.Current.Closure(id).All(need =>
                archive.TryGetValue(need.Key, out var row) && row.Level >= need.Value)), "Full ranked Engage Foes profile");
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        var targets = await VerifyTargetsAsync(actor);
        var primary = targets[0];
        actor.SendRpc(StarterCoderPetBuffsAndDetonateMode.CoderStock);
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 f0 03 00 00");
        await EngageFoesFixture.RequireFullHealthAsync(actor, primary);
        actor.SendRpc($"81 51 {StockAbilityProfile.LittleEndianHex(primary.ViewId)} {StockAbilityProfile.LittleEndianHex(primary.SpawnId)}");
        int rpc = actor.World.RpcSnapshot().Count, views = actor.World.ViewSnapshot().Count;
        int log = server.DebugLog().Length;
        // Reuse the established Engage Foes headless encoder and retain the first full native 610 request during QA.
        var request = new AbilityUseRecord(Engage, DuelPackets.Handle(primary.ViewId, primary.SpawnId),
            0, 0, 0, 0, 0x6b, 0x6b, 0, 0);
        actor.SendRpc(request.Encode());
        await EvidenceRetestWire.CastBarAsync(actor, Engage, 2);
        await EvidenceRetestWire.TimerAsync(actor, rpc, Engage, AbilityCooldowns.OwnTimer, 30);
        await actor.WaitUntilAsync(() => actor.World.ViewSnapshot().Skip(views)
            .SelectMany(AbilityParityCoderSweepMode.InnerStrengthUpdates).Contains((ushort)200), "Exactly 50 IS spent", HeadlessPlayer.DefaultTimeout);
        await actor.WaitUntilAsync(() => Lines().Count(line => line.Contains("source=610 stat=93 remove value=0 duration=6s")) == 2,
            "Both applied Enraged states expire at their stored six-second duration", TimeSpan.FromSeconds(10));

        var result = Lines();
        for (int i = 0; i < targets.Length; i++)
        {
            var target = targets[i];
            bool hit = Targets[i].Hit;
            int expected = hit ? 1 : 0;
            Check.Equal(expected, result.Count(line => line.Contains($"mob='{target.Name}'") && line.Contains("source=610 stat=93 add value=0 duration=6s")), $"Enraged on {target.Name}");
            Check.Equal(expected, result.Count(line => line.Contains($"[ENGAGE-FOES] source={actor.CharId} ") &&
                line.Contains($"view={target.ViewId} ") && line.Contains($"spawn={target.SpawnId} ")), $"Taunt on {target.Name}");
            if (hit)
            {
                Check.That(result.Any(line => line.Contains($"[ABILITY] 610 lands on view {target.ViewId} for 0")), "The control deals no direct damage");
                Check.That(result.Any(line => line.Contains($"[AGGRO] {target.Name} pursues {actor.CharId}")),
                    "The successful TauntAggro initializer can start ordinary NPC pursuit after landing");
            }
            else
                await EngageFoesFixture.RequireFullHealthAsync(observer, observerTargets[i]);
        }
        Check.That(!result.Any(line => line.Contains("[PVP] Round")), "The NPC responses do not open PvP");
        Check.Equal(0, actor.World.Problems.Count + observer.World.Problems.Count, "Ability wire framing");
        Console.WriteLine("ENGAGE FOES PASS: selected healthy primary, inclusive 6m edge, two exclusions, taunt and Enraged expiration.");
        return Array.Empty<byte>();

        string[] Lines() => server.DebugLog()[log..].Split('\n');

        async Task<ViewCreation[]> VerifyTargetsAsync(HeadlessPlayer player)
        {
            var views = new List<ViewCreation>();
            foreach (var (name, x, z, _) in Targets)
            {
                var view = await player.WaitForViewNamedAsync(name);
                Check.That(view.Goid == 599 && view.Attributes["IsEnemy"][0] == 1 &&
                    BitConverter.ToUInt16(view.Attributes["Health"]) == 5000 &&
                    EngageFoesFixture.Position(view).SequenceEqual(new double[] { x, Y, z }),
                    $"{name} is an attackable full-health NPC at its marked stand");
                views.Add(view);
            }
            return views.ToArray();
        }
    }
}
