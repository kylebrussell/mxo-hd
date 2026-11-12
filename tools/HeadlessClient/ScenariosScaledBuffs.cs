namespace HeadlessClient;

public static partial class Scenarios
{
    private static async Task AbilityScaledBuffs(ScenarioContext context)
    {
        var p=await context.AddPlayerAsync(new CharacterSeed("ScaledBuff") { Level=20,Health=100,InnerStrength=1000,MaxInnerStrength=1000 });
        await context.Database.ExecuteAsync($"DELETE FROM char_abilities WHERE char_id={p.CharId}");
        await p.EnterWorldAsync();
        Cast(p,124);
        var damage=await p.WaitForRpcAsync(ModPattern(124,171));
        Check.Equal(10,BitConverter.ToInt32(damage.Bytes,13),"Firearms Skill uses caster20 x0.5");
        Check.Equal(20f,BitConverter.ToSingle(damage.Bytes,18),"Stored20-second duration");
        await p.WaitForRpcAsync(ModPattern(124,171,true),TimeSpan.FromSeconds(25));
        Cast(p,170);
        var healing=await p.WaitForRpcAsync(ModPattern(170,174));
        Check.Equal(10,BitConverter.ToInt32(healing.Bytes,13),"Passive Code uses caster level");
        Cast(p,54);
        await p.WaitUntilAsync(()=>context.Server.DebugLog().Contains("Heal 54 -> ScaledBuff for 40"),
            "Scaled healing buff reaches the actual heal consumer",TimeSpan.FromSeconds(10));
        p.SendRpc("80 bb aa 00");
        await p.WaitForRpcAsync(ModPattern(170,174,true));
        Check.Equal(0,p.World.Problems.Count,"Scaled self buff protocol");
    }

    private static async Task AbilityScaledBuffVitals(ScenarioContext context)
    {
        var low=await context.AddPlayerAsync(new CharacterSeed("BuffLow") { Level=20 });
        var high=await context.AddPlayerAsync(new CharacterSeed("BuffHigh") { X=17220,Level=50 });
        var target=await context.AddPlayerAsync(new CharacterSeed("BuffRecipient") { X=17120,Level=50 });
        await context.Database.ExecuteAsync($"DELETE FROM char_abilities WHERE char_id IN ({low.CharId},{high.CharId},{target.CharId})");
        foreach(var p in new[]{low,high,target}) await p.EnterWorldAsync();
        var lowView=await low.WaitForViewNamedAsync(target.Handle);
        var highView=await high.WaitForViewNamedAsync(target.Handle);
        Cast(low,417,lowView.ViewId);
        var initial=await target.WaitForRpcAsync(ModPattern(417,106));
        Check.Equal(200,BitConverter.ToInt32(initial.Bytes,13),"Friendly health boost uses caster20, not recipient50");
        await target.WaitForViewUpdateAsync(2,"02 80 80 ?? bc 02"); // own MaxHealth14=700
        Cast(high,419,highView.ViewId);
        var removed=await target.WaitForRpcAsync(ModPattern(417,106,true));
        Check.Equal(200,BitConverter.ToInt32(removed.Bytes,13),"Replacement removes the originally applied amount");
        var replacement=await target.WaitForRpcAsync(ModPattern(419,106));
        Check.Equal(500,BitConverter.ToInt32(replacement.Bytes,13),"New caster50 health bonus");
        await target.WaitForViewUpdateAsync(2,"02 80 80 ?? e8 03"); // 1000, not1200
        target.SendRpc("80 bb a3 01");
        await target.WaitForRpcAsync(ModPattern(419,106,true));
        await target.WaitForViewUpdateAsync(2,"02 80 80 ?? f4 01"); // original500
        Check.Equal(0,low.World.Problems.Count+high.World.Problems.Count+target.World.Problems.Count,"Scaled friendly vital protocol");
    }

    private static async Task AbilityBeneficialGroups(ScenarioContext context)
    {
        var caster=await context.AddPlayerAsync(new CharacterSeed("GroupSource") {Level=20,InnerStrength=1000,MaxInnerStrength=1000});
        var member=await context.AddPlayerAsync(new CharacterSeed("GroupMember") {X=17120,Level=50});
        var outsider=await context.AddPlayerAsync(new CharacterSeed("GroupOutsider") {X=17220,Level=50});
        await context.Database.ExecuteAsync($"DELETE FROM char_abilities WHERE char_id IN ({caster.CharId},{member.CharId},{outsider.CharId})");
        foreach(var p in new[]{caster,member,outsider}) await p.EnterWorldAsync();
        caster.SendRpc(GroupInvite(Team,member.Handle,""));
        await member.WaitForRpcAsync("80 8f 0c 00");
        member.SendRpc(InviteAnswer(Team,0,caster.Handle));
        await member.WaitForRpcAsync("80 8e");
        await caster.WaitForRpcAsync($"70 03 {Le(member.CharId)} 09 00");
        Cast(caster,489);
        var group=await member.WaitForRpcAsync(ModPattern(489,172),TimeSpan.FromSeconds(15));
        Check.Equal(10,BitConverter.ToInt32(group.Bytes,13),"Group buff uses caster20, not recipient50");
        await caster.WaitForRpcAsync(ModPattern(489,174));
        Check.Equal(0,outsider.Received(ModPattern(489,172)).Count,"Group buff excludes a nearby nonmember");
        Cast(caster,42);
        await caster.WaitForRpcAsync("80 bd 05 2a 00 00 00 00 00 00 01");
        var pulse=await member.WaitForRpcAsync(ModPattern(695,522));
        Check.Equal(25,BitConverter.ToInt32(pulse.Bytes,13),"Combat Aura uses source20 native1.3 truncation");
        Check.Equal(5f,BitConverter.ToSingle(pulse.Bytes,18),"Aura effect lasts one5s pulse");
        await member.WaitUntilAsync(()=>member.Received(ModPattern(695,522)).Count>=2,"Aura repeats its effect",TimeSpan.FromSeconds(8));
        Check.Equal(0,outsider.Received(ModPattern(695,522)).Count,"Aura excludes nonmembers");
        Cast(caster,42);
        await caster.WaitForRpcAsync("80 bd 05 2a 00 00 00 00 00 00 00");
        member.SkipReceived();
        await member.WaitForRpcAsync(ModPattern(695,522,true),TimeSpan.FromSeconds(8));
        Cast(caster,485);
        await caster.WaitForRpcAsync("80 bd 05 e5 01 00 00 00 00 00 01");
        var damage=await member.WaitForRpcAsync(ModPattern(696,171));
        Check.Equal(10,BitConverter.ToInt32(damage.Bytes,13),"Combat Aura2 gives actual ballistic damage influence");
        await Task.Delay(600);
        Cast(caster,485);
        await caster.WaitForRpcAsync("80 bd 05 e5 01 00 00 00 00 00 00");
        Check.Equal(0,caster.World.Problems.Count+member.World.Problems.Count+outsider.World.Problems.Count,"Beneficial group protocol");
    }

    private static async Task AbilityTacticalBlur(ScenarioContext context)
    {
        var actor=await context.AddPlayerAsync(new CharacterSeed("BlurTactician") {InnerStrength=500,MaxInnerStrength=500});
        var observer=await context.AddPlayerAsync(new CharacterSeed("BlurWatcher") {X=17120});
        await context.Database.ExecuteAsync($"DELETE FROM char_abilities WHERE char_id IN ({actor.CharId},{observer.CharId})");
        await actor.EnterWorldAsync(); await observer.EnterWorldAsync();
        var view=await observer.WaitForViewNamedAsync(actor.Handle);
        Cast(actor,15);
        await actor.WaitForRpcAsync("80 bd 05 0f 00 00 00 00 00 00 01");
        Cast(actor,9);
        await actor.WaitForRpcAsync("80 ba"); // script mutual exclusion, no energized switch-on
        Check.Equal(0,actor.Received("80 bd 05 09 00 00 00 00 00 00 01").Count,"Precise refuses energized conflict");
        await Task.Delay(600); Cast(actor,15);
        await actor.WaitForRpcAsync("80 bd 05 0f 00 00 00 00 00 00 00");
        await Task.Delay(600); Cast(actor,9);
        await actor.WaitForRpcAsync("80 bd 05 09 00 00 00 00 00 00 01");
        Cast(actor,49);
        await actor.WaitForRpcAsync(ModPattern(49,520),TimeSpan.FromSeconds(8));
        await actor.WaitForViewUpdateAsync(2,"02 80 80 80 80 80 02 40 00");
        await observer.WaitForViewUpdateAsync(view.ViewId,"02 80 80 80 40 40 00");
        actor.SendRpc("80 bb 31 00");
        await actor.WaitForRpcAsync(ModPattern(49,520,true));
        await observer.WaitForViewUpdateAsync(view.ViewId,"02 80 80 80 40 00 00");
        Check.Equal(0,actor.World.Problems.Count+observer.World.Problems.Count,"Tactical and blur protocol");
    }
}
