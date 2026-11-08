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

            public InventoryItem(UInt16 slot, UInt32 goid)
            {
                Slot = slot;
                Goid = goid;
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
        /// The inventory slots that pay for a recipe, one slot per ingredient (a stack counts as one item),
        /// or null when an ingredient is missing. Only bag slots (below 97, the worn items) are used.
        /// </summary>
        public static List<UInt16> MatchIngredients(Recipe recipe, IEnumerable<InventoryItem> inventory)
        {
            List<InventoryItem> available = new List<InventoryItem>();
            foreach (InventoryItem item in inventory)
            {
                if (item.Slot < 97)
                {
                    available.Add(item);
                }
            }
            available.Sort((a, b) => a.Slot.CompareTo(b.Slot));

            List<UInt16> used = new List<UInt16>();
            foreach (UInt32 ingredient in recipe.Ingredients)
            {
                int index = available.FindIndex(item => item.Goid == ingredient);
                if (index < 0)
                {
                    return null;
                }
                used.Add(available[index].Slot);
                available.RemoveAt(index);
            }
            return used;
        }

        /// <summary>The goids a recipe still lacks (each missing one listed once per missing copy).</summary>
        public static List<UInt32> MissingIngredients(Recipe recipe, IEnumerable<InventoryItem> inventory)
        {
            List<UInt32> available = new List<UInt32>();
            foreach (InventoryItem item in inventory)
            {
                if (item.Slot < 97)
                {
                    available.Add(item.Goid);
                }
            }

            List<UInt32> missing = new List<UInt32>();
            foreach (UInt32 ingredient in recipe.Ingredients)
            {
                if (!available.Remove(ingredient))
                {
                    missing.Add(ingredient);
                }
            }
            return missing;
        }
    }
}
