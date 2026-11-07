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
    public void LoadsMissionNpcsInWorldUnits()
    {
        XmlDocument doc = new XmlDocument();
        doc.LoadXml(@"<config><data title=""T"">
            <objective1 description=""Talk to Denise."" idNpc=""5"" command=""TALK"" dial=""Hi %"" />
            <npc1 type=""FRIENDLY"" x=""296"" y=""5"" z=""-25"" idNpc=""5"" handle=""Denise"" rsi=""86010058"" level=""60"" />
            <npc2 type=""HOSTILE"" x=""296"" y=""5"" z=""-22"" idNpc=""6"" handle=""Andromeda"" rsi=""13050008"" level=""5"" maxHP=""500"" />
          </data></config>");

        Mission mission = DataLoader.ParseConfigMission(doc.SelectSingleNode("config/data"));

        Assert.Equal(5, mission.phases[0].missionObjectives[0].npcId);
        Assert.Collection(mission.npcs,
            n =>
            {
                Assert.Equal("Denise", n.handle);
                Assert.True(n.friendly);
                Assert.Equal(29600, n.x);
                Assert.Equal(500, n.y);
                Assert.Equal(-2500, n.z);
                Assert.Equal(60, n.level);
            },
            n =>
            {
                Assert.Equal("Andromeda", n.handle);
                Assert.False(n.friendly);
                Assert.Equal(6, n.id);
                Assert.Equal(500, n.maxHealth);
            });
    }

    [Fact]
    public void ExperienceGainCarriesTheGainNotTheTotal()
    {
        // Retail after a 391-EXP kill: 80 e6 87 01 00 00 01 00 00 00
        Assert.Equal("80e6" + "87010000" + "01000000", Hex(ServerPackets.BuildEXPGain(391)));
    }

    [Fact]
    public void WaypointMatchesRetail()
    {
        // Retail: 80 9e 00 <27800 f64> <8945 f64> <-4600 f64> 01 00 00 00 22 01 80 07
        Assert.Equal("809e00" + "000000000026db40" + "000000008078c140" + "0000000000f8b1c0" + "0100000022018007",
            Hex(ServerPackets.BuildMissionWaypoint(27800, 8945, -4600)));
    }

    [Fact]
    public void NpcDialogMatchesRetail()
    {
        // Retail "Armor" dialog header: 80 a1 1f 12 00 28 00 00 00 00 14 00 00 00 00 00 1c 00 01 00 06 00 Armor 00 <sized text>
        Assert.Equal("80a1" + "1f120028" + "00000000" + "1400" + "0000" + "0000" + "1c00" + "0100" +
                     "0600" + "41726d6f7200" + "0300" + "486900",
            Hex(ServerPackets.BuildNpcDialog("Armor", "Hi")));
    }

    [Fact]
    public void MissionKeyUpdateMatchesRetail()
    {
        Assert.Equal("02000280400" + "02f0002", Hex(ServerPackets.BuildMissionKeyUpdate(0x2f)));
    }
}
