using System.Buffers.Binary;

namespace MxoHd.PacketResearch;

/// <summary>Client 6213b94d/62151b00's fixed 22-byte SERVER_MANAGE_BONUS layout.
/// Raw bits are authoritative: Value is not universally an integer and unknown Types remain decodable.</summary>
public sealed record ManageBonusRecord(byte Type, uint Locator, uint SourceId, ushort TargetId,
    uint ValueBits, byte AddRemoveType, uint DurationBits)
{
    // 625ebb90 dispatches on locator LOW BYTE; Type remains an uninterpreted bit field.
    // Other locator kinds and operation bytes retain raw fields, without guessed aggregate semantics.
    public IndividualBonusOperation? IndividualOperation => (Locator & 255)==0 && AddRemoveType<=2
        ? (IndividualBonusOperation)AddRemoveType : null;
    public int SignedValue => unchecked((int)ValueBits);
    [System.Text.Json.Serialization.JsonIgnore]
    public float FloatValue => BitConverter.UInt32BitsToSingle(ValueBits);
    [System.Text.Json.Serialization.JsonIgnore]
    public float Duration => BitConverter.UInt32BitsToSingle(DurationBits);
    // Compatibility views used by the existing capture research, not additional wire fields.
    public ushort Family => (ushort)(Type | ((Locator & 255) << 8));
    public ushort RecordId => (ushort)(Locator >> 8);
    public byte LocatorHighByte => (byte)(Locator >> 24);

    public static bool TryDecode(ReadOnlySpan<byte> rpc, out ManageBonusRecord? record)
    {
        record = null;
        if (rpc.Length != 22 || rpc[0] != 0x80 || rpc[1] != 0xbc) return false;
        return TryDecodePayload(rpc[2..], out record);
    }

    public static bool TryDecodePayload(ReadOnlySpan<byte> payload, out ManageBonusRecord? record)
    {
        record = null;
        if (payload.Length != 20) return false;
        record = new(payload[0], BinaryPrimitives.ReadUInt32LittleEndian(payload[1..]),
            BinaryPrimitives.ReadUInt32LittleEndian(payload[5..]), BinaryPrimitives.ReadUInt16LittleEndian(payload[9..]),
            BinaryPrimitives.ReadUInt32LittleEndian(payload[11..]), payload[15],
            BinaryPrimitives.ReadUInt32LittleEndian(payload[16..]));
        return true;
    }
}

public enum IndividualBonusOperation : byte { Add=0, Remove=1, Update=2 }
