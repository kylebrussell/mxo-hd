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

        /// <summary>Puts each looted item in the first free bag slot and tells the client.</summary>
        public static void GiveItems(WorldClient client, IEnumerable<UInt32> items)
        {
            foreach (UInt32 goid in items)
            {
                // Stacks onto an existing stack (Code Bits, pills) and refuses a full bag
                UInt16 slot = InventoryHandler.AddItem(client, goid, 0);
                Output.WriteDebugLog("[LOOT] Item " + goid + " into slot " + slot);
            }
        }
    }
}
