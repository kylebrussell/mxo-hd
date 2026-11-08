using System;
using System.Collections.Generic;
using System.Collections;
using System.Text;

using hds.shared;

namespace hds
{
    class InventoryHandler
    {
        public void processItemMove(ref byte[] packet)
        {
            byte[] sourceSlot = {packet[0], packet[1]};
            byte[] destSlot   = {packet[2], packet[3]};

            UInt16 fromSlot =  NumericalUtils.ByteArrayToUint16(sourceSlot,1);
            UInt16 toSlot   = NumericalUtils.ByteArrayToUint16(destSlot,1);
            Store.dbManager.WorldDbHandler.UpdateInventorySlot(fromSlot, toSlot, Store.currentClient.playerData.getCharID());

            ServerPackets pak = new ServerPackets();
            pak.sendInventoryItemMove(fromSlot, toSlot, Store.currentClient);
        }


        public void processItemAdd(UInt32 itemID, UInt16 type)
        {
            // Called from looting and vendor buy (vendor buy takes care of the money).
            AddItem(Store.currentClient, itemID, type);
        }

        /// <summary>
        /// Puts one item into the inventory. A stackable item (Code Bits, code fragments, pills) joins a stack of the
        /// same item that has room (inventory.count + 1, <c>69 01 &lt;slot&gt; &lt;(count-1)*4&gt;</c>); anything else, or
        /// a full stack, takes the first free bag slot (<c>5e</c>). Returns the slot.
        /// </summary>
        public static UInt16 AddItem(WorldClient client, UInt32 itemID, UInt16 type)
        {
            UInt32 charId = client.playerData.getCharID();
            ServerPackets pak = new ServerPackets();

            if (ItemStacks.IsStackable(itemID))
            {
                foreach (Crafting.InventoryItem item in Store.dbManager.WorldDbHandler.GetInventoryItems(charId))
                {
                    if (item.Slot < 97 && item.Goid == itemID && item.Count < ItemStacks.MaxStack)
                    {
                        int count = item.Count + 1;
                        Store.dbManager.WorldDbHandler.SetInventoryCount(item.Slot, charId, count);
                        client.messageQueue.addRpcMessage(ItemStacks.BuildStackCount(item.Slot, count));
                        Output.WriteDebugLog("[INVENTORY] Item " + itemID + " stacked in slot " + item.Slot + " (" + count + ")");
                        return item.Slot;
                    }
                }
            }

            UInt16 freeSlot = Store.dbManager.WorldDbHandler.GetFirstNewSlot();
            if (freeSlot == 0)
            {
                pak.sendSystemChatMessage(client, "Your inventory is full.", "BROADCAST");
                Output.WriteDebugLog("[INVENTORY] No free bag slot for item " + itemID);
                return 0;
            }
            Store.dbManager.WorldDbHandler.AddItemToInventory(freeSlot, itemID);
            pak.sendInventoryItemAdd(freeSlot, itemID, 0, type, client);
            return freeSlot;
        }

        /// <summary>
        /// Takes one item out of a slot: a stack shrinks by one (<c>69 01</c>, as retail did for a used cookie), the
        /// last item leaves the slot (<c>5f &lt;slot&gt; 01</c>).
        /// </summary>
        public static void RemoveOne(WorldClient client, UInt16 slot)
        {
            UInt32 charId = client.playerData.getCharID();
            int count = 1;
            foreach (Crafting.InventoryItem item in Store.dbManager.WorldDbHandler.GetInventoryItems(charId))
            {
                if (item.Slot == slot)
                {
                    count = item.Count;
                    break;
                }
            }

            if (count > 1)
            {
                Store.dbManager.WorldDbHandler.SetInventoryCount(slot, charId, count - 1);
                client.messageQueue.addRpcMessage(ItemStacks.BuildStackCount(slot, count - 1));
            }
            else
            {
                Store.dbManager.WorldDbHandler.RemoveItemAtInventorySlot(slot, charId);
                new ServerPackets().sendInventoryItemDelete(slot, client);
            }
        }

        public void processItemDelete(ref byte[] packet)
        {
            if (packet.Length < 2)
            {
                Output.WriteDebugLog("[INVENTORY] Item delete/recycle packet too short.");
                return;
            }

            // CLIENT_ITEM_RECYCLE (5d) body: slot u16 little-endian, then 01 01. Confirmed with the
            // real client: recycling the item in slot 10 sent "0a 00 01 01"; retail "5d 04 00 01 01".
            byte[] slotBytes = {packet[0], packet[1]};
            UInt16 slot = NumericalUtils.ByteArrayToUint16(slotBytes, 1);

            UInt32 charId = Store.currentClient.playerData.getCharID();
            UInt32 itemGoId = Store.dbManager.WorldDbHandler.GetItemGOIDAtInventorySlot(slot);
            ServerPackets pak = new ServerPackets();
            if (itemGoId == 0)
            {
                pak.sendSystemChatMessage(Store.currentClient, "There is nothing to recycle in that slot.", "MODAL");
                return;
            }

            // Retail order: 80 e7 (+value, reason 0x0f) -> 80 e4 (new cash, 0x0f) -> 5f (remove slot).
            long currentCash = Store.currentClient.playerData.getInfo();
            UInt32 value = VendorPricing.GetRecyclePrice(itemGoId);
            UInt32 newCash = (UInt32)Math.Min(Math.Max(currentCash, 0) + (long)value, UInt32.MaxValue);

            Store.dbManager.WorldDbHandler.RemoveItemAtInventorySlot(slot, charId);
            Store.dbManager.WorldDbHandler.SaveInfo(Store.currentClient, newCash);
            Store.currentClient.playerData.setInfo(newCash);

            pak.SendInfoDelta(Store.currentClient, (long)newCash - currentCash, ServerPackets.InfoReasonRecycle);
            pak.SendInfoCurrent(Store.currentClient, newCash, ServerPackets.InfoReasonRecycle);
            pak.sendInventoryItemDelete(slot, Store.currentClient);
        }


        /// <summary>
        /// Uses the consumable in a bag slot, asked for with the item's ability in a 80 b9 request. Retail order
        /// (Antibiotic 5.0, PacketsZombiesPortingEtc lines 77036-77221): cast bar <c>80 ac</c> right away; when
        /// the cast ends, the item's and the group's timers (<c>80 bd 01 .. 00</c>) and the slot removal
        /// <c>5f &lt;slot&gt; 01</c>. Heal pills heal by the ability's value at that moment.
        /// Returns false when the slot holds no consumable for that ability (a normal ability use then).
        /// </summary>
        public bool UseItem(UInt16 slot, UInt16 abilityId)
        {
            WorldClient client = Store.currentClient;
            ClientData data = client.playerData;
            ServerPackets pak = new ServerPackets();
            UInt32 goid = Store.dbManager.WorldDbHandler.GetItemGOIDAtInventorySlot(slot);

            if (!ConsumableItems.TryGet(goid, out ConsumableItems.Consumable consumable) ||
                consumable.AbilityId != abilityId)
            {
                return false;
            }

            if (!ConsumableItems.IsUsable(consumable))
            {
                pak.sendSystemChatMessage(client, "This item can't be used yet.", "MODAL");
                return true;
            }

            double now = TimeUtils.GetSimTimeSeconds();
            if (data.isDead || AbilityCooldowns.IsTimerRunning(data, (UInt16)goid, now) ||
                (consumable.Group != 0 && AbilityCooldowns.IsTimerRunning(data, consumable.Group, now)))
            {
                Output.WriteDebugLog("[INVENTORY] Use of item " + goid + " ignored: dead or its timer is running");
                return true;
            }

            AbilityItem ability = DataLoader.getInstance().getAbilityByID(abilityId);
            float castTime = ability != null ? ability.getCastingTime() : 0;
            UInt16 amount = (UInt16)Math.Max(1, ability != null ? ability.getValueFrom() : 1);

            // Block a second use while this one is still casting.
            lock (data.cooldownEndsAt)
            {
                data.cooldownEndsAt[(UInt16)goid] = now + castTime + consumable.Seconds;
            }

            if (castTime > 0)
            {
                pak.SendCastAbilityBar(abilityId, castTime);
                client.FlushQueue();
            }

            DelayedActions.AfterFor(client, castTime, () =>
            {
                // The item may have been moved or listed during the cast: then nothing is used up and nothing happens
                if (Store.dbManager.WorldDbHandler.GetItemGOIDAtInventorySlot(slot) != goid)
                {
                    lock (data.cooldownEndsAt)
                    {
                        data.cooldownEndsAt.Remove((UInt16)goid);
                    }
                    Output.WriteDebugLog("[INVENTORY] Item " + goid + " left slot " + slot + " during the cast; not used");
                    return;
                }

                AbilityCooldowns.StartItem(client, (UInt16)goid, consumable.Group, consumable.Seconds,
                    TimeUtils.GetSimTimeSeconds());
                RemoveOne(client, slot);
                client.FlushQueue();
                if (consumable.Effect == ConsumableItems.Effect.Heal)
                {
                    AbilityHandler.HealSelf(client, amount);
                }
                Output.WriteDebugLog("[INVENTORY] Used item " + goid + " from slot " + slot + " (ability " + abilityId + ")");
            });
            return true;
        }

        public void processUnmountItem(ref byte[] rpcData)
        {
            byte sourceSlot = rpcData[0];
            byte destSlot   = rpcData[1];

            if (sourceSlot == WeaponSlot)
            {
                UnequipWeapon(destSlot);
                return;
            }

            UInt32 clothingItemId = Store.dbManager.WorldDbHandler.GetItemGOIDAtInventorySlot(rpcData[0]);
            DataLoader dataLoader = DataLoader.getInstance();
            ClothingItem item = dataLoader.getItemValues(clothingItemId);

            // Update Appeareance as we know which appeareance to update (as we unmount item it should be ok to set zeros for the values)
            string partModel = null;
            string partColor = null;
            switch (item.getClothesType())
            {
                case "1950892361":
                case "FEMALE HAT":
                case "MALE HAT":
                    partModel = "hat";
                    break;
                case "2001027401":
                case "FEMALE GLASSES":
                case "MALE GLASSES":
                    partModel = "glasses";
                    partColor = "glassescolor";
                    break;
                case "1750286665": // New Way but lets stay old values there
                case "FEMALE SHIRT":
                case "MALE SHIRT":
                    partModel = "shirt";
                    partColor = "shirtcolor";
                    break;
                case "1816609097":
                case "FEMALE GLOVES":
                case "MALE GLOVES":
                    partModel = "gloves";
                    break;
                case "2001682761":
                case "FEMALE COAT":
                case "MALE COAT":
                    partModel = "coat";
                    partColor = "coatcolor";
                    break;
                case "1397768521":
                case "MALE PANTS":
                case "FEMALE PANTS":
                    partModel = "pants";
                    partColor = "pantscolor";
                    break;
                case "1733050697":
                case "FEMALE LEGGINGS":
                    partModel = "leggins";
                    break;
                case "2001092937":
                case "FEMALE SHOES":
                case "MALE SHOES":
                    partModel = "shoes";
                    partColor = "shoecolor";
                    break;

            }

            if (partModel != null)
            {
                Store.dbManager.WorldDbHandler.UpdateRsiPartValue(partModel,0, Store.currentClient.playerData.getCharID());
            }

            if (partColor != null && item.getColorId()>0)
            {
                Store.dbManager.WorldDbHandler.UpdateRsiPartValue(partColor,0, Store.currentClient.playerData.getCharID());
            }
            // Move slot
            UInt16 newSlotId = Store.dbManager.WorldDbHandler.GetFirstNewSlot();
            Store.dbManager.WorldDbHandler.UpdateInventorySlot(sourceSlot,newSlotId, Store.currentClient.playerData.getCharID());

            // Send move packet
            ServerPackets pak = new ServerPackets();
            pak.sendInventoryItemMove(sourceSlot, newSlotId, Store.currentClient);

            Store.dbManager.WorldDbHandler.SetRsiValues();

            int[] current = Store.currentClient.playerData.getRsiValues();
            Store.currentClient.playerData.setRsiValues(current);
            byte[] rsiData = PacketsUtils.getRSIBytes(current);
            pak.sendAppeareanceUpdate(Store.currentClient, rsiData);
        }


        /// <summary>The weapon slot on the paper doll; clothing uses 0x61 (hat) to 0x68 (shoes).</summary>
        public const byte WeaponSlot = 0x69;

        /// <summary>
        /// Equip a weapon (RPC 0x63 with destination 0x69), as retail did: move any weapon already held
        /// to a free bag slot, move this one into the weapon slot (65 src 00 69 00) and set the player's
        /// EquippedItemID.
        /// </summary>
        private void EquipWeapon(byte sourceSlot)
        {
            UInt32 charId = Store.currentClient.playerData.getCharID();
            UInt32 weaponGoid = Store.dbManager.WorldDbHandler.GetItemGOIDAtInventorySlot(sourceSlot);
            ServerPackets pak = new ServerPackets();

            if (Store.dbManager.WorldDbHandler.IsSlotinUseByItem(WeaponSlot))
            {
                UInt16 freeSlot = Store.dbManager.WorldDbHandler.GetFirstNewSlot();
                Store.dbManager.WorldDbHandler.UpdateInventorySlot(WeaponSlot, freeSlot, charId);
                pak.sendInventoryItemMove(WeaponSlot, freeSlot, Store.currentClient);
            }

            Store.dbManager.WorldDbHandler.UpdateInventorySlot(sourceSlot, WeaponSlot, charId);
            pak.sendInventoryItemMove(sourceSlot, WeaponSlot, Store.currentClient);
            SetEquippedItem(weaponGoid);
            Output.WriteDebugLog("[INVENTORY] Equipped weapon " + weaponGoid + " from slot " + sourceSlot);
        }

        /// <summary>Unequip the weapon to the slot the client chose (65 69 00 dst 00), or a free one.</summary>
        private void UnequipWeapon(byte destSlot)
        {
            UInt32 charId = Store.currentClient.playerData.getCharID();
            UInt16 toSlot = destSlot != 0 ? destSlot : Store.dbManager.WorldDbHandler.GetFirstNewSlot();
            Store.dbManager.WorldDbHandler.UpdateInventorySlot(WeaponSlot, toSlot, charId);
            new ServerPackets().sendInventoryItemMove(WeaponSlot, toSlot, Store.currentClient);
            SetEquippedItem(0);
            Output.WriteDebugLog("[INVENTORY] Unequipped weapon to slot " + toSlot);
        }

        private static void SetEquippedItem(UInt32 goid)
        {
            Store.currentClient.playerInstance.EquippedItemID.setValue(goid);
            Store.currentClient.messageQueue.addObjectMessage(ServerPackets.BuildEquippedItemUpdate(goid), false);
            Store.currentClient.FlushQueue();
        }

        public void processMountItem(ref byte[] rpcData)
        {
            byte sourceSlot = rpcData[0];
            byte destSlot   = rpcData[1];

            if (destSlot == WeaponSlot)
            {
                EquipWeapon(sourceSlot);
                return;
            }

            UInt32 clothingItemId = Store.dbManager.WorldDbHandler.GetItemGOIDAtInventorySlot(rpcData[0]);
            DataLoader dataLoader = DataLoader.getInstance();
            ClothingItem item = dataLoader.getItemValues(clothingItemId);


            string partModel = null;
            string partColor = null;

            UInt16 toRealSlotId = 0;

            switch (item.getClothesType())
            {
                case "1950892361":
                case "FEMALE HAT":
                case "MALE HAT":
                    toRealSlotId = 0x61;
                    partModel = "hat";
                    break;
                case "2001027401":
                case "FEMALE GLASSES":
                case "MALE GLASSES":
                    toRealSlotId = 0x62;
                    partModel = "glasses";
                    partColor = "glassescolor";
                    break;
                case "1750286665": // New Way but lets stay old values there
                case "FEMALE SHIRT":
                case "MALE SHIRT":
                    toRealSlotId = 0x63;
                    partModel = "shirt";
                    partColor = "shirtcolor";
                    break;
                case "1816609097":
                case "FEMALE GLOVES":
                case "MALE GLOVES":
                    toRealSlotId = 0x64;
                    partModel = "gloves";
                    break;
                    
                case "2001682761":
                case "FEMALE COAT":
                case "MALE COAT":
                    toRealSlotId = 0x65;
                    partModel = "coat";
                    partColor = "coatcolor";
                    break;
                case "1397768521":
                case "MALE PANTS":
                case "FEMALE PANTS":
                    toRealSlotId = 0x66;
                    partModel = "pants";
                    partColor = "pantscolor";
                    break;
                case "1733050697":
                case "FEMALE LEGGINGS":
                    toRealSlotId = 0x67;
                    partModel = "leggins";
                    break;
                case "2001092937":
                case "FEMALE SHOES":
                case "MALE SHOES":
                    toRealSlotId = 0x68;
                    partModel = "shoes";
                    partColor = "shoecolor";
                    break;

            }

            if (toRealSlotId == 0)
            {
                // Not a known clothing item. This used to carry on and move the item to inventory slot 0.
                Output.WriteDebugLog("[INVENTORY] Mount request for item " + clothingItemId + " from slot " +
                                     sourceSlot + " to " + destSlot + " ignored: not a known clothing item");
                return;
            }

            if (partModel != null)
            {
                Store.dbManager.WorldDbHandler.UpdateRsiPartValue(partModel,item.getModelId(), Store.currentClient.playerData.getCharID());
            }

            if (partColor != null)
            {
                Store.dbManager.WorldDbHandler.UpdateRsiPartValue(partColor,item.getColorId(), Store.currentClient.playerData.getCharID());
            }
            Store.dbManager.WorldDbHandler.SetRsiValues();

            int[] current = Store.currentClient.playerData.getRsiValues();
            Store.currentClient.playerData.setRsiValues(current);
            byte[] rsiData = PacketsUtils.getRSIBytes(current);


            // Move the wear item from the wearing slot to the next new slot (but maybe doesnt work if inventory is full)

            ServerPackets pak = new ServerPackets();
            if (Store.dbManager.WorldDbHandler.IsSlotinUseByItem(toRealSlotId))
            {
                UInt16 newSlotId = Store.dbManager.WorldDbHandler.GetFirstNewSlot();
                Store.dbManager.WorldDbHandler.UpdateInventorySlot(toRealSlotId,newSlotId, Store.currentClient.playerData.getCharID());
                pak.sendInventoryItemMove(toRealSlotId, newSlotId, Store.currentClient);
            }
            pak.sendAppeareanceUpdate(Store.currentClient, rsiData);
            Store.dbManager.WorldDbHandler.UpdateInventorySlot(sourceSlot,toRealSlotId, Store.currentClient.playerData.getCharID());
            pak.sendInventoryItemMove(sourceSlot, toRealSlotId, Store.currentClient);



        }
    }
}
