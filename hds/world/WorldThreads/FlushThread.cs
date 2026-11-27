using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace hds
{
    public partial class WorldThreads
    {
        private const int ParallelFlushThreshold = 16;

        public void FlushThreadProcess()
        {
            Output.WriteLine("[WORLD SERVER]Flush Thread started");
            List<WorldClient> clients = new List<WorldClient>();
            ParallelOptions parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Min(4, Math.Max(2, Environment.ProcessorCount / 2))
            };

            while (true)
            {
                long tickStart = Environment.TickCount64;
                SnapshotClientsForFlush(clients);
                FlushClientBatch(clients, parallelOptions);

                long elapsed = Environment.TickCount64 - tickStart;
                int sleepMs = WorldClient.FlushIntervalMs - (int)elapsed;
                Thread.Sleep(sleepMs > 0 ? sleepMs : 1);
            }
        }

        internal static void FlushClientBatch(List<WorldClient> clients, ParallelOptions parallelOptions = null)
        {
            int count = clients.Count;
            if (count == 0)
            {
                return;
            }

            if (count < ParallelFlushThreshold || Environment.ProcessorCount <= 1)
            {
                for (int i = 0; i < count; i++)
                {
                    FlushSingleClient(clients[i]);
                }
                return;
            }

            ParallelOptions opts = parallelOptions ?? new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Min(4, Math.Max(2, Environment.ProcessorCount / 2))
            };
            Parallel.For(0, count, opts, i => FlushSingleClient(clients[i]));
        }

        private static void FlushSingleClient(WorldClient client)
        {
            if (client == null)
            {
                return;
            }

            try
            {
                client.TickFlush();
            }
            catch (Exception ex)
            {
                Output.WriteDebugLog("[FLUSH] TickFlush failed for char " + client.playerData?.getCharID() + ": " + ex.Message);
            }
        }

        private static void SnapshotClientsForFlush(List<WorldClient> destination)
        {
            destination.Clear();
            lock (WorldServer.Clients)
            {
                if (destination.Capacity < WorldServer.Clients.Count)
                {
                    destination.Capacity = WorldServer.Clients.Count;
                }
                foreach (WorldClient client in WorldServer.Clients.Values)
                {
                    destination.Add(client);
                }
            }
        }
    }
}
