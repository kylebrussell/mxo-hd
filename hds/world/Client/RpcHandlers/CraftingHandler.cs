using System;
using System.Collections.Generic;
using hds.shared;

namespace hds
{
    /// <summary>
    /// Compiles an item from its recipe: the ingredients leave the inventory (5f for each slot) and the
    /// result arrives in a free slot (5e), the same inventory messages retail used for sells and loot.
    /// </summary>
    public class CraftingHandler
    {
        /// <summary>Returns the message shown to the player.</summary>
        public string Compile(UInt32 resultGoid)
        {
            WorldClient client = Store.currentClient;
            UInt32 charId = client.playerData.getCharID();
            ServerPackets pak = new ServerPackets();

            if (!Crafting.Recipes.TryGetValue(resultGoid, out Crafting.Recipe recipe))
            {
                return "There is no recipe for item " + resultGoid + ".";
            }

            List<Crafting.InventoryItem> inventory = Store.dbManager.WorldDbHandler.GetInventoryItems(charId);
            List<UInt16> slots = Crafting.MatchIngredients(recipe, inventory);
            if (slots == null)
            {
                List<string> names = new List<string>();
                foreach (UInt32 goid in Crafting.MissingIngredients(recipe, inventory))
                {
                    names.Add(ItemName(goid));
                }
                return "Missing to compile " + ItemName(resultGoid) + ": " + string.Join(", ", names);
            }

            foreach (UInt16 slot in slots)
            {
                Store.dbManager.WorldDbHandler.RemoveItemAtInventorySlot(slot, charId);
                pak.sendInventoryItemDelete(slot, client);
            }

            // Ingredients are gone, so their slots count as free again.
            UInt16 freeSlot = Store.dbManager.WorldDbHandler.GetFirstNewSlot();
            Store.dbManager.WorldDbHandler.AddItemToInventory(freeSlot, resultGoid);
            pak.sendInventoryItemAdd(freeSlot, resultGoid, 0, 0, client);
            client.FlushQueue();

            Output.WriteDebugLog("[CRAFTING] Compiled " + resultGoid + " into slot " + freeSlot + " from slots " +
                                 string.Join(",", slots));
            return "Compiled " + ItemName(resultGoid) + ".";
        }

        private static string ItemName(UInt32 goid)
        {
            GameObjectItem item = goid <= UInt16.MaxValue ? DataLoader.getInstance().getGameObjectItemById((UInt16)goid) : null;
            return item != null ? item.getName() : goid.ToString();
        }
    }
}
