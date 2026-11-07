using hds;
using hds.shared;

namespace hds.Tests;

public class EquipPacketTests
{
    [Fact]
    public void EquippedItemUpdateMatchesRetail()
    {
        // Retail, after "63 04 69" (equip a Revolver0, goid 2565): own-view attribute 31, EquippedItemID.
        Assert.Equal("020002808080800805" + "0a0000",
            StringUtils.bytesToString_NS(ServerPackets.BuildEquippedItemUpdate(2565)).ToLowerInvariant());
    }

    [Theory]
    [InlineData(0x21, "0200028080808080800221")] // retail answer to 3d 01 (weapon drawn)
    [InlineData(0x22, "0200028080808080800222")] // at rest
    public void CombatantModeUpdateMatchesRetail(byte mode, string expected)
    {
        Assert.Equal(expected, StringUtils.bytesToString_NS(ServerPackets.BuildCombatantModeUpdate(mode)).ToLowerInvariant());
    }

    [Fact]
    public void ClearingTheWeaponSendsZero()
    {
        // Retail, after "64 69 4f" (unequip to bag slot 0x4f).
        Assert.Equal("02000280808080080000" + "0000",
            StringUtils.bytesToString_NS(ServerPackets.BuildEquippedItemUpdate(0)).ToLowerInvariant());
    }
}
