using hds;

namespace hds.Tests;

// Byte-for-byte checks against a retail vendor buy reply: a 30,000 weapon (GoID 0x09c1) bought into
// slot 0. Source: research/packet-dumps/**/crashover2k_trinitys_proxy_second_pc_008_buyed_weapons_
// for_30000_info_per_weapon_all_place_to_first_slot_every_row_4_times_same_weapon.log.txt
public class VendorBuyReplyTests
{
    private static byte[] Hex(string hex)
    {
        return Convert.FromHexString(hex.Replace(" ", ""));
    }

    [Fact]
    public void InventoryItemAddMatchesRetail()
    {
        Assert.Equal(Hex("5e 00 00 c1 09 00 00 00 00 00 10 01"),
            ServerPackets.BuildInventoryItemAdd(0, 0x09c1, 0, 0x10));
    }

    [Fact]
    public void BuyCashDeltaMatchesRetail()
    {
        Assert.Equal(Hex("80 e7 d0 8a ff ff ff ff ff ff 08 00 00 00"),
            ServerPackets.BuildInfoDelta(-30000, ServerPackets.InfoReasonVendorBuy));
    }

    [Fact]
    public void BuyCashCurrentMatchesRetail()
    {
        // "80 e4 cc 9c 52 ee 08 00 00 00" in the same capture.
        Assert.Equal(Hex("80 e4 cc 9c 52 ee 08 00 00 00"),
            ServerPackets.BuildInfoCurrent(0xee529ccc, ServerPackets.InfoReasonVendorBuy));
    }

    [Fact]
    public void VendorBuyAckMatchesRetail()
    {
        Assert.Equal(Hex("81 0f c1 09 00 00 00 00 00 10 30 75 00 00 00"),
            ServerPackets.BuildVendorBuyAck(0x09c1, ServerPackets.DefaultItemDescriptor, 30000));
    }
}
