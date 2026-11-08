using System;
using hds.shared;

namespace hds
{
    /// <summary>
    /// Stacked inventory items (Code Bits, code fragments, pills). Retail keeps a stack's size as
    /// (count - 1) * 4:
    /// <list type="bullet">
    /// <item>in the item add <c>5e &lt;slot&gt; &lt;goid&gt; 00 00 &lt;amount&gt; &lt;type&gt; 01</c>, e.g. a marketplace stack of 20 Code
    /// Bit 8 <c>5e 44 00 ab 1c 00 00 00 00 4c 00 01</c> (luizoe_recursion1 line 409; it sold for 20,000, 9 for 9,000 with
    /// amount 20, 4 for 4,000 with amount 0c);</item>
    /// <item>in the stack update <c>69 01 &lt;slot u32&gt; &lt;(count - 1) * 4 u32&gt;</c>: buying Icy Snowballs one at a time
    /// (RajkoPacketsNearTheEnd_PacketsMovingAroundDowntown 20240-20374) sent 5e with amount 00, then 69 01 36 00 00 00
    /// with 04, 08, 0c, ...; using an Oracle cookie from a stack sent 69 01 instead of 5f.</item>
    /// </list>
    /// The database <c>inventory.count</c> holds the number of items; 0 (every older row) means one.
    /// </summary>
    public static class ItemStacks
    {
        /// <summary>The amount byte holds (count - 1) * 4, so a stack holds at most 64.</summary>
        public const int MaxStack = 64;

        public static int CountFromDb(int dbCount)
        {
            return Math.Max(1, dbCount);
        }

        /// <summary>The 5e / margin-inventory amount byte for a stack of <paramref name="count"/>.</summary>
        public static byte AmountByte(int count)
        {
            int clamped = Math.Min(MaxStack, Math.Max(1, count));
            return (byte)((clamped - 1) << 2);
        }

        /// <summary><c>69 01 &lt;slot u32&gt; &lt;(count - 1) * 4 u32&gt;</c></summary>
        public static byte[] BuildStackCount(UInt16 slot, int count)
        {
            PacketContent pak = new PacketContent();
            pak.AddByte(0x69);
            pak.AddByte(0x01);
            pak.AddUint32(slot, 1);
            pak.AddUint32((UInt32)((Math.Max(1, count) - 1) << 2), 1);
            return pak.ReturnFinalPacket();
        }

        /// <summary>Code Bits, code fragments and consumables stack; everything else takes a slot each.</summary>
        public static bool IsStackable(UInt32 goid)
        {
            if (goid >= 7332 && goid <= 7339)
            {
                return true; // Code Bit 1-8
            }

            if (ConsumableItems.Items.ContainsKey(goid))
            {
                return true;
            }

            // Class Routines, Function Subroutines and Variable Fragments (invitems.txt ClassFrag_*, FuncFrag_*,
            // VariableFrag_*; cookbook types FCC0, FFC0, FVC0).
            return (goid >= 2435 && goid <= 2480) || goid == 7458 || (goid >= 9139 && goid <= 9170);
        }
    }
}
