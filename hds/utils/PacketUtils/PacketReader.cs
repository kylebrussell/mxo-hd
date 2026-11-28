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
            if (packetData != null && packetData.Length > 0)
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
            int remaining = packetData != null && offset >= 0 && offset <= packetData.Length
                ? packetData.Length - offset
                : 0;
            int advance = sizeOfString < remaining ? sizeOfString : remaining;
            string value = ReadRawChars(advance);
            offset += advance;
            return value;
        }

        public string ReadSizedZeroTerminatedString()
        {
            UInt16 sizeOfString = ReadUInt16(1);
            if (sizeOfString == 0)
            {
                return string.Empty;
            }

            int remaining = packetData != null && offset >= 0 && offset <= packetData.Length
                ? packetData.Length - offset
                : 0;
            int advance = sizeOfString < remaining ? sizeOfString : remaining;
            int charCount = sizeOfString - 1;
            if (charCount > advance)
            {
                charCount = advance;
            }
            string value = ReadRawChars(charCount); // -1 strips the zero byte when present
            offset += advance;
            return value;
        }

        private string ReadRawChars(int length)
        {
            if (length <= 0 || packetData == null || offset < 0 || offset >= packetData.Length)
            {
                return string.Empty;
            }

            int available = packetData.Length - offset;
            int copyCount = available < length ? available : length;
            if (copyCount <= 0)
            {
                return string.Empty;
            }

            return string.Create(copyCount, (packetData, offset), (span, state) =>
            {
                byte[] data = state.packetData;
                int start = state.offset;
                for (int i = 0; i < span.Length; i++)
                {
                    span[i] = (char)data[start + i];
                }
            });
        }

        public byte[] ReadBytes(int size)
        {
            if (size < 0 || packetData == null || offset < 0 || size > packetData.Length - offset)
            {
                throw new ArgumentOutOfRangeException(nameof(size), "Requested byte count exceeds remaining packet data.");
            }
            if (size == 0)
            {
                return Array.Empty<byte>();
            }
            byte[] newByteArray = new byte[size];
            Buffer.BlockCopy(packetData, offset, newByteArray, 0, size);
            offset += size;
            return newByteArray;
        }

        public byte[] ReturnFullData()
        {
            return packetData;
        }
    }
}
