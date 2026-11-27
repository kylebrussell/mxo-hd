using System;
using hds.shared;

namespace hds
{
    /// <summary>Refreshes an owned weapon for the stock Free Attack item's discovery path.</summary>
    public static class FreeAttackBootstrap
    {
        public const string Mode = "MXO_FREE_ATTACK_BOOTSTRAP_QA";
        public const string Login = "login";
        private static bool TestLoginRefresh =>
            Environment.GetEnvironmentVariable(Mode) == Login &&
            (Store.config?.dbParams?.DatabaseName?.StartsWith("mxotest_", StringComparison.Ordinal) ?? false);

        internal static void OnChoice(WorldClient client, ushort slot, uint kind)
        {
            var data = client.playerData;
            bool empty = slot == 0x6b && kind == 0;
            data.freeAttackChoiceEmpty = empty;
            if (kind == CombatHandler.FreeFireKindItem)
                data.freeAttackItemReported = true;
            if (!TestLoginRefresh) return;
            if (kind == CombatHandler.FreeFireKindItem && data.freeAttackBootstrapStagingSlot != 0)
                RestoreHand(client, data.freeAttackBootstrapStagingSlot, data.freeAttackBootstrapStagingWorld);
            data.freeAttackBootstrapPending ??= empty;
            if (!empty) data.freeAttackBootstrapPending = false;
            ScheduleLoginRefresh(client);
        }

        internal static void LoadingComplete(WorldClient client, byte[] request)
        {
            if (request == null || request.Length != 1 || request[0] != 1) return;
            client.playerData.freeAttackBootstrapWorldReady = true;
            if (TestLoginRefresh) ScheduleLoginRefresh(client);
        }

        internal static void StyleSelection(WorldClient client, byte[] request)
        {
            if (!TestLoginRefresh || !InWorld(client) || !CombatSelection.Parse(request).HasValue) return;
            client.playerData.freeAttackBootstrapStyleReady = true;
            ScheduleLoginRefresh(client);
        }

        private static bool InWorld(WorldClient client)
        {
            var data = client.playerData;
            return client.Alive && data.getOnWorld() && !data.worldChangePending && !data.DistrictIsLA() && !data.isDead;
        }

        private static void ScheduleLoginRefresh(WorldClient client)
        {
            var data = client.playerData;
            if (data.freeAttackBootstrapPending != true || !data.freeAttackBootstrapWorldReady ||
                !data.freeAttackBootstrapStyleReady || !InWorld(client)) return;
            data.freeAttackBootstrapPending = false;
            int world = data.worldGeneration;
            // Stock can send 3e and 0c in one setup batch. Give that batch time to finish
            // before exercising inventory discovery in the disposable login profile.
            DelayedActions.AfterFor(client, 0.25, () =>
            {
                if (data.worldGeneration == world && data.freeAttackChoiceEmpty && InWorld(client))
                    RefreshLoginInventory(client, world);
            });
        }

        private static void RefreshLoginInventory(WorldClient client, int world)
        {
            var data = client.playerData;
            var rows = Store.dbManager.WorldDbHandler.GetInventoryItems(data.getCharID());
            bool UniqueGun(Crafting.InventoryItem row) => row.Count == 1 && RangedWeapons.GetStats(row.Goid) != null &&
                rows.FindLastIndex(other => other.Slot == row.Slot) == rows.FindIndex(other => other.Slot == row.Slot);
            var held = rows.Find(row => row.Slot == InventoryHandler.WeaponSlot && UniqueGun(row));
            ushort staging = 0;
            if (held.Goid != 0)
            {
                for (ushort slot = 1; slot <= 96; slot++)
                {
                    if (rows.Exists(row => row.Slot == slot)) continue;
                    staging = slot;
                    break;
                }
            }
            if (staging != 0)
            {
                // Allow the client to report its selection while the discovery slot is still visible.
                data.freeAttackBootstrapStagingSlot = staging;
                data.freeAttackBootstrapStagingWorld = world;
                lock (client.messageQueue.RPCMessagesQueue.SyncRoot)
                    QueueRefresh(client, held.Slot, staging, held.Goid);
                client.FlushQueue();
                DelayedActions.AfterFor(client, 2, () =>
                {
                    RestoreHand(client, staging, world);
                    if (client.Alive) client.FlushQueue();
                });
                return;
            }
            var bag = rows.Find(row => row.Slot >= 1 && row.Slot <= 96 && UniqueGun(row));
            if (bag.Goid == 0) return;
            lock (client.messageQueue.RPCMessagesQueue.SyncRoot)
                QueueRefresh(client, bag.Slot, bag.Slot, bag.Goid);
            client.FlushQueue();
        }

        private static void RestoreHand(WorldClient client, ushort staging, int world)
        {
            var data = client.playerData;
            if (data.freeAttackBootstrapStagingWorld != world || data.freeAttackBootstrapStagingSlot != staging) return;
            data.freeAttackBootstrapStagingSlot = 0;
            if (data.worldGeneration != world || !InWorld(client)) return;
            new ServerPackets().sendInventoryItemMove(staging, InventoryHandler.WeaponSlot, client);
        }

        internal static bool PrepareEquip(WorldClient client, uint goid)
        {
            var data = client.playerData;
            if (data.freeAttackBootstrapStagingSlot != 0)
                RestoreHand(client, data.freeAttackBootstrapStagingSlot, data.freeAttackBootstrapStagingWorld);
            // The committed equip already validated the source row and count.
            return data.freeAttackChoiceEmpty && !data.freeAttackItemReported && data.freeAttackBootstrapWorldReady &&
                InWorld(client) && RangedWeapons.GetStats(goid) != null;
        }

        // Queue with the ordinary moves after the persisted equip has succeeded.
        internal static void RefreshEquip(WorldClient client, byte bagSlot, uint goid) =>
            QueueRefresh(client, bagSlot, bagSlot, goid);

        private static void QueueRefresh(WorldClient client, ushort oldSlot, ushort addSlot, uint goid)
        {
            client.messageQueue.addRpcMessage(ServerPackets.BuildInventoryItemDelete(oldSlot));
            client.messageQueue.addRpcMessage(ServerPackets.BuildInventoryItemAdd(addSlot, goid, 0, 0x10));
            Output.WriteDebugLog($"[FREE ATTACK] Refreshed owned gun {goid} from slot {oldSlot} into slot {addSlot} for character {client.playerData.getCharID()}");
        }
    }
}
