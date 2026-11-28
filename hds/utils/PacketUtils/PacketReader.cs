using System;
using System.Buffers.Binary;

namespace hds
{
    public class PacketReader
    {
        private byte[] packetData;
        private int offset = 0;
        private int lenght = 0;

        public PacketReader(byte[] packetData)
        {
            this.packetData = packetData;
            if (packetData.Length > 0)
            {
                lenght = packetData.Length;
            }
        }

        public void IncrementOffsetByValue(int value)
        {
            offset = offset + value;
        }

        public void SetOffsetOverrideValue(int value)
        {
            offset = value;
        }

        public int GetOffset()
        {
            return offset;
        }

        public uint ReadUint8()
        {
            uint value = (uint)packetData[offset];
            offset++;
            return value;
        }

        public UInt16 ReadUInt16(int reversed)
        {
            ReadOnlySpan<byte> slice = packetData.AsSpan(offset, 2);
            UInt16 value = reversed == 1
                ? BinaryPrimitives.ReadUInt16LittleEndian(slice)
                : BinaryPrimitives.ReadUInt16BigEndian(slice);
            offset += 2;
            return value;
        }

        public UInt32 ReadUInt32(int reversed)
        {
            ReadOnlySpan<byte> slice = packetData.AsSpan(offset, 4);
            UInt32 value = reversed == 1
                ? BinaryPrimitives.ReadUInt32LittleEndian(slice)
                : BinaryPrimitives.ReadUInt32BigEndian(slice);
            offset += 4;
            return value;
        }

        public float ReadFloat(int reversed)
        {
            ReadOnlySpan<byte> slice = packetData.AsSpan(offset, 4);
            float value = reversed == 1
                ? BinaryPrimitives.ReadSingleLittleEndian(slice)
                : BinaryPrimitives.ReadSingleBigEndian(slice);
            offset += 4;
            return value;
        }

        public double ReadDouble(int reversed)
        {
            ReadOnlySpan<byte> slice = packetData.AsSpan(offset, 8);
            double value = reversed == 1
                ? BinaryPrimitives.ReadDoubleLittleEndian(slice)
                : BinaryPrimitives.ReadDoubleBigEndian(slice);
            offset += 8;
            return value;
        }

        public string ReadSizedString()
        {
            UInt16 sizeOfString = ReadUInt16(1);
            string value = ReadRawChars(sizeOfString);
            offset += sizeOfString;
            return value;
        }

        public string ReadSizedZeroTerminatedString()
        {
            UInt16 sizeOfString = ReadUInt16(1);
            if (sizeOfString == 0)
            {
                return string.Empty;
            }

            string value = ReadRawChars(sizeOfString - 1); // -1 strips the zero byte
            offset += sizeOfString; // just one for the zero termination
            return value;
        }

        private string ReadRawChars(int length)
        {
            if (length <= 0)
            {
                return string.Empty;
            }

            char[] chars = new char[length];
            int available = packetData.Length - offset;
            int copyCount = available < length ? (available > 0 ? available : 0) : length;
            for (int i = 0; i < copyCount; i++)
            {
                chars[i] = (char)packetData[offset + i];
            }
            return new string(chars);
        }

        public byte[] ReadBytes(int size)
        {
            // Just read amount of bytes
            byte[] newByteArray = new byte[size];
            ArrayUtils.copy(packetData, offset, newByteArray, 0, size);
            offset += size;
            return newByteArray;
        }

        public byte[] ReturnFullData()
        {
            return packetData;
        }
    }
}