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
}
