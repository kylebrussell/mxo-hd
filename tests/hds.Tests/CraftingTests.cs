using System.IO;
using System.Linq;
using hds;

namespace hds.Tests;

public class CraftingTests
{
    private static string RecipesPath()
    {
        string dir = AppContext.BaseDirectory;
        while (dir != null && !File.Exists(Path.Combine(dir, "data", "recipes.csv")))
        {
            dir = Path.GetDirectoryName(dir);
        }
        Assert.NotNull(dir);
        return Path.Combine(dir!, "data", "recipes.csv");
    }

    private static Crafting.InventoryItem Item(ushort slot, uint goid) => new Crafting.InventoryItem(slot, goid);

    [Fact]
    public void CookbookExportHasTheClientsRecipes()
    {
        var recipes = Crafting.Parse(File.ReadLines(RecipesPath()));
        Assert.Equal(38663, recipes.Count);

        // Health Pill 1.0 = Class Routine: Item + Function Subroutines Grant and Consumable + two Variable Fragments
        var pill = recipes[2399];
        Assert.Equal(1, pill.Level);
        Assert.Equal("ICP0", pill.Type);
        Assert.Equal(new uint[] { 2435, 2438, 7458, 2472, 2454 }, pill.Ingredients);

        // Fragments come from Code Bits: FuncFrag_Pill_F02 (7458) = Code Bit 5 twice.
        Assert.Equal(new uint[] { 7336, 7336 }, recipes[7458].Ingredients);
    }

    [Fact]
    public void ParseReadsTheCsvColumns()
    {
        var recipes = Crafting.Parse(new[]
        {
            "# comment",
            "ResultGOID;Level;Type;FieldA;FieldB;Ingredients",
            "2475;3;FCV0;540000d7;7a000122;7335 7338 7338",
        });
        var fragment = recipes[2475];
        Assert.Equal(3, fragment.Level);
        Assert.Equal("FCV0", fragment.Type);
        Assert.Equal(new uint[] { 7335, 7338, 7338 }, fragment.Ingredients);
    }

    [Fact]
    public void IngredientsComeFromSeparateBagSlots()
    {
        var recipe = new Crafting.Recipe { ResultGoid = 7458, Ingredients = new uint[] { 7336, 7336 } };

        // One Code Bit 5 is not enough for a recipe that needs two.
        Assert.Null(Crafting.MatchIngredients(recipe, new[] { Item(4, 7336), Item(5, 7332) }));
        Assert.Equal(new uint[] { 7336 }, Crafting.MissingIngredients(recipe, new[] { Item(4, 7336) }));

        // Two of them: the lowest slots are used; worn items (slot 97 and up) never are.
        var slots = Crafting.MatchIngredients(recipe, new[] { Item(9, 7336), Item(98, 7336), Item(2, 7336), Item(4, 7336) });
        Assert.Equal(new ushort[] { 2, 4 }, slots!.ToArray());
    }
}
