using System;
using System.Collections.Generic;
using System.Threading;
using System.Linq;
using hds.shared;
using hds.world.Structures;

namespace hds
{
    public partial class WorldThreads
    {
        private const float ViewRange = 5000f;
        private const ushort SubwayTypeId = 6568;
        private static readonly object StaticIndexLock = new object();
        private static readonly object SubwayIndexLock = new object();
        private static Dictionary<ushort, SpatialGrid<StaticWorldObject>> staticObjectsByDistrict;
        private static HashSet<uint> signpostStaticIds;
        private static Dictionary<ushort, SpatialGrid<Subway>> subwaysByDistrict;
        private static int subwayIndexCount;

        public void ViewVisibleThread()
        {
            Output.WriteLine("[WORLD SERVER]View Visible Thread started");
            while (true)
            {
              long scaleStart=ScaleDiagnostics.Start;
              // An exception escaping this loop ends the thread and the server; log it and try again next pass.
              try
              {
                long stage=scaleStart;
                List<WorldClient> clients = SnapshotClients();
                List<Mob> mobs = SnapshotMobs();
                List<Subway> subways = SnapshotSubways();

                List<WorldClient> deadClients = new List<WorldClient>();
                foreach (WorldClient client in clients)
                {
                    if (client != null && client.Alive == false)
                    {
                        deadClients.Add(client);
                    }
                }

                if (deadClients.Count > 0)
                {
                    CleanDeadPlayers(deadClients, clients);
                }

                List<WorldClient> aliveClients = new List<WorldClient>();
                foreach (WorldClient client in clients)
                {
                    if (client != null && client.Alive)
                    {
                        aliveClients.Add(client);
                    }
                }

                Dictionary<uint, SpatialGrid<WorldClient>> clientGrids = BuildClientGrids(aliveClients);
                Dictionary<ushort, SpatialGrid<Mob>> mobGrids = BuildMobGrids(mobs);
                Dictionary<ulong, WorldClient> clientsByEntity = BuildClientByEntity(aliveClients);
                Dictionary<ulong, Mob> mobsByEntity = BuildMobByEntity(mobs);
                stage=ScaleDiagnostics.Stage("snapshot",stage);

                CheckPlayerViews(aliveClients, clientGrids, clientsByEntity);
                stage=ScaleDiagnostics.Stage("player-views",stage);
                CheckPlayerMobViews(aliveClients, mobGrids, mobsByEntity);
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
                finally {if(scaleStart!=0)ScaleDiagnostics.VisibilityCompleted(scaleStart,SnapshotClients());}
                Thread.Sleep(500);
            }
        }

        private static void CheckForServerEntites()
        {
            // This can later replace ALL Methods 
            lock (WorldServer.gameServerEntities)
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

        private static List<WorldClient> SnapshotClients()
        {
            lock (WorldServer.Clients)
            {
                return new List<WorldClient>(WorldServer.Clients.Values);
            }
        }

        private static List<Mob> SnapshotMobs()
        {
            List<Mob> mobs = new List<Mob>();
            lock (WorldServer.mobs.SyncRoot)
            {
                foreach (object mob in WorldServer.mobs)
                {
                    Mob castMob = mob as Mob;
                    if (castMob != null)
                    {
                        mobs.Add(castMob);
                    }
                }
            }

            return mobs;
        }

        private static List<Subway> SnapshotSubways()
        {
            List<Subway> subways = new List<Subway>();
            lock (WorldServer.subways.SyncRoot)
            {
                foreach (object subway in WorldServer.subways)
                {
                    Subway castSubway = subway as Subway;
                    if (castSubway != null)
                    {
                        subways.Add(castSubway);
                    }
                }
            }

            return subways;
        }

        internal static Dictionary<uint, SpatialGrid<WorldClient>> BuildClientGrids(List<WorldClient> clients)
        {
            Dictionary<uint, SpatialGrid<WorldClient>> grids = new Dictionary<uint, SpatialGrid<WorldClient>>();
            foreach (WorldClient client in clients)
            {
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

            return grids;
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
            Dictionary<ulong, WorldClient> lookup = new Dictionary<ulong, WorldClient>();
            foreach (WorldClient client in clients)
            {
                if (client == null)
                {
                    continue;
                }

                lookup[client.playerData.getEntityId()] = client;
            }

            return lookup;
        }

        private static Dictionary<ulong, Mob> BuildMobByEntity(List<Mob> mobs)
        {
            Dictionary<ulong, Mob> lookup = new Dictionary<ulong, Mob>();
            foreach (Mob mob in mobs)
            {
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

        public static void CheckForStaticSubways(List<WorldClient> clients, List<Subway> subways)
        {
            EnsureSubwayIndex(subways);
            if (subwaysByDistrict == null)
            {
                return;
            }

            Maths mathUtils = new Maths();
            var nearbySubwayEntities = new HashSet<ulong>();
            var neighborCells = new List<List<Subway>>(9);
            var viewsToDelete = new List<ClientView>();
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

                        bool objectInCircle = mathUtils.IsInCircle((float)playerX, (float)playerZ,
                            (float)thisSubway.worldObject.pos_x, (float)thisSubway.worldObject.pos_z, ViewRange);
                        if (!objectInCircle)
                        {
                            continue;
                        }

                        UInt64 entityStaticId = GetSubwayEntityId(thisSubway);
                        nearbySubwayEntities.Add(entityStaticId);

                        ClientView view = thisclient.viewMan.GetViewForEntityAndGo(entityStaticId,
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

        private static void CheckForStaticObjectsViewsInRange(List<WorldClient> clients)
        {
            EnsureStaticObjectIndex();
            if (staticObjectsByDistrict == null)
            {
                return;
            }

            Maths mathUtils = new Maths();
            var nearbyBooths = new HashSet<ulong>();
            var neighborCells = new List<List<StaticWorldObject>>(9);
            var viewsToDelete = new List<ClientView>();
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
                            if (!mathUtils.IsInCircle((float)playerX, (float)playerZ,
                                    (float)staticWorldObject.pos_x, (float)staticWorldObject.pos_z, ViewRange))
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
                                    ClientView boothView = thisclient.viewMan.GetViewForEntityAndGo(boothEntity, typeId);
                                    if (!boothView.viewCreated)
                                    {
                                        boothView.staticObject = staticWorldObject;
                                        // SendSpawnGameObject takes the current spawn counter for the creation
                                        boothView.spawnId = thisclient.playerData.spawnViewUpdateCounter;
                                        new ServerPackets().SendSpawnGameObject(thisclient,
                                            HardlineBooths.BuildBoothObject(staticWorldObject), boothEntity);
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

        private static void CheckPlayerMobViews(List<WorldClient> clients, Dictionary<ushort, SpatialGrid<Mob>> mobGrids,
            Dictionary<ulong, Mob> mobsByEntity)
        {
            Maths mathUtils = new Maths();
            var nearbyMobEntities = new HashSet<ulong>();
            var neighborCells = new List<List<Mob>>(9);
            var viewsToDelete = new List<(ClientView View, Mob Mob)>();
            foreach (WorldClient thisclient in clients)
            {
                if (thisclient == null || thisclient.Alive == false)
                {
                    continue;
                }

                if (thisclient.playerData.getOnWorld() == false || thisclient.playerData.waitForRPCShutDown)
                {
                    continue;
                }

                ushort districtId = (ushort)thisclient.playerData.getDistrictId();
                if (!mobGrids.TryGetValue(districtId, out SpatialGrid<Mob> grid))
                {
                    continue;
                }

                double playerX = 0;
                double playerY = 0;
                double playerZ = 0;
                NumericalUtils.LtVector3dToDoubles(thisclient.playerInstance.Position.getValue(),
                    ref playerX, ref playerY, ref playerZ);

                nearbyMobEntities.Clear();
                grid.CollectNeighborCells((float)playerX, (float)playerZ, ViewRange, neighborCells);
                for (int c = 0; c < neighborCells.Count; c++)
                {
                    List<Mob> cell = neighborCells[c];
                    for (int m = 0; m < cell.Count; m++)
                    {
                        Mob thismob = cell[m];
                        // Unspawned NPCs (corpse gone, waiting to respawn) aren't nearby, so their views get deleted below
                        if (thismob == null || !thismob.getIsSpawned())
                        {
                            continue;
                        }

                        bool mobIsInCircle = mathUtils.IsInCircle((float)playerX, (float)playerZ,
                            (float)thismob.getXPos(), (float)thismob.getZPos(), ViewRange);

                        if (!mobIsInCircle)
                        {
                            continue;
                        }

                        nearbyMobEntities.Add(thismob.getEntityId());

                        // Ordinary corpses stay with their existing viewers. Scripted mission bodies are introduced
                        // with their dead attributes and sourced pose. GetViewForEntityAndGo hands out a view id,
                        // and one the client never sees spawned puts every later view id one off.
                        if (!DeathPresentation.CanIntroduceNpc(thismob) && !MissionPersistence.PendingLootOwner(thisclient,thismob))
                        {
                            continue;
                        }

                        // Spawn Mob if its in Visibility Range
                        ClientView mobView = thisclient.viewMan.GetViewForEntityAndGo(thismob.getEntityId(),
                            NumericalUtils.ByteArrayToUint16(thismob.getGoId(), 1));
                        if (mobView.viewCreated == false &&
                            thismob.getDistrict() == thisclient.playerData.getDistrictId() &&
                            thisclient.playerData.getOnWorld() && mobIsInCircle)
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
                        else if (mobView.viewCreated && !mobView.viewNeedsToBeDeleted && thismob.pet != null &&
                            thismob.getDistrict() == districtId && thisclient.playerData.getOnWorld())
                        {
                            CoderPets.RefreshPresentation(thismob.pet, thisclient, mobView);
                        }
                    }
                }

                thisclient.viewMan.CollectStaleMobViews(districtId, nearbyMobEntities, mobsByEntity, viewsToDelete);
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

        /// <summary>
        /// One player's state as this pass reads it: decoded once per pass instead of once per pair (at 200 dense
        /// players the pair loop decoded 40,000 positions and goids a pass, each allocating).
        /// </summary>
        private sealed class PlayerSample
        {
            public WorldClient Client;
            public bool Eligible, Hidden;
            public uint District;
            public double X, Z;
            public ulong Entity;
            public UInt16 Goid;
            /// <summary>Different from the previous pass (or new): position, liveness, district or stealth.</summary>
            public bool Changed = true;
            public bool SameAs(PlayerSample o) => o != null && o.Eligible == Eligible && o.Hidden == Hidden && o.District == District &&
                o.X == X && o.Z == Z && o.Entity == Entity && o.Goid == Goid;
        }

        // The previous pass's samples and each player's view-list version after it was last evaluated. Only the
        // visibility thread (and tests calling it directly) runs CheckPlayerViews.
        private static Dictionary<WorldClient, PlayerSample> previousSamples;
        private static readonly Dictionary<WorldClient, (long Views, List<WorldClient> Near)> evaluatedViews =
            new Dictionary<WorldClient, (long Views, List<WorldClient> Near)>(ReferenceEqualityComparer.Instance);
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
                Goid = NumericalUtils.ByteArrayToUint16(client.playerInstance.GetGoid(), 1)
            };
        }

        private static Dictionary<WorldClient, PlayerSample> SamplePlayers(List<WorldClient> clients, List<PlayerSample> sampleList)
        {
            var samples = new Dictionary<WorldClient, PlayerSample>(clients.Count, ReferenceEqualityComparer.Instance);
            for (int i = 0; i < clients.Count; i++)
            {
                WorldClient client = clients[i];
                if (client == null || samples.ContainsKey(client)) continue;
                PlayerSample sample = SampleSingle(client);
                client.VisibilitySampleIndex = sampleList.Count;
                sampleList.Add(sample);
                samples[client] = sample;
            }
            return samples;
        }

        private static PlayerSample LookupSample(WorldClient client, List<PlayerSample> sampleList, Dictionary<WorldClient, PlayerSample> samples)
        {
            int idx = client.VisibilitySampleIndex;
            if ((uint)idx < (uint)sampleList.Count && ReferenceEquals(sampleList[idx].Client, client))
            {
                return sampleList[idx];
            }
            return samples.TryGetValue(client, out PlayerSample s) ? s : null;
        }

        internal static void CheckPlayerViews(List<WorldClient> clients,
            Dictionary<uint, SpatialGrid<WorldClient>> clientGrids,
            Dictionary<ulong, WorldClient> clientsByEntity)
        {
            var sampleList = new List<PlayerSample>(clients.Count);
            Dictionary<WorldClient, PlayerSample> samples = SamplePlayers(clients, sampleList);
            Dictionary<WorldClient, PlayerSample> previous = previousSamples;
            // A player's outcome depends only on its own sample, its views and the samples near it, so when none of
            // those changed it is skipped. Any join, leave or hidden player (stealth visibility also depends on PvP
            // state, which a sample doesn't carry) makes this a full pass.
            bool canSkip = previous != null && previous.Count == samples.Count;
            for (int i = 0; i < sampleList.Count; i++)
            {
                PlayerSample sample = sampleList[i];
                if (sample.Hidden) canSkip = false;
                if (previous == null || !previous.TryGetValue(sample.Client, out PlayerSample before)) { canSkip = false; continue; }
                sample.Changed = !sample.SameAs(before);
                // Leaving the world or the district takes a player out of the grid, where neighbours wouldn't see it.
                if (before.Eligible != sample.Eligible || before.District != sample.District) canSkip = false;
            }
            previousSamples = samples;
            if (evaluatedViews.Count > 0)
            {
                List<WorldClient> goneClients = null;
                foreach (WorldClient c in evaluatedViews.Keys)
                {
                    if (!samples.ContainsKey(c))
                    {
                        if (goneClients == null) goneClients = new List<WorldClient>();
                        goneClients.Add(c);
                    }
                }
                if (goneClients != null)
                {
                    for (int i = 0; i < goneClients.Count; i++) evaluatedViews.Remove(goneClients[i]);
                }
            }

            const double viewRangeSq = (double)ViewRange * ViewRange;
            var neighborCells = new List<List<WorldClient>>(9);
            var nearbyClients = new List<WorldClient>(clients.Count);
            var nearbyEntities = new List<ulong>(clients.Count);
            var nearbyGoids = new List<UInt16>(clients.Count);
            var nearbyPlayerEntities = new HashSet<ulong>(clients.Count);
            var toSpawn = new List<(WorldClient Other, ClientView View)>();
            var toDelete = new List<ClientView>();

            for (int ci = 0; ci < clients.Count; ci++)
            {
                WorldClient currentClient = clients[ci];
                if (currentClient == null) continue;
                PlayerSample current = LookupSample(currentClient, sampleList, samples);
                if (current == null || !current.Eligible)
                {
                    continue;
                }

                uint districtId = current.District;
                if (!clientGrids.TryGetValue(districtId, out SpatialGrid<WorldClient> grid))
                {
                    continue;
                }

                bool hasEvaluated = evaluatedViews.TryGetValue(currentClient, out var seen);
                if (canSkip && !current.Changed && hasEvaluated &&
                    seen.Views == currentClient.viewMan.Version &&
                    !NeighbourChanged(grid, current, seen.Near, sampleList, samples, neighborCells))
                {
                    SkippedPlayerEvaluations++;
                    continue;
                }

                float currentPlayerX = (float)current.X;
                float currentPlayerZ = (float)current.Z;

                nearbyClients.Clear();
                nearbyEntities.Clear();
                nearbyGoids.Clear();
                nearbyPlayerEntities.Clear();
                List<WorldClient> nearList = hasEvaluated ? seen.Near : new List<WorldClient>();
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
                        PlayerSample other = LookupSample(otherClient, sampleList, samples);
                        if (other == null)
                        {
                            other = SampleSingle(otherClient);
                            otherClient.VisibilitySampleIndex = sampleList.Count;
                            sampleList.Add(other);
                            samples[otherClient] = other;
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

                        nearbyPlayerEntities.Add(other.Entity);
                        nearbyClients.Add(otherClient);
                        nearbyEntities.Add(other.Entity);
                        nearbyGoids.Add(other.Goid);
                    }
                }

                long version = currentClient.viewMan.ReconcilePlayerViews(
                    nearbyClients, nearbyEntities, nearbyGoids, nearbyPlayerEntities,
                    current.Goid, toSpawn, toDelete);

                if (toSpawn.Count > 0)
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

                if (toDelete.Count > 0)
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
                    version = currentClient.viewMan.Version;
                }

                evaluatedViews[currentClient] = (version, nearList);
            }
        }

        /// <summary>Whether anyone in the player's neighbour cells now, or in them at its last evaluation, changed.</summary>
        private static bool NeighbourChanged(SpatialGrid<WorldClient> grid, PlayerSample current, List<WorldClient> near,
            List<PlayerSample> sampleList, Dictionary<WorldClient, PlayerSample> samples, List<List<WorldClient>> neighborCells)
        {
            for (int i = 0; i < near.Count; i++)
            {
                PlayerSample s = LookupSample(near[i], sampleList, samples);
                if (s == null || s.Changed) return true;
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
                        PlayerSample s = LookupSample(other, sampleList, samples);
                        if (s == null || s.Changed) return true;
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
                var owners=registry.Where(p=>ReferenceEquals(p.Value,client)).Take(2).ToArray();
                if(owners.Length!=1||!client.TryClaimInactive())return false;
                key=owners[0].Key;return true;
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
                CombatManager.RemoveClient(deadClient);
                MissionPersistence.Suspend(deadClient);
                TutorialMission.Of(deadClient)?.Abandon();
                foreach (WorldClient otherclient in allClients)
                {
                    if (otherclient == null || otherclient == deadClient)
                    {
                        continue;
                    }

                    ClientView view = otherclient.viewMan.GetViewForEntityAndGo(deadClient.playerData.getEntityId(),
                        NumericalUtils.ByteArrayToUint16(deadClient.playerInstance.GetGoid(), 1));
                    if (view != null)
                    {
                        new ServerPackets().sendDeleteViewPacket(otherclient, view.ViewID);
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
                if(RemoveClaimedClient(endpoint,deadClient))Output.WriteLine("Removed inactive Client with Key " + endpoint);
                // Views are now deleted to other players
                // ToDo: Cleanup Missions (kill all running missions the player have)
                // ToDo: Cleanup Teams (if your mission team has more than one player, you need to announce an update for the mission team to your mates)
                // ToDo: Announce friendlists from other users that you are going offline (just collect all players whohave this client in list and send the packet)
                // ToDo: Finally save the current character Data to the Database^^
            }

        }
    }
}
