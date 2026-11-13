namespace HeadlessClient;
public static partial class Scenarios
{
    private static async Task DirectDebuffRadius(ScenarioContext context)
    {
        var caster=await context.AddPlayerAsync(new CharacterSeed("DebuffCaster"){X=23999,Z=-1498,Level=50,Organization=2,Health=5000,MaxHealth=5000,InnerStrength=2000,MaxInnerStrength=2000});
        await context.Database.ExecuteAsync($"DELETE FROM char_abilities WHERE char_id={caster.CharId}");
        await LoadAbilitiesAsync(context,caster,DebuffRadiusFixture.Abilities);await caster.EnterWorldAsync();
        caster.SendChat("?pvpflag on");await caster.WaitForViewUpdateAsync(2,OwnPvpFlagOn);
        var second=await context.AddPlayerAsync(new CharacterSeed("SecondCaster"){X=23999,Z=502,Level=50,Organization=2,Health=5000,MaxHealth=5000,InnerStrength=2000,MaxInnerStrength=2000});
        await context.Database.ExecuteAsync($"DELETE FROM char_abilities WHERE char_id={second.CharId}");
        await LoadAbilitiesAsync(context,second,DebuffRadiusFixture.Abilities);await second.EnterWorldAsync();
        second.SendChat("?pvpflag on");await second.WaitForViewUpdateAsync(2,OwnPvpFlagOn);
        var near=await context.AddPlayerAsync(new CharacterSeed("DebuffNear"){Level=50,X=24879,Z=-1498,Organization=1,Health=5000,MaxHealth=5000});
        var edge=await context.AddPlayerAsync(new CharacterSeed("DebuffEdge"){Level=50,X=24939,Z=-1498,Organization=1,Health=5000,MaxHealth=5000});
        var far=await context.AddPlayerAsync(new CharacterSeed("DebuffFar"){Level=50,X=24999,Z=-1498,Organization=1,Health=5000,MaxHealth=5000});
        var primary=await context.AddPlayerAsync(new CharacterSeed("DebuffPrimary"){Level=50,X=24499,Z=502,Organization=1,Health=5000,MaxHealth=5000});
        await context.Database.ExecuteAsync(DebuffRadiusFixture.TargetsSql(near.CharId,edge.CharId,far.CharId,0));
        var pNear=await context.AddPlayerAsync(new CharacterSeed("PvpRadiusNear"){Level=50,X=24879,Z=502,Organization=1,Health=5000,MaxHealth=5000});
        var pEdge=await context.AddPlayerAsync(new CharacterSeed("PvpRadiusEdge"){Level=50,X=24939,Z=502,Organization=1,Health=5000,MaxHealth=5000});
        var pFar=await context.AddPlayerAsync(new CharacterSeed("PvpRadiusFar"){Level=50,X=24999,Z=502,Organization=1,Health=5000,MaxHealth=5000});
        foreach(var p in new[]{near,edge,far,primary,pNear,pEdge,pFar}){await p.EnterWorldAsync();p.SendChat("?pvpflag on");await p.WaitForViewUpdateAsync(2,OwnPvpFlagOn);}
        var npc=(await caster.WaitForViewNamedAsync("WinterFrost"));
        var pv=await second.WaitForViewNamedAsync(primary.Handle);
        async Task Area(HeadlessPlayer actor,ushort view,bool expanded,string label)
        {
            var inside=actor==caster?near:pNear;var expandedTarget=actor==caster?edge:pEdge;var outside=actor==caster?far:pFar;
            int onInside=inside.Received(ModPattern(333,102)).Count,onExpanded=expandedTarget.Received(ModPattern(333,102)).Count,onOutside=outside.Received(ModPattern(333,102)).Count;
            int start=context.Server.DebugLog().Length;Cast(actor,333,view);
            await near.WaitUntilAsync(()=>context.Server.DebugLog()[start..].Split('\n').Any(l=>l.Contains(actor.Handle+"'s")&&l.Contains("lands on "+inside.Handle)),label+" reaches3.8m",TimeSpan.FromSeconds(12));
            await Task.Delay(1000);string log=string.Join("\n",context.Server.DebugLog()[start..].Split('\n').Where(l=>l.Contains(actor.Handle+"'s")));
            Check.Equal(expanded,log.Contains("lands on "+expandedTarget.Handle),label+"4.4m boundary");
            Check.That(!log.Contains("lands on "+outside.Handle),label+" excludes5.0m");
            await inside.WaitUntilAsync(()=>inside.Received(ModPattern(333,102)).Count>onInside,label+" actual near stun record",TimeSpan.FromSeconds(4));
            Check.Equal(expanded,expandedTarget.Received(ModPattern(333,102)).Count>onExpanded,label+" actual expanded stun record");
            Check.Equal(onOutside,outside.Received(ModPattern(333,102)).Count,label+" no outside stun record");
            Console.WriteLine(label+":near=yes edge="+expanded+" far=no");
        }
        await Area(caster,npc.ViewId,false,"NPC primary baseline");
        await Area(second,pv.ViewId,false,"Player primary baseline");
        await Task.Delay(32000);Cast(caster,345,2);await caster.WaitForRpcAsync(ModPattern(345,177));
        Check.Equal(21,BitConverter.ToInt32(caster.Received(ModPattern(345,177)).Last(r=>r.Bytes[17]==0).Bytes,13),"Actual345 captured direct177 value21");
        Cast(second,345,2);await second.WaitForRpcAsync(ModPattern(345,177));
        Check.Equal(21,BitConverter.ToInt32(second.Received(ModPattern(345,177)).Last(r=>r.Bytes[17]==0).Bytes,13),"Second caster same actual345 value21");
        await Area(caster,npc.ViewId,true,"NPC primary boosted");
        await Area(second,pv.ViewId,true,"Player primary boosted");
        await caster.WaitForRpcAsync(ModPattern(345,177,true),TimeSpan.FromSeconds(35));
        await second.WaitForRpcAsync(ModPattern(345,177,true),TimeSpan.FromSeconds(35));
        await Task.Delay(6000);await Area(caster,npc.ViewId,false,"NPC primary expired");
        await Area(second,pv.ViewId,false,"Player primary expired");
        foreach(var p in new[]{caster,second,near,edge,far,primary,pNear,pEdge,pFar})Check.Equal(0,p.World.Problems.Count,"Debuff radius protocol framing");
        await File.WriteAllTextAsync(Path.Combine(Path.GetTempPath(),"mxo-debuff-radius-success-runtime.log"),context.Server.DebugLog());
    }
}
