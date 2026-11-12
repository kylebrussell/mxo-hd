using System;
using System.Collections.Generic;
using hds.databases.interfaces;
using hds.shared;

namespace hds
{
    /// <summary>
    /// Uploading Code Bits and code fragments into code storage at a hardline, and taking compile ingredients
    /// from storage. See <see cref="CodeStorage"/> for the retail records.
    /// </summary>
    public static class CodeStorageHandler
    {
        /// <summary>
        /// Moves the bag's Code Bits and code fragments into storage and queues the retail records (2e message, 5f per
        /// emptied bag slot, 69 00 per grown entry, 5e per new entry). The caller sends the booth's 81 18 answer after
        /// this and flushes. Returns how many items moved.
        /// </summary>
        public static int UploadCode(WorldClient client, out bool abilityCodeUploaded)
        {
            abilityCodeUploaded = false;
            UInt32 charId = client.playerData.getCharID();
            IWorldDBHandler db = Store.dbManager.WorldDbHandler;

            List<Crafting.InventoryItem> bag = db.GetInventoryItems(charId);
            List<CodeStorage.Entry> stored = db.GetCodeStorage(charId);
            UInt16 nextSlot = db.GetNextAbilitySlot(charId);
            CodeStorage.UploadPlan plan = CodeStorage.PlanUpload(bag, stored, nextSlot);

            List<Crafting.InventoryItem> abilityCode = new List<Crafting.InventoryItem>();
            HashSet<UInt32> uploading = new HashSet<UInt32>();
            foreach (Crafting.InventoryItem item in bag)
            {
                if (Crafting.IsBagSlot(item.Slot) && AbilityProgression.IsAbilityCode(item.Goid) &&
                    !db.HasAbility(charId, unchecked((Int32)item.Goid)) && uploading.Add(item.Goid))
                {
                    abilityCode.Add(item);
                }
            }

            if (plan.ItemsMoved == 0 && abilityCode.Count == 0)
            {
                Output.WriteDebugLog("[CODE STORAGE] Nothing to upload");
                return 0;
            }

            string name = StringUtils.charBytesToString_NZ(client.playerInstance.CharacterName.getValue());
            client.messageQueue.addRpcMessage(CodeStorage.BuildUploadMessage(name));

            abilityCodeUploaded = abilityCode.Count > 0;

            // Bought abilities join the known-abilities list after the storage entries this upload adds.
            UInt16 abilitySlot = (UInt16)(nextSlot + plan.Added.Count);
            foreach (Crafting.InventoryItem item in abilityCode)
            {
                db.RemoveItemAtInventorySlot(item.Slot, charId);
                db.AddAbility(charId, abilitySlot, unchecked((Int32)item.Goid), 1);
                client.messageQueue.addRpcMessage(ServerPackets.BuildInventoryItemDelete(item.Slot));
                client.messageQueue.addRpcMessage(ServerPackets.BuildKnownAbilityAdd(abilitySlot, AbilityProgression.AbilityCodeValue(item.Goid)));
                Output.WriteDebugLog("[CODE STORAGE] Uploaded ability code " + item.Goid + " to slot " + abilitySlot);
                abilitySlot++;
            }

            foreach (UInt16 slot in plan.ClearedBagSlots)
            {
                db.RemoveItemAtInventorySlot(slot, charId);
                client.messageQueue.addRpcMessage(ServerPackets.BuildInventoryItemDelete(slot));
            }

            foreach (KeyValuePair<UInt16, int> shrunk in plan.ShrunkBagSlots)
            {
                db.SetInventoryCount(shrunk.Key, charId, shrunk.Value);
                client.messageQueue.addRpcMessage(ItemStacks.BuildStackCount(shrunk.Key, shrunk.Value));
            }

            foreach (CodeStorage.Entry entry in plan.Updated)
            {
                db.SetCodeStorageCount(charId, entry.Slot, entry.Count);
                client.messageQueue.addRpcMessage(CodeStorage.BuildUpdate(entry.Slot, entry.Goid, entry.Count));
            }

            foreach (CodeStorage.Entry entry in plan.Added)
            {
                db.AddCodeStorage(charId, entry);
                client.messageQueue.addRpcMessage(CodeStorage.BuildAdd(entry.Slot, entry.Goid, entry.Count));
            }

            Output.WriteDebugLog("[CODE STORAGE] Uploaded " + plan.ItemsMoved + " items: " + plan.ClearedBagSlots.Count +
                                 " bag slots emptied, " + plan.Updated.Count + " entries grew, " + plan.Added.Count + " new");
            return plan.ItemsMoved + abilityCode.Count;
        }

        /// <summary>
        /// Hardline booth action 04 on a server without the travel booth handling: upload, then the 81 18 answer
        /// retail sent after an upload with code (crashover2k uploadcodebits 235, abilitychangecoder 61114).
        /// </summary>
        public static void ProcessUploadRequest(WorldClient client)
        {
            UploadCode(client, out _);
            client.messageQueue.addRpcMessage(new byte[] { 0x81, 0x18, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 });
            client.FlushQueue();
        }

        /// <summary>Takes one item from storage: 69 00 with the new count, or 5f with PersonalInventoryItem 0 for the last one.</summary>
        public static void TakeOne(WorldClient client, UInt16 storageSlot)
        {
            UInt32 charId = client.playerData.getCharID();
            IWorldDBHandler db = Store.dbManager.WorldDbHandler;
            foreach (CodeStorage.Entry entry in db.GetCodeStorage(charId))
            {
                if (entry.Slot != storageSlot)
                {
                    continue;
                }

                if (entry.Count > 1)
                {
                    db.SetCodeStorageCount(charId, storageSlot, entry.Count - 1);
                    client.messageQueue.addRpcMessage(CodeStorage.BuildUpdate(storageSlot, entry.Goid, entry.Count - 1));
                }
                else
                {
                    // client.dll 0x621e1b20 clears the known-abilities entry and the storage window.
                    db.RemoveCodeStorage(charId, storageSlot);
                    client.messageQueue.addRpcMessage(CodeStorage.BuildRemove(storageSlot));
                    Output.WriteDebugLog("[CODE STORAGE] Last " + entry.Goid + " used from storage slot " + storageSlot);
                }
                return;
            }
        }

        /// <summary>Storage entries as InventoryItems for <see cref="Crafting.MatchIngredients"/> (slot | StorageSlotFlag).</summary>
        public static List<Crafting.InventoryItem> AsIngredients(IEnumerable<CodeStorage.Entry> stored)
        {
            List<Crafting.InventoryItem> items = new List<Crafting.InventoryItem>();
            foreach (CodeStorage.Entry entry in stored)
            {
                items.Add(new Crafting.InventoryItem((UInt16)(Crafting.StorageSlotFlag | entry.Slot), entry.Goid, entry.Count));
            }
            return items;
        }
    }
}
