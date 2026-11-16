using System.Text.Json;
using System.Xml.Linq;
using System.Text.RegularExpressions;
using System.Globalization;
using hds;
namespace HeadlessClient;
public static partial class Scenarios
{
    private static IReadOnlyDictionary<string,string> ZionAfter1212Environment=>new Dictionary<string,string>(StillNpcs){["MXO_DEV_COMMANDS"]="1",["MXO_MISSION_SITE_QA_SEED"]="101213",["MXO_PACKET_LOG"]="1"};
    private static Task ZionCriticalAfter1212(ScenarioContext context)
    {
        ZionAfter1212Fixture.RequirePin(System.Environment.GetEnvironmentVariable(ZionAfter1212Fixture.SourcePin),context.Server.SourceAssemblySha256);
        return RunZionAfter1212Chain(context,"ZionAfter1212",1,
        ZionAfter1212Fixture.Missions,ZionAfter1212Fixture.Prerequisites,"zion_1_2_*.xml",requireStandardCourier:true,evidenceLabel:"zion-critical-after-101212",
        evidenceOptions:new(checked((int)PlayerProgression.ExperienceForLevel(50)),ColdRestart:true,RestartEnvironment:ZionAfter1212Environment,
            ExpectedExperienceDelta:812625,ExpectedInformationDelta:455000,FreshUploadBooth:true,
            MissionExperience:ZionAfter1212Fixture.MissionExperience,MissionInformation:ZionAfter1212Fixture.MissionInformation,ExactZionAfter1212:true));
    }
    private static async Task ZionAfter1212Exit(ScenarioContext context,HeadlessPlayer p,Action<object>? note=null)
    {
        await ZionAfter1212Ride(context,p,false,note);
    }
    private static async Task<MissionProgress.SavedState> ZionAfter1212Checkpoint(ScenarioContext context,HeadlessPlayer p)
    {
        string hex=await context.Database.ScalarAsync($"SELECT IFNULL((SELECT HEX(payload) FROM char_active_missions WHERE charId={p.CharId}),'NULL')");
        return ZionAfter1212CheckpointPayload.Read(hex)??throw new ScenarioFailure("Expected current owned Zion checkpoint");
    }
    private static async Task ZionAfter1212EscortExit(ScenarioContext context,HeadlessPlayer p,Mission authored,int goal,
        MissionProgress.SavedState before,int intentCursor,int intentAbsoluteCursor,Action<object> note)
    {
        string? currentRaw=null;
        async Task<MissionProgress.SavedState?> Current()
        {
            string hex=await context.Database.ScalarAsync($"SELECT IFNULL((SELECT HEX(payload) FROM char_active_missions WHERE charId={p.CharId}),'NULL')");
            currentRaw=ZionAfter1212CheckpointPayload.Raw(hex);
            return ZionAfter1212CheckpointPayload.Read(hex);
        }
        void Snapshot(string kind)=>note(new{Kind=kind,Goal=goal,IntentRpcCursor=intentCursor,IntentAbsoluteRpcCursor=intentAbsoluteCursor,
            CurrentRpcCursor=p.World.RpcSnapshot().Count,RawCheckpoint=currentRaw});
        bool Same(MissionProgress.SavedState? s)=>s?.Mission==before.Mission&&s.Phase==before.Phase&&s.Identity==before.Identity&&s.Started==before.Started;
        int logCursor=context.Server.DebugLog().Length;string? request=null;bool completionPending=false;
        System.Diagnostics.Stopwatch? deadline=null;var ownLife=p.World.CurrentOwnViewLife();
        var current=await Current();Snapshot("Escort pre-approach checkpoint");
        if(Same(current))
        {
            Check.That(ZionAfter1212EscortProof.CanApproach(current!,authored,goal),"Exact pending authored exit has live following actors and completed prerequisites");
            var door=before.Waypoints.Single();
            Snapshot("Escort saved-door approach authorized before movement");
            await MoveForMissionTest(p,door.x,door.y-50,door.z);
            if(before.Exits.Single()[1]!=door.y)
            {
                deadline=System.Diagnostics.Stopwatch.StartNew();
                var ids=authored.phases[before.Phase].missionObjectives.Single(o=>o.goal==goal).Npcs.ToArray();
                IReadOnlyList<ZionAfter1212FollowerReadiness.Observation>? readiness=null;
                await p.WaitUntilAsync(()=>
                {
                    var actual=Current().GetAwaiter().GetResult();if(actual==null)return false;
                    try{readiness=ZionAfter1212FollowerReadiness.Require(p.World.CurrentOwnViewLife(),ownLife.Life,p.Handle,before,actual,ids,new[]{door.x,door.y-50,door.z});return true;}
                    catch(InvalidDataException){return false;}
                },"Current exit followers reached unchanged1500 range before ordinary downride",TimeSpan.FromSeconds(15));
                note(new{Kind="Current exit follower readiness",before.Mission,before.Phase,Goal=goal,Followers=readiness});
            }
            current=await Current();Snapshot("Escort pre-request checkpoint after authorized movement");
            if(Same(current))
            {
                if(ZionAfter1212EscortProof.CanApproach(current!,authored,goal))
                {
                    uint key=BitConverter.ToUInt32(Convert.FromHexString(door.doorHex));
                    request=$"80 c8 {WireFormat.ToHex(BitConverter.GetBytes(key))} 03 00";p.SendRpc(request);
                }
                else
                {
                    Check.That(ZionAfter1212EscortProof.CompletedPendingAdvance(before,current,authored,goal),"Exit capability remains current or exact completed outcome is pending after authorized movement");
                    completionPending=true;Snapshot("Escort completed pending advance; obsolete door request skipped");
                }
            }
        }
        deadline??=System.Diagnostics.Stopwatch.StartNew();
        while(!ZionAfter1212EscortProof.Advanced(before,current,authored,goal,p.World.RpcSnapshot().Select(r=>r.Bytes).ToArray(),intentCursor))
        {
            if(deadline.Elapsed>TimeSpan.FromSeconds(15)){Snapshot("Escort strict transition timeout");throw new ScenarioFailure("Authored escort exit lacks fresh matching goal acknowledgement and exact committed next phase");}
            await Task.Delay(25);current=await Current();
        }
        string rideLog=context.Server.DebugLog()[logCursor..];
        note(new{Kind="Authored escort exit outcome",before.Mission,CompletedPhase=before.Phase,Goal=goal,IntentRpcCursor=intentCursor,IntentAbsoluteRpcCursor=intentAbsoluteCursor,
            CompletionWasPending=completionPending,CommittedCheckpoint=currentRaw,Request=request,PhysicalTeleportObserved=rideLog.Contains("[MISSION] Teleported to "),RideLog=rideLog});
    }
    private static async Task ZionAfter1212Ride(ScenarioContext context,HeadlessPlayer p,bool enter,Action<object>? note=null,int creationStart=0,bool waitForPhaseAdvance=true,Action? beforeRequest=null)
    {
        var state=await ZionAfter1212Checkpoint(context,p);var door=state.Waypoints.Single();var lobby=state.Exits.Single();
        var life=p.World.CurrentOwnViewLife();Check.That(life.Current&&life.OwnViewReady,"Ordinary Ride requires the current own-view life");
        var authoredXml=Directory.GetFiles(Path.Combine(context.RepositoryRoot,"data","missions"),"zion_1_2_*.xml").Select(XElement.Load).Single(m=>(int)m.Attribute("id")! ==state.Mission);
        var authoredDocument=new System.Xml.XmlDocument();authoredDocument.LoadXml(authoredXml.ToString());
        var objectOrder=MissionXml.Parse(authoredDocument.DocumentElement!).phases[state.Phase].objects.Select(o=>o.id).ToArray();
        var plan=ZionAfter1212RidePlan.Select(state,enter,new[]{"staticObjects_slums_1.csv","staticObjects_slums_2.csv"}.SelectMany(f=>File.ReadLines(Path.Combine(context.RepositoryRoot,"data",f))),objectOrder);
        uint key=plan.Key;var pose=plan.Pose;
        await MoveForMissionTest(p,plan.Approach[0],plan.Approach[1],plan.Approach[2]);
        ZionAfter1212RidePlan.SameEntry(state,await ZionAfter1212Checkpoint(context,p));
        Check.That(life.Current&&p.World.CurrentOwnViewLife().Life==life.Life,"Current own-view life retained immediately before Ride wire request");
        var pending=enter?state.Objects.Where(o=>o.OpenFloor!=null&&o.OpenFloor.SourceGoid!=o.Goid&&ZionCurrentFixedBody.Find(p.World.CurrentOwnViewLife(),o,state.Instance)==null).ToArray():Array.Empty<MissionProgress.SavedObject>();
        string packetPath=Path.Combine(context.Server.WorkingDirectory,"PacketLog.txt");
        int packetCursor=InteriorAdmissionDeliveryProof.Read(packetPath).Count(c=>c==10);
        int cursor=context.Server.DebugLog().Length,rpc=p.World.RpcSnapshot().Count;string request=$"80 c8 {WireFormat.ToHex(BitConverter.GetBytes(key))} 03 00";beforeRequest?.Invoke();p.SendRpc(request);
        if(door.y!=lobby[1])await p.WaitUntilAsync(()=>context.Server.DebugLog()[cursor..].Contains("[MISSION] Teleported to "),"ordinary saved-site static-door ride",TimeSpan.FromSeconds(15));
        string rideLog=context.Server.DebugLog()[cursor..];
        note?.Invoke(new{Kind="Ordinary saved-site door request",Mission=state.Mission,Phase=state.Phase,Enter=enter,Key=key,Request=request,RpcCursor=rpc,SourceWaypoint=door,SourceExit=lobby,RideLog=rideLog});
        if(plan.Measured)
        {
            ViewCreation? overlay=null;
            await p.WaitUntilAsync(()=>
            {
                try{overlay=ZionCurrentDoorOverlay.Require(p.World.CurrentOwnViewLife(),plan);return true;}
                catch(InvalidDataException){return false;}
            },"Current live requested measured door03 overlay after ordinary request",TimeSpan.FromSeconds(15));
            Check.That(life.Current&&p.World.CurrentOwnViewLife().Life==life.Life,"Measured overlay belongs to the current requested own-view life");
            plan.RequireOverlay(overlay!);
            if(enter)
            {
                var actual=Regex.Matches(rideLog,@"\[MISSION\] Teleported to ([-\d.]+),([-\d.]+),([-\d.]+)").LastOrDefault();
                Check.That(actual!=null,"Measured ordinary entry must produce an actual Ride root");
                double[] root=Enumerable.Range(1,3).Select(i=>double.Parse(actual!.Groups[i].Value,CultureInfo.InvariantCulture)).ToArray();
                SectorProperties.Load(Path.Combine(context.RepositoryRoot,"data","sector_properties.csv"));NavMesh.UseDirectory(Path.Combine(context.RepositoryRoot,"data","navmesh"));
                ushort region=plan.Street!=null?plan.Street.Raw.GetProperty("Context").GetProperty("Floor").GetProperty("Node").GetProperty("Region").GetUInt16():plan.Npc!=null?checked((ushort)plan.Npc.Witness.GetProperty("Floor").GetProperty("Node").GetProperty("Region").GetInt32()):plan.Witness!.Region;
                Check.That(NavMesh.Locate(plan.World,root[0],root[1],root[2],out var nav)&&nav.sector==state.SiteDoors[state.OpenFloorCurrentSiteIndex!.Value].sectorID&&nav.region==region&&Math.Abs(root[1]-(nav.floorY+95))<=.01,"Actual measured Ride root is grounded on the selected floor NAV layer, including NPC-only phases");
                if(plan.Street!=null)Check.That(root.SequenceEqual(plan.Street.Floor.Standing!),"Street ordinary Ride uses exact saved floor Standing");
                if(plan.Npc!=null&&plan.Street==null)
                {
                    Check.That(root.SequenceEqual(plan.Npc.Floor.Standing!),"NPC-v2 ordinary Ride uses its exact owned Standing");
                    for(int actor=0;actor<plan.Npc.Witness.GetProperty("Actors").GetArrayLength();actor++)
                    {
                        var route=ZionAfter1212ActorRoute.Route(context.RepositoryRoot,plan.Npc,actor);
                        note?.Invoke(new{Kind="Independently audited NPC actor topology",Actor=actor,Route=route,NativePassageProven=false,HeadlessPortalOpenProven=false});
                    }
                }
                foreach(var obj in state.Objects.Where(o=>o.OpenFloor!=null))Check.That(obj.OpenFloor!.Route.Take(3).SequenceEqual(root),"Every current fixed object retains the actual measured Ride root");
                ZionAfter1212RidePlan.SameEntry(state,await ZionAfter1212Checkpoint(context,p));
            }
            note?.Invoke(new{Kind="Current measured elevator witness",Witness=plan.Witness,NpcWitness=plan.Npc?.Raw,RequestedKey=key,DoorView=overlay!.ViewId,DoorSpawn=overlay.SpawnId});
        }
        if(enter&&pending.Length>0)
        {
            var arrival=Regex.Matches(rideLog,@"\[MISSION\] Teleported to ([-\d.]+),([-\d.]+),([-\d.]+)").LastOrDefault();
            bool rode=arrival!=null;double[] root=pending[0].OpenFloor!.Route.Take(3).ToArray();
            if(rode)Check.That(Enumerable.Range(1,3).Select(i=>double.Parse(arrival!.Groups[i].Value,CultureInfo.InvariantCulture)).SequenceEqual(root),"Actual ordinary ride reaches published free-prop root");
            else
            {
                await MoveForMissionTest(p,root[0],root[1],root[2]);
                p.World.SendViewList(ZionAfter1212Fixture.ClientPosition(root));
            }
            await p.WaitUntilAsync(()=>pending.All(o=>ZionCurrentFixedBody.Find(p.World.CurrentOwnViewLife(),o,state.Instance)!=null),"First current free bodies after stationary arrival receipt",TimeSpan.FromSeconds(15));
            string raw=InteriorAdmissionDeliveryProof.Read(packetPath);
            foreach(var obj in pending)
            {
                object? receipt=rode?InteriorAdmissionDeliveryProof.Require(raw,p.CharId,packetCursor,root,obj.Goid,obj.OpenFloor!.Centre):null;
                note?.Invoke(new{Kind="First free body before route/NPC movement",state.Mission,state.Phase,obj.Id,RawSource=obj.Spot,FixedPlacement=obj.OpenFloor,Stationary=rode,TransportReceipt=receipt,ClientPositionInterestOnly=!rode,NativeRenderingProven=false});
            }
        }
        if(!enter&&waitForPhaseAdvance)
        {
            await p.WaitUntilAsync(()=>context.Database.ScalarAsync($"SELECT IFNULL((SELECT CONCAT(JSON_EXTRACT(payload,'$.Mission'),':',JSON_EXTRACT(payload,'$.Phase')) FROM char_active_missions WHERE charId={p.CharId}),'NULL')").GetAwaiter().GetResult()!=state.Mission+":"+state.Phase,
                "Ordinary exit persisted its next phase before reading another door",TimeSpan.FromSeconds(20));
        }
    }
    private static async Task ZionAfter1212Defeat(HeadlessPlayer p,ViewCreation target)
    {
        int cursor=p.World.RpcSnapshot().Count;int npcLevel=ZionAfter1212Fixture.NpcLevel(target);
        uint expected=ZionAfter1212Fixture.ExpectedKill(npcLevel);
        await DefeatMissionNpc(p,target);
        await p.WaitUntilAsync(()=>p.World.RpcSnapshot().Skip(cursor).Any(r=>r.Bytes.Length==10&&r.Bytes[0]==0x80&&r.Bytes[1]==0xe6&&BitConverter.ToUInt32(r.Bytes,6)==1),"actual fighting XP after normal combat",TimeSpan.FromSeconds(10));
        var gains=p.World.RpcSnapshot().Skip(cursor).Where(r=>r.Bytes.Length==10&&r.Bytes[0]==0x80&&r.Bytes[1]==0xe6&&BitConverter.ToUInt32(r.Bytes,6)==1).ToArray();
        Check.Equal(1,gains.Length,"One earned kill gain for actual target");Check.Equal(expected,BitConverter.ToUInt32(gains[0].Bytes,2),"Independent shipped experience formula for actual NPC level, current all-weakling policy");
    }
    private static async Task<ViewCreation> ZionAfter1212Npc(ScenarioContext context,HeadlessPlayer p,XElement definition,int creationStart)
    {
        var state=await ZionAfter1212Checkpoint(context,p);ushort id=checked((ushort)(int)definition.Attribute("id")!);
        var mob=state.Mobs.Single(m=>m.Npc==id);
        await MoveForMissionTest(p,mob.X+100,mob.Y,mob.Z);
        ViewCreation? current=null;
        await p.WaitUntilAsync(()=>(current=ZionAfter1212FollowerReadiness.CurrentNpc(p.World.CurrentOwnViewLife(),p.Handle,mob,state.Instance))!=null,"actual current-life saved NPC identity/position for "+id,TimeSpan.FromSeconds(15));
        return current!;
    }
    private static async Task<ViewCreation> ZionAfter1212Object(ScenarioContext context,HeadlessPlayer p,int objectId,int creationStart,Action<object>? note=null)
    {
        var state=await ZionAfter1212Checkpoint(context,p);var obj=state.Objects.Single(o=>o.Id==objectId);
        if(obj.OpenFloor==null)
        {
            Check.That(NumericalUtils.ByteArrayToUint16(obj.Spot.type,1)==obj.Goid,"Only an actual bound native object can lack a free-proxy witness");
            var boundScope=ZionCurrentBoundBody.Capture(state,checked((ushort)objectId));
            // Native furniture stays bound in place. This fixture projects onto actual NAV from the
            // current avatar component; it does not turn a spawner centre into a player destination.
            SectorProperties.Load(Path.Combine(context.RepositoryRoot,"data","sector_properties.csv"));NavMesh.UseDirectory(Path.Combine(context.RepositoryRoot,"data","navmesh"));
            var ride=Regex.Matches(context.Server.DebugLog(),@"\[MISSION\] Teleported to ([-\d.]+),([-\d.]+),([-\d.]+)").Last();
            double[] from=Enumerable.Range(1,3).Select(i=>double.Parse(ride.Groups[i].Value,CultureInfo.InvariantCulture)).ToArray();
            Check.That(NavMesh.Locate(1,from[0],from[1],from[2],out _),"Actual mission Ride root is on current floor NAV");
            var rootSector=SectorProperties.At(1,from[0],from[2]);var objectSector=SectorProperties.At(1,obj.Spot.pos_x,obj.Spot.pos_z);
            double flat=(from[0]-obj.Spot.pos_x)*(from[0]-obj.Spot.pos_x)+(from[2]-obj.Spot.pos_z)*(from[2]-obj.Spot.pos_z);
            Check.That(rootSector!=null&&objectSector!=null&&(rootSector.sector==objectSector.sector||flat<=1600d*1600d),"NAV projection must retain its root-component anchor rather than drop a distant cross-sector origin");
            Check.That(NavMesh.Project(1,obj.Spot.pos_x,obj.Spot.pos_y,obj.Spot.pos_z,out var bound,maxDistance:800,reachableFrom:from),"Bound native furniture has current-component NAV approach");
            note?.Invoke(new{Kind="Bound native furniture NAV-only approach",Mission=state.Mission,Phase=state.Phase,ObjectId=objectId,Source=obj.Spot,ActualRideRoot=from,Standing=new[]{bound.x,bound.y,bound.z},NativeCollisionClearanceProven=false});
            await MoveForMissionTest(p,bound.x,bound.y,bound.z);
            // The current owned phase, not a fabricated post-phase birth, is authoritative.
            var owned=await ZionAfter1212Checkpoint(context,p);ZionCurrentBoundBody.Result? currentBody=null;
            await p.WaitUntilAsync(()=>
            {
                try{currentBody=ZionCurrentBoundBody.Require(p.World.CurrentOwnViewLife(),boundScope,owned);return true;}
                catch(InvalidDataException){return false;}
            },"native furniture current own-life bound body",TimeSpan.FromSeconds(15));
            note?.Invoke(new{Kind="Current own-life bound body",state.Mission,state.Phase,ObjectId=objectId,Scope=boundScope,
                Current=currentBody,RawSource=obj.Spot,PhaseCreationCursor=creationStart,
                SelectionPolicy="Current own life; no post-phase birth requirement",NativeRenderingProven=false});
            return currentBody!.View;
        }
        var w=MissionOpenFloorWitness.Read(JsonSerializer.SerializeToElement(obj.OpenFloor,new JsonSerializerOptions{IncludeFields=true}));
        w.Require(state.Mission,state.Phase,objectId,obj.Spot.mxoStaticId,NumericalUtils.ByteArrayToUint16(obj.Spot.type,1),obj.Goid);
        SectorProperties.Load(Path.Combine(context.RepositoryRoot,"data","sector_properties.csv"));NavMesh.UseDirectory(Path.Combine(context.RepositoryRoot,"data","navmesh"));
        Check.That(NavMesh.Locate(1,w.Standing[0],w.Standing[1],w.Standing[2],out var standing),"Published free-object Standing is on actual NAV");
        Check.Equal(standing.floorY+95,w.Standing[1],"Published Standing uses actual NAV height");
        for(int i=0;i<w.Route.Count;i+=3)await MoveForMissionTest(p,w.Route[i],w.Route[i+1],w.Route[i+2]);
        await p.WaitUntilAsync(()=>ZionCurrentFixedBody.Find(p.World.CurrentOwnViewLife(),obj,state.Instance)!=null,"current generation fixed object body at committed witness",TimeSpan.FromSeconds(15));
        var body=ZionCurrentFixedBody.Find(p.World.CurrentOwnViewLife(),obj,state.Instance)!;
        Check.Equal(w.Quaternion,Convert.ToHexString(body.Attributes["Orientation"]),"Fixed DTO orientation on actual03 body");
        var after=await ZionAfter1212Checkpoint(context,p);var current=after.Objects.Single(o=>o.Id==objectId);
        Check.Equal(JsonSerializer.Serialize(new{obj.Id,obj.Goid,obj.Spot,obj.OpenFloor},new JsonSerializerOptions{IncludeFields=true}),JsonSerializer.Serialize(new{current.Id,current.Goid,current.Spot,current.OpenFloor},new JsonSerializerOptions{IncludeFields=true}),"Whole raw source/fixed DTO unchanged through ordinary ride/route movement");
        Check.Equal(JsonSerializer.Serialize(new{state.SiteDoors,state.OpenFloorSites,state.OpenFloorCurrentSiteIndex,state.Waypoints,state.Exits},new JsonSerializerOptions{IncludeFields=true}),JsonSerializer.Serialize(new{after.SiteDoors,after.OpenFloorSites,after.OpenFloorCurrentSiteIndex,after.Waypoints,after.Exits},new JsonSerializerOptions{IncludeFields=true}),"Entire fixed site history/current door tuple unchanged");
        note?.Invoke(new{Kind="Immutable source/fixed DTO",Source=obj.Spot,Witness=w.RawJson,body.ViewId,body.SpawnId});return body;
    }
}
