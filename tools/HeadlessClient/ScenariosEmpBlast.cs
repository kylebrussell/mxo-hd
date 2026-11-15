namespace HeadlessClient;

public static partial class Scenarios
{
    private static async Task EmpBlast(ScenarioContext context)
    {
        var first=await context.AddPlayerAsync(NearNpc("EmpOrdinary") with {Level=50,InnerStrength=1000,MaxInnerStrength=1000});
        var second=await context.AddPlayerAsync(NearNpc("EmpMarked",100) with {Level=50,InnerStrength=1000,MaxInnerStrength=1000});
        var third=await context.AddPlayerAsync(NearNpc("EmpDeath",200) with {Level=50,InnerStrength=1000,MaxInnerStrength=1000});
        foreach(var p in new[]{first,second,third}) await p.EnterWorldAsync();
        var mob=await first.WaitForViewNamedAsync("WinterFrost");
        Cast(first,1026); await first.WaitForTextAsync("NPC-only ownership marker");
        Cast(first,1025);
        await first.WaitUntilAsync(()=>context.Server.DebugLog().Contains("[EMP] " + mob.ViewId + " damage=0"),"Unmarked NPC takes zero scripted damage",TimeSpan.FromSeconds(8));
        Check.Equal(0,ViewUpdateValues(first,mob.ViewId,NpcHealthUpdate).Count,"Zero damage does not mutate NPC health");
        Cast(first,1025);await first.WaitForRpcAsync("80 ba 01 04 00 00 6b 05");
        var target=await second.WaitForViewNamedAsync("WinterFrost");
        second.SendRpc($"81 51 {Le(target.ViewId)} {target.SpawnId:x2}");
        second.SendChat("?combatdepth empweakness on 6000"); await second.WaitForTextAsync("EMP fixture weakness=1");
        Cast(second,1025);
        await second.WaitUntilAsync(()=>context.Server.DebugLog().Contains("[EMP] " + target.ViewId + " damage=5000 hp=1000"),"Explicit server NPC grant subtracts exactly5000 HP",TimeSpan.FromSeconds(8));
        await second.WaitForViewUpdateAsync(target.ViewId,"04 80 80 80 c0 e8 03 c0 43 0a 00 28");
        var deathTarget=await third.WaitForViewNamedAsync("WinterFrost");
        Cast(third,1025);
        await third.WaitForViewUpdateAsync(deathTarget.ViewId,"04 80 80 80 c0 00 00 c0 43 0a 00 28");
        await third.WaitUntilAsync(()=>context.Server.DebugLog().Contains("[EMP] " + deathTarget.ViewId + " damage=5000 hp=0"),"Marked NPC dies through ordinary NPC defeat handling",TimeSpan.FromSeconds(8));
        Check.Equal(0,first.World.Problems.Count+second.World.Problems.Count+third.World.Problems.Count,"EMP protocol");
    }
}
