using System;
using System.Linq;
using hds;
using hds.shared;

namespace hds.Tests;

public class ItemStackTests
{
    private static string Hex(byte[] bytes) => StringUtils.bytesToString_NS(bytes).ToLowerInvariant();

    private static Crafting.InventoryItem Item(ushort slot, uint goid, int count = 1) => new Crafting.InventoryItem(slot, goid, count);

    [Fact]
    public void AmountByteMatchesRetailStacks()
    {
        // Marketplace stacks (luizoe_recursion1/2): 20 Code Bit 8 = 4c (sold for 20,000), 9 = 20 (9,000), 4 = 0c (4,000).
        Assert.Equal(0x4c, ItemStacks.AmountByte(20));
        Assert.Equal(0x20, ItemStacks.AmountByte(9));
        Assert.Equal(0x0c, ItemStacks.AmountByte(4));
        Assert.Equal(0x00, ItemStacks.AmountByte(1));
        Assert.Equal(0x00, ItemStacks.AmountByte(0)); // older rows have count 0 = one item
        Assert.Equal(0xfc, ItemStacks.AmountByte(ItemStacks.MaxStack));

        // 5e with that amount: 5e 44 00 ab 1c 00 00 00 00 4c 00 01
        Assert.Equal("5e4400ab1c000000004c0001", Hex(ServerPackets.BuildInventoryItemAdd(0x44, 7339, ItemStacks.AmountByte(20), 0)));
    }

    [Fact]
    public void StackUpdateMatchesRetail()
    {
        // Icy Snowballs bought one at a time into slot 0x36: 69 01 36 00 00 00 04 00 00 00 for the second, 08 for the third.
        Assert.Equal("6901360000000400000" + "0", Hex(ItemStacks.BuildStackCount(0x36, 2)));
        Assert.Equal("6901360000000800000" + "0", Hex(ItemStacks.BuildStackCount(0x36, 3)));
        // Cookie stack after one was used (PacketsZombiesPortingEtc 10519): 69 01 04 00 00 00 10 00 00 00 = 5 left.
        Assert.Equal("6901040000001000000" + "0", Hex(ItemStacks.BuildStackCount(0x04, 5)));
    }

    [Fact]
    public void CodeBitsFragmentsAndPillsStack()
    {
        Assert.True(ItemStacks.IsStackable(7332));  // Code Bit 1
        Assert.True(ItemStacks.IsStackable(7339));  // Code Bit 8
        Assert.True(ItemStacks.IsStackable(2435));  // ClassFrag_Item_T01
        Assert.True(ItemStacks.IsStackable(7458));  // FuncFrag_Pill_F02
        Assert.True(ItemStacks.IsStackable(9154));  // VariableFrag_R16
        Assert.True(ItemStacks.IsStackable(2399));  // Health Pill 1.0
        Assert.False(ItemStacks.IsStackable(2565)); // Redpill Special (a gun)
        Assert.False(ItemStacks.IsStackable(1271)); // Code Compiler Tool
    }

    [Fact]
    public void IngredientsCanComeFromOneStack()
    {
        var recipe = new Crafting.Recipe { ResultGoid = 7458, Ingredients = new uint[] { 7336, 7336 } };

        // A stack of two Code Bit 5 pays for both.
        Assert.Equal(new ushort[] { 4, 4 }, Crafting.MatchIngredients(recipe, new[] { Item(4, 7336, 2) })!.ToArray());
        // A stack of one does not.
        Assert.Null(Crafting.MatchIngredients(recipe, new[] { Item(4, 7336, 1) }));
        Assert.Equal(new uint[] { 7336 }, Crafting.MissingIngredients(recipe, new[] { Item(4, 7336, 1) }));
        // The lowest slot first, then the next.
        Assert.Equal(new ushort[] { 2, 9 }, Crafting.MatchIngredients(recipe, new[] { Item(9, 7336, 3), Item(2, 7336, 1) })!.ToArray());
    }

    [Fact]
    public void KnownRecipesKeepTheMarginListAndAddCodeBitFragments()
    {
        byte[] baseBlock = MarginClient.KnownRecipesBaseBlock;
        Assert.Equal(4u, BitConverter.ToUInt32(baseBlock, 0));
        Assert.Equal(425, baseBlock.Length / 4 - 1);

        byte[] block = Crafting.BuildKnownRecipesBlock(baseBlock);
        var goids = Enumerable.Range(1, block.Length / 4 - 1).Select(i => BitConverter.ToUInt32(block, i * 4)).ToList();
        Assert.Equal(4u, BitConverter.ToUInt32(block, 0));
        Assert.Equal(goids.Count, goids.Distinct().Count());
        Assert.Contains(0x1a7u, goids);   // first entry of the old list (DataNodeLocatorGoggles)
        Assert.Contains(7458u, goids);    // FuncFrag_Pill_F02 (Code Bit 5 + Code Bit 5)
        Assert.Contains(2435u, goids);    // ClassFrag_Item_T01 (Code Bit 1 + Code Bit 2)
        Assert.Contains(2399u, goids);    // Health Pill 1.0
        Assert.Equal(47, Crafting.ExtraKnownRecipes().Count(g => g != 2399 && g != 6897 && g != 6924 && g != 6925 && g != 7913));
    }

    [Fact]
    public void CompileRequestScanFindsAKnownRecipeGoid()
    {
        // An 80 b9 body with ability 969 and a recipe goid somewhere in its middle (hypothetical layout).
        byte[] body = new byte[41];
        BitConverter.GetBytes((ushort)969).CopyTo(body, 0);
        BitConverter.GetBytes(7458u).CopyTo(body, 22);
        body[34] = 0x08; body[35] = 0x6b;
        Assert.Equal(7458u, CraftingHandler.FindKnownRecipe(body, new System.Collections.Generic.HashSet<uint> { 2399, 7458 }));
        Assert.Null(CraftingHandler.FindKnownRecipe(new byte[41], new System.Collections.Generic.HashSet<uint> { 2399 }));
    }
}
