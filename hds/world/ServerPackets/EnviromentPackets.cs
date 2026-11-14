using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using hds.resources.gameobjects;
using hds.shared;

namespace hds
{
    public partial class ServerPackets
    {
        public void SendSpawnGameObject(WorldClient client, GameObject creationObjectData, UInt64 entityID)
        {
            ClientView staticObjectView = client.viewMan.GetViewForEntityAndGo(entityID,NumericalUtils.ByteArrayToUint16(creationObjectData.GetGoid(),1));
            if (staticObjectView.viewCreated == false)
            {
                byte spawnId = client.playerData.assignSpawnIdCounter();
                staticObjectView.spawnId = spawnId;
                byte[] creation = BuildSpawnGameObject(creationObjectData,staticObjectView.ViewID,spawnId);
                MissionComputer1407Qa.Spawn(client, creationObjectData, staticObjectView, creation);
                client.messageQueue.addObjectMessage(creation, false);
                client.FlushQueue();
                staticObjectView.viewCreated = true;
            }

        }

        // Pure dynamic-object creation codec; never queues or flushes.
        internal static byte[] BuildSpawnGameObject(GameObject obj,ushort view,byte spawn)
        {
            var pak=new PacketContent();pak.AddUint16(1,1);
            pak.AddByteArray(Store.world.objMan.GenerateCreationPacket(obj,0,spawn).getBytes());
            pak.AddUint16(view,1);pak.AddByte(0);return pak.ReturnFinalPacket();
        }

        public void SendUpdateViewStatePacket(WorldClient client, UInt16 viewId, byte[] updateData)
        {
            PacketContent pak = new PacketContent();
            pak.AddUint16(viewId,1);
            pak.AddByteArray(updateData);
            client.messageQueue.addObjectMessage(pak.ReturnFinalPacket(), true);
        }

        public void SendElevatorPanel(WorldClient client, StaticWorldObject objectValues)
        {
            PacketContent pak = new PacketContent();
            pak.AddUint16((UInt16)RPCResponseHeaders.SERVER_ELEVATOR_PANEL,0);
            pak.AddUint16((UInt16)NumericalUtils.RotateRight(objectValues.sectorID,4),1);
            pak.AddHexBytes("4400");
            client.messageQueue.addRpcMessage(pak.ReturnFinalPacket());
        }

        public void SendHardlineExit(WorldClient client)
        {
            PacketContent pak = new PacketContent();
            pak.AddUint16((UInt16)RPCResponseHeaders.SERVER_EXIT_HL, 0);
            pak.AddByte(0x95);
            pak.AddByte(0x00);
            pak.AddByte(0x00);
            pak.AddByte(0x00);
            pak.AddByte(0x01);

            Store.currentClient.messageQueue.addRpcMessage(pak.ReturnFinalPacket());
        }

        /// <summary>
        /// Retail reply to 6c &lt;district u32&gt; (trinitys second_pc_010, 12:41:50):
        /// 6d &lt;district u16&gt; 00 00 08 00 01 &lt;count u16&gt;, then per synced hardline 00 &lt;id u16&gt; 00 00 00.
        /// The ids are data_hardlines.HardLineId for that district (Slums: 48, 49, 55, 72, ...).
        /// </summary>
        public static byte[] BuildHardlineStatus(UInt16 district, IList<UInt16> syncedHardlines)
        {
            PacketContent pak = new PacketContent();
            pak.AddByte((byte)RPCResponseHeaders.SERVER_HARDLINE_STATUS);
            pak.AddUint16(district, 1);
            pak.AddUint16(0, 1);
            pak.AddUint16(8, 1);    // offset of the list descriptor below
            pak.AddByte(0x01);
            pak.AddUint16((UInt16)syncedHardlines.Count, 1);
            foreach (UInt16 id in syncedHardlines)
            {
                pak.AddByte(0x00);
                pak.AddUint16(id, 1);
                pak.AddHexBytes("000000");
            }
            return pak.ReturnFinalPacket();
        }

        public void SendHardlineStatus(WorldClient client, UInt16 district, IList<UInt16> syncedHardlines)
        {
            client.messageQueue.addRpcMessage(BuildHardlineStatus(district, syncedHardlines));
            client.FlushQueue();
        }

        public void SendInteractionSubway(WorldClient client, double x, double y, double z)
        {
            PacketContent pak = new PacketContent();
            pak.AddUint16((UInt16)RPCResponseHeaders.SERVER_SUBWAY_INTERACTION,0);
            pak.AddDouble(x,1);
            pak.AddDouble(y,1);
            pak.AddDouble(z,1);
            client.messageQueue.addRpcMessage(pak.ReturnFinalPacket());
        }
        public void SendSubwaymapWindow(WorldClient client)
        {
            PacketContent pak = new PacketContent();
            pak.AddUint16((UInt16)RPCResponseHeaders.SERVER_SUBWAY_MAP, 0);
            pak.AddUShort(10);
            Store.currentClient.messageQueue.addRpcMessage(pak.ReturnFinalPacket());

        }
    }
}
