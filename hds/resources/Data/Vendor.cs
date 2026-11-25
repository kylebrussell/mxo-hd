using System;
using System.Collections.Generic;
using System.Text;

namespace hds
{
    public class Vendor
    {
        public UInt32[] items;
        public UInt16 metrId;
        public UInt32 vendorStaticID;
        /// <summary>The 7.6005 merchant static the stock client draws at this vendor's position (0 when none was
        /// resolved). vendor_static_id came from older captures and often names a door, a bench or nothing.</summary>
        public UInt32 npcStaticID;

        public Vendor(UInt16 metrId, UInt32 vendorStaticID)
        {
            this.metrId = metrId;
            this.vendorStaticID = vendorStaticID;
        }

        public Vendor(UInt16 metrId, UInt32 vendorStaticID, UInt32[] items)
        {
            this.metrId = metrId;
            this.vendorStaticID = vendorStaticID;
            this.items = items;
        }

    }
}
