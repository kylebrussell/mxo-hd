using hds;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Globalization;
namespace HeadlessClient;
public static partial class Scenarios
{
    private static readonly JsonSerializerOptions MervFollowupFields=new(){IncludeFields=true};
    private sealed record SweepBoundArrival(int Mission,int Phase,ushort Instance,double[] Root);
    private static async Task<MissionProgress.SavedState> MervFollowupCheckpoint(ScenarioContext context,HeadlessPlayer p)=>
        JsonSerializer.Deserialize<MissionProgress.SavedState>(await CarriedMissionNpcFixture.ReadOwnedAsync(context,p),MervFollowupFields)!;
    private static async Task<MissionProgress.SavedState?> MervFollowupCurrent(ScenarioContext context,HeadlessPlayer p)
    {
        string hex=await context.Database.ScalarAsync($"SELECT IFNULL(HEX(payload),'') FROM char_active_missions WHERE charId={p.CharId}");
        return MervFollowupCheckpointFixture.ParseHex(hex);
    }
    private static async Task MervFollowupWaitTransition(ScenarioContext context,HeadlessPlayer p,int mission,int phase)
    {
        var clock=System.Diagnostics.Stopwatch.StartNew();
        while(!MervFollowupApproachFixture.Transitioned(await MervFollowupCurrent(context,p),mission,phase))
        {
            if(clock.Elapsed>TimeSpan.FromSeconds(15))throw new ScenarioFailure("Owned mission/phase did not advance after ordinary departure");
            await Task.Delay(25); // Poll authoritative persistence; elapsed time never qualifies a transition.
        }
    }
    private static async Task MervFollowupWaitCompletion(ScenarioContext context,HeadlessPlayer p,int mission,int cursor)
    {
        var clock=System.Diagnostics.Stopwatch.StartNew();
        while(true)
        {
            var current=await MervFollowupCurrent(context,p);
            long receipts=await context.Database.LongAsync($"SELECT COALESCE(SUM(completions),0) FROM char_missions WHERE charId={p.CharId} AND missionId={mission}");
            Check.That(receipts<=1,"Completed mission cannot have duplicate earned receipts");
            bool wire=p.World.RpcSnapshot().Skip(cursor).Any(r=>BytePattern.Parse("80 a2 00").IsMatch(r.Bytes));
            if(MervFollowupApproachFixture.CompletionReady(current,receipts,wire))return;
            if(clock.Elapsed>TimeSpan.FromSeconds(15))throw new ScenarioFailure("Current mission completion lacks fresh summary, terminal payload or exactly one earned receipt");
            await Task.Delay(25); // Poll independent wire/SQL facts, never infer completion from elapsed time.
        }
    }
    private static async Task MervFollowupWaitEscortExit(ScenarioContext context,HeadlessPlayer p,MissionProgress.SavedState before,Mission authored,int goal,int cursor)
    {
        var clock=System.Diagnostics.Stopwatch.StartNew();
        while(true)
        {
            var current=await MervFollowupCurrent(context,p);
            long receipts=current==null?await context.Database.LongAsync($"SELECT COALESCE(SUM(completions),0) FROM char_missions WHERE charId={p.CharId} AND missionId={before.Mission}"):0;
            if(MervFollowupApproachFixture.EscortExitAdvanced(before,current,authored,goal,p.World.RpcSnapshot().Select(r=>r.Bytes).ToArray(),cursor,receipts))return;
            if(clock.Elapsed>TimeSpan.FromSeconds(15))throw new ScenarioFailure("Authored escort exit lacks fresh goal ACK and exact committed phase/terminal outcome");
            await Task.Delay(25);
        }
    }
    private static async Task MervFollowupRide(ScenarioContext context,HeadlessPlayer p,bool enter,List<object> evidence,int expectedMission,int expectedPhase,Mission? authoredExit=null,int? exitEscortGoal=null,MissionProgress.SavedState? exitOwner=null,int? exitIntentCursor=null,bool unfinishedOutside=false,Action? beforeRequest=null)
    {
        Check.That(!unfinishedOutside||!enter,"Unfinished outside trip is a downride only");
        var initialLife=p.World.CurrentOwnViewLife();
        int intent=exitIntentCursor??p.World.RpcSnapshot().Count;
        var saved=await MervFollowupCurrent(context,p);
        var before=exitOwner??saved;
        if(!enter&&!unfinishedOutside&&!MervFollowupApproachFixture.CanDepart(saved,expectedMission,expectedPhase,authoredExit,exitEscortGoal))
        {
            if(exitEscortGoal.HasValue)
            {
                Check.That(before!=null&&MervFollowupApproachFixture.OwnsWaitingPhase(before,expectedMission,expectedPhase),"Exit intent belongs to expected phase");
                await MervFollowupWaitEscortExit(context,p,before!,authoredExit!,exitEscortGoal.Value,intent);
                evidence.Add(new{Operation="authored-escort-completed-before-door",Mission=expectedMission,CompletedPhase=expectedPhase,IntentRpcCursor=intent,PhysicalTeleportObserved=false});return;
            }
            Check.That(MervFollowupApproachFixture.Transitioned(saved,expectedMission,expectedPhase),"Same phase requires waiting-for-exit or its exact pending authored escort exit");
            evidence.Add(new{Operation="legitimate-auto-advance-before-door",Mission=expectedMission,CompletedPhase=expectedPhase,ActualMission=saved?.Mission,ActualPhase=saved?.Phase});return;
        }
        Check.That(MervFollowupApproachFixture.OwnsWaitingPhase(saved,expectedMission,expectedPhase),"Door belongs to expected current mission/phase");
        var door=saved!.Waypoints.Single();var lobby=saved.Exits.Single();
        var authoredSite=Directory.GetFiles(Path.Combine(context.RepositoryRoot,"data","missions"),"merovingian_1_2_*.xml")
            .Select(System.Xml.Linq.XElement.Load).Single(m=>(int)m.Attribute("id")! == expectedMission);
        var sourceOrder=authoredSite.Elements("phase").ElementAt(expectedPhase).Elements("object").Select(o=>checked((ushort)(int)o.Attribute("id")!)).ToArray();
        var selectedRide=MervFollowupElevatorFixture.Select(saved,enter,new[]{"staticObjects_slums_1.csv","staticObjects_slums_2.csv"}.SelectMany(f=>File.ReadLines(Path.Combine(context.RepositoryRoot,"data",f))),sourceOrder);
        uint key=selectedRide.Key;var pose=selectedRide.Pose;var approach=selectedRide.Approach;
        await MoveForMissionTest(p,approach[0],approach[1],approach[2]);
        int log=context.Server.DebugLog().Length; var beforeArrival=p.World.CreationSnapshot();
        string packetPath=Path.Combine(context.Server.WorkingDirectory,"PacketLog.txt");
        int packetCursor=InteriorAdmissionDeliveryProof.Read(packetPath).Count(c=>c=='\n');
        if(!enter&&!unfinishedOutside)
        {
            var current=await MervFollowupCurrent(context,p);
            if(!MervFollowupApproachFixture.CanDepart(current,expectedMission,expectedPhase,authoredExit,exitEscortGoal))
            {
                if(exitEscortGoal.HasValue)
                {
                    await MervFollowupWaitEscortExit(context,p,before!,authoredExit!,exitEscortGoal.Value,intent);
                    evidence.Add(new{Operation="authored-escort-completed-during-door-approach",Mission=expectedMission,CompletedPhase=expectedPhase,IntentRpcCursor=intent,PhysicalTeleportObserved=false});return;
                }
                Check.That(MervFollowupApproachFixture.Transitioned(current,expectedMission,expectedPhase),"Same phase still requires the exact authoritative departure capability before sending door RPC");
                evidence.Add(new{Operation="legitimate-auto-advance-before-door-send",Mission=expectedMission,CompletedPhase=expectedPhase,ActualMission=current?.Mission,ActualPhase=current?.Phase});return;
            }
        }
        if(enter||unfinishedOutside)Check.That(MervFollowupElevatorFixture.SameEntry(saved,await MervFollowupCurrent(context,p)),
            "Current accepted entry/site/witness remains identical immediately before ordinary door request");
        var ownWorld=p.World.CurrentOwnViewLife();
        if(unfinishedOutside)Check.That(initialLife.Current&&ownWorld.Current&&ReferenceEquals(initialLife.Owner,ownWorld.Owner)&&initialLife.Life==ownWorld.Life,"Unfinished down request retains original current owner/life");
        int viewCursor=ownWorld.Events.Count==0?ownWorld.OwnBirthSection+1:ownWorld.Events.Max(e=>e.Section)+1;
        string request=$"80 c8 {WireFormat.ToHex(BitConverter.GetBytes(key))} 03 00";beforeRequest?.Invoke();p.SendRpc(request);
        if(!enter&&exitEscortGoal.HasValue)await MervFollowupWaitEscortExit(context,p,before!,authoredExit!,exitEscortGoal.Value,intent);
        else if(door.y!=lobby[1])await p.WaitUntilAsync(()=>context.Server.DebugLog()[log..].Contains("[MISSION] Teleported to "),"ordinary saved-door elevator ride",TimeSpan.FromSeconds(15));
        evidence.Add(new{Operation="ordinary-door",saved.Mission,saved.Phase,Enter=enter,DoorKey=key,Request=request,AuthoredExitEscort=exitEscortGoal,IntentRpcCursor=intent,PhysicalTeleportObserved=context.Server.DebugLog()[log..].Contains("[MISSION] Teleported to "),Waypoint=door,Lobby=lobby,RideLog=context.Server.DebugLog()[log..]});
        if(selectedRide.Measured)
        {
            MervFollowupElevatorFixture.DoorObservation? currentDoor=null;
            await p.WaitUntilAsync(()=>
            {
                try{currentDoor=MervFollowupElevatorFixture.CurrentOverlay(p.World.CurrentOwnViewLife(),selectedRide);return true;}
                catch(InvalidDataException){return false;}
            },"Exact current-life measured door key/pose/quaternion",TimeSpan.FromSeconds(15));
            var overlay=currentDoor!.View;
            var arrival=Regex.Matches(context.Server.DebugLog()[log..],@"\[MISSION\] Teleported to ([-\d.]+),([-\d.]+),([-\d.]+)").LastOrDefault();
            Check.That(selectedRide.Npc==null||arrival!=null,"Owned NPC entry requires the ordinary actual ride");
            if(arrival!=null)
            {
                var actual=Enumerable.Range(1,3).Select(i=>double.Parse(arrival.Groups[i].Value,CultureInfo.InvariantCulture)).ToArray();
                SectorProperties.Load(Path.Combine(context.RepositoryRoot,"data","sector_properties.csv"));NavMesh.UseDirectory(Path.Combine(context.RepositoryRoot,"data","navmesh"));
                Check.That(NavMesh.Locate(selectedRide.District,actual[0],actual[1],actual[2],out var nav),"Measured actual arrival has NAV");
                MervFollowupElevatorFixture.RequireRoot(selectedRide,enter,actual,nav.sector,nav.region,nav.floorY,
                    saved.Objects.Where(o=>o.OpenFloor!=null&&o.OpenFloor.SourceGoid!=o.Goid).Select(o=>o.OpenFloor!.Route.Take(3).ToArray()));
                NpcInteriorComputerState.Teleport? npcArrival=null;
                if(selectedRide.Npc!=null||selectedRide.Street!=null)
                    await p.WaitUntilAsync(()=>{try{npcArrival=NpcInteriorComputerState.FreshTeleport(p.World.CurrentOwnViewLife(),ownWorld,viewCursor,actual);return true;}catch(InvalidDataException){return false;}},
                        "Fresh ordinary arrival at the owned NPC Standing",TimeSpan.FromSeconds(15));
                evidence.Add(new{Operation="selected-measured-elevator-root",Key=key,ActualRoot=actual,nav.sector,nav.region,nav.floorY,selectedRide.Witness,NpcWitness=selectedRide.Npc?.Raw,NpcArrival=npcArrival});
            }
            evidence.Add(new{Operation="selected-measured-elevator-overlay",Key=key,Pose=pose,selectedRide.Witness,overlay.ViewId,overlay.SpawnId,currentDoor.OwnLife,currentDoor.BirthSection,ObservedCurrentAfterRequest=true,NewCreationNotRequired=true});
        }
        if(enter&&MervAfterFurnitureApproach.NeedsArrival(saved))
        {
            var current=await MervFollowupCheckpoint(context,p);
            var selection=MervFollowupStreetEntry.ForSite(current)??throw new ScenarioFailure("Missing current V3 car context");
            var expected=selection.Floor.Standing!;
            NpcInteriorComputerState.Teleport? typed=null;
            await p.WaitUntilAsync(()=>{try{typed=NpcInteriorComputerState.FreshTeleport(p.World.CurrentOwnViewLife(),ownWorld,viewCursor,expected);return true;}catch(InvalidDataException){return false;}},"Fresh V3 ordinary car arrival",TimeSpan.FromSeconds(15));
            MissionMidphaseReturnFixture.Arrival? receipt=null;
            await p.WaitUntilAsync(()=>{try{receipt=MissionMidphaseReturnFixture.RequireArrival(InteriorAdmissionDeliveryProof.Read(packetPath),p.CharId,packetCursor,typed!);return true;}catch(InvalidDataException){return false;}},"V3 ordinary arrival transport receipt",TimeSpan.FromSeconds(10));
            var trip=Regex.Matches(context.Server.DebugLog()[log..],@"\[MISSION\] Teleported to ([-\d.]+),([-\d.]+),([-\d.]+)").LastOrDefault();
            Check.That(trip!=null,"V3 car has actual physical trip");
            var actual=Enumerable.Range(1,3).Select(i=>double.Parse(trip!.Groups[i].Value,CultureInfo.InvariantCulture)).ToArray();
            Check.That(NavMesh.Locate(selectedRide.District,actual[0],actual[1],actual[2],out var nav),"Actual V3 car Root has NAV");
            evidence.Add(MervAfterFurnitureApproach.Capture(context.RepositoryRoot,current,actual,nav,p.World.CurrentOwnViewLife()));
            evidence.Add(new{Operation="furniture-v3-ordinary-arrival",Arrival=typed,Receipt=receipt,RendererReadyProven=false});
        }
        if(enter&&!MervAfterFurnitureApproach.NeedsArrival(saved)&&MervBound326Approach.NeedsArrival(saved))
        {
            var actualArrival=Regex.Matches(context.Server.DebugLog()[log..],@"\[MISSION\] Teleported to ([-\d.]+),([-\d.]+),([-\d.]+)").LastOrDefault();
            if(actualArrival!=null)
            {
                var actual=Enumerable.Range(1,3).Select(i=>double.Parse(actualArrival.Groups[i].Value,CultureInfo.InvariantCulture)).ToArray();
                SectorProperties.Load(Path.Combine(context.RepositoryRoot,"data","sector_properties.csv"));NavMesh.UseDirectory(Path.Combine(context.RepositoryRoot,"data","navmesh"));
                if(NavMesh.Locate(selectedRide.District,actual[0],actual[1],actual[2],out var actualNav))
                    evidence.Add(MervBound326Approach.Capture(saved,actual,actualNav,p.World.CurrentOwnViewLife(),selectedRide.Measured));
            }
        }
        if(!enter&&!unfinishedOutside)
        {
            await MervFollowupWaitTransition(context,p,expectedMission,expectedPhase);
            var current=await MervFollowupCurrent(context,p);
            evidence.Add(new{Operation="owned-phase-advanced-after-ordinary-exit",Mission=expectedMission,CompletedPhase=expectedPhase,ActualMission=current?.Mission,ActualPhase=current?.Phase});
        }
        if(enter)
        {
            var free=saved.Objects.Where(o=>o.OpenFloor!=null&&o.OpenFloor.SourceGoid!=o.Goid).ToArray();
            var pending=free.Where(o=>MervFollowupApproachFixture.CurrentBody(beforeArrival,o.Goid,o.OpenFloor!.Centre,o.OpenFloor.Quaternion,saved.Instance)==null).ToArray();
            foreach(var o in free.Except(pending))evidence.Add(new{Operation="current-free-body-already-present",saved.Mission,saved.Phase,o.Id,PriorArrivalInterestAllowed=true});
            if(pending.Length>0)
            {
                var witnesses=pending.Select(o=>MissionOpenFloorWitness.Read(JsonSerializer.SerializeToElement(o.OpenFloor,MervFollowupFields))).ToArray();
                var root=witnesses[0].Route.Take(3).ToArray();
                foreach(var w in witnesses)Check.That(w.Route.Take(3).SequenceEqual(root),"Current floor's free props share their published entry root");
                bool rode=door.y!=lobby[1];
                if(rode)
                {
                    var committed=Regex.Matches(context.Server.DebugLog()[log..],@"\[MISSION\] Teleported to ([-\d.]+),([-\d.]+),([-\d.]+)").Last();
                    var actual=Enumerable.Range(1,3).Select(i=>double.Parse(committed.Groups[i].Value,CultureInfo.InvariantCulture)).ToArray();
                    Check.That(actual.SequenceEqual(root),"Actual ordinary Ride reaches the current published root");
                }
                else
                {
                    await MoveForMissionTest(p,root[0],root[1],root[2]);
                    p.World.SendViewList(MervFollowupApproachFixture.ClientPosition(root));
                }
                // First creation follows current arrival interest; never wait for it before the ride.
                // The ordinary ride path stays stationary here: no client position/movement can replace its ACK.
                await p.WaitUntilAsync(()=>pending.All(o=>MervFollowupApproachFixture.CurrentBody(p.World.CreationSnapshot(),o.Goid,o.OpenFloor!.Centre,o.OpenFloor.Quaternion,saved.Instance)!=null),"Current free bodies after actual floor arrival",TimeSpan.FromSeconds(15));
                foreach(var o in pending)
                {
                    object? receipt=rode?InteriorAdmissionDeliveryProof.Require(InteriorAdmissionDeliveryProof.Read(packetPath),p.CharId,packetCursor,root,o.Goid,o.OpenFloor!.Centre):null;
                    evidence.Add(new{Operation="current-free-body-first-introduction",saved.Mission,saved.Phase,o.Id,Source=o.Spot.mxoStaticId,Root=root,Stationary=rode,TransportReceipt=receipt,ClientPositionInterestOnly=!rode,RendererReadyProven=false});
                }
            }
            var area=Regex.Matches(context.Server.DebugLog(),@"\[MISSION\] Phase \d+ at a .* floor in .* \(([-\d.]+),([-\d.]+),([-\d.]+)\), elevator ").Last();
            double D(int n)=>double.Parse(area.Groups[n].Value,CultureInfo.InvariantCulture);
            await MoveForMissionTest(p,D(1),D(2),D(3)); // Current phase only; test movement, not native walking proof.
        }
    }
    private static async Task<ViewCreation> MervFollowupObject(ScenarioContext context,HeadlessPlayer p,int id,int cursor,List<object> evidence)
    {
        var saved=await MervFollowupCheckpoint(context,p);var owned=saved.Objects.Single(o=>o.Id==id);
        string Raw(MissionProgress.SavedObject o)=>JsonSerializer.Serialize(new{o.Id,o.Spot,o.Goid,o.OpenFloor},MervFollowupFields);
        string original=Raw(owned);double[] centre;string quaternion;string qualification;MissionBound326RouteProof.Plan? committedRoute=null;MissionBoundFurnitureRouteProof.Plan? furnitureRoute=null;
        if(owned.OpenFloor!=null)
        {
            var w=MissionOpenFloorWitness.Read(JsonSerializer.SerializeToElement(owned.OpenFloor,MervFollowupFields));
            w.Require(saved.Mission,saved.Phase,id,owned.Spot.mxoStaticId,MissionSites.Goid(owned.Spot),owned.Goid);
            centre=w.Centre.ToArray();quaternion=w.Quaternion;
            Check.That(ContainerActions.InReach(w.Standing.SelectMany(BitConverter.GetBytes).ToArray(),centre[0],centre[1],centre[2]),"Published Standing remains in ordinary use range");
            for(int i=0;i<w.Route.Count;i+=3)await MoveForMissionTest(p,w.Route[i],w.Route[i+1],w.Route[i+2]);
            qualification="current persisted Root/Route/Standing; native collision/picking unproven";
        }
        else
        {
            Check.That(!MervFollowupApproachFixture.RequiresWitness(owned)&&MissionSites.Goid(owned.Spot)==owned.Goid,"Only genuinely bound sources may lack an adopted witness");
            SectorProperties.Load(Path.Combine(context.RepositoryRoot,"data","sector_properties.csv"));NavMesh.UseDirectory(Path.Combine(context.RepositoryRoot,"data","navmesh"));
            furnitureRoute=MissionBoundFurnitureRouteProof.SupportsGoid(owned.Goid)?MissionBoundFurnitureRouteProof.Read(context.RepositoryRoot,saved,id):null;
            var pose=furnitureRoute==null?MissionObjectPoses.For(new MissionObjectState{spot=owned.Spot,goid=owned.Goid}):default;
            centre=furnitureRoute==null?new[]{pose.X,pose.Y,pose.Z}:furnitureRoute.Centre;quaternion=furnitureRoute==null?Convert.ToHexString(pose.Orientation):furnitureRoute.Quaternion;
            double[]? standing=null;
            if(furnitureRoute!=null)
            {
                var route=furnitureRoute;var arrival=evidence.OfType<MervAfterFurnitureApproach.Arrival>().LastOrDefault()??throw new ScenarioFailure("No actual V3 ordinary arrival");
                centre=route.Centre;quaternion=route.Quaternion;
                async Task SameFurniture(){MervAfterFurnitureApproach.Require(route,await MervFollowupCheckpoint(context,p),arrival,p.World.CurrentOwnViewLife());}
                foreach(var step in MissionBoundFurnitureRouteProof.Steps(context.RepositoryRoot,route))
                {
                    await SameFurniture();
                    foreach(uint key in step.Doors)
                    {
                        var door=route.Doors.Single(d=>d.Key==key);var requestLife=p.World.CurrentOwnViewLife();int sections=p.World.ViewSnapshot().Count;
                        await SameFurniture();int debugCursor=context.Server.DebugLog().Length;
                        p.SendRpc($"80 c8 {WireFormat.ToHex(BitConverter.GetBytes(key))} 03 00");
                        await p.WaitUntilAsync(()=>{try{_=MissionBound326RouteProof.RequireDoorUse(context.Server.DebugLog(),debugCursor,door);_=MissionBound326RouteProof.RequireOpen(p.World.CurrentOwnViewLife(),requestLife,door);return true;}catch(InvalidDataException){return false;}},"V3 saved own-GO door current Open",TimeSpan.FromSeconds(15));
                        p.SkipReceived();p.SendRpc("81 48");await p.WaitForRpcAsync("81 49");await SameFurniture();
                        _=MissionBound326RouteProof.RequireOpen(p.World.CurrentOwnViewLife(),requestLife,door);
                        Check.Equal(0,MissionPublicStreetWire.Teleports(p.World.CurrentOwnViewLife(),requestLife,sections).Count,"Internal Open is not a car trip");
                        evidence.Add(new{Operation="furniture-v3-required-door",Door=door,DebugCursor=debugCursor,ExactPacketAckProven=false,NativePassageProven=false});
                    }
                    await SameFurniture();await MoveForMissionTest(p,step.Standing[0],step.Standing[1],step.Standing[2]);
                }
                standing=route.Standing;await SameFurniture();
                evidence.Add(new{Operation="fixed-furniture-v3-route",saved.Mission,saved.Phase,Object=id,Route=route.Route,NativeCollisionClearanceProven=false});
            }
            else if(owned.Goid==326)
            {
                var arrival=evidence.OfType<MervBound326Approach.Arrival>().LastOrDefault()??throw new ScenarioFailure("No current actual arrival context for bound326");
                var bounds=MissionBound326Approach.Load(context.RepositoryRoot,owned);
                MissionBound326Approach.Sample? Locate(double[] point)=>NavMesh.Locate(1,point[0],point[1],point[2],out var at)?new(point,at.sector,at.region,at.polygon,at.floorY):null;
                var route=MissionBound326RouteProof.Read(saved,id,bounds,new(arrival.Root,arrival.Sector,arrival.Region,arrival.FloorY),Locate);
                if(route!=null)
                {
                    committedRoute=route;
                    var life=p.World.CurrentOwnViewLife();
                    async Task SameRoute(){MissionBound326RouteProof.RequireCurrent(route,await MervFollowupCheckpoint(context,p));Check.That(life.Current,"Bound route own life remains current");}
                    foreach(var step in MissionBound326RouteProof.Steps(context.RepositoryRoot,route))
                    {
                        await SameRoute();
                        foreach(uint key in step.Doors)
                        {
                            var door=route.Doors.Single(d=>d.Key==key);var requestLife=p.World.CurrentOwnViewLife();
                            Check.That(requestLife.Current&&requestLife.OwnViewReady,"Current own life before required portal Open");
                            int debugCursor=context.Server.DebugLog().Length;p.SendRpc($"80 c8 {WireFormat.ToHex(BitConverter.GetBytes(key))} 03 00");
                            await p.WaitUntilAsync(()=>{try{var now=p.World.CurrentOwnViewLife();if(!ReferenceEquals(now.Owner,requestLife.Owner)||now.Life!=requestLife.Life||!requestLife.Current)return false;_=MissionBound326RouteProof.RequireDoorUse(context.Server.DebugLog(),debugCursor,door);_=MissionBound326RouteProof.RequireOpen(now,requestLife,door);return true;}catch(InvalidDataException){return false;}},"Owned route door actual use and Open",TimeSpan.FromSeconds(15));
                            p.SkipReceived();p.SendRpc("81 48");await p.WaitForRpcAsync("81 49");await SameRoute();
                            _=MissionBound326RouteProof.RequireOpen(p.World.CurrentOwnViewLife(),requestLife,door);
                            evidence.Add(new{Operation="bound-route-portal",Door=door,DebugCursor=debugCursor,ExactPacketAckProven=false,NativePassageProven=false});
                        }
                        await SameRoute();await MoveForMissionTest(p,step.Standing[0],step.Standing[1],step.Standing[2]);
                    }
                    standing=route.Standing;await SameRoute();
                    evidence.Add(new{Operation="fixed-bound326-route",saved.Mission,saved.Phase,Object=id,Route=route.Route,NativeCollisionClearanceProven=false});
                }
                else
                {
                bool measured=MervBound326Approach.Classify(saved)==MervBound326Approach.ProofScope.Measured;
                var result=measured?MervBound326Approach.Choose(context.RepositoryRoot,saved,id,arrival,p.World.CurrentOwnViewLife()):
                    MervBound326Approach.ChooseLegacy(context.RepositoryRoot,saved,id,arrival,p.World.CurrentOwnViewLife());
                standing=result.Standing;evidence.Add(new{Operation="native326-NAV-approach",saved.Mission,saved.Phase,Object=id,Source=owned.Spot.mxoStaticId,Arrival=arrival,Result=result,Legacy=!measured,MeasuredAuthority=measured});
                }
            }
            else if (evidence.OfType<SweepBoundArrival>().LastOrDefault(a=>a.Mission==saved.Mission&&a.Phase==saved.Phase&&a.Instance==saved.Instance) is { } arrival &&
                MissionObjectPoses.TryAuditedBinding(owned.Spot, owned.Goid, out var native))
            {
                Check.That(MissionObjectPoses.TryBoundStanding(owned.Spot.metrId,owned.Goid,native,arrival.Root,out var at),
                    "Audited bound source requires a same-room NAV approach from the ordinary arrival");
                standing=new[]{at.x,at.y,at.z};
            }
            else
            {
                foreach(var d in new[]{(100d,0d),(-100d,0d),(0d,100d),(0d,-100d),(0d,0d)})
                    if(NavMesh.Locate(owned.Spot.metrId,pose.X+d.Item1,pose.Y,pose.Z+d.Item2,out var at))
                    {
                        var candidate=new[]{pose.X+d.Item1,at.floorY+95,pose.Z+d.Item2};
                        if(ContainerActions.InReach(candidate.SelectMany(BitConverter.GetBytes).ToArray(),pose.X,pose.Y,pose.Z)){standing=candidate;break;}
                    }
            }
            Check.That(standing!=null,"Bound source requires a NAV-grounded in-range approach");
            await MoveForMissionTest(p,standing![0],standing[1],standing[2]);qualification=furnitureRoute!=null?"V3 fixed native-profile path/Standing with actual ordinary car arrival and required own-GO Open; movement aids are not native walking":owned.Goid==326&&MervBound326Approach.Classify(saved)==MervBound326Approach.ProofScope.Legacy?
                "legacy bound326 actual-arrival/native100/NAV-component approach; no measured witness, direct route/collision/picking proof":
                owned.Goid==326?"measured bound326 native100/NAV-component witness; no direct route/collision/picking proof":
                "bound source NAV/range only; no route/collision/picking proof";
        }
        ViewCreation? Current()=>MervFollowupApproachFixture.CurrentBody(p.World.CreationSnapshot(),owned.Goid,centre,quaternion,saved.Instance);
        await p.WaitUntilAsync(()=>Current()!=null,"Current saved object XYZ/quaternion/owned-instance view",TimeSpan.FromSeconds(12));
        var after=await MervFollowupCheckpoint(context,p);
        Check.Equal(saved.Mission,after.Mission,"Approach retains selected mission");Check.Equal(saved.Phase,after.Phase,"Approach retains current phase");
        Check.Equal(original,Raw(after.Objects.Single(o=>o.Id==id)),"Approach retains exact raw source and fixed witness DTO");
        if(furnitureRoute!=null)MervAfterFurnitureApproach.Require(furnitureRoute,after,evidence.OfType<MervAfterFurnitureApproach.Arrival>().Last(),p.World.CurrentOwnViewLife());
        else if(owned.Goid==326&&owned.OpenFloor==null)
        {
            if(committedRoute!=null)MissionBound326RouteProof.RequireCurrent(committedRoute,after);
            else
            {
            if(MervBound326Approach.Classify(saved)==MervBound326Approach.ProofScope.Measured)MervBound326Approach.RequireScope(after,id,evidence.OfType<MervBound326Approach.Arrival>().Last(),p.World.CurrentOwnViewLife());
            else MervBound326Approach.RequireLegacyScope(after,id,evidence.OfType<MervBound326Approach.Arrival>().LastOrDefault(),p.World.CurrentOwnViewLife());
            }
        }
        evidence.Add(new{Operation="current-object-approach",saved.Mission,saved.Phase,Object=id,SourceAndWitness=original,Centre=centre,Quaternion=quaternion,Qualification=qualification});
        return Current()!;
    }
}
