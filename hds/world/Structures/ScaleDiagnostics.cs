using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using hds.shared;
namespace hds
{
    /// <summary>Bounded aggregate diagnostics, enabled only by explicit opt-in on an actual disposable configuration.</summary>
    public static class ScaleDiagnostics
    {
        public const string Variable="MXO_SCALE_METRICS";
        public static bool Allowed(string opt,string dev,string database)=>opt=="1" && dev=="1" &&
            database!=null && database.StartsWith("mxotest_",StringComparison.Ordinal) && database.Length>8 &&
            database.All(c=>char.IsAsciiLetterOrDigit(c)||c=='_');
        private static readonly Lazy<bool> enabled=new Lazy<bool>(()=>Allowed(Environment.GetEnvironmentVariable(Variable),
            Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS"),Store.config?.dbParams?.DatabaseName));
        public static bool Enabled=>enabled.Value;
        public static long Start=>Enabled?Stopwatch.GetTimestamp():0;
        private static readonly object gate=new object();
        private static ScaleHistogram ticks=new ScaleHistogram(),flushes=new ScaleHistogram(),rpc=new ScaleHistogram(),objects=new ScaleHistogram();
        private static DateTime windowStart=DateTime.UtcNow;
        public static double ElapsedMs(long start)=>Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        public static void FirstSend(long start,bool objectMessage)
        { if(start==0||!Enabled)return;lock(gate)(objectMessage?objects:rpc).Record(ElapsedMs(start)); }
        public static void FlushCompleted(long start)
        { if(start==0||!Enabled)return;lock(gate)flushes.Record(ElapsedMs(start)); }
        public static void VisibilityCompleted(long start,IEnumerable<WorldClient> clients)
        {
            if(start==0||!Enabled)return;
            double elapsed=ElapsedMs(start);DateTime now=DateTime.UtcNow;ScaleWindow result;
            lock(gate)
            {
                ticks.Record(elapsed);if((now-windowStart).TotalSeconds<1)return;
                result=new ScaleWindow{StartUtc=windowStart,EndUtc=now,Visibility=ticks.Snapshot(),FlushProcessing=flushes.Snapshot(),RpcFirstSend=rpc.Snapshot(),ObjectFirstSend=objects.Snapshot()};
                ticks=new ScaleHistogram();flushes=new ScaleHistogram();rpc=new ScaleHistogram();objects=new ScaleHistogram();windowStart=now;
            }
            foreach(var client in clients)
            {
                if(client==null||client.IsBot||client.messageQueue==null)continue;
                if(client.Alive&&client.playerData.getOnWorld())result.ActiveClients++;
                var queue=client.messageQueue;
                // Independent existing queue locks: no nested RPC/object or network lock and no I/O inside them.
                lock(queue.RPCMessagesQueue.SyncRoot)
                {result.RpcRetained+=queue.RPCMessagesQueue.Count;foreach(SequencedMessage m in queue.RPCMessagesQueue)if(m.rpcNumber<0)result.RpcUnsent++;}
                lock(queue.ObjectMessagesQueue.SyncRoot)
                {result.ObjectRetained+=queue.ObjectMessagesQueue.Count;foreach(SequencedMessage m in queue.ObjectMessagesQueue)if(m.sendCounter==0)result.ObjectUnsent++;}
            }
            Output.WriteLine("[SCALE] "+JsonSerializer.Serialize(result));
        }
    }
    public sealed class ScaleWindow
    {
        public DateTime StartUtc{get;set;}public DateTime EndUtc{get;set;}
        public int ActiveClients{get;set;}public int RpcRetained{get;set;}public int RpcUnsent{get;set;}public int ObjectRetained{get;set;}public int ObjectUnsent{get;set;}
        public ScaleMetric Visibility{get;set;}public ScaleMetric FlushProcessing{get;set;}public ScaleMetric RpcFirstSend{get;set;}public ScaleMetric ObjectFirstSend{get;set;}
    }
    public sealed class ScaleMetric
    {
        public long Count{get;set;}public double SumMs{get;set;}public double? MeanMs{get;set;}public double? MaxMs{get;set;}
        public long OverflowCount{get;set;}public double? P50UpperMs{get;set;}public double? P95UpperMs{get;set;}public long[] Bins{get;set;}
    }
    public sealed class ScaleHistogram
    {
        private static readonly double[] bounds={.125,.25,.5,1,2,4,8,16,32,64,128,256,512,1000,2000,5000,10000,30000};
        public static IReadOnlyList<double> Bounds {get;}=Array.AsReadOnly(bounds);
        private readonly long[] bins=new long[bounds.Length+1];private long count;private double sum,max;
        public void Record(double ms)
        {if(!double.IsFinite(ms)||ms<0)throw new ArgumentOutOfRangeException(nameof(ms));int at=Array.FindIndex(bounds,b=>ms<=b);bins[at<0?bounds.Length:at]++;count++;sum+=ms;max=Math.Max(max,ms);}
        public void Merge(ScaleMetric metric)
        {
            if(metric==null||(metric.Count==0&&(metric.SumMs!=0||metric.MaxMs.HasValue))||!double.IsFinite(metric.SumMs)||metric.SumMs<0||(metric.Count>0&&(!metric.MaxMs.HasValue||!double.IsFinite(metric.MaxMs.Value)||metric.MaxMs.Value<0||metric.MaxMs.Value*metric.Count+1e-6*Math.Max(1,metric.SumMs)<metric.SumMs))||metric.Bins==null||metric.Bins.Length!=bins.Length||metric.Count<0||metric.Bins.Any(v=>v<0)||metric.Bins.Sum()!=metric.Count)throw new ArgumentException("Invalid histogram");
            for(int i=0;i<bins.Length;i++)bins[i]+=metric.Bins[i];count+=metric.Count;sum+=metric.SumMs;max=Math.Max(max,metric.MaxMs??0);
        }
        public ScaleMetric Snapshot()=>new ScaleMetric{Count=count,SumMs=sum,MeanMs=count==0?(double?)null:sum/count,MaxMs=count==0?(double?)null:max,P50UpperMs=Percentile(.50),P95UpperMs=Percentile(.95),OverflowCount=bins[bins.Length-1],Bins=(long[])bins.Clone()};
        private double? Percentile(double fraction)
        {if(count==0)return null;long threshold=(long)Math.Ceiling(count*fraction),total=0;for(int i=0;i<bins.Length;i++){total+=bins[i];if(total>=threshold)return i<bounds.Length?bounds[i]:(double?)null;}return null;}
    }
}
