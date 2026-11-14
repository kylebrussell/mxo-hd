using System;
using System.Buffers.Binary;

namespace hds
{
    /// <summary>Native jump request records; raw floating-point payloads are preserved without new admission policy.</summary>
    public static class JumpRecords
    {
        public readonly struct Destination
        {
            public readonly ulong XBits,YBits,ZBits; public readonly ushort Sector,Prefab,NavPoly;
            public Destination(ulong xBits,ulong yBits,ulong zBits,ushort sector,ushort prefab,ushort navPoly)
            { XBits=xBits;YBits=yBits;ZBits=zBits;Sector=sector;Prefab=prefab;NavPoly=navPoly; }
            public double X => BitConverter.Int64BitsToDouble(unchecked((long)XBits));
            public double Y => BitConverter.Int64BitsToDouble(unchecked((long)YBits));
            public double Z => BitConverter.Int64BitsToDouble(unchecked((long)ZBits));
        }
        public readonly struct Start
        {
            public readonly Destination Destination;public readonly uint PeakBits,TimeStamp;public readonly byte IsHyperJump;
            public Start(Destination destination,uint peakBits,byte isHyperJump,uint timeStamp)
            {Destination=destination;PeakBits=peakBits;IsHyperJump=isHyperJump;TimeStamp=timeStamp;}
        }
        public readonly struct Cancel
        {
            public readonly Destination Destination;
            public Cancel(Destination destination){Destination=destination;}
        }
        static Destination ReadDestination(ReadOnlySpan<byte> b) => new Destination(
            BinaryPrimitives.ReadUInt64LittleEndian(b), BinaryPrimitives.ReadUInt64LittleEndian(b[8..]),
            BinaryPrimitives.ReadUInt64LittleEndian(b[16..]), BinaryPrimitives.ReadUInt16LittleEndian(b[24..]),
            BinaryPrimitives.ReadUInt16LittleEndian(b[26..]), BinaryPrimitives.ReadUInt16LittleEndian(b[28..]));
        static void WriteDestination(Span<byte> b, Destination d)
        {
            BinaryPrimitives.WriteUInt64LittleEndian(b,d.XBits); BinaryPrimitives.WriteUInt64LittleEndian(b[8..],d.YBits);
            BinaryPrimitives.WriteUInt64LittleEndian(b[16..],d.ZBits); BinaryPrimitives.WriteUInt16LittleEndian(b[24..],d.Sector);
            BinaryPrimitives.WriteUInt16LittleEndian(b[26..],d.Prefab); BinaryPrimitives.WriteUInt16LittleEndian(b[28..],d.NavPoly);
        }
        public static bool TryStartBody(ReadOnlySpan<byte> b,out Start value)
        {
            value=default;if(b.Length!=39)return false;
            value=new Start(ReadDestination(b),BinaryPrimitives.ReadUInt32LittleEndian(b[30..]),b[34],BinaryPrimitives.ReadUInt32LittleEndian(b[35..]));return true;
        }
        public static bool TryCancelBody(ReadOnlySpan<byte> b,out Cancel value)
        { value=default;if(b.Length!=30)return false;value=new Cancel(ReadDestination(b));return true; }
        public static bool TryStart(ReadOnlySpan<byte> b,out Start value)
        { value=default;return b.Length==41&&b[0]==0x80&&b[1]==0xc2&&TryStartBody(b[2..],out value); }
        public static bool TryCancel(ReadOnlySpan<byte> b,out Cancel value)
        { value=default;return b.Length==32&&b[0]==0x80&&b[1]==0xc4&&TryCancelBody(b[2..],out value); }
        public static byte[] Encode(Start value)
        {
            var b=new byte[41];b[0]=0x80;b[1]=0xc2;WriteDestination(b.AsSpan(2),value.Destination);
            BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(32),value.PeakBits);b[36]=value.IsHyperJump;
            BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(37),value.TimeStamp);return b;
        }
        public static byte[] Encode(Cancel value)
        { var b=new byte[32];b[0]=0x80;b[1]=0xc4;WriteDestination(b.AsSpan(2),value.Destination);return b; }
        public static byte[] EncodeClockEcho(uint clientTickSample)
        {
            var b=new byte[6];b[0]=0x80;b[1]=0xc3;
            BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(2),clientTickSample);return b;
        }
    }
}
