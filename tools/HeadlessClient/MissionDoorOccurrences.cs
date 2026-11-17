using System.Text.Json;

namespace HeadlessClient;

public static class MissionDoorOccurrences
{
    // A portal's two faces share a key; its declared schema can belong to either adjacent room.
    public static JsonElement[] ForPortal(JsonElement group,ushort goid,int? fromRegion,int? toRegion) =>
        group.GetProperty("Occurrences").EnumerateArray().Where(o=>o.GetProperty("Goid").GetUInt16()==goid &&
            (!fromRegion.HasValue || o.GetProperty("Region").GetInt32()==fromRegion || o.GetProperty("Region").GetInt32()==toRegion)).ToArray();
}
