using hds;
using hds.shared;

namespace hds.Tests;

public class MissionTests
{
    private static string Hex(byte[] bytes) => StringUtils.bytesToString_NS(bytes).ToLowerInvariant();

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
    public void ExperienceGainCarriesTheGainNotTheTotal()
    {
        // Retail after a 391-EXP kill: 80 e6 87 01 00 00 01 00 00 00
        Assert.Equal("80e6" + "87010000" + "01000000", Hex(ServerPackets.BuildEXPGain(391)));
    }

    [Fact]
    public void MissionCompleteMatchesRetail()
    {
        // Retail end of Welcome to the Matrix (luizoe Recursion, 19:50:35): 1 kill, 120 s, 21,000 info, 5,500 XP, intro state 1
        byte[] message = ServerPackets.BuildMissionComplete(5500, 21000, 1, 120);
        Assert.Equal(26, message.Length);
        Assert.Equal("80a2" + "00" + "0100" + "0000" + "78000000" + "08520000" + "7c150000" + "00000000000001", Hex(message));
    }

    [Fact]
    public void MissionExperienceUsesTheMissionGainType()
    {
        Assert.Equal("80e6" + "f4010000" + "02000000", Hex(ServerPackets.BuildEXPGain(500, ServerPackets.ExpGainMission)));
    }

    [Fact]
    public void MissionItemCarriesTheKeyOfTheNpcItIsFor()
    {
        // Retail, The Internuncio, the statue for Gagarin (key 01 9a 02 02)
        Assert.Equal("5e" + "0700" + "f2040000" + "019a0282" + "01",
            Hex(ServerPackets.BuildMissionItemAdd(7, 1266, 1, 0x029a)));
    }

    [Fact]
    public void WaypointMatchesRetail()
    {
        // Retail: 80 9e 00 <27800 f64> <8945 f64> <-4600 f64> 01 00 00 00 22 01 80 07
        Assert.Equal("809e00" + "000000000026db40" + "000000008078c140" + "0000000000f8b1c0" + "0100000022018007",
            Hex(ServerPackets.BuildMissionWaypoint(27800, 8945, -4600)));
    }

    [Fact]
    public void WaypointNamesItsDoor()
    {
        // Retail, Exile Extermination, second door: 80 9e 01 ... 01 00 00 00 53 00 60 53
        Assert.Equal("809e01" + "0000000080b4e640" + "0000000000c2a040" + "0000000000880340".Substring(0, 0) + "0000000000881340".Substring(0,0) +
                     "000000000088d340" + "0100000053006053",
            Hex(ServerPackets.BuildMissionWaypoint(46500, 2145, 20000, 1, "53006053")));
    }

    [Fact]
    public void OperatorLineFromTheClientsStringTable()
    {
        // Retail: 80 a1 70 0a 00 28 14 00 00 26 14 00 0c 00 00 26 17 00 01 01 01 00 00 01 00 00
        Assert.Equal("80a1" + "700a0028" + "14000026" + "1400" + "0c000026" + "1700" + "01" + "01" + "010000" + "010000",
            Hex(ServerPackets.BuildDialog(0x28000a70, 0x26000014, 0x2600000c, 1, 1, "", "")));
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
