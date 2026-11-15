using hds;
namespace HeadlessClient;
/// <summary>QA-only selection of the already persisted current mission elevator; no new key/pose policy.</summary>
public static class MervFollowupElevatorFixture
{
    public sealed record Ride(ushort District,uint Key,double[] Pose,string? Quaternion,MissionElevatorWitness? Witness);
    static bool Point(double[]? p)=>p?.Length==3&&p.All(double.IsFinite);
    static bool Same(double[]? a,double[]? b)=>Point(a)&&Point(b)&&a!.SequenceEqual(b!);
    static bool Quaternion(string? q){try{return q!=null&&Convert.FromHexString(q).Length==16;}catch(FormatException){return false;}}
    public static Ride Select(MissionProgress.SavedState state,bool enter,IEnumerable<string> legacyRows,IReadOnlyList<ushort>? objectOrder=null)
    {
        if(state.Waypoints.Length!=1||state.Exits.Length!=1)throw new InvalidDataException("One current mission door/lobby required");
        var door=state.Waypoints[0];var floor=new[]{door.x,door.y,door.z};var lobby=state.Exits[0];
        uint floorKey;try{var bytes=Convert.FromHexString(door.doorHex);if(bytes.Length!=4)throw new FormatException();floorKey=BitConverter.ToUInt32(bytes);}catch(Exception e)when(e is FormatException or ArgumentNullException){throw new InvalidDataException("Invalid current floor key",e);}
        if(!Point(floor)||!Point(lobby)||floorKey==0)throw new InvalidDataException("Invalid current door/lobby pose");
        MissionOpenFloorSite? selected=null;int index=state.SiteDoors.Length-1;
        if(state.Version==2)
        {
            index=state.OpenFloorCurrentSiteIndex??-1;
            if(state.OpenFloorSites==null||index<0||index>=state.OpenFloorSites.Length)throw new InvalidDataException("Missing selected site identity");
            selected=state.OpenFloorSites[index];
            if(selected==null||string.IsNullOrWhiteSpace(selected.Fingerprint)||selected.DoorKey!=floorKey)throw new InvalidDataException("Wrong selected floor identity");
        }
        else if(state.Version!=1||state.ElevatorWitnesses!=null)throw new InvalidDataException("Invalid legacy elevator metadata");
        if(index<0||index>=state.SiteDoors.Length)throw new InvalidDataException("Missing raw current site door");
        var raw=state.SiteDoors[index];
        if(raw==null||raw.mxoStaticId!=floorKey||!new ushort[]{417,419}.Contains(MissionSites.Goid(raw))||
            selected!=null&&(selected.District!=raw.metrId||selected.DoorX!=raw.pos_x||selected.DoorY!=raw.pos_y||selected.DoorZ!=raw.pos_z))
            throw new InvalidDataException("Current raw site identity differs");
        if(raw.metrId!=1||selected!=null&&selected.District!=1||state.Objects.Any(o=>o.Spot?.metrId!=1||o.OpenFloor!=null&&o.OpenFloor.World!=1))
            throw new InvalidDataException("Fixture world/selected site/raw object/open-floor world differs");
        var matches=(state.ElevatorWitnesses??Array.Empty<MissionElevatorWitness>()).Where(v=>v!=null&&v.SiteFingerprint==selected?.Fingerprint).ToArray();
        if(matches.Length>1)throw new InvalidDataException("Ambiguous selected elevator witness");
        if(matches.Length==1)
        {
            var measured=matches[0];
            if(measured.Version!=1||measured.FloorKey!=floorKey||measured.LobbyKey==0||measured.Sources==null||measured.Sources.Length==0||measured.Sources.Any(string.IsNullOrWhiteSpace)||
                !Same(measured.Floor,floor)||!Same(measured.Lobby,lobby)||!Quaternion(measured.FloorQuaternion)||!Quaternion(measured.LobbyQuaternion))
                throw new InvalidDataException("Selected measured elevator identity/pose differs");
            var fields=new System.Text.Json.JsonSerializerOptions{IncludeFields=true};
            foreach(string source in measured.Sources)
            {
                StaticWorldObject? original;try{original=System.Text.Json.JsonSerializer.Deserialize<StaticWorldObject>(source,fields);}catch(System.Text.Json.JsonException e){throw new InvalidDataException("Malformed selected elevator raw source",e);}
                // Same raw shape as244 ValidSpot/MatchesChoice, plus this fixture's world1.
                if(original==null||original.metrId!=1||original.type==null||(original.type.Length!=2&&original.type.Length!=4)||
                    !new[]{original.pos_x,original.pos_y,original.pos_z,original.rot}.All(double.IsFinite)||original.quat==null)
                    throw new InvalidDataException("Selected historical elevator source raw/world contract differs");
            }
            // Mirror244 ValidateRestored: historical selected witnesses need not equal a later NPC-only/current object set.
            if(state.OpenFloorPhase==state.Phase)
            {
                if(objectOrder==null||objectOrder.Count!=state.Objects.Count||objectOrder.Distinct().Count()!=objectOrder.Count||objectOrder.Any(id=>state.Objects.Count(o=>o.Id==id)!=1))
                    throw new InvalidDataException("Exact authored current object order required");
                var sources=objectOrder.Select(id=>System.Text.Json.JsonSerializer.Serialize(state.Objects.Single(o=>o.Id==id).Spot,fields));
                if(!measured.Sources.SequenceEqual(sources))throw new InvalidDataException("Current elevator sources differ from exact244 selected source signatures");
            }
            return new(raw.metrId,enter?measured.LobbyKey:measured.FloorKey,enter?measured.Lobby:measured.Floor,
                enter?measured.LobbyQuaternion:measured.FloorQuaternion,measured);
        }
        // Missing measured metadata cannot fall back using corrected coordinates or another raw CSV door.
        if(!Same(floor,new[]{raw.pos_x,raw.pos_y,raw.pos_z})||lobby[0]!=raw.pos_x||lobby[2]!=raw.pos_z)
            throw new InvalidDataException("Nonlegacy pose lacks selected elevator witness");
        return new(raw.metrId,enter?MervFollowupApproachFixture.DoorAt(legacyRows,lobby):floorKey,enter?lobby:floor,null,null);
    }
    public static bool SameEntry(MissionProgress.SavedState before,MissionProgress.SavedState? current)
    {
        if(current==null||before.Version!=current.Version||before.Mission!=current.Mission||before.Phase!=current.Phase||
            before.Identity!=current.Identity||before.Started!=current.Started||before.Instance!=current.Instance)return false;
        var options=new System.Text.Json.JsonSerializerOptions{IncludeFields=true};
        string Snapshot(MissionProgress.SavedState s)=>System.Text.Json.JsonSerializer.Serialize(new{s.Waypoints,s.Exits,s.SiteDoors,s.OpenFloorSites,s.OpenFloorCurrentSiteIndex,s.ElevatorWitnesses,NpcEntry=MissionNpcInteriorRide.ForSite(s)?.Raw,Objects=s.Objects.Select(o=>new{o.Id,o.Goid,o.Spot,o.OpenFloor})},options);
        return Snapshot(before)==Snapshot(current);
    }
    public static void RequireOverlay(Ride selected,ViewCreation view)
    {
        if(selected.Witness==null||view.RelatedStaticObjectId!=selected.Key||!new ushort[]{417,419}.Contains(view.Goid)||!view.Attributes.TryGetValue("Position",out var p)||p.Length!=24||
            !Enumerable.Range(0,3).All(i=>BitConverter.ToDouble(p,i*8)==selected.Pose[i])||!view.Attributes.TryGetValue("Orientation",out var q)||
            !q.SequenceEqual(Convert.FromHexString(selected.Quaternion!)))throw new InvalidDataException("Requested measured door overlay differs");
    }
    public sealed record DoorObservation(ViewCreation View,long OwnLife,int BirthSection,uint State);
    public static DoorObservation CurrentOverlay(OwnViewLifeSnapshot stream,Ride selected)
    {
        if(!stream.Current||!stream.OwnViewReady||stream.OwnBirthSection<0||stream.Events.Any(e=>e.Life!=stream.Life))throw new InvalidDataException("No current own life for measured door");
        var live=new Dictionary<ushort,(ViewCreation View,string Raw,int Birth,bool Known)>();var retired=new HashSet<(ushort,byte)>();bool own=false;
        foreach(var e in stream.Events)
        {
            if(e.Section<stream.OwnBirthSection)continue;var bytes=e.Bytes;var births=WireFormat.FindCreationEvents(bytes);int at=0;
            if(e.Section==stream.OwnBirthSection){var self=births.LastOrDefault(c=>c.Creation.ViewId==2&&c.Creation.Goid==12)??throw new InvalidDataException("Actual own2 birth absent");at=self.Offset+self.Length;own=true;}
            if(!own)throw new InvalidDataException("Door cannot precede current own2");
            while(at<bytes.Length)
            {
                var birth=births.FirstOrDefault(c=>c.Offset==at);
                if(birth!=null)
                {
                    var v=birth.Creation;var raw=Convert.ToHexString(bytes.AsSpan(at,birth.Length));
                    if(!live.TryGetValue(v.ViewId,out var prior)||prior.Raw!=raw)
                    {if(prior.View!=null)retired.Add((prior.View.ViewId,prior.View.SpawnId));live[v.ViewId]=(v,raw,e.Section,!retired.Contains((v.ViewId,v.SpawnId)));}
                    at+=birth.Length;continue;
                }
                ushort? kind=at+2<=bytes.Length&&live.TryGetValue(BitConverter.ToUInt16(bytes,at),out var addressed)?addressed.View.Goid:
                    at+2<=bytes.Length&&BitConverter.ToUInt16(bytes,at)==2?(ushort)12:null;
                if(!MervFollowupViewFrames.TryRead(bytes,at,kind,out var frame))throw new InvalidDataException("Unknown/truncated current measured-door life");
                if(frame.Type==MervFollowupViewFrames.Kind.Delete){if(live.Remove(frame.View,out var old))retired.Add((old.View.ViewId,old.View.SpawnId));}
                else if(live.TryGetValue(frame.View,out var changed)&&frame.PositionOffset>=0&&changed.View.Goid!=599)live[frame.View]=(changed.View,changed.Raw,changed.Birth,false);
                at+=frame.Length;
            }
        }
        var matches=live.Values.Where(v=>v.View.RelatedStaticObjectId==selected.Key).ToArray();
        if(!own||matches.Length!=1||!matches[0].Known)throw new InvalidDataException("Missing/ambiguous/deleted/reused current measured door key");
        var chosen=matches[0];RequireOverlay(selected,chosen.View);if(!stream.Current)throw new InvalidDataException("Door own life changed");
        var open=NpcInteriorComputerState.CurrentDoor(stream,new MissionNpcInteriorProof.Door(selected.Key,chosen.View.Goid,selected.Pose,selected.Quaternion!,null,""));
        return new(open.View,open.Capability.Life,open.Capability.Birth,open.State);
    }
    public static void RequireRoot(Ride selected,bool enter,double[] actual,ushort sector,ushort region,double floorY,IEnumerable<double[]> freeRoots)
    {
        var w=selected.Witness??throw new InvalidDataException("Measured root requires selected witness");
        uint key=enter?w.FloorKey:w.LobbyKey;ushort expected=enter?w.Region:w.LobbyRegion;
        if(!Point(actual)||sector!=(key>>20)||region!=expected||!double.IsFinite(floorY)||Math.Abs(actual[1]-(floorY+95))>1e-7||
            enter&&freeRoots.Any(r=>!Same(r,actual)))throw new InvalidDataException("Actual measured arrival root/NAV/current free witness differs");
    }
}
