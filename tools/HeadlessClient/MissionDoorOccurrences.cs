using System.Text.Json;

namespace HeadlessClient;

public static class MissionDoorOccurrences
{
    // Internal-door requests use DataLoader's first CSV row, even when the other face has another schema.
    public static JsonElement OriginalLegacy(JsonElement group) =>
        group.GetProperty("Legacy").EnumerateArray()
            .OrderBy(row=>row.GetProperty("File").GetString(),StringComparer.Ordinal)
            .ThenBy(row=>row.GetProperty("Line").GetInt32()).First();

    // Select the face in the room being crossed; some schemas exist only on the adjacent face.
    public static JsonElement[] ForPortal(JsonElement group,ushort goid,int? fromRegion,int? toRegion)
    {
        var own=group.GetProperty("Occurrences").EnumerateArray().Where(o=>o.GetProperty("Goid").GetUInt16()==goid).ToArray();
        if(!fromRegion.HasValue)return own;
        var source=own.Where(o=>o.GetProperty("Region").GetInt32()==fromRegion).ToArray();
        return source.Length>0?source:own.Where(o=>o.GetProperty("Region").GetInt32()==toRegion).ToArray();
    }
}
