using System.Text.RegularExpressions;
namespace HeadlessClient;
public static partial class Scenarios
{
    private static async Task TypedAbilityResistanceScenario(ScenarioContext context)
    {
        var caster=await context.AddPlayerAsync(new CharacterSeed("ResistCaster"){Level=50,Organization=2,Health=5000,MaxHealth=5000,InnerStrength=2000,MaxInnerStrength=2000});
        var baseline=await context.AddPlayerAsync(new CharacterSeed("ResistBaseline"){X=17200,Level=50,Organization=1,Health=5000,MaxHealth=5000,InnerStrength=2000,MaxInnerStrength=2000});
        var buffed=await context.AddPlayerAsync(new CharacterSeed("ResistBuffed"){X=17300,Level=50,Organization=1,Health=5000,MaxHealth=5000,InnerStrength=2000,MaxInnerStrength=2000});
        var dot=await context.AddPlayerAsync(new CharacterSeed("ResistDot"){X=17400,Level=50,Organization=1,Health=5000,MaxHealth=5000,InnerStrength=2000,MaxInnerStrength=2000});
        foreach(var player in new[]{caster,baseline,buffed,dot})
        {
            await context.Database.ExecuteAsync($"DELETE FROM char_abilities WHERE char_id={player.CharId}");
            await LoadAbilitiesAsync(context,player,TypedResistanceFixture.Abilities);await player.EnterWorldAsync();
            player.SendChat("?pvpflag on");await player.WaitForViewUpdateAsync(2,OwnPvpFlagOn);
        }
        async Task Hit(ushort ability,HeadlessPlayer victim,int expected)
        {
            var view=await caster.WaitForViewNamedAsync(victim.Handle);int start=context.Server.DebugLog().Length;
            Cast(caster,ability,view.ViewId);
            await caster.WaitUntilAsync(()=>Regex.IsMatch(context.Server.DebugLog()[start..],@"\[PVP\] ResistCaster's .+ lands on "+victim.Handle+" for "+expected+@"(?![0-9])"),
                "normal ability damage "+ability+"="+expected,TimeSpan.FromSeconds(10));
            Console.WriteLine($"Actual normal cast{ability} on {victim.Handle}: damage{expected}");
        }
        await Hit(330,baseline,424);await Task.Delay(4500);
        Cast(buffed,8,2);await buffed.WaitForRpcAsync(ModPattern(8,157));await Hit(330,buffed,348);
        Cast(buffed,5,2);await buffed.WaitForRpcAsync(ModPattern(5,1033));await Hit(1135,baseline,83);
        var spy=caster.Received(ModPattern(1112,1034)).Last(r=>r.Bytes[17]==0);
        Check.Equal(29,BitConverter.ToInt32(spy.Bytes,13),"Actual Spy Style contributes29 thrown influence");
        Console.WriteLine("Actual SpyStyle1112 type15/target1034/value29 record: "+Convert.ToHexString(spy.Bytes));
        int styleOffs=caster.Received(ModPattern(1112,1034,true)).Count;
        await Task.Delay(8500);await Hit(1135,buffed,68);
        Check.Equal(styleOffs,caster.Received(ModPattern(1112,1034,true)).Count,"Same caster stance/influence across thrown comparison");
        Cast(dot,8,2);await dot.WaitForRpcAsync(ModPattern(8,157));
        var dotView=await caster.WaitForViewNamedAsync(dot.Handle);Cast(caster,474,dotView.ViewId);
        await dot.WaitForRpcAsync(ModPattern(474,1157));
        await dot.WaitUntilAsync(()=>context.Server.DebugLog().Contains("[TYPED-RESISTANCE OURS] 474 type2 total75"),"DOT reads active resistance",TimeSpan.FromSeconds(8));
        await dot.WaitUntilAsync(()=>dot.Received(ModPattern(8,157,true)).Count>0,"8 natural expiry",TimeSpan.FromSeconds(25));
        int expiry=context.Server.DebugLog().Length;await Task.Delay(3000);
        Check.That(context.Server.DebugLog()[expiry..].Contains("474 ticks on ResistDot"),"Same DOT continues after buff expiry");
        Check.That(!context.Server.DebugLog()[expiry..].Contains("[TYPED-RESISTANCE OURS] 474 type2 total75"),"Expired buff no longer mitigates later ticks");
        Check.Equal(0,new[]{caster,baseline,buffed,dot}.Sum(p=>p.World.Problems.Count),"Typed damage framing");
        await File.WriteAllTextAsync(Path.Combine(Path.GetTempPath(),"mxo-a1-typed-success-runtime-20261112.log"),context.Server.DebugLog());
    }
}
