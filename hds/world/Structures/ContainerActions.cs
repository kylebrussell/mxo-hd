using System;

namespace hds
{
    /// <summary>81 21 drops a bag slot onto the current container. There is no destination in this request;
    /// the open window is mandatory. Marketplace open is 81 29.</summary>
    public static class ContainerActions
    {
        public static byte? Parse(byte[] body) => body?.Length == 1 && body[0] > 0 && body[0] < Crafting.FirstWornSlot
            ? body[0] : (byte?)null;

        // client.dll 6214176c / 621549a0: ObjectID u32, InstanceData u32, Status u32.
        public static byte[] BuildReply(uint item, uint instanceData, uint status)
        {
            byte[] packet = new byte[14];
            packet[0] = 0x81; packet[1] = 0x22;
            BitConverter.GetBytes(item).CopyTo(packet, 2);
            BitConverter.GetBytes(instanceData).CopyTo(packet, 6);
            BitConverter.GetBytes(status).CopyTo(packet, 10);
            return packet;
        }

        public static bool InReach(byte[] position, double x, double y, double z)
        {
            if (position?.Length != 24) return false;
            double dx = BitConverter.ToDouble(position, 0) - x, dy = BitConverter.ToDouble(position, 8) - y,
                dz = BitConverter.ToDouble(position, 16) - z;
            return Math.Abs(dy) <= 200 && dx * dx + dy * dy + dz * dz <= 600 * 600;
        }

        public static void Process(WorldClient client, byte[] body)
        {
            byte? slot = Parse(body);
            if (!slot.HasValue || client.playerData.isDead || client.playerData.duelDeath ||
                !MissionRuntime.OnPutItem(client, slot.Value))
                client.messageQueue.addRpcMessage(BuildReply(0, 0, 1));
            client.FlushQueue();
        }
    }
}
