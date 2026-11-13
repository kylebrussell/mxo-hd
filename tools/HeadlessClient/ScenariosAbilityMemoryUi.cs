using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
namespace HeadlessClient;
public static partial class Scenarios
{
    private static async Task AbilityMemoryUiCompatibility(ScenarioContext context)
    {
        string baseline=Environment.GetEnvironmentVariable("MXO_MEMORY_MASTER_SOURCE")??throw new ArgumentException("Explicit exact-master source export required");
        string provenance=Path.Combine(Directory.GetParent(baseline)!.FullName,"provenance.json");
        using var source=JsonDocument.Parse(await File.ReadAllTextAsync(provenance));
        Check.Equal("bfdb9875f98d9c118a1804d52f107733b48e32fa",source.RootElement.GetProperty("Commit").GetString()!,"Pinned exact master export");
        Check.Equal(Path.GetFullPath(baseline),Path.GetFullPath(source.RootElement.GetProperty("Source").GetString()!),"Baseline source path");
        var seed=await context.AddPlayerAsync(new CharacterSeed("MemoryUi"){Level=1,Experience=0});
        string user=await context.Database.ScalarAsync($"SELECT username FROM users JOIN characters USING(userId) WHERE charId={seed.CharId}");
        seed.Dispose();context.Server.Dispose();
        var evidence=new List<object>();var servers=new List<ServerInstance>();ServerInstance? server=null;HeadlessPlayer? actor=null;
        string step="seed";Exception? failure=null;bool passed=false;byte[]? loaded=null,known=null,ack=null;
        void Record(string label)
        {
            if(server==null)return;
            evidence.Add(new{Label=label,SourceAssembly=server.SourceAssembly,SourceConfiguration=Directory.GetParent(Path.GetDirectoryName(server.SourceAssembly)!)!.Name,SourceSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(server.SourceAssembly))),Output=server.FullOutput(),Debug=server.DebugLog(),Margin06=actor==null?null:Convert.ToHexString(actor.CharacterData[6]),Margin07=actor==null?null:Convert.ToHexString(actor.CharacterData[7]),Loaded=actor==null?null:AbilityMemoryUiFixture.Decode(actor.CharacterData[6]),Known=actor==null?null:AbilityMemoryUiFixture.Decode(actor.CharacterData[7]),AllRpc=actor?.World.RpcSnapshot().Select(r=>Convert.ToHexString(r.Bytes)).ToArray(),AllViews=actor?.World.ViewSnapshot().Select(Convert.ToHexString).ToArray(),Problems=actor?.World.Problems.ToArray()});
        }
        try
        {
            foreach(var entry in new[]{(Label:"PR",Root:context.RepositoryRoot),(Label:"master",Root:baseline)})
            {
                step=entry.Label+"-seed";await context.Database.ExecuteAsync(AbilityMemoryUiFixture.SeedSql(seed.CharId));
                string rows=await context.Database.ExecuteAsync($"SELECT slot,ability_id,level,is_loaded FROM char_abilities WHERE char_id={seed.CharId} ORDER BY slot");
                evidence.Add(new{Label=entry.Label,OrderedArchiveRows=rows});
                server=await ServerInstance.StartAsync(entry.Root,context.Database.Name,StillNpcs);servers.Add(server);
                step=entry.Label+"-login";actor=await HeadlessPlayer.LoginAsync(server.Address,user,seed.Handle);
                var l=actor.CharacterData[6];var k=actor.CharacterData[7];
                Check.Equal(84,l.Length,"Fourteen loaded six-byte rows");Check.Equal(114,k.Length,"Nineteen known six-byte rows");
                var tuples=AbilityMemoryUiFixture.Decode(k);for(int i=0;i<tuples.Length;i++){Check.Equal((ushort)i,tuples[i].Slot,"Exact ordered known slot");Check.Equal(0x80000000u|((uint)AbilityMemoryUiFixture.Known[i]<<10),tuples[i].Descriptor,"Exact typed descriptor");Check.Equal((ushort)1,tuples[i].Level,"All training1");}
                var lt=AbilityMemoryUiFixture.Decode(l);foreach(var tuple in lt){Check.Equal(tuples[tuple.Slot].Descriptor,tuple.Descriptor,"Loaded descriptor matches known slot");Check.Equal((ushort)1,tuple.Level,"Loaded training1");}Check.That(lt.Select(t=>t.Slot).SequenceEqual(Enumerable.Range(0,14).Select(i=>(ushort)i)),"Exact loaded slots0..13");
                if(loaded==null){loaded=l.ToArray();known=k.ToArray();}else {Check.That(loaded.SequenceEqual(l),"Literal Margin06 equals exact master");Check.That(known!.SequenceEqual(k),"Literal Margin07 equals exact master");}
                await actor.EnterWorldAsync();Check.That(actor.World.CreationSnapshot().Any(c=>c.ViewId==2),"Actual self before normal ready");actor.SendRpc("0c 01");
                step=entry.Label+"-ordinary217-load";SetAbilityLoaded(actor,14,217,1,true);var loadedAck=await actor.WaitForRpcAsync("80 b2 d9 00");if(ack==null)ack=loadedAck.Bytes.ToArray();else Check.That(ack.SequenceEqual(loadedAck.Bytes),"Literal ordinary217 acknowledgement equals master");
                Check.Equal(1L,await context.Database.LongAsync($"SELECT is_loaded FROM char_abilities WHERE char_id={seed.CharId} AND slot=14"),"Ordinary217 durable load");
                evidence.Add(new{Label=entry.Label,Load217Ack=Convert.ToHexString(loadedAck.Bytes),AfterLoadRows=await context.Database.ExecuteAsync($"SELECT slot,ability_id,level,is_loaded FROM char_abilities WHERE char_id={seed.CharId} ORDER BY slot")});
                Check.Equal(0,actor.World.Problems.Count,"Measured session protocol problems");Record(entry.Label);actor.Dispose();actor=null;
                int pid=server.ProcessId;server.Dispose();bool gone;try{using var process=Process.GetProcessById(pid);gone=process.HasExited;}catch(ArgumentException){gone=true;}Check.That(gone,"Owned server exits before replacement");evidence.Add(new{Label=entry.Label,DisposedPid=pid,ExitedBeforeNext=gone});server=null;
            }
            step="complete";passed=true;
        }
        catch(Exception error){failure=error;throw;}
        finally
        {
            try
            {
                Record("final");string stem=Path.Combine(Path.GetTempPath(),"mxo-memory-ui-"+context.Database.Name);
                await File.WriteAllTextAsync(stem+".json",JsonSerializer.Serialize(new{Passed=passed,Step=step,Failure=failure?.ToString(),Database=context.Database.Name,BaselineProvenance=source.RootElement.Clone(),Evidence=evidence,NativeUiProven=false},new JsonSerializerOptions{WriteIndented=true}));
                Console.WriteLine("Memory UI proof: "+stem+".json");
            }
            catch(Exception proofError){if(failure==null)throw;Console.Error.WriteLine("Required memory UI evidence also failed: "+proofError);}
            finally{actor?.Dispose();foreach(var owned in servers)owned.Dispose();}
        }
    }
}
