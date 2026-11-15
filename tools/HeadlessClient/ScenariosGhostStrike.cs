using System.Text.Json;
using System.Security.Cryptography;
using hds;
namespace HeadlessClient;
public static partial class Scenarios
{
    private static async Task GhostStrikeCallback(ScenarioContext context)
    {
        using var caster=await GhostStrikeFixture.CsrAsync(context);
        var victim=await context.AddPlayerAsync(GhostStrikeFixture.Seed("GhostEnemy",true));
        var observer=await context.AddPlayerAsync(GhostStrikeFixture.Seed("GhostFriend") with{X=24900});
        var proof=new List<object>();bool passed=false;string stage="entry";
        string profiles=await context.Database.ExecuteAsync($"SELECT charId,level,alignment,x,y,z,healthC,innerStrC FROM characters WHERE charId IN (110,{victim.CharId},{observer.CharId}) ORDER BY charId");
        string owned=await context.Database.ExecuteAsync("SELECT slot,ability_id,level,is_loaded FROM char_abilities WHERE char_id=110 ORDER BY slot,id");
        try
        {
            await caster.EnterWorldAsync();await victim.EnterWorldAsync();await observer.EnterWorldAsync();
            foreach(var p in new[]{caster,victim}){p.SendChat("?pvpflag on");await p.WaitForViewUpdateAsync(2,OwnPvpFlagOn);}
            var target=await caster.WaitForViewNamedAsync(victim.Handle);var observed=await observer.WaitForViewNamedAsync(caster.Handle);
            Cast(caster,1112);await caster.WaitForRpcAsync(ModPattern(1112,1034));
            stage="no-invisibility-current-base-policy";int noInvisibility=context.Server.DebugLog().Length;
            Cast(caster,365,target.ViewId);await caster.WaitUntilAsync(()=>context.Server.DebugLog()[noInvisibility..].Contains(" lands on GhostEnemy for "),"ordinary hit without supported invisibility",TimeSpan.FromSeconds(6));
            Check.That(!context.Server.DebugLog()[noInvisibility..].Contains("[GHOST-STRIKE] accepted callback"),"No unsupported Test/refund callback is fabricated");
            proof.Add(new{stage,Log=context.Server.DebugLog()[noInvisibility..]});await Task.Delay(11000);
            foreach(int roll in new[]{9,8})
            {
                stage="roll-"+roll;caster.SkipReceived();caster.SendChat("?ghoststrike "+roll);await caster.WaitForTextAsync("callback roll="+roll);
                observer.SkipReceived();Cast(caster,1116);var initial=await caster.WaitForRpcAsync(ModPattern(1116,115));
                await observer.WaitForViewUpdateAsync(observed.ViewId,WireFormat.ToHex(Stealth.BuildOther(Stealth.Invisible,Stealth.StealthedFlag)));
                double originalDuration=BitConverter.ToSingle(initial.Bytes,18);int cursor=caster.World.RpcSnapshot().Count;
                int log=context.Server.DebugLog().Length;var before=await context.Database.ExecuteAsync("SELECT healthC,innerStrC FROM characters WHERE charId=110");
                DateTime start=DateTime.UtcNow;Cast(caster,365,target.ViewId);
                await caster.WaitForRpcAsync(ModPattern(1116,115,true)); // this cast keeps the ordinary break
                await observer.WaitForViewUpdateAsync(observed.ViewId,WireFormat.ToHex(Stealth.BuildOther(0,0)));
                await caster.WaitUntilAsync(()=>context.Server.DebugLog()[log..].Contains(" lands on GhostEnemy for "),"accepted ordinary365 hit",TimeSpan.FromSeconds(6));
                await Task.Delay(500);
                var restorations=caster.World.RpcSnapshot().Skip(cursor).Where(r=>BytePattern.Parse(ModPattern(1116,115)).IsMatch(r.Bytes)).ToArray();
                Check.Equal(roll==8?1:0,restorations.Length,"Only successful callback creates one normal1116 modifier");
                if(roll==8)
                {
                    float remaining=BitConverter.ToSingle(restorations.Single().Bytes,18);
                    Check.That(remaining>0&&remaining<originalDuration-1,"Restoration uses remaining original duration, never a fresh130s grant");
                    Check.Equal(1,System.Text.RegularExpressions.Regex.Matches(context.Server.DebugLog()[log..],@"\[GHOST-STRIKE\] accepted callback").Count,"Once per accepted cast");
                    await observer.WaitForViewUpdateAsync(observed.ViewId,WireFormat.ToHex(Stealth.BuildOther(Stealth.Invisible,Stealth.StealthedFlag)));
                    // Turning the normal restored grant off uses its own standard activation path.
                    Cast(caster,1116);await caster.WaitForRpcAsync(ModPattern(1116,115,true));
                }
                proof.Add(new{stage,roll,Started=start,Original=Convert.ToHexString(initial.Bytes),Restorations=restorations.Select(r=>Convert.ToHexString(r.Bytes)),Before=before,
                    After=await context.Database.ExecuteAsync("SELECT healthC,innerStrC FROM characters WHERE charId=110"),Log=context.Server.DebugLog()[log..]});
                await Task.Delay(31000); // normal1116 term cooldown30s and365 reuse10s; no timer injection
            }
            stage="npc-accepted";caster.SkipReceived();caster.SendChat("?ghoststrike 8");await caster.WaitForTextAsync("callback roll=8");
            var npc=await caster.WaitForViewNamedAsync("Trainman");
            Cast(caster,1116);await caster.WaitForRpcAsync(ModPattern(1116,115));int npcLog=context.Server.DebugLog().Length;
            Cast(caster,365,npc.ViewId);await caster.WaitForRpcAsync(ModPattern(1116,115,true));await caster.WaitForRpcAsync(ModPattern(1116,115));
            Check.That(context.Server.DebugLog()[npcLog..].Contains("[ABILITY] 365 lands"),"NPC ordinary accepted-hit path invokes source callback");
            proof.Add(new{stage,NpcView=npc.ViewId,NpcSpawn=npc.SpawnId,Log=context.Server.DebugLog()[npcLog..]});
            Cast(caster,1116);await caster.WaitForRpcAsync(ModPattern(1116,115,true));await Task.Delay(31000);
            stage="actual-world-departure";Cast(caster,1116);await caster.WaitForRpcAsync(ModPattern(1116,115));
            int cancelLog=context.Server.DebugLog().Length,cancelRpc=caster.World.RpcSnapshot().Count;
            var clock=System.Diagnostics.Stopwatch.StartNew();Cast(caster,365,target.ViewId);
            var bar=await caster.WaitForRpcAsync("80 ac 6d 01");float seconds=BitConverter.ToSingle(bar.Bytes,bar.Bytes.Length-4);
            Check.Equal(1.5f,seconds,"Stored normal365 cast window");
            caster.SendRpc("80 fc 01");var finish=await caster.WaitForRpcAsync("80 fd");caster.SendRpc("80 fe 01");var reset=await caster.WaitForRpcAsync("81 07");
            double boundaryMs=clock.Elapsed.TotalMilliseconds;Check.That(boundaryMs<seconds*1000,"Actual world reset precedes cast due time");
            Check.That(context.Server.DebugLog()[cancelLog..].Contains("[LA] Loading the Loading Area"),"Real departure changed world lifecycle");
            caster.SendRpc("81 08");await caster.WaitForRpcAsync("81 09");caster.SendRpc("05");
            await caster.WaitUntilAsync(()=>caster.World.RpcSnapshot().Skip(cancelRpc).Any(r=>BytePattern.IndexOf(r.Bytes,System.Text.Encoding.UTF8.GetBytes("la.metr"),0)>=0),"Actual LA world command",TimeSpan.FromSeconds(4));
            await Task.Delay(2000);Check.That(!context.Server.DebugLog()[cancelLog..].Contains("[GHOST-STRIKE] accepted callback"),"Actual departure cancels callback");
            proof.Add(new{stage,seconds,boundaryMs,CastBar=bar.Hex,Finish=finish.Hex,Reset=reset.Hex,Log=context.Server.DebugLog()[cancelLog..]});
            Check.Equal(0,caster.World.Problems.Count+victim.World.Problems.Count+observer.World.Problems.Count,"Protocol parsing");passed=true;
        }
        finally
        {
            string stem=Path.Combine(Path.GetTempPath(),"mxo-ghost-strike-"+context.Database.Name);
            await File.WriteAllTextAsync(stem+".json",JsonSerializer.Serialize(new{passed,stage,profiles,owned,proof,Database=context.Database.Name,ServerPid=context.Server.ProcessId,
                SourceAssembly=context.Server.SourceAssembly,SourceSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(context.Server.SourceAssembly))),Environment=GhostStrikeFixture.Environment,
                Actors=new[]{caster,victim,observer}.Select(p=>new{p.Handle,p.CharId,Rpc=p.World.RpcSnapshot().Select(r=>r.Hex),Views=p.World.ViewSnapshot().Select(Convert.ToHexString)}),Final=await context.Database.ExecuteAsync("SELECT charId,healthC,innerStrC,exp,cash FROM characters WHERE charId=110"),
                Debug=context.Server.DebugLog()},new JsonSerializerOptions{WriteIndented=true}));
            await File.WriteAllTextAsync(stem+".log",context.Server.FullOutput()+"\n"+context.Server.DebugLog());
        }
    }
}
