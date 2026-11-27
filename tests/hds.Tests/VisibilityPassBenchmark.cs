using System.Diagnostics;
using System.Net;
using hds;
using Xunit.Abstractions;

namespace hds.Tests;

/// <summary>
/// In-process visibility-pass cost at the scale harness's dense footprint, without MySQL: 200 players in Mara at
/// X17020+(i%20)*80, Z2693+(i/20)*80 who all already see each other and the contacts in range, each moved every pass
/// as LoadTest --move does. The timing run is opt-in (MXO_VISIBILITY_BENCH=1); the always-on part checks that a
/// steady pass sends nothing and that moving a player out of range deletes its view.
/// </summary>
[Collection("AbilityParityWorld")]
public class VisibilityPassBenchmark
{
    private readonly ITestOutputHelper output;
    public VisibilityPassBenchmark(ITestOutputHelper output) => this.output = output;

    internal static WorldClient Player(int i)
    {
        var c = (WorldClient)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        c.Alive = true;
        typeof(WorldClient).GetField("lastUsedTime", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(c, TimeUtils.getUnixTimeUint32());
        c.playerData = new ClientData();
        c.playerData.setCharID((uint)(5000 + i));
        c.playerData.setEntityId((ulong)(900000 + i));
        c.playerData.setDistrictId(1);
        c.playerData.setOnWorld(true);
        c.playerInstance = new PlayerCharacter();
        c.playerInstance.CharacterName.setValue(StringUtils.stringToBytes("Bench" + i));
        c.messageQueue = new MessageQueue();
        c.viewMan = new ViewManager();
        typeof(WorldClient).GetField("lastFlushTick", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(c, long.MaxValue);
        typeof(WorldClient).GetField("<TransportFence>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(c, new WorldSendFence());
        Place(c, i, 0);
        return c;
    }

    internal static void Place(WorldClient c, int i, double phase)
    {
        double x = 17020 + (i % 20) * 80 + 300 * Math.Cos(phase + i * .7), z = 2693 + (i / 20) * 80 + 300 * Math.Sin(phase + i * .7);
        c.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(x, 495, z));
    }

    /// <summary>Everyone already sees everyone, and the contacts within spawn range: the steady state of the scale run.</summary>
    internal static List<WorldClient> Steady(int count)
    {
        var clients = Enumerable.Range(0, count).Select(Player).ToList();
        foreach (var a in clients)
        {
            foreach (var b in clients)
                if (!ReferenceEquals(a, b)) a.viewMan.addView(b.playerData.getEntityId(), 12).viewCreated = true;
            double x = 0, y = 0, z = 0; NumericalUtils.LtVector3dToDoubles(a.playerInstance.Position.getValue(), ref x, ref y, ref z);
            foreach (var spot in MissionContactNpcs.All.Where(s => s.metrId == 1 && s.Contact != null &&
                         Math.Sqrt((s.x - x) * (s.x - x) + (s.z - z) * (s.z - z)) <= 10600))
                a.viewMan.addView(MissionContactNpcs.EntityId(spot), MissionContactNpcs.ContactGoid).viewCreated = true;
        }
        return clients;
    }

    private static int Queued(IEnumerable<WorldClient> clients) =>
        clients.Sum(c => c.messageQueue.ObjectMessagesQueue.Count + c.messageQueue.RPCMessagesQueue.Count);

    [Fact]
    public void SteadyDensePassesSendNothingAndALeaverIsDeleted()
    {
        var clients = Steady(30);
        for (int pass = 0; pass < 3; pass++)
        {
            for (int i = 0; i < clients.Count; i++) Place(clients[i], i, pass * .1);
            WorldThreads.CheckPlayerViews(clients, WorldThreads.BuildClientGrids(clients), WorldThreads.BuildClientByEntity(clients));
            MissionContactNpcs.CheckViews(clients);
        }
        Assert.Equal(0, Queued(clients));
        Assert.All(clients, c => Assert.Equal(29, c.viewMan.Snapshot().Count(v => v.GoID == 12 && v.viewCreated)));

        clients[0].playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(100000, 495, 100000));
        WorldThreads.CheckPlayerViews(clients, WorldThreads.BuildClientGrids(clients), WorldThreads.BuildClientByEntity(clients));
        Assert.All(clients.Skip(1), c => Assert.Null(c.viewMan.FindViewForEntity(clients[0].playerData.getEntityId())));
        Assert.Empty(clients[0].viewMan.Snapshot().Where(v => v.GoID == 12));
    }

    private static void Pass(List<WorldClient> clients) =>
        WorldThreads.CheckPlayerViews(clients, WorldThreads.BuildClientGrids(clients), WorldThreads.BuildClientByEntity(clients));

    [Fact]
    public void StillPlayersAreSkippedButLeaversStillLoseTheirViews()
    {
        var clients = Steady(20);
        Pass(clients); Pass(clients);
        int skipped = WorldThreads.SkippedPlayerEvaluations;
        Pass(clients);
        Assert.Equal(20, WorldThreads.SkippedPlayerEvaluations - skipped); // nobody moved: nothing re-evaluated
        Assert.Equal(0, Queued(clients));

        // One far move: the mover and everyone who had it nearby are evaluated, and its views go.
        clients[3].playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(100000, 495, 100000));
        skipped = WorldThreads.SkippedPlayerEvaluations;
        Pass(clients);
        Assert.Equal(0, WorldThreads.SkippedPlayerEvaluations - skipped);
        Assert.All(clients.Where((c, i) => i != 3), c => Assert.Null(c.viewMan.FindViewForEntity(clients[3].playerData.getEntityId())));

        // Leaving the world takes a player out of the grid; the others still drop it on the next pass.
        Pass(clients);
        clients[5].playerData.setOnWorld(false);
        Pass(clients);
        Assert.All(clients.Where((c, i) => i != 5 && i != 3), c => Assert.Null(c.viewMan.FindViewForEntity(clients[5].playerData.getEntityId())));

        // Nobody moves now: everyone left is skipped, even the two whose views of the leavers just went.
        Pass(clients);
        skipped = WorldThreads.SkippedPlayerEvaluations;
        Pass(clients);
        Assert.Equal(19, WorldThreads.SkippedPlayerEvaluations - skipped);
    }

    [Fact]
    public void IsInCircleAndGridCollectAndBatchReconcileMatchExpectedSemantics()
    {
        var maths = new Maths();
        Assert.True(maths.IsInCircle(17000f, 2500f, 17000f, 7500f, 5000f));
        Assert.False(maths.IsInCircle(17000f, 2500f, 17000f, 7501f, 5000f));
        Assert.True(maths.IsInCircle(-60000f, 60000f, -57000f, 64000f, 5000f));
        Assert.False(maths.IsInCircle(-60000f, 60000f, -56999f, 64000f, 5000f));

        var grid = new SpatialGrid<int>(5000f);
        grid.Add(0f, 0f, 1);
        grid.Add(4900f, 4900f, 2);
        grid.Add(5100f, 5100f, 3);
        grid.Add(25000f, 25000f, 4);
        var collected = new List<List<int>>();
        grid.CollectNeighborCells(2500f, 2500f, 5000f, collected);
        var viaYield = grid.GetNeighborCells(2500f, 2500f, 5000f).SelectMany(c => c).OrderBy(x => x).ToList();
        var viaCollect = collected.SelectMany(c => c).OrderBy(x => x).ToList();
        Assert.Equal(viaYield, viaCollect);
        Assert.Equal(new[] { 1, 2, 3 }, viaCollect);

        var viewer = Player(0);
        var nearbyA = Player(1);
        var nearbyB = Player(2);
        var stalePlayer = Player(3);
        viewer.viewMan.addView(nearbyA.playerData.getEntityId(), 12).viewCreated = true;
        viewer.viewMan.addView(stalePlayer.playerData.getEntityId(), 12).viewCreated = true;
        viewer.viewMan.addView(999999UL, HardlineBooths.PhoneBoothType).viewCreated = true;

        var nearbyClients = new List<WorldClient> { nearbyA, nearbyB };
        var nearbyEntities = new List<ulong> { nearbyA.playerData.getEntityId(), nearbyB.playerData.getEntityId() };
        var nearbyGoids = new List<ushort> { 12, 12 };
        var nearbySet = new HashSet<ulong>(nearbyEntities);
        var toSpawn = new List<(WorldClient Other, ClientView View)>();
        var toDelete = new List<ClientView>();

        long ver = viewer.viewMan.ReconcilePlayerViews(
            nearbyClients, nearbyEntities, nearbyGoids, nearbySet, 12, toSpawn, toDelete);
        Assert.Equal(viewer.viewMan.Version, ver);
        Assert.Single(toSpawn);
        Assert.Same(nearbyB, toSpawn[0].Other);
        Assert.Single(toDelete);
        Assert.Equal(stalePlayer.playerData.getEntityId(), toDelete[0].entityId);

        // Non-player booth view stays intact in ReconcilePlayerViews and is caught by CollectStaleViewsByGoId.
        var staleBooths = new List<ClientView>();
        viewer.viewMan.CollectStaleViewsByGoId(HardlineBooths.PhoneBoothType, new HashSet<ulong>(), staleBooths);
        Assert.Single(staleBooths);
        Assert.Equal(999999UL, staleBooths[0].entityId);
    }

    [Fact]
    public void DensePassTimings()
    {
        if (Environment.GetEnvironmentVariable("MXO_VISIBILITY_BENCH") != "1") return;
        int count = int.TryParse(Environment.GetEnvironmentVariable("MXO_VISIBILITY_BENCH_CLIENTS"), out int n) ? n : 200;
        var clients = Steady(count);
        var g = WorldThreads.BuildClientGrids(clients)[1];
        double cx = 0, cy = 0, cz = 0; NumericalUtils.LtVector3dToDoubles(clients[0].playerInstance.Position.getValue(), ref cx, ref cy, ref cz);
        output.WriteLine($"goid={BitConverter.ToString(clients[0].playerInstance.GetGoid())} pos={cx},{cy},{cz} neighbours={g.GetNeighborCells((float)cx,(float)cz,5000f).Sum(c=>c.Count)} contacts={MissionContactNpcs.All.Count} views={clients[0].viewMan.Count}");
        var player = new List<double>(); var contacts = new List<double>();
        for (int pass = 0; pass < 60; pass++)
        {
            for (int i = 0; i < clients.Count; i++) Place(clients[i], i, pass * .1);
            var clock = Stopwatch.StartNew();
            WorldThreads.CheckPlayerViews(clients, WorldThreads.BuildClientGrids(clients), WorldThreads.BuildClientByEntity(clients));
            double a = clock.Elapsed.TotalMilliseconds; clock.Restart();
            MissionContactNpcs.CheckViews(clients);
            double b = clock.Elapsed.TotalMilliseconds;
            if (pass >= 10) { player.Add(a); contacts.Add(b); }
        }
        var still = new List<double>();
        for (int pass = 0; pass < 30; pass++) { var clock = Stopwatch.StartNew(); Pass(clients); if (pass >= 5) still.Add(clock.Elapsed.TotalMilliseconds); }
        output.WriteLine($"[VISIBILITY BENCH] still player-views mean={still.Average():F3} max={still.Max():F3} ms");
        Assert.Equal(0, Queued(clients));
        string line = $"[VISIBILITY BENCH] clients={count} player-views mean={player.Average():F2} max={player.Max():F2} ms; " +
                      $"contacts mean={contacts.Average():F2} max={contacts.Max():F2} ms";
        output.WriteLine(line);
        var path = Environment.GetEnvironmentVariable("MXO_VISIBILITY_BENCH_OUT");
        if (!string.IsNullOrEmpty(path)) File.AppendAllText(path, line + Environment.NewLine);
    }
}
