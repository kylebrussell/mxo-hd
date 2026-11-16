using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace HeadlessClient;
public static partial class Scenarios
{
    private static async Task ThrownAccuracyWire(ScenarioContext context)
    {
        string expected=Environment.GetEnvironmentVariable("MXO_THROWN_ACCURACY_SOURCE_SHA256")??throw new InvalidOperationException("Exact thrown-accuracy source pin required");
        Check.Equal(expected,context.Server.SourceAssemblySha256,"Actual source before authentication");
        var players=new List<HeadlessPlayer>();var observations=new List<object>();string stage="login";bool passed=false;Exception? failure=null;
        try
        {
            var actor=await context.AddPlayerAsync(ThrownAccuracyFixture.Seed("ThrowAccuracy",false));players.Add(actor);
            var enemy=await context.AddPlayerAsync(ThrownAccuracyFixture.Seed("ThrowBlock",true));players.Add(enemy);
            await PvpOpponentsFixture.EnterAsync(actor);await PvpOpponentsFixture.EnterAsync(enemy);
            var target=await actor.WaitForViewNamedAsync(enemy.Handle);var observer=await enemy.WaitForViewNamedAsync(actor.Handle);
            enemy.SendRpc("42 03 00 00 00 00 00");await enemy.WaitForRpcAsync(ModPattern(1013,1012)); // Ordinary Block, no injected condition/RNG.
            Cast(actor,1112);var style=await actor.WaitForRpcAsync(ModPattern(1112,1032));
            Check.Equal(66,BitConverter.ToInt32(style.Bytes,13),"Stored binary32 1.3*50 truncation64 plus min2 = direct66");
            string parents=await context.Database.ExecuteAsync($"SELECT slot,ability_id,level,is_loaded FROM char_abilities WHERE char_id={actor.CharId} AND ability_id<>2147651584 ORDER BY slot");
            async Task Stage(bool loaded,int total)
            {
                stage=loaded?"loaded164":"unloaded164";actor.SkipReceived();int loadCursor=actor.World.RpcSnapshot().Count;
                SetAbilityLoaded(actor,ThrownAccuracyFixture.WeakPointsSlot,164,50,loaded);
                await actor.WaitForRpcAsync(loaded?"80 b2 a4 00":"80 b3 a4 00");
                await actor.WaitForRpcAsync(PassiveRecord(164,1145,10,!loaded));
                Check.Equal(loaded?1L:0L,await context.Database.LongAsync($"SELECT is_loaded FROM char_abilities WHERE char_id={actor.CharId} AND slot={ThrownAccuracyFixture.WeakPointsSlot} AND ability_id=2147651584"),"Normal owned164 loaded state");
                Check.Equal(parents,await context.Database.ExecuteAsync($"SELECT slot,ability_id,level,is_loaded FROM char_abilities WHERE char_id={actor.CharId} AND ability_id<>2147651584 ORDER BY slot"),"Only164 changes; all actual owned parents fixed");
                actor.SkipReceived();enemy.SkipReceived();actor.SendRpc("40 "+HandleOf(target)+" 01 6b");
                await actor.WaitForRpcAsync("46 "+HandleOf(target)+" 01");await actor.WaitForRpcAsync("45");
                int cursor=actor.World.RpcSnapshot().Count,logCursor=context.Server.DebugLog().Length;
                Cast(actor,134,target.ViewId);
                await actor.WaitUntilAsync(()=>actor.World.RpcSnapshot().Skip(cursor).Any(r=>ThrownAccuracyFixture.QueuedLegSweep(r.Bytes)),"Actual new RPC47 kind6 names queued134",TimeSpan.FromSeconds(10));
                await actor.WaitUntilAsync(()=>context.Server.DebugLog()[logCursor..].Contains("[COMBAT-ATTACK OURS] special 134 stat 1032 total "+total),"Normal queued executor consumes exact thrown score",TimeSpan.FromSeconds(15));
                var starts=actor.World.RpcSnapshot().Skip(cursor).Where(r=>r.Bytes.Length==11&&r.Bytes[0]==0x80&&r.Bytes[1]==0xbd&&r.Bytes[2]==1&&BitConverter.ToUInt16(r.Bytes,3)==134&&r.Bytes[5]==4&&r.Bytes[10]==1).ToArray();
                Check.That(starts.Length>0,"Normal134 own cooldown starts");Check.Equal(12f,BitConverter.ToSingle(starts[0].Bytes,6),"Unchanged12s reuse");
                var queue=actor.World.RpcSnapshot().Skip(cursor).First(r=>ThrownAccuracyFixture.QueuedLegSweep(r.Bytes));
                observations.Add(new{stage,Loaded=loaded,Direct1032=66,Bonus1145=loaded?10:0,OursDelta=loaded?5:0,Total=total,LoadCursor=loadCursor,CastCursor=cursor,Queue=queue.Hex,Log=context.Server.DebugLog()[logCursor..]});
                actor.SkipReceived();enemy.SkipReceived();actor.SendRpc("44 00");enemy.SendRpc("44 00");
                await actor.WaitForRpcAsync("46 "+HandleOf(target)+" 00",TimeSpan.FromSeconds(30));
                await enemy.WaitForRpcAsync("46 "+HandleOf(observer)+" 00",TimeSpan.FromSeconds(30));
                await actor.WaitUntilAsync(()=>actor.World.RpcSnapshot().Skip(cursor).Any(r=>r.Bytes.Length==11&&r.Bytes[0]==0x80&&r.Bytes[1]==0xbd&&r.Bytes[2]==1&&BitConverter.ToUInt16(r.Bytes,3)==134&&r.Bytes[5]==4&&BitConverter.ToSingle(r.Bytes,6)==12f&&r.Bytes[10]==0),"Real134 cooldown expiry before next stage",TimeSpan.FromSeconds(20));
            }
            await Stage(false,66);await Stage(true,71);await Stage(false,66);
            Check.Equal(0,players.Sum(p=>p.World.Problems.Count),"Both parser problem counts zero");passed=true;stage="complete";
        }
        catch(Exception e){failure=e;throw;}
        finally
        {
            string path=Path.Combine(Path.GetTempPath(),"mxo-thrown-accuracy-"+context.Database.Name+".json");string owned="";
            try{if(players.Count>0)owned=await context.Database.ExecuteAsync("SELECT char_id,slot,ability_id,level,is_loaded FROM char_abilities WHERE char_id IN("+string.Join(",",players.Select(p=>p.CharId))+") ORDER BY char_id,slot");}catch(Exception e){owned="Capture failure: "+e;}
            try{await File.WriteAllTextAsync(path,JsonSerializer.Serialize(new{Passed=passed,Stage=stage,Failure=failure?.ToString(),ExpectedSource=expected,ActualLaunchSource=context.Server.SourceAssemblySha256,
                Source=context.Server.SourceAssembly,SourceFileSha=Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(context.Server.SourceAssembly))),ServerPid=context.Server.ProcessId,Schema=context.Database.Name,Workdir=context.Server.WorkingDirectory,Observations=observations,OwnedRows=owned,
                Sessions=players.Select(p=>new{p.Handle,p.CharId,Rpc=p.World.RpcSnapshot().Select(r=>r.Hex),Views=p.World.ViewSnapshot().Select(Convert.ToHexString),Problems=p.World.Problems}),
                Debug=context.Server.DebugLog(),Output=context.Server.FullOutput(),RandomOverride=false,NativeProven=false,Policy="OURS existing level-percent conversion for primary1032/1145 link; natural RNG, real134 queue/cooldown, helper normalBlock; no causal random-hit claim"},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine("Thrown accuracy proof: "+path);}
            catch(Exception e){if(failure==null)throw;Console.Error.WriteLine(e);}
        }
    }
}
