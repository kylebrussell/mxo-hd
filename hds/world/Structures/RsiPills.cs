using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace hds
{
    /// <summary>Owned appearance consumables from the shipped 1245 item templates.</summary>
    internal static class RsiPills
    {
        internal const ushort Ability = 1245;

        // Positions in the saved RSI array that PacketsUtils packs into the world view.
        internal enum Part { Body = 1, Face = 3, Hair = 10, Facialdetail = 11, Haircolor = 17,
            Skintone = 18, Tattoo = 19, Facialdetailcolor = 20 }

        internal readonly struct Pill
        {
            internal readonly Part Field;
            internal readonly ushort Descriptor;
            internal readonly ushort Sex;

            internal Pill(Part field, ushort descriptor, ushort sex)
            {
                Field = field;
                Descriptor = descriptor;
                Sex = sex;
            }

            internal int Resolve(int sex, IEnumerable<NewRSIItem> parts)
            {
                if (sex != Sex) return -1;
                switch (Field)
                {
                    case Part.Body:
                    case Part.Face:
                    case Part.Hair:
                        string type = Field == Part.Body ? "BODY" : Field == Part.Face ? "HEAD" : "HAIR";
                        foreach (var part in parts)
                            if (part.type == type && part.gender == Sex && part.newRSIID == Descriptor)
                                return part.internalId;
                        return -1;
                    case Part.Facialdetail:
                        return CharacterAppearance.FacialDetailOrdinal(Descriptor, Sex == 1);
                    case Part.Haircolor:
                    case Part.Skintone:
                        return Descriptor <= 31 ? Descriptor : -1;
                    case Part.Tattoo:
                    case Part.Facialdetailcolor:
                        return Descriptor <= 7 ? Descriptor : -1;
                    default:
                        return -1;
                }
            }
        }

        private static readonly Lazy<Dictionary<uint, Pill>> items = new Lazy<Dictionary<uint, Pill>>(() =>
        {
            using var json = JsonDocument.Parse(File.ReadAllText(DataLoader.DataPath("rsi_pills.json")));
            return json.RootElement.GetProperty("items").EnumerateArray().ToDictionary(row => row[0].GetUInt32(),
                row => new Pill(ParsePart(row[1].GetString()), row[2].GetUInt16(), row[3].GetUInt16()));
        });

        private static Part ParsePart(string call) => call switch
        {
            "SetBodyType" => Part.Body,
            "SetHeadType" => Part.Face,
            "SetHairType" => Part.Hair,
            "SetFacialHairType" => Part.Facialdetail,
            "SetHairColor" => Part.Haircolor,
            "SetSkinTone" => Part.Skintone,
            "SetTattooType" => Part.Tattoo,
            "SetFacialHairColor" => Part.Facialdetailcolor,
            _ => throw new InvalidDataException("Unknown RSI pill callback: " + call)
        };

        internal static bool TryGet(uint goid, out Pill pill)
        {
            pill = default;
            return goid >= 46739 && goid <= 46973 && items.Value.TryGetValue(goid, out pill);
        }
    }
}
