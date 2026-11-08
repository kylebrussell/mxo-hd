using System;
using System.Collections.Generic;
using System.IO;

namespace hds
{
    /// <summary>
    /// Code crafting recipes from the client's own cookbook (resource/GameObjects/Cookbook.cbb, exported to
    /// data/recipes.csv by scripts/export-client-cookbook.py). A recipe turns its ingredients into one item:
    /// Code Bits (7332-7339) compile into code fragments (Class Routines, Function Subroutines, Variable
    /// Fragments), and fragments compile into items and ability codes.
    /// </summary>
    public static class Crafting
    {
        public class Recipe
        {
            public UInt32 ResultGoid;
            /// <summary>Second u32 of the cookbook record: 1-50, rises with the item tier (taken to be the level).</summary>
            public int Level;
            /// <summary>Four-character type tag, e.g. ICP0 (consumable pill), FCV0 (variable fragment).</summary>
            public string Type;
            /// <summary>Ingredient goids; a goid listed twice is needed twice.</summary>
            public UInt32[] Ingredients;
        }

        public struct InventoryItem
        {
            public UInt16 Slot;
            public UInt32 Goid;
            /// <summary>Items in the slot (a stack); at least 1.</summary>
            public int Count;

            public InventoryItem(UInt16 slot, UInt32 goid, int count = 1)
            {
                Slot = slot;
                Goid = goid;
                Count = Math.Max(1, count);
            }
        }

        private static Dictionary<UInt32, Recipe> recipes;
        private static readonly object loadLock = new object();

        /// <summary>Recipes by result goid, loaded from data/recipes.csv on first use.</summary>
        public static Dictionary<UInt32, Recipe> Recipes
        {
            get
            {
                lock (loadLock)
                {
                    if (recipes == null)
                    {
                        string path = DataLoader.DataPath("recipes.csv");
                        recipes = File.Exists(path) ? Parse(File.ReadLines(path)) : new Dictionary<UInt32, Recipe>();
                        Output.WriteDebugLog("[CRAFTING] Loaded " + recipes.Count + " recipes from " + path);
                    }
                    return recipes;
                }
            }
        }

        /// <summary>Parses recipes.csv lines: ResultGOID;Level;Type;FieldA;FieldB;Ingredients (space separated).</summary>
        public static Dictionary<UInt32, Recipe> Parse(IEnumerable<string> lines)
        {
            Dictionary<UInt32, Recipe> result = new Dictionary<UInt32, Recipe>();
            foreach (string line in lines)
            {
                if (line.Length == 0 || line[0] == '#' || line.StartsWith("ResultGOID"))
                {
                    continue;
                }

                string[] fields = line.Split(';');
                if (fields.Length < 6 || !UInt32.TryParse(fields[0], out UInt32 goid))
                {
                    continue;
                }

                string[] parts = fields[5].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                UInt32[] ingredients = new UInt32[parts.Length];
                for (int i = 0; i < parts.Length; i++)
                {
                    ingredients[i] = UInt32.Parse(parts[i]);
                }

                int.TryParse(fields[1], out int level);
                result[goid] = new Recipe { ResultGoid = goid, Level = level, Type = fields[2], Ingredients = ingredients };
            }
            return result;
        }

        /// <summary>
        /// The inventory slots that pay for a recipe, one entry per ingredient item (a slot appears twice when two
        /// items come from its stack), or null when an ingredient is missing. Only bag slots (below 97, the worn
        /// items) are used, lowest slots first.
        /// </summary>
        public static List<UInt16> MatchIngredients(Recipe recipe, IEnumerable<InventoryItem> inventory)
        {
            List<InventoryItem> available = BagItems(inventory);
            int[] left = new int[available.Count];
            for (int i = 0; i < available.Count; i++)
            {
                left[i] = available[i].Count;
            }

            List<UInt16> used = new List<UInt16>();
            foreach (UInt32 ingredient in recipe.Ingredients)
            {
                int index = -1;
                for (int i = 0; i < available.Count; i++)
                {
                    if (available[i].Goid == ingredient && left[i] > 0)
                    {
                        index = i;
                        break;
                    }
                }
                if (index < 0)
                {
                    return null;
                }
                left[index]--;
                used.Add(available[index].Slot);
            }
            return used;
        }

        /// <summary>The goids a recipe still lacks (each missing one listed once per missing copy).</summary>
        public static List<UInt32> MissingIngredients(Recipe recipe, IEnumerable<InventoryItem> inventory)
        {
            Dictionary<UInt32, int> have = new Dictionary<UInt32, int>();
            foreach (InventoryItem item in BagItems(inventory))
            {
                have.TryGetValue(item.Goid, out int n);
                have[item.Goid] = n + item.Count;
            }

            List<UInt32> missing = new List<UInt32>();
            foreach (UInt32 ingredient in recipe.Ingredients)
            {
                if (have.TryGetValue(ingredient, out int n) && n > 0)
                {
                    have[ingredient] = n - 1;
                }
                else
                {
                    missing.Add(ingredient);
                }
            }
            return missing;
        }

        private static List<InventoryItem> BagItems(IEnumerable<InventoryItem> inventory)
        {
            List<InventoryItem> bag = new List<InventoryItem>();
            foreach (InventoryItem item in inventory)
            {
                if (item.Slot < 97)
                {
                    bag.Add(item);
                }
            }
            bag.Sort((a, b) => a.Slot.CompareTo(b.Slot));
            return bag;
        }

        /// <summary>
        /// The margin's known-recipe block (character data opcode 0x0a): u32 4, then the result goid (u32) of
        /// every recipe the character knows; the Code Writer lists these. All 425 goids of the list the margin
        /// sent until now are cookbook results (tools, weapons, clothing, ...). This keeps that list and adds the
        /// recipes that turn Code Bits into code fragments and the health pill recipes, so Code Bits looted from
        /// NPCs can be compiled.
        /// </summary>
        public static byte[] BuildKnownRecipesBlock(byte[] baseBlock)
        {
            List<UInt32> goids = new List<UInt32>();
            HashSet<UInt32> seen = new HashSet<UInt32>();
            for (int i = 4; i + 4 <= baseBlock.Length; i += 4)
            {
                UInt32 goid = BitConverter.ToUInt32(baseBlock, i);
                if (seen.Add(goid))
                {
                    goids.Add(goid);
                }
            }

            foreach (UInt32 goid in ExtraKnownRecipes())
            {
                if (seen.Add(goid))
                {
                    goids.Add(goid);
                }
            }

            byte[] block = new byte[4 + goids.Count * 4];
            Array.Copy(baseBlock, 0, block, 0, Math.Min(4, baseBlock.Length));
            for (int i = 0; i < goids.Count; i++)
            {
                BitConverter.GetBytes(goids[i]).CopyTo(block, 4 + i * 4);
            }
            return block;
        }

        /// <summary>Recipes made only of Code Bits (the code fragments), plus the health pills.</summary>
        public static List<UInt32> ExtraKnownRecipes()
        {
            List<UInt32> extra = new List<UInt32>();
            foreach (Recipe recipe in Recipes.Values)
            {
                bool fromBits = recipe.Ingredients.Length > 0;
                foreach (UInt32 ingredient in recipe.Ingredients)
                {
                    fromBits &= ingredient >= 7332 && ingredient <= 7339;
                }
                if (fromBits)
                {
                    extra.Add(recipe.ResultGoid);
                }
            }
            extra.Sort();
            foreach (UInt32 pill in new UInt32[] { 2399, 6897, 6924, 6925, 7913 })
            {
                if (Recipes.ContainsKey(pill))
                {
                    extra.Add(pill);
                }
            }
            return extra;
        }
    }
}
