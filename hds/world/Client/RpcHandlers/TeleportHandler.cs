using System;
using System.Collections.Generic;
using hds.shared;

namespace hds
{
    class TeleportHandler
    {
        /// <summary>
        /// 6c &lt;district u32&gt; &lt;u32&gt;: the client asks which hardlines of a district are synced, for the
        /// hardline map. Every hardline counts as synced until per-character sync is stored.
        /// </summary>
        public void processHardlineStatusRequest(ref byte[] packet)
        {
            if (packet.Length < 2)
            {
                return;
            }

            UInt16 district = (UInt16)(packet[0] | (packet[1] << 8));
            List<UInt16> hardlines = Store.dbManager.WorldDbHandler.GetHardlineIdsForDistrict(district);
            Output.WriteDebugLog("[HARDLINE] Status for district " + district + ": " + hardlines.Count + " hardlines");
            new ServerPackets().SendHardlineStatus(Store.currentClient, district, hardlines);
        }

        public void processTeleportReset(ref byte[] packet)
        {
            Store.currentClient.playerData.setRPCShutDown(true);

            // We want to reset
            PacketContent pak = new PacketContent();
            pak.AddUint16((UInt16) RPCResponseHeaders.SERVER_LOAD_WORLD, 0);
            Store.currentClient.messageQueue.addRpcMessage(pak.ReturnFinalPacket());
        }

        public void processHardlineTeleport(ref byte[] packet)
        {
            // we dont care where the journey goes
            // just want to see IF the journey will do :)
            // for this just ack and send 0x42 packet
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
            Store.dbManager.WorldDbHandler.updateLocationByHL(destDIS, destHL);
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
            if (!client.playerData.isDead)
            {
                Output.WriteDebugLog("[DEATH] Respawn request from a living player ignored");
                return;
            }

            UInt16 maxHealth = NumericalUtils.ByteArrayToUint16(client.playerInstance.MaxHealth.getValue(), 1);
            client.playerInstance.Health.setValue(maxHealth);
            client.playerData.respawnPending = true;

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
            UInt16 hardline = Store.dbManager.WorldDbHandler.GetNearestHardline(district, x, z);
            Output.WriteDebugLog("[LA] Fallback location: hardline " + hardline + " in district " + district);

            if (hardline != 0)
            {
                Store.dbManager.WorldDbHandler.updateLocationByHL(district, hardline);
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

            Store.dbManager.WorldDbHandler.updateLocationByHL(district, hardline);
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