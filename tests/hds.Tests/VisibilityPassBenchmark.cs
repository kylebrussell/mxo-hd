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
    public void LockFreeFindViewForEntityAndPartitionedIndexesStayConsistent()
    {
        var vm = new ViewManager();
        var playerView = vm.addView(100UL, 12);
        playerView.viewCreated = true;
        var boothView = vm.addView(200UL, HardlineBooths.PhoneBoothType);
        boothView.viewCreated = true;
        var dmView = vm.addView(982000000042UL, 607);
        dmView.viewCreated = true;

        // First-match semantics for duplicate entity entries + lock-free FindViewForEntity parity.
        var secondForEntity = new ClientView(599, 900, 200UL) { viewCreated = true };
        vm.views.Add(secondForEntity);
        Assert.Same(boothView, vm.FindViewForEntity(200UL));
        Assert.Same(playerView, vm.FindViewForEntity(100UL));

        // NonPlayerViews excludes GoID 12 and includes DataMining entity range.
        var dmList = new List<ClientView>();
        vm.CollectNonPlayerViewsInEntityRange(982000000000UL, 983000000000UL, dmList);
        Assert.Single(dmList);
        Assert.Same(dmView, dmList[0]);

        // Removing the first view for entity 200 promotes the second view in lock-free lookup and collapses multiByEntity.
        Assert.True(vm.views.HasMultiEntity);
        vm.removeViewByViewId(boothView.ViewID);
        Assert.False(vm.views.HasMultiEntity);
        Assert.Same(secondForEntity, vm.FindViewForEntity(200UL));
        vm.removeViewByViewId(secondForEntity.ViewID);
        Assert.Null(vm.FindViewForEntity(200UL));

        // Concurrent FindViewForEntity reads while views are added and removed must never throw.
        using var stop = new CancellationTokenSource();
        var reader = Task.Run(() =>
        {
            while (!stop.IsCancellationRequested)
            {
                _ = vm.FindViewForEntity(100UL);
                _ = vm.FindViewForEntity(500UL);
            }
        });
        for (int i = 0; i < 500; i++)
        {
            var added = vm.addView(500UL, 599);
            added.viewCreated = true;
            Assert.Same(added, vm.FindViewForEntity(500UL));
            vm.removeViewByViewId(added.ViewID);
        }
        stop.Cancel();
        reader.Wait();

        vm.Clear();
        Assert.Null(vm.FindViewForEntity(100UL));
        vm.CollectNonPlayerViewsInEntityRange(982000000000UL, 983000000000UL, dmList);
        Assert.Empty(dmList);
    }

    [Fact]
    public void StampBasedReconcileMobViewsRetainsInRangeCorpseAndDeletesOutOfRangeOrOtherDistrict()
    {
        var viewer = Player(0);
        viewer.viewMan.addView(700001UL, 12).viewCreated = true; // player view (ignored)

        var corpseMob = new Mob(); corpseMob.setEntityId(800001UL); corpseMob.setDistrict(1); corpseMob.setGoId(new byte[] { 0x00, 0x57, 0x02, 0x00 });
        var freshMob = new Mob(); freshMob.setEntityId(800002UL); freshMob.setDistrict(1); freshMob.setGoId(new byte[] { 0x00, 0x57, 0x02, 0x00 });
        var farMob = new Mob(); farMob.setEntityId(800003UL); farMob.setDistrict(1); farMob.setGoId(new byte[] { 0x00, 0x57, 0x02, 0x00 });
        var otherDistrictMob = new Mob(); otherDistrictMob.setEntityId(800004UL); otherDistrictMob.setDistrict(2); otherDistrictMob.setGoId(new byte[] { 0x00, 0x57, 0x02, 0x00 });

        viewer.viewMan.addView(corpseMob.getEntityId(), 599).viewCreated = true;
        viewer.viewMan.addView(farMob.getEntityId(), 599).viewCreated = true;
        viewer.viewMan.addView(otherDistrictMob.getEntityId(), 599).viewCreated = true;

        var mobsByEntity = new Dictionary<ulong, Mob>
        {
            [corpseMob.getEntityId()] = corpseMob,
            [freshMob.getEntityId()] = freshMob,
            [farMob.getEntityId()] = farMob,
            [otherDistrictMob.getEntityId()] = otherDistrictMob,
        };
        // corpseMob is nearby but NOT introducible (already spawned corpse); freshMob is both nearby and introducible.
        var nonIntroducible = new List<Mob> { corpseMob };
        var introducible = new List<Mob> { freshMob };
        var toSpawn = new List<(Mob Mob, ClientView View)>();
        var pets = new List<(Mob Mob, ClientView View)>();
        var toDelete = new List<(ClientView View, Mob Mob)>();

        viewer.viewMan.ReconcileMobViews(1, nonIntroducible, introducible, mobsByEntity, toSpawn, pets, toDelete);

        Assert.Single(toSpawn);
        Assert.Same(freshMob, toSpawn[0].Mob);
        Assert.Empty(pets);
        Assert.Equal(2, toDelete.Count);
        Assert.Contains(toDelete, d => ReferenceEquals(d.Mob, farMob));
        Assert.Contains(toDelete, d => ReferenceEquals(d.Mob, otherDistrictMob));
        Assert.DoesNotContain(toDelete, d => ReferenceEquals(d.Mob, corpseMob));
    }

    [Fact]
    public void SpatialGridIncrementalCellMoveAndClearPreserveNeighborQueries()
    {
        var grid = new SpatialGrid<string>(5000f);
        long cellA = grid.GetCellKey(100f, 100f);
        long cellB = grid.GetCellKey(25000f, 25000f);
        grid.AddToCell(cellA, "m1");
        grid.AddToCell(cellA, "m2");

        var cells = new List<List<string>>();
        grid.CollectNeighborCells(100f, 100f, 5000f, cells);
        Assert.Equal(new[] { "m1", "m2" }, cells.SelectMany(c => c).OrderBy(x => x).ToArray());

        // Move m1 from cellA to cellB incrementally.
        Assert.True(grid.RemoveFromCell(cellA, "m1"));
        grid.AddToCell(cellB, "m1");

        grid.CollectNeighborCells(100f, 100f, 5000f, cells);
        Assert.Equal(new[] { "m2" }, cells.SelectMany(c => c).ToArray());
        grid.CollectNeighborCells(25000f, 25000f, 5000f, cells);
        Assert.Equal(new[] { "m1" }, cells.SelectMany(c => c).ToArray());

        // Clear empties all buckets and CollectNeighborCells skips empty lists.
        grid.Clear();
        grid.CollectNeighborCells(100f, 100f, 5000f, cells);
        Assert.Empty(cells);
    }

    [Fact]
    public void FlushClientBatchHandlesSequentialParallelAndFaultIsolation()
    {
        // Build 24 bot clients (exceeding ParallelFlushThreshold = 16) plus a null slot and a broken client
        // with null messageQueue whose TickFlush throws: every healthy bot must still drain its queued messages.
        var isBotField = typeof(WorldClient).GetField("<IsBot>k__BackingField",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var clients = new List<WorldClient>();
        for (int i = 0; i < 24; i++)
        {
            var bot = Player(i);
            isBotField.SetValue(bot, true);
            bot.messageQueue.addObjectMessage(new byte[] { 0x01, 0x02 }, false);
            clients.Add(bot);
        }
        var broken = (WorldClient)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        clients.Insert(0, null!);
        clients.Insert(5, broken);

        // Parallel path (>= 16 clients)
        WorldThreads.FlushClientBatch(clients);
        Assert.All(clients.Where(c => c?.messageQueue != null),
            c => Assert.Empty(c.messageQueue.ObjectMessagesQueue));

        // Sequential path (< 16 clients)
        var small = clients.Where(c => c?.messageQueue != null).Take(4).ToList();
        foreach (var c in small)
        {
            c.messageQueue.addObjectMessage(new byte[] { 0x03, 0x04 }, false);
        }
        small.Insert(1, broken);
        WorldThreads.FlushClientBatch(small);
        Assert.All(small.Where(c => c?.messageQueue != null),
            c => Assert.Empty(c.messageQueue.ObjectMessagesQueue));
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
