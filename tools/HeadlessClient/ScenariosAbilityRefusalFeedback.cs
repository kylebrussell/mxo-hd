using hds;
using MxoHd.PacketResearch;
using System.Security.Cryptography;
using System.Text.Json;
namespace HeadlessClient;
public static partial class Scenarios
{
    private static ushort RefusalOwnValue(HeadlessPlayer p,string field,string prefix)
    {
        var values=ViewUpdateValues(p,2,prefix);if(values.Count>0)return values.Last();
        return BitConverter.ToUInt16(p.World.CreationSnapshot().Last(v=>v.ViewId==2).Attributes[field]);
    }
    private static async Task AbilityRefusalFeedback(ScenarioContext context)
    {
        var sessions=new List<HeadlessPlayer>();var operations=new List<object>();bool passed=false;string step="entry";Exception? failure=null;ServerInstance? reloaded=null;
        var loaded=new (ushort,ushort)[]{(2,1),(17,1),(45,1),(1006,1),(77,1)};
        async Task Barrier(HeadlessPlayer p){p.SkipReceived();p.SendRpc("81 48");await p.WaitForRpcAsync("81 49");}
        byte[] Request(HeadlessPlayer p)=>new AbilityUseRecord(77,DuelPackets.Handle(2,p.World.CreationSnapshot().Last(v=>v.ViewId==2).SpawnId),0,0,0,0,0x6b,0x6b,0,0).Encode();
        async Task Denied(HeadlessPlayer p,string literal)
        {
            await Barrier(p);int rpcCursor=p.World.RpcSnapshot().Count,viewCursor=p.World.ViewSnapshot().Count;int debugCursor=context.Server.DebugLog().Length;
            ushort before=RefusalOwnValue(p,"InnerStrengthAvailable","02 80 80 80 10");byte[] request=Request(p);p.SkipReceived();p.SendRpc(request);var denied=await p.WaitForRpcAsync(literal);await Barrier(p);
            var replies=p.World.RpcSnapshot().Skip(rpcCursor).ToArray();
            Check.That(denied.Bytes.SequenceEqual(WireFormat.FromHex(literal)),"Independent exact native19-byte refusal literal");
            Check.Equal(0,replies.Count(r=>r.Bytes.Length>=2&&r.Bytes[0]==0x80&&(r.Bytes[1]==0xac||r.Bytes[1]==0xbc||r.Bytes[1]==0xbd&&r.Bytes.Last()==1)),"Denied request has no positive echo/modifier/new timer");
            Check.That(ViewUpdateValues(p,2,"02 80 80 80 10",viewCursor).All(v=>v>=before),"Denied request does not spend IS; unrelated regeneration may rise");
            Check.That(!context.Server.DebugLog()[debugCursor..].Contains("Heal 77 -> "+p.Handle+" for ",StringComparison.Ordinal),"Denied request does not execute heal");
            operations.Add(new{Step=step,p.Handle,RpcCursor=rpcCursor,ViewCursor=viewCursor,BeforeInnerStrength=before,Sent=Convert.ToHexString(request),Reply=Convert.ToHexString(denied.Bytes),Records=replies.Select(r=>r.Hex).ToArray(),InnerHistory=ViewUpdateValues(p,2,"02 80 80 80 10",viewCursor)});
        }
        try
        {
            var normal=await context.AddPlayerAsync(new CharacterSeed("RefusalNormal"){Level=50,Experience=(int)PlayerProgression.ExperienceForLevel(50),X=17000,Y=495,Z=2300,Health=100,MaxHealth=1000,InnerStrength=1000,MaxInnerStrength=1000,OwnedLoadedAbilities=loaded});sessions.Add(normal);
            var poor=await context.AddPlayerAsync(new CharacterSeed("RefusalPoor"){Level=50,Experience=(int)PlayerProgression.ExperienceForLevel(50),X=17100,Y=495,Z=2300,Health=100,MaxHealth=1000,InnerStrength=1,MaxInnerStrength=1,OwnedLoadedAbilities=loaded});sessions.Add(poor);
            await normal.EnterWorldAsync();normal.SendRpc("0c 01");await poor.EnterWorldAsync();poor.SendRpc("0c 01");await Barrier(normal);await Barrier(poor);
            step="normal-success";normal.SendChat("?health 100");await normal.WaitUntilAsync(()=>OwnHealth(normal)==100,"Existing health preparation",TimeSpan.FromSeconds(10));await Barrier(normal);
            ushort initialInner=RefusalOwnValue(normal,"InnerStrengthAvailable","02 80 80 80 10");int rpcCursor=normal.World.RpcSnapshot().Count,viewCursor=normal.World.ViewSnapshot().Count;byte[] request=Request(normal);
            normal.SkipReceived();normal.SendRpc(request);await normal.WaitForRpcAsync("80 ac 4d 00");await normal.WaitForRpcAsync("80 bd 01 4d 00");
            await normal.WaitUntilAsync(()=>context.Server.DebugLog().Contains("Heal 77 -> RefusalNormal for "),"Normal heal executor",TimeSpan.FromSeconds(10));
            Check.That(ViewUpdateValues(normal,2,"02 80 80 80 40",viewCursor).Any(h=>h>=109),"Normal successful healing still reaches own view");
            Check.That(ViewUpdateValues(normal,2,"02 80 80 80 10",viewCursor).Contains((ushort)(initialInner-10)),"Normal successful cast pays unchanged10 IS");
            operations.Add(new{Step=step,RpcCursor=rpcCursor,ViewCursor=viewCursor,Sent=Convert.ToHexString(request),InitialInnerStrength=initialInner,Records=normal.World.RpcSnapshot().Skip(rpcCursor).Select(r=>r.Hex).ToArray(),Health=ViewUpdateValues(normal,2,"02 80 80 80 40",viewCursor),Inner=ViewUpdateValues(normal,2,"02 80 80 80 10",viewCursor)});
            step="existing-cooldown-predicate";await Denied(normal,"80 ba 4d 00 00 00 6b 05 00 00 00 00 00 00 00 00 00 00 00");
            step="existing-inner-strength-predicate";Check.Equal((ushort)1,RefusalOwnValue(poor,"InnerStrengthAvailable","02 80 80 80 10"),"Poor fixture actual available IS");
            await Denied(poor,"80 ba 4d 00 00 00 6b 11 00 00 00 09 00 00 00 00 00 00 00");
            Check.That(!context.Server.DebugLog().Contains("[ABILITY-REFUSAL-QA]"),"Actual predicate case did not use render probes");
            string user=await context.Database.ScalarAsync($"SELECT username FROM users JOIN characters USING(userId) WHERE charId={normal.CharId}");
            normal.Dispose();poor.Dispose();context.Server.Dispose();
            step="cold-relog";using var server=await ServerInstance.StartAsync(context.RepositoryRoot,context.Database.Name,StillNpcs);reloaded=server;
            using var again=await HeadlessPlayer.LoginAsync(server.Address,user,normal.Handle);sessions.Add(again);await again.EnterWorldAsync();again.SendRpc("0c 01");await Barrier(again);
            int afterLogin=again.World.RpcSnapshot().Count;again.SkipReceived();again.SendRpc(Request(again));await again.WaitForRpcAsync("80 ac 4d 00");await again.WaitForRpcAsync("80 bd 01 4d 00");
            Check.Equal(0,again.World.RpcSnapshot().Skip(afterLogin).Count(r=>r.Bytes.Length>=2&&r.Bytes[0]==0x80&&r.Bytes[1]==0xba),"Fresh session ordinary cast is not spuriously refused");
            foreach(var p in sessions)Check.Equal(0,p.World.Problems.Count,"Refusal session parser clean");step="complete";passed=true;
        }
        catch(Exception error){failure=error;throw;}
        finally
        {
            string proof=Path.Combine(Path.GetTempPath(),"mxo-ability-refusal-"+context.Database.Name+".json");
            try{string dll=context.Server.SourceAssembly;await File.WriteAllTextAsync(proof,JsonSerializer.Serialize(new{Passed=passed,Step=step,Failure=failure?.ToString(),SourceAssembly=dll,SourceConfiguration=Directory.GetParent(Path.GetDirectoryName(dll)!)!.Name,SourceSha256=Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(dll))),Operations=operations,Sessions=sessions.Select(p=>new{p.Handle,p.CharId,CharacterData=p.CharacterData.ToDictionary(k=>k.Key,v=>Convert.ToHexString(v.Value)),Rpcs=p.World.RpcSnapshot().Select(r=>r.Hex),Views=p.World.ViewSnapshot().Select(Convert.ToHexString),Problems=p.World.Problems.ToArray()}),Debug=context.Server.DebugLog(),Output=context.Server.FullOutput(),RelogDebug=reloaded?.DebugLog(),RelogOutput=reloaded?.FullOutput(),NativeUiGeneratedRefusalProven=false},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine("Refusal proof before teardown: "+proof);}
            catch(Exception proofError){if(failure==null)throw;Console.Error.WriteLine(proofError);}
        }
    }
}
