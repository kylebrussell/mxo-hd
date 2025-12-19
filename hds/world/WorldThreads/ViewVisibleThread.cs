using System;
using System.Collections.Generic;
using System.Threading;
using hds.shared;
using hds.world.Structures;

namespace hds
{
    public partial class WorldThreads
    {
        private const float ViewRange = 5000f;
        private static readonly object StaticIndexLock = new object();
        private static Dictionary<ushort, SpatialGrid<StaticWorldObject>> staticObjectsByDistrict;
        private static HashSet<uint> signpostStaticIds;

        public void ViewVisibleThread()
        {
            Output.WriteLine("[WORLD SERVER]View Visible Thread started");
            while (true)
            {
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

                CheckPlayerViews(aliveClients, clientGrids, clientsByEntity);
                CheckPlayerMobViews(aliveClients, mobGrids, mobsByEntity);
                CheckForStaticSubways(aliveClients, subways);
                CheckForStaticObjectsViewsInRange(aliveClients);
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

        private static Dictionary<uint, SpatialGrid<WorldClient>> BuildClientGrids(List<WorldClient> clients)
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

        private static Dictionary<ulong, WorldClient> BuildClientByEntity(List<WorldClient> clients)
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
                foreach (StaticWorldObject worldObject in loader.WorldObjectsDB)
                {
                    ushort districtId = worldObject.metrId;
                    if (!index.TryGetValue(districtId, out SpatialGrid<StaticWorldObject> grid))
                    {
                        grid = new SpatialGrid<StaticWorldObject>(ViewRange);
                        index.Add(districtId, grid);
                    }

                    grid.Add((float)worldObject.pos_x, (float)worldObject.pos_z, worldObject);
                }

                foreach (NPC_Singpost signpost in loader.Signposts)
                {
                    signposts.Add(signpost.mxoStaticId);
                }

                staticObjectsByDistrict = index;
                signpostStaticIds = signposts;
            }
        }

        public static void CheckForStaticSubways(List<WorldClient> clients, List<Subway> subways)
        {
            Maths mathUtils = new Maths();
            foreach (Subway thisSubway in subways)
            {
                foreach (WorldClient thisclient in clients)
                {
                    if (thisclient == null || thisclient.Alive == false)
                    {
                        continue;
                    }

                    if (thisclient.playerData.getOnWorld() == true &&
                        thisclient.playerData.waitForRPCShutDown == false)
                    {
                        double playerX = 0;
                        double playerY = 0;
                        double playerZ = 0;
                        NumericalUtils.LtVector3dToDoubles(thisclient.playerInstance.Position.getValue(),
                            ref playerX, ref playerY, ref playerZ);
                        bool objectInCircle = mathUtils.IsInCircle((float)playerX, (float)playerZ,
                            (float)thisSubway.worldObject.pos_x, (float)thisSubway.worldObject.pos_z, ViewRange);

                        // EntityHackString
                        String entityHackString =
                            "" + thisSubway.worldObject.metrId + "" + thisSubway.worldObject.mxoStaticId;
                        UInt64 entityStaticId = UInt64.Parse(entityHackString);

                        ClientView view = thisclient.viewMan.GetViewForEntityAndGo(entityStaticId,
                            NumericalUtils.ByteArrayToUint16(thisSubway.worldObject.type, 1));

                        if (!view.viewCreated &&
                            thisSubway.worldObject.metrId == thisclient.playerData.getDistrictId() &&
                            thisclient.playerData.getOnWorld() &&
                            objectInCircle)
                        {
                            ServerPackets pak = new ServerPackets();
                            pak.SendSpawnGameObject(thisclient, thisSubway.gameObjectData, entityStaticId);
                            view.spawnId = thisclient.playerData.spawnViewUpdateCounter;
                            view.viewCreated = true;
                        }


                        // Delete SubwayView 
                        if (view.viewCreated && !objectInCircle &&
                            thisSubway.worldObject.metrId == thisclient.playerData.getDistrictId())
                        {
                            ServerPackets packets = new ServerPackets();
                            packets.sendDeleteViewPacket(thisclient, view.ViewID);
                            thisclient.viewMan.removeViewByViewId(view.ViewID);
                        }
                    }
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

                    foreach (List<StaticWorldObject> cell in grid.GetNeighborCells((float)playerX, (float)playerZ, ViewRange))
                    {
                        foreach (StaticWorldObject staticWorldObject in cell)
                        {
                            if (!mathUtils.IsInCircle((float)playerX, (float)playerZ,
                                    (float)staticWorldObject.pos_x, (float)staticWorldObject.pos_z, ViewRange))
                            {
                                continue;
                            }

                            UInt16 typeId = NumericalUtils.ByteArrayToUint16(staticWorldObject.type, 1);
                            // WE get all staticObjects in range but we dont just want them all
                            switch (typeId)
                            {
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
                }
            }
        }

        private static void CheckPlayerMobViews(List<WorldClient> clients, Dictionary<ushort, SpatialGrid<Mob>> mobGrids,
            Dictionary<ulong, Mob> mobsByEntity)
        {
            Maths mathUtils = new Maths();
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

                HashSet<ulong> nearbyMobEntities = new HashSet<ulong>();
                foreach (List<Mob> cell in grid.GetNeighborCells((float)playerX, (float)playerZ, ViewRange))
                {
                    foreach (Mob thismob in cell)
                    {
                        if (thismob == null)
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

                        // Spawn Mob if its in Visibility Range
                        ClientView mobView = thisclient.viewMan.GetViewForEntityAndGo(thismob.getEntityId(),
                            NumericalUtils.ByteArrayToUint16(thismob.getGoId(), 1));
                        if (mobView.viewCreated == false &&
                            thismob.getDistrict() == thisclient.playerData.getDistrictId() &&
                            thisclient.playerData.getOnWorld() && mobIsInCircle)
                        {
#if DEBUG
                            ServerPackets pak = new ServerPackets();
                            pak.sendSystemChatMessage(thisclient,
                                "Mob with Name " + thismob.getName() + " with new View ID " + mobView.ViewID +
                                " spawned", "BROADCAST");
#endif

                            ServerPackets mobPak = new ServerPackets();
                            mobPak.SpawnMobView(thisclient, thismob, mobView);
                            mobView.spawnId = thisclient.playerData.spawnViewUpdateCounter;
                            mobView.viewCreated = true;
                            thismob.isUpdateable = true;
                            thismob.DoMobUpdate(thismob);
                        }
                    }
                }

                if (thisclient.viewMan.views.Count == 0)
                {
                    continue;
                }

                List<UInt16> viewsToRemove = new List<UInt16>();
                List<ClientView> viewSnapshot = new List<ClientView>(thisclient.viewMan.views);
                foreach (ClientView view in viewSnapshot)
                {
                    if (!view.viewCreated)
                    {
                        continue;
                    }

                    if (!mobsByEntity.TryGetValue(view.entityId, out Mob mob))
                    {
                        continue;
                    }

                    if (!nearbyMobEntities.Contains(view.entityId) || mob.getDistrict() != districtId)
                    {
                        // Delete Mob's View from Client if we are outside
                        ServerPackets packets = new ServerPackets();
                        packets.sendDeleteViewPacket(thisclient, view.ViewID);
#if DEBUG
                        packets.sendSystemChatMessage(thisclient,
                            "MobView (" + mob.getName() + " LVL: " + mob.getLevel() +
                            " ) with View ID " + view.ViewID + " is out of range and is deleted!",
                            "MODAL");
#endif
                        viewsToRemove.Add(view.ViewID);
                        mob.isUpdateable = false;
                    }
                }

                foreach (UInt16 viewId in viewsToRemove)
                {
                    thisclient.viewMan.removeViewByViewId(viewId);
                }
            }
        }

        private static void CheckPlayerViews(List<WorldClient> clients,
            Dictionary<uint, SpatialGrid<WorldClient>> clientGrids,
            Dictionary<ulong, WorldClient> clientsByEntity)
        {
            Maths mathUtils = new Maths();
            foreach (WorldClient currentClient in clients)
            {
                if (currentClient == null || currentClient.Alive == false)
                {
                    continue;
                }

                if (currentClient.playerData.getOnWorld() == false || currentClient.playerData.waitForRPCShutDown)
                {
                    continue;
                }

                uint districtId = currentClient.playerData.getDistrictId();
                if (!clientGrids.TryGetValue(districtId, out SpatialGrid<WorldClient> grid))
                {
                    continue;
                }

                double currentPlayerX = 0;
                double currentPlayerY = 0;
                double currentPlayerZ = 0;
                NumericalUtils.LtVector3dToDoubles(currentClient.playerInstance.Position.getValue(),
                    ref currentPlayerX, ref currentPlayerY, ref currentPlayerZ);

                HashSet<ulong> nearbyPlayerEntities = new HashSet<ulong>();
                foreach (List<WorldClient> cell in grid.GetNeighborCells((float)currentPlayerX, (float)currentPlayerZ, ViewRange))
                {
                    foreach (WorldClient otherClient in cell)
                    {
                        if (otherClient == null || otherClient == currentClient)
                        {
                            continue;
                        }

                        if (otherClient.Alive == false || otherClient.playerData.getOnWorld() == false ||
                            otherClient.playerData.waitForRPCShutDown)
                        {
                            continue;
                        }

                        if (otherClient.playerData.getDistrictId() != districtId)
                        {
                            continue;
                        }

                        double otherPlayerX = 0;
                        double otherPlayerY = 0;
                        double otherPlayerZ = 0;
                        NumericalUtils.LtVector3dToDoubles(otherClient.playerInstance.Position.getValue(),
                            ref otherPlayerX, ref otherPlayerY, ref otherPlayerZ);

                        bool playerIsInCircle = mathUtils.IsInCircle((float)currentPlayerX, (float)currentPlayerZ,
                            (float)otherPlayerX, (float)otherPlayerZ, ViewRange);

                        if (!playerIsInCircle)
                        {
                            continue;
                        }

                        nearbyPlayerEntities.Add(otherClient.playerData.getEntityId());

                        ClientView clientView = currentClient.viewMan.GetViewForEntityAndGo(
                            otherClient.playerData.getEntityId(),
                            NumericalUtils.ByteArrayToUint16(otherClient.playerInstance.GetGoid(), 1));

                        if (clientView.viewCreated == false &&
                            currentClient.playerData.getDistrictId() == otherClient.playerData.getDistrictId() &&
                            otherClient.playerData.getOnWorld() && currentClient.playerData.getOnWorld() &&
                            playerIsInCircle)
                        {
                            // Spawn player
                            ServerPackets pak = new ServerPackets();
                            pak.sendSystemChatMessage(currentClient,
                                "Player " + StringUtils.charBytesToString_NZ(otherClient.playerInstance
                                    .CharacterName.getValue()) + " with new View ID " +
                                clientView.ViewID + " jacked in", "BROADCAST");
                            pak.SendPlayerSpawn(currentClient, otherClient, clientView.ViewID);
                            clientView.spawnId = currentClient.playerData.spawnViewUpdateCounter;
                            clientView.viewCreated = true;
                        }
                    }
                }

                if (currentClient.viewMan.views.Count == 0)
                {
                    continue;
                }

                UInt16 playerGoId = NumericalUtils.ByteArrayToUint16(currentClient.playerInstance.GetGoid(), 1);
                List<UInt16> viewsToRemove = new List<UInt16>();
                List<ClientView> viewSnapshot = new List<ClientView>(currentClient.viewMan.views);
                foreach (ClientView view in viewSnapshot)
                {
                    if (!view.viewCreated || view.GoID != playerGoId)
                    {
                        continue;
                    }

                    if (!nearbyPlayerEntities.Contains(view.entityId))
                    {
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
                        viewsToRemove.Add(view.ViewID);
                    }
                }

                foreach (UInt16 viewId in viewsToRemove)
                {
                    currentClient.viewMan.removeViewByViewId(viewId);
                }
            }
        }

        private static void CleanDeadPlayers(List<WorldClient> deadClients, List<WorldClient> allClients)
        {
            if (deadClients.Count == 0)
            {
                return;
            }

            HashSet<WorldClient> deadLookup = new HashSet<WorldClient>(deadClients);
            foreach (WorldClient deadClient in deadClients)
            {
                if (deadClient == null)
                {
                    continue;
                }

                CombatManager.RemoveClient(deadClient);
                foreach (WorldClient otherclient in allClients)
                {
                    if (otherclient == null || otherclient == deadClient)
                    {
                        continue;
                    }

                    ClientView view = otherclient.viewMan.GetViewForEntityAndGo(deadClient.playerData.getEntityId(),
                        NumericalUtils.ByteArrayToUint16(deadClient.playerInstance.GetGoid(), 1));

                    Store.dbManager.WorldDbHandler.SetOnlineStatus(otherclient.playerData.getCharID(), 0);
                    ServerPackets pak = new ServerPackets();
                    pak.sendDeleteViewPacket(otherclient, view.ViewID);
                    Store.margin.RemoveClientsByCharId(otherclient.playerData.getCharID());
                }

                string handle = StringUtils.charBytesToString_NZ(deadClient.playerInstance.CharacterName.getValue());
                new BuddylistHandler().ProcessAnnounceFriendsOffline(deadClient.playerData.getCharID(), handle);
                // Views are now deleted to other players
                // ToDo: Cleanup Missions (kill all running missions the player have)
                // ToDo: Cleanup Teams (if your mission team has more than one player, you need to announce an update for the mission team to your mates)
                // ToDo: Announce friendlists from other users that you are going offline (just collect all players whohave this client in list and send the packet)
                // ToDo: Finally save the current character Data to the Database^^
            }

            List<string> keysToRemove = new List<string>();
            lock (WorldServer.Clients)
            {
                foreach (KeyValuePair<string, WorldClient> entry in WorldServer.Clients)
                {
                    if (deadLookup.Contains(entry.Value))
                    {
                        keysToRemove.Add(entry.Key);
                    }
                }

                foreach (string key in keysToRemove)
                {
                    Output.WriteLine("Removed inactive Client with Key " + key);
                    WorldServer.Clients.Remove(key);
                }
            }
        }
    }
}
