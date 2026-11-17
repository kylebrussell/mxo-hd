using System.Text.Json;

namespace HeadlessClient;

public static class MissionDoorOccurrences
{
    // Internal-door requests use DataLoader's first CSV row, even when the other face has another schema.
    public static JsonElement OriginalLegacy(JsonElement group) =>
        group.GetProperty("Legacy").EnumerateArray()
            .OrderBy(row=>row.GetProperty("File").GetString(),StringComparer.Ordinal)
            .ThenBy(row=>row.GetProperty("Line").GetInt32()).First();

    // A portal's two faces share a key; its declared schema can belong to either adjacent room.
    public static JsonElement[] ForPortal(JsonElement group,ushort goid,int? fromRegion,int? toRegion) =>
        group.GetProperty("Occurrences").EnumerateArray().Where(o=>o.GetProperty("Goid").GetUInt16()==goid &&
            (!fromRegion.HasValue || o.GetProperty("Region").GetInt32()==fromRegion || o.GetProperty("Region").GetInt32()==toRegion)).ToArray();
}
