using System;
using System.Collections.Generic;

namespace hds
{
    /// <summary>Static IDs identify an object within a district. Different districts reuse them.</summary>
    internal sealed class StaticObjectIndex
    {
        private readonly Dictionary<ulong, StaticWorldObject> objects =
            new Dictionary<ulong, StaticWorldObject>();
        private readonly Dictionary<UInt32, UInt16> firstDistrict = new Dictionary<UInt32, UInt16>();

        private static ulong PackKey(UInt16 district, UInt32 id) => ((ulong)district << 32) | id;

        public void EnsureCapacity(int capacity)
        {
            if (capacity <= 0)
            {
                return;
            }

            objects.EnsureCapacity(capacity);
            firstDistrict.EnsureCapacity(capacity);
        }

        public void Add(StaticWorldObject obj)
        {
            objects.TryAdd(PackKey(obj.metrId, obj.mxoStaticId), obj);
            firstDistrict.TryAdd(obj.mxoStaticId, obj.metrId);
        }

        public StaticWorldObject Find(UInt32 id, UInt16? district = null)
        {
            if (!district.HasValue && firstDistrict.TryGetValue(id, out UInt16 first))
            {
                district = first;
            }
            return district.HasValue && objects.TryGetValue(PackKey(district.Value, id), out StaticWorldObject obj) ? obj : null;
        }
    }
}
