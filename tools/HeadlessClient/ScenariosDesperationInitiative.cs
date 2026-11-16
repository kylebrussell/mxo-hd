using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using hds;
namespace HeadlessClient;
public static partial class Scenarios
{
    private static async Task DesperationInitiative(ScenarioContext context)
    {
        CharacterSeed Seed(string name,int org,int offset)=>new(name){X=24699+offset,Y=495,Z=-1498,
            Level=50,Experience=(int)PlayerProgression.ExperienceForLevel(50),Organization=org,
            Health=5000,MaxHealth=10000,InnerStrength=2000,MaxInnerStrength=2000,
            OwnedLoadedAbilities=new[]{((ushort)1149,(ushort)50)}};
        var actor=await context.AddPlayerAsync(Seed("InitiativeActor",1,0));
        var target=await context.AddPlayerAsync(Seed("InitiativeTarget",2,100));
        var notes=new List<object>();bool passed=false;Exception? failure=null;
        try
        {
            foreach(var p in new[]{actor,target})
            {await p.EnterWorldAsync();p.SendChat("?pvpflag on");await p.WaitForViewUpdateAsync(2,OwnPvpFlagOn);}
            async Task<(int Base,int Bonus,int Total,int Hits)> Diagnostic()
            {
                actor.SkipReceived();actor.SendChat("?combatdepth initiative");
                var packet=await actor.WaitForTextAsync("Initiative OURS base=");
                var m=Regex.Match(Encoding.UTF8.GetString(packet.Bytes),@"base=(-?\d+) bonus=(\d+) total=(-?\d+) seeded=(\d+)");
                Check.That(m.Success,"Read-only paired tactic-roll diagnostic");
                return(int.Parse(m.Groups[1].Value),int.Parse(m.Groups[2].Value),int.Parse(m.Groups[3].Value),int.Parse(m.Groups[4].Value));
            }
            var baseline=await Diagnostic();Check.Equal(0,baseline.Bonus,"No initiative before the normal cast");
            actor.SkipReceived();Cast(actor,1149,2);
            var record=await actor.WaitForRpcAsync(ModPattern(1149,1044),TimeSpan.FromSeconds(8));
            Check.Equal(50,BitConverter.ToInt32(record.Bytes,13),"Half-health cast snapshots50 once");
            var active=await Diagnostic();Check.Equal(baseline.Base,active.Base,"Ordinary accuracy is unchanged");
            Check.Equal(50,active.Bonus,"Current capped snapshot is consumed directly once");
            Check.Equal(baseline.Total+50,active.Total,"Initiative is additive interlock attack-base points");
            Check.That(active.Hits>baseline.Hits,"Identical isolated draws benefit from the actual cast");
            var tv=await actor.WaitForViewNamedAsync(target.Handle);var av=await target.WaitForViewNamedAsync(actor.Handle);
            int log=context.Server.DebugLog().Length;actor.SkipReceived();target.SkipReceived();
            actor.SendRpc($"40 {HandleOf(tv)} 01 6b");
            await actor.WaitForRpcAsync($"46 {HandleOf(tv)} 01");
            var round=await actor.WaitForRpcAsync("45",TimeSpan.FromSeconds(10));
            Check.Equal(89,round.Bytes.Length,"Ordinary PvP round layout remains intact");
            await actor.WaitUntilAsync(()=>context.Server.DebugLog()[log..].Contains("[INITIATIVE OURS] InitiativeActor base="+active.Base+" bonus=50 total="+active.Total),
                "Normal live interlock uses the same actual initiative input",TimeSpan.FromSeconds(8));
            await actor.WaitForRpcAsync(ModPattern(1149,1044,true),TimeSpan.FromSeconds(25));
            var expired=await Diagnostic();Check.Equal(baseline,expired,"Natural expiry restores the exact diagnostic totals and isolated draws");
            actor.SkipReceived();target.SkipReceived();actor.SendRpc("44 00");target.SendRpc("44 00");
            await actor.WaitForRpcAsync($"46 {HandleOf(tv)} 00",TimeSpan.FromSeconds(20));
            await target.WaitForRpcAsync($"46 {HandleOf(av)} 00",TimeSpan.FromSeconds(20));
            Check.Equal(0,actor.World.Problems.Count+target.World.Problems.Count,"Both ordinary parsers clean");
            notes.Add(new{Baseline=baseline,Active=active,Expired=expired,Round=round.Hex});passed=true;
        }
        catch(Exception e){failure=e;throw;}
        finally
        {
            string file=Path.Combine(Path.GetTempPath(),"mxo-desperation-initiative-"+context.Database.Name+".json");
            await File.WriteAllTextAsync(file,JsonSerializer.Serialize(new{Passed=passed,Failure=failure?.ToString(),Notes=notes,
                Policy="OURS additive known initiative in interlock; retail authoritative caller unknown",RandomOverride=false,NativeProven=false,
                context.Database.Name,context.Server.SourceAssembly,ServerPid=context.Server.ProcessId,
                Sessions=new[]{actor,target}.Select(p=>new{p.Handle,p.CharId,Rpcs=p.World.RpcSnapshot().Select(r=>r.Hex),Problems=p.World.Problems}),
                Debug=context.Server.DebugLog(),Output=context.Server.FullOutput()},new JsonSerializerOptions{WriteIndented=true,IncludeFields=true}));
            Console.WriteLine("Desperation initiative proof: "+file);
        }
    }
}
