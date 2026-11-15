using System.Text;
using System.Text.Json;
using hds;

namespace HeadlessClient;

/// <summary>Read opaque stored JSON through HEX, avoiding mysql batch escaping of backslashes.</summary>
public static class ZionAfter1212CheckpointPayload
{
    public static string? Raw(string? hex) => string.IsNullOrEmpty(hex) || hex == "NULL" ? null :
        new UTF8Encoding(false,true).GetString(Convert.FromHexString(hex));

    public static MissionProgress.SavedState? Read(string? hex)
    {
        string? raw=Raw(hex);
        return raw==null?null:JsonSerializer.Deserialize<MissionProgress.SavedState>(raw,new JsonSerializerOptions{IncludeFields=true});
    }
}
