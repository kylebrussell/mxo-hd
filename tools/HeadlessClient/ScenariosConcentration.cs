namespace HeadlessClient;
public static partial class Scenarios
{
    private static Task ConcentrationLoaded(ScenarioContext context)=>ConcentrationDuringDamage(context,true);
    private static Task ConcentrationControl(ScenarioContext context)=>ConcentrationDuringDamage(context,false);
    private static async Task ConcentrationDuringDamage(ScenarioContext context,bool loaded)
    {
        var caster=await context.AddPlayerAsync(new CharacterSeed("ConcentrateCaster"){Level=50,Organization=1,Health=1000,MaxHealth=1000,InnerStrength=2000,MaxInnerStrength=2000});
        var enemy=await context.AddPlayerAsync(new CharacterSeed("ConcentrateEnemy"){X=17200,Level=50,Organization=2,Health=1000,MaxHealth=1000,InnerStrength=2000,MaxInnerStrength=2000});
        await context.Database.ExecuteAsync($"DELETE FROM char_abilities WHERE char_id IN ({caster.CharId},{enemy.CharId})");
        await LoadAbilitiesAsync(context,caster,ConcentrationFixture.Abilities(loaded));await LoadAbilitiesAsync(context,enemy,ConcentrationFixture.Abilities(loaded));
        await caster.EnterWorldAsync();await enemy.EnterWorldAsync();
        await ConcentrationFixture.PrepareEnemyAsync(enemy);caster.SendChat("?pvpflag on");await caster.WaitForViewUpdateAsync(2,OwnPvpFlagOn);
        if(loaded)Check.That(caster.Received(PassiveRecord(628,564,30)).Count>0,"Loaded628 supplies exact native-value30 concentration");
        var target=await caster.WaitForViewNamedAsync(enemy.Handle);var victim=await enemy.WaitForViewNamedAsync(caster.Handle);
        Cast(caster,360,target.ViewId);await caster.WaitForRpcAsync("80 ac 68 01");
        await Task.Delay(400);Cast(enemy,261,victim.ViewId);
        await caster.WaitUntilAsync(()=>context.Server.DebugLog().Contains("MachinegunFistComboAbility lands on ConcentrateCaster"),"Actual wire damage during four-second cast",TimeSpan.FromSeconds(10));
        await caster.WaitUntilAsync(()=>context.Server.DebugLog().Contains("[CAST-INTERRUPT] 360 loss="),"Qualified damage invokes sourced concentration rule",TimeSpan.FromSeconds(8));
        Check.That(context.Server.DebugLog().Contains("through="+(loaded?90:75)+" roll=85"),"Same85 outcome differs only by loaded concentration");
        if(loaded)
        {
            await enemy.WaitForRpcAsync(ModPattern(360,157),TimeSpan.FromSeconds(10));
            Check.Equal(0,caster.Received("80 ad 68 01 00 00 02").Count,"Successful cast-through has no interruption packet");
        }
        else
        {
            await caster.WaitForRpcAsync("80 ad 68 01 00 00 02",TimeSpan.FromSeconds(8));
            await Task.Delay(5000);
            Check.Equal(0,enemy.Received(ModPattern(360,157)).Count,"Cancelled delayed effect never lands");
            Check.Equal(0,caster.Received("80 bd 01 68 01").Count,"Cancelled delayed cooldown never starts");
        }
        Check.Equal(0,caster.World.Problems.Count+enemy.World.Problems.Count,"Concentration cast lifecycle wire framing");
    }
}
