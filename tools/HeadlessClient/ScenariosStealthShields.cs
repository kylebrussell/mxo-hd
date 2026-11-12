namespace HeadlessClient;
public static partial class Scenarios
{
    private static async Task StealthShieldNativeFixture(ScenarioContext context)
    {
        var caster=await context.AddPlayerAsync(StealthShieldFixture.Seed("CSR_Tester"));
        var victim=await context.AddPlayerAsync(StealthShieldFixture.Seed("QAEnemy",true));
        foreach(var p in new[]{caster,victim})
        {
            await context.Database.ExecuteAsync($"DELETE FROM char_abilities WHERE char_id={p.CharId}");
            await LoadAbilitiesAsync(context,p,StealthShieldFixture.Abilities);await context.Database.ExecuteAsync($"UPDATE characters SET pvpflag=1 WHERE charId={p.CharId}");await p.EnterWorldAsync();
        }
        await StealthShieldFixture.PrepareCasterAsync(caster);
        var original=await StealthShieldFixture.ActivateTargetAsync(victim);
        Check.Equal("1",await context.Database.ScalarAsync($"SELECT alignment FROM characters WHERE charId={caster.CharId}"),"Fixture caster organization saved");
        Check.Equal("2",await context.Database.ScalarAsync($"SELECT alignment FROM characters WHERE charId={victim.CharId}"),"Fixture target organization saved");
        var target=await caster.WaitForViewNamedAsync(victim.Handle);
        caster.SendChat("?stealth invis");await caster.WaitForRpcAsync(ModPattern(1116,115));
        Cast(caster,1125,target.ViewId); // actual80b9, same handler used by native selected-target helper
        await Task.Delay(3000);
        Check.Equal(0,caster.Received(ModPattern(1116,115,true)).Count,"Original invisibility survives first3s of4s cast");
        Check.Equal(0,victim.Received(ModPattern(217,624,true)).Count,"No early shield suppression before landing");
        await caster.WaitForRpcAsync(ModPattern(1125,681));
        Check.Equal(1,caster.Received(ModPattern(1116,115,true)).Count,"Only successful landing ends original invisibility");
        await victim.WaitForRpcAsync(ModPattern(217,624,true));
        await caster.WaitForRpcAsync(ModPattern(1125,681,true));
        await caster.WaitUntilAsync(()=>caster.Received(ModPattern(1116,115)).Count==2,"Restealth follows natural681expiry",TimeSpan.FromSeconds(4));
        await victim.WaitUntilAsync(()=>victim.Received(ModPattern(217,624)).Count==2,"Target624 returns under retained217",TimeSpan.FromSeconds(10));
        var restored=victim.Received(ModPattern(217,624)).Last();
        Check.Equal(BitConverter.ToUInt16(original,4),BitConverter.ToUInt16(restored.Bytes,4),"Same parent modifier record ID");
        Check.Equal(0,victim.Received("80 bd 05 d9 00 00 00 00 00 00 00").Count,"Parent217 never toggled off");
        Check.That(context.Server.DebugLog().Contains("1125 landed; target suppression8s"),"Actual landing logged");
        Check.That(!context.Server.DebugLog().Contains("off (used ability 1125)"),"No generic stealth break");
        Check.Equal(0,caster.World.Problems.Count+victim.World.Problems.Count,"Native-equivalent protocol");
    }
    private static async Task StealthShieldNativeRefusal(ScenarioContext context)
    {
        var caster=await context.AddPlayerAsync(StealthShieldFixture.Seed("UnalignedSpy") with{Organization=0});
        var victim=await context.AddPlayerAsync(StealthShieldFixture.Seed("ShieldEnemy",true));
        foreach(var p in new[]{caster,victim})
        {await context.Database.ExecuteAsync($"DELETE FROM char_abilities WHERE char_id={p.CharId}");await LoadAbilitiesAsync(context,p,StealthShieldFixture.Abilities);await context.Database.ExecuteAsync($"UPDATE characters SET pvpflag=1 WHERE charId={p.CharId}");await p.EnterWorldAsync();}
        await StealthShieldFixture.ActivateTargetAsync(victim);
        var target=await caster.WaitForViewNamedAsync(victim.Handle);caster.SendChat("?stealth invis");await caster.WaitForRpcAsync(ModPattern(1116,115));
        Cast(caster,1125,target.ViewId);
        await caster.WaitUntilAsync(()=>context.Server.DebugLog().Contains("1125 landing refused: PvP NotOpponent casterOrg=0"),"Illegal disposablefixture accurately refused",TimeSpan.FromSeconds(7));
        Check.Equal(0,caster.Received(ModPattern(1125,681)).Count,"No false-success marker");
        Check.Equal(0,caster.Received(ModPattern(1116,115,true)).Count,"Failed landing preserves original stealth");
        Check.Equal(0,victim.Received(ModPattern(217,624,true)).Count,"Illegal PvP cannot suppress shield");
    }
    private static async Task StealthShieldSuppression(ScenarioContext context)
    {
        var caster=await context.AddPlayerAsync(new CharacterSeed("ShieldSpy"){Level=50,Organization=1,InnerStrength=1000,MaxInnerStrength=1000,Items=new[]{(1,531u,1)}});
        var victim=await context.AddPlayerAsync(new CharacterSeed("ShieldVictim"){X=17120,Level=50,Organization=2,Health=5000,MaxHealth=5000});
        await context.Database.ExecuteAsync($"DELETE FROM char_abilities WHERE char_id IN ({caster.CharId},{victim.CharId})");
        await LoadAbilitiesAsync(context,caster,1110,1125,1116);await LoadAbilitiesAsync(context,victim,217);
        await caster.EnterWorldAsync();await victim.EnterWorldAsync();
        victim.SendChat("?pvpflag on");await victim.WaitForViewUpdateAsync(2,OwnPvpFlagOn);
        Cast(victim,217);var original=await victim.WaitForRpcAsync(ModPattern(217,624));
        Cast(caster,1116);await caster.WaitForRpcAsync(ModPattern(1116,115));
        var target=await caster.WaitForViewNamedAsync(victim.Handle);Cast(caster,1125,target.ViewId);
        var debuff=await victim.WaitForRpcAsync(ModPattern(1125,710));
        Check.Equal(8f,BitConverter.ToSingle(debuff.Bytes,18),"Target suppression has its stored8seconds");
        await victim.WaitForRpcAsync(ModPattern(217,624,true));
        Check.Equal(0,victim.Received("80 bd 05 d9 00 00 00 00 00 00 00").Count,"217parent stays toggled on");
        await caster.WaitForRpcAsync(ModPattern(1125,681));
        await caster.WaitUntilAsync(()=>caster.Received(ModPattern(1116,115)).Count==2,"Caster returns to invis after1.5s marker",TimeSpan.FromSeconds(5));
        await victim.WaitForRpcAsync(ModPattern(1125,710,true),TimeSpan.FromSeconds(10));
        await victim.WaitUntilAsync(()=>victim.Received(ModPattern(217,624)).Count==2,"Only the evasion state returns",TimeSpan.FromSeconds(4));
        var reapplied=victim.Received(ModPattern(217,624)).Last();Check.Equal(BitConverter.ToUInt16(original.Bytes,4),BitConverter.ToUInt16(reapplied.Bytes,4),"Parent record ID preserved");
        Check.That(!context.Server.DebugLog().Contains("ticks on ShieldVictim"),"Disable Evasion deals no fallback damage");
        Check.Equal(0,caster.World.Problems.Count+victim.World.Problems.Count,"Shield suppression protocol");
    }
    private static async Task StealthBullDogPvpAndViews(ScenarioContext context)
    {
        var caster=await context.AddPlayerAsync(new CharacterSeed("DogSpy"){Level=50,Organization=1,InnerStrength=500,MaxInnerStrength=500});
        var victim=await context.AddPlayerAsync(new CharacterSeed("DogVictim"){X=17120,Level=50,Organization=2,Health=5000,MaxHealth=5000});
        var watcher=await context.AddPlayerAsync(new CharacterSeed("DogWatcher"){X=17220,Level=50,Organization=1});
        var late=await context.AddPlayerAsync(new CharacterSeed("DogLate"){X=100000,Level=50,Organization=1});
        await context.Database.ExecuteAsync($"DELETE FROM char_abilities WHERE char_id IN ({caster.CharId},{victim.CharId},{watcher.CharId},{late.CharId})");
        await LoadAbilitiesAsync(context,caster,1110,1112);
        foreach(var p in new[]{caster,victim,watcher,late})await p.EnterWorldAsync();
        victim.SendChat("?pvpflag on");await victim.WaitForViewUpdateAsync(2,OwnPvpFlagOn);
        var cv=await caster.WaitForViewNamedAsync(victim.Handle);var actor=await watcher.WaitForViewNamedAsync(caster.Handle);var wv=await watcher.WaitForViewNamedAsync(victim.Handle);
        caster.SendChat("?stealth invis");await caster.WaitForRpcAsync(ModPattern(1116,115));
        Cast(caster,1110,cv.ViewId);await caster.WaitForViewUpdateAsync(2,$"01 20 ?? 01 0c 40 56 04 {HandleOf(cv)}");
        await watcher.WaitForViewUpdateAsync(actor.ViewId,$"01 20 ?? 01 0c 40 56 04 {HandleOf(wv)}");
        late.World.SendViewList(WireFormat.FromHex("02 00 01 08").Concat(BitConverter.GetBytes(17320f)).Concat(BitConverter.GetBytes(495f)).Concat(BitConverter.GetBytes(2693f)).Concat(new byte[]{0,0}).ToArray());
        var la=await late.WaitForViewNamedAsync(caster.Handle);var lv=await late.WaitForViewNamedAsync(victim.Handle);
        await late.WaitForViewUpdateAsync(la.ViewId,$"01 20 ?? 01 0c 40 56 04 {HandleOf(lv)}",TimeSpan.FromSeconds(3));
        await victim.WaitForViewUpdateAsync(2,"02 80 80 80 c0 7e 13"); //5000-10=4990
        await victim.WaitForRpcAsync(ModPattern(1110,710,true));
        await watcher.WaitForViewUpdateAsync(actor.ViewId,"01 2a ?? 40 00 00 00 10");
        await Task.Delay(1700);
        Check.Equal(1,System.Text.RegularExpressions.Regex.Matches(context.Server.DebugLog(),@"1110 terminal10 OURS on DogVictim").Count,"One terminal hit, no extra generic/sneak/typedthrow damage");
        Check.Equal(0,caster.World.Problems.Count+victim.World.Problems.Count+watcher.World.Problems.Count+late.World.Problems.Count,"Builtin throw and late-view protocol");
    }
    private static async Task StealthBullDogNpc(ScenarioContext context)
    {
        var caster=await context.AddPlayerAsync(NearNpc("DogNpc") with{Level=50,InnerStrength=500,MaxInnerStrength=500,Items=new[]{(1,531u,1)}});
        await context.Database.ExecuteAsync($"DELETE FROM char_abilities WHERE char_id={caster.CharId}");await LoadAbilitiesAsync(context,caster,1110,1125);
        await caster.EnterWorldAsync();var mob=await caster.WaitForViewNamedAsync("WinterFrost");
        caster.SendChat("?stealth invis");await caster.WaitForRpcAsync(ModPattern(1116,115));
        Cast(caster,1125,mob.ViewId);await caster.WaitForTextAsync("requires an active Evasion State");
        Cast(caster,1110,mob.ViewId);await caster.WaitForViewUpdateAsync(mob.ViewId,"04 80 80 80 c0 16 03"); //800-10=790
        await caster.WaitUntilAsync(()=>context.Server.DebugLog().Contains("1110 terminal10 OURS on WinterFrost"),"NPC terminal damage",TimeSpan.FromSeconds(5));
        Check.Equal(1,System.Text.RegularExpressions.Regex.Matches(context.Server.DebugLog(),@"1110 terminal10 OURS on WinterFrost").Count,"One NPC callback");
        Check.Equal(0,caster.World.Problems.Count,"NPC terminal protocol");
    }
    private static async Task StealthShieldLifecycle(ScenarioContext context)
    {
        var a=await context.AddPlayerAsync(new CharacterSeed("ShieldDying"){Level=50,Organization=1,InnerStrength=1000,MaxInnerStrength=1000,Items=new[]{(1,531u,1)}});
        var b=await context.AddPlayerAsync(new CharacterSeed("ShieldActing"){X=17020,Z=2793,Level=50,Organization=1,InnerStrength=1000,MaxInnerStrength=1000,Items=new[]{(1,531u,1)}});
        var x=await context.AddPlayerAsync(new CharacterSeed("ShieldKeep"){X=17120,Level=50,Organization=2});
        var y=await context.AddPlayerAsync(new CharacterSeed("ShieldOff"){X=17120,Z=2793,Level=50,Organization=2});
        await context.Database.ExecuteAsync($"DELETE FROM char_abilities WHERE char_id IN ({a.CharId},{b.CharId},{x.CharId},{y.CharId})");
        foreach(var p in new[]{a,b,x,y})await p.EnterWorldAsync();
        foreach(var p in new[]{x,y}){p.SendChat("?pvpflag on");await p.WaitForViewUpdateAsync(2,OwnPvpFlagOn);Cast(p,217);await p.WaitForRpcAsync(ModPattern(217,624));}
        foreach(var p in new[]{a,b}){p.SendChat("?stealth invis");await p.WaitForRpcAsync(ModPattern(1116,115));}
        var xv=await a.WaitForViewNamedAsync(x.Handle);var yv=await b.WaitForViewNamedAsync(y.Handle);
        Cast(a,1125,xv.ViewId);Cast(b,1125,yv.ViewId);
        await a.WaitForRpcAsync(ModPattern(1125,681));await b.WaitForRpcAsync(ModPattern(1125,681));
        a.SendChat("?die");Cast(b,967);Cast(y,217);
        await y.WaitForRpcAsync("80 bd 05 d9 00 00 00 00 00 00 00");
        await Task.Delay(2500);
        Check.Equal(1,a.Received(ModPattern(1116,115)).Count,"Dead caster cannot restealth");
        Check.Equal(1,b.Received(ModPattern(1116,115)).Count,"A later successful ability cancels restealth");
        await x.WaitForRpcAsync(ModPattern(1125,710,true),TimeSpan.FromSeconds(10));
        await y.WaitForRpcAsync(ModPattern(1125,710,true),TimeSpan.FromSeconds(10));
        Check.Equal(2,x.Received(ModPattern(217,624)).Count,"Living target keeps its parent and recovers state even after caster dies");
        Check.Equal(1,y.Received(ModPattern(217,624)).Count,"Turned-off parent cannot return at target modifier expiry");
        Check.Equal(0,a.World.Problems.Count+b.World.Problems.Count+x.World.Problems.Count+y.World.Problems.Count,"Lifecycle protocol");
    }
    private static async Task StealthBullDogDeathCancels(ScenarioContext context)
    {
        var a=await context.AddPlayerAsync(new CharacterSeed("DogDying"){Level=50,Organization=1});
        var b=await context.AddPlayerAsync(new CharacterSeed("DogSafe"){X=17120,Level=50,Organization=2,Health=5000,MaxHealth=5000});
        await context.Database.ExecuteAsync($"DELETE FROM char_abilities WHERE char_id IN ({a.CharId},{b.CharId})");
        await a.EnterWorldAsync();await b.EnterWorldAsync();b.SendChat("?pvpflag on");await b.WaitForViewUpdateAsync(2,OwnPvpFlagOn);
        var target=await a.WaitForViewNamedAsync(b.Handle);a.SendChat("?stealth invis");await a.WaitForRpcAsync(ModPattern(1116,115));
        Cast(a,1110,target.ViewId);await a.WaitForViewUpdateAsync(2,$"01 20 ?? 01 0c 40 56 04 {HandleOf(target)}");
        a.SendChat("?die");await b.WaitForRpcAsync(ModPattern(1110,710,true));await Task.Delay(800);
        Check.That(!context.Server.DebugLog().Contains("1110 terminal10 OURS on DogSafe"),"Dead caster's terminal hit is cancelled");
        Check.Equal(0,ViewUpdateValues(b,2,"02 80 80 80 c0").Count,"No target health tick after caster death");
        Check.Equal(0,a.World.Problems.Count+b.World.Problems.Count,"Cancelled terminal protocol");
    }

}
