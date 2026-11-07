using hds;
using hds.shared;

namespace hds.Tests;

// Byte strings are real client packets: the 7.6005 client walking, turning and strafing against this
// server, plus retail captures from research/ for flag combinations it didn't produce.
public class SelfViewUpdateTests
{
    private static byte[] Hex(string hex) => StringUtils.hexStringToBytes(hex.Replace(" ", ""));

    private static SelfViewEntry Entry(string hex)
    {
        Assert.True(SelfViewEntry.TryParse(Hex(hex), 0, out SelfViewEntry entry, out string failure), failure);
        return entry;
    }

    [Fact]
    public void ParsesPositionOnly()
    {
        SelfViewEntry entry = Entry("01 08 9f a5 da 47 f0 bf 2d 44 52 11 d3 c5 00 00");

        SelfViewUpdate state = entry.State;
        Assert.Equal(0x08, state.Flags);
        Assert.Null(state.Action);
        Assert.Null(state.Yaw);
        Assert.True(state.HasPosition);
        Assert.Equal(111947.2f, state.X, 1);
        Assert.Equal(694.999f, state.Y, 2);
        Assert.Equal(-6754.165f, state.Z, 2);
        Assert.Equal(14, entry.End);
        Assert.Null(entry.LastName);
    }

    [Fact]
    public void ParsesActionThenPosition()
    {
        // Sent when the player stops walking: action 00 (idle) and the final position.
        SelfViewUpdate state = Entry("01 0a 00 00 60 dc 47 f0 bf 2d 44 00 a0 c3 c5 00 00").State;

        Assert.Equal((byte)0x00, state.Action);
        Assert.Null(state.Yaw);
        Assert.Equal(112832f, state.X);
        Assert.Equal(-6260f, state.Z);
    }

    [Fact]
    public void ParsesActionThenYaw()
    {
        // End of a turn: action 00 then yaw e5 (the old parser had no case for 0x06).
        SelfViewEntry entry = Entry("01 06 00 e5 00 00");

        Assert.Equal((byte)0x00, entry.State.Action);
        Assert.Equal((byte)0xe5, entry.State.Yaw);
        Assert.False(entry.State.HasPosition);
        Assert.Equal(4, entry.End);
    }

    [Fact]
    public void ParsesYawThenPositionFromRetail()
    {
        // Retail 0x0c (turning while moving); the old parser dropped these and lost the position.
        SelfViewUpdate state = Entry("01 0c 0a ae 0d 84 c7 00 00 be 42 42 60 84 46 00 00").State;

        Assert.Null(state.Action);
        Assert.Equal((byte)0x0a, state.Yaw);
        Assert.Equal(95f, state.Y);
    }

    [Fact]
    public void ParsesAllThreeFieldsFromRetail()
    {
        SelfViewUpdate state = Entry("01 0e 05 75 3e 8a 84 c7 00 00 be 42 a5 b4 82 46 00 00").State;

        Assert.Equal((byte)0x05, state.Action);
        Assert.Equal((byte)0x75, state.Yaw);
        Assert.Equal(95f, state.Y);
    }

    [Fact]
    public void Bit20IsOneByteAfterYaw()
    {
        // From the 7.6005 client as it started walking after a relog.
        SelfViewEntry entry = Entry("01 26 00 3e c6 00 00");

        Assert.Equal((byte)0x00, entry.State.Action);
        Assert.Equal((byte)0x3e, entry.State.Yaw);
        Assert.Equal((byte)0xc6, entry.State.Unknown20);
        Assert.Equal(5, entry.End);
    }

    [Fact]
    public void Bit20ComesBeforeThePositionInRetail()
    {
        // Retail 0x2e: action 07, yaw 03, bit-0x20 byte 59, then a position whose y is the usual 95.
        SelfViewUpdate state = Entry("01 2e 07 03 59 07 77 78 47 00 00 be 42 0b c7 b3 c7 00 00").State;

        Assert.Equal((byte)0x59, state.Unknown20);
        Assert.Equal(95f, state.Y);
        Assert.Equal(63607.03f, state.X, 1);
    }

    [Fact]
    public void ParsesStateAndNamesTogether()
    {
        // Sub-view 03 = state part (action 03) followed by the names part.
        SelfViewEntry entry = Entry("03 02 03 60 03 4e 65 6f 02 4d 72 00 00");

        Assert.Equal((byte)0x03, entry.State.Action);
        Assert.Equal("Neo"u8.ToArray(), entry.LastName);
        Assert.Equal("Mr"u8.ToArray(), entry.FirstName);
        Assert.Equal(11, entry.End);
    }

    [Theory]
    [InlineData("01 28 0a c0 00 8c 4d 00 b0 fc 0f 47 00 20 8a c4 50 e6 fd c5 78 91 9c 04 00 00 04 01")] // retail 0x28 that fits no known layout
    [InlineData("01 1c 1e 05 01 01 00 00 00 d1 99 dd 3c c7 00 00 be 42")] // bit 0x10: layout unknown
    [InlineData("01 01 00 00 1d 00")]                                     // bit 0x01: layout unknown
    [InlineData("01 08 9f a5 da 47 f0 bf")]                               // position cut short
    [InlineData("02 61 03 4e 65 6f 02 4d 72 00 00")]                      // unknown name flags
    [InlineData("04 00 00")]                                              // unknown sub-view part
    public void RejectsWhatItCannotWalk(string hex)
    {
        Assert.False(SelfViewEntry.TryParse(Hex(hex), 0, out SelfViewEntry entry, out string failure));
        Assert.Null(entry);
        Assert.False(string.IsNullOrEmpty(failure));
    }

    private static int ParseOwnView(byte[] packet, int offset) =>
        SelfViewEntry.TryParse(packet, offset, out SelfViewEntry entry, out _) ? entry.End : packet.Length;

    [Fact]
    public void SectionEndsAfterAPositionUpdateSoTheFollowingRpcsAreRead()
    {
        // Position update, end of view list, then an RPC list. The old parser stopped at the 00 00
        // after a position or yaw update, so RPCs sent in the same packet were never handled.
        byte[] packet = Hex("02 03 02 00 01 08 9f a5 da 47 f0 bf 2d 44 52 11 d3 c5 00 00 04 01 00 31 01 02 80 c9");

        int end = Protocol03Manager.ParseSection(packet, 2, ParseOwnView);

        Assert.Equal(20, end);
        Assert.Equal(0x04, packet[end]);
    }

    [Fact]
    public void SectionEndsAfterTheSpawnReplyNames()
    {
        // First packet after the self-spawn: the names, then five RPCs that the old parser skipped
        // because it read the sub-view byte as the name flags.
        byte[] packet = Hex("02 03 02 00 02 60 03 4e 65 6f 02 4d 72 00 00 04 01 00 01 05 02 80 be 07 3f 6b 00 00 00 00 00 08 3e 6b 00 10 00 00 00 00 02 81 48 02 0c 01");

        int end = Protocol03Manager.ParseSection(packet, 2, ParseOwnView);

        Assert.Equal(15, end);
        Assert.Equal(0x04, packet[end]);
    }

    [Fact]
    public void UnknownViewStopsAtTheEndOfThePacket()
    {
        byte[] packet = Hex("02 03 05 00 01 08 00 00 04 01");

        Assert.Equal(packet.Length, Protocol03Manager.ParseSection(packet, 2, ParseOwnView));
    }
}
