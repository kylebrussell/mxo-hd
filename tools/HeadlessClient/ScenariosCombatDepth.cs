namespace HeadlessClient;
public static partial class Scenarios
{
    private static async Task CombatDepthMitigation(ScenarioContext context)
    {
        var caster=await context.AddPlayerAsync(new CharacterSeed("DepthCaster"){Level=10,Organization=1});
        var target=await context.AddPlayerAsync(new CharacterSeed("DepthTarget"){X=17120,Level=50,Organization=2});
        await context.Database.ExecuteAsync($"DELETE FROM char_abilities WHERE char_id IN ({caster.CharId},{target.CharId})");
        await caster.EnterWorldAsync(); await target.EnterWorldAsync();
        target.SendChat("?combatdepth determination 75"); await target.WaitForTextAsync("Determination 75");
        target.SendChat("?pvpflag on"); await target.WaitForViewUpdateAsync(2,OwnPvpFlagOn);
        var view=await caster.WaitForViewNamedAsync(target.Handle);
        Cast(caster,315,view.ViewId);
        var stun=await target.WaitForRpcAsync(ModPattern(315,102));
        Check.That(Math.Abs(BitConverter.ToSingle(stun.Bytes,18)-4.69f)<.0001,"Target Determination reduces stun fractionally");
        Check.Equal(14f,BitConverter.ToSingle(target.Received(ModPattern(734,681)).Last().Bytes,18),"Immunity stays14 seconds");
        await target.WaitForRpcAsync(ModPattern(315,102,true),TimeSpan.FromSeconds(8));
        Cast(target,967); await target.WaitUntilAsync(()=>context.Server.DebugLog().Contains("Heal 967 -> DepthTarget"),"Act after shortened expiry",TimeSpan.FromSeconds(10));
        target.SendChat("?combatdepth stun");
        await target.WaitForTextAsync("level 5 stun applied");
        Check.Equal(0,caster.World.Problems.Count+target.World.Problems.Count,"Mitigation protocol");
    }
    private static async Task CombatDepthSneakOpener(ScenarioContext context)
    {
        var spy=await context.AddPlayerAsync(new CharacterSeed("DepthSpy"){Level=50,Organization=1,InnerStrength=500,MaxInnerStrength=500});
        var victim=await context.AddPlayerAsync(new CharacterSeed("DepthVictim"){X=17120,Level=50,Organization=2,Health=5000,MaxHealth=5000});
        await context.Database.ExecuteAsync($"DELETE FROM char_abilities WHERE char_id IN ({spy.CharId},{victim.CharId})");
        await LoadAbilitiesAsync(context,spy,1112);
        await spy.EnterWorldAsync();await victim.EnterWorldAsync();
        victim.SendChat("?pvpflag on"); await victim.WaitForViewUpdateAsync(2,OwnPvpFlagOn);
        var view=await spy.WaitForViewNamedAsync(victim.Handle);
        spy.SendChat("?stealth invis"); await spy.WaitForRpcAsync(ModPattern(1116,115));
        Cast(spy,152,view.ViewId);
        await victim.WaitUntilAsync(()=>context.Server.DebugLog().Contains("SneakAttackSpinBackAbility lands on DepthVictim"),"Sneak opener lands",TimeSpan.FromSeconds(12));
        // Packet health must include the sourced75 bonus on the existing50..80 base.
        await victim.WaitUntilAsync(()=>OwnHealth(victim)>0 && OwnHealth(victim)<4900,"Bonus damage exceeded ordinary base",TimeSpan.FromSeconds(10));
        Check.That(OwnHealth(victim)>=4845 && OwnHealth(victim)<=4875,"Stealth opener damage125..155");
        Check.That(spy.Received(ModPattern(1116,115,true)).Count>0,"Stealth breaks before the delayed hit without losing snapshot");
        Check.Equal(0,spy.World.Problems.Count+victim.World.Problems.Count,"Sneak protocol");
    }
    private static async Task CombatDepthSpecialAndShared(ScenarioContext context)
    {
        var fighter=await context.AddPlayerAsync(NearNpc("DepthFighter") with {Level=50,Health=5000,MaxHealth=5000});
        var second=await context.AddPlayerAsync(NearNpc("DepthSecond",100) with {Health=5000,MaxHealth=5000});
        var observer=await context.AddPlayerAsync(NearNpc("DepthObserver",200));
        await context.Database.ExecuteAsync($"DELETE FROM char_abilities WHERE char_id IN ({fighter.CharId},{second.CharId},{observer.CharId})");
        foreach(var p in new[]{fighter,second,observer})await p.EnterWorldAsync();
        var a=await fighter.WaitForViewNamedAsync("WinterFrost");var b=await second.WaitForViewNamedAsync("WinterFrost");
        await observer.WaitForViewNamedAsync(fighter.Handle);
        fighter.SendRpc($"40 {HandleOf(a)} 00 6b");
        ushort fv=await WaitForCombatView(fighter),ov=await WaitForCombatView(observer);
        await WaitForEntry(observer,ov);
        second.SendRpc($"40 {HandleOf(b)} 00 6b");
        Cast(fighter,197,a.ViewId);
        await fighter.WaitUntilAsync(()=>context.Server.DebugLog().Contains("animation 284 (HeadButt)"),"Own special choreography",TimeSpan.FromSeconds(25));
        await WaitForEntry(observer,ov,3); VerifySharedEntry(fighter,fv,observer,ov);
        await Task.Delay(5000);
        string log=context.Server.DebugLog();
        var times=System.Text.RegularExpressions.Regex.Matches(log,@"\[COMBAT-NPC\] action WinterFrost at ([0-9.]+)")
            .Select(m=>double.Parse(m.Groups[1].Value,System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        Check.That(times.Length>=2,"NPC retaliates during shared combat");
        for(int i=1;i<times.Length;i++)Check.That(times[i]-times[i-1]>=4.49,"Two timers cannot multiply the NPC action cadence");
        second.SendRpc("44 00");
        await fighter.WaitUntilAsync(()=>context.Server.DebugLog().Contains("Combat ended: AttackerFled"),"Second fighter leaves",TimeSpan.FromSeconds(5));
        int before=ViewUpdateValues(fighter,a.ViewId,NpcHealthUpdate).Count;
        await fighter.WaitUntilAsync(()=>ViewUpdateValues(fighter,a.ViewId,NpcHealthUpdate).Count>before,"Remaining combat continues",TimeSpan.FromSeconds(10));
        fighter.SendRpc("44 00");
        Check.Equal(0,fighter.World.Problems.Count+second.World.Problems.Count+observer.World.Problems.Count,"Shared special protocol");
    }
    private static async Task CombatDepthFixture(ScenarioContext context)
    {
        var player=await context.AddPlayerAsync(NearNpc("DepthFixture") with {Level=50,Organization=1,Health=5000,MaxHealth=5000});
        await context.Database.ExecuteAsync($"DELETE FROM char_abilities WHERE char_id={player.CharId}");
        await player.EnterWorldAsync();var mob=await player.WaitForViewNamedAsync("WinterFrost");
        player.SendRpc($"81 51 {Le(mob.ViewId)} {mob.SpawnId:x2}");
        player.SendChat("?combatdepth status");await player.WaitForTextAsync("combatants 0");
        player.SendRpc($"40 {HandleOf(mob)} 00 6b");await WaitForCombatView(player);
        player.SendChat("?combatdepth join");await player.WaitForTextAsync("joins selected NPC");
        await Task.Delay(4000);player.SendChat("?combatdepth status");await player.WaitForTextAsync("combatants 2");
        player.SendChat("?combatdepth cast 197");await player.WaitForTextAsync("cast requested 197");
        await player.WaitUntilAsync(()=>context.Server.DebugLog().Contains("animation 284 (HeadButt)"),"Fixture uses real ability queue",TimeSpan.FromSeconds(15));
        player.SendChat("?combatdepth off");await player.WaitForTextAsync("fixture removed");
        player.SkipReceived();player.SendChat("?combatdepth status");await player.WaitForTextAsync("combatants 1");
        player.SendRpc("44 00");
        player.SendChat("?combatdepth sightgap");await player.WaitForTextAsync("sightgap");
        await player.WaitUntilAsync(()=>context.Server.DebugLog().Contains("floor-mesh sight blocked"),"Fixture gap blocks level0 detection",TimeSpan.FromSeconds(12));
        player.SendChat("?combatdepth sightclear");await player.WaitForTextAsync("sightclear");
        await player.WaitUntilAsync(()=>context.Server.DebugLog().Contains("DuelBot 1111 off (revealed)"),"Fixture clear floor reveals level0 bot",TimeSpan.FromSeconds(12));
        player.SendChat("?combatdepth off");
        Check.Equal(0,player.World.Problems.Count,"Fixture protocol");
    }
    private static async Task CombatDepthStealthSight(ScenarioContext context)
    {
        var watcher=await context.AddPlayerAsync(new CharacterSeed("DepthSightWatcher"){X=23828.3,Y=1295,Z=13984.9,Level=50,Organization=1});
        var spy=await context.AddPlayerAsync(new CharacterSeed("DepthSightSpy"){X=23540.7,Y=1295,Z=15548.3,Level=5,Organization=2});
        await context.Database.ExecuteAsync($"DELETE FROM char_abilities WHERE char_id IN ({watcher.CharId},{spy.CharId})");
        await watcher.EnterWorldAsync();await spy.EnterWorldAsync();
        var seen=await watcher.WaitForViewNamedAsync(spy.Handle);
        spy.SendChat("?pvpflag on");await spy.WaitForViewUpdateAsync(2,OwnPvpFlagOn);
        // The watcher faces +Z, within the60degree cone and detection radius, on the same floor.
        // The shipped floor mesh has a gap along this straight segment; initial watching must not refuse Sneak.
        Cast(spy,1111);await spy.WaitForRpcAsync(ModPattern(1111,881));
        await watcher.WaitForViewUpdateAsync(1,$"01 01 00 {Le(seen.ViewId)}",TimeSpan.FromSeconds(10));
        await spy.WaitUntilAsync(()=>context.Server.DebugLog().Contains("floor-mesh sight blocked"),"Periodic sight rejects the mapped gap before detection roll",TimeSpan.FromSeconds(12));
        Check.That(spy.Received(ModPattern(1111,881,true)).Count==0,"Blocked watcher does not reveal Sneak");
        Check.Equal(0,spy.World.Problems.Count+watcher.World.Problems.Count,"Sight protocol");
    }
    private static ushort OwnHealth(HeadlessPlayer player) => ViewUpdateValues(player,2,"02 80 80 80 40").LastOrDefault();
}
