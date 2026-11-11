using System;
using System.Collections.Generic;

namespace hds
{
    public static class CombatManager
    {
        private static readonly Dictionary<WorldClient, CombatHandler> Handlers =
            new Dictionary<WorldClient, CombatHandler>();

        public static CombatHandler GetHandler(WorldClient client)
        {
            if (client == null)
            {
                throw new ArgumentNullException(nameof(client));
            }

            lock (Handlers)
            {
                if (!Handlers.TryGetValue(client, out CombatHandler handler))
                {
                    handler = new CombatHandler(client);
                    Handlers[client] = handler;
                }

                return handler;
            }
        }

        public static void RemoveClient(WorldClient client)
        {
            if (client == null)
            {
                return;
            }

            // Before the lock: ending a fight between players takes that fight's lock, and a fight starting takes this one
            PvpCombat.OnPlayerGone(client);

            lock (Handlers)
            {
                if (Handlers.TryGetValue(client, out CombatHandler handler))
                {
                    handler.StopCombatIfActive(CombatSession.CombatEndReason.AttackerFled);
                    Handlers.Remove(client);
                }
            }
        }
    }
}
