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
        int siblingOff=target.Received(ModPattern(327,172,true)).Count;
        Cast(caster,261,view.ViewId);
        await target.WaitUntilAsync(()=>context.Server.DebugLog().Contains("MachinegunFistComboAbility lands on DispelFixtureTarget"),"Normal261 landing",TimeSpan.FromSeconds(12));
        await target.WaitUntilAsync(()=>OwnHealth(target)>0 && OwnHealth(target)<10000,"Both proc and non-proc still do ordinary damage",TimeSpan.FromSeconds(8));
        if(proc)await target.WaitUntilAsync(()=>target.Received(ModPattern(49,520,true)).Count>off,"Forced0 removes Gaussian Blur",TimeSpan.FromSeconds(8));
        else {await Task.Delay(500);Check.Equal(off,target.Received(ModPattern(49,520,true)).Count,"Forced99 preserves upgrade on a landed hit");}
        Check.Equal(siblingOff,target.Received(ModPattern(327,172,true)).Count,"261 leaves sibling upgrade intact");
        Check.Equal(0,target.Received("80 bd 05 2a 00 00 00 00 00 00 00").Count,"261 leaves sibling aura on");
        if(proc)
        {
            // With the same guarded0 fixture, Delete Upgrade2's stored20% sweep removes every remaining parent.
            target.SkipReceived();await Task.Delay(600);Cast(caster,420,view.ViewId);
            await target.WaitForRpcAsync("80 bd 05 2a 00 00 00 00 00 00 00",TimeSpan.FromSeconds(12));
            Check.That(context.Server.DebugLog().Contains("[AURA] 42 off"),"Normal420 deactivates aura without cooldown retries");
            Check.That(target.Received(ModPattern(327,172,true)).Count>siblingOff,"Guaranteed sweep removes remaining upgrade");
            int after=target.Received(ModPattern(695,522)).Count;
            await Task.Delay(5600);
            Check.Equal(after,target.Received(ModPattern(695,522)).Count,"Stopped aura never re-adds child records");
        }
        Check.Equal(0,caster.World.Problems.Count+target.World.Problems.Count,"Native fixture wire coverage");
    }
}
