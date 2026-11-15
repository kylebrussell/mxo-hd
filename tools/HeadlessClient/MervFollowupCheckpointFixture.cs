using System.Text;
using System.Text.Json;
using hds;
namespace HeadlessClient;

/// <summary>Lossless owned checkpoint reads through mysql batch output; never unescapes JSON text.</summary>
public static class MervFollowupCheckpointFixture
{
    private static readonly UTF8Encoding StrictUtf8=new(false,true);
    private static readonly JsonSerializerOptions Fields=new(){IncludeFields=true};
    public static string? DecodeHex(string? hex)=>string.IsNullOrWhiteSpace(hex)?null:StrictUtf8.GetString(Convert.FromHexString(hex));
    public static MissionProgress.SavedState? ParseHex(string? hex)
    {
        string? raw=DecodeHex(hex);
        return raw==null?null:JsonSerializer.Deserialize<MissionProgress.SavedState>(raw,Fields);
    }
}
