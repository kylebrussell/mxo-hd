using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using hds;
namespace HeadlessClient;

// Explicit opt-in only; existing Prima callers retain their previous character and lifecycle.
public sealed record PrimaChainEvidenceOptions(int? StartingExperience=null,bool ColdRestart=false,IReadOnlyDictionary<string,string>? RestartEnvironment=null,long? ExpectedExperienceDelta=null,long? ExpectedInformationDelta=null,bool FreshUploadBooth=false,IReadOnlyList<uint>? MissionExperience=null,IReadOnlyList<uint>? MissionInformation=null);
internal sealed class PrimaChainEvidence : IDisposable
{
    private readonly ScenarioContext context;private readonly HeadlessPlayer actor;private readonly int[] ids;private readonly PrimaChainEvidenceOptions options;
    private readonly List<object> checkpoints=new(),operations=new(),targets=new(),sessions=new(),processes=new(),awards=new();
    private ServerInstance? replacement;private HeadlessPlayer? relogged;private bool initialRecorded;
    private int rpcStart,viewStart;private string operation="world-entry",operationKind="";
    private readonly List<PrimaLedgerOperation> ledgerOperations=new();private uint? baselineExperience,baselineInformation;
    internal PrimaRewardLedgerResult? RewardLedger {get;private set;}
    internal string Step {get;set;}="world-entry";
    internal object? ColdRestartProof {get;private set;}
    internal PrimaChainEvidence(ScenarioContext c,HeadlessPlayer p,int[] selected,PrimaChainEvidenceOptions settings){context=c;actor=p;ids=selected;options=settings;}
    internal void Begin(int mission,int phase,int goal,string kind)
    {Step=operation=$"mission-{mission}-phase-{phase+1}-goal-{goal}";operationKind=kind;rpcStart=actor.World.RpcSnapshot().Count;viewStart=actor.World.ViewSnapshot().Count;}
    internal void End()
    {
        int end=actor.World.RpcSnapshot().Count;
        operations.Add(new{Step=operation,Kind=operationKind,RpcStart=rpcStart,RpcEnd=end,ViewStart=viewStart,ViewEnd=actor.World.ViewSnapshot().Count});
        ledgerOperations.Add(new(operation,operationKind,rpcStart,end));
    }
    internal async Task BaselineAsync()
    {
        if(options.MissionExperience==null&&options.MissionInformation==null)return;
        Check.That(options.MissionExperience!=null&&options.MissionInformation!=null,"Both independent literal mission arrays are required");
        await actor.WaitUntilAsync(()=>actor.World.RpcSnapshot().Any(r=>r.Bytes.Length>=2&&r.Bytes[0]==0x80&&r.Bytes[1]==0xe5)&&
            actor.World.RpcSnapshot().Any(r=>r.Bytes.Length>=2&&r.Bytes[0]==0x80&&r.Bytes[1]==0xe4),"Initial earned-ledger totals before mission accept",TimeSpan.FromSeconds(10));
        var wire=PrimaRewardLedger.ReadBaseline(actor.World.RpcSnapshot().Select(r=>r.Bytes).ToArray());
        long xp=await context.Database.LongAsync($"SELECT exp FROM characters WHERE charId={actor.CharId}");
        long cash=await context.Database.LongAsync($"SELECT cash FROM characters WHERE charId={actor.CharId}");
        Check.Equal((long)wire.Experience,xp,"Initial wire XP equals durable pre-accept baseline");
        Check.Equal((long)wire.Information,cash,"Initial wire cash equals durable pre-accept baseline");
        Check.Equal((long)(options.StartingExperience??0),xp,"Actual pre-accept XP matches profile");
        Check.Equal(1000L,cash,"Actual pre-accept cash matches unchanged CharacterSeed default");
        baselineExperience=wire.Experience;baselineInformation=wire.Information;
    }
    internal void Target(string kind,ViewCreation view)=>targets.Add(new{Step,Kind=kind,view.ViewId,view.SpawnId,view.Goid,view.Name,Attributes=view.Attributes.ToDictionary(p=>p.Key,p=>Convert.ToHexString(p.Value))});
    internal async Task PhaseAsync(int mission,int phase)
    {
        Step=$"mission-{mission}-phase-{phase+1}-entered";
        string hex=await context.Database.ScalarAsync($"SELECT IFNULL((SELECT HEX(payload) FROM char_active_missions WHERE charId={actor.CharId}),'NULL')");
        Check.That(hex!="NULL","Entered phase has an owned active checkpoint");string raw=System.Text.Encoding.UTF8.GetString(Convert.FromHexString(hex));
        using var parsed=JsonDocument.Parse(raw);Check.Equal(mission,parsed.RootElement.GetProperty("Mission").GetInt32(),"Checkpoint mission matches actual accepted title");
        Check.Equal(phase,parsed.RootElement.GetProperty("Phase").GetInt32(),"Checkpoint phase matches current wire area");
        checkpoints.Add(new{Mission=mission,Phase=phase,Checkpoint=raw,RpcCursor=actor.World.RpcSnapshot().Count,ViewCursor=actor.World.ViewSnapshot().Count});
    }
    internal async Task AwardAsync(int id)
    {
        var summary=actor.Received("80 a2 00").Last().Bytes;
        awards.Add(new{Mission=id,Summary=Convert.ToHexString(summary),Experience=BitConverter.ToUInt32(summary,15),Information=BitConverter.ToUInt32(summary,11),
            Totals=await context.Database.ScalarAsync($"SELECT CONCAT(exp,':',cash) FROM characters WHERE charId={actor.CharId}")});
    }
    private static object Snapshot(HeadlessPlayer p)=>new{p.Handle,p.CharId,Rpc=p.World.RpcSnapshot().Select(r=>Convert.ToHexString(r.Bytes)).ToArray(),Views=p.World.ViewSnapshot().Select(Convert.ToHexString).ToArray(),Problems=p.World.Problems.ToArray()};
    private static object ProcessProof(ServerInstance s)=>new{s.ProcessId,SourceAssembly=s.SourceAssembly,SourceConfiguration=new DirectoryInfo(Path.GetDirectoryName(s.SourceAssembly)!).Parent!.Name,
        SourceSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(s.SourceAssembly))),Output=s.FullOutput(),Debug=s.DebugLog()};
    private void RecordInitial(){if(initialRecorded)return;sessions.Add(Snapshot(actor));processes.Add(ProcessProof(context.Server));initialRecorded=true;}
    internal async Task FinishAsync()
    {
        Step="chain-complete";
        if(options.MissionExperience!=null||options.MissionInformation!=null)
        {
            Check.That(baselineExperience.HasValue&&baselineInformation.HasValue,"Pre-accept ledger baseline captured");
            await actor.WaitUntilAsync(()=>PrimaRewardLedger.HasCompleteMissionAwards(actor.World.RpcSnapshot().Select(r=>r.Bytes).ToArray(),options.MissionExperience!.Count),
                "Complete final mission gain/total/cash packets before strict ledger",TimeSpan.FromSeconds(10));
            RewardLedger=PrimaRewardLedger.Reconcile(actor.World.RpcSnapshot().Select(r=>r.Bytes).ToArray(),ledgerOperations,
                baselineExperience!.Value,baselineInformation!.Value,options.MissionExperience!,options.MissionInformation!);
            Check.Equal(options.ExpectedExperienceDelta!.Value,RewardLedger.MissionExperience,"Exact independent mission XP sum");
            Check.Equal(options.ExpectedInformationDelta!.Value,RewardLedger.MissionInformation,"Exact independent mission information sum");
            Check.Equal((long)RewardLedger.Experience,await context.Database.LongAsync($"SELECT exp FROM characters WHERE charId={actor.CharId}"),"Earned-event XP ledger equals last wire total and durable DB");
            Check.Equal((long)RewardLedger.Information,await context.Database.LongAsync($"SELECT cash FROM characters WHERE charId={actor.CharId}"),"Earned-event cash ledger equals last wire total and durable DB");
        }
        if(!options.ColdRestart)return;
        string where=$"charId={actor.CharId}",before=await context.Database.ScalarAsync($"SELECT CONCAT(exp,':',cash) FROM characters WHERE {where}");
        if(RewardLedger==null&&options.ExpectedExperienceDelta.HasValue)Check.Equal((long)(options.StartingExperience??0)+options.ExpectedExperienceDelta.Value,
            await context.Database.LongAsync($"SELECT exp FROM characters WHERE {where}"),"Earned summary XP pair paid once for the selected chain");
        if(RewardLedger==null&&options.ExpectedInformationDelta.HasValue)Check.Equal(1000L+options.ExpectedInformationDelta.Value,
            await context.Database.LongAsync($"SELECT cash FROM characters WHERE {where}"),"Earned summary information pair paid once for the selected chain");
        Check.Equal("NULL",await context.Database.ScalarAsync($"SELECT IFNULL((SELECT HEX(payload) FROM char_active_missions WHERE {where}),'NULL')"),"No active payload after earned chain");
        string username=await context.Database.ScalarAsync($"SELECT username FROM users JOIN characters USING(userId) WHERE {where}");
        Step="normal-logout-before-cold-restart";actor.SendRpc("80 fe 00");await actor.WaitForRpcAsync("80 fd");RecordInitial();
        int pid=context.Server.ProcessId;actor.Dispose();context.Server.Dispose();bool exited;
        try{using var process=Process.GetProcessById(pid);exited=process.HasExited;}catch(ArgumentException){exited=true;}
        Check.That(exited,"Owned old server exits before replacement starts");
        Step="cold-restart-login";replacement=await ServerInstance.StartAsync(context.RepositoryRoot,context.Database.Name,options.RestartEnvironment);
        relogged=await HeadlessPlayer.LoginAsync(replacement.Address,username,actor.Handle);await relogged.EnterWorldAsync();
        await relogged.WaitUntilAsync(()=>relogged.World.CreationSnapshot().Any(v=>v.ViewId==2),"Cold relog actual self",TimeSpan.FromSeconds(10));
        relogged.SendRpc("0c 01");relogged.SkipReceived();relogged.SendRpc("81 48");await relogged.WaitForRpcAsync("81 49");
        string after=await context.Database.ScalarAsync($"SELECT CONCAT(exp,':',cash) FROM characters WHERE {where}");Check.Equal(before,after,"Cold relog adds no reward payout");
        Check.Equal((long)ids.Length,await context.Database.LongAsync($"SELECT COUNT(*) FROM char_missions WHERE {where} AND missionId IN ({string.Join(",",ids)}) AND completions=1"),"All earned receipts remain exactly1");
        Check.Equal("NULL",await context.Database.ScalarAsync($"SELECT IFNULL((SELECT HEX(payload) FROM char_active_missions WHERE {where}),'NULL')"),"Cold relog cannot revive completed chain");
        Check.That(PrimaRewardLedger.HasNoCompletionReplay(relogged.World.RpcSnapshot().Select(r=>r.Bytes).ToArray()),"Cold relog whole new session emits no completion replay");Check.Equal(0,relogged.World.Problems.Count,"Cold relog parser problems");
        ColdRestartProof=new{DisposedPid=pid,ExitedBeforeNext=exited,RewardBefore=before,RewardAfter=after,NoReplay=true};Step="complete";
    }
    internal async Task SaveAsync(string label,Exception? failure,int[] prerequisites)
    {
        try
        {
            RecordInitial();if(relogged!=null)sessions.Add(Snapshot(relogged));if(replacement!=null)processes.Add(ProcessProof(replacement));
            string stem=Path.Combine(Path.GetTempPath(),"mxo-"+label+"-"+context.Database.Name);
            File.WriteAllText(stem+"-debug.log",context.Server.DebugLog());File.WriteAllText(stem+"-stdout.log",context.Server.FullOutput());
            await File.WriteAllTextAsync(stem+".json",JsonSerializer.Serialize(new{Passed=failure==null,Step,Failure=failure?.ToString(),SelectedMissions=ids,Prerequisites=prerequisites,
                PhaseCheckpoints=checkpoints,Operations=operations,Targets=targets,Awards=awards,Sessions=sessions,Processes=processes,ColdRestart=ColdRestartProof,RewardBaseline=new{Experience=baselineExperience,Information=baselineInformation},RewardLedger,
                CurrentPayloadHex=await context.Database.ScalarAsync($"SELECT IFNULL((SELECT HEX(payload) FROM char_active_missions WHERE charId={actor.CharId}),'NULL')"),
                CompletionReceipts=await context.Database.ExecuteAsync($"SELECT missionId,completions FROM char_missions WHERE charId={actor.CharId} AND missionId IN ({string.Join(",",ids)}) ORDER BY missionId"),
                NativeRenderingProven=false,NativeWalkingProven=false,OrdinaryUnassistedSurvivalProven=false,Policy="StillNpcs/weaken/test movement are assistance; actual source placements are not inferred from seed equality"},new JsonSerializerOptions{WriteIndented=true}));
            Console.WriteLine("  Required Prima evidence "+stem+".json");
        }
        catch(Exception proofError){if(failure==null)throw;Console.Error.WriteLine("Required evidence also failed; original scenario failure retained: "+proofError);}
        finally{try{relogged?.Dispose();}finally{replacement?.Dispose();}}
    }
    public void Dispose(){try{relogged?.Dispose();}finally{replacement?.Dispose();}}
}
