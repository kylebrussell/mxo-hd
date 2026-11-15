using hds;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Globalization;
namespace HeadlessClient;
public static partial class Scenarios
{
    private static readonly JsonSerializerOptions MervFollowupFields=new(){IncludeFields=true};
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
    private static async Task MervFollowupRide(ScenarioContext context,HeadlessPlayer p,bool enter,List<object> evidence,int expectedMission,int expectedPhase,Mission? authoredExit=null,int? exitEscortGoal=null,MissionProgress.SavedState? exitOwner=null,int? exitIntentCursor=null)
    {
        int intent=exitIntentCursor??p.World.RpcSnapshot().Count;
        var saved=await MervFollowupCurrent(context,p);
        var before=exitOwner??saved;
        if(!enter&&!MervFollowupApproachFixture.CanDepart(saved,expectedMission,expectedPhase,authoredExit,exitEscortGoal))
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
        uint key=selectedRide.Key;var pose=selectedRide.Pose;await MoveForMissionTest(p,pose[0],pose[1]-50,pose[2]);
        int log=context.Server.DebugLog().Length; var beforeArrival=p.World.CreationSnapshot();
        string packetPath=Path.Combine(context.Server.WorkingDirectory,"PacketLog.txt");
        int packetCursor=InteriorAdmissionDeliveryProof.Read(packetPath).Count(c=>c=='\n');
        if(!enter)
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
        if(enter)Check.That(MervFollowupElevatorFixture.SameEntry(saved,await MervFollowupCurrent(context,p)),
            "Current accepted entry/site/witness remains identical immediately before ordinary door request");
        string request=$"80 c8 {WireFormat.ToHex(BitConverter.GetBytes(key))} 03 00";p.SendRpc(request);
        if(!enter&&exitEscortGoal.HasValue)await MervFollowupWaitEscortExit(context,p,before!,authoredExit!,exitEscortGoal.Value,intent);
        else if(door.y!=lobby[1])await p.WaitUntilAsync(()=>context.Server.DebugLog()[log..].Contains("[MISSION] Teleported to "),"ordinary saved-door elevator ride",TimeSpan.FromSeconds(15));
        evidence.Add(new{Operation="ordinary-door",saved.Mission,saved.Phase,Enter=enter,DoorKey=key,Request=request,AuthoredExitEscort=exitEscortGoal,IntentRpcCursor=intent,PhysicalTeleportObserved=context.Server.DebugLog()[log..].Contains("[MISSION] Teleported to "),Waypoint=door,Lobby=lobby,RideLog=context.Server.DebugLog()[log..]});
        if(selectedRide.Witness!=null)
        {
            MervFollowupElevatorFixture.DoorObservation? currentDoor=null;
            await p.WaitUntilAsync(()=>
            {
                try{currentDoor=MervFollowupElevatorFixture.CurrentOverlay(p.World.CurrentOwnViewLife(),selectedRide);return true;}
                catch(InvalidDataException){return false;}
            },"Exact current-life measured door key/pose/quaternion",TimeSpan.FromSeconds(15));
            var overlay=currentDoor!.View;
            var arrival=Regex.Matches(context.Server.DebugLog()[log..],@"\[MISSION\] Teleported to ([-\d.]+),([-\d.]+),([-\d.]+)").LastOrDefault();
            if(arrival!=null)
            {
                var actual=Enumerable.Range(1,3).Select(i=>double.Parse(arrival.Groups[i].Value,CultureInfo.InvariantCulture)).ToArray();
                SectorProperties.Load(Path.Combine(context.RepositoryRoot,"data","sector_properties.csv"));NavMesh.UseDirectory(Path.Combine(context.RepositoryRoot,"data","navmesh"));
                Check.That(NavMesh.Locate(selectedRide.District,actual[0],actual[1],actual[2],out var nav),"Measured actual arrival has NAV");
                MervFollowupElevatorFixture.RequireRoot(selectedRide,enter,actual,nav.sector,nav.region,nav.floorY,
                    saved.Objects.Where(o=>o.OpenFloor!=null&&o.OpenFloor.SourceGoid!=o.Goid).Select(o=>o.OpenFloor!.Route.Take(3).ToArray()));
                evidence.Add(new{Operation="selected-measured-elevator-root",Key=key,ActualRoot=actual,nav.sector,nav.region,nav.floorY,selectedRide.Witness});
            }
            evidence.Add(new{Operation="selected-measured-elevator-overlay",Key=key,Pose=pose,selectedRide.Witness,overlay.ViewId,overlay.SpawnId,currentDoor.OwnLife,currentDoor.BirthSection,ObservedCurrentAfterRequest=true,NewCreationNotRequired=true});
        }
        if(enter)
        {
            var actualArrival=Regex.Matches(context.Server.DebugLog()[log..],@"\[MISSION\] Teleported to ([-\d.]+),([-\d.]+),([-\d.]+)").LastOrDefault();
            if(actualArrival!=null)
            {
                var actual=Enumerable.Range(1,3).Select(i=>double.Parse(actualArrival.Groups[i].Value,CultureInfo.InvariantCulture)).ToArray();
                SectorProperties.Load(Path.Combine(context.RepositoryRoot,"data","sector_properties.csv"));NavMesh.UseDirectory(Path.Combine(context.RepositoryRoot,"data","navmesh"));
                if(NavMesh.Locate(selectedRide.District,actual[0],actual[1],actual[2],out var actualNav))
                    evidence.Add(MervBound326Approach.Capture(saved,actual,actualNav,p.World.CurrentOwnViewLife()));
            }
        }
        if(!enter)
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
        string original=Raw(owned);double[] centre;string quaternion;string qualification;
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
            var pose=MissionObjectPoses.For(new MissionObjectState{spot=owned.Spot,goid=owned.Goid});centre=new[]{pose.X,pose.Y,pose.Z};quaternion=Convert.ToHexString(pose.Orientation);
            double[]? standing=null;
            if(owned.Goid==326)
            {
                var arrival=evidence.OfType<MervBound326Approach.Arrival>().LastOrDefault()??throw new ScenarioFailure("No current actual arrival context for bound326");
                var result=MervBound326Approach.Choose(context.RepositoryRoot,saved,id,arrival,p.World.CurrentOwnViewLife());
                standing=result.Standing;evidence.Add(new{Operation="native326-NAV-approach",saved.Mission,saved.Phase,Object=id,Source=owned.Spot.mxoStaticId,Arrival=arrival,Result=result});
            }
            else foreach(var d in new[]{(100d,0d),(-100d,0d),(0d,100d),(0d,-100d),(0d,0d)})
                if(NavMesh.Locate(owned.Spot.metrId,pose.X+d.Item1,pose.Y,pose.Z+d.Item2,out var at))
                {
                    var candidate=new[]{pose.X+d.Item1,at.floorY+95,pose.Z+d.Item2};
                    if(ContainerActions.InReach(candidate.SelectMany(BitConverter.GetBytes).ToArray(),pose.X,pose.Y,pose.Z)){standing=candidate;break;}
                }
            Check.That(standing!=null,"Bound source requires a NAV-grounded in-range approach");
            await MoveForMissionTest(p,standing![0],standing[1],standing[2]);qualification="bound source NAV/range only; no route/collision/picking proof";
        }
        ViewCreation? Current()=>MervFollowupApproachFixture.CurrentBody(p.World.CreationSnapshot(),owned.Goid,centre,quaternion,saved.Instance);
        await p.WaitUntilAsync(()=>Current()!=null,"Current saved object XYZ/quaternion/owned-instance view",TimeSpan.FromSeconds(12));
        var after=await MervFollowupCheckpoint(context,p);
        Check.Equal(saved.Mission,after.Mission,"Approach retains selected mission");Check.Equal(saved.Phase,after.Phase,"Approach retains current phase");
        Check.Equal(original,Raw(after.Objects.Single(o=>o.Id==id)),"Approach retains exact raw source and fixed witness DTO");
        if(owned.Goid==326&&owned.OpenFloor==null)MervBound326Approach.RequireScope(after,id,evidence.OfType<MervBound326Approach.Arrival>().Last(),p.World.CurrentOwnViewLife());
        evidence.Add(new{Operation="current-object-approach",saved.Mission,saved.Phase,Object=id,SourceAndWitness=original,Centre=centre,Quaternion=quaternion,Qualification=qualification});
        return Current()!;
    }
}
