using System.Text.Json;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using hds;
namespace HeadlessClient;

public static partial class Scenarios
{
    private static async Task PunishingBlowsWire(ScenarioContext context)
    {
        var actor=await context.AddPlayerAsync(PunishingBlowsFixture.Seed("PunishingShooter",0,1));
        var target=await context.AddPlayerAsync(PunishingBlowsFixture.Seed("PunishingTarget",100,2));
        var measurements=new List<object>();bool passed=false;string stage="entry";Exception? failure=null;
        try
        {
            foreach(var player in new[]{actor,target})
            {await player.EnterWorldAsync();player.SendChat("?pvpflag on");await player.WaitForViewUpdateAsync(2,OwnPvpFlagOn);}
            var view=await actor.WaitForViewNamedAsync(target.Handle);
            async Task Fire(int percent,bool vulnerable)
            {
                int logCursor=context.Server.DebugLog().Length,rpcCursor=actor.World.RpcSnapshot().Count;
                actor.SendRpc("3d 01");actor.SendRpc("41 "+HandleOf(view));
                Match? hit=null;
                await actor.WaitUntilAsync(()=>
                {
                    hit=Regex.Matches(context.Server.DebugLog()[logCursor..],@"\[PUNISHING-BLOWS OURS\] PunishingShooter -> PunishingTarget raw(\d+) stat(\d+) vulnerable(True|False) final(\d+)")
                        .FirstOrDefault(m=>int.Parse(m.Groups[2].Value)==percent&&bool.Parse(m.Groups[3].Value)==vulnerable);
                    return hit!=null;
                },"Ordinary winning rifle contest in requested current state",TimeSpan.FromSeconds(20));
                actor.SendRpc("44 00");
                int raw=int.Parse(hit!.Groups[1].Value),actual=int.Parse(hit.Groups[4].Value);
                int expected=raw*(100+(vulnerable?percent:0))/100;
                Check.Equal(expected,actual,"Independent integer percent, with no extra base15");
                await actor.WaitUntilAsync(()=>actor.World.RpcSnapshot().Skip(rpcCursor).Any(r=>r.Bytes.Length>=23&&r.Bytes[0]==0x81&&r.Bytes[1]==0x68&&
                    BitConverter.ToUInt32(r.Bytes,6)==3&&r.Bytes[22]==1&&BitConverter.ToUInt16(r.Bytes,10)==actual),"Received ordinary combat log contains final damage",TimeSpan.FromSeconds(5));
                measurements.Add(new{Stage=stage,Raw=raw,Percent=percent,Vulnerable=vulnerable,Actual=actual,Expected=expected,RpcCursor=rpcCursor,Log=context.Server.DebugLog()[logCursor..]});
            }
            stage="off-healthy";await Fire(0,false);
            stage="normal16-on";actor.SkipReceived();Cast(actor,16,2);
            var modifier=await actor.WaitForRpcAsync(r=>BytePattern.Parse(ModPattern(16,212)).IsMatch(r.Bytes)&&r.Bytes[17]==0,"Normal16 stat212 activation",TimeSpan.FromSeconds(8));
            Check.Equal(15,BitConverter.ToInt32(modifier.Bytes,13),"Actual stored16 grants15 once");
            stage="on-healthy";await Fire(15,false);
            stage="on-offbalance";actor.SendRpc($"81 51 {Le(view.ViewId)} {view.SpawnId:x2}");actor.SendChat("?combatdepth offbalance 30");
            await actor.WaitForTextAsync("OffBalance fixture applied 885/200");await Fire(15,true);
            stage="normal16-off";actor.SkipReceived();Cast(actor,16,2);
            await actor.WaitForRpcAsync(r=>BytePattern.Parse(ModPattern(16,212,true)).IsMatch(r.Bytes),"Normal toggle removes16 stat212",TimeSpan.FromSeconds(8));
            stage="off-offbalance";await Fire(0,true);
            Check.Equal(0,actor.World.Problems.Count+target.World.Problems.Count,"Both session parsers clean");
            passed=true;stage="complete";
        }
        catch(Exception error){failure=error;throw;}
        finally
        {
            string stem=Path.Combine(Path.GetTempPath(),"mxo-punishing-blows-"+context.Database.Name);
            await File.WriteAllTextAsync(stem+".json",JsonSerializer.Serialize(new{Passed=passed,Stage=stage,Failure=failure?.ToString(),
                Source=context.Server.SourceAssembly,SourceSha256=Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(context.Server.SourceAssembly))),
                ServerPid=context.Server.ProcessId,Schema=context.Database.Name,Measurements=measurements,Policy="OURS approximation; retail vulnerability counter unrecovered",
                Sessions=new[]{actor,target}.Select(p=>new{p.Handle,p.CharId,Rpcs=p.World.RpcSnapshot().Select(r=>r.Hex),Views=p.World.ViewSnapshot().Select(Convert.ToHexString),Problems=p.World.Problems}),
                Debug=context.Server.DebugLog(),Output=context.Server.FullOutput(),NativeProven=false,RandomOverride=false},new JsonSerializerOptions{WriteIndented=true}));
            Console.WriteLine("Punishing Blows proof: "+stem+".json");
        }
    }
}
