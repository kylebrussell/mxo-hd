using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using hds;
namespace HeadlessClient;
public static partial class Scenarios
{
    private static async Task InteriorAdmissionProof(ScenarioContext context,InteriorAdmissionCase c)
    {
        InteriorAdmissionFixture.RequireSource(System.Environment.GetEnvironmentVariable(InteriorAdmissionFixture.ExpectedSourceEnvironment),Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(context.Server.SourceAssembly))));
        var p=await context.AddPlayerAsync(new CharacterSeed("Interior"+c.Organization){Level=c.Level,Experience=(int)PlayerProgression.ExperienceForLevel(c.Level),Organization=c.Organization,
            X=c.X,Y=c.Y,Z=c.Z,Health=5000,MaxHealth=5000,InnerStrength=2000,MaxInnerStrength=2000,CompletedMissions=c.Prerequisites});
        var fields=new JsonSerializerOptions{IncludeFields=true};var proof=new List<object>();string step="prerequisites";Exception? failure=null;
        long baselineXp=0,baselineInfo=0;int initialViews=0,initialSections=0;double[]? actualRideRoot=null;
        async Task<MissionProgress.SavedState> Saved()
        {
            string raw=await context.Database.ScalarAsync($"SELECT payload FROM char_active_missions WHERE charId={p.CharId}");
            proof.Add(new{Step=step,RawCheckpoint=raw,RpcCursor=p.World.RpcSnapshot().Count,CreationCursor=p.World.CreationSnapshot().Count});
            var state=JsonSerializer.Deserialize<MissionProgress.SavedState>(raw,fields)!;Check.Equal(c.Mission,state.Mission,"Actual selected mission identity");return state;
        }
        async Task Goal(int goal,int cursor)
        {await p.WaitUntilAsync(()=>p.World.RpcSnapshot().Skip(cursor).Any(r=>BytePattern.Parse($"80 a0 {goal:x2} 06 00 01").IsMatch(r.Bytes)),"authored goal "+goal,TimeSpan.FromSeconds(15));}
        async Task<ViewCreation> Npc(ushort id)
        {
            var s=await Saved();var m=s.Mobs.Single(x=>x.Npc==id);
            await MoveForMissionTest(p,m.X+100,m.Y,m.Z);
            bool Match(ViewCreation v)=>v.Name==m.Name&&v.Goid==599&&v.Attributes.TryGetValue("Position",out var pos)&&pos.Length==24&&
                Enumerable.Range(0,3).All(i=>BitConverter.ToDouble(pos,i*8)==new[]{m.X,m.Y,m.Z}[i])&&
                (v.Attributes.TryGetValue("IsEnemy",out var enemy)&&enemy[0]!=0)==!m.Friendly;
            await p.WaitUntilAsync(()=>p.World.CreationSnapshot().Skip(initialViews).Any(Match),"exact current NPC "+id,TimeSpan.FromSeconds(15));
            var view=p.World.CreationSnapshot().Skip(initialViews).Last(Match);proof.Add(new{Step=step,Npc=id,Source=m,view.ViewId,view.SpawnId,Attributes=view.Attributes.ToDictionary(x=>x.Key,x=>Convert.ToHexString(x.Value))});return view;
        }
        uint Lobby(MissionProgress.SavedState s)=>InteriorAdmissionFixture.DoorAt(new[]{"staticObjects_slums_1.csv","staticObjects_slums_2.csv"}.SelectMany(f=>File.ReadLines(Path.Combine(context.RepositoryRoot,"data",f))),s.Exits.Single());
        async Task Ride(bool enter,MissionProgress.SavedState? source=null)
        {
            var s=source??await Saved();var door=s.Waypoints.Single();var lobby=s.Exits.Single();
            double[] pose=enter?lobby:new[]{door.x,door.y,door.z};uint key=enter?Lobby(s):BitConverter.ToUInt32(Convert.FromHexString(door.doorHex),0);
            await MoveForMissionTest(p,pose[0],pose[1]-50,pose[2]);
            int cursor=p.World.RpcSnapshot().Count,log=context.Server.DebugLog().Length;p.SkipReceived();string request=$"80 c8 {WireFormat.ToHex(BitConverter.GetBytes(key))} 03 00";p.SendRpc(request);
            if(door.y!=lobby[1])await p.WaitUntilAsync(()=>context.Server.DebugLog()[log..].Contains("[MISSION] Teleported to "),"ordinary static-door ride commit",TimeSpan.FromSeconds(15));
            var arrived=Regex.Matches(context.Server.DebugLog()[log..],@"\[MISSION\] Teleported to ([-\d.]+),([-\d.]+),([-\d.]+)").LastOrDefault();
            if(arrived!=null)actualRideRoot=Enumerable.Range(1,3).Select(i=>double.Parse(arrived.Groups[i].Value,CultureInfo.InvariantCulture)).ToArray();
            proof.Add(new{Step=step,Enter=enter,DoorKey=key,Request=request,RpcCursor=cursor,RideLog=context.Server.DebugLog()[log..]});
        }
        async Task InitialNpcs(MissionProgress.SavedState s)
        {
            SectorProperties.Load(Path.Combine(context.RepositoryRoot,"data","sector_properties.csv"));NavMesh.UseDirectory(Path.Combine(context.RepositoryRoot,"data","navmesh"));
            foreach(var m in s.Mobs.Where(m=>m.Phase==s.Phase&&!m.Dead))
            {
                Check.That(NavMesh.Locate(1,m.X,m.Y,m.Z,out var nav),"Initial admitted NPC is on actual NAV: "+m.Name);
                Check.Equal(nav.floorY+95,m.Y,"Initial NPC standing height uses its actual NAV floor");
                ViewCreation? initialBody=null;
                await p.WaitUntilAsync(()=>(initialBody=p.World.CreationSnapshot().Skip(initialViews).LastOrDefault(v=>v.Goid==599&&v.Name==m.Name&&v.Attributes.TryGetValue("Position",out var pos)&&pos.Length==24&&Enumerable.Range(0,3).All(i=>BitConverter.ToDouble(pos,i*8)==new[]{m.X,m.Y,m.Z}[i])))!=null,"actual03 initial NPC at saved NAV pose: "+m.Name,TimeSpan.FromSeconds(15));
                proof.Add(new{Step=step,InitialNpc=m,NavFloor=nav.floorY,InitialView=initialBody!.ViewId,InitialSpawn=initialBody.SpawnId,Attributes=initialBody.Attributes.ToDictionary(x=>x.Key,x=>Convert.ToHexString(x.Value)),Scope="Saved and actual03 initial NPC position/NAV; native rendering/clearance unproven"});
            }
            if(c==InteriorAdmissionFixture.Machines&&s.Phase==1)Check.That(new ushort[]{3,5}.All(id=>s.Mobs.Any(m=>m.Npc==id)),"Wong and Park are in actual admitted checkpoint");
            await Task.CompletedTask;
        }
        try
        {
            Check.That(!c.Prerequisites.Contains(c.Mission),"No selected completion seeded");
            Check.Equal((long)c.Prerequisites.Distinct().Count(),await context.Database.LongAsync($"SELECT COUNT(*) FROM char_missions WHERE charId={p.CharId}"),"Only earlier receipts installed before Margin, as in NativeQa");
            step="normal-entry-and-offer";await p.EnterWorldAsync();baselineXp=await context.Database.LongAsync($"SELECT exp FROM characters WHERE charId={p.CharId}");baselineInfo=await context.Database.LongAsync($"SELECT cash FROM characters WHERE charId={p.CharId}");
            int offer=p.World.RpcSnapshot().Count;p.SkipReceived();p.SendRpc($"80 94 05 00 {c.Organization:x2}");await p.WaitForRpcAsync("80 97 05 00");
            int index=MissionContactChoice.CriticalIndex(p.World.RpcSnapshot().Skip(offer).Select(r=>r.Bytes),c.Title);
            p.SendRpc($"80 98 06 00 {Le((ushort)index)} 01 02 00");await p.WaitForRpcAsync("80 99");initialViews=p.World.CreationSnapshot().Count;initialSections=p.World.ViewSnapshot().Count;
            p.SendRpc("80 9b 07 00 01");await p.WaitForRpcAsync("80 9e 00");var accepted=await Saved();Check.Equal(0,accepted.Phase,"Normal acceptance begins first phase");
            proof.Add(new{Step=step,Origin=new[]{c.X,c.Y,c.Z},c.LayoutSeed,OriginalNativeDoor=c.OriginalDoor,OriginalNativeSource=c.OriginalSource,OfferCursor=offer,SelectedIndex=index,OfferRpcs=p.World.RpcSnapshot().Skip(offer).Select(r=>r.Hex).ToArray(),Qualification="Historical native tuple and current filtered acceptance remain separately attributed"});
            step="ordinary-first-floor-entry";await Ride(true);var firstSite=await Saved();await InitialNpcs(firstSite);
            if(c.TargetPhase>0)
            {
                step="ordinary-first-phase-goals";var first=await Npc(1);int cursor=p.World.RpcSnapshot().Count;
                int nextViews=p.World.CreationSnapshot().Count,nextSections=p.World.ViewSnapshot().Count;p.SendRpc($"80 c7 {MissionNpcId(first)} 09 00");await Goal(1,cursor);
                if(c.Mission==301203)
                {
                    var fortuna=await Npc(2);cursor=p.World.RpcSnapshot().Count;await DefeatMissionNpc(p,fortuna);await Goal(2,cursor);
                    var alpha=await Npc(1);cursor=p.World.RpcSnapshot().Count;nextViews=p.World.CreationSnapshot().Count;nextSections=p.World.ViewSnapshot().Count;p.SendRpc($"80 c7 {MissionNpcId(alpha)} 09 00");await Goal(3,cursor);
                }
                step="ordinary-exit-to-admitted-second-phase";await Ride(false,firstSite);
                await p.WaitUntilAsync(()=>context.Server.DebugLog().Contains("[MISSION] Phase 2 at a"),"actual second phase staging",TimeSpan.FromSeconds(15));
                initialViews=nextViews;initialSections=nextSections;var next=await Saved();Check.Equal(1,next.Phase,"Actual second phase admitted durably");
                step="ordinary-second-floor-entry";await Ride(true);
            }
            step="target-initial-placements";var target=await Saved();Check.Equal(c.TargetPhase,target.Phase,"Actual target phase");await InitialNpcs(target);
            if(c.Mission==201202)
            {
                Check.Equal(3,target.Objects.Count,"Poster first phase retains all three authored objects");
                proof.Add(new{PositiveCompatibility="Original native-passed floor is tested separately at its exact tuple offline; this normal acceptance uses the current eligible site pool, without forcing that floor",ActualDoor=target.Waypoints.Single().doorHex,ActualSources=target.Objects.Select(o=>o.Spot.mxoStaticId).ToArray(),HistoricalDoor=c.OriginalDoor,HistoricalSources=c.OriginalSources});
            }
            string FixedSet(MissionProgress.SavedState s)=>JsonSerializer.Serialize(s.Objects.OrderBy(o=>o.Id).Select(o=>new{o.Id,o.Spot,o.Goid,o.OpenFloor}),fields);
            string SiteSet(MissionProgress.SavedState s)=>JsonSerializer.Serialize(new{s.SiteDoors,s.OpenFloorSites,s.OpenFloorCurrentSiteIndex,s.Waypoints,s.Exits},fields);
            string originalSet=FixedSet(target),originalSite=SiteSet(target);
            for(int objectId=1;objectId<=c.Objects;objectId++)
            {
            var obj=target.Objects.Single(x=>x.Id==objectId);
            string originalSource=JsonSerializer.Serialize(obj.Spot,fields),originalWitness=JsonSerializer.Serialize(obj.OpenFloor,fields);
            Check.That(actualRideRoot!=null,"Target floor has an actual ordinary ride root");
            double[] centre,standing,route;string quaternion;bool free=obj.OpenFloor!=null;
            if(free)
            {
                var w=MissionOpenFloorWitness.Read(JsonSerializer.SerializeToElement(obj.OpenFloor,fields));w.Require(c.Mission,c.TargetPhase,objectId,obj.Spot.mxoStaticId,BitConverter.ToUInt16(obj.Spot.type,0),obj.Goid);
                centre=w.Centre.ToArray();standing=w.Standing.ToArray();route=w.Route.ToArray();quaternion=w.Quaternion;
                Check.That(actualRideRoot!.SequenceEqual(route.Take(3)),"Actual ordinary ride equals the committed free-prop route root");
            }
            else
            {
                Check.Equal(obj.Goid,BitConverter.ToUInt16(obj.Spot.type,0),"A missing free-prop witness is allowed only for an already-bound native object");
                Check.That(NavMesh.Locate(1,actualRideRoot![0],actualRideRoot[1],actualRideRoot[2],out var boundRoot),"Bound-native actual ride root NAV");
                centre=new[]{obj.Spot.pos_x,obj.Spot.pos_y,obj.Spot.pos_z};quaternion=obj.Spot.quat;
                Check.That(NavMesh.Project(1,centre[0]+100,boundRoot.floorY+95,centre[2],out var approach,maxDistance:300,reachableFrom:actualRideRoot),"Bound-native approach uses current root-connected NAV");
                Check.That(approach.location.indoors==boundRoot.indoors&&approach.location.sector==boundRoot.sector&&approach.location.region==boundRoot.region,"Bound-native approach stays inside the actual arrival interior");
                standing=approach.Position;route=actualRideRoot.Concat(standing).ToArray();
                proof.Add(new{Step=step,BoundNative=true,obj.Goid,obj.Spot.mxoStaticId,SourceCentre=centre,NavStanding=standing,Scope="Existing native object, NAV approach only; no generated footprint/static-route witness or native walking/picking proof"});
            }
            Check.That(NavMesh.Locate(1,route[0],route[1],route[2],out var rootNav),"Actual root is NAV");Check.Equal(rootNav.floorY+95,route[1],"Root uses actual floor standing height");
            foreach(var npc in target.Mobs.Where(m=>m.Phase==target.Phase&&!m.Dead))
            {Check.That(NavMesh.Locate(1,npc.X,npc.Y,npc.Z,out var npcNav),"Target NPC remains on NAV");Check.Equal(rootNav.floorY,npcNav.floorY,"Target NPC and admitted root are on the same actual floor");
             Check.Equal(rootNav.indoors,npcNav.indoors,"Target NPC and root share NAV space kind");
             if(rootNav.indoors){Check.Equal(rootNav.sector,npcNav.sector,"Target NPC shares root interior sector");Check.Equal(rootNav.region,npcNav.region,"Target NPC shares root interior placement");}}
            if(c.Mission==301203){step="ordinary-Rhett-talk";var rhett=await Npc(4);int cursor=p.World.RpcSnapshot().Count;p.SendRpc($"80 c7 {MissionNpcId(rhett)} 09 00");await Goal(6,cursor);}
            step="published-route-and-actual-body";int routeRpc=p.World.RpcSnapshot().Count,routeViews=p.World.ViewSnapshot().Count;
            for(int i=0;i<route.Length;i+=3)await MoveForMissionTest(p,route[i],route[i+1],route[i+2]);
            int poseSave=p.World.RpcSnapshot().Count;p.SkipReceived();p.SendChat("?save");await p.WaitForRpcAsync("2e 07");
            string actualPose=await context.Database.ScalarAsync($"SELECT CONCAT(x+0e0,',',y+0e0,',',z+0e0) FROM characters WHERE charId={p.CharId}");
            Check.That(actualPose.Split(',').Select(x=>double.Parse(x,CultureInfo.InvariantCulture)).SequenceEqual(standing),"QA save/client movement evidence reaches the exact approach before authored action");
            proof.Add(new{Step=step,ObjectId=objectId,FreeProp=free,RequestedStanding=standing,PersistedActorPose=actualPose,RouteRpcCursor=routeRpc,RouteViewCursor=routeViews,PoseSaveCursor=poseSave,
                MovementRpcs=p.World.RpcSnapshot().Skip(routeRpc).Select(r=>r.Hex).ToArray(),ClientViews=p.World.ViewSnapshot().Skip(routeViews).Select(Convert.ToHexString).ToArray(),Scope="Disclosed movement plus save barrier, no SQL pose overwrite or native walking claim"});
            ViewCreation? body=null;await p.WaitUntilAsync(()=>(body=InteriorAdmissionFixture.Body(p.World.CreationSnapshot(),initialViews,obj.Goid,centre))!=null,"actual03 target body at unchanged centre",TimeSpan.FromSeconds(15));
            Check.Equal(quaternion.ToUpperInvariant(),Convert.ToHexString(body!.Attributes["Orientation"]),"Actual03 target full quaternion");
            var at=await Saved();var current=at.Objects.Single(x=>x.Id==objectId);Check.Equal(originalSet,FixedSet(at),"Whole authored fixed source/DTO set survives each approach");Check.Equal(originalSite,SiteSet(at),"Exact accepted site/history/waypoint survive each approach");Check.Equal(originalSource,JsonSerializer.Serialize(current.Spot,fields),"Approach never rewrites raw source");Check.Equal(originalWitness,JsonSerializer.Serialize(current.OpenFloor,fields),"Approach never replans fixed placement");
            string action=c.Item==0?"Open":"Take";byte use=ObjectStates.UseNumber(body.Goid,action)??throw new ScenarioFailure("No actual authored native use: "+action);
            uint EnabledState()
            {
                uint state=body.Attributes.TryGetValue("CurrentState",out var initialState)?BitConverter.ToUInt32(initialState):ObjectStates.DefaultState(body.Goid);byte[] prefix=WireFormat.FromHex($"{Le(body.ViewId)} 01 08");
                foreach(byte[] section in p.World.ViewSnapshot().Skip(initialSections))for(int offset=BytePattern.IndexOf(section,prefix,0);offset>=0;offset=BytePattern.IndexOf(section,prefix,offset+1))
                    if(offset+prefix.Length+4<=section.Length)state=BitConverter.ToUInt32(section,offset+prefix.Length);
                return state;
            }
            await p.WaitUntilAsync(()=>ObjectStates.Offered(body.Goid,EnabledState()).Contains(use),"actual enabled native target action "+action,TimeSpan.FromSeconds(10));
            step="ordinary-authored-target-interaction";int goalCursor=p.World.RpcSnapshot().Count;p.SkipReceived();p.SendRpc($"80 c7 {MissionNpcId(body)} {use:x2} 00");await Goal(c.Objects==3?objectId:c.Goal,goalCursor);
            var after=await Saved();
            Check.Equal(target.Identity,after.Identity,"Authored interaction retains selected mission identity");Check.Equal(target.Started,after.Started,"Authored interaction retains acceptance time");
            Check.That(target.EverDone.All(after.EverDone.Contains),"Earned prior goal history is retained across automatic phase advancement");
            int oldSites=target.OpenFloorSites?.Length??0;
            Check.Equal(JsonSerializer.Serialize(target.OpenFloorSites,fields),JsonSerializer.Serialize(after.OpenFloorSites?.Take(oldSites).ToArray(),fields),"Original admitted site history remains retained after interaction");
            if(after.Phase==target.Phase){Check.Equal(originalSet,FixedSet(after),"Same-phase interaction retains original source/fixed DTOs");Check.Equal(originalSite,SiteSet(after),"Same-phase interaction retains exact site");}
            if(c.Item!=0)Check.That(after.Items.ContainsKey(c.Item),"Actual Search earned the owned item");
            Check.Equal(1,p.World.RpcSnapshot().Skip(goalCursor).Count(r=>BytePattern.Parse($"80 a0 {(c.Objects==3?objectId:c.Goal):x2} 06 00 01").IsMatch(r.Bytes)),"Authored current goal tick emitted once");
            Check.Equal(0,p.Received("80 a2 00").Count,"Narrow admission proof did not complete selected mission");
            Check.That(!p.World.RpcSnapshot().Any(r=>r.Bytes.Length==10&&r.Bytes[0]==0x80&&r.Bytes[1]==0xe6&&BitConverter.ToUInt32(r.Bytes,6)==2),"No mission XP gain in narrow partial proof");
            Check.Equal(0L,await context.Database.LongAsync($"SELECT COUNT(*) FROM char_missions WHERE charId={p.CharId} AND missionId={c.Mission}"),"No selected receipt granted or falsely completed");
            long expectedKill=c.Mission==301203?19204:0;Check.Equal(baselineXp+expectedKill,await context.Database.LongAsync($"SELECT exp FROM characters WHERE charId={p.CharId}"),"Only actual Fortuna kill XP, no mission award");Check.Equal(baselineInfo,await context.Database.LongAsync($"SELECT cash FROM characters WHERE charId={p.CharId}"),"No pocket or mission information awarded");
            Check.Equal(0,p.World.Problems.Count,"Admission proof healthy parser");proof.Add(new{Step=step,ObjectId=objectId,GoalCursor=goalCursor,AuthoredAction=action,Use=use,body.ViewId,body.SpawnId,InitialRawSource=originalSource,InitialWitness=originalWitness,WholeInitialSet=originalSet,InitialSite=originalSite,CurrentPhase=after.Phase,AutomaticPhaseAdvance=after.Phase!=c.TargetPhase,OwnedItems=after.Items,NarrowProofOnly=true});
            }
            step="complete";
        }
        catch(Exception ex){failure=ex;throw;}
        finally
        {
            try
            {
                string stem=Path.Combine(Path.GetTempPath(),"mxo-"+c.Name+"-"+context.Database.Name);string dll=context.Server.SourceAssembly;
                await File.WriteAllTextAsync(stem+".json",JsonSerializer.Serialize(new{Passed=failure==null,Step=step,Failure=failure?.ToString(),Case=c,Proof=proof,Database=context.Database.Name,context.Server.ProcessId,SourceAssembly=dll,
                    SourceConfiguration=new DirectoryInfo(Path.GetDirectoryName(dll)!).Parent!.Name,SourceSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(dll))),SourceMtimeUtc=File.GetLastWriteTimeUtc(dll).ToString("O"),
                    Rpc=p.World.RpcSnapshot().Select(r=>r.Hex).ToArray(),Views=p.World.ViewSnapshot().Select(Convert.ToHexString).ToArray(),Problems=p.World.Problems.ToArray(),Debug=context.Server.DebugLog(),Output=context.Server.FullOutput(),
                    FinalPayload=await context.Database.ScalarAsync($"SELECT payload FROM char_active_missions WHERE charId={p.CharId}"),NativeWalkingProven=false,NativePickingProven=false,FullMissionCompletionProven=false,
                    Aids="Existing QA movement; Merv Fortuna uses disclosed weaken1 plus ordinary protocol attack. No selected progress/item/reward injection; no source override or retries."},new JsonSerializerOptions{WriteIndented=true}));
                Console.WriteLine("INTERIOR ADMISSION EVIDENCE "+stem+".json");
            }
            catch(Exception evidenceError){if(failure==null)throw;Console.Error.WriteLine("Admission evidence failed while retaining original error: "+evidenceError);}
            finally{p.Dispose();}
        }
    }
}
