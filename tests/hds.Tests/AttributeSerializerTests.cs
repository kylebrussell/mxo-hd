using hds;
using hds.world.Structures;

namespace hds.Tests;

/// <summary>
/// Golden tests proving the AttributeBundle builders are byte-exact with the
/// historical hardcoded hex blobs from PlayerPackets.sendPlayerAttributes /
/// sendAttribute. If any of these fail, the refactor changed the bytes on the
/// wire.
/// </summary>
public class AttributeSerializerTests
{
    private static string Hex(byte[] bytes) => StringUtils.bytesToString_NS(bytes);

    // ------------------------------------------------------------------
    // 80 b2 / SERVER_PLAYER_ATTRIBUTE golden tests
    // ------------------------------------------------------------------

    [Fact]
    public void Build80b2Attribute_DefaultTail_MatchesCodeWriteBlob()
    {
        // Original: client.messageQueue.addRpcMessage(hexStringToBytes("80b2ca0300000802"));
        byte[] message = AttributeBundle.Build80b2Attribute(0xca, 0x0300);
        Assert.Equal("80b2ca0300000802", Hex(message));
    }

    [Fact]
    public void Build80b2Attribute_DefaultTail_MatchesSendAttributeShape()
    {
        // sendAttribute previously emitted: 80 b2 | type | attr(big-endian) | 00 08 02.
        // Matches the original commented blob 80b23a0400000802: type 0x3a, value 0x0400
        // serialized big-endian as bytes 04 00 (= uint16ToByteArray(value, 0)).
        byte[] message = AttributeBundle.Build80b2Attribute(0x3a, 0x0400);
        Assert.Equal("80b23a0400000802", Hex(message));
    }

    [Fact]
    public void Build80b2Attribute_CustomTail_MatchesCodeWriteBonusBlob()
    {
        // Original: client.messageQueue.addRpcMessage(hexStringToBytes("80b225000f001900"));
        byte[] message = AttributeBundle.Build80b2Attribute(0x25, 0x000f, new byte[] { 0x00, 0x19, 0x00 });
        Assert.Equal("80b225000f001900", Hex(message));
    }

    [Fact]
    public void Build80b2Attribute_RejectsTailThatIsNotThreeBytes()
    {
        Assert.Throws<ArgumentException>(
            () => AttributeBundle.Build80b2Attribute(0x25, 0x000f, new byte[] { 0x00, 0x19 }));
    }

    // ------------------------------------------------------------------
    // 80 bc / SERVER_MANAGE_BONUS (45 03 family) golden tests
    // ------------------------------------------------------------------

    [Fact]
    public void Build80bc4503_MatchesLiveBlob()
    {
        // Original: client.messageQueue.addRpcMessage(
        //     hexStringToBytes("80bc4503ca030025000000ca03000000000000000000"));
        byte[] message = AttributeBundle.Build80bc4503(0x03ca, 0x25);
        Assert.Equal("80bc4503ca030025000000ca03000000000000000000", Hex(message));
    }

    [Fact]
    public void Build80bc4503_ProducesFullTwentyTwoByteMessage()
    {
        byte[] message = AttributeBundle.Build80bc4503(0x03ca, 0x25);
        // 2 opcode bytes + 20-byte payload.
        Assert.Equal(22, message.Length);
    }

    [Fact]
    public void Build80bc4503_RepeatsField1AtPayloadByteOffsetNine()
    {
        // Use distinct stateId/node so the repeat is unambiguous.
        UInt16 stateId = 0x0011; // little-endian on the wire: 11 00
        byte[] message = AttributeBundle.Build80bc4503(stateId, 0x12345678);

        int payloadStart = AttributeBundle.OpcodeLength;
        int field1Start = payloadStart + 2; // payload offsets 2..3
        int repeatStart = payloadStart + AttributeBundle.Field1RepeatPayloadOffset; // payload offset 9

        // field1 (little-endian) appears at payload offset 2 and is echoed at offset 9.
        Assert.Equal(message[field1Start], message[repeatStart]);
        Assert.Equal(message[field1Start + 1], message[repeatStart + 1]);

        // And the echoed bytes are exactly the little-endian stateId.
        Assert.Equal((byte)(stateId & 0xff), message[repeatStart]);
        Assert.Equal((byte)((stateId >> 8) & 0xff), message[repeatStart + 1]);
    }

    [Fact]
    public void Build80bc4503_EncodesNodeAsLittleEndianUint32()
    {
        byte[] message = AttributeBundle.Build80bc4503(0x03ca, 0x12345678);
        // payload offset 5..8 = opcode(2) + 5 => message[7..10]
        int nodeStart = AttributeBundle.OpcodeLength + 5;
        Assert.Equal(0x78, message[nodeStart]);
        Assert.Equal(0x56, message[nodeStart + 1]);
        Assert.Equal(0x34, message[nodeStart + 2]);
        Assert.Equal(0x12, message[nodeStart + 3]);
    }
}
