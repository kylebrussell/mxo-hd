using hds;
using hds.shared;

namespace hds.Tests;

public class NpcLootTests
{
    private static string Hex(byte[] bytes) => StringUtils.bytesToString_NS(bytes).ToLowerInvariant();

    [Fact]
    public void LootWindowMatchesRetailLayout()
    {
        // Retail: 81 19 00 00 00 00 10 00 f4 3d 00 00 00 00 00 00 03 00
        //         00 00 00 00 a7 1c 00 00 | 00 00 00 00 51 b8 00 00 | 00 00 00 00 c9 09 00 00
        Assert.Equal("811900000000100" + "0f43d0000" + "00000000" + "0300" +
                     "00000000a71c0000" + "0000000051b80000" + "00000000c9090000",
            Hex(NpcLoot.BuildLootWindow(0x3df4, new uint[] { 0x1ca7, 0xb851, 0x09c9 }, new byte[] { 0, 0, 0 })));

        // No items: 81 19 00 00 00 00 10 00 0c 03 00 00 00 00 00 00 00 00
        Assert.Equal("81190000000010000c03000000000000" + "0000",
            Hex(NpcLoot.BuildLootWindow(0x030c, new uint[0], new byte[0])));
    }

    [Fact]
    public void CodeBitTierFollowsLevel()
    {
        Assert.Equal(7332u, NpcLoot.CodeBitFor(5));   // Code Bit 1
        Assert.Equal(7333u, NpcLoot.CodeBitFor(15));  // Code Bit 2
        Assert.Equal(7339u, NpcLoot.CodeBitFor(99));  // Code Bit 8 at most
    }

    [Fact]
    public void DropsAreAtMostThreeKnownItems()
    {
        var random = new System.Random(7);
        for (int i = 0; i < 200; i++)
        {
            var items = NpcLoot.Roll(25, random);
            Assert.InRange(items.Count, 0, 3);
            Assert.All(items, g => Assert.True(g == NpcLoot.HealthPill1 || (g >= 7332 && g <= 7339)));
        }
    }
}
