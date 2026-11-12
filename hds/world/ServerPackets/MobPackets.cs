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
            // Remember the spawn id this view really got: combat messages the server starts (aggro) carry it
            byte spawnId = client.playerData.assignSpawnIdCounter();
            mobView.spawnId = spawnId;
            pak.AddByteArray(Store.world.objMan.GenerateCreationPacket(viewData, 0x0000, spawnId).getBytes());
            pak.AddUint16(mobView.ViewID, 1);
            pak.AddByte(0x00);

            client.messageQueue.addObjectMessage(pak.ReturnFinalPacket(), false);
            if (thismob.getIsDead() && (thismob.ScriptedCorpse || MissionPersistence.PendingLootOwner(client,thismob)))
                SendNpcUpdateData(mobView.ViewID, client, DeathPresentation.NpcCorpse(thismob));
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
            death.AddByteArray(BuildNpcDeathUpdate(theMob.NextAnimationSeq(), (byte)theMob.getRotation(),
                (float)theMob.getXPos(), (float)theMob.getYPos(), (float)theMob.getZPos()));
            Store.world.SendViewUpdateToClientsWhoHasMobSpawned(death, theMob);
            MobLifecycle.Died(theMob);
        }

        /// <summary>Dead stance in the movement part (retail NPCs: 0x000d, or 0x0005 on a finishing move).</summary>
        public const UInt16 NpcStanceDead = 0x000d;

        /// <summary>
        /// The death with retail's movement part (parts 01, 02 and 04; crashover2k kills of "Bob"):
        /// <c>07 01 | 1d &lt;stance u16&gt; &lt;seq&gt; &lt;animA u16&gt; &lt;animB u16&gt; &lt;u16&gt; &lt;yaw&gt; &lt;x y z f32&gt; | part 04</c>.
        /// Flags 1d = stance (0x01), animation block (0x10), yaw (0x04), position (0x08), in that order. The
        /// animation pair 0x0001/0x002b and the u16 0x0025 are what Bob's dying NPCs got.
        /// </summary>
        public static byte[] BuildNpcDeathUpdate(byte seq, byte yaw, float x, float y, float z)
        {
            PacketContent pak = new PacketContent();
            pak.AddHexBytes("0701");
            pak.AddByte(0x1d);
            pak.AddUint16(NpcStanceDead, 1);
            pak.AddByte(seq);
            pak.AddUint16(0x0001, 1);
            pak.AddUint16(0x002b, 1);
            pak.AddUint16(0x0025, 1);
            pak.AddByte(yaw);
            pak.AddFloatLtVector3f(x, y, z);
            pak.AddHexBytes("c202008080e0010000808003fd07000000000000");
            return pak.ReturnFinalPacket();
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

        public void SendLootAccepted(WorldClient client, (byte[] Data, UInt32 Item)[] remaining = null)
        {
            client.messageQueue.addRpcMessage(BuildLootAccepted(remaining ?? Array.Empty<(byte[], UInt32)>()));
            client.FlushQueue();
        }

        /// <summary>Retail 81 20 returns the unclaimed items, allowing Take All to be retried when the bag was full.</summary>
        internal static byte[] BuildLootAccepted((byte[] Data, UInt32 Item)[] remaining)
        {
            PacketContent pak = new PacketContent();
            pak.AddUint16((UInt16)RPCResponseHeaders.SERVER_LOOT_ACCEPTED_RESPONSE,0);
            pak.AddUint16(12, 1); // ItemArray offset
            pak.AddUint32(0, 1); // Status
            pak.AddUint32(0, 1); // InfoAmount
            pak.AddUint16((UInt16)remaining.Length, 1);
            foreach ((byte[] data, UInt32 item) in remaining)
            {
                pak.AddByteArray(data);
                pak.AddUint32(item, 1);
            }
            return pak.ReturnFinalPacket();
        }
    }
}
