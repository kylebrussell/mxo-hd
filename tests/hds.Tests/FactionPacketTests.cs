using System;
using System.Collections.Generic;
using hds;
using hds.world.Structures;

namespace hds.Tests;

// Golden / structural tests that lock the decoded wire layouts of the faction (7c /
// SERVER_FACTION_PLAYER_INFO mode 1) and crew (80 86 / SERVER_CREW_MEMBERS_LIST) packets.
// These exercise the pure body-builder helpers extracted from FCPackets.SendFactionInfo /
// SendCrewInfo so no live Store/WorldClient is required.
//
// NOTE on fixed-size strings: PacketContent.AddStringWithFixedSized(value, size) emits
// (size + 1) bytes when value is shorter than size (an off-by-one in its padding loop).
// The expectations below encode the actual wire behaviour, not the nominal size.
public class FactionPacketTests
{
    private static string ToHex(byte[] bytes) => Convert.ToHexString(bytes);

    private static string Slice(byte[] bytes, int offset, int length) =>
        Convert.ToHexString(bytes, offset, length);

    // ---- 7c / SERVER_FACTION_PLAYER_INFO (mode 1) ---------------------------------------

    [Fact]
    public void FactionInfoLayoutMatchesDecodedFieldOrder()
    {
        uint charId = 0x11223344;
        uint factionId = 0x0000000A;
        uint masterId = 0x55667788;
        uint money = 0x000003E8; // 1000

        byte[] packet = ServerPackets.BuildFactionInfoPacket(
            charId, factionId, alignment: 1, factionName: "TST",
            masterPlayerCharId: masterId, money: money);

        // opcode 7c (single byte via AddUShort)
        Assert.Equal((byte)0x7C, packet[0]);
        // char id (u32 LE) at offset 1
        Assert.Equal("44332211", Slice(packet, 1, 4));
        // faction id (u32 LE) at offset 5
        Assert.Equal("0A000000", Slice(packet, 5, 4));
        // decoded mode-1 prefix (01 00 00 01) + count/flag (0F 00) at offset 9
        Assert.Equal("010000010F00", Slice(packet, 9, 6));
        // decoded data-length field (52 == 0x34) as u16 LE at offset 15
        Assert.Equal("3400", Slice(packet, 15, 2));
        // alignment single byte at offset 17
        Assert.Equal((byte)0x01, packet[17]);
        // master char id (u32 LE) at offset 61
        Assert.Equal("88776655", Slice(packet, 61, 4));
        // money (u32 LE) at offset 65
        Assert.Equal("E8030000", Slice(packet, 65, 4));
        // total length: 1 + 4 + 4 + 6 + 2 + 1 + 43 (fixed-42 name -> 43 bytes) + 4 + 4
        Assert.Equal(69, packet.Length);
    }

    [Fact]
    public void FactionInfoAlignmentByteIsDynamic()
    {
        byte[] zion = ServerPackets.BuildFactionInfoPacket(1, 1, alignment: 1, factionName: "X",
            masterPlayerCharId: 0, money: 0);
        byte[] mero = ServerPackets.BuildFactionInfoPacket(1, 1, alignment: 3, factionName: "X",
            masterPlayerCharId: 0, money: 0);

        Assert.Equal((byte)0x01, zion[17]); // Zion
        Assert.Equal((byte)0x03, mero[17]); // Mero
    }

    [Fact]
    public void FactionInfoDefaultAlignmentIsZion()
    {
        Assert.Equal((ushort)1, ServerPackets.FactionAlignmentDefault);
    }

    [Fact]
    public void FactionInfoFixedNameFieldIsAlways43Bytes()
    {
        // The decoded layout always reserves the fixed name field regardless of name length,
        // so the master-id/money tail stays at the same offset for any (sub-length) name.
        byte[] shortName = ServerPackets.BuildFactionInfoPacket(1, 1, 1, "AB", 0xAABBCCDD, 0);
        byte[] longerName = ServerPackets.BuildFactionInfoPacket(1, 1, 1, "ABCDEFGHIJ", 0xAABBCCDD, 0);

        Assert.Equal(shortName.Length, longerName.Length);
        // master id lands at the same offset (61) in both
        Assert.Equal("DDCCBBAA", Slice(shortName, 61, 4));
        Assert.Equal("DDCCBBAA", Slice(longerName, 61, 4));
    }

    // ---- 80 86 / SERVER_CREW_MEMBERS_LIST -----------------------------------------------

    private static Crew MakeCrew()
    {
        return new Crew
        {
            crewId = 0x000000AB,
            crewName = "CRW",
            org = 1,
            money = 0x00000064, // 100
        };
    }

    [Fact]
    public void CrewInfoLayoutMatchesDecodedFieldOrder()
    {
        Crew crew = MakeCrew();
        var members = new List<CrewMember>
        {
            new CrewMember { charId = 0x11112222, handle = "Cap", isOnline = 1, isCaptain = true },
        };

        byte[] packet = ServerPackets.BuildCrewInfoPacket(
            charId: 0x33334444, crew: crew, members: members,
            charIdCaptain: 0x11112222, charIdFM: 0);

        // opcode 80 86 (big-endian via AddUint16(...,0))
        Assert.Equal("8086", Slice(packet, 0, 2));
        // char id (u32 LE) at offset 2
        Assert.Equal("44443333", Slice(packet, 2, 4));
        // crew/org id (u32 LE) at offset 6
        Assert.Equal("AB000000", Slice(packet, 6, 4));
        // org reputation byte at offset 10
        Assert.Equal((byte)0x01, packet[10]);
        // crew-name offset u16 (33 == 0x21) at offset 11
        Assert.Equal("2100", Slice(packet, 11, 2));
        // captain char id (u32 LE) at offset 13
        Assert.Equal("22221111", Slice(packet, 13, 4));
        // first-mate char id (u32 LE) at offset 17
        Assert.Equal("00000000", Slice(packet, 17, 4));
        // money (u32 LE) at offset 21
        Assert.Equal("64000000", Slice(packet, 21, 4));
        // member-list offset u16 at offset 25 (= 33 + crewName.Length + 3 = 39 == 0x27)
        Assert.Equal("2700", Slice(packet, 25, 2));
        // decode-confirmed stable constant 14 02 00 00 at offset 27
        Assert.Equal("14020000", Slice(packet, 27, 4));
    }

    [Fact]
    public void CrewInfoConstantFollowsMemberListOffset()
    {
        // Regardless of crew name length, the 14 02 00 00 constant sits immediately after the
        // member-list offset u16, i.e. always at offset 27.
        Crew crew = MakeCrew();
        crew.crewName = "ALongerCrewName";
        var members = new List<CrewMember>
        {
            new CrewMember { charId = 1, handle = "M", isOnline = 0 },
        };

        byte[] packet = ServerPackets.BuildCrewInfoPacket(1, crew, members, 1, 0);
        Assert.Equal("14020000", Slice(packet, 27, 4));
    }

    [Fact]
    public void CrewInfoFullSizeFieldMatchesHeaderPlusMemberBlock()
    {
        // The full-size u16 (offset 31) is computed BEFORE the size field itself and the member
        // block are appended, so on the wire it equals (header-before-size + member-block) which is
        // exactly (total packet length - 2). This locks the existing, decode-matching behaviour.
        Crew crew = MakeCrew();
        var members = new List<CrewMember>
        {
            new CrewMember { charId = 0xDEADBEEF, handle = "Cap", isOnline = 1, isCaptain = true },
            new CrewMember { charId = 0x00000002, handle = "Mate", isOnline = 0 },
        };

        byte[] packet = ServerPackets.BuildCrewInfoPacket(7, crew, members, 0xDEADBEEF, 0);

        ushort fullSize = BitConverter.ToUInt16(packet, 31);
        Assert.Equal(packet.Length - 2, fullSize);
    }
}
