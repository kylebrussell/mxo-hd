using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using hds;
namespace HeadlessClient;
public static partial class Scenarios
{
    // Shared test-only driver. Every selected completion is earned over the protocol; SQL seeds prerequisites only.
    private static async Task RunPrimaChain(ScenarioContext context,string handle,byte org,int[] ids,int[] prerequisites,
        string filePattern,double startX=17000,double startZ=2300)
    {
        var p=await context.AddPlayerAsync(new CharacterSeed(handle){Level=50,X=startX,Z=startZ,Health=5000,MaxHealth=5000,
            InnerStrength=2000,MaxInnerStrength=2000,Organization=org});
        await context.Database.ExecuteAsync("CREATE TABLE IF NOT EXISTS char_missions (charId INT UNSIGNED NOT NULL,missionId INT NOT NULL,completions INT NOT NULL DEFAULT 1,PRIMARY KEY(charId,missionId))");
        Check.That(!ids.Intersect(prerequisites).Any(),"Selected missions are never prerequisite grants");
        if(prerequisites.Length>0)await context.Database.ExecuteAsync("INSERT IGNORE INTO char_missions(charId,missionId) VALUES "+string.Join(",",prerequisites.Select(id=>$"({p.CharId},{id})")));
        await p.EnterWorldAsync();
        foreach(int id in ids)
        {
            var mission=Directory.GetFiles(Path.Combine(context.RepositoryRoot,"data","missions"),filePattern).Select(XElement.Load)
                .Single(m=>(int)m.Attribute("id")! == id);
            int logStart=context.Server.DebugLog().Length;int ends=p.Received("80 a2 00").Count;
            p.SkipReceived();p.SendRpc($"80 94 05 00 {org:x2}");await p.WaitForTextAsync((string)mission.Attribute("title")!);
            await p.WaitForRpcAsync("80 97 05 00");p.SendRpc("80 98 06 00 00 00 01 02 00");await p.WaitForRpcAsync("80 99");
            p.SendRpc("80 9b 07 00 01");await p.WaitForRpcAsync("80 9e 00");
            int phase=0;
            foreach(var area in mission.Elements("phase"))
            {
                int rpcStart=p.World.RpcSnapshot().Count,viewsStart=p.World.CreationSnapshot().Count;
                var killed=new HashSet<(ushort,byte)>();
                bool Done(int n)=>p.World.RpcSnapshot().Skip(rpcStart).Any(r=>BytePattern.Parse($"80 a0 {n:x2} 06 00 01").IsMatch(r.Bytes));
                await EnterMissionArea(context,p);
                var goals=area.Elements("objective").Where(g=>(string)g.Attribute("type")! != "Protect").ToList();
                while(goals.Any(g=>!Done((int)g.Attribute("goal")!)))
                {
                    // Complete open optional goals first so an automatic final mandatory action cannot skip them.
                    var goal=goals.Where(g=>!Done((int)g.Attribute("goal")!) && ((int?)g.Attribute("after") is not int after || Done(after)))
                        .OrderBy(g=>(int?)g.Attribute("optional")==1?0:1).FirstOrDefault();
                    Check.That(goal!=null,$"Mission{id} phase{phase+1} has an eligible remaining goal");
                    string type=(string)goal!.Attribute("type")!;int number=(int)goal.Attribute("goal")!;
                    Console.WriteLine($"  Prima{id} phase{phase+1} goal{number} {type}");
                    XElement Npc(string key)=>mission.Elements("npc").Single(n=>(string)n.Attribute("id")! == key);
                    switch(type)
                    {
                        case "Talk":case "Get":case "Give":case "Defeat":case "Loot":
                        {
                            var npc=Npc((string)goal.Attribute("npc")!);
                            var view=await PrimaNpc(context,p,npc,viewsStart,(int?)npc.Attribute("at")==phase+1);
                            bool hostile=view.Attributes.TryGetValue("IsEnemy",out var enemy) && enemy[0]!=0;
                            var key=(view.ViewId,view.SpawnId);
                            if(type=="Defeat" || type=="Loot" && !killed.Contains(key) &&
                                (!view.Attributes.TryGetValue("IsDead",out var dead) || dead[0]==0))
                            {await DefeatMissionNpc(p,view);killed.Add(key);}
                            if(type=="Loot")
                            {
                                if(view.Attributes.TryGetValue("IsDead",out var corpse) && corpse[0]!=0)
                                {p.SendRpc($"80 c7 {MissionNpcId(view)} 05 00");await p.WaitForRpcAsync("81 19");}
                                p.SendRpc("81 1f");
                            }
                            else if(type!="Defeat" && !(type=="Talk" && hostile))
                            {string action=type=="Talk"?"09":type=="Get"?"0d":"0e";p.SendRpc($"80 c7 {MissionNpcId(view)} {action} 00");}
                            // Hostile Find/Talk is proximity, as the native client offers no Talk interaction on it.
                            break;
                        }
                        case "Search":case "Use":case "Put":
                        {
                            int objectId=(int)goal.Attribute("object")!;uint item=(uint?)goal.Attribute("item")??0;
                            var view=await ZionObject(context,p,objectId);
                            if(!MissionStaging.IsClickable(view.Goid))
                            {await p.WaitUntilAsync(()=>Done(number),"nonclickable mission object proximity",TimeSpan.FromSeconds(10));break;}
                            bool computer=MissionStaging.IsComputer(view.Goid);
                            string label=computer ? type=="Put"?"Put":type=="Use" || item==0?"Hack":"Take" : "Open";
                            byte use=ObjectStates.UseNumber(view.Goid,label) ?? throw new Exception($"No sourced {label} use for{view.Goid}");
                            int loot=p.Received("81 19").Count;
                            Console.WriteLine($"    object{objectId} goid{view.Goid} {label} use{use}");
                            p.SendRpc($"80 c7 {MissionNpcId(view)} {use:x2} 00");
                            await p.WaitUntilAsync(()=>Done(number) || p.Received("81 19").Count>loot,"object action or container window",TimeSpan.FromSeconds(10));
                            if(!Done(number))
                            {
                                if(type=="Put")
                                {
                                    long slot=await context.Database.LongAsync($"SELECT slot FROM inventory WHERE charId={p.CharId} AND goid={item} LIMIT 1");
                                    Check.That(slot>0 && slot<97,"Put uses an actual owned mission item bag slot");
                                    p.SendRpc($"81 21 {slot:x2}");
                                }
                                else p.SendRpc("81 1f");
                            }
                            break;
                        }
                        case "Escort":
                        {
                            foreach(string key in ((string)goal.Attribute("npc")!).Split(','))
                            {
                                var npc=Npc(key);
                                if((int?)npc.Attribute("at")==phase+1)
                                {await PrimaNpc(context,p,npc,viewsStart,true);await Task.Delay(800);}
                            }
                            string to=(string?)goal.Attribute("to")??"exit";
                            if(to=="exit")await LeaveMissionArea(p);
                            else if(to=="hardline")await ZionUpload(p);
                            else if(to.StartsWith("npc:"))await PrimaNpc(context,p,Npc(to[4..]),viewsStart,true);
                            else if(to.StartsWith("object:"))await ZionObject(context,p,int.Parse(to[7..]));
                            else throw new Exception("Unsupported escort destination "+to);
                            break;
                        }
                        case "Upload":await ZionUpload(p);break;
                        case "DefeatAll":
                        {
                            var names=new HashSet<string>(new[]{"Machine Program","Zion Soldier","Exile Soldier"});
                            foreach(var npc in mission.Elements("npc"))names.Add((string)npc.Attribute("handle")!);
                            List<ViewCreation> Targets()=>p.World.CreationSnapshot().Skip(viewsStart)
                                .Where(v=>v.Goid==599 && names.Contains(v.Name??"") && !killed.Contains((v.ViewId,v.SpawnId)) &&
                                    v.Attributes.TryGetValue("IsEnemy",out var e) && e[0]!=0 &&
                                    (!v.Attributes.TryGetValue("IsDead",out var d) || d[0]==0)).ToList();
                            for(int attempts=0;!Done(number) && attempts<100;attempts++)
                            {
                                await p.WaitUntilAsync(()=>Done(number)||Targets().Count>0,"next current-phase hostile or wave completion",TimeSpan.FromSeconds(30));
                                if(Done(number))break;
                                var target=Targets().Last();await DefeatMissionNpc(p,target);killed.Add((target.ViewId,target.SpawnId));
                            }
                            break;
                        }
                        default:throw new Exception("Unsupported Prima goal "+type);
                    }
                    await p.WaitUntilAsync(()=>Done(number),$"Prima{id} phase{phase+1} actual goal{number} done",TimeSpan.FromSeconds(20));
                }
                string Log()=>context.Server.DebugLog()[logStart..];
                await p.WaitUntilAsync(()=>Log().Contains($"[MISSION] Phase {phase} goals done"),"phase goals complete",TimeSpan.FromSeconds(10));
                if(Log().Contains($"[MISSION] Phase {phase} goals done; waiting"))await LeaveMissionArea(p);
                phase++;
                if(phase<mission.Elements("phase").Count())
                    await p.WaitUntilAsync(()=>Regex.Matches(Log(),@"\[MISSION\] Phase \d+ at a").Count>phase,"next phase staged",TimeSpan.FromSeconds(15));
                else await p.WaitUntilAsync(()=>p.Received("80 a2 00").Count>ends,"mission completed over wire",TimeSpan.FromSeconds(15));
            }
            Check.Equal(1L,await context.Database.LongAsync($"SELECT completions FROM char_missions WHERE charId={p.CharId} AND missionId={id}"),"Selected chain completion earned exactly once");
            Check.Equal(0,p.World.Problems.Count,"Prima chain protocol problems");
        }
        Check.Equal((long)ids.Length,await context.Database.LongAsync($"SELECT COUNT(*) FROM char_missions WHERE charId={p.CharId} AND missionId IN ({string.Join(",",ids)})"),"Every selected completion earned, not granted");
    }

    private static async Task<ViewCreation> PrimaNpc(ScenarioContext context,HeadlessPlayer p,XElement npc,int viewsStart,bool introducedThisPhase)
    {
        string name=(string)npc.Attribute("handle")!;
        await VisitMissionNpc(context,p,name);
        var match=Regex.Matches(context.Server.DebugLog(),@"\[MISSION\] Spawned (friendly|hostile) NPC "+Regex.Escape(name)+@" at ([^,\r\n]+),([^,\r\n]+),([^\r\n]+)").Last();
        bool friendly=match.Groups[1].Value=="friendly";
        double x=double.Parse(match.Groups[2].Value,CultureInfo.InvariantCulture),z=double.Parse(match.Groups[4].Value,CultureInfo.InvariantCulture);
        bool Matches(ViewCreation v)=>v.Name==name && v.Attributes.ContainsKey("TalkDefaultable")==friendly &&
            v.Attributes.TryGetValue("Position",out var pos) && Math.Abs(BitConverter.ToDouble(pos)-x)<1 && Math.Abs(BitConverter.ToDouble(pos,16)-z)<1;
        List<ViewCreation> Candidates()=>p.World.CreationSnapshot().Skip(introducedThisPhase?viewsStart:0).Where(Matches).ToList();
        await p.WaitUntilAsync(()=>Candidates().Count>0,name+" current source-side view",TimeSpan.FromSeconds(15));
        return Candidates().Last();
    }
}
