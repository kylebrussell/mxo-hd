using System.Security.Cryptography;
using System.Text.Json;
namespace HeadlessClient;

public static partial class Scenarios
{
    private static async Task ShadowReadyPresentation(ScenarioContext context)
    {
        var server=context.Server;ServerInstance? cold=null;HeadlessPlayer? actor=null;
        var sessions=new List<HeadlessPlayer>();var observations=new List<object>();var serverProof=new List<object>();
        string stage="source-guard",originalArchive="",originalAccount="";bool passed=false;Exception? failure=null;
        void Pin()=>ShadowReadyWireFixture.RequireSource(Environment.GetEnvironmentVariable(ShadowReadyWireFixture.SourcePin),server.SourceAssemblySha256);
        async Task<string> Archive()=>await context.Database.ExecuteAsync($"SELECT char_id,slot,ability_id,level,is_loaded FROM char_abilities WHERE char_id={actor!.CharId} ORDER BY slot");
        async Task<string> Account()=>await context.Database.ExecuteAsync($"SELECT exp,cash FROM characters WHERE charId={actor!.CharId}");
        async Task NoChild()=>Check.Equal(0L,await context.Database.LongAsync($"SELECT COUNT(*) FROM char_abilities WHERE char_id={actor!.CharId} AND (ability_id & 4227858432)=2147483648 AND ((ability_id & 4193280)>>10)=1117"),"No owned1117 archive row");
        async Task Barrier()
        {
            var p=actor!;int cursor=p.World.RpcSnapshot().Count;p.SendRpc("81 48");
            await p.WaitUntilAsync(()=>p.World.RpcSnapshot().Skip(cursor).Any(r=>r.Bytes.Length>=2&&r.Bytes[0]==0x81&&r.Bytes[1]==0x49),"Current-session feature barrier",TimeSpan.FromSeconds(15));
        }
        async Task Ready(string label)
        {
            var p=actor!;Check.Equal(0,p.World.CreationSnapshot().Count,"New session has no inherited ownlife");
            await p.EnterWorldAsync(loadingComplete:false);var own=p.World.CreationSnapshot().Where(v=>v.ViewId==2).ToArray();Check.Equal(1,own.Length,"Current session actual own03");
            await Barrier();p.SendRpc("0c");p.SendRpc("0c 00");await Barrier();
            var before=p.World.RpcSnapshot();Check.Equal(0,before.Count(r=>ShadowReadyWireFixture.ChildBase(r.Bytes)),"No1117 base before exact complete");
            Check.Equal(0,before.Count(r=>ShadowReadyWireFixture.AnyMember(r.Bytes)),"No1117 relation before exact complete");
            int cursor=before.Count;p.SendRpc("0c 01");await p.WaitUntilAsync(()=>p.World.RpcSnapshot().Skip(cursor).Any(r=>ShadowReadyWireFixture.Member(r.Bytes)),"Late sourced1117 membership",TimeSpan.FromSeconds(15));
            await Barrier();var fresh=p.World.RpcSnapshot().Skip(cursor).ToArray();int b=Array.FindIndex(fresh,r=>ShadowReadyWireFixture.Base(r.Bytes)),m=Array.FindIndex(fresh,r=>ShadowReadyWireFixture.Member(r.Bytes));
            Check.That(b>=0&&m>b,"Late exact zero/520 base precedes Type45 ADD");
            int bases=p.World.RpcSnapshot().Count(r=>ShadowReadyWireFixture.ChildBase(r.Bytes)),members=p.World.RpcSnapshot().Count(r=>ShadowReadyWireFixture.AnyMember(r.Bytes));
            p.SendRpc("0c 01");await Barrier();Check.Equal(bases,p.World.RpcSnapshot().Count(r=>ShadowReadyWireFixture.ChildBase(r.Bytes)),"Duplicate ready no base replay");Check.Equal(members,p.World.RpcSnapshot().Count(r=>ShadowReadyWireFixture.AnyMember(r.Bytes)),"Duplicate ready no relation replay");
            observations.Add(new{Stage=label,p.CharId,Own=own[0],ReadyCursor=cursor,BaseIndex=b,MemberIndex=m,Margin06=Convert.ToHexString(p.CharacterData[6]),Margin07=Convert.ToHexString(p.CharacterData[7])});await NoChild();
        }
        void RetainServer(string label)
        {serverProof.Add(new{Label=label,server.ProcessId,server.SourceAssembly,server.SourceAssemblySha256,Output=server.FullOutput(),Debug=server.DebugLog(),PacketLog=InteriorAdmissionDeliveryProof.Read(Path.Combine(server.WorkingDirectory,"PacketLog.txt"))});}
        try
        {
            Pin();stage="normal-owned-profile";actor=await context.AddPlayerAsync(new CharacterSeed("ShadowReady"){Level=50,Experience=(int)hds.PlayerProgression.ExperienceForLevel(50),X=17000,Y=495,Z=2300,OwnedLoadedAbilities=ShadowReadyWireFixture.Parents.Select(id=>(id,(ushort)50)).ToArray()});sessions.Add(actor);
            originalArchive=await Archive();originalAccount=await Account();await NoChild();await Ready("initial");
            var rows=AbilityMemoryUiFixture.Decode(actor.CharacterData[7]);var parent=rows.Single(r=>r.Descriptor==(0x80000000u|(290u<<10)));
            Check.Equal((ushort)50,parent.Level,"Actual known290 learned level");
            string[] OtherFamilies()=>actor!.World.RpcSnapshot().Where(r=>SneakScaledGrantFixture.ChildMembership(r.Bytes)||SneakScaledGrantFixture.ChildBase(r.Bytes)||
                (r.Bytes.Length>=4&&r.Bytes[0]==0x80&&r.Bytes[1]==0xb2&&BitConverter.ToUInt16(r.Bytes,2)==1116)||
                (MxoHd.PacketResearch.ManageBonusRecord.TryDecode(r.Bytes,out var m)&&m is{Type:0x45,SourceId:175,TargetId:1116})).Select(r=>r.Hex).ToArray();
            var oldFamilies=OtherFamilies();stage="ordinary290-unload";int cursor=actor.World.RpcSnapshot().Count;SetAbilityLoaded(actor,parent.Slot,290,parent.Level,false);
            await actor.WaitUntilAsync(()=>actor.World.RpcSnapshot().Skip(cursor).Any(r=>r.Bytes.SequenceEqual(hds.ServerPackets.BuildAbilityUnload(1117))),"Normal290 unload removes child base",TimeSpan.FromSeconds(15));await Barrier();
            var removed=actor.World.RpcSnapshot().Skip(cursor).ToArray();int off=Array.FindIndex(removed,r=>ShadowReadyWireFixture.Member(r.Bytes,true)),unload=Array.FindIndex(removed,r=>r.Bytes.SequenceEqual(hds.ServerPackets.BuildAbilityUnload(1117)));
            Check.That(off>=0&&unload>off,"Defined Shadow reducer removes Type45 before child unload");
            Check.Equal(0L,await context.Database.LongAsync($"SELECT is_loaded FROM char_abilities WHERE char_id={actor.CharId} AND slot={parent.Slot}"),"Ordinary exact290 slot committed unloaded");observations.Add(new{Stage=stage,Rows=await Archive(),Rpcs=removed.Select(r=>r.Hex).ToArray()});
            stage="ordinary290-reload";cursor=actor.World.RpcSnapshot().Count;SetAbilityLoaded(actor,parent.Slot,290,parent.Level,true);
            await actor.WaitUntilAsync(()=>actor.World.RpcSnapshot().Skip(cursor).Any(r=>ShadowReadyWireFixture.Member(r.Bytes)),"Normal290 reload adds membership",TimeSpan.FromSeconds(15));await Barrier();
            var loaded=actor.World.RpcSnapshot().Skip(cursor).ToArray();Check.That(Array.FindIndex(loaded,r=>ShadowReadyWireFixture.Base(r.Bytes))>=0&&Array.FindIndex(loaded,r=>ShadowReadyWireFixture.Member(r.Bytes))>Array.FindIndex(loaded,r=>ShadowReadyWireFixture.Base(r.Bytes)),"Reload base then ADD");
            Check.Equal(originalArchive,await Archive(),"Normal unload/reload restores exact owned rows");Check.That(oldFamilies.SequenceEqual(OtherFamilies()),"290 transition leaves historical175/151 actual bytes unchanged");await NoChild();
            stage="ordinarylogout-before-cold";uint id=actor.CharId;actor.SkipReceived();actor.SendRpc("80 fe 00");await actor.WaitForRpcAsync("80 fd");actor.Dispose();await Task.Delay(TimeSpan.FromSeconds(33));RetainServer("original");server.Dispose();
            stage="cold-reentry";cold=await ServerInstance.StartAsync(context.RepositoryRoot,context.Database.Name,ShadowReadyWireFixture.Environment);server=cold;Pin();actor=await HeadlessPlayer.LoginAsync(server.Address,"user"+id,"ShadowReady");sessions.Add(actor);Check.Equal(id,actor.CharId,"Cold identity");await Ready("cold");
            Check.Equal(originalArchive,await Archive(),"Cold exact owned rows unchanged");Check.Equal(originalAccount,await Account(),"No earned/debited XP or cash");await NoChild();foreach(var p in sessions)Check.Equal(0,p.World.Problems.Count,"Current session parser clean");passed=true;stage="complete";
        }
        catch(Exception e){failure=e;throw;}
        finally
        {
            string? finalArchive=null,finalAccount=null;try{if(actor!=null){finalArchive=await Archive();finalAccount=await Account();}}catch(Exception e){observations.Add(new{FinalSqlFailure=e.ToString()});}
            try
            {
                RetainServer(cold==null?"original-final":"cold-final");string path=Path.Combine(Path.GetTempPath(),"mxo-shadow-ready-presentation-"+context.Database.Name+".json");
                await File.WriteAllTextAsync(path,JsonSerializer.Serialize(new{Passed=passed,Stage=stage,Failure=failure?.ToString(),Database=context.Database.Name,ExpectedSource=Environment.GetEnvironmentVariable(ShadowReadyWireFixture.SourcePin),OriginalArchive=originalArchive,OriginalAccount=originalAccount,FinalArchive=finalArchive,FinalAccount=finalAccount,Observations=observations,Servers=serverProof,Sessions=sessions.Select(p=>new{p.CharId,p.Handle,Margin06=Convert.ToHexString(p.CharacterData[6]),Margin07=Convert.ToHexString(p.CharacterData[7]),Rpcs=p.World.RpcSnapshot().Select(r=>r.Hex).ToArray(),Views=p.World.ViewSnapshot().Select(Convert.ToHexString).ToArray(),Problems=p.World.Problems.ToArray()}),Qualification="Protocol/state presentation only; no native listing or hidden1117 invocation; no gameplay formula/timer change."},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine("SHADOW PRESENTATION EVIDENCE="+path);
            }
            finally{foreach(var p in sessions)p.Dispose();cold?.Dispose();}
        }
    }
}
