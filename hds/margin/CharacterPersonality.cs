using System;
namespace hds
{
    // Native creation selector62870600 and class5 attribute contributions; profession/TitleAbility is independent.
    public static class CharacterPersonality
    {
        public const uint Legacy=299;
        public static bool Valid(uint trait)=>trait>=299&&trait<=308;
        public static uint Resolve(uint? assigned)
        {
            uint trait=assigned??Legacy;
            if(!Valid(trait))throw new InvalidOperationException("Invalid assigned character trait");
            return trait;
        }
        private static readonly ushort[,] Bases={
            {8,11,8,5,8},{8,5,8,11,8},{5,8,11,8,8},{11,8,5,8,8},{8,8,8,5,11},
            {8,8,8,11,5},{8,8,5,11,8},{8,8,11,5,8},{11,5,8,8,8},{5,11,8,8,8}};
        public static ushort Base(uint? trait,byte attribute)
        {
            int column=Array.IndexOf(CharacterAttributes.All,attribute);
            if(column<0)throw new ArgumentOutOfRangeException(nameof(attribute));
            return Bases[Resolve(trait)-Legacy,column];
        }
        public static byte[] BackgroundPrefix(bool returning,uint? trait)
        {
            var packet=new PacketContent();
            if(returning)packet.AddHexBytes("01000000640000007B0000006500000000000000000000006C00000000000000");
            packet.AddUint32(Resolve(trait),1);
            return packet.ReturnFinalPacket();
        }
    }
}
