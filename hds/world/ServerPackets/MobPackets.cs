using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using hds.shared;
namespace hds
{
    public partial class ServerPackets 
    {
        public void SpawnMobView(WorldClient client, Mob thismob, ClientView mobView)
        {
            Output.WriteDebugLog("Spawn MobView Data ( Entity ID : " + thismob.getEntityId() + " Name:" + thismob.getName() + " ID: " + thismob.getMobId() + " RSI HEX: " + thismob.getRsiHex());
            GameObject viewData = thismob.getCreationData();
            PacketContent pak = new PacketContent();
            
            pak.AddUint16(1, 1);
            pak.AddByteArray(Store.world.objMan.GenerateCreationPacket(viewData, 0x0000, client.playerData.assignSpawnIdCounter()).getBytes());
            pak.AddUint16(mobView.ViewID, 1);
            pak.AddByte(0x00);

            client.messageQueue.addObjectMessage(pak.ReturnFinalPacket(), false);
            client.FlushQueue();
        }

        public void SendNpcUpdateData(UInt16 viewId, WorldClient client, byte[] updateData)
        {
            PacketContent pak = new PacketContent();
            
            pak.AddUint16(viewId, 1);
            pak.AddByteArray(updateData);
            
            client.messageQueue.addObjectMessage(pak.ReturnFinalPacket(), false);
            client.FlushQueue();
            
        }

        public void SendNpcUpdateAnimation(UInt16 viewId, WorldClient client, Mob theMob, byte animation )
        {
            PacketContent pak = new PacketContent();
            pak.AddUint16(viewId, 1);
            pak.AddByte(0x02);
            pak.AddByte(0x0e);
            pak.AddByte(animation);
            pak.AddUShort((UInt16)theMob.getRotation());
            pak.AddFloatLtVector3f((float)theMob.getXPos(), (float)theMob.getYPos(), (float)theMob.getZPos());


            client.messageQueue.addObjectMessage(pak.ReturnFinalPacket(), false);
            client.FlushQueue();

        }

        /// <summary>
        /// Sends the NPC's death to every player who has it spawned (each through their own view of it),
        /// not only to the killer, so bystanders see it die too.
        /// </summary>
        public void SendNpcDies(UInt16 viewId, WorldClient client, Mob theMob)
        {
            PacketContent death = new PacketContent();
            death.AddByteArray(BuildNpcDeathUpdate());
            Store.world.SendViewUpdateToClientsWhoHasMobSpawned(death, theMob);
        }

        /// <summary>
        /// NPC_BASE view update for a death, as retail sent it when NPCs died (a zombie killed in melee,
        /// and with an extra effect after an ability kill):
        /// <c>05 01 c2 02 00 80 80 e0 01 00 00 80 80 03 fd 07 00 00 00 00 00 00</c>.
        /// 05 = parts 01 (one byte, 01) and 04 (attribute groups). The groups select, by view index in
        /// AttributeClass3827: UseCount=2, StopFollowActiveTracker=0, IsDead=1, Health=0,
        /// CurrentState=0x7fd (dead) and ConditionStateFlags=0. Verified with the 7.6005 client: the NPC
        /// stops offering combat and the corpse can be looted. The retail zombie death also carried a
        /// movement part (02, flags 1d) whose layout isn't known; guessing it (02 01 0d) made the
        /// client reject the whole update.
        /// </summary>
        public static byte[] BuildNpcDeathUpdate()
        {
            PacketContent pak = new PacketContent();
            pak.AddHexBytes("0501c202008080e0010000808003fd07000000000000");
            return pak.ReturnFinalPacket();
        }


        public void SendNpcUpdatePos(UInt16 viewId, WorldClient client, Mob theMob)
        {
            PacketContent pak = new PacketContent();
            pak.AddUint16(viewId, 1);
            pak.AddByte(0x02);
            pak.AddByte(0x0c);
            pak.AddUShort((UInt16)theMob.getRotation());
            pak.AddFloatLtVector3f((float)theMob.getXPos(), (float)theMob.getYPos(), (float)theMob.getZPos());


            client.messageQueue.addObjectMessage(pak.ReturnFinalPacket(), false);
            client.FlushQueue();
        }

        public void SendLootWindow(UInt32 infoAmount, WorldClient client, UInt32[] objectIds)
        {
            // ToDo: figure the real object packet out (with more than one object) and show it up
            PacketContent pak = new PacketContent();
            pak.AddUint16((UInt16)RPCResponseHeaders.SERVER_LOOT_WINDOW_RESPONSE,0);
            pak.AddHexBytes("000000001000");
            pak.AddUint32(infoAmount,1);
            pak.AddHexBytes("00000000010000000008");
            if (objectIds.Length > 0)
            {
                pak.AddUint32(objectIds[0],1);
            }
            client.messageQueue.addRpcMessage(pak.ReturnFinalPacket());
            client.FlushQueue();
        }

        public void SendLootAccepted(WorldClient client)
        {
            PacketContent pak = new PacketContent();
            pak.AddUint16((UInt16)RPCResponseHeaders.SERVER_LOOT_ACCEPTED_RESPONSE,0);
            client.messageQueue.addRpcMessage(pak.ReturnFinalPacket());
            client.FlushQueue();
        }
    }
}
