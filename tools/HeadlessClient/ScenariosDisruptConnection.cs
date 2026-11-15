namespace HeadlessClient;
public static partial class Scenarios
{
    private static async Task DisruptConnectionToolRefusal(ScenarioContext context)
    {
        foreach (uint tool in new uint[]{0,470,531})
        {
            var caster=await context.AddPlayerAsync(new CharacterSeed("OldDisrupt"+tool){Level=50,InnerStrength=200,MaxInnerStrength=200,
                Items=tool==0?Array.Empty<(int,uint,int)>():new[]{(1,tool,1)}});
            await context.Database.ExecuteAsync($"DELETE FROM char_abilities WHERE char_id={caster.CharId}");
            await LoadAbilitiesAsync(context,caster,1124,384);
            await caster.EnterWorldAsync();
            await caster.WaitForRpcAsync(PassiveRecord(384,995,15));
            Cast(caster,1124);
            await caster.WaitForRpcAsync("80 ba 64 04");
            await Task.Delay(2400); // the old template's two-second cast must never start/land
            Check.That(context.Server.DebugLog().Contains("Denied 1124: A matching tool"),"Exact tool gate reached");
            Check.Equal(0,caster.Received("80 bd 05 64 04").Count,"No activation");
            Check.Equal(0,caster.Received("80 bc ?? ?? ?? ?? 00 64 04 00 00").Count,"No modifier");
            Check.That(!ViewUpdateValues(caster,2,"02 80 80 80 10").Any(v=>v<200),"No IS charge");
            Check.Equal(0,caster.Received("80 bd 01 64 04").Count,"No cooldown");
            Check.Equal(0,caster.Received("80 ac 64 04").Count,"No cast bar");
            Check.That(!ViewUpdateValues(caster,2,"02 80 80 80 40").Any(v=>v<500),"No health loss");
            caster.SendChat("?combatdepth cast 1124");
            await caster.WaitForTextAsync("Combat depth cast requested 1124");
            await caster.WaitUntilAsync(()=>caster.Received("80 ba 64 04").Count==2,"Strict dev cast reaches same normal gate",TimeSpan.FromSeconds(3));
            Check.Equal(0,caster.World.Problems.Count,"Refusal protocol");
            caster.Dispose();
        }
    }
}
