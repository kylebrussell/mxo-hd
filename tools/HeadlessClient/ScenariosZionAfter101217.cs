using System.Text.Json;
using System.Xml.Linq;
using System.Text.RegularExpressions;
using System.Globalization;
using hds;
namespace HeadlessClient;
public static partial class Scenarios
{
    private static IReadOnlyDictionary<string,string> ZionAfter101217Environment=>new Dictionary<string,string>(StillNpcs){["MXO_DEV_COMMANDS"]="1",["MXO_MISSION_SITE_QA_SEED"]="101301",["MXO_PACKET_LOG"]="1"};
    private static Task ZionCriticalAfter101217(ScenarioContext context)
    {
        ZionAfter101217Fixture.RequirePin(System.Environment.GetEnvironmentVariable(ZionAfter101217Fixture.SourcePin),context.Server.SourceAssemblySha256);
        return RunZionAfter101217Chain(context,"ZionAfter101217",1,
        ZionAfter101217Fixture.Missions,ZionAfter101217Fixture.Prerequisites,"zion_1_3_*.xml",requireStandardCourier:true,evidenceLabel:"zion-critical-after-101217",
        evidenceOptions:new(checked((int)PlayerProgression.ExperienceForLevel(50)),ColdRestart:true,RestartEnvironment:ZionAfter101217Environment,
            ExpectedExperienceDelta:1821980,ExpectedInformationDelta:479500,FreshUploadBooth:true,
            MissionExperience:ZionAfter101217Fixture.MissionExperience,MissionInformation:ZionAfter101217Fixture.MissionInformation,ExactZionAfter101217:true));
    }
    private static async Task ZionAfter101217Exit(ScenarioContext context,HeadlessPlayer p,Action<object>? note=null)
    {
        await ZionAfter101217Ride(context,p,false,note);
    }
    // The no-area stage inherits a real completed downride, not an invented new room.
    private static async Task<MissionOutsideUploadProof.Token> ZionAfter101217ExitToNoSite(ScenarioContext context,HeadlessPlayer p,Mission authored,int nextPhase,Action<object> note)
    {
        var before=await ZionAfter101217Checkpoint(context,p);
        Check.That(nextPhase==before.Phase+1&&ZionAfter1212OutsideStage.Matches(authored,nextPhase)&&before.Flags.GetValueOrDefault("waitingForExit"),"Only a completed interior exit can hand off to the authored outdoor stage");
        var street=ZionAfter1212StreetEntry.ForSite(before);var npc=MissionNpcInteriorRide.ForSite(before);
        var standing=street?.Lobby.Standing??npc?.Lobby.Standing??throw new ScenarioFailure("Completed departure requires an exact saved lobby Standing");
        var original=p.World.CurrentOwnViewLife();OwnViewLifeSnapshot? requestLife=null;
        int sections=-1,packetLine=-1,logOffset=-1;var boundary=new MissionOutsideUploadProof.RequestBoundary();
        string packetPath=Path.Combine(context.Server.WorkingDirectory,"PacketLog.txt");
        void Mark()=>boundary.Mark(()=>
        {
            var now=p.World.CurrentOwnViewLife();
            Check.That(original.Current&&now.Current&&now.OwnViewReady&&ReferenceEquals(original.Owner,now.Owner)&&original.Life==now.Life,"Completed downride retains its actual current owner/life");
            requestLife=now;sections=p.World.ViewSnapshot().Count;packetLine=InteriorAdmissionDeliveryProof.Read(packetPath).Count(c=>c=='\n');logOffset=context.Server.DebugLog().Length;
        });
        await ZionAfter101217Ride(context,p,false,note,beforeRequest:Mark);boundary.RequireMarked();
        MissionOutsideUploadProof.RequirePhysicalTrip(context.Server.DebugLog()[logOffset..],standing);
        NpcInteriorComputerState.Teleport? arrival=null;
        await p.WaitUntilAsync(()=>{try{arrival=NpcInteriorComputerState.FreshTeleport(p.World.CurrentOwnViewLife(),requestLife!,sections,standing);return true;}catch(InvalidDataException){return false;}},"Actual completed downride before outdoor Upload stage",TimeSpan.FromSeconds(15));
        MissionMidphaseReturnFixture.Arrival? receipt=null;
        await p.WaitUntilAsync(()=>{try{receipt=MissionMidphaseReturnFixture.RequireArrival(InteriorAdmissionDeliveryProof.Read(packetPath),p.CharId,packetLine,arrival!);return true;}catch(InvalidDataException){return false;}},"Exact completed-downride ACK before no-area handoff",TimeSpan.FromSeconds(10));
        var after=await ZionAfter101217Checkpoint(context,p);MissionNoSiteActorHistory.RequireTransition(before,after);
        string Items(MissionProgress.SavedState x)=>JsonSerializer.Serialize(x.Items.OrderBy(k=>k.Key));
        string History(MissionProgress.SavedState x)=>JsonSerializer.Serialize(new{x.SiteDoors,x.OpenFloorSites,x.ElevatorWitnesses,
            Npc=x.NpcInterior.ValueKind==JsonValueKind.Undefined?null:x.NpcInterior.GetProperty("Witnesses").GetRawText(),
            Street=x.StreetAccess.ValueKind==JsonValueKind.Undefined?null:x.StreetAccess.GetProperty("Witnesses").GetRawText()},new JsonSerializerOptions{IncludeFields=true});
        Check.That(after.Phase==nextPhase&&after.Done.Length==0&&Items(before)==Items(after)&&before.EverDone.Order().SequenceEqual(after.EverDone.Order())&&History(before)==History(after),"No-area phase handoff preserves earned items/goals and complete historical placement");
        var token=new MissionOutsideUploadProof.Token(after,p.World.CurrentOwnViewLife());
        MissionOutsideUploadProof.RequireCurrent(token,after,requestLife!);ZionAfter1212OutsideStage.Require(authored,nextPhase,after,token.Checkpoint);
        note(new{Kind="Actual completed downride/ACK to authored outdoor Upload stage",Before=before,After=after,ViewCursor=sections,PacketLine=packetLine,LogOffset=logOffset,Arrival=arrival,Receipt=receipt,MovementAid=false});return token;
    }
    private static async Task<MissionProgress.SavedState> ZionAfter101217Checkpoint(ScenarioContext context,HeadlessPlayer p)
    {
        string hex=await context.Database.ScalarAsync($"SELECT IFNULL((SELECT HEX(payload) FROM char_active_missions WHERE charId={p.CharId}),'NULL')");
        return ZionAfter1212CheckpointPayload.Read(hex)??throw new ScenarioFailure("Expected current owned Zion checkpoint");
    }
    private static async Task ZionAfter101217EscortExit(ScenarioContext context,HeadlessPlayer p,Mission authored,int goal,
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
    private static async Task ZionAfter101217Ride(ScenarioContext context,HeadlessPlayer p,bool enter,Action<object>? note=null,int creationStart=0,bool waitForPhaseAdvance=true,Action? beforeRequest=null)
    {
        var state=await ZionAfter101217Checkpoint(context,p);var door=state.Waypoints.Single();var lobby=state.Exits.Single();
        var life=p.World.CurrentOwnViewLife();Check.That(life.Current&&life.OwnViewReady,"Ordinary Ride requires the current own-view life");
        var authoredXml=Directory.GetFiles(Path.Combine(context.RepositoryRoot,"data","missions"),"zion_1_3_*.xml").Select(XElement.Load).Single(m=>(int)m.Attribute("id")! ==state.Mission);
        var authoredDocument=new System.Xml.XmlDocument();authoredDocument.LoadXml(authoredXml.ToString());
        var objectOrder=MissionXml.Parse(authoredDocument.DocumentElement!).phases[state.Phase].objects.Select(o=>o.id).ToArray();
        var plan=ZionAfter1212RidePlan.Select(state,enter,new[]{"staticObjects_slums_1.csv","staticObjects_slums_2.csv"}.SelectMany(f=>File.ReadLines(Path.Combine(context.RepositoryRoot,"data",f))),objectOrder);
        uint key=plan.Key;var pose=plan.Pose;
        var approach=plan.Approach;await MoveForMissionTest(p,approach[0],approach[1],approach[2]);
        ZionAfter1212RidePlan.SameEntry(state,await ZionAfter101217Checkpoint(context,p));
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
                ushort region=plan.Street!=null?plan.Street.Raw.GetProperty("Context").GetProperty("Floor").GetProperty("Node").GetProperty("Region").GetUInt16():plan.Npc!=null?checked((ushort)plan.Npc.Witness.GetProperty("Floor").GetProperty("Node").GetProperty("Region").GetInt32()):plan.Witness!.Region;
                Check.That(NavMesh.Locate(plan.World,root[0],root[1],root[2],out var nav)&&nav.sector==state.SiteDoors[state.OpenFloorCurrentSiteIndex!.Value].sectorID&&nav.region==region&&Math.Abs(root[1]-(nav.floorY+95))<=.01,"Actual measured Ride root is grounded on the selected floor NAV layer, including NPC-only phases");
                if(plan.Street!=null)Check.That(root.SequenceEqual(plan.Street.Floor.Standing!),"Street Objects ordinary Ride retains exact saved Standing and current floor layer");
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
                ZionAfter1212RidePlan.SameEntry(state,await ZionAfter101217Checkpoint(context,p));
            }
            var expectedDoor=plan.Street!=null?(enter?plan.Street.Lobby:plan.Street.Floor):plan.Npc==null?new MissionNpcInteriorProof.Door(key,overlay!.Goid,pose,plan.Quaternion!,null,""):(enter?plan.Npc.Lobby:plan.Npc.Floor);
            var observedDoor=NpcInteriorComputerState.CurrentDoor(p.World.CurrentOwnViewLife(),expectedDoor);
            note?.Invoke(new{Kind="Current measured elevator witness",Witness=plan.Witness,NpcWitness=plan.Npc?.Raw,StreetWitness=plan.Street?.Raw,RequestedKey=key,DoorView=overlay!.ViewId,DoorSpawn=overlay.SpawnId,CurrentState=observedDoor.State,Offered=ObjectStates.Offered(overlay.Goid,observedDoor.State)});
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
    private static async Task ZionAfter101217Defeat(HeadlessPlayer p,ViewCreation target)
    {
        int cursor=p.World.RpcSnapshot().Count;int npcLevel=ZionAfter101217Fixture.NpcLevel(target);
        uint expected=ZionAfter101217Fixture.ExpectedKill(npcLevel);
        await DefeatMissionNpc(p,target);
        await p.WaitUntilAsync(()=>p.World.RpcSnapshot().Skip(cursor).Any(r=>r.Bytes.Length==10&&r.Bytes[0]==0x80&&r.Bytes[1]==0xe6&&BitConverter.ToUInt32(r.Bytes,6)==1),"actual fighting XP after normal combat",TimeSpan.FromSeconds(10));
        var gains=p.World.RpcSnapshot().Skip(cursor).Where(r=>r.Bytes.Length==10&&r.Bytes[0]==0x80&&r.Bytes[1]==0xe6&&BitConverter.ToUInt32(r.Bytes,6)==1).ToArray();
        Check.Equal(1,gains.Length,"One earned kill gain for actual target");Check.Equal(expected,BitConverter.ToUInt32(gains[0].Bytes,2),"Independent shipped experience formula for actual NPC level, current all-weakling policy");
    }
    private static async Task<ViewCreation> ZionAfter101217Npc(ScenarioContext context,HeadlessPlayer p,XElement definition,int creationStart)
    {
        var state=await ZionAfter101217Checkpoint(context,p);var accepted=DisturbingTrendsProgressProof.Capture(state);ushort id=checked((ushort)(int)definition.Attribute("id")!);
        var mob=state.Mobs.Single(m=>m.Npc==id);string uuid=mob.Identity;
        if(MissionNpcInteriorRide.ForSite(state)!=null)await ZionAfter101217CertifiedActorRoute(context,p,state,mob,_=>{});
        await MoveForMissionTest(p,mob.X+100,mob.Y,mob.Z);ViewCreation? current=null;
        await p.WaitUntilAsync(()=>
        {
            var fresh=ZionAfter101217Checkpoint(context,p).GetAwaiter().GetResult();Check.That(DisturbingTrendsProgressProof.Owns(accepted,fresh)&&fresh.Phase==state.Phase,"NPC target retains exact accepted phase");
            var actual=fresh.Mobs.Single(m=>m.Npc==id);Check.Equal(uuid,actual.Identity,"Current saved NPC UUID survives approach");
            current=ZionAfter1212FollowerReadiness.CurrentNpc(p.World.CurrentOwnViewLife(),p.Handle,actual,fresh.Instance);return current!=null;
        },"Actual current-life NPC/current saved identity and latest position",TimeSpan.FromSeconds(15));return current!;
    }
    private static async Task<ViewCreation> ZionAfter101217Object(ScenarioContext context,HeadlessPlayer p,int objectId,int creationStart,Action<object>? note=null)
    {
        var state=await ZionAfter101217Checkpoint(context,p);var obj=state.Objects.Single(o=>o.Id==objectId);
        if(obj.OpenFloor==null)
        {
            Check.That(NumericalUtils.ByteArrayToUint16(obj.Spot.type,1)==obj.Goid,"Only an actual bound native object can lack a free-proxy witness");
            var boundScope=ZionCurrentBoundBody.Capture(state,checked((ushort)objectId));
            // Native furniture stays bound in place. This fixture projects onto actual NAV from the
            // current avatar component; it does not turn a spawner centre into a player destination.
            SectorProperties.Load(Path.Combine(context.RepositoryRoot,"data","sector_properties.csv"));NavMesh.UseDirectory(Path.Combine(context.RepositoryRoot,"data","navmesh"));
            var ride=Regex.Matches(context.Server.DebugLog(),@"\[MISSION\] Teleported to ([-\d.]+),([-\d.]+),([-\d.]+)").Last();
            double[] from=Enumerable.Range(1,3).Select(i=>double.Parse(ride.Groups[i].Value,CultureInfo.InvariantCulture)).ToArray();
            Check.That(NavMesh.Locate(1,from[0],from[1],from[2],out var root),"Actual current RideRoot membership");
            // V3 may contain both326 and new furniture: consume its fixed authority before the old326 dispatcher.
            var furnitureRoute=MissionBoundFurnitureRouteProof.SupportsGoid(obj.Goid)?MissionBoundFurnitureRouteProof.Read(context.RepositoryRoot,state,objectId):null;
            if(furnitureRoute!=null)
            {
                var life=p.World.CurrentOwnViewLife();Check.That(life.Current&&life.OwnViewReady,"Current own life for fixed furniture route");
                MissionBoundFurnitureRouteProof.RequireArrival(furnitureRoute,from,root.sector,root.region,root.floorY);
                async Task SameFurniture(){MissionBoundFurnitureRouteProof.RequireCurrent(furnitureRoute,await ZionAfter101217Checkpoint(context,p));Check.That(life.Current,"Fixed furniture route own life remains current");}
                foreach(var step in MissionBoundFurnitureRouteProof.Steps(context.RepositoryRoot,furnitureRoute))
                {
                    await SameFurniture();
                    foreach(uint key in step.Doors)
                    {
                        var door=furnitureRoute.Doors.Single(d=>d.Key==key);var requestLife=p.World.CurrentOwnViewLife();int views=p.World.ViewSnapshot().Count,debug=context.Server.DebugLog().Length;
                        Check.That(requestLife.Current&&requestLife.OwnViewReady,"Current own life before V3 portal Open");p.SendRpc($"80 c8 {WireFormat.ToHex(BitConverter.GetBytes(key))} 03 00");
                        await p.WaitUntilAsync(()=>{try{var now=p.World.CurrentOwnViewLife();_=MissionBound326RouteProof.RequireDoorUse(context.Server.DebugLog(),debug,door);_=MissionBound326RouteProof.RequireOpen(now,requestLife,door);return true;}catch(InvalidDataException){return false;}},"Actual V3 own-GOID portal Open before route crossing",TimeSpan.FromSeconds(15));
                        p.SkipReceived();p.SendRpc("81 48");await p.WaitForRpcAsync("81 49");await SameFurniture();
                        _=MissionBound326RouteProof.RequireOpen(p.World.CurrentOwnViewLife(),requestLife,door);Check.Equal(0,MissionPublicStreetWire.Teleports(p.World.CurrentOwnViewLife(),requestLife,views).Count,"V3 internal Open is not an elevator ride");
                        note?.Invoke(new{Kind="V3 required own-GOID door Open",state.Mission,state.Phase,ObjectId=objectId,Door=door,ViewCursor=views,DebugCursor=debug,Exact03PacketAckProven=false,NativePassageProven=false});
                    }
                    await SameFurniture();await MoveForMissionTest(p,step.Standing[0],step.Standing[1],step.Standing[2]);
                }
                await SameFurniture();note?.Invoke(new{Kind="Current fixed furniture V3 route consumed",state.Mission,state.Phase,ObjectId=objectId,ActualRideRoot=from,Route=furnitureRoute.Route,NativeCollisionClearanceProven=false});
            }
            else if(obj.Goid==326)
            {
            var life=p.World.CurrentOwnViewLife();Check.That(life.Current&&life.OwnViewReady,"Current own life for bound approach");
            var bounds=MissionBound326Approach.Load(context.RepositoryRoot,obj);
            MissionBound326Approach.Sample? Locate(double[] point)=>NavMesh.Locate(1,point[0],point[1],point[2],out var n)?new(point,n.sector,n.region,n.polygon,n.floorY):null;
            MissionBound326Approach.Sample? Project(double[] point,double radius,double[] actual)=>NavMesh.Project(1,point[0],point[1],point[2],out var n,radius,actual,false)?new(n.Position,n.location.sector,n.location.region,n.location.polygon,n.location.floorY):null;
            var route=MissionBound326RouteProof.Read(state,objectId,bounds,new(from,root.sector,root.region,root.floorY),Locate);
            if(route!=null)
            {
                // An owned V2 route is consumed, never projected/reselected. Midpoints are movement aids, not collision proof.
                var steps=MissionBound326RouteProof.Steps(context.RepositoryRoot,route);
                async Task SameRoute(){MissionBound326RouteProof.RequireCurrent(route,await ZionAfter101217Checkpoint(context,p));Check.That(life.Current,"Bound route own life remains current");}
                foreach(var step in steps)
                {
                    await SameRoute();
                    foreach(uint key in step.Doors)
                    {
                        await SameRoute();var door=route.Doors.Single(d=>d.Key==key);var requestLife=p.World.CurrentOwnViewLife();int views=p.World.ViewSnapshot().Count,rpc=p.World.RpcSnapshot().Count;
                        Check.That(requestLife.Current&&requestLife.OwnViewReady,"Current own life before required portal Open");
                        int debugCursor=context.Server.DebugLog().Length;string request=$"80 c8 {WireFormat.ToHex(BitConverter.GetBytes(key))} 03 00";p.SendRpc(request);
                        NpcInteriorComputerState.DoorObservation? opened=null;string? useLine=null;
                        await p.WaitUntilAsync(()=>{try{var now=p.World.CurrentOwnViewLife();if(!ReferenceEquals(now.Owner,requestLife.Owner)||now.Life!=requestLife.Life||!requestLife.Current)return false;useLine=MissionBound326RouteProof.RequireDoorUse(context.Server.DebugLog(),debugCursor,door);opened=MissionBound326RouteProof.RequireOpen(now,requestLife,door);return true;}catch(InvalidDataException){return false;}},"Actual owned same-floor route door Open before crossing",TimeSpan.FromSeconds(15));
                        p.SkipReceived();p.SendRpc("81 48");await p.WaitForRpcAsync("81 49");await SameRoute();
                        Check.That(requestLife.Current,"Portal Open does not change own life");
                        _=MissionBound326RouteProof.RequireOpen(p.World.CurrentOwnViewLife(),requestLife,door);
                        note?.Invoke(new{Kind="Bound326 required own-GOID portal Open before crossing",state.Mission,state.Phase,ObjectId=objectId,Door=door,Request=request,ViewCursor=views,RpcCursor=rpc,DebugCursor=debugCursor,ActualDoorUse=useLine,Current=opened,ServerBarrier="8148/8149",Exact03PacketAckProven=false,NativePassageProven=false});
                    }
                    await SameRoute();await MoveForMissionTest(p,step.Standing[0],step.Standing[1],step.Standing[2]);
                }
                note?.Invoke(new{Kind="Owned fixed bound326 route consumed",state.Mission,state.Phase,ObjectId=objectId,Source=obj.Spot,ActualRideRoot=from,Bounds=bounds,Route=route.Route,NativeCollisionClearanceProven=false});
            }
            else
            {
                var selected=MissionBound326Approach.Select(bounds,new(from,root.sector,root.region,root.floorY),new(Locate,Project));
                var beforeMove=await ZionAfter101217Checkpoint(context,p);MissionOutsideContinuation.Require(state,beforeMove);
                Check.That(life.Current,"Bound approach owner life remains current");
                note?.Invoke(new{Kind="Audited native326 current-component NAV approach",state.Mission,state.Phase,ObjectId=objectId,Source=obj.Spot,ActualRideRoot=from,Bounds=bounds,Approach=selected,NativeCollisionClearanceProven=false});
                await MoveForMissionTest(p,selected.Standing[0],selected.Standing[1],selected.Standing[2]);
            }
            }
            else
            {
                throw new ScenarioFailure("Bound object lacks a qualified native326 or typed V3 furniture approach; no raw-source projection fallback");
            }
            // The current owned phase, not a fabricated post-phase birth, is authoritative.
            var owned=await ZionAfter101217Checkpoint(context,p);ZionCurrentBoundBody.Result? currentBody=null;
            await p.WaitUntilAsync(()=>
            {
                try{currentBody=ZionCurrentBoundBody.Require(p.World.CurrentOwnViewLife(),boundScope,owned,furnitureRoute);return true;}
                catch(InvalidDataException){return false;}
            },"native furniture current own-life bound body",TimeSpan.FromSeconds(15));
            if(furnitureRoute!=null)
            {
                var view=currentBody!.View;Check.That(view.Attributes.TryGetValue("Position",out var nativePosition)&&nativePosition.Length==24&&Enumerable.Range(0,3).All(i=>BitConverter.ToDouble(nativePosition,i*8)==furnitureRoute.Centre[i])&&
                    view.Attributes.TryGetValue("Orientation",out var nativeQuaternion)&&nativeQuaternion.SequenceEqual(Convert.FromHexString(furnitureRoute.Quaternion)),"Current body matches independent V3 profile/route pose");
                MissionBoundFurnitureRouteProof.RequireCurrent(furnitureRoute,await ZionAfter101217Checkpoint(context,p));
            }
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
        var after=await ZionAfter101217Checkpoint(context,p);var current=after.Objects.Single(o=>o.Id==objectId);
        Check.Equal(JsonSerializer.Serialize(new{obj.Id,obj.Goid,obj.Spot,obj.OpenFloor},new JsonSerializerOptions{IncludeFields=true}),JsonSerializer.Serialize(new{current.Id,current.Goid,current.Spot,current.OpenFloor},new JsonSerializerOptions{IncludeFields=true}),"Whole raw source/fixed DTO unchanged through ordinary ride/route movement");
        Check.Equal(JsonSerializer.Serialize(new{state.SiteDoors,state.OpenFloorSites,state.OpenFloorCurrentSiteIndex,state.Waypoints,state.Exits},new JsonSerializerOptions{IncludeFields=true}),JsonSerializer.Serialize(new{after.SiteDoors,after.OpenFloorSites,after.OpenFloorCurrentSiteIndex,after.Waypoints,after.Exits},new JsonSerializerOptions{IncludeFields=true}),"Entire fixed site history/current door tuple unchanged");
        note?.Invoke(new{Kind="Immutable source/fixed DTO",Source=obj.Spot,Witness=w.RawJson,body.ViewId,body.SpawnId});return body;
    }
    private static async Task<MissionBoundCabinetFixture.InventoryProof?> ZionAfter101217Inventory(ScenarioContext context,HeadlessPlayer p,uint item)
    {
        var state=await ZionAfter101217Checkpoint(context,p);string rows=await context.Database.ExecuteAsync($"SELECT charId,goid,slot,count FROM inventory WHERE charId={p.CharId} AND goid={item}");
        if(!state.Items.TryGetValue(item,out ushort slot)){Check.That(string.IsNullOrWhiteSpace(rows),"No duplicate/stale owned row outside current mission items");return null;}
        return MissionBoundCabinetFixture.RequireMissionInventory(rows,p.CharId,item,slot);
    }
    private static async Task ZionAfter101217CertifiedActorRoute(ScenarioContext context,HeadlessPlayer p,MissionProgress.SavedState state,MissionProgress.SavedMob actor,Action<object> note)
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
                int views=p.World.ViewSnapshot().Count,debug=context.Server.DebugLog().Length;string request=$"80 c8 {WireFormat.ToHex(BitConverter.GetBytes(key))} 03 00";p.SendRpc(request);
                await p.WaitUntilAsync(()=>{try{_=MissionBound326RouteProof.RequireDoorUse(context.Server.DebugLog(),debug,door);current=MissionBound326RouteProof.RequireOpen(p.World.CurrentOwnViewLife(),life,door);return true;}catch(InvalidDataException){return false;}},"current certified actor-route portal Open",TimeSpan.FromSeconds(15));
                p.SendRpc("81 48");await p.WaitForRpcAsync("81 49");_=MissionBound326RouteProof.RequireOpen(p.World.CurrentOwnViewLife(),life,door);Check.Equal(0,MissionPublicStreetWire.Teleports(p.World.CurrentOwnViewLife(),life,views).Count,"NPC route door Open is not an elevator ride");
                note(new{Kind="Reacquire certified portal Open",Door=door,Request=request,Current=current,NativePassageProven=false});
            }
            await MoveForMissionTest(p,point.Standing[0],point.Standing[1],point.Standing[2]);
        }
        var after=await ZionAfter101217Checkpoint(context,p);ZionAfter1212RidePlan.SameEntry(state,after);
        Check.That(after.Mobs.Any(m=>m.Npc==actor.Npc&&m.Identity==actor.Identity),"Reacquired actor retains exact saved identity");
        note(new{Kind="Certified actor reacquire route",state.Mission,state.Phase,Actor=actor.Npc,actor.Identity,RoleIndex=index,Route=route,MovementAid="Existing test movement to lobby/route points/latest actor; ordinary elevator and portal RPCs only",NativeWalkingProven=false});
    }

}
