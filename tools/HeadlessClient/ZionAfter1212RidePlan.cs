using hds;
namespace HeadlessClient;

// QA-only projection of the current persisted door; never repairs a checkpoint or searches for another site.
public sealed record ZionAfter1212RidePlan(ushort World,uint Key,double[] Pose,string? Quaternion,MissionElevatorWitness? Witness)
{
    public MissionNpcInteriorProof.Plan? Npc {get;init;}
    public bool Measured => Witness!=null||Npc!=null;
    public static ZionAfter1212RidePlan Select(MissionProgress.SavedState state,bool enter,IEnumerable<string> legacyRows,IReadOnlyList<ushort>? objectOrder=null)
    {
        void Require(bool value,string reason){if(!value)throw new InvalidDataException(reason);}
        bool Triple(double[]? p)=>p?.Length==3&&p.All(double.IsFinite);
        bool Quat(string? q){try{var b=q==null?Array.Empty<byte>():Convert.FromHexString(q);return b.Length==16&&Enumerable.Range(0,4).All(i=>float.IsFinite(BitConverter.ToSingle(b,i*4)));}catch(FormatException){return false;}}
        Require(state.Version==1||state.Version==2,"Only supported saved checkpoint versions");
        Require(state.Waypoints.Length==1&&state.Exits.Length==1,"One current saved floor/lobby pair required");
        var door=state.Waypoints.Single();var floor=new[]{door.x,door.y,door.z};var lobby=state.Exits.Single();
        Require(Triple(floor)&&Triple(lobby),"Finite current floor/lobby triples required");
        byte[] keyBytes=Convert.FromHexString(door.doorHex);Require(keyBytes.Length==4,"Exact saved floor key required");
        uint floorKey=BitConverter.ToUInt32(keyBytes);
        int index=state.Version==2?state.OpenFloorCurrentSiteIndex??-1:state.SiteDoors.Length-1;
        Require(index>=0&&index<state.SiteDoors.Length,"Current raw site index required");
        var raw=state.SiteDoors[index];Require(raw!=null&&raw.metrId==1&&raw.mxoStaticId==floorKey&&raw.type!=null&&raw.type.Length>=2&&new ushort[]{417,419}.Contains(BitConverter.ToUInt16(raw.type)),"Current raw site key must match waypoint");
        Require(state.Objects.All(o=>o.Spot!=null&&o.Spot.metrId==raw.metrId&&(o.OpenFloor==null||o.OpenFloor.World==raw.metrId)),"Current raw objects and OpenFloor witnesses must retain selected world");
        var npc=MissionNpcInteriorRide.ForSite(state);
        if(npc!=null)
        {
            var target=enter?npc.Lobby:npc.Floor;
            return new(raw.metrId,target.Key,target.Position,target.Quaternion,null){Npc=npc};
        }
        MissionOpenFloorSite? site=null;
        if(state.Version==2)
        {
            Require(state.OpenFloorSites!=null&&index<state.OpenFloorSites.Length,"Current fixed site history required");
            site=state.OpenFloorSites![index];
            Require(site.DoorKey==raw.mxoStaticId&&site.District==raw.metrId&&site.DoorX==raw.pos_x&&site.DoorY==raw.pos_y&&site.DoorZ==raw.pos_z,"Raw selected door must retain saved site identity");
        }
        var matching=(state.ElevatorWitnesses??Array.Empty<MissionElevatorWitness>()).Where(w=>site!=null&&w.SiteFingerprint==site.Fingerprint).ToArray();
        Require(!(state.ElevatorWitnesses??Array.Empty<MissionElevatorWitness>()).Any(w=>w.FloorKey==floorKey&&(site==null||w.SiteFingerprint!=site.Fingerprint)),"Current-key witness has wrong saved site fingerprint");
        Require(matching.Length<=1,"Duplicate current elevator witnesses refused");
        if(matching.Length==1)
        {
            var w=matching[0];Require(w.Version==1&&w.FloorKey==floorKey&&w.LobbyKey!=0,"Current witness must bind exact floor/lobby keys");
            Require(Triple(w.Floor)&&Triple(w.Lobby)&&Quat(w.FloorQuaternion)&&Quat(w.LobbyQuaternion),"Measured witness pose/quaternion malformed");
            Require(floor.SequenceEqual(w.Floor)&&lobby.SequenceEqual(w.Lobby),"Current checkpoint must equal its measured witness");
            var options=new System.Text.Json.JsonSerializerOptions{IncludeFields=true};
            Require(w.Sources!=null,"Persisted source identities required");
            foreach(string source in w.Sources!)
            {
                StaticWorldObject? parsed;
                try{parsed=System.Text.Json.JsonSerializer.Deserialize<StaticWorldObject>(source,options);}
                catch(System.Text.Json.JsonException e){throw new InvalidDataException("Historical source JSON malformed",e);}
                Require(parsed!=null&&parsed.metrId==raw.metrId&&parsed.type!=null&&(parsed.type.Length==2||parsed.type.Length==4)&&parsed.quat!=null&&
                    new[]{parsed.pos_x,parsed.pos_y,parsed.pos_z,parsed.rot}.All(double.IsFinite),
                    "Historical witness source must satisfy the existing ValidSpot shape and selected world");
            }
            // Runtime MissionOpenFloorAdmission compares current sources only for the currently adopted phase.
            // Historical native profile validation remains the server's authoritative contract; do not replace it with current NPC-only objects.
            if(state.OpenFloorPhase==state.Phase)
            {
                if(state.Objects.Count>0)Require(objectOrder!=null&&objectOrder.Count==state.Objects.Count&&
                    objectOrder.Distinct().Count()==objectOrder.Count&&objectOrder.OrderBy(x=>x).SequenceEqual(state.Objects.Select(o=>o.Id).OrderBy(x=>x)),"Exact authored current phase object order required");
                var currentSources=(objectOrder??Array.Empty<ushort>()).Select(id=>System.Text.Json.JsonSerializer.Serialize(state.Objects.Single(o=>o.Id==id).Spot,options));
                Require(currentSources.SequenceEqual(w.Sources),"Adopted current phase raw sources must equal the persisted authored source order");
            }
            return new(raw.metrId,enter?w.LobbyKey:w.FloorKey,enter?w.Lobby:w.Floor,enter?w.LobbyQuaternion:w.FloorQuaternion,w);
        }
        // Only an unchanged raw tuple can use the original CSV path. A missing measured witness is not repaired.
        Require(floor.SequenceEqual(new[]{raw.pos_x,raw.pos_y,raw.pos_z}),"Unwitnessed corrected floor cannot fall back to CSV");
        uint lobbyKey=MissionRawDoorLookup.DoorAt(legacyRows,lobby);
        return new(raw.metrId,enter?lobbyKey:floorKey,enter?lobby:floor,null,null);
    }

    public void RequireOverlay(ViewCreation view)
    {
        if(!Measured||view.RelatedStaticObjectId!=Key||!new ushort[]{417,419}.Contains(view.Goid)||
            !view.Attributes.TryGetValue("Position",out var p)||p.Length!=24||!Enumerable.Range(0,3).All(i=>BitConverter.ToDouble(p,i*8)==Pose[i])||
            !view.Attributes.TryGetValue("Orientation",out var q)||!q.SequenceEqual(Convert.FromHexString(Quaternion!)))
            throw new InvalidDataException("Measured current door03 must retain the exact requested related-static key and pose");
    }

    public static void SameEntry(MissionProgress.SavedState before,MissionProgress.SavedState after)
    {
        if(before.Version!=after.Version||before.Mission!=after.Mission||before.Phase!=after.Phase||before.Identity!=after.Identity||before.Started!=after.Started||before.Instance!=after.Instance)
            throw new InvalidDataException("Ride owner/mission/phase changed before ordinary door request");
        var options=new System.Text.Json.JsonSerializerOptions{IncludeFields=true};
        string Snapshot(MissionProgress.SavedState s)=>System.Text.Json.JsonSerializer.Serialize(new{s.SiteDoors,s.OpenFloorSites,s.OpenFloorCurrentSiteIndex,s.ElevatorWitnesses,NpcWitness=MissionNpcInteriorRide.ForSite(s)?.Raw,s.Waypoints,s.Exits,Objects=s.Objects.Select(o=>new{o.Id,o.Goid,o.Spot,o.OpenFloor})},options);
        if(Snapshot(before)!=Snapshot(after))throw new InvalidDataException("Saved ride identity changed before ordinary door request");
    }
}
