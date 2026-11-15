namespace HeadlessClient;
public static partial class Scenarios
{
    private static async Task ConditionalDazedStun(ScenarioContext context)
    {
        var caster=await context.AddPlayerAsync(new CharacterSeed("DazeThrower") {Level=50,Organization=1,InnerStrength=1000,MaxInnerStrength=1000});
        var victim=await context.AddPlayerAsync(new CharacterSeed("DazeVictim") {X=17120,Level=50,Organization=2,Health=5000,MaxHealth=5000});
        await context.Database.ExecuteAsync($"DELETE FROM char_abilities WHERE char_id IN ({caster.CharId},{victim.CharId})");
        await LoadAbilitiesAsync(context,caster,347,884);
        await caster.EnterWorldAsync();await victim.EnterWorldAsync();
        victim.SendChat("?pvpflag on");await victim.WaitForViewUpdateAsync(2,OwnPvpFlagOn);
        var view=await caster.WaitForViewNamedAsync(victim.Handle);
        Cast(caster,347,view.ViewId);
        await victim.WaitForRpcAsync(ModPattern(347,347,true),TimeSpan.FromSeconds(8));
        Check.Equal(0,victim.Received(ModPattern(347,102)).Count,"Undazed throw never stuns");
        Cast(caster,884,view.ViewId);
        await victim.WaitForRpcAsync(ModPattern(884,199),TimeSpan.FromSeconds(10));
        await Task.Delay(8500); // allow347's8s cooldown; fixture NPCDaze lasts30s
        Cast(caster,347,view.ViewId);
        var stun=await victim.WaitForRpcAsync(ModPattern(347,102),TimeSpan.FromSeconds(8));
        Check.Equal(4f,BitConverter.ToSingle(stun.Bytes,18),"Dazed target receives script4s stun");
        var timer=victim.Received(ModPattern(734,681)).Last();
        Check.Equal(4f,BitConverter.ToSingle(timer.Bytes,18),"Independent immunity matches script value");
        Cast(victim,967);await victim.WaitForTextAsync("cannot act while stunned");
        await victim.WaitForRpcAsync(ModPattern(347,102,true),TimeSpan.FromSeconds(6));
        await victim.WaitForRpcAsync(ModPattern(734,681,true),TimeSpan.FromSeconds(6));
        Check.Equal(0,caster.World.Problems.Count+victim.World.Problems.Count,"Conditional stun protocol");
    }
}
