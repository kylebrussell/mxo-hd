namespace HeadlessClient;

public static partial class Scenarios
{
    private static async Task SetProcHealth(HeadlessPlayer p,ushort value,ushort expected)
    {
        int before=p.World.ViewSnapshot().Count;
        p.SendChat("?health "+value);
        var pattern=BytePattern.Parse("02 00 02 80 80 80 40 "+Le(expected));
        await p.WaitUntilAsync(()=>p.World.ViewSnapshot().Skip(before).Any(pattern.Occurs),
            "New health update after callback: "+expected,TimeSpan.FromSeconds(8));
    }

    private static async Task ZenMaster(ScenarioContext context)
    {
        // Explicit forced level99 disposable actor: shipped parent871 is inaccessible at retail level50.
        var p=await context.AddPlayerAsync(new CharacterSeed("ZenMaster") {Level=99,Health=500,MaxHealth=500,InnerStrength=198,MaxInnerStrength=200});
        await context.Database.ExecuteAsync($"DELETE FROM char_abilities WHERE char_id={p.CharId}");
        await LoadAbilitiesAsync(context,p,871);
        await p.EnterWorldAsync();
        const string activation="81 67 ?? ?? ?? ?? 20 21 00 c6";
        await SetProcHealth(p,400,400);
        var restored=await p.WaitForRpcAsync(activation);
        int values=BitConverter.ToUInt16(restored.Bytes,19);
        Check.Equal((ushort)1,BitConverter.ToUInt16(restored.Bytes,values),"Restore line has its required numeric substitution");
        Check.That(BitConverter.ToInt32(restored.Bytes,values+2) is >=0 and <=5,"Restore line reports capped IS delta");
        await p.WaitForViewUpdateAsync(2,"02 80 80 80 10 c8 00");
        int previous=p.Received(activation).Count;
        await SetProcHealth(p,450,450); // external healing invokes the same sourced callback
        await p.WaitUntilAsync(()=>p.Received(activation).Count>previous,"Zen external-heal activation",TimeSpan.FromSeconds(5));
        await SetProcHealth(p,500,500);
        previous=p.Received(activation).Count;
        Cast(p,872);await Task.Delay(400);
        Check.Equal(previous,p.Received(activation).Count,"Direct872 refused without executing internal effect");
        await KillForResurrection(p);
        Check.Equal(0,p.World.Problems.Count,"Zen Master source health proc protocol");
    }

    private static async Task HealthProcLifecycle(ScenarioContext context)
    {
        var p=await context.AddPlayerAsync(new CharacterSeed("HealthProc") {Level=20,Health=100,MaxHealth=1000,InnerStrength=1000,MaxInnerStrength=1000});
        await context.Database.ExecuteAsync($"DELETE FROM char_abilities WHERE char_id={p.CharId}");
        await p.EnterWorldAsync();
        Cast(p,196);
        var proc=await p.WaitForRpcAsync(ModPattern(196,196));
        Check.Equal(10,BitConverter.ToInt32(proc.Bytes,13),"Fixture's native15-bit minimum sampled once");
        await SetProcHealth(p,90,100);
        await SetProcHealth(p,110,120); // script does not filter external healing
        p.SendChat("?dismissbuff 196");await p.WaitForRpcAsync(ModPattern(196,180,true));
        var off=await p.WaitForRpcAsync(ModPattern(196,196,true));
        Check.Equal(10,BitConverter.ToInt32(off.Bytes,13),"Original proc value retained on off record");
        await SetProcHealth(p,80,80);
        Cast(p,861);await p.WaitForRpcAsync(ModPattern(861,861),TimeSpan.FromSeconds(8));
        await SetProcHealth(p,70,120); // NeutralHeal minimum50, once per transaction
        await SetProcHealth(p,160,210);
        await KillForResurrection(p);
        Check.That(p.Received(ModPattern(861,861,true)).Count>0,"Death removed Balance before the death dialog");
        Check.Equal(0,p.World.Problems.Count,"Health callback lifecycle protocol");
    }

    private static async Task HealthReactiveArea(ScenarioContext context)
    {
        var p=await context.AddPlayerAsync(new CharacterSeed("CallbackOwner") {Level=50,Organization=1,Health=1000,MaxHealth=1000,InnerStrength=2000,MaxInnerStrength=2000});
        var friend=await context.AddPlayerAsync(new CharacterSeed("CallbackFriend") {X=17120,Level=50,Organization=1,Health=1000,MaxHealth=1000});
        var enemy=await context.AddPlayerAsync(new CharacterSeed("CallbackEnemy") {X=17220,Level=50,Organization=2,Health=1000,MaxHealth=1000});
        await context.Database.ExecuteAsync($"DELETE FROM char_abilities WHERE char_id IN ({p.CharId},{friend.CharId},{enemy.CharId})");
        foreach(var actor in new[]{p,friend,enemy}) await actor.EnterWorldAsync();
        await p.WaitForViewNamedAsync(enemy.Handle);await p.WaitForViewNamedAsync(friend.Handle);
        enemy.SendChat("?pvpflag on");await enemy.WaitForViewUpdateAsync(2,OwnPvpFlagOn);
        p.SendChat("?pvpflag on");await p.WaitForViewUpdateAsync(2,OwnPvpFlagOn);
        Cast(p,941);await p.WaitForRpcAsync(ModPattern(941,941),TimeSpan.FromSeconds(8));
        Cast(enemy,941);await enemy.WaitForRpcAsync(ModPattern(941,941),TimeSpan.FromSeconds(8));
        p.SendChat("?health 900");
        const string reactiveDamage="81 67 ?? ?? ?? ?? f9 00 00 7c 00 00 10 00 00 00 b8 0e 80";
        var damage=await enemy.WaitForRpcAsync(reactiveDamage);
        int values=BitConverter.ToUInt16(damage.Bytes,19);
        Check.Equal(50,BitConverter.ToInt32(damage.Bytes,values+2),"Reactive neutral damage samples50 with no influence");
        await p.WaitForRpcAsync(reactiveDamage);
        await p.WaitForViewUpdateAsync(2,"02 80 80 80 40 52 03"); //900-50=850: each reactive parent once
        enemy.SendChat("?dismissbuff 941");await enemy.WaitForRpcAsync(ModPattern(941,941,true));
        p.SendChat("?dismissbuff 941");await p.WaitForRpcAsync(ModPattern(941,941,true));
        Cast(p,943);await p.WaitForRpcAsync(ModPattern(943,943),TimeSpan.FromSeconds(8));
        await SetProcHealth(friend,300,300);await SetProcHealth(enemy,200,200);
        int friendBefore=ViewUpdateValues(friend,2,"02 80 80 80 40").Count;
        int enemyBefore=ViewUpdateValues(enemy,2,"02 80 80 80 40").Count;
        await SetProcHealth(p,400,460);
        await friend.WaitUntilAsync(()=>ViewUpdateValues(friend,2,"02 80 80 80 40").Skip(friendBefore).Any(h=>h>=360&&h<=380),
            "Code Conversion heals a nearby friendly non-team player",TimeSpan.FromSeconds(5));
        Check.That(ViewUpdateValues(enemy,2,"02 80 80 80 40").Skip(enemyBefore).All(h=>h<250),"Code Conversion excludes the hostile player");
        p.SendChat("?dismissbuff 943");await p.WaitForRpcAsync(ModPattern(943,943,true));
        await SetProcHealth(p,300,300);
        Check.Equal(0,p.World.Problems.Count+friend.World.Problems.Count+enemy.World.Problems.Count,"Reactive and friendly health-area protocol");
    }

    private static async Task HealthDeflectLineup(ScenarioContext context)
    {
        var p=await context.AddPlayerAsync(new CharacterSeed("DeflectOwner") {Level=20,Organization=1,Health=500,MaxHealth=1000,InnerStrength=2000,MaxInnerStrength=2000});
        var friend=await context.AddPlayerAsync(new CharacterSeed("DeflectFriend") {X=17120,Level=50,Organization=1});
        var enemy=await context.AddPlayerAsync(new CharacterSeed("DeflectEnemy") {X=17220,Level=50,Organization=2,InnerStrength=1000,MaxInnerStrength=1000});
        await context.Database.ExecuteAsync($"DELETE FROM char_abilities WHERE char_id IN ({p.CharId},{friend.CharId},{enemy.CharId})");
        await LoadAbilitiesAsync(context,p,1137);
        foreach(var actor in new[]{p,friend,enemy}) await actor.EnterWorldAsync();
        p.SendRpc(GroupInvite(Team,friend.Handle,""));await friend.WaitForRpcAsync("80 8f 0c 00");
        friend.SendRpc(InviteAnswer(Team,0,p.Handle));await friend.WaitForRpcAsync("80 8e");await p.WaitForRpcAsync($"70 03 {Le(friend.CharId)} 09 00");
        Cast(p,380);await p.WaitForRpcAsync("80 bd 05 7c 01 00 00 00 00 00 01",TimeSpan.FromSeconds(8));
        var ballistic=await friend.WaitForRpcAsync(ModPattern(692,1010));Check.Equal(19,BitConverter.ToInt32(ballistic.Bytes,13),"Deflect uses caster20");
        await friend.WaitForRpcAsync(ModPattern(692,1011));
        Check.That(friend.Received(ModPattern(692,564)).Count>0,"Concentration component accompanies the pulse");
        await friend.WaitUntilAsync(()=>friend.Received(ModPattern(692,1010)).Count>=2,"Deflect parent repeats its child",TimeSpan.FromSeconds(8));
        Cast(p,380);await p.WaitForRpcAsync("80 bd 05 7c 01 00 00 00 00 00 00");
        Cast(p,378);await friend.WaitForRpcAsync(ModPattern(693,183),TimeSpan.FromSeconds(8));
        friend.SendChat("?pvpflag on");await friend.WaitForViewUpdateAsync(2,OwnPvpFlagOn);
        var victim=await enemy.WaitForViewNamedAsync(friend.Handle);
        Cast(enemy,481,victim.ViewId);await friend.WaitForRpcAsync(ModPattern(481,481),TimeSpan.FromSeconds(8));
        await friend.WaitForRpcAsync(ModPattern(481,481,true),TimeSpan.FromSeconds(8)); // fixture roll0 < term sweep4
        Cast(p,378);await p.WaitForRpcAsync("80 bd 05 7a 01 00 00 00 00 00 00");
        Cast(p,1137);await p.WaitForRpcAsync(ModPattern(1137,881),TimeSpan.FromSeconds(8));
        int concealOff=p.Received(ModPattern(1137,881,true)).Count;
        Cast(p,506);await Task.Delay(1000);
        Check.Equal(concealOff,p.Received(ModPattern(1137,881,true)).Count,"Line Up preserves concealment during its cast");
        await p.WaitForRpcAsync(ModPattern(506,524),TimeSpan.FromSeconds(8));
        Check.That(p.Received(ModPattern(506,171)).Count>0,"Both Line Up modifiers arrived");
        await SetProcHealth(p,1000,1000); // healing/full health cannot cancel the damage-sensitive modifier
        Check.Equal(0,p.Received(ModPattern(506,171,true)).Count,"Healing preserves Line Up damage component");
        p.SendChat("?health 400");await p.WaitForRpcAsync(ModPattern(506,171,true));await p.WaitForRpcAsync(ModPattern(506,524,true));
        Check.Equal(0,p.World.Problems.Count+friend.World.Problems.Count+enemy.World.Problems.Count,"Deflect parents and concealed Line Up protocol");
    }

    private static async Task HealthLineupTimings(ScenarioContext context)
    {
        var p=await context.AddPlayerAsync(new CharacterSeed("LineupTimer") {Level=20,Health=500,MaxHealth=1000,InnerStrength=1000,MaxInnerStrength=1000});
        await context.Database.ExecuteAsync($"DELETE FROM char_abilities WHERE char_id={p.CharId}");await p.EnterWorldAsync();
        Cast(p,506);var accuracy=await p.WaitForRpcAsync(ModPattern(506,524),TimeSpan.FromSeconds(9));
        Check.Equal(20f,BitConverter.ToSingle(accuracy.Bytes,18),"Accuracy lasts20s");
        var damage=p.Received(ModPattern(506,171)).Last();Check.Equal(12f,BitConverter.ToSingle(damage.Bytes,18),"Damage and its health callback last12s");
        await p.WaitForRpcAsync(ModPattern(506,171,true),TimeSpan.FromSeconds(15));
        Check.Equal(0,p.Received(ModPattern(506,524,true)).Count,"Accuracy survives the12s component expiry");
        p.SendChat("?health 400");await p.WaitForRpcAsync(ModPattern(1137,881,true));
        await Task.Delay(600);Check.Equal(0,p.Received(ModPattern(506,524,true)).Count,"Damage after12s does not cancel the remaining accuracy");
        await p.WaitForRpcAsync(ModPattern(506,524,true),TimeSpan.FromSeconds(10));
        Check.Equal(0,p.World.Problems.Count,"Line Up split duration protocol");
    }
}
