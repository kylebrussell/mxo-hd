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

            if (!Crafting.Recipes.TryGetValue(resultGoid, out Crafting.Recipe recipe))
            {
                return "There is no recipe for item " + resultGoid + ".";
            }

            // Bag items first, then code storage (a guess: no capture shows where the Code Writer takes ingredients from).
            List<Crafting.InventoryItem> inventory = Store.dbManager.WorldDbHandler.GetInventoryItems(charId);
            inventory.AddRange(CodeStorageHandler.AsIngredients(Store.dbManager.WorldDbHandler.GetCodeStorage(charId)));
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

            // One item per ingredient: a stack shrinks (69 01), a single item leaves its slot (5f), a storage entry
            // shrinks (69 00).
            foreach (UInt16 slot in slots)
            {
                if ((slot & Crafting.StorageSlotFlag) != 0)
                {
                    CodeStorageHandler.TakeOne(client, (UInt16)(slot & ~Crafting.StorageSlotFlag));
                }
                else
                {
                    InventoryHandler.RemoveOne(client, slot);
                }
            }

            // Ingredients are gone, so their slots count as free again; a fragment joins a stack of its kind.
            UInt16 resultSlot = InventoryHandler.AddItem(client, resultGoid, 0);
            client.FlushQueue();

            Output.WriteDebugLog("[CRAFTING] Compiled " + resultGoid + " into slot " + resultSlot + " from slots " +
                                 string.Join(",", slots));
            return "Compiled " + ItemName(resultGoid) + ".";
        }

        /// <summary>CompileActivateAbility: the stance held while compiling (abilityIDs.csv).</summary>
        public const UInt16 CompileActivateAbility = 969;

        /// <summary>
        /// A guess at the Code Writer's Compile request, which no capture shows. Write Code (970,
        /// WriteCodeActivateAbility) reached retail as a normal 80 b9 ability request, so Compile is expected as
        /// 80 b9 with CompileActivateAbility (969). The whole body is logged so a real-client test shows it; if any
        /// u32 in it is the result goid of a recipe the character knows, that recipe is compiled.
        /// </summary>
        public bool HandleCompileRequest(byte[] body)
        {
            WorldClient client = Store.currentClient;
            Output.WriteDebugLog("[CRAFTING] Compile request body: " + BitConverter.ToString(body).Replace("-", " "));

            UInt32? goid = FindKnownRecipe(body, KnownRecipes);
            if (!goid.HasValue)
            {
                Output.WriteDebugLog("[CRAFTING] No known recipe goid in the compile request");
                return false;
            }

            string result = Compile(goid.Value);
            new ServerPackets().sendSystemChatMessage(client, result, "BROADCAST");
            client.FlushQueue();
            return true;
        }

        /// <summary>The first u32 (at any byte offset) of a request body that is a known recipe's result goid.</summary>
        public static UInt32? FindKnownRecipe(byte[] body, ICollection<UInt32> known)
        {
            for (int i = 0; i + 4 <= body.Length; i++)
            {
                UInt32 value = BitConverter.ToUInt32(body, i);
                if (value != 0 && known.Contains(value))
                {
                    return value;
                }
            }
            return null;
        }

        private static HashSet<UInt32> knownRecipes;

        /// <summary>The recipes every character knows: the margin's known-recipe list (opcode 0x0a).</summary>
        public static HashSet<UInt32> KnownRecipes
        {
            get
            {
                if (knownRecipes == null)
                {
                    byte[] block = Crafting.BuildKnownRecipesBlock(MarginClient.KnownRecipesBaseBlock);
                    HashSet<UInt32> set = new HashSet<UInt32>();
                    for (int i = 4; i + 4 <= block.Length; i += 4)
                    {
                        set.Add(BitConverter.ToUInt32(block, i));
                    }
                    knownRecipes = set;
                }
                return knownRecipes;
            }
        }

        private static string ItemName(UInt32 goid)
        {
            GameObjectItem item = goid <= UInt16.MaxValue ? DataLoader.getInstance().getGameObjectItemById((UInt16)goid) : null;
            return item != null ? item.getName() : goid.ToString();
        }
    }
}
