using System.Buffers.Binary;
namespace MxoHd.PacketResearch;

/// <summary>Native inline80b9 (621e5100 / vtable6288c1e4), exactly43 bytes.
/// Names follow printer621d7a70; they do not establish gameplay purpose or admission policy.
/// Ground words and all unknown flag/sentinel bits remain lossless.</summary>
public sealed record AbilityUseRecord(ushort Id,uint Target,uint ObjectTargetCreate,
    ulong TargetGroundLocationWord0,ulong TargetGroundLocationWord1,ulong TargetGroundLocationWord2,
    byte TargetInventorySlot,byte InvSlotOfToolUsed,byte FromPill,uint TargetMiniTeamMemberId)
{
    public const int RpcLength=43,PayloadLength=41;
    public static bool TryDecode(ReadOnlySpan<byte> rpc,out AbilityUseRecord? record)
    {
        record=null;
        return rpc.Length==RpcLength && rpc[0]==0x80 && rpc[1]==0xb9 && TryDecodePayload(rpc[2..],out record);
    }
    public static bool TryDecodePayload(ReadOnlySpan<byte> body,out AbilityUseRecord? record)
    {
        record=null;if(body.Length!=PayloadLength)return false;
        record=new(BinaryPrimitives.ReadUInt16LittleEndian(body),BinaryPrimitives.ReadUInt32LittleEndian(body[2..]),
            BinaryPrimitives.ReadUInt32LittleEndian(body[6..]),BinaryPrimitives.ReadUInt64LittleEndian(body[10..]),
            BinaryPrimitives.ReadUInt64LittleEndian(body[18..]),BinaryPrimitives.ReadUInt64LittleEndian(body[26..]),
            body[34],body[35],body[36],BinaryPrimitives.ReadUInt32LittleEndian(body[37..]));
        return true;
    }
    /// <summary>Research encoder only; not a replacement for the server's ability parser.</summary>
    public byte[] Encode()
    {
        byte[] rpc=new byte[RpcLength];rpc[0]=0x80;rpc[1]=0xb9;var body=rpc.AsSpan(2);
        BinaryPrimitives.WriteUInt16LittleEndian(body,Id);BinaryPrimitives.WriteUInt32LittleEndian(body[2..],Target);
        BinaryPrimitives.WriteUInt32LittleEndian(body[6..],ObjectTargetCreate);
        BinaryPrimitives.WriteUInt64LittleEndian(body[10..],TargetGroundLocationWord0);
        BinaryPrimitives.WriteUInt64LittleEndian(body[18..],TargetGroundLocationWord1);
        BinaryPrimitives.WriteUInt64LittleEndian(body[26..],TargetGroundLocationWord2);
        body[34]=TargetInventorySlot;body[35]=InvSlotOfToolUsed;body[36]=FromPill;
        BinaryPrimitives.WriteUInt32LittleEndian(body[37..],TargetMiniTeamMemberId);return rpc;
    }
}
