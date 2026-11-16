using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using hds;
namespace HeadlessClient;

public static partial class Scenarios
{
    private static async Task CombatInsightWire(ScenarioContext context)
    {
        string expected=System.Environment.GetEnvironmentVariable("MXO_COMBAT_INSIGHT_SOURCE_SHA256")??throw new InvalidOperationException("Exact Combat Insight source pin required");
        Check.Equal(expected,context.Server.SourceAssemblySha256,"Actual selected server source before authentication");
        var actor=await context.AddPlayerAsync(CombatInsightFixture.Seed("InsightAttacker",false));
        var target=await context.AddPlayerAsync(CombatInsightFixture.Seed("InsightTarget",true));
        var observations=new List<object>();string stage="entry";bool passed=false;Exception? failure=null;
        try
        {
            await PvpOpponentsFixture.EnterAsync(actor);await PvpOpponentsFixture.EnterAsync(target);
            var targetView=await actor.WaitForViewNamedAsync(target.Handle);var actorView=await target.WaitForViewNamedAsync(actor.Handle);
            string targetHandle=HandleOf(targetView),actorHandle=HandleOf(actorView);
            async Task Load(bool on)
            {
                actor.SkipReceived();SetAbilityLoaded(actor,CombatInsightFixture.InsightSlot,113,50,on);
                await actor.WaitForRpcAsync(on?"80 b2 71 00":"80 b3 71 00");
                await actor.WaitForRpcAsync(PassiveRecord(113,1048,10,!on));
                Check.Equal(on?1L:0L,await context.Database.LongAsync($"SELECT is_loaded FROM char_abilities WHERE char_id={actor.CharId} AND slot={CombatInsightFixture.InsightSlot} AND ability_id=2147599360"),"Normal owned113 load state");
            }
            async Task Enter(int force,int evade,bool allowed)
            {
                actor.SkipReceived();target.SkipReceived();int cursor=actor.World.RpcSnapshot().Count,logCursor=context.Server.DebugLog().Length;
                actor.SendRpc("40 "+targetHandle+" 01 6b");
                if(allowed)
                {
                    await actor.WaitForRpcAsync("46 "+targetHandle+" 01");await actor.WaitForRpcAsync("45");
                }
                else
                {
                    await actor.WaitForRpcAsync("4d 0e 00 01 00 "+targetHandle);
                    Check.That(!actor.World.RpcSnapshot().Skip(cursor).Any(r=>r.Bytes[0]==0x45||r.Bytes[0]==0x47),"Refused entry has no fresh combat round");
                    Check.That(!context.Server.DebugLog()[logCursor..].Contains("pulls InsightTarget into close combat"),"Refusal precedes interlock construction");
                }
                string log=context.Server.DebugLog()[logCursor..];
                var score=Regex.Match(log,@"\[COMBAT-INSIGHT OURS\] InsightAttacker -> InsightTarget force(\d+) evade(\d+) allowed(True|False)");
                Check.That(score.Success,"Actual current entry score log");Check.Equal(force,int.Parse(score.Groups[1].Value),"Independent force literal");
                Check.Equal(evade,int.Parse(score.Groups[2].Value),"Independent evade literal");Check.Equal(allowed,bool.Parse(score.Groups[3].Value),"Actual entry verdict");
                observations.Add(new{Stage=stage,Force=force,Evade=evade,Allowed=allowed,RpcCursor=cursor,Log=log});
                if(allowed)
                {
                    actor.SkipReceived();target.SkipReceived();actor.SendRpc("44 00");target.SendRpc("44 00");
                    await actor.WaitForRpcAsync("46 "+targetHandle+" 00",TimeSpan.FromSeconds(20));
                    await target.WaitForRpcAsync("46 "+actorHandle+" 00",TimeSpan.FromSeconds(20));
                }
            }
            //Normal requests remove/reload the initially owned passive; no SQL rewrites after login.
            stage="normal113-unload";await Load(false);
            actor.SendRpc("42 01 00 00 00 00 00");await actor.WaitForRpcAsync(ModPattern(1017,1012));
            stage="normal217";Cast(target,217,2);await target.WaitForRpcAsync(ModPattern(217,624));
            stage="unloaded-refusal";await Enter(100,110,false);
            stage="normal113-load";await Load(true);
            stage="loaded-admission";await Enter(110,110,true);
            stage="normal49";target.SkipReceived();Cast(target,49,2);
            var blur=await target.WaitForRpcAsync(ModPattern(49,520),TimeSpan.FromSeconds(10));
            Check.Equal(25,BitConverter.ToInt32(blur.Bytes,13),"Actual49 direct520 value");Check.Equal(600f,BitConverter.ToSingle(blur.Bytes,18),"Unchanged49 duration");
            stage="blur-refusal";await Enter(110,135,false);
            stage="ordinary-grab";actor.SendRpc("42 02 00 00 00 00 00");await actor.WaitForRpcAsync(ModPattern(1016,1012));
            stage="grab-admission";await Enter(220,135,true);
            Check.Equal(0,actor.World.Problems.Count+target.World.Problems.Count,"Both parser problem counts zero");
            passed=true;stage="complete";
        }
        catch(Exception error){failure=error;throw;}
        finally
        {
            string stem=Path.Combine(Path.GetTempPath(),"mxo-combat-insight-"+context.Database.Name);
            try
            {
                await File.WriteAllTextAsync(stem+".json",JsonSerializer.Serialize(new{Passed=passed,Stage=stage,Failure=failure?.ToString(),ExpectedSource=expected,
                    ActualLaunchSource=context.Server.SourceAssemblySha256,Source=context.Server.SourceAssembly,SourceFileSha=Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(context.Server.SourceAssembly))),
                    ServerPid=context.Server.ProcessId,Schema=context.Database.Name,Workdir=context.Server.WorkingDirectory,Observations=observations,
                    OwnedRows=await context.Database.ExecuteAsync($"SELECT char_id,slot,ability_id,level,is_loaded FROM char_abilities WHERE char_id IN({actor.CharId},{target.CharId}) ORDER BY char_id,slot"),
                    Sessions=new[]{actor,target}.Select(p=>new{p.Handle,p.CharId,Rpc=p.World.RpcSnapshot().Select(r=>r.Hex),Views=p.World.ViewSnapshot().Select(Convert.ToHexString),Problems=p.World.Problems}),
                    Debug=context.Server.DebugLog(),Output=context.Server.FullOutput(),Policy="OURS deterministic entry scores; no retail shield/contest claim",RandomOverride=false,NativeProven=false},new JsonSerializerOptions{WriteIndented=true}));
                Console.WriteLine("Combat Insight proof: "+stem+".json");
            }
            catch(Exception proofError){if(failure==null)throw;Console.Error.WriteLine(proofError);}
        }
    }
}
