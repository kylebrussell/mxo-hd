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
            actor.SkipReceived();Cast(actor,1112);var style=await actor.WaitForRpcAsync(ModPattern(1112,1005)); // Normal priming so first17 transition is not a default-style no-op.
            Check.Equal(72,BitConverter.ToInt32(style.Bytes,13),"Actual SpyStyle1005 min8+binary32 1.3*50 truncation64");
            string parents=await context.Database.ExecuteAsync($"SELECT slot,ability_id,level,is_loaded FROM char_abilities WHERE char_id={actor.CharId} ORDER BY slot");
            async Task Stage(ushort selectedStyle,int direct)
            {
                stage="style"+selectedStyle;
                Check.Equal(parents,await context.Database.ExecuteAsync($"SELECT slot,ability_id,level,is_loaded FROM char_abilities WHERE char_id={actor.CharId} ORDER BY slot"),"All actual owned/loaded parents fixed through active style changes");
                // Enter with ordinary Power, then select ordinary Block in the active fight. No forced hit, control or health edit.
                actor.SkipReceived();actor.SendRpc("42 01 00 00 00 00 00");await actor.WaitForRpcAsync(ModPattern(1017,1012));
                actor.SkipReceived();enemy.SkipReceived();actor.SendRpc("40 "+HandleOf(target)+" 01 6b");await actor.WaitForRpcAsync("46 "+HandleOf(target)+" 01");await actor.WaitForRpcAsync("45");
                // Native radial CloseCombat can select17; select/verify the wanted style AFTER engagement.
                actor.SkipReceived();Cast(actor,selectedStyle);var actualStyle=await actor.WaitForRpcAsync(ModPattern(selectedStyle,1005));
                Check.Equal(direct,BitConverter.ToInt32(actualStyle.Bytes,13),"Fresh ordinary active-style direct1005 after engagement");
                actor.SkipReceived();actor.SendRpc("42 03 00 00 00 00 00");await actor.WaitForRpcAsync(ModPattern(1013,1012));
                int cursor=actor.World.RpcSnapshot().Count,logCursor=context.Server.DebugLog().Length;actor.SkipReceived();actor.SendRpc("44 01");
                int input=direct,total=50+input;
                string inputLine=$"[WITHDRAW INPUT OURS] WithdrawActor direct={direct} input={input}";
                await actor.WaitUntilAsync(()=>context.Server.DebugLog()[logCursor..].Contains(inputLine)&&context.Server.DebugLog()[logCursor..].Contains($"[WITHDRAW ROLL OURS] tactic=Block level=50 input={input} base={total} roll="),"Actual ordinary44 primary direct1005/Block withdrawal input",TimeSpan.FromSeconds(15));
                await actor.WaitForRpcAsync("46 "+HandleOf(target)+" 00",TimeSpan.FromSeconds(30));await enemy.WaitForRpcAsync("46 "+HandleOf(reverse)+" 00",TimeSpan.FromSeconds(30));
                var round=actor.World.RpcSnapshot().Skip(cursor).FirstOrDefault(r=>WithdrawalFixture.WithdrawalRound(r.Bytes));Check.That(round!=null,"Fresh complete89-byte actual contested withdrawal result0x200/combatResult>0");
                observations.Add(new{stage,ActiveStyle=selectedStyle,Direct1005=direct,Linked1045Consumed=false,ActualInput=input,AttackBase=total,RpcCursor=cursor,Round=round!.Hex,Log=context.Server.DebugLog()[logCursor..],RandomOverride=false});
            }
            await Stage(17,65);await Stage(1112,72);await Stage(17,65);
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
                Debug=context.Server.DebugLog(),Output=context.Server.FullOutput(),RandomOverride=false,NativeProven=false,Scope="Actual ordinary44 raw1005 style17/1112/17 inputs,1045 unimplemented, normal opponentBlock and naturally rolled withdrawal; no win-rate or damage comparison; no TF6 escape change"},new JsonSerializerOptions{WriteIndented=true}));
        }
    }
}
