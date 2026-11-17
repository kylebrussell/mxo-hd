using System.Diagnostics;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using hds;

namespace HeadlessClient;

public static partial class Scenarios
{
    private sealed record SweepMission(string File, XElement Xml)
    {
        public int Id => (int)Xml.Attribute("id")!;
        public string Title => (string?)Xml.Attribute("title") ?? "";
        public byte Org => checked((byte)((int?)Xml.Attribute("org") ?? 1));
        public string Contact => (string?)Xml.Attribute("contact") ?? "Tyndall";
    }
    private sealed record SweepRow(int Mission, string Result, string FailureClass, string Detail, double Seconds);
    private sealed class SweepStep { public string Class = "other"; public string Detail = "login"; }
    private static readonly ConcurrentDictionary<(string Root,ushort District),MissionSite[]> sweepSites=new();

    public static async Task<int> RunMissionSweep(string root, int parallel, string output, string? filter, bool listOnly)
    {
        var catalog = Directory.GetFiles(Path.Combine(root,"data","missions"),"*.xml")
            .Select(f=>new SweepMission(f,XElement.Load(f))).OrderBy(m=>m.Id).ToArray();
        if(catalog.Select(m=>m.Id).Distinct().Count()!=catalog.Length) throw new InvalidDataException("Duplicate mission IDs");
        var terms=(filter??"").Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);
        var selected=catalog.Where(m=>filter==null||terms.Any(t=>int.TryParse(t,out int id)?m.Id==id:m.Title.Contains(t,StringComparison.OrdinalIgnoreCase))).ToArray();
        if(selected.Length==0)throw new ArgumentException("No catalog missions match the filter");
        if(listOnly){foreach(var m in selected)Console.WriteLine($"{m.Id} {m.Title}");return 0;}
        Directory.CreateDirectory(output);
        MissionContacts.Load(Path.Combine(root,"data","mission_contacts.csv"));
        MissionContactNpcs.Load(Path.Combine(root,"data","mission_contact_npcs.csv"));
        SectorProperties.Load(Path.Combine(root,"data","sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root,"data","navmesh"));
        var rows=new List<SweepRow>(); var gate=new object(); using var slots=new SemaphoreSlim(parallel);
        void Write()
        {
            string Csv(string s)=>"\""+s.Replace("\"","\"\"").Replace("\r"," ").Replace("\n"," ")+"\"";
            File.WriteAllLines(Path.Combine(output,"mission-sweep.csv"),new[]{"mission id,pass/fail,failure class,detail,seconds"}.Concat(rows.OrderBy(r=>r.Mission).Select(r=>$"{r.Mission},{r.Result},{Csv(r.FailureClass)},{Csv(r.Detail)},{r.Seconds.ToString("0.0",CultureInfo.InvariantCulture)}")));
            File.WriteAllText(Path.Combine(output,"mission-sweep.json"),JsonSerializer.Serialize(rows.OrderBy(r=>r.Mission),new JsonSerializerOptions{WriteIndented=true}));
            var counts=rows.GroupBy(r=>r.Result=="PASS"?"PASS":r.FailureClass).OrderBy(g=>g.Key).Select(g=>$"| {g.Key} | {g.Count()} |");
            File.WriteAllLines(Path.Combine(output,"counts.md"),new[]{$"Completed {rows.Count}/{selected.Length} missions.","| Result / failure class | Count |","|---|---:|"}.Concat(counts));
        }
        Write();
        var tasks=selected.Select(async mission=>
        {
            await slots.WaitAsync(); var watch=Stopwatch.StartNew(); var step=new SweepStep(); string? failure=null;
            ScenarioContext? context=null;
            try
            {
                var env=new Dictionary<string,string>(StillNpcs){["MXO_DEV_COMMANDS"]="1",["MXO_MISSION_SITE_QA_SEED"]=mission.Id.ToString(),["MXO_PACKET_LOG"]="0"};
                context=await ScenarioContext.StartAsync(root,"sweep_"+mission.Id,env);
                double holdSeconds=mission.Xml.Descendants("objective").Where(g=>(string?)g.Attribute("type")=="Survive")
                    .Sum(g=>(double?)g.Attribute("seconds")??0);
                double arrivalSeconds=mission.Xml.Descendants().Attributes("spawnDelay").Select(a=>(double)a).DefaultIfEmpty(0).Max();
                await SweepOne(context,mission,catalog,step).WaitAsync(TimeSpan.FromSeconds(240+holdSeconds+arrivalSeconds));
                var problems=context.ClientProblems().Concat(context.ServerProblems()).ToArray();
                if(problems.Length>0)throw new ScenarioFailure(string.Join("; ",problems));
            }
            catch(Exception ex)
            {
                failure=step.Detail+": "+ex.Message.Split(" Received ")[0];
                if(context!=null)
                {
                    string debug=context.Server.DebugLog();
                    string? refusal=debug.Split('\n').LastOrDefault(l=>l.Contains("refused")||l.Contains("admission deferred"));
                    if(refusal!=null){failure+="; "+refusal.Trim();if(refusal.Contains("NPC"))step.Class="NPC role placement";else if(refusal.Contains("Box")||refusal.Contains("street"))step.Class="room/NAV placement";}
                    await File.WriteAllTextAsync(Path.Combine(output,mission.Id+".log"),failure+"\n"+debug);
                    await File.WriteAllTextAsync(Path.Combine(output,mission.Id+"-state.json"),
                        await context.Database.ScalarAsync("SELECT IFNULL(payload,'null') FROM char_active_missions ORDER BY charId LIMIT 1"));
                }
            }
            finally
            {
                if(context!=null){await context.DisposeAsync();Directory.Delete(context.Server.WorkingDirectory,true);}
                var row=new SweepRow(mission.Id,failure==null?"PASS":"FAIL",failure==null?"":step.Class,failure??"All required objectives, completion summary and receipt",watch.Elapsed.TotalSeconds);
                lock(gate){rows.Add(row);Write();Console.WriteLine($"[{rows.Count}/{selected.Length}] {row.Result} {mission.Id} {row.FailureClass} {row.Detail}");}
                slots.Release();
            }
        }).ToArray();
        await Task.WhenAll(tasks);
        Console.WriteLine(File.ReadAllText(Path.Combine(output,"counts.md")));
        return rows.Any(r=>r.Result=="FAIL")?1:0;
    }

    private static async Task SweepOne(ScenarioContext c,SweepMission m,SweepMission[] catalog,SweepStep step)
    {
        void Stage(string kind,string detail){step.Class=kind;step.Detail=detail;}
        var items=new List<(int,uint,int)>(); uint entry=(uint?)m.Xml.Attribute("entryItem")??0;
        if(entry!=0)items.Add((1,entry,0));
        var contactSpot=MissionContactNpcs.All.FirstOrDefault(x=>x.Contact?.name==m.Contact);
        var p=await c.AddPlayerAsync(PrimaActorSeed("Sweep"+m.Id,m.Org==0?(byte)1:m.Org,17000,2300,50) with
        {CompletedMissions=catalog.Where(x=>x.Id!=m.Id).Select(x=>x.Id).ToArray(),Items=items,
         DistrictId=contactSpot?.metrId??((int?)m.Xml.Attribute("district")??1),District=contactSpot?.metrId==2?"dt":(string?)m.Xml.Attribute("districtName")??"slums",
         MerovingianReputation=(int?)m.Xml.Attribute("minReputation")??0,
         OwnedLoadedAbilities=new(ushort,ushort)[]{(2,50),(17,50),(45,50),(1006,50),(77,50)}});
        await p.EnterWorldAsync();
        if(entry!=0){Stage("other","ticket acceptance");p.SendRpc("59 01");}
        else
        {
        int list=p.World.RpcSnapshot().Count;
        if(m.Contact is "Tyndall" or "Agent Gray" or "Flood")
        {p.SendRpc($"80 94 05 00 {(m.Org==0?1:m.Org):x2}");await p.WaitForRpcAsync("80 97 05 00");}
        else
        {
            Stage("other","contact "+m.Contact);Check.That(contactSpot!=null,"Known neighborhood contact");p.SendChat("?contact "+contactSpot!.contactId);
            ViewCreation? contact=null;
            await p.WaitUntilAsync(()=>(contact=p.ViewsOfGoid(6520).LastOrDefault(v=>v.Attributes.TryGetValue("ContactID",out var id)&&id.Length==4&&BitConverter.ToUInt32(id)==contactSpot.contactId))!=null,"neighborhood contact",TimeSpan.FromSeconds(15));
            p.SendRpc($"80 c7 {MissionNpcId(contact!)} 02 00");await p.WaitForRpcAsync("80 97");
        }
        var entries=p.World.RpcSnapshot().Skip(list).Select(r=>r.Bytes).Where(b=>b.Length>17&&b[0]==0x80&&b[1]==0x96).ToArray();
        var chosen=entries.SingleOrDefault(b=>Encoding.ASCII.GetString(b,17,b.Length-18)==m.Title)??throw new ScenarioFailure("Mission absent from contact list");
        int index=BitConverter.ToUInt16(chosen,4);
        p.SendRpc($"80 98 06 00 {Le((ushort)index)} 01 02 00");await p.WaitForRpcAsync("80 99");
        Stage("room/NAV placement","accept");p.SendRpc("80 9b 07 00 01");await p.WaitForRpcAsync("80 9e 00",TimeSpan.FromSeconds(20));
        }
        if(entry!=0)await p.WaitForRpcAsync("80 9e 00",TimeSpan.FromSeconds(20));
        var accepted=await MervFollowupCheckpoint(c,p);Check.Equal(m.Id,accepted.Mission,"Accepted mission ID");
        var document=new System.Xml.XmlDocument();document.LoadXml(m.Xml.ToString());var authored=MissionXml.Parse(document.DocumentElement!);
        var notes=new List<object>();int summaryCursor=p.World.RpcSnapshot().Count;
        for(int phase=0;phase<authored.phases.Count;phase++)
        {
            var area=m.Xml.Elements("phase").ElementAt(phase);int cursor=p.World.RpcSnapshot().Count;
            var current=await MervFollowupCheckpoint(c,p);
            if(current.Waypoints.Length>0&&area.Attribute("site")!=null)
            {Stage("lobby car",$"phase {phase+1} entry");await SweepRide(c,p,current,area,true,notes);}
            var completed=new HashSet<int>();
            async Task<bool> Done(int n)
            {
                if(completed.Contains(n))return true;
                var s=await MervFollowupCurrent(c,p);
                bool ack=p.World.RpcSnapshot().Skip(cursor).Any(r=>BytePattern.Parse($"80 a0 {n:x2} 06 00 01").IsMatch(r.Bytes));
                if(ack&&(s==null||s.Phase>phase||s.Done.Contains(n))){completed.Add(n);return true;}
                return false;
            }
            var goals=area.Elements("objective").Where(g=>(string?)g.Attribute("type")!="Protect").ToList();
            while(true)
            {
                XElement? goal=null;
                foreach(var g in goals.OrderByDescending(g=>(int?)g.Attribute("optional")==1))
                    if(!await Done((int)g.Attribute("goal")!)&&((int?)g.Attribute("after") is not int prev||await Done(prev))){goal=g;break;}
                if(goal==null)break;
                int n=(int)goal.Attribute("goal")!;string type=(string)goal.Attribute("type")!;
                Stage("other",$"phase {phase+1} goal {n} {type}");
                switch(type)
                {
                    case "Talk":case "Get":case "Give":case "Defeat":case "Loot":
                    {
                        Stage("NPC role placement",step.Detail);string key=((string)goal.Attribute("npc")!).Split(',')[0];
                        var v=await SweepNpc(c,p,ushort.Parse(key));
                        if(type is "Defeat" or "Loot")
                        {if(!v.Attributes.TryGetValue("IsDead",out var dead)||dead[0]==0)await DefeatMissionNpc(p,v);}
                        if(type=="Loot"){p.SkipReceived();p.SendRpc($"80 c7 {MissionNpcId(v)} 05 00");await p.WaitForRpcAsync("81 19");p.SendRpc("81 1f");}
                        else if(type!="Defeat"&&!(type=="Talk"&&v.Attributes.TryGetValue("IsEnemy",out var e)&&e[0]!=0))
                            p.SendRpc($"80 c7 {MissionNpcId(v)} {(type=="Talk"?9:type=="Get"?13:14):x2} 00");
                        break;
                    }
                    case "Search":case "Use":case "Put":
                    {
                        Stage("prop placement",step.Detail);int id=(int)goal.Attribute("object")!;uint item=(uint?)goal.Attribute("item")??0;
                        var v=await MervFollowupObject(c,p,id,0,notes);
                        if(!MissionStaging.IsClickable(v.Goid))break;
                        string label=MissionStaging.IsComputer(v.Goid)?type=="Put"?"Put":type=="Use"||item==0?"Hack":"Take":"Open";
                        byte use=ObjectStates.UseNumber(v.Goid,label)??throw new ScenarioFailure("Missing "+label+" use");
                        int before=p.World.RpcSnapshot().Count;p.SendRpc($"80 c7 {MissionNpcId(v)} {use:x2} 00");
                        await p.WaitUntilAsync(()=>Done(n).GetAwaiter().GetResult()||p.World.RpcSnapshot().Skip(before).Any(r=>r.Bytes.AsSpan().StartsWith(new byte[]{0x81,0x19})),"object result",TimeSpan.FromSeconds(10));
                        if(!await Done(n))
                        {
                            if(type=="Put"){long slot=await c.Database.LongAsync($"SELECT slot FROM inventory WHERE charId={p.CharId} AND goid={item} LIMIT 1");Check.That(slot>0,"Put item owned");p.SendRpc($"81 21 {slot:x2}");}
                            else p.SendRpc("81 1f");
                        }
                        break;
                    }
                    case "Escort":
                    {
                        foreach(string key in ((string)goal.Attribute("npc")!).Split(','))
                        {
                            ushort follower=ushort.Parse(key);await SweepNpc(c,p,follower);
                            await p.WaitUntilAsync(()=>
                            {
                                var s=MervFollowupCurrent(c,p).GetAwaiter().GetResult();
                                return Done(n).GetAwaiter().GetResult()||s==null||s.Phase!=phase||s.Following.Contains(follower);
                            },"escort starts following",TimeSpan.FromSeconds(6));
                            if(await Done(n))break;
                        }
                        if(await Done(n))break;
                        string to=(string?)goal.Attribute("to")??"exit";
                        if(to=="exit"){Stage("elevator",step.Detail);await SweepRide(c,p,await MervFollowupCheckpoint(c,p),area,false,notes);}
                        else if(to=="hardline")await ZionUpload(p);
                        else if(to.StartsWith("npc:"))await SweepNpc(c,p,ushort.Parse(to[4..]));
                        else if(to.StartsWith("object:"))await MervFollowupObject(c,p,int.Parse(to[7..]),0,notes);
                        else throw new ScenarioFailure("Unsupported escort destination "+to);
                        break;
                    }
                    case "Upload":await ZionUpload(p);break;
                    case "Survive":await Task.Delay(TimeSpan.FromSeconds((int?)goal.Attribute("seconds")??30));break;
                    case "DefeatAll":
                    {
                        while(!await Done(n))
                        {
                            var s=await MervFollowupCheckpoint(c,p);var target=s.Mobs.LastOrDefault(x=>!x.Friendly&&!x.Dead&&x.Health>0);
                            if(target==null){await Task.Delay(500);continue;}
                            await DefeatMissionNpc(p,await SweepNpc(c,p,target.Npc,target.Identity));
                        }
                        break;
                    }
                    default:throw new ScenarioFailure("Unsupported objective "+type);
                }
                await p.WaitUntilAsync(()=>Done(n).GetAwaiter().GetResult(),"objective acknowledgement and saved progress",TimeSpan.FromSeconds(12));
            }
            current=await MervFollowupCurrent(c,p);
            if(current!=null&&current.Phase==phase&&current.Flags.GetValueOrDefault("waitingForExit"))
            {Stage("elevator",$"phase {phase+1} departure");await SweepRide(c,p,current,area,false,notes);}
            await p.WaitUntilAsync(()=>{var s=MervFollowupCurrent(c,p).GetAwaiter().GetResult();return s==null||s.Phase==phase+1;},"next phase or completion",TimeSpan.FromSeconds(15));
        }
        Stage("payload/receipt","completion");
        await p.WaitUntilAsync(()=>p.World.RpcSnapshot().Skip(summaryCursor).Any(r=>r.Bytes.AsSpan().StartsWith(new byte[]{0x80,0xa2,0})),"completion summary",TimeSpan.FromSeconds(10));
        Check.Equal(1L,await c.Database.LongAsync($"SELECT completions FROM char_missions WHERE charId={p.CharId} AND missionId={m.Id}"),"Exactly one earned receipt");
        Check.That(await MervFollowupCurrent(c,p)==null,"No active completed payload");
    }

    private static async Task<ViewCreation> SweepNpc(ScenarioContext c,HeadlessPlayer p,ushort npc,string? identity=null)
    {
        MissionProgress.SavedMob Find(MissionProgress.SavedState s)=>s.Mobs.Single(m=>identity==null?m.Npc==npc:m.Identity==identity);
        await p.WaitUntilAsync(()=>MervFollowupCheckpoint(c,p).GetAwaiter().GetResult().Mobs.Any(m=>identity==null?m.Npc==npc:m.Identity==identity),
            "scheduled mission NPC arrival",TimeSpan.FromSeconds(45));
        var saved=await MervFollowupCheckpoint(c,p);var initial=Find(saved);
        await MoveForMissionTest(p,initial.X+100,initial.Y,initial.Z);
        ViewCreation? current=null;
        await p.WaitUntilAsync(()=>
        {
            var state=MervFollowupCheckpoint(c,p).GetAwaiter().GetResult();var mob=Find(state);
            mob.Name=Encoding.ASCII.GetString(Encoding.ASCII.GetBytes(mob.Name).Take(32).ToArray()).TrimEnd('\0');
            try{current=ZionAfter1212FollowerReadiness.CurrentNpc(p.World.CurrentOwnViewLife(),p.Handle,mob,state.Instance);}
            catch(InvalidDataException){return false;}
            return current!=null;
        },"current owned NPC",TimeSpan.FromSeconds(15));
        return current!;
    }

    private static async Task SweepRide(ScenarioContext c,HeadlessPlayer p,MissionProgress.SavedState s,XElement area,bool enter,List<object> notes)
    {
        if(s.SiteDoors.Length==0){if(enter)await EnterMissionArea(c,p);else await LeaveMissionArea(p);return;}
        var rows=new[]{"staticObjects_slums_1.csv","staticObjects_slums_2.csv"}.SelectMany(f=>File.ReadLines(Path.Combine(c.RepositoryRoot,"data",f)));
        var ride=s.SiteDoors.Last().metrId==1?MervFollowupElevatorFixture.Select(s,enter,rows,area.Elements("object").Select(o=>(ushort)(int)o.Attribute("id")!).ToArray()):SweepLegacyDistrictRide(c,s,enter);
        await MoveForMissionTest(p,ride.Approach[0],ride.Approach[1],ride.Approach[2]);
        if(!enter){var now=await MervFollowupCurrent(c,p);if(now==null||now.Phase>s.Phase)return;}
        if(!enter)
        {
            var followers=ZionAfter1212FollowerReadiness.OpenFollowers(area.Parent!,s);
            if(followers.Length>0)
            {
                static void WireNames(MissionProgress.SavedState state)
                {foreach(var mob in state.Mobs)mob.Name=Encoding.ASCII.GetString(Encoding.ASCII.GetBytes(mob.Name).Take(32).ToArray()).TrimEnd('\0');}
                WireNames(s);var life=p.World.CurrentOwnViewLife();var wait=Stopwatch.StartNew();
                while(true)
                {
                    var current=await MervFollowupCurrent(c,p);if(current==null||current.Phase>s.Phase)return;
                    WireNames(current);
                    try{ZionAfter1212FollowerReadiness.Require(p.World.CurrentOwnViewLife(),life.Life,p.Handle,s,current,followers,ride.Approach);break;}
                    catch(InvalidDataException ex){if(wait.Elapsed>TimeSpan.FromSeconds(15))throw new ScenarioFailure("Companion readiness: "+ex.Message);}
                    await Task.Delay(200);
                }
            }
        }
        int log=c.Server.DebugLog().Length;p.SendRpc($"80 c8 {WireFormat.ToHex(BitConverter.GetBytes(ride.Key))} 03 00");
        if(s.Waypoints[0].y!=s.Exits[0][1])await p.WaitUntilAsync(()=>c.Server.DebugLog()[log..].Contains("[MISSION] Teleported to "),"ordinary elevator transport",TimeSpan.FromSeconds(12));
        if(enter&&ride.Measured)
        {
            string reason="No car observation";
            try{await p.WaitUntilAsync(()=>{try{MervFollowupElevatorFixture.CurrentOverlay(p.World.CurrentOwnViewLife(),ride);return true;}catch(InvalidDataException ex){reason=ex.Message;return false;}},"elevator view",TimeSpan.FromSeconds(10));}
            catch(Exception ex){throw new ScenarioFailure($"{ex.Message.Split(" Received ")[0]} Car {ride.Key}: {reason}");}
        }
        if(enter&&MervBound326Approach.NeedsArrival(s))
        {
            var match=System.Text.RegularExpressions.Regex.Matches(c.Server.DebugLog()[log..],@"\[MISSION\] Teleported to ([-\d.]+),([-\d.]+),([-\d.]+)").LastOrDefault();
            if(match!=null)
            {
                var root=Enumerable.Range(1,3).Select(i=>double.Parse(match.Groups[i].Value,CultureInfo.InvariantCulture)).ToArray();
                Check.That(NavMesh.Locate(ride.District,root[0],root[1],root[2],out var nav),"Arrival NAV floor");
                notes.Add(MervBound326Approach.Capture(s,root,nav,p.World.CurrentOwnViewLife(),ride.Measured));
            }
        }
        if(enter)
        {
            var current=await MervFollowupCurrent(c,p);
            if(current==null||current.Phase!=s.Phase||current.Flags.GetValueOrDefault("entered"))return;
            var door=current.SiteDoors[current.OpenFloorCurrentSiteIndex??(current.SiteDoors.Length-1)];
            var sites=SweepSites(c.RepositoryRoot,door.metrId);
            var candidates=sites.Where(x=>x.elevatorDoor!=null&&
                x.elevatorDoor.mxoStaticId==door.mxoStaticId&&x.elevatorDoor.pos_x==door.pos_x&&
                x.elevatorDoor.pos_y==door.pos_y&&x.elevatorDoor.pos_z==door.pos_z).ToArray();
            var site=current.OpenFloorSites!=null&&current.OpenFloorCurrentSiteIndex is int index?
                current.OpenFloorSites[index].Resolve(sites):candidates.Length==1?candidates[0]:null;
            if(site==null)return;
            // Disclosed movement aid: exercise the area's enter hooks after the real car request.
            var actual=System.Text.RegularExpressions.Regex.Matches(c.Server.DebugLog()[log..],@"\[MISSION\] Teleported to ([-\d.]+),([-\d.]+),([-\d.]+)").LastOrDefault();
            var root=actual==null?ride.Approach:Enumerable.Range(1,3).Select(i=>double.Parse(actual.Groups[i].Value,CultureInfo.InvariantCulture)).ToArray();
            if(!NavMesh.Project(ride.District,site.CentreX,site.floorY,site.CentreZ,out var inside,reachableFrom:root))return;
            await MoveForMissionTest(p,inside.x,inside.y,inside.z);
            await p.WaitUntilAsync(()=>{var now=MervFollowupCurrent(c,p).GetAwaiter().GetResult();return now==null||now.Phase>s.Phase||now.Flags.GetValueOrDefault("entered");},
                "mission-area enter hooks",TimeSpan.FromSeconds(6));
        }
    }

    private static MissionSite[] SweepSites(string root,ushort district)=>sweepSites.GetOrAdd((root,district),key=>
    {
        var statics=new List<StaticWorldObject>();
        foreach(string file in Directory.EnumerateFiles(Path.Combine(key.Root,"data"),"staticObjects*.csv"))
        foreach(string row in File.ReadLines(file).Skip(1))
        {
            var f=row.Split(',');if(f.Length<11||!ushort.TryParse(f[0],out var world)||world!=key.District)continue;
            double D(int n)=>double.Parse(f[n],CultureInfo.InvariantCulture);
            statics.Add(new(){metrId=world,sectorID=ushort.Parse(f[1]),mxoStaticId=BitConverter.ToUInt32(Convert.FromHexString(f[2])),
                staticId=BitConverter.ToUInt32(Convert.FromHexString(f[3])),type=Convert.FromHexString(f[4][..4]),exterior=bool.Parse(f[5]),
                pos_x=D(6),pos_y=D(7),pos_z=D(8),rot=D(9),quat=f[10]});
        }
        return MissionSites.Build(statics,key.District).ToArray();
    });

    private static MervFollowupElevatorFixture.Ride SweepLegacyDistrictRide(ScenarioContext c,MissionProgress.SavedState s,bool enter)
    {
        Check.That(s.Waypoints.Length==1&&s.Exits.Length==1&&!s.Objects.Any(o=>o.OpenFloor!=null)&&s.ElevatorWitnesses?.Length is null or 0,
            "Other-district entry requires its unchanged legacy door");
        var raw=s.SiteDoors.Last();var floor=s.Waypoints[0];
        uint floorKey=BitConverter.ToUInt32(Convert.FromHexString(floor.doorHex));
        Check.That(raw.mxoStaticId==floorKey&&new[]{floor.x,floor.y,floor.z}.SequenceEqual(new[]{raw.pos_x,raw.pos_y,raw.pos_z}),"Legacy floor identity");
        var pose=enter?s.Exits[0]:new[]{floor.x,floor.y,floor.z};uint key=floorKey;
        if(enter)
        {
            var keys=Directory.EnumerateFiles(Path.Combine(c.RepositoryRoot,"data"),"staticObjects*.csv").SelectMany(File.ReadLines)
                .Where(l=>l.StartsWith(raw.metrId+",",StringComparison.Ordinal)).Select(l=>l.Split(','))
                .Where(f=>f.Length>=11&&new ushort[]{417,419}.Contains(BitConverter.ToUInt16(Convert.FromHexString(f[4])))&&Enumerable.Range(0,3).All(i=>double.Parse(f[6+i],CultureInfo.InvariantCulture)==pose[i]))
                .Select(f=>BitConverter.ToUInt32(Convert.FromHexString(f[2]))).Distinct().ToArray();
            Check.That(keys.Length==1,"Unique current-district lobby car");key=keys[0];
        }
        return new(raw.metrId,key,pose,null,null);
    }
}
