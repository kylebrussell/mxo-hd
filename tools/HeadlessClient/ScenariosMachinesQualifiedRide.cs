using System.Text.Json;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using hds;
namespace HeadlessClient;
// QA-only adapter of the qualified241 current-owner ride/object/escort contracts; no mission selection.
public static partial class Scenarios
{
    private static async Task<MissionProgress.SavedState> MachinesNextCheckpoint(ScenarioContext context,HeadlessPlayer p)
    {
        string hex=await context.Database.ScalarAsync($"SELECT IFNULL((SELECT HEX(payload) FROM char_active_missions WHERE charId={p.CharId}),'NULL')");
        if(hex=="NULL")throw new ScenarioFailure("Current owned Machines checkpoint required");
        return JsonSerializer.Deserialize<MissionProgress.SavedState>(MissionBoundCabinetFixture.DecodeCheckpointHex(hex),new JsonSerializerOptions{IncludeFields=true})!;
    }
    private static async Task MachinesNextRide(ScenarioContext context,HeadlessPlayer p,bool enter,Action<object>? note=null,int creationStart=0,bool waitForPhaseAdvance=true,Action? beforeRequest=null)
    {
        var state=await MachinesNextCheckpoint(context,p);var door=state.Waypoints.Single();var lobby=state.Exits.Single();
        var life=p.World.CurrentOwnViewLife();Check.That(life.Current&&life.OwnViewReady,"Ordinary Ride requires the current own-view life");
        var authoredXml=Directory.GetFiles(Path.Combine(context.RepositoryRoot,"data","missions"),"machines_1_2_*.xml").Select(XElement.Load).Single(m=>(int)m.Attribute("id")! ==state.Mission);
        var authoredDocument=new System.Xml.XmlDocument();authoredDocument.LoadXml(authoredXml.ToString());
        var objectOrder=MissionXml.Parse(authoredDocument.DocumentElement!).phases[state.Phase].objects.Select(o=>o.id).ToArray();
        var plan=ZionAfter1212RidePlan.Select(state,enter,new[]{"staticObjects_slums_1.csv","staticObjects_slums_2.csv"}.SelectMany(f=>File.ReadLines(Path.Combine(context.RepositoryRoot,"data",f))),objectOrder);
        uint key=plan.Key;var pose=plan.Pose;
        await MoveForMissionTest(p,pose[0],pose[1]-50,pose[2]);
        ZionAfter1212RidePlan.SameEntry(state,await MachinesNextCheckpoint(context,p));
        Check.That(life.Current&&p.World.CurrentOwnViewLife().Life==life.Life,"Current own-view life retained immediately before Ride wire request");
        var pending=enter?state.Objects.Where(o=>o.OpenFloor!=null&&o.OpenFloor.SourceGoid!=o.Goid&&ZionCurrentFixedBody.Find(p.World.CurrentOwnViewLife(),o,state.Instance)==null).ToArray():Array.Empty<MissionProgress.SavedObject>();
        string packetPath=Path.Combine(context.Server.WorkingDirectory,"PacketLog.txt");
        int packetCursor=InteriorAdmissionDeliveryProof.Read(packetPath).Count(c=>c==10);
        var requestLife=p.World.CurrentOwnViewLife();int ownCursor=requestLife.Events.Count==0?0:requestLife.Events[^1].Section+1;
        var rideDeadline=System.Diagnostics.Stopwatch.StartNew();
        int cursor=context.Server.DebugLog().Length,rpc=p.World.RpcSnapshot().Count;string request=$"80 c8 {WireFormat.ToHex(BitConverter.GetBytes(key))} 03 00";beforeRequest?.Invoke();p.SendRpc(request);
        if(door.y!=lobby[1])await p.WaitUntilAsync(()=>context.Server.DebugLog()[cursor..].Contains("[MISSION] Teleported to "),"ordinary saved-site static-door ride",TimeSpan.FromSeconds(15));
        string rideLog=context.Server.DebugLog()[cursor..];
        var transport=Regex.Matches(rideLog,@"\[MISSION\] Teleported to ([-\d.]+),([-\d.]+),([-\d.]+)").LastOrDefault();
        if(transport!=null)
        {
            var expected=Enumerable.Range(1,3).Select(i=>double.Parse(transport.Groups[i].Value,CultureInfo.InvariantCulture)).ToArray();NpcInteriorComputerState.Teleport? actual=null;
            await p.WaitUntilAsync(()=>{try{actual=NpcInteriorComputerState.FreshTeleport(p.World.CurrentOwnViewLife(),requestLife,ownCursor,expected);return true;}catch(InvalidDataException){return false;}},"fresh actual own-floor teleport after ordinary Ride",TimeSpan.FromSeconds(Math.Max(0.001,15-rideDeadline.Elapsed.TotalSeconds)));
            note?.Invoke(new{Kind="Actual own-floor transport",Mission=state.Mission,Phase=state.Phase,RequestKey=key,Teleport=actual});
        }
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
                ushort region=plan.Npc!=null?checked((ushort)plan.Npc.Witness.GetProperty("Floor").GetProperty("Node").GetProperty("Region").GetInt32()):plan.Witness!.Region;
                Check.That(NavMesh.Locate(plan.World,root[0],root[1],root[2],out var nav)&&nav.sector==state.SiteDoors[state.OpenFloorCurrentSiteIndex!.Value].sectorID&&nav.region==region&&Math.Abs(root[1]-(nav.floorY+95))<=.01,"Actual measured Ride root is grounded on the selected floor NAV layer, including NPC-only phases");
                if(plan.Npc!=null)
                {
                    Check.That(root.SequenceEqual(plan.Npc.Floor.Standing!),"NPC-v2 ordinary Ride uses its exact owned Standing");
                    for(int actor=0;actor<plan.Npc.Witness.GetProperty("Actors").GetArrayLength();actor++)
                    {
                        var route=MissionNpcInteriorProof.Route(context.RepositoryRoot,plan.Npc,actor);
                        note?.Invoke(new{Kind="Independently audited NPC actor topology",Actor=actor,Route=route,NativePassageProven=false,HeadlessPortalOpenProven=false});
                    }
                }
                foreach(var obj in state.Objects.Where(o=>o.OpenFloor!=null))Check.That(obj.OpenFloor!.Route.Take(3).SequenceEqual(root),"Every current fixed object retains the actual measured Ride root");
                ZionAfter1212RidePlan.SameEntry(state,await MachinesNextCheckpoint(context,p));
            }
            var expectedDoor=plan.Npc==null?new MissionNpcInteriorProof.Door(key,overlay!.Goid,pose,plan.Quaternion!,null,""):(enter?plan.Npc.Lobby:plan.Npc.Floor);
            var observedDoor=NpcInteriorComputerState.CurrentDoor(p.World.CurrentOwnViewLife(),expectedDoor);
            note?.Invoke(new{Kind="Current measured elevator witness",Witness=plan.Witness,NpcWitness=plan.Npc?.Raw,RequestedKey=key,DoorView=overlay!.ViewId,DoorSpawn=overlay.SpawnId,CurrentState=observedDoor.State,Offered=ObjectStates.Offered(overlay.Goid,observedDoor.State)});
        }
        if(enter&&pending.Length>0)
        {
            var arrival=Regex.Matches(rideLog,@"\[MISSION\] Teleported to ([-\d.]+),([-\d.]+),([-\d.]+)").LastOrDefault();
            bool rode=arrival!=null;double[] root=pending[0].OpenFloor!.Route.Take(3).ToArray();
            if(rode)Check.That(Enumerable.Range(1,3).Select(i=>double.Parse(arrival!.Groups[i].Value,CultureInfo.InvariantCulture)).SequenceEqual(root),"Actual ordinary ride reaches published free-prop root");
            else
            {
                await MoveForMissionTest(p,root[0],root[1],root[2]);
                p.World.SendViewList(MachinesCriticalNextFiveFixture.ClientPosition(root));
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
                "Ordinary exit persisted its next phase before reading another door",TimeSpan.FromSeconds(MachinesCriticalNextFiveFixture.PhaseTransitionSeconds));
        }
    }
    private static async Task MachinesNextCertifiedActorRoute(ScenarioContext context,HeadlessPlayer p,MissionProgress.SavedState state,MissionProgress.SavedMob actor,Action<object> note)
    {
        var plan=MissionNpcInteriorRide.ForSite(state)??throw new ScenarioFailure("Post-upload return requires the current certified NPC floor");
        int index=MachinesCriticalNextFiveFixture.CertifiedActorIndex(state,actor,plan);var route=MissionNpcInteriorProof.Route(context.RepositoryRoot,plan,index);
        var doors=MissionNpcInteriorProof.RequiredDoors(plan);var opened=new HashSet<uint>();var life=p.World.CurrentOwnViewLife();
        foreach(var point in route)
        {
            Check.That(life.Current&&p.World.CurrentOwnViewLife().Life==life.Life,"Certified reacquire route retains current owner/life");
            foreach(uint key in point.Doors)
            {
                if(!opened.Add(key))continue;var door=doors.Single(d=>d.Key==key);NpcInteriorComputerState.DoorObservation? current=null;
                string request=$"80 c8 {WireFormat.ToHex(BitConverter.GetBytes(key))} 03 00";p.SendRpc(request);
                await p.WaitUntilAsync(()=>{try{current=NpcInteriorComputerState.CurrentDoor(p.World.CurrentOwnViewLife(),door);return true;}catch(InvalidDataException){return false;}},"current certified actor-route portal Open",TimeSpan.FromSeconds(15));
                note(new{Kind="Reacquire certified portal Open",Door=door,Request=request,Current=current,NativePassageProven=false});
            }
            await MoveForMissionTest(p,point.Standing[0],point.Standing[1],point.Standing[2]);
        }
        var after=await MachinesNextCheckpoint(context,p);ZionAfter1212RidePlan.SameEntry(state,after);
        Check.That(after.Mobs.Any(m=>m.Npc==actor.Npc&&m.Identity==actor.Identity),"Reacquired actor retains exact saved identity");
        note(new{Kind="Certified actor reacquire route",state.Mission,state.Phase,Actor=actor.Npc,actor.Identity,RoleIndex=index,Route=route,MovementAid="Existing test movement to lobby/route points/latest actor; ordinary elevator and portal RPCs only",NativeWalkingProven=false});
    }
    private static async Task<ViewCreation> MachinesNextObjectQualified(ScenarioContext context,HeadlessPlayer p,int objectId,int creationStart,Action<object>? note=null)
    {
        var state=await MachinesNextCheckpoint(context,p);var obj=state.Objects.Single(o=>o.Id==objectId);
        if(obj.OpenFloor==null)
        {
            Check.That(NumericalUtils.ByteArrayToUint16(obj.Spot.type,1)==obj.Goid,"Only an actual bound native object can lack a free-proxy witness");
            var boundScope=ZionCurrentBoundBody.Capture(state,checked((ushort)objectId));
            // Native furniture stays bound in place. This fixture projects onto actual NAV from the
            // current avatar component; it does not turn a spawner centre into a player destination.
            SectorProperties.Load(Path.Combine(context.RepositoryRoot,"data","sector_properties.csv"));NavMesh.UseDirectory(Path.Combine(context.RepositoryRoot,"data","navmesh"));
            var ride=Regex.Matches(context.Server.DebugLog(),@"\[MISSION\] Teleported to ([-\d.]+),([-\d.]+),([-\d.]+)").Last();
            double[] from=Enumerable.Range(1,3).Select(i=>double.Parse(ride.Groups[i].Value,CultureInfo.InvariantCulture)).ToArray();
            var (effective,bound)=MachinesCriticalNextFiveFixture.BoundApproach(obj,from);
            note?.Invoke(new{Kind="Bound native furniture effective-pose NAV-only approach",Mission=state.Mission,Phase=state.Phase,ObjectId=objectId,Source=obj.Spot,EffectivePose=effective,ActualRideRoot=from,Standing=new[]{bound.x,bound.y,bound.z},NativeCollisionClearanceProven=false});
            await MoveForMissionTest(p,bound.x,bound.y,bound.z);
            // The current owned phase, not a fabricated post-phase birth, is authoritative.
            var owned=await MachinesNextCheckpoint(context,p);ZionCurrentBoundBody.Result? currentBody=null;
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
        var after=await MachinesNextCheckpoint(context,p);var current=after.Objects.Single(o=>o.Id==objectId);
        Check.Equal(JsonSerializer.Serialize(new{obj.Id,obj.Goid,obj.Spot,obj.OpenFloor},new JsonSerializerOptions{IncludeFields=true}),JsonSerializer.Serialize(new{current.Id,current.Goid,current.Spot,current.OpenFloor},new JsonSerializerOptions{IncludeFields=true}),"Whole raw source/fixed DTO unchanged through ordinary ride/route movement");
        Check.Equal(JsonSerializer.Serialize(new{state.SiteDoors,state.OpenFloorSites,state.OpenFloorCurrentSiteIndex,state.Waypoints,state.Exits},new JsonSerializerOptions{IncludeFields=true}),JsonSerializer.Serialize(new{after.SiteDoors,after.OpenFloorSites,after.OpenFloorCurrentSiteIndex,after.Waypoints,after.Exits},new JsonSerializerOptions{IncludeFields=true}),"Entire fixed site history/current door tuple unchanged");
        note?.Invoke(new{Kind="Immutable source/fixed DTO",Source=obj.Spot,Witness=w.RawJson,body.ViewId,body.SpawnId});return body;
    }
    private static async Task MachinesNextEscortExit(ScenarioContext context,HeadlessPlayer p,Mission authored,int goal,
        MissionProgress.SavedState before,int intentCursor,int intentAbsoluteCursor,Action<object> note)
    {
        string? currentRaw=null;
        async Task<MissionProgress.SavedState?> Current()
        {
            string hex=await context.Database.ScalarAsync($"SELECT IFNULL((SELECT HEX(payload) FROM char_active_missions WHERE charId={p.CharId}),'NULL')");
            currentRaw=hex=="NULL"?null:MissionBoundCabinetFixture.DecodeCheckpointHex(hex);
            return currentRaw==null?null:JsonSerializer.Deserialize<MissionProgress.SavedState>(currentRaw,new JsonSerializerOptions{IncludeFields=true});
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
            var objectOrder=authored.phases[before.Phase].objects.Select(o=>o.id).ToArray();
            var exitPlan=ZionAfter1212RidePlan.Select(before,false,new[]{"staticObjects_slums_1.csv","staticObjects_slums_2.csv"}.SelectMany(f=>File.ReadLines(Path.Combine(context.RepositoryRoot,"data",f))),objectOrder);
            var exitPose=exitPlan.Pose;
            Snapshot("Escort saved-door approach authorized before movement");
            await MoveForMissionTest(p,exitPose[0],exitPose[1]-50,exitPose[2]);
            if(before.Exits.Single()[1]!=door.y)
            {
                deadline=System.Diagnostics.Stopwatch.StartNew();
                var ids=authored.phases[before.Phase].missionObjectives.Single(o=>o.goal==goal).Npcs.ToArray();
                IReadOnlyList<ZionAfter1212FollowerReadiness.Observation>? readiness=null;
                await p.WaitUntilAsync(()=>
                {
                    var actual=Current().GetAwaiter().GetResult();if(actual==null)return false;
                    try{readiness=ZionAfter1212FollowerReadiness.Require(p.World.CurrentOwnViewLife(),ownLife.Life,p.Handle,before,actual,ids,new[]{exitPose[0],exitPose[1]-50,exitPose[2]});return true;}
                    catch(InvalidDataException){return false;}
                },"Current exit followers reached unchanged1500 range before ordinary downride",TimeSpan.FromSeconds(15));
                note(new{Kind="Current exit follower readiness",before.Mission,before.Phase,Goal=goal,Followers=readiness});
            }
            current=await Current();Snapshot("Escort pre-request checkpoint after authorized movement");
            if(Same(current))
            {
                if(ZionAfter1212EscortProof.CanApproach(current!,authored,goal))
                {
                    uint key=exitPlan.Key;
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
}
