using System.Diagnostics;
using System.Text.Json;
using System.Security.Cryptography;
using HeadlessClient;
using hds;

public sealed record ScaleOptions(string Root,string Output,int Clients,int LoginParallel,int WarmupSeconds,int MeasureSeconds,int RampTimeoutSeconds,bool Plan)
{
    public static ScaleOptions Parse(string[] args)
    {
        string root=Directory.GetCurrentDirectory(),output=Path.Combine(Path.GetTempPath(),"mxo-scale",DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ")+"-"+Guid.NewGuid().ToString("N")[..6]);
        int clients=50,parallel=4,warmup=30,measure=60,ramp=120;bool plan=false;
        for(int i=0;i<args.Length;i++)
        {
            string Value()=>i+1<args.Length?args[++i]:throw new ArgumentException("Missing option value");
            switch(args[i])
            {
                case "--authenticated":break;case "--plan":plan=true;break;
                case "--root":root=Path.GetFullPath(Value());break;case "--output":output=Path.GetFullPath(Value());break;
                case "--clients":clients=int.Parse(Value());break;case "--login-parallel":parallel=int.Parse(Value());break;
                case "--warmup-seconds":warmup=int.Parse(Value());break;case "--measure-seconds":measure=int.Parse(Value());break;case "--ramp-timeout-seconds":ramp=int.Parse(Value());break;
                default:throw new ArgumentException("Unknown authenticated-load option: "+args[i]);
            }
        }
        if(clients<1||clients>200||parallel<1||parallel>8||warmup<0||warmup>120||measure<1||measure>300||ramp<1||ramp>300)throw new ArgumentException("Load bounds exceeded");
        return new(root,output,clients,parallel,warmup,measure,ramp,plan);
    }
}
public sealed record ScaleCpuSample(DateTime Utc,double ElapsedSeconds,double ServerCpuCorePercent,double ServerCpuHostPercent,double GeneratorCpuCorePercent,long ServerWorkingSetBytes,long GeneratorWorkingSetBytes);
public static class ScaleDecoder
{
    public static ScaleWindow Parse(string line)
    {
        const string prefix="[SCALE] ";if(!line.StartsWith(prefix,StringComparison.Ordinal))throw new FormatException("Not a scale aggregate");
        var item=JsonSerializer.Deserialize<ScaleWindow>(line[prefix.Length..])??throw new FormatException("Empty aggregate");
        if(item.EndUtc<=item.StartUtc||item.ActiveClients<0||item.RpcRetained<item.RpcUnsent||item.ObjectRetained<item.ObjectUnsent||item.RpcUnsent<0||item.ObjectUnsent<0)throw new FormatException("Invalid aggregate bounds");
        foreach(var metric in new[]{item.Visibility,item.FlushProcessing,item.RpcFirstSend,item.ObjectFirstSend})
        {var validate=new ScaleHistogram();validate.Merge(metric??throw new FormatException("Missing metric"));}
        return item;
    }
    public static object SummarizeCpu(IReadOnlyList<ScaleCpuSample> samples)
    {
        double before=0,server=0,generator=0;
        foreach(var s in samples){double delta=s.ElapsedSeconds-before;if(delta<=0)throw new ArgumentException("Non-increasing CPU sample time");server+=s.ServerCpuCorePercent*delta;generator+=s.GeneratorCpuCorePercent*delta;before=s.ElapsedSeconds;}
        return new {Samples=samples.Count,CoveredSeconds=before,ServerCpuCorePercent=before==0?(double?)null:server/before,
            ServerCpuHostPercent=before==0?(double?)null:server/before/Environment.ProcessorCount,GeneratorCpuCorePercent=before==0?(double?)null:generator/before,
            MaxSampledServerWorkingSetBytes=samples.Count==0?(long?)null:samples.Max(s=>s.ServerWorkingSetBytes),MaxSampledGeneratorWorkingSetBytes=samples.Count==0?(long?)null:samples.Max(s=>s.GeneratorWorkingSetBytes)};
    }
    public static object Summarize(IReadOnlyList<ScaleWindow> windows)
    {
        ScaleMetric Merge(Func<ScaleWindow,ScaleMetric> get){var h=new ScaleHistogram();foreach(var w in windows)h.Merge(get(w));return h.Snapshot();}
        return new {Windows=windows.Count,CoveredSeconds=windows.Sum(w=>(w.EndUtc-w.StartUtc).TotalSeconds),
            MinActiveClients=windows.Count==0?(int?)null:windows.Min(w=>w.ActiveClients),
            RpcRetainedMax=windows.Count==0?(int?)null:windows.Max(w=>w.RpcRetained),RpcUnsentMax=windows.Count==0?(int?)null:windows.Max(w=>w.RpcUnsent),
            ObjectRetainedMax=windows.Count==0?(int?)null:windows.Max(w=>w.ObjectRetained),ObjectUnsentMax=windows.Count==0?(int?)null:windows.Max(w=>w.ObjectUnsent),
            Visibility=Merge(w=>w.Visibility),FlushProcessing=Merge(w=>w.FlushProcessing),RpcFirstSend=Merge(w=>w.RpcFirstSend),ObjectFirstSend=Merge(w=>w.ObjectFirstSend)};
    }
}
public static class AuthenticatedScale
{
    public static async Task<int> RunAsync(string[] args)
    {
        var o=ScaleOptions.Parse(args);
        if(o.Plan){Console.WriteLine(JsonSerializer.Serialize(o,new JsonSerializerOptions{WriteIndented=true}));return 0;}
        if(!File.Exists(Path.Combine(o.Root,"SQL","reality_hd.sql")))throw new ArgumentException("Repository SQL fixture not found");
        string expected=Path.GetFullPath(Path.Combine(o.Root,"hds","bin","Release","net8.0","hds.dll"));
        string? newest=Directory.EnumerateFiles(Path.Combine(o.Root,"hds","bin"),"hds.dll",SearchOption.AllDirectories)
            .Where(p=>File.Exists(Path.ChangeExtension(p,"runtimeconfig.json"))).OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
        if(newest==null||!Path.GetFullPath(newest).Equals(expected,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Authenticated baseline requires the newest server DLL to be Release. Rebuild Release after other configurations; no server was started.");
        string Git(string argument)
        {
            var si=new ProcessStartInfo("git"){WorkingDirectory=o.Root,RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false};foreach(string word in argument.Split(' '))si.ArgumentList.Add(word);
            using var p=Process.Start(si)!;string output=p.StandardOutput.ReadToEnd();p.WaitForExit();if(p.ExitCode!=0)throw new InvalidOperationException("Cannot record source provenance");return output.Trim();
        }
        string gitHead=Git("rev-parse HEAD"),gitStatus=Git("status --porcelain");
        Directory.CreateDirectory(o.Output);
        var environment=new Dictionary<string,string>{{"MXO_MOB_WANDER","0"},{"MXO_AGGRO","0"},{"MXO_MOBS","test"},{"MXO_DEBUG_LOG","0"},{"MXO_PACKET_LOG","0"},{"MXO_RPC_LOG","0"},{"MXO_CLIENT_VIEW_LOG","0"},{"MXO_CHAT_DEBUG","0"},{"MXO_DROP_RPC_PACKET","0"},{"MXO_DEV_COMMANDS","1"},{ScaleDiagnostics.Variable,"1"}};
        var entered=new List<HeadlessPlayer>();var failures=new List<object>();var rtts=new List<double>();var samples=new List<ScaleCpuSample>();
        var options=new JsonSerializerOptions{WriteIndented=true};await File.WriteAllTextAsync(Path.Combine(o.Output,"plan.json"),JsonSerializer.Serialize(new {Options=o,GitHead=gitHead,GitDirty=gitStatus.Length>0,Environment=environment,HistogramBoundsMs=ScaleHistogram.Bounds,LogicalProcessors=Environment.ProcessorCount,Framework=System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,OS=System.Runtime.InteropServices.RuntimeInformation.OSDescription},options));
        await using var context=await ScenarioContext.StartAsync(o.Root,"scale"+o.Clients,environment);
        using var server=Process.GetProcessById(context.Server.ProcessId);using var generator=Process.GetCurrentProcess();
        var ramp=Stopwatch.StartNew();using var rampCancel=new CancellationTokenSource(TimeSpan.FromSeconds(o.RampTimeoutSeconds));using var slots=new SemaphoreSlim(o.LoginParallel);
        await Task.WhenAll(Enumerable.Range(0,o.Clients).Select(async i=>
        {
            try
            {
                await slots.WaitAsync(rampCancel.Token);
                try
                {
                    var player=await context.AddPlayerAsync(new CharacterSeed("Scale"+i.ToString("D3")){X=17020+(i%20)*80,Y=495,Z=2693+(i/20)*80});
                    await player.EnterWorldAsync();lock(entered)entered.Add(player);
                }
                finally{slots.Release();}
            }
            catch(Exception e){lock(failures)failures.Add(new{ClientIndex=i,Error=e.GetType().Name,Message=e.Message});}
        }));ramp.Stop();Console.WriteLine($"Entered {entered.Count}/{o.Clients} authenticated clients in {ramp.Elapsed.TotalSeconds:F1}s");
        await Task.Delay(TimeSpan.FromSeconds(o.WarmupSeconds));
        DateTime start=DateTime.UtcNow;var duration=Stopwatch.StartNew();TimeSpan oldServer=server.TotalProcessorTime,oldGenerator=generator.TotalProcessorTime;double oldSeconds=0;
        using var measureCancel=new CancellationTokenSource(TimeSpan.FromSeconds(o.MeasureSeconds));
        var workers=entered.Select((player,index)=>Task.Run(async()=>
        {
            uint sequence=(uint)(index<<16);
            while(!measureCancel.IsCancellationRequested)
            {
                var ping=Stopwatch.StartNew();uint token=++sequence;
                try
                {
                    player.SendRpc("81 4d "+WireFormat.ToHex(BitConverter.GetBytes(token)));
                    await player.WaitForRpcAsync("81 4d "+WireFormat.ToHex(BitConverter.GetBytes(token)),TimeSpan.FromSeconds(5));
                    lock(rtts)rtts.Add(ping.Elapsed.TotalMilliseconds);
                }
                catch(Exception e){lock(failures)failures.Add(new{ClientIndex=index,Error="Ping:"+e.GetType().Name,Message=e.Message});}
                try{await Task.Delay(1000,measureCancel.Token);}catch(OperationCanceledException){}
            }
        })).ToArray();
        while(duration.Elapsed.TotalSeconds<o.MeasureSeconds&&!context.Server.HasExited)
        {
            await Task.Delay(1000);server.Refresh();generator.Refresh();double seconds=duration.Elapsed.TotalSeconds,delta=seconds-oldSeconds;
            var cpu=server.TotalProcessorTime;var own=generator.TotalProcessorTime;
            samples.Add(new(DateTime.UtcNow,seconds,(cpu-oldServer).TotalSeconds/delta*100,(cpu-oldServer).TotalSeconds/delta*100/Environment.ProcessorCount,(own-oldGenerator).TotalSeconds/delta*100,server.WorkingSet64,generator.WorkingSet64));
            oldServer=cpu;oldGenerator=own;oldSeconds=seconds;
        }
        DateTime end=DateTime.UtcNow;measureCancel.Cancel();await Task.WhenAll(workers);
        var lines=context.Server.OutputLines("[SCALE] ").ToArray();var decoded=new List<ScaleWindow>();foreach(string line in lines)
        {try{decoded.Add(ScaleDecoder.Parse(line));}catch(Exception e){failures.Add(new{Phase="MetricsDecode",Error=e.GetType().Name,Message=e.Message});}}
        var contained=decoded.Where(w=>w.StartUtc>=start&&w.EndUtc<=end).ToArray();
        var protocol=context.ClientProblems().ToArray();var serverProblems=context.ServerProblems().ToArray();
        bool complete=entered.Count==o.Clients&&failures.Count==0&&protocol.Length==0&&serverProblems.Length==0&&contained.Length>0&&contained.All(w=>w.ActiveClients==o.Clients);
        var rtt=new ScaleHistogram();foreach(double ms in rtts)rtt.Record(ms);
        await File.WriteAllLinesAsync(Path.Combine(o.Output,"server-metrics.ndjson"),lines);
        await File.WriteAllTextAsync(Path.Combine(o.Output,"cpu-samples.json"),JsonSerializer.Serialize(samples,options));
        await File.WriteAllTextAsync(Path.Combine(o.Output,"rtt-ms.json"),JsonSerializer.Serialize(rtts,options));
        await File.WriteAllTextAsync(Path.Combine(o.Output,"server-output.log"),string.Join(Environment.NewLine,context.Server.OutputLines("")));
        var result=new {Status=complete?"PASS":"INCOMPLETE",Requested=o.Clients,Entered=entered.Count,RampSeconds=ramp.Elapsed.TotalSeconds,MeasurementStartUtc=start,MeasurementEndUtc=end,
            GitHead=gitHead,GitDirty=gitStatus.Length>0,Database=context.Database.Name,Ports=context.Server.Address,ServerPid=context.Server.ProcessId,SourceAssembly=context.Server.SourceAssembly,SourceAssemblySha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(context.Server.SourceAssembly))),ServerExited=context.Server.HasExited,
            Failures=failures,ProtocolProblems=protocol,ServerProblems=serverProblems,RawPackets=entered.Sum(p=>p.World.RawPackets),Cpu=ScaleDecoder.SummarizeCpu(samples),RoundTrip=rtt.Snapshot(),Metrics=ScaleDecoder.Summarize(contained)};
        await File.WriteAllTextAsync(Path.Combine(o.Output,"summary.json"),JsonSerializer.Serialize(result,options));
        Console.WriteLine(JsonSerializer.Serialize(result,options));
        return complete?0:1;
    }
}
