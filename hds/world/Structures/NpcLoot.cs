using System;
using System.Collections.Generic;
using hds.shared;

namespace hds
{
    /// <summary>
    /// Items a killed NPC drops. The retail loot windows (81 19) in research/packet-dumps mostly held Code Bits 1-8
    /// (goids 7332-7339, the crafting material), now and then a Health Pill 1.0 (2399) and rarely clothing, class
    /// routines or event items; up to three items, often none. The odds below are a rough fit to those windows.
    /// </summary>
    public static class NpcLoot
    {
        public const UInt32 CodeBit1 = 7332; // Code Bit 1 .. Code Bit 8 are 7332 .. 7339
        public const UInt32 HealthPill1 = 2399;

        /// <summary>Code Bit tier for an NPC level: 1 up to level 9, then one tier per ten levels, at most 8.</summary>
        public static UInt32 CodeBitFor(ushort level)
        {
            int tier = Math.Min(8, Math.Max(1, 1 + level / 10));
            return CodeBit1 + (UInt32)(tier - 1);
        }

        public static List<UInt32> Roll(ushort level, Random random)
        {
            List<UInt32> items = new List<UInt32>();
            if (random.NextDouble() < 0.6)
            {
                items.Add(CodeBitFor(level));
            }
            if (random.NextDouble() < 0.2)
            {
                items.Add(CodeBitFor((ushort)Math.Max(0, level - 10)));
            }
            if (random.NextDouble() < 0.15)
            {
                items.Add(HealthPill1);
            }
            return items;
        }

        /// <summary>
        /// Retail loot window: <c>81 19 00 00 00 00 10 00 &lt;info u32&gt; 00 00 00 00 &lt;count u16&gt;</c> then per item
        /// <c>00 00 00 &lt;flags&gt; &lt;goid u32&gt;</c> (e.g. <c>... 03 00 | 00 00 00 00 a7 1c 00 00 | 00 00 00 00 51 b8 00 00 | ...</c>).
        /// Code Bits and pills had flags 00; mission items go out with 08, which the client already accepted.
        /// </summary>
        public static byte[] BuildLootWindow(UInt32 info, IList<UInt32> items, IList<byte> flags)
        {
            PacketContent pak = new PacketContent();
            pak.AddUint16((UInt16)RPCResponseHeaders.SERVER_LOOT_WINDOW_RESPONSE, 0);
            pak.AddHexBytes("000000001000");
            pak.AddUint32(info, 1);
            pak.AddHexBytes("00000000");
            pak.AddUint16((UInt16)items.Count, 1);
            for (int i = 0; i < items.Count; i++)
            {
                pak.AddHexBytes("000000");
                pak.AddByte(flags[i]);
                pak.AddUint32(items[i], 1);
            }
            return pak.ReturnFinalPacket();
        }

        public static void Drop(WorldClient client, Mob source, UInt32 info, UInt32 missionItem, IEnumerable<UInt32> items)
        {
            lock (client.playerData.loot)
            {
                SendWindow(client, client.playerData.loot.Add(source, info, missionItem, items));
            }
        }

        /// <summary>Reopens the killer's saved contents for this corpse, including a partially looted full bag.</summary>
        public static void Open(WorldClient client, UInt64 entity)
        {
            lock (client.playerData.loot)
            {
                SendWindow(client, client.playerData.loot.Select(entity));
            }
            client.FlushQueue();
        }

        private static void SendWindow(WorldClient client, NpcLootCache.Corpse corpse)
        {
            List<UInt32> items = new List<UInt32>();
            List<byte> flags = new List<byte>();
            if (corpse != null)
            {
                if (corpse.MissionItem != 0)
                {
                    items.Add(corpse.MissionItem);
                    flags.Add(0x08);
                }
                foreach (UInt32 item in corpse.Items)
                {
                    items.Add(item);
                    flags.Add(0);
                }
            }
            client.messageQueue.addRpcMessage(BuildLootWindow(corpse?.Info ?? 0, items, flags));
        }

        /// <summary>Grants the selected corpse once, retaining every item that does not fit.</summary>
        public static void TakeAll(WorldClient client)
        {
            Mob emptied = null;
            ServerPackets packets = new ServerPackets();
            lock (client.playerData.loot)
            {
                NpcLootCache.Corpse corpse = client.playerData.loot.Current;
                if (corpse == null)
                {
                    packets.SendLootAccepted(client);
                    return;
                }

                if (corpse.Info > 0)
                {
                    UInt32 total = (UInt32)Math.Min(client.playerData.getInfo() + (long)corpse.Info, UInt32.MaxValue);
                    Store.dbManager.WorldDbHandler.SaveInfo(client, total);
                    client.playerData.setInfo(total);
                    Output.WriteDebugLog("[LOOT] Player received " + corpse.Info + " info. Total: " + total);
                    corpse.Info = 0;
                }
                packets.SendInfoCurrent(client, (UInt32)client.playerData.getInfo());
                GiveItems(client, corpse.Items);

                var remaining = new List<(byte[] Data, UInt32 Item)>();
                foreach (UInt32 item in corpse.Items)
                {
                    remaining.Add((new byte[4], item));
                }
                if (corpse.MissionItem != 0)
                {
                    if (MissionRuntime.OnMissionItemLooted(client, corpse.MissionItem))
                    {
                        corpse.MissionItem = 0;
                    }
                    else
                    {
                        remaining.Add((new byte[] { 0, 0, 0, 0x08 }, corpse.MissionItem));
                    }
                }
                packets.SendLootAccepted(client, remaining.ToArray());
                if (remaining.Count == 0)
                {
                    emptied = corpse.Source;
                }
            }
            // Tutorial callbacks can remove NPCs. Keep their lock outside the loot cache's lock.
            if (emptied != null)
            {
                TutorialMission.Of(client)?.OnCorpseLooted(emptied);
            }
        }

        /// <summary>Removes each granted item immediately, so a later failure cannot grant it again on retry.</summary>
        private static void GiveItems(WorldClient client, List<UInt32> items)
        {
            int index = 0;
            while (index < items.Count)
            {
                UInt32 goid = items[index];
                // Stacks onto an existing stack (Code Bits, pills) and refuses a full bag
                UInt16 slot = InventoryHandler.AddItem(client, goid, 0);
                if (slot == 0)
                {
                    index++;
                }
                else
                {
                    items.RemoveAt(index);
                }
                Output.WriteDebugLog("[LOOT] Item " + goid + " into slot " + slot);
            }
        }
    }
}
