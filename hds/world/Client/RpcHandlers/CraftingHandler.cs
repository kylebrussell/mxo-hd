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
        private const ushort DecompileItemsAbility = 276;

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
                bool writer = client.playerData.craftingSession?.Ability == CraftingSession.WriterAbility;
                var request = CraftingRequests.ParseCompile(body, writer);
                var db = Store.dbManager.WorldDbHandler;
                byte refused = writer ? CraftingRequests.WriterRefused : (byte)0;
                if (request == null || !Ready(client) || !Crafting.Recipes.TryGetValue(request.Item, out var recipe) ||
                    CraftingRules.Find(request.Item)?.CantCraft != false || !CraftingRequests.Matches(request, recipe) ||
                    !KnownRecipes.Contains(request.Item) && !db.GetLearnedRecipes(client.playerData.getCharID()).Contains(request.Item))
                    return Finish(client, refused, 0, request?.Item ?? 0, "That compile request is not available.");
                ushort ability = writer ? CraftingSession.WriterAbility : CraftingSession.Ability;
                if (!CraftingSession.Authorized(client, request.ToolSlot, ability))
                    return Finish(client, refused, 0, request.Item, "That compile request is not available.");
                if (writer)
                {
                    bool abilityCode = AbilityProgression.IsAbilityCode(recipe.ResultGoid);
                    uint expectedSource = abilityCode ? recipe.ResultGoid : (recipe.ResultGoid << 7);
                    if (request.SourceCode != expectedSource)
                        return Finish(client, refused, 0, request.Item, "That compile request is not available.");
                    return WriteCode(client, request, abilityCode);
                }
                var bag = db.GetInventoryItems(client.playerData.getCharID());
                var available = bag.Concat(CodeStorageHandler.AsIngredients(db.GetCodeStorage(client.playerData.getCharID()))).ToList();
                var slots = Crafting.MatchIngredients(recipe, available);
                if (slots == null || !CraftingRules.HasResultSpace(request.Item, bag, slots))
                    return Finish(client, 0, 0, request.Item, "You need the recipe's ingredients and room for its result.");
                var item = CraftingRules.Find(request.Item);
                if (!item.Complexity.HasValue) return Finish(client, 0, 0, request.Item, "This item's crafting complexity is unknown.");
                long cost = CraftingRules.InfoCost(item, Skill(client, 530));
                if (client.playerData.getInfo() < cost) return Finish(client, 4, 0, request.Item, "Not enough information to compile this item.");
                int skill = Skill(client, 706);
                ushort specialty = Specialization(item.Category);
                int bonus = Skill(client, specialty);
                int roll = Roll();
                bool success = CraftingRules.Succeeds(skill, item.Complexity.Value, roll, bonus);
                Output.WriteDebugLog($"[COMPILE RECIPE] char={client.playerData.getCharID()} item={request.Item} " +
                    $"skill={skill} stat={specialty} specialization={bonus} effective={skill + Math.Max(0, bonus) / 10} " +
                    $"complexity={item.Complexity.Value} roll={roll} info={cost} success={success}");
                long cash = client.playerData.getInfo() - cost;
                db.SaveInfo(client, cash);
                client.playerData.setInfo(cash);
                new ServerPackets().SendInfoCurrent(client, (uint)cash);
                if (!success) return Finish(client, 0, 0, request.Item, "Failed to compile the item.");
                string result = Compile(request.Item, out ushort resultSlot);
                return Finish(client, resultSlot == 0 ? (byte)0 : (byte)1, (byte)resultSlot, request.Item, result);
            }
        }

        private static bool WriteCode(WorldClient client, CraftingRequests.Compile request, bool abilityCode)
        {
            uint goid = request.Item;
            var item = CraftingRules.Find(goid);
            if (!(abilityCode ? CraftingRules.CanWriteAbilityCode(goid) : OrdinaryItemCode.Supported(goid)))
                return Finish(client, CraftingRequests.WriterRefused, 0, goid, "That Write Code result is not supported.");
            var (cost, requiredInfo) = CraftingRules.WriterCost(item, Skill(client, 530), Skill(client, 956));
            if (client.playerData.getInfo() < requiredInfo)
                return Finish(client, CraftingRequests.WriterNoInfo, 0, goid, "Not enough information to write this code.");
            int writing = Skill(client, 649);
            ushort category = Specialization(item.Category);
            int specialty = Skill(client, category);
            int skill = Math.Max(writing, specialty);
            int roll = Roll();
            bool success = CraftingRules.Succeeds(skill, item.Complexity.Value, roll);
            Output.WriteDebugLog($"[WRITECODE] char={client.playerData.getCharID()} item={goid} writing={writing} " +
                $"stat={category} specialization={specialty} effective={skill} complexity={item.Complexity.Value} " +
                $"roll={roll} info={cost} success={success}");
            if (!success)
                return Finish(client, CraftingRequests.WriterRefused, 0, goid, abilityCode ? "Failed to write the ability code." : "Failed to write the item code.");

            CraftingRequests.WrittenCode result;
            try
            {
                result = Store.dbManager.WorldDbHandler.WriteRecipeCode(
                    client.playerData.getCharID(), request, cost, requiredInfo);
            }
            catch (Exception error)
            {
                Output.WriteDebugLog("[WRITECODE] Rolled back " + goid + ": " + error.Message);
                return Finish(client, CraftingRequests.WriterRefused, 0, goid, "The item code could not be written.");
            }
            if (result.Result != CraftingRequests.WriterSuccess)
                return Finish(client, result.Result, 0, goid, result.Result switch
                {
                    CraftingRequests.WriterNoSpace => abilityCode ? "Your bag is full." : "Your code inventory is full.",
                    CraftingRequests.WriterNoInfo => "Not enough information to write this code.",
                    _ => "You need the recipe's ingredients and the active Code Analyser."
                });

            foreach (var (ingredient, remaining) in result.Ingredients)
            {
                ushort slot = (ushort)(ingredient.Slot & ~Crafting.StorageSlotFlag);
                if (slot != ingredient.Slot)
                    client.messageQueue.addRpcMessage(remaining == 0 ? CodeStorage.BuildRemove(slot)
                        : CodeStorage.BuildUpdate(slot, ingredient.Goid, remaining));
                else
                    client.messageQueue.addRpcMessage(remaining == 0 ? ServerPackets.BuildInventoryItemDelete(slot)
                        : ItemStacks.BuildStackCount(slot, remaining));
            }
            if (abilityCode)
                client.messageQueue.addRpcMessage(ServerPackets.BuildAbilityCodeAdd(result.Slot, goid));
            else
                client.messageQueue.addRpcMessage(ServerPackets.BuildKnownAbilityAdd(result.Slot,
                    OrdinaryItemCode.Packed(goid << 7, item.DefaultCodeCharges.Value)));
            client.playerData.setInfo(result.Cash);
            new ServerPackets().SendInfoCurrent(client, (uint)result.Cash);
            Output.WriteDebugLog($"[WRITECODE] Wrote {goid} into {(abilityCode ? "bag" : "code")} slot {result.Slot}");
            // In the Writer, 6208cac2 passes result type and XP to the UI without reading the byte slot.
            return Finish(client, CraftingRequests.WriterSuccess, 0, goid, "Wrote code for " + ItemName(goid) + ".");
        }

        private static readonly Random random = new Random();
        private static int Roll()
        {
            lock (random) return (int)Math.Clamp(50 + 16 * Math.Sqrt(-2 * Math.Log(1 - random.NextDouble())) *
                Math.Cos(2 * Math.PI * random.NextDouble()), 0, 100);
        }

        private static bool Ready(WorldClient client) => !client.playerData.isDead && !client.playerData.duelDeath &&
            !CombatManager.GetHandler(client).isCombatRunning && ControlActions.CanUseAbilities(client);

        private static bool Finish(WorldClient client, byte result, byte slot, uint item, string message, string chat = "SYSTEM")
        {
            client.messageQueue.addRpcMessage(CraftingRequests.BuildResult(result, slot, item));
            new ServerPackets().sendSystemChatMessage(client, message, chat);
            client.FlushQueue();
            return true;
        }

        internal static int Skill(WorldClient client, ushort stat) => AbilityStats.Player(client, stat);

        internal static ushort Specialization(uint category)
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
                if (request == null || request.Ability != DecompileItemsAbility || request.Slot == 0 || request.Slot >= 97 ||
                    !Ready(client) || !CombatSelection.IsLoaded(DecompileItemsAbility,
                        Store.dbManager.MarginDbHandler.LoadAbilities((int)client.playerData.getCharID())))
                { Finish(client, 0, 0, 0, "That inventory ability is not available."); return; }
                var db = Store.dbManager.WorldDbHandler;
                var inventory = db.GetInventoryItems(client.playerData.getCharID());
                var source = inventory.FirstOrDefault(i => i.Slot == request.Slot);
                if (request.ToolSlot < 1 || request.ToolSlot > 96 ||
                    !inventory.Any(i => i.Slot == request.ToolSlot && i.Goid == Crafting.DecompilerTool))
                { Finish(client, 0, request.Slot, source.Goid, "Decompile Items requires an Item Decompiler."); return; }
                var item = CraftingRules.Find(source.Goid);
                if (item == null || item.CantDecompile || item.Complexity.GetValueOrDefault() <= 0 || !item.Learnable ||
                    !Crafting.Recipes.TryGetValue(source.Goid, out var recipe))
                { Finish(client, 0, request.Slot, source.Goid, "This item cannot teach a recipe by decompiling."); return; }
                double now = TimeUtils.GetSimTimeSeconds();
                if (AbilityCooldowns.IsCasting(client.playerData, now) ||
                    AbilityCooldowns.IsCoolingDown(client.playerData, DecompileItemsAbility, now))
                { Finish(client, 0, request.Slot, source.Goid, "Decompile Items is not ready."); return; }
                AbilityCooldowns.Start(client, DecompileItemsAbility, now);
                int skill = Skill(client, 707);
                int roll = Roll();
                int complexity = item.Complexity.Value;
                bool success = CraftingRules.Succeeds(skill, complexity, roll);
                Output.WriteDebugLog($"[DECOMPILE] char={client.playerData.getCharID()} item={source.Goid} skill={skill} " +
                    $"complexity={complexity} roll={roll} total={4 * skill + roll} requiredAbove={4 * complexity + 50} success={success}");
                if (!success)
                { Finish(client, 0, request.Slot, source.Goid, "Failed to decompile the item."); return; }
                var recovered = CraftingRules.RecoverFragments(recipe, skill, Roll);
                var publication = new AbilityAcquisition.Publication(client);
                Crafting.DecompileResult result;
                try
                {
                    result = db.DecompileItem(publication.Character, request.Slot, request.ToolSlot, source.Goid,
                        KnownRecipes.Contains(source.Goid), recovered);
                }
                catch (System.Data.Common.DbException ex)
                {
                    Output.WriteDebugLog($"[DECOMPILE] Transaction failed for char={client.playerData.getCharID()} item={source.Goid}: {ex}");
                    Finish(client, 0, request.Slot, source.Goid, "The item could not be decompiled.", "BROADCAST");
                    return;
                }
                if (result.Outcome != Crafting.DecompileOutcome.Succeeded)
                {
                    string refusal = result.Outcome == Crafting.DecompileOutcome.BagFull
                        ? "Your inventory is full. Make room for the recovered fragments."
                        : "The item or decompiler moved before the decompile finished.";
                    Finish(client, 0, request.Slot, source.Goid, refusal, "BROADCAST");
                    return;
                }
                var packets = new List<byte[]>
                {
                    result.Remaining == 0 ? ServerPackets.BuildInventoryItemDelete(request.Slot)
                        : ItemStacks.BuildStackCount(request.Slot, result.Remaining)
                };
                string message;
                if (result.Learned)
                {
                    byte[] learned = new byte[11] { 0x81, 0x30, 0, 0, 0, 0, (byte)item.Complexity.Value, 0, 0, 0, 0 };
                    BitConverter.GetBytes(source.Goid).CopyTo(learned, 2);
                    BitConverter.GetBytes(item.Category).CopyTo(learned, 7);
                    packets.Add(learned);
                    message = "Learned the recipe for " + item.Name + ".";
                }
                else
                {
                    foreach (var (grant, added) in result.Grants)
                        packets.Add(added
                            ? ServerPackets.BuildInventoryItemAdd(grant.Slot, grant.Goid, ItemStacks.AmountByte(grant.Count), 0)
                            : ItemStacks.BuildStackCount(grant.Slot, grant.Count));
                    Output.WriteDebugLog($"[DECOMPILE] Recovered char={publication.Character} item={source.Goid} fragments={string.Join(",", recovered)}");
                    message = recovered.Count == 0 ? "No fragments recovered from " + item.Name + "."
                        : $"Recovered {recovered.Count} code fragments from {item.Name}.";
                }
                packets.Add(CraftingRequests.BuildResult(1, request.Slot, source.Goid));
                packets.Add(ServerPackets.BuildSystemChatMessage(message, "BROADCAST"));
                if (publication.Publish(client, packets.ToArray())) client.FlushQueue();
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
