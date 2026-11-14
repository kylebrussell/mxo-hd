using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Text.Json;
using hds;
namespace HeadlessClient;
public static partial class Scenarios
{
    private static async Task ConditionalThrownDamageWire(ScenarioContext context)
    {
        var caster=await context.AddPlayerAsync(ConditionalThrowFixture.Seed("ThrowCaster",0,1));
        var victim=await context.AddPlayerAsync(ConditionalThrowFixture.Seed("ThrowVictim",100,2));
        var npcActor=await context.AddPlayerAsync(ConditionalThrowFixture.Seed("ThrowNpc",200,1));
        var cancelled=await context.AddPlayerAsync(ConditionalThrowFixture.Seed("ThrowCancelled",300,1));
        var actors=new[]{caster,victim,npcActor,cancelled};var profiles=new List<object>();var measurements=new List<object>();string stage="entry";bool passed=false;Exception? failure=null;
        try
        {
            foreach(var p in actors)
            {
                profiles.Add(new{p.Handle,p.CharId,Character=await context.Database.ExecuteAsync($"SELECT charId,districtId,x,y,z,level,exp,alignment,healthC,healthM FROM characters WHERE charId={p.CharId}"),Owned=await context.Database.ExecuteAsync($"SELECT slot,ability_id,level,is_loaded FROM char_abilities WHERE char_id={p.CharId} ORDER BY slot,id")});
                Check.Equal((long)ConditionalThrowFixture.Abilities.Length,await context.Database.LongAsync($"SELECT COUNT(*) FROM char_abilities WHERE char_id={p.CharId} AND slot<520 AND is_loaded=1"),"Only declared fixture archive abilities are loaded before world entry");
            }
            foreach(var p in actors){await p.EnterWorldAsync();p.SendChat("?pvpflag on");await p.WaitForViewUpdateAsync(2,OwnPvpFlagOn);}
            var target=await caster.WaitForViewNamedAsync(victim.Handle);var npc=await npcActor.WaitForViewNamedAsync("Trainman");
            async Task Prepare(HeadlessPlayer actor,ViewCreation view,string state,int seconds=30)
            {
                actor.SkipReceived();actor.SendRpc($"81 51 {Le(view.ViewId)} {view.SpawnId:x2}");actor.SendChat($"?combatdepth {state} {seconds}");
                await actor.WaitForTextAsync(state=="offbalance"?"OffBalance fixture applied 885/200":"Daze fixture applied 884/199");
            }
            async Task Hit(HeadlessPlayer actor,ViewCreation view,ushort ability,string name,int bonus,bool duplicate=false)
            {
                int start=context.Server.DebugLog().Length,rpc=actor.World.RpcSnapshot().Count;
                actor.SkipReceived();Cast(actor,ability,view.ViewId);await actor.WaitForRpcAsync($"80 ac {Le(ability)}");
                if(duplicate)actor.World.ResendLastRpcs();
                await actor.WaitUntilAsync(()=>context.Server.DebugLog()[start..].Contains(actor==npcActor?$"[ABILITY] {ability} lands":$"[PVP] {actor.Handle}'s"),"Actual stored two-second throw lands",TimeSpan.FromSeconds(8));
                await Task.Delay(200);
                string log=context.Server.DebugLog()[start..];var hits=Regex.Matches(log,@"\[CONDITIONAL-THROW\] "+ability+" on "+Regex.Escape(name)+@" value([0-9]+) raw([0-9]+) final([0-9]+) hp([0-9]+)->([0-9]+)");
                Check.Equal(bonus==0?0:1,hits.Count,"Exactly one conditional component for this actual cast, or none without Off Balance");
                if(bonus>0){var hit=hits.Single();Check.Equal(bonus,int.Parse(hit.Groups[3].Value),"Independent conditional damage literal with Spy Style29");Check.Equal(bonus,int.Parse(hit.Groups[4].Value)-int.Parse(hit.Groups[5].Value),"Authoritative health loses the conditional amount once");}
                measurements.Add(new{Stage=stage,Ability=ability,Target=name,ExpectedConditional=bonus,Log=log,RpcStart=rpc,View=view});
            }
            stage="none-player279";await Hit(caster,target,279,victim.Handle,0);
            await Task.Delay(10500);
            stage="daze-player282";await Prepare(caster,target,"daze");await Hit(caster,target,282,victim.Handle,0);
            stage="offbalance-player279";await Prepare(caster,target,"offbalance");await Hit(caster,target,279,victim.Handle,58,true);
            var style=caster.Received(ModPattern(1112,1034)).Last(r=>r.Bytes[17]==0);Check.Equal(29,BitConverter.ToInt32(style.Bytes,13),"Actual Spy Style influence is29");
            await Task.Delay(8500);
            stage="offbalance-player282";await Prepare(caster,target,"offbalance");await Hit(caster,target,282,victim.Handle,30);
            await Task.Delay(10500);
            stage="expired-player279";victim.SkipReceived();await Prepare(caster,target,"offbalance",1);
            var armed=await victim.WaitForRpcAsync(r=>BytePattern.Parse(ModPattern(885,200)).IsMatch(r.Bytes)&&r.Bytes[17]==0&&BitConverter.ToSingle(r.Bytes,18)==1,"New one-second885 state",TimeSpan.FromSeconds(4));
            ushort record=BitConverter.ToUInt16(armed.Bytes,4);
            await victim.WaitForRpcAsync(r=>BytePattern.Parse(ModPattern(885,200,true)).IsMatch(r.Bytes)&&BitConverter.ToUInt16(r.Bytes,4)==record,"Exact885 state natural expiry",TimeSpan.FromSeconds(4));await Hit(caster,target,279,victim.Handle,0);
            stage="npc-interlocked279";
            var pos=npc.Attributes["Position"];await MoveForMissionTest(npcActor,BitConverter.ToDouble(pos)+100,BitConverter.ToDouble(pos,8),BitConverter.ToDouble(pos,16));
            await Prepare(npcActor,npc,"offbalance");npcActor.SendRpc("3d 01");npcActor.SendRpc($"40 {HandleOf(npc)} 00 6b");await npcActor.WaitForRpcAsync("46");
            await Hit(npcActor,npc,279,"Trainman",58,true);npcActor.SendRpc("44 00");await npcActor.WaitForRpcAsync("46");
            stage="npc-free282";await Prepare(npcActor,npc,"offbalance");await Hit(npcActor,npc,282,"Trainman",30);
            stage="world-departure-before-cast";var cancelTarget=await cancelled.WaitForViewNamedAsync(victim.Handle);await Prepare(cancelled,cancelTarget,"offbalance");int cancelledStart=context.Server.DebugLog().Length;
            int departureRpc=cancelled.World.RpcSnapshot().Count;var departureClock=System.Diagnostics.Stopwatch.StartNew();
            Cast(cancelled,279,cancelTarget.ViewId);var castBar=await cancelled.WaitForRpcAsync("80 ac 17 01");float castSeconds=BitConverter.ToSingle(castBar.Bytes,castBar.Bytes.Length-4);Check.Equal(2f,castSeconds,"Real stored cast window");
            cancelled.SendRpc("80 fc 01");var finish=await cancelled.WaitForRpcAsync("80 fd");cancelled.SendRpc("80 fe 01");var reset=await cancelled.WaitForRpcAsync("81 07");
            double boundaryMs=departureClock.Elapsed.TotalMilliseconds;Check.That(boundaryMs<castSeconds*1000,"Observable world reset arrives before the earliest cast completion");
            Check.That(context.Server.DebugLog()[cancelledStart..].Contains("[LA] Loading the Loading Area"),"Existing GoToLoadingArea changed district and world epoch before reset");
            cancelled.SendRpc("81 08");await cancelled.WaitForRpcAsync("81 09");cancelled.SendRpc("05");
            await cancelled.WaitUntilAsync(()=>cancelled.World.RpcSnapshot().Skip(departureRpc).Any(r=>BytePattern.IndexOf(r.Bytes,System.Text.Encoding.UTF8.GetBytes("la.metr"),0)>=0),"Actual Loading Area world command",TimeSpan.FromSeconds(4));
            await Task.Delay(3000);
            measurements.Add(new{Stage=stage,WorldDeparture="80fc01 /80fd /80fe01 /8107 /8108 /8109 /05 +la.metr",BoundaryMs=boundaryMs,CastSeconds=castSeconds,CastBar=castBar.Hex,Finish=finish.Hex,Reset=reset.Hex,Log=context.Server.DebugLog()[cancelledStart..],ExpectedConditional=0});
            Check.That(!context.Server.DebugLog()[cancelledStart..].Contains("[CONDITIONAL-THROW]"),"Completed Loading Area world change during cast cannot produce a conditional hit");
            int after=context.Server.DebugLog().Length;await Task.Delay(3000);Check.That(!context.Server.DebugLog()[after..].Contains("[CONDITIONAL-THROW]"),"DOT ticks and state expiry never rerun the initializer");
            Check.Equal(0,actors.Sum(a=>a.World.Problems.Count),"All conditional throw sessions parse cleanly");passed=true;stage="complete";
        }
        catch(Exception error){failure=error;throw;}
        finally
        {
            try
            {
                string stem=Path.Combine(Path.GetTempPath(),"mxo-conditional-throw-"+context.Database.Name);
                await File.WriteAllTextAsync(stem+".log",context.Server.FullOutput()+"\n"+context.Server.DebugLog());
                await File.WriteAllTextAsync(stem+".json",JsonSerializer.Serialize(new{Passed=passed,Stage=stage,Failure=failure?.ToString(),Database=context.Database.Name,ServerPid=context.Server.ProcessId,SourceAssembly=context.Server.SourceAssembly,SourceSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(context.Server.SourceAssembly))),FixtureEnvironment=ConditionalThrowFixture.Environment,InitialProfiles=profiles,Measurements=measurements,Sessions=actors.Select(a=>new{a.Handle,a.CharId,Rpc=a.World.RpcSnapshot().Select(r=>Convert.ToHexString(r.Bytes)).ToArray(),Views=a.World.ViewSnapshot().Select(Convert.ToHexString).ToArray(),Problems=a.World.Problems.ToArray()}),RandomOverrides=false,QueueQualification="Stored2s279/282 casts bypass current round queue even while interlocked; this does not implement or claim queued hit parity.",NativeProven=false},new JsonSerializerOptions{IncludeFields=true,WriteIndented=true}));
            }
            catch(Exception proofError){if(failure==null)throw;Console.Error.WriteLine("Conditional throw proof capture failed: "+proofError);}
        }
    }
}
