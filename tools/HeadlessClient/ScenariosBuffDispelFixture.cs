namespace HeadlessClient;
public static partial class Scenarios
{
    private static async Task BuffDispelFixtureHit(ScenarioContext context)=>await BuffDispelFixtureLanding(context,true);
    private static async Task BuffDispelFixtureMiss(ScenarioContext context)=>await BuffDispelFixtureLanding(context,false);
    private static async Task BuffDispelFixtureLanding(ScenarioContext context,bool proc)
    {
        var caster=await context.AddPlayerAsync(new CharacterSeed("DispelFixtureCaster"){Level=50,Organization=1,Health=10000,MaxHealth=10000,InnerStrength=2000,MaxInnerStrength=2000});
        var target=await context.AddPlayerAsync(new CharacterSeed("DispelFixtureTarget"){X=17200,Level=50,Organization=2,Health=10000,MaxHealth=10000,InnerStrength=2000,MaxInnerStrength=2000});
        await context.Database.ExecuteAsync($"DELETE FROM char_abilities WHERE char_id IN ({caster.CharId},{target.CharId})");
        await LoadAbilitiesAsync(context,caster,BuffDispelFixture.Abilities);await LoadAbilitiesAsync(context,target,BuffDispelFixture.Abilities);
        await caster.EnterWorldAsync();await target.EnterWorldAsync();
        byte[][] ready=await BuffDispelFixture.PrepareTargetAsync(target);
        Check.Equal(5,ready.Length,"Real bootstrap waited both upgrades, aura toggle and both child records");
        var view=await caster.WaitForViewNamedAsync(target.Handle);
        int off=target.Received(ModPattern(49,520,true)).Count;
        int siblingOff=target.Received(ModPattern(489,172,true)).Count;
        SelectStyle(caster,133);
        await caster.WaitForRpcAsync(ModPattern(133,522));
        caster.SendRpc($"40 {HandleOf(view)} 01 6b");
        await caster.WaitForRpcAsync("45");
        await WaitForCombatView(caster);
        bool landed=false;
        for(int attempt=0;attempt<8 && !landed;attempt++)
        {
            Cast(caster,261,view.ViewId);
            // A failed normal contest consumes that queued move. Requeue only after the real10s recast,
            // rather than treating later ordinary rounds as another attempt at the same special.
            for(int tick=0;tick<25 && !landed;tick++)
            {
                await Task.Delay(500);
                landed=context.Server.DebugLog().Contains("MachinegunFistComboAbility lands on DispelFixtureTarget");
            }
        }
        Check.That(landed,"Queued261 executes through an ordinary interlock contest within eight paid attempts");
        Check.That(context.Server.DebugLog().Contains("MachinegunFistComboAbility is DispelFixtureCaster's next move"),"80b9 queued the special in the active40 interlock");
        Check.That(caster.Received("45").Any(r=>(RoundResult(r)&0x100)!=0),"Actual RPC45 contains executed-special result0x100");
        Check.That(context.Server.DebugLog().Contains("Machinegun_First_Combo"),"Special execution presentation names Machinegun Fist Combo");
        var damage=System.Text.RegularExpressions.Regex.Match(context.Server.DebugLog(),@"MachinegunFistComboAbility lands on DispelFixtureTarget for (\d+)");
        Check.That(damage.Success && int.Parse(damage.Groups[1].Value)>0,"Executed261 deals its ordinary damage in both profiles");
        await target.WaitUntilAsync(()=>OwnHealth(target)>0 && OwnHealth(target)<10000,"Executed261 produces a health update",TimeSpan.FromSeconds(8));
        if(proc)await target.WaitUntilAsync(()=>target.Received(ModPattern(49,520,true)).Count>off,"Forced0 removes Gaussian Blur",TimeSpan.FromSeconds(8));
        else {await Task.Delay(500);Check.Equal(off,target.Received(ModPattern(49,520,true)).Count,"Forced99 preserves upgrade on a landed hit");}
        Check.Equal(siblingOff,target.Received(ModPattern(489,172,true)).Count,"261 leaves sibling upgrade intact");
        Check.Equal(0,target.Received("80 bd 05 2a 00 00 00 00 00 00 00").Count,"261 leaves sibling aura on");
        if(proc)
        {
            // With the same guarded0 fixture, Delete Upgrade2's stored20% sweep removes every remaining parent.
            target.SkipReceived();await Task.Delay(600);Cast(caster,420,view.ViewId);
            await target.WaitForRpcAsync("80 bd 05 2a 00 00 00 00 00 00 00",TimeSpan.FromSeconds(12));
            Check.That(context.Server.DebugLog().Contains("[AURA] 42 off"),"Normal420 deactivates aura without cooldown retries");
            Check.That(target.Received(ModPattern(489,172,true)).Count>siblingOff,"Guaranteed sweep removes remaining upgrade");
            int after=target.Received(ModPattern(695,522)).Count;
            await Task.Delay(5600);
            Check.Equal(after,target.Received(ModPattern(695,522)).Count,"Stopped aura never re-adds child records");
        }
        Check.Equal(0,caster.World.Problems.Count+target.World.Problems.Count,"Native fixture wire coverage");
    }
}
