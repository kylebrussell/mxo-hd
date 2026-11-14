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
    public void LootReplyMatchesRetailClaimedIdentityCapture()
    {
        // Retail claimed identity list. Client621e2bb0 removes these exact pairs; it is not a remaining-items list.
        Assert.Equal("81200c000000000000000000020000000028d61e000000000000a41c0000",
            Hex(ServerPackets.BuildLootAccepted(new[]
            {
                (new byte[] { 0, 0, 0, 0x28 }, 7894u),
                (new byte[4], 7332u)
            })));
        Assert.Equal("81200c0000000000000000000000",
            Hex(ServerPackets.BuildLootAccepted(Array.Empty<(byte[], uint)>())));
    }

    [Fact]
    public void PartialClaimListsTransferredIdentitiesAndRetainsOnlyFailuresForRetry()
    {
        var items = new List<uint> { 7332, 2399, 7332 };
        int capacity = 1;
        var claimed = NpcLoot.ClaimItems(items, _ => capacity-- > 0);
        Assert.Equal(7332u, Assert.Single(claimed).Item);
        Assert.Equal(new byte[4], claimed[0].Data);
        Assert.Equal(new uint[] { 2399, 7332 }, items);
        Assert.Empty(NpcLoot.ClaimItems(items, _ => false));
        var retry = NpcLoot.ClaimItems(items, _ => true);
        Assert.Equal(new uint[] { 2399, 7332 }, retry.Select(c => c.Item));
        Assert.Empty(items);
        Assert.Empty(NpcLoot.ClaimItems(items, _ => throw new Exception("Duplicate grant")));
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
