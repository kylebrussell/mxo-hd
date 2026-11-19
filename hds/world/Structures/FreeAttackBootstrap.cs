using System;
using hds.shared;

namespace hds
{
    /// <summary>Disposable native experiments for a gun already present in the saved inventory.</summary>
    public static class FreeAttackBootstrap
    {
        public const string Mode = "MXO_FREE_ATTACK_BOOTSTRAP_QA";
        public const string Login = "login";
        public const string Equip = "equip";

        private static string ActiveMode() =>
            (Store.config?.dbParams?.DatabaseName?.StartsWith("mxotest_", StringComparison.Ordinal) ?? false)
                ? Environment.GetEnvironmentVariable(Mode) : null;

        internal static void OnChoice(WorldClient client, ushort slot, uint kind)
        {
            string mode = ActiveMode();
            if (mode != Login && mode != Equip) return;
            var data = client.playerData;
            bool empty = slot == 0x6b && kind == 0;
            data.freeAttackChoiceEmpty = empty;
            data.freeAttackBootstrapPending ??= empty;
            if (!empty) data.freeAttackBootstrapPending = false;
            if (mode == Login) TryRefresh(client);
        }

        internal static void LoadingComplete(WorldClient client, byte[] request)
        {
            string mode = ActiveMode();
            if ((mode != Login && mode != Equip) || request == null || request.Length != 1 || request[0] != 1) return;
            client.playerData.freeAttackBootstrapWorldReady = true;
            if (mode == Login) TryRefresh(client);
        }

        private static bool Ready(ClientData data) => data.freeAttackBootstrapWorldReady && data.getOnWorld() &&
            !data.worldChangePending && !data.DistrictIsLA() && !data.isDead;

        private static void TryRefresh(WorldClient client)
        {
            var data = client.playerData;
            if (data.freeAttackBootstrapPending != true || !Ready(data)) return;
            data.freeAttackBootstrapPending = false;

            var rows = Store.dbManager.WorldDbHandler.GetInventoryItems(data.getCharID());
            bool UniqueGun(Crafting.InventoryItem row) => row.Count == 1 && RangedWeapons.GetStats(row.Goid) != null &&
                rows.FindLastIndex(other => other.Slot == row.Slot) == rows.FindIndex(other => other.Slot == row.Slot);
            var held = rows.Find(row => row.Slot == InventoryHandler.WeaponSlot && UniqueGun(row));
            lock (client.messageQueue.RPCMessagesQueue.SyncRoot)
            {
                if (held.Goid != 0)
                {
                    // Let the client discover the held gun through the observed bag-add and bag-to-hand
                    // handlers. The staging slot exists only within this ordered UI refresh.
                    for (ushort slot = 1; slot <= 96; slot++)
                    {
                        if (rows.Exists(row => row.Slot == slot)) continue;
                        QueueRefresh(client, held.Slot, slot, held.Goid);
                        new ServerPackets().sendInventoryItemMove(slot, held.Slot, client);
                        return;
                    }
                }

                var bag = rows.Find(row => row.Slot >= 1 && row.Slot <= 96 && UniqueGun(row));
                if (bag.Goid != 0) QueueRefresh(client, bag.Slot, bag.Slot, bag.Goid);
            }
        }

        // Called immediately before publishing the ordinary persisted bag-to-hand move.
        internal static void BeforeEquip(WorldClient client, byte bagSlot, uint goid)
        {
            var data = client.playerData;
            if (ActiveMode() == Equip && data.freeAttackChoiceEmpty && Ready(data) && RangedWeapons.GetStats(goid) != null)
                QueueRefresh(client, bagSlot, bagSlot, goid);
        }

        private static void QueueRefresh(WorldClient client, ushort oldSlot, ushort addSlot, uint goid)
        {
            client.messageQueue.addRpcMessage(ServerPackets.BuildInventoryItemDelete(oldSlot));
            client.messageQueue.addRpcMessage(ServerPackets.BuildInventoryItemAdd(addSlot, goid, 0, 0x10));
            Output.WriteDebugLog($"[FREE ATTACK QA] Refreshed owned gun {goid} from slot {oldSlot} into slot {addSlot} for character {client.playerData.getCharID()}");
        }
    }
}
