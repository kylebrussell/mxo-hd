using System.Security.Cryptography;
using System.Text.Json;
using hds;
namespace hds.Tests;
public class ScaleMovementEvidenceTests
{
    private static string Root
    {
        get {string? p=AppContext.BaseDirectory;while(p!=null&&!Directory.Exists(Path.Combine(p,"research","scale","2026-11-25")))p=Path.GetDirectoryName(p);return p??throw new InvalidOperationException("Movement evidence missing");}
    }
    private static string Data(string run,string file)=>Path.Combine(Root,"research","scale","2026-11-25",run,file);
    [Fact]
    public void RawEvidenceBytesMatchPublishedManifest()
    {
        var manifest=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(Path.Combine(Root,"research","scale","2026-11-25","SHA256.json")))!;
        Assert.Equal(30,manifest.Count);
        foreach(var pair in manifest)Assert.Equal(pair.Value,Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(Root,"research","scale","2026-11-25",pair.Key)))));
    }
    [Theory]
    [InlineData("after-idle-200","PASS")][InlineData("after-move-50","PASS")][InlineData("after-move-100","PASS")]
    [InlineData("after-move-200","INCOMPLETE")][InlineData("after-move-worldmobs-200","INCOMPLETE")]
    public void PublishedVisibilityAndStageSummariesRecomputeFromRawWindows(string run,string status)
    {
        using var summary=JsonDocument.Parse(File.ReadAllText(Data(run,"summary.json")));var s=summary.RootElement;
        Assert.Equal(status,s.GetProperty("Status").GetString());
        DateTime begin=s.GetProperty("MeasurementStartUtc").GetDateTime(),end=s.GetProperty("MeasurementEndUtc").GetDateTime();
        var windows=File.ReadLines(Data(run,"server-metrics.ndjson")).Where(l=>!string.IsNullOrWhiteSpace(l)).Select(ScaleDecoder.Parse).Where(w=>w.StartUtc>=begin&&w.EndUtc<=end).ToArray();
        Assert.NotEmpty(windows);Assert.All(windows,w=>Assert.Equal(s.GetProperty("Requested").GetInt32(),w.ActiveClients));
        using var recomputed=JsonDocument.Parse(JsonSerializer.Serialize(ScaleDecoder.Summarize(windows)));
        var m=s.GetProperty("Metrics");var r=recomputed.RootElement;
        Assert.Equal(m.GetProperty("Visibility").GetProperty("SumMs").GetDouble(),r.GetProperty("Visibility").GetProperty("SumMs").GetDouble(),6);
        foreach(var stage in m.GetProperty("Stages").EnumerateObject())
        {
            Assert.Equal(stage.Value.GetProperty("Count").GetInt64(),r.GetProperty("Stages").GetProperty(stage.Name).GetProperty("Count").GetInt64());
            Assert.Equal(stage.Value.GetProperty("SumMs").GetDouble(),r.GetProperty("Stages").GetProperty(stage.Name).GetProperty("SumMs").GetDouble(),6);
        }
    }
    [Fact]
    public void BroadcastLockEvidenceMatchesItsManifestAndTheFixedRunsPass()
    {
        string folder=Path.Combine(Root,"research","scale","2026-11-25-broadcast-lock");
        var manifest=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(Path.Combine(folder,"SHA256.json")))!;
        Assert.Equal(20,manifest.Count);
        foreach(var pair in manifest)Assert.Equal(pair.Value,Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(folder,pair.Key)))));
        foreach(string run in new[]{"fix-move-100","fix-move-200"})
        {
            using var summary=JsonDocument.Parse(File.ReadAllText(Path.Combine(folder,run,"summary.json")));
            Assert.Equal("PASS",summary.RootElement.GetProperty("Status").GetString());
            Assert.Empty(summary.RootElement.GetProperty("Failures").EnumerateArray());
        }
    }
    [Fact]
    public void TheUnfixedMovingRunEndedWithoutAnyMeasuredWindow()
    {
        using var summary=JsonDocument.Parse(File.ReadAllText(Data("before-move-200","summary.json")));
        Assert.Equal("INCOMPLETE",summary.RootElement.GetProperty("Status").GetString());
        Assert.Equal(0,summary.RootElement.GetProperty("Metrics").GetProperty("Windows").GetInt32());
    }
}
