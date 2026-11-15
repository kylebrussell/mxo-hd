namespace HeadlessClient;
public static partial class Scenarios
{
    private static async Task<ViewCreation> SummonTechnicianProxy(HeadlessPlayer owner)
    {
        Cast(owner,594);
        await owner.WaitForRpcAsync("81 76",TimeSpan.FromSeconds(20));
        owner.SendRpc("81 7c fe 05 02"); // Passive: only the explicit window Attack order may fight.
        ViewCreation? pet=null;
        await owner.WaitUntilAsync(()=> (pet=owner.World.CreationSnapshot().LastOrDefault(v=>v.Goid==40376 && BitConverter.ToUInt32(v.Attributes["OwnerCharacterID"])==owner.CharId))!=null,"Actual owned Proxy view",TimeSpan.FromSeconds(10));
        return pet!;
    }
    private static async Task ProxyTechnicianFriendlyAttacks(ScenarioContext context)
    {
        var source=await context.AddPlayerAsync(NearNpc("TechnicianSource") with {Level=50,Organization=1,Health=5000,MaxHealth=5000,InnerStrength=1000,MaxInnerStrength=1000});
        var friend=await context.AddPlayerAsync(NearNpc("TechnicianFriend",100) with {Level=50,Organization=1,Health=5000,MaxHealth=5000,InnerStrength=1000,MaxInnerStrength=1000});
        var enemy=await context.AddPlayerAsync(NearNpc("TechnicianEnemy",200) with {Level=50,Organization=2,Health=5000,MaxHealth=5000,InnerStrength=1000,MaxInnerStrength=1000});
        await context.Database.ExecuteAsync($"DELETE FROM char_abilities WHERE char_id IN ({source.CharId},{friend.CharId},{enemy.CharId})");
        await LoadAbilitiesAsync(context,source,594,589);await LoadAbilitiesAsync(context,friend,594);await LoadAbilitiesAsync(context,enemy,594);
        foreach(var p in new[]{source,friend,enemy})await p.EnterWorldAsync();
        enemy.SendChat("?pvpflag on");await enemy.WaitForViewUpdateAsync(2,OwnPvpFlagOn);
        await SummonTechnicianProxy(source);await SummonTechnicianProxy(friend);await SummonTechnicianProxy(enemy);
        friend.SendChat("?pet dummy 50");await friend.WaitForTextAsync("stands 8 m away");var dummy=await friend.WaitForViewNamedAsync("Simulacra Dummy");
        friend.SendRpc($"81 7b fe 02 {Le(dummy.ViewId)} 00 00");
        await friend.WaitUntilAsync(()=>context.Server.DebugLog().Contains("attack owner=TechnicianFriend bonus=0 roll=80 chance=75 hit=False"),"Actual unbuffed Proxy misses fixed80",TimeSpan.FromSeconds(15));
        var clock=System.Diagnostics.Stopwatch.StartNew();Cast(source,589);
        async Task<double> FirstDrainTime()
        {
            await source.WaitUntilAsync(()=>context.Server.DebugLog().Contains("drain source=TechnicianSource amount=5"),"First scheduled5s drain",TimeSpan.FromSeconds(8));
            return clock.Elapsed.TotalSeconds;
        }
        var firstDrain=FirstDrainTime();
        await source.WaitForRpcAsync("80 bd 05 4d 02 00 00 00 00 00 01");
        await source.WaitUntilAsync(()=>context.Server.DebugLog().Contains("pulse source=TechnicianSource recipient=TechnicianFriend"),"Friendly other-owner pet receives698",TimeSpan.FromSeconds(5));
        Check.That(context.Server.DebugLog().Contains("pulse source=TechnicianSource recipient=TechnicianSource"),"Caster's own pet also receives698");
        Check.That(!context.Server.DebugLog().Contains("pulse source=TechnicianSource recipient=TechnicianEnemy"),"Legal enemy owner's pet excluded");
        await friend.WaitUntilAsync(()=>context.Server.DebugLog().Contains("attack owner=TechnicianFriend bonus=64 roll=80 chance=95 hit=True"),"Actual friendly Proxy hit changes with caster-level score",TimeSpan.FromSeconds(12));
        Check.That(await firstDrain>=4.5,"IS update drain is periodic, never an extra init drain");
        friend.SendChat("?pet");await friend.WaitForTextAsync("technician melee/ballistic 64/64");
        enemy.SendChat("?pet");await enemy.WaitForTextAsync("technician melee/ballistic 0/0");
        Cast(source,589);await source.WaitForRpcAsync("80 bd 05 4d 02 00 00 00 00 00 00");
        await Task.Delay(5500);
        friend.SendChat("?pet");await friend.WaitForTextAsync("technician melee/ballistic 0/0");
        Check.Equal(0,source.World.Problems.Count+friend.World.Problems.Count+enemy.World.Problems.Count,"589 friendly simulacra actual wire protocol");
    }

    private static async Task ProxyTechnicianLifecycle(ScenarioContext context)
    {
        var p=await context.AddPlayerAsync(NearNpc("TechnicianLifecycle") with {Level=50,Organization=1,Health=5000,MaxHealth=5000,InnerStrength=1000,MaxInnerStrength=1000});
        await context.Database.ExecuteAsync($"DELETE FROM char_abilities WHERE char_id={p.CharId}");await LoadAbilitiesAsync(context,p,594,589);await p.EnterWorldAsync();
        await SummonTechnicianProxy(p);p.SendRpc("81 7a fe 00"); // Stay enables an actual caster-to-pet range boundary.
        Cast(p,589);await p.WaitForRpcAsync("80 bd 05 4d 02 00 00 00 00 00 01");
        p.SendChat("?pet");await p.WaitForTextAsync("technician melee/ballistic 64/64");
        p.SendChat($"?tp {NpcX+3000} {NpcY} {NpcZ} 0 0 0");await p.WaitForTextAsync("Teleported to 0/0");await Task.Delay(5500);
        p.SendChat("?pet");await p.WaitForTextAsync("technician melee/ballistic 0/0");
        p.SendChat($"?tp {NpcX} {NpcY} {NpcZ} 0 0 0");await p.WaitForTextAsync("Teleported to 0/0");
        await p.WaitUntilAsync(()=>context.Server.DebugLog().Split("pulse source=TechnicianLifecycle").Length>=3,"Return within20m reapplies child",TimeSpan.FromSeconds(7));
        p.SendChat("?qaunload 589");await p.WaitForRpcAsync("80 bd 05 4d 02 00 00 00 00 00 00");
        await Task.Delay(5500);p.SendChat("?pet");await p.WaitForTextAsync("technician melee/ballistic 0/0");
        Cast(p,589);await p.WaitForTextAsync("Load Proxy Technician");
        Check.Equal(0,p.World.Problems.Count,"589 range/unload lifecycle protocol");
    }
    private static async Task ProxyTechnicianControlRefusal(ScenarioContext context)
    {
        var p=await context.AddPlayerAsync(NearNpc("TechnicianControlled") with {Level=50,InnerStrength=1000,MaxInnerStrength=1000});
        await context.Database.ExecuteAsync($"DELETE FROM char_abilities WHERE char_id={p.CharId}");await LoadAbilitiesAsync(context,p,589);await p.EnterWorldAsync();
        p.SendChat("?combatdepth stun");await p.WaitForRpcAsync(ModPattern(315,102));
        Cast(p,589);await p.WaitForTextAsync("cannot act while stunned");
        Check.Equal(0,p.Received("80 bd 05 4d 02 00 00 00 00 00 01").Count,"Stun refuses ordinary589 toggle");
        Check.That(!context.Server.DebugLog().Contains("pulse source=TechnicianControlled"),"Controlled caster creates no aura pulse");
        Check.Equal(1000L,await context.Database.LongAsync($"SELECT innerStrC FROM characters WHERE charId={p.CharId}"),"Refused aura spends no IS");
        Check.Equal(0,p.World.Problems.Count,"Controlled589 refusal protocol");
    }

}
