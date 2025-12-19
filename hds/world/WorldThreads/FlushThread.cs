using System.Collections.Generic;
using System.Threading;

namespace hds
{
    public partial class WorldThreads
    {
        public void FlushThreadProcess()
        {
            Output.WriteLine("[WORLD SERVER]Flush Thread started");
            while (true)
            {
                Thread.Sleep(WorldClient.FlushIntervalMs);
                List<WorldClient> clients = SnapshotClientsForFlush();
                foreach (WorldClient client in clients)
                {
                    if (client != null)
                    {
                        client.TickFlush();
                    }
                }
            }
        }

        private static List<WorldClient> SnapshotClientsForFlush()
        {
            lock (WorldServer.Clients)
            {
                return new List<WorldClient>(WorldServer.Clients.Values);
            }
        }
    }
}
