using hds;
using hds.shared;

namespace hds.Tests;

/// <summary>The tutorial's records against the bytes in retail's logs (research/missions/tutorial-wire-spec.md).</summary>
public class TutorialPacketTests
{
    private static string Hex(byte[] bytes) => StringUtils.bytesToString_NS(bytes).ToLowerInvariant();

    [Fact]
    public void Narration()
    {
        Assert.Equal("814575080086", Hex(ServerPackets.BuildNarration(0x86000875)));
    }

    [Fact]
    public void ToggleView()
    {
        Assert.Equal("81020e0001", Hex(ServerPackets.BuildToggleView(0x0e, 1)));
    }

    [Fact]
    public void MissionEvent()
    {
        Assert.Equal("819101000000", Hex(ServerPackets.BuildMissionEvent(1)));
    }

    [Fact]
    public void LocalFxWithAToken()
    {
        Assert.Equal("81421d0000228a130000" + "00000000", Hex(ServerPackets.BuildLocalFx(0x2200001d, 5002)));
    }

    [Fact]
    public void ThePillChoice()
    {
        Assert.Equal("818f" + "00000000" + "15000088" + "1c00" + "03000000" + "050000c2" + "010000c2" + "00000000" + "010000",
            Hex(ServerPackets.BuildModalMessage(0, 0x88000015, 3, 0xc2000005, 0xc2000001)));
    }

    [Fact]
    public void BroadcastOfAClientString()
    {
        Assert.Equal("80a1" + "a1060090" + "00000000" + "1400" + "14000088" + "1700" + "01" + "00" + "010000" + "010000",
            Hex(ServerPackets.BuildDialog(0x900006a1, 0, 0x88000014, 1, 0, "", "")));
    }

    [Fact]
    public void TeleportToAMissionElement()
    {
        // Retail, slot 1's Gallery: (-26900, 420, 47300), quaternion (0, -0.7071, 0, 0.7071)
        Assert.Equal("8199" + "000000000045dac0" + "0000000000407a40" + "0000000080" + "18e740" + "00000000" + "f20435bf" + "00000000" + "f304353f",
            Hex(ServerPackets.BuildMissionTeleport(new double[] { -26900, 420, 47300, 0, BitConverter.Int32BitsToSingle(unchecked((int)0xbf3504f2)), 0, BitConverter.Int32BitsToSingle(0x3f3504f3) })));
    }

    [Fact]
    public void TheTutorialsAcceptNamesTemplate986()
    {
        // Retail: 80 9c 00000000 0000 2b00 0000 d701 02 00000000 00000000 00000000 14000026 700a0028 0000ff 3c00 3900 00
        // 0c00 "Tutorial v2" 010000 0100 da030000
        Assert.Equal("809c" + "00000000" + "0000" + "2b00" + "0000" + "d701" + "02" + "00000000" + "00000000" + "00000000" +
                     "14000026" + "700a0028" + "0000ff" + "3c00" + "3900" + "00" +
                     "0c00" + "5475746f7269616c20763200" + "010000" + "0100" + "da030000",
            Hex(ServerPackets.BuildTutorialSetName(0x01d7)));
    }

    [Fact]
    public void EveryGoalHasItsText()
    {
        Assert.Equal(38, TutorialMission.GoalTexts.Length);
        Assert.Equal("Move the camera around by holding down the right mouse button", TutorialMission.GoalTexts[0]);
        Assert.Equal("Exit the tutorial", TutorialMission.GoalTexts[37]);
        Assert.All(TutorialMission.GoalTexts, text => Assert.False(string.IsNullOrEmpty(text)));
    }
}
