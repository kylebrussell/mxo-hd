using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace HeadlessClient;
public static partial class Scenarios
{
    private static async Task MultifightingWire(ScenarioContext context)
    {
        string expected=Environment.GetEnvironmentVariable("MXO_MULTIFIGHTING_SOURCE_SHA256")??throw new InvalidOperationException("Exact Multifighting source pin required");
        Check.Equal(expected,context.Server.SourceAssemblySha256,"Selected source before authentication");
        var players=new List<HeadlessPlayer>();var observations=new List<object>();bool passed=false;string stage="login";Exception? failure=null;
        try
        {
            for(int i=0;i<4;i++){var p=await context.AddPlayerAsync(MultifightingFixture.Seed("Multi"+i,i));players.Add(p);await PvpOpponentsFixture.EnterAsync(p);}
            var central=players[0];
            string parents=await context.Database.ExecuteAsync($"SELECT slot,ability_id,level,is_loaded FROM char_abilities WHERE char_id={central.CharId} AND ability_id<>2147627008 ORDER BY slot");
            async Task Load(bool on)
            {
                central.SkipReceived();SetAbilityLoaded(central,MultifightingFixture.Slot,140,50,on);
                await central.WaitForRpcAsync(on?"80 b2 8c 00":"80 b3 8c 00");
                Check.Equal(on?1L:0L,await context.Database.LongAsync($"SELECT is_loaded FROM char_abilities WHERE char_id={central.CharId} AND slot=402 AND ability_id=2147627008"),"Actual owned140 normal loaded state");
                Check.Equal(parents,await context.Database.ExecuteAsync($"SELECT slot,ability_id,level,is_loaded FROM char_abilities WHERE char_id={central.CharId} AND ability_id<>2147627008 ORDER BY slot"),"Same owned loaded parents before/after only140");
            }
            async Task Group(bool loaded)
            {
                int logCursor=context.Server.DebugLog().Length;
                for(int i=1;i<4;i++)
                {
                    var p=players[i];p.SkipReceived();var v=await p.WaitForViewNamedAsync(central.Handle);
                    p.SendRpc("40 "+HandleOf(v)+" 01 6b");await p.WaitForRpcAsync("45");
                    if(i<2)continue;
                    int count=i,penalty=loaded?(i==2?0:-5):(i==2?-10:-15);
                    await central.WaitUntilAsync(()=>Regex.IsMatch(context.Server.DebugLog()[logCursor..],@"\[MULTIFIGHTING OURS\] Multi0 opponents"+count+" loaded"+loaded+" penalty"+penalty+@" (attack|defence)-?\d+->-?\d+"),"Actual direct-link count and loaded140 penalty",TimeSpan.FromSeconds(15));
                    var matches=Regex.Matches(context.Server.DebugLog()[logCursor..],@"\[MULTIFIGHTING OURS\] Multi0 opponents"+count+" loaded"+loaded+@" penalty(-?\d+) (attack|defence)(-?\d+)->(-?\d+)");
                    foreach(Match m in matches){Check.Equal(penalty,int.Parse(m.Groups[1].Value),"Literal recovered penalty");Check.Equal(int.Parse(m.Groups[3].Value)+penalty,int.Parse(m.Groups[4].Value),"Exactly once on actual input");}
                    observations.Add(new{stage,Count=count,Loaded=loaded,Penalty=penalty,Inputs=matches.Select(m=>m.Value).ToArray()});
                }
                foreach(var p in players){p.SkipReceived();p.SendRpc("44 00");}
                foreach(var p in players)await p.WaitForRpcAsync("46 ?? ?? ?? ?? 00",TimeSpan.FromSeconds(120));
            }
            stage="off";await Load(false);await Group(false);
            stage="loaded";await Load(true);await Group(true);
            stage="unloaded";await Load(false);await Group(false);
            Check.Equal(0,players.Sum(p=>p.World.Problems.Count),"All parser problem counts zero");
            passed=true;stage="complete";
        }
        catch(Exception e){failure=e;throw;}
        finally
        {
            string path=Path.Combine(Path.GetTempPath(),"mxo-multifighting-"+context.Database.Name+".json");
            try{await File.WriteAllTextAsync(path,JsonSerializer.Serialize(new{Passed=passed,Stage=stage,Failure=failure?.ToString(),ExpectedSource=expected,
                ActualLaunchSource=context.Server.SourceAssemblySha256,Source=context.Server.SourceAssembly,SourceFileSha=Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(context.Server.SourceAssembly))),
                ServerPid=context.Server.ProcessId,Schema=context.Database.Name,Workdir=context.Server.WorkingDirectory,Observations=observations,
                OwnedRows=await context.Database.ExecuteAsync("SELECT char_id,slot,ability_id,level,is_loaded FROM char_abilities WHERE char_id IN("+string.Join(",",players.Select(p=>p.CharId))+") ORDER BY char_id,slot"),
                Sessions=players.Select(p=>new{p.Handle,p.CharId,Rpc=p.World.RpcSnapshot().Select(r=>r.Hex),Views=p.World.ViewSnapshot().Select(Convert.ToHexString),Problems=p.World.Problems}),
                Debug=context.Server.DebugLog(),Output=context.Server.FullOutput(),RandomOverride=false,NativeProven=false,
                Scope="Recovered math; OURS threshold1/current direct PvP links. Actual input delta, not causal random damage difference. Arbitrary multi-NPC not implemented."},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine("Multifighting proof: "+path);}
            catch(Exception e){if(failure==null)throw;Console.Error.WriteLine(e);}
        }
    }
}
