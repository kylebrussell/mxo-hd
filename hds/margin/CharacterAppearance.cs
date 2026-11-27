using System.Collections.Generic;
using System.Linq;
using hds.databases.Entities;

namespace hds
{
    internal static class CharacterAppearance
    {
        // The FACIAL_HAIR allocation tables in library.rsia, in packed RSI order.
        private static readonly ushort[] MaleFacialDetails = { 102, 101, 100, 103, 104, 105, 106, 118, 119, 120, 124, 125, 123 };
        private static readonly ushort[] FemaleFacialDetails = { 108, 107, 109, 110, 111, 112, 113, 114, 115, 116, 117, 122 };

        internal static int FacialDetailOrdinal(ushort descriptor, bool female) =>
            System.Array.IndexOf(female ? FemaleFacialDetails : MaleFacialDetails, descriptor);

        internal static byte[] Build(Rsivalue rsi, IEnumerable<NewRSIItem> parts)
        {
            // Characters with a missing legacy RSI row can still log in with the previous preview.
            if (rsi == null)
                return System.Convert.FromHexString("01000000640000007B0000006500000000000000000000006C00000000000000");

            bool female = rsi.Sex == 1;
            ushort gender = female ? (ushort)1 : (ushort)0;
            ushort[] facialDetails = female ? FemaleFacialDetails : MaleFacialDetails;
            uint Descriptor(string type, short ordinal, ushort fallback) =>
                parts.FirstOrDefault(part => part.type == type && part.gender == gender && part.internalId == ordinal)?.newRSIID ?? fallback;

            // Returning Margin 02 starts with eight u32 fields, followed by the existing trait.
            // The Loading Area model resolves equivalent descriptor IDs instead of packed world RSI ordinals.
            var preview = new PacketContent();
            preview.AddUint32(Bounded(rsi.Skintone, 31), 1);
            preview.AddUint32(Descriptor("BODY", rsi.Body, female ? (ushort)105 : (ushort)106), 1);
            preview.AddUint32(Descriptor("HEAD", rsi.Face, female ? (ushort)100 : (ushort)103), 1);
            preview.AddUint32(Descriptor("HAIR", rsi.Hair, female ? (ushort)100 : (ushort)105), 1);
            preview.AddUint32(Bounded(rsi.Haircolor, 31), 1);
            preview.AddUint32(Bounded(rsi.Tattoo, 7), 1);
            preview.AddUint32(facialDetails[rsi.Facialdetail >= 0 && rsi.Facialdetail < facialDetails.Length ? rsi.Facialdetail : 0], 1);
            preview.AddUint32(Bounded(rsi.Facialdetailcolor, 7), 1);
            return preview.ReturnFinalPacket();
        }

        private static uint Bounded(short value, uint max) => value >= 0 && value <= max ? (uint)value : 0;
    }
}
