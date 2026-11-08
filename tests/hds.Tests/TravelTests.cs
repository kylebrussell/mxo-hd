using hds;
using hds.resources.gameobjects;
using hds.shared;

namespace hds.Tests;

// Expected bytes from retail captures (research/packet-dumps/community-intake).
public class TravelTests
{
    private static string Hex(byte[] bytes) => StringUtils.bytesToString_NS(bytes).ToLowerInvariant();

    [Fact]
    public void BoothCreationMatchesRetail()
    {
        // RajkoPacketsNearTheEnd_PacketsSpawnedInMaraCThenExitedViaHardline packet 52:
        // 08 50 02 ea 01 30 39 da cd ab 01 01 <17000.0, 530.0, 2200.0 as doubles>
        // Our static data has this booth (slums 1/915, mxoId ea013039) at y 545; retail spawned it 15 lower.
        StaticWorldObject booth = new StaticWorldObject
        {
            metrId = 1, mxoStaticId = 0x393001ea, pos_x = 17000.0, pos_y = 545.0, pos_z = 2200.0,
            type = new byte[] { 0x50, 0x02 }
        };

        byte[] creation = new ObjectManager().GenerateCreationPacket(HardlineBooths.BuildBoothObject(booth), 0, 0xda).getBytes();

        Assert.Equal("085002" + "ea013039" + "da" + "cdab" + "0101" +
                     "00000000009ad040" + "0000000000908040" + "000000000030a140", Hex(creation));
    }

    [Fact]
    public void ExitPromptsMatchRetail()
    {
        // proxy_second_pc_001_exit_to_loadingarea: 80 c7 0d 00 0d 00 05 00 -> 80 fb 4c 00 00 00 01
        Assert.Equal("80fb" + "4c000000" + "01", Hex(HardlineBooths.BuildExitPrompt(0x4c, true)));
        // PacketsSpawnedInMaraCThenExitedViaHardline: 80 c7 31 00 da 00 03 00 -> 80 fb 98 00 00 00 00 (MaraCentral, 152)
        Assert.Equal("80fb" + "98000000" + "00", Hex(HardlineBooths.BuildExitPrompt(152, false)));
    }

    [Fact]
    public void UploadAnswerMatchesRetail()
    {
        // crashover2k_008_some_pvp_and_teleports: 80 c8 04 00 a0 1b 04 00 -> 81 18 00 00 0d 00 00 98
        Assert.Equal("8118" + "00000d000098", Hex(HardlineBooths.BuildUploadDone()));
    }

    [Fact]
    public void OnlyPhoneBoothsAreHardlineBooths()
    {
        Assert.True(HardlineBooths.IsBooth(592));
        Assert.False(HardlineBooths.IsBooth(6568)); // subway car
        Assert.False(HardlineBooths.IsBooth(12));   // player
    }

    [Fact]
    public void ClearingViewsKeepsViewIdsCountingUp()
    {
        ViewManager views = new ViewManager();
        ClientView first = views.GetViewForEntityAndGo(100, 599);
        first.viewCreated = true;
        views.GetViewForEntityAndGo(101, 599);

        views.Clear();

        Assert.Equal(0, views.Count);
        ClientView again = views.GetViewForEntityAndGo(100, 599);
        Assert.False(again.viewCreated);            // the NPC gets spawned again after the world reload
        Assert.True(again.ViewID > first.ViewID + 1); // and gets a fresh view id
    }
}
