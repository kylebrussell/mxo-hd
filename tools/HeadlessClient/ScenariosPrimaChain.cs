using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using hds;
namespace HeadlessClient;
public static partial class Scenarios
{
    // Shared test-only driver. Every selected completion is earned over the protocol; SQL seeds prerequisites only.
    private static async Task RunPrimaChain(ScenarioContext context,string handle,byte org,int[] ids,int[] prerequisites,
        string filePattern,double startX=17000,double startZ=2300,bool requireStandardCourier=false,string? evidenceLabel=null,PrimaChainEvidenceOptions? evidenceOptions=null)
    {
        var p=await context.AddPlayerAsync(new CharacterSeed(handle){Level=50,Experience=evidenceOptions?.StartingExperience??0,X=startX,Z=startZ,Health=5000,MaxHealth=5000,
            InnerStrength=2000,MaxInnerStrength=2000,Organization=org});
        await context.Database.ExecuteAsync("CREATE TABLE IF NOT EXISTS char_missions (charId INT UNSIGNED NOT NULL,missionId INT NOT NULL,completions INT NOT NULL DEFAULT 1,PRIMARY KEY(charId,missionId))");
        Check.That(!ids.Intersect(prerequisites).Any(),"Selected missions are never prerequisite grants");
        if(prerequisites.Length>0)await context.Database.ExecuteAsync("INSERT IGNORE INTO char_missions(charId,missionId) VALUES "+string.Join(",",prerequisites.Select(id=>$"({p.CharId},{id})")));
        using var diagnostics=evidenceOptions==null?null:new PrimaChainEvidence(context,p,ids,evidenceOptions);
        Exception? scenarioFailure=null;
        try
        {
        await p.EnterWorldAsync();
        if(diagnostics!=null)await diagnostics.BaselineAsync();
        foreach(int id in ids)
        {
            var mission=Directory.GetFiles(Path.Combine(context.RepositoryRoot,"data","missions"),filePattern).Select(XElement.Load)
                .Single(m=>(int)m.Attribute("id")! == id);
            int logStart=context.Server.DebugLog().Length;int ends=p.Received("80 a2 00").Count;
            int listStart=p.World.RpcSnapshot().Count;
            p.SkipReceived();p.SendRpc($"80 94 05 00 {org:x2}");await p.WaitForRpcAsync("80 97 05 00");
            var entries=p.World.RpcSnapshot().Skip(listStart).Select(r=>r.Bytes).Where(b=>b.Length>17 && b[0]==0x80 && b[1]==0x96 && BitConverter.ToUInt16(b,2)==5).ToArray();
            // The list contains indices and titles, not definition IDs. Resolve the actual Critical entry,
            // then verify the selected definition from the accepted mission's authoritative staging record.
            int index=MissionContactChoice.CriticalIndex(entries,(string)mission.Attribute("title")!);
            if(requireStandardCourier)
                Check.That(entries.Any(b=>b[14]==0 && System.Text.Encoding.ASCII.GetString(b,17,b.Length-18)=="Courier"),"Standard Courier remains present beside the Critical entry");
            p.SendRpc($"80 98 06 00 {Le((ushort)index)} 01 02 00");await p.WaitForRpcAsync("80 99");
            p.SendRpc("80 9b 07 00 01");await p.WaitForRpcAsync("80 9e 00");
            Check.Equal((long)id,await context.Database.LongAsync($"SELECT JSON_EXTRACT(payload,'$.Mission') FROM char_active_missions WHERE charId={p.CharId}"),"Actual accepted definition ID matches selected Critical title");
            int phase=0;
            foreach(var area in mission.Elements("phase"))
            {
                int rpcStart=p.World.RpcSnapshot().Count,viewsStart=p.World.CreationSnapshot().Count,sectionsStart=p.World.ViewSnapshot().Count;
                var killed=new HashSet<(ushort,byte)>();
                bool Done(int n)=>p.World.RpcSnapshot().Skip(rpcStart).Any(r=>BytePattern.Parse($"80 a0 {n:x2} 06 00 01").IsMatch(r.Bytes));
                await EnterMissionArea(context,p);
                if(evidenceOptions?.ExactZionAfter1212==true)
                {
                    var next=await diagnostics!.CarryRelogAsync(id);
                    if(!ReferenceEquals(next,p)){p=next;rpcStart=0;viewsStart=0;sectionsStart=0;killed.Clear();}
                }
                if(diagnostics!=null)await diagnostics.PhaseAsync(id,phase);
                var goals=area.Elements("objective").Where(g=>(string)g.Attribute("type")! != "Protect").ToList();
                while(goals.Any(g=>!Done((int)g.Attribute("goal")!)))
                {
                    // Complete open optional goals first so an automatic final mandatory action cannot skip them.
                    var goal=goals.Where(g=>!Done((int)g.Attribute("goal")!) && ((int?)g.Attribute("after") is not int after || Done(after)))
                        .OrderBy(g=>(int?)g.Attribute("optional")==1?0:1).FirstOrDefault();
                    Check.That(goal!=null,$"Mission{id} phase{phase+1} has an eligible remaining goal");
                    string type=(string)goal!.Attribute("type")!;int number=(int)goal.Attribute("goal")!;
                    diagnostics?.Begin(id,phase,number,type);
                    Console.WriteLine($"  Prima{id} phase{phase+1} goal{number} {type}");
                    XElement Npc(string key)=>mission.Elements("npc").Single(n=>(string)n.Attribute("id")! == key);
                    switch(type)
                    {
                        case "Talk":case "Get":case "Give":case "Defeat":case "Loot":
                        {
                            string reference=(string)goal.Attribute("npc")!;
                            var npc=Npc(type=="Loot"?reference.Split(',')[0]:reference);
                            var view=evidenceOptions?.ExactZionAfter1212==true?await ZionAfter1212Npc(context,p,npc,viewsStart):await PrimaNpc(context,p,npc,viewsStart,(int?)npc.Attribute("at")==phase+1);
                            diagnostics?.Target(type,view);
                            bool hostile=view.Attributes.TryGetValue("IsEnemy",out var enemy) && enemy[0]!=0;
                            var key=(view.ViewId,view.SpawnId);
                            if(type=="Defeat" || type=="Loot" && !killed.Contains(key) &&
                                (!view.Attributes.TryGetValue("IsDead",out var dead) || dead[0]==0))
                            {
                                if(type=="Loot"&&evidenceOptions?.ExactZionAfter1212==true){diagnostics!.End();diagnostics.Begin(id,phase,number,"Defeat");}
                                if(evidenceOptions?.ExactZionAfter1212==true)await ZionAfter1212Defeat(p,view);else await DefeatMissionNpc(p,view);killed.Add(key);
                                if(type=="Loot"&&evidenceOptions?.ExactZionAfter1212==true){diagnostics!.End();diagnostics.Begin(id,phase,number,"Loot");}
                            }
                            if(type=="Loot")
                            {
                                // Select THIS corpse even when an earlier DefeatAll left another corpse window open.
                                p.SendRpc($"80 c7 {MissionNpcId(view)} 05 00");await p.WaitForRpcAsync("81 19");
                                p.SendRpc("81 1f");
                            }
                            else if(type!="Defeat" && !(type=="Talk" && hostile))
                            {
                                if(type is "Get" or "Give")
                                {
                                    string tracker=type=="Get"?"GiverActiveTracker":"TakerActiveTracker";
                                    Check.That(view.Attributes.TryGetValue(tracker,out var active) && active.Any(b=>b!=0),$"{type} is exposed by the actual NPC {tracker}");
                                }
                                string action=type=="Talk"?"09":type=="Get"?"0d":"0e";p.SendRpc($"80 c7 {MissionNpcId(view)} {action} 00");}
                            // Hostile Find/Talk is proximity, as the native client offers no Talk interaction on it.
                            break;
                        }
                        case "Search":case "Use":case "Put":
                        {
                            int objectId=(int)goal.Attribute("object")!;uint item=(uint?)goal.Attribute("item")??0;
                            var view=evidenceOptions?.ExactZionAfter1212==true?await ZionAfter1212Object(context,p,objectId,viewsStart,diagnostics!.Note):await ZionObject(context,p,objectId);diagnostics?.Target(type,view);
                            if(!MissionStaging.IsClickable(view.Goid))
                            {await p.WaitUntilAsync(()=>Done(number),"nonclickable mission object proximity",TimeSpan.FromSeconds(10));break;}
                            bool computer=MissionStaging.IsComputer(view.Goid);
                            string label=computer ? type=="Put"?"Put":type=="Use" || item==0?"Hack":"Take" : "Open";
                            byte use=ObjectStates.UseNumber(view.Goid,label) ?? throw new Exception($"No sourced {label} use for{view.Goid}");
                            if(computer)
                            {
                                uint CurrentState()
                                {
                                    uint state=BitConverter.ToUInt32(view.Attributes["CurrentState"]);
                                    byte[] prefix=WireFormat.FromHex($"{Le(view.ViewId)} 01 08");
                                    foreach(byte[] section in p.World.ViewSnapshot().Skip(sectionsStart))
                                        for(int at=BytePattern.IndexOf(section,prefix,0);at>=0;at=BytePattern.IndexOf(section,prefix,at+1))
                                            if(at+prefix.Length+4<=section.Length)state=BitConverter.ToUInt32(section,at+prefix.Length);
                                    return state;
                                }
                                await p.WaitUntilAsync(()=>ObjectStates.Offered(view.Goid,CurrentState()).Contains(use),
                                    $"actual enabled computer {label} use{use}",TimeSpan.FromSeconds(10));
                            }
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
                                if(evidenceOptions?.ExactZionAfter1212==true&&(await MissionCheckpoint(context,p)).Following.Contains(ushort.Parse(key)))continue;
                                if((int?)npc.Attribute("at")==phase+1)
                                {var recruit=evidenceOptions?.ExactZionAfter1212==true?await ZionAfter1212Npc(context,p,npc,viewsStart):await PrimaNpc(context,p,npc,viewsStart,true);diagnostics?.Target("Escort recruitment",recruit);await Task.Delay(800);}
                            }
                            string to=(string?)goal.Attribute("to")??"exit";
                            if(to=="exit"){if(evidenceOptions?.ExactZionAfter1212==true)await ZionAfter1212Exit(context,p);else await LeaveMissionArea(p);}
                            else if(to=="hardline")await ZionUpload(p);
                            else if(to.StartsWith("npc:")){var destination=evidenceOptions?.ExactZionAfter1212==true?await ZionAfter1212Npc(context,p,Npc(to[4..]),viewsStart):await PrimaNpc(context,p,Npc(to[4..]),viewsStart,true);diagnostics?.Target("Escort destination",destination);}
                            else if(to.StartsWith("object:")){if(evidenceOptions?.ExactZionAfter1212==true)await ZionAfter1212Object(context,p,int.Parse(to[7..]),viewsStart,diagnostics!.Note);else await ZionObject(context,p,int.Parse(to[7..]));}
                            else throw new Exception("Unsupported escort destination "+to);
                            break;
                        }
                        case "Upload":
                            if(evidenceOptions?.FreshUploadBooth==true)
                            {
                                // Opt-in218: historical creations can name a removed pre-mission booth.
                                // Capture BEFORE movement; already-in-range/no-new-creation fails closed.
                                int uploadCreations=p.World.CreationSnapshot().Count;
                                await MoveForMissionTest(p,17000,495,2200);
                                ViewCreation? uploadBooth=null;
                                await p.WaitUntilAsync(()=>(uploadBooth=FreshPrimaUploadBooth(p.World.CreationSnapshot(),uploadCreations))!=null,
                                    "newly received exact Mara booth after mission return (historical/currentness not inferred)",TimeSpan.FromSeconds(10));
                                diagnostics?.Target("Upload booth",uploadBooth!);
                                p.SkipReceived();
                                p.SendRpc($"80 c7 {MissionNpcId(uploadBooth!)} 04 00");
                                await p.WaitForRpcAsync("81 18");
                            }
                            else await ZionUpload(p);
                            break;
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
                                var target=Targets().Last();if(evidenceOptions?.ExactZionAfter1212==true)await ZionAfter1212Defeat(p,target);else await DefeatMissionNpc(p,target);killed.Add((target.ViewId,target.SpawnId));
                            }
                            break;
                        }
                        default:throw new Exception("Unsupported Prima goal "+type);
                    }
                    await p.WaitUntilAsync(()=>Done(number),$"Prima{id} phase{phase+1} actual goal{number} done",TimeSpan.FromSeconds(20));
                    diagnostics?.End();
                }
                string Log()=>context.Server.DebugLog()[logStart..];
                await p.WaitUntilAsync(()=>Log().Contains($"[MISSION] Phase {phase} goals done"),"phase goals complete",TimeSpan.FromSeconds(10));
                if(Log().Contains($"[MISSION] Phase {phase} goals done; waiting")){if(evidenceOptions?.ExactZionAfter1212==true)await ZionAfter1212Exit(context,p);else await LeaveMissionArea(p);}
                phase++;
                if(phase<mission.Elements("phase").Count())
                    await p.WaitUntilAsync(()=>Regex.Matches(Log(),@"\[MISSION\] Phase \d+ at a").Count>phase,"next phase staged",TimeSpan.FromSeconds(15));
                else await p.WaitUntilAsync(()=>p.Received("80 a2 00").Count>ends,"mission completed over wire",TimeSpan.FromSeconds(15));
            }
            Check.Equal(1L,await context.Database.LongAsync($"SELECT completions FROM char_missions WHERE charId={p.CharId} AND missionId={id}"),"Selected chain completion earned exactly once");
            Check.Equal(0,p.World.Problems.Count,"Prima chain protocol problems");
            if(diagnostics!=null)await diagnostics.AwardAsync(id);
        }
        Check.Equal((long)ids.Length,await context.Database.LongAsync($"SELECT COUNT(*) FROM char_missions WHERE charId={p.CharId} AND missionId IN ({string.Join(",",ids)})"),"Every selected completion earned, not granted");
        if(evidenceOptions?.ExactZionAfter1212==true)
        {
            int nextCursor=p.World.RpcSnapshot().Count;p.SkipReceived();p.SendRpc("80 94 05 00 01");await p.WaitForRpcAsync("80 97 05 00");
            MissionContactChoice.CriticalIndex(p.World.RpcSnapshot().Skip(nextCursor).Select(r=>r.Bytes),ZionAfter1212Fixture.NextTitle);
        }
        if(diagnostics!=null)await diagnostics.FinishAsync();
        }
        catch(Exception error) {scenarioFailure=error;throw;}
        finally
        {
            if(diagnostics!=null)await diagnostics.SaveAsync(evidenceLabel??handle,scenarioFailure,prerequisites);
            else if(evidenceLabel!=null)
            try
            {
                string stem=Path.Combine(Path.GetTempPath(),"mxo-"+evidenceLabel+"-"+context.Database.Name);
                File.WriteAllText(stem+"-debug.log",context.Server.DebugLog());
                var dll=context.Server.SourceAssembly;
                await File.WriteAllTextAsync(stem+".json",System.Text.Json.JsonSerializer.Serialize(new{
                    SourceAssembly=dll,SourceConfiguration=new DirectoryInfo(Path.GetDirectoryName(dll)!).Parent!.Name,
                    SourceSha256=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(dll))),
                    SelectedMissions=ids,Prerequisites=prerequisites,
                    CurrentPayloadHex=await context.Database.ExecuteAsync($"SELECT IFNULL(HEX(payload),'NULL') FROM char_active_missions WHERE charId={p.CharId}"),
                    CompletionReceipts=await context.Database.ExecuteAsync($"SELECT missionId,completions FROM char_missions WHERE charId={p.CharId} AND missionId IN ({string.Join(",",ids)}) ORDER BY missionId"),
                    Rpc=p.World.RpcSnapshot().Select(r=>Convert.ToHexString(r.Bytes)).ToArray(),
                    ReceivedViewLists=p.World.ViewSnapshot().Select(Convert.ToHexString).ToArray(),
                    ProtocolProblems=p.World.Problems.ToArray(),NativeReachabilityProven=false,NativeRenderingProven=false,
                    MovementPolicy="Existing test-only movement/weaken helpers; completion is protocol evidence, not wall/room reachability proof"
                },new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
                Console.WriteLine("  Required mission evidence "+stem+".json");
            }
            catch(Exception proofError)
            {
                if(scenarioFailure==null)throw; // A successful scenario cannot silently omit required proof.
                Console.Error.WriteLine("Required evidence capture also failed; preserving original scenario exception: "+proofError);
            }
        }
    }

    private static async Task<ViewCreation> PrimaNpc(ScenarioContext context,HeadlessPlayer p,XElement npc,int viewsStart,bool introducedThisPhase)
    {
        string name=(string)npc.Attribute("handle")!;
        await VisitMissionNpc(context,p,name);
        var match=Regex.Matches(context.Server.DebugLog(),@"\[MISSION\] Spawned (friendly|hostile) NPC "+Regex.Escape(name)+@" at ([^,\r\n]+),([^,\r\n]+),([^\r\n]+)").Last();
        bool friendly=match.Groups[1].Value=="friendly";
        double x=double.Parse(match.Groups[2].Value,CultureInfo.InvariantCulture),y=double.Parse(match.Groups[3].Value,CultureInfo.InvariantCulture),z=double.Parse(match.Groups[4].Value,CultureInfo.InvariantCulture);
        bool Matches(ViewCreation v)=>v.Name==name && v.Attributes.ContainsKey("TalkDefaultable")==friendly &&
            v.Attributes.TryGetValue("Position",out var pos) && Math.Abs(BitConverter.ToDouble(pos)-x)<1 && Math.Abs(BitConverter.ToDouble(pos,8)-y)<1 && Math.Abs(BitConverter.ToDouble(pos,16)-z)<1;
        List<ViewCreation> Candidates()=>p.World.CreationSnapshot().Skip(introducedThisPhase?viewsStart:0).Where(Matches).ToList();
        await p.WaitUntilAsync(()=>Candidates().Count>0,name+" current source-side view",TimeSpan.FromSeconds(15));
        return Candidates().Last();
    }
    private static ViewCreation? FreshPrimaUploadBooth(IReadOnlyList<ViewCreation> views,int cursor)
    {
        if(cursor<0 || cursor>views.Count)return null;
        return views.Skip(cursor).LastOrDefault(v=>v.Goid==592 && v.ViewId>2 &&
            v.Attributes.TryGetValue("Position",out var xyz) && xyz.Length==24 &&
            BitConverter.ToDouble(xyz,0)==17000 && BitConverter.ToDouble(xyz,8)==530 && BitConverter.ToDouble(xyz,16)==2200);
    }

}
