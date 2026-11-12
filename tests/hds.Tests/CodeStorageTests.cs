using System.Collections.Generic;
using System.Linq;
using hds;
using hds.shared;

namespace hds.Tests;

public class CodeStorageTests
{
    private static string Hex(byte[] bytes) => StringUtils.bytesToString_NS(bytes).ToLowerInvariant();

    private static Crafting.InventoryItem Item(ushort slot, uint goid, int count = 1) => new Crafting.InventoryItem(slot, goid, count);

    private static CodeStorage.Entry Stored(ushort slot, uint goid, int count) => new CodeStorage.Entry(slot, goid, count);

    [Fact]
    public void PackedValuesMatchTheKnownAbilitiesList()
    {
        // Trinity's login (proxy_second_pc_011): 24 00 85 53 0e 84 = slot 0x24, Code Bit 4 (7335) x6;
        // 01 00 11 c4 01 80 = slot 1, an ability (0x80 flag, no 0x04).
        Assert.True(CodeStorage.IsCode(0x840e5385));
        Assert.Equal(7335u, CodeStorage.GoidOf(0x840e5385));
        Assert.Equal(6, CodeStorage.CountOf(0x840e5385));
        Assert.False(CodeStorage.IsCode(0x8001c411));
        // 00 cd 04 84: ClassFrag 2458, one.
        Assert.Equal(2458u, CodeStorage.GoidOf(0x8404cd00));
        Assert.Equal(1, CodeStorage.CountOf(0x8404cd00));

        Assert.Equal(0x840e5385u, CodeStorage.Pack(7335, 6));
        Assert.Equal(unchecked((int)0x840e5380), CodeStorage.BaseValue(7335));
    }

    [Fact]
    public void UpdateMatchesRetailUploads()
    {
        // crashover2k uploadcodebits 235: 69 00 bd 01 0f 53 0e 84 00 00 (Code Bit 3, 16 stored)
        Assert.Equal("6900bd010f530e840000", Hex(CodeStorage.BuildUpdate(0x1bd, 7334, 16)));
        // abilitychangecoder 61114 / 62075: Code Bit 4 in slot 0x1e4 grew by one.
        Assert.Equal("6900e4018e530e840000", Hex(CodeStorage.BuildUpdate(0x1e4, 7335, 15)));
        Assert.Equal("6900e4018f530e840000", Hex(CodeStorage.BuildUpdate(0x1e4, 7335, 16)));
    }

    [Theory]
    [InlineData(0, "5f000000")]
    [InlineData(4, "5f040000")]
    [InlineData(0x1c3, "5fc30100")]
    [InlineData(0x207, "5f070200")]
    public void RemoveTargetsStorageRatherThanTheSameBagSlot(int slot, string expected)
    {
        Assert.Equal(expected, Hex(CodeStorage.BuildRemove((ushort)slot)));
        Assert.Equal((byte)1, ServerPackets.BuildInventoryItemDelete((ushort)slot)[3]);
    }

    [Fact]
    public void MatchConsumesExactStorageCountAndThenReportsMissing()
    {
        var recipe = new Crafting.Recipe { ResultGoid = 7458, Ingredients = new uint[] { 7336, 7336 } };
        var exact = CodeStorageHandler.AsIngredients(new[] { Stored(0x1c3, 7336, 2) });
        Assert.Equal(new ushort[] { 0x81c3, 0x81c3 }, Crafting.MatchIngredients(recipe, exact)!.ToArray());
        Assert.Null(Crafting.MatchIngredients(recipe,
            CodeStorageHandler.AsIngredients(new[] { Stored(0x1c3, 7336, 1) })));
        Assert.Null(Crafting.MatchIngredients(recipe, CodeStorageHandler.AsIngredients(Array.Empty<CodeStorage.Entry>())));
    }

    [Fact]
    public void AddMatchesRetailNewKind()
    {
        // luizoe_recursion2 1936: 5e 82 00 00 00 00 00 13 c3 04 84 00 (ClassFrag 2438, 20)
        Assert.Equal("5e820000000000" + "13c3048400", Hex(CodeStorage.BuildAdd(0x82, 2438, 20)));
        // 5e 89 00 00 00 00 00 02 c9 04 84 00 (2450, 3)
        Assert.Equal("5e890000000000" + "02c9048400", Hex(CodeStorage.BuildAdd(0x89, 2450, 3)));
    }

    [Fact]
    public void UploadMessageMatchesRetailBytes()
    {
        const string retail = "2e07" + "0000000000000000000000" + "6400002e002400" +
                              "00000000000000000000000000000000" + "0e00" + "416674657257686f72754e656f00";
        Assert.Equal(retail, Hex(CodeStorage.BuildUploadMessage("AfterWhoruNeo")));
    }

    [Fact]
    public void UploadPlanReproducesRetailSecondUpload()
    {
        // abilitychangecoder 62075: bag slot 0x28 (Code Bit 8) and 0x53 (Code Bit 4, just looted) uploaded,
        // ClassFrag 2436 in 0x4a stayed in retail (see docs); a gun and a worn Code Bit never move.
        var bag = new[] { Item(0x28, 7339), Item(0x53, 7335), Item(0x30, 2565), Item(100, 7336) };
        var stored = new[] { Stored(0x192, 7339, 19), Stored(0x1e4, 7335, 15) };

        CodeStorage.UploadPlan plan = CodeStorage.PlanUpload(bag, stored, 0x205);

        Assert.Equal(new ushort[] { 0x28, 0x53 }, plan.ClearedBagSlots.ToArray());
        Assert.Empty(plan.ShrunkBagSlots);
        Assert.Empty(plan.Added);
        Assert.Equal(2, plan.ItemsMoved);
        Assert.Equal(new[] { "690092019355" + "0e840000", "6900e4018f53" + "0e840000" },
            plan.Updated.Select(e => Hex(CodeStorage.BuildUpdate(e.Slot, e.Goid, e.Count))).ToArray());
    }

    [Fact]
    public void UploadPlanMakesNewEntriesAndStacksMove()
    {
        var bag = new[] { Item(0x44, 2438, 20), Item(0x45, 7336, 3), Item(0x46, 7336, 2) };

        CodeStorage.UploadPlan plan = CodeStorage.PlanUpload(bag, new CodeStorage.Entry[0], 0x82);

        Assert.Equal(new ushort[] { 0x44, 0x45, 0x46 }, plan.ClearedBagSlots.ToArray());
        Assert.Equal(25, plan.ItemsMoved);
        Assert.Equal(2, plan.Added.Count);
        Assert.Equal((ushort)0x82, plan.Added[0].Slot);
        Assert.Equal(2438u, plan.Added[0].Goid);
        Assert.Equal(20, plan.Added[0].Count);
        Assert.Equal((ushort)0x83, plan.Added[1].Slot);
        Assert.Equal(7336u, plan.Added[1].Goid);
        Assert.Equal(5, plan.Added[1].Count);
    }

    [Fact]
    public void FullStorageLeavesTheRestInTheBag()
    {
        var plan = CodeStorage.PlanUpload(new[] { Item(0x28, 7339, 5) }, new[] { Stored(0x192, 7339, 127) }, 0x200);

        Assert.Empty(plan.ClearedBagSlots);
        Assert.Equal(new[] { new KeyValuePair<ushort, int>(0x28, 4) }, plan.ShrunkBagSlots.ToArray());
        Assert.Equal(128, plan.Updated.Single().Count);

        var none = CodeStorage.PlanUpload(new[] { Item(0x28, 7339, 5) }, new[] { Stored(0x192, 7339, 128) }, 0x200);
        Assert.Equal(0, none.ItemsMoved);
        Assert.Empty(none.Updated);
    }

    [Fact]
    public void CompileTakesFromTheBagFirstThenStorage()
    {
        var recipe = new Crafting.Recipe { ResultGoid = 7458, Ingredients = new uint[] { 7336, 7336 } };
        var items = new List<Crafting.InventoryItem> { Item(0x20, 7336) };
        items.AddRange(CodeStorageHandler.AsIngredients(new[] { Stored(0x1c3, 7336, 3) }));

        Assert.Equal(new ushort[] { 0x20, (ushort)(Crafting.StorageSlotFlag | 0x1c3) },
            Crafting.MatchIngredients(recipe, items)!.ToArray());
        // Storage alone pays as well; worn items still never do.
        Assert.Equal(new ushort[] { 0x81c3, 0x81c3 },
            Crafting.MatchIngredients(recipe, CodeStorageHandler.AsIngredients(new[] { Stored(0x1c3, 7336, 3) }))!.ToArray());
        Assert.Null(Crafting.MatchIngredients(recipe, new[] { Item(100, 7336, 2) }));
    }
}
