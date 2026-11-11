using System;
using System.Collections.Generic;

namespace hds
{
    /// <summary>Static IDs identify an object within a district. Different districts reuse them.</summary>
    internal sealed class StaticObjectIndex
    {
        private readonly Dictionary<(UInt16 District, UInt32 Id), StaticWorldObject> objects =
            new Dictionary<(UInt16, UInt32), StaticWorldObject>();
        private readonly Dictionary<UInt32, UInt16> firstDistrict = new Dictionary<UInt32, UInt16>();

        public void Add(StaticWorldObject obj)
        {
            objects.TryAdd((obj.metrId, obj.mxoStaticId), obj);
            firstDistrict.TryAdd(obj.mxoStaticId, obj.metrId);
        }

        public StaticWorldObject Find(UInt32 id, UInt16? district = null)
        {
            if (!district.HasValue && firstDistrict.TryGetValue(id, out UInt16 first))
            {
                district = first;
            }
            return district.HasValue && objects.TryGetValue((district.Value, id), out StaticWorldObject obj) ? obj : null;
        }
    }
}
