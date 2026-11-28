using System;
using System.Collections.Generic;

namespace hds
{
    /// <summary>
    /// Compact zero-GC-reference open-addressing index for static world objects (~2.9M rows).
    /// Static IDs identify an object within a district; different districts reuse them.
    /// Stores only value-type <c>ulong[] keys</c> and <c>int[] indices</c> (pointing into a sequentially
    /// appended backing list such as <c>DataLoader.WorldObjectsDB</c>), eliminating scattered LOH reference
    /// write-barrier scans, the secondary ~71 MB <c>firstDistrict</c> dictionary, and ~2.9M startup hash
    /// inserts while preserving exact first-insertion semantics for <c>Find(id, null)</c>.
    /// </summary>
    internal sealed class StaticObjectIndex
    {
        private const int InitialCapacity = 16;

        private readonly List<StaticWorldObject> backing;
        private readonly bool ownsBacking;
        private ulong[] keys = Array.Empty<ulong>();
        private int[] indices = Array.Empty<int>();
        private int count;
        private ushort[] seenDistricts = Array.Empty<ushort>();
        private int seenDistrictCount;
        private int lastDistrictIndex = -1;

        // Populated only if inserts return to an older district and collide with an ID already
        // inserted in a later district (empty/null during normal contiguous per-district CSV load).
        private Dictionary<UInt32, UInt16> outOfOrderFirstDistrict;

        public StaticObjectIndex()
        {
            backing = new List<StaticWorldObject>();
            ownsBacking = true;
        }

        internal StaticObjectIndex(List<StaticWorldObject> sharedBacking)
        {
            backing = sharedBacking ?? throw new ArgumentNullException(nameof(sharedBacking));
            ownsBacking = false;
        }

        internal int Count => count;
        internal int TableCapacity => keys.Length;
        internal int OutOfOrderOverrideCount => outOfOrderFirstDistrict != null ? outOfOrderFirstDistrict.Count : 0;

        // Always non-zero because (district + 1) is in [1, 65536]; 0 denotes an empty slot.
        private static ulong PackKey(UInt16 district, UInt32 id) => (((ulong)district + 1UL) << 32) | id;

        private static int HashSlot(ulong packedKey, int mask)
        {
            ulong x = packedKey;
            x ^= x >> 30;
            x *= 0xbf58476d1ce4e5b9UL;
            x ^= x >> 27;
            x *= 0x94d049bb133111ebUL;
            x ^= x >> 31;
            return (int)x & mask;
        }

        private static int ComputeTableSize(int minEntries)
        {
            long target = ((long)minEntries * 4L) / 3L + 1L;
            long size = InitialCapacity;
            while (size < target && size < (1L << 30))
            {
                size <<= 1;
            }
            return (int)size;
        }

        public void EnsureCapacity(int capacity)
        {
            if (capacity <= 0)
            {
                return;
            }

            if (ownsBacking)
            {
                backing.EnsureCapacity(capacity);
            }

            int desired = ComputeTableSize(capacity);
            if (desired > keys.Length)
            {
                Resize(desired);
            }
        }

        public void Add(StaticWorldObject obj)
        {
            if (obj == null)
            {
                return;
            }

            if (!ownsBacking)
            {
                // Fallback if a caller invokes Add(obj) directly on a shared-backing index.
                int idx = backing.Count;
                backing.Add(obj);
                AddAt(obj, idx);
                return;
            }

            AddCore(obj, backingIndex: -1);
        }

        /// <summary>Indexes <paramref name="obj"/> already stored at <paramref name="backingIndex"/> in the shared backing list.</summary>
        internal void AddAt(StaticWorldObject obj, int backingIndex)
        {
            if (obj == null)
            {
                return;
            }

            AddCore(obj, backingIndex);
        }

        private void AddCore(StaticWorldObject obj, int backingIndex)
        {
            if (keys.Length == 0)
            {
                keys = new ulong[InitialCapacity];
                indices = new int[InitialCapacity];
            }
            else if ((long)(count + 1) * 4L > (long)keys.Length * 3L)
            {
                Resize(keys.Length << 1);
            }

            UInt16 district = obj.metrId;
            UInt32 id = obj.mxoStaticId;
            ulong packed = PackKey(district, id);
            ulong[] k = keys;
            int mask = k.Length - 1;
            int slot = HashSlot(packed, mask);
            while (true)
            {
                ulong existing = k[slot];
                if (existing == 0)
                {
                    int idx = backingIndex;
                    if (idx < 0)
                    {
                        idx = backing.Count;
                        backing.Add(obj);
                    }
                    k[slot] = packed;
                    indices[slot] = idx;
                    count++;
                    TrackDistrictOrderOnInsert(district, id);
                    return;
                }
                if (existing == packed)
                {
                    return;
                }
                slot = (slot + 1) & mask;
            }
        }

        private void TrackDistrictOrderOnInsert(UInt16 district, UInt32 id)
        {
            int districtIdx;
            if (lastDistrictIndex >= 0 && seenDistricts[lastDistrictIndex] == district)
            {
                districtIdx = lastDistrictIndex;
            }
            else
            {
                districtIdx = -1;
                for (int i = 0; i < seenDistrictCount; i++)
                {
                    if (seenDistricts[i] == district)
                    {
                        districtIdx = i;
                        break;
                    }
                }

                if (districtIdx < 0)
                {
                    if (seenDistrictCount == seenDistricts.Length)
                    {
                        int nextLen = seenDistricts.Length == 0 ? 16 : seenDistricts.Length << 1;
                        Array.Resize(ref seenDistricts, nextLen);
                    }
                    districtIdx = seenDistrictCount++;
                    seenDistricts[districtIdx] = district;
                    lastDistrictIndex = districtIdx;
                    return;
                }

                lastDistrictIndex = districtIdx;
            }

            // Returned to an earlier-seen district after loading a later district: preserve exact
            // first-insertion order for Find(id, null) if this ID already exists in any later district
            // (unless it already exists in an even earlier district, which is already scanned first).
            if (districtIdx < seenDistrictCount - 1)
            {
                if (outOfOrderFirstDistrict != null && outOfOrderFirstDistrict.ContainsKey(id))
                {
                    return;
                }

                for (int j = 0; j < districtIdx; j++)
                {
                    if (FindInDistrict(id, seenDistricts[j]) != null)
                    {
                        return;
                    }
                }

                for (int j = districtIdx + 1; j < seenDistrictCount; j++)
                {
                    UInt16 laterDistrict = seenDistricts[j];
                    if (FindInDistrict(id, laterDistrict) != null)
                    {
                        outOfOrderFirstDistrict ??= new Dictionary<UInt32, UInt16>();
                        outOfOrderFirstDistrict[id] = laterDistrict;
                        return;
                    }
                }
            }
        }

        public StaticWorldObject Find(UInt32 id, UInt16? district = null)
        {
            if (keys.Length == 0 || count == 0)
            {
                return null;
            }

            if (district.HasValue)
            {
                return FindInDistrict(id, district.Value);
            }

            if (outOfOrderFirstDistrict != null && outOfOrderFirstDistrict.TryGetValue(id, out UInt16 overrideDistrict))
            {
                return FindInDistrict(id, overrideDistrict);
            }

            for (int i = 0; i < seenDistrictCount; i++)
            {
                StaticWorldObject found = FindInDistrict(id, seenDistricts[i]);
                if (found != null)
                {
                    return found;
                }
            }
            return null;
        }

        private StaticWorldObject FindInDistrict(UInt32 id, UInt16 district)
        {
            ulong packed = PackKey(district, id);
            ulong[] k = keys;
            int mask = k.Length - 1;
            int slot = HashSlot(packed, mask);
            while (true)
            {
                ulong existing = k[slot];
                if (existing == 0)
                {
                    return null;
                }
                if (existing == packed)
                {
                    return backing[indices[slot]];
                }
                slot = (slot + 1) & mask;
            }
        }

        private void Resize(int newSize)
        {
            ulong[] oldKeys = keys;
            int[] oldIndices = indices;
            ulong[] nextKeys = new ulong[newSize];
            int[] nextIndices = new int[newSize];
            int mask = newSize - 1;

            for (int i = 0; i < oldKeys.Length; i++)
            {
                ulong packed = oldKeys[i];
                if (packed == 0)
                {
                    continue;
                }
                int slot = HashSlot(packed, mask);
                while (nextKeys[slot] != 0)
                {
                    slot = (slot + 1) & mask;
                }
                nextKeys[slot] = packed;
                nextIndices[slot] = oldIndices[i];
            }

            keys = nextKeys;
            indices = nextIndices;
        }
    }
}
