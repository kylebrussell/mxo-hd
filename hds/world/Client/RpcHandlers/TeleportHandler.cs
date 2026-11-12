using System;
using System.Collections.Generic;
using hds.shared;

namespace hds
{
    class TeleportHandler
    {
        /// <summary>
        /// 6c &lt;district u32&gt; &lt;u32&gt;: the client asks which hardlines of a district it can use, for the
        /// hardline map. Retail answered with the hardlines the character had uploaded (lists of 1 to 52 for
        /// Richland across the captures; a fresh tutorial character had 1). With HardlineUploadRequired off (the
        /// default) every hardline of the district is listed.
        /// </summary>
        public void processHardlineStatusRequest(ref byte[] packet)
        {
            if (packet.Length < 2)
            {
                return;
            }

            UInt16 district = (UInt16)(packet[0] | (packet[1] << 8));
            List<UInt16> hardlines = Store.worldConfig.HardlineUploadRequired
                ? Store.dbManager.WorldDbHandler.GetUploadedHardlines(Store.currentClient.playerData.getCharID(), district)
                : HardlineNetwork.Covers(district)
                    ? HardlineNetwork.Ids(district)
                    : Store.dbManager.WorldDbHandler.GetHardlineIdsForDistrict(district);
            Output.WriteDebugLog("[HARDLINE] Status for district " + district + ": " + hardlines.Count + " hardlines");
            new ServerPackets().SendHardlineStatus(Store.currentClient, district, hardlines);
        }

        /// <summary>
        /// Puts the player at a hardline: the client's own network (HardlineNetwork) for the three cities, data_hardlines
        /// for the rest (the constructs).
        /// </summary>
        public static void MoveToHardline(WorldClient client, UInt16 district, UInt16 hardline)
        {
            if (!HardlineNetwork.MoveTo(client, district, hardline))
            {
                Store.dbManager.WorldDbHandler.updateLocationByHL(district, hardline);
            }
        }

        /// <summary>The hardline whose booth is nearest to (x, z): from the network in the cities, else data_hardlines.</summary>
        public static UInt16 NearestHardline(UInt16 district, double x, double z)
        {
            return HardlineNetwork.Covers(district)
                ? HardlineNetwork.Nearest(district, x, z)
                : Store.dbManager.WorldDbHandler.GetNearestHardline(district, x, z);
        }

        public void processTeleportReset(ref byte[] packet)
        {
            Store.currentClient.playerData.setRPCShutDown(true);

            // We want to reset
            PacketContent pak = new PacketContent();
            pak.AddUint16((UInt16) RPCResponseHeaders.SERVER_LOAD_WORLD, 0);
            Store.currentClient.messageQueue.addRpcMessage(pak.ReturnFinalPacket());
        }

        /// <summary>
        /// 81 8e &lt;source hardline u32&gt; &lt;source district u32&gt; &lt;target hardline u32&gt; &lt;target district u32&gt;.
        /// Retail answered with 81 07; after the client's 81 08 reset it loaded the target district (06) and
        /// spawned the player at the hardline, across districts too (actions_combat: Richland → Downtown →
        /// International).
        /// </summary>
        public void processHardlineTeleport(ref byte[] packet)
        {
            if (packet.Length < 16)
            {
                Output.WriteDebugLog("[HARDLINE] Teleport request too short");
                return;
            }

            byte[] sourceHardline = new byte[2];
            sourceHardline[0] = packet[0];
            sourceHardline[1] = packet[1];

            byte[] sourceDistrict = new byte[2];
            sourceDistrict[0] = packet[4];
            sourceDistrict[1] = packet[5];

            byte[] destHardline = new byte[2];
            destHardline[0] = packet[8];
            destHardline[1] = packet[9];

            byte[] destDistrict = new byte[2];
            destDistrict[0] = packet[12];
            destDistrict[1] = packet[13];

            UInt16 sourceHL = NumericalUtils.ByteArrayToUint16(sourceHardline, 1);
            UInt16 sourceDIS = NumericalUtils.ByteArrayToUint16(sourceDistrict, 1);

            UInt16 destHL = NumericalUtils.ByteArrayToUint16(destHardline, 1);
            UInt16 destDIS = NumericalUtils.ByteArrayToUint16(destDistrict, 1);

            // This should do the magic - we just catch
            CraftingSession.End(Store.currentClient);
            MoveToHardline(Store.currentClient, destDIS, destHL);
            Store.dbManager.WorldDbHandler.updateSourceHlForObjectTracking(sourceDIS, sourceHL,
                Store.currentClient.playerData.lastClickedObjectId);

#if DEBUG
            ServerPackets serverPak = new ServerPackets();
            serverPak.sendSystemChatMessage(Store.currentClient,
                "User wants teleport from : HL ID: " + sourceHL.ToString() + " (DIS: " + sourceDIS.ToString() +
                " ) TO HL ID: " + destHL.ToString() + " (DIS: " + destDIS.ToString() + ") ", "MODAL");
#endif

            // Tell client we want to unload the World
            PacketContent pak = new PacketContent();
            pak.AddUint16((UInt16) RPCResponseHeaders.SERVER_LOAD_RPC_RESET, 0);
            Store.currentClient.messageQueue.addRpcMessage(pak.ReturnFinalPacket());
            Store.currentClient.FlushQueue();
        }

        public void processHardlineExitConfirm(ref byte[] packet)
        {
            ServerPackets packets = new ServerPackets();
            switch (packet[0])
            {
                case 0x00:
                    // Jack out at a hardline (after the 80 fb ... 00 prompt): retail sent 80 fd 00 00 00 00 00 00
                    // 0-1 s later in all 13 captured hardline jack-outs, no 30 s wait; the client then quits
                    // with 80 fe 00.
                    Output.WriteDebugLog("[HARDLINE] Jack out at a hardline");
                    packets.sendExitGame(Store.currentClient);
                    Store.currentClient.FlushQueue();
                    break;

                case 0x01:
                    // "Exit to Loading Area": retail answered 80 fd ... 01 00, the client confirmed with 80 fe 01
                    // and only then was the Loading Area loaded (see processGameFinish).
                    Output.WriteDebugLog("[LA] Exit to the Loading Area requested at a hardline");
                    Store.currentClient.messageQueue.addRpcMessage(ServerPackets.BuildJackoutToLoadingArea());
                    Store.currentClient.FlushQueue();
                    break;

                case 0x02:
                    Store.currentClient.playerData.isJackoutInProgress = true;
                    Store.currentClient.playerData.jackoutStartTime = TimeUtils.getUnixTimeUint32();
                    packets.sendJackoutEffect(Store.currentClient);
                    break;
            }
        }

        /// <summary>Seconds between the jack-out request (80 fc 02) and the server's 80 fd finish in retail.</summary>
        public const uint JackoutSeconds = 30;

        public static bool JackoutFinished(uint startTime, uint now)
        {
            return now >= startTime && now - startTime >= JackoutSeconds;
        }

        public void processHardlineExitRequest()
        {
            ServerPackets packets = new ServerPackets();
            packets.sendJackoutEffect(Store.currentClient);
        }

        /// <summary>
        /// The player used a hardline booth (80 c7 on its view, or 80 c8 on the static object): 03 jack out,
        /// 04 upload, 05 exit to the Loading Area. See HardlineBooths for the retail exchanges.
        /// </summary>
        public static void ProcessBoothAction(WorldClient client, byte action, StaticWorldObject booth)
        {
            UInt16 district = (UInt16)client.playerData.getDistrictId();
            UInt16 hardline = NearestHardline(district, booth.pos_x, booth.pos_z);
            Output.WriteDebugLog("[HARDLINE] Booth " + booth.mxoStaticId + " action " + action + " -> hardline " + hardline +
                                 " in district " + district);
            MissionRuntime.OnHardline(client, action == HardlineBooths.ActionUpload);

            switch (action)
            {
                case HardlineBooths.ActionJackOut:
                    client.messageQueue.addRpcMessage(HardlineBooths.BuildExitPrompt(hardline, false));
                    break;

                case HardlineBooths.ActionExitToLoadingArea:
                    client.messageQueue.addRpcMessage(HardlineBooths.BuildExitPrompt(hardline, true));
                    break;

                case HardlineBooths.ActionUpload:
                    if (hardline != 0 &&
                        Store.dbManager.WorldDbHandler.AddUploadedHardline(client.playerData.getCharID(), district, hardline))
                    {
                        Output.WriteDebugLog("[HARDLINE] Uploaded hardline " + hardline + " in district " + district);
                    }
                    // Upload also moves the bag's Code Bits and fragments into code storage; its records go before the 81 18
                    int uploaded = CodeStorageHandler.UploadCode(client, out bool abilityCodeUploaded);
                    client.messageQueue.addRpcMessage(uploaded == 0
                        ? HardlineBooths.BuildUploadDone()
                        : HardlineBooths.BuildItemsUploaded(uploaded, abilityCodeUploaded));
                    break;

                case 0x07: // Hack Construct (rule 10024 HardlineHackConstruct)
                    UtilityAbilities.HackConstruct(client);
                    break;

                default:
                    Output.WriteDebugLog("[HARDLINE] Unknown booth action " + action);
                    return;
            }

            client.FlushQueue();
        }

        /// <summary>
        /// 80 fe &lt;mode&gt;: 01 goes to the Loading Area (after Reconstruct, or after "Exit to Loading Area" at a
        /// hardline), 00 quits.
        /// </summary>
        public void processGameFinish(ref byte[] packet)
        {
            WorldClient client = Store.currentClient;
            if (packet.Length > 0 && packet[0] == 0x01)
            {
                if (client.playerData.DistrictIsLA())
                {
                    Output.WriteDebugLog("[LA] 80 fe 01 while already in the Loading Area ignored");
                    return;
                }

                GoToLoadingArea(client);
                return;
            }

            Store.dbManager.WorldDbHandler.SetOnlineStatus(client.playerData.getCharID(), 0);
            ServerPackets packets = new ServerPackets();
            packets.sendExitGame(client);
        }

        /// <summary>
        /// 81 4c from a dead player. Retail (three deaths in the afterwhoruneo/morpheuzzz captures) healed the
        /// player in place and answered 80 fd 00 00 00 00 01 01; the client then confirms with 80 fe 01.
        /// </summary>
        public void ProcessRespawnRequest()
        {
            WorldClient client = Store.currentClient;
            // A duel's loser isn't dead: the same request stands them up where they are
            if (DuelHandler.StandUp(client))
            {
                return;
            }
            UInt16 maxHealth;
            lock (client.playerData.lifeSync)
            {
                if (!client.playerData.isDead)
                {
                    Output.WriteDebugLog("[DEATH] Respawn request from a living player ignored");
                    return;
                }
                maxHealth = NumericalUtils.ByteArrayToUint16(client.playerInstance.MaxHealth.getValue(), 1);
                client.playerInstance.Health.setValue(maxHealth);
                client.playerData.lifeGeneration++;
                client.playerData.respawnPending = true;
            }
            // The heal below clears every ConditionStateFlags bit, the PvP flag with it, as retail's did after a PvP
            // death; a PvP server flags the player again when they next fight (PvpFlag.OnFightStarts)
            PvpFlag.ClearForRespawn(client);

            client.messageQueue.addObjectMessage(ServerPackets.BuildSelfRespawnHeal(maxHealth), false);
            client.messageQueue.addRpcMessage(ServerPackets.BuildRespawnToLoadingArea());
            client.FlushQueue();
            Output.WriteDebugLog("[DEATH] Respawn: healed to " + maxHealth + ", sending the player to the Loading Area");
        }

        /// <summary>
        /// Stores the hardline nearest to the player's current spot as their saved location. The Loading Area
        /// itself can't be saved (see SavePlayer), so this is where a player who disconnects in the LA comes back.
        /// </summary>
        private static void SaveNearestHardlineAsLocation(WorldClient client)
        {
            double x = 0, y = 0, z = 0;
            NumericalUtils.LtVector3dToDoubles(client.playerInstance.Position.getValue(), ref x, ref y, ref z);
            UInt16 district = (UInt16)client.playerData.getDistrictId();
            UInt16 hardline = NearestHardline(district, x, z);
            Output.WriteDebugLog("[LA] Fallback location: hardline " + hardline + " in district " + district);

            if (hardline != 0)
            {
                MoveToHardline(client, district, hardline);
            }
        }

        /// <summary>
        /// Moves the player into the Loading Area as retail did after Reconstruct or "Exit to Loading Area": 81 07,
        /// the client resets (81 08), then 81 09 + 06 la.metr (sendWorldCMD). The client then sends 05 and 6c
        /// &lt;district&gt;, gets no character spawn (PlayerHandler.processSpawn) and shows the hardline picker; the
        /// pick comes back as 80 ff (crashover2k died_in_pvp packets 1325-1384, exit_to_loadingarea 16-69).
        /// </summary>
        public static void GoToLoadingArea(WorldClient client)
        {
            SaveNearestHardlineAsLocation(client);
            CraftingSession.End(client);
            client.playerData.setOnWorld(false);
            client.playerData.worldChangePending = true;

            client.playerData.respawnPending = false;
            client.playerData.isDead = false;
            client.playerInstance.IsDead.setValue(false);
            client.playerData.setDistrictId((uint)MxOLocations.LA);
            client.playerData.setDistrict("la");

            PacketContent pak = new PacketContent();
            pak.AddUint16((UInt16)RPCResponseHeaders.SERVER_LOAD_RPC_RESET, 0);
            client.messageQueue.addRpcMessage(pak.ReturnFinalPacket());
            client.FlushQueue();
            Output.WriteDebugLog("[LA] Loading the Loading Area");
        }

        /// <summary>81 00 selects a tutorial in the LA picker. client.dll62171620..61755 uses it for800..899;
        /// normal selections use80ff. Retail answers8107, then loads tutorial_v2 on a private construct.</summary>
        public static void EnterTutorial(WorldClient client, byte[] payload)
        {
            if (payload.Length != 0 || !client.playerData.DistrictIsLA() || client.playerData.worldChangePending) return;
            CraftingSession.End(client);
            client.playerData.setOnWorld(false);
            client.playerData.worldChangePending = true;
            client.playerData.tutorialConstructId = 800; // fixed local instance; retail allocated800..899
            client.playerData.setDistrictId((uint)MxOLocations.TUTORIAL);
            client.playerData.setDistrict("tutorial");
            client.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(7600,3995,-16150));
            Store.dbManager.WorldDbHandler.SavePlayer(client);
            client.messageQueue.addRpcMessage(new byte[] { 0x81,0x07 });
            client.FlushQueue();
            Output.WriteDebugLog("[LA] Tutorial selection: loading tutorial_v2, construct800");
        }

        /// <summary>80 ff &lt;district u32&gt; &lt;hardline u32&gt;: the player picked a hardline in the Loading Area.</summary>
        public void ProcessLoadingAreaHardline(ref byte[] packet)
        {
            if (packet.Length < 8 || !Store.currentClient.playerData.DistrictIsLA())
            {
                Output.WriteDebugLog("[LA] 80 ff ignored (not in the Loading Area or too short)");
                return;
            }

            PacketReader reader = new PacketReader(packet);
            UInt16 district = (UInt16)reader.ReadUInt32(1);
            UInt16 hardline = (UInt16)reader.ReadUInt32(1);
            Output.WriteDebugLog("[LA] Leaving the Loading Area for district " + district + " hardline " + hardline);

            MoveToHardline(Store.currentClient, district, hardline);
            if (Store.currentClient.playerData.DistrictIsLA())
            {
                // Unknown hardline: updateLocationByHL found no row. Go back to the saved location instead of
                // reloading the Loading Area forever.
                Output.WriteDebugLog("[LA] Unknown hardline " + hardline + " in district " + district + ", using the saved location");
                Store.dbManager.WorldDbHandler.SetPlayerValues();
            }

            PacketContent pak = new PacketContent();
            pak.AddUint16((UInt16)RPCResponseHeaders.SERVER_LOAD_RPC_RESET, 0);
            Store.currentClient.messageQueue.addRpcMessage(pak.ReturnFinalPacket());
            Store.currentClient.FlushQueue();
        }
    }
}
