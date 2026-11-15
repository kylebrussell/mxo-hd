using System;
using System.Collections.Generic;
using System.Linq;
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
        public string Compile(UInt32 resultGoid) => Compile(resultGoid, out _);

        private string Compile(UInt32 resultGoid, out UInt16 resultSlot)
        {
            resultSlot = 0;
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

            if (!CraftingRules.HasResultSpace(resultGoid, inventory, slots)) return "Your inventory is full.";

            // One item per ingredient: a stack shrinks (69 01), a single item leaves its slot (5f), a storage entry
            // shrinks (69 00) or leaves the archive (5f with PersonalInventoryItem 0).
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
            resultSlot = InventoryHandler.AddItem(client, resultGoid, 0);
            client.FlushQueue();

            Output.WriteDebugLog("[CRAFTING] Compiled " + resultGoid + " into slot " + resultSlot + " from slots " +
                                 string.Join(",", slots));
            return "Compiled " + ItemName(resultGoid) + ".";
        }

        /// <summary>CompileActivateAbility: the stance held while compiling (abilityIDs.csv).</summary>
        public const UInt16 CompileActivateAbility = 969;

        /// <summary>81 2e's structured request; the ordinary ability-use opcode never compiles an item.</summary>
        public bool HandleCompileRequest(byte[] body)
        {
            WorldClient client = Store.currentClient;
            lock (client.playerData.craftingSync)
            {
                var request = CraftingRequests.ParseCompile(body);
                var db = Store.dbManager.WorldDbHandler;
                var bag = db.GetInventoryItems(client.playerData.getCharID());
                if (request == null || !CraftingSession.Authorized(client, request.ToolSlot) || !Ready(client) || !bag.Any(i => i.Slot == request.ToolSlot && i.Goid == 1271) ||
                    !CombatSelection.IsLoaded(969, Store.dbManager.MarginDbHandler.LoadAbilities((int)client.playerData.getCharID())) ||
                    !Crafting.Recipes.TryGetValue(request.Item, out var recipe) || CraftingRules.Find(request.Item)?.CantCraft != false ||
                    !CraftingRequests.Matches(request, recipe) ||
                    !KnownRecipes.Contains(request.Item) && !db.GetLearnedRecipes(client.playerData.getCharID()).Contains(request.Item))
                    return Finish(client, 0, 0, request?.Item ?? 0, "That compile request is not available.");
                var available = bag.Concat(CodeStorageHandler.AsIngredients(db.GetCodeStorage(client.playerData.getCharID()))).ToList();
                var slots = Crafting.MatchIngredients(recipe, available);
                if (slots == null || !CraftingRules.HasResultSpace(request.Item, bag, slots))
                    return Finish(client, 0, 0, request.Item, "You need the recipe's ingredients and room for its result.");
                var item = CraftingRules.Find(request.Item);
                if (!item.Complexity.HasValue) return Finish(client, 0, 0, request.Item, "This item's crafting complexity is unknown.");
                long cost = CraftingRules.InfoCost(item, Skill(client, 530));
                if (client.playerData.getInfo() < cost) return Finish(client, 4, 0, request.Item, "Not enough information to compile this item.");
                int skill = Skill(client, 706);
                bool success = CraftingRules.Succeeds(skill, item.Complexity.Value, Roll(), Skill(client, Specialization(item.Category)));
                long cash = client.playerData.getInfo() - cost;
                db.SaveInfo(client, cash);
                client.playerData.setInfo(cash);
                new ServerPackets().SendInfoCurrent(client, (uint)cash);
                if (!success) return Finish(client, 0, 0, request.Item, "Failed to compile the item.");
                string result = Compile(request.Item, out ushort resultSlot);
                return Finish(client, resultSlot == 0 ? (byte)0 : (byte)1, (byte)resultSlot, request.Item, result);
            }
        }

        private static readonly Random random = new Random();
        private static int Roll()
        {
            lock (random) return (int)Math.Clamp(50 + 16 * Math.Sqrt(-2 * Math.Log(1 - random.NextDouble())) *
                Math.Cos(2 * Math.PI * random.NextDouble()), 0, 100);
        }

        private static bool Ready(WorldClient client) => !client.playerData.isDead && !client.playerData.duelDeath &&
            !CombatManager.GetHandler(client).isCombatRunning && ControlActions.CanUseAbilities(client);

        private static bool Finish(WorldClient client, byte result, byte slot, uint item, string message)
        {
            client.messageQueue.addRpcMessage(CraftingRequests.BuildResult(result, slot, item));
            new ServerPackets().sendSystemChatMessage(client, message, "SYSTEM");
            client.FlushQueue();
            return true;
        }

        internal static int Skill(WorldClient client, ushort stat) => AbilityStats.Player(client, stat);

        private static ushort Specialization(uint category)
        {
            string tag = System.Text.Encoding.ASCII.GetString(BitConverter.GetBytes(category));
            return tag.StartsWith("IW") ? (ushort)655 : tag.StartsWith("IA") ? (ushort)652 : tag.StartsWith("IT") ? (ushort)654 :
                tag.StartsWith("IC") ? (ushort)656 : tag.StartsWith("A") ? (ushort)653 : (ushort)0;
        }

        /// <summary>81 31, inventory-targeted abilities. Decompile's recipe-known byte is a UI hint, never authority.</summary>
        public static void ProcessInventoryAbility(WorldClient client, byte[] body)
        {
            lock (client.playerData.craftingSync)
            {
                var request = CraftingRequests.ParseInventoryAbility(body);
                if (request == null || !CraftingRequests.IsInventoryTargetAbility(request.Ability) ||
                    request.Ability != 276 || request.Slot == 0 || request.Slot >= 97 || !Ready(client) ||
                    !CombatSelection.IsLoaded(276, Store.dbManager.MarginDbHandler.LoadAbilities((int)client.playerData.getCharID())))
                { Finish(client, 0, 0, 0, "That inventory ability is not available."); return; }
                var db = Store.dbManager.WorldDbHandler;
                var source = db.GetInventoryItems(client.playerData.getCharID()).FirstOrDefault(i => i.Slot == request.Slot);
                var item = CraftingRules.Find(source.Goid);
                if (item == null || item.CantDecompile || !item.Complexity.HasValue || !item.Learnable ||
                    !Crafting.Recipes.TryGetValue(source.Goid, out var recipe))
                { Finish(client, 0, request.Slot, source.Goid, "This item cannot teach a recipe by decompiling."); return; }
                int skill = Skill(client, 707);
                if (!CraftingRules.Succeeds(skill, item.Complexity.Value, Roll()))
                { Finish(client, 0, request.Slot, source.Goid, "Failed to decompile the item."); return; }
                // Persist learning before deleting the item; a failed database write cannot destroy the source.
                db.LearnRecipe(client.playerData.getCharID(), source.Goid);
                InventoryHandler.RemoveOne(client, request.Slot);
                byte[] learned = new byte[11] { 0x81, 0x30, 0, 0, 0, 0, (byte)item.Complexity.Value, 0, 0, 0, 0 };
                BitConverter.GetBytes(source.Goid).CopyTo(learned, 2);
                BitConverter.GetBytes(item.Category).CopyTo(learned, 7);
                client.messageQueue.addRpcMessage(learned);
                Finish(client, 1, request.Slot, source.Goid, "Learned the recipe for " + item.Name + ".");
            }
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
