using System;
using System.Buffers.Binary;
namespace hds
{
    /// <summary>Native19-byte80ba layout. Named messages do not establish new admission rules.</summary>
    public static class AbilityRefusals
    {
        public const uint ReuseWait=5, CurrentTactic=6, NotLoaded=7, WrongStyle=13, InsufficientInnerStrength=17;
        /// <summary>Compatibility value for unresolved aggregate failures;6 is not a generic busy message.</summary>
        public const uint LegacyAmbiguous=CurrentTactic;
        public const byte NoToolSlot=0x6b;
        public readonly struct Record
        {
            public readonly uint AbilityID,ErrorID,InnerstrengthDeficit,StatesNotMet;
            public readonly byte ToolsSlot;
            public Record(uint ability,uint error,uint deficit=0,uint states=0,byte slot=NoToolSlot)
            {AbilityID=ability;ErrorID=error;InnerstrengthDeficit=deficit;StatesNotMet=states;ToolsSlot=slot;}
        }
        public static byte[] Encode(Record value)
        {
            byte[] bytes=new byte[19];bytes[0]=0x80;bytes[1]=0xba;
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(2,4),value.AbilityID);bytes[6]=value.ToolsSlot;
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(7,4),value.ErrorID);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(11,4),value.InnerstrengthDeficit);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(15,4),value.StatesNotMet);return bytes;
        }
        public static bool TryDecode(ReadOnlySpan<byte> bytes,out Record value)
        {
            value=default;if(bytes.Length!=19||bytes[0]!=0x80||bytes[1]!=0xba)return false;
            value=new Record(BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(2,4)),BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(7,4)),
                BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(11,4)),BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(15,4)),bytes[6]);return true;
        }
    }
}
