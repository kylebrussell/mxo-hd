using System;
using System.Buffers.Binary;

namespace hds
{
    /// <summary>Packed ILCombatHandler entry: client.dll 6263ab10/6263ac00 consume exactly 0x79 bytes.
    /// Slot numbers refer to the combat init's fighter list, identically for fighters and observers.</summary>
    public sealed class CombatRoundEntry
    {
        public const int Size = 121;
        public byte FirstSlot = 1, SecondSlot = 2;
        public ushort FirstApproachMs, SecondApproachMs;
        public float[] FirstOffset = new float[3], SecondOffset = new float[3];
        public byte FirstFacing, SecondFacing;
        public ushort Sequence;
        public uint StartMs;
        public short SecondDelayMs;
        public ushort FirstDurationMs, SecondDurationMs;
        public byte Flags = 3;
        public ulong FirstTransition, SecondTransition, Animation, FirstRecovery, SecondRecovery;
        // Tactical/ability/style/result fields consumed by the timed exchange command (625f4ed0).
        // Keep the undecoded portions explicit rather than embedding an unrelated captured fight.
        public byte[] ResultData = new byte[36];

        public byte[] Serialize()
        {
            if (FirstOffset.Length != 3 || SecondOffset.Length != 3 || ResultData.Length != 36)
                throw new ArgumentException("Invalid combat entry field width");
            byte[] data = new byte[Size];
            data[0] = FirstSlot; data[1] = SecondSlot;
            U16(data, 2, FirstApproachMs); U16(data, 4, SecondApproachMs);
            for (int i = 0; i < 3; i++)
            {
                BinaryPrimitives.WriteSingleLittleEndian(data.AsSpan(6 + i * 4), FirstOffset[i]);
                BinaryPrimitives.WriteSingleLittleEndian(data.AsSpan(18 + i * 4), SecondOffset[i]);
            }
            data[30] = FirstFacing; data[31] = SecondFacing;
            U16(data, 32, Sequence); U32(data, 34, StartMs);
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(38), SecondDelayMs);
            U16(data, 40, FirstDurationMs); U16(data, 42, SecondDurationMs); data[44] = Flags;
            ulong[] refs = { FirstTransition, SecondTransition, Animation, FirstRecovery, SecondRecovery };
            for (int i = 0; i < refs.Length; i++) BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(45 + i * 8), refs[i]);
            ResultData.CopyTo(data, 85);
            return data;
        }
        internal static void U16(byte[] data, int at, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(at), value);
        internal static void U32(byte[] data, int at, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(at), value);
        public static ulong Reference(uint id) => ((ulong)0x24000b8b << 32) | id;
    }
}
