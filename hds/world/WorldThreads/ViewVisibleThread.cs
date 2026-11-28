using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using hds.shared;
using hds.world.Structures;

namespace hds
{
    public partial class WorldThreads
    {
        private const float ViewRange = 5000f;
        private const ushort SubwayTypeId = 6568;
        private const int ParallelVisibilityClientThreshold = 32;
        private static readonly ParallelOptions VisibilityParallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = Math.Min(4, Math.Max(2, Environment.ProcessorCount / 2))
        };
        private static readonly object StaticIndexLock = new object();
        private static readonly object SubwayIndexLock = new object();
        private static Dictionary<ushort, SpatialGrid<StaticWorldObject>> staticObjectsByDistrict;
        private static HashSet<uint> signpostStaticIds;
        private static Dictionary<ushort, SpatialGrid<Subway>> subwaysByDistrict;
        private static int subwayIndexCount;

        // Compact value-type record so CheckPlayerMobViews checks spawn/distance/introducibility from a
        // contiguous array without chasing scattered Gen2 Mob objects for out-of-range neighbors.
        internal struct MobEntry
        {
            public Mob Mob;
            public float X;
            public float Z;
            public ushort District;
            public long CellKey;
            public int VisibilityVersion;
            public bool Spawned;
            public bool CanIntroduce;
        }

        // Single-threaded cache owned by ViewVisibleThread so the ~7,000 world mobs are not copied and
        // re-bucketed into fresh dictionaries/lists every 500 ms pass when only a handful wander.
        internal sealed class MobVisibilityCache
        {
            public readonly List<Mob> Mobs = new List<Mob>();
            public MobEntry[] Entries = Array.Empty<MobEntry>();
            public readonly Dictionary<ulong, Mob> ByEntity = new Dictionary<ulong, Mob>();
            public readonly Dictionary<ushort, SpatialGrid<Mob>> Grids = new Dictionary<ushort, SpatialGrid<Mob>>();
            public readonly Dictionary<ushort, SpatialGrid<int>> IndexGrids = new Dictionary<ushort, SpatialGrid<int>>();
            private long lastGlobalVisibilityVersion = -1;

            public void UpdateFromWorld()
            {
                bool membershipChanged = false;
                lock (WorldServer.mobs.SyncRoot)
                {
                    int worldCount = WorldServer.mobs.Count;
                    if (worldCount != Mobs.Count)
                    {
                        membershipChanged = true;
                    }
                    else
                    {
                        for (int i = 0; i < worldCount; i++)
                        {
                            if (!ReferenceEquals(WorldServer.mobs[i], Mobs[i]))
                            {
                                membershipChanged = true;
                                break;
                            }
                        }
                    }

                    if (membershipChanged)
                    {
                        Mobs.Clear();
                        if (Mobs.Capacity < worldCount) Mobs.Capacity = worldCount;
                        for (int i = 0; i < worldCount; i++)
                        {
                            Mobs.Add(WorldServer.mobs[i] as Mob);
                        }
                    }
                }

                if (membershipChanged)
                {
                    Rebuild();
                    return;
                }

                // O(1) fast path when membership is unchanged and no mob moved, died, respawned, or switched district.
                long globalVer = Volatile.Read(ref Mob.AnyVisibilityVersion);
                if (globalVer == lastGlobalVisibilityVersion)
                {
                    return;
                }

                // Partial dirty scan: skip the ~6,970 untouched mobs in a single integer compare and refresh only
                // mobs whose VisibilityVersion changed since the last pass.
                MobEntry[] entries = Entries;
                int count = Mobs.Count;
                for (int i = 0; i < count; i++)
                {
                    Mob mob = Mobs[i];
                    if (mob == null) continue;
                    ref MobEntry e = ref entries[i];
                    int mobVer = Volatile.Read(ref mob.VisibilityVersion);
                    if (e.VisibilityVersion == mobVer) continue;

                    float x = (float)mob.getXPos();
                    float z = (float)mob.getZPos();
                    ushort district = mob.getDistrict();
                    e.X = x;
                    e.Z = z;
                    e.Spawned = mob.getIsSpawned();
                    e.CanIntroduce = DeathPresentation.CanIntroduceNpc(mob);
                    e.VisibilityVersion = mobVer;
                    SpatialGrid<Mob> grid = GetOrCreateGrid(district);
                    long cellKey = grid.GetCellKey(x, z);
                    if (e.District != district || e.CellKey != cellKey)
                    {
                        if (Grids.TryGetValue(e.District, out SpatialGrid<Mob> oldGrid))
                        {
                            oldGrid.RemoveFromCell(e.CellKey, mob);
                        }
                        if (IndexGrids.TryGetValue(e.District, out SpatialGrid<int> oldIndexGrid))
                        {
                            oldIndexGrid.RemoveFromCell(e.CellKey, i);
                        }
                        grid.AddToCell(cellKey, mob);
                        GetOrCreateIndexGrid(district).AddToCell(cellKey, i);
                        e.District = district;
                        e.CellKey = cellKey;
                    }
                }
                lastGlobalVisibilityVersion = globalVer;
            }

            private SpatialGrid<Mob> GetOrCreateGrid(ushort district)
            {
                if (!Grids.TryGetValue(district, out SpatialGrid<Mob> grid))
                {
                    grid = new SpatialGrid<Mob>(ViewRange);
                    Grids.Add(district, grid);
                }
                return grid;
            }

            private SpatialGrid<int> GetOrCreateIndexGrid(ushort district)
            {
                if (!IndexGrids.TryGetValue(district, out SpatialGrid<int> grid))
                {
                    grid = new SpatialGrid<int>(ViewRange);
                    IndexGrids.Add(district, grid);
                }
                return grid;
            }

            private void Rebuild()
            {
                long globalVer = Volatile.Read(ref Mob.AnyVisibilityVersion);
                ByEntity.Clear();
                foreach (SpatialGrid<Mob> g in Grids.Values) g.Clear();
                foreach (SpatialGrid<int> g in IndexGrids.Values) g.Clear();
                if (Entries.Length < Mobs.Count)
                {
                    Entries = new MobEntry[Mobs.Count];
                }

                MobEntry[] entries = Entries;
                for (int i = 0; i < Mobs.Count; i++)
                {
                    Mob mob = Mobs[i];
                    if (mob == null)
                    {
                        entries[i] = default;
                        continue;
                    }

                    int mobVer = Volatile.Read(ref mob.VisibilityVersion);
                    ByEntity[mob.getEntityId()] = mob;
                    float x = (float)mob.getXPos();
                    float z = (float)mob.getZPos();
                    ushort district = mob.getDistrict();
                    SpatialGrid<Mob> grid = GetOrCreateGrid(district);
                    long cellKey = grid.GetCellKey(x, z);
                    grid.AddToCell(cellKey, mob);
                    GetOrCreateIndexGrid(district).AddToCell(cellKey, i);
                    entries[i] = new MobEntry
                    {
                        Mob = mob,
                        X = x,
                        Z = z,
                        District = district,
                        CellKey = cellKey,
                        VisibilityVersion = mobVer,
                        Spawned = mob.getIsSpawned(),
                        CanIntroduce = DeathPresentation.CanIntroduceNpc(mob)
                    };
                }
                lastGlobalVisibilityVersion = globalVer;
            }
        }

        public void ViewVisibleThread()
        {
            Output.WriteLine("[WORLD SERVER]View Visible Thread started");
            MobVisibilityCache mobCache = new MobVisibilityCache();
            List<WorldClient> clients = new List<WorldClient>();
            List<WorldClient> postPassClients = new List<WorldClient>();
            List<Subway> subways = new List<Subway>();
            List<WorldClient> deadClients = new List<WorldClient>();
            List<WorldClient> aliveClients = new List<WorldClient>();
            Dictionary<uint, SpatialGrid<WorldClient>> clientGrids = new Dictionary<uint, SpatialGrid<WorldClient>>();
            Dictionary<ulong, WorldClient> clientsByEntity = new Dictionary<ulong, WorldClient>();
            while (true)
            {
              long scaleStart=ScaleDiagnostics.Start;
              // An exception escaping this loop ends the thread and the server; log it and try again next pass.
              try
              {
                long stage=scaleStart;
                SnapshotClientsInto(clients);
                mobCache.UpdateFromWorld();
                SnapshotSubwaysInto(subways);

                deadClients.Clear();
                for (int i = 0; i < clients.Count; i++)
                {
                    WorldClient client = clients[i];
                    if (client != null && client.Alive == false)
                    {
                        deadClients.Add(client);
                    }
                }

                if (deadClients.Count > 0)
                {
                    CleanDeadPlayers(deadClients, clients);
                }

                aliveClients.Clear();
                for (int i = 0; i < clients.Count; i++)
                {
                    WorldClient client = clients[i];
                    if (client != null && client.Alive)
                    {
                        aliveClients.Add(client);
                    }
                }

                PopulateClientGrids(aliveClients, clientGrids);
                PopulateClientByEntity(aliveClients, clientsByEntity);
                Dictionary<ushort, SpatialGrid<Mob>> mobGrids = mobCache.Grids;
                Dictionary<ulong, Mob> mobsByEntity = mobCache.ByEntity;
                stage=ScaleDiagnostics.Stage("snapshot",stage);

                CheckPlayerViews(aliveClients, clientGrids, clientsByEntity);
                stage=ScaleDiagnostics.Stage("player-views",stage);
                CheckPlayerMobViews(aliveClients, mobCache);
                stage=ScaleDiagnostics.Stage("mob-views",stage);
                MobMotion.Tick(aliveClients,mobsByEntity,TimeUtils.GetSimTimeSeconds());
                stage=ScaleDiagnostics.Stage("mob-motion",stage);
                CombatPresentation.CheckObservers();
                StealthShieldAbilities.CheckObservers();
                foreach (WorldClient client in aliveClients) DeathPenalty.Tick(client);
                foreach (WorldClient client in aliveClients) DataMining.Check(client);
                foreach (WorldClient client in aliveClients) CraftingSession.Check(client);
                stage=ScaleDiagnostics.Stage("per-player",stage);
                MobAggro.Check(aliveClients, mobGrids);
                stage=ScaleDiagnostics.Stage("aggro",stage);
                CheckForStaticSubways(aliveClients, subways);
                stage=ScaleDiagnostics.Stage("subways",stage);
                CheckForStaticObjectsViewsInRange(aliveClients);
                stage=ScaleDiagnostics.Stage("static-objects",stage);
                MissionContactNpcs.CheckViews(aliveClients);
                stage=ScaleDiagnostics.Stage("contacts",stage);
                foreach (WorldClient client in aliveClients) SafeFloorProbe.Check(client);
                foreach (WorldClient client in aliveClients) PosterBoxFloorProbe.Check(client);
                stage=ScaleDiagnostics.Stage("floor-probes",stage);
                foreach (WorldClient client in aliveClients) PvpArchives.Check(client, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()/1000.0);
                ScaleDiagnostics.Stage("pvp-archives",stage);
              }
              catch (Exception ex)
              {
                Output.WriteLine("[VIEWS] Visibility pass failed: " + ex);
              }
                finally {if(scaleStart!=0){SnapshotClientsInto(postPassClients);ScaleDiagnostics.VisibilityCompleted(scaleStart,postPassClients);}}
                Thread.Sleep(500);
            }
        }

        private static void CheckForServerEntites()
        {
            // This can later replace ALL Methods 
            lock (WorldServer.gameServerEntities.SyncRoot)
            {
                foreach (var serverEntity in WorldServer.gameServerEntities)
                {
                    lock (WorldServer.Clients)
                    {
                        foreach (var clientKey in WorldServer.Clients.Keys)
                        {
                            WorldClient thisclient = WorldServer.Clients[clientKey] as WorldClient;
                            if (thisclient != null)
                            {
                                // ToDo: Server Entity doesnt match a real rule currently so we need a class or something
                                //ClientView clientEntityView = thisclient.viewMan.getViewForEntityAndGo(serverEntity, NumericalUtils.ByteArrayToUint16(thismob.getGoId(), 1));
                            }
                        }
                    }
                }
            }
        }

        private static void SnapshotClientsInto(List<WorldClient> destination)
        {
            destination.Clear();
            lock (WorldServer.Clients)
            {
                if (destination.Capacity < WorldServer.Clients.Count)
                {
                    destination.Capacity = WorldServer.Clients.Count;
                }
                foreach (WorldClient client in WorldServer.Clients.Values)
                {
                    destination.Add(client);
                }
            }
        }

        private static List<WorldClient> SnapshotClients()
        {
            List<WorldClient> copy = new List<WorldClient>();
            SnapshotClientsInto(copy);
            return copy;
        }

        private static List<Mob> SnapshotMobs()
        {
            lock (WorldServer.mobs.SyncRoot)
            {
                List<Mob> mobs = new List<Mob>(WorldServer.mobs.Count);
                for (int i = 0; i < WorldServer.mobs.Count; i++)
                {
                    if (WorldServer.mobs[i] is Mob castMob)
                    {
                        mobs.Add(castMob);
                    }
                }
                return mobs;
            }
        }

        private static void SnapshotSubwaysInto(List<Subway> destination)
        {
            lock (WorldServer.subways.SyncRoot)
            {
                int worldCount = WorldServer.subways.Count;
                if (worldCount == destination.Count)
                {
                    bool same = true;
                    for (int i = 0; i < worldCount; i++)
                    {
                        if (!ReferenceEquals(WorldServer.subways[i], destination[i]))
                        {
                            same = false;
                            break;
                        }
                    }
                    if (same) return;
                }

                destination.Clear();
                if (destination.Capacity < worldCount) destination.Capacity = worldCount;
                for (int i = 0; i < worldCount; i++)
                {
                    Subway castSubway = WorldServer.subways[i] as Subway;
                    if (castSubway != null)
                    {
                        destination.Add(castSubway);
                    }
                }
            }
        }

        private static List<Subway> SnapshotSubways()
        {
            List<Subway> subways = new List<Subway>();
            SnapshotSubwaysInto(subways);
            return subways;
        }

        internal static Dictionary<uint, SpatialGrid<WorldClient>> BuildClientGrids(List<WorldClient> clients)
        {
            Dictionary<uint, SpatialGrid<WorldClient>> grids = new Dictionary<uint, SpatialGrid<WorldClient>>();
            PopulateClientGrids(clients, grids);
            return grids;
        }

        private static void PopulateClientGrids(List<WorldClient> clients, Dictionary<uint, SpatialGrid<WorldClient>> grids)
        {
            foreach (SpatialGrid<WorldClient> g in grids.Values) g.Clear();
            for (int i = 0; i < clients.Count; i++)
            {
                WorldClient client = clients[i];
                if (client == null || client.playerData.getOnWorld() == false || client.playerData.waitForRPCShutDown)
                {
                    continue;
                }

                uint districtId = client.playerData.getDistrictId();
                if (!grids.TryGetValue(districtId, out SpatialGrid<WorldClient> grid))
                {
                    grid = new SpatialGrid<WorldClient>(ViewRange);
                    grids.Add(districtId, grid);
                }

                double x = 0;
                double y = 0;
                double z = 0;
                NumericalUtils.LtVector3dToDoubles(client.playerInstance.Position.getValue(), ref x, ref y, ref z);
                grid.Add((float)x, (float)z, client);
            }
        }

        private static Dictionary<ushort, SpatialGrid<Mob>> BuildMobGrids(List<Mob> mobs)
        {
            Dictionary<ushort, SpatialGrid<Mob>> grids = new Dictionary<ushort, SpatialGrid<Mob>>();
            foreach (Mob mob in mobs)
            {
                if (mob == null)
                {
                    continue;
                }

                ushort districtId = mob.getDistrict();
                if (!grids.TryGetValue(districtId, out SpatialGrid<Mob> grid))
                {
                    grid = new SpatialGrid<Mob>(ViewRange);
                    grids.Add(districtId, grid);
                }

                grid.Add((float)mob.getXPos(), (float)mob.getZPos(), mob);
            }

            return grids;
        }

        internal static Dictionary<ulong, WorldClient> BuildClientByEntity(List<WorldClient> clients)
        {
            Dictionary<ulong, WorldClient> lookup = new Dictionary<ulong, WorldClient>(clients.Count);
            PopulateClientByEntity(clients, lookup);
            return lookup;
        }

        private static void PopulateClientByEntity(List<WorldClient> clients, Dictionary<ulong, WorldClient> lookup)
        {
            lookup.Clear();
            for (int i = 0; i < clients.Count; i++)
            {
                WorldClient client = clients[i];
                if (client == null)
                {
                    continue;
                }

                lookup[client.playerData.getEntityId()] = client;
            }
        }

        private static Dictionary<ulong, Mob> BuildMobByEntity(List<Mob> mobs)
        {
            Dictionary<ulong, Mob> lookup = new Dictionary<ulong, Mob>(mobs.Count);
            for (int i = 0; i < mobs.Count; i++)
            {
                Mob mob = mobs[i];
                if (mob == null)
                {
                    continue;
                }

                lookup[mob.getEntityId()] = mob;
            }

            return lookup;
        }

        private static void EnsureStaticObjectIndex()
        {
            if (staticObjectsByDistrict != null)
            {
                return;
            }

            lock (StaticIndexLock)
            {
                if (staticObjectsByDistrict != null)
                {
                    return;
                }

                Dictionary<ushort, SpatialGrid<StaticWorldObject>> index = new Dictionary<ushort, SpatialGrid<StaticWorldObject>>();
                HashSet<uint> signposts = new HashSet<uint>();

                DataLoader loader = DataLoader.getInstance();
                foreach (NPC_Singpost signpost in loader.Signposts)
                {
                    signposts.Add(signpost.mxoStaticId);
                }

                // The visibility pass acts only on hardline booths and listed signposts (CheckForStaticObjectsViewsInRange),
                // so the index holds only those: scanning every static object near every player dominated the pass.
                foreach (StaticWorldObject worldObject in loader.WorldObjectsDB)
                {
                    if (!InStaticVisibilityIndex(worldObject, signposts))
                    {
                        continue;
                    }

                    ushort districtId = worldObject.metrId;
                    if (!index.TryGetValue(districtId, out SpatialGrid<StaticWorldObject> grid))
                    {
                        grid = new SpatialGrid<StaticWorldObject>(ViewRange);
                        index.Add(districtId, grid);
                    }

                    grid.Add((float)worldObject.pos_x, (float)worldObject.pos_z, worldObject);
                }

                staticObjectsByDistrict = index;
                signpostStaticIds = signposts;
            }
        }

        internal static bool InStaticVisibilityIndex(StaticWorldObject worldObject, HashSet<uint> signposts)
        {
            UInt16 typeId = NumericalUtils.ByteArrayToUint16(worldObject.type, 1);
            return typeId == HardlineBooths.PhoneBoothType || typeId == 8400 && signposts.Contains(worldObject.staticId);
        }

        private static void EnsureSubwayIndex(List<Subway> subways)
        {
            if (subwaysByDistrict != null && subwayIndexCount == subways.Count)
            {
                return;
            }

            lock (SubwayIndexLock)
            {
                if (subwaysByDistrict != null && subwayIndexCount == subways.Count)
                {
                    return;
                }

                Dictionary<ushort, SpatialGrid<Subway>> index = new Dictionary<ushort, SpatialGrid<Subway>>();

                foreach (Subway subway in subways)
                {
                    if (subway == null)
                    {
                        continue;
                    }

                    ushort districtId = subway.worldObject.metrId;
                    if (!index.TryGetValue(districtId, out SpatialGrid<Subway> grid))
                    {
                        grid = new SpatialGrid<Subway>(ViewRange);
                        index.Add(districtId, grid);
                    }

                    grid.Add((float)subway.worldObject.pos_x, (float)subway.worldObject.pos_z, subway);
                }

                subwaysByDistrict = index;
                subwayIndexCount = subways.Count;
            }
        }

        private static UInt64 GetSubwayEntityId(Subway subway) => Subway.EntityId(subway.worldObject);

        [ThreadStatic] private static HashSet<ulong> subwayNearbyScratch;
        [ThreadStatic] private static List<List<Subway>> subwayNeighborScratch;
        [ThreadStatic] private static List<ClientView> subwayDeleteScratch;
        [ThreadStatic] private static HashSet<ulong> staticBoothNearbyScratch;
        [ThreadStatic] private static List<List<StaticWorldObject>> staticNeighborScratch;
        [ThreadStatic] private static List<ClientView> staticDeleteScratch;

        public static void CheckForStaticSubways(List<WorldClient> clients, List<Subway> subways)
        {
            EnsureSubwayIndex(subways);
            if (subwaysByDistrict == null)
            {
                return;
            }

            const double viewRangeSq = (double)ViewRange * ViewRange;
            HashSet<ulong> nearbySubwayEntities = subwayNearbyScratch ?? (subwayNearbyScratch = new HashSet<ulong>());
            List<List<Subway>> neighborCells = subwayNeighborScratch ?? (subwayNeighborScratch = new List<List<Subway>>(9));
            List<ClientView> viewsToDelete = subwayDeleteScratch ?? (subwayDeleteScratch = new List<ClientView>());
            try
            {
            foreach (WorldClient thisclient in clients)
            {
                if (thisclient == null || thisclient.Alive == false)
                {
                    continue;
                }

                if (thisclient.playerData.getOnWorld() == false ||
                    thisclient.playerData.waitForRPCShutDown)
                {
                    continue;
                }

                ushort districtId = (ushort)thisclient.playerData.getDistrictId();
                if (!subwaysByDistrict.TryGetValue(districtId, out SpatialGrid<Subway> grid))
                {
                    continue;
                }

                double playerX = 0;
                double playerY = 0;
                double playerZ = 0;
                NumericalUtils.LtVector3dToDoubles(thisclient.playerInstance.Position.getValue(),
                    ref playerX, ref playerY, ref playerZ);

                nearbySubwayEntities.Clear();
                grid.CollectNeighborCells((float)playerX, (float)playerZ, ViewRange, neighborCells);
                for (int c = 0; c < neighborCells.Count; c++)
                {
                    List<Subway> cell = neighborCells[c];
                    for (int s = 0; s < cell.Count; s++)
                    {
                        Subway thisSubway = cell[s];
                        if (thisSubway == null)
                        {
                            continue;
                        }

                        double dx = (double)(float)playerX - (float)thisSubway.worldObject.pos_x;
                        double dz = (double)(float)playerZ - (float)thisSubway.worldObject.pos_z;
                        if (dx * dx + dz * dz > viewRangeSq)
                        {
                            continue;
                        }

                        UInt64 entityStaticId = GetSubwayEntityId(thisSubway);
                        nearbySubwayEntities.Add(entityStaticId);

                        ClientView view = thisclient.viewMan.FindViewForEntity(entityStaticId);
                        if (view == null || !view.viewCreated)
                        {
                            view = thisclient.viewMan.GetViewForEntityAndGo(entityStaticId,
                                NumericalUtils.ByteArrayToUint16(thisSubway.worldObject.type, 1));
                            if (!view.viewCreated && thisclient.playerData.getOnWorld())
                            {
                                ServerPackets pak = new ServerPackets();
                                pak.SendSpawnGameObject(thisclient, thisSubway.gameObjectData, entityStaticId);
                                view.spawnId = thisclient.playerData.spawnViewUpdateCounter;
                                view.viewCreated = true;
                            }
                        }
                    }
                }

                thisclient.viewMan.CollectStaleViewsByGoId(SubwayTypeId, nearbySubwayEntities, viewsToDelete);
                for (int i = 0; i < viewsToDelete.Count; i++)
                {
                    ClientView view = viewsToDelete[i];
                    ServerPackets packets = new ServerPackets();
                    packets.sendDeleteViewPacket(thisclient, view.ViewID);
                    thisclient.viewMan.removeViewByViewId(view.ViewID);
                }
            }
            }
            finally
            {
                nearbySubwayEntities.Clear();
                neighborCells.Clear();
                viewsToDelete.Clear();
            }
        }

        private static void CheckForStaticObjectsViewsInRange(List<WorldClient> clients)
        {
            EnsureStaticObjectIndex();
            if (staticObjectsByDistrict == null)
            {
                return;
            }

            const double viewRangeSq = (double)ViewRange * ViewRange;
            HashSet<ulong> nearbyBooths = staticBoothNearbyScratch ?? (staticBoothNearbyScratch = new HashSet<ulong>());
            List<List<StaticWorldObject>> neighborCells = staticNeighborScratch ?? (staticNeighborScratch = new List<List<StaticWorldObject>>(9));
            List<ClientView> viewsToDelete = staticDeleteScratch ?? (staticDeleteScratch = new List<ClientView>());
            try
            {
            foreach (WorldClient thisclient in clients)
            {
                if (thisclient == null || thisclient.Alive == false)
                {
                    continue;
                }

                if (thisclient.playerData.getOnWorld() &&
                    thisclient.playerData.waitForRPCShutDown == false)
                {
                    double playerX = 0;
                    double playerY = 0;
                    double playerZ = 0;
                    NumericalUtils.LtVector3dToDoubles(thisclient.playerInstance.Position.getValue(), ref playerX,
                        ref playerY, ref playerZ);

                    ushort districtId = (ushort)thisclient.playerData.getDistrictId();
                    if (!staticObjectsByDistrict.TryGetValue(districtId, out SpatialGrid<StaticWorldObject> grid))
                    {
                        continue;
                    }

                    nearbyBooths.Clear();
                    grid.CollectNeighborCells((float)playerX, (float)playerZ, ViewRange, neighborCells);
                    for (int c = 0; c < neighborCells.Count; c++)
                    {
                        List<StaticWorldObject> cell = neighborCells[c];
                        for (int s = 0; s < cell.Count; s++)
                        {
                            StaticWorldObject staticWorldObject = cell[s];
                            double dx = (double)(float)playerX - (float)staticWorldObject.pos_x;
                            double dz = (double)(float)playerZ - (float)staticWorldObject.pos_z;
                            if (dx * dx + dz * dz > viewRangeSq)
                            {
                                continue;
                            }

                            UInt16 typeId = NumericalUtils.ByteArrayToUint16(staticWorldObject.type, 1);
                            // WE get all staticObjects in range but we dont just want them all
                            switch (typeId)
                            {
                                case HardlineBooths.PhoneBoothType:
                                    UInt64 boothEntity = HardlineBooths.EntityId(staticWorldObject);
                                    nearbyBooths.Add(boothEntity);
                                    ClientView boothView = thisclient.viewMan.FindViewForEntity(boothEntity);
                                    if (boothView == null || !boothView.viewCreated)
                                    {
                                        boothView = thisclient.viewMan.GetViewForEntityAndGo(boothEntity, typeId);
                                        if (!boothView.viewCreated)
                                        {
                                            boothView.staticObject = staticWorldObject;
                                            // SendSpawnGameObject takes the current spawn counter for the creation
                                            boothView.spawnId = thisclient.playerData.spawnViewUpdateCounter;
                                            new ServerPackets().SendSpawnGameObject(thisclient,
                                                HardlineBooths.BuildBoothObject(staticWorldObject), boothEntity);
                                        }
                                    }
                                    break;

                                case 8400:
                                    if (signpostStaticIds != null &&
                                        signpostStaticIds.Contains(staticWorldObject.staticId) == false)
                                    {
                                        break;
                                    }

                                    String entityHackString =
                                        "" + staticWorldObject.metrId + "" + staticWorldObject.mxoStaticId;
                                    UInt64 entityStaticId = UInt64.Parse(entityHackString);

                                    ClientView view = thisclient.viewMan.GetViewForEntityAndGo(entityStaticId,
                                        NumericalUtils.ByteArrayToUint16(staticWorldObject.type, 1));

                                    if (!view.viewCreated && thisclient.playerData.getOnWorld())
                                    {
                                        // ToDo: Refaktor ? 
                                        /*
                                        ServerPackets pak = new ServerPackets();
                                        pak.SendSpawnStaticObject(thisclient, thisSubway.gameObjectData, entityStaticId);
                                        view.spawnId = thisclient.playerData.spawnViewUpdateCounter;
                                        view.viewCreated = true;
                                        */
                                    }

                                    break;
                            }
                        }
                    }

                    // Booths that are out of range (or in another district) leave the client's view
                    thisclient.viewMan.CollectStaleViewsByGoId(HardlineBooths.PhoneBoothType, nearbyBooths, viewsToDelete);
                    for (int i = 0; i < viewsToDelete.Count; i++)
                    {
                        ClientView view = viewsToDelete[i];
                        new ServerPackets().sendDeleteViewPacket(thisclient, view.ViewID);
                        thisclient.viewMan.removeViewByViewId(view.ViewID);
                    }
                }
            }
            }
            finally
            {
                nearbyBooths.Clear();
                neighborCells.Clear();
                viewsToDelete.Clear();
            }
        }

        private sealed class MobViewWorkerScratch
        {
            public readonly List<Mob> NonIntroducibleMobs = new List<Mob>(16);
            public readonly List<Mob> IntroducibleMobs = new List<Mob>(32);
            public readonly List<List<int>> NeighborCells = new List<List<int>>(9);
            public readonly List<(Mob Mob, ClientView View)> MobsToSpawn = new List<(Mob Mob, ClientView View)>();
            public readonly List<(Mob Mob, ClientView View)> PetsToRefresh = new List<(Mob Mob, ClientView View)>();
            public readonly List<(ClientView View, Mob Mob)> ViewsToDelete = new List<(ClientView View, Mob Mob)>();
        }

        [ThreadStatic] private static MobViewWorkerScratch threadMobViewScratch;

        private static MobViewWorkerScratch GetThreadMobViewScratch()
        {
            MobViewWorkerScratch scratch = threadMobViewScratch;
            if (scratch == null)
            {
                scratch = new MobViewWorkerScratch();
                threadMobViewScratch = scratch;
            }
            return scratch;
        }

        private struct DeferredMobViewActions
        {
            public int ClientIndex;
            public WorldClient Client;
            public ushort DistrictId;
            public List<(Mob Mob, ClientView View)> MobsToSpawn;
            public List<(Mob Mob, ClientView View)> PetsToRefresh;
            public List<(ClientView View, Mob Mob)> ViewsToDelete;
        }

        private static void EvaluateClientMobViews(
            WorldClient thisclient,
            MobEntry[] entries,
            Dictionary<ushort, SpatialGrid<int>> indexGrids,
            Dictionary<ulong, Mob> mobsByEntity,
            MobViewWorkerScratch scratch,
            out ushort districtId)
        {
            districtId = 0;
            scratch.MobsToSpawn.Clear();
            scratch.PetsToRefresh.Clear();
            scratch.ViewsToDelete.Clear();

            if (thisclient == null || thisclient.Alive == false)
            {
                return;
            }

            if (thisclient.playerData.getOnWorld() == false || thisclient.playerData.waitForRPCShutDown)
            {
                return;
            }

            districtId = (ushort)thisclient.playerData.getDistrictId();
            if (!indexGrids.TryGetValue(districtId, out SpatialGrid<int> grid))
            {
                return;
            }

            const float viewRangeSq = ViewRange * ViewRange;
            double playerX = 0;
            double playerY = 0;
            double playerZ = 0;
            NumericalUtils.LtVector3dToDoubles(thisclient.playerInstance.Position.getValue(),
                ref playerX, ref playerY, ref playerZ);
            float px = (float)playerX;
            float pz = (float)playerZ;

            List<Mob> nonIntroducibleMobs = scratch.NonIntroducibleMobs;
            List<Mob> introducibleMobs = scratch.IntroducibleMobs;
            List<List<int>> neighborCells = scratch.NeighborCells;
            nonIntroducibleMobs.Clear();
            introducibleMobs.Clear();
            grid.CollectNeighborCells(px, pz, ViewRange, neighborCells);
            for (int c = 0; c < neighborCells.Count; c++)
            {
                List<int> cell = neighborCells[c];
                for (int m = 0; m < cell.Count; m++)
                {
                    ref readonly MobEntry e = ref entries[cell[m]];
                    // Unspawned NPCs (corpse gone, waiting to respawn) aren't nearby, so their views get deleted below
                    if (!e.Spawned)
                    {
                        continue;
                    }

                    float dx = px - e.X;
                    float dz = pz - e.Z;
                    if (dx * dx + dz * dz > viewRangeSq)
                    {
                        continue;
                    }

                    // Ordinary corpses stay with their existing viewers. Scripted mission bodies are introduced
                    // with their dead attributes and sourced pose. GetViewForEntityAndGo hands out a view id,
                    // and one the client never sees spawned puts every later view id one off.
                    if (!e.CanIntroduce && !MissionPersistence.PendingLootOwner(thisclient, e.Mob))
                    {
                        nonIntroducibleMobs.Add(e.Mob);
                        continue;
                    }

                    introducibleMobs.Add(e.Mob);
                }
            }

            thisclient.viewMan.ReconcileMobViews(
                districtId, nonIntroducibleMobs, introducibleMobs, mobsByEntity,
                scratch.MobsToSpawn, scratch.PetsToRefresh, scratch.ViewsToDelete);
        }

        private static void ApplyMobViewActions(
            WorldClient thisclient,
            ushort districtId,
            List<(Mob Mob, ClientView View)> mobsToSpawn,
            List<(Mob Mob, ClientView View)> petsToRefresh,
            List<(ClientView View, Mob Mob)> viewsToDelete)
        {
            if (mobsToSpawn != null)
            {
                for (int i = 0; i < mobsToSpawn.Count; i++)
                {
                    Mob thismob = mobsToSpawn[i].Mob;
                    ClientView mobView = mobsToSpawn[i].View;
                    if (mobView.viewCreated == false &&
                        thismob.getDistrict() == thisclient.playerData.getDistrictId() &&
                        thisclient.playerData.getOnWorld())
                    {
#if DEBUG
                        ServerPackets pak = new ServerPackets();
                        if (DebugChat.Enabled)
                        pak.sendSystemChatMessage(thisclient,
                            "Mob with Name " + thismob.getName() + " with new View ID " + mobView.ViewID +
                            " spawned", "BROADCAST");
#endif

                        ServerPackets mobPak = new ServerPackets();
                        mobPak.SpawnMobView(thisclient, thismob, mobView); // sets mobView.spawnId
                        mobView.viewCreated = true;
                        thismob.isUpdateable = true;
                        thismob.DoMobUpdate(thismob);
                    }
                }
            }

            if (petsToRefresh != null)
            {
                for (int i = 0; i < petsToRefresh.Count; i++)
                {
                    Mob thismob = petsToRefresh[i].Mob;
                    ClientView mobView = petsToRefresh[i].View;
                    if (mobView.viewCreated && !mobView.viewNeedsToBeDeleted && thismob.pet != null &&
                        thismob.getDistrict() == districtId && thisclient.playerData.getOnWorld())
                    {
                        CoderPets.RefreshPresentation(thismob.pet, thisclient, mobView);
                    }
                }
            }

            if (viewsToDelete != null)
            {
                for (int i = 0; i < viewsToDelete.Count; i++)
                {
                    ClientView view = viewsToDelete[i].View;
                    Mob mob = viewsToDelete[i].Mob;
                    // Delete Mob's View from Client if we are outside
                    ServerPackets packets = new ServerPackets();
                    packets.sendDeleteViewPacket(thisclient, view.ViewID);
#if DEBUG
                    if (DebugChat.Enabled)
                    packets.sendSystemChatMessage(thisclient,
                        "MobView (" + mob.getName() + " LVL: " + mob.getLevel() +
                        " ) with View ID " + view.ViewID + " is out of range and is deleted!",
                        "MODAL");
#endif
                    mob.isUpdateable = false;
                    thisclient.viewMan.removeViewByViewId(view.ViewID);
                }
            }
        }

        private static void CheckPlayerMobViews(List<WorldClient> clients, MobVisibilityCache mobCache)
        {
            MobEntry[] entries = mobCache.Entries;
            Dictionary<ushort, SpatialGrid<int>> indexGrids = mobCache.IndexGrids;
            Dictionary<ulong, Mob> mobsByEntity = mobCache.ByEntity;

            if (clients.Count < ParallelVisibilityClientThreshold)
            {
                MobViewWorkerScratch scratch = GetThreadMobViewScratch();
                for (int ci = 0; ci < clients.Count; ci++)
                {
                    WorldClient thisclient = clients[ci];
                    EvaluateClientMobViews(thisclient, entries, indexGrids, mobsByEntity, scratch, out ushort districtId);
                    if (scratch.MobsToSpawn.Count > 0 || scratch.PetsToRefresh.Count > 0 || scratch.ViewsToDelete.Count > 0)
                    {
                        ApplyMobViewActions(thisclient, districtId, scratch.MobsToSpawn, scratch.PetsToRefresh, scratch.ViewsToDelete);
                    }
                }
                return;
            }

            // Parallel read-only spatial scan + per-client ViewManager reconciliation; any actual spawns,
            // pet refreshes, or view deletes are applied sequentially in deterministic client order below.
            List<DeferredMobViewActions> deferred = null;
            object deferredLock = new object();

            Parallel.For(0, clients.Count, VisibilityParallelOptions,
                GetThreadMobViewScratch,
                (ci, state, scratch) =>
                {
                    WorldClient thisclient = clients[ci];
                    EvaluateClientMobViews(thisclient, entries, indexGrids, mobsByEntity, scratch, out ushort districtId);
                    if (scratch.MobsToSpawn.Count > 0 || scratch.PetsToRefresh.Count > 0 || scratch.ViewsToDelete.Count > 0)
                    {
                        var action = new DeferredMobViewActions
                        {
                            ClientIndex = ci,
                            Client = thisclient,
                            DistrictId = districtId,
                            MobsToSpawn = scratch.MobsToSpawn.Count > 0 ? new List<(Mob Mob, ClientView View)>(scratch.MobsToSpawn) : null,
                            PetsToRefresh = scratch.PetsToRefresh.Count > 0 ? new List<(Mob Mob, ClientView View)>(scratch.PetsToRefresh) : null,
                            ViewsToDelete = scratch.ViewsToDelete.Count > 0 ? new List<(ClientView View, Mob Mob)>(scratch.ViewsToDelete) : null
                        };
                        lock (deferredLock)
                        {
                            if (deferred == null) deferred = new List<DeferredMobViewActions>();
                            deferred.Add(action);
                        }
                    }
                    return scratch;
                },
                _ => { });

            if (deferred != null)
            {
                deferred.Sort((a, b) => a.ClientIndex.CompareTo(b.ClientIndex));
                for (int i = 0; i < deferred.Count; i++)
                {
                    DeferredMobViewActions action = deferred[i];
                    ApplyMobViewActions(action.Client, action.DistrictId, action.MobsToSpawn, action.PetsToRefresh, action.ViewsToDelete);
                }
            }
        }

        /// <summary>
        /// One player's state as this pass reads it: decoded once per pass into a contiguous value-type array
        /// instead of once per pair (at 200 dense players the pair loop decoded 40,000 positions and goids a pass).
        /// </summary>
        internal struct PlayerSample
        {
            public WorldClient Client;
            public bool Eligible, Hidden;
            public uint District;
            public double X, Z;
            public ulong Entity;
            public UInt16 Goid;
            /// <summary>Different from the previous pass (or new): position, liveness, district or stealth.</summary>
            public bool Changed;
            public bool SameAs(in PlayerSample o) => o.Client != null && o.Eligible == Eligible && o.Hidden == Hidden && o.District == District &&
                o.X == X && o.Z == Z && o.Entity == Entity && o.Goid == Goid;
        }

        private sealed class PlayerViewWorkerScratch
        {
            public readonly List<List<WorldClient>> NeighborCells = new List<List<WorldClient>>(9);
            public readonly List<PlayerSample> NearbySamples = new List<PlayerSample>(64);
            public readonly List<(WorldClient Other, ClientView View)> ToSpawn = new List<(WorldClient Other, ClientView View)>();
            public readonly List<ClientView> ToDelete = new List<ClientView>();
            public int Skipped;
        }

        [ThreadStatic] private static PlayerViewWorkerScratch threadPlayerViewScratch;

        private static PlayerViewWorkerScratch GetThreadPlayerViewScratch()
        {
            PlayerViewWorkerScratch scratch = threadPlayerViewScratch;
            if (scratch == null)
            {
                scratch = new PlayerViewWorkerScratch();
                threadPlayerViewScratch = scratch;
            }
            scratch.Skipped = 0;
            return scratch;
        }

        private struct DeferredPlayerViewActions
        {
            public int SampleIndex;
            public WorldClient Client;
            public List<(WorldClient Other, ClientView View)> ToSpawn;
            public List<ClientView> ToDelete;
        }

        // Monotonic pass id (starts at 1 so first pass = 2 never matches default 0 on freshly constructed/uninitialized WorldClient).
        private static long playerViewPassCounter = 1;
        private static int previousPlayerSampleCount = -1;
        private static PlayerSample[] reusablePlayerSamples = Array.Empty<PlayerSample>();
        internal static int SkippedPlayerEvaluations;

        private static PlayerSample SampleSingle(WorldClient client)
        {
            double x = 0, y = 0, z = 0;
            NumericalUtils.LtVector3dToDoubles(client.playerInstance.Position.getValue(), ref x, ref y, ref z);
            return new PlayerSample
            {
                Client = client,
                Eligible = client.Alive && client.playerData.getOnWorld() && !client.playerData.waitForRPCShutDown,
                Hidden = Stealth.IsHidden(client),
                District = client.playerData.getDistrictId(),
                X = x, Z = z,
                Entity = client.playerData.getEntityId(),
                Goid = NumericalUtils.ByteArrayToUint16(client.playerInstance.GetGoid(), 1),
                Changed = true
            };
        }

        private static bool TryLookupSample(WorldClient client, PlayerSample[] sampleArray, int sampleCount, out PlayerSample sample)
        {
            int idx = client.VisibilitySampleIndex;
            if ((uint)idx < (uint)sampleCount && ReferenceEquals(sampleArray[idx].Client, client))
            {
                sample = sampleArray[idx];
                return true;
            }
            sample = default;
            return false;
        }

        private static void EvaluateSinglePlayerViews(
            in PlayerSample current,
            PlayerSample[] sampleArray,
            int sampleCount,
            bool canSkip,
            Dictionary<uint, SpatialGrid<WorldClient>> clientGrids,
            PlayerViewWorkerScratch scratch)
        {
            scratch.ToSpawn.Clear();
            scratch.ToDelete.Clear();

            if (!current.Eligible)
            {
                return;
            }

            WorldClient currentClient = current.Client;
            uint districtId = current.District;
            if (!clientGrids.TryGetValue(districtId, out SpatialGrid<WorldClient> grid))
            {
                return;
            }

            List<List<WorldClient>> neighborCells = scratch.NeighborCells;
            if (canSkip && !current.Changed && currentClient.HasEvaluatedPlayerViews &&
                currentClient.LastEvaluatedViewVersion == currentClient.viewMan.Version &&
                !NeighbourChanged(grid, in current, currentClient.LastNearPlayers, sampleArray, sampleCount, neighborCells))
            {
                scratch.Skipped++;
                return;
            }

            const double viewRangeSq = (double)ViewRange * ViewRange;
            float currentPlayerX = (float)current.X;
            float currentPlayerZ = (float)current.Z;

            List<PlayerSample> nearbySamples = scratch.NearbySamples;
            nearbySamples.Clear();
            List<WorldClient> nearList = currentClient.LastNearPlayers;
            if (nearList == null)
            {
                nearList = new List<WorldClient>();
                currentClient.LastNearPlayers = nearList;
            }
            nearList.Clear();

            grid.CollectNeighborCells(currentPlayerX, currentPlayerZ, ViewRange, neighborCells);
            for (int cellIdx = 0; cellIdx < neighborCells.Count; cellIdx++)
            {
                List<WorldClient> cell = neighborCells[cellIdx];
                for (int oi = 0; oi < cell.Count; oi++)
                {
                    WorldClient otherClient = cell[oi];
                    if (otherClient == null || ReferenceEquals(otherClient, currentClient))
                    {
                        continue;
                    }

                    nearList.Add(otherClient);

                    // A client the grid holds but this pass didn't sample (added since the snapshot) is read live.
                    if (!TryLookupSample(otherClient, sampleArray, sampleCount, out PlayerSample other))
                    {
                        other = SampleSingle(otherClient);
                    }

                    if (!other.Eligible || other.District != districtId)
                    {
                        continue;
                    }

                    double dx = currentPlayerX - (float)other.X;
                    double dz = currentPlayerZ - (float)other.Z;
                    if (dx * dx + dz * dz > viewRangeSq)
                    {
                        continue;
                    }

                    // A sneaking or invisible enemy stays out of this player's sight (docs/STEALTH.md)
                    if (other.Hidden && Stealth.HiddenFrom(currentClient, otherClient))
                    {
                        continue;
                    }

                    nearbySamples.Add(other);
                }
            }

            long version = currentClient.viewMan.ReconcilePlayerSamples(
                nearbySamples, current.Goid, scratch.ToSpawn, scratch.ToDelete);
            currentClient.LastEvaluatedViewVersion = version;
            currentClient.HasEvaluatedPlayerViews = true;
        }

        private static void ApplyPlayerViewActions(
            WorldClient currentClient,
            Dictionary<ulong, WorldClient> clientsByEntity,
            List<(WorldClient Other, ClientView View)> toSpawn,
            List<ClientView> toDelete)
        {
            if (toSpawn != null && toSpawn.Count > 0)
            {
                for (int s = 0; s < toSpawn.Count; s++)
                {
                    WorldClient otherClient = toSpawn[s].Other;
                    ClientView clientView = toSpawn[s].View;
                    if (clientView.viewCreated == false &&
                        currentClient.playerData.getDistrictId() == otherClient.playerData.getDistrictId() &&
                        otherClient.playerData.getOnWorld() && currentClient.playerData.getOnWorld())
                    {
                        // Spawn player
                        ServerPackets pak = new ServerPackets();
                        pak.sendSystemChatMessage(currentClient,
                            "Player " + StringUtils.charBytesToString_NZ(otherClient.playerInstance
                                .CharacterName.getValue()) + " with new View ID " +
                            clientView.ViewID + " jacked in", "BROADCAST");
                        // The spawn id is the creation record's counter byte: with the view id it is the handle the
                        // client names this player by (combat, duels, trade). This used to store the next counter value.
                        clientView.spawnId = pak.SendPlayerSpawn(currentClient, otherClient, clientView.ViewID);
                        clientView.viewCreated = true;
                        Stealth.AfterSpawn(currentClient, otherClient, clientView.ViewID);
                        EvasionShield.AfterSpawn(currentClient, otherClient, clientView.ViewID);
                        StealthShieldAbilities.AfterSpawn(currentClient, otherClient, clientView.ViewID);
                    }
                }
            }

            if (toDelete != null && toDelete.Count > 0)
            {
                for (int d = 0; d < toDelete.Count; d++)
                {
                    ClientView view = toDelete[d];
                    ServerPackets packets = new ServerPackets();
                    if (clientsByEntity.TryGetValue(view.entityId, out WorldClient otherClient))
                    {
                        packets.sendSystemChatMessage(currentClient,
                            "Player " + StringUtils.charBytesToString_NZ(otherClient.playerInstance
                                .CharacterName.getValue()) + " with View ID " + view.ViewID +
                            " jacked out!", "MODAL");
                    }
                    else
                    {
                        packets.sendSystemChatMessage(currentClient,
                            "Player with View ID " + view.ViewID + " jacked out!", "MODAL");
                    }

                    packets.sendDeleteViewPacket(currentClient, view.ViewID);
                    currentClient.viewMan.removeViewByViewId(view.ViewID);
                }
                currentClient.LastEvaluatedViewVersion = currentClient.viewMan.Version;
            }
        }

        internal static void CheckPlayerViews(List<WorldClient> clients,
            Dictionary<uint, SpatialGrid<WorldClient>> clientGrids,
            Dictionary<ulong, WorldClient> clientsByEntity)
        {
            long currentPass = ++playerViewPassCounter;
            long prevPass = currentPass - 1;
            if (reusablePlayerSamples.Length < clients.Count)
            {
                reusablePlayerSamples = new PlayerSample[Math.Max(clients.Count, 64)];
            }

            PlayerSample[] sampleArray = reusablePlayerSamples;
            int sampleCount = 0;
            // A player's outcome depends only on its own sample, its views and the samples near it, so when none of
            // those changed it is skipped. Any join, leave or hidden player (stealth visibility also depends on PvP
            // state, which a sample doesn't carry) makes this a full pass.
            bool canSkip = previousPlayerSampleCount >= 0;

            for (int i = 0; i < clients.Count; i++)
            {
                WorldClient client = clients[i];
                if (client == null || client.VisibilityCurrentPassId == currentPass) continue;
                client.VisibilityCurrentPassId = currentPass;

                PlayerSample sample = SampleSingle(client);
                if (sample.Hidden) canSkip = false;

                if (client.VisibilityLastPassId != prevPass)
                {
                    canSkip = false;
                    sample.Changed = true;
                }
                else
                {
                    PlayerSample before = client.LastPlayerSample;
                    sample.Changed = !sample.SameAs(in before);
                    // Leaving the world or the district takes a player out of the grid, where neighbours wouldn't see it.
                    if (before.Eligible != sample.Eligible || before.District != sample.District)
                    {
                        canSkip = false;
                    }
                }

                client.LastPlayerSample = sample;
                client.VisibilityLastPassId = currentPass;
                client.VisibilitySampleIndex = sampleCount;
                sampleArray[sampleCount++] = sample;
            }

            if (sampleCount != previousPlayerSampleCount)
            {
                canSkip = false;
            }
            previousPlayerSampleCount = sampleCount;

            if (sampleCount < ParallelVisibilityClientThreshold)
            {
                PlayerViewWorkerScratch scratch = GetThreadPlayerViewScratch();
                for (int si = 0; si < sampleCount; si++)
                {
                    ref readonly PlayerSample current = ref sampleArray[si];
                    EvaluateSinglePlayerViews(in current, sampleArray, sampleCount, canSkip, clientGrids, scratch);
                    if (scratch.ToSpawn.Count > 0 || scratch.ToDelete.Count > 0)
                    {
                        ApplyPlayerViewActions(current.Client, clientsByEntity, scratch.ToSpawn, scratch.ToDelete);
                    }
                }
                if (scratch.Skipped > 0)
                {
                    Interlocked.Add(ref SkippedPlayerEvaluations, scratch.Skipped);
                }
                return;
            }

            // Parallel read-only spatial scan + per-client ViewManager reconciliation; spawns and deletes
            // run sequentially in deterministic client order after Parallel.For finishes.
            List<DeferredPlayerViewActions> deferred = null;
            object deferredLock = new object();

            Parallel.For(0, sampleCount, VisibilityParallelOptions,
                GetThreadPlayerViewScratch,
                (si, state, scratch) =>
                {
                    ref readonly PlayerSample current = ref sampleArray[si];
                    EvaluateSinglePlayerViews(in current, sampleArray, sampleCount, canSkip, clientGrids, scratch);
                    if (scratch.ToSpawn.Count > 0 || scratch.ToDelete.Count > 0)
                    {
                        var action = new DeferredPlayerViewActions
                        {
                            SampleIndex = si,
                            Client = current.Client,
                            ToSpawn = scratch.ToSpawn.Count > 0 ? new List<(WorldClient Other, ClientView View)>(scratch.ToSpawn) : null,
                            ToDelete = scratch.ToDelete.Count > 0 ? new List<ClientView>(scratch.ToDelete) : null
                        };
                        lock (deferredLock)
                        {
                            if (deferred == null) deferred = new List<DeferredPlayerViewActions>();
                            deferred.Add(action);
                        }
                    }
                    return scratch;
                },
                scratch =>
                {
                    if (scratch.Skipped > 0)
                    {
                        Interlocked.Add(ref SkippedPlayerEvaluations, scratch.Skipped);
                    }
                });

            if (deferred != null)
            {
                deferred.Sort((a, b) => a.SampleIndex.CompareTo(b.SampleIndex));
                for (int i = 0; i < deferred.Count; i++)
                {
                    DeferredPlayerViewActions action = deferred[i];
                    ApplyPlayerViewActions(action.Client, clientsByEntity, action.ToSpawn, action.ToDelete);
                }
            }
        }

        /// <summary>Whether anyone in the player's neighbour cells now, or in them at its last evaluation, changed.</summary>
        private static bool NeighbourChanged(SpatialGrid<WorldClient> grid, in PlayerSample current, List<WorldClient> near,
            PlayerSample[] sampleArray, int sampleCount, List<List<WorldClient>> neighborCells)
        {
            if (near != null)
            {
                for (int i = 0; i < near.Count; i++)
                {
                    if (!TryLookupSample(near[i], sampleArray, sampleCount, out PlayerSample s) || s.Changed) return true;
                }
            }
            grid.CollectNeighborCells((float)current.X, (float)current.Z, ViewRange, neighborCells);
            for (int c = 0; c < neighborCells.Count; c++)
            {
                List<WorldClient> cell = neighborCells[c];
                for (int i = 0; i < cell.Count; i++)
                {
                    WorldClient other = cell[i];
                    if (other != null)
                    {
                        if (!TryLookupSample(other, sampleArray, sampleCount, out PlayerSample s) || s.Changed) return true;
                    }
                }
            }
            return false;
        }

        internal static bool TryClaimInactiveClient(WorldClient client,out string key)
        {
            key=null;var registry=WorldServer.Clients;if(client==null||registry==null)return false;
            lock(registry)
            {
                if(!ReferenceEquals(registry,WorldServer.Clients))return false;
                string ownerKey = null;
                foreach (KeyValuePair<string, WorldClient> pair in registry)
                {
                    if (!ReferenceEquals(pair.Value, client)) continue;
                    if (ownerKey != null) return false;
                    ownerKey = pair.Key;
                }
                if (ownerKey == null || !client.TryClaimInactive()) return false;
                key = ownerKey;
                return true;
            }
        }
        internal static bool RemoveClaimedClient(string key,WorldClient client)
        {
            if(client==null||!client.IngressSnapshot.Claimed)return false;
            var registry=WorldServer.Clients;if(registry==null)return false;lock(registry)
            {
                if(!ReferenceEquals(registry,WorldServer.Clients)||!registry.TryGetValue(key,out var owner)||!ReferenceEquals(owner,client))return false;
                registry.Remove(key);return true;
            }
        }
        private static void CleanDeadPlayers(List<WorldClient> deadClients, List<WorldClient> allClients)
        {
            if (deadClients.Count == 0)
            {
                return;
            }

            foreach (WorldClient deadClient in deadClients)
            {
                // Revalidate the actual endpoint owner and latest ingress, then claim terminality
                // BEFORE Trade/Combat/Suspend can block or change persistent state. Keep this owner
                // mapped while cleanup runs; late ingress cannot resurrect it or create a replacement.
                if (!TryClaimInactiveClient(deadClient,out string endpoint))
                {
                    continue;
                }

                try
                {
                TradeHandler.Abort(deadClient);
                SubwayHandler.OnDisconnect(deadClient);
                CombatManager.RemoveClient(deadClient);
                MissionPersistence.Suspend(deadClient);
                TutorialMission.Of(deadClient)?.Abandon();
                ulong deadEntityId = deadClient.playerData.getEntityId();
                foreach (WorldClient otherclient in allClients)
                {
                    if (otherclient == null || ReferenceEquals(otherclient, deadClient) || otherclient.viewMan == null)
                    {
                        continue;
                    }

                    // Lookup only: GetViewForEntityAndGo used to allocate a new ClientView on every out-of-range
                    // player on the server, desynchronizing their ViewID counter and sending a delete for a view
                    // they never spawned, while never removing the dead view from viewers who did see them.
                    ClientView view = otherclient.viewMan.FindViewForEntity(deadEntityId);
                    if (view != null)
                    {
                        if (view.viewCreated)
                        {
                            new ServerPackets().sendDeleteViewPacket(otherclient, view.ViewID);
                        }
                        otherclient.viewMan.removeViewByViewId(view.ViewID);
                        otherclient.messageQueue?.ClearPendingMovement(view.ViewID);
                    }
                }

                // Release the dead client's own character. This used to run inside the loop above
                // with otherclient's charId, so it marked the *other* players offline and dropped
                // *their* margin entries, while the dead character stayed "already in use" until
                // a server restart.
                UInt32 deadCharId = deadClient.playerData.getCharID();
                Store.dbManager.WorldDbHandler.SetOnlineStatus(deadCharId, 0);
                // A late datagram from a released endpoint makes a fresh, never-selected client (character 0).
                // Reaping that one must not drop the margin entry of a session still at character select.
                if (deadCharId != 0) Store.margin.RemoveClientsByCharId(deadCharId);

                string handle = StringUtils.charBytesToString_NZ(deadClient.playerInstance.CharacterName.getValue());
                new BuddylistHandler().ProcessAnnounceFriendsOffline(deadClient.playerData.getCharID(), handle);
                SocialHandler.PlayerLeftWorld(deadClient);
                }
                catch {deadClient.ReleaseFailedReap();throw;}
                if (RemoveClaimedClient(endpoint, deadClient))
                {
                    Output.WriteLine("Removed inactive Client with Key " + endpoint);
                    Store.world?.objMan?.PopClient(endpoint);
                    if (deadClient.playerInstance != null)
                    {
                        lock (WorldServer.gameServerEntities.SyncRoot)
                        {
                            WorldServer.gameServerEntities.Remove(deadClient.playerInstance);
                        }
                    }
                    deadClient.CleanupOnDisconnect();
                }
                // Views are now deleted to other players
                // ToDo: Cleanup Missions (kill all running missions the player have)
                // ToDo: Cleanup Teams (if your mission team has more than one player, you need to announce an update for the mission team to your mates)
                // ToDo: Announce friendlists from other users that you are going offline (just collect all players whohave this client in list and send the packet)
                // ToDo: Finally save the current character Data to the Database^^
            }

        }
    }
}
