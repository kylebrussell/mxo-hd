using System.Security.Cryptography;
using System.Text.Json;
namespace HeadlessClient;
public static partial class Scenarios
{
    private static async Task LoadedGrantMembership(ScenarioContext context)
    {
        var p=await context.AddPlayerAsync(new CharacterSeed("LoadedGrants"){Level=50,Experience=(int)hds.PlayerProgression.ExperienceForLevel(50),
            OwnedLoadedAbilities=new (ushort,ushort)[]{(64,50),(137,50),(143,50),(356,50),(2,50),(17,50)}});
        var vectors=new (ushort Parent,ushort Child,ushort Slot,string Add,string Off)[]{
            (64,1078,400,"80bc4503360400400000003604000000000000000000","80bc4503360400400000003604000000000100000000"),
            (137,1062,401,"80bc4503260400890000002604000000000000000000","80bc4503260400890000002604000000000100000000"),
            (143,1060,402,"80bc45032404008f0000002404000000000000000000","80bc45032404008f0000002404000000000100000000"),
            (356,1087,403,"80bc45033f0400640100003f04000000000000000000","80bc45033f0400640100003f04000000000100000000")
        };
        var sessions=new List<object>();string stage="initial-entry";Exception? failure=null;bool passed=false;
        int Count(string hex)=>p.World.RpcSnapshot().Count(r=>Convert.ToHexString(r.Bytes)==hex.ToUpperInvariant());
        async Task Ready()
        {
            Check.That(p.World.CreationSnapshot().Any(c=>c.ViewId==2),"Actual own03 before readiness");
            foreach(var v in vectors)Check.Equal(0,Count(v.Add),"No pre-avatar grant membership");
            int start=p.World.RpcSnapshot().Count;p.SendRpc("0c 01");
            foreach(var v in vectors)await p.WaitUntilAsync(()=>Count(v.Add)==1,"Actual initial grant "+v.Child,TimeSpan.FromSeconds(10));
            var rpcs=p.World.RpcSnapshot().Select(r=>r.Bytes).ToList();
            foreach(var v in vectors)
            {
                string baseHex="80B2"+Convert.ToHexString(BitConverter.GetBytes(v.Child))+"00000802";
                int b=rpcs.FindIndex(b=>Convert.ToHexString(b)==baseHex),g=rpcs.FindIndex(b=>Convert.ToHexString(b)==v.Add.ToUpperInvariant());
                Check.That(b>=start&&g>b,"Actual self/0c/base/grant order for "+v.Child);
            }
            int features=p.Received("81 49").Count;p.SendRpc("0c 01");p.SendRpc("81 48");
            await p.WaitUntilAsync(()=>p.Received("81 49").Count>features,"New feature reply fences duplicate readiness",TimeSpan.FromSeconds(10));
            foreach(var v in vectors)Check.Equal(1,Count(v.Add),"Duplicate readiness does not duplicate grants");
        }
        void Record()
        { sessions.Add(new{p.CharId,p.Handle,AllRpc=p.World.RpcSnapshot().Select(r=>Convert.ToHexString(r.Bytes)).ToArray(),AllViews=p.World.ViewSnapshot().Select(Convert.ToHexString).ToArray(),Problems=p.World.Problems.ToArray()});Check.Equal(0,p.World.Problems.Count,"Owned grant session framing"); }
        try
        {
            await p.EnterWorldAsync();await Ready();stage="normal-unload-reload";
            var selected=vectors.Single(v=>v.Parent==143);int start=p.World.RpcSnapshot().Count;
            SetAbilityLoaded(p,selected.Slot,selected.Parent,50,false);
            await p.WaitUntilAsync(()=>Count(selected.Off)==1,"Normal last-parent unload removes membership",TimeSpan.FromSeconds(10));
            var removed=p.World.RpcSnapshot().Skip(start).Select(r=>Convert.ToHexString(r.Bytes)).ToList();
            Check.That(removed.IndexOf("80B32404")>=0&&removed.IndexOf("80B32404")<removed.IndexOf(selected.Off.ToUpperInvariant()),"Child unload precedes membership remove");
            foreach(var v in vectors.Where(v=>v.Parent!=143))Check.Equal(0,Count(v.Off),"Unrelated grant remains registered");
            SetAbilityLoaded(p,selected.Slot,selected.Parent,50,true);
            await p.WaitUntilAsync(()=>Count(selected.Add)==2,"Normal parent reload re-registers once",TimeSpan.FromSeconds(10));
            Check.Equal(0L,await context.Database.LongAsync($"SELECT COUNT(*) FROM char_abilities WHERE char_id={p.CharId} AND ((ability_id & 4193280)>>10) IN (1060,1062,1078,1087)"),"Hidden grants are never archived as owned");
            stage="normal-relog";uint id=p.CharId;p.SendRpc("80 fe 00");await p.WaitForRpcAsync("80 fd");Record();p.Dispose();
            await Task.Delay(TimeSpan.FromSeconds(33));p=await HeadlessPlayer.LoginAsync(context.Server.Address,"user"+id,"LoadedGrants");
            await p.EnterWorldAsync();await Ready();stage="complete";passed=true;
        }
        catch(Exception e){failure=e;throw;}
        finally
        {
            Exception? proofFailure=null;try{Record();}catch(Exception e){proofFailure=e;}
            try
            {
                string path=Path.Combine(Path.GetTempPath(),"mxo-loaded-grants-"+context.Database.Name);
                await File.WriteAllTextAsync(path+".log",context.Server.FullOutput()+"\n--- Debug ---\n"+context.Server.DebugLog());
                await File.WriteAllTextAsync(path+".json",JsonSerializer.Serialize(new{Passed=passed&&proofFailure==null,Stage=stage,Failure=failure?.ToString()??proofFailure?.ToString(),Database=context.Database.Name,
                    SourceAssembly=context.Server.SourceAssembly,SourceConfiguration=Directory.GetParent(Path.GetDirectoryName(context.Server.SourceAssembly)!)!.Name,
                    SourceSha256=Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(context.Server.SourceAssembly))),Sessions=sessions,NativeActionsProven=false},new JsonSerializerOptions{WriteIndented=true}));
                Console.WriteLine("Loaded-grant proof: "+path);
            }
            catch(Exception e){proofFailure??=e;Console.Error.WriteLine("Required grant evidence failed: "+e);}
            finally{p.Dispose();}
            if(failure==null&&proofFailure!=null)throw new ScenarioFailure("Required grant evidence failed: "+proofFailure);
        }
    }
}
