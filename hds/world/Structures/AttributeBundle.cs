using System;

namespace hds.world.Structures
{
    /// <summary>
    /// Pure, allocation-only builders for the decoded player-attribute packet
    /// layouts (80 b2 / SERVER_PLAYER_ATTRIBUTE and 80 bc / SERVER_MANAGE_BONUS).
    ///
    /// These methods deliberately take NO Store / DB / client dependency so they
    /// are trivially unit-testable and can be proven byte-exact against the
    /// historical hardcoded hex blobs by golden tests
    /// (see tests/hds.Tests/AttributeSerializerTests.cs).
    ///
    /// CONVENTION: every method returns the FULL on-the-wire RPC message,
    /// INCLUDING the two opcode bytes (80 b2 or 80 bc). The opcode is written
    /// big-endian on the wire (literal bytes 0x80 0xb2 / 0x80 0xbc), matching the
    /// existing PacketContent.AddUint16(0x80b2, 0) usage and the hardcoded blobs.
    /// </summary>
    public static class AttributeBundle
    {
        /// <summary>Opcode for SERVER_PLAYER_ATTRIBUTE, on the wire as 0x80 0xb2.</summary>
        private static readonly byte[] Opcode80b2 = { 0x80, 0xb2 };

        /// <summary>Opcode for SERVER_MANAGE_BONUS, on the wire as 0x80 0xbc.</summary>
        private static readonly byte[] Opcode80bc = { 0x80, 0xbc };

        // ------------------------------------------------------------------
        // 80 b2 / SERVER_PLAYER_ATTRIBUTE
        // ------------------------------------------------------------------

        /// <summary>The fixed 3-byte tail historically appended by <c>sendAttribute</c>.</summary>
        public static readonly byte[] DefaultAttributeTail = { 0x00, 0x08, 0x02 };

        /// <summary>
        /// Builds the 80 b2 / SERVER_PLAYER_ATTRIBUTE message used by
        /// <c>sendAttribute</c> and by the live <c>80b2ca0300000802</c> blob.
        ///
        /// Layout (full message, 8 bytes):
        ///   [0..1] opcode             80 b2
        ///   [2]    type               attribute "type"/slot byte (e.g. 0xca)
        ///   [3..4] attributeValue     u16, BIG-ENDIAN on the wire
        ///   [5..7] tail               3-byte tail (default 00 08 02)
        ///
        /// Reproduces <c>80b2ca0300000802</c> via Build80b2Attribute(0xca, 0x0300)
        /// (attributeValue 0x0300 serializes big-endian as bytes 03 00, default tail).
        ///
        /// The single "code write bonuses" blob <c>80b225000f001900</c> shares this
        /// exact shape but with a different tail; reproduce it via
        /// Build80b2Attribute(0x25, 0x000f, new byte[] { 0x00, 0x19, 0x00 }).
        /// </summary>
        public static byte[] Build80b2Attribute(byte type, UInt16 attributeValue, byte[] tail = null)
        {
            byte[] tailBytes = tail ?? DefaultAttributeTail;
            if (tailBytes.Length != 3)
            {
                throw new ArgumentException("80 b2 attribute tail must be exactly 3 bytes.", nameof(tail));
            }

            byte[] result = new byte[8];
            result[0] = Opcode80b2[0];
            result[1] = Opcode80b2[1];
            result[2] = type;
            // attributeValue big-endian (high byte first), matching the original
            // PacketContent.AddUint16(attributeValue, 0) reversed serialization.
            result[3] = (byte)((attributeValue >> 8) & 0xff);
            result[4] = (byte)(attributeValue & 0xff);
            result[5] = tailBytes[0];
            result[6] = tailBytes[1];
            result[7] = tailBytes[2];
            return result;
        }

        // ------------------------------------------------------------------
        // 80 bc / SERVER_MANAGE_BONUS  (45 03 family)
        // ------------------------------------------------------------------

        /// <summary>
        /// Builds the strongest-decoded 80 bc / SERVER_MANAGE_BONUS layout:
        /// the <c>45 03</c> family, with the decoded byte-9 field1 self-repeat.
        ///
        /// Layout (full message, 22 bytes = 2 opcode + 20 payload):
        ///   opcode  [0..1]            80 bc
        ///   payload (20 bytes, offsets below are PAYLOAD-relative):
        ///     [0..1]  family header   45 03
        ///     [2..3]  field1 stateId  u16, LITTLE-ENDIAN (e.g. ca 03 = 0x03ca)
        ///     [4]     separator       00
        ///     [5..8]  node value      u32, LITTLE-ENDIAN (e.g. 25 00 00 00 = 0x25)
        ///     [9..10] field1 REPEAT   u16, LITTLE-ENDIAN — DECODED INVARIANT:
        ///                             field1 (stateId) is echoed at payload
        ///                             byte offset 9.
        ///     [11..19] zero padding   nine 0x00 bytes
        ///
        /// Reproduces <c>80bc4503ca030025000000ca03000000000000000000</c> via
        /// Build80bc4503(0x03ca, 0x25).
        /// </summary>
        public static byte[] Build80bc4503(UInt16 stateId, UInt32 node)
        {
            byte[] result = new byte[22];
            int i = 0;

            // Opcode 80 bc.
            result[i++] = Opcode80bc[0];
            result[i++] = Opcode80bc[1];

            // --- payload begins here (payload offset 0) ---

            // Payload [0..1] family header 45 03.
            result[i++] = 0x45;
            result[i++] = 0x03;

            // Payload [2..3] field1 stateId, little-endian.
            result[i++] = (byte)(stateId & 0xff);
            result[i++] = (byte)((stateId >> 8) & 0xff);

            // Payload [4] separator.
            result[i++] = 0x00;

            // Payload [5..8] node value, u32 little-endian.
            result[i++] = (byte)(node & 0xff);
            result[i++] = (byte)((node >> 8) & 0xff);
            result[i++] = (byte)((node >> 16) & 0xff);
            result[i++] = (byte)((node >> 24) & 0xff);

            // Payload [9..10] field1 REPEAT (decoded byte-9 invariant), little-endian.
            result[i++] = (byte)(stateId & 0xff);
            result[i++] = (byte)((stateId >> 8) & 0xff);

            // Payload [11..19] nine zero-padding bytes (already 0 from allocation).
            // i advances implicitly; remaining bytes stay 0x00.
            return result;
        }

        /// <summary>
        /// Byte offset, within the 80 bc payload (i.e. excluding the 2 opcode
        /// bytes), at which the 45 03 family echoes field1 (the stateId).
        /// Exposed so the structural golden test can assert the invariant.
        /// </summary>
        public const int Field1RepeatPayloadOffset = 9;

        /// <summary>Number of opcode bytes prepended before the payload.</summary>
        public const int OpcodeLength = 2;
    }
}
