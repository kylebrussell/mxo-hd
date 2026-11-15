using System.Security.Cryptography;
using System.Text.Json;
using hds;
namespace hds.Tests;
public class ScaleBaselineEvidenceTests
{
    private static string Root
    {
        get {string? p=AppContext.BaseDirectory;while(p!=null&&!Directory.Exists(Path.Combine(p,"research","scale","2026-11-12")))p=Path.GetDirectoryName(p);return p??throw new InvalidOperationException("Benchmark evidence missing");}
    }
    private static string Data(int clients,string file)=>Path.Combine(Root,"research","scale","2026-11-12",clients.ToString(),file);
    [Fact]
    public void RawEvidenceBytesMatchPublishedManifest()
    {
        var manifest=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(Path.Combine(Root,"research","scale","2026-11-12","SHA256.json")))!;
        Assert.Equal(18,manifest.Count);
        foreach(var pair in manifest)Assert.Equal(pair.Value,Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(Root,pair.Key)))).ToLowerInvariant());
    }
    [Theory]
    [InlineData(50)][InlineData(100)][InlineData(200)]
    public void PublishedSummaryRecomputesFromFrozenRawWindowsCpuAndRtts(int clients)
    {
        using var summary=JsonDocument.Parse(File.ReadAllText(Data(clients,"summary.json")));var s=summary.RootElement;
        Assert.Equal("PASS",s.GetProperty("Status").GetString());Assert.Equal(clients,s.GetProperty("Entered").GetInt32());Assert.Equal("0",File.ReadAllText(Data(clients,"exit-code.txt")).Trim());
        Assert.Equal("ab4c0a4125508d17810b1478e05cb67d55774846",s.GetProperty("GitHead").GetString());Assert.False(s.GetProperty("GitDirty").GetBoolean());
        Assert.Equal("669066860147C1CD3DABB2F835EBDBAC939ED8297F17311317C672E098285BA7",s.GetProperty("SourceAssemblySha256").GetString());
        Assert.Empty(s.GetProperty("Failures").EnumerateArray());Assert.Empty(s.GetProperty("ProtocolProblems").EnumerateArray());Assert.Empty(s.GetProperty("ServerProblems").EnumerateArray());
        DateTime begin=s.GetProperty("MeasurementStartUtc").GetDateTime(),end=s.GetProperty("MeasurementEndUtc").GetDateTime();
        var windows=File.ReadLines(Data(clients,"server-metrics.ndjson")).Where(line=>!string.IsNullOrWhiteSpace(line)).Select(ScaleDecoder.Parse).Where(w=>w.StartUtc>=begin&&w.EndUtc<=end).ToArray();
        Assert.All(windows,w=>Assert.Equal(clients,w.ActiveClients));
        using var recomputed=JsonDocument.Parse(JsonSerializer.Serialize(ScaleDecoder.Summarize(windows)));var m=s.GetProperty("Metrics");
        Assert.Equal(m.GetProperty("Windows").GetInt32(),windows.Length);
        Assert.Equal(m.GetProperty("CoveredSeconds").GetDouble(),recomputed.RootElement.GetProperty("CoveredSeconds").GetDouble(),7);
        foreach(string field in new[]{"Visibility","FlushProcessing","RpcFirstSend","ObjectFirstSend"})CompareMetric(m.GetProperty(field),recomputed.RootElement.GetProperty(field));
        var rtt=new ScaleHistogram();foreach(double value in JsonSerializer.Deserialize<double[]>(File.ReadAllText(Data(clients,"rtt-ms.json")))!)rtt.Record(value);
        using var parsedRtt=JsonDocument.Parse(JsonSerializer.Serialize(rtt.Snapshot()));CompareMetric(s.GetProperty("RoundTrip"),parsedRtt.RootElement);
        var cpu=JsonSerializer.Deserialize<ScaleCpuSample[]>(File.ReadAllText(Data(clients,"cpu-samples.json")))!;
        using var recomputedCpu=JsonDocument.Parse(JsonSerializer.Serialize(ScaleDecoder.SummarizeCpu(cpu)));var c=s.GetProperty("Cpu");
        Assert.Equal(c.GetProperty("ServerCpuCorePercent").GetDouble(),recomputedCpu.RootElement.GetProperty("ServerCpuCorePercent").GetDouble(),7);
        using var plan=JsonDocument.Parse(File.ReadAllText(Data(clients,"plan.json")));
        Assert.Equal(c.GetProperty("ServerCpuHostPercent").GetDouble(),c.GetProperty("ServerCpuCorePercent").GetDouble()/plan.RootElement.GetProperty("LogicalProcessors").GetInt32(),7);
        Assert.Equal(0,m.GetProperty("ObjectFirstSend").GetProperty("Count").GetInt64());Assert.Equal(JsonValueKind.Null,m.GetProperty("ObjectFirstSend").GetProperty("MeanMs").ValueKind);
    }
    private static void CompareMetric(JsonElement a,JsonElement b)
    {
        Assert.Equal(a.GetProperty("Count").GetInt64(),b.GetProperty("Count").GetInt64());
        Assert.Equal(a.GetProperty("SumMs").GetDouble(),b.GetProperty("SumMs").GetDouble(),7);
        foreach(string field in new[]{"MeanMs","MaxMs","P50UpperMs","P95UpperMs"})
        {if(a.GetProperty(field).ValueKind==JsonValueKind.Null)Assert.Equal(JsonValueKind.Null,b.GetProperty(field).ValueKind);else Assert.Equal(a.GetProperty(field).GetDouble(),b.GetProperty(field).GetDouble(),7);}
    }
}
