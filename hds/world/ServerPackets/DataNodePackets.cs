using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using hds.shared;

namespace hds
{
    public partial class ServerPackets
    {

        // Legacy entry point now uses the dev-gated, persisted node fixture (606), not a fake container packet.
        public void spawnDataNodeView(WorldClient client) => DataMining.Dev(client, new[] { "?utilityobject", "606" });
    }
}
