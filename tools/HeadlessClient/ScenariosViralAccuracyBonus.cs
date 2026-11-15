using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace HeadlessClient;
public static partial class Scenarios
{
    private static IReadOnlyDictionary<string,string> ViralAccuracyBonusEnvironment()=>new Dictionary<string,string>(FreeCastViralEnvironment(true))
    {
        ["MXO_FREECAST_VIRAL_QA_ATTACK15"]="17000",["MXO_FREECAST_VIRAL_QA_DEFENSE15"]="17000"
    };
    private static async Task ViralAccuracyBonus(ScenarioContext context)
    {
        var caster=await context.AddPlayerAsync(NearNpc("ViralBonus") with{Level=50,Experience=checked((int)hds.PlayerProgression.ExperienceForLevel(50)),
            Organization=1,Health=5000,MaxHealth=5000,InnerStrength=2000,MaxInnerStrength=2000,
            OwnedLoadedAbilities=ViralAccuracyBonusFixture.Abilities.Select(id=>(id,(ushort)50)).ToArray()});
        var pvp=await context.AddPlayerAsync(NearNpc("ViralBonusPvp",1000) with{Level=50,Experience=checked((int)hds.PlayerProgression.ExperienceForLevel(50)),
            Organization=1,Health=5000,MaxHealth=5000,InnerStrength=2000,MaxInnerStrength=2000,
            OwnedLoadedAbilities=ViralAccuracyBonusFixture.Abilities.Select(id=>(id,(ushort)50)).ToArray()});
        var enemy=await context.AddPlayerAsync(NearNpc("ViralBonusEnemy",1100) with{Level=50,Organization=2,Health=5000,MaxHealth=5000});
        HeadlessPlayer actor=caster; // Keep NPC retaliation/interlock separate from the legal PvP cast test.
        var observations=new List<object>();bool passed=false;
        try
        {
            await caster.EnterWorldAsync();await pvp.EnterWorldAsync();await enemy.EnterWorldAsync();
            async Task<(int Total,int Hits)> Diagnostic()
            {
                actor.SkipReceived();actor.SendChat("?combatdepth specialattack 33");
                var packet=await actor.WaitForTextAsync("Special attack 33 total ");
                var m=Regex.Match(Encoding.UTF8.GetString(packet.Bytes),@"total (-?\d+); seeded landed (\d+)");
                Check.That(m.Success,"Actual production total and isolated seeded diagnostic");
                return(int.Parse(m.Groups[1].Value),int.Parse(m.Groups[2].Value));
            }
            async Task Toggle(bool loaded)
            {
                actor.SkipReceived();actor.SendChat(loaded?"?qaload 32":"?qaunload 32");
                await actor.WaitForRpcAsync(loaded?"80 b2 20 00":"80 b3 20 00");
                await actor.WaitForRpcAsync(PassiveRecord(32,1147,10,!loaded));
            }
            async Task CastAt(ViewCreation target,string name,int expectedInput)
            {
                int log=context.Server.DebugLog().Length;actor.SkipReceived();
                Cast(actor,33,target.ViewId);await actor.WaitForRpcAsync("80 ac 21 00");
                await actor.WaitUntilAsync(()=>context.Server.DebugLog()[log..].Contains("[FREECAST-VIRAL] ability=33 source="+actor.Handle+" target="+name+" inputs="+expectedInput+"/0"),
                    "Normal free-cast consumes the expected accuracy input",TimeSpan.FromSeconds(8));
                var line=context.Server.DebugLog()[log..].Split('\n').Single(l=>l.Contains("[FREECAST-VIRAL] ability=33 source="+actor.Handle+" target="+name));
                observations.Add(new{Target=name,Input=expectedInput,Line=line});
                await actor.WaitForRpcAsync("80 bd 01 aa 02 00 00 00 80 40 00",TimeSpan.FromSeconds(10));
                await actor.WaitForRpcAsync("80 bd 01 21 00 04 00 00 c0 40 00",TimeSpan.FromSeconds(10));
            }
            await Toggle(false);var baseline=await Diagnostic();Check.Equal(0,baseline.Total,"Fixture has no other viral accuracy producer");
            var npc=await caster.WaitForViewNamedAsync("WinterFrost");await CastAt(npc,"WinterFrost",0);
            await Toggle(true);var loaded=await Diagnostic();Check.Equal(5,loaded.Total-baseline.Total,"Run Program50 contributes5 under declared server policy");
            Check.That(loaded.Hits>baseline.Hits,"Same isolated draws land more often with the loaded passive");
            await CastAt(npc,"WinterFrost",5);
            actor=pvp;Check.Equal(loaded.Total,(await Diagnostic()).Total,"Independent PvP actor has the same loaded accuracy");
            actor.SendChat("?pvpflag on");await actor.WaitForViewUpdateAsync(2,OwnPvpFlagOn);
            enemy.SendChat("?pvpflag on");await enemy.WaitForViewUpdateAsync(2,OwnPvpFlagOn);
            var victim=await actor.WaitForViewNamedAsync(enemy.Handle);await CastAt(victim,enemy.Handle,5);
            await Toggle(false);var unloaded=await Diagnostic();Check.Equal(baseline.Total,unloaded.Total,"Normal unload removes the contribution immediately");
            await CastAt(victim,enemy.Handle,0);
            Check.Equal(0,caster.World.Problems.Count+pvp.World.Problems.Count+enemy.World.Problems.Count,"Normal cast, passive and contest packet framing");
            observations.Add(new{Baseline=baseline,Loaded=loaded,Unloaded=unloaded});passed=true;
        }
        finally
        {
            string file=Path.Combine(Path.GetTempPath(),"mxo-viral-accuracy-bonus-"+context.Database.Name+".json");
            await File.WriteAllTextAsync(file,JsonSerializer.Serialize(new{Passed=passed,Policy="OURS level*1147percent/100",context.Database.Name,
                context.Server.SourceAssembly,Observations=observations,Problems=new[]{caster,pvp,enemy}.SelectMany(p=>p.World.Problems),
                Actors=new[]{caster,pvp,enemy}.Select(p=>new{p.Handle,Rpc=p.World.RpcSnapshot().Select(r=>Convert.ToHexString(r.Bytes))}),
                Debug=context.Server.DebugLog(),NativeProven=false},new JsonSerializerOptions{WriteIndented=true,IncludeFields=true}));
            Console.WriteLine("Viral accuracy evidence: "+file);
        }
    }
}
