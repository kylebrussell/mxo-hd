namespace HeadlessClient;

public static partial class Scenarios
{
    // Preserve packet order across ordinary HP updates and DOT HP+FX updates.
    private static List<ushort> PresentedOwnHealth(HeadlessPlayer player)
    {
        var values=new List<ushort>();var needle=WireFormat.FromHex("02 00 02 80 80 80");
        foreach(var section in player.World.ViewSnapshot())
            for(int at=BytePattern.IndexOf(section,needle,0);at>=0;at=BytePattern.IndexOf(section,needle,at+1))
                if(at+9<=section.Length && (section[at+6]&0x40)!=0) values.Add(BitConverter.ToUInt16(section,at+7));
        return values;
    }

    private static async Task AssertPresentedHealth(HeadlessPlayer player,HeadlessPlayer watcher,ushort view,string stage)
    {
        player.SkipReceived();player.SendChat("?health");
        var report=await player.WaitForTextAsync("Current health: ");
        var text=System.Text.Encoding.Latin1.GetString(report.Bytes);
        ushort actual=ushort.Parse(System.Text.RegularExpressions.Regex.Match(text,@"Current health: (\d+)").Groups[1].Value);
        await watcher.WaitUntilAsync(()=>ViewUpdateValues(watcher,view,"02 80 80 10").LastOrDefault()==actual,
            stage+" observer finalHP equals authoritative "+actual,TimeSpan.FromSeconds(2));
        Check.Equal(actual,PresentedOwnHealth(player).Last(),stage+" own finalHP equals authoritative");
        Check.Equal(actual,ViewUpdateValues(watcher,view,"02 80 80 10").Last(),stage+" observer finalHP");
    }

    private static async Task HealthPresentationHealRegen(ScenarioContext context)
    {
        var p=await context.AddPlayerAsync(new CharacterSeed("ProcPatient") {Level=20,Health=100,MaxHealth=1000,InnerStrength=1000});
        var healer=await context.AddPlayerAsync(new CharacterSeed("ProcHealer") {X=17120,Level=50,InnerStrength=1000});
        await context.Database.ExecuteAsync($"DELETE FROM char_abilities WHERE char_id IN ({p.CharId},{healer.CharId})");
        await p.EnterWorldAsync();await healer.EnterWorldAsync();var view=await healer.WaitForViewNamedAsync(p.Handle);
        Cast(p,196);await p.WaitForRpcAsync(ModPattern(196,196));
        int before=PresentedOwnHealth(p).Count;
        Cast(healer,77,view.ViewId);
        await healer.WaitForRpcAsync("80 bd 01 4d 00 04 00 00 80 40 01"); // landed external heal, not an earlier regen tick
        await p.WaitUntilAsync(()=>PresentedOwnHealth(p).Count>=before+2,"Heal plus Ignore Pain packets",TimeSpan.FromSeconds(5));
        await AssertPresentedHealth(p,healer,view.ViewId,"External healing");
        before=PresentedOwnHealth(p).Count;
        await p.WaitUntilAsync(()=>PresentedOwnHealth(p).Count>=before+2,"Natural regen plus Ignore Pain packets",TimeSpan.FromSeconds(3));
        await AssertPresentedHealth(p,healer,view.ViewId,"Natural regeneration");
        Check.Equal(0,p.World.Problems.Count+healer.World.Problems.Count,"Heal/regen presentation protocol");
    }

    private static async Task HealthPresentationDamage(ScenarioContext context)
    {
        var p=await context.AddPlayerAsync(new CharacterSeed("ProcDamaged") {Level=20,Organization=1,Health=1000,MaxHealth=1000,InnerStrength=1000});
        var enemy=await context.AddPlayerAsync(new CharacterSeed("ProcAttacker") {X=17120,Level=50,Organization=2,InnerStrength=2000,MaxInnerStrength=2000});
        await context.Database.ExecuteAsync($"DELETE FROM char_abilities WHERE char_id IN ({p.CharId},{enemy.CharId})");
        await p.EnterWorldAsync();await enemy.EnterWorldAsync();var view=await enemy.WaitForViewNamedAsync(p.Handle);
        foreach(var actor in new[]{p,enemy}) {actor.SendChat("?pvpflag on");await actor.WaitForViewUpdateAsync(2,OwnPvpFlagOn);}
        Cast(p,196);await p.WaitForRpcAsync(ModPattern(196,196));
        await PrepareFreeCastViralFixture(context,enemy,474);
        Cast(enemy,474,view.ViewId);await p.WaitForRpcAsync(ModPattern(474,1157));
        await p.WaitForViewUpdateAsync(2,"02 80 80 80 c0 ?? ?? 30 30 04 00 28 ??",TimeSpan.FromSeconds(6));
        await AssertPresentedHealth(p,enemy,view.ViewId,"Damage over time");
        p.SendChat("?dismissbuff 196");await p.WaitForRpcAsync(ModPattern(196,196,true));
        Check.Equal(0,p.World.Problems.Count+enemy.World.Problems.Count,"Damage presentation protocol");
    }
}
