using System.Xml;
using hds;
using hds.shared;

namespace hds.Tests;

public class MissionTests
{
    private static string Hex(byte[] bytes) => StringUtils.bytesToString_NS(bytes).ToLowerInvariant();

    [Fact]
    public void LoadsTheCheckedInMissionFormat()
    {
        // data/missions/*.xml use <config><data ...><objectiveN .../>; nothing loaded them before.
        XmlDocument doc = new XmlDocument();
        doc.LoadXml(@"<config><data title=""Welcome in Matrix"" exp=""500"" info=""100"" description=""Just for test"">
            <total objective=""2"" />
            <objective2 description=""Defeat Andromeda."" command=""DEFEAT"" dial=""Kill him!"" />
            <objective1 description=""Talk to Denise."" command=""TALK"" dial=""Hi"" />
            <npc1 type=""FRIENDLY"" />
          </data></config>");

        Mission mission = DataLoader.ParseConfigMission(doc.SelectSingleNode("config/data"));

        Assert.Equal("Welcome in Matrix", mission.title);
        Assert.Equal("Just for test", mission.description);
        Assert.Equal(500, mission.exp);
        Assert.Equal(100, mission.info);
        Assert.Collection(mission.phases[0].missionObjectives,
            o => Assert.Equal("Talk to Denise.", o.description),
            o => Assert.Equal("Defeat Andromeda.", o.description));
    }

    [Fact]
    public void MissionNameReproducesTheRetailLayout()
    {
        // The retail Assassination 809c (txn 0x0008, instance 0x2f), which the old code sent verbatim.
        string expected = "809c" + "00000000" + "0800" +
                          "2b0001002f0002d0070000310000b4c00c002814000026700a00280100003e003b00000e00" +
                          "417373617373696e6174696f6e00" + "010000" + "0300aa0600009c070000dc050000";
        Assert.Equal(expected, Hex(ServerPackets.BuildMissionSetName(8, 0x2f, "Assassination")));
    }

    [Fact]
    public void MissionNameOffsetsFollowTheTitleLength()
    {
        byte[] message = ServerPackets.BuildMissionSetName(1, 0x30, "Welcome in Matrix");
        // list offset, empty-string offset at bytes 38..41 (from the start of the message, header included)
        Assert.Equal(0x2b + 2 + 18 + 3, message[38] | (message[39] << 8));
        Assert.Equal(0x2b + 2 + 18, message[40] | (message[41] << 8));
        // the u32 list really starts at the list offset
        Assert.Equal(0x03, message[0x2b + 2 + 18 + 3]);
    }

    [Fact]
    public void BriefingDeclaresItsRealLength()
    {
        byte[] briefing = ServerPackets.BuildMissionBriefing("Bring the disk back.");
        Assert.Equal(0x30, briefing[2]);                 // text offset
        int size = briefing[0x30] | (briefing[0x31] << 8);
        Assert.Equal("Bring the disk back.".Length + 1, size);
        Assert.Equal(0x30 + 2 + size, briefing.Length);  // nothing missing, nothing extra
    }

    [Fact]
    public void MissionKeyUpdateMatchesRetail()
    {
        Assert.Equal("02000280400" + "02f0002", Hex(ServerPackets.BuildMissionKeyUpdate(0x2f)));
    }
}
