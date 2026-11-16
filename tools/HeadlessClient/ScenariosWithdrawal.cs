using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace HeadlessClient;
public static partial class Scenarios
{
    private static async Task OrdinaryWithdrawalWire(ScenarioContext context)
    {
        string expected=Environment.GetEnvironmentVariable(WithdrawalFixture.SourcePin)??throw new InvalidOperationException("Exact withdrawal source pin required");
        Check.Equal(expected,context.Server.SourceAssemblySha256,"Actual source before authentication");
        var players=new List<HeadlessPlayer>();var observations=new List<object>();bool passed=false;string stage="login";Exception? failure=null;
        try
        {
            var actor=await context.AddPlayerAsync(WithdrawalFixture.Seed("WithdrawActor",false));players.Add(actor);
            var enemy=await context.AddPlayerAsync(WithdrawalFixture.Seed("WithdrawBlock",true));players.Add(enemy);
            await PvpOpponentsFixture.EnterAsync(actor);await PvpOpponentsFixture.EnterAsync(enemy);
            var target=await actor.WaitForViewNamedAsync(enemy.Handle);var reverse=await enemy.WaitForViewNamedAsync(actor.Handle);
            enemy.SkipReceived();enemy.SendRpc("42 03 00 00 00 00 00");await enemy.WaitForRpcAsync(ModPattern(1013,1012));
            actor.SkipReceived();Cast(actor,1112);var style=await actor.WaitForRpcAsync(ModPattern(1112,1005));
            Check.Equal(72,BitConverter.ToInt32(style.Bytes,13),"Actual SpyStyle1005 min8+binary32 1.3*50 truncation64");
            string parents=await context.Database.ExecuteAsync($"SELECT slot,ability_id,level,is_loaded FROM char_abilities WHERE char_id={actor.CharId} AND ability_id<>2147606528 ORDER BY slot");
            async Task Stage(bool loaded)
            {
                stage=loaded?"loaded120":"unloaded120";actor.SkipReceived();SetAbilityLoaded(actor,WithdrawalFixture.DisguiseSlot,120,50,loaded);
                await actor.WaitForRpcAsync(loaded?"80 b2 78 00":"80 b3 78 00");await actor.WaitForRpcAsync(PassiveRecord(120,1045,25,!loaded));
                Check.Equal(loaded?1L:0L,await context.Database.LongAsync($"SELECT is_loaded FROM char_abilities WHERE char_id={actor.CharId} AND slot={WithdrawalFixture.DisguiseSlot} AND ability_id=2147606528"),"Normal owned120 loaded state");
                Check.Equal(parents,await context.Database.ExecuteAsync($"SELECT slot,ability_id,level,is_loaded FROM char_abilities WHERE char_id={actor.CharId} AND ability_id<>2147606528 ORDER BY slot"),"All actual parents fixed; only120 changed");
                // Enter with ordinary Power, then select ordinary Block in the active fight. No forced hit, control or health edit.
                actor.SkipReceived();actor.SendRpc("42 01 00 00 00 00 00");await actor.WaitForRpcAsync(ModPattern(1017,1012));
                actor.SkipReceived();enemy.SkipReceived();actor.SendRpc("40 "+HandleOf(target)+" 01 6b");await actor.WaitForRpcAsync("46 "+HandleOf(target)+" 01");await actor.WaitForRpcAsync("45");
                actor.SkipReceived();actor.SendRpc("42 03 00 00 00 00 00");await actor.WaitForRpcAsync(ModPattern(1013,1012));
                int cursor=actor.World.RpcSnapshot().Count,logCursor=context.Server.DebugLog().Length;actor.SkipReceived();actor.SendRpc("44 01");
                int bonus=loaded?25:0,delta=loaded?12:0,input=72+delta,total=50+input;
                string inputLine=$"[WITHDRAW INPUT OURS] WithdrawActor direct=72 bonus={bonus} delta={delta} input={input}";
                await actor.WaitUntilAsync(()=>context.Server.DebugLog()[logCursor..].Contains(inputLine)&&context.Server.DebugLog()[logCursor..].Contains($"[WITHDRAW ROLL OURS] tactic=Block level=50 input={input} base={total} roll="),"Actual ordinary44 raw/bonus/Block withdrawal input",TimeSpan.FromSeconds(15));
                await actor.WaitForRpcAsync("46 "+HandleOf(target)+" 00",TimeSpan.FromSeconds(30));await enemy.WaitForRpcAsync("46 "+HandleOf(reverse)+" 00",TimeSpan.FromSeconds(30));
                var round=actor.World.RpcSnapshot().Skip(cursor).FirstOrDefault(r=>WithdrawalFixture.WithdrawalRound(r.Bytes));Check.That(round!=null,"Fresh complete89-byte actual contested withdrawal result0x200/combatResult>0");
                observations.Add(new{stage,Loaded120=loaded,Direct1005=72,Bonus1045=bonus,OursDelta=delta,ActualInput=input,AttackBase=total,RpcCursor=cursor,Round=round!.Hex,Log=context.Server.DebugLog()[logCursor..],RandomOverride=false});
            }
            await Stage(false);await Stage(true);await Stage(false);
            Check.Equal(0,players.Sum(p=>p.World.Problems.Count),"Both session parsers clean");passed=true;stage="complete";
        }
        catch(Exception e){failure=e;throw;}
        finally
        {
            string owned="";try{if(players.Count>0)owned=await context.Database.ExecuteAsync("SELECT char_id,slot,ability_id,level,is_loaded FROM char_abilities WHERE char_id IN("+string.Join(",",players.Select(p=>p.CharId))+") ORDER BY char_id,slot");}catch(Exception e){owned="CAPTURE FAILED: "+e;}
            string path=Path.Combine(Path.GetTempPath(),"mxo-withdrawal-input-"+context.Database.Name+".json");
            await File.WriteAllTextAsync(path,JsonSerializer.Serialize(new{Passed=passed,Stage=stage,Failure=failure?.ToString(),ExpectedSource=expected,ActualLaunchSource=context.Server.SourceAssemblySha256,
                Source=context.Server.SourceAssembly,SourceFileSha=Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(context.Server.SourceAssembly))),ServerPid=context.Server.ProcessId,Schema=context.Database.Name,Workdir=context.Server.WorkingDirectory,
                Observations=observations,OwnedRows=owned,Sessions=players.Select(p=>new{p.Handle,p.CharId,Rpc=p.World.RpcSnapshot().Select(r=>r.Hex),Views=p.World.ViewSnapshot().Select(Convert.ToHexString),Problems=p.World.Problems}),
                Debug=context.Server.DebugLog(),Output=context.Server.FullOutput(),RandomOverride=false,NativeProven=false,Scope="Actual ordinary44 input off/on/off, normal opponentBlock and naturally rolled withdrawal; no win-rate or damage comparison; no TF6 escape change"},new JsonSerializerOptions{WriteIndented=true}));
        }
    }
}
