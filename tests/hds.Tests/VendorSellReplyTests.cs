using hds;

namespace hds.Tests;

// Byte-for-byte checks against a retail vendor sell reply: FM-13SK (GoID 43060) from slot 88 for
// 5000, new cash 8,053,143. Source: research/packet-dumps/community-intake/
// 20260606T202620Z-hdneo-mxopackets1-sell-inventory-selected/logs/
// crashover2k_afterwhoruneo_old_world_packets_second_pc_others_sell_item_FM13sk_saikungoperative.log.txt
public class VendorSellReplyTests
{
    private static byte[] Hex(string hex)
    {
        return Convert.FromHexString(hex.Replace(" ", ""));
    }

    [Fact]
    public void InfoDeltaMatchesRetail()
    {
        Assert.Equal(Hex("80 e7 88 13 00 00 00 00 00 00 07 00 00 00"),
            ServerPackets.BuildInfoDelta(5000, ServerPackets.InfoReasonVendorSell));
    }

    [Fact]
    public void InfoDeltaEncodesSpendingAsNegative()
    {
        // Retail buy replies carry -100 as a two's-complement i64 with reason 8.
        Assert.Equal(Hex("80 e7 9c ff ff ff ff ff ff ff 08 00 00 00"),
            ServerPackets.BuildInfoDelta(-100, ServerPackets.InfoReasonVendorBuy));
    }

    [Fact]
    public void InfoCurrentMatchesRetail()
    {
        Assert.Equal(Hex("80 e4 97 e1 7a 00 07 00 00 00"),
            ServerPackets.BuildInfoCurrent(8053143, ServerPackets.InfoReasonVendorSell));
    }

    [Fact]
    public void InventoryItemDeleteMatchesRetail()
    {
        Assert.Equal(Hex("5f 58 00 01"), ServerPackets.BuildInventoryItemDelete(88));
    }

    [Fact]
    public void VendorSellAckMatchesRetail()
    {
        Assert.Equal(Hex("81 12 00 34 a8 00 00 00 00 00 34 88 13 00 00"),
            ServerPackets.BuildVendorSellAck(43060, new byte[] {0x00, 0x00, 0x00, 0x34}, 5000));
    }
}
