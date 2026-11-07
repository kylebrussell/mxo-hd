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
                    ServerPackets serverPak = new ServerPackets();
#if DEBUG
                    serverPak.sendSystemChatMessage(Store.currentClient, "Exit to LA init...", "MODAL");
#endif

                    // Tell client we want to reset
                    byte[] response = {0x81, 0x07};
                    Store.currentClient.messageQueue.addRpcMessage(response);
                    break;

                case 0x02:
                    Store.currentClient.playerData.isJackoutInProgress = true;
                    Store.currentClient.playerData.jackoutStartTime = TimeUtils.getUnixTimeUint32();
                    packets.sendJackoutEffect(Store.currentClient);
                    break;
            }
        }

        public void processHardlineExitRequest()
        {
            ServerPackets packets = new ServerPackets();
            packets.sendJackoutEffect(Store.currentClient);
        }

        /// <summary>80 fe &lt;mode&gt;: 01 confirms a death respawn (go to the Loading Area), 00 quits.</summary>
        public void processGameFinish(ref byte[] packet)
        {
            WorldClient client = Store.currentClient;
            if (packet.Length > 0 && packet[0] == 0x01 && client.playerData.respawnPending)
            {
                RespawnAtNearestHardline(client);
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
        /// Retail sent the dead player to the Loading Area and let them pick a hardline there (80 ff), but the
        /// client shows no hardline picker in our LA yet, so the player reconstructs at the hardline nearest to
        /// where they died, through the same world reset (81 07) a hardline teleport uses.
        /// </summary>
        public static void RespawnAtNearestHardline(WorldClient client)
        {
            client.playerData.respawnPending = false;
            client.playerData.isDead = false;
            client.playerInstance.IsDead.setValue(false);

            double x = 0, y = 0, z = 0;
            NumericalUtils.LtVector3dToDoubles(client.playerInstance.Position.getValue(), ref x, ref y, ref z);
            UInt16 district = (UInt16)client.playerData.getDistrictId();
            UInt16 hardline = Store.dbManager.WorldDbHandler.GetNearestHardline(district, x, z);
            Output.WriteDebugLog("[DEATH] Reconstructing at hardline " + hardline + " in district " + district);

            if (hardline != 0)
            {
                Store.dbManager.WorldDbHandler.updateLocationByHL(district, hardline);
            }

            PacketContent pak = new PacketContent();
            pak.AddUint16((UInt16)RPCResponseHeaders.SERVER_LOAD_RPC_RESET, 0);
            client.messageQueue.addRpcMessage(pak.ReturnFinalPacket());
            client.FlushQueue();
        }

        /// <summary>
        /// Moves the player into the Loading Area (la.metr) through the usual world reset (81 07 → client 81 08
        /// → 06 load world). There the client asks for hardline status (6c) and picks one with 80 ff.
        /// </summary>
        public static void GoToLoadingArea(WorldClient client)
        {
            client.playerData.respawnPending = false;
            client.playerData.isDead = false;
            client.playerInstance.IsDead.setValue(false);
            client.playerData.setDistrictId((uint)MxOLocations.LA);
            client.playerData.setDistrict("la");
            // Retail's self-spawn in the Loading Area put the player at (0, 95, 0); the death spot is far outside
            // the LA geometry and left the client floating in an empty sky.
            client.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(0, 95, 0));

            PacketContent pak = new PacketContent();
            pak.AddUint16((UInt16)RPCResponseHeaders.SERVER_LOAD_RPC_RESET, 0);
            client.messageQueue.addRpcMessage(pak.ReturnFinalPacket());
            client.FlushQueue();
            Output.WriteDebugLog("[DEATH] Loading the Loading Area");
        }

        /// <summary>80 ff &lt;district u32&gt; &lt;hardline u32&gt;: the player picked a hardline in the Loading Area.</summary>
        public void ProcessLoadingAreaHardline(ref byte[] packet)
        {
            if (packet.Length < 8)
            {
                return;
            }

            PacketReader reader = new PacketReader(packet);
            UInt16 district = (UInt16)reader.ReadUInt32(1);
            UInt16 hardline = (UInt16)reader.ReadUInt32(1);
            Output.WriteDebugLog("[DEATH] Leaving the Loading Area for district " + district + " hardline " + hardline);

            Store.dbManager.WorldDbHandler.updateLocationByHL(district, hardline);

            PacketContent pak = new PacketContent();
            pak.AddUint16((UInt16)RPCResponseHeaders.SERVER_LOAD_RPC_RESET, 0);
            Store.currentClient.messageQueue.addRpcMessage(pak.ReturnFinalPacket());
            Store.currentClient.FlushQueue();
        }
    }
}