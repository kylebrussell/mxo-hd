using System;
using System.Buffers.Binary;
using System.Text;

namespace hds
{
    public class PacketContent
    {
        private readonly DynamicArray packet = new DynamicArray();

        public void AddHexBytes(String hexbytes)
        {
            packet.append(StringUtils.hexStringToBytes(hexbytes));
        }

        public void AddInt16(Int16 value, int reversed)
        {
            Span<byte> dest = packet.GetAppendSpan(2);
            if (reversed == 1)
                BinaryPrimitives.WriteInt16LittleEndian(dest, value);
            else
                BinaryPrimitives.WriteInt16BigEndian(dest, value);
        }

        public void AddInt32(Int32 value, int reversed)
        {
            Span<byte> dest = packet.GetAppendSpan(4);
            if (reversed == 1)
                BinaryPrimitives.WriteInt32LittleEndian(dest, value);
            else
                BinaryPrimitives.WriteInt32BigEndian(dest, value);
        }

        public void AddUint32(UInt32 value, int reversed)
        {
            Span<byte> dest = packet.GetAppendSpan(4);
            if (reversed == 1)
                BinaryPrimitives.WriteUInt32LittleEndian(dest, value);
            else
                BinaryPrimitives.WriteUInt32BigEndian(dest, value);
        }

        public void AddUint16(UInt16 value, int reversed)
        {
            Span<byte> dest = packet.GetAppendSpan(2);
            if (reversed == 1)
                BinaryPrimitives.WriteUInt16LittleEndian(dest, value);
            else
                BinaryPrimitives.WriteUInt16BigEndian(dest, value);
        }

        public void AddUShort(UInt16 value)
        {
            packet.append((byte)value);
        }

        public void AddByteArray(byte[] array)
        {
            packet.append(array);
        }

        public void AddByte(byte value)
        {
            packet.append(value);
        }

        public void AddFloat(float value, int reversed)
        {
            Span<byte> dest = packet.GetAppendSpan(4);
            if (reversed == 1)
                BinaryPrimitives.WriteSingleLittleEndian(dest, value);
            else
                BinaryPrimitives.WriteSingleBigEndian(dest, value);
        }

        public void AddDouble(double value, int reversed)
        {
            Span<byte> dest = packet.GetAppendSpan(8);
            if (reversed == 1)
                BinaryPrimitives.WriteDoubleLittleEndian(dest, value);
            else
                BinaryPrimitives.WriteDoubleBigEndian(dest, value);
        }

        public void AddFloatLtVector3f(float x, float y, float z)
        {
            Span<byte> dest = packet.GetAppendSpan(12);
            BinaryPrimitives.WriteSingleLittleEndian(dest.Slice(0, 4), x);
            BinaryPrimitives.WriteSingleLittleEndian(dest.Slice(4, 4), y);
            BinaryPrimitives.WriteSingleLittleEndian(dest.Slice(8, 4), z);
        }

        public void AddDoubleLtVector3d(double x, double y, double z)
        {
            Span<byte> dest = packet.GetAppendSpan(24);
            BinaryPrimitives.WriteDoubleLittleEndian(dest.Slice(0, 8), x);
            BinaryPrimitives.WriteDoubleLittleEndian(dest.Slice(8, 8), y);
            BinaryPrimitives.WriteDoubleLittleEndian(dest.Slice(16, 8), z);
        }

        public byte[] ReturnFinalPacket()
        {
            return packet.getBytes();
        }

        public void AddString(string value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            if (value.Length == 0) return;
            int byteCount = Encoding.ASCII.GetByteCount(value);
            Encoding.ASCII.GetBytes(value.AsSpan(), packet.GetAppendSpan(byteCount));
        }

        public void AddSizedTerminatedString(string value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            int size = value.Length + 1;
            AddUint16((UInt16)size, 1);
            AddString(value);
            packet.append(0x00);
        }

        public void AddStringWithFixedSized(string value, int size)
        {
            AddString(value);
            int paddingSize = size - value.Length;
            if (paddingSize > 0)
            {
                // Preserve exact legacy wire count: (size - value.Length + 1) trailing null bytes
                // when value.Length < size (total size + 1 bytes).
                packet.AppendZeros(paddingSize + 1);
            }
        }

        public void AddSizedString(string value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            AddUint16((UInt16)value.Length, 1);
            AddString(value);
        }
    }
}
