using hds;
using System.Text.Json;
namespace hds.Tests;
public class ScaleDiagnosticsTests
{
    [Theory]
    [InlineData("1","1","mxotest_run_case",true)]
    [InlineData("true","1","mxotest_run_case",false)][InlineData("1","true","mxotest_run_case",false)]
    [InlineData("1","1","reality_hd",false)][InlineData("1","1","mxotest_",false)]
    [InlineData("1","1","mxotest_case-other",false)][InlineData("1","1","MXOTEST_case",false)]
    public void OnlyExactOptInAndActualDisposableConfigurationEnableMetrics(string opt,string dev,string database,bool allowed)=>Assert.Equal(allowed,ScaleDiagnostics.Allowed(opt,dev,database));
    [Fact]
    public void EmptyMetricsRemainUnavailableRatherThanFakeZeros()
    {
        var s=new ScaleHistogram().Snapshot();Assert.Equal(0,s.Count);Assert.Null(s.MeanMs);Assert.Null(s.MaxMs);Assert.Null(s.P50UpperMs);Assert.Null(s.P95UpperMs);
    }
    [Fact]
    public void NearestRankUpperBoundsAndOverflowKeepExactCountMeanAndMax()
    {
        var h=new ScaleHistogram();foreach(double x in new[]{.1,.2,.3,40000d})h.Record(x);
        var s=h.Snapshot();Assert.Equal(4,s.Count);Assert.Equal(.25,s.P50UpperMs);Assert.Null(s.P95UpperMs);Assert.Equal(40000d,s.MaxMs);Assert.Equal(1,s.OverflowCount);Assert.Equal(10000.15,s.MeanMs!.Value,5);
        var merged=new ScaleHistogram();merged.Merge(s);merged.Merge(s);var m=merged.Snapshot();Assert.Equal(8,m.Count);Assert.Equal(s.MeanMs,m.MeanMs);Assert.Equal(s.P50UpperMs,m.P50UpperMs);
        s.Bins[0]=999;Assert.Equal(4,h.Snapshot().Count);
    }
    [Fact]
    public void InvalidHistogramCannotSilentlyBecomeAValidBenchmark()
    {
        var h=new ScaleHistogram();Assert.Throws<ArgumentOutOfRangeException>(()=>h.Record(double.NaN));
        var s=h.Snapshot();s.Count=1;Assert.Throws<ArgumentException>(()=>h.Merge(s));
    }
    [Fact]
    public void DecoderRequiresAllMetricsAndValidQueues()
    {
        var e=new ScaleHistogram().Snapshot();var w=new ScaleWindow{StartUtc=DateTime.UtcNow,EndUtc=DateTime.UtcNow.AddSeconds(1),ActiveClients=5,RpcRetained=2,RpcUnsent=1,Visibility=e,FlushProcessing=e,RpcFirstSend=e,ObjectFirstSend=e};
        Assert.Equal(5,ScaleDecoder.Parse("[SCALE] "+JsonSerializer.Serialize(w)).ActiveClients);
        w.RpcUnsent=3;Assert.Throws<FormatException>(()=>ScaleDecoder.Parse("[SCALE] "+JsonSerializer.Serialize(w)));
        Assert.Throws<FormatException>(()=>ScaleDecoder.Parse("unrelated output"));
    }
    [Fact]
    public void StaticVisibilityIndexKeepsOnlyBoothsAndListedSignposts()
    {
        static StaticWorldObject Of(ushort type,uint id)=>new StaticWorldObject{type=NumericalUtils.uint16ToByteArray(type,1),staticId=id};
        var listed=new HashSet<uint>{7};
        Assert.True(WorldThreads.InStaticVisibilityIndex(Of(HardlineBooths.PhoneBoothType,1),listed));
        Assert.True(WorldThreads.InStaticVisibilityIndex(Of(8400,7),listed));
        Assert.False(WorldThreads.InStaticVisibilityIndex(Of(8400,8),listed));
        Assert.False(WorldThreads.InStaticVisibilityIndex(Of(6568,7),listed));
    }
    [Fact]
    public void StageDurationsAreValidatedAndMergedAcrossWindows()
    {
        var e=new ScaleHistogram().Snapshot();
        ScaleWindow Window(double ms){var h=new ScaleHistogram();h.Record(ms);return new ScaleWindow{StartUtc=DateTime.UtcNow,EndUtc=DateTime.UtcNow.AddSeconds(1),ActiveClients=1,
            Visibility=e,FlushProcessing=e,RpcFirstSend=e,ObjectFirstSend=e,Stages=new Dictionary<string,ScaleMetric>{["player-views"]=h.Snapshot()}};}
        var a=ScaleDecoder.Parse("[SCALE] "+JsonSerializer.Serialize(Window(3)));var b=ScaleDecoder.Parse("[SCALE] "+JsonSerializer.Serialize(Window(9)));
        var old=ScaleDecoder.Parse("[SCALE] "+JsonSerializer.Serialize(new ScaleWindow{StartUtc=DateTime.UtcNow,EndUtc=DateTime.UtcNow.AddSeconds(1),Visibility=e,FlushProcessing=e,RpcFirstSend=e,ObjectFirstSend=e}));
        Assert.Null(old.Stages);
        using var json=JsonDocument.Parse(JsonSerializer.Serialize(ScaleDecoder.Summarize(new[]{a,b,old})));
        var stage=json.RootElement.GetProperty("Stages").GetProperty("player-views");
        Assert.Equal(2,stage.GetProperty("Count").GetInt64());Assert.Equal(6,stage.GetProperty("MeanMs").GetDouble(),6);Assert.Equal(9,stage.GetProperty("MaxMs").GetDouble(),6);
        var bad=Window(3);bad.Stages["player-views"].Count=5;
        Assert.Throws<ArgumentException>(()=>ScaleDecoder.Parse("[SCALE] "+JsonSerializer.Serialize(bad)));
        Assert.Equal(0,ScaleDiagnostics.Stage("disabled",0));
    }
    [Theory]
    [InlineData("201")][InlineData("0")]
    public void AuthenticatedCapacityBoundIsExplicit(string clients)=>Assert.Throws<ArgumentException>(()=>ScaleOptions.Parse(new[]{"--authenticated","--clients",clients}));
    [Fact]
    public void CpuAggregationWeightsActualIntervals()
    {
        var samples=new[]{new ScaleCpuSample(DateTime.UtcNow,1,100,0,20,1000,2000),new ScaleCpuSample(DateTime.UtcNow,3,0,0,10,3000,2000)};
        using var json=JsonDocument.Parse(JsonSerializer.Serialize(ScaleDecoder.SummarizeCpu(samples)));
        Assert.Equal(100d/3,json.RootElement.GetProperty("ServerCpuCorePercent").GetDouble(),6);
        Assert.Equal(3000,json.RootElement.GetProperty("MaxSampledServerWorkingSetBytes").GetInt64());
        using var empty=JsonDocument.Parse(JsonSerializer.Serialize(ScaleDecoder.SummarizeCpu(Array.Empty<ScaleCpuSample>())));
        Assert.Equal(JsonValueKind.Null,empty.RootElement.GetProperty("ServerCpuCorePercent").ValueKind);
    }
    [Fact]
    public void ReadOnlyPlanCannotTargetAnotherServer()
    {
        var o=ScaleOptions.Parse(new[]{"--authenticated","--clients","200","--plan"});Assert.True(o.Plan);Assert.Equal(200,o.Clients);Assert.Equal(30,o.WarmupSeconds);Assert.Equal(60,o.MeasureSeconds);
        Assert.Throws<ArgumentException>(()=>ScaleOptions.Parse(new[]{"--authenticated","--host","127.0.0.1"}));
    }
}
