using System.Security.Cryptography;
using System.Text.Json;
using hds;
namespace HeadlessClient;
public static partial class Scenarios
{
    private static async Task AreaDisruptionWire(ScenarioContext context,bool hit)
    {
        CharacterSeed Seed(string n,int x,int y,int org)=>new(n){X=x,Y=y,Z=2300,Level=50,Organization=org,Health=5000,MaxHealth=5000,InnerStrength=2000,MaxInnerStrength=2000};
        var actor=await context.AddPlayerAsync(Seed("DisruptActor",17000,495,2));var second=await context.AddPlayerAsync(Seed("DisruptSelected",17000,495,2));
        var near=await context.AddPlayerAsync(Seed("DisruptNear",17200,495,1));var edge=await context.AddPlayerAsync(Seed("DisruptEdge",17800,495,1));
        var outside=await context.AddPlayerAsync(Seed("DisruptOutside",17801,495,1));var friendly=await context.AddPlayerAsync(Seed("DisruptFriendly",17100,495,2));var floor=await context.AddPlayerAsync(Seed("DisruptFloor",17100,695,1));
        var npcActor=await context.AddPlayerAsync(NearNpc("DisruptNpc") with{Organization=2,Health=5000,MaxHealth=5000,InnerStrength=2000,MaxInnerStrength=2000});
        var all=new[]{actor,second,near,edge,outside,friendly,floor,npcActor};bool passed=false;string phase="entry";string? failure=null;
        List<ViewCreation> Npcs(HeadlessPlayer p,double x,double y,double z)=>p.World.CreationSnapshot().Where(v=>v.Goid==599).GroupBy(v=>(v.ViewId,v.SpawnId)).Select(g=>g.Last()).Where(v=>v.Attributes.TryGetValue("IsEnemy",out var enemy)&&enemy[0]!=0&&(!v.Attributes.TryGetValue("IsDead",out var dead)||dead[0]==0)&&v.Attributes.TryGetValue("Position",out var pos)&&AbilityTargets.WithinArea(x,y,z,BitConverter.ToDouble(pos),BitConverter.ToDouble(pos,8),BitConverter.ToDouble(pos,16),8)).ToList();
        var npcInventory=Array.Empty<string>();
        try
        {
            foreach(var c in new[]{actor,second,npcActor}){await LoadAbilitiesAsync(context,c,AreaDisruptionFixture.Abilities);await DebuffRadiusFixture.EnterFlaggedAsync(c);await PrepareFreeCastViralFixture(context,c,38);}
            foreach(var c in new[]{near,edge,outside,friendly,floor})await DebuffRadiusFixture.EnterFlaggedAsync(c);
            foreach(var c in new[]{actor,second})foreach(var t in new[]{near,edge,outside,friendly,floor})await c.WaitForViewNamedAsync(t.Handle);
            Check.Equal(0,Npcs(actor,17000,495,2300).Count,"Actual player-fixture NPC inventory is empty inside8m");
            var far=await second.WaitForViewNamedAsync(outside.Handle);await npcActor.WaitForViewNamedAsync("WinterFrost");
            var npcs=Npcs(npcActor,NpcX+200,NpcY,NpcZ);Check.That(npcs.Count>0,"Separate actual NPC cluster inventory exists");npcInventory=npcs.Select(v=>$"{v.ViewId}/{v.SpawnId}:{v.Name}").ToArray();
            Console.WriteLine(" 38 actualNPC recipients: "+string.Join(",",npcInventory));
            string outcome=$"81 67 ?? ?? ?? ?? {(hit?"f1":"f2")} 00 00 24 01 26 00 00 00";
            string control=ModPattern(38,102);int start=context.Server.DebugLog().Length;
            async Task PlayerCast(HeadlessPlayer caster,ushort selected)
            {
                phase=caster.Handle;int logStart=context.Server.DebugLog().Length;caster.SkipReceived();var clock=System.Diagnostics.Stopwatch.StartNew();
                Cast(caster,38,selected);await caster.WaitForRpcAsync("80 ac 26 00");await caster.WaitForRpcAsync(outcome,TimeSpan.FromSeconds(9));
                Check.That(clock.Elapsed.TotalSeconds>=1.8,"Source2s cast is not instant");
                await near.WaitForRpcAsync(outcome,TimeSpan.FromSeconds(5));await edge.WaitForRpcAsync(outcome,TimeSpan.FromSeconds(5));
                if(hit){await near.WaitForRpcAsync(control);await edge.WaitForRpcAsync(control);}
                await Task.Delay(250);
                var lines=context.Server.DebugLog()[logStart..].Split('\n').Where(l=>l.Contains("[FREECAST-VIRAL] ability=38 source="+caster.Handle+" ")).ToArray();
                Check.Equal(2,lines.Length,"Exactly two legal PvP recipients, no ambient overlap");
                foreach(string name in new[]{near.Handle,edge.Handle})Check.Equal(1,lines.Count(l=>l.Contains("target="+name+" ")&&l.Contains("hit="+(hit?"True":"False")+" qa=True")),"One contest per legal recipient");
                Check.That(ViewUpdateValues(caster,2,"02 80 80 80 10").Contains((ushort)1986),"IS14 paid once");
                Check.That(caster.Received("80 bd 01 26 00 04 00 00 2c 42 01").Count>0,"Source43s reuse timer");
                int casts=caster.Received("80 ac 26 00").Count;caster.SkipReceived();Cast(caster,38,selected);await caster.WaitForRpcAsync("80 ba 26 00");Check.Equal(casts,caster.Received("80 ac 26 00").Count,"Reuse does not publish another cast");
            }
            await PlayerCast(actor,0);
            if(hit)
            {
                foreach(var c in new[]{near,edge})
                {var record=c.Received(control).Last();Check.Equal(3f,BitConverter.ToSingle(record.Bytes,18),"Source stun3s unchanged");await c.WaitForRpcAsync(ModPattern(38,102,true),TimeSpan.FromSeconds(6));await c.WaitForRpcAsync(ModPattern(734,681,true),TimeSpan.FromSeconds(12));}
            }
            near.SkipReceived();edge.SkipReceived();await PlayerCast(second,far.ViewId);
            phase="npc-cluster";Cast(npcActor,38,0);await npcActor.WaitForRpcAsync("80 ac 26 00");await npcActor.WaitForRpcAsync(outcome,TimeSpan.FromSeconds(9));await Task.Delay(500);
            string trace=context.Server.DebugLog()[start..];var npcLines=trace.Split('\n').Where(l=>l.Contains("[FREECAST-VIRAL] ability=38 source=DisruptNpc ")).ToArray();
            Check.Equal(npcs.Count,npcLines.Length,"Every actual legal NPC in8m receives one contest");
            foreach(var group in npcs.GroupBy(v=>v.Name))Check.Equal(group.Count(),npcLines.Count(l=>l.Contains("target="+group.Key+" ")&&l.Contains("hit="+(hit?"True":"False")+" qa=True")),"NPC duplicate names remain distinct recipients");
            Check.Equal(hit,trace.Contains("[ABILITY] 38 lands"),"Deflect never reaches NPC effect landing");
            foreach(var c in new[]{outside,friendly,floor,actor,second})Check.Equal(0,c.Received(control).Count,"Excluded recipient no38: "+c.Handle);
            foreach(var c in new[]{near,edge})Check.Equal(hit?2:0,c.Received(control).Count,"One source control per successful cast");
            Check.That(!trace.Split('\n').Any(l=>l.Contains("[ABILITY] 38 lands")&&!l.Contains("for 0")),"38 never gains generic direct damage");
            Check.Equal(0,context.ClientProblems().Count(),"38 wire parsers clean");passed=true;phase="complete";
        }
        catch(Exception ex){failure=ex.ToString();throw;}
        finally
        {
            string stem=Path.Combine(Path.GetTempPath(),"mxo-area-disruption-"+(hit?"hit":"deflect")+"-"+context.Database.Name);
            await File.WriteAllTextAsync(stem+".log",context.Server.FullOutput()+"\n"+context.Server.DebugLog());
            await File.WriteAllTextAsync(stem+".json",JsonSerializer.Serialize(new{Passed=passed,Phase=phase,Failure=failure,NpcInventory=npcInventory,SourceAssembly=context.Server.SourceAssembly,SourceConfiguration=Directory.GetParent(Path.GetDirectoryName(context.Server.SourceAssembly)!)!.Name,SourceAssemblySha256=Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(context.Server.SourceAssembly))),Actors=all.Select(p=>new{p.Handle,ReceivedRpcs=p.World.RpcSnapshot().Select(r=>WireFormat.ToHex(r.Bytes)).ToArray(),ReceivedViewLists=p.World.ViewSnapshot().Select(WireFormat.ToHex).ToArray()}),NativeUiProven=false},new JsonSerializerOptions{WriteIndented=true}));
            Console.WriteLine("38 evidence PRETEARDOWN: "+stem);
        }
    }
}
