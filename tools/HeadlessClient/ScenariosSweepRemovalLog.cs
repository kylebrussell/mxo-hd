namespace HeadlessClient;
public static partial class Scenarios
{
    private static async Task ConsumableSweepRemovalLog(ScenarioContext context)
    {
        var patient=await context.AddPlayerAsync(new CharacterSeed("LoggedSweep") {Level=50,Organization=1,Health=5000,MaxHealth=5000,Items=new[]{(3,39410u,2)}});
        var enemy=await context.AddPlayerAsync(new CharacterSeed("SweepDebuffer") {Level=50,Organization=2,X=17120,InnerStrength=2000,MaxInnerStrength=2000});
        await LoadAbilitiesAsync(context,enemy,1006,97,343,401,476);
        foreach(var p in new[]{patient,enemy}) {await p.EnterWorldAsync();p.SendChat("?pvpflag on");await p.WaitForViewUpdateAsync(2,OwnPvpFlagOn);}
        var target=await enemy.WaitForViewNamedAsync(patient.Handle);
        var candidates=new[]{(Ability:(ushort)97,Stat:(ushort)342),(Ability:(ushort)343,Stat:(ushort)1009),(Ability:(ushort)401,Stat:(ushort)117),(Ability:(ushort)476,Stat:(ushort)106)};
        foreach(var candidate in candidates)
        { Cast(enemy,candidate.Ability,target.ViewId);await patient.WaitForRpcAsync(ModPattern(candidate.Ability,candidate.Stat)); }
        // No QA RNG override: four real debuff parents provide ordinary quarter-chance sweep opportunities.
        int start=context.Server.DebugLog().Length;
        patient.SendRpc("80 c6 03 00");await patient.WaitForRpcAsync(ModPattern(678,678));
        await patient.WaitUntilAsync(()=>context.Server.DebugLog().Substring(start).Contains("[SWEEP] 678 removed "),"Actual owned antibiotic removes and logs a parent",TimeSpan.FromSeconds(42));
        await Task.Delay(5500); // another periodic pulse must not report an already absent parent
        var logs=context.Server.DebugLog().Substring(start).Split('\n').Where(l=>l.Contains("[SWEEP] 678 removed ")).Select(l=>l.Trim()).ToList();
        Check.That(logs.Count>0,"At least one real parent removal is logged");
        foreach(var line in logs)
        {
            var match=System.Text.RegularExpressions.Regex.Match(line,@"^\[SWEEP\] 678 removed (\d+) from LoggedSweep$");
            Check.That(match.Success,"Sweep log includes source, removed parent and actual target name");
            ushort parent=ushort.Parse(match.Groups[1].Value);var candidate=candidates.Single(c=>c.Ability==parent);
            await patient.WaitUntilAsync(()=>patient.Received(ModPattern(parent,candidate.Stat,true)).Count>0,"Logged parent has actual off records",TimeSpan.FromSeconds(3));
            Check.Equal(1,logs.Count(l=>l==line),"A parent applied once is logged once, not once per sibling stat or later pulse");
        }
        Check.Equal(1L,await context.Database.LongAsync($"SELECT count FROM inventory WHERE charId={patient.CharId} AND slot=3"),"Logging does not spend another item");
        var low=await context.AddPlayerAsync(new CharacterSeed("RefusedSweep") {Level=1,Items=new[]{(3,39410u,1)}});
        await low.EnterWorldAsync();low.SendRpc("80 c6 03 00");await low.WaitForTextAsync("character level is too low");
        Check.That(!context.Server.DebugLog().Contains("from RefusedSweep"),"Refused use produces no sweep-removal line");
        Check.Equal(0,patient.World.Problems.Count+enemy.World.Problems.Count+low.World.Problems.Count,"Sweep logging wire protocol");
    }
}
