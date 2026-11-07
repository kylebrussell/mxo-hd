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
    public void JumpEndTimeIsSimMillisecondsCloseToRetail()
    {
        // Retail hyperjump: packet sim time 87106.438 s, peak height 10300, JumpEndTime 87116339 (9.9 s later).
        uint end = AbilityHandler.JumpEndTime(87106.438f, 10300f);
        Assert.InRange(end, 87_115_000u, 87_117_000u);

        // A plain jump (peak 197) lands about 1.3 s later.
        Assert.InRange(AbilityHandler.JumpEndTime(100f, 197f), 101_200u, 101_350u);
    }

    [Fact]
    public void EmoteUpdateCarriesSimTimeAndNothingAfter()
    {
        // Retail clap: 01 28 06 40 00 25 04 00 00 10 <x y z> b3 a6 5e 18 (and the view list's 00 00 follows).
        byte[] update = ServerPackets.BuildEmoteUpdate(6, 4, 0f, 95f, 0f, 0x185ea6b3);
        Assert.Equal("0200" + "012806400025040000" + "10" + "00000000" + "0000be42" + "00000000" + "b3a65e18",
            StringUtils.bytesToString_NS(update).ToLowerInvariant());
    }

    [Fact]
    public void ClearingTheWeaponSendsZero()
    {
        // Retail, after "64 69 4f" (unequip to bag slot 0x4f).
        Assert.Equal("02000280808080080000" + "0000",
            StringUtils.bytesToString_NS(ServerPackets.BuildEquippedItemUpdate(0)).ToLowerInvariant());
    }
}
