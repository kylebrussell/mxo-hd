using System;
using System.Collections.Generic;
using hds.shared;

namespace hds
{
    /// <summary>
    /// Code storage, the archive Code Bits and code fragments go into when they are uploaded at a hardline.
    /// Retail kept it in the same list as the known abilities (margin character data opcode 0x07): each entry is
    /// <c>&lt;slot u16&gt; &lt;value u32&gt;</c>, where an ability is <c>0x80000000 | abilityId &lt;&lt; 7 | level</c> and stored
    /// code is <c>0x84000000 | goid &lt;&lt; 7 | (count - 1)</c>. Trinity's last-day login (crashover2k proxy_second_pc_011)
    /// mixes both, e.g. <c>24 00 85 53 0e 84</c> = slot 0x24, Code Bit 4 (7335) x6.
    /// <para>Uploading (hardline booth action 04, retail <c>80 c7 &lt;view&gt; &lt;spawn&gt; 04 00</c>) answered with, in order:</para>
    /// <list type="bullet">
    /// <item><c>2e 07 ...</c>, a system message from the client's string table with the character's name;</item>
    /// <item><c>5f &lt;bag slot&gt; 01</c> for each uploaded bag slot;</item>
    /// <item><c>69 00 &lt;slot u16&gt; &lt;value u32&gt; 00 00</c> for a kind already stored (abilitychangecoder 61114:
    /// Code Bit 4 went from <c>8e 53 0e 84</c> to <c>8f 53 0e 84</c> when one more was uploaded at 62075);</item>
    /// <item><c>5e &lt;slot u16&gt; 00 00 00 00 &lt;value u32&gt; 00</c> for a new kind (luizoe_recursion2 1936:
    /// <c>5e 82 00 00 00 00 00 13 c3 04 84 00</c>), the bag-add frame <c>5e &lt;slot&gt; &lt;goid u32&gt; 00 00 &lt;amount&gt; &lt;type&gt; 01</c>
    /// with goid 0, the packed value in place of <c>00 00 &lt;amount&gt; &lt;type&gt;</c> and 00 in place of 01;</item>
    /// <item><c>81 18 ...</c> last (the booth's upload answer).</item>
    /// </list>
    /// The count bits hold count - 1, as the bag's amount byte does: new kinds arrived with 0 (and 2, 0x13), never as an
    /// empty entry. That is the reading the captures favour, not something they prove.
    /// </summary>
    public static class CodeStorage
    {
        public const UInt32 CodeFlag = 0x84000000;
        public const UInt32 AbilityFlag = 0x80000000;

        /// <summary>Seven count bits hold count - 1.</summary>
        public const int MaxCount = 128;

        public struct Entry
        {
            public UInt16 Slot;
            public UInt32 Goid;
            public int Count;

            public Entry(UInt16 slot, UInt32 goid, int count)
            {
                Slot = slot;
                Goid = goid;
                Count = count;
            }
        }

        public static UInt32 Pack(UInt32 goid, int count)
        {
            int clamped = Math.Min(MaxCount, Math.Max(1, count));
            return CodeFlag | (goid << 7) | (UInt32)(clamped - 1);
        }

        /// <summary>True for a known-abilities value that is stored code rather than an ability.</summary>
        public static bool IsCode(UInt32 value)
        {
            return (value & 0xFC000000) == CodeFlag;
        }

        public static UInt32 GoidOf(UInt32 value)
        {
            return (value & 0x03FFFFFF) >> 7;
        }

        public static int CountOf(UInt32 value)
        {
            return (int)(value & 0x7F) + 1;
        }

        /// <summary>The value without its count bits, as kept in <c>char_abilities.ability_id</c> (the margin adds the level).</summary>
        public static Int32 BaseValue(UInt32 goid)
        {
            return unchecked((Int32)(CodeFlag | (goid << 7)));
        }

        /// <summary>Code Bits and code fragments go into storage; everything else stays in the bag.</summary>
        public static bool IsUploadable(UInt32 goid)
        {
            return (goid >= 7332 && goid <= 7339) || (goid >= 2435 && goid <= 2480) || goid == 7458 ||
                   (goid >= 9139 && goid <= 9170);
        }

        /// <summary><c>69 00 &lt;slot u16&gt; &lt;value u32&gt; 00 00</c>: a stored kind's new count.</summary>
        public static byte[] BuildUpdate(UInt16 slot, UInt32 goid, int count)
        {
            PacketContent pak = new PacketContent();
            pak.AddByte(0x69);
            pak.AddByte(0x00);
            pak.AddUint16(slot, 1);
            pak.AddUint32(Pack(goid, count), 1);
            pak.AddUint16(0, 1);
            return pak.ReturnFinalPacket();
        }

        /// <summary><c>5f &lt;slot u16&gt; 00</c>: remove a storage entry (client.dll 0x621e1b20), not the bag slot.</summary>
        public static byte[] BuildRemove(UInt16 slot)
        {
            PacketContent pak = new PacketContent();
            pak.AddByte(0x5f);
            pak.AddUint16(slot, 1);
            pak.AddByte(0x00);
            return pak.ReturnFinalPacket();
        }

        /// <summary><c>5e &lt;slot u16&gt; 00 00 00 00 &lt;value u32&gt; 00</c>: a new kind in storage.</summary>
        public static byte[] BuildAdd(UInt16 slot, UInt32 goid, int count)
        {
            PacketContent pak = new PacketContent();
            pak.AddByte(0x5e);
            pak.AddUint16(slot, 1);
            pak.AddUint32(0, 1);
            pak.AddUint32(Pack(goid, count), 1);
            pak.AddByte(0x00);
            return pak.ReturnFinalPacket();
        }

        /// <summary>
        /// The message retail sent before an upload's inventory changes, byte for byte except the name:
        /// <c>2e 07</c>, 11 zero bytes, <c>64 00 00 2e 00 24 00</c>, 16 zero bytes, then the character's name as a sized
        /// string. 0x64 / 0x2e look like a string-table reference with the name as its argument; the text itself is in the
        /// client. Both captures (crashover2k uploadcodebits 235, abilitychangecoder 61114) were identical.
        /// </summary>
        public static byte[] BuildUploadMessage(string characterName)
        {
            PacketContent pak = new PacketContent();
            pak.AddByte(0x2e);
            pak.AddByte(0x07);
            pak.AddByteArray(new byte[11]);
            pak.AddHexBytes("640000 2e00 2400".Replace(" ", ""));
            pak.AddByteArray(new byte[16]);
            pak.AddSizedTerminatedString(characterName);
            return pak.ReturnFinalPacket();
        }

        /// <summary>One upload step, worked out without touching the database or the client.</summary>
        public class UploadPlan
        {
            /// <summary>Bag slots that empty completely (5f).</summary>
            public List<UInt16> ClearedBagSlots = new List<UInt16>();

            /// <summary>Bag slots that keep part of their stack because storage is full: slot, items left.</summary>
            public List<KeyValuePair<UInt16, int>> ShrunkBagSlots = new List<KeyValuePair<UInt16, int>>();

            /// <summary>Storage entries that already existed and grew.</summary>
            public List<Entry> Updated = new List<Entry>();

            /// <summary>Storage entries made for new kinds.</summary>
            public List<Entry> Added = new List<Entry>();

            public int ItemsMoved;
        }

        /// <summary>
        /// Moves every uploadable bag item into storage: an existing entry of the same kind grows (up to
        /// <see cref="MaxCount"/>), otherwise a new entry takes <paramref name="nextSlot"/> onwards. Worn items
        /// (slot 97 and up) are never touched.
        /// </summary>
        public static UploadPlan PlanUpload(IEnumerable<Crafting.InventoryItem> bag, IEnumerable<Entry> stored, UInt16 nextSlot)
        {
            UploadPlan plan = new UploadPlan();
            Dictionary<UInt32, Entry> byGoid = new Dictionary<UInt32, Entry>();
            HashSet<UInt32> existing = new HashSet<UInt32>();
            foreach (Entry entry in stored)
            {
                if (!byGoid.ContainsKey(entry.Goid))
                {
                    byGoid[entry.Goid] = entry;
                    existing.Add(entry.Goid);
                }
            }

            List<Crafting.InventoryItem> items = new List<Crafting.InventoryItem>(bag);
            items.Sort((a, b) => a.Slot.CompareTo(b.Slot));
            foreach (Crafting.InventoryItem item in items)
            {
                if (!Crafting.IsBagSlot(item.Slot) || !IsUploadable(item.Goid))
                {
                    continue;
                }

                Entry entry;
                if (!byGoid.TryGetValue(item.Goid, out entry))
                {
                    entry = new Entry(nextSlot++, item.Goid, 0);
                }

                int moved = Math.Min(item.Count, MaxCount - entry.Count);
                if (moved <= 0)
                {
                    continue;
                }

                entry.Count += moved;
                byGoid[item.Goid] = entry;
                plan.ItemsMoved += moved;
                if (moved == item.Count)
                {
                    plan.ClearedBagSlots.Add(item.Slot);
                }
                else
                {
                    plan.ShrunkBagSlots.Add(new KeyValuePair<UInt16, int>(item.Slot, item.Count - moved));
                }
            }

            foreach (Entry entry in byGoid.Values)
            {
                if (entry.Count == 0)
                {
                    continue;
                }
                if (existing.Contains(entry.Goid))
                {
                    bool changed = true;
                    foreach (Entry before in stored)
                    {
                        if (before.Goid == entry.Goid && before.Count == entry.Count)
                        {
                            changed = false;
                        }
                    }
                    if (changed)
                    {
                        plan.Updated.Add(entry);
                    }
                }
                else
                {
                    plan.Added.Add(entry);
                }
            }

            plan.Updated.Sort((a, b) => a.Slot.CompareTo(b.Slot));
            plan.Added.Sort((a, b) => a.Slot.CompareTo(b.Slot));
            return plan;
        }
    }
}
