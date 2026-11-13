using System.Text;
using System.Text.RegularExpressions;
namespace HeadlessClient;
public static partial class Scenarios
{
    private static Task TypedAttackNpc(ScenarioContext context)=>TypedAttackQueued(context,false);
    private static Task TypedAttackPvp(ScenarioContext context)=>TypedAttackQueued(context,true);
    private static async Task TypedAttackQueued(ScenarioContext context,bool pvp)
    {
        var actor=await context.AddPlayerAsync(NearNpc("TypedAccuracy") with {Level=50,Organization=1,Health=5000,MaxHealth=5000,InnerStrength=2000,MaxInnerStrength=2000});
        var enemy=await context.AddPlayerAsync(NearNpc("TypedLag",100) with {Level=50,Organization=2,Health=5000,MaxHealth=5000,InnerStrength=2000,MaxInnerStrength=2000});
        await LoadAbilitiesAsync(context,actor,1112,134,314);await LoadAbilitiesAsync(context,enemy,1006,97);
        await actor.EnterWorldAsync();await enemy.EnterWorldAsync();
        actor.SendChat("?pvpflag on");await actor.WaitForViewUpdateAsync(2,OwnPvpFlagOn);
        enemy.SendChat("?pvpflag on");await enemy.WaitForViewUpdateAsync(2,OwnPvpFlagOn);
        Cast(actor,1112);await actor.WaitForRpcAsync(ModPattern(1112,1034));
        async Task<(int Total,int Hits)> Diagnostic(ushort move)
        {
            actor.SendChat("?combatdepth specialattack "+move);
            var rpc=await actor.WaitForTextAsync("Special attack "+move+" total ",TimeSpan.FromSeconds(5));
            var m=Regex.Match(Encoding.UTF8.GetString(rpc.Bytes),@"total (-?\d+); seeded landed (\d+)");
            Check.That(m.Success,"Read-only paired production contest diagnostic");
            return(int.Parse(m.Groups[1].Value),int.Parse(m.Groups[2].Value));
        }
        if(!pvp)
        {
            var viralBefore=await Diagnostic(33);Cast(actor,314);await actor.WaitForRpcAsync(ModPattern(314,342));
            var record=actor.Received(ModPattern(314,342)).Last(r=>r.Bytes[17]==0);
            Check.Equal(108,BitConverter.ToInt32(record.Bytes,13),"Preserved captured Upgrade Attacks direct342+108");
            var viralAfter=await Diagnostic(33);Check.Equal(108,viralAfter.Total-viralBefore.Total,"Direct viral total consumed without percentage links");
            Check.That(viralAfter.Hits>viralBefore.Hits,"Direct viral buff affects seeded production contests (diagnostic only, not queued viral admission)");
        }
        var before=await Diagnostic(134);
        var actorView=await enemy.WaitForViewNamedAsync(actor.Handle);Cast(enemy,97,actorView.ViewId);
        await actor.WaitForRpcAsync(ModPattern(97,1032));
        var lag=actor.Received(ModPattern(97,1032)).Last(r=>r.Bytes[17]==0);
        Check.Equal(-10,BitConverter.ToInt32(lag.Bytes,13),"Normal UI Lag captured thrown accuracy-10");
        var after=await Diagnostic(134);Check.Equal(-10,after.Total-before.Total,"UI Lag affects selected thrown accuracy");
        Check.That(after.Hits<before.Hits,"Same seeded contests respond to captured debuff");
        var target=pvp?await actor.WaitForViewNamedAsync(enemy.Handle):await actor.WaitForViewNamedAsync("WinterFrost");
        actor.SendRpc($"40 {HandleOf(target)} 00 6b");await actor.WaitForRpcAsync("46");
        int log=context.Server.DebugLog().Length;
        bool landed=false;
        for(int attempt=0;attempt<8 && !landed;attempt++)
        {
            int rounds=actor.Received("45").Count;Cast(actor,134,target.ViewId);
            await Task.Delay(12500); // actual recast, paid normal requests; no outcome/RNG override
            landed=actor.Received("45").Skip(rounds).Any(r=>(RoundResult(r)&0x100)!=0);
        }
        Check.That(landed,"Normal queued134 executes with RPC45 result0x100 within eight paid attempts");
        Check.That(context.Server.DebugLog()[log..].Contains("[COMBAT-ATTACK OURS] special 134 stat 1032 total "+after.Total),
            "Normal zero-cast134 executor consumed the exact captured debuffed total before expiry");
        Check.That(actor.Received("47").Count>0,"Queued next-move records sent");
        if(pvp) await enemy.WaitUntilAsync(()=>OwnHealth(enemy)<5000,"PvP authoritative HP changes in actual combat",TimeSpan.FromSeconds(15));
        else Check.That(ViewUpdateValues(actor,target.ViewId,NpcHealthUpdate).Count>0,"NPC authoritative HP updates");
        actor.SendRpc("44 00");await actor.WaitForRpcAsync("46");
        Check.Equal(0,actor.World.Problems.Count+enemy.World.Problems.Count,"Typed accuracy wire framing");
        await File.WriteAllTextAsync(Path.Combine(Path.GetTempPath(),"mxo-typed-attack-"+(pvp?"pvp":"npc")+"-success.log"),context.Server.FullOutput()+"\n"+context.Server.DebugLog());
    }
}
