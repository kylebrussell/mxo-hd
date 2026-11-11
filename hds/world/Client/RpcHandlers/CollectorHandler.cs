using System;
using System.Collections.Generic;
using hds.shared;

namespace hds
{
    public sealed class CollectorExchange
    {
        public UInt16 RewardSlot { get; }
        public IReadOnlyList<(UInt16 Slot, int Remaining)> Consumed { get; }

        public CollectorExchange(UInt16 rewardSlot, IReadOnlyList<(UInt16 Slot, int Remaining)> consumed)
        {
            RewardSlot = rewardSlot;
            Consumed = consumed;
        }
    }

    /// <summary>The native collector dialog. Azuna exchanges ten Function Data for her surveillance.</summary>
    internal static class CollectorHandler
    {
        private const UInt32 AzunaStaticId = 0x2890033d;
        internal const UInt32 FunctionData = 48150;
        private const UInt32 Surveillance = 48149;
        private const int RequiredData = 10;
        private const float InteractionDistance = 450;
        private const UInt32 OfferText = 0x040006c3;
        private const UInt32 ClosingText = 0x040006c0;

        public static bool TryOpen(WorldClient client, StaticWorldObject collector)
        {
            if (collector?.mxoStaticId != AzunaStaticId || collector.metrId != 2)
            {
                return false;
            }
            if (!CanReach(client, collector))
            {
                return true;
            }

            client.playerData.activeCollector = collector;
            byte[] session = Guid.NewGuid().ToByteArray();
            client.messageQueue.addRpcMessage(CollectorPosition(session, collector));
            client.messageQueue.addRpcMessage(CollectorText(session));
            for (int item = 0; item < RequiredData; item++)
            {
                client.messageQueue.addRpcMessage(CollectorItem(session, FunctionData, item == 0 ? Surveillance : 0, false));
            }
            client.messageQueue.addRpcMessage(CollectorItem(session, 0, 0, true));
            client.FlushQueue();
            return true;
        }

        public static void Exchange(WorldClient client, byte[] payload)
        {
            // 81 16 <item-set index u8> <prize-index count u16>. Azuna offers one fixed prize.
            StaticWorldObject collector = client.playerData.activeCollector;
            if (collector == null || payload.Length != 3 || payload[0] != 0 || payload[1] != 0 || payload[2] != 0)
            {
                return;
            }

            client.playerData.activeCollector = null;
            if (!CanReach(client, collector))
            {
                Close(client, false);
                return;
            }

            CollectorExchange exchange;
            try
            {
                exchange = Store.dbManager.WorldDbHandler.ExchangeCollectorItems(
                    client.playerData.getCharID(), FunctionData, RequiredData, Surveillance);
            }
            catch (MySqlConnector.MySqlException error)
            {
                Output.WriteDebugLog("[COLLECTOR] Exchange failed: " + error.Message);
                exchange = null;
            }

            var packets = new ServerPackets();
            if (exchange == null)
            {
                packets.sendSystemChatMessage(client, "Azuna requires ten Function Data and room for her surveillance.", "BROADCAST");
                Close(client, false);
                return;
            }

            foreach ((UInt16 slot, int remaining) in exchange.Consumed)
            {
                if (remaining == 0)
                {
                    packets.sendInventoryItemDelete(slot, client);
                }
                else
                {
                    client.messageQueue.addRpcMessage(ItemStacks.BuildStackCount(slot, remaining));
                }
            }
            packets.sendInventoryItemAdd(exchange.RewardSlot, Surveillance, 0, 0, client);
            Close(client, true);
        }

        private static bool CanReach(WorldClient client, StaticWorldObject collector)
        {
            if (client.playerData.getDistrictId() != collector.metrId)
            {
                return false;
            }
            double x = 0, y = 0, z = 0;
            NumericalUtils.LtVector3dToDoubles(client.playerInstance.Position.getValue(), ref x, ref y, ref z);
            return Math.Abs(y - collector.pos_y) <= InteractionDistance &&
                   HorizontalDistanceSquared(x, z, collector) <= InteractionDistance * InteractionDistance;
        }

        public static bool DropsFunctionData(Mob mob)
        {
            if (mob.missionInstance != 0 || mob.getDistrict() != 2 || mob.getName() != "Override Function")
            {
                return false;
            }
            // The captured Creston Heights spawns are around Azuna's museum floor.
            StaticWorldObject azuna = DataLoader.getInstance().getObjectValues(AzunaStaticId, 2);
            return azuna?.metrId == 2 && Math.Abs(mob.getYPos() - azuna.pos_y) <= 600 &&
                   HorizontalDistanceSquared(mob.getXPos(), mob.getZPos(), azuna) <= 4000 * 4000;
        }

        private static double HorizontalDistanceSquared(double x, double z, StaticWorldObject collector)
        {
            double dx = x - collector.pos_x;
            double dz = z - collector.pos_z;
            return dx * dx + dz * dz;
        }

        // Retail sent 81 15, 81 13 and one or more 81 14 rows followed by a final empty 81 14.
        private static byte[] CollectorPosition(byte[] session, StaticWorldObject collector)
        {
            PacketContent packet = new PacketContent();
            packet.AddHexBytes("8115");
            packet.AddByteArray(session);
            packet.AddDoubleLtVector3d(collector.pos_x, collector.pos_y, collector.pos_z);
            packet.AddFloat(InteractionDistance, 1);
            packet.AddHexBytes("0000000000"); // visible trade and requirements, all required, fixed prizes, first set
            return packet.ReturnFinalPacket();
        }

        private static byte[] CollectorText(byte[] session)
        {
            PacketContent packet = new PacketContent();
            packet.AddHexBytes("8113");
            packet.AddByteArray(session);
            packet.AddUint32(OfferText, 1); // InitialID
            packet.AddUint32(OfferText, 1); // IncompleteID
            packet.AddUint32(OfferText, 1); // CompleteID
            packet.AddUint32(ClosingText, 1);
            return packet.ReturnFinalPacket();
        }

        private static byte[] CollectorItem(byte[] session, UInt32 required, UInt32 prize, bool final)
        {
            PacketContent packet = new PacketContent();
            packet.AddHexBytes("8114");
            packet.AddByteArray(session);
            packet.AddByte(0); // item-set index
            packet.AddUint32(required, 1);
            packet.AddUint32(0, 1); // ReqData
            packet.AddUint32(prize, 1);
            packet.AddUint32(0, 1); // PrizeData
            packet.AddByte(final ? (byte)1 : (byte)0);
            packet.AddHexBytes("00000000000000000000000000000000"); // dialogue comes from 81 13
            return packet.ReturnFinalPacket();
        }

        private static void Close(WorldClient client, bool showClosing)
        {
            client.messageQueue.addRpcMessage(new byte[] { 0x81, 0x17, 0, 0, 0, 0, showClosing ? (byte)1 : (byte)0 });
            client.FlushQueue();
        }
    }
}
