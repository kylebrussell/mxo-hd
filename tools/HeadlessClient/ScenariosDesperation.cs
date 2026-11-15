namespace HeadlessClient;
public static partial class Scenarios
{
    private static async Task DesperationSnapshot(ScenarioContext context)
    {
        var p=await context.AddPlayerAsync(new CharacterSeed("DesperationHigh") {Level=20,Health=501,MaxHealth=1000,InnerStrength=200,MaxInnerStrength=200});
        var half=await context.AddPlayerAsync(new CharacterSeed("DesperationHalf") {Level=20,Health=500,MaxHealth=1000,InnerStrength=200,MaxInnerStrength=200});
        var low=await context.AddPlayerAsync(new CharacterSeed("DesperationLow") {Level=20,Health=100,MaxHealth=1000,InnerStrength=200,MaxInnerStrength=200});
        await context.Database.ExecuteAsync($"DELETE FROM char_abilities WHERE char_id IN ({p.CharId},{half.CharId},{low.CharId})");
        foreach(var actor in new[]{p,half,low}) await actor.EnterWorldAsync();
        Cast(p,1149);Cast(half,1149);Cast(low,1149);
        var highRecord=await p.WaitForRpcAsync(ModPattern(1149,1044),TimeSpan.FromSeconds(8));
        var halfRecord=await half.WaitForRpcAsync(ModPattern(1149,1044),TimeSpan.FromSeconds(8));
        var lowRecord=await low.WaitForRpcAsync(ModPattern(1149,1044),TimeSpan.FromSeconds(8));
        Check.Equal(10,BitConverter.ToInt32(highRecord.Bytes,13),"Above half health snapshot");
        Check.Equal(50,BitConverter.ToInt32(halfRecord.Bytes,13),"Exactly half health snapshot");
        Check.Equal(90,BitConverter.ToInt32(lowRecord.Bytes,13),"Low health snapshot");
        Check.Equal(20f,BitConverter.ToSingle(highRecord.Bytes,18),"Stored duration20s");
        Check.That(p.Received("80 bd 01 7d 04 04 00 00 b4 42 01").Count>0,"Sourced90s cooldown");
        Check.That(ViewUpdateValues(p,2,"02 80 80 80 10").Contains((ushort)190),"Cast spends10IS");
        await SetProcHealth(p,100,100);
        Check.Equal(1,p.Received(ModPattern(1149,1044)).Count,"Health changes do not recompute the snapshot");
        Cast(p,1149);await Task.Delay(300);
        Check.Equal(1,p.Received(ModPattern(1149,1044)).Count,"Reuse90 prevents reapplication");
        half.SendChat("?dismissbuff 1149");
        var halfOff=await half.WaitForRpcAsync(ModPattern(1149,1044,true));
        Check.Equal(50,BitConverter.ToInt32(halfOff.Bytes,13),"Dismiss retains original value");
        await KillForResurrection(low);
        var deadOff=low.Received(ModPattern(1149,1044,true)).Last();
        Check.Equal(90,BitConverter.ToInt32(deadOff.Bytes,13),"Death clears original snapshot");
        await p.WaitUntilAsync(()=>p.Received(ModPattern(1149,1044,true)).Count>0,"Desperation expiry",TimeSpan.FromSeconds(25));
        var expired=p.Received(ModPattern(1149,1044,true)).Last();
        Check.Equal(10,BitConverter.ToInt32(expired.Bytes,13),"Expiry retains application value despite health change");
        Check.Equal(0,p.World.Problems.Count+half.World.Problems.Count+low.World.Problems.Count,"Desperation cosmetic protocol");
    }
}
