using System.Security.Cryptography;
using System.Text.Json;
namespace HeadlessClient;
public static partial class Scenarios
{
    private static IReadOnlyDictionary<string,string> UiLagEnvironment(bool hit)=>new Dictionary<string,string>(FreeCastViralEnvironment(hit))
    {["MXO_PVP_MAX_SAFE_LEVEL"]="15",["MXO_DEBUFF_RADIUS_QA"]="1"};
    private static async Task UiLagAreaWire(ScenarioContext context,bool hit)
    {
        CharacterSeed Seed(string name,int x,int y,int org)=>new(name){X=x,Y=y,Z=2300,Level=50,Organization=org,Health=5000,MaxHealth=5000,InnerStrength=2000,MaxInnerStrength=2000};
        var actor=await context.AddPlayerAsync(Seed("UiLagActor",17000,495,2));
        var second=await context.AddPlayerAsync(Seed("UiLagSelected",17000,495,2));
        var near=await context.AddPlayerAsync(Seed("UiLagNear",17200,495,1));
        var outside=await context.AddPlayerAsync(Seed("UiLagOutside",17301,495,1));
        var friendly=await context.AddPlayerAsync(Seed("UiLagFriendly",17100,495,2));
        var floor=await context.AddPlayerAsync(Seed("UiLagFloor",17100,695,1));
        var npcActor=await context.AddPlayerAsync(NearNpc("UiLagNpc") with{Organization=2,Health=5000,MaxHealth=5000,InnerStrength=2000,MaxInnerStrength=2000});
        var all=new[]{actor,second,near,outside,friendly,floor,npcActor};bool passed=false;
        try
        {
            foreach(var c in new[]{actor,second,npcActor}){await LoadAbilitiesAsync(context,c,UiLagAreaFixture.Abilities);await DebuffRadiusFixture.EnterFlaggedAsync(c);await PrepareFreeCastViralFixture(context,c,97);}
            foreach(var c in new[]{near,outside,friendly,floor})await DebuffRadiusFixture.EnterFlaggedAsync(c);
            foreach(var c in new[]{actor,second})foreach(var target in new[]{near,outside,friendly,floor})await c.WaitForViewNamedAsync(target.Handle);
            var far=await second.WaitForViewNamedAsync(outside.Handle);
            var npc=await npcActor.WaitForViewNamedAsync("WinterFrost");
            int start=context.Server.DebugLog().Length;
            Cast(actor,97,0);await actor.WaitForRpcAsync("80 ac 61 00");
            string result=hit?"f1":"f2";
            string outcome=$"81 67 ?? ?? ?? ?? {result} 00 00 24 01 61 00 00 00";
            await actor.WaitForRpcAsync(outcome,TimeSpan.FromSeconds(9));await near.WaitForRpcAsync(outcome,TimeSpan.FromSeconds(9));
            if(hit)await near.WaitForRpcAsync(ModPattern(97,342),TimeSpan.FromSeconds(5));
            Cast(second,97,far.ViewId);await second.WaitForRpcAsync("80 ac 61 00");await second.WaitForRpcAsync(outcome,TimeSpan.FromSeconds(9));
            Cast(npcActor,97,0);await npcActor.WaitForRpcAsync("80 ac 61 00");await npcActor.WaitForRpcAsync(outcome,TimeSpan.FromSeconds(9));
            await Task.Delay(500);
            string trace=context.Server.DebugLog()[start..];
            foreach(var c in new[]{actor,second})
            {
                var lines=trace.Split('\n').Where(l=>l.Contains("[FREECAST-VIRAL] ability=97 source="+c.Handle+" ")).ToArray();
                Check.Equal(1,lines.Length,"Exactly one legal nearby PvP recipient for "+c.Handle);
                Check.That(lines[0].Contains("target=UiLagNear ")&&lines[0].Contains("hit="+(hit?"True":"False")+" qa=True"),"Normal owned97 uses strict scoped contest draws");
            }
            Check.That(trace.Split('\n').Any(l=>l.Contains("[FREECAST-VIRAL] ability=97 source=UiLagNpc target=WinterFrost ")&&l.Contains("hit="+(hit?"True":"False")+" qa=True")),"No-target cast reaches actual nearby NPC through the same contest");
            Check.Equal(hit,trace.Contains("[ABILITY] 97 lands"),"Deflection does not reach NPC timed/damage landing");
            foreach(var c in new[]{outside,friendly,floor,actor,second})Check.Equal(0,c.Received(ModPattern(97,342)).Count,"No97 debuff on excluded recipient "+c.Handle);
            foreach(var tuple in new[]{(Stat:1032,Value:-10),(Stat:183,Value:-17),(Stat:1011,Value:-12),(Stat:342,Value:-14)})
            {
                var records=near.Received(ModPattern(97,(ushort)tuple.Stat)).Where(r=>r.Bytes[17]==0).ToArray();
                Check.Equal(hit?2:0,records.Length,"One captured component per successful cast on near recipient");
                foreach(var r in records){Check.Equal(tuple.Value,BitConverter.ToInt32(r.Bytes,13),"Captured97 value unchanged");Check.Equal(27f,BitConverter.ToSingle(r.Bytes,18),"Captured97 duration unchanged");}
                if(hit)await near.WaitForRpcAsync(ModPattern(97,(ushort)tuple.Stat,true),TimeSpan.FromSeconds(34));
            }
            Check.Equal(0,context.ClientProblems().Count(),"All UiLag wire parsers remain clean");passed=true;
        }
        finally
        {
            string stem=Path.Combine(Path.GetTempPath(),"mxo-ui-lag-"+(hit?"hit":"deflect")+"-"+context.Database.Name);
            await File.WriteAllTextAsync(stem+"-runtime.log",context.Server.FullOutput()+"\n"+context.Server.DebugLog());
            await File.WriteAllTextAsync(stem+"-wire.json",JsonSerializer.Serialize(new{Passed=passed,SourceAssembly=context.Server.SourceAssembly,SourceConfiguration=Directory.GetParent(Path.GetDirectoryName(context.Server.SourceAssembly)!)!.Name,SourceAssemblySha256=Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(context.Server.SourceAssembly))),Actors=all.Select(p=>new{p.Handle,ReceivedRpcs=p.World.RpcSnapshot().Select(r=>WireFormat.ToHex(r.Bytes)).ToArray(),ReceivedViewLists=p.World.ViewSnapshot().Select(WireFormat.ToHex).ToArray()}),NativeUiProven=false},new JsonSerializerOptions{WriteIndented=true}));
            Console.WriteLine("UI Lag evidence saved before teardown: "+stem+"-wire.json");
        }
    }
}
