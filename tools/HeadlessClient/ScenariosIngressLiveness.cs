using System.Security.Cryptography;
using System.Text.Json;
namespace HeadlessClient;
public static partial class Scenarios
{
    private static async Task IngressLiveness(ScenarioContext context)
    {
        string? expectedSource=System.Environment.GetEnvironmentVariable(IngressLivenessFixture.SourcePin);
        IngressLivenessFixture.RequirePin(expectedSource,context.Server.SourceAssemblySha256);
        using var p=await IngressLivenessFixture.CsrAsync(context);bool passed=false;Exception? failure=null;var proof=new List<object>();string stage="entry";
        string PacketPath()=>Path.Combine(context.Server.WorkingDirectory,"PacketLog.txt");
        async Task<string> Account()=>await context.Database.ExecuteAsync($"SELECT exp,cash,is_online,(SELECT COALESCE(MAX(payload),'NULL') FROM char_active_missions WHERE charId=110) FROM characters WHERE charId=110");
        try
        {
            await p.EnterWorldAsync();p.SkipReceived();p.SendRpc("81 48");await p.WaitForRpcAsync("81 49");
            string before=await Account();Check.Equal(1L,await context.Database.LongAsync("SELECT is_online FROM characters WHERE charId=110"),"Current CSR is online before controlled pause");
            int rpc=p.World.RpcSnapshot().Count,packets=InteriorAdmissionDeliveryProof.Read(PacketPath()).Count(c=>c=='\n');
            stage="intentional-worker-pause";p.SkipReceived();p.SendChat("?ingresspause");var start=await p.WaitForTextAsync("INGRESS PAUSE START35");
            await Task.Delay(32000);string during=await Account();Check.Equal(before,during,"Mission/wallet/XP/online state unchanged while real worker is blocked beyond30s");
            var done=await p.WaitForTextAsync("INGRESS PAUSE DONE35");
            stage="post-resume-normal-rpc";p.SkipReceived();p.SendRpc("81 48");var barrier=await p.WaitForRpcAsync("81 49");
            string after=await Account();Check.Equal(before,after,"Resumed endpoint retains exact mission/wallet/XP/online state");
            using var qa=JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(context.Server.WorkingDirectory,"IngressLivenessQa.json")));
            var q=qa.RootElement;Check.That(q.GetProperty("CurrentOwner").GetBoolean(),"Same actual endpoint owner/world/life/transport survived");
            long delta=q.GetProperty("After").GetProperty("Generation").GetInt64()-q.GetProperty("Before").GetProperty("Generation").GetInt64();
            Check.That(delta>=20&&q.GetProperty("Pending").GetInt32()>0,"Normal keepalives reached ingress while serialized worker was blocked");
            Check.That(q.GetProperty("ElapsedMs").GetDouble()>=35000,"The explicit QA delay actually exceeded inactivity30s");
            Check.That(!context.Server.DebugLog().Contains("Removed inactive Client with Key "+q.GetProperty("Endpoint").GetString()),"This actual endpoint was never reaped");
            var wire=InteriorAdmissionDeliveryProof.Parse(InteriorAdmissionDeliveryProof.Read(PacketPath()),p.CharId).Where(x=>x.Line>packets).ToArray();
            Check.That(wire.All(x=>(x.Bytes[0]&0x40)==0),"No world/transport reset handshake during pause/resume");
            var sends=wire.Where(x=>x.Server).ToArray();for(int i=1;i<sends.Length;i++)Check.That(((sends[i].Sequence-sends[i-1].Sequence)&0xfff)<0x800,"SSEQ continues forward under normal12-bit wrap");
            Check.That(p.World.RpcSnapshot().Count>rpc,"Reliable RPC stream continues after actual worker resume");
            p.SkipReceived();p.SendChat("?ingresspause");var refused=await p.WaitForTextAsync("Ingress pause refused");
            Check.Equal(0,p.World.Problems.Count,"Actual protocol parser remains clean");
            proof.Add(new{Before=before,During=during,After=after,Start=start.Hex,Done=done.Hex,Barrier=barrier.Hex,SecondUse=refused.Hex,Control=q.Clone(),IngressDelta=delta,
                Wire=wire,Rpcs=p.World.RpcSnapshot().Skip(rpc)});passed=true;stage="complete";
        }
        catch(Exception error){failure=error;throw;}
        finally
        {
            string stem=Path.Combine(Path.GetTempPath(),"mxo-ingress-liveness-"+context.Database.Name);string? final=null,readError=null;
            try{final=await Account();}catch(Exception error){readError=error.ToString();}
            string raw=InteriorAdmissionDeliveryProof.Read(PacketPath());File.WriteAllText(stem+"-PacketLog.txt",raw);
            string controlPath=Path.Combine(context.Server.WorkingDirectory,"IngressLivenessQa.json");
            await File.WriteAllTextAsync(stem+".json",JsonSerializer.Serialize(new{passed,stage,Failure=failure?.ToString(),Database=context.Database.Name,ServerPid=context.Server.ProcessId,
                ExpectedSourceSha256=expectedSource,SourceAssembly=context.Server.SourceAssembly,SourceSha256=context.Server.SourceAssemblySha256,Proof=proof,FinalAccount=final,ReadError=readError,
                Control=File.Exists(controlPath)?File.ReadAllText(controlPath):null,RawPacketLog=stem+"-PacketLog.txt",RawSha256=Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(raw))),
                CharacterData=p.CharacterData.ToDictionary(k=>(int)k.Key,v=>Convert.ToHexString(v.Value)),Rpcs=p.World.RpcSnapshot(),Views=p.World.ViewSnapshot().Select(Convert.ToHexString),Problems=p.World.Problems,
                Qualification="Intentional35s blocked-worker diagnostic; no normal cold-preparation latency/nativeUI claim"},new JsonSerializerOptions{WriteIndented=true}));
            File.WriteAllText(stem+".log",context.Server.FullOutput()+"\n---Debug---\n"+context.Server.DebugLog());Console.WriteLine("INGRESS EVIDENCE="+stem);
        }
    }
}
