using System.Buffers.Binary;
namespace MxoHd.PacketResearch;

/// <summary>6213aefc/621515d0: SERVER_ABILITY_LOAD / attribute base value, not a derived total.</summary>
public sealed record BaseAttributeRecord(ushort Id, ushort Value, ushort CsSlot)
{
    public static bool TryDecode(ReadOnlySpan<byte> rpc, out BaseAttributeRecord? record)
    {
        record=null;
        return rpc.Length==8 && rpc[0]==0x80 && rpc[1]==0xb2 && TryDecodePayload(rpc[2..],out record);
    }
    public static bool TryDecodePayload(ReadOnlySpan<byte> payload,out BaseAttributeRecord? record)
    {
        record=null;
        if(payload.Length!=6)return false;
        record=new(BinaryPrimitives.ReadUInt16LittleEndian(payload),BinaryPrimitives.ReadUInt16LittleEndian(payload[2..]),BinaryPrimitives.ReadUInt16LittleEndian(payload[4..]));
        return true;
    }
}
