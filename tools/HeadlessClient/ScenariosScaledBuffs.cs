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

    private static async Task AbilityDisguiseTools(ScenarioContext context)
    {
        var actor=await context.AddPlayerAsync(new CharacterSeed("DisguiseActor") {Level=20,Items=new[]{(1,615u,1)}});
        var observer=await context.AddPlayerAsync(new CharacterSeed("DisguiseObserver") {X=17120,Level=20});
        await context.Database.ExecuteAsync($"DELETE FROM char_abilities WHERE char_id IN ({actor.CharId},{observer.CharId}); " +
            $"INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES({actor.CharId},1,{AbilityGoid(1114)},20,1)");
        await actor.EnterWorldAsync(); await observer.EnterWorldAsync();
        var view=await observer.WaitForViewNamedAsync(actor.Handle);
        Cast(observer,1114);
        await observer.WaitForRpcAsync("80 ba");
        Cast(actor,1114);
        var state=await actor.WaitForRpcAsync(ModPattern(1114,881),TimeSpan.FromSeconds(8));
        Check.Equal(41f,BitConverter.ToSingle(state.Bytes,18),"Disguise duration31+learned20/2");
        await actor.WaitForViewUpdateAsync(2,"02 83 00 00 00 00 00 80 80 80 80 04 33 05 00 40");
        await observer.WaitForViewUpdateAsync(view.ViewId,"02 83 00 00 00 00 00 80 80 80 01 33 05 00 40");
        await observer.WaitForViewUpdateAsync(view.ViewId,"02 80 80 80 c0 08 00 04 00 00 00 02");
        Cast(actor,1114);
        await actor.WaitForRpcAsync(ModPattern(1114,881,true));
        await observer.WaitForViewUpdateAsync(view.ViewId,"02 83 01 ?? ?? ?? ?? 80 80 80 01 ?? ?? ?? ??");
        Check.Equal(0,actor.World.Problems.Count+observer.World.Problems.Count,"Disguise tool model protocol");
    }

    private static async Task AbilityBuffBufferDeath(ScenarioContext context)
    {
        var hacker=await context.AddPlayerAsync(new CharacterSeed("BufferHacker") {Level=50,InnerStrength=2000,MaxInnerStrength=2000,Organization=1});
        var medic=await context.AddPlayerAsync(new CharacterSeed("BufferMedic") {X=17220,Level=50,InnerStrength=1000,MaxInnerStrength=1000,Organization=2});
        var target=await context.AddPlayerAsync(new CharacterSeed("BufferVictim") {X=17120,Level=20,InnerStrength=1000,MaxInnerStrength=1000,Organization=2});
        await context.Database.ExecuteAsync($"DELETE FROM char_abilities WHERE char_id IN ({hacker.CharId},{target.CharId})");
        await hacker.EnterWorldAsync(); await target.EnterWorldAsync(); await medic.EnterWorldAsync();
        var corpseView=await medic.WaitForViewNamedAsync(target.Handle);
        target.SendChat("?pvpflag on"); await target.WaitForViewUpdateAsync(2,OwnPvpFlagOn);
        var view=await hacker.WaitForViewNamedAsync(target.Handle);
        Cast(target,327); await target.WaitForRpcAsync(ModPattern(327,172));
        Cast(hacker,481,view.ViewId); await target.WaitForRpcAsync("80 bc 09 00",TimeSpan.FromSeconds(8));
        target.SkipReceived(); Cast(target,489); await target.WaitForRpcAsync("80 ba");
        Check.Equal(0,target.Received(ModPattern(489,172)).Count,"Overflow blocks a new47-unit group upgrade");
        await KillForResurrection(target);
        Cast(medic,416,corpseView.ViewId); await target.WaitForRpcAsync("81 88",TimeSpan.FromSeconds(8));
        target.SendRpc(NativeResurrectionReply(416)); await target.WaitForRpcAsync(ResurrectionLine);
        target.SkipReceived(); Cast(target,489);
        await target.WaitForRpcAsync(ModPattern(489,172),TimeSpan.FromSeconds(15));
        Check.Equal(0,hacker.World.Problems.Count+target.World.Problems.Count,"Buffer death release and resurrection protocol");
    }

    private static async Task AbilityDrainFirewalls(ScenarioContext context)
    {
        var strong=await context.AddPlayerAsync(new CharacterSeed("StrengthDrainer") {Level=20,InnerStrength=25,MaxInnerStrength=25});
        var aim=await context.AddPlayerAsync(new CharacterSeed("AimDrainer") {X=17120,Level=20,InnerStrength=30,MaxInnerStrength=30});
        var caster=await context.AddPlayerAsync(new CharacterSeed("FirewallCaster") {X=17220,Level=20,InnerStrength=2000,MaxInnerStrength=2000});
        await context.Database.ExecuteAsync($"DELETE FROM char_abilities WHERE char_id IN ({strong.CharId},{aim.CharId},{caster.CharId}); " +
            $"INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES({strong.CharId},1,{AbilityGoid(13)},20,1)");
        foreach(var p in new[]{strong,aim,caster}) await p.EnterWorldAsync();
        Cast(strong,13); var boost=await strong.WaitForRpcAsync(ModPattern(13,159));
        Check.Equal(40,BitConverter.ToInt32(boost.Bytes,13),"Hyper-Strength uses learned20 x2");
        Cast(aim,442); await aim.WaitForRpcAsync(ModPattern(442,524));
        await strong.WaitForRpcAsync(ModPattern(13,159,true),TimeSpan.FromSeconds(8));
        await aim.WaitForRpcAsync(ModPattern(442,524,true),TimeSpan.FromSeconds(8));
        await aim.WaitForRpcAsync("80 bd 01 ba 01 04"); // AddRecastTimer15 begins on deactivation
        foreach(ushort id in new ushort[]{68,319,63,436})
        {
            Cast(caster,id);
            ushort stat=(ushort)(id==68 || id==319 ? 157 : 183);
            await caster.WaitForRpcAsync(ModPattern(id,stat),TimeSpan.FromSeconds(12));
            caster.SendRpc($"80 bb {Le(id)}");
            await caster.WaitForRpcAsync(ModPattern(id,stat,true));
        }
        Cast(caster,16); await caster.WaitForRpcAsync(ModPattern(16,212));
        await Task.Delay(1200); Cast(caster,16);
        await caster.WaitForRpcAsync(ModPattern(16,212,true));
        Check.Equal(0,strong.World.Problems.Count+aim.World.Problems.Count+caster.World.Problems.Count,"Drain and firewall protocol");
    }

    private static async Task AbilityWeaponProficiencies(ScenarioContext context)
    {
        var p=await context.AddPlayerAsync(new CharacterSeed("WeaponStylist") {Level=50});
        await context.Database.ExecuteAsync($"DELETE FROM char_abilities WHERE char_id={p.CharId}");
        await LoadAbilitiesAsync(context,p,2,129,147,158);
        await context.Database.ExecuteAsync($"UPDATE char_abilities SET level=20 WHERE char_id={p.CharId}");
        await p.EnterWorldAsync();
        ushort previous=0;
        foreach(ushort id in new ushort[]{129,147,158})
        {
            Cast(p,id);
            var accuracy=await p.WaitForRpcAsync(ModPattern(id,524));
            Check.Equal(25,BitConverter.ToInt32(accuracy.Bytes,13),"Weapon style uses learned20, not character50");
            var damage=await p.WaitForRpcAsync(ModPattern(id,171));
            Check.Equal(6,BitConverter.ToInt32(damage.Bytes,13),"Stored min2 + native20*.235");
            if(previous!=0) Check.That(p.Received(ModPattern(previous,524,true)).Count>0,"Previous weapon style records removed before new style");
            previous=id;
        }
        Cast(p,17); await p.WaitForRpcAsync(ModPattern(158,524,true));
        Check.Equal(0,p.World.Problems.Count,"Weapon proficiency style protocol");
    }
}
